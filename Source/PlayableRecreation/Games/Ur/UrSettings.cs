using PlayableRecreation;
using UnityEngine;
using Verse;

namespace Ur
{
    /// <summary>
    /// 우르만의 설정. 프레임워크의 설정 자루에 이름표를 붙여 얹는다.
    /// 프레임워크는 이 항목들이 있다는 사실조차 모른다.
    /// </summary>
    public static class UrSettings
    {
        private const string Prefix = "PR_Ur.";

        /// <summary>합법수가 1개뿐일 때 클릭 없이 진행한다.</summary>
        public static bool AutoAdvanceSingleMove
        {
            get { return Get("autoAdvance", true); }
            set { Set("autoAdvance", value); }
        }

        /// <summary>선공을 무작위로 정한다. 끄면 항상 플레이어가 먼저 둔다.</summary>
        public static bool RandomFirstPlayer
        {
            get { return Get("randomFirst", false); }
            set { Set("randomFirst", value); }
        }

        public static bool HighlightLegalMoves
        {
            get { return Get("highlight", true); }
            set { Set("highlight", value); }
        }

        public static bool ShowCellTooltips
        {
            get { return Get("tooltips", true); }
            set { Set("tooltips", value); }
        }

        /// <summary>이 수를 두면 다음 턴에 잡힐 확률을 보여준다. 상급자용이라 기본 OFF.</summary>
        public static bool ShowRiskWarning
        {
            get { return Get("risk", false); }
            set { Set("risk", value); }
        }

        public static void DoSettings(Listing_Standard list)
        {
            bool highlight = HighlightLegalMoves;
            bool tooltips = ShowCellTooltips;
            bool risk = ShowRiskWarning;
            bool auto = AutoAdvanceSingleMove;
            bool first = RandomFirstPlayer;

            list.CheckboxLabeled("RGU.Settings.Highlight".Translate(), ref highlight);
            list.CheckboxLabeled("RGU.Settings.Tooltips".Translate(), ref tooltips);
            list.CheckboxLabeled("RGU.Settings.RiskWarning".Translate(), ref risk,
                "RGU.Settings.RiskWarning.Desc".Translate());
            list.CheckboxLabeled("RGU.Settings.AutoAdvance".Translate(), ref auto);
            list.CheckboxLabeled("RGU.Settings.RandomFirst".Translate(), ref first,
                "RGU.Settings.RandomFirst.Desc".Translate());

            HighlightLegalMoves = highlight;
            ShowCellTooltips = tooltips;
            ShowRiskWarning = risk;
            AutoAdvanceSingleMove = auto;
            RandomFirstPlayer = first;
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
