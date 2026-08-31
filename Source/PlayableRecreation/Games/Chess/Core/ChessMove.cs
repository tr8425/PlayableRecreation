namespace Chess.Core
{
    /// <summary>
    /// 한 수. 되돌리기를 위해 잡은 기물과 직전 상태를 함께 들고 다닌다 -
    /// 탐색이 초당 수십만 번 되감으므로 별도의 스택보다 이쪽이 싸다.
    /// </summary>
    public struct ChessMove
    {
        public int From;
        public int To;

        /// <summary>승격할 기물 종류. 0이면 승격 아님.</summary>
        public int Promotion;

        public bool IsEnPassant;
        public bool IsCastle;
        public bool IsDoublePush;

        /// <summary>잡힌 기물. 앙파상이면 실제로 사라진 폰이 들어간다.</summary>
        public sbyte Captured;

        // 되돌릴 때 복원할 직전 상태
        public int PrevCastling;
        public int PrevEnPassant;
        public int PrevHalfmove;

        public bool IsCapture
        {
            get { return Captured != Piece.None; }
        }

        public bool IsNull
        {
            get { return From == To; }
        }

        public override string ToString()
        {
            string text = Chess88.Name(From) + Chess88.Name(To);
            if (Promotion != 0) text += char.ToLowerInvariant(".pnbrqk"[Promotion]);
            return text;
        }
    }
}
