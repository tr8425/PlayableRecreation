using System;
using Stargazing.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 궤도에 뜨는 것들을 하늘에 흩는 일.
    ///
    /// 소행성과 정거장의 자리는 그 타일 번호에서 나온다. 그런데 그 번호들은 서로 이웃한
    /// 값이고, 섞지 않고 쓰면 스물몇 개가 지평선 한 자락에 겹쳐 뜬다 - 실제로 그랬다.
    /// 적위는 아예 전부 같은 값이 나왔다.
    ///
    /// 그래서 여기서 지키는 것은 하나다 - 이웃한 번호는 이웃하지 않은 자리를 받아야 한다.
    /// </summary>
    public class SkyScatterTests
    {
        /// <summary>실제 코드가 해시에서 자리를 뽑는 방식 그대로.</summary>
        private static void Place(int tile, out float ra, out float dec)
        {
            int hash = SkyMath.Scatter(tile);

            ra = (hash & 0xFFFF) / 65535f * SkyMath.TwoPi;
            dec = ((hash >> 16 & 0xFFFF) / 65535f - 0.5f) * 1.9f;
        }

        /// <summary>
        /// 방향 벡터의 평균 길이. 한 점에 뭉쳐 있으면 1 에 가깝고, 고루 퍼지면 0 에 가깝다.
        /// </summary>
        private static float Clumping(int first, int count)
        {
            float x = 0f, y = 0f, z = 0f;

            for (int i = 0; i < count; i++)
            {
                float ra, dec;
                Place(first + i, out ra, out dec);

                x += (float)(Math.Cos(dec) * Math.Cos(ra));
                y += (float)(Math.Cos(dec) * Math.Sin(ra));
                z += (float)Math.Sin(dec);
            }

            return (float)Math.Sqrt(x * x + y * y + z * z) / count;
        }

        [Fact]
        public void 이웃한_타일들이_한자리에_뭉치지_않는다()
        {
            // 어디서 시작하든 마찬가지여야 한다. 타일 번호는 세계마다 다르다.
            foreach (int first in new[] { 0, 977, 53100, 100000, 262144 })
            {
                float clumping = Clumping(first, 21);

                Assert.True(clumping < 0.45f,
                    "타일 " + first + " 부터 21개가 뭉쳤다 (R=" + clumping.ToString("0.000") + ")");
            }
        }

        [Fact]
        public void 적위가_전부_같은_값으로_주저앉지_않는다()
        {
            float low = float.MaxValue, high = float.MinValue;

            for (int i = 0; i < 21; i++)
            {
                float ra, dec;
                Place(100000 + i, out ra, out dec);

                if (dec < low) low = dec;
                if (dec > high) high = dec;
            }

            // 적위가 쓸 수 있는 폭은 1.9 rad 다. 그 절반은 덮어야 흩어졌다고 할 수 있다.
            Assert.True(high - low > 0.95f, "적위 폭이 " + (high - low).ToString("0.000") + " 밖에 안 된다");
        }

        [Fact]
        public void 같은_타일은_언제나_같은_자리를_받는다()
        {
            // 무작위가 아니라 섞기다. 창을 닫았다 열어도 소행성은 그 자리에 있어야 한다.
            for (int tile = 900; tile < 920; tile++)
                Assert.Equal(SkyMath.Scatter(tile), SkyMath.Scatter(tile));

            Assert.NotEqual(SkyMath.Scatter(900), SkyMath.Scatter(901));
        }

        [Fact]
        public void 음수와_0_도_받는다()
        {
            // 타일이 없을 때 0 이 들어온다. 던지지만 않으면 된다.
            SkyMath.Scatter(0);
            SkyMath.Scatter(-1);
            SkyMath.Scatter(int.MinValue);
            SkyMath.Scatter(int.MaxValue);
        }
    }
}
