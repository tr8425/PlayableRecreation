using System;
using System.Collections.Generic;

namespace Stargazing.Core
{
    /// <summary>이어진 별 몇 개와 이름 하나. 그게 별자리의 전부다.</summary>
    public sealed class Constellation
    {
        /// <summary>이어진 순서. 이웃한 둘씩 선으로 잇는다.</summary>
        public int[] Stars;

        /// <summary>플레이어가 지었다면 그 이름, 아니면 번역 키의 번호.</summary>
        public string Name;

        public bool PlayerMade;

        public Constellation(int[] stars, string name, bool playerMade)
        {
            Stars = stars;
            Name = name;
            PlayerMade = playerMade;
        }

        /// <summary>별자리의 한가운데. 이름표를 어디에 놓을지 정하는 데 쓴다.</summary>
        public void Center(StarField field, out float ra, out float dec)
        {
            float x = 0f, y = 0f, z = 0f;

            for (int i = 0; i < Stars.Length; i++)
            {
                Star star = field.Stars[Stars[i]];
                float cos = (float)Math.Cos(star.Dec);

                x += cos * (float)Math.Cos(star.Ra);
                y += cos * (float)Math.Sin(star.Ra);
                z += (float)Math.Sin(star.Dec);
            }

            ra = (float)Math.Atan2(y, x);
            if (ra < 0f) ra += SkyMath.TwoPi;

            float flat = (float)Math.Sqrt(x * x + y * y);
            dec = (float)Math.Atan2(z, flat);
        }
    }

    /// <summary>
    /// 하늘에 처음부터 그어져 있는 선들. 사람이 하늘을 보면 어차피 잇게 되어 있고,
    /// 그 선도 별과 같은 시드에서 나오므로 세계마다 다르되 언제나 같다.
    /// </summary>
    public static class ConstellationMaker
    {
        /// <summary>이 각도보다 멀리 떨어진 별끼리는 잇지 않는다. 라디안.</summary>
        private const float MaxLink = 0.30f;

        /// <summary>이을 후보로 삼는 밝은 별의 수.</summary>
        private const int BrightPool = 110;

        public static List<int[]> Generate(StarField field, int count, int seed)
        {
            List<int[]> shapes = new List<int[]>();
            if (field == null || count <= 0) return shapes;

            int[] bright = field.ByBrightness();
            int pool = Math.Min(BrightPool, bright.Length);

            bool[] used = new bool[field.Count];
            SkyRng rng = new SkyRng(seed ^ 0x2545F491);

            for (int made = 0; made < count; made++)
            {
                int start = -1;
                for (int i = 0; i < pool; i++)
                    if (!used[bright[i]]) { start = bright[i]; break; }

                if (start < 0) break;

                List<int> chain = new List<int> { start };
                used[start] = true;

                int links = 3 + rng.Next(4);   // 별 4~7개짜리

                for (int step = 0; step < links; step++)
                {
                    int next = Nearest(field, bright, pool, used, chain[chain.Count - 1]);
                    if (next < 0) break;

                    used[next] = true;
                    chain.Add(next);
                }

                // 둘뿐이면 별자리라 부르기 민망하다.
                if (chain.Count < 3) continue;

                shapes.Add(chain.ToArray());
            }

            return shapes;
        }

        private static int Nearest(StarField field, int[] bright, int pool, bool[] used, int from)
        {
            Star origin = field.Stars[from];
            int best = -1;
            float bestGap = MaxLink;

            for (int i = 0; i < pool; i++)
            {
                int index = bright[i];
                if (used[index]) continue;

                Star star = field.Stars[index];
                float gap = SkyMath.Separation(origin.Ra, origin.Dec, star.Ra, star.Dec);

                if (gap >= bestGap) continue;

                bestGap = gap;
                best = index;
            }

            return best;
        }
    }
}
