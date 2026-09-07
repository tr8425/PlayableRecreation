using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Slots
{
    /// <summary>슬롯머신의 색. 카지노의 번쩍임을 이 모드의 가라앉은 색감으로 눌러 담았다.</summary>
    public static class SlotsTheme
    {
        public static readonly Color Cabinet = new Color(0.30f, 0.24f, 0.20f);
        public static readonly Color WindowBack = new Color(0.90f, 0.86f, 0.76f);
        public static readonly Color WindowEdge = new Color(0.16f, 0.14f, 0.12f);

        public static readonly Color Seven = new Color(0.70f, 0.32f, 0.27f);
        public static readonly Color Bar = new Color(0.23f, 0.21f, 0.19f);
        public static readonly Color Coin = new Color(0.85f, 0.70f, 0.34f);
        public static readonly Color Plum = new Color(0.34f, 0.52f, 0.38f);

        public static readonly Color WinFlash = new Color(0.93f, 0.75f, 0.38f, 0.35f);
        public static readonly Color Credits = new Color(0.93f, 0.75f, 0.38f);
    }

    /// <summary>바닐라 소리를 빌려 쓴다.</summary>
    [StaticConstructorOnStartup]
    public static class SlotsSounds
    {
        public static readonly SoundDef Pull = PRSounds.Lookup("Crunch");
        public static readonly SoundDef Stop = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Win = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Jackpot = PRSounds.Lookup("Message_PositiveEvent");
    }
}
