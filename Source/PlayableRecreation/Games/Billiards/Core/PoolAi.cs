using System;

namespace Billiards.Core
{
    /// <summary>
    /// 상대의 조준. 탐색이 아니라 기하학이다 - 넣으려는 공 뒤쪽의 "고스트볼" 자리를 겨눈다.
    ///
    /// 위 단계는 거기서 한 걸음 더 간다. 시뮬레이터가 결정적이므로 **후보 샷을 미리 굴려 보고
    /// 실제로 들어가는 것만 고른다.** 다만 그 계산은 한 프레임에 다 하지 않고
    /// 뜸들이는 시간에 걸쳐 조금씩 나눠 한다 - 창이 끊기면 안 되니까.
    /// </summary>
    public sealed class PoolPlanner
    {
        private readonly NineBall match;
        private readonly int tier;
        private readonly int seed;
        private readonly int budget;

        private int examined;
        private float bestScore = float.NegativeInfinity;
        private Vec2 best;

        public PoolPlanner(NineBall match, int tier, int seed)
        {
            this.match = match;
            this.tier = tier < 0 ? 0 : tier > 4 ? 4 : tier;
            this.seed = seed;
            budget = Previews(this.tier);

            best = Candidate(0);
        }

        /// <summary>더 재 볼 후보가 남았는가.</summary>
        public bool Done
        {
            get { return examined >= budget; }
        }

        public Vec2 BestShot
        {
            get { return best; }
        }

        /// <summary>단계가 높을수록 미리 굴려 보는 후보가 많다. 초보는 한 번도 재지 않는다.</summary>
        private static int Previews(int tier)
        {
            switch (tier)
            {
                case 0: return 0;
                case 1: return 2;
                case 2: return 5;
                case 3: return 10;
                default: return 20;
            }
        }

        /// <summary>단계가 높을수록 손이 곧다.</summary>
        public static float NoiseFor(int tier)
        {
            switch (tier)
            {
                case 0: return 0.160f;
                case 1: return 0.090f;
                case 2: return 0.050f;
                case 3: return 0.025f;
                default: return 0.010f;
            }
        }

        /// <summary>후보 몇 개를 실제로 굴려 본다. 프레임마다 조금씩 부르면 된다.</summary>
        public void Step(int count)
        {
            for (int i = 0; i < count && !Done; i++)
            {
                Vec2 shot = Candidate(examined);
                float score = Score(shot);
                examined++;

                if (score <= bestScore) continue;

                bestScore = score;
                best = shot;
            }
        }

        private float Score(Vec2 shot)
        {
            ShotOutcome outcome = match.Preview(shot);
            int lowest = match.LowestBall;

            bool foul = outcome.CueScratched
                        || outcome.FirstContact < 0
                        || outcome.FirstContact != lowest;

            float score = foul ? -300f : 0f;

            for (int i = 0; i < outcome.Pocketed.Count; i++)
            {
                int number = outcome.Pocketed[i];
                if (number == 0) continue;

                if (number == 9) score += foul ? 5f : 400f;
                else if (number == lowest) score += foul ? 5f : 120f;
                else score += foul ? 2f : 25f;
            }

            // 아무것도 못 넣어도 정당하게 맞히기만 하면 넘겨줄 만한 샷이다.
            if (!foul) score += 10f;

            return score;
        }

        // ---------- 후보 만들기 ----------

        /// <summary>
        /// index 번째 후보. 구멍 · 세기 · 손 떨림을 index 로 조합해 만든다.
        /// 떨림을 먼저 얹고 나서 굴려 보므로, 재 본 결과는 실제로 칠 샷의 결과와 정확히 같다.
        /// </summary>
        private Vec2 Candidate(int index)
        {
            int lowest = match.LowestBall;
            if (lowest == 0) return new Vec2(1f, 0f);

            Ball cue = match.Balls[0];
            Ball target = match.Balls[lowest];

            int pocketIndex = index % PoolTable.Pockets.Length;
            int speedLevel = index / PoolTable.Pockets.Length % 3;

            Vec2 pocket = PoolTable.Pockets[pocketIndex];

            // 고스트볼: 목적구가 구멍 쪽으로 굴러가려면 큐볼이 닿아야 하는 자리.
            Vec2 fromPocket = (target.Pos - pocket).Normalized;
            Vec2 ghost = target.Pos + fromPocket * (PoolTable.BallRadius * 2f);

            Vec2 toGhost = ghost - cue.Pos;
            float reach = toGhost.Length;

            // 너무 두껍게 잘라야 하는 구멍은 포기하고 그냥 정면으로 맞힌다.
            Vec2 aim = toGhost.Normalized;
            float cut = Vec2.Dot((pocket - target.Pos).Normalized, aim);

            if (reach <= 1e-4f || cut < 0.12f)
            {
                aim = (target.Pos - cue.Pos).Normalized;
                reach = Vec2.Distance(cue.Pos, target.Pos);
            }

            float path = reach + Vec2.Distance(target.Pos, pocket);
            float speed = (float)Math.Sqrt(2f * PoolTable.Friction * path) * 1.35f;
            speed *= speedLevel == 0 ? 0.9f : speedLevel == 1 ? 1.1f : 1.35f;

            if (speed < 0.8f) speed = 0.8f;
            if (speed > PoolTable.MaxShotSpeed) speed = PoolTable.MaxShotSpeed;

            float wobble = (Uniform(index * 2) - 0.5f) * 2f * NoiseFor(tier);
            return Vec2.FromAngle(aim.Angle + wobble, speed);
        }

        private float Uniform(int index)
        {
            uint h = (uint)seed ^ (uint)(index * 0x9E3779B9);
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
