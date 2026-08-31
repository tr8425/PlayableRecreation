using Poker.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 상대. 세기를 공식이 아니라 시뮬레이션으로 재므로, 확인할 것은 두 가지뿐이다 -
    /// 좋은 손을 좋다고 읽는가, 그리고 규칙이 허용하지 않는 것을 고르지 않는가.
    /// </summary>
    public class PokerAiTests
    {
        private static double Equity(string hole, string board, int samples)
        {
            HoldemMatch match = new HoldemMatch(1);

            // 상대 자리에 특정 손을 앉히고 승률만 재는 도구.
            EquityProbe probe = new EquityProbe(hole, board);
            return probe.Run(samples);
        }

        [Fact]
        public void 에이스_한_쌍은_대부분_이긴다()
        {
            double equity = Equity("Ah As", "", 4000);
            Assert.InRange(equity, 0.80, 0.92);
        }

        [Fact]
        public void 최악의_손은_대부분_진다()
        {
            double equity = Equity("7h 2c", "", 4000);
            Assert.InRange(equity, 0.28, 0.40);
        }

        [Fact]
        public void 보드가_깔리면_확신이_생긴다()
        {
            // 이미 완성된 스트레이트플러시. 질 수가 없다.
            double equity = Equity("Ah Kh", "Qh Jh Th", 2000);
            Assert.True(equity > 0.99, "equity was " + equity);
        }

        [Fact]
        public void 표본이_쌓일수록_값이_안정된다()
        {
            EquityProbe probe = new EquityProbe("Ah As", "");

            double coarse = probe.Run(200);
            double fine = probe.Run(6000);

            Assert.InRange(coarse, 0.60, 1.00);
            Assert.InRange(fine, 0.80, 0.92);
        }

        [Fact]
        public void 상대는_규칙_밖의_수를_두지_않는다()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                HoldemMatch match = new HoldemMatch(seed);
                int steps = 0;

                while (!match.IsOver && steps < 20000)
                {
                    steps++;
                    if (match.HandDone) { match.NextHand(); continue; }

                    int tier = seed % 5;
                    PokerPlanner planner = new PokerPlanner(match, tier, seed * 977 + steps);
                    planner.Step(80);

                    int raiseTo;
                    PokerAction action = planner.Decide(out raiseTo);

                    if (action == PokerAction.Check) Assert.True(match.CanCheck);
                    if (action == PokerAction.Raise)
                    {
                        Assert.True(match.CanRaise);
                        Assert.InRange(raiseTo, match.MinRaiseTo, match.MaxRaiseTo);
                    }

                    match.Act(action, raiseTo);
                }

                Assert.True(match.IsOver, "seed " + seed + " never finished");
            }
        }

        /// <summary>지정한 손과 보드로 승률만 재는 작은 도구. AI 가 쓰는 것과 같은 방식이다.</summary>
        private sealed class EquityProbe
        {
            private readonly int[] mine = new int[7];
            private readonly int[] theirs = new int[7];
            private readonly int[] deck = new int[Cards.Count];
            private readonly int boardKnown;
            private int deckCount;

            public EquityProbe(string hole, string board)
            {
                int[] holeCards = PokerHandTests_Parse(hole);
                int[] boardCards = board.Length == 0 ? new int[0] : PokerHandTests_Parse(board);

                boardKnown = boardCards.Length;
                mine[0] = holeCards[0];
                mine[1] = holeCards[1];
                for (int i = 0; i < boardKnown; i++) mine[2 + i] = boardCards[i];

                bool[] used = new bool[Cards.Count];
                used[mine[0]] = true;
                used[mine[1]] = true;
                for (int i = 0; i < boardKnown; i++) used[mine[2 + i]] = true;

                for (int card = 0; card < Cards.Count; card++)
                    if (!used[card]) deck[deckCount++] = card;
            }

            public double Run(int samples)
            {
                PokerRng rng = new PokerRng(4242);
                int wins = 0, ties = 0;
                int missing = 5 - boardKnown;

                for (int s = 0; s < samples; s++)
                {
                    for (int i = 0; i < 2 + missing; i++)
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
                }

                return (wins + ties * 0.5) / samples;
            }
        }

        private static int[] PokerHandTests_Parse(string text)
        {
            string[] parts = text.Split(' ');
            int[] cards = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                int rank = Cards.RankLetters.IndexOf(char.ToUpperInvariant(parts[i][0]));
                int suit = "cdhs".IndexOf(char.ToLowerInvariant(parts[i][1]));
                cards[i] = Cards.Of(rank, suit);
            }

            return cards;
        }
    }
}
