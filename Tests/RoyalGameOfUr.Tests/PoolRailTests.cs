using Billiards.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 레일 규칙.
    ///
    /// 정식 나인볼은 제대로 맞히는 것만으로 끝나지 않는다 - 맞힌 뒤에 공이 하나 떨어지거나,
    /// 어느 공이든 쿠션에 닿아야 한다. 없으면 살짝 건드려 놓고 자리만 지키는 수가 통한다.
    /// </summary>
    public class PoolRailTests
    {
        private static Ball[] Two(Vec2 cueAt, Vec2 targetAt, Vec2 cueVelocity)
        {
            return new[]
            {
                new Ball { Number = 0, Pos = cueAt, Vel = cueVelocity },
                new Ball { Number = 1, Pos = targetAt },
            };
        }

        private static ShotOutcome Run(Ball[] balls)
        {
            ShotOutcome outcome = new ShotOutcome();

            while (!PoolSim.AtRest(balls) && outcome.Seconds < PoolTable.MaxShotSeconds)
                PoolSim.Step(balls, outcome);

            return outcome;
        }

        [Fact]
        public void 살짝_건드리기만_하면_쿠션에_닿지_않는다()
        {
            // 대 한가운데. 마찰이 0.52 이므로 넣어준 속력은 쓸 만큼만 굴러가고 멈춘다.
            Ball[] balls = Two(new Vec2(0.3f, 0.5f), new Vec2(0.55f, 0.5f), new Vec2(0.7f, 0f));
            ShotOutcome outcome = Run(balls);

            Assert.Equal(1, outcome.FirstContact);
            Assert.Empty(outcome.Pocketed);
            Assert.False(outcome.RailAfterContact);
        }

        [Fact]
        public void 세게_치면_쿠션에_닿은_것이_남는다()
        {
            Ball[] balls = Two(new Vec2(0.3f, 0.5f), new Vec2(0.6f, 0.5f), new Vec2(3f, 0f));
            ShotOutcome outcome = Run(balls);

            Assert.Equal(1, outcome.FirstContact);
            Assert.True(outcome.RailAfterContact);
        }

        [Fact]
        public void 맞히기_전에_닿은_쿠션은_세지_않는다()
        {
            // 흰 공만 벽을 맞고 돌아와 멈춘다. 목적구는 건드리지도 못했다 -
            // 규칙이 묻는 것은 "맞힌 뒤에" 닿았느냐이므로 이것은 아니다.
            Ball[] balls = Two(new Vec2(0.3f, 0.5f), new Vec2(1.6f, 0.5f), new Vec2(-1.2f, 0f));
            ShotOutcome outcome = Run(balls);

            Assert.Equal(-1, outcome.FirstContact);
            Assert.False(outcome.RailAfterContact);
        }
    }
}
