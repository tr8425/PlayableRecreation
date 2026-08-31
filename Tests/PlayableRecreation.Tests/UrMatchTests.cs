using System;
using Ur.AI;
using Ur.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 매치 진행 모델 검증. UI 없이 한 판을 끝까지 돌릴 수 있어야 M2 의 조작 흐름이 성립한다.
    /// </summary>
    public class UrMatchTests
    {
        /// <summary>첫 굴림이 지정한 눈이 나오는 시드를 찾는다.</summary>
        private static int SeedWithFirstRoll(int total)
        {
            for (int seed = 0; seed < 100000; seed++)
                if (UrDice.Roll(seed, 0).Total == total) return seed;

            throw new InvalidOperationException("해당 눈이 나오는 시드를 찾지 못했습니다: " + total);
        }

        [Fact]
        public void 새_판은_굴리기_대기_상태로_시작한다()
        {
            var match = new UrMatch(1, Side.Player);

            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
            Assert.Equal(Side.Player, match.Turn);
            Assert.False(match.RollRevealed);
            Assert.Equal(0, match.LegalCount);
            Assert.Equal(1, match.TurnCount);
            Assert.False(match.IsOver);
        }

        [Fact]
        public void 굴리면_합법수가_계산되고_주사위가_공개된다()
        {
            var match = new UrMatch(SeedWithFirstRoll(3), Side.Player);
            match.RollDice();

            Assert.True(match.RollRevealed);
            Assert.Equal(3, match.Roll.Total);
            Assert.Equal(UrPhase.AwaitingMove, match.Phase);
            Assert.Equal(1, match.LegalCount);          // 첫 턴은 투입 한 가지뿐
            Assert.Equal(1, match.DiceIndex);
        }

        [Fact]
        public void 눈이_0이면_패스_상태가_되고_턴이_넘어간다()
        {
            var match = new UrMatch(SeedWithFirstRoll(0), Side.Player);
            match.RollDice();

            Assert.Equal(UrPhase.MustPass, match.Phase);
            Assert.Equal(0, match.LegalCount);

            match.Pass();

            Assert.Equal(Side.Bot, match.Turn);
            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
            Assert.False(match.RollRevealed);
            Assert.Single(match.Log);
            Assert.True(match.Log[0].Passed);
        }

        [Fact]
        public void 로제트에_착지하면_턴을_유지한_채_다시_굴린다()
        {
            // 눈 4 로 투입하면 경로 4번(자기 로제트)에 바로 안착한다.
            var match = new UrMatch(SeedWithFirstRoll(4), Side.Player);
            match.RollDice();
            match.PlayMove(0);

            Assert.Equal(Side.Player, match.Turn);
            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
            Assert.True(match.State.IsOccupied(Side.Player, 4));
            Assert.True(match.Log[0].Move.GrantsExtraTurn);
        }

        [Fact]
        public void 로제트가_아니면_턴이_상대에게_넘어간다()
        {
            var match = new UrMatch(SeedWithFirstRoll(2), Side.Player);
            match.RollDice();
            match.PlayMove(0);

            Assert.Equal(Side.Bot, match.Turn);
            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
        }

        [Fact]
        public void 잘못된_상태나_인덱스의_호출은_무시된다()
        {
            var match = new UrMatch(SeedWithFirstRoll(2), Side.Player);

            match.PlayMove(0);                       // 아직 굴리지 않음
            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
            Assert.Empty(match.Log);

            match.Pass();                            // 패스 상태가 아님
            Assert.Equal(Side.Player, match.Turn);

            match.RollDice();
            int before = match.DiceIndex;
            match.RollDice();                        // 이미 굴린 뒤 재굴림
            Assert.Equal(before, match.DiceIndex);

            match.PlayMove(99);                      // 범위 밖
            Assert.Empty(match.Log);
        }

        [Fact]
        public void 랜덤_봇끼리_2000판이_모두_정상_종료된다()
        {
            int playerWins = 0;

            for (int game = 0; game < 2000; game++)
            {
                UrMatch match = PlayOut(game * 7717 + 13, out int steps);

                Assert.True(match.IsOver);
                Assert.True(match.Winner.HasValue);
                Assert.True(match.State.IsConsistent());
                Assert.Equal(UrBoardLayout.PieceCount, match.State.Scored(match.Winner.Value));
                Assert.True(steps < 5000);
                Assert.NotEmpty(match.Log);

                if (match.Winner.Value == Side.Player) playerWins++;
            }

            // 양쪽 다 같은 무작위 AI 이므로 선공(플레이어) 이점만큼만 앞선다.
            Assert.InRange(playerWins / 2000.0, 0.45, 0.65);
        }

        [Fact]
        public void 같은_시드는_같은_대국을_재현한다()
        {
            UrMatch a = PlayOut(31337, out _);
            UrMatch b = PlayOut(31337, out _);

            Assert.Equal(a.Log.Count, b.Log.Count);
            Assert.Equal(a.Winner, b.Winner);
            Assert.Equal(a.DiceIndex, b.DiceIndex);

            for (int i = 0; i < a.Log.Count; i++)
            {
                Assert.Equal(a.Log[i].Roll, b.Log[i].Roll);
                Assert.Equal(a.Log[i].Side, b.Log[i].Side);
                Assert.Equal(a.Log[i].Move.From, b.Log[i].Move.From);
                Assert.Equal(a.Log[i].Move.To, b.Log[i].Move.To);
            }
        }

        [Fact]
        public void 무작위_AI는_즉시_골인을_놓치지_않는다()
        {
            var ai = new RandomAi(new Random(1));
            var state = TestHelpers.State(Side.Player, new[] { 3, 14 }, new int[0], scoredPlayer: 5);
            var legal = new UrMove[UrRules.MaxMoves];
            int count = UrRules.GenerateMoves(in state, 1, legal);

            Assert.Equal(2, count);   // 3->4 와 14->골인

            for (int trial = 0; trial < 50; trial++)
            {
                int chosen = ai.ChooseMove(in state, 1, legal, count);
                Assert.True(legal[chosen].IsBearOff);
            }
        }

        private static UrMatch PlayOut(int seed, out int steps)
        {
            var match = new UrMatch(seed, Side.Player);
            var ai = new RandomAi(new Random(seed));
            steps = 0;

            while (!match.IsOver)
            {
                if (++steps > 5000) break;

                switch (match.Phase)
                {
                    case UrPhase.AwaitingRoll:
                        match.RollDice();
                        break;
                    case UrPhase.MustPass:
                        match.Pass();
                        break;
                    case UrPhase.AwaitingMove:
                        match.PlayMove(ai.ChooseMove(in match.State, match.Roll.Total,
                                                     match.LegalMoves, match.LegalCount));
                        break;
                }
            }

            return match;
        }
    }
}
