using System;
using System.Diagnostics;
using Ur.AI;
using Ur.Core;
using Xunit;
using Xunit.Abstractions;

namespace PlayableRecreation.Tests
{
    public class UrAiTests
    {
        private readonly ITestOutputHelper output;

        public UrAiTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // ---------- 평가 함수 ----------

        [Fact]
        public void 골인한_말이_가장_높게_평가된다()
        {
            var empty = TestHelpers.State(Side.Player, new int[0], new int[0]);
            var scored = TestHelpers.State(Side.Player, new int[0], new int[0], scoredPlayer: 1);

            Assert.True(UrEvaluator.Evaluate(in scored, Side.Player)
                      > UrEvaluator.Evaluate(in empty, Side.Player) + 90f);
        }

        [Fact]
        public void 중앙_안전칸_점유가_더_전진한_일반칸보다_가치있다()
        {
            var safe = TestHelpers.State(Side.Player, new[] { 8 }, new int[0]);
            var ahead = TestHelpers.State(Side.Player, new[] { 9 }, new int[0]);

            Assert.True(UrEvaluator.Evaluate(in safe, Side.Player)
                      > UrEvaluator.Evaluate(in ahead, Side.Player));
        }

        [Fact]
        public void 잡힐_확률이_높을수록_낮게_평가된다()
        {
            // 내 말 12번. 상대가 10번에 있으면 눈 2(6/16)로, 11번에 있으면 눈 1(4/16)로 잡는다.
            // 상대 말의 전진 가치 차이(2)보다 위협 차이(3.5)가 커서 10번 쪽이 더 나쁜 국면이다.
            var moreRisk = TestHelpers.State(Side.Player, new[] { 12 }, new[] { 10 });
            var lessRisk = TestHelpers.State(Side.Player, new[] { 12 }, new[] { 11 });

            Assert.True(UrEvaluator.Evaluate(in moreRisk, Side.Player)
                      < UrEvaluator.Evaluate(in lessRisk, Side.Player));
        }

        [Fact]
        public void 안전칸_위의_말은_위협으로_계산되지_않는다()
        {
            // 상대가 바로 뒤 7번에 있어도 8번(안전칸)의 내 말은 잡히지 않는다.
            var onSafe = TestHelpers.State(Side.Player, new[] { 8 }, new[] { 7 });
            var offSafe = TestHelpers.State(Side.Player, new[] { 9 }, new[] { 7 });

            float safeGap = UrEvaluator.Evaluate(in onSafe, Side.Player)
                          - UrEvaluator.Evaluate(in offSafe, Side.Player);

            // 로제트 보너스(30) + 위협 회피분이 전진 1칸 손해(2)를 압도한다.
            Assert.True(safeGap > 25f, "안전칸 이점이 반영되지 않음: " + safeGap);
        }

        // ---------- 수 선택 ----------

        [Fact]
        public void 견습은_잡기를_로제트보다_우선한다()
        {
            // 눈 1: 6->7 은 상대 말을 잡고, 13->14 는 로제트에 착지한다.
            var state = TestHelpers.State(Side.Player, new[] { 6, 13 }, new[] { 7 }, scoredPlayer: 5);
            var legal = new UrMove[UrRules.MaxMoves];
            int count = UrRules.GenerateMoves(in state, 1, legal);

            Assert.Equal(2, count);

            var ai = new GreedyAi(new Random(1), 0f);
            int chosen = ai.ChooseMove(in state, 1, legal, count);

            Assert.True(legal[chosen].IsCapture);
        }

        [Fact]
        public void 탐색_AI는_같은_국면에서_같은_수를_고른다()
        {
            var state = TestHelpers.State(Side.Player, new[] { 2, 6, 11 }, new[] { 5, 9 });
            var legal = new UrMove[UrRules.MaxMoves];
            int count = UrRules.GenerateMoves(in state, 2, legal);

            var a = new ExpectiminimaxAi(3, 0f, new Random(1));
            var b = new ExpectiminimaxAi(3, 0f, new Random(999));

            Assert.Equal(a.ChooseMove(in state, 2, legal, count),
                         b.ChooseMove(in state, 2, legal, count));
        }

        [Fact]
        public void 난이도별로_생성되는_AI가_다르다()
        {
            Assert.IsType<RandomAi>(UrDifficultyInfo.Create(UrDifficulty.Novice, new Random(1)));
            Assert.IsType<GreedyAi>(UrDifficultyInfo.Create(UrDifficulty.Apprentice, new Random(1)));
            Assert.IsType<ExpectiminimaxAi>(UrDifficultyInfo.Create(UrDifficulty.Skilled, new Random(1)));
            Assert.IsType<ExpectiminimaxAi>(UrDifficultyInfo.Create(UrDifficulty.Master, new Random(1)));

            Assert.Equal(4, UrDifficultyInfo.SearchDepth(UrDifficulty.Master));
            Assert.Equal(0f, UrDifficultyInfo.BlunderChance(UrDifficulty.Master));
        }

