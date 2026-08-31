using System;

namespace Stargazing.Core
{
    public struct Star
    {
        /// <summary>적경. 0~2π.</summary>
        public float Ra;

        /// <summary>적위. -π/2 ~ π/2.</summary>
        public float Dec;

        /// <summary>등급. 작을수록 밝다.</summary>
        public float Magnitude;

        /// <summary>0이면 푸른 별, 1이면 붉은 별.</summary>
        public float Warmth;
    }

    /// <summary>
    /// 이 행성에서 보이는 하늘. 별은 세계를 만들 때 쓴 시드가 정한다 -
    /// 같은 세계에서는 언제 봐도 같은 별이고, 다른 세계에서는 아예 다른 하늘이다.
    ///
    /// 하늘은 세계의 성질이지 망원경의 성질이 아니므로, 여기에는 저장할 것이 없다.
    /// 시드만 있으면 언제든 똑같이 다시 만들어진다.
    /// </summary>
    public sealed class StarField
    {
        public const int DefaultCount = 1400;

        /// <summary>가장 어두운 별. 맨눈 한계보다 조금 아래까지 만들어 둔다.</summary>
        public const float FaintestMagnitude = 6.4f;
        public const float BrightestMagnitude = -1.4f;

        private const string SectorLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        public Star[] Stars { get; private set; }
        public int Seed { get; private set; }

        public int Count { get { return Stars.Length; } }

        public StarField(int seed) : this(seed, DefaultCount)
        {
        }

        public StarField(int seed, int count)
        {
            Seed = seed;
            Stars = new Star[Math.Max(1, count)];

            SkyRng rng = new SkyRng(seed ^ 0x51ED2701);

            for (int i = 0; i < Stars.Length; i++)
            {
                // 구면에 고르게 뿌린다. 위도로 그냥 균등하게 뽑으면 극에 몰린다.
                float ra = rng.Value * SkyMath.TwoPi;
                float dec = (float)Math.Asin(SkyMath.Clamp(rng.Value * 2f - 1f, -1f, 1f));

                // 밝은 별은 드물어야 한다. 지수를 낮게 잡아 어두운 쪽으로 몰아 둔다.
                float roll = (float)Math.Pow(rng.Value, 0.38);
                float magnitude = BrightestMagnitude + (FaintestMagnitude - BrightestMagnitude) * roll;

                Stars[i] = new Star
                {
                    Ra = ra,
                    Dec = dec,
                    Magnitude = magnitude,
                    Warmth = rng.Value * 0.85f + rng.Value * 0.15f,
                };
            }
        }

        /// <summary>
        /// 별 이름. 하늘을 24조각으로 나눈 구역 글자와 번호를 붙인다 -
        /// 실제 성표가 하는 방식이고, 무엇보다 번역할 것이 없다.
        /// </summary>
        public string Designation(int index)
        {
            if (index < 0 || index >= Stars.Length) return "?";

            int sector = (int)(Stars[index].Ra / SkyMath.TwoPi * SectorLetters.Length);
            if (sector >= SectorLetters.Length) sector = SectorLetters.Length - 1;

            return SectorLetters[sector] + "-" + (101 + index % 899);
        }

        /// <summary>밝은 순서. 별자리를 만들 때와 흐린 날 무엇부터 보일지에 쓴다.</summary>
        public int[] ByBrightness()
        {
            int[] order = new int[Stars.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;

            Array.Sort(order, delegate (int a, int b)
            {
                return Stars[a].Magnitude.CompareTo(Stars[b].Magnitude);
            });

            return order;
        }

        /// <summary>그 등급까지 보인다면 몇 개나 보이는가.</summary>
        public int VisibleCount(float limit)
        {
            int count = 0;
            for (int i = 0; i < Stars.Length; i++)
                if (Stars[i].Magnitude <= limit) count++;

            return count;
        }
    }
}
