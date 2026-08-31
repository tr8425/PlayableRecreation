using PlayableRecreation;
using Verse;

namespace Poker
{
    /// <summary>포커만의 설정.</summary>
    public static class PokerSettings
    {
        private const string Prefix = "PR_Poker.";

        /// <summary>지금 내 손이 무엇인지(원페어, 플러시…) 아래에 적어 준다.</summary>
        public static bool ShowHandName
        {
            get { return Get("handName", true); }
            set { Set("handName", value); }
        }

        /// <summary>핸드가 끝나면 잠시 뒤 저절로 다음 핸드로 넘어간다.</summary>
        public static bool AutoNextHand
        {
            get { return Get("autoNext", true); }
            set { Set("autoNext", value); }
        }

        /// <summary>죽은 핸드에서도 상대의 손을 보여준다. 연습용이라 기본은 꺼져 있다.</summary>
        public static bool RevealFolded
        {
            get { return Get("reveal", false); }
            set { Set("reveal", value); }
        }

        public static void DoSettings(Listing_Standard list)
        {
            bool handName = ShowHandName;
            bool autoNext = AutoNextHand;
            bool reveal = RevealFolded;

            list.CheckboxLabeled("POK.Settings.HandName".Translate(), ref handName);
            list.CheckboxLabeled("POK.Settings.AutoNext".Translate(), ref autoNext);
            list.CheckboxLabeled("POK.Settings.Reveal".Translate(), ref reveal,
                "POK.Settings.Reveal.Desc".Translate());

            ShowHandName = handName;
            AutoNextHand = autoNext;
            RevealFolded = reveal;
        }

        private static bool Get(string key, bool fallback)
        {
            return PRMod.Settings.GetBool(Prefix + key, fallback);
        }

        private static void Set(string key, bool value)
        {
            PRMod.Settings.SetBool(Prefix + key, value);
        }
    }
}
