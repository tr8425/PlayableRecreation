using System;

namespace Darts.Core
{
    /// <summary>맞은 자리 하나. Multiplier 0 은 판 밖이다.</summary>
    public struct DartHit
    {
        /// <summary>섹터 숫자(1~20). 불은 25.</summary>
        public int Sector;

        /// <summary>0 = 빗나감, 1 = 싱글, 2 = 더블, 3 = 트리플. 불은 1(25) 또는 2(50).</summary>
        public int Multiplier;

        public int Points
        {
            get { return Sector * Multiplier; }
        }

        public bool IsBull
        {
            get { return Sector == 25; }
        }

        /// <summary>"T20" "D5" "S1" "25" "50". 빗나가면 빈 문자열 - 문구는 UI 의 몫이다.</summary>
        public string Code
        {
            get
            {
                if (Multiplier == 0) return string.Empty;
                if (IsBull) return Multiplier == 2 ? "50" : "25";
                return (Multiplier == 3 ? "T" : Multiplier == 2 ? "D" : "S") + Sector;
            }
        }
    }

    /// <summary>
    /// 다트판의 기하. 반지름 1.0 이 더블 링의 바깥 가장자리다.
    /// 좌표는 +x 오른쪽 · +y 위 - 화면 좌표로 바꾸는 것은 그리는 쪽의 일이다.
    ///
    /// 비율은 실제 경기용 판(지름 340mm 기준)을 그대로 옮겼다.
    /// </summary>
    public static class DartBoard
    {
        public const float InnerBull = 0.037f;
        public const float OuterBull = 0.094f;
        public const float TripleInner = 0.582f;
        public const float TripleOuter = 0.629f;
        public const float DoubleInner = 0.953f;
        public const float DoubleOuter = 1.0f;

        /// <summary>12시부터 시계 방향. 큰 수 옆에 작은 수를 두는 표준 배열이다.</summary>
        public static readonly int[] Sectors =
        {
            20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5,
        };

        public const float SectorAngle = (float)(Math.PI * 2.0 / 20.0);

        /// <summary>12시에서 시계 방향으로 잰 각(라디안, 0~2π).</summary>
        public static float AngleOf(float x, float y)
        {
            double angle = Math.Atan2(x, y);
            if (angle < 0.0) angle += Math.PI * 2.0;
            return (float)angle;
        }

        /// <summary>그 각이 속한 섹터 칸(0~19). 경계는 섹터 중심에서 ±9° 다.</summary>
        public static int SectorIndexAt(float angle)
        {
            int index = (int)Math.Floor((angle + SectorAngle * 0.5f) / SectorAngle);
            return ((index % 20) + 20) % 20;
        }

        /// <summary>섹터 중심각. 숫자 배치와 상대의 조준에 쓴다.</summary>
        public static float SectorCenterAngle(int index)
        {
            return index * SectorAngle;
        }

        public static DartHit ScoreAt(float x, float y)
        {
            float radius = (float)Math.Sqrt(x * x + y * y);

            if (radius > DoubleOuter) return new DartHit();
            if (radius <= InnerBull) return new DartHit { Sector = 25, Multiplier = 2 };
            if (radius <= OuterBull) return new DartHit { Sector = 25, Multiplier = 1 };

            int sector = Sectors[SectorIndexAt(AngleOf(x, y))];

            int multiplier = 1;
            if (radius >= TripleInner && radius <= TripleOuter) multiplier = 3;
            else if (radius >= DoubleInner) multiplier = 2;

            return new DartHit { Sector = sector, Multiplier = multiplier };
        }
    }
}
