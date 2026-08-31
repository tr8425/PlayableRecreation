using UnityEngine;
using Verse;

namespace RoyalGameOfUr
{
    /// <summary>
    /// 절차적으로 생성하는 UI 텍스처. M2 단계에서는 아트 에셋 없이 도형만으로 판을 읽을 수 있게 한다.
    /// 확정된 아트 방향(바닐라 톤 손그림)은 M4 이후 Textures/RGU/ 의 실제 이미지로 교체한다.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class UrTextures
    {
        private const int Size = 64;

        /// <summary>플레이어 말. 채워진 원.</summary>
        public static readonly Texture2D Disc = MakeDisc(Size);

        /// <summary>봇 말. 가운데가 뚫린 고리 — 색맹 대응을 위해 색이 아닌 모양으로 구분한다.</summary>
        public static readonly Texture2D Ring = MakeRing(Size);

        /// <summary>로제트 칸 표식. 메소포타미아식 8엽 문양.</summary>
        public static readonly Texture2D Rosette = MakeRosette(Size, 8);

        private static Texture2D MakeDisc(int size)
        {
            return Build(size, delegate (float dx, float dy)
            {
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01((0.94f - r) * size * 0.25f);
            });
        }

        private static Texture2D MakeRing(int size)
        {
            return Build(size, delegate (float dx, float dy)
            {
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((0.94f - r) * size * 0.25f);
                float inner = Mathf.Clamp01((r - 0.50f) * size * 0.25f);
                return outer * inner;
            });
        }

        private static Texture2D MakeRosette(int size, int petals)
        {
            return Build(size, delegate (float dx, float dy)
            {
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.52f + 0.34f * Mathf.Cos(petals * angle);
                return Mathf.Clamp01((edge - r) * size * 0.30f);
            });
        }

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

    public static class UrTheme
    {
        public static readonly Color Cell = new Color(0.20f, 0.18f, 0.15f);
        public static readonly Color CellRosette = new Color(0.31f, 0.25f, 0.13f);
        public static readonly Color CellBorder = new Color(0.52f, 0.45f, 0.34f);
        public static readonly Color RosetteMark = new Color(0.78f, 0.64f, 0.32f, 0.55f);

        public static readonly Color PlayerPiece = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color BotPiece = new Color(0.58f, 0.68f, 0.80f);
        public static readonly Color EmptySlot = new Color(1f, 1f, 1f, 0.13f);

        public static readonly Color LegalSource = new Color(0.42f, 0.82f, 0.45f);
        public static readonly Color LegalHover = new Color(0.62f, 0.95f, 0.62f);
        public static readonly Color MoveTarget = new Color(0.42f, 0.82f, 0.45f, 0.30f);
        public static readonly Color CaptureTarget = new Color(0.88f, 0.36f, 0.31f, 0.35f);
        public static readonly Color CaptureBorder = new Color(0.92f, 0.44f, 0.38f);
        public static readonly Color BearOff = new Color(0.95f, 0.85f, 0.45f);

        public static readonly Color DieMarked = new Color(0.93f, 0.86f, 0.62f);
        public static readonly Color DieBlank = new Color(1f, 1f, 1f, 0.28f);
        public static readonly Color DieHidden = new Color(1f, 1f, 1f, 0.13f);

        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Paused = new Color(0.55f, 0.85f, 0.55f, 0.9f);
        public static readonly Color Running = new Color(0.92f, 0.78f, 0.42f, 0.9f);
        public static readonly Color ActiveTurn = new Color(0.95f, 0.88f, 0.60f);
    }
}
