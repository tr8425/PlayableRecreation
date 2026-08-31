using System;
using Poker.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 일곱 장 중 실제로 쓰인 다섯 장.
    ///
    /// 여기서 지키는 것은 하나다 - 골라낸 다섯 장의 점수가 일곱 장 전체의 점수와 같아야 한다.
    /// 어긋나면 쇼다운에서 이긴 손과 다른 카드에 테두리가 쳐진다는 뜻이다.
    /// </summary>
    public class PokerBestFiveTests
    {
        private static int Card(string text)
        {
            int rank = Cards.RankLetters.IndexOf(char.ToUpperInvariant(text[0]));
            int suit = "cdhs".IndexOf(char.ToLowerInvariant(text[1]));

            Assert.True(rank >= 0 && suit >= 0, "bad card " + text);
            return Cards.Of(rank, suit);
        }

        private static int[] Seven(params string[] names)
        {
            int[] cards = new int[names.Length];
            for (int i = 0; i < names.Length; i++) cards[i] = Card(names[i]);

            return cards;
        }

        [Fact]
        public void 고른_다섯_장이_일곱_장의_점수와_같다()
        {
            int[] cards = Seven("As", "Ks", "Qs", "Js", "Ts", "2c", "3d");
            int[] five = new int[5];

            Assert.Equal(HandEval.Score(cards, 7), HandEval.BestFive(cards, 7, five));
            Assert.Equal(HandCategory.StraightFlush, HandEval.Category(HandEval.Score(five, 5)));
        }

        [Fact]
        public void 풀하우스에서는_남는_두_장을_버린다()
        {
            int[] cards = Seven("9c", "9d", "9h", "8c", "8d", "As", "2h");
            int[] five = new int[5];

            HandEval.BestFive(cards, 7, five);

            Assert.Equal(HandCategory.FullHouse, HandEval.Category(HandEval.Score(five, 5)));

            // 에이스는 킥커가 될 자리가 없다. 들어가면 안 된다.
            Assert.DoesNotContain(Card("As"), five);
            Assert.DoesNotContain(Card("2h"), five);
        }

        [Fact]
        public void 무작위_판_전부에서_점수가_어긋나지_않는다()
        {
            Random rng = new Random(4242);
            int[] cards = new int[7];
            int[] five = new int[5];

            for (int round = 0; round < 3000; round++)
            {
                // 겹치지 않게 일곱 장.
                bool[] taken = new bool[Cards.Count];
                for (int i = 0; i < 7; i++)
                {
                    int card;
                    do { card = rng.Next(Cards.Count); } while (taken[card]);

                    taken[card] = true;
                    cards[i] = card;
                }

                int whole = HandEval.Score(cards, 7);
                int best = HandEval.BestFive(cards, 7, five);

                Assert.Equal(whole, best);
                Assert.Equal(whole, HandEval.Score(five, 5));

                // 다섯 장은 서로 달라야 하고, 전부 그 일곱 장 안에 있어야 한다.
                for (int i = 0; i < 5; i++)
                {
                    Assert.Contains(five[i], cards);
                    for (int j = i + 1; j < 5; j++) Assert.NotEqual(five[i], five[j]);
                }
            }
        }

        [Fact]
        public void 다섯_장이_안_되면_고를_것도_없다()
        {
            int[] cards = Seven("As", "Ks", "Qs", "Js");
            int[] five = new int[5];

            Assert.Equal(-1, HandEval.BestFive(cards, 4, five));
            Assert.Equal(-1, HandEval.BestFive(null, 7, five));
            Assert.Equal(-1, HandEval.BestFive(cards, 7, null));
        }
    }
}
