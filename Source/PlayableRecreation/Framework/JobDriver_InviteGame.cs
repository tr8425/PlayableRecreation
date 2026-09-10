using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 손님이 먼저 판 앞에 앉아 상대를 기다린다.
    ///
    /// 창은 이 Job 이 열지 않는다. 플레이어가 그 가구를 눌러 이 손님을 고르면 그때
    /// <see cref="TogetherMatch"/> 가 평소대로 둘을 부르고, 이 Job 은 자리를 내준다.
    /// 아무도 안 오면 <see cref="TogetherInvite"/> 가 혼자 두는 Job 으로 갈아 준다.
    /// </summary>
    public class JobDriver_InviteGame : JobDriver
    {
        private Thing Board
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 다른 두 Job 과 **반드시 같은 수**여야 한다 - ReservationManager 는 MaxPawns 가
            // 다르면 개수와 무관하게 즉시 거절한다. 그래야 식구가 같은 가구에 붙을 수 있다.
            return pawn.Reserve(job.targetA, job, Together.SeatCount(Board), 0, null, errorOnFailed);
        }

        /// <summary>걷는 동안과 기다리는 동안이 같은 Job 이라 닿은 뒤에는 문구를 바꿔 준다.</summary>
        public override string GetReport()
        {
            Thing board = Board;
            if (!TogetherToils.Arrived(pawn, board)) return base.GetReport();

            string label = TogetherToils.GameLabel(board);
            return label != null ? (string)"PR.Job.Waiting".Translate(label) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TogetherToils.Wait(this, TargetIndex.A);
        }
    }
}
