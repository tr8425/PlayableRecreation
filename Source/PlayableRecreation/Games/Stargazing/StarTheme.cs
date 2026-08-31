using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Stargazing
{
    /// <summary>밤하늘의 색.</summary>
    public static class StarTheme
    {
        public static readonly Color Sky = new Color(0.035f, 0.045f, 0.075f);
        public static readonly Color SkyDay = new Color(0.20f, 0.29f, 0.42f);
        public static readonly Color Horizon = new Color(0.42f, 0.46f, 0.55f, 0.55f);
        public static readonly Color Grid = new Color(0.42f, 0.46f, 0.55f, 0.16f);
        public static readonly Color Compass = new Color(0.72f, 0.76f, 0.84f, 0.70f);

        public static readonly Color Known = new Color(0.46f, 0.62f, 0.92f, 0.55f);
        public static readonly Color KnownInk = new Color(0.62f, 0.74f, 0.96f, 0.85f);
        public static readonly Color Mine = new Color(0.92f, 0.78f, 0.38f, 0.70f);
        public static readonly Color MineInk = new Color(0.96f, 0.86f, 0.55f);

        public static readonly Color Drawing = new Color(0.55f, 0.92f, 0.66f, 0.85f);
        public static readonly Color Pick = new Color(0.62f, 0.96f, 0.72f);

        /// <summary>대기 밖의 바탕. 밤하늘보다 한 단계 더 검다.</summary>
        public static readonly Color Void = new Color(0.02f, 0.02f, 0.035f);

        public static readonly Color Planet = new Color(0.42f, 0.52f, 0.66f);
        public static readonly Color PlanetRim = new Color(0.58f, 0.70f, 0.88f, 0.45f);

        public static readonly Color Moon = new Color(0.88f, 0.88f, 0.82f);
        public static readonly Color Craft = new Color(0.58f, 0.92f, 0.70f);
        public static readonly Color Rock = new Color(0.82f, 0.66f, 0.45f);

        /// <summary>
        /// 땅의 색. 지어낸 색표가 아니라 그 타일이 실제로 가진 기온·강수·고도에서 나온다 -
        /// 추운 곳은 희고, 더운 데다 젖으면 푸르고, 더운 데다 마르면 누렇다.
        /// 밤인 칸은 그대로 어둡게 눌러 준다. 명암 경계선이 화면을 가로지르는 이유다.
        /// </summary>
        public static Color GroundInk(GroundCell cell)
        {
            Color ink;

            if (cell.Water)
            {
                ink = Color.Lerp(new Color(0.66f, 0.74f, 0.80f),   // 언 바다
                                 new Color(0.10f, 0.24f, 0.40f),   // 깊은 물
                                 cell.Warm);
            }
            else
            {
                Color cold = Color.Lerp(new Color(0.80f, 0.83f, 0.87f),   // 얼음
                                        new Color(0.26f, 0.38f, 0.32f),   // 침엽수림
                                        cell.Wet);

                Color warm = Color.Lerp(new Color(0.78f, 0.68f, 0.45f),   // 모래
                                        new Color(0.22f, 0.48f, 0.24f),   // 밀림
                                        cell.Wet);

                ink = Color.Lerp(cold, warm, cell.Warm);
                ink = Color.Lerp(ink, new Color(0.48f, 0.45f, 0.43f), cell.High * 0.60f);
            }

            if (!cell.Lit) ink *= 0.34f;

            ink.a = 1f;
            return ink;
        }

        /// <summary>푸른 별에서 붉은 별까지. 등급이 낮을수록 희게 뜬다.</summary>
        public static Color StarColor(float warmth, float alpha)
        {
            Color cool = new Color(0.72f, 0.80f, 1.00f);
            Color warm = new Color(1.00f, 0.80f, 0.62f);

            Color mixed = Color.Lerp(cool, warm, warmth);
            mixed.a = alpha;
            return mixed;
        }
    }

    [StaticConstructorOnStartup]
    public static class StarSounds
    {
        public static readonly SoundDef Pick = PRSounds.Lookup("Tick_Tiny");
        public static readonly SoundDef Name = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Cancel = PRSounds.Lookup("ClickReject");
    }
}
