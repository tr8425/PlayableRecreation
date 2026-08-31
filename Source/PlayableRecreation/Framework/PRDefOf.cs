using RimWorld;
using Verse;

namespace PlayableRecreation
{
    [DefOf]
    public static class PRDefOf
    {
        /// <summary>'몰입 모드' 전용. 가구까지 걸어간 뒤 창을 연다.</summary>
        public static JobDef PR_GoToGame;

        static PRDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PRDefOf));
        }
    }
}
