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

        public static readonly Color Moon = new Color(0.88f, 0.88f, 0.82f);
        public static readonly Color Craft = new Color(0.58f, 0.92f, 0.70f);
        public static readonly Color Rock = new Color(0.82f, 0.66f, 0.45f);

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
