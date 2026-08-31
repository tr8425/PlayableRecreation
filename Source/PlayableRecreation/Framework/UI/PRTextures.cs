using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 창 껍데기가 쓰는 기본 도형. 원과 고리는 어느 게임에나 필요해서 여기 둔다 -
    /// 게임끼리 서로의 텍스처를 빌려 쓰지 않게 하려는 것이다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PRTextures
    {
        private const int Size = 64;

        /// <summary>채워진 원. 쪽 표시 점이자 말 하나.</summary>
        public static readonly Texture2D Dot = Build(Size, delegate (float dx, float dy)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((0.94f - r) * Size * 0.25f);
        });

        /// <summary>가운데가 뚫린 고리. 색이 아닌 모양으로 편을 나눌 때 쓴다.</summary>
        public static readonly Texture2D Ring = Build(Size, delegate (float dx, float dy)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((0.94f - r) * Size * 0.25f) * Mathf.Clamp01((r - 0.50f) * Size * 0.25f);
        });

        /// <summary>가느다란 원 테두리. 과녁의 등고선.</summary>
        public static readonly Texture2D Outline = Build(Size, delegate (float dx, float dy)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((0.97f - r) * Size * 0.5f) * Mathf.Clamp01((r - 0.86f) * Size * 0.5f);
        });

        private delegate float AlphaAt(float dx, float dy);

        private static Texture2D Build(int size, AlphaAt alphaAt)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alphaAt(dx, dy));
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
