using System;

namespace Poker.Core
{
    /// <summary>
    /// 상대. 손의 세기를 공식으로 재지 않고 <b>끝까지 돌려 본다</b> -
    /// 내 두 장과 깔린 보드를 고정한 채 나머지를 무작위로 채워 이기는 비율을 센다.
    ///
    /// 그 표본을 한 프레임에 다 뽑지 않는다. 창이 매 프레임 <see cref="Step"/> 를 부르면
    /// 뜸들이는 사이에 저절로 정확해진다 - 체스가 한 깊이씩 깊어지는 것과 같은 자리다.
    /// </summary>
    public sealed class PokerPlanner
    {
        private static readonly int[] Samples = { 60, 200, 600, 1500, 3000 };

        /// <summary>승률을 잘못 읽는 폭. 아래 단계일수록 자기 손을 착각한다.</summary>
        private static readonly double[] Noise = { 0.22, 0.12, 0.06, 0.02, 0.0 };

        /// <summary>약한 손으로 걸어 보는 빈도.</summary>
        private static readonly double[] BluffRate = { 0.0, 0.05, 0.12, 0.18, 0.22 };

        /// <summary>손해인 콜을 받아 주는 폭. T0 은 웬만해선 안 죽는다.</summary>
        private static readonly double[] FoldSlack = { 0.20, 0.11, 0.04, 0.01, 0.0 };

        /// <summary>좋은 손을 실제로 키우는 빈도.</summary>
        private static readonly double[] Aggression = { 0.20, 0.40, 0.62, 0.78, 0.88 };

        private readonly HoldemMatch match;
        private readonly int tier;
        private readonly PokerRng rng;
        private readonly int target;

        private readonly int[] deck = new int[Cards.Count];
        private readonly int[] mine = new int[7];
        private readonly int[] theirs = new int[7];

        private int deckCount;
        private int boardKnown;
        private int wins;
        private int ties;
        private int played;

        private double bias;
        private bool biasTaken;

        public PokerPlanner(HoldemMatch match, int tier, int seed)
        {
            this.match = match;
            this.tier = Math.Max(0, Math.Min(Samples.Length - 1, tier));
            rng = new PokerRng(seed);
            target = Samples[this.tier];

            Prepare();
        }

        /// <summary>내 두 장과 이미 깔린 보드를 빼고 남은 카드를 모아 둔다.</summary>
        private void Prepare()
        {
            boardKnown = match.BoardCount;

            mine[0] = match.Hole(PokerSeat.Opponent, 0);
            mine[1] = match.Hole(PokerSeat.Opponent, 1);
            for (int i = 0; i < boardKnown; i++) mine[2 + i] = match.Board(i);

            bool[] used = new bool[Cards.Count];
            used[mine[0]] = true;
            used[mine[1]] = true;
            for (int i = 0; i < boardKnown; i++) used[mine[2 + i]] = true;

            deckCount = 0;
            for (int card = 0; card < Cards.Count; card++)
                if (!used[card]) deck[deckCount++] = card;
        }

        public bool Done { get { return played >= target; } }

        public double Equity
        {
            get { return played <= 0 ? 0.5 : (wins + ties * 0.5) / played; }
        }

        /// <summary>표본을 조금 더 뽑는다. 한 프레임에 몰아 하면 창이 끊긴다.</summary>
        public void Step(int count)
        {
            for (int i = 0; i < count && played < target; i++) Rollout();
        }

        private void Rollout()
        {
            int missing = 5 - boardKnown;
            int need = 2 + missing;

            // 남은 덱에서 필요한 만큼만 앞으로 끌어온다. 전체를 섞을 이유가 없다.
            for (int i = 0; i < need; i++)
            {
                int j = i + rng.Next(deckCount - i);
                int swap = deck[i];
                deck[i] = deck[j];
                deck[j] = swap;
            }

            theirs[0] = deck[0];
            theirs[1] = deck[1];

            for (int i = 0; i < boardKnown; i++) theirs[2 + i] = mine[2 + i];
            for (int i = 0; i < missing; i++)
            {
                int card = deck[2 + i];
                mine[2 + boardKnown + i] = card;
                theirs[2 + boardKnown + i] = card;
            }

            int here = HandEval.Score(mine, 7);
            int there = HandEval.Score(theirs, 7);

            if (here > there) wins++;
            else if (here == there) ties++;

            played++;
        }

        // ---------- 결정 ----------

        /// <summary>이번 판 내내 같은 방향으로 틀린다. 매 결정마다 새로 흔들면 그냥 산만해 보인다.</summary>
        private double Read
        {
            get
            {
                if (!biasTaken)
                {
                    biasTaken = true;
                    bias = (rng.NextDouble() * 2.0 - 1.0) * Noise[tier];
                }

                double read = Equity + bias;
                return read < 0.0 ? 0.0 : (read > 1.0 ? 1.0 : read);
            }
        }

        public PokerAction Decide(out int raiseTo)
        {
            raiseTo = 0;

            double equity = Read;
            int toCall = match.ToCall;
            int pot = match.Pot + match.Bet(PokerSeat.Player) + match.Bet(PokerSeat.Opponent);

            if (toCall <= 0) return DecideUnraised(equity, pot, out raiseTo);

            double potOdds = toCall / (double)(pot + toCall);

            if (equity < potOdds - FoldSlack[tier]) return PokerAction.Fold;

            if (equity > 0.78 && match.CanRaise && rng.NextDouble() < Aggression[tier])
            {
                raiseTo = Size(pot, equity > 0.92 ? 1.0 : 0.6);
                return PokerAction.Raise;
            }

            return PokerAction.Call;
        }

        private PokerAction DecideUnraised(double equity, int pot, out int raiseTo)
        {
            raiseTo = 0;

            if (match.CanRaise)
            {
                if (equity > 0.66 && rng.NextDouble() < Aggression[tier])
                {
                    raiseTo = Size(pot, equity > 0.90 ? 0.85 : 0.5);
                    return PokerAction.Raise;
                }

                // 아무것도 없을 때만 걸어 본다. 어중간한 손으로 부풀리지는 않는다.
                if (equity < 0.34 && rng.NextDouble() < BluffRate[tier])
                {
                    raiseTo = Size(pot, 0.4);
                    return PokerAction.Raise;
                }
            }

            return PokerAction.Check;
        }

        /// <summary>팟의 몇 할을 걸지 정해 5칩 단위로 맞춘다.</summary>
        private int Size(int pot, double fraction)
        {
            int high = Math.Max(match.Bet(PokerSeat.Player), match.Bet(PokerSeat.Opponent));
            int target = high + (int)Math.Round(pot * fraction / 5.0) * 5;

            if (target < match.MinRaiseTo) target = match.MinRaiseTo;
            if (target > match.MaxRaiseTo) target = match.MaxRaiseTo;

            return target;
        }
    }
}
