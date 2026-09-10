using RimWorld;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 판 앞에 붙잡아 두는 토일. 두 JobDriver 가 같은 것을 쓴다.
    ///
    /// 여가 Job 이 아니므로 바닐라가 여가를 채워 주지 않는다. 그래서 여기서 직접 채운다 —
    /// 얼마를 줄지는 <see cref="MiniGameDef.vanillaJob"/> 이 이미 들고 있는 값에서 가져온다.
    /// </summary>
    public static class TogetherToils
    {
        /// <summary>바닐라 여가 Job 이 쓰는 것과 같은 잣대. 설정으로 빼지 않는다(§6.3).</summary>
        private const float HungerThreshold = 0.15f;
        private const float RestThreshold = 0.15f;

        private const int CheckInterval = 60;
        private const float JoyPerTick = 0.0000375f;

        /// <summary>
        /// 판이 끝날 때까지 서 있는다. 끝내는 조건은 §6.3 표 그대로다.
        /// </summary>
        public static Toil Hold(JobDriver driver, TargetIndex boardIndex)
        {
            Toil toil = ToilMaker.MakeToil("PR_HoldAtBoard");

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;

            // 붙잡혀 있는 동안 바닐라가 대화를 굴린다. D6 이 성립하는 자리가 여기다.
            toil.socialMode = RandomSocialMode.Normal;

            toil.initAction = delegate
            {
                Pawn pawn = driver.pawn;
                Thing board = driver.job.GetTarget(boardIndex).Thing;
                if (board != null) pawn.rotationTracker.FaceTarget(board);
            };

            toil.tickAction = delegate
            {
                Pawn pawn = driver.pawn;
                Thing board = driver.job.GetTarget(boardIndex).Thing;

                if (board == null)
                {
                    driver.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                GainJoy(pawn, board);

                if (!pawn.IsHashIntervalTick(CheckInterval)) return;

                FaceAcross(pawn, board);

                // 판도 없고 기다리는 것도 없다. 서 있을 이유가 사라졌다.
                if (!TogetherMatch.ShouldHold(board, pawn))
                {
                    driver.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                // D4 — 배고픔·피로는 멈춤이 아니라 자리를 뜨는 것으로 푼다.
                if (NeedsBreak(pawn))
                {
                    driver.EndJobWith(JobCondition.InterruptForced);
                    return;
                }
            };

            // 여기에 마무리 동작을 달아 판을 지우지 않는다. 바닐라는 **그때 실행 중이던
            // 토일 하나의** 마무리만 부르므로, 걸어가는 도중에 빠지면 이 토일은 시작조차
            // 안 해 마무리도 없다. 치우는 일은 TogetherMatch.Sweep 이 밖에서 맡는다.
            return toil;
        }

        /// <summary>
        /// 상대를 기다리며 판 앞에 선다. **먼저 청한 손님이 쓰는 토일이다.**
        ///
        /// 끝낼 때를 여기서 재지 않는다 — 시간은 <see cref="TogetherInvite"/> 가 재고
        /// 여기는 그 답만 본다. 손님이 도중에 다른 일을 받으면 이 토일은 아예 안 돌기 때문이다.
        /// </summary>
        public static Toil Wait(JobDriver driver, TargetIndex boardIndex)
        {
            Toil toil = ToilMaker.MakeToil("PR_WaitAtBoard");

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;

            // 기다리는 동안에도 바닐라가 대화를 굴린다. 다만 손님은 같은 팩션끼리만 굴린다(D6).
            toil.socialMode = RandomSocialMode.Normal;

            toil.initAction = delegate
            {
                Thing board = driver.job.GetTarget(boardIndex).Thing;
                if (board != null) driver.pawn.rotationTracker.FaceTarget(board);
            };

            toil.tickAction = delegate
            {
                Pawn pawn = driver.pawn;
                Thing board = driver.job.GetTarget(boardIndex).Thing;

                if (board == null)
                {
                    driver.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!pawn.IsHashIntervalTick(CheckInterval)) return;

                pawn.rotationTracker.FaceTarget(board);

                // 청이 지워졌으면 서 있을 이유가 없다. 혼자 두기로 넘어갔다면 이미 새 Job 이다.
                if (!TogetherInvite.IsWaiting(pawn))
                {
                    driver.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (NeedsBreak(pawn))
                {
                    driver.EndJobWith(JobCondition.InterruptForced);
                    return;
                }
            };

            return toil;
        }

        /// <summary>
        /// 이 사람이 앉을 자리를 잡아 <see cref="TargetIndex.C"/> 에 넣는다.
        /// <b>예약까지 여기서 한다</b> — 잡아 두지 않으면 다음 사람이 같은 칸을 고른다.
        ///
        /// 여태 <c>PathEndMode.Touch</c> 로만 걸어가게 두었는데, Touch 는 "닿기만 하면
        /// 된다" 라서 둘이 **같은 칸에 겹쳐 선다.** 실제로 겹쳤다(QA-01). 바닐라
        /// <c>JoyGiver_InteractBuildingSitAdjacent</c> 가 자리를 먼저 예약하는 이유가 이것이다.
        ///
        /// 규칙도 바닐라와 같다 — 가구에 상하좌우로 붙은 칸, 의자가 있으면 그쪽이 먼저.
        /// 자리를 못 잡아도 Job 을 실패시키지 않는다. 겹쳐 서는 것이 못 노는 것보다 낫다.
        /// </summary>
        public static bool ClaimSeat(Pawn pawn, Job job, Thing board, TargetIndex index)
        {
            if (pawn == null || job == null || board == null || !board.Spawned) return false;

            IntVec3 spot = FindSeat(pawn, board);
            if (!spot.IsValid) return false;

            if (!pawn.ReserveSittableOrSpot(spot, job, false)) return false;

            job.SetTarget(index, spot);
            return true;
        }

        /// <summary>
        /// 빈 자리 하나. 의자가 있는 칸을 한 바퀴 다 본 뒤에야 맨바닥을 본다 —
        /// 바닐라도 그 순서다(<c>requireChair</c> 가 아니면 두 바퀴를 돈다).
        ///
        /// 맞은편 사람이 이미 앉을 자리를 잡았으면 **그 사람에게서 가장 먼 칸**을 고른다.
        /// 체스판처럼 1×1 이면 정확히 마주 보는 칸이고, 큰 가구에서도 "판 건너" 로 읽힌다.
        /// 의자가 먼저다 — 마주 보자고 맨바닥에 세우지는 않는다.
        /// </summary>
        private static IntVec3 FindSeat(Pawn pawn, Thing board)
        {
            IntVec3 across = PartnerSeat(pawn, board);

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

            return chair.IsValid ? chair : bare;
        }

        /// <summary>
        /// 맞은편 사람이 이미 잡아 둔 칸. 아직 안 잡았거나 상대가 없으면 Invalid 다.
        ///
        /// <c>TogetherMatch.Arrange</c> 가 상대를 먼저 보내므로, 나중에 앉는 쪽이 이 값을 본다.
        /// 먼저 앉는 쪽은 아직 아무것도 모르니 예전처럼 첫 의자를 잡는다 — 그거면 충분하다.
        /// </summary>
        private static IntVec3 PartnerSeat(Pawn pawn, Thing board)
        {
            Pawn partner = TogetherMatch.PartnerOf(board, pawn);
            if (partner == null || partner.CurJob == null) return IntVec3.Invalid;

            JobDef def = partner.CurJobDef;
            if (def != PRDefOf.PR_GoToGame && def != PRDefOf.PR_JoinGame && def != PRDefOf.PR_InviteGame)
                return IntVec3.Invalid;

            LocalTargetInfo seat = partner.CurJob.GetTarget(TargetIndex.C);
            return seat.IsValid ? seat.Cell : IntVec3.Invalid;
        }

        private static bool Free(Pawn pawn, Thing board, IntVec3 cell)
        {
            Map map = board.Map;

            if (map == null || !cell.InBounds(map)) return false;
            if (cell.IsForbidden(pawn) || !cell.Standable(map)) return false;
            if (!pawn.CanReserveSittableOrSpot(cell)) return false;

            return pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some);
        }

        private static bool Chair(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def.building != null && edifice.def.building.isSittable;
        }

        /// <summary>
        /// 이미 판 앞에 닿았는가. Job 은 걷기와 붙잡히기를 한 묶음으로 갖고 있어
        /// 보고문구가 하나라, 앉아 두는 동안에도 "가는 중" 이라고 뜨는 것을 막는다.
        /// </summary>
        public static bool Arrived(Pawn pawn, Thing board)
        {
            return pawn != null && board != null && board.Spawned && pawn.Spawned
                && pawn.Map == board.Map && pawn.Position.AdjacentTo8WayOrInside(board);
        }

        /// <summary>이 가구가 무슨 게임인가. 모르면 null.</summary>
        public static string GameLabel(Thing board)
        {
            if (board == null) return null;

            CompMiniGame comp = board.TryGetComp<CompMiniGame>();
            return comp != null && comp.Game != null ? comp.Game.LabelCap : null;
        }

        /// <summary>
        /// 마주 보게 세운다. 맞은편을 모르면 판을 본다 — 허공을 보고 서 있지는 않게 한다.
        /// 토일이 handlingFacing 을 가져갔으므로 바닐라가 방향을 대신 돌려 주지 않는다.
        /// </summary>
        private static void FaceAcross(Pawn pawn, Thing board)
        {
            Pawn partner = TogetherMatch.PartnerOf(board, pawn);

            if (partner != null && partner.Spawned && partner.Map == pawn.Map)
                pawn.rotationTracker.FaceTarget(partner);
            else
                pawn.rotationTracker.FaceTarget(board);
        }

        /// <summary>
        /// 여가가 가득 찼다고 판을 끊지는 않는다 — 바닐라 여가 Job 과 다른 점이다.
        /// 두던 판이 우선이고, 넘치는 만큼은 그냥 버려진다.
        /// </summary>
        private static void GainJoy(Pawn pawn, Thing board)
        {
            if (pawn.needs == null || pawn.needs.joy == null) return;

            CompMiniGame comp = board.TryGetComp<CompMiniGame>();
            if (comp == null || comp.Game == null) return;

            JobDef vanilla = comp.Game.vanillaJob;
            if (vanilla == null || vanilla.joyKind == null) return;

            float factor = board.GetStatValue(StatDefOf.JoyGainFactor);
            pawn.needs.joy.GainJoy(JoyPerTick * factor, vanilla.joyKind);

            if (vanilla.joySkill != null && pawn.skills != null && vanilla.joyXpPerTick > 0f)
                pawn.skills.Learn(vanilla.joySkill, vanilla.joyXpPerTick);
        }

        private static bool NeedsBreak(Pawn pawn)
        {
            if (pawn.needs == null) return false;

            if (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < HungerThreshold)
                return true;

            if (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < RestThreshold)
                return true;

            return false;
        }
    }
}
