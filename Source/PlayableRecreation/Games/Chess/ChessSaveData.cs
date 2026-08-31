using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Chess.Core;
using PlayableRecreation;
using Verse;

namespace Chess
{
    /// <summary>
    /// 판 위에 남는 대국.
    ///
    /// 남기는 것은 수순이다. FEN 한 줄이면 위치는 다 담기지만 위치만으로는
    /// 기보도 무르기도 몇 수째인지도 되살릴 수 없다 - 그것들은 지나온 수에만 있다.
    /// 그래서 처음부터 다시 두어 판을 세운다.
    ///
    /// FEN 과 해시는 그대로 둔다. 기록이 상했거나 수순이 없는 예전 세이브가
    /// 물러설 자리가 필요하기 때문이다.
    /// </summary>
    public class ChessSaveData : MiniGameSaveData
    {
        public string fen = ChessBoard.StartFen;
        public bool playerBlack;

        /// <summary>16진수를 공백으로 이어 붙인 것. 세이브 파일에 숫자 목록을 늘어놓지 않으려는 것이다.</summary>
        public string keys = string.Empty;

        /// <summary>지나온 수. "e2e4" 하나가 한 수고, 승격은 글자가 하나 더 붙는다("e7e8q").</summary>
        public string moves = string.Empty;

        public ChessSaveData()
        {
        }

        public ChessSaveData(ChessGame game)
        {
            fen = game.Fen;
            playerBlack = game.PlayerSide == ChessSide.Black;

            List<ulong> history = game.HistoryKeys;
            StringBuilder text = new StringBuilder();

            for (int i = 0; i < history.Count; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(history[i].ToString("x", CultureInfo.InvariantCulture));
            }

            keys = text.ToString();

            IReadOnlyList<ChessMove> played = game.Played;
            StringBuilder line = new StringBuilder();

            for (int i = 0; i < played.Count; i++)
            {
                if (i > 0) line.Append(' ');
                line.Append(Encode(played[i]));
            }

            moves = line.ToString();
        }

        public ChessGame ToGame()
        {
            ChessSide side = playerBlack ? ChessSide.Black : ChessSide.White;

            List<ChessMove> played = ParseMoves();

            if (played != null && played.Count > 0)
            {
                ChessGame game = ChessGame.Replay(side, played);

                // 다시 둔 결과가 저장된 위치와 같을 때만 받아들인다.
                if (game != null && game.Fen == fen) return game;
            }

            return ChessGame.Restore(fen, side, ParseKeys());
        }

        /// <summary>한 수라도 못 읽으면 통째로 버린다. 반쯤 읽은 수순은 틀린 판을 만든다.</summary>
        private List<ChessMove> ParseMoves()
        {
            if (moves.NullOrEmpty()) return null;

            string[] parts = moves.Split(' ');
            List<ChessMove> parsed = new List<ChessMove>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                ChessMove move;
                if (!TryDecode(parts[i], out move)) return null;

                parsed.Add(move);
            }

            return parsed;
        }

        private static string Encode(ChessMove move)
        {
            string text = Chess88.Name(move.From) + Chess88.Name(move.To);
            if (move.Promotion == 0) return text;

            return text + char.ToLowerInvariant(Piece.Letter((sbyte)move.Promotion));
        }

        private static bool TryDecode(string text, out ChessMove move)
        {
            move = new ChessMove();
            if (text == null || text.Length < 4 || text.Length > 5) return false;

            int from, to;
            if (!TrySquare(text, 0, out from)) return false;
            if (!TrySquare(text, 2, out to)) return false;

            int promotion = 0;

            if (text.Length == 5)
            {
                promotion = PromotionLetters.IndexOf(char.ToLowerInvariant(text[4]));
                if (promotion < Piece.Knight) return false;
            }

            move = new ChessMove { From = from, To = to, Promotion = promotion };
            return true;
        }

        /// <summary>자리가 곧 기물 코드다. 앞의 두 칸은 빈 칸과 폰의 자리로 비워 둔다.</summary>
        private const string PromotionLetters = "..nbrq";

        private static bool TrySquare(string text, int at, out int square)
        {
            square = 0;

            int file = text[at] - 'a';
            int rank = text[at + 1] - '1';

            if (file < 0 || file > 7 || rank < 0 || rank > 7) return false;

            square = Chess88.Square(file, rank);
            return true;
        }

        private List<ulong> ParseKeys()
        {
            List<ulong> parsed = new List<ulong>();
            if (keys.NullOrEmpty()) return parsed;

            string[] parts = keys.Split(' ');

            for (int i = 0; i < parts.Length; i++)
            {
                ulong value;
                if (ulong.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    parsed.Add(value);
            }

            return parsed;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref fen, "fen", ChessBoard.StartFen);
            Scribe_Values.Look(ref playerBlack, "playerBlack", false);
            Scribe_Values.Look(ref keys, "keys", string.Empty);
            Scribe_Values.Look(ref moves, "moves", string.Empty);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (fen.NullOrEmpty()) fen = ChessBoard.StartFen;
                if (keys == null) keys = string.Empty;
                if (moves == null) moves = string.Empty;
            }
        }
    }
}
