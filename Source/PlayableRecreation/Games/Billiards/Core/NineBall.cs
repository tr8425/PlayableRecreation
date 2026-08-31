using System.Collections.Generic;

namespace Billiards.Core
{
    public enum PoolSide : byte
    {
        Player = 0,
        Opponent = 1,
    }

    public struct PoolLogEntry
    {
        public int Shot;
        public PoolSide Side;
        public int Pocketed;
        public int FirstContact;
        public bool Foul;
        public bool Scratch;
    }

    /// <summary>
    /// 나인볼. 큐볼과 1~9번으로 치는 짧은 판이라 한 자리에서 끝나고, 규칙이 한 줄로 요약된다 -
    /// **테이블 위의 가장 낮은 번호를 먼저 맞혀야 한다.**
    ///
    /// 어기면 반칙이고 큐볼이 제자리로 돌아간다. 9번을 정당하게 넣으면 그 자리에서 이긴다.
    /// (실제 규칙의 볼인핸드는 넣지 않았다 - 놓을 자리를 고르는 조작이 판을 늘어지게 만든다.)
    /// </summary>
    public sealed class NineBall
    {
        public const int BallCount = 10;

        private readonly Ball[] balls = new Ball[BallCount];
        private readonly List<PoolLogEntry> log = new List<PoolLogEntry>();
        private readonly int[] pocketedBy = new int[2];
        private readonly int[] fouls = new int[2];

        private ShotOutcome pending;
        private int lowestAtShot;

        public int Seed { get; private set; }
        public int Shots { get; private set; }

        /// <summary>규칙 판정까지 끝난 샷 수. 저장은 이 값이 바뀔 때만 일어난다 -
        /// 굴러가는 도중에 저장되면 창을 닫았다 여는 것으로 샷을 무를 수 있게 된다.</summary>
        public int Resolved { get; private set; }
        public PoolSide Turn { get; private set; }
        public bool IsOver { get; private set; }
        public PoolSide Winner { get; private set; }

        public IReadOnlyList<PoolLogEntry> Log
        {
            get { return log; }
        }

        public Ball[] Balls
        {
            get { return balls; }
        }

        /// <summary>공이 아직 구르고 있는가. 저장도 조작도 멈춘 뒤에만 허용한다.</summary>
        public bool Moving
        {
            get { return pending != null; }
        }

        public int PocketedBy(PoolSide side) { return pocketedBy[(int)side]; }
        public int Fouls(PoolSide side) { return fouls[(int)side]; }

        /// <summary>테이블에 남은 가장 낮은 번호. 이번에 반드시 먼저 맞혀야 하는 공이다.</summary>
        public int LowestBall
        {
            get
            {
                for (int n = 1; n <= 9; n++)
                    if (balls[n].InPlay) return n;

                return 0;
            }
        }

        public NineBall(int seed, PoolSide first)
        {
            Seed = seed;
            Turn = first;
            Rack();
        }

        /// <summary>세이브에서 되살린다. 공이 멈춘 순간의 배치만 저장되므로 굴러가던 중간은 없다.</summary>
        public static NineBall Restore(int seed, PoolSide turn, int shots, Ball[] positions,
                                       int pocketedPlayer, int pocketedOpponent,
                                       int foulsPlayer, int foulsOpponent)
        {
            NineBall match = new NineBall(seed, turn) { Shots = shots, Resolved = shots };

            for (int i = 0; i < BallCount && i < positions.Length; i++) match.balls[i] = positions[i];

            match.pocketedBy[0] = pocketedPlayer;
            match.pocketedBy[1] = pocketedOpponent;
            match.fouls[0] = foulsPlayer;
            match.fouls[1] = foulsOpponent;

            return match;
        }

        // ---------- 랙 ----------

        private void Rack()
        {
            const float r = PoolTable.BallRadius;
            float rowGap = r * 2f * 0.866f;

            balls[0] = new Ball { Number = 0, Pos = PoolTable.HeadSpot };

            // 다이아몬드 1-2-3-2-1. 꼭짓점이 1번, 한가운데가 9번이고 나머지는 시드로 섞는다.
            int[] sizes = { 1, 2, 3, 2, 1 };
            List<int> spare = new List<int> { 2, 3, 4, 5, 6, 7, 8 };
            Shuffle(spare, Seed);

            int spareAt = 0;

            for (int row = 0; row < sizes.Length; row++)
            {
                for (int k = 0; k < sizes[row]; k++)
                {
                    int number;
                    if (row == 0) number = 1;
                    else if (row == 2 && k == 1) number = 9;
                    else number = spare[spareAt++];

                    float y = PoolTable.Height * 0.5f + (k - (sizes[row] - 1) * 0.5f) * r * 2f;
                    balls[number] = new Ball
                    {
                        Number = number,
                        Pos = new Vec2(PoolTable.FootSpot.X + row * rowGap, y),
                    };
                }
            }
        }

