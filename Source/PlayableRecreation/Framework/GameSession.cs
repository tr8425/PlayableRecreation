using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 가구 위에 남겨둔 판. 세이브 슬롯이 아니라 "실제로 놓여 있는 말의 배치"다.
    ///
    /// 저장 시점은 게임이 정한다(<see cref="MiniGameWorker.SavePoint"/>). 우르는 턴이 시작하는 순간이고,
    /// 그 시점의 주사위가 (시드, 순번)으로 결정되므로 창을 닫았다 여는 세이브스컴이 통하지 않는다.
    /// </summary>
    public sealed class GameSession : IExposable
    {
        public Thing board;

        /// <summary>플레이어가 조작하는 쪽. 뜻은 예전과 같다.</summary>
        public Pawn seatedPawn;

        /// <summary>맞은편에 앉은 사람. 몰입 모드 2칸이 아니면 null 이다.</summary>
        public Pawn opponentPawn;

        public MiniGameDef game;

        public int tier;
        public bool practice;
        public int rounds = 1;
        public int undosUsed;

        /// <summary>게임이 떠낸 알맹이. 프레임워크는 내용을 모른다.</summary>
        public MiniGameSaveData data;

        public int startTick;
        public int lastPlayedTick;

        /// <summary>이 판에 들인 실제 시간(초). 창을 여러 번 나눠 열어도 합산된다.</summary>
        public float realSeconds;

        // 무효화 감시 스냅샷
        public int boardHitPoints;
        public int nearbyFilth;
        public IntVec3 boardPosition;

        public GameSession()
        {
        }

        public GameSession(Thing board, Pawn seatedPawn, MiniGameDef game, int tier, bool practice)
            : this(board, seatedPawn, null, game, tier, practice)
        {
        }

        public GameSession(Thing board, Pawn seatedPawn, Pawn opponentPawn, MiniGameDef game, int tier, bool practice)
        {
            this.board = board;
            this.seatedPawn = seatedPawn;
            this.opponentPawn = opponentPawn;
            this.game = game;
            this.tier = tier;
            this.practice = practice;

            startTick = CurrentTick;
            lastPlayedTick = startTick;
            TakeSnapshot();
        }

        public bool BoardAlive
        {
            get { return board != null && board.Spawned && !board.Destroyed; }
        }

        private static int CurrentTick
        {
            get { return Find.TickManager != null ? Find.TickManager.TicksGame : 0; }
        }

        /// <summary>지금 판을 그대로 옮겨 담는다.</summary>
        public void CaptureFrom(MiniGameWorker worker)
        {
            if (worker == null) return;

            data = worker.MakeSaveData();
            rounds = worker.Rounds;
            lastPlayedTick = CurrentTick;
            TakeSnapshot();
        }

        /// <summary>무효화 판정의 기준선을 지금 상태로 다시 잡는다.</summary>
        public void TakeSnapshot()
        {
            if (!BoardAlive) return;

            boardHitPoints = board.HitPoints;
            boardPosition = board.Position;
            nearbyFilth = Invalidation.CountNearbyFilth(board);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref board, "board");
            Scribe_References.Look(ref seatedPawn, "seatedPawn");
            Scribe_References.Look(ref opponentPawn, "opponentPawn");
            Scribe_Defs.Look(ref game, "game");

            Scribe_Values.Look(ref tier, "tier", 0);
            Scribe_Values.Look(ref practice, "practice", false);
            Scribe_Values.Look(ref rounds, "rounds", 1);
            Scribe_Values.Look(ref undosUsed, "undosUsed", 0);

            Scribe_Deep.Look(ref data, "data");

            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref lastPlayedTick, "lastPlayedTick", 0);
            Scribe_Values.Look(ref realSeconds, "realSeconds", 0f);

            Scribe_Values.Look(ref boardHitPoints, "boardHitPoints", 0);
            Scribe_Values.Look(ref nearbyFilth, "nearbyFilth", 0);
            Scribe_Values.Look(ref boardPosition, "boardPosition", IntVec3.Invalid);
        }
    }
}
