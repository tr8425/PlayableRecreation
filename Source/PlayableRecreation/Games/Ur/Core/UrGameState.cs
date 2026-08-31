using System;

namespace RoyalGameOfUr.Core
{
    /// <summary>
    /// 한 시점의 판 전체. 점유는 비트마스크(bit i = 경로 인덱스 i)로 들고 있어
    /// 구조체 대입만으로 완전 복사가 되고, 탐색 중 힙 할당이 발생하지 않는다.
    /// </summary>
    public struct UrGameState
    {
        /// <summary>플레이어 말 점유 비트마스크. 비트 1..14 사용.</summary>
        public ushort OccPlayer;

        /// <summary>봇 말 점유 비트마스크. 비트 1..14 사용.</summary>
        public ushort OccBot;

        public byte WaitingPlayer;
        public byte WaitingBot;
        public byte ScoredPlayer;
        public byte ScoredBot;

        public Side Turn;

        public static UrGameState NewGame(Side first)
        {
            UrGameState s = default(UrGameState);
            s.WaitingPlayer = UrBoardLayout.PieceCount;
            s.WaitingBot = UrBoardLayout.PieceCount;
            s.Turn = first;
            return s;
        }

        public ushort Occupancy(Side side)
        {
            return side == Side.Player ? OccPlayer : OccBot;
        }

        public byte Waiting(Side side)
        {
            return side == Side.Player ? WaitingPlayer : WaitingBot;
        }

        public byte Scored(Side side)
        {
            return side == Side.Player ? ScoredPlayer : ScoredBot;
        }

        public bool IsOccupied(Side side, int pathIndex)
        {
            return (Occupancy(side) & (1 << pathIndex)) != 0;
        }

        public void SetOccupied(Side side, int pathIndex, bool value)
        {
            int mask = 1 << pathIndex;
            if (side == Side.Player)
                OccPlayer = (ushort)(value ? (OccPlayer | mask) : (OccPlayer & ~mask));
            else
                OccBot = (ushort)(value ? (OccBot | mask) : (OccBot & ~mask));
        }

        public void SetWaiting(Side side, int value)
        {
            if (side == Side.Player) WaitingPlayer = (byte)value;
            else WaitingBot = (byte)value;
        }

        public void SetScored(Side side, int value)
        {
            if (side == Side.Player) ScoredPlayer = (byte)value;
            else ScoredBot = (byte)value;
        }

        public int OnBoardCount(Side side)
        {
            return PopCount(Occupancy(side));
        }

        private static int PopCount(ushort v)
        {
            int n = 0;
            while (v != 0) { v &= (ushort)(v - 1); n++; }
            return n;
        }

        /// <summary>진영별 말 총합이 항상 7인지 확인한다. 테스트/디버그용 불변식.</summary>
        public bool IsConsistent()
        {
            if ((OccPlayer & 1) != 0 || (OccBot & 1) != 0) return false;          // bit 0 은 미사용
            if ((OccPlayer >> 15) != 0 || (OccBot >> 15) != 0) return false;      // bit 15 이상 미사용
            int p = OnBoardCount(Side.Player) + WaitingPlayer + ScoredPlayer;
            int b = OnBoardCount(Side.Bot) + WaitingBot + ScoredBot;
            return p == UrBoardLayout.PieceCount && b == UrBoardLayout.PieceCount;
        }

        public override string ToString()
        {
            return string.Format("Turn={0} P(대기{1} 판{2} 골인{3}) B(대기{4} 판{5} 골인{6})",
                Turn, WaitingPlayer, OnBoardCount(Side.Player), ScoredPlayer,
                WaitingBot, OnBoardCount(Side.Bot), ScoredBot);
        }
    }
}
