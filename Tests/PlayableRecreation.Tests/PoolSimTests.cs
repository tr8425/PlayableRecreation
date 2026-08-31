using Billiards.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 당구는 규칙이 아니라 물리다. 난수가 한 톨도 없으므로 그대로 단위 테스트에 올라간다 -
    /// 같은 배치에서 같은 샷은 언제나 같은 자리에 공을 세운다.
    /// </summary>
    public class PoolSimTests
    {
        private static Ball[] Two(Vec2 cueAt, Vec2 targetAt, Vec2 cueVelocity)
        {
            return new[]
            {
                new Ball { Number = 0, Pos = cueAt, Vel = cueVelocity },
                new Ball { Number = 1, Pos = targetAt },
            };
        }

        [Fact]
        public void 정면으로_맞히면_속도가_거의_그대로_넘어간다()
        {
            // 쿠션에 닿지 않을 만큼만 살살 친다. 부딪히는 순간만 보고 싶으니까.
            Ball[] balls = Two(new Vec2(0.3f, 0.5f), new Vec2(0.6f, 0.5f), new Vec2(1.0f, 0f));
            ShotOutcome outcome = new ShotOutcome();

            while (outcome.FirstContact < 0 && outcome.Seconds < 3f) PoolSim.Step(balls, outcome);

            float cue = balls[0].Vel.Length;
            float target = balls[1].Vel.Length;

            // 질량이 같은 정면 충돌이므로 흰 공은 거의 서고 속도는 목적구로 넘어간다.
            Assert.True(target > cue * 10f, "cue " + cue + " target " + target);
            Assert.True(target > 0.7f);
        }

        [Fact]
        public void 처음_맞힌_공이_기록된다()
        {
            Ball[] balls = Two(new Vec2(0.4f, 0.5f), new Vec2(0.8f, 0.5f), new Vec2(2.0f, 0f));
            ShotOutcome outcome = PoolSim.RunToRest(balls);

            Assert.Equal(1, outcome.FirstContact);
        }

        [Fact]
        public void 아무것도_못_맞히면_접촉이_없다()
        {
            Ball[] balls = Two(new Vec2(0.4f, 0.2f), new Vec2(0.8f, 0.8f), new Vec2(0f, -1.0f));
            ShotOutcome outcome = PoolSim.RunToRest(balls);

            Assert.Equal(-1, outcome.FirstContact);
        }

        [Fact]
        public void 마찰이_결국_공을_세운다()
        {
            Ball[] balls = { new Ball { Number = 0, Pos = new Vec2(0.3f, 0.5f), Vel = new Vec2(1.2f, 0.3f) } };
            PoolSim.RunToRest(balls);

            Assert.True(PoolSim.AtRest(balls));
            Assert.True(balls[0].Vel.IsZero);
        }

        [Fact]
        public void 쿠션은_되튕긴다()
        {
            // 쿠션을 향해 곧게 보낸다. 사이드 포켓(x = 1.0)은 피해서.
            Ball[] balls = { new Ball { Number = 0, Pos = new Vec2(0.6f, 0.5f), Vel = new Vec2(0f, -1.6f) } };
            PoolSim.RunToRest(balls);

            Assert.False(balls[0].Pocketed);
            Assert.True(PoolSim.AtRest(balls));
            Assert.True(balls[0].Pos.Y > 0.2f, "ended at " + balls[0].Pos);
        }

        [Fact]
        public void 구멍에_들어간_공은_사라진다()
        {
            Vec2 pocket = PoolTable.Pockets[0];
            Ball[] balls = { new Ball { Number = 3, Pos = new Vec2(0.35f, 0.35f), Vel = (pocket - new Vec2(0.35f, 0.35f)).Normalized * 1.4f } };

            ShotOutcome outcome = PoolSim.RunToRest(balls);

            Assert.True(balls[0].Pocketed);
            Assert.Contains(3, outcome.Pocketed);
        }

        [Fact]
        public void 같은_샷은_같은_자리에_세운다()
        {
            Ball[] a = Two(new Vec2(0.4f, 0.42f), new Vec2(1.1f, 0.55f), new Vec2(2.4f, 0.35f));
            Ball[] b = Two(new Vec2(0.4f, 0.42f), new Vec2(1.1f, 0.55f), new Vec2(2.4f, 0.35f));

            PoolSim.RunToRest(a);
            PoolSim.RunToRest(b);

            for (int i = 0; i < a.Length; i++)
            {
                Assert.Equal(a[i].Pos.X, b[i].Pos.X, 6);
                Assert.Equal(a[i].Pos.Y, b[i].Pos.Y, 6);
                Assert.Equal(a[i].Pocketed, b[i].Pocketed);
            }
        }

        [Fact]
        public void 공은_대_밖으로_나가지_않는다()
        {
            Ball[] balls = Two(new Vec2(0.5f, 0.5f), new Vec2(1.2f, 0.52f), new Vec2(3.2f, 0.9f));
            PoolSim.RunToRest(balls);

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].Pocketed) continue;

                Assert.InRange(balls[i].Pos.X, 0f, PoolTable.Width);
                Assert.InRange(balls[i].Pos.Y, 0f, PoolTable.Height);
            }
        }

        [Fact]
        public void 조준선은_가장_가까운_공을_찾는다()
        {
            Ball[] balls =
            {
                new Ball { Number = 0, Pos = new Vec2(0.3f, 0.5f) },
                new Ball { Number = 1, Pos = new Vec2(1.4f, 0.5f) },
                new Ball { Number = 2, Pos = new Vec2(0.9f, 0.5f) },
            };

            int hit;
            float distance = PoolSim.Trace(balls, balls[0].Pos, new Vec2(1f, 0f), 0, out hit);

            Assert.Equal(2, hit);
            Assert.InRange(distance, 0.5f, 0.6f);
        }
    }
}
