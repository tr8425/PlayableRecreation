using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Darts
{
    /// <summary>
    /// 다트판의 색. 경기용 판의 배색을 이 모드의 가라앉은 색감으로 옮겼다 -
    /// 원색 대신 벽돌과 이끼, 검정 대신 그을음.
    /// </summary>
    public static class DartsTheme
    {
        public static readonly Color SectorLight = new Color(0.84f, 0.78f, 0.66f);
        public static readonly Color SectorDark = new Color(0.23f, 0.21f, 0.19f);
        public static readonly Color BandRed = new Color(0.70f, 0.32f, 0.27f);
        public static readonly Color BandGreen = new Color(0.34f, 0.52f, 0.38f);
        public static readonly Color Wire = new Color(0.12f, 0.11f, 0.10f);

        public static readonly Color Numbers = new Color(0.72f, 0.66f, 0.55f);
        public static readonly Color Crosshair = new Color(0.95f, 0.88f, 0.60f, 0.9f);

        public static readonly Color PlayerMark = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color OpponentMark = new Color(0.58f, 0.68f, 0.80f);

        // 막대는 던지기 게임들과 같은 생김새다. 색만 같고 서로를 참조하지는 않는다.
        public static readonly Color BarBack = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color BarSweet = new Color(0.42f, 0.82f, 0.45f, 0.45f);
        public static readonly Color BarMarker = new Color(0.95f, 0.88f, 0.60f);
    }

    /// <summary>바닐라 UI 사운드를 빌려 쓴다. 던지기 게임들과 같은 소리라 손맛도 이어진다.</summary>
    [StaticConstructorOnStartup]
    public static class DartsSounds
    {
        public static readonly SoundDef Lock = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Land = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Big = PRSounds.Lookup("TinyBell");
    }
}
