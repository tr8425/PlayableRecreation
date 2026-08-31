using PlayableRecreation;
using Ur.Core;
using Verse;

namespace Ur
{
    /// <summary>
    /// 보드에 남는 우르 판. 저장 시점은 언제나 턴이 시작하는 순간이다.
    ///
    /// 주사위가 (시드, 순번)으로 결정되므로 이어 두면 중단한 그 턴의 눈이 그대로 다시 나온다 -
    /// 창을 닫았다 여는 식의 세이브스컴이 통하지 않는다.
    /// </summary>
    public class UrSaveData : MiniGameSaveData
    {
        public int seed;
        public int diceIndex;
        public int turnCount = 1;
        public UrGameState state;

        public int capturesPlayer, capturesBot;
        public int rosettesPlayer, rosettesBot;

        public UrSaveData()
        {
        }

        public UrSaveData(UrMatch match)
        {
            UrTurnMark mark = match.MarkTurnStart();

            seed = match.Seed;
            diceIndex = mark.DiceIndex;
            turnCount = mark.TurnCount;
            state = mark.State;

            capturesPlayer = mark.CapturesPlayer;
            capturesBot = mark.CapturesBot;
            rosettesPlayer = mark.RosettesPlayer;
            rosettesBot = mark.RosettesBot;
        }

        public UrMatch ToMatch()
        {
            return UrMatch.Restore(seed, diceIndex, state, turnCount,
                                   capturesPlayer, capturesBot, rosettesPlayer, rosettesBot);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref diceIndex, "diceIndex", 0);
            Scribe_Values.Look(ref turnCount, "turnCount", 1);

            Scribe_Values.Look(ref capturesPlayer, "capturesPlayer", 0);
            Scribe_Values.Look(ref capturesBot, "capturesBot", 0);
            Scribe_Values.Look(ref rosettesPlayer, "rosettesPlayer", 0);
            Scribe_Values.Look(ref rosettesBot, "rosettesBot", 0);

            // UrGameState 는 Verse 를 모르는 순수 구조체라 여기서 손으로 펴고 접는다.
            int occPlayer = state.OccPlayer;
            int occBot = state.OccBot;
            int waitingPlayer = state.WaitingPlayer;
            int waitingBot = state.WaitingBot;
            int scoredPlayer = state.ScoredPlayer;
            int scoredBot = state.ScoredBot;
            bool botTurn = state.Turn == Side.Bot;

            Scribe_Values.Look(ref occPlayer, "occPlayer", 0);
            Scribe_Values.Look(ref occBot, "occBot", 0);
            Scribe_Values.Look(ref waitingPlayer, "waitingPlayer", UrBoardLayout.PieceCount);
            Scribe_Values.Look(ref waitingBot, "waitingBot", UrBoardLayout.PieceCount);
            Scribe_Values.Look(ref scoredPlayer, "scoredPlayer", 0);
            Scribe_Values.Look(ref scoredBot, "scoredBot", 0);
            Scribe_Values.Look(ref botTurn, "botTurn", false);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                state = default(UrGameState);
                state.OccPlayer = (ushort)occPlayer;
                state.OccBot = (ushort)occBot;
                state.WaitingPlayer = (byte)waitingPlayer;
                state.WaitingBot = (byte)waitingBot;
                state.ScoredPlayer = (byte)scoredPlayer;
                state.ScoredBot = (byte)scoredBot;
                state.Turn = botTurn ? Side.Bot : Side.Player;
            }
        }
    }
}
