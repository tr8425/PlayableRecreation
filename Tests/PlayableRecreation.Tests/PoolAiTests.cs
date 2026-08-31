using Billiards.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 상대의 조준. 시뮬레이터가 결정적이므로 "이 단계는 이 배치를 넣을 수 있는가"를
    /// 추측이 아니라 실행으로 확인할 수 있다.
    /// </summary>
    public class PoolAiTests
    {
        private static void RollOut(NineBall match)
        {
            for (int i = 0; i < 4000 && match.Moving; i++) match.Advance(0.02f);
        }

        private static NineBall OneBallSetup(int seed)
        {
            NineBall match = new NineBall(seed, PoolSide.Opponent);

            for (int n = 2; n <= 9; n++) match.Balls[n].Pocketed = true;

            Vec2 pocket = PoolTable.Pockets[5];
            match.Balls[1].Pocketed = false;
            match.Balls[1].Pos = new Vec2(pocket.X - 0.28f, pocket.Y - 0.28f);
            match.Balls[0].Pos = match.Balls[1].Pos + (match.Balls[1].Pos - pocket).Normalized * 0.40f;

            return match;
        }

        [Fact]
        public void 명인은_쉬운_배치를_넣는다()
        {
            NineBall match = OneBallSetup(1234);

            PoolPlanner planner = new PoolPlanner(match, 4, 1234);
            while (!planner.Done) planner.Step(4);

            match.Shoot(planner.BestShot);
            RollOut(match);

            Assert.True(match.Balls[1].Pocketed, "the master missed an open shot");
        }

        [Fact]
        public void 미리_굴려_본_결과와_실제로_친_결과가_같다()
        {
            NineBall match = OneBallSetup(99);

            PoolPlanner planner = new PoolPlanner(match, 4, 99);
            while (!planner.Done) planner.Step(4);

            ShotOutcome preview = match.Preview(planner.BestShot);

            match.Shoot(planner.BestShot);
            RollOut(match);

            // 떨림을 먼저 얹고 나서 재 보므로, 재 본 것이 곧 칠 샷이어야 한다.
            Assert.Equal(preview.Pocketed.Count > 0, match.PocketedBy(PoolSide.Opponent) > 0);
            Assert.Equal(preview.FirstContact, match.Log[0].FirstContact);
        }

        [Fact]
        public void 위_단계일수록_더_많이_넣는다()
        {
            const int racks = 14;

            int novice = Pots(0, racks);
            int master = Pots(4, racks);

            Assert.True(master > novice, "novice " + novice + " master " + master);
        }

        private static int Pots(int tier, int racks)
        {
            int potted = 0;

            for (int i = 0; i < racks; i++)
            {
                NineBall match = OneBallSetup(500 + i * 37);

                PoolPlanner planner = new PoolPlanner(match, tier, 500 + i * 37);
                while (!planner.Done) planner.Step(4);

                match.Shoot(planner.BestShot);
                RollOut(match);

                if (match.Balls[1].Pocketed) potted++;
            }

            return potted;
        }

        [Fact]
        public void 초보는_한_번도_재_보지_않는다()
        {
            NineBall match = OneBallSetup(7);

            PoolPlanner planner = new PoolPlanner(match, 0, 7);
            Assert.True(planner.Done);
        }
    }
}
