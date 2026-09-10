using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 판 앞의 자리. 몰입 모드 2칸이 켜져 있든 아니든 같은 규칙이다.
    ///
    /// **바닐라가 의자를 요구하는 놀이는 의자가 없으면 아예 시작하지 않는다.**
    /// <c>JoyGiver_InteractBuildingSitAdjacent.TryGivePlayJob</c> 은 칸을 두 바퀴 도는데,
    /// 첫 바퀴는 의자 칸만 보고 <c>def.requireChair</c> 면 거기서 <c>break</c> 한다 —
    /// 그러면 <c>null</c> 이 나가고 그 폰은 그 가구로 놀러 가지 않는다.
    /// 체스와 포커가 그 경우다(우르만 <c>requireChair false</c> 를 적어 두었다).
    /// </summary>
    public static class Seating
    {
        /// <summary>이 가구의 놀이가 의자를 요구하는가.</summary>
        public static bool Required(Thing board)
        {
            if (board == null) return false;

            CompMiniGame comp = board.TryGetComp<CompMiniGame>();
            return comp != null && comp.Game != null && comp.Game.RequiresChair;
        }

        /// <summary>
        /// 빈 자리 하나. 의자가 있는 칸을 한 바퀴 다 본 뒤에야 맨바닥을 본다 —
        /// 바닐라도 그 순서다. 의자를 요구하는 놀이면 맨바닥은 아예 안 본다.
        ///
        /// <paramref name="across"/> 가 유효하면 **그 칸에서 가장 먼 자리**를 고른다.
        /// 체스판처럼 1×1 이면 정확히 마주 보는 칸이고, 큰 가구에서도 "판 건너" 로 읽힌다.
        /// 의자가 먼저다 — 마주 보자고 맨바닥에 세우지는 않는다.
        /// </summary>
        public static IntVec3 Find(Pawn pawn, Thing board, IntVec3 across)
        {
            if (pawn == null || board == null || !board.Spawned) return IntVec3.Invalid;

            IntVec3 chair = IntVec3.Invalid;
            IntVec3 bare = IntVec3.Invalid;
            int chairScore = -1;
            int bareScore = -1;

            foreach (IntVec3 cell in GenAdjFast.AdjacentCellsCardinal(board))
            {
                if (!Free(pawn, board, cell)) continue;

                // 맞은편을 모르면 전부 0 이라 먼저 걸린 칸이 남는다 — 예전 그대로다.
                int score = across.IsValid ? (cell - across).LengthHorizontalSquared : 0;

                if (Chair(board.Map, cell))
                {
                    if (score > chairScore) { chairScore = score; chair = cell; }
                }
                else if (score > bareScore) { bareScore = score; bare = cell; }
            }

            if (chair.IsValid) return chair;

            return Required(board) ? IntVec3.Invalid : bare;
        }

        /// <summary>
        /// 이 사람이 쓸 수 있는 자리가 몇 개인가. 둘이 두려면 둘이 있어야 한다.
        /// 아무도 정해지지 않았으면(가구만 클릭한 기즈모) 예약·경로는 묻지 않고 칸만 본다.
        /// </summary>
        public static int Count(Pawn pawn, Thing board)
        {
            if (board == null || !board.Spawned || board.Map == null) return 0;

            bool chairOnly = Required(board);
            int found = 0;

            foreach (IntVec3 cell in GenAdjFast.AdjacentCellsCardinal(board))
            {
                bool usable = pawn != null ? Free(pawn, board, cell) : Open(board.Map, cell);
                if (!usable) continue;

                if (chairOnly && !Chair(board.Map, cell)) continue;
                found++;
            }

            return found;
        }

        /// <summary>이 사람이 저 칸을 잡을 수 있는가.</summary>
        public static bool Free(Pawn pawn, Thing board, IntVec3 cell)
        {
            Map map = board != null ? board.Map : null;

            if (pawn == null || map == null || !cell.InBounds(map)) return false;
            if (cell.IsForbidden(pawn) || !cell.Standable(map)) return false;
            if (!pawn.CanReserveSittableOrSpot(cell)) return false;

            return pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some);
        }

        /// <summary>사람을 모르고 칸만 볼 때. 예약과 경로는 그 사람이 정해진 뒤에 묻는다.</summary>
        private static bool Open(Map map, IntVec3 cell)
        {
            return cell.InBounds(map) && cell.Standable(map);
        }

        private static bool Chair(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def.building != null && edifice.def.building.isSittable;
        }
    }
}
