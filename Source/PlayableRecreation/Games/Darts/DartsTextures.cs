using Darts.Core;
using UnityEngine;

namespace Darts
{
    /// <summary>
    /// 다트판 텍스처. 그림 파일 없이 실행 중에 한 번 굽는다 - 이 모드의 다른 판들과 같은 방식이다.
    ///
    /// 판 좌표는 +x 오른쪽 · +y 위. 텍스처는 아래가 0행이고 GUI 는 위가 먼저지만,
    /// 행 인덱스가 큰 쪽을 +y 로 삼으면 GUI.DrawTexture 를 거친 뒤 위가 +y 로 보인다.
    /// </summary>
    public static class DartsTextures
    {
        /// <summary>텍스처 가장자리까지의 판 단위 반지름. 판(1.0) 밖의 여유는 테두리 철사 몫이다.</summary>
        public const float Margin = 1.05f;

        private const int Size = 512;
        private const float WireHalf = 0.006f;

        private static Texture2D board;

        public static Texture2D Board
        {
            get { return board != null ? board : (board = Build()); }
        }

        private static Texture2D Build()
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false)
            {
                name = "PR_DartBoard",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[Size * Size];
            float center = Size * 0.5f;
            float scale = Margin / center;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // 2×2 초과표집. 철사가 1픽셀 안팎이라 이것 없이는 층이 진다.
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

            if (radius > 1.03f) return Color.clear;
            if (radius > DartBoard.DoubleOuter) return DartsTheme.Wire;

            // 불과 그 테두리.
            if (Near(radius, DartBoard.InnerBull) || Near(radius, DartBoard.OuterBull))
                return DartsTheme.Wire;
            if (radius < DartBoard.InnerBull) return DartsTheme.BandRed;
            if (radius < DartBoard.OuterBull) return DartsTheme.BandGreen;

            // 링 경계 철사.
            if (Near(radius, DartBoard.TripleInner) || Near(radius, DartBoard.TripleOuter)
                || Near(radius, DartBoard.DoubleInner))
                return DartsTheme.Wire;

            float angle = DartBoard.AngleOf(x, y);
            int index = DartBoard.SectorIndexAt(angle);

            // 섹터 경계 철사. 반지름 방향 선이므로 호 길이로 두께를 잰다.
            float offset = angle - DartBoard.SectorCenterAngle(index);
            if (Mathf.Abs(Mathf.Abs(offset) - DartBoard.SectorAngle * 0.5f) * radius < WireHalf)
                return DartsTheme.Wire;

            bool dark = index % 2 == 0;

            bool band = (radius >= DartBoard.TripleInner && radius <= DartBoard.TripleOuter)
                        || radius >= DartBoard.DoubleInner;

            if (band) return dark ? DartsTheme.BandRed : DartsTheme.BandGreen;
            return dark ? DartsTheme.SectorDark : DartsTheme.SectorLight;
        }

        private static bool Near(float radius, float edge)
        {
            return Mathf.Abs(radius - edge) < WireHalf;
        }
    }
}
