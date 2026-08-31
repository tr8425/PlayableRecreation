namespace Chess.Core
{
    /// <summary>
    /// 국면 점수. 재료값에 칸별 가산표를 얹은 고전적인 구성이다 -
    /// 깊이 4~5 에서 기물을 공짜로 내주지 않고 중앙과 킹 안전을 챙기기에는 이 정도면 된다.
    ///
    /// 값은 언제나 **둘 차례인 쪽** 기준이다.
    /// </summary>
    public static class ChessEval
    {
        public const int MateScore = 30000;

        public static readonly int[] Material = { 0, 100, 320, 330, 500, 900, 0 };

        /// <summary>비숍 두 개는 하나씩 있는 것보다 낫다.</summary>
        private const int BishopPair = 30;

        /// <summary>폰을 뺀 재료가 이보다 적으면 끝내기로 본다. 킹의 가산표가 뒤집힌다.</summary>
        private const int EndgameThreshold = 1300;

        private static readonly int[] PawnTable =
        {
             0,  0,  0,  0,  0,  0,  0,  0,
             5, 10, 10,-20,-20, 10, 10,  5,
             5, -5,-10,  0,  0,-10, -5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5,  5, 10, 25, 25, 10,  5,  5,
            10, 10, 20, 30, 30, 20, 10, 10,
            50, 50, 50, 50, 50, 50, 50, 50,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        private static readonly int[] KnightTable =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50,
        };

        private static readonly int[] BishopTable =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -20,-10,-10,-10,-10,-10,-10,-20,
        };

        private static readonly int[] RookTable =
        {
              0,  0,  0,  5,  5,  0,  0,  0,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              5, 10, 10, 10, 10, 10, 10,  5,
              0,  0,  0,  0,  0,  0,  0,  0,
        };

        private static readonly int[] QueenTable =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -10,  5,  5,  5,  5,  5,  0,-10,
              0,  0,  5,  5,  5,  5,  0, -5,
             -5,  0,  5,  5,  5,  5,  0, -5,
            -10,  0,  5,  5,  5,  5,  0,-10,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20,
        };

        private static readonly int[] KingMiddle =
        {
             20, 30, 10,  0,  0, 10, 30, 20,
             20, 20,  0,  0,  0,  0, 20, 20,
            -10,-20,-20,-20,-20,-20,-20,-10,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
        };

        private static readonly int[] KingEnd =
        {
            -50,-30,-30,-30,-30,-30,-30,-50,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -50,-40,-30,-20,-20,-30,-40,-50,
        };

        /// <summary>둘 차례인 쪽에서 본 점수.</summary>
        public static int Evaluate(ChessBoard board)
        {
            int score = 0;
            int[] bishops = { 0, 0 };
            int heavy = 0;

            for (int square = 0; square < 128; square++)
            {
                if (!Chess88.OnBoard(square)) continue;

                sbyte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                int kind = Piece.Kind(piece);
                if (kind != Piece.Pawn && kind != Piece.King) heavy += Material[kind];
                if (kind == Piece.Bishop) bishops[(int)Piece.SideOf(piece)]++;
            }

            bool endgame = heavy < EndgameThreshold;

            for (int square = 0; square < 128; square++)
            {
                if (!Chess88.OnBoard(square)) continue;

                sbyte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                bool white = Piece.IsWhite(piece);
                int kind = Piece.Kind(piece);

                // 흑은 판을 위아래로 뒤집어 같은 표를 본다.
                int index = Chess88.ToIndex(square);
                if (!white) index = (7 - index / 8) * 8 + index % 8;

                int value = Material[kind] + TableOf(kind, endgame)[index];
                score += white ? value : -value;
            }

            if (bishops[0] >= 2) score += BishopPair;
            if (bishops[1] >= 2) score -= BishopPair;

            return board.SideToMove == ChessSide.White ? score : -score;
        }

        private static int[] TableOf(int kind, bool endgame)
        {
            switch (kind)
            {
                case Piece.Pawn: return PawnTable;
                case Piece.Knight: return KnightTable;
                case Piece.Bishop: return BishopTable;
                case Piece.Rook: return RookTable;
                case Piece.Queen: return QueenTable;
                default: return endgame ? KingEnd : KingMiddle;
            }
        }
    }
}
