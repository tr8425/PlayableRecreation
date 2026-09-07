using System;
using Darts.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>다트판의 기하 - 자리가 곧 점수다.</summary>
    public class DartBoardTests
    {
        private static void Polar(float angleDeg, float radius, out float x, out float y)
        {
            double angle = angleDeg * Math.PI / 180.0;
            x = (float)Math.Sin(angle) * radius;
            y = (float)Math.Cos(angle) * radius;
        }

        [Fact]
        public void 정중앙은_50점이다()
        {
            DartHit hit = DartBoard.ScoreAt(0f, 0f);
            Assert.Equal(50, hit.Points);
            Assert.Equal("50", hit.Code);
        }

        [Fact]
        public void 바깥_불은_25점이다()
        {
            DartHit hit = DartBoard.ScoreAt(0f, 0.06f);
            Assert.Equal(25, hit.Points);
            Assert.Equal("25", hit.Code);
        }

        [Fact]
        public void 판_밖은_0점이다()
        {
            DartHit hit = DartBoard.ScoreAt(0f, 1.01f);
            Assert.Equal(0, hit.Points);
            Assert.Equal(string.Empty, hit.Code);
        }

        [Fact]
        public void 십이시_방향은_20이다()
        {
            Assert.Equal("S20", DartBoard.ScoreAt(0f, 0.75f).Code);
            Assert.Equal("T20", DartBoard.ScoreAt(0f, 0.6f).Code);
            Assert.Equal("D20", DartBoard.ScoreAt(0f, 0.98f).Code);
        }

        [Fact]
        public void 트리플은_세_배_더블은_두_배다()
        {
            Assert.Equal(60, DartBoard.ScoreAt(0f, 0.6f).Points);
            Assert.Equal(40, DartBoard.ScoreAt(0f, 0.98f).Points);
            Assert.Equal(20, DartBoard.ScoreAt(0f, 0.75f).Points);
        }

        [Fact]
        public void 표준_배열대로_섹터가_박혀_있다()
        {
            // 시계 방향: 12시가 20, 오른쪽(3시)이 6, 아래(6시)가 3, 왼쪽(9시)이 11.
            float x, y;

            Polar(90f, 0.75f, out x, out y);
            Assert.Equal("S6", DartBoard.ScoreAt(x, y).Code);

            Polar(180f, 0.75f, out x, out y);
            Assert.Equal("S3", DartBoard.ScoreAt(x, y).Code);

            Polar(270f, 0.75f, out x, out y);
            Assert.Equal("S11", DartBoard.ScoreAt(x, y).Code);
        }

        [Fact]
        public void 이십의_이웃은_1과_5다()
        {
            float x, y;

            Polar(18f, 0.75f, out x, out y);
            Assert.Equal("S1", DartBoard.ScoreAt(x, y).Code);

            Polar(-18f, 0.75f, out x, out y);
            Assert.Equal("S5", DartBoard.ScoreAt(x, y).Code);
        }
    }

    /// <summary>다트 한 판 - 5라운드 × 3발 합계전.</summary>
    public class DartsMatchTests
    {
        /// <summary>양쪽 모두 트리플 20 정타로만 던지는 한 라운드.</summary>
        private static void PlayRound(DartsMatch match, float playerY, float opponentY)
        {
            for (int i = 0; i < DartsMatch.DartsPerVisit; i++) match.Throw(0f, playerY);
            for (int i = 0; i < DartsMatch.DartsPerVisit; i++) match.Throw(0f, opponentY);
        }

        [Fact]
        public void 플레이어가_세_발_던지면_상대_차례다()
        {
            DartsMatch match = new DartsMatch(1);
            Assert.Equal(DartSide.Player, match.Turn);

            for (int i = 0; i < 3; i++) match.Throw(0f, 0.6f);

            Assert.Equal(DartSide.Opponent, match.Turn);
        }

        [Fact]
        public void 라운드가_닫히면_다시_플레이어부터다()
        {
            DartsMatch match = new DartsMatch(1);
            PlayRound(match, 0.6f, 0.75f);

            Assert.Equal(2, match.Round);
            Assert.Equal(DartSide.Player, match.Turn);
            Assert.Equal(0, match.VisitCountPlayer);
        }

        [Fact]
        public void 다섯_라운드_뒤_합계가_큰_쪽이_이긴다()
        {
            DartsMatch match = new DartsMatch(1);

            // 플레이어는 트리플 20(60), 상대는 싱글 20(20).
            for (int round = 0; round < DartsMatch.BaseRounds; round++)
                PlayRound(match, 0.6f, 0.75f);

            Assert.True(match.IsOver);
            Assert.Equal(DartSide.Player, match.Winner);
            Assert.Equal(60 * 3 * 5, match.TotalPlayer);
            Assert.Equal(20 * 3 * 5, match.TotalOpponent);
        }

        [Fact]
        public void 동점이면_갈릴_때까지_한_라운드씩_더_던진다()
        {
            DartsMatch match = new DartsMatch(1);

            for (int round = 0; round < DartsMatch.BaseRounds; round++)
                PlayRound(match, 0.6f, 0.6f);

            Assert.False(match.IsOver);
            Assert.Equal(DartsMatch.BaseRounds + 1, match.Round);

            PlayRound(match, 0.6f, 0.75f);
            Assert.True(match.IsOver);
            Assert.Equal(DartSide.Player, match.Winner);
        }

        [Fact]
        public void 집계는_플레이어의_것만_센다()
        {
            DartsMatch match = new DartsMatch(1);

            // 플레이어: 트리플 둘 + 불 하나. 상대: 트리플 셋.
            match.Throw(0f, 0.6f);
            match.Throw(0f, 0.6f);
            match.Throw(0f, 0f);
            for (int i = 0; i < 3; i++) match.Throw(0f, 0.6f);

            Assert.Equal(2, match.Triples);
            Assert.Equal(1, match.Bulls);
            Assert.Equal(60 + 60 + 50, match.BestVisit);
        }

        [Fact]
        public void 기록에서_되살리면_상태가_그대로다()
        {
            DartsMatch match = new DartsMatch(77);
            PlayRound(match, 0.6f, 0.75f);
            match.Throw(0f, 0.98f);
            match.Throw(0.4f, 0.4f);

            DartsMatch restored = DartsMatch.Restore(77, match.Log);

            Assert.Equal(match.Round, restored.Round);
            Assert.Equal(match.Turn, restored.Turn);
            Assert.Equal(match.ThrowIndex, restored.ThrowIndex);
            Assert.Equal(match.TotalPlayer, restored.TotalPlayer);
            Assert.Equal(match.TotalOpponent, restored.TotalOpponent);
            Assert.Equal(match.VisitCountPlayer, restored.VisitCountPlayer);
            Assert.Equal(match.VisitSumPlayer, restored.VisitSumPlayer);
            Assert.Equal(match.Triples, restored.Triples);
            Assert.Equal(match.Bulls, restored.Bulls);
        }

        [Fact]
        public void 모든_라운드를_이겨야_완봉이다()
        {
            DartsMatch match = new DartsMatch(1);

            for (int round = 0; round < DartsMatch.BaseRounds; round++)
                PlayRound(match, 0.6f, 0.75f);

            Assert.Equal(match.RoundsClosed, match.RoundsWonPlayer);
        }
    }

    /// <summary>상대의 다트 - (시드, 순번) 결정론과 난이도의 방향.</summary>
    public class DartsAiTests
    {
        [Fact]
        public void 같은_시드와_순번은_같은_자리에_꽂힌다()
        {
            for (int index = 0; index < 30; index++)
            {
                float x1, y1, x2, y2;
                DartsAi.BotDart(4242, index, 3, out x1, out y1);
                DartsAi.BotDart(4242, index, 3, out x2, out y2);

                Assert.Equal(x1, x2, 6);
                Assert.Equal(y1, y2, 6);
            }
        }

        [Fact]
        public void 티어가_오를수록_기대_점수가_단조롭게_오른다()
        {
            long previous = -1;

            for (int tier = 0; tier < 5; tier++)
            {
                long total = 0;
                for (int index = 0; index < 3000; index++)
                {
                    float x, y;
                    DartsAi.BotDart(9, index, tier, out x, out y);
                    total += DartBoard.ScoreAt(x, y).Points;
                }

                Assert.True(total > previous, "tier " + tier + " 이 그 아래보다 못 던진다");
                previous = total;
            }
        }
    }
}
