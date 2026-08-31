using System.Collections.Generic;

namespace Ur.Core
{
    /// <summary>
    /// Finkel 룰 엔진. DESIGN.md §4.4 의 R1~R11 을 그대로 구현한다.
    /// 모든 함수는 순수 함수이며 힙 할당을 하지 않는다(List 편의 오버로드 제외).
    /// </summary>
    public static class UrRules
    {
        /// <summary>한 턴에 나올 수 있는 최대 합법수. 말 7개 + 투입 1 이 상한이다.</summary>
        public const int MaxMoves = UrBoardLayout.PieceCount + 1;

        /// <summary>
        /// 합법수를 buffer 에 채우고 개수를 돌려준다. buffer 길이는 <see cref="MaxMoves"/> 이상이어야 한다.
        /// roll 이 0 이면 항상 0을 돌려준다(R1).
        /// </summary>
        public static int GenerateMoves(in UrGameState s, int roll, UrMove[] buffer)
        {
            int count = 0;
            if (roll <= 0 || roll > UrDice.DiceCount) return 0;

            Side me = s.Turn;
            Side opp = me.Opponent();

            // R2: 대기 말 투입. 도착 인덱스는 곧 주사위 눈이다.
            if (s.Waiting(me) > 0)
                TryAdd(in s, me, opp, UrMove.FromWaiting, roll, buffer, ref count);

            // R3: 보드 위의 말 전진.
            for (int i = 1; i <= UrBoardLayout.PathLength; i++)
                if (s.IsOccupied(me, i))
                    TryAdd(in s, me, opp, i, i + roll, buffer, ref count);

            return count;
        }

        private static void TryAdd(in UrGameState s, Side me, Side opp, int from, int to,
                                   UrMove[] buffer, ref int count)
        {
            // R8: 골인은 정확히 15. 넘치면 불법.
            if (to > UrBoardLayout.ScoredIndex) return;

            if (to == UrBoardLayout.ScoredIndex)
            {
                buffer[count++] = new UrMove(from, to, false, false);
                return;
            }

            // R4: 자기 말 위에는 못 올라간다.
            if (s.IsOccupied(me, to)) return;

            // 상대 말 판정은 공유 전장에서만 의미가 있다. 자기 진영 칸의 같은
            // 인덱스는 상대에게는 물리적으로 다른 칸이므로 비교하면 안 된다.
            bool opponentThere = UrBoardLayout.IsShared(to) && s.IsOccupied(opp, to);

            // R6: 중앙 로제트 위의 말은 잡을 수 없으므로 그 칸으로의 이동 자체가 불법.
            if (to == UrBoardLayout.SafeIndex && opponentThere) return;

            buffer[count++] = new UrMove(from, to, opponentThere, UrBoardLayout.IsRosette(to));
        }

        /// <summary>편의 오버로드. UI 쪽에서만 쓰고, AI 탐색에서는 배열 버전을 쓴다.</summary>
        public static List<UrMove> LegalMoves(in UrGameState s, int roll)
        {
            UrMove[] buffer = new UrMove[MaxMoves];
            int n = GenerateMoves(in s, roll, buffer);
            List<UrMove> list = new List<UrMove>(n);
            for (int i = 0; i < n; i++) list.Add(buffer[i]);
            return list;
        }

        /// <summary>수를 적용한 새 상태를 돌려준다. 원본은 변경하지 않는다.</summary>
        public static UrGameState Apply(in UrGameState s, in UrMove move, out bool extraTurn)
        {
            UrGameState n = s;
            Side me = s.Turn;
            Side opp = me.Opponent();

            if (move.IsEntry) n.SetWaiting(me, s.Waiting(me) - 1);
            else n.SetOccupied(me, move.From, false);

            if (move.IsBearOff)
            {
                n.SetScored(me, s.Scored(me) + 1);
            }
            else
            {
                n.SetOccupied(me, move.To, true);

                // R5: 잡힌 말은 상대 대기열로 되돌아간다.
                if (move.IsCapture)
                {
                    n.SetOccupied(opp, move.To, false);
                    n.SetWaiting(opp, s.Waiting(opp) + 1);
                }
            }

            // R7: 로제트에 착지하면 턴을 넘기지 않는다.
            extraTurn = move.GrantsExtraTurn;
            if (!extraTurn) n.Turn = opp;

            return n;
        }

        /// <summary>R1/R9: 눈이 0이거나 합법수가 없을 때 턴을 넘긴다.</summary>
        public static UrGameState PassTurn(in UrGameState s)
        {
            UrGameState n = s;
            n.Turn = s.Turn.Opponent();
            return n;
        }

        /// <summary>R10: 7개를 모두 골인시킨 진영. 아직 승부가 안 났으면 null.</summary>
        public static Side? Winner(in UrGameState s)
        {
            if (s.ScoredPlayer >= UrBoardLayout.PieceCount) return Side.Player;
            if (s.ScoredBot >= UrBoardLayout.PieceCount) return Side.Bot;
            return null;
        }

        public static bool IsGameOver(in UrGameState s)
        {
            return Winner(in s).HasValue;
        }
    }
}
