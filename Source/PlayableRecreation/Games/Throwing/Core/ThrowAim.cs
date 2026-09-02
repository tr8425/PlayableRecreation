using System;

namespace Throwing.Core
{
    /// <summary>
    /// 던지기의 산수. 좌우 조준과 세기를 하나의 거리로 합치고, 상대의 흩어짐을 시드에서 뽑는다.
    ///
    /// 상대의 던지기는 (시드, 순번)으로 결정된다 - 이어 던져도 같은 결과가 나오므로
    /// 창을 닫았다 여는 식의 세이브스컴이 통하지 않는다. 우르의 주사위와 같은 원리다.
    /// </summary>
    public static class ThrowAim
    {
        /// <summary>플레이어 막대가 만들어낼 수 있는 최대 오차(한 축).</summary>
        public const float MaxAxisError = 0.75f;

        /// <summary>정타 구간 난수를 상대 던지기·착지 각도의 난수와 섞이지 않게 가르는 소금.</summary>
        private const int ZoneSalt = unchecked((int)0x51C3A9E7);

        /// <summary>좌우 오차와 세기 오차를 거리 하나로 합친다.</summary>
        public static float Distance(float lateral, float depth)
        {
            return (float)Math.Sqrt(lateral * lateral + depth * depth);
        }

        /// <summary>난이도가 높을수록 상대가 잘 던진다.</summary>
        public static float SigmaFor(int tier)
        {
            switch (tier)
            {
                case 0: return 0.55f;
                case 1: return 0.42f;
                case 2: return 0.32f;
                case 3: return 0.24f;
                default: return 0.17f;
            }
        }

        /// <summary>난이도가 높을수록 막대가 빨리 움직인다.</summary>
        public static float BarSpeedFor(int tier)
        {
            return 1.0f + 0.2f * tier;
        }

        // ---------- 정타 구간 ----------
        //
        // 초록 구간은 발마다 자리와 크기가 다르다. (시드, 순번)에서 나오므로
        // 창을 닫았다 열어도 같은 자리다 - 상대의 던지기와 같은 원리.
        //
        // 구간 가장자리에서 멈추면 한 축 오차가 정타 반경의 절반이 된다.
        // 그래서 두 막대 모두 초록 안이면 합친 거리가 반드시 정타 반경 안이다.

        /// <summary>정타 구간의 절반 너비(0~1 막대 기준). 난이도가 높을수록 좁고, 발마다 조금씩 다르다.</summary>
        public static float ZoneHalf(int seed, int throwIndex, int axis, int tier, float ringerRadius)
        {
            float baseHalf = ringerRadius / MaxAxisError * 0.25f;
            float size = 1.25f - 0.14f * tier;
            float u = Uniform(seed ^ ZoneSalt, throwIndex * 4 + axis * 2);
            return baseHalf * size * (0.85f + 0.3f * u);
        }

        /// <summary>정타 구간의 중심. 난이도가 높을수록 멀리 돌아다닌다.</summary>
        public static float ZoneCenter(int seed, int throwIndex, int axis, int tier, float half)
        {
            float wander = 0.12f + 0.07f * tier;
            float u = Uniform(seed ^ ZoneSalt, throwIndex * 4 + axis * 2 + 1);
            float center = 0.5f + (u - 0.5f) * 2f * wander;

            float lo = half + 0.06f;
            float hi = 1f - half - 0.06f;
            return center < lo ? lo : center > hi ? hi : center;
        }

        /// <summary>구간 중심에서 벗어난 만큼이 한 축의 오차다. 구간이 좁을수록 같은 거리가 더 아프다.</summary>
        public static float AxisError(float sweep, float center, float half, float ringerRadius)
        {
            float error = (sweep - center) / half * (ringerRadius * 0.5f);

            if (error > MaxAxisError) return MaxAxisError;
            if (error < -MaxAxisError) return -MaxAxisError;
            return error;
        }

        /// <summary>상대의 한 발. 두 축 모두 정규분포로 흩어진 뒤 거리로 합쳐진다.</summary>
        public static float BotThrow(int seed, int throwIndex, float sigma)
        {
            float u1 = Uniform(seed, throwIndex * 2);
            float u2 = Uniform(seed, throwIndex * 2 + 1);

            // Box-Muller. u1 이 0이면 로그가 발산하므로 아래를 잘라둔다.
            if (u1 < 1e-6f) u1 = 1e-6f;

            double radius = sigma * Math.Sqrt(-2.0 * Math.Log(u1));
            double angle = 2.0 * Math.PI * u2;

            float lateral = (float)(radius * Math.Cos(angle));
            float depth = (float)(radius * Math.Sin(angle));

            return Distance(lateral, depth);
        }

        /// <summary>(시드, 순번) 에서 뽑는 0~1 난수. murmur3 finalizer.</summary>
        public static float Uniform(int seed, int index)
        {
            uint h = (uint)seed ^ (uint)(index * 0x9E3779B9);

            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
