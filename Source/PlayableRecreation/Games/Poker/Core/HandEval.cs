namespace Poker.Core
{
    public enum HandCategory
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        Trips = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        Quads = 7,
        StraightFlush = 8,
    }

    /// <summary>
    /// 5~7장에서 가장 좋은 다섯 장의 값을 매긴다. 결과는 그냥 <c>int</c> 하나이고,
    /// 큰 쪽이 이긴다 - 등급을 13진수 다섯 자리 위에 얹은 것이다.
    ///
    /// 손을 실제로 골라내지 않는다는 것이 핵심이다. 어느 다섯 장인지는 아무도 묻지 않고,
    /// 누가 이겼는지만 물으므로 세는 것으로 충분하다.
    /// </summary>
    public static class HandEval
    {
        private const int Base = 13;
        private const int P1 = Base;
        private const int P2 = P1 * Base;
        private const int P3 = P2 * Base;
        private const int P4 = P3 * Base;
        private const int P5 = P4 * Base;

        public static HandCategory Category(int score)
        {
            return (HandCategory)(score / P5);
        }

        private static int Pack(HandCategory category, int a, int b, int c, int d, int e)
        {
            return (int)category * P5 + a * P4 + b * P3 + c * P2 + d * P1 + e;
        }

        /// <summary>연속한 다섯 랭크의 가장 높은 자리. 없으면 -1. A2345 는 5(랭크 3)로 친다.</summary>
        public static int StraightHigh(int rankMask)
        {
            for (int high = 12; high >= 4; high--)
            {
                int need = 0;
                for (int i = 0; i < 5; i++) need |= 1 << (high - i);
                if ((rankMask & need) == need) return high;
            }

            const int wheel = (1 << 12) | 1 | (1 << 1) | (1 << 2) | (1 << 3);
            return (rankMask & wheel) == wheel ? 3 : -1;
        }

        private static void Top(int mask, int skipA, int skipB, int[] into, int wanted)
        {
            int found = 0;

            for (int rank = 12; rank >= 0 && found < wanted; rank--)
            {
                if (rank == skipA || rank == skipB) continue;
                if ((mask & (1 << rank)) == 0) continue;

                into[found++] = rank;
            }

            while (found < wanted) into[found++] = 0;
        }

        public static int Score(int[] cards, int count)
        {
            int[] rankCount = new int[13];
            int[] suitCount = new int[4];
            int[] suitMask = new int[4];
            int rankMask = 0;

            for (int i = 0; i < count; i++)
            {
                int card = cards[i];
                if (card < 0) continue;

                int rank = Cards.Rank(card);
                int suit = Cards.Suit(card);

                rankCount[rank]++;
                suitCount[suit]++;
                suitMask[suit] |= 1 << rank;
                rankMask |= 1 << rank;
            }

            int flushSuit = -1;
            for (int suit = 0; suit < 4; suit++)
                if (suitCount[suit] >= 5) flushSuit = suit;

            if (flushSuit >= 0)
            {
                int high = StraightHigh(suitMask[flushSuit]);
                if (high >= 0) return Pack(HandCategory.StraightFlush, high, 0, 0, 0, 0);
            }

            // 같은 랭크가 몇 장씩인지. 높은 쪽부터 본다.
            int quad = -1, tripHigh = -1, tripLow = -1, pairHigh = -1, pairLow = -1;

            for (int rank = 12; rank >= 0; rank--)
            {
                int n = rankCount[rank];

                if (n == 4 && quad < 0) quad = rank;
                else if (n == 3) { if (tripHigh < 0) tripHigh = rank; else if (tripLow < 0) tripLow = rank; }
                else if (n == 2) { if (pairHigh < 0) pairHigh = rank; else if (pairLow < 0) pairLow = rank; }
            }

            int[] kickers = new int[5];

            if (quad >= 0)
            {
                Top(rankMask, quad, -1, kickers, 1);
                return Pack(HandCategory.Quads, quad, kickers[0], 0, 0, 0);
            }

            // 트립이 둘이면 낮은 쪽은 페어로 쓴다.
            int fullPair = tripLow >= 0 ? tripLow : pairHigh;
            if (tripHigh >= 0 && fullPair >= 0)
                return Pack(HandCategory.FullHouse, tripHigh, fullPair, 0, 0, 0);

            if (flushSuit >= 0)
            {
                Top(suitMask[flushSuit], -1, -1, kickers, 5);
                return Pack(HandCategory.Flush, kickers[0], kickers[1], kickers[2], kickers[3], kickers[4]);
            }

            int straight = StraightHigh(rankMask);
            if (straight >= 0) return Pack(HandCategory.Straight, straight, 0, 0, 0, 0);

            if (tripHigh >= 0)
            {
                Top(rankMask, tripHigh, -1, kickers, 2);
                return Pack(HandCategory.Trips, tripHigh, kickers[0], kickers[1], 0, 0);
            }

            if (pairHigh >= 0 && pairLow >= 0)
            {
                Top(rankMask, pairHigh, pairLow, kickers, 1);
                return Pack(HandCategory.TwoPair, pairHigh, pairLow, kickers[0], 0, 0);
            }

            if (pairHigh >= 0)
            {
                Top(rankMask, pairHigh, -1, kickers, 3);
                return Pack(HandCategory.Pair, pairHigh, kickers[0], kickers[1], kickers[2], 0);
            }

            Top(rankMask, -1, -1, kickers, 5);
            return Pack(HandCategory.HighCard, kickers[0], kickers[1], kickers[2], kickers[3], kickers[4]);
        }
    }
}
