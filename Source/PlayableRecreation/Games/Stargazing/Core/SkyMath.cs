using System;

namespace Stargazing.Core
{
    /// <summary>
    /// 구면에서 원판으로. 별을 어디에 찍을지는 위도와 시각이 정한다 -
    /// 이 파일이 이 기능의 전부이자, 위도가 실제로 눈에 보이는 이유다.
    ///
    /// 여기에 있는 것은 진짜 천문 공식이다. 값이 지구의 것이 아닐 뿐이다.
    /// </summary>
    public static class SkyMath
    {
        public const float TwoPi = 6.28318548f;
        public const float HalfPi = 1.57079637f;
        public const float Deg2Rad = 0.0174532924f;

        /// <summary>자전축이 기울어진 정도. 계절이 있는 이유고, 해의 적위를 흔드는 항이다.</summary>
        public const float Obliquity = 0.409f;

        /// <summary>
        /// 국지 항성시. 하루에 한 바퀴 돌고, 한 해에 한 바퀴를 더 돈다 -
        /// 같은 시각에 하늘을 봐도 계절이 바뀌면 다른 별이 떠 있는 이유가 이 한 항이다.
        /// </summary>
        public static float SiderealTime(float dayFraction, float hourFraction, float longitudeDegrees)
        {
            float value = TwoPi * (hourFraction + dayFraction) + longitudeDegrees * Deg2Rad;
            return Wrap(value);
        }

        public static float Wrap(float angle)
        {
            angle %= TwoPi;
            return angle < 0f ? angle + TwoPi : angle;
        }

        /// <summary>적경·적위를 그 자리 그 시각의 고도와 방위로 옮긴다. 방위는 북쪽 0, 동쪽으로 증가.</summary>
        public static void AltAz(float ra, float dec, float latitudeRad, float sidereal,
                                 out float altitude, out float azimuth)
        {
            FromHourAngle(sidereal - ra, dec, latitudeRad, out altitude, out azimuth);
        }

        /// <summary>
        /// 해가 지금 어디 있는가. 정오에 남중하고, 적위는 한 해를 주기로 흔들린다 -
        /// 계절이 생기는 이유가 그 흔들림이다. 행성의 명암 경계선을 정하는 것도 이 방향 하나다.
        /// </summary>
        public static void SunAltAz(float dayFraction, float hourFraction, float latitudeRad,
                                    out float altitude, out float azimuth)
        {
            float dec = Obliquity * (float)Math.Sin(TwoPi * dayFraction);
            FromHourAngle((hourFraction - 0.5f) * TwoPi, dec, latitudeRad, out altitude, out azimuth);
        }

        private static void FromHourAngle(float hour, float dec, float latitudeRad,
                                          out float altitude, out float azimuth)
        {
            float sinDec = (float)Math.Sin(dec);
            float cosDec = (float)Math.Cos(dec);
            float sinLat = (float)Math.Sin(latitudeRad);
            float cosLat = (float)Math.Cos(latitudeRad);

            float sinAlt = sinDec * sinLat + cosDec * cosLat * (float)Math.Cos(hour);
            sinAlt = Clamp(sinAlt, -1f, 1f);
            altitude = (float)Math.Asin(sinAlt);

            float cosAlt = (float)Math.Cos(altitude);

            if (cosAlt < 1e-5f || cosLat < 1e-5f)
            {
                // 천정이나 극점. 방위가 정의되지 않으므로 아무 값이나 하나 고른다.
                azimuth = 0f;
                return;
            }

            float cosAz = Clamp((sinDec - sinAlt * sinLat) / (cosAlt * cosLat), -1f, 1f);
            float angle = (float)Math.Acos(cosAz);

            azimuth = (float)Math.Sin(hour) > 0f ? TwoPi - angle : angle;
        }

        /// <summary>
        /// 지평선 위쪽만 담는 원판. 가운데가 천정, 테두리가 지평선이다.
        /// x 는 동쪽이 양수, y 는 남쪽이 양수 - 화면 좌표에 그대로 얹으라고 이렇게 둔다.
        /// </summary>
        public static void Project(float altitude, float azimuth, out float x, out float y)
        {
            float radius = 1f - altitude / HalfPi;

            x = radius * (float)Math.Sin(azimuth);
            y = -radius * (float)Math.Cos(azimuth);
        }

        /// <summary>대기가 없는 곳에서 쓰는 투영. 지평선 아래까지 구 전체를 담는다.</summary>
        public static void ProjectFull(float altitude, float azimuth, out float x, out float y)
        {
            float radius = (HalfPi - altitude) / (HalfPi * 2f);

            x = radius * (float)Math.Sin(azimuth);
            y = -radius * (float)Math.Cos(azimuth);
        }

        /// <summary>이 위도에서 이 별이 한 번이라도 지평선 위로 올라오는가.</summary>
        public static bool EverRises(float dec, float latitudeRad)
        {
            return Math.Abs(latitudeRad - dec) < HalfPi;
        }

        /// <summary>한 번도 지지 않는가. 극 주위를 도는 별은 계절과 무관하게 늘 떠 있다.</summary>
        public static bool Circumpolar(float dec, float latitudeRad)
        {
            return Math.Abs(latitudeRad + dec) > HalfPi;
        }

        /// <summary>고도·방위로 주어진 두 방향 사이의 각. 구면 공식은 적경·적위 때와 똑같다.</summary>
        public static float SeparationAltAz(float alt1, float az1, float alt2, float az2)
        {
            return Separation(az1, alt1, az2, alt2);
        }

        /// <summary>
        /// 얼마나 차 있는가. 해에서 멀리 떨어져 보일수록 둥글다 -
        /// 정반대편이면 보름이고, 해 쪽에 붙어 있으면 삭이다.
        /// </summary>
        public static float LitFraction(float elongation)
        {
            return 0.5f - 0.5f * (float)Math.Cos(elongation);
        }

        /// <summary>두 방향 사이의 각. 별을 이을 때 너무 먼 것끼리 묶이지 않게 하는 데 쓴다.</summary>
        public static float Separation(float ra1, float dec1, float ra2, float dec2)
        {
            float value = (float)(Math.Sin(dec1) * Math.Sin(dec2)
                                + Math.Cos(dec1) * Math.Cos(dec2) * Math.Cos(ra1 - ra2));

            return (float)Math.Acos(Clamp(value, -1f, 1f));
        }

        public static float Clamp(float value, float low, float high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }

    /// <summary>하늘 전용 난수. 다른 게임의 것을 빌려오지 않는다.</summary>
    public sealed class SkyRng
    {
        private uint state;

        public SkyRng(int seed)
        {
            state = (uint)seed;
            if (state == 0u) state = 0x6D2B79F5u;
        }

        public uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public int Next(int bound)
        {
            return bound <= 1 ? 0 : (int)(Next() % (uint)bound);
        }

        public float Value
        {
            get { return (Next() >> 8) / 16777216f; }
        }

        public float Range(float low, float high)
        {
            return low + (high - low) * Value;
        }
    }
}
