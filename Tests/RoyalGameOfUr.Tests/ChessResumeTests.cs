using System.Collections.Generic;
using Chess.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 이어 두기.
    ///
    /// 한 판은 지금의 배치가 아니라 지나온 수의 목록이다. 위치만 되살리면 기보도,
    /// 몇 수째인지도, 무를 수 있는지도 함께 사라진다 - 그래서 처음부터 다시 둔다.
    /// </summary>
    public class ChessResumeTests
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
        public void 다시_두면_판도_기보도_수순도_그대로_돌아온다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5c6", "d7c6");

            ChessGame back = ChessGame.Replay(ChessSide.White, new List<ChessMove>(game.Played));

            Assert.NotNull(back);
            Assert.Equal(game.Fen, back.Fen);
            Assert.Equal(game.Ply, back.Ply);
            Assert.Equal(game.Result, back.Result);
            Assert.Equal(game.LegalCount, back.LegalCount);

            Assert.Equal(game.Notation.Count, back.Notation.Count);
            for (int i = 0; i < game.Notation.Count; i++)
                Assert.Equal(game.Notation[i], back.Notation[i]);
        }

        [Fact]
        public void 다시_둔_판도_무를_수_있다()
        {
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "e2e4", "e7e5", "g1f3", "b8c6");

            ChessGame back = ChessGame.Replay(ChessSide.White, new List<ChessMove>(game.Played));
            Assert.NotNull(back);

            back.Undo(2);
            game.Undo(2);

            Assert.Equal(game.Fen, back.Fen);
            Assert.Equal(game.Ply, back.Ply);
        }

        [Fact]
        public void 되풀이_판정도_같이_돌아온다()
        {
            // 나이트를 제자리로 돌리기를 두 번. 시작 자리가 세 번째로 나오면 되풀이 무승부다.
            ChessGame game = new ChessGame(ChessSide.White);
            Play(game, "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1", "f6g8");

            ChessGame back = ChessGame.Replay(ChessSide.White, new List<ChessMove>(game.Played));

            Assert.NotNull(back);
            Assert.Equal(ChessResult.Repetition, game.Result);
            Assert.Equal(game.Result, back.Result);
        }

        [Fact]
        public void 상한_수순은_받아들이지_않는다()
        {
            // 못 두는 수가 하나라도 있으면 null 이다. 반쯤 재생된 판은 틀린 판이다.
            List<ChessMove> broken = new List<ChessMove>
            {
                new ChessMove { From = Sq("e2"), To = Sq("e4") },
                new ChessMove { From = Sq("a1"), To = Sq("a8") },
            };

            Assert.Null(ChessGame.Replay(ChessSide.White, broken));
        }

        [Fact]
        public void 수순이_없으면_시작_위치를_준다()
        {
            ChessGame fresh = ChessGame.Replay(ChessSide.Black, null);

            Assert.NotNull(fresh);
            Assert.Equal(0, fresh.Ply);
            Assert.Equal(ChessBoard.StartFen, fresh.Fen);
        }
    }
}
