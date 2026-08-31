namespace Chess.Core
{
    public enum ChessSide : byte
    {
        White = 0,
        Black = 1,
    }

    /// <summary>
    /// 0x88 판. 128칸짜리 배열의 절반만 쓰고, 나머지 절반이 "판 밖"을 한 번의 & 로 걸러 준다.
    /// 비트보드보다 느리지만 훨씬 읽기 쉽고, 우리에게 필요한 깊이에는 충분하다.
    /// </summary>
    public static class Chess88
    {
        public const int A1 = 0;
        public const int H1 = 7;
        public const int A8 = 112;
        public const int H8 = 119;

        public static bool OnBoard(int square)
        {
            return (square & 0x88) == 0;
        }

        public static int Square(int file, int rank)
        {
            return rank * 16 + file;
        }

        public static int File(int square) { return square & 7; }
        public static int Rank(int square) { return square >> 4; }

        /// <summary>밝은 칸인가. a1 이 어두운 칸이다.</summary>
        public static bool IsLight(int square)
        {
            return ((File(square) + Rank(square)) & 1) != 0;
        }

        /// <summary>0x88 칸을 0~63 으로 접는다. 저장과 해시에 쓴다.</summary>
        public static int ToIndex(int square)
        {
            return Rank(square) * 8 + File(square);
        }

        public static int FromIndex(int index)
        {
            return index / 8 * 16 + index % 8;
        }

        public static string Name(int square)
        {
            return ((char)('a' + File(square))).ToString() + (char)('1' + Rank(square));
        }
    }

    /// <summary>기물 코드. 양수가 백, 음수가 흑, 0이 빈 칸이다.</summary>
    public static class Piece
    {
        public const sbyte None = 0;
        public const sbyte Pawn = 1;
        public const sbyte Knight = 2;
        public const sbyte Bishop = 3;
        public const sbyte Rook = 4;
        public const sbyte Queen = 5;
        public const sbyte King = 6;

        public static int Kind(sbyte piece)
        {
            return piece < 0 ? -piece : piece;
        }

        public static bool IsWhite(sbyte piece) { return piece > 0; }
        public static bool IsBlack(sbyte piece) { return piece < 0; }

        public static ChessSide SideOf(sbyte piece)
        {
            return piece > 0 ? ChessSide.White : ChessSide.Black;
        }

        public static sbyte Make(int kind, ChessSide side)
        {
            return (sbyte)(side == ChessSide.White ? kind : -kind);
        }

        /// <summary>기보와 FEN 에서 쓰는 글자. 백은 대문자.</summary>
        public static char Letter(sbyte piece)
        {
            const string letters = ".PNBRQK";
            char c = letters[Kind(piece)];
            return piece < 0 ? char.ToLowerInvariant(c) : c;
        }
    }

    /// <summary>캐슬링 권리 비트.</summary>
    public static class Castle
    {
        public const int WhiteKing = 1;
        public const int WhiteQueen = 2;
        public const int BlackKing = 4;
        public const int BlackQueen = 8;
        public const int All = 15;
    }

    public static class Offsets
    {
        public static readonly int[] Knight = { 33, 31, 18, 14, -33, -31, -18, -14 };
        public static readonly int[] King = { 16, -16, 1, -1, 17, 15, -17, -15 };
        public static readonly int[] Bishop = { 17, 15, -17, -15 };
        public static readonly int[] Rook = { 16, -16, 1, -1 };
        public static readonly int[] Queen = { 16, -16, 1, -1, 17, 15, -17, -15 };
    }
}
