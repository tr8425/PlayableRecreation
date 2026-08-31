using System;
using Chess.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// 탐색이 실제로 수를 찾는지. perft 가 수 생성의 정확성을 증명한다면,
    /// 여기는 그 위에 얹은 알파베타가 쓸모 있는지를 본다.
    /// </summary>
    public class ChessSearchTests
    {
        private static ChessMove Think(string fen, int depth)
        {
            ChessBoard board = ChessBoard.FromFen(fen);
            ChessSearch search = new ChessSearch(board, depth, 0.0, new Random(1));

            while (!search.Done) search.Step();

            return search.Chosen;
        }

        [Fact]
        public void 백랭크_외통을_찾는다()
        {
            // 룩이 8열에 들어가면 킹이 자기 폰에 막혀 도망갈 곳이 없다.
            ChessMove move = Think("6k1/5ppp/8/8/8/8/5PPP/R5K1 w - - 0 1", 3);

            Assert.Equal("a1a8", move.ToString());
        }

        [Fact]
        public void 공짜_퀸을_잡는다()
        {
            ChessMove move = Think("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1", 4);

            Assert.Equal("e4d5", move.ToString());
        }

        [Fact]
        public void 체크를_받아치며_푼다()
        {
            // h1 룩이 1열로 체크를 걸고 있다. 도망갈 수도 있지만 비숍으로 잡는 것이 최선이다.
            ChessMove move = Think("4k3/8/8/8/4B3/8/8/4K2r w - - 0 1", 4);

            Assert.Equal("e4h1", move.ToString());
        }

        [Fact]
        public void 깊이를_늘려도_언제나_합법수를_낸다()
        {
            string[] positions =
            {
                ChessBoard.StartFen,
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            };

            foreach (string fen in positions)
            {
                for (int depth = 1; depth <= 4; depth++)
                {
                    ChessBoard board = ChessBoard.FromFen(fen);
                    ChessMove chosen = Think(fen, depth);

                    ChessMove[] legal = new ChessMove[ChessRules.MaxMoves];
                    int count = ChessRules.GenerateLegal(board, legal);

                    bool found = false;
                    for (int i = 0; i < count; i++)
                        if (legal[i].From == chosen.From && legal[i].To == chosen.To) found = true;

                    Assert.True(found, fen + " depth " + depth + " -> " + chosen);
                }
            }
        }

        [Fact]
        public void 깊이는_건너뛰지_않고_한_칸씩만_올라간다()
        {
            // 나누는 단위는 뿌리의 수 하나이므로 한 Step 이 곧 한 깊이는 아니다.
            // 그래도 완성된 깊이는 1, 2, 3, 4 순서로만 올라가야 한다 - 건너뛴 깊이의
            // 결과를 쓰면 정렬되지 않은 목록에서 수를 고르게 된다.
            ChessBoard board = ChessBoard.Start();
            ChessSearch search = new ChessSearch(board, 4, 0.0, new Random(1));

            int seen = 0;
            int steps = 0;

            while (!search.Done && steps < 10000)
            {
                search.Step();
                steps++;

                Assert.True(search.CompletedDepth == seen || search.CompletedDepth == seen + 1,
                            "jumped " + seen + " -> " + search.CompletedDepth);

                seen = search.CompletedDepth;
            }

            Assert.True(search.Done);
            Assert.Equal(4, search.CompletedDepth);
        }

        [Fact]
        public void 무거운_판도_깊이를_끝까지_판다()
        {
            // 예전에는 깊이 하나를 한 프레임이 통째로 떠맡았고, 그 한 프레임이 너무 무거우면
            // 거기서 접었다. 이제는 여러 프레임에 나눠 지므로 접지 않고 끝까지 간다.
            const string fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

            ChessSearch search = new ChessSearch(ChessBoard.FromFen(fen), 5, 0.0, new Random(1));

            int steps = 0;
            while (!search.Done && steps < 100000) { search.Step(); steps++; }

            Assert.Equal(5, search.CompletedDepth);
            Assert.True(steps > 1, "한 프레임에 다 해치웠다면 나누는 의미가 없다");
        }

        [Fact]
        public void 탐색이_판을_원래대로_돌려놓는다()
        {
            const string fen = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

            ChessBoard board = ChessBoard.FromFen(fen);
            ChessSearch search = new ChessSearch(board, 4, 0.0, new Random(1));

            while (!search.Done) search.Step();

            Assert.Equal(fen, board.ToFen());
        }
    }
}
