using System.Collections.Generic;
using RoyalGameOfUr.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    public class UrBoardLayoutTests
    {
        [Theory]
        [InlineData(1, 2, 3)]   // 시작칸
        [InlineData(2, 2, 2)]
        [InlineData(3, 2, 1)]
        [InlineData(4, 2, 0)]   // 자기 로제트
        [InlineData(5, 1, 0)]   // 공유 전장 진입
        [InlineData(8, 1, 3)]   // 중앙 로제트
        [InlineData(12, 1, 7)]  // 공유 전장 마지막
        [InlineData(13, 2, 7)]  // 자기 진영 복귀
        [InlineData(14, 2, 6)]  // 마지막 로제트
        public void 플레이어_경로가_명세와_일치한다(int pathIndex, int row, int col)
        {
            Assert.Equal(new UrCell(row, col), UrBoardLayout.CellOf(Side.Player, pathIndex));
        }

        [Theory]
        [InlineData(1, 0, 3)]
        [InlineData(4, 0, 0)]
        [InlineData(13, 0, 7)]
        [InlineData(14, 0, 6)]
        public void 봇_경로는_상단_행으로_미러된다(int pathIndex, int row, int col)
        {
            Assert.Equal(new UrCell(row, col), UrBoardLayout.CellOf(Side.Bot, pathIndex));
        }

        [Fact]
        public void 공유_구간은_두_진영이_같은_물리_칸을_쓴다()
        {
            for (int i = UrBoardLayout.SharedFirst; i <= UrBoardLayout.SharedLast; i++)
            {
                Assert.Equal(UrBoardLayout.CellOf(Side.Player, i), UrBoardLayout.CellOf(Side.Bot, i));
                Assert.True(UrBoardLayout.IsShared(i));
            }
        }

        [Fact]
        public void 자기_진영_칸은_상대와_절대_겹치지_않는다()
        {
            var ownIndices = new[] { 1, 2, 3, 4, 13, 14 };
            foreach (int i in ownIndices)
            {
                Assert.False(UrBoardLayout.IsShared(i));
                var mine = UrBoardLayout.CellOf(Side.Player, i);
                for (int j = 1; j <= UrBoardLayout.PathLength; j++)
                    Assert.NotEqual(mine, UrBoardLayout.CellOf(Side.Bot, j));
            }
        }

        [Fact]
        public void 경로_인덱스와_좌표는_양방향_변환된다()
        {
            foreach (Side side in new[] { Side.Player, Side.Bot })
                for (int i = 1; i <= UrBoardLayout.PathLength; i++)
                {
                    var cell = UrBoardLayout.CellOf(side, i);
                    Assert.Equal(i, UrBoardLayout.PathIndexAt(side, cell.Row, cell.Col));
                }
        }

        [Fact]
        public void 보드는_정확히_20칸이다()
        {
            int count = 0;
            for (int r = 0; r < UrBoardLayout.Rows; r++)
                for (int c = 0; c < UrBoardLayout.Columns; c++)
                    if (UrBoardLayout.CellExists(r, c)) count++;

            Assert.Equal(20, count);
        }

        [Fact]
        public void 위아래_행의_c4_c5는_비어있다()
        {
            Assert.False(UrBoardLayout.CellExists(0, 4));
            Assert.False(UrBoardLayout.CellExists(0, 5));
            Assert.False(UrBoardLayout.CellExists(2, 4));
            Assert.False(UrBoardLayout.CellExists(2, 5));
            Assert.True(UrBoardLayout.CellExists(1, 4));
            Assert.True(UrBoardLayout.CellExists(1, 5));
        }

        [Fact]
        public void 로제트는_경로상_4_8_14이며_물리적으로는_5칸이다()
        {
            for (int i = 1; i <= UrBoardLayout.PathLength; i++)
                Assert.Equal(i == 4 || i == 8 || i == 14, UrBoardLayout.IsRosette(i));

            var cells = new HashSet<UrCell>();
            foreach (Side side in new[] { Side.Player, Side.Bot })
                foreach (int i in UrBoardLayout.RosetteIndices)
                    cells.Add(UrBoardLayout.CellOf(side, i));

            Assert.Equal(5, cells.Count);
            foreach (var c in UrBoardLayout.RosetteCells) Assert.Contains(c, cells);
        }

        [Fact]
        public void 중앙_로제트는_공유_구간에_있고_안전칸이다()
        {
            Assert.True(UrBoardLayout.IsShared(UrBoardLayout.SafeIndex));
            Assert.True(UrBoardLayout.IsRosette(UrBoardLayout.SafeIndex));
            Assert.Equal(new UrCell(1, 3), UrBoardLayout.CellOf(Side.Player, UrBoardLayout.SafeIndex));
        }
    }
}
