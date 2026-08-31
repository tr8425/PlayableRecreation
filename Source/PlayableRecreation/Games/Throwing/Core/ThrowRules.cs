namespace Throwing.Core
{
    public enum ThrowSide : byte
    {
        Player = 0,
        Opponent = 1,
    }

    /// <summary>
    /// 던지는 게임 한 판의 규칙. 편자막대와 후프스톤은 같은 코드가 이 값만 다르게 받는다.
    /// </summary>
    public sealed class ThrowRules
    {
        /// <summary>한 이닝에 한쪽이 던지는 횟수.</summary>
        public int ThrowsPerInning = 2;

        /// <summary>이 점수에 먼저 닿으면 이긴다. 이닝이 끝나야 판정한다.</summary>
        public int TargetScore = 21;

        /// <summary>
        /// 켜면 던질 때마다 따로 채점한다(후프스톤 - 통과 2점, 테두리 1점).
        /// 끄면 이닝이 끝난 뒤 양쪽을 비교해 가까운 쪽만 얻는다(편자막대).
        /// </summary>
        public bool PerThrowScoring;

        /// <summary>정확히 꽂히거나 통과한 것으로 치는 거리.</summary>
        public float RingerRadius = 0.16f;

        /// <summary>점수로 인정하는 최대 거리. 이보다 멀면 0점.</summary>
        public float ScoreRadius = 0.55f;

        public int RingerPoints = 3;
        public int NearPoints = 1;

        public static ThrowRules Horseshoes()
        {
            return new ThrowRules
            {
                ThrowsPerInning = 2,
                TargetScore = 21,
                PerThrowScoring = false,
                RingerRadius = 0.16f,
                ScoreRadius = 0.55f,
                RingerPoints = 3,
                NearPoints = 1,
            };
        }

        public static ThrowRules Hoopstone()
        {
            return new ThrowRules
            {
                ThrowsPerInning = 3,
                TargetScore = 15,
                PerThrowScoring = true,
                RingerRadius = 0.20f,
                ScoreRadius = 0.34f,
                RingerPoints = 2,
                NearPoints = 1,
            };
        }
    }
}
