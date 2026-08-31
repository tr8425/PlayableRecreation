using Billiards.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 나인볼의 규칙. 물리와 분리되어 있어서 배치를 손으로 세워 놓고 확인할 수 있다.
    /// </summary>
    public class NineBallTests
    {
        private static NineBall Fresh(int seed = 7)
        {
            return new NineBall(seed, PoolSide.Player);
        }

        private static void RollOut(NineBall match)
        {
            for (int i = 0; i < 4000 && match.Moving; i++) match.Advance(0.02f);
        }

        /// <summary>테이블을 비우고 지정한 공만 남긴다.</summary>
        private static void Clear(NineBall match, params int[] keep)
        {
            for (int n = 1; n <= 9; n++)
            {
                bool wanted = false;
                for (int i = 0; i < keep.Length; i++) if (keep[i] == n) wanted = true;

                match.Balls[n].Pocketed = !wanted;
                match.Balls[n].Vel = Vec2.Zero;
            }
        }

        [Fact]
        public void 랙은_아홉_개를_겹치지_않게_놓는다()
        {
            NineBall match = Fresh();

            for (int n = 1; n <= 9; n++)
            {
                Assert.True(match.Balls[n].InPlay);
                Assert.InRange(match.Balls[n].Pos.X, 0f, PoolTable.Width);
                Assert.InRange(match.Balls[n].Pos.Y, 0f, PoolTable.Height);
            }

            for (int a = 0; a < NineBall.BallCount; a++)
            {
                for (int b = a + 1; b < NineBall.BallCount; b++)
                {
                    float gap = Vec2.Distance(match.Balls[a].Pos, match.Balls[b].Pos);
                    Assert.True(gap >= PoolTable.BallRadius * 1.9f, a + " and " + b + " overlap");
                }
            }
        }

        [Fact]
        public void 랙의_꼭짓점은_1번이고_한가운데가_9번이다()
        {
            NineBall match = Fresh();

            // 1번이 가장 앞(작은 x)이고, 9번은 세로 한가운데에 있다.
            for (int n = 2; n <= 9; n++)
                Assert.True(match.Balls[1].Pos.X <= match.Balls[n].Pos.X + 1e-4f);

            Assert.Equal(PoolTable.Height * 0.5f, match.Balls[9].Pos.Y, 3);
        }

        [Fact]
        public void 남은_가장_낮은_번호를_먼저_맞혀야_한다()
        {
            NineBall match = Fresh();
            Clear(match, 3, 5);

            Assert.Equal(3, match.LowestBall);
        }

        [Fact]
        public void 높은_공을_먼저_맞히면_파울이다()
        {
            NineBall match = Fresh();
            Clear(match, 2, 5);

            match.Balls[0].Pos = new Vec2(0.4f, 0.5f);
            match.Balls[2].Pos = new Vec2(0.4f, 0.15f);   // 옆으로 비켜 있다
            match.Balls[5].Pos = new Vec2(1.0f, 0.5f);    // 정면

            match.Shoot(new Vec2(2.0f, 0f));              // 5번을 먼저 맞힌다
            RollOut(match);

            Assert.Equal(1, match.Fouls(PoolSide.Player));
            Assert.Equal(PoolSide.Opponent, match.Turn);
        }

        [Fact]
        public void 흰_공이_빠지면_파울이고_제자리로_돌아온다()
        {
            NineBall match = Fresh();
            Clear(match, 1);

            Vec2 pocket = PoolTable.Pockets[0];
            match.Balls[0].Pos = new Vec2(0.35f, 0.35f);
            match.Balls[1].Pos = new Vec2(1.5f, 0.9f);

            match.Shoot((pocket - match.Balls[0].Pos).Normalized * 1.4f);
            RollOut(match);

            Assert.Equal(1, match.Fouls(PoolSide.Player));
            Assert.True(match.Balls[0].InPlay);
            Assert.Equal(PoolSide.Opponent, match.Turn);
        }

        [Fact]
        public void 아홉번을_정당하게_넣으면_이긴다()
        {
            NineBall match = Fresh();
            Clear(match, 9);

            Vec2 pocket = PoolTable.Pockets[2];
            match.Balls[9].Pos = new Vec2(pocket.X - 0.30f, pocket.Y + 0.30f);
            match.Balls[0].Pos = match.Balls[9].Pos + (match.Balls[9].Pos - pocket).Normalized * 0.35f;

            match.Shoot((pocket - match.Balls[0].Pos).Normalized * 2.2f);
            RollOut(match);

            Assert.True(match.IsOver);
            Assert.Equal(PoolSide.Player, match.Winner);
        }

        [Fact]
        public void 못_넣으면_차례가_넘어간다()
        {
            NineBall match = Fresh();
            Clear(match, 1);

            match.Balls[0].Pos = new Vec2(0.4f, 0.5f);
            match.Balls[1].Pos = new Vec2(1.0f, 0.5f);

            // 제대로 맞힐 것만으로는 모자란다. 넣지 못했으면 어느 공이든
            // 쿠션에는 닿아야 파울이 아니다 - 그래서 건드리는 정도로는 부족하다.
            match.Shoot(new Vec2(2.0f, 0f));
            RollOut(match);

            Assert.Equal(0, match.Fouls(PoolSide.Player));
            Assert.Equal(PoolSide.Opponent, match.Turn);
        }

        [Fact]
        public void 넣으면_계속_친다()
        {
            NineBall match = Fresh();
            Clear(match, 1, 9);

            Vec2 pocket = PoolTable.Pockets[2];
            match.Balls[1].Pos = new Vec2(pocket.X - 0.30f, pocket.Y + 0.30f);
            match.Balls[9].Pos = new Vec2(0.6f, 0.2f);
            match.Balls[0].Pos = match.Balls[1].Pos + (match.Balls[1].Pos - pocket).Normalized * 0.35f;

            match.Shoot((pocket - match.Balls[0].Pos).Normalized * 2.2f);
            RollOut(match);

            Assert.False(match.IsOver);
            Assert.Equal(1, match.PocketedBy(PoolSide.Player));
            Assert.Equal(PoolSide.Player, match.Turn);
        }

        [Fact]
        public void 저장_지점은_공이_멈춘_뒤에만_늘어난다()
        {
            NineBall match = Fresh();
            int before = match.Resolved;

            match.Shoot(new Vec2(2.4f, 0.05f));
            Assert.Equal(before, match.Resolved);   // 굴러가는 동안은 그대로다

            RollOut(match);
            Assert.Equal(before + 1, match.Resolved);
        }
    }
}
