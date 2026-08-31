using System;
using Ur.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 되감기(무르기)와 이어두기(세션 복원) 검증.
    ///
    /// 핵심 불변식: 되감으면 주사위 순번까지 되돌아가므로 **같은 눈이 다시 나온다**.
    /// 이것이 무너지면 무르기와 창 닫기가 곧 세이브스컴이 된다(DESIGN.md P4).
    /// </summary>
    public class UrRewindTests
    {
        /// <summary>첫 굴림이 지정한 눈이 나오는 시드를 찾는다.</summary>
        private static int SeedWithFirstRoll(int total)
        {
            for (int seed = 0; seed < 100000; seed++)
                if (UrDice.Roll(seed, 0).Total == total) return seed;

            throw new InvalidOperationException("해당 눈이 나오는 시드를 찾지 못했습니다: " + total);
        }

        /// <summary>한 판을 무작위로 진행시킨다. 반환값은 (마지막으로 잡은 마크, 그 시점 이후 진행 여부).</summary>
        private static UrMatch PlayRandom(int seed, int turns, Random rng, out UrTurnMark mark)
        {
            var match = new UrMatch(seed, Side.Player);
            mark = match.MarkTurnStart();

            for (int i = 0; i < turns && !match.IsOver; i++)
            {
                if (i == turns / 2) mark = match.MarkTurnStart();

                match.RollDice();

                if (match.Phase == UrPhase.MustPass) match.Pass();
                else match.PlayMove(rng.Next(match.LegalCount));
            }

            return match;
        }

        [Fact]
        public void 되감으면_같은_눈이_다시_나온다()
        {
            var match = new UrMatch(12345, Side.Player);

            for (int turn = 0; turn < 20 && !match.IsOver; turn++)
            {
                UrTurnMark mark = match.MarkTurnStart();

                match.RollDice();
                int firstRoll = match.Roll.Total;

                match.RewindTo(in mark);
                match.RollDice();

                Assert.Equal(firstRoll, match.Roll.Total);

                if (match.Phase == UrPhase.MustPass) match.Pass();
                else match.PlayMove(0);
            }
        }

        [Fact]
        public void 되감으면_판과_기보와_수순이_그대로_돌아온다()
        {
            var match = new UrMatch(777, Side.Player);
            var rng = new Random(777);

            for (int i = 0; i < 6; i++)
            {
                match.RollDice();
                if (match.Phase == UrPhase.MustPass) match.Pass();
                else match.PlayMove(rng.Next(match.LegalCount));
            }

            UrTurnMark mark = match.MarkTurnStart();
            UrGameState before = match.State;
            int logBefore = match.Log.Count;
            int turnBefore = match.TurnCount;
            int diceBefore = match.DiceIndex;

            for (int i = 0; i < 5 && !match.IsOver; i++)
            {
                match.RollDice();
                if (match.Phase == UrPhase.MustPass) match.Pass();
                else match.PlayMove(rng.Next(match.LegalCount));
            }

            match.RewindTo(in mark);

            Assert.Equal(before.OccPlayer, match.State.OccPlayer);
            Assert.Equal(before.OccBot, match.State.OccBot);
            Assert.Equal(before.WaitingPlayer, match.State.WaitingPlayer);
            Assert.Equal(before.WaitingBot, match.State.WaitingBot);
            Assert.Equal(before.ScoredPlayer, match.State.ScoredPlayer);
            Assert.Equal(before.ScoredBot, match.State.ScoredBot);
            Assert.Equal(before.Turn, match.State.Turn);

            Assert.Equal(logBefore, match.Log.Count);
            Assert.Equal(turnBefore, match.TurnCount);
            Assert.Equal(diceBefore, match.DiceIndex);
            Assert.Equal(UrPhase.AwaitingRoll, match.Phase);
            Assert.False(match.RollRevealed);
        }

        [Fact]
        public void 잡기와_로제트가_집계되고_되감기로_함께_복원된다()
        {
            // 봇 말이 공유 구간 6에 있고 내 말이 5에 있다. 눈 1이면 잡는다.
            UrGameState state = TestHelpers.State(Side.Player, new[] { 5 }, new[] { 6 });
            var match = UrMatch.Restore(SeedWithFirstRoll(1), 0, state, 1, 0, 0, 0, 0);

            UrTurnMark mark = match.MarkTurnStart();
            Assert.Equal(0, match.Captures(Side.Player));

            match.RollDice();
            Assert.Equal(1, match.Roll.Total);

            int index = -1;
            for (int i = 0; i < match.LegalCount; i++)
                if (match.LegalMoves[i].From == 5 && match.LegalMoves[i].To == 6) index = i;

            Assert.True(index >= 0, "5 → 6 잡기가 합법수에 없습니다.");
            Assert.True(match.LegalMoves[index].IsCapture);

            match.PlayMove(index);
            Assert.Equal(1, match.Captures(Side.Player));

            match.RewindTo(in mark);
            Assert.Equal(0, match.Captures(Side.Player));
            Assert.True(match.State.IsOccupied(Side.Bot, 6));
        }

        [Fact]
        public void 로제트_도착이_집계된다()
        {
            // 경로 3의 말이 눈 1로 로제트(4)에 도착한다.
            UrGameState state = TestHelpers.State(Side.Player, new[] { 3 }, new int[0]);
            var match = UrMatch.Restore(SeedWithFirstRoll(1), 0, state, 1, 0, 0, 0, 0);

            match.RollDice();
            Assert.Equal(UrPhase.AwaitingMove, match.Phase);

            int index = -1;
            for (int i = 0; i < match.LegalCount; i++)
                if (match.LegalMoves[i].To == 4) index = i;

            Assert.True(index >= 0, "3 → 4 로제트 도착이 합법수에 없습니다.");
            match.PlayMove(index);

            Assert.Equal(1, match.RosetteLandings(Side.Player));
            Assert.Equal(Side.Player, match.Turn);      // 로제트는 추가 턴
        }

        [Fact]
        public void 복원한_판은_중단한_턴을_그대로_이어간다()
        {
            var rng = new Random(4242);

            for (int trial = 0; trial < 200; trial++)
            {
                int seed = rng.Next();
                UrTurnMark mark;
                UrMatch original = PlayRandom(seed, 12, new Random(seed), out mark);

                // 세션이 저장하는 것과 똑같은 값들만 넘겨 새 판을 만든다.
                UrMatch restored = UrMatch.Restore(seed, mark.DiceIndex, mark.State, mark.TurnCount,
                                                   mark.CapturesPlayer, mark.CapturesBot,
                                                   mark.RosettesPlayer, mark.RosettesBot);

                Assert.Equal(mark.State.OccPlayer, restored.State.OccPlayer);
                Assert.Equal(mark.State.OccBot, restored.State.OccBot);
                Assert.Equal(mark.State.Turn, restored.Turn);
                Assert.Equal(mark.TurnCount, restored.TurnCount);
                Assert.True(restored.State.IsConsistent());

                // 그리고 그 턴의 눈이 원래 나왔어야 할 눈과 같아야 한다.
                restored.RollDice();
                Assert.Equal(UrDice.Roll(seed, mark.DiceIndex).Total, restored.Roll.Total);

                GC.KeepAlive(original);
            }
        }

        [Fact]
        public void 되감기를_반복해도_판의_불변식이_유지된다()
        {
            var rng = new Random(31337);

            for (int trial = 0; trial < 300; trial++)
            {
                var match = new UrMatch(rng.Next(), Side.Player);
                UrTurnMark mark = match.MarkTurnStart();

                for (int i = 0; i < 40 && !match.IsOver; i++)
                {
                    if (rng.Next(4) == 0)
                    {
                        match.RewindTo(in mark);
                        Assert.True(match.State.IsConsistent());
                        continue;
                    }

                    mark = match.MarkTurnStart();
                    match.RollDice();

                    if (match.Phase == UrPhase.MustPass) match.Pass();
                    else match.PlayMove(rng.Next(match.LegalCount));

                    Assert.True(match.State.IsConsistent());
                }
            }
        }
    }
}
