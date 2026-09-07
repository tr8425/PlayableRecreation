using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Roulette
{
    /// <summary>
    /// 룰렛의 색. 카지노의 초록 펠트와 휠의 빨강·검정을 이 모드의 가라앉은 색감으로 옮겼다.
    /// 다트판과 같은 벽돌빛 빨강이지만 서로를 참조하지는 않는다 - 게임끼리는 모른다.
    /// </summary>
    public static class RouletteTheme
    {
        public static readonly Color Felt = new Color(0.22f, 0.28f, 0.24f);
        public static readonly Color Rim = new Color(0.30f, 0.24f, 0.20f);
        public static readonly Color Hub = new Color(0.16f, 0.14f, 0.12f);
        public static readonly Color Wire = new Color(0.12f, 0.11f, 0.10f);

        public static readonly Color PocketRed = new Color(0.70f, 0.32f, 0.27f);
        public static readonly Color PocketBlack = new Color(0.21f, 0.20f, 0.18f);
        public static readonly Color PocketGreen = new Color(0.34f, 0.52f, 0.38f);

        public static readonly Color Ball = new Color(0.92f, 0.90f, 0.84f);
        public static readonly Color CellText = new Color(0.92f, 0.89f, 0.80f);
        public static readonly Color Select = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color Bankroll = new Color(0.93f, 0.75f, 0.38f);
        public static readonly Color WinFlash = new Color(0.93f, 0.75f, 0.38f, 0.35f);
    }

    /// <summary>바닐라 UI 사운드를 빌려 쓴다. 화면에 없는 월드 사운드는 쓰지 않는다.</summary>
    [StaticConstructorOnStartup]
    public static class RouletteSounds
    {
        public static readonly SoundDef Pick = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Spin = PRSounds.Lookup("Crunch");
        public static readonly SoundDef Tick = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Win = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Big = PRSounds.Lookup("Message_PositiveEvent");
    }
}
