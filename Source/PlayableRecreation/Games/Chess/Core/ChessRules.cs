namespace Chess.Core
{
    public enum ChessResult
    {
        Ongoing,
        Checkmate,
        Stalemate,
        FiftyMove,
        Repetition,
        InsufficientMaterial,
    }

    /// <summary>
    /// 수 생성과 합법성. 유사합법수를 만든 뒤 실제로 두어 보고 자기 왕이 잡히는지로 거른다 -
    /// 핀을 따로 계산하는 것보다 느리지만, 틀릴 구석이 없다.
    ///
    /// 이 파일이 맞는지는 perft 로 증명한다. 공표된 수치와 노드 수가 일치하면 맞는 것이다.
    /// </summary>
    public static class ChessRules
    {
        public const int MaxMoves = 256;

        // ---------- 공격 판정 ----------

        public static bool InCheck(ChessBoard board, ChessSide side)
        {
            int king = board.KingSquare[(int)side];
            return king >= 0 && IsAttacked(board, king, ChessBoard.Other(side));
        }

        /// <summary>그 칸이 <paramref name="by"/> 진영에게 공격받고 있는가.</summary>
        public static bool IsAttacked(ChessBoard board, int square, ChessSide by)
        {
            sbyte[] squares = board.Squares;
            bool white = by == ChessSide.White;

            // 폰. 백이 공격한다면 그 폰은 한 칸 아래 대각선에 있다.
            int back = white ? -16 : 16;
            sbyte pawn = Piece.Make(Piece.Pawn, by);

            int left = square + back - 1;
            int right = square + back + 1;
            if (Chess88.OnBoard(left) && squares[left] == pawn) return true;
            if (Chess88.OnBoard(right) && squares[right] == pawn) return true;

            sbyte knight = Piece.Make(Piece.Knight, by);
            for (int i = 0; i < Offsets.Knight.Length; i++)
            {
                int at = square + Offsets.Knight[i];
                if (Chess88.OnBoard(at) && squares[at] == knight) return true;
            }

            sbyte king = Piece.Make(Piece.King, by);
            for (int i = 0; i < Offsets.King.Length; i++)
            {
                int at = square + Offsets.King[i];
                if (Chess88.OnBoard(at) && squares[at] == king) return true;
            }

            if (SlideHits(squares, square, Offsets.Bishop, by, Piece.Bishop)) return true;
            if (SlideHits(squares, square, Offsets.Rook, by, Piece.Rook)) return true;

            return false;
        }

        /// <summary>그 방향으로 처음 만나는 기물이 해당 종류나 퀸이면 공격이다.</summary>
        private static bool SlideHits(sbyte[] squares, int from, int[] directions, ChessSide by, int kind)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                int at = from + directions[i];

                while (Chess88.OnBoard(at))
                {
                    sbyte piece = squares[at];

                    if (piece != Piece.None)
                    {
                        if (Piece.SideOf(piece) == by)
                        {
                            int found = Piece.Kind(piece);
                            if (found == kind || found == Piece.Queen) return true;
                        }

                        break;
                    }

                    at += directions[i];
                }
            }

            return false;
        }

        // ---------- 수 생성 ----------

        /// <summary>합법수만. 유사합법수를 만들고 두어 본 뒤 자기 왕이 걸리는 것을 뺀다.</summary>
        public static int GenerateLegal(ChessBoard board, ChessMove[] into)
        {
            int pseudo = GeneratePseudo(board, into, false);
            int legal = 0;

            for (int i = 0; i < pseudo; i++)
            {
                ChessMove move = into[i];
                ChessSide mover = board.SideToMove;

                board.Make(ref move);
                bool ok = !InCheck(board, mover);
                board.Unmake(in move);

                if (ok) into[legal++] = move;
            }

            return legal;
        }

        public static int GeneratePseudo(ChessBoard board, ChessMove[] into, bool capturesOnly)
        {
            int count = 0;
            ChessSide side = board.SideToMove;
            sbyte[] squares = board.Squares;

            for (int square = 0; square < 128; square++)
            {
                if (!Chess88.OnBoard(square)) continue;

                sbyte piece = squares[square];
                if (piece == Piece.None || Piece.SideOf(piece) != side) continue;

                switch (Piece.Kind(piece))
                {
                    case Piece.Pawn: count = Pawns(board, square, into, count, capturesOnly); break;
                    case Piece.Knight: count = Steps(board, square, Offsets.Knight, into, count, capturesOnly); break;
                    case Piece.Bishop: count = Slides(board, square, Offsets.Bishop, into, count, capturesOnly); break;
                    case Piece.Rook: count = Slides(board, square, Offsets.Rook, into, count, capturesOnly); break;
                    case Piece.Queen: count = Slides(board, square, Offsets.Queen, into, count, capturesOnly); break;
                    default:
                        count = Steps(board, square, Offsets.King, into, count, capturesOnly);
                        if (!capturesOnly) count = Castles(board, square, into, count);
                        break;
                }
            }

            return count;
        }

        private static int Steps(ChessBoard board, int from, int[] offsets,
                                 ChessMove[] into, int count, bool capturesOnly)
        {
            ChessSide side = board.SideToMove;

            for (int i = 0; i < offsets.Length; i++)
            {
                int to = from + offsets[i];
                if (!Chess88.OnBoard(to)) continue;

                sbyte target = board.Squares[to];
                if (target != Piece.None && Piece.SideOf(target) == side) continue;
                if (capturesOnly && target == Piece.None) continue;

                into[count++] = new ChessMove { From = from, To = to };
            }

            return count;
        }

        private static int Slides(ChessBoard board, int from, int[] directions,
                                  ChessMove[] into, int count, bool capturesOnly)
        {
            ChessSide side = board.SideToMove;

            for (int i = 0; i < directions.Length; i++)
            {
                int to = from + directions[i];

                while (Chess88.OnBoard(to))
                {
                    sbyte target = board.Squares[to];

                    if (target == Piece.None)
                    {
                        if (!capturesOnly) into[count++] = new ChessMove { From = from, To = to };
                        to += directions[i];
                        continue;
                    }

                    if (Piece.SideOf(target) != side)
                        into[count++] = new ChessMove { From = from, To = to };

                    break;
                }
            }

            return count;
        }

        private static int Pawns(ChessBoard board, int from, ChessMove[] into, int count, bool capturesOnly)
        {
            ChessSide side = board.SideToMove;
            bool white = side == ChessSide.White;

            int forward = white ? 16 : -16;
            int startRank = white ? 1 : 6;
            int lastRank = white ? 7 : 0;

            int ahead = from + forward;

            if (!capturesOnly && Chess88.OnBoard(ahead) && board.Squares[ahead] == Piece.None)
            {
                count = PawnMove(into, count, from, ahead, lastRank, false, false);

                int twice = ahead + forward;
                if (Chess88.Rank(from) == startRank && board.Squares[twice] == Piece.None)
                    into[count++] = new ChessMove { From = from, To = twice, IsDoublePush = true };
            }

            for (int side8 = -1; side8 <= 1; side8 += 2)
            {
                int to = ahead + side8;
                if (!Chess88.OnBoard(to)) continue;

                sbyte target = board.Squares[to];

                if (target != Piece.None && Piece.SideOf(target) != side)
                {
                    count = PawnMove(into, count, from, to, lastRank, false, false);
                    continue;
                }

                if (target == Piece.None && to == board.EnPassant)
                    into[count++] = new ChessMove { From = from, To = to, IsEnPassant = true };
            }

            return count;
        }

        /// <summary>마지막 줄에 닿는 폰은 네 갈래가 된다.</summary>
        private static int PawnMove(ChessMove[] into, int count, int from, int to,
                                    int lastRank, bool enPassant, bool doublePush)
        {
            if (Chess88.Rank(to) != lastRank)
            {
                into[count++] = new ChessMove
                {
                    From = from, To = to, IsEnPassant = enPassant, IsDoublePush = doublePush,
                };
                return count;
            }

            int[] promotions = { Piece.Queen, Piece.Rook, Piece.Bishop, Piece.Knight };
            for (int i = 0; i < promotions.Length; i++)
                into[count++] = new ChessMove { From = from, To = to, Promotion = promotions[i] };

            return count;
        }

        /// <summary>
        /// 캐슬링. 킹은 체크 중에도, 지나가는 칸이 공격받고 있어도, 도착칸이 공격받아도 안 된다.
        /// 퀸사이드에서 b칸은 비어 있기만 하면 되고 공격받아도 상관없다.
        /// </summary>
        private static int Castles(ChessBoard board, int from, ChessMove[] into, int count)
        {
            ChessSide side = board.SideToMove;
            bool white = side == ChessSide.White;

            int home = white ? 4 : 116;
            if (from != home) return count;

            ChessSide enemy = ChessBoard.Other(side);
            if (IsAttacked(board, home, enemy)) return count;

            int kingRight = white ? Castle.WhiteKing : Castle.BlackKing;
            int queenRight = white ? Castle.WhiteQueen : Castle.BlackQueen;

            if ((board.Castling & kingRight) != 0
                && board.Squares[home + 1] == Piece.None
                && board.Squares[home + 2] == Piece.None
                && !IsAttacked(board, home + 1, enemy)
                && !IsAttacked(board, home + 2, enemy))
                into[count++] = new ChessMove { From = home, To = home + 2, IsCastle = true };

            if ((board.Castling & queenRight) != 0
                && board.Squares[home - 1] == Piece.None
                && board.Squares[home - 2] == Piece.None
                && board.Squares[home - 3] == Piece.None
                && !IsAttacked(board, home - 1, enemy)
                && !IsAttacked(board, home - 2, enemy))
                into[count++] = new ChessMove { From = home, To = home - 2, IsCastle = true };

            return count;
        }

        // ---------- 판정 ----------

        public static ChessResult Result(ChessBoard board, int legalCount, int repetitions)
        {
            if (legalCount == 0)
                return InCheck(board, board.SideToMove) ? ChessResult.Checkmate : ChessResult.Stalemate;

            if (board.Halfmove >= 100) return ChessResult.FiftyMove;
            if (repetitions >= 3) return ChessResult.Repetition;
            if (InsufficientMaterial(board)) return ChessResult.InsufficientMaterial;

            return ChessResult.Ongoing;
        }

        /// <summary>
        /// 어느 쪽도 이길 수 없는 잔여 기물. 폰·룩·퀸이 하나라도 남아 있으면 아니고,
        /// 양쪽 다 마이너 하나 이하이면 그렇다.
        /// </summary>
        public static bool InsufficientMaterial(ChessBoard board)
        {
            int[] minors = { 0, 0 };

            for (int square = 0; square < 128; square++)
            {
                if (!Chess88.OnBoard(square)) continue;

                sbyte piece = board.Squares[square];
                if (piece == Piece.None) continue;

                int kind = Piece.Kind(piece);
                if (kind == Piece.King) continue;

                if (kind == Piece.Pawn || kind == Piece.Rook || kind == Piece.Queen) return false;

                if (++minors[(int)Piece.SideOf(piece)] > 1) return false;
            }

            return true;
        }

        /// <summary>
        /// 수 생성이 맞는지 증명하는 유일한 방법. 깊이별 잎 노드 수는 공표된 값이 있으므로
        /// 이 숫자가 맞으면 핀·앙파상·캐슬링·승격이 전부 맞는 것이다.
        /// </summary>
        public static long Perft(ChessBoard board, int depth)
        {
            if (depth <= 0) return 1;

            ChessMove[] moves = new ChessMove[MaxMoves];
            int count = GenerateLegal(board, moves);

            if (depth == 1) return count;

            long nodes = 0;

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                board.Make(ref move);
                nodes += Perft(board, depth - 1);
                board.Unmake(in move);
            }

            return nodes;
        }
    }
}
