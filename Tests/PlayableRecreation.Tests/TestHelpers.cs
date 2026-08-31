using Ur.Core;

namespace PlayableRecreation.Tests
{
    internal static class TestHelpers
    {
        /// <summary>
        /// 지정한 경로 인덱스에 말을 놓은 상태를 만든다.
        /// 대기 말 수는 7 - (보드 위) - (골인) 으로 자동 계산되어 불변식이 항상 성립한다.
        /// </summary>
        public static UrGameState State(Side turn,
                                        int[] playerPieces,
                                        int[] botPieces,
                                        int scoredPlayer = 0,
                                        int scoredBot = 0)
        {
            UrGameState s = default;
            s.Turn = turn;

            foreach (int i in playerPieces) s.SetOccupied(Side.Player, i, true);
            foreach (int i in botPieces) s.SetOccupied(Side.Bot, i, true);

            s.SetScored(Side.Player, scoredPlayer);
            s.SetScored(Side.Bot, scoredBot);
            s.SetWaiting(Side.Player, UrBoardLayout.PieceCount - playerPieces.Length - scoredPlayer);
            s.SetWaiting(Side.Bot, UrBoardLayout.PieceCount - botPieces.Length - scoredBot);

            return s;
        }

        public static UrMove[] Moves(in UrGameState s, int roll)
        {
            var buffer = new UrMove[UrRules.MaxMoves];
            int n = UrRules.GenerateMoves(in s, roll, buffer);
            var result = new UrMove[n];
            System.Array.Copy(buffer, result, n);
            return result;
        }

        public static bool HasMove(in UrGameState s, int roll, int from, int to)
        {
            foreach (var m in Moves(in s, roll))
                if (m.From == from && m.To == to) return true;
            return false;
        }
    }
}
