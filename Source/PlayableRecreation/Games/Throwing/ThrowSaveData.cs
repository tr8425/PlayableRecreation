using PlayableRecreation;
using Throwing.Core;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 가구에 남는 던지기 판. 저장은 이닝 경계에서만 일어난다 -
    /// 던지다 만 상태는 없고, 상대의 다음 발은 (시드, 순번)으로 이미 정해져 있다.
    /// </summary>
    public class ThrowSaveData : MiniGameSaveData
    {
        public int seed;
        public int inning = 1;
        public int throwIndex;
        public int scorePlayer;
        public int scoreOpponent;
        public int ringers;
        public bool opponentTurn;

        public ThrowSaveData()
        {
        }

        public ThrowSaveData(ThrowMatch match)
        {
            seed = match.Seed;
            inning = match.Inning;
            throwIndex = match.ThrowIndex;
            scorePlayer = match.ScorePlayer;
            scoreOpponent = match.ScoreOpponent;
            ringers = match.Ringers;
            opponentTurn = match.Turn == ThrowSide.Opponent;
        }

        public ThrowMatch ToMatch(ThrowRules rules)
        {
            return ThrowMatch.Restore(rules, seed, inning, throwIndex, scorePlayer, scoreOpponent,
                                      ringers, opponentTurn ? ThrowSide.Opponent : ThrowSide.Player);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref inning, "inning", 1);
            Scribe_Values.Look(ref throwIndex, "throwIndex", 0);
            Scribe_Values.Look(ref scorePlayer, "scorePlayer", 0);
            Scribe_Values.Look(ref scoreOpponent, "scoreOpponent", 0);
            Scribe_Values.Look(ref ringers, "ringers", 0);
            Scribe_Values.Look(ref opponentTurn, "opponentTurn", false);
        }
    }
}