        [Theory]
        [InlineData(0, UrDifficulty.Novice)]
        [InlineData(5, UrDifficulty.Apprentice)]
        [InlineData(10, UrDifficulty.Skilled)]
        [InlineData(14, UrDifficulty.Expert)]
        [InlineData(20, UrDifficulty.Master)]
        public void 폰_지능_연동_매핑이_명세와_일치한다(int skill, UrDifficulty expected)
        {
            Assert.Equal(expected, UrDifficultyInfo.FromIntellectual(skill));
        }

        // ---------- 성능 ----------

        [Fact]
        public void 명인의_한_수_판단은_50ms_이내다()
        {
            var ai = new ExpectiminimaxAi(4, 0f, new Random(1));
            var state = TestHelpers.State(Side.Player, new[] { 2, 5, 8, 11 }, new[] { 6, 9 },
                                          scoredPlayer: 1, scoredBot: 1);
            var legal = new UrMove[UrRules.MaxMoves];
            int count = UrRules.GenerateMoves(in state, 2, legal);
            Assert.True(count > 1);

            ai.ChooseMove(in state, 2, legal, count);   // JIT 워밍업

            const int iterations = 20;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) ai.ChooseMove(in state, 2, legal, count);
            sw.Stop();

            double avg = sw.Elapsed.TotalMilliseconds / iterations;
            output.WriteLine($"명인(depth 4) 1수 판단 평균 = {avg:F2} ms (합법수 {count}개)");
            Assert.True(avg < 50.0, $"평균 {avg:F2} ms 로 50ms 예산 초과");
        }

        // ---------- 실력 서열 ----------

        [Fact]
        public void 숙련은_초보를_확실히_이긴다()
        {
            double rate = WinRate(UrDifficulty.Skilled, UrDifficulty.Novice, 200);
            output.WriteLine($"숙련 vs 초보 = {rate:P1}");
            Assert.True(rate > 0.60, $"승률 {rate:P1}");
        }

        [Fact]
        public void 명인은_초보를_압도한다()
        {
            double rate = WinRate(UrDifficulty.Master, UrDifficulty.Novice, 100);
            output.WriteLine($"명인 vs 초보 = {rate:P1}");
            Assert.True(rate > 0.65, $"승률 {rate:P1}");
        }

        [Fact]
        public void 명인은_견습보다_강하다()
        {
            double rate = WinRate(UrDifficulty.Master, UrDifficulty.Apprentice, 150);
            output.WriteLine($"명인 vs 견습 = {rate:P1}");
            Assert.True(rate > 0.50, $"승률 {rate:P1}");
        }

        // ---------- 대전 유틸 ----------

        /// <summary>선공 이점을 상쇄하기 위해 절반은 자리를 바꿔 둔다.</summary>
        private static double WinRate(UrDifficulty challenger, UrDifficulty defender, int games)
        {
            int wins = 0;

            for (int i = 0; i < games; i++)
            {
                int seed = i * 6151 + 17;
                bool challengerFirst = i % 2 == 0;

                IUrAi first = UrDifficultyInfo.Create(
                    challengerFirst ? challenger : defender, new Random(seed));
                IUrAi second = UrDifficultyInfo.Create(
                    challengerFirst ? defender : challenger, new Random(seed + 1));

                Side winner = PlayMatch(seed, first, second);
                bool challengerWon = challengerFirst ? winner == Side.Player : winner == Side.Bot;
                if (challengerWon) wins++;
            }

            return wins / (double)games;
        }

        private static Side PlayMatch(int seed, IUrAi playerAi, IUrAi botAi)
        {
            var match = new UrMatch(seed, Side.Player);
            int guard = 0;

            while (!match.IsOver)
            {
                if (++guard > 5000) throw new InvalidOperationException("대국이 종료되지 않음");

                switch (match.Phase)
                {
                    case UrPhase.AwaitingRoll:
                        match.RollDice();
                        break;
                    case UrPhase.MustPass:
                        match.Pass();
                        break;
                    case UrPhase.AwaitingMove:
                        IUrAi ai = match.Turn == Side.Player ? playerAi : botAi;
                        match.PlayMove(ai.ChooseMove(in match.State, match.Roll.Total,
                                                     match.LegalMoves, match.LegalCount));
                        break;
                }
            }

            return match.Winner.Value;
        }
    }
}
