using System.Collections.Generic;
using RimWorld;
using PlayableRecreation.UI;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>우클릭·기즈모·Job 이 모두 거쳐가는 단일 진입점.</summary>
    public static class GameEntry
    {
        /// <summary>몰입 모드면 폰을 가구까지 보낸 뒤 열고, 아니면 즉시 연다.</summary>
        public static void Begin(MiniGameDef game, Thing board, Pawn pawn)
        {
            if (game == null || board == null) return;

            if (pawn != null && pawn.jobs != null && PRMod.Settings.immersionMode)
            {
                Job job = JobMaker.MakeJob(PRDefOf.PR_GoToGame, board);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return;
            }

            OpenWindow(game, board, pawn);
        }

        /// <summary>가구에 두던 판이 남아 있으면 이어 두고, 아니면 새 판을 시작한다.</summary>
        public static void OpenWindow(MiniGameDef game, Thing board, Pawn pawn)
        {
            GameSession session = SessionFor(board);

            if (session != null && game.Accepts(session.game)) Resume(session);
            else StartNew(game, board, pawn);
        }

        /// <summary>난이도를 정해 새 판을 연다. 단계가 하나뿐이거나 폰 연동이면 선택 창을 건너뛴다.</summary>
        public static void StartNew(MiniGameDef game, Thing board, Pawn pawn)
        {
            if (game.difficultyCount <= 1)
            {
                Launch(game, board, pawn, 0, false);
                return;
            }

            if (pawn != null && PRMod.Settings.linkToPawnSkill)
            {
                Launch(game, board, pawn, TierForPawn(game, pawn), false);
                return;
            }

            Find.WindowStack.Add(new Dialog_Difficulty(game, board, pawn));
        }

        /// <summary>
        /// 난이도가 정해진 뒤의 마지막 관문. 보통은 고른 그 게임을 열지만,
        /// 추첨함이라면 여기서 통을 흔든다 - 난이도는 이미 사람이 골랐고, 무엇을 할지만 남았다.
        /// </summary>
        public static void Launch(MiniGameDef game, Thing board, Pawn pawn, int tier, bool practice)
        {
            if (game == null) return;

            MiniGameDef chosen = game;

            if (game.randomPick)
            {
                chosen = PickFor(game);

                if (chosen == null)
                {
                    Messages.Message("PR.Random.Empty".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Messages.Message("PR.Random.Picked".Translate(chosen.LabelCap),
                    board, MessageTypeDefOf.NeutralEvent, false);
            }

            Find.WindowStack.Add(new Dialog_MiniGame(chosen, board, pawn, tier, practice));
        }

        /// <summary>
        /// 뽑기 통. 지금 로드되어 있는 승부 게임 전부다 - 설치한 모드에 따라 통의 내용이 달라지고,
        /// 꺼 둔 항목은 들어오지 않는다. 목록을 어디에 적어 두지 않는 이유가 그것이다.
        /// </summary>
        private static MiniGameDef PickFor(MiniGameDef picker)
        {
            List<MiniGameDef> pool = new List<MiniGameDef>();

            List<MiniGameDef> all = DefDatabase<MiniGameDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                MiniGameDef game = all[i];

                if (game == picker || game.randomPick) continue;
                if (!game.hasMatch) continue;              // 승부가 없으면 고른 난이도가 뜻을 잃는다
                if (!PRMod.Settings.IsEnabled(game)) continue;

                pool.Add(game);
            }

            return pool.Count > 0 ? pool.RandomElement() : null;
        }

        public static void Resume(GameSession session)
        {
            if (session == null || session.game == null) return;

            Find.WindowStack.Add(new Dialog_MiniGame(session));
        }

        public static GameSession SessionFor(Thing board)
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            return component != null ? component.SessionFor(board) : null;
        }

        /// <summary>연동 스킬 + 열정 보정으로 난이도를 정한다. 20레벨을 단계 수로 나눈다.</summary>
        public static int TierForPawn(MiniGameDef game, Pawn pawn)
        {
            int middle = game.difficultyCount / 2;
            if (game.linkedSkill == null || pawn == null || pawn.skills == null) return game.ClampTier(middle);

            SkillRecord record = pawn.skills.GetSkill(game.linkedSkill);
            if (record == null) return game.ClampTier(middle);

            int skill = record.Level;
            if (record.passion == Passion.Minor) skill += 1;
            else if (record.passion == Passion.Major) skill += 2;

            // 0~20(+2) 을 단계 수로 균등 분할한다.
            int tier = skill * game.difficultyCount / 21;
            return game.ClampTier(tier);
        }
    }
}
