using PlayableRecreation;
using Verse;

namespace Chess
{
    /// <summary>체스만의 설정. 프레임워크의 설정 자루에 이름표를 붙여 얹는다.</summary>
    public static class ChessSettings
    {
        private const string Prefix = "PR_Chess.";

        /// <summary>고른 기물이 갈 수 있는 칸을 판 위에 표시한다.</summary>
        public static bool ShowLegalMoves
        {
            get { return Get("highlight", true); }
            set { Set("highlight", value); }
        }

        /// <summary>판 가장자리에 a~h · 1~8 을 적는다.</summary>
        public static bool ShowCoordinates
        {
            get { return Get("coords", true); }
            set { Set("coords", value); }
        }

        /// <summary>끄면 판마다 색이 바뀐다. 켜면 언제나 백을 잡는다.</summary>
        public static bool AlwaysWhite
        {
            get { return Get("alwaysWhite", false); }
            set { Set("alwaysWhite", value); }
        }

        public static void DoSettings(Listing_Standard list)
        {
            bool highlight = ShowLegalMoves;
            bool coords = ShowCoordinates;
            bool white = AlwaysWhite;

            list.CheckboxLabeled("CHS.Settings.Highlight".Translate(), ref highlight);
            list.CheckboxLabeled("CHS.Settings.Coords".Translate(), ref coords);
            list.CheckboxLabeled("CHS.Settings.AlwaysWhite".Translate(), ref white,
                "CHS.Settings.AlwaysWhite.Desc".Translate());

            ShowLegalMoves = highlight;
            ShowCoordinates = coords;
            AlwaysWhite = white;
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
