using RimWorld;
using Verse;

namespace PlayableRecreation
{
    [DefOf]
    public static class PRDefOf
    {
        /// <summary>'몰입 모드' 전용. 가구까지 걸어간 뒤 창을 연다.</summary>
        public static JobDef PR_GoToGame;

        /// <summary>몰입 모드 2칸. 상대가 판 앞으로 가 그 자리에 선다.</summary>
        public static JobDef PR_JoinGame;

        static PRDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PRDefOf));
        }
    }
}
