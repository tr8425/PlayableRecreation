using System;
using RoyalGameOfUr.Core;

namespace RoyalGameOfUr.AI
{
    /// <summary>
    /// T1 견습. 한 수 앞만 보고 우선순위로 고른다: 잡기 &gt; 로제트 &gt; 골인 &gt; 전진 &gt; 투입.
    /// (DESIGN.md §5.1)
    /// </summary>
    public sealed class GreedyAi : IUrAi
    {
        private readonly Random rng;
        private readonly float blunderChance;

        public GreedyAi(Random rng, float blunderChance)
        {
            this.rng = rng ?? new Random();
            this.blunderChance = blunderChance;
        }

        public string Id { get { return "T1"; } }

        public int ChooseMove(in UrGameState state, int roll, UrMove[] legal, int legalCount)
        {
            int bestIndex = 0, bestScore = int.MinValue;
            int secondIndex = -1, secondScore = int.MinValue;

            for (int i = 0; i < legalCount; i++)
            {
                int score = Score(legal[i]);

                if (score > bestScore)
                {
                    secondScore = bestScore;
                    secondIndex = bestIndex;
                    bestScore = score;
                    bestIndex = i;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                    secondIndex = i;
                }
            }

            if (secondIndex >= 0 && blunderChance > 0f && rng.NextDouble() < blunderChance)
                return secondIndex;

            return bestIndex;
        }

        private static int Score(in UrMove move)
        {
            if (move.IsCapture) return 1000 + move.To;
            if (move.GrantsExtraTurn) return 800 + move.To;
            if (move.IsBearOff) return 600;
            if (!move.IsEntry) return 300 + move.To;
            return 100;
        }
    }
}
