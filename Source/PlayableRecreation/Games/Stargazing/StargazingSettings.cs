using PlayableRecreation;
using Verse;

namespace Stargazing
{
    public static class StargazingSettings
    {
        private const string Prefix = "PR_Stargazing.";

        /// <summary>처음부터 그어져 있는 별자리를 보여준다.</summary>
        public static bool ShowKnown
        {
            get { return Get("known", true); }
            set { Set("known", value); }
        }

        /// <summary>별 이름표를 밝은 별 위에 띄운다.</summary>
        public static bool ShowLabels
        {
            get { return Get("labels", true); }
            set { Set("labels", value); }
        }

        /// <summary>낮이나 흐린 날에도 하늘 전체를 보여준다. 관측이 아니라 성도로 쓰는 셈이다.</summary>
        public static bool IgnoreConditions
        {
            get { return Get("ignoreSky", false); }
            set { Set("ignoreSky", value); }
        }

        public static void DoSettings(Listing_Standard list)
        {
            bool known = ShowKnown;
            bool labels = ShowLabels;
            bool ignore = IgnoreConditions;

            list.CheckboxLabeled("STG.Settings.Known".Translate(), ref known);
            list.CheckboxLabeled("STG.Settings.Labels".Translate(), ref labels);
            list.CheckboxLabeled("STG.Settings.Ignore".Translate(), ref ignore,
                "STG.Settings.Ignore.Desc".Translate());

            ShowKnown = known;
            ShowLabels = labels;
            IgnoreConditions = ignore;
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
