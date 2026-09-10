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
    ///
    /// 아무도 안 오면 <b>이 Job 그대로</b> 혼자 두기로 넘어간다 — 바닐라 여가 Job 으로
    /// 갈아타면 손님에게 여가 욕구가 없어서 첫 틱에 죽는다(QA-03).
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
            if (!pawn.Reserve(job.targetA, job, Together.SeatCount(Board), 0, null, errorOnFailed))
                return false;

            // 자리도 함께 잡는다. 안 잡으면 둘이 같은 칸에 겹쳐 선다 (QA-01).
            TogetherToils.ClaimSeat(pawn, job, Board, TargetIndex.C);

            return true;
        }

        /// <summary>걷는 동안과 기다리는 동안이 같은 Job 이라 닿은 뒤에는 문구를 바꿔 준다.</summary>
        public override string GetReport()
        {
            Thing board = Board;
            if (!TogetherToils.Arrived(pawn, board)) return base.GetReport();

            string label = TogetherToils.GameLabel(board);
            if (label == null) return base.GetReport();

            // 기다리기를 접은 뒤에도 같은 Job 이다. 문구만 갈린다.
            return TogetherInvite.IsAlone(pawn)
                ? (string)"PR.Job.PlayingAlone".Translate(label)
                : (string)"PR.Job.Waiting".Translate(label);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // 자리를 잡았으면 그 칸으로 간다. 못 잡았으면 예전처럼 닿기만 한다.
            if (job.GetTarget(TargetIndex.C).IsValid)
                yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            else
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TogetherToils.Wait(this, TargetIndex.A);
        }
    }
}
