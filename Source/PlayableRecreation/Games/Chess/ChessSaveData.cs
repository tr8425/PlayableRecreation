using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Chess.Core;
using PlayableRecreation;
using Verse;

namespace Chess
{
    /// <summary>
    /// 판 위에 남는 대국. FEN 한 줄이면 위치도 캐슬링 권리도 50수 규칙도 전부 담긴다.
    ///
    /// 지나온 해시를 따로 들고 다니는 이유는 하나뿐이다 - 되풀이 무승부.
    /// FEN 은 "지금"만 말해 주므로, 같은 자리에 세 번 왔다는 사실은 이쪽에 있어야 한다.
    /// </summary>
    public class ChessSaveData : MiniGameSaveData
    {
        public string fen = ChessBoard.StartFen;
        public bool playerBlack;

        /// <summary>16진수를 공백으로 이어 붙인 것. 세이브 파일에 숫자 목록을 늘어놓지 않으려는 것이다.</summary>
        public string keys = string.Empty;

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
        }

        public ChessGame ToGame()
        {
            return ChessGame.Restore(fen, playerBlack ? ChessSide.Black : ChessSide.White, ParseKeys());
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (fen.NullOrEmpty()) fen = ChessBoard.StartFen;
                if (keys == null) keys = string.Empty;
            }
        }
    }
}
