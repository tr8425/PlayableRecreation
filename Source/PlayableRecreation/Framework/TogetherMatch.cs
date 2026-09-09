using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 둘이 함께 두는 판의 진행 상태. **세이브에 남지 않는다** — 일부러 그렇게 두었다.
    ///
    /// 창이 세이브에 남지 않으므로 불러온 판에는 붙잡을 이유도 없다. 여기가 비어 있으면
    /// 두 JobDriver 의 <c>Hold</c> 가 스스로 끝나므로, 저장·불러오기 도중에 폰이 영원히
    /// 서 있는 그림이 생기지 않는다. 자기 치유가 공짜로 따라오는 셈이다.
    /// </summary>
    public static class TogetherMatch
    {
        /// <summary>둘 다 닿기를 기다리는 판.</summary>
        private sealed class Pending
        {
            public MiniGameDef game;
            public Thing board;
            public Pawn seated;
            public Pawn opponent;
            public int tier;
            public bool practice;

            /// <summary>이어 두는 판이면 그 세션. 새 판이면 null.</summary>
            public GameSession session;

            public int startedTick;
        }

        private sealed class Live
        {
            public Thing board;
            public Pawn seated;
            public Pawn opponent;
        }

        /// <summary>상대를 기다려 주는 한도. 넘으면 판을 열지 않고 한 줄로 알린다.</summary>
        private const int WaitLimitTicks = 2500;

        private static readonly List<Pending> pending = new List<Pending>();
        private static readonly List<Live> live = new List<Live>();

        // ---------- 부르기 ----------

        /// <summary>
        /// 두 사람을 판 앞으로 부른다. 창은 아직 열지 않는다 — 둘 다 닿아야 연다.
        /// 빈 의자를 먼저 보여 줄 이유가 없다(§4-4).
        /// </summary>
        public static void Arrange(MiniGameDef game, Thing board, Pawn seated, Pawn opponent,
            int tier, bool practice, GameSession session)
        {
            if (game == null || board == null || seated == null || opponent == null) return;

            Cancel(board);

            // 상대가 못 오면 한 사람만 서 있는 그림을 만들지 않는다 — 그냥 혼자 두는 판으로 연다.
            if (!Send(opponent, PRDefOf.PR_JoinGame, board, seated))
            {
                Messages.Message("PR.Together.CannotCome".Translate(opponent.LabelShortCap),
                    board, MessageTypeDefOf.RejectInput, false);

                OpenNow(game, board, seated, null, tier, practice, session);
                return;
            }

            pending.Add(new Pending
            {
                game = game,
                board = board,
                seated = seated,
                opponent = opponent,
                tier = tier,
                practice = practice,
                session = session,
                startedTick = Find.TickManager.TicksGame
            });

            // 주도한 쪽에게도 다시 준다. 이미 판 앞이면 걷는 토일이 그 자리에서 끝나므로
            // 손해가 없고, 고르는 사이에 자리를 떴어도 되돌아온다.
            if (!Send(seated, PRDefOf.PR_GoToGame, board, null))
            {
                Cancel(board);
                if (opponent.jobs != null) opponent.jobs.EndCurrentJob(JobCondition.InterruptForced);

                Messages.Message("PR.Together.CannotCome".Translate(seated.LabelShortCap),
                    board, MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>이 폰이 이 가구에서 무언가를 기다리거나 두고 있는가. Hold 의 유일한 근거다.</summary>
        public static bool ShouldHold(Thing board, Pawn pawn)
        {
            if (board == null || pawn == null) return false;

            for (int i = 0; i < live.Count; i++)
            {
                Live entry = live[i];
                if (entry.board == board && (entry.seated == pawn || entry.opponent == pawn)) return true;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                Pending entry = pending[i];
                if (entry.board == board && (entry.seated == pawn || entry.opponent == pawn)) return true;
            }

            return false;
        }

        /// <summary>누군가 가구 앞에 닿았다. 둘 다 닿았으면 그 자리에서 창을 연다.</summary>
        public static void NotifyArrived(Thing board, Pawn pawn)
        {
            Pending entry = PendingFor(board);
            if (entry == null) return;
            if (entry.seated != pawn && entry.opponent != pawn) return;

            if (!Arrived(entry.seated, board) || !Arrived(entry.opponent, board)) return;

            pending.Remove(entry);
            OpenNow(entry.game, entry.board, entry.seated, entry.opponent,
                entry.tier, entry.practice, entry.session);
        }

        /// <summary>기다림이 길어지거나 한쪽이 못 오게 되면 접는다. Hold 가 자기 틱에서 부른다.</summary>
        public static void TickWatch(Thing board)
        {
            Pending entry = PendingFor(board);
            if (entry == null) return;

            bool lost = !Alive(entry.seated) || !Alive(entry.opponent)
                || entry.board == null || !entry.board.Spawned;

            bool timedOut = Find.TickManager.TicksGame - entry.startedTick > WaitLimitTicks;
            if (!lost && !timedOut) return;

            pending.Remove(entry);

            Pawn missing = Alive(entry.opponent) ? entry.seated : entry.opponent;
            if (missing != null)
                Messages.Message("PR.Together.NeverCame".Translate(missing.LabelShortCap),
                    board, MessageTypeDefOf.NeutralEvent, false);
        }

        // ---------- 창이 열린 뒤 ----------

        public static void OpenedWindow(Thing board, Pawn seated, Pawn opponent)
        {
            if (board == null || opponent == null) return;

            Release(board);
            live.Add(new Live { board = board, seated = seated, opponent = opponent });
        }

        public static void ClosedWindow(Thing board)
        {
            Release(board);
        }

        /// <summary>
        /// 자리를 뜬 사람. 창이 0.5초마다 묻는다 — 하나라도 뜨면 창이 스스로 닫히고
        /// 판은 가구에 남는다(§7.1). 기권이 아니라 중단이다.
        /// </summary>
        public static Pawn WhoLeft(Thing board)
        {
            for (int i = 0; i < live.Count; i++)
            {
                Live entry = live[i];
                if (entry.board != board) continue;

                if (!Seated(entry.seated, board)) return entry.seated;
                if (!Seated(entry.opponent, board)) return entry.opponent;
                return null;
            }

            return null;
        }

        /// <summary>
        /// 이 폰이 **다른** 가구의 판에 붙잡혀 있는가. 세션이 아직 저장되기 전에도
        /// 참인 값이라, 같은 사람을 두 판에 지목하는 것을 여기서 먼저 막는다.
        /// </summary>
        public static bool HeldElsewhere(Pawn pawn, Thing except)
        {
            if (pawn == null) return false;

            for (int i = 0; i < live.Count; i++)
            {
                Live entry = live[i];
                if (entry.board == except) continue;
                if (entry.seated == pawn || entry.opponent == pawn) return true;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                Pending entry = pending[i];
                if (entry.board == except) continue;
                if (entry.seated == pawn || entry.opponent == pawn) return true;
            }

            return false;
        }

        public static void Cancel(Thing board)
        {
            Pending entry = PendingFor(board);
            if (entry != null) pending.Remove(entry);
        }

        // ---------- 조각 ----------

        private static void OpenNow(MiniGameDef game, Thing board, Pawn seated, Pawn opponent,
            int tier, bool practice, GameSession session)
        {
            if (session != null) GameEntry.Resume(session);
            else GameEntry.Launch(game, board, seated, opponent, tier, practice);
        }

        private static bool Send(Pawn pawn, JobDef def, Thing board, Pawn partner)
        {
            if (pawn == null || pawn.jobs == null) return false;

            Job job = partner != null
                ? JobMaker.MakeJob(def, board, partner)
                : JobMaker.MakeJob(def, board);

            return pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static void Release(Thing board)
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].board == board) live.RemoveAt(i);
        }

        private static Pending PendingFor(Thing board)
        {
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].board == board) return pending[i];

            return null;
        }

        private static bool Alive(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Destroyed
                && !pawn.Downed && !pawn.InMentalState;
        }

        private static bool Arrived(Pawn pawn, Thing board)
        {
            return Alive(pawn) && pawn.Map == board.Map && pawn.Position.AdjacentTo8WayOrInside(board);
        }

        /// <summary>
        /// 아직 앉아 있는가. 우리 Job 을 들고 가구 옆에 있으면 앉아 있는 것이다 —
        /// 배고파 일어났거나 징집되면 Job 이 바뀌므로 여기서 바로 드러난다.
        /// </summary>
        private static bool Seated(Pawn pawn, Thing board)
        {
            if (!Alive(pawn) || pawn.Map != board.Map) return false;
            if (!pawn.Position.AdjacentTo8WayOrInside(board)) return false;

            JobDef current = pawn.CurJobDef;
            return current == PRDefOf.PR_GoToGame || current == PRDefOf.PR_JoinGame;
        }
    }
}
