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
