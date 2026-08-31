using System.Linq;
using RoyalGameOfUr.Core;
using Xunit;
using static RoyalGameOfUr.Tests.TestHelpers;

namespace RoyalGameOfUr.Tests
{
    /// <summary>DESIGN.md §4.4 의 R1~R11 을 1:1 로 검증한다.</summary>
    public class UrRulesTests
    {
        [Fact]
        public void R1_눈이_0이면_합법수가_없다()
        {
            var s = UrGameState.NewGame(Side.Player);
            Assert.Empty(Moves(in s, 0));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void R2_대기_말은_주사위_눈_위치로_투입된다(int roll)
        {
            var s = UrGameState.NewGame(Side.Player);
            var moves = Moves(in s, roll);

            Assert.Single(moves);
            Assert.True(moves[0].IsEntry);
            Assert.Equal(roll, moves[0].To);

            var next = UrRules.Apply(in s, moves[0], out _);
            Assert.Equal(UrBoardLayout.PieceCount - 1, next.WaitingPlayer);
            Assert.True(next.IsOccupied(Side.Player, roll));
            Assert.True(next.IsConsistent());
        }

        [Fact]
        public void R3_보드_위_말은_눈만큼_전진한다()
        {
            var s = State(Side.Player, new[] { 5 }, new int[0]);
            Assert.True(HasMove(in s, 3, from: 5, to: 8));
        }

        [Fact]
        public void R4_자기_말_위에는_올라갈_수_없다()
        {
            // 5번과 7번에 내 말. 눈 2 로 5->7 은 불법이어야 한다.
            var s = State(Side.Player, new[] { 5, 7 }, new int[0]);
            Assert.False(HasMove(in s, 2, from: 5, to: 7));
            Assert.True(HasMove(in s, 2, from: 7, to: 9));
        }

        [Fact]
        public void R5_공유_전장에서_상대_말을_잡으면_대기열로_돌아간다()
        {
            var s = State(Side.Player, new[] { 6 }, new[] { 7 });
            var moves = Moves(in s, 1);
            var capture = moves.Single(m => m.From == 6 && m.To == 7);
            Assert.True(capture.IsCapture);

            var next = UrRules.Apply(in s, capture, out _);
            Assert.False(next.IsOccupied(Side.Bot, 7));
            Assert.True(next.IsOccupied(Side.Player, 7));
            Assert.Equal(UrBoardLayout.PieceCount - 1 + 1, next.WaitingBot); // 6 -> 7 로 복귀
            Assert.True(next.IsConsistent());
        }

        [Fact]
        public void R5_자기_진영_칸은_같은_인덱스라도_상대와_무관하다()
        {
            // 봇의 3번은 r0c1, 플레이어의 3번은 r2c1 로 물리적으로 다른 칸이다.
            var s = State(Side.Player, new[] { 1 }, new[] { 3 });
            var move = Moves(in s, 2).Single(m => m.From == 1 && m.To == 3);

            Assert.False(move.IsCapture);
            var next = UrRules.Apply(in s, move, out _);
            Assert.True(next.IsOccupied(Side.Bot, 3));  // 봇 말은 그대로 있다
            Assert.True(next.IsConsistent());
        }

        [Fact]
        public void R6_중앙_로제트에_상대_말이_있으면_그_칸으로_갈_수_없다()
        {
            // 말 1개만 보드에 있고 나머지는 골인 처리해 투입 수를 배제한다.
            var s = State(Side.Player, new[] { 5 }, new[] { 8 }, scoredPlayer: 6);
            Assert.Equal(0, s.WaitingPlayer);
            Assert.Empty(Moves(in s, 3));   // 5 + 3 = 8 은 안전칸이라 불법
        }

        [Fact]
        public void R6_중앙_로제트가_비어_있으면_진입_가능하다()
        {
            var s = State(Side.Player, new[] { 5 }, new int[0], scoredPlayer: 6);
            var move = Moves(in s, 3).Single();
            Assert.Equal(8, move.To);
            Assert.False(move.IsCapture);
            Assert.True(move.GrantsExtraTurn);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(14)]
        public void R7_로제트_착지는_추가_턴을_준다(int rosette)
        {
            var s = State(Side.Player, new[] { rosette - 1 }, new int[0], scoredPlayer: 6);
            var move = Moves(in s, 1).Single();

            Assert.Equal(rosette, move.To);
            Assert.True(move.GrantsExtraTurn);

            var next = UrRules.Apply(in s, move, out bool extraTurn);
            Assert.True(extraTurn);
            Assert.Equal(Side.Player, next.Turn);   // 턴이 넘어가지 않는다
        }

        [Fact]
        public void R7_로제트가_아니면_턴이_넘어간다()
        {
            var s = State(Side.Player, new[] { 5 }, new int[0], scoredPlayer: 6);
            var move = Moves(in s, 1).Single();     // 5 -> 6

            Assert.False(move.GrantsExtraTurn);
            var next = UrRules.Apply(in s, move, out bool extraTurn);
            Assert.False(extraTurn);
            Assert.Equal(Side.Bot, next.Turn);
        }

        [Theory]
        [InlineData(14, 1, true)]    // 14 + 1 = 15 정확히 골인
        [InlineData(14, 2, false)]   // 16 은 초과
        [InlineData(13, 2, true)]    // 13 + 2 = 15
        [InlineData(13, 3, false)]
        [InlineData(11, 4, true)]    // 11 + 4 = 15
        public void R8_골인은_정확히_15여야_한다(int from, int roll, bool legal)
        {
            var s = State(Side.Player, new[] { from }, new int[0], scoredPlayer: 6);
            var moves = Moves(in s, roll);

            if (legal)
            {
                var move = moves.Single();
                Assert.True(move.IsBearOff);
                var next = UrRules.Apply(in s, move, out _);
                Assert.Equal(7, next.ScoredPlayer);
                Assert.True(next.IsConsistent());
            }
            else
            {
                Assert.DoesNotContain(moves, m => m.IsBearOff);
            }
        }

        [Fact]
        public void R9_모든_수가_막히면_합법수가_0이다()
        {
            // 말 7개가 1..7 을 채우고 8번(안전칸)은 봇이 점유 -> 눈 1 로 갈 곳이 전혀 없다.
            var s = State(Side.Player, new[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { 8 });
            Assert.Equal(0, s.WaitingPlayer);
            Assert.Empty(Moves(in s, 1));
        }

        [Fact]
        public void R9_패스는_턴만_넘긴다()
        {
            var s = UrGameState.NewGame(Side.Player);
            var next = UrRules.PassTurn(in s);

            Assert.Equal(Side.Bot, next.Turn);
            Assert.Equal(s.OccPlayer, next.OccPlayer);
            Assert.Equal(s.WaitingPlayer, next.WaitingPlayer);
        }

        [Fact]
        public void R10_7개를_모두_골인시키면_승리한다()
        {
            var s = State(Side.Player, new[] { 14 }, new int[0], scoredPlayer: 6);
            Assert.Null(UrRules.Winner(in s));

            var next = UrRules.Apply(in s, Moves(in s, 1).Single(), out _);
            Assert.Equal(Side.Player, UrRules.Winner(in next));
            Assert.True(UrRules.IsGameOver(in next));
        }

        [Fact]
        public void R11_새_판은_지정한_선공과_말_7개로_시작한다()
        {
            var s = UrGameState.NewGame(Side.Player);

            Assert.Equal(Side.Player, s.Turn);
            Assert.Equal(7, s.WaitingPlayer);
            Assert.Equal(7, s.WaitingBot);
            Assert.Equal(0, s.OnBoardCount(Side.Player));
            Assert.Equal(0, s.ScoredPlayer);
            Assert.True(s.IsConsistent());

            Assert.Equal(Side.Bot, UrGameState.NewGame(Side.Bot).Turn);
        }

        [Fact]
        public void 투입_눈_4는_자기_로제트에_바로_안착해_추가_턴을_준다()
        {
            var s = UrGameState.NewGame(Side.Player);
            var move = Moves(in s, 4).Single();

            Assert.True(move.IsEntry);
            Assert.Equal(4, move.To);
            Assert.True(move.GrantsExtraTurn);
        }

        [Fact]
        public void 합법수는_상한_이내로만_생성된다()
        {
            // 서로 다른 칸 7개 + 대기 0 -> 최대 7수
            var s = State(Side.Player, new[] { 1, 3, 5, 7, 9, 11, 13 }, new int[0]);
            var moves = Moves(in s, 2);
            Assert.True(moves.Length <= UrRules.MaxMoves);
        }

        [Fact]
        public void Apply는_원본_상태를_바꾸지_않는다()
        {
            var s = UrGameState.NewGame(Side.Player);
            var before = s;

            UrRules.Apply(in s, Moves(in s, 2).Single(), out _);

            Assert.Equal(before.OccPlayer, s.OccPlayer);
            Assert.Equal(before.WaitingPlayer, s.WaitingPlayer);
            Assert.Equal(before.Turn, s.Turn);
        }
    }
}
