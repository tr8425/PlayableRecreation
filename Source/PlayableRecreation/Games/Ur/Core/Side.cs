namespace Ur.Core
{
    /// <summary>대국의 두 진영. 상대는 항상 AI 봇이다.</summary>
    public enum Side : byte
    {
        /// <summary>플레이어. 보드 하단(row 2)을 자기 진영으로 쓴다.</summary>
        Player = 0,

        /// <summary>AI 봇. 보드 상단(row 0)을 자기 진영으로 쓴다.</summary>
        Bot = 1,
    }

    public static class SideExtensions
    {
        public static Side Opponent(this Side side)
        {
            return side == Side.Player ? Side.Bot : Side.Player;
        }
    }
}
