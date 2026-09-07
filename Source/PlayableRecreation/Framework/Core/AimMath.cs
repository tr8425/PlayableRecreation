using System;

namespace PlayableRecreation.Core
{
    /// <summary>
    /// 결정론 난수와 두 막대 조준의 산수. Verse 를 모르고, 어떤 게임도 모른다.
    ///
    /// 여러 게임이 같은 손맛(막대 멈추기)과 같은 원칙(시드가 곧 운명 - 세이브스컴 무효)을
    /// 쓰지만, 게임끼리는 서로 모른다는 규칙이 있다. 그래서 공유분은 여기 중립 지대에 산다.
    /// 게임별 튜닝(상대의 흩어짐 등)은 각자의 Core 에 남는다.
    /// </summary>
    public static class AimMath
    {
        /// <summary>플레이어 막대가 만들어낼 수 있는 최대 오차(한 축).</summary>
        public const float MaxAxisError = 0.75f;

        /// <summary>정타 구간 난수를 다른 난수와 섞이지 않게 가르는 소금.</summary>
        private const int ZoneSalt = unchecked((int)0x51C3A9E7);

        /// <summary>좌우 오차와 세기 오차를 거리 하나로 합친다.</summary>
        public static float Distance(float lateral, float depth)
        {
            return (float)Math.Sqrt(lateral * lateral + depth * depth);
        }

        /// <summary>난이도가 높을수록 막대가 빨리 움직인다.</summary>
        public static float BarSpeedFor(int tier)
        {
            return 1.0f + 0.2f * tier;
        }

        // ---------- 정타 구간 ----------
        //
        // 초록 구간은 발마다 자리와 크기가 다르다. (시드, 순번)에서 나오므로
        // 창을 닫았다 열어도 같은 자리다.
        //
        // 구간 가장자리에서 멈추면 한 축 오차가 기준 반경의 절반이 된다.
        // 그래서 두 막대 모두 초록 안이면 합친 거리가 반드시 기준 반경 안이다.

        /// <summary>정타 구간의 절반 너비(0~1 막대 기준). 난이도가 높을수록 좁고, 발마다 조금씩 다르다.</summary>
        public static float ZoneHalf(int seed, int throwIndex, int axis, int tier, float tightRadius)
        {
            float baseHalf = tightRadius / MaxAxisError * 0.25f;
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
        public static float AxisError(float sweep, float center, float half, float tightRadius)
        {
            float error = (sweep - center) / half * (tightRadius * 0.5f);

            if (error > MaxAxisError) return MaxAxisError;
            if (error < -MaxAxisError) return -MaxAxisError;
            return error;
        }

        /// <summary>(시드, 순번)에서 뽑는 2차원 정규분포 오프셋. Box-Muller.</summary>
        public static void Gaussian2D(int seed, int index, float sigma, out float dx, out float dy)
        {
            float u1 = Uniform(seed, index * 2);
            float u2 = Uniform(seed, index * 2 + 1);
            if (u1 < 1e-6f) u1 = 1e-6f;

            double radius = sigma * Math.Sqrt(-2.0 * Math.Log(u1));
            double angle = 2.0 * Math.PI * u2;

            dx = (float)(radius * Math.Cos(angle));
            dy = (float)(radius * Math.Sin(angle));
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
