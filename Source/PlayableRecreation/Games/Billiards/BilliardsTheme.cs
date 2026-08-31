using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Billiards
{
    /// <summary>당구대의 색. 다른 게임의 색을 빌려오지 않는다.</summary>
    public static class BilliardsTheme
    {
        public static readonly Color Felt = new Color(0.13f, 0.31f, 0.21f);
        public static readonly Color FeltEdge = new Color(0.10f, 0.24f, 0.16f);
        public static readonly Color Rail = new Color(0.26f, 0.17f, 0.11f);
        public static readonly Color Pocket = new Color(0.05f, 0.05f, 0.05f);

        public static readonly Color Cue = new Color(0.96f, 0.94f, 0.88f);
        public static readonly Color AimLine = new Color(1f, 1f, 1f, 0.45f);
        public static readonly Color GhostBall = new Color(1f, 1f, 1f, 0.22f);

        public static readonly Color PowerBack = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color PowerFill = new Color(0.92f, 0.72f, 0.34f);

        /// <summary>1~9번 공. 8번만 검정이라 테두리를 따로 그린다.</summary>
        public static readonly Color[] Numbers =
        {
            new Color(0.96f, 0.94f, 0.88f),   // 0 큐볼
            new Color(0.93f, 0.79f, 0.24f),   // 1 노랑
            new Color(0.24f, 0.42f, 0.78f),   // 2 파랑
            new Color(0.80f, 0.25f, 0.22f),   // 3 빨강
            new Color(0.48f, 0.32f, 0.65f),   // 4 보라
            new Color(0.90f, 0.53f, 0.22f),   // 5 주황
            new Color(0.24f, 0.60f, 0.36f),   // 6 초록
            new Color(0.56f, 0.22f, 0.24f),   // 7 밤색
            new Color(0.13f, 0.13f, 0.14f),   // 8 검정
            new Color(0.93f, 0.79f, 0.24f),   // 9 노랑 줄무늬
        };
    }

    /// <summary>바닐라 UI 사운드를 빌려 쓴다.</summary>
    [StaticConstructorOnStartup]
    public static class BilliardsSounds
    {
        public static readonly SoundDef Strike = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Pocket = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Foul = PRSounds.Lookup("ClickReject");
        public static readonly SoundDef Lock = PRSounds.Lookup("Tick_High");
    }
}
