using Roulette.Core;
using UnityEngine;

namespace Roulette
{
    /// <summary>
    /// 휠 텍스처. 그림 파일 없이 실행 중에 한 번 굽는다 - 다른 판들과 같은 방식이다.
    ///
    /// 숫자는 굽지 않는다. 37칸에 숫자를 새기면 이 크기에서는 읽히지 않고,
    /// 결과는 어차피 허브 가운데에 크게 띄운다. 휠은 구경거리, 정보는 숫자판의 몫이다.
    /// </summary>
    public static class RouletteTextures
    {
        private const int Size = 512;

        /// <summary>칸 띠의 안팎 반지름(휠 단위). 그 밖은 나무 테, 그 안은 허브.</summary>
        public const float BandInner = 0.58f;
        public const float BandOuter = 0.92f;

        /// <summary>공이 도는 반지름(휠 단위).</summary>
        public const float BallRadius = 0.75f;

        private const float WireHalf = 0.006f;

        private static Texture2D wheel;

        public static Texture2D Wheel
        {
            get { return wheel != null ? wheel : (wheel = Build()); }
        }

        private static Texture2D Build()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false)
            {
                name = "PR_RouletteWheel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[Size * Size];
            float center = Size * 0.5f;
            float scale = 1f / center;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // 2×2 초과표집 - 칸 경계선이 1픽셀 안팎이라 이것 없이는 층이 진다.
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        sum += Sample((x + 0.25f + 0.5f * sx - center) * scale,
                                      (y + 0.25f + 0.5f * sy - center) * scale);

                    pixels[y * Size + x] = sum * 0.25f;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color Sample(float x, float y)
        {
            float radius = Mathf.Sqrt(x * x + y * y);

            if (radius > 1f) return Color.clear;
            if (radius > BandOuter) return RouletteTheme.Rim;

            if (radius < BandInner)
            {
                // 허브와 그 테두리.
                if (radius > BandInner - WireHalf * 2f) return RouletteTheme.Wire;
                return RouletteTheme.Hub;
            }

            float angle = RouletteWheel.AngleOf(x, y);
            int index = RouletteWheel.WheelIndexAt(angle);

            // 칸 경계선. 반지름 방향 선이므로 호 길이로 두께를 잰다.
            float offset = angle - index * RouletteWheel.SectorAngle;
            if (offset > Mathf.PI) offset -= Mathf.PI * 2f;
            if (Mathf.Abs(Mathf.Abs(offset) - RouletteWheel.SectorAngle * 0.5f) * radius < WireHalf)
                return RouletteTheme.Wire;

            int pocket = RouletteWheel.Order[index];

            if (pocket == 0) return RouletteTheme.PocketGreen;
            return RouletteWheel.IsRed(pocket) ? RouletteTheme.PocketRed : RouletteTheme.PocketBlack;
        }
    }
}