        private static void Shuffle(List<int> items, int seed)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = (int)(Hash(seed, i) % (uint)(i + 1));
                int swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }

        private static uint Hash(int seed, int index)
        {
            uint h = (uint)seed ^ (uint)(index * 0x9E3779B9);
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;
            return h;
        }

        // ---------- 샷 ----------

        /// <summary>큐볼을 친다. 이후 <see cref="Advance"/> 로 굴리면 멈추는 순간 규칙이 적용된다.</summary>
        public void Shoot(Vec2 velocity)
        {
            if (IsOver || Moving || !balls[0].InPlay) return;

            lowestAtShot = LowestBall;
            balls[0].Vel = velocity;

            pending = new ShotOutcome();
            Shots++;
        }

        public void Advance(float seconds)
        {
            if (pending == null) return;

            PoolSim.Advance(balls, pending, seconds);
            if (!PoolSim.AtRest(balls)) return;

            ShotOutcome outcome = pending;
            pending = null;
            Resolve(outcome);
        }

        /// <summary>봇이 후보 샷을 미리 굴려 본다. 실제 판은 건드리지 않는다.</summary>
        public ShotOutcome Preview(Vec2 velocity)
        {
            Ball[] copy = PoolSim.Clone(balls);
            copy[0].Vel = velocity;
            return PoolSim.RunToRest(copy);
        }

        private void Resolve(ShotOutcome outcome)
        {
            int shooter = (int)Turn;

            bool nine = false;
            int pocketed = 0;

            for (int i = 0; i < outcome.Pocketed.Count; i++)
            {
                int number = outcome.Pocketed[i];
                if (number == 0) continue;

                pocketed++;
                if (number == 9) nine = true;
            }

            pocketedBy[shooter] += pocketed;

            bool wrongFirst = outcome.FirstContact != lowestAtShot;

            // 제대로 맞혔더라도 그 뒤에 아무것도 떨어지지 않고 어느 공도 쿠션에 닿지 않으면
            // 파울이다. 살짝 건드려 놓고 자리만 지키는 수를 막는, 나인볼의 레일 규칙이다.
            bool noRail = pocketed == 0 && !outcome.RailAfterContact;

            bool foul = outcome.CueScratched || outcome.FirstContact < 0 || wrongFirst || noRail;

            log.Add(new PoolLogEntry
            {
                Shot = Shots,
                Side = Turn,
                Pocketed = pocketed,
                FirstContact = outcome.FirstContact,
                Foul = foul,
                Scratch = outcome.CueScratched,
            });

            Resolved++;

            // 9번을 정당하게 넣었으면 그 자리에서 끝난다.
            if (nine && !foul)
            {
                IsOver = true;
                Winner = Turn;
                return;
            }

            if (nine) Respot(9, PoolTable.FootSpot);
            if (outcome.CueScratched) Respot(0, PoolTable.HeadSpot);

            if (foul)
            {
                fouls[shooter]++;
                if (!outcome.CueScratched) Respot(0, PoolTable.HeadSpot);
                Turn = Other(Turn);
                return;
            }

            // 하나라도 넣었으면 계속 친다. 아니면 넘긴다.
            if (pocketed == 0) Turn = Other(Turn);
        }

        public static PoolSide Other(PoolSide side)
        {
            return side == PoolSide.Player ? PoolSide.Opponent : PoolSide.Player;
        }

        /// <summary>제자리에 놓는다. 이미 차 있으면 긴 쪽으로 밀어 빈자리를 찾는다.</summary>
        private void Respot(int number, Vec2 spot)
        {
            const float r = PoolTable.BallRadius;

            balls[number].Pocketed = false;
            balls[number].Vel = Vec2.Zero;

            for (int step = 0; step < 80; step++)
            {
                float offset = step * r * 2.1f;
                Vec2 candidate = new Vec2(spot.X + (spot.X > PoolTable.Width * 0.5f ? offset : -offset), spot.Y);

                if (candidate.X < r) candidate.X = r;
                if (candidate.X > PoolTable.Width - r) candidate.X = PoolTable.Width - r;

                if (Free(candidate, number))
                {
                    balls[number].Pos = candidate;
                    return;
                }
            }

            balls[number].Pos = spot;
        }

        private bool Free(Vec2 point, int ignore)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (i == ignore || balls[i].Pocketed) continue;
                if (Vec2.Distance(balls[i].Pos, point) < PoolTable.BallRadius * 2.05f) return false;
            }

            return true;
        }
    }
}
