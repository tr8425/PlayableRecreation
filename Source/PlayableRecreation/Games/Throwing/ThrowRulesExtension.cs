using Throwing.Core;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 던지는 게임의 규칙을 Def 에 붙인다. 편자막대와 후프스톤은 같은 워커에 이 값만 달리 준다.
    /// Def 를 파생시키는 대신 확장으로 붙여 프레임워크가 이 값들을 알 필요가 없게 한다.
    /// </summary>
    public class ThrowRulesExtension : DefModExtension
    {
        public int throwsPerInning = 2;
        public int targetScore = 21;
        public bool perThrowScoring;
        public float ringerRadius = 0.16f;
        public float scoreRadius = 0.55f;
        public int ringerPoints = 3;
        public int nearPoints = 1;

        /// <summary>정확히 맞혔을 때의 이름. 링어 또는 통과.</summary>
        public string ringerKey = "THR.Ringer.Shoe";

        public ThrowRules ToRules()
        {
            return new ThrowRules
            {
                ThrowsPerInning = throwsPerInning,
                TargetScore = targetScore,
                PerThrowScoring = perThrowScoring,
                RingerRadius = ringerRadius,
                ScoreRadius = scoreRadius,
                RingerPoints = ringerPoints,
                NearPoints = nearPoints,
            };
        }
    }
}
