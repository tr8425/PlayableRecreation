using System.Collections.Generic;

namespace RoyalGameOfUr.Core
{
    public enum UrPhase : byte
    {
        /// <summary>주사위를 굴려야 한다.</summary>
        AwaitingRoll,

        /// <summary>굴린 결과로 둘 수를 골라야 한다.</summary>
        AwaitingMove,

        /// <summary>눈이 0이거나 합법수가 없어 턴을 넘겨야 한다(R1/R9).</summary>
        MustPass,

        /// <summary>승부가 났다.</summary>
        Finished,
    }

    /// <summary>기보 한 줄.</summary>
    public readonly struct UrLogEntry
    {
        public readonly int Turn;
        public readonly Side Side;
        public readonly int Roll;
        public readonly UrMove Move;
        public readonly bool Passed;

        private UrLogEntry(int turn, Side side, int roll, UrMove move, bool passed)
        {
            Turn = turn;
            Side = side;
            Roll = roll;
            Move = move;
            Passed = passed;
        }

        public static UrLogEntry ForMove(int turn, Side side, int roll, UrMove move)
        {
            return new UrLogEntry(turn, side, roll, move, false);
        }

        public static UrLogEntry ForPass(int turn, Side side, int roll)
        {
            return new UrLogEntry(turn, side, roll, default(UrMove), true);
        }
    }

    /// <summary>
    /// 턴이 시작하는 순간의 판. 주사위가 (시드, 순번)으로 결정되므로 이 넷만 있으면
    /// 그 턴을 눈까지 똑같이 재현할 수 있다. 무르기와 세션 저장이 같은 표현을 쓴다. (DESIGN.md §7.2)
    /// </summary>
    public readonly struct UrTurnMark
    {
        public readonly UrGameState State;
        public readonly int DiceIndex;
        public readonly int TurnCount;
        public readonly int LogCount;
        public readonly int CapturesPlayer;
        public readonly int CapturesBot;
        public readonly int RosettesPlayer;
        public readonly int RosettesBot;

        public UrTurnMark(UrGameState state, int diceIndex, int turnCount, int logCount,
                          int capturesPlayer, int capturesBot, int rosettesPlayer, int rosettesBot)
        {
            State = state;
            DiceIndex = diceIndex;
            TurnCount = turnCount;
            LogCount = logCount;
            CapturesPlayer = capturesPlayer;
            CapturesBot = capturesBot;
            RosettesPlayer = rosettesPlayer;
            RosettesBot = rosettesBot;
        }
    }

    /// <summary>
    /// 한 판의 진행 상태. 타이밍/연출은 UI 가 담당하고 여기는 순수 로직만 갖는다.
    /// Verse 의존이 없으므로 그대로 단위 테스트할 수 있다.
    /// </summary>
    public sealed class UrMatch
    {
        private readonly UrMove[] legalMoves = new UrMove[UrRules.MaxMoves];

        private readonly int[] captures = new int[2];
        private readonly int[] rosettes = new int[2];

        public int Seed { get; private set; }

        /// <summary>지금까지 굴린 횟수. 무르기 시 이 값을 되감으면 같은 눈이 재현된다(P4).</summary>
        public int DiceIndex { get; private set; }

        public UrGameState State;

        public UrRoll Roll { get; private set; }

        /// <summary>이번 턴의 주사위가 공개되었는가. 굴리기 전이면 false.</summary>
        public bool RollRevealed { get; private set; }

        /// <summary>이번 턴이 시작될 때의 굴림 순번. 여기로 되감으면 같은 눈이 다시 나온다(P4).</summary>
        public int TurnStartDiceIndex { get; private set; }

        public UrPhase Phase { get; private set; }

        /// <summary>1부터 시작하는 수순 번호. 추가 턴도 한 수로 센다.</summary>
        public int TurnCount { get; private set; }

        public readonly List<UrLogEntry> Log = new List<UrLogEntry>();

        /// <summary>합법수 버퍼. 앞의 <see cref="LegalCount"/> 개만 유효하다.</summary>
        public UrMove[] LegalMoves { get { return legalMoves; } }

        public int LegalCount { get; private set; }

        public Side Turn { get { return State.Turn; } }

        public Side? Winner { get { return UrRules.Winner(in State); } }

        public bool IsOver { get { return Phase == UrPhase.Finished; } }

        /// <summary>이 판에서 side 가 상대 말을 잡은 횟수.</summary>
        public int Captures(Side side) { return captures[(int)side]; }

