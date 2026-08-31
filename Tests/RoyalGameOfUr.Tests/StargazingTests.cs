using System;
using System.Collections.Generic;
using Stargazing.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 하늘. 여기서 확인하는 것은 "예쁜가"가 아니라 두 가지다 -
    /// 같은 세계에서 언제나 같은 하늘이 나오는가, 그리고 위도가 실제로 무엇을 바꾸는가.
    /// </summary>
    public class StargazingTests
    {
        [Fact]
        public void 같은_시드는_같은_하늘을_만든다()
        {
            StarField a = new StarField(20260831);
            StarField b = new StarField(20260831);

            Assert.Equal(a.Count, b.Count);

            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a.Stars[i].Ra, b.Stars[i].Ra);
                Assert.Equal(a.Stars[i].Dec, b.Stars[i].Dec);
                Assert.Equal(a.Stars[i].Magnitude, b.Stars[i].Magnitude);
            }

            Assert.Equal(a.Designation(17), b.Designation(17));
        }

        [Fact]
        public void 다른_시드는_다른_하늘을_만든다()
        {
            StarField a = new StarField(1);
            StarField b = new StarField(2);

            int same = 0;
            for (int i = 0; i < a.Count; i++)
                if (Math.Abs(a.Stars[i].Ra - b.Stars[i].Ra) < 1e-6f) same++;

            Assert.True(same < a.Count / 100, "skies should differ, same=" + same);
        }

        [Fact]
        public void 별은_극에_몰리지_않는다()
        {
            StarField field = new StarField(99, 20000);

            // 같은 넓이의 띠 셋. 구면에 고르다면 개수가 비슷해야 한다.
            // sin(dec) 이 -1..1 에 균등하므로 경계는 -1/3, 1/3 이다.
            int low = 0, middle = 0, high = 0;

            for (int i = 0; i < field.Count; i++)
            {
                float sin = (float)Math.Sin(field.Stars[i].Dec);

                if (sin < -1f / 3f) low++;
                else if (sin > 1f / 3f) high++;
                else middle++;
            }

            int third = field.Count / 3;
            Assert.InRange(low, third * 9 / 10, third * 11 / 10);
            Assert.InRange(middle, third * 9 / 10, third * 11 / 10);
            Assert.InRange(high, third * 9 / 10, third * 11 / 10);
        }

        [Fact]
        public void 밝은_별은_드물다()
        {
            StarField field = new StarField(7, 20000);

            int veryBright = field.VisibleCount(1f);
            int naked = field.VisibleCount(6f);

            Assert.True(veryBright < field.Count / 12, "too many bright stars: " + veryBright);
            Assert.True(veryBright > 0, "no bright stars at all");
            Assert.True(naked > field.Count * 8 / 10, "too few visible: " + naked);
        }

        [Fact]
        public void 적도에서는_온_하늘이_뜨고_극에서는_절반만_뜬다()
        {
            StarField field = new StarField(4242);

            int atEquator = 0;
            int atPole = 0;

            for (int i = 0; i < field.Count; i++)
            {
                float dec = field.Stars[i].Dec;

                if (SkyMath.EverRises(dec, 0f)) atEquator++;
                if (SkyMath.EverRises(dec, SkyMath.HalfPi * 0.98f)) atPole++;
            }

            Assert.Equal(field.Count, atEquator);
            Assert.InRange(atPole, field.Count * 45 / 100, field.Count * 55 / 100);
        }

        [Fact]
        public void 머리_위의_별은_원판_가운데에_찍힌다()
        {
            const float latitude = 0.6f;
            const float sidereal = 1.2f;

            // 적위가 위도와 같고 시간각이 0인 별은 정확히 천정에 있다.
            float altitude, azimuth;
            SkyMath.AltAz(sidereal, latitude, latitude, sidereal, out altitude, out azimuth);

            Assert.InRange(altitude, SkyMath.HalfPi - 0.001f, SkyMath.HalfPi + 0.001f);

            float x, y;
            SkyMath.Project(altitude, azimuth, out x, out y);

            Assert.InRange(Math.Sqrt(x * x + y * y), 0.0, 0.001);
        }

        [Fact]
        public void 지평선의_별은_테두리에_찍힌다()
        {
            float x, y;
            SkyMath.Project(0f, 0f, out x, out y);

            Assert.InRange(Math.Sqrt(x * x + y * y), 0.999, 1.001);
            Assert.True(y < 0f, "north must be up");

            SkyMath.Project(0f, SkyMath.HalfPi, out x, out y);
            Assert.True(x > 0.99f, "east must be right");
        }

        [Fact]
        public void 위도가_바뀌면_보이는_별이_바뀐다()
        {
            StarField field = new StarField(31337);
            const float sidereal = 2.0f;

            int north = Above(field, 0.9f, sidereal);
            int south = Above(field, -0.9f, sidereal);
            int shared = 0;

            for (int i = 0; i < field.Count; i++)
            {
                float alt, az;

                SkyMath.AltAz(field.Stars[i].Ra, field.Stars[i].Dec, 0.9f, sidereal, out alt, out az);
                if (alt <= 0f) continue;

                SkyMath.AltAz(field.Stars[i].Ra, field.Stars[i].Dec, -0.9f, sidereal, out alt, out az);
                if (alt > 0f) shared++;
            }

            Assert.True(north > 0 && south > 0);
            Assert.True(shared < north / 2, "sky barely changed: shared=" + shared + " north=" + north);
        }

        [Fact]
        public void 계절이_바뀌면_같은_시각의_하늘이_달라진다()
        {
            float now = SkyMath.SiderealTime(0f, 0.25f, 30f);
            float sameHourNextDay = SkyMath.SiderealTime(1f / 60f, 0.25f, 30f);
            float exactlyOneTurn = SkyMath.SiderealTime(0f, 1.25f, 30f);

            Assert.InRange(Math.Abs(now - exactlyOneTurn), 0.0, 0.001);
            Assert.True(Math.Abs(now - sameHourNextDay) > 0.05f, "the season must shift the sky");
        }

        [Fact]
        public void 경도가_바뀌면_하늘이_돌아간다()
        {
            float west = SkyMath.SiderealTime(0.2f, 0.5f, -90f);
            float east = SkyMath.SiderealTime(0.2f, 0.5f, 90f);

            Assert.True(Math.Abs(west - east) > 1f, "longitude must rotate the sky");
        }

        [Fact]
        public void 별자리는_가까운_별끼리만_잇는다()
        {
            StarField field = new StarField(555);
            List<int[]> shapes = ConstellationMaker.Generate(field, 9, 555);

            Assert.NotEmpty(shapes);

            HashSet<int> seen = new HashSet<int>();

            foreach (int[] shape in shapes)
            {
                Assert.True(shape.Length >= 3, "a constellation needs at least three stars");

                for (int i = 0; i < shape.Length; i++)
                {
                    Assert.True(seen.Add(shape[i]), "a star was used by two constellations");

                    if (i == 0) continue;

                    Star a = field.Stars[shape[i - 1]];
                    Star b = field.Stars[shape[i]];

                    Assert.True(SkyMath.Separation(a.Ra, a.Dec, b.Ra, b.Dec) < 0.31f,
                                "linked stars are too far apart");
                }
            }
        }

        [Fact]
        public void 같은_시드는_같은_별자리를_긋는다()
        {
            StarField field = new StarField(808);

            List<int[]> first = ConstellationMaker.Generate(field, 8, 808);
            List<int[]> again = ConstellationMaker.Generate(field, 8, 808);

            Assert.Equal(first.Count, again.Count);
            for (int i = 0; i < first.Count; i++) Assert.Equal(first[i], again[i]);
        }

        [Fact]
        public void 별자리의_가운데는_별들_사이에_있다()
        {
            StarField field = new StarField(1234);
            List<int[]> shapes = ConstellationMaker.Generate(field, 5, 1234);

            Constellation constellation = new Constellation(shapes[0], "test", false);

            float ra, dec;
            constellation.Center(field, out ra, out dec);

            foreach (int index in shapes[0])
            {
                Star star = field.Stars[index];
                Assert.True(SkyMath.Separation(ra, dec, star.Ra, star.Dec) < 0.6f);
            }
        }

        private static int Above(StarField field, float latitude, float sidereal)
        {
            int count = 0;

            for (int i = 0; i < field.Count; i++)
            {
                float alt, az;
                SkyMath.AltAz(field.Stars[i].Ra, field.Stars[i].Dec, latitude, sidereal, out alt, out az);
                if (alt > 0f) count++;
            }

            return count;
        }
    }
}
