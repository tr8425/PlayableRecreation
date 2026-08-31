using System;
using System.Collections.Generic;

namespace Billiards.Core
{
    public struct Ball
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public int Number;      // 0 = 큐볼
        public bool Pocketed;

        public bool InPlay
        {
            get { return !Pocketed; }
        }
    }

    /// <summary>한 샷이 남긴 것. 규칙 판정은 전부 이 값만 보고 한다.</summary>
    public sealed class ShotOutcome
    {
        /// <summary>큐볼이 처음 맞힌 공. 아무것도 못 맞혔으면 -1.</summary>
        public int FirstContact = -1;

        /// <summary>떨어진 순서대로.</summary>
        public readonly List<int> Pocketed = new List<int>();

        public bool CueScratched;

        /// <summary>첫 접촉 뒤에 어느 공이든 쿠션에 닿았는가. 나인볼의 레일 규칙이 이것만 본다.</summary>
        public bool RailAfterContact;

        public float Seconds;
        public bool TimedOut;
    }

    /// <summary>
    /// 당구공 몇 개와 벽 몇 개. 난수는 한 톨도 없다 -
    /// 같은 배치에서 같은 샷을 치면 언제나 같은 결과가 나온다.
    ///
    /// 고정 스텝으로만 전진하므로 프레임률이 흔들려도 결과가 달라지지 않는다.
    /// 봇은 같은 함수로 자기 샷을 미리 굴려 본다.
    /// </summary>
    public static class PoolSim
    {
        public static bool AtRest(Ball[] balls)
        {
            for (int i = 0; i < balls.Length; i++)
                if (balls[i].InPlay && !balls[i].Vel.IsZero) return false;

            return true;
        }

        public static Ball[] Clone(Ball[] balls)
        {
            Ball[] copy = new Ball[balls.Length];
            Array.Copy(balls, copy, balls.Length);
            return copy;
        }

        /// <summary>끝까지 굴린다. 봇이 후보 샷을 재 볼 때 쓴다.</summary>
        public static ShotOutcome RunToRest(Ball[] balls)
        {
            ShotOutcome outcome = new ShotOutcome();

            while (!AtRest(balls) && outcome.Seconds < PoolTable.MaxShotSeconds)
                Step(balls, outcome);

            if (!AtRest(balls))
            {
                outcome.TimedOut = true;
                for (int i = 0; i < balls.Length; i++) balls[i].Vel = Vec2.Zero;
            }

            return outcome;
        }

        /// <summary>실시간 재생용. 흘러간 시간만큼만 전진시킨다.</summary>
        public static void Advance(Ball[] balls, ShotOutcome outcome, float seconds)
        {
            float budget = seconds;

            while (budget >= PoolTable.Step && !AtRest(balls))
            {
                if (outcome.Seconds >= PoolTable.MaxShotSeconds)
                {
                    outcome.TimedOut = true;
                    for (int i = 0; i < balls.Length; i++) balls[i].Vel = Vec2.Zero;
                    return;
                }

                Step(balls, outcome);
                budget -= PoolTable.Step;
            }
        }

        /// <summary>고정 한 스텝. 굴리고, 부딪히고, 벽을 튕기고, 구멍을 확인한다.</summary>
        public static void Step(Ball[] balls, ShotOutcome outcome)
        {
            const float dt = PoolTable.Step;
            outcome.Seconds += dt;

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].Pocketed || balls[i].Vel.IsZero) continue;

                balls[i].Pos = balls[i].Pos + balls[i].Vel * dt;

                float speed = balls[i].Vel.Length;
                float slowed = speed - PoolTable.Friction * dt;

                if (slowed <= PoolTable.RestSpeed) balls[i].Vel = Vec2.Zero;
                else balls[i].Vel = balls[i].Vel * (slowed / speed);
            }

            ResolveCollisions(balls, outcome);
            ResolvePockets(balls, outcome);
            ResolveCushions(balls, outcome);
        }

        /// <summary>
        /// 조준선. from 에서 dir 로 갔을 때 처음 닿는 공까지의 거리를 잰다.
        /// 아무 공에도 안 닿으면 쿠션까지의 거리를 준다. 조준 보조선을 그리는 데 쓴다.
        /// </summary>
        public static float Trace(Ball[] balls, Vec2 from, Vec2 dir, int ignore, out int hit)
        {
            const float reach = PoolTable.BallRadius * 2f;

            hit = -1;
            float best = CushionDistance(from, dir);

            for (int i = 0; i < balls.Length; i++)
            {
                if (i == ignore || balls[i].Pocketed) continue;

                Vec2 delta = balls[i].Pos - from;
                float along = Vec2.Dot(delta, dir);
                if (along <= 0f) continue;

                float side = (delta - dir * along).Length;
                if (side > reach) continue;

                float back = (float)Math.Sqrt(reach * reach - side * side);
                float distance = along - back;
                if (distance < 0f || distance >= best) continue;

                best = distance;
                hit = balls[i].Number;
            }

            return best;
        }

        private static float CushionDistance(Vec2 from, Vec2 dir)
        {
            const float r = PoolTable.BallRadius;
            float best = 10f;

            if (dir.X > 1e-6f) best = Math.Min(best, (PoolTable.Width - r - from.X) / dir.X);
            else if (dir.X < -1e-6f) best = Math.Min(best, (r - from.X) / dir.X);

            if (dir.Y > 1e-6f) best = Math.Min(best, (PoolTable.Height - r - from.Y) / dir.Y);
            else if (dir.Y < -1e-6f) best = Math.Min(best, (r - from.Y) / dir.Y);

            return best < 0f ? 0f : best;
        }

        private static void ResolveCollisions(Ball[] balls, ShotOutcome outcome)
        {
            const float diameter = PoolTable.BallRadius * 2f;

            for (int a = 0; a < balls.Length; a++)
            {
                if (balls[a].Pocketed) continue;

                for (int b = a + 1; b < balls.Length; b++)
                {
                    if (balls[b].Pocketed) continue;

                    Vec2 delta = balls[b].Pos - balls[a].Pos;
                    float distance = delta.Length;
                    if (distance >= diameter || distance <= 1e-6f) continue;

                    Vec2 normal = delta * (1f / distance);
                    float approach = Vec2.Dot(balls[b].Vel - balls[a].Vel, normal);

                    // 겹친 만큼 서로 밀어내 다음 스텝에 다시 붙잡히지 않게 한다.
                    Vec2 push = normal * ((diameter - distance) * 0.5f);
                    balls[a].Pos = balls[a].Pos - push;
                    balls[b].Pos = balls[b].Pos + push;

                    if (approach >= 0f) continue;

                    // 큐볼이 처음 무엇을 맞혔는가. 반칙 판정이 이것 하나에 달려 있다.
                    if (outcome.FirstContact < 0)
                    {
                        if (balls[a].Number == 0) outcome.FirstContact = balls[b].Number;
                        else if (balls[b].Number == 0) outcome.FirstContact = balls[a].Number;
                    }

                    // 질량이 같으므로 법선 방향 성분만 주고받는다.
                    float impulse = -(1f + PoolTable.BallRestitution) * approach * 0.5f;
                    balls[a].Vel = balls[a].Vel - normal * impulse;
                    balls[b].Vel = balls[b].Vel + normal * impulse;
                }
            }
        }

        private static void ResolvePockets(Ball[] balls, ShotOutcome outcome)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].Pocketed) continue;

                for (int p = 0; p < PoolTable.Pockets.Length; p++)
                {
                    if (Vec2.Distance(balls[i].Pos, PoolTable.Pockets[p]) > PoolTable.PocketRadius) continue;

                    balls[i].Pocketed = true;
                    balls[i].Vel = Vec2.Zero;
                    outcome.Pocketed.Add(balls[i].Number);

                    if (balls[i].Number == 0) outcome.CueScratched = true;
                    break;
                }
            }
        }

        private static void ResolveCushions(Ball[] balls, ShotOutcome outcome)
        {
            const float r = PoolTable.BallRadius;
            const float e = PoolTable.CushionRestitution;

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].Pocketed) continue;

                Vec2 pos = balls[i].Pos;
                Vec2 vel = balls[i].Vel;
                bool bounced = false;

                if (pos.X < r) { pos.X = r; if (vel.X < 0f) { vel.X = -vel.X * e; bounced = true; } }
                else if (pos.X > PoolTable.Width - r) { pos.X = PoolTable.Width - r; if (vel.X > 0f) { vel.X = -vel.X * e; bounced = true; } }

                if (pos.Y < r) { pos.Y = r; if (vel.Y < 0f) { vel.Y = -vel.Y * e; bounced = true; } }
                else if (pos.Y > PoolTable.Height - r) { pos.Y = PoolTable.Height - r; if (vel.Y > 0f) { vel.Y = -vel.Y * e; bounced = true; } }

                balls[i].Pos = pos;
                balls[i].Vel = vel;

                // 벽에 밀어붙여 세워 두는 것은 접촉이 아니다. 실제로 튕겨 나온 것만 센다.
                if (bounced && outcome.FirstContact >= 0) outcome.RailAfterContact = true;
            }
        }
    }
}
