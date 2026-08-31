using System;

namespace Ur.AI
{
    /// <summary>플레이어가 판 시작 시 고르는 난이도. (DESIGN.md §5.1)</summary>
    public enum UrDifficulty : byte
    {
        Novice = 0,      // T0 초보
        Apprentice = 1,  // T1 견습
        Skilled = 2,     // T2 숙련
        Expert = 3,      // T3 상급
        Master = 4,      // T4 명인
    }

    public static class UrDifficultyInfo
    {
        public static readonly UrDifficulty[] All =
        {
            UrDifficulty.Novice, UrDifficulty.Apprentice, UrDifficulty.Skilled,
            UrDifficulty.Expert, UrDifficulty.Master
        };

        /// <summary>탐색 깊이(플라이). Novice 는 탐색하지 않는다.</summary>
        public static int SearchDepth(UrDifficulty difficulty)
        {
            switch (difficulty)
            {
                case UrDifficulty.Skilled: return 2;
                case UrDifficulty.Expert: return 3;
                case UrDifficulty.Master: return 4;
                default: return 1;
            }
        }

        /// <summary>최선수 대신 차선수를 두는 확률. 하위 난이도가 기계적으로 느껴지지 않게 한다.</summary>
        public static float BlunderChance(UrDifficulty difficulty)
        {
            switch (difficulty)
            {
                case UrDifficulty.Apprentice: return 0.15f;
                case UrDifficulty.Skilled: return 0.08f;
                case UrDifficulty.Expert: return 0.03f;
                default: return 0f;
            }
        }

        public static IUrAi Create(UrDifficulty difficulty, Random rng)
        {
            switch (difficulty)
            {
                case UrDifficulty.Novice:
                    return new RandomAi(rng);
                case UrDifficulty.Apprentice:
                    return new GreedyAi(rng, BlunderChance(difficulty));
                default:
                    return new ExpectiminimaxAi(SearchDepth(difficulty), BlunderChance(difficulty), rng);
            }
        }

        /// <summary>선택 옵션 '폰 지능 연동' 용 매핑. 열정 보정은 호출부에서 더한 값을 넘긴다.</summary>
        public static UrDifficulty FromIntellectual(int skill)
        {
            if (skill <= 3) return UrDifficulty.Novice;
            if (skill <= 7) return UrDifficulty.Apprentice;
            if (skill <= 11) return UrDifficulty.Skilled;
            if (skill <= 15) return UrDifficulty.Expert;
            return UrDifficulty.Master;
        }

        public static UrDifficulty Clamp(int raw)
        {
            if (raw < 0) return UrDifficulty.Novice;
            if (raw > 4) return UrDifficulty.Master;
            return (UrDifficulty)raw;
        }
    }
}