        /// <summary>이 판에서 side 가 로제트에 정확히 도착한 횟수.</summary>
        public int RosetteLandings(Side side) { return rosettes[(int)side]; }

        public UrMatch(int seed, Side first)
        {
            Seed = seed;
            State = UrGameState.NewGame(first);
            Phase = UrPhase.AwaitingRoll;
            TurnCount = 1;
        }

        public void RollDice()
        {
            if (Phase != UrPhase.AwaitingRoll) return;

            Roll = UrDice.Roll(Seed, DiceIndex);
            DiceIndex++;
            RollRevealed = true;

            LegalCount = UrRules.GenerateMoves(in State, Roll.Total, legalMoves);
            Phase = LegalCount > 0 ? UrPhase.AwaitingMove : UrPhase.MustPass;
        }

        public void Pass()
        {
            if (Phase != UrPhase.MustPass) return;

            Log.Add(UrLogEntry.ForPass(TurnCount, State.Turn, Roll.Total));
            State = UrRules.PassTurn(in State);
            BeginNextTurn();
        }

        public void PlayMove(int legalIndex)
        {
            if (Phase != UrPhase.AwaitingMove) return;
            if (legalIndex < 0 || legalIndex >= LegalCount) return;

            UrMove move = legalMoves[legalIndex];
            Log.Add(UrLogEntry.ForMove(TurnCount, State.Turn, Roll.Total, move));

            int mover = (int)State.Turn;
            if (move.IsCapture) captures[mover]++;
            if (move.GrantsExtraTurn) rosettes[mover]++;

            bool extraTurn;
            State = UrRules.Apply(in State, move, out extraTurn);

            if (UrRules.IsGameOver(in State))
            {
                Phase = UrPhase.Finished;
                RollRevealed = false;
                LegalCount = 0;
                return;
            }

            // 추가 턴이면 UrRules.Apply 가 이미 턴을 유지했다. 어느 쪽이든 다시 굴리기부터 시작한다.
            BeginNextTurn();
        }

        /// <summary>이 합법수의 도착 칸에 상대 말이 있어 잡게 되는가. UI 강조용.</summary>
        public bool IsCaptureMove(int legalIndex)
        {
            return legalIndex >= 0 && legalIndex < LegalCount && legalMoves[legalIndex].IsCapture;
        }

        private void BeginNextTurn()
        {
            RollRevealed = false;
            LegalCount = 0;
            TurnCount++;
            TurnStartDiceIndex = DiceIndex;
            Phase = UrPhase.AwaitingRoll;
        }

        // ---------- 되감기 · 이어두기 ----------

        /// <summary>지금 턴의 시작 지점을 기록한다. 무르기와 세션 저장이 함께 쓴다.</summary>
        public UrTurnMark MarkTurnStart()
        {
            return new UrTurnMark(State, TurnStartDiceIndex, TurnCount, Log.Count,
                                  captures[0], captures[1], rosettes[0], rosettes[1]);
        }

        /// <summary>
        /// 기록해 둔 턴 시작 지점으로 되돌린다. 주사위 순번까지 되감으므로
        /// 같은 눈이 다시 나오고, 무르기로 좋은 눈을 새로 뽑을 수는 없다(P4).
        /// </summary>
        public void RewindTo(in UrTurnMark mark)
        {
            State = mark.State;
            DiceIndex = mark.DiceIndex;
            TurnStartDiceIndex = mark.DiceIndex;
            TurnCount = mark.TurnCount;

            captures[0] = mark.CapturesPlayer;
            captures[1] = mark.CapturesBot;
            rosettes[0] = mark.RosettesPlayer;
            rosettes[1] = mark.RosettesBot;

            if (mark.LogCount < Log.Count) Log.RemoveRange(mark.LogCount, Log.Count - mark.LogCount);

            Roll = default(UrRoll);
            RollRevealed = false;
            LegalCount = 0;
            Phase = UrPhase.AwaitingRoll;
        }

        /// <summary>보드에 남겨둔 판을 다시 펼친다. 저장은 언제나 턴 시작 시점 기준이다.</summary>
        public static UrMatch Restore(int seed, int diceIndex, UrGameState state, int turnCount,
                                      int capturesPlayer, int capturesBot,
                                      int rosettesPlayer, int rosettesBot)
        {
            UrMatch match = new UrMatch(seed, state.Turn);
            match.RewindTo(new UrTurnMark(state, diceIndex, turnCount, 0,
                                          capturesPlayer, capturesBot, rosettesPlayer, rosettesBot));
            return match;
        }
    }
}
