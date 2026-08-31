using Chess.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>판 위의 규칙이 아니라 한 판의 진행 — 기보, 무승부, 무르기.</summary>
    public class ChessGameTests
    {
        private static int Sq(string name)
        {
            return Chess88.Square(name[0] - 'a', name[1] - '1');
        }

        private static void Play(ChessGame game, params string[] moves)
        {
            foreach (string move in moves)
                Assert.True(game.Play(Sq(move.Substring(0, 2)), Sq(move.Substring(2, 2)), Piece.Queen),
                            "illegal: " + move);
        }

        [Fact]
        public void 기보를_사람이_읽는_대로_쓴다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "e2e4", "e7e5", "g1f3", "b8c6", "f1b5");

            Assert.Equal(new[] { "e4", "e5", "Nf3", "Nc6", "Bb5" }, game.Notation);
        }

        [Fact]
        public void 잡기와_체크를_표기에_붙인다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "e2e4", "d7d5", "e4d5", "d8d5", "b1c3");

            Assert.Equal("exd5", game.Notation[2]);
            Assert.Equal("Qxd5", game.Notation[3]);
            Assert.Equal("Nc3", game.Notation[4]);
        }

        [Fact]
        public void 외통은_샵으로_끝난다()
        {
            // 학자의 외통.
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "e2e4", "e7e5", "f1c4", "b8c6", "d1h5", "g8f6", "h5f7");

            Assert.Equal("Qxf7#", game.Notation[6]);
            Assert.Equal(ChessResult.Checkmate, game.Result);
            Assert.Equal(ChessSide.White, game.Winner);
        }

        [Fact]
        public void 같은_국면이_세_번이면_무승부다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8",
                       "g1f3", "g8f6", "f3g1", "f6g8");

            Assert.Equal(ChessResult.Repetition, game.Result);
            Assert.Null(game.Winner);
        }

        [Fact]
        public void 스테일메이트는_무승부다()
        {
            // 흑 킹이 갈 곳이 없지만 체크는 아니다.
            ChessGame game = new ChessGame("7k/5Q2/8/8/8/8/8/6K1 w - - 0 1", ChessSide.White);
            Play(game, "f7g6");

            Assert.Equal(ChessResult.Stalemate, game.Result);
            Assert.Null(game.Winner);
        }

        [Fact]
        public void 기물이_모자라면_무승부다()
        {
            ChessGame game = new ChessGame("4k3/8/8/8/8/8/4B3/4K3 w - - 0 1", ChessSide.White);

            Assert.Equal(ChessResult.InsufficientMaterial, game.Result);
        }

        [Fact]
        public void 무르면_국면이_그대로_돌아온다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            string start = game.Fen;

            Play(game, "e2e4", "e7e5", "g1f3", "b8c6");
            Assert.NotEqual(start, game.Fen);

            game.Undo(4);

            Assert.Equal(start, game.Fen);
            Assert.Empty(game.Notation);
            Assert.Equal(ChessResult.Ongoing, game.Result);
        }

        [Fact]
        public void 승격이_필요한_수를_알아본다()
        {
            ChessGame game = new ChessGame("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", ChessSide.White);

            Assert.True(game.NeedsPromotion(Sq("a7"), Sq("a8")));
            Assert.True(game.Play(Sq("a7"), Sq("a8"), Piece.Knight));
            Assert.Equal("a8=N", game.Notation[0]);
        }

        [Fact]
        public void 세이브에서_되살리면_되풀이도_함께_돌아온다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6");

            ChessGame restored = ChessGame.Restore(game.Fen, ChessSide.White, game.HistoryKeys);

            Assert.Equal(game.Fen, restored.Fen);
            Assert.Equal(game.Result, restored.Result);
        }
    }
}
