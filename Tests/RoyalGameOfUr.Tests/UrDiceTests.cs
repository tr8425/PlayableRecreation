using RoyalGameOfUr.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    public class UrDiceTests
    {
        [Fact]
        public void 같은_시드와_순번은_항상_같은_눈을_준다()
        {
            for (int seed = -50; seed < 50; seed++)
                for (int i = 0; i < 200; i++)
                    Assert.Equal(UrDice.Roll(seed, i).Faces, UrDice.Roll(seed, i).Faces);
        }

        [Fact]
        public void 눈은_항상_0에서_4_사이다()
        {
            for (int seed = 0; seed < 200; seed++)
                for (int i = 0; i < 200; i++)
                {
                    int total = UrDice.Roll(seed, i).Total;
                    Assert.InRange(total, 0, 4);
                }
        }

        [Fact]
        public void 주사위_4개의_합이_Total과_일치한다()
        {
            for (int i = 0; i < 2000; i++)
            {
                var roll = UrDice.Roll(12345, i);
                int manual = 0;
                for (int d = 0; d < UrDice.DiceCount; d++)
                    if (roll.Die(d)) manual++;

                Assert.Equal(manual, roll.Total);
            }
        }

        [Fact]
        public void 분포가_이항분포_1_4_6_4_1에_수렴한다()
        {
            const int n = 320000;
            var counts = new int[5];

            for (int i = 0; i < n; i++)
                counts[UrDice.Roll(20260830, i).Total]++;

            for (int face = 0; face <= 4; face++)
            {
                double expected = n * UrDice.Probability[face];
                double error = System.Math.Abs(counts[face] - expected) / expected;
                Assert.True(error < 0.05,
                    $"눈 {face}: 기대 {expected:F0}, 실제 {counts[face]} (오차 {error:P2})");
            }
        }

        [Fact]
        public void 시드를_바꾸면_다른_수열이_나온다()
        {
            int differences = 0;
            for (int i = 0; i < 500; i++)
                if (UrDice.Roll(1, i).Faces != UrDice.Roll(2, i).Faces) differences++;

            Assert.True(differences > 300, $"시드 간 차이가 너무 적다: {differences}/500");
        }

        [Fact]
        public void 스트림은_정적_함수와_동일한_결과를_준다()
        {
            var stream = new UrDiceStream(777);
            for (int i = 0; i < 500; i++)
                Assert.Equal(UrDice.Roll(777, i).Faces, stream.Roll(i).Faces);
        }

        [Fact]
        public void 웹_프리뷰_이식본과_비트_단위로_일치한다()
        {
            // Tools/WebPreview 의 JS 이식본이 뽑은 기준값(seed 12345).
            // 어느 쪽 해시를 건드려도 이 테스트가 먼저 깨져서 두 구현이 조용히 갈라지는 것을 막는다.
            int[] expectedFaces = { 6, 11, 7, 12, 15, 2, 6, 4, 2, 2, 14, 4,
                                    11, 5, 8, 3, 11, 7, 7, 10, 0, 1, 7, 0 };

            for (int i = 0; i < expectedFaces.Length; i++)
                Assert.Equal(expectedFaces[i], UrDice.Roll(12345, i).Faces);
        }

        [Fact]
        public void 확률표의_합은_1이다()
        {
            double sum = 0;
            foreach (double p in UrDice.Probability) sum += p;
            Assert.Equal(1.0, sum, 10);
        }
    }
}
