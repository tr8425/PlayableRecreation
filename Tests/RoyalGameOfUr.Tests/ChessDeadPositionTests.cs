using Chess.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 죽은 판과, 없는 룩.
    ///
    /// "이길 수 없다"와 "메이트가 나올 수 없다"는 다른 말이다. 앞의 것으로 판을 끊으면
    /// 나이트 둘이 마주 본 판이 즉시 무승부가 되는데, 그 판에서도 메이트는 만들어진다.
    /// 여기서 지키는 것은 뒤의 것 - 규칙이 실제로 쓰는 정의다.
    /// </summary>
    public class ChessDeadPositionTests
    {
        private static bool Dead(string fen)
        {
            return ChessRules.InsufficientMaterial(ChessBoard.FromFen(fen));
        }

        [Fact]
        public void 킹만_남으면_죽은_판이다()
        {
            Assert.True(Dead("8/8/4k3/8/8/4K3/8/8 w - - 0 1"));
        }

        [Fact]
        public void 한쪽에_마이너_하나뿐이면_죽은_판이다()
        {
            Assert.True(Dead("8/8/4k3/8/8/8/8/2B1K3 w - - 0 1"));
            Assert.True(Dead("8/8/4k3/8/8/8/8/1N2K3 w - - 0 1"));
        }

        [Fact]
        public void 나이트_둘이_마주_본_판은_죽지_않았다()
        {
            // 강제로 이길 수는 없다. 그래도 메이트가 나오는 수순은 있다.
            Assert.False(Dead("6n1/8/4k3/8/8/8/8/1N2K3 w - - 0 1"));
        }

        [Fact]
        public void 비숍과_나이트가_마주_보면_죽지_않았다()
        {
            Assert.False(Dead("6n1/8/4k3/8/8/8/8/2B1K3 w - - 0 1"));
        }

        [Fact]
        public void 비숍은_칸_색이_같을_때만_죽은_판이다()
        {
            // c1 과 f8 은 둘 다 어두운 칸이다. 서로에게 영원히 닿지 않는다.
            Assert.True(Dead("5b2/8/4k3/8/8/8/8/2B1K3 w - - 0 1"));

            // c1 은 어둡고 e8 은 밝다. 닿는 칸이 겹치므로 아직 판이 살아 있다.
            Assert.False(Dead("4b3/8/4k3/8/8/8/8/2B1K3 w - - 0 1"));
        }

        [Fact]
        public void 나이트_둘을_한쪽이_가져도_죽지_않았다()
        {
            Assert.False(Dead("8/8/4k3/8/8/8/8/1N2K1N1 w - - 0 1"));
        }

        [Fact]
        public void 폰이_하나라도_있으면_죽은_판이_아니다()
        {
            Assert.False(Dead("8/8/4k3/8/4P3/8/8/4K3 w - - 0 1"));
        }

        [Fact]
        public void 룩이_없으면_캐슬링도_없다()
        {
            // 권리 비트만 남고 룩이 사라진 FEN. 정상 플레이로는 나오지 않지만
            // 손상된 세이브에서는 나올 수 있고, 그때 빈 칸을 룩 자리에 복사하면 안 된다.
            ChessBoard board = ChessBoard.FromFen("4k3/8/8/8/8/8/8/4K3 w KQ - 0 1");

            ChessMove[] moves = new ChessMove[256];
            int count = ChessRules.GenerateLegal(board, moves);

            for (int i = 0; i < count; i++)
                Assert.False(moves[i].IsCastle, "castled without a rook");
        }

        [Fact]
        public void 룩이_있으면_캐슬링이_그대로_생성된다()
        {
            ChessBoard board = ChessBoard.FromFen("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");

            ChessMove[] moves = new ChessMove[256];
            int count = ChessRules.GenerateLegal(board, moves);

            int castles = 0;
            for (int i = 0; i < count; i++) if (moves[i].IsCastle) castles++;

            Assert.Equal(2, castles);
        }
    }
}
