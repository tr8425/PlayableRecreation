namespace Ur.Core
{
    /// <summary>한 수. 값 타입이라 탐색 중 복사 비용이 없다.</summary>
    public readonly struct UrMove
    {
        /// <summary>From 이 이 값이면 대기열에서 보드로 투입하는 수(R2).</summary>
        public const int FromWaiting = 0;

        public readonly byte From;
        public readonly byte To;

        /// <summary>공유 전장에서 상대 말을 잡는 수인가(R5).</summary>
        public readonly bool IsCapture;

        /// <summary>로제트 착지로 추가 턴을 얻는가(R7).</summary>
        public readonly bool GrantsExtraTurn;

        public UrMove(int from, int to, bool isCapture, bool grantsExtraTurn)
        {
            From = (byte)from;
            To = (byte)to;
            IsCapture = isCapture;
            GrantsExtraTurn = grantsExtraTurn;
        }

        public bool IsEntry { get { return From == FromWaiting; } }

        public bool IsBearOff { get { return To == UrBoardLayout.ScoredIndex; } }

        public override string ToString()
        {
            string src = IsEntry ? "투입" : From.ToString();
            string dst = IsBearOff ? "골인" : To.ToString();
            string tag = IsCapture ? " x잡기" : (GrantsExtraTurn ? " *로제트" : "");
            return src + "->" + dst + tag;
        }
    }
}
