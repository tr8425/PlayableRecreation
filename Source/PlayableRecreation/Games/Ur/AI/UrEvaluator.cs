using RoyalGameOfUr.Core;

namespace RoyalGameOfUr.AI
{
    /// <summary>
    /// 국면 평가. 진행도 + 요충지 점유 - 잡힐 기댓값. (DESIGN.md §5.2)
    /// 잡힐 확률은 주사위 분포로 정확히 계산하며 근사하지 않는다.
    /// </summary>
    public static class UrEvaluator
    {
        public const float ScoredValue = 100f;
        public const float CenterRosetteBonus = 30f;
        public const float RosetteBonus = 12f;
        public const float FreeLaneBonus = 8f;

        /// <summary>말 하나의 전진 가치.</summary>
        public static float PieceValue(int pathIndex)
        {
            return 4f + pathIndex * 2f;
        }

        public static float Evaluate(in UrGameState state, Side me)
        {
            Side opponent = me.Opponent();

            return Material(in state, me) - Material(in state, opponent)
                 + Exposure(in state, me) - Exposure(in state, opponent);
        }

        private static float Material(in UrGameState state, Side side)
        {
            float score = state.Scored(side) * ScoredValue;

            for (int i = 1; i <= UrBoardLayout.PathLength; i++)
            {
                if (!state.IsOccupied(side, i)) continue;

                score += PieceValue(i);

                if (i == UrBoardLayout.SafeIndex) score += CenterRosetteBonus;
                else if (UrBoardLayout.IsRosette(i)) score += RosetteBonus;
            }

            if (!HasPieceInSharedLane(in state, side.Opponent())) score += FreeLaneBonus;

            return score;
        }

        /// <summary>공유 구간에 노출된 내 말이 다음 턴에 잡힐 기댓값(음수).</summary>
        private static float Exposure(in UrGameState state, Side side)
        {
            Side attacker = side.Opponent();
            float loss = 0f;

            for (int i = UrBoardLayout.SharedFirst; i <= UrBoardLayout.SharedLast; i++)
            {
                if (i == UrBoardLayout.SafeIndex) continue;     // 안전칸은 잡히지 않는다(R6)
                if (!state.IsOccupied(side, i)) continue;

                loss += PieceValue(i) * CaptureChance(in state, attacker, i);
            }

            return -loss;
        }

        /// <summary>
        /// attacker 가 다음 한 턴에 targetIndex 로 도달할 확률.
        /// UI 의 '위험 경고' 보조도 이 값을 그대로 쓴다.
        /// </summary>
        public static float CaptureChance(in UrGameState state, Side attacker, int targetIndex)
        {
            float chance = 0f;

            for (int roll = 1; roll <= UrDice.DiceCount; roll++)
            {
                int from = targetIndex - roll;

                // 투입은 경로 1~4 까지만 닿으므로 공유 구간(5~12)을 잡을 수 없다.
                if (from >= 1 && state.IsOccupied(attacker, from))
                    chance += (float)UrDice.Probability[roll];
            }

            return chance > 1f ? 1f : chance;
        }

        private static bool HasPieceInSharedLane(in UrGameState state, Side side)
        {
            for (int i = UrBoardLayout.SharedFirst; i <= UrBoardLayout.SharedLast; i++)
                if (state.IsOccupied(side, i)) return true;

            return false;
        }
    }
}
