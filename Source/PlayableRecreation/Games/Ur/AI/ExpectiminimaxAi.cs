using System;
using Ur.Core;

namespace Ur.AI
{
    /// <summary>
    /// T2~T4. 주사위 확률 노드를 포함한 완전 탐색(Expectiminimax).
    /// 상태가 구조체라 재귀 중 힙 할당이 없고, 합법수 버퍼는 깊이별로 미리 잡아 재사용한다.
    /// (DESIGN.md §5.1)
    /// </summary>
    public sealed class ExpectiminimaxAi : IUrAi
    {
        private const float WinScore = 10000f;

        private readonly int depth;
        private readonly float blunderChance;
        private readonly Random rng;
        private readonly UrMove[][] buffers;

        public ExpectiminimaxAi(int depth, float blunderChance, Random rng)
        {
            this.depth = depth < 1 ? 1 : depth;
            this.blunderChance = blunderChance;
            this.rng = rng ?? new Random();

            buffers = new UrMove[this.depth + 1][];
            for (int i = 0; i < buffers.Length; i++)
                buffers[i] = new UrMove[UrRules.MaxMoves];
        }

        public string Id { get { return "T" + depth; } }

        public int ChooseMove(in UrGameState state, int roll, UrMove[] legal, int legalCount)
        {
            Side me = state.Turn;

            int bestIndex = 0, secondIndex = -1;
            float bestScore = float.NegativeInfinity, secondScore = float.NegativeInfinity;

            for (int i = 0; i < legalCount; i++)
            {
                bool extraTurn;
                UrGameState next = UrRules.Apply(in state, legal[i], out extraTurn);
                float score = Search(in next, me, depth - 1);

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

        /// <summary>확률 노드. 주사위 눈 0~4의 기댓값을 돌려준다.</summary>
        private float Search(in UrGameState state, Side me, int remaining)
        {
            Side? winner = UrRules.Winner(in state);
            if (winner.HasValue) return winner.Value == me ? WinScore : -WinScore;
            if (remaining <= 0) return UrEvaluator.Evaluate(in state, me);

            float expected = 0f;
            for (int roll = 0; roll <= UrDice.DiceCount; roll++)
                expected += (float)UrDice.Probability[roll] * BestReply(in state, me, roll, remaining);

            return expected;
        }

        /// <summary>결정 노드. 눈이 정해진 상태에서 수를 두는 쪽이 최선/최악을 고른다.</summary>
        private float BestReply(in UrGameState state, Side me, int roll, int remaining)
        {
            UrMove[] buffer = buffers[remaining];
            int count = UrRules.GenerateMoves(in state, roll, buffer);

            // R1/R9: 둘 수가 없으면 턴만 넘어간다.
            if (count == 0) return Search(UrRules.PassTurn(in state), me, remaining - 1);

            bool maximizing = state.Turn == me;
            float best = maximizing ? float.NegativeInfinity : float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                bool extraTurn;
                UrGameState next = UrRules.Apply(in state, buffer[i], out extraTurn);
                float score = Search(in next, me, remaining - 1);

                if (maximizing) { if (score > best) best = score; }
                else { if (score < best) best = score; }
            }

            return best;
        }
    }
}
