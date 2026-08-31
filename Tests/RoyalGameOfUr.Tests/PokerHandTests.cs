using Poker.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 손의 값매김. 포커에서 조용히 틀리기 좋은 곳이 여기다 -
    /// 킥커 하나를 빠뜨려도 게임은 잘 돌아가고, 다만 가끔 엉뚱한 쪽이 팟을 가져간다.
    /// </summary>
    public class PokerHandTests
    {
        /// <summary>"Ah Kd 7c" 처럼 읽어 카드 배열로 바꾼다.</summary>
        private static int[] Parse(string text)
        {
            string[] parts = text.Split(' ');
            int[] cards = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                int rank = Cards.RankLetters.IndexOf(char.ToUpperInvariant(parts[i][0]));
                int suit = "cdhs".IndexOf(char.ToLowerInvariant(parts[i][1]));

                Assert.True(rank >= 0 && suit >= 0, "bad card: " + parts[i]);
                cards[i] = Cards.Of(rank, suit);
            }

            return cards;
        }

        private static int Score(string text)
        {
            int[] cards = Parse(text);
            return HandEval.Score(cards, cards.Length);
        }

        private static HandCategory Kind(string text)
        {
            return HandEval.Category(Score(text));
        }

        [Theory]
        [InlineData("Ah Kh Qh Jh Th 2c 3d", HandCategory.StraightFlush)]
        [InlineData("5h 4h 3h 2h Ah Kd Qc", HandCategory.StraightFlush)]
        [InlineData("9c 9d 9h 9s 2c 3d 4h", HandCategory.Quads)]
        [InlineData("8c 8d 8h 3s 3c 2d 7h", HandCategory.FullHouse)]
        [InlineData("Ac Jc 9c 6c 3c 2d 4h", HandCategory.Flush)]
        [InlineData("9c 8d 7h 6s 5c 2d 3h", HandCategory.Straight)]
        [InlineData("Ah 2d 3c 4s 5h 9c Kd", HandCategory.Straight)]
        [InlineData("Qc Qd Qh 8s 5c 2d 3h", HandCategory.Trips)]
        [InlineData("Kc Kd 7h 7s 5c 2d 3h", HandCategory.TwoPair)]
        [InlineData("Kc Kd 9h 7s 5c 2d 3h", HandCategory.Pair)]
        [InlineData("Ac Kd 9h 7s 5c 3d 2h", HandCategory.HighCard)]
        public void 등급을_알아본다(string cards, HandCategory expected)
        {
            Assert.Equal(expected, Kind(cards));
        }

        [Fact]
        public void 등급_사이의_순서가_맞다()
        {
            string[] ascending =
            {
                "Ac Kd 9h 7s 5c 3d 2h",   // 하이카드
                "2c 2d 9h 7s 5c 3d 4h",   // 원페어
                "2c 2d 3h 3s 5c 9d 7h",   // 투페어
                "2c 2d 2h 9s 5c 3d 4h",   // 트립
                "9c 8d 7h 6s 5c 2d 3h",   // 스트레이트
                "Ac Jc 9c 6c 3c 2d 4h",   // 플러시
                "8c 8d 8h 3s 3c 2d 7h",   // 풀하우스
                "9c 9d 9h 9s 2c 3d 4h",   // 포카드
                "Ah Kh Qh Jh Th 2c 3d",   // 스트레이트플러시
            };

            for (int i = 1; i < ascending.Length; i++)
                Assert.True(Score(ascending[i]) > Score(ascending[i - 1]),
                            ascending[i] + " should beat " + ascending[i - 1]);
        }

        [Fact]
        public void 킥커까지_따진다()
        {
            // 같은 원페어, 킥커만 다르다.
            Assert.True(Score("Kc Kd Ah 7s 5c 2d 3h") > Score("Kc Kd Qh 7s 5c 2d 3h"));

            // 같은 투페어, 마지막 한 장이 갈랐다.
            Assert.True(Score("Kc Kd 7h 7s Ac 2d 3h") > Score("Kc Kd 7h 7s Qc 2d 3h"));

            // 같은 트립, 킥커 둘 중 아래 것이 갈랐다.
            Assert.True(Score("9c 9d 9h Ac 8s 2d 3h") > Score("9c 9d 9h Ac 7s 2d 3h"));

            // 완전히 같은 손은 같은 값이다. 무늬는 순위를 가르지 않는다.
            Assert.Equal(Score("Kc Kd 7h 7s Ac 2d 3h"), Score("Kh Ks 7c 7d As 2c 3s"));
        }

        [Fact]
        public void 다섯장과_일곱장이_같은_손을_고른다()
        {
            // 남는 두 장은 손을 나쁘게 만들 수 없다.
            Assert.Equal(Score("Ah Kh Qh Jh Th"), Score("Ah Kh Qh Jh Th 2c 3d"));
            Assert.Equal(Score("8c 8d 8h 3s 3c"), Score("8c 8d 8h 3s 3c 2d 7h"));
        }

        [Fact]
        public void 트립이_둘이면_풀하우스가_된다()
        {
            // 999 + 888 은 999 에 88 을 붙인 풀하우스다. 888 쪽은 페어로만 쓰인다.
            int score = Score("9c 9d 9h 8c 8d 8h 2s");
            Assert.Equal(HandCategory.FullHouse, HandEval.Category(score));

            // 999 에 88 을 붙인 것과 정확히 같은 손이다.
            Assert.Equal(Score("9c 9d 9h 8c 8d 2h 3s"), score);

            // 더 낮은 페어를 붙인 쪽보다는 낫다.
            Assert.True(score > Score("9c 9d 9h 3c 3d 2h 4s"));
        }

        [Fact]
        public void 스트레이트를_비트로_찾는다()
        {
            Assert.Equal(-1, HandEval.StraightHigh(0));
            Assert.Equal(3, HandEval.StraightHigh((1 << 12) | 1 | 2 | 4 | 8));   // A2345
            Assert.Equal(12, HandEval.StraightHigh(0x1F00));                      // TJQKA
            Assert.Equal(-1, HandEval.StraightHigh((1 << 12) | 1 | 2 | 4));       // A234 뿐
        }
    }
}
