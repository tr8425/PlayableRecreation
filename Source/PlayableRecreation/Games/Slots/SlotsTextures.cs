using UnityEngine;

namespace Slots
{
    /// <summary>
    /// 슬롯머신의 상징들. 그림 파일 없이 실행 중에 한 번 굽는다 - 다트판과 같은 방식.
    ///
    /// 7은 슬롯머신의 그 7이어야 한다 - 폰트 글자가 아니라, 굵고 비스듬하고
    /// 테두리가 도는 빨간 7. 위 가로획과 대각선 획 두 개의 거리장으로 만든다.
    /// </summary>
    public static class SlotsTextures
    {
        private const int Size = 128;
        private const float Outline = 0.045f;

        private static Texture2D seven;

        public static Texture2D Seven
        {
            get { return seven != null ? seven : (seven = BakeSeven()); }
        }

        private static readonly Color SevenFill = new Color(0.80f, 0.24f, 0.20f);
        private static readonly Color SevenEdge = new Color(0.95f, 0.91f, 0.82f);

        private static Texture2D BakeSeven()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false)
            {
                name = "PR_SlotSeven",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[Size * Size];

            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float u = (px + 0.25f + 0.5f * sx) / Size;
                        // 텍스처는 아래가 0행이지만 글자는 위에서 아래로 생각하는 것이 편하다.
                        float v = 1f - (py + 0.25f + 0.5f * sy) / Size;
                        sum += SampleSeven(u, v);
                    }

                    pixels[py * Size + px] = sum * 0.25f;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color SampleSeven(float u, float v)
        {
            // 위쪽이 오른쪽으로 기운 이탤릭. 슬롯머신의 7은 서 있지 않는다.
            u -= (0.5f - v) * 0.14f;

            float distance = Mathf.Min(
                BoxDistance(u, v, 0.50f, 0.20f, 0.33f, 0.085f),          // 위 가로획
                SegmentDistance(u, v, 0.76f, 0.285f, 0.40f, 0.88f) - 0.105f); // 대각선 획

            if (distance <= 0f) return SevenFill;
            if (distance <= Outline) return SevenEdge;
            return Color.clear;
        }

        private static float BoxDistance(float u, float v, float cx, float cy, float hx, float hy)
        {
            float qx = Mathf.Abs(u - cx) - hx;
            float qy = Mathf.Abs(v - cy) - hy;

            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                                       + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        private static float SegmentDistance(float u, float v, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float t = Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / (dx * dx + dy * dy));

            float px = ax + dx * t - u;
            float py = ay + dy * t - v;
            return Mathf.Sqrt(px * px + py * py);
        }
    }
}
