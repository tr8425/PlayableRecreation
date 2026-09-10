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

            // 손님이 청해 놓고 기다리던 자리라면 그 청은 여기서 이루어진 것이다.
            // 지우지 않으면 잠시 뒤 "아무도 안 왔다" 로 읽고 혼자 두게 만든다.
            TogetherInvite.Cancel(board);

            // 상대가 못 오면 한 사람만 서 있는 그림을 만들지 않는다 — 그냥 혼자 두는 판으로 연다.
            if (!Send(opponent, PRDefOf.PR_JoinGame, board, seated))
            {
                Messages.Message("PR.Together.CannotCome".Translate(opponent.LabelShortCap),
                    board, MessageTypeDefOf.RejectInput, false);

                // **둘이 두던 판은 못 오는 상대로 열지 않는다.** 이어 두는 판은 상대를
                // 그대로 들고 있어서 혼자 여는 길이 없다 — 열면 §7.1 이 반 초 만에 다시
                // 닫고 "못 온다" 다음에 "자리를 떴다" 가 잇따르는 이상한 두 줄이 된다.
                // 판은 가구 위에 그대로 남으므로 나중에 다시 부르면 그만이다.
                if (session == null) OpenNow(game, board, seated, null, tier, practice, null);
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

        /// <summary>
        /// 기다리다 만 판을 치운다. **<see cref="GameComponent_Recreation"/> 가 1초마다 부른다.**
        ///
        /// 전에는 Hold 토일의 마무리 동작이 이 일을 했는데, 바닐라 <c>JobDriver.Cleanup</c> 은
        /// **그때 실행 중이던 토일 하나의** 마무리만 부른다. 걸어가는 도중에 징집되거나 다른
        /// 일을 받으면 Hold 는 시작조차 안 했으므로 마무리도 없다 — 둘 다 그렇게 빠지면
        /// 기다리는 판이 영영 남아 그 두 사람이 계속 "이미 다른 판에 있음" 으로 걸린다.
        /// 안에서 지우게 두지 않고 밖에서 한 번 훑는 편이 확실하다.
        /// </summary>
        public static void Sweep()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pending entry = pending[i];

                bool lost = !Alive(entry.seated) || !Alive(entry.opponent)
                    || entry.board == null || !entry.board.Spawned;

                bool timedOut = Find.TickManager.TicksGame - entry.startedTick > WaitLimitTicks;

                // 둘 다 우리 Job 을 놓았으면 아무도 오지 않는다.
                bool coming = Coming(entry.seated, entry.board) || Coming(entry.opponent, entry.board);

                if (!lost && !timedOut && coming) continue;

                pending.Remove(entry);

                // 못 왔다는 말은 못 온 것일 때만 한다. 플레이어가 직접 다른 일을 시켜
                // 흩어진 경우는 이미 아는 일이라 알릴 말이 없다.
                if (!lost && !timedOut) continue;

                Pawn missing = Alive(entry.opponent) ? entry.seated : entry.opponent;
                if (missing != null)
                    Messages.Message("PR.Together.NeverCame".Translate(missing.LabelShortCap),
                        entry.board, MessageTypeDefOf.NeutralEvent, false);
            }

            // 창이 닫히면 PostClose 가 늘 놓아 주므로 보통은 비어 있다. 가구가 사라진 판만 줍는다.
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].board == null || !live[i].board.Spawned) live.RemoveAt(i);
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

        /// <summary>이 가구에서 맞은편에 선 사람. 마주 보게 세우려면 누군지 알아야 한다.</summary>
        public static Pawn PartnerOf(Thing board, Pawn pawn)
        {
            if (board == null || pawn == null) return null;

            for (int i = 0; i < live.Count; i++)
            {
                Live entry = live[i];
                if (entry.board != board) continue;
                if (entry.seated == pawn) return entry.opponent;
                if (entry.opponent == pawn) return entry.seated;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                Pending entry = pending[i];
                if (entry.board != board) continue;
                if (entry.seated == pawn) return entry.opponent;
                if (entry.opponent == pawn) return entry.seated;
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

        /// <summary>
        /// 판을 전부 잊는다. **새 판을 시작하거나 불러올 때 불러야 한다.**
        ///
        /// 여기 두 목록은 static 이라 메인메뉴로 나갔다 들어와도 그대로 남는다.
        /// 지난 판의 폰과 가구를 계속 잡고 있으면 이미 버려진 Game 하나가 통째로
        /// 메모리에 남고, 무엇보다 없는 판을 있다고 대답하게 된다.
        /// </summary>
        public static void Reset()
        {
            pending.Clear();
            live.Clear();
        }

        public static void Cancel(Thing board)
        {
            Pending entry = PendingFor(board);
            if (entry != null) pending.Remove(entry);
        }

        /// <summary>
        /// 지금 들고 있는 것 전부. 개발자 도구(<c>PRDebug</c>)가 로그로 뽑는다 —
        /// 이 두 목록은 세이브에 안 남아서 밖에서 볼 방법이 이것뿐이다.
        /// </summary>
        public static string Describe()
        {
            string text = "";

            for (int i = 0; i < pending.Count; i++)
            {
                Pending entry = pending[i];
                int held = Find.TickManager.TicksGame - entry.startedTick;

                text += "  pending: " + entry.seated.ToStringSafe() + " + " + entry.opponent.ToStringSafe()
                    + " at " + entry.board.ToStringSafe()
                    + " (" + held + "/" + WaitLimitTicks + " ticks"
                    + (entry.session != null ? ", resuming" : ", new") + ")\n";
            }

            for (int i = 0; i < live.Count; i++)
                text += "  live: " + live[i].seated.ToStringSafe() + " + " + live[i].opponent.ToStringSafe()
                    + " at " + live[i].board.ToStringSafe() + "\n";

            return text.Length > 0 ? text.TrimEnd('\n') : "  pending/live: (none)";
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

            // **같은 Job 을 이미 들고 있으면 바닐라는 바꾸지 않고 true 를 돌려준다** —
            // <c>Pawn_JobTracker.TryTakeOrderedJob</c> 의 첫 줄이 <c>JobIsSameAs</c> 다.
            // 그러면 걷는 토일이 다시 돌지 않아 도착 통지도 다시 일어나지 않고, 새 판이
            // 영영 안 열린 채 2500틱 뒤 조용히 취소된다. 연습 판의 "다시 두기" 가 정확히
            // 이 자리였다 (QA-05). 먼저 놓게 해서 진짜로 새로 시작시킨다.
            if (pawn.CurJob != null && pawn.CurJobDef == def)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);

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

        /// <summary>
        /// 이 사람이 아직 그 가구로 오는 중인가. 우리 Job 을 그 가구에 대고 들고 있으면
        /// 오는 중이다 — 걷는 동안에도, 닿아 서 있는 동안에도 같은 Job 이다.
        /// </summary>
        private static bool Coming(Pawn pawn, Thing board)
        {
            if (!Alive(pawn) || pawn.CurJob == null) return false;

            JobDef def = pawn.CurJobDef;
            if (def != PRDefOf.PR_GoToGame && def != PRDefOf.PR_JoinGame) return false;

            return pawn.CurJob.GetTarget(TargetIndex.A).Thing == board;
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
