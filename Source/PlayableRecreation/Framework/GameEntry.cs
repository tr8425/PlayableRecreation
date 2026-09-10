using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using PlayableRecreation.UI;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>우클릭·기즈모·Job 이 모두 거쳐가는 단일 진입점.</summary>
    public static class GameEntry
    {
        /// <summary>
        /// 모든 진입이 지나가는 문. 우클릭·기즈모·재개가 전부 여기로 모인다.
        ///
        /// 폰이 없는 진입(가구 기즈모)이 여기서 걸린다. 예전에는 그대로 창을 열어서
        /// 몰입 모드를 켜 둬도 아무도 걷지 않았다 — 이제 앉을 사람부터 고른다.
        /// </summary>
        public static void Begin(MiniGameDef game, Thing board, Pawn pawn)
        {
            if (game == null || board == null) return;

            // 앉을 사람이 없다. 몰입 모드가 원하는 그림이 아니므로 먼저 사람을 고른다.
            if (pawn == null && PRMod.Settings.immersionMode && AskWhoSits(game, board)) return;

            if (pawn != null && pawn.jobs != null && PRMod.Settings.immersionMode)
            {
                Job job = JobMaker.MakeJob(PRDefOf.PR_GoToGame, board);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return;
            }

            OpenWindow(game, board, pawn);
        }

        /// <summary>
        /// 가구만 클릭했을 때 앉을 사람을 고른다. 고르면 걸어가는 것까지 다시 Begin 이 맡는다.
        /// 앉힐 사람이 아무도 없으면 false 를 주고 예전처럼 그냥 연다 — 막지는 않는다.
        /// </summary>
        private static bool AskWhoSits(MiniGameDef game, Thing board)
        {
            if (board.Map == null) return false;

            List<Pawn> pawns = board.Map.mapPawns.FreeColonistsSpawned;
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate.Downed || candidate.InMentalState || candidate.jobs == null) continue;
                if (!candidate.CanReach(board, PathEndMode.Touch, Danger.Some)) continue;

                Pawn bound = candidate;
                options.Add(new FloatMenuOption(
                    "PR.Together.Sit".Translate(bound.LabelShortCap),
                    delegate { Begin(game, board, bound); }));
            }

            if (options.Count == 0) return false;

            Find.WindowStack.Add(new FloatMenu(options));
            return true;
        }

        /// <summary>가구에 두던 판이 남아 있으면 이어 두고, 아니면 새 판을 시작한다.</summary>
        public static void OpenWindow(MiniGameDef game, Thing board, Pawn pawn)
        {
            GameSession session = SessionFor(board);

            if (session == null || !game.Accepts(session.game))
            {
                StartNew(game, board, pawn);
                return;
            }

            if (ResumeTogether(session)) return;

            Resume(session);
        }

        /// <summary>
        /// 둘이 두던 판을 이어 두는 유일한 문. 이어 둘 판이 아니면 false 를 돌려주고
        /// 부르는 쪽이 평소대로 진행한다.
        ///
        /// <b>상대가 못 오면 아무것도 열지 않는다.</b> 세션은 상대를 그대로 들고 있어서
        /// 혼자 여는 길이 없다 — 열어 봐야 §7.1 이 곧바로 닫고 "자리를 떴다" 가 잇따른다.
        /// 판은 가구 위에 남으므로 나중에 다시 부르면 그만이다.
        /// </summary>
        private static bool ResumeTogether(GameSession session)
        {
            if (!Together.Enabled) return false;
            if (session.opponentPawn == null || session.seatedPawn == null) return false;

            Thing board = session.board;

            Together.Refusal refusal =
                Together.Judge(session.opponentPawn, session.seatedPawn, board, session.game);

            if (refusal == Together.Refusal.None)
            {
                TogetherMatch.Arrange(session.game, board, session.seatedPawn, session.opponentPawn,
                    session.tier, session.practice, session);
                return true;
            }

            Messages.Message(
                "PR.Together.Cannot".Translate(
                    session.opponentPawn.LabelShortCap, Together.ReasonText(refusal)),
                board, MessageTypeDefOf.RejectInput, false);

            return true;
        }

        /// <summary>난이도를 정해 새 판을 연다. 단계가 하나뿐이거나 폰 연동이면 선택 창을 건너뛴다.</summary>
        public static void StartNew(MiniGameDef game, Thing board, Pawn pawn)
        {
            // 2칸이면 난이도보다 먼저 상대를 고른다. 제안 난이도가 상대의 실력에서 나오기 때문이다.
            if (pawn != null && Together.AppliesTo(game))
            {
                AskWhoPlays(game, board, pawn);
                return;
            }

            StartNewWith(game, board, pawn, null);
        }

        /// <summary>
        /// 상대 고르기. 자격 있는 사람이 먼저 오고, 없는 사람은 이유와 함께 회색으로 뒤에 온다.
        /// 회색을 보여 주는 이유는 "왜 저 사람은 안 되는가" 가 플레이어의 질문이기 때문이다.
        /// </summary>
        private static void AskWhoPlays(MiniGameDef game, Thing board, Pawn initiator)
        {
            List<Pawn> candidates = Together.Candidates(initiator, board, game);

            List<FloatMenuOption> able = new List<FloatMenuOption>();
            List<FloatMenuOption> unable = new List<FloatMenuOption>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn bound = candidates[i];
                Together.Refusal refusal = Together.Judge(bound, initiator, board, game);

                if (refusal == Together.Refusal.None)
                {
                    // 배경이 이 오락을 직접 말하는 사람은 그렇다고 적어 준다. 그 사람을 고르면
                    // 제안 난이도가 뛰는데, 왜 뛰는지가 목록에 안 보이면 그냥 이상한 일이 된다.
                    BackstoryAffinity affinity = AffinityFor(game, bound);

                    string label;
                    if (bound == TogetherInvite.WaitingAt(board))
                        label = "PR.Together.Waiting".Translate(bound.LabelShortCap);
                    else if (affinity != null)
                        label = "PR.Together.Affinity".Translate(
                            bound.LabelShortCap, affinity.backstory.TitleCapFor(bound.gender));
                    else
                        label = bound.LabelShortCap;

                    able.Add(new FloatMenuOption(
                        label,
                        delegate { StartNewWith(game, board, initiator, bound); }));
                }
                else
                {
                    unable.Add(new FloatMenuOption(
                        "PR.Together.Cannot".Translate(bound.LabelShortCap, Together.ReasonText(refusal)),
                        null));
                }
            }

            // 혼자 두는 길은 언제나 남겨 둔다. 2칸을 켰다고 혼자 두는 것을 막지는 않는다.
            able.Add(new FloatMenuOption("PR.Together.Alone".Translate(),
                delegate { StartNewWith(game, board, initiator, null); }));

            able.AddRange(unable);
            Find.WindowStack.Add(new FloatMenu(able));
        }

        private static void StartNewWith(MiniGameDef game, Thing board, Pawn pawn, Pawn opponent)
        {
            if (game.difficultyCount <= 1)
            {
                Commit(game, board, pawn, opponent, 0, false);
                return;
            }

            Pawn measured = Measured(pawn, opponent);

            if (measured != null && PRMod.Settings.linkToPawnSkill)
            {
                Commit(game, board, pawn, opponent, TierForPawn(game, measured), false);
                return;
            }

            Find.WindowStack.Add(new Dialog_Difficulty(game, board, pawn, opponent));
        }

        /// <summary>
        /// 난이도까지 정해진 뒤의 갈림길. 상대가 있으면 창을 바로 열지 않고 둘을 부른다 —
        /// 창은 둘 다 판 앞에 닿았을 때 열린다(§4-4).
        /// </summary>
        public static void Commit(MiniGameDef game, Thing board, Pawn pawn, Pawn opponent,
            int tier, bool practice)
        {
            if (opponent != null && Together.Enabled)
            {
                TogetherMatch.Arrange(game, board, pawn, opponent, tier, practice, null);
                return;
            }

            Launch(game, board, pawn, opponent, tier, practice);
        }

        public static void Launch(MiniGameDef game, Thing board, Pawn pawn, int tier, bool practice)
        {
            Launch(game, board, pawn, null, tier, practice);
        }

        /// <summary>
        /// 난이도가 정해진 뒤의 마지막 관문. 보통은 고른 그 게임을 열지만,
        /// 추첨함이라면 여기서 통을 흔든다 - 난이도는 이미 사람이 골랐고, 무엇을 할지만 남았다.
        /// </summary>
        public static void Launch(MiniGameDef game, Thing board, Pawn pawn, Pawn opponent, int tier, bool practice)
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

            Find.WindowStack.Add(new Dialog_MiniGame(chosen, board, pawn, opponent, tier, practice));
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

        /// <summary>
        /// 이어 두기의 문. 몰입 모드면 앉았던 사람이 다시 걸어간 뒤에 열린다 —
        /// 예전에는 재개만 걷기를 건너뛰어서, 몰입 모드를 켜 둬도 판이 그냥 열렸다.
        /// </summary>
        public static void OpenSession(GameSession session)
        {
            if (session == null || session.game == null || session.board == null)
            {
                Resume(session);
                return;
            }

            // 둘이 두던 판은 **언제나** 여기를 지난다. 예전에는 기즈모와 플로트 메뉴가
            // 이 검사를 건너뛰고 곧장 열어서, 주도한 쪽이 이미 판 옆에 서 있으면 상대를
            // 부르지도 않고 창이 열렸다가 §7.1 이 반 초 만에 닫았다 (QA-02).
            if (ResumeTogether(session)) return;

            Pawn seated = session.seatedPawn;

            if (PRMod.Settings.immersionMode && seated != null && seated.jobs != null
                && seated.Spawned && !seated.Dead && !seated.Downed && !seated.InMentalState
                && seated.Map == session.board.Map
                && !seated.Position.AdjacentTo8WayOrInside(session.board)
                && seated.CanReach(session.board, PathEndMode.Touch, Danger.Some))
            {
                Job job = JobMaker.MakeJob(PRDefOf.PR_GoToGame, session.board);
                seated.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return;
            }

            Resume(session);
        }

        /// <summary>판을 실제로 연다. 준비 절차를 마친 뒤에만 부른다.</summary>
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

        /// <summary>
        /// 난이도를 재는 대상. 상대가 있으면 **맞은편에 앉은 사람**이다 (명세 D2) —
        /// 판이 어려운지 쉬운지는 내가 아니라 그 사람의 실력이 정하기 때문이다.
        /// </summary>
        public static Pawn Measured(Pawn pawn, Pawn opponent)
        {
            return opponent != null ? opponent : pawn;
        }

        /// <summary>
        /// 이 게임에서 이 사람이 가진 실력. 연동 스킬 + 열정 + 배경 친화다.
        /// 잴 수 없으면 -1 — 연동 스킬이 없는 게임(추첨함 따위)이 그렇다.
        /// </summary>
        public static int EffectiveSkill(MiniGameDef game, Pawn pawn)
        {
            if (game == null || game.linkedSkill == null || pawn == null || pawn.skills == null) return -1;

            SkillRecord record = pawn.skills.GetSkill(game.linkedSkill);
            if (record == null) return -1;

            int skill = record.Level;
            if (record.passion == Passion.Minor) skill += 1;
            else if (record.passion == Passion.Major) skill += 2;

            // 배경 이야기가 이 오락을 직접 말하면 그만큼 더 얹는다. **이 게임에서만이다** -
            // 체스 마스터는 체스가 센 것이지 지능이 높은 것이 아니다.
            BackstoryAffinity affinity = AffinityFor(game, pawn);
            if (affinity != null) skill += affinity.levels;

            return skill;
        }

        /// <summary>연동 스킬 + 열정 보정으로 난이도를 정한다. 20레벨을 단계 수로 나눈다.</summary>
        public static int TierForPawn(MiniGameDef game, Pawn pawn)
        {
            if (game == null) return 0;

            int skill = EffectiveSkill(game, pawn);
            if (skill < 0) return game.ClampTier(game.difficultyCount / 2);

            // 0~20(+2) 을 단계 수로 균등 분할한다.
            return game.ClampTier(skill * game.difficultyCount / 21);
        }

        /// <summary>
        /// 그 칸에 앉으려면 실력이 얼마나 있어야 하는가. <see cref="TierForPawn"/> 의 역이다 —
        /// 저쪽이 <c>floor(skill * dc / 21)</c> 이므로 이쪽은 올림이다.
        /// </summary>
        public static int SkillNeededFor(MiniGameDef game, int tier)
        {
            if (game == null || tier <= 0) return 0;

            int count = Mathf.Max(1, game.difficultyCount);
            return (tier * 21 + count - 1) / count;
        }

        /// <summary>
        /// 이 사람이 앉을 수 있는 가장 높은 칸. 잠금이 꺼져 있거나 잴 수 없으면 상한이 없다.
        ///
        /// 실력이 곧 상한이다 — 아래로는 얼마든지 내려갈 수 있고 위로만 못 간다.
        /// 전적은 원래 칸별로 쌓이므로 장부가 꼬이지는 않는다. 다만 낮은 실력의 폰으로는
        /// 상위 칸을 못 채우는데, 그게 이 설정을 켠다는 뜻이다.
        /// </summary>
        public static int CapForPawn(MiniGameDef game, Pawn pawn)
        {
            if (game == null) return 0;

            int top = Mathf.Max(0, game.difficultyCount - 1);
            if (!PRMod.Settings.lockTiersAboveSkill) return top;

            int skill = EffectiveSkill(game, pawn);
            if (skill < 0) return top;

            return game.ClampTier(skill * game.difficultyCount / 21);
        }

        /// <summary>
        /// 이 사람의 배경 중 이 오락을 직접 말하는 것. 없으면 null.
        /// 둘 이상 걸리면 **가장 큰 것 하나만** 쓴다 — 어린 시절과 어른 시절이 같은 놀이를
        /// 말한다고 해서 두 배로 세지는 것은 아니다.
        /// </summary>
        public static BackstoryAffinity AffinityFor(MiniGameDef game, Pawn pawn)
        {
            if (game == null || game.backstoryAffinities == null) return null;
            if (pawn == null || pawn.story == null) return null;

            List<BackstoryDef> mine = pawn.story.AllBackstories;
            if (mine == null) return null;

            BackstoryAffinity best = null;

            for (int i = 0; i < game.backstoryAffinities.Count; i++)
            {
                BackstoryAffinity affinity = game.backstoryAffinities[i];
                if (affinity == null || affinity.backstory == null) continue;
                if (!mine.Contains(affinity.backstory)) continue;

                if (best == null || affinity.levels > best.levels) best = affinity;
            }

            return best;
        }
    }
}
