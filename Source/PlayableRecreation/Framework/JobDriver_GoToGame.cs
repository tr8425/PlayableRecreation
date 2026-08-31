using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 몰입 모드 전용. 가구까지 걸어가 마주 본 뒤 창을 연다.
    /// 여가 시스템과는 무관하며, 바닐라 여가 Job 은 건드리지 않는다.
    /// </summary>
    public class JobDriver_GoToGame : JobDriver
    {
        private Thing Board
        {
            get { return job.GetTarget(TargetIndex.A).Thing; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
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
                GameEntry.OpenWindow(comp.Game, board, pawn);
            };
            yield return open;
        }
    }
}
