using System.Text;

namespace Chess.Core
{
    /// <summary>
    /// 기보 표기. 진행 기록에 "Nf3" 처럼 나가는 그 문자열을 만든다.
    /// 같은 칸에 갈 수 있는 기물이 둘이면 어느 쪽인지 밝혀 줘야 해서 합법수 목록이 필요하다.
    /// </summary>
    public static class ChessNotation
    {
        public static string San(ChessBoard board, ChessMove move, ChessMove[] legal, int legalCount)
        {
            if (move.IsCastle)
                return Suffix(board, move, Chess88.File(move.To) == 6 ? "O-O" : "O-O-O");

            sbyte piece = board.Squares[move.From];
            int kind = Piece.Kind(piece);
            bool capture = move.IsCapture || move.IsEnPassant
                           || board.Squares[move.To] != Piece.None;

            StringBuilder text = new StringBuilder();

            if (kind == Piece.Pawn)
            {
                if (capture) text.Append((char)('a' + Chess88.File(move.From))).Append('x');
                text.Append(Chess88.Name(move.To));
                if (move.Promotion != 0) text.Append('=').Append(".PNBRQK"[move.Promotion]);

                return Suffix(board, move, text.ToString());
            }

            text.Append(".PNBRQK"[kind]);
            text.Append(Disambiguate(board, move, kind, legal, legalCount));
            if (capture) text.Append('x');
            text.Append(Chess88.Name(move.To));

            return Suffix(board, move, text.ToString());
        }

        /// <summary>같은 종류의 다른 기물도 그 칸에 갈 수 있으면 출발 파일이나 랭크를 밝힌다.</summary>
        private static string Disambiguate(ChessBoard board, ChessMove move, int kind,
                                           ChessMove[] legal, int legalCount)
        {
            bool sameFile = false;
            bool sameRank = false;
            bool ambiguous = false;

            for (int i = 0; i < legalCount; i++)
            {
                ChessMove other = legal[i];
                if (other.To != move.To || other.From == move.From) continue;
                if (Piece.Kind(board.Squares[other.From]) != kind) continue;

                ambiguous = true;
                if (Chess88.File(other.From) == Chess88.File(move.From)) sameFile = true;
                if (Chess88.Rank(other.From) == Chess88.Rank(move.From)) sameRank = true;
            }

            if (!ambiguous) return string.Empty;
            if (!sameFile) return ((char)('a' + Chess88.File(move.From))).ToString();
            if (!sameRank) return ((char)('1' + Chess88.Rank(move.From))).ToString();

            return Chess88.Name(move.From);
        }

        /// <summary>두고 나서 상대가 체크인지 외통인지 확인한 뒤 붙인다.</summary>
        private static string Suffix(ChessBoard board, ChessMove move, string text)
        {
            ChessMove applied = move;
            board.Make(ref applied);

            bool check = ChessRules.InCheck(board, board.SideToMove);
            string mark = string.Empty;

            if (check)
            {
                ChessMove[] replies = new ChessMove[ChessRules.MaxMoves];
                mark = ChessRules.GenerateLegal(board, replies) == 0 ? "#" : "+";
            }

            board.Unmake(in applied);
            return text + mark;
        }
    }
}
