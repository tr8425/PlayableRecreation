using PlayableRecreation.Core;

namespace Throwing.Core
{
    /// <summary>
    /// 던지기의 산수. 좌우 조준과 세기를 하나의 거리로 합치고, 상대의 흩어짐을 시드에서 뽑는다.
    ///
    /// 상대의 던지기는 (시드, 순번)으로 결정된다 - 이어 던져도 같은 결과가 나오므로
    /// 창을 닫았다 여는 식의 세이브스컴이 통하지 않는다. 우르의 주사위와 같은 원리다.
    ///
    /// 막대와 난수의 공통 산수는 중립 지대(<see cref="AimMath"/>)에 있고,
    /// 여기 남은 것은 이 게임의 튜닝 - 상대가 얼마나 잘 던지는가 - 뿐이다.
    /// </summary>
    public static class ThrowAim
    {
        /// <summary>플레이어 막대가 만들어낼 수 있는 최대 오차(한 축).</summary>
        public const float MaxAxisError = AimMath.MaxAxisError;

        /// <summary>좌우 오차와 세기 오차를 거리 하나로 합친다.</summary>
        public static float Distance(float lateral, float depth)
        {
            return AimMath.Distance(lateral, depth);
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
            return AimMath.BarSpeedFor(tier);
        }

        /// <summary>정타 구간의 절반 너비(0~1 막대 기준).</summary>
        public static float ZoneHalf(int seed, int throwIndex, int axis, int tier, float ringerRadius)
        {
            return AimMath.ZoneHalf(seed, throwIndex, axis, tier, ringerRadius);
        }

        /// <summary>정타 구간의 중심.</summary>
        public static float ZoneCenter(int seed, int throwIndex, int axis, int tier, float half)
        {
            return AimMath.ZoneCenter(seed, throwIndex, axis, tier, half);
        }

        /// <summary>구간 중심에서 벗어난 만큼이 한 축의 오차다.</summary>
        public static float AxisError(float sweep, float center, float half, float ringerRadius)
        {
            return AimMath.AxisError(sweep, center, half, ringerRadius);
        }

        /// <summary>상대의 한 발. 두 축 모두 정규분포로 흩어진 뒤 거리로 합쳐진다.</summary>
        public static float BotThrow(int seed, int throwIndex, float sigma)
        {
            float lateral, depth;
            AimMath.Gaussian2D(seed, throwIndex, sigma, out lateral, out depth);
            return AimMath.Distance(lateral, depth);
        }

        /// <summary>(시드, 순번) 에서 뽑는 0~1 난수.</summary>
        public static float Uniform(int seed, int index)
        {
            return AimMath.Uniform(seed, index);
        }
    }
}
