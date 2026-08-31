using System;
using Poker.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 판 자체. 규칙이 틀렸는지는 눈으로 잘 안 보이지만, 칩은 거짓말을 못 한다 -
    /// 어느 순간에도 판 위의 칩 합계는 처음 그대로여야 한다.
    /// </summary>
    public class PokerMatchTests
    {
        private const int Total = HoldemMatch.StartingStack * 2;

        private static int OnTable(HoldemMatch match)
        {
            return match.Stack(PokerSeat.Player) + match.Stack(PokerSeat.Opponent)
                 + match.Pot + match.Bet(PokerSeat.Player) + match.Bet(PokerSeat.Opponent);
        }

        /// <summary>아무렇게나 두는 손님 둘. 규칙이 허용하는 것만 고른다.</summary>
        private static int PlayRandomly(HoldemMatch match, int seed, Action<HoldemMatch> after)
        {
            PokerRng rng = new PokerRng(seed);
            int steps = 0;

            while (!match.IsOver && steps < 20000)
            {
                steps++;

                if (match.HandDone) { match.NextHand(); if (after != null) after(match); continue; }

                int roll = rng.Next(100);

                if (match.ToCall > 0 && roll < 15) match.Act(PokerAction.Fold, 0);
                else if (match.CanRaise && roll >= 80)
                    match.Act(PokerAction.Raise, match.MinRaiseTo + rng.Next(60));
                else if (match.CanCheck) match.Act(PokerAction.Check, 0);
                else match.Act(PokerAction.Call, 0);

                if (after != null) after(match);
            }

            return steps;
        }

        [Fact]
        public void 칩은_사라지지도_생기지도_않는다()
        {
            for (int seed = 1; seed <= 250; seed++)
            {
                HoldemMatch match = new HoldemMatch(seed);
                Assert.Equal(Total, OnTable(match));

                PlayRandomly(match, seed * 31, m => Assert.Equal(Total, OnTable(m)));

                Assert.True(match.IsOver);
                Assert.Equal(Total, OnTable(match));
            }
        }

        [Fact]
        public void 판은_반드시_끝난다()
        {
            for (int seed = 1; seed <= 250; seed++)
            {
                HoldemMatch match = new HoldemMatch(seed * 7919);
                int steps = PlayRandomly(match, seed, null);

                Assert.True(match.IsOver, "seed " + seed + " never finished");
                Assert.True(steps < 20000);
                Assert.True(match.Hand <= HoldemMatch.MaxHands);
            }
        }

        [Fact]
        public void 같은_시드는_같은_패를_돌린다()
        {
            HoldemMatch a = new HoldemMatch(12345);
            HoldemMatch b = new HoldemMatch(12345);

            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(a.Hole(PokerSeat.Player, i), b.Hole(PokerSeat.Player, i));
                Assert.Equal(a.Hole(PokerSeat.Opponent, i), b.Hole(PokerSeat.Opponent, i));
            }

            HoldemMatch c = new HoldemMatch(12346);
            Assert.True(a.Hole(PokerSeat.Player, 0) != c.Hole(PokerSeat.Player, 0)
                     || a.Hole(PokerSeat.Player, 1) != c.Hole(PokerSeat.Player, 1));
        }

        [Fact]
        public void 한_벌에서_같은_카드가_두_번_나오지_않는다()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                HoldemMatch match = new HoldemMatch(seed);
                bool[] seen = new bool[Cards.Count];

                int[] dealt =
                {
                    match.Hole(PokerSeat.Player, 0), match.Hole(PokerSeat.Player, 1),
                    match.Hole(PokerSeat.Opponent, 0), match.Hole(PokerSeat.Opponent, 1),
                };

                foreach (int card in dealt)
                {
                    Assert.InRange(card, 0, Cards.Count - 1);
                    Assert.False(seen[card], "duplicate card at seed " + seed);
                    seen[card] = true;
                }
            }
        }

        [Fact]
        public void 일대일에서는_버튼이_스몰블라인드이고_먼저_친다()
        {
            HoldemMatch match = new HoldemMatch(99);

            Assert.Equal(PokerSeat.Player, match.Button);
            Assert.Equal(PokerSeat.Player, match.ToAct);
            Assert.Equal(match.SmallBlind, match.Bet(PokerSeat.Player));
            Assert.Equal(match.BigBlind, match.Bet(PokerSeat.Opponent));
            Assert.Equal(match.BigBlind - match.SmallBlind, match.ToCall);
        }

        [Fact]
        public void 빅블라인드는_콜을_받고도_한_번_더_결정한다()
        {
            HoldemMatch match = new HoldemMatch(99);

            match.Act(PokerAction.Call, 0);   // 버튼이 맞춘다

            Assert.Equal(PokerStreet.Preflop, match.Street);
            Assert.Equal(PokerSeat.Opponent, match.ToAct);
            Assert.True(match.CanCheck);
        }

        [Fact]
        public void 죽으면_상대가_팟을_가져간다()
        {
            HoldemMatch match = new HoldemMatch(77);

            int before = match.Stack(PokerSeat.Opponent);
            int pot = match.Bet(PokerSeat.Player) + match.Bet(PokerSeat.Opponent);

            match.Act(PokerAction.Fold, 0);

            Assert.True(match.HandDone);
            Assert.Equal(PokerSeat.Opponent, match.HandWinner);
            Assert.Equal(before + pot, match.Stack(PokerSeat.Opponent));
            Assert.False(match.ShowdownReached);
            Assert.Equal(Total, OnTable(match));
        }

        [Fact]
        public void 올인이_붙으면_보드가_끝까지_깔린다()
        {
            HoldemMatch match = new HoldemMatch(4242);

            match.Act(PokerAction.Raise, match.MaxRaiseTo);
            match.Act(PokerAction.Call, 0);

            Assert.True(match.HandDone);
            Assert.True(match.ShowdownReached);
            Assert.Equal(5, match.BoardCount);
            Assert.Equal(Total, OnTable(match));
        }

        [Fact]
        public void 블라인드는_시간이_지나면_오른다()
        {
            HoldemMatch early = new HoldemMatch(5);
            int first = early.BigBlind;

            PlayRandomly(early, 5, null);

            // 판이 끝날 무렵에는 처음보다 커져 있어야 한다 - 그래서 판이 끝난다.
            HoldemMatch late = HoldemMatch.Restore(5, HoldemMatch.MaxHands, false,
                                                   200, 200, 0, 0, 0, false, false, 0, false, 10, 0, 0);
            Assert.True(late.BigBlind > first);
        }

        [Fact]
        public void 끝난_핸드에서_되살리면_다음_핸드부터_시작한다()
        {
            HoldemMatch match = new HoldemMatch(2024);

            int hand = match.Hand;
            bool button = match.ButtonIsOpponent;

            match.Act(PokerAction.Fold, 0);
            Assert.True(match.HandDone);

            // 끝난 핸드를 그대로 담으면 팟도 없는 죽은 판 위에 앉게 된다.
            HoldemMatch next = HoldemMatch.RestoreAtHand(
                match.Seed, hand + 1, !button,
                match.Stack(PokerSeat.Player), match.Stack(PokerSeat.Opponent), match.Actions);

            // 상대의 시드는 행동 수에서 나온다. 여기서 끊기면 창을 여닫아 상대의 수를 굴릴 수 있다.
            Assert.Equal(match.Actions, next.Actions);

            Assert.Equal(hand + 1, next.Hand);
            Assert.False(next.HandDone);
            Assert.Equal(PokerStreet.Preflop, next.Street);
            Assert.Equal(0, next.BoardCount);

            // 블라인드가 다시 걸려 있어야 한다.
            Assert.True(next.Bet(PokerSeat.Player) + next.Bet(PokerSeat.Opponent) > 0);
            Assert.Equal(Total, OnTable(next));

            // 버튼은 넘어갔고, 그쪽이 먼저 친다.
            Assert.Equal(!button, next.ButtonIsOpponent);
            Assert.Equal(next.Button, next.ToAct);
        }

        [Fact]
        public void 되살린_판은_같은_자리에서_이어진다()
        {
            HoldemMatch match = new HoldemMatch(31337);
            match.Act(PokerAction.Call, 0);
            match.Act(PokerAction.Check, 0);   // 플랍으로 넘어간다

            HoldemMatch back = HoldemMatch.Restore(
                match.Seed, match.Hand, match.ButtonIsOpponent,
                match.Stack(PokerSeat.Player), match.Stack(PokerSeat.Opponent),
                match.Pot, match.Bet(PokerSeat.Player), match.Bet(PokerSeat.Opponent),
                match.ActedBy(PokerSeat.Player), match.ActedBy(PokerSeat.Opponent),
                match.StreetIndex, match.ToActIsOpponent,
                match.LastRaiseSize, match.BoardCount, match.Actions);

            Assert.Equal(match.Actions, back.Actions);
            Assert.Equal(match.Street, back.Street);
            Assert.Equal(match.ToAct, back.ToAct);
            Assert.Equal(match.Pot, back.Pot);
            Assert.Equal(match.BoardCount, back.BoardCount);

            for (int i = 0; i < match.BoardCount; i++) Assert.Equal(match.Board(i), back.Board(i));
            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(match.Hole(PokerSeat.Player, i), back.Hole(PokerSeat.Player, i));
                Assert.Equal(match.Hole(PokerSeat.Opponent, i), back.Hole(PokerSeat.Opponent, i));
            }
        }
    }
}
