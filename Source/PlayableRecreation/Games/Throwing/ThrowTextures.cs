using UnityEngine;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 편자 모양. 트인 쪽이 +X 를 향하게 구웠으므로, 그릴 때 착지 각도만큼 돌리면
    /// 막대를 등지거나 껴안은 채 떨어진 것처럼 보인다. X 축 대칭이라 상하 반전에도 무사하다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ThrowTextures
    {
        private const int Size = 64;

        /// <summary>트인 각도의 절반. 이보다 좁으면 고리로, 넓으면 낫으로 보인다.</summary>
        private const float GapHalf = 0.65f;

        /// <summary>당신의 편자. 두껍다.</summary>
        public static readonly Texture2D Shoe = Build(0.40f);

        /// <summary>상대의 편자. 가늘게 그려 색이 아닌 모양으로도 편이 갈리게 한다.</summary>
        public static readonly Texture2D ShoeThin = Build(0.62f);

        private static Texture2D Build(float inner)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[Size * Size];
            float center = (Size - 1) * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float band = Mathf.Clamp01((0.94f - r) * Size * 0.25f)
                               * Mathf.Clamp01((r - inner) * Size * 0.25f);

                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx));
                    band *= Mathf.Clamp01((angle - GapHalf) * 5f);

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, band);
                }
            }

            tex.SetPixels(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }
    }
}
