using System.Collections.Generic;
using Billiards.Core;
using PlayableRecreation;
using Verse;

namespace Billiards
{
    /// <summary>
    /// 당구대에 남는 판. 공이 멈춘 배치만 저장된다 -
    /// 굴러가는 도중은 저장 지점이 아니므로, 창을 닫아 샷을 무를 수 없다.
    /// </summary>
    public class BilliardsSaveData : MiniGameSaveData
    {
        public int seed;
        public int shots;
        public bool opponentTurn;
        public int pocketedPlayer, pocketedOpponent;
        public int foulsPlayer, foulsOpponent;

        private List<float> xs = new List<float>();
        private List<float> ys = new List<float>();
        private List<bool> down = new List<bool>();

        public BilliardsSaveData()
        {
        }

        public BilliardsSaveData(NineBall match)
        {
            seed = match.Seed;
            shots = match.Shots;
            opponentTurn = match.Turn == PoolSide.Opponent;
            pocketedPlayer = match.PocketedBy(PoolSide.Player);
            pocketedOpponent = match.PocketedBy(PoolSide.Opponent);
            foulsPlayer = match.Fouls(PoolSide.Player);
            foulsOpponent = match.Fouls(PoolSide.Opponent);

            for (int i = 0; i < match.Balls.Length; i++)
            {
                xs.Add(match.Balls[i].Pos.X);
                ys.Add(match.Balls[i].Pos.Y);
                down.Add(match.Balls[i].Pocketed);
            }
        }

        public NineBall ToMatch()
        {
            Ball[] positions = new Ball[NineBall.BallCount];

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Ball
                {
                    Number = i,
                    Pos = i < xs.Count ? new Vec2(xs[i], ys[i]) : PoolTable.HeadSpot,
                    Pocketed = i < down.Count && down[i],
                };
            }

            return NineBall.Restore(seed, opponentTurn ? PoolSide.Opponent : PoolSide.Player, shots,
                                    positions, pocketedPlayer, pocketedOpponent,
                                    foulsPlayer, foulsOpponent);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref shots, "shots", 0);
            Scribe_Values.Look(ref opponentTurn, "opponentTurn", false);
            Scribe_Values.Look(ref pocketedPlayer, "pocketedPlayer", 0);
            Scribe_Values.Look(ref pocketedOpponent, "pocketedOpponent", 0);
            Scribe_Values.Look(ref foulsPlayer, "foulsPlayer", 0);
            Scribe_Values.Look(ref foulsOpponent, "foulsOpponent", 0);

            Scribe_Collections.Look(ref xs, "xs", LookMode.Value);
            Scribe_Collections.Look(ref ys, "ys", LookMode.Value);
            Scribe_Collections.Look(ref down, "down", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (xs == null) xs = new List<float>();
                if (ys == null) ys = new List<float>();
                if (down == null) down = new List<bool>();
            }
        }
    }
}
