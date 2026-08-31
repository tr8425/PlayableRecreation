using Throwing.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 상대의 던지기는 (시드, 순번)으로 결정된다 — 우르의 주사위와 같은 원리다.
    /// 이어 던져도 같은 결과가 나오므로 창을 닫았다 여는 세이브스컴이 통하지 않는다.
    /// </summary>
    public class ThrowAimTests
    {
        [Fact]
        public void 같은_시드와_순번은_같은_결과를_낸다()
        {
            for (int index = 0; index < 50; index++)
            {
                float a = ThrowAim.BotThrow(4242, index, 0.3f);
                float b = ThrowAim.BotThrow(4242, index, 0.3f);
                Assert.Equal(a, b, 6);
            }
        }

        [Fact]
        public void 순번이_다르면_결과도_흩어진다()
        {
            int same = 0;
            for (int index = 0; index < 50; index++)
                if (ThrowAim.BotThrow(7, index, 0.3f) == ThrowAim.BotThrow(7, index + 1, 0.3f)) same++;

            Assert.True(same <= 1);
        }

        [Fact]
        public void 난수는_0과_1_사이다()
        {
            for (int index = 0; index < 2000; index++)
            {
                float u = ThrowAim.Uniform(12345, index);
                Assert.InRange(u, 0f, 1f);
            }
        }

        [Fact]
        public void 잘_던지는_상대일수록_가깝게_떨어진다()
        {
            const int trials = 3000;

            double loose = 0, tight = 0;
            for (int index = 0; index < trials; index++)
            {
                loose += ThrowAim.BotThrow(99, index, ThrowAim.SigmaFor(0));
                tight += ThrowAim.BotThrow(99, index, ThrowAim.SigmaFor(4));
            }

            Assert.True(tight < loose);
        }

        [Fact]
        public void 두_축의_오차는_거리로_합쳐진다()
        {
            Assert.Equal(5f, ThrowAim.Distance(3f, 4f), 5);
            Assert.Equal(0f, ThrowAim.Distance(0f, 0f), 5);
        }

        [Fact]
        public void 난이도가_높을수록_막대가_빠르다()
        {
            for (int tier = 1; tier < 5; tier++)
                Assert.True(ThrowAim.BarSpeedFor(tier) > ThrowAim.BarSpeedFor(tier - 1));
        }
    }
}
