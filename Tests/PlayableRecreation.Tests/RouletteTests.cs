using System.Collections.Generic;
using Roulette.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>휠의 산수 - 색, 배당, 그리고 0 의 몫.</summary>
    public class RouletteWheelTests
    {
        [Fact]
        public void 휠에는_37칸이_한_번씩_들어_있다()
        {
            Assert.Equal(RouletteWheel.Pockets, RouletteWheel.Order.Length);

            HashSet<int> seen = new HashSet<int>(RouletteWheel.Order);
            Assert.Equal(RouletteWheel.Pockets, seen.Count);
            for (int pocket = 0; pocket < RouletteWheel.Pockets; pocket++) Assert.Contains(pocket, seen);
        }

        [Fact]
        public void 빨강은_18칸이고_0은_빨강도_검정도_아니다()
        {
            int reds = 0;
            for (int pocket = 1; pocket < RouletteWheel.Pockets; pocket++)
                if (RouletteWheel.IsRed(pocket)) reds++;

            Assert.Equal(18, reds);
            Assert.False(RouletteWheel.IsRed(0));
        }

        [Fact]
        public void 배당은_스트레이트_35_다즌_2_짝수배당_1이다()
        {
            Assert.Equal(35, RouletteWheel.NetMultiplier(BetKind.Straight));
            Assert.Equal(2, RouletteWheel.NetMultiplier(BetKind.Dozen1));
            Assert.Equal(2, RouletteWheel.NetMultiplier(BetKind.Dozen2));
            Assert.Equal(2, RouletteWheel.NetMultiplier(BetKind.Dozen3));
            Assert.Equal(1, RouletteWheel.NetMultiplier(BetKind.Red));
            Assert.Equal(1, RouletteWheel.NetMultiplier(BetKind.Even));
        }

        [Fact]
        public void 영은_영을_집은_스트레이트_말고는_모두를_이긴다()
        {
            Assert.True(RouletteWheel.Wins(BetKind.Straight, 0, 0));

            Assert.False(RouletteWheel.Wins(BetKind.Red, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Black, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Odd, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Even, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Dozen1, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Dozen2, 0, 0));
            Assert.False(RouletteWheel.Wins(BetKind.Dozen3, 0, 0));
        }

        [Fact]
        public void 바깥_베팅의_판정이_숫자대로_맞는다()
        {
            // 17: 검정 · 홀수 · 둘째 다즌.
            Assert.False(RouletteWheel.Wins(BetKind.Red, 0, 17));
            Assert.True(RouletteWheel.Wins(BetKind.Black, 0, 17));
            Assert.True(RouletteWheel.Wins(BetKind.Odd, 0, 17));
            Assert.False(RouletteWheel.Wins(BetKind.Even, 0, 17));
            Assert.False(RouletteWheel.Wins(BetKind.Dozen1, 0, 17));
            Assert.True(RouletteWheel.Wins(BetKind.Dozen2, 0, 17));
            Assert.False(RouletteWheel.Wins(BetKind.Dozen3, 0, 17));

            // 36: 빨강 · 짝수 · 셋째 다즌.
            Assert.True(RouletteWheel.Wins(BetKind.Red, 0, 36));
            Assert.True(RouletteWheel.Wins(BetKind.Even, 0, 36));
            Assert.True(RouletteWheel.Wins(BetKind.Dozen3, 0, 36));
        }

        [Fact]
        public void 같은_시드와_순번이면_같은_칸이_나온다()
        {
            for (int spin = 0; spin < 50; spin++)
            {
                int pocket = RouletteWheel.PocketAt(1234, spin);
                Assert.Equal(pocket, RouletteWheel.PocketAt(1234, spin));
                Assert.InRange(pocket, 0, RouletteWheel.Pockets - 1);
            }
        }

        [Fact]
        public void 충분히_돌리면_모든_칸이_나온다()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int spin = 0; spin < 4000; spin++) seen.Add(RouletteWheel.PocketAt(77, spin));
            Assert.Equal(RouletteWheel.Pockets, seen.Count);
        }

        [Fact]
        public void 휠_자리와_각도가_서로를_되찾는다()
        {
            for (int index = 0; index < RouletteWheel.Pockets; index++)
            {
                int pocket = RouletteWheel.Order[index];
                Assert.Equal(index, RouletteWheel.WheelIndexOf(pocket));
                Assert.Equal(index, RouletteWheel.WheelIndexAt(index * RouletteWheel.SectorAngle));
            }
        }
    }

    /// <summary>뱅크롤 런 - 목표에 닿으면 이기고, 마르면 진다.</summary>
    public class RouletteRunTests
    {
        /// <summary>이 시드의 첫 칸. 테스트가 결과를 미리 알고 걸 수 있게 한다.</summary>
        private static int FirstPocket(int seed)
        {
            return RouletteWheel.PocketAt(seed, 0);
        }

        [Fact]
        public void 목표_사다리는_단조롭게_오른다()
        {
            for (int tier = 1; tier < 5; tier++)
                Assert.True(RouletteRun.TargetFor(tier) > RouletteRun.TargetFor(tier - 1));

            Assert.Equal(30, RouletteRun.TargetFor(0));
            Assert.Equal(100, RouletteRun.TargetFor(4));
        }

        [Fact]
        public void 이기면_배당만큼_늘고_지면_건_만큼_준다()
        {
            int seed = 11;
            int pocket = FirstPocket(seed);

            RouletteRun winner = new RouletteRun(seed, 1000);
            winner.Spin(BetKind.Straight, pocket, 2);
            Assert.Equal(RouletteRun.StartBankroll + 70, winner.Bankroll);

            int missed = pocket == 0 ? 1 : 0;
            RouletteRun loser = new RouletteRun(seed, 1000);
            loser.Spin(BetKind.Straight, missed, 2);
            Assert.Equal(RouletteRun.StartBankroll - 2, loser.Bankroll);
        }

        [Fact]
        public void 건_돈은_테이블_리밋과_뱅크롤에_눌린다()
        {
            int seed = 5;
            int pocket = FirstPocket(seed);
            int missed = pocket == 0 ? 1 : 0;

            RouletteRun run = new RouletteRun(seed, 1000);
            RouletteEntry entry = run.Spin(BetKind.Straight, missed, 999);

            Assert.Equal(RouletteRun.MaxStake, entry.Stake);
            Assert.Equal(RouletteRun.StartBankroll - RouletteRun.MaxStake, run.Bankroll);
        }

        [Fact]
        public void 목표에_닿으면_이기고_끝난다()
        {
            int seed = 21;
            int pocket = FirstPocket(seed);

            RouletteRun run = new RouletteRun(seed, RouletteRun.StartBankroll + 35);
            run.Spin(BetKind.Straight, pocket, 1);

            Assert.True(run.IsOver);
            Assert.True(run.Won);
        }

        [Fact]
        public void 다_잃으면_지고_끝난다()
        {
            int seed = 8;

            RouletteRun run = new RouletteRun(seed, 10000);
            int guard = 0;

            while (!run.IsOver && guard++ < 10000)
            {
                // 항상 빗나가는 스트레이트에 최대로 건다.
                int pocket = RouletteWheel.PocketAt(seed, run.Spins);
                run.Spin(BetKind.Straight, pocket == 0 ? 1 : 0, RouletteRun.MaxStake);
            }

            Assert.True(run.IsOver);
            Assert.False(run.Won);
            Assert.Equal(0, run.Bankroll);
        }

        [Fact]
        public void 끝난_판은_더_받지_않는다()
        {
            int seed = 21;
            int pocket = FirstPocket(seed);

            RouletteRun run = new RouletteRun(seed, RouletteRun.StartBankroll + 35);
            run.Spin(BetKind.Straight, pocket, 1);

            int bankroll = run.Bankroll;
            int spins = run.Spins;

            run.Spin(BetKind.Red, 0, 5);

            Assert.Equal(bankroll, run.Bankroll);
            Assert.Equal(spins, run.Spins);
        }

        [Fact]
        public void 기록을_재생하면_같은_판이_된다()
        {
            int seed = 33;
            RouletteRun run = new RouletteRun(seed, 500);

            BetKind[] kinds = { BetKind.Red, BetKind.Dozen2, BetKind.Straight, BetKind.Black, BetKind.Even };
            for (int i = 0; i < 12 && !run.IsOver; i++)
                run.Spin(kinds[i % kinds.Length], 17, 1 + i % RouletteRun.MaxStake);

            RouletteRun restored = RouletteRun.Restore(seed, 500, run.Log);

            Assert.Equal(run.Bankroll, restored.Bankroll);
            Assert.Equal(run.Spins, restored.Spins);
            Assert.Equal(run.Peak, restored.Peak);
            Assert.Equal(run.BestWin, restored.BestWin);
            Assert.Equal(run.StraightHits, restored.StraightHits);
            Assert.Equal(run.IsOver, restored.IsOver);
            Assert.Equal(run.DippedBelowStart, restored.DippedBelowStart);
        }

        [Fact]
        public void 시작액_아래로_내려가면_완주_흠집이_남는다()
        {
            int seed = 5;
            int pocket = FirstPocket(seed);
            int missed = pocket == 0 ? 1 : 0;

            RouletteRun run = new RouletteRun(seed, 1000);
            Assert.False(run.DippedBelowStart);

            run.Spin(BetKind.Straight, missed, 1);
            Assert.True(run.DippedBelowStart);
        }

        [Fact]
        public void 집계가_큰_적중과_최고_잔고를_기억한다()
        {
            int seed = 40;
            int pocket = FirstPocket(seed);

            RouletteRun run = new RouletteRun(seed, 10000);
            run.Spin(BetKind.Straight, pocket, 3);

            Assert.Equal(105, run.BestWin);
            Assert.Equal(1, run.StraightHits);
            Assert.Equal(RouletteRun.StartBankroll + 105, run.Peak);
        }
    }
}
