namespace Poker.Core
{
    /// <summary>
    /// 카드 한 장은 0~51 의 정수다. 랭크는 <c>card % 13</c>(0이 2, 12가 A), 무늬는 <c>card / 13</c>.
    /// 구조체를 만들지 않은 이유는 하나뿐이다 - AI 가 초당 수십만 장을 돌려 본다.
    /// </summary>
    public static class Cards
    {
        public const int Count = 52;
        public const int None = -1;

        public const int Clubs = 0;
        public const int Diamonds = 1;
        public const int Hearts = 2;
        public const int Spades = 3;

        public const string RankLetters = "23456789TJQKA";

        public static int Rank(int card) { return card % 13; }
        public static int Suit(int card) { return card / 13; }
        public static int Of(int rank, int suit) { return suit * 13 + rank; }

        public static string Name(int card)
        {
            if (card < 0 || card >= Count) return "??";
            return RankLetters[Rank(card)].ToString() + "cdhs"[Suit(card)];
        }
    }

    /// <summary>
    /// 작고 빠른 난수(xorshift32). 시뮬레이션 전용이라 암호학적 성질은 필요 없고,
    /// 대신 같은 시드가 언제나 같은 열을 낸다는 것만 지킨다.
    /// </summary>
    public sealed class PokerRng
    {
        private uint state;

        public PokerRng(int seed)
        {
            state = (uint)seed;
            if (state == 0u) state = 0x9E3779B9u;
        }

        public uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>0 이상 <paramref name="bound"/> 미만.</summary>
        public int Next(int bound)
        {
            return bound <= 1 ? 0 : (int)(Next() % (uint)bound);
        }

        public double NextDouble()
        {
            return (Next() >> 8) / 16777216.0;
        }
    }

    /// <summary>한 벌. 섞은 순서는 시드가 정하므로 같은 판을 다시 세울 수 있다.</summary>
    public sealed class Deck
    {
        private readonly int[] cards = new int[Cards.Count];
        private int next;

        public Deck(int seed)
        {
            for (int i = 0; i < Cards.Count; i++) cards[i] = i;

            PokerRng rng = new PokerRng(seed);

            for (int i = Cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = cards[i];
                cards[i] = cards[j];
                cards[j] = swap;
            }
        }

        public int Remaining { get { return Cards.Count - next; } }

        public int Draw()
        {
            return next < Cards.Count ? cards[next++] : Cards.None;
        }
    }
}
