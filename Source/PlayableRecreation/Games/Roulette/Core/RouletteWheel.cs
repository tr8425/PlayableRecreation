using System;
using PlayableRecreation.Core;

namespace Roulette.Core
{
    /// <summary>거는 방법. 스트레이트만 값(숫자)을 쓰고 나머지는 종류 자체가 값이다.</summary>
    public enum BetKind
    {
        Straight,
        Red,
        Black,
        Odd,
        Even,
        Dozen1,
        Dozen2,
        Dozen3,
    }

    /// <summary>
    /// 유럽식 휠. 0 하나에 37칸 - 어디에 걸든 하우스 몫은 2.7% 로 같다.
    /// 차이는 기대값이 아니라 변동성이고, 그것이 이 게임의 유일한 진짜 결정이다.
    ///
    /// Verse 를 모른다 - 규칙과 산수만 있다.
    /// </summary>
    public static class RouletteWheel
    {
        public const int Pockets = 37;
        public const float SectorAngle = (float)(Math.PI * 2.0 / Pockets);

        /// <summary>휠에 새겨진 실제 순서. 숫자판과 달리 빨강과 검정이 번갈아 온다.</summary>
        public static readonly int[] Order =
        {
            0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
            5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
        };

        private static readonly bool[] redPockets = BuildRed();

        private static bool[] BuildRed()
        {
            bool[] table = new bool[Pockets];
            int[] reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            for (int i = 0; i < reds.Length; i++) table[reds[i]] = true;
            return table;
        }

        public static bool IsRed(int pocket)
        {
            return pocket >= 0 && pocket < Pockets && redPockets[pocket];
        }

        /// <summary>
        /// 이번 스핀의 칸. (시드, 순번)으로 결정된다 - 돌리는 순간 결과는 이미 정해져 있고,
        /// 창을 닫았다 여는 세이브스컴이 통하지 않는다.
        /// </summary>
        public static int PocketAt(int seed, int spin)
        {
            int pocket = (int)(AimMath.Uniform(seed, spin) * Pockets);
            return pocket < Pockets ? pocket : Pockets - 1;
        }

        /// <summary>휠 위의 자리(0..36번째 칸). 공이 멈출 각도가 여기서 나온다.</summary>
        public static int WheelIndexOf(int pocket)
        {
            return Array.IndexOf(Order, pocket);
        }

        /// <summary>순수익 배수. 이기면 건 돈 × 이만큼을 더 받는다.</summary>
        public static int NetMultiplier(BetKind kind)
        {
            if (kind == BetKind.Straight) return 35;
            if (kind == BetKind.Dozen1 || kind == BetKind.Dozen2 || kind == BetKind.Dozen3) return 2;
            return 1;
        }

        /// <summary>0 은 스트레이트로 0 을 집은 사람 말고는 모두를 진다 - 그것이 하우스의 몫이다.</summary>
        public static bool Wins(BetKind kind, int value, int pocket)
        {
            switch (kind)
            {
                case BetKind.Straight: return pocket == value;
                case BetKind.Red: return IsRed(pocket);
                case BetKind.Black: return pocket != 0 && !IsRed(pocket);
                case BetKind.Odd: return pocket % 2 == 1;
                case BetKind.Even: return pocket != 0 && pocket % 2 == 0;
                case BetKind.Dozen1: return pocket >= 1 && pocket <= 12;
                case BetKind.Dozen2: return pocket >= 13 && pocket <= 24;
                case BetKind.Dozen3: return pocket >= 25 && pocket <= 36;
                default: return false;
            }
        }

        /// <summary>판 좌표(+x 오른쪽 · +y 위) → 12시부터 시계방향 각도 [0, 2π).</summary>
        public static float AngleOf(float x, float y)
        {
            double angle = Math.Atan2(x, y);
            if (angle < 0.0) angle += Math.PI * 2.0;
            return (float)angle;
        }

        /// <summary>그 각도가 가리키는 휠 자리. 칸은 자기 중심각 ± 반칸을 차지한다.</summary>
        public static int WheelIndexAt(float angle)
        {
            int index = (int)Math.Floor(angle / SectorAngle + 0.5);
            return ((index % Pockets) + Pockets) % Pockets;
        }
    }
}
