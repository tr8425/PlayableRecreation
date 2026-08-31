using System.Collections.Generic;
using Throwing.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>
    /// 던지는 게임의 규칙. 우르와 아무것도 공유하지 않는 두 번째 구현이라,
    /// 여기가 돌아간다는 것은 프레임워크가 턴이나 주사위에 묶여 있지 않다는 뜻이기도 하다.
    /// </summary>
    public class ThrowMatchTests
    {
        private static ThrowMatch Horseshoes(int seed = 1)
        {
            return new ThrowMatch(ThrowRules.Horseshoes(), seed, ThrowSide.Player);
        }

        private static ThrowMatch Hoopstone(int seed = 1)
        {
            return new ThrowMatch(ThrowRules.Hoopstone(), seed, ThrowSide.Player);
        }

        [Fact]
        public void 양쪽이_번갈아_던진다()
        {
            ThrowMatch match = Horseshoes();

            Assert.Equal(ThrowSide.Player, match.Turn);
            match.Throw(0.4f);
            Assert.Equal(ThrowSide.Opponent, match.Turn);
            match.Throw(0.4f);
            Assert.Equal(ThrowSide.Player, match.Turn);
        }

        [Fact]
        public void 한_이닝을_다_던지면_이닝이_넘어간다()
        {
            ThrowMatch match = Horseshoes();
            Assert.Equal(1, match.Inning);

            for (int i = 0; i < 4; i++) match.Throw(0.9f);

            Assert.Equal(2, match.Inning);
            Assert.Equal(ThrowSide.Player, match.Turn);
            Assert.Equal(2, match.Remaining(ThrowSide.Player));
        }

        [Fact]
        public void 후프스톤은_던질_때마다_채점한다()
        {
            ThrowMatch match = Hoopstone();
            ThrowRules rules = match.Rules;

            match.Throw(0f);                            // 통과
            Assert.Equal(rules.RingerPoints, match.ScorePlayer);

            match.Throw(rules.ScoreRadius - 0.01f);     // 상대, 테두리
            Assert.Equal(rules.NearPoints, match.ScoreOpponent);

            match.Throw(2f);                            // 완전히 빗나감
            Assert.Equal(rules.RingerPoints, match.ScorePlayer);
        }

        [Fact]
        public void 편자는_이닝이_끝나야_채점한다()
        {
            ThrowMatch match = Horseshoes();

            match.Throw(0.2f);      // 나, 가깝다
            match.Throw(0.5f);      // 상대
            match.Throw(0.3f);      // 나
            Assert.Equal(0, match.ScorePlayer);

            match.Throw(0.5f);      // 상대 — 여기서 이닝이 닫힌다
            Assert.Equal(2, match.ScorePlayer);
            Assert.Equal(0, match.ScoreOpponent);
        }

        [Fact]
        public void 편자는_한_이닝에_한쪽만_얻는다()
        {
            ThrowMatch match = Horseshoes();

            match.Throw(0.5f);      // 나
            match.Throw(0.2f);      // 상대, 더 가깝다
            match.Throw(0.4f);      // 나
            match.Throw(0.3f);      // 상대

            Assert.Equal(0, match.ScorePlayer);
            Assert.Equal(2, match.ScoreOpponent);
        }

        [Fact]
        public void 링어는_상대와_무관하게_점수가_된다()
        {
            ThrowMatch match = Horseshoes();
            ThrowRules rules = match.Rules;

            match.Throw(0f);        // 나, 링어
            match.Throw(0f);        // 상대, 링어
            match.Throw(2f);        // 나, 완전히 빗나감
            match.Throw(2f);        // 상대

            Assert.Equal(rules.RingerPoints, match.ScorePlayer);
            Assert.Equal(rules.RingerPoints, match.ScoreOpponent);
        }

        [Fact]
        public void 점수권_밖은_아무것도_아니다()
        {
            ThrowMatch match = Horseshoes();
            float far = match.Rules.ScoreRadius + 0.2f;

            match.Throw(far);
            match.Throw(far + 0.5f);
            match.Throw(far + 0.1f);
            match.Throw(far + 0.6f);

            Assert.Equal(0, match.ScorePlayer);
            Assert.Equal(0, match.ScoreOpponent);
        }

        [Fact]
        public void 승부는_이닝_끝에서만_난다()
        {
            ThrowMatch match = Hoopstone();

            bool crossedMidInning = false;
            int guard = 0;

            while (!match.IsOver && guard++ < 200)
            {
                int inningBefore = match.Inning;
                match.Throw(match.Turn == ThrowSide.Player ? 0f : 3f);

                // 목표 점수를 넘고도 이닝이 닫히기 전이면 판은 계속되어야 한다.
                if (match.Inning == inningBefore && match.ScorePlayer >= match.Rules.TargetScore)
                {
                    crossedMidInning = true;
                    Assert.False(match.IsOver);
                }
            }

            Assert.True(crossedMidInning);
            Assert.True(match.IsOver);
            Assert.Equal(ThrowSide.Player, match.Winner);
        }
    }
}
