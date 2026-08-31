using Stargazing.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 해와, 해가 비추는 것들.
    ///
    /// 궤도에 올라가면 하늘의 주인공은 별이 아니라 행성이고, 행성이 얼마나 차 있는지는
    /// 해가 어디 있느냐가 전부 정한다. 그래서 여기서 확인하는 것은 하나다 -
    /// 해가 진짜로 동쪽에서 떠서 서쪽으로 지는가, 그리고 계절이 그 높이를 바꾸는가.
    /// </summary>
    public class SkyBodyTests
    {
        private const float East = SkyMath.HalfPi;
        private const float West = SkyMath.HalfPi * 3f;

        [Fact]
        public void 정오에_해가_가장_높다()
        {
            float best = -9f;
            float bestHour = -1f;

            for (int step = 0; step <= 96; step++)
            {
                float hour = step / 96f;

                float altitude, azimuth;
                SkyMath.SunAltAz(0.2f, hour, 0.6f, out altitude, out azimuth);

                if (altitude <= best) continue;

                best = altitude;
                bestHour = hour;
            }

            Assert.InRange(bestHour, 0.49f, 0.51f);
        }

        [Fact]
        public void 자정에는_해가_지평선_아래에_있다()
        {
            float altitude, azimuth;
            SkyMath.SunAltAz(0.2f, 0f, 0.6f, out altitude, out azimuth);

            Assert.True(altitude < 0f);
        }

        [Fact]
        public void 해는_동쪽에서_떠서_서쪽으로_진다()
        {
            float altitude, morning, evening;

            SkyMath.SunAltAz(0f, 0.30f, 0f, out altitude, out morning);
            Assert.True(altitude > 0f);
            Assert.InRange(morning, East - 0.02f, East + 0.02f);

            SkyMath.SunAltAz(0f, 0.70f, 0f, out altitude, out evening);
            Assert.True(altitude > 0f);
            Assert.InRange(evening, West - 0.02f, West + 0.02f);
        }

        [Fact]
        public void 적도의_춘분_정오에는_해가_천정에_온다()
        {
            float altitude, azimuth;
            SkyMath.SunAltAz(0f, 0.5f, 0f, out altitude, out azimuth);

            Assert.InRange(altitude, SkyMath.HalfPi - 0.001f, SkyMath.HalfPi + 0.001f);
        }

        [Fact]
        public void 계절이_남중고도를_바꾼다()
        {
            const float latitude = 0.785f;   // 북위 45도

            float summer, winter, azimuth;
            SkyMath.SunAltAz(0.25f, 0.5f, latitude, out summer, out azimuth);
            SkyMath.SunAltAz(0.75f, 0.5f, latitude, out winter, out azimuth);

            // 두 남중고도의 차이는 자전축이 기운 만큼의 두 배다. 그것이 계절의 정의다.
            Assert.InRange(summer - winter, SkyMath.Obliquity * 2f - 0.01f,
                                            SkyMath.Obliquity * 2f + 0.01f);
        }

        [Fact]
        public void 해_반대편이_보름이고_해_쪽이_삭이다()
        {
            Assert.InRange(SkyMath.LitFraction(0f), -0.001f, 0.001f);
            Assert.InRange(SkyMath.LitFraction(SkyMath.HalfPi), 0.499f, 0.501f);
            Assert.InRange(SkyMath.LitFraction(SkyMath.HalfPi * 2f), 0.999f, 1.001f);
        }

        [Fact]
        public void 같은_방향끼리는_각이_없다()
        {
            Assert.InRange(SkyMath.SeparationAltAz(0.4f, 1.2f, 0.4f, 1.2f), -0.001f, 0.001f);

            // 천정과 지평선은 정확히 직각으로 떨어져 있다.
            float apart = SkyMath.SeparationAltAz(SkyMath.HalfPi, 0f, 0f, 2.3f);
            Assert.InRange(apart, SkyMath.HalfPi - 0.001f, SkyMath.HalfPi + 0.001f);
        }

        [Fact]
        public void 한_바퀴는_지상이든_궤도든_한_바퀴다()
        {
            // 궤도에서는 이 값이 공전 위상으로 들어온다. 하늘이 열여섯 배 빨라지는 자리가 거기다.
            float start = SkyMath.SiderealTime(0f, 0f, 0f);
            float quarter = SkyMath.SiderealTime(0f, 0.25f, 0f);

            Assert.InRange(quarter - start, SkyMath.HalfPi - 0.001f, SkyMath.HalfPi + 0.001f);
            Assert.InRange(SkyMath.SiderealTime(0f, 1f, 0f), -0.001f, 0.001f);
        }
    }
}
