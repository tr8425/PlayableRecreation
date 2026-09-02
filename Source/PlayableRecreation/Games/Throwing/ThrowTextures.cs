using UnityEngine;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 편자 모양. 회전 행렬은 UI 배율·피벗 변환에 따라 위치가 틀어지는 사고가 잦아서
    /// 아예 방향별로 미리 구워 둔다. 그릴 때는 트인 쪽이 향할 각도로 한 장을 고를 뿐이다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ThrowTextures
    {
        private const int Size = 64;

        /// <summary>구워 두는 방향 수. 15도 간격이면 트인 방향이 어긋나 보이지 않는다.</summary>
        private const int Steps = 24;

        /// <summary>트인 각도의 절반. 이보다 좁으면 고리로, 넓으면 낫으로 보인다.</summary>
        private const float GapHalf = 0.65f;

        /// <summary>당신의 편자. 두껍다.</summary>
        private static readonly Texture2D[] Shoe = Bake(0.40f);

        /// <summary>상대의 편자. 가늘게 그려 색이 아닌 모양으로도 편이 갈리게 한다.</summary>
        private static readonly Texture2D[] ShoeThin = Bake(0.62f);

        /// <summary>트인 쪽이 화면 각도 facing(+x 오른쪽, +y 아래) 을 향하는 변형.</summary>
        public static Texture2D For(bool thick, float facing)
        {
            int step = Mathf.RoundToInt(facing / (Mathf.PI * 2f) * Steps);
            step = (step % Steps + Steps) % Steps;
            return (thick ? Shoe : ShoeThin)[step];
        }

        private static Texture2D[] Bake(float inner)
        {
            Texture2D[] all = new Texture2D[Steps];
            for (int k = 0; k < Steps; k++) all[k] = Build(inner, k * Mathf.PI * 2f / Steps);
            return all;
        }

        private static Texture2D Build(float inner, float facing)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[Size * Size];
            float center = (Size - 1) * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - center) / center;
                    // 텍스처의 y 는 위로, 화면의 y 는 아래로 자란다. 화면 기준으로 계산한다.
                    float dy = (center - y) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float band = Mathf.Clamp01((0.94f - r) * Size * 0.25f)
                               * Mathf.Clamp01((r - inner) * Size * 0.25f);

                    float away = Mathf.Atan2(dy, dx) - facing;
                    away = Mathf.Abs(Mathf.Repeat(away + Mathf.PI, Mathf.PI * 2f) - Mathf.PI);
                    band *= Mathf.Clamp01((away - GapHalf) * 5f);

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
