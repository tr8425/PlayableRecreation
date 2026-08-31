using System;

namespace Ur.Core
{
    /// <summary>보드의 물리 좌표 하나. Row 0=상단(봇 진영), 1=중앙(공유), 2=하단(플레이어 진영).</summary>
    public readonly struct UrCell : IEquatable<UrCell>
    {
        public readonly int Row;
        public readonly int Col;

        public UrCell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(UrCell other) { return Row == other.Row && Col == other.Col; }
        public override bool Equals(object obj) { return obj is UrCell && Equals((UrCell)obj); }
        public override int GetHashCode() { return (Row * 397) ^ Col; }
        public override string ToString() { return "r" + Row + "c" + Col; }
    }

    /// <summary>
    /// Finkel 룰(대영박물관 표준) 기준 보드 형상과 경로 테이블.
    /// 20칸 = 좌측 3x4(12) + 중앙 다리 1x2(2) + 우측 3x2(6).
    /// 각 진영의 경로는 1..14, 15는 골인(보드 밖).
    /// </summary>
    public static class UrBoardLayout
    {
        /// <summary>진영당 말 개수.</summary>
        public const int PieceCount = 7;

        /// <summary>보드 위 경로 칸 수. 유효 경로 인덱스는 1..14.</summary>
        public const int PathLength = 14;

        /// <summary>골인 지점. 정확히 이 값이 되어야 말이 나간다(R8).</summary>
        public const int ScoredIndex = 15;

        /// <summary>중앙 로제트. 공유 구간이지만 잡기 면역(R6).</summary>
        public const int SafeIndex = 8;

        /// <summary>공유 전장(잡기 가능 구간)의 시작 인덱스.</summary>
        public const int SharedFirst = 5;

        /// <summary>공유 전장의 끝 인덱스.</summary>
        public const int SharedLast = 12;

        public const int Columns = 8;
        public const int Rows = 3;
        public const int BotRow = 0;
        public const int MiddleRow = 1;
        public const int PlayerRow = 2;

        /// <summary>로제트 경로 인덱스. 착지 시 추가 턴(R7).</summary>
        public static readonly int[] RosetteIndices = { 4, 8, 14 };

        /// <summary>로제트 물리 좌표 5개. 렌더링용.</summary>
        public static readonly UrCell[] RosetteCells =
        {
            new UrCell(0, 0), new UrCell(2, 0), new UrCell(1, 3), new UrCell(0, 6), new UrCell(2, 6)
        };

        public static bool IsRosette(int pathIndex)
        {
            return pathIndex == 4 || pathIndex == 8 || pathIndex == 14;
        }

        /// <summary>공유 전장 여부. 이 구간에서만 잡기가 발생한다(R5).</summary>
        public static bool IsShared(int pathIndex)
        {
            return pathIndex >= SharedFirst && pathIndex <= SharedLast;
        }

        public static bool IsOnBoard(int pathIndex)
        {
            return pathIndex >= 1 && pathIndex <= PathLength;
        }

        public static int HomeRow(Side side)
        {
            return side == Side.Player ? PlayerRow : BotRow;
        }

        /// <summary>물리 좌표가 실제로 존재하는 칸인지. 중앙 행은 8칸, 위아래 행은 c4/c5가 비어 있다.</summary>
        public static bool CellExists(int row, int col)
        {
            if (col < 0 || col >= Columns || row < 0 || row >= Rows) return false;
            if (row == MiddleRow) return true;
            return col <= 3 || col >= 6;
        }

        /// <summary>경로 인덱스(1..14)를 물리 좌표로 변환한다.</summary>
        public static UrCell CellOf(Side side, int pathIndex)
        {
            if (!IsOnBoard(pathIndex))
                throw new ArgumentOutOfRangeException("pathIndex", pathIndex, "경로 인덱스는 1..14 여야 합니다.");

            int home = HomeRow(side);
            if (pathIndex <= 4) return new UrCell(home, 4 - pathIndex);   // 1->c3, 2->c2, 3->c1, 4->c0
            if (pathIndex <= 12) return new UrCell(MiddleRow, pathIndex - 5); // 5->c0 ... 12->c7
            return new UrCell(home, pathIndex == 13 ? 7 : 6);             // 13->c7, 14->c6
        }

        /// <summary>물리 좌표를 해당 진영의 경로 인덱스로 역변환한다. 그 진영의 경로가 아니면 -1.</summary>
        public static int PathIndexAt(Side side, int row, int col)
        {
            if (!CellExists(row, col)) return -1;

            if (row == MiddleRow) return col + 5;

            if (row != HomeRow(side)) return -1;
            if (col <= 3) return 4 - col;
            if (col == 7) return 13;
            if (col == 6) return 14;
            return -1;
        }
    }
}
