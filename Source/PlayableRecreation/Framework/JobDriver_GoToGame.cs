using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 몰입 모드 전용. 가구까지 걸어가 마주 본 뒤 창을 연다.
    /// 여가 시스템과는 무관하며, 바닐라 여가 Job 은 건드리지 않는다.
    ///
    /// 2칸에서는 창을 연 뒤에도 끝나지 않고 판이 끝날 때까지 그 자리에 선다.
    /// </summary>
    public class JobDriver_GoToGame : JobDriver
    {
        private Thing Board
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 2칸이 아니면 예약할 것이 없다 - Job 이 곧 끝나므로 남의 예약을 물 이유도 없다.
            if (!Together.Enabled) return true;

            // 자리 수는 바닐라가 그 가구에 쓰는 값 그대로다(Together.SeatCount).
            // 실패는 조용히 받는다 - 예약은 다른 모드와 부딪히기 쉬운 자리다.
            return pawn.Reserve(job.targetA, job, Together.SeatCount(Board), 0, null, errorOnFailed);
        }

        /// <summary>
        /// 걷는 동안과 두는 동안이 같은 Job 이라 기본 문구 하나로는 틀린 말이 된다 —
        /// 판 앞에 앉아 두고 있는데 "하러 가는 중" 이라고 뜨는 것을 여기서 바꿋다.
        /// </summary>
        public override string GetReport()
        {
            Thing board = Board;
            if (!Together.Enabled || !TogetherToils.Arrived(pawn, board)) return base.GetReport();

            string label = TogetherToils.GameLabel(board);
            return label != null ? (string)"PR.Job.Playing".Translate(label) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil open = ToilMaker.MakeToil("OpenMiniGameWindow");
            open.defaultCompleteMode = ToilCompleteMode.Instant;
            open.initAction = delegate
            {
                Thing board = Board;
                if (board == null) return;

                CompMiniGame comp = board.TryGetComp<CompMiniGame>();
                if (comp == null || comp.Game == null) return;

                pawn.rotationTracker.FaceTarget(board);

                // 상대를 기다리는 중이라면 여는 것은 상대가 닿았을 때다.
                if (TogetherMatch.ShouldHold(board, pawn))
                {
                    TogetherMatch.NotifyArrived(board, pawn);
                    return;
                }

                GameEntry.OpenWindow(comp.Game, board, pawn);
            };
            yield return open;

            // 2칸이 아니면 여기서 Job 이 끝난다 - 예전과 똑같다.
            if (!Together.Enabled) yield break;

            yield return TogetherToils.Hold(this, TargetIndex.A);
        }
    }
}
