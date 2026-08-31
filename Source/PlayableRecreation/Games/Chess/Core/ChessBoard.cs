using System;
using System.Text;

namespace Chess.Core
{
    /// <summary>
    /// 국면 하나. 두고 되돌리는 것이 전부이고, 합법성 판정은 <see cref="ChessRules"/> 가 맡는다.
    ///
    /// 탐색이 이 위에서 수십만 번 두었다 물리므로 새 객체를 만들지 않는다 -
    /// 되돌릴 때 필요한 것은 전부 수(ChessMove) 안에 실려 있다.
    /// </summary>
    public sealed class ChessBoard
    {
        public readonly sbyte[] Squares = new sbyte[128];
        public ChessSide SideToMove;
        public int Castling;
        public int EnPassant = -1;
        public int Halfmove;
        public int Fullmove = 1;

        /// <summary>왕의 자리를 들고 다닌다. 체크 판정이 가장 잦은 질문이라서.</summary>
        public readonly int[] KingSquare = { -1, -1 };

        public ChessSide Opponent
        {
            get { return SideToMove == ChessSide.White ? ChessSide.Black : ChessSide.White; }
        }

        public static ChessSide Other(ChessSide side)
        {
            return side == ChessSide.White ? ChessSide.Black : ChessSide.White;
        }

        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static ChessBoard Start()
        {
            return FromFen(StartFen);
        }

        public ChessBoard Clone()
        {
            ChessBoard copy = new ChessBoard
            {
                SideToMove = SideToMove,
                Castling = Castling,
                EnPassant = EnPassant,
                Halfmove = Halfmove,
                Fullmove = Fullmove,
            };

            Array.Copy(Squares, copy.Squares, Squares.Length);
            copy.KingSquare[0] = KingSquare[0];
            copy.KingSquare[1] = KingSquare[1];
            return copy;
        }

        // ---------- FEN ----------

        public static ChessBoard FromFen(string fen)
        {
            ChessBoard board = new ChessBoard();
            string[] parts = fen.Trim().Split(' ');

            int rank = 7;
            int file = 0;

            foreach (char c in parts[0])
            {
                if (c == '/') { rank--; file = 0; continue; }

                if (c >= '1' && c <= '8') { file += c - '0'; continue; }

                sbyte piece = Decode(c);
                int square = Chess88.Square(file, rank);
                board.Squares[square] = piece;

                if (Piece.Kind(piece) == Piece.King)
                    board.KingSquare[(int)Piece.SideOf(piece)] = square;

                file++;
            }

            board.SideToMove = parts.Length > 1 && parts[1] == "b" ? ChessSide.Black : ChessSide.White;

            board.Castling = 0;
            if (parts.Length > 2 && parts[2] != "-")
            {
                if (parts[2].IndexOf('K') >= 0) board.Castling |= Castle.WhiteKing;
                if (parts[2].IndexOf('Q') >= 0) board.Castling |= Castle.WhiteQueen;
                if (parts[2].IndexOf('k') >= 0) board.Castling |= Castle.BlackKing;
                if (parts[2].IndexOf('q') >= 0) board.Castling |= Castle.BlackQueen;
            }

            board.EnPassant = parts.Length > 3 && parts[3] != "-"
                ? Chess88.Square(parts[3][0] - 'a', parts[3][1] - '1')
                : -1;

            if (parts.Length > 4) int.TryParse(parts[4], out board.Halfmove);
            if (parts.Length > 5) int.TryParse(parts[5], out board.Fullmove);

            return board;
        }

        private static sbyte Decode(char c)
        {
            int kind;
            switch (char.ToLowerInvariant(c))
            {
                case 'p': kind = Piece.Pawn; break;
                case 'n': kind = Piece.Knight; break;
                case 'b': kind = Piece.Bishop; break;
                case 'r': kind = Piece.Rook; break;
                case 'q': kind = Piece.Queen; break;
                case 'k': kind = Piece.King; break;
                default: return Piece.None;
            }

            return Piece.Make(kind, char.IsUpper(c) ? ChessSide.White : ChessSide.Black);
        }

        public string ToFen()
        {
            StringBuilder text = new StringBuilder();

            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;

                for (int file = 0; file < 8; file++)
                {
                    sbyte piece = Squares[Chess88.Square(file, rank)];

                    if (piece == Piece.None) { empty++; continue; }

                    if (empty > 0) { text.Append(empty); empty = 0; }
                    text.Append(Piece.Letter(piece));
                }

                if (empty > 0) text.Append(empty);
                if (rank > 0) text.Append('/');
            }

            text.Append(SideToMove == ChessSide.White ? " w " : " b ");

