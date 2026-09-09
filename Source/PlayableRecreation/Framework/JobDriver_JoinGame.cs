using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 상대 쪽. 판 앞까지 걸어가 마주 본 뒤, 판이 끝날 때까지 그 자리에 선다.
    /// 창을 여는 것은 주도한 쪽이 아니라 <see cref="TogetherMatch"/> 다 — 둘 다 닿아야 열린다.
    /// </summary>
    public class JobDriver_JoinGame : JobDriver
    {
        private Thing Board
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        private Pawn Partner
        {
            get { return job.GetTarget(TargetIndex.B).Thing as Pawn; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 주도한 쪽과 **반드시 같은** 수여야 예약이 붙는다 -
            // ReservationManager 는 MaxPawns 가 다르면 개수와 무관하게 즉시 거절한다.
            if (!pawn.Reserve(job.targetA, job, Together.SeatCount(Board), 0, null, errorOnFailed))
                return false;

            // 상대 폰 자체도 잡는다. 가구만 예약해서는 같은 사람을 두 판에 지목하는 것을
            // 막지 못한다 - 보드가 둘이면 각각 두 자리를 따로 잡기 때문이다.
            return pawn.Reserve(job.targetB, job, 1, 0, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil arrive = ToilMaker.MakeToil("PR_JoinArrived");
            arrive.defaultCompleteMode = ToilCompleteMode.Instant;
            arrive.initAction = delegate
            {
                Thing board = Board;
                if (board == null) return;

                Pawn partner = Partner;
                if (partner != null) pawn.rotationTracker.FaceTarget(partner);
                else pawn.rotationTracker.FaceTarget(board);

                TogetherMatch.NotifyArrived(board, pawn);
            };
            yield return arrive;

            yield return TogetherToils.Hold(this, TargetIndex.A);
        }
    }
}
