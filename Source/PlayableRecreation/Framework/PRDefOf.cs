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

        /// <summary>둘이 한 판을 끝까지 뒀다.</summary>
        public static ThoughtDef PR_PlayedTogether;

        /// <summary>판 앞에 있던 사람이 없어졌다.</summary>
        public static ThoughtDef PR_OpponentGone;

        static PRDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PRDefOf));
        }
    }
}
