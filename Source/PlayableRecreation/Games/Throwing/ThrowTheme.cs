using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Throwing
{
    /// <summary>과녁의 색. 우르의 보드와 계열은 같지만 서로를 참조하지 않는다.</summary>
    public static class ThrowTheme
    {
        public static readonly Color Pin = new Color(0.78f, 0.68f, 0.42f);
        public static readonly Color ScoreRing = new Color(0.52f, 0.45f, 0.34f, 0.75f);
        public static readonly Color RingerRing = new Color(0.85f, 0.70f, 0.34f, 0.85f);

        public static readonly Color PlayerMark = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color OpponentMark = new Color(0.58f, 0.68f, 0.80f);

        public static readonly Color BarBack = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color BarSweet = new Color(0.42f, 0.82f, 0.45f, 0.45f);
        public static readonly Color BarMarker = new Color(0.95f, 0.88f, 0.60f);
    }

    /// <summary>바닐라 UI 사운드를 빌려 쓴다.</summary>
    [StaticConstructorOnStartup]
    public static class ThrowSounds
    {
        public static readonly SoundDef Lock = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Land = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Ringer = PRSounds.Lookup("TinyBell");
    }
}
