using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Poker
{
    /// <summary>포커 테이블의 색과 무늬. 무늬는 알파 한 장씩이고 빨강·검정은 여기서 입힌다.</summary>
    [StaticConstructorOnStartup]
    public static class PokerTheme
    {
        public static readonly Color Felt = new Color(0.14f, 0.26f, 0.22f);
        public static readonly Color FeltEdge = new Color(0.10f, 0.19f, 0.16f);
        public static readonly Color Rail = new Color(0.24f, 0.16f, 0.12f);

        public static readonly Color CardFace = new Color(0.94f, 0.92f, 0.86f);
        public static readonly Color CardBack = new Color(0.42f, 0.20f, 0.20f);
        public static readonly Color CardBackMark = new Color(0.60f, 0.33f, 0.31f);
        public static readonly Color CardEdge = new Color(0.16f, 0.14f, 0.12f);
        public static readonly Color Empty = new Color(1f, 1f, 1f, 0.07f);

        public static readonly Color Red = new Color(0.74f, 0.20f, 0.17f);
        public static readonly Color Black = new Color(0.13f, 0.12f, 0.13f);

        public static readonly Color Chip = new Color(0.92f, 0.78f, 0.36f);
        public static readonly Color PotChip = new Color(0.95f, 0.87f, 0.55f);
        public static readonly Color Button = new Color(0.90f, 0.88f, 0.82f);
        public static readonly Color ButtonInk = new Color(0.18f, 0.16f, 0.14f);
        public static readonly Color Winner = new Color(0.42f, 0.72f, 0.46f);

        /// <summary><see cref="Core.Cards.Suit"/> 순서 - 클럽 · 다이아 · 하트 · 스페이드.</summary>
        public static readonly Texture2D[] Suits =
        {
            ContentFinder<Texture2D>.Get("PR/Cards/club"),
            ContentFinder<Texture2D>.Get("PR/Cards/diamond"),
            ContentFinder<Texture2D>.Get("PR/Cards/heart"),
            ContentFinder<Texture2D>.Get("PR/Cards/spade"),
        };

        public static Color InkFor(int suit)
        {
            return suit == 1 || suit == 2 ? Red : Black;
        }
    }

    [StaticConstructorOnStartup]
    public static class PokerSounds
    {
        public static readonly SoundDef Deal = PRSounds.Lookup("Tick_Tiny");
        public static readonly SoundDef Chips = PRSounds.Lookup("Tick_Low");
        public static readonly SoundDef Raise = PRSounds.Lookup("Tick_High");
        public static readonly SoundDef Win = PRSounds.Lookup("TinyBell");
        public static readonly SoundDef Fold = PRSounds.Lookup("ClickReject");
    }
}