            if (Castling == 0) text.Append('-');
            else
            {
                if ((Castling & Castle.WhiteKing) != 0) text.Append('K');
                if ((Castling & Castle.WhiteQueen) != 0) text.Append('Q');
                if ((Castling & Castle.BlackKing) != 0) text.Append('k');
                if ((Castling & Castle.BlackQueen) != 0) text.Append('q');
            }

            text.Append(' ');
            text.Append(EnPassant >= 0 ? Chess88.Name(EnPassant) : "-");
            text.Append(' ').Append(Halfmove).Append(' ').Append(Fullmove);

            return text.ToString();
        }

        // ---------- 두기 / 되돌리기 ----------

        public void Make(ref ChessMove move)
        {
            move.PrevCastling = Castling;
            move.PrevEnPassant = EnPassant;
            move.PrevHalfmove = Halfmove;

            sbyte piece = Squares[move.From];
            int kind = Piece.Kind(piece);
            ChessSide side = SideToMove;

            // 앙파상은 잡히는 폰이 도착칸에 없다.
            int capturedAt = move.IsEnPassant
                ? move.To + (side == ChessSide.White ? -16 : 16)
                : move.To;

            move.Captured = Squares[capturedAt];
            if (move.Captured != Piece.None) Squares[capturedAt] = Piece.None;

            Squares[move.From] = Piece.None;
            Squares[move.To] = move.Promotion != 0 ? Piece.Make(move.Promotion, side) : piece;

            if (kind == Piece.King)
            {
                KingSquare[(int)side] = move.To;

                if (move.IsCastle)
                {
                    // 킹이 두 칸 갔으면 룩도 같이 넘어간다.
                    bool kingSide = Chess88.File(move.To) == 6;
                    int rookFrom = kingSide ? move.To + 1 : move.To - 2;
                    int rookTo = kingSide ? move.To - 1 : move.To + 1;

                    Squares[rookTo] = Squares[rookFrom];
                    Squares[rookFrom] = Piece.None;
                }
            }

            UpdateCastlingRights(move.From, move.To);

            EnPassant = move.IsDoublePush ? (move.From + move.To) / 2 : -1;

            if (kind == Piece.Pawn || move.Captured != Piece.None) Halfmove = 0;
            else Halfmove++;

            if (side == ChessSide.Black) Fullmove++;
            SideToMove = Other(side);
        }

        public void Unmake(in ChessMove move)
        {
            ChessSide side = Other(SideToMove);
            SideToMove = side;

            if (side == ChessSide.Black) Fullmove--;

            Castling = move.PrevCastling;
            EnPassant = move.PrevEnPassant;
            Halfmove = move.PrevHalfmove;

            sbyte moved = Squares[move.To];
            sbyte original = move.Promotion != 0 ? Piece.Make(Piece.Pawn, side) : moved;

            Squares[move.From] = original;
            Squares[move.To] = Piece.None;

            if (Piece.Kind(original) == Piece.King)
            {
                KingSquare[(int)side] = move.From;

                if (move.IsCastle)
                {
                    bool kingSide = Chess88.File(move.To) == 6;
                    int rookFrom = kingSide ? move.To + 1 : move.To - 2;
                    int rookTo = kingSide ? move.To - 1 : move.To + 1;

                    Squares[rookFrom] = Squares[rookTo];
                    Squares[rookTo] = Piece.None;
                }
            }

            if (move.Captured == Piece.None) return;

            int capturedAt = move.IsEnPassant
                ? move.To + (side == ChessSide.White ? -16 : 16)
                : move.To;

            Squares[capturedAt] = move.Captured;
        }

        /// <summary>
        /// 캐슬링 권리는 셋 중 하나로 사라진다 - 킹이 움직이거나, 룩이 움직이거나, 룩이 잡히거나.
        /// 어느 쪽이든 그 칸을 건드리기만 하면 되는 일이라 출발칸과 도착칸만 보면 된다.
        /// </summary>
        private void UpdateCastlingRights(int from, int to)
        {
            Castling &= ~RightsAt(from);
            Castling &= ~RightsAt(to);
        }

        private static int RightsAt(int square)
        {
            switch (square)
            {
                case 4: return Castle.WhiteKing | Castle.WhiteQueen;    // e1
                case 0: return Castle.WhiteQueen;                       // a1
                case 7: return Castle.WhiteKing;                        // h1
                case 116: return Castle.BlackKing | Castle.BlackQueen;  // e8
                case 112: return Castle.BlackQueen;                     // a8
                case 119: return Castle.BlackKing;                      // h8
                default: return 0;
            }
        }
    }
}
