using System.Collections.Generic;
using System.Text;
using PlayableRecreation.UI;
using RimWorld;
using Verse;

namespace PlayableRecreation
{
    public class CompProperties_MiniGame : CompProperties
    {
        /// <summary>이 가구에서 할 수 있는 게임.</summary>
        public MiniGameDef game;

        public CompProperties_MiniGame()
        {
            compClass = typeof(CompMiniGame);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (game == null) yield return parentDef.defName + ": CompProperties_MiniGame 에 game 이 없습니다";
        }
    }

    /// <summary>
    /// 모드의 유일한 게임 접점. 오락 가구에 우클릭 메뉴와 기즈모를 붙인다.
    /// FloatMenuMakerMap 을 Harmony 로 패치하지 않으므로 다른 모드와 충돌하지 않는다.
    /// </summary>
    public class CompMiniGame : ThingComp
    {
        public MiniGameDef Game
        {
            get { return ((CompProperties_MiniGame)props).game; }
        }

        private bool Available
        {
            get { return Game != null && PRMod.Settings.IsEnabled(Game); }
        }

        /// <summary>가구가 너무 부서져 있으면 쓸 수 없다.</summary>
        private bool Usable
        {
            get
            {
                if (parent == null || !parent.Spawned || parent.Destroyed) return false;
                if (parent.def.useHitPoints && parent.MaxHitPoints > 0)
                    return parent.HitPoints > parent.MaxHitPoints / 10;
                return true;
            }
        }

        private GameSession Session
        {
            get
            {
                GameSession session = GameEntry.SessionFor(parent);
                return session != null && Game != null && Game.Accepts(session.game) ? session : null;
            }
        }

        /// <summary>이어 하기 문구. 추첨함에 남은 판은 무엇이 걸렸던 것인지까지 말해 준다.</summary>
        private string ResumeLabel(GameSession session)
        {
            if (session.game != Game)
                return "PR.Play.ResumeNamed".Translate(
                    session.game.LabelCap, session.rounds, Game.TierLabel(session.tier)).ToString();

            return "PR.Play.Resume".Translate(session.rounds, Game.TierLabel(session.tier)).ToString();
        }

        // 식민자 선택 → 가구 우클릭 — 다른 상호작용 가구와 같은 조작 관습
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!Available) yield break;

            if (!Usable)
            {
                yield return new FloatMenuOption("PR.Play.Broken".Translate(), null);
                yield break;
            }

            GameSession session = Session;

            if (session != null)
            {
                yield return new FloatMenuOption(
                    ResumeLabel(session),
                    delegate { GameEntry.Resume(session); });

                yield return new FloatMenuOption(
                    "PR.Play.Discard".Translate(),
                    delegate { ConfirmDiscard(selPawn); });

                yield break;
            }

            yield return new FloatMenuOption(
                "PR.Play.Label".Translate(Game.LabelCap),
                delegate { GameEntry.Begin(Game, parent, selPawn); });
        }

        // 가구만 선택했을 때의 기즈모
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Available || !Usable) yield break;

            GameSession session = Session;

            yield return new Command_Action
            {
                defaultLabel = session != null
                    ? ResumeLabel(session)
                    : "PR.Play.Label".Translate(Game.LabelCap).ToString(),
                defaultDesc = Game.description,
                icon = parent.def.uiIcon,
                action = delegate
                {
                    if (session != null) GameEntry.Resume(session);
                    else GameEntry.Begin(Game, parent, null);
                }
            };

            // 승부가 아닌 항목에는 남길 전적이 없다. 추첨함도 마찬가지다 -
            // 전적은 실제로 한 게임 쪽에 쌓이므로 여기서 펼쳐 봐야 늘 비어 있다.
            if (!Game.hasMatch || Game.randomPick) yield break;

            yield return new Command_Action
            {
                defaultLabel = "PR.Btn.Records".Translate(),
                defaultDesc = "PR.Btn.Records.Tip".Translate(),
                icon = parent.def.uiIcon,
                action = delegate { Find.WindowStack.Add(new Dialog_Leaderboard(Game)); }
            };
        }

        public override string CompInspectStringExtra()
        {
            if (!Available) return null;

            StringBuilder text = new StringBuilder();

            GameSession session = Session;
            if (session != null)
                text.Append("PR.Inspect.Saved".Translate(session.rounds, Game.TierLabel(session.tier)));

            int tier = Mastery.CurrentTier(Game);
            if (tier > 0)
            {
                if (text.Length > 0) text.AppendLine();
                text.Append("PR.Inspect.Mastery".Translate(
                    tier, Game.difficultyCount, Mastery.BonusPercentText(tier)));
            }

            return text.ToString();
        }

        private void ConfirmDiscard(Pawn selPawn)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "PR.Confirm.Discard".Translate(),
                delegate
                {
                    GameComponent_Recreation component = GameComponent_Recreation.Current;
                    if (component != null) component.Remove(Session);

                    GameEntry.StartNew(Game, parent, selPawn);
                },
                false));
        }
    }
}
