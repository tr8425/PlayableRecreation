using Chess.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// perft. 수 생성이 맞는지 확인하는 것이 아니라 **증명하는** 테스트다 -
    /// 깊이별 잎 노드 수는 체스 프로그래밍 계에서 오래전에 확정된 값이고,
    /// 핀·앙파상·캐슬링·승격 중 하나라도 틀리면 이 숫자가 어긋난다.
    /// </summary>
    public class ChessPerftTests
    {
        private const string Kiwipete =
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        private const string Endgame = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";

        private const string Promotions =
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";

        private const string Tangled =
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

        private static long Perft(string fen, int depth)
        {
            return ChessRules.Perft(ChessBoard.FromFen(fen), depth);
        }

        [Theory]
        [InlineData(1, 20L)]
        [InlineData(2, 400L)]
        [InlineData(3, 8902L)]
        [InlineData(4, 197281L)]
        public void 시작_위치(int depth, long nodes)
        {
            Assert.Equal(nodes, Perft(ChessBoard.StartFen, depth));
        }

        [Theory]
        [InlineData(1, 48L)]
        [InlineData(2, 2039L)]
        [InlineData(3, 97862L)]
        public void 키위피트_캐슬링과_핀이_뒤엉킨_국면(int depth, long nodes)
        {
            Assert.Equal(nodes, Perft(Kiwipete, depth));
        }

        [Theory]
        [InlineData(1, 14L)]
        [InlineData(2, 191L)]
        [InlineData(3, 2812L)]
        [InlineData(4, 43238L)]
        [InlineData(5, 674624L)]
        public void 앙파상_핀이_걸리는_끝내기(int depth, long nodes)
        {
            Assert.Equal(nodes, Perft(Endgame, depth));
        }

        [Theory]
        [InlineData(1, 6L)]
        [InlineData(2, 264L)]
        [InlineData(3, 9467L)]
        public void 승격이_쏟아지는_국면(int depth, long nodes)
        {
            Assert.Equal(nodes, Perft(Promotions, depth));
        }

        [Theory]
        [InlineData(1, 44L)]
        [InlineData(2, 1486L)]
        [InlineData(3, 62379L)]
        public void 좁은_국면(int depth, long nodes)
        {
            Assert.Equal(nodes, Perft(Tangled, depth));
        }

        [Fact]
        public void FEN_은_왕복한다()
        {
            string[] positions = { ChessBoard.StartFen, Kiwipete, Endgame, Promotions, Tangled };

            foreach (string fen in positions)
                Assert.Equal(fen, ChessBoard.FromFen(fen).ToFen());
        }

        [Fact]
        public void 되돌리면_판이_원래대로_돌아온다()
        {
            ChessBoard board = ChessBoard.FromFen(Kiwipete);
            string before = board.ToFen();

            ChessMove[] moves = new ChessMove[ChessRules.MaxMoves];
            int count = ChessRules.GenerateLegal(board, moves);

            for (int i = 0; i < count; i++)
            {
                ChessMove move = moves[i];
                board.Make(ref move);
                board.Unmake(in move);

                Assert.Equal(before, board.ToFen());
            }
        }
    }
}
