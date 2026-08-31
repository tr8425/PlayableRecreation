using System;
using RoyalGameOfUr.Core;

namespace RoyalGameOfUr.AI
{
    /// <summary>
    /// T0 초보. 합법수 중 무작위로 고르되 즉시 골인만은 놓치지 않는다. (DESIGN.md §5.1)
    /// 난수는 주입된 Random 을 쓰므로 시드를 고정하면 대국이 그대로 재현된다.
    /// </summary>
    public sealed class RandomAi : IUrAi
    {
        private readonly Random rng;

        public RandomAi(Random rng)
        {
            this.rng = rng ?? new Random();
        }

        public string Id { get { return "T0"; } }

        public int ChooseMove(in UrGameState state, int roll, UrMove[] legal, int legalCount)
        {
            for (int i = 0; i < legalCount; i++)
                if (legal[i].IsBearOff) return i;

            return rng.Next(legalCount);
        }
    }
}
