namespace Chess.Core
{
    /// <summary>
    /// 같은 국면이 세 번 나왔는지 알아보기 위한 해시. 난수표는 고정 시드로 만든다 -
    /// 세이브에 해시를 적어 두므로 실행할 때마다 값이 달라지면 안 된다.
    /// </summary>
    public static class ChessZobrist
    {
        private static readonly ulong[,] Pieces = new ulong[13, 64];
        private static readonly ulong[] Castling = new ulong[16];
        private static readonly ulong[] EnPassantFile = new ulong[8];
        private static readonly ulong BlackToMove;

        static ChessZobrist()
        {
            ulong state = 0x9E3779B97F4A7C15UL;

            for (int piece = 0; piece < 13; piece++)
                for (int square = 0; square < 64; square++)
                    Pieces[piece, square] = Next(ref state);

            for (int i = 0; i < Castling.Length; i++) Castling[i] = Next(ref state);
            for (int i = 0; i < EnPassantFile.Length; i++) EnPassantFile[i] = Next(ref state);

            BlackToMove = Next(ref state);
        }

        /// <summary>splitmix64. 표만 만들면 되므로 이 정도면 충분하다.</summary>
        private static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>기물 코드(-6..6)를 표의 줄 번호(0..12)로 옮긴다.</summary>
        private static int Row(sbyte piece)
        {
            return piece + 6;
        }

        public static ulong Of(ChessBoard board)
        {
            ulong hash = 0;

            for (int index = 0; index < 64; index++)
            {
                sbyte piece = board.Squares[Chess88.FromIndex(index)];
                if (piece != Piece.None) hash ^= Pieces[Row(piece), index];
            }

            hash ^= Castling[board.Castling & 15];

            if (board.EnPassant >= 0) hash ^= EnPassantFile[Chess88.File(board.EnPassant)];
            if (board.SideToMove == ChessSide.Black) hash ^= BlackToMove;

            return hash;
        }
    }
}
