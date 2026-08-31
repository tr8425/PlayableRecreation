using System.Collections.Generic;
using RoyalGameOfUr.AI;
using RoyalGameOfUr.Core;
using UnityEngine;
using Verse;

namespace RoyalGameOfUr
{
    /// <summary>
    /// 보드 + 말 + 대기/골인 트레이를 그리고, 플레이어가 클릭한 합법수 인덱스를 돌려준다.
    /// 한 수의 출발지는 유일하므로 "출발 말 클릭 = 그 수를 둔다" 로 원클릭 조작이 성립한다.
    /// </summary>
    public static class UrBoardRenderer
    {
        public const float CellSize = 52f;
        public const float CellGap = 6f;
        public const float PieceInset = 7f;

        public const float TrayHeight = 34f;
        public const float TrayGap = 10f;
        private const float PipSize = 20f;
        private const float PipGap = 3f;
        private const float TrayLabelWidth = 44f;

        public static float BoardWidth
        {
            get { return UrBoardLayout.Columns * (CellSize + CellGap) - CellGap; }
        }

        public static float BoardHeight
        {
            get { return UrBoardLayout.Rows * (CellSize + CellGap) - CellGap; }
        }

        public static float TotalHeight
        {
            get { return BoardHeight + (TrayHeight + TrayGap) * 2f; }
        }

        /// <summary>클릭된 합법수 인덱스. 없으면 -1.</summary>
        public static int Draw(Rect area, UrMatch match, bool interactive)
        {
            float x0 = area.x + (area.width - BoardWidth) / 2f;
            float y0 = area.y + Mathf.Max(0f, (area.height - TotalHeight) / 2f);

            Rect botTray = new Rect(x0, y0, BoardWidth, TrayHeight);
            Rect grid = new Rect(x0, botTray.yMax + TrayGap, BoardWidth, BoardHeight);
            Rect playerTray = new Rect(x0, grid.yMax + TrayGap, BoardWidth, TrayHeight);

            DrawTray(botTray, match, Side.Bot);
            DrawGrid(grid, match);
            DrawTray(playerTray, match, Side.Player);

            if (UrSettings.ShowCellTooltips) DrawCellTooltips(grid);

            return interactive ? DrawInteractions(grid, playerTray, match) : -1;
        }

        // ---------- 보드 ----------

        private static void DrawGrid(Rect grid, UrMatch match)
        {
            DrawCells(grid, CellSize, null);

            DrawPiecesOnBoard(grid, match, Side.Bot);
            DrawPiecesOnBoard(grid, match, Side.Player);
        }

        private static void DrawCells(Rect grid, float cellSize, Dictionary<UrCell, Color> tint)
        {
            for (int row = 0; row < UrBoardLayout.Rows; row++)
            {
                for (int col = 0; col < UrBoardLayout.Columns; col++)
                {
                    if (!UrBoardLayout.CellExists(row, col)) continue;

                    UrCell coord = new UrCell(row, col);
                    Rect cell = CellRect(grid, coord, cellSize);
                    bool rosette = IsRosetteCell(row, col);

                    Color background = rosette ? UrTheme.CellRosette : UrTheme.Cell;
                    if (tint != null)
                    {
                        Color custom;
                        if (tint.TryGetValue(coord, out custom)) background = custom;
                    }

                    Widgets.DrawBoxSolid(cell, background);

                    GUI.color = UrTheme.CellBorder;
                    Widgets.DrawBox(cell, 1);
                    GUI.color = Color.white;

                    if (rosette)
                    {
                        GUI.color = UrTheme.RosetteMark;
                        GUI.DrawTexture(cell.ContractedBy(cellSize * 0.1f), UrTextures.Rosette);
                        GUI.color = Color.white;
                    }
                }
            }
        }

        private static void DrawPiecesOnBoard(Rect grid, UrMatch match, Side side)
        {
            for (int i = 1; i <= UrBoardLayout.PathLength; i++)
            {
                if (!match.State.IsOccupied(side, i)) continue;
                Rect cell = CellRect(grid, UrBoardLayout.CellOf(side, i), CellSize).ContractedBy(PieceInset);
                DrawPiece(cell, side);
            }
        }

        public static void DrawPiece(Rect rect, Side side)
        {
            GUI.color = side == Side.Player ? UrTheme.PlayerPiece : UrTheme.BotPiece;
            GUI.DrawTexture(rect, side == Side.Player ? UrTextures.Disc : UrTextures.Ring);
            GUI.color = Color.white;
        }

        // ---------- 트레이 ----------

        private static void DrawTray(Rect tray, UrMatch match, Side side)
        {
            int waiting = match.State.Waiting(side);
            int scored = match.State.Scored(side);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UrTheme.Dim;
            Widgets.Label(new Rect(tray.x, tray.y, TrayLabelWidth, tray.height), "RGU.Tray.Waiting".Translate());
            GUI.color = Color.white;

            for (int i = 0; i < UrBoardLayout.PieceCount; i++)
                DrawSlot(WaitingSlotRect(tray, i), side, i < waiting);

            float scoredX = ScoredOriginX(tray);
            GUI.color = UrTheme.Dim;
            Widgets.Label(new Rect(scoredX, tray.y, TrayLabelWidth, tray.height), "RGU.Tray.Scored".Translate());
            GUI.color = Color.white;

            for (int i = 0; i < UrBoardLayout.PieceCount; i++)
                DrawSlot(ScoredSlotRect(tray, i), side, i < scored);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawSlot(Rect rect, Side side, bool filled)
        {
            if (filled)
            {
                DrawPiece(rect, side);
            }
            else
            {
                GUI.color = UrTheme.EmptySlot;
                GUI.DrawTexture(rect, UrTextures.Ring);
                GUI.color = Color.white;
            }
        }

        // ---------- 상호작용 ----------

        private static int DrawInteractions(Rect grid, Rect playerTray, UrMatch match)
        {
            if (match.Phase != UrPhase.AwaitingMove) return -1;

            bool alwaysHighlight = UrSettings.HighlightLegalMoves;
            int clicked = -1;

            for (int k = 0; k < match.LegalCount; k++)
            {
                UrMove move = match.LegalMoves[k];

                Rect source = move.IsEntry
                    ? WaitingGroupRect(playerTray, match.State.Waiting(Side.Player))
                    : CellRect(grid, UrBoardLayout.CellOf(Side.Player, move.From), CellSize);

                bool hover = Mouse.IsOver(source);

                if (hover) DrawDestination(grid, playerTray, match, move);

                if (alwaysHighlight || hover)
                {
                    GUI.color = hover ? UrTheme.LegalHover : UrTheme.LegalSource;
                    Widgets.DrawBox(source, 2);
                    GUI.color = Color.white;
                }

                if (Widgets.ButtonInvisible(source)) clicked = k;
            }

            return clicked;
        }

        private static void DrawDestination(Rect grid, Rect playerTray, UrMatch match, UrMove move)
        {
            if (move.IsBearOff)
            {
                Rect goal = ScoredSlotRect(playerTray, match.State.Scored(Side.Player));
                GUI.color = UrTheme.BearOff;
                Widgets.DrawBox(goal.ExpandedBy(2f), 2);
                GUI.color = Color.white;
                return;
            }

            Rect target = CellRect(grid, UrBoardLayout.CellOf(Side.Player, move.To), CellSize);
            Widgets.DrawBoxSolid(target, move.IsCapture ? UrTheme.CaptureTarget : UrTheme.MoveTarget);
            GUI.color = move.IsCapture ? UrTheme.CaptureBorder : UrTheme.LegalHover;
            Widgets.DrawBox(target, 2);
            GUI.color = Color.white;

            if (UrSettings.ShowRiskWarning) DrawRisk(target, match, move);
        }

        /// <summary>이 수를 두면 다음 턴에 잡힐 확률. 상급자용 보조. (DESIGN.md §6.3)</summary>
        private static void DrawRisk(Rect target, UrMatch match, UrMove move)
        {
            if (!UrBoardLayout.IsShared(move.To) || move.To == UrBoardLayout.SafeIndex) return;

            bool extraTurn;
            UrGameState next = UrRules.Apply(in match.State, move, out extraTurn);
            float risk = UrEvaluator.CaptureChance(in next, Side.Bot, move.To);
            if (risk <= 0f) return;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = UrTheme.CaptureBorder;
            Widgets.Label(target, (risk * 100f).ToString("0") + "%");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawCellTooltips(Rect grid)
        {
            for (int row = 0; row < UrBoardLayout.Rows; row++)
            {
                for (int col = 0; col < UrBoardLayout.Columns; col++)
                {
                    if (!UrBoardLayout.CellExists(row, col)) continue;

                    Rect cell = CellRect(grid, new UrCell(row, col), CellSize);
                    if (!Mouse.IsOver(cell)) continue;

                    TooltipHandler.TipRegion(cell, CellTooltip(row, col));
                }
            }
        }

        private static string CellTooltip(int row, int col)
        {
            int playerIndex = UrBoardLayout.PathIndexAt(Side.Player, row, col);
            int botIndex = UrBoardLayout.PathIndexAt(Side.Bot, row, col);
            int index = playerIndex > 0 ? playerIndex : botIndex;

            List<string> parts = new List<string>();

            if (playerIndex > 0) parts.Add("RGU.Tip.MyPath".Translate(playerIndex));
            else if (botIndex > 0) parts.Add("RGU.Tip.BotPath".Translate(botIndex));

            if (UrBoardLayout.IsShared(index)) parts.Add("RGU.Tip.Shared".Translate());
            else parts.Add("RGU.Tip.Home".Translate());

            if (index == UrBoardLayout.SafeIndex) parts.Add("RGU.Tip.Safe".Translate());
            if (UrBoardLayout.IsRosette(index)) parts.Add("RGU.Tip.Rosette".Translate());

            return string.Join("\n", parts.ToArray());
        }

        // ---------- 튜토리얼용 도식 ----------

        /// <summary>
        /// 조작 없는 정적 보드 그림. 튜토리얼이 칸을 물들이고 말과 경로 번호를 얹어 쓴다.
        /// </summary>
        public static void DrawDiagram(Rect area, float cellSize,
                                       Dictionary<UrCell, Color> tint,
                                       Dictionary<UrCell, Side> pieces,
                                       Side? numberFor)
        {
            float gap = cellSize * (CellGap / CellSize);
            float width = UrBoardLayout.Columns * (cellSize + gap) - gap;
            float height = UrBoardLayout.Rows * (cellSize + gap) - gap;

            Rect grid = new Rect(
                area.x + (area.width - width) / 2f,
                area.y + (area.height - height) / 2f,
                width, height);

            DrawCells(grid, cellSize, tint);

            if (pieces != null)
            {
                foreach (KeyValuePair<UrCell, Side> entry in pieces)
                {
                    Rect cell = CellRect(grid, entry.Key, cellSize).ContractedBy(cellSize * 0.16f);
                    DrawPiece(cell, entry.Value);
                }
            }

            if (numberFor.HasValue)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UrTheme.Dim;

                for (int i = 1; i <= UrBoardLayout.PathLength; i++)
                {
                    Rect cell = CellRect(grid, UrBoardLayout.CellOf(numberFor.Value, i), cellSize);
                    Widgets.Label(cell, i.ToString());
                }

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        // ---------- 좌표 ----------

        private static Rect CellRect(Rect grid, UrCell cell, float cellSize)
        {
            float gap = cellSize * (CellGap / CellSize);
            return new Rect(
                grid.x + cell.Col * (cellSize + gap),
                grid.y + cell.Row * (cellSize + gap),
                cellSize, cellSize);
        }

        private static Rect WaitingSlotRect(Rect tray, int index)
        {
            float cy = tray.y + (tray.height - PipSize) / 2f;
            return new Rect(tray.x + TrayLabelWidth + index * (PipSize + PipGap), cy, PipSize, PipSize);
        }

        private static Rect WaitingGroupRect(Rect tray, int waitingCount)
        {
            float cy = tray.y + (tray.height - PipSize) / 2f;
            float width = Mathf.Max(waitingCount, 1) * (PipSize + PipGap) - PipGap;
            return new Rect(tray.x + TrayLabelWidth, cy, width, PipSize);
        }

        private static float ScoredOriginX(Rect tray)
        {
            float groupWidth = TrayLabelWidth + UrBoardLayout.PieceCount * (PipSize + PipGap) - PipGap;
            return tray.xMax - groupWidth;
        }

        private static Rect ScoredSlotRect(Rect tray, int index)
        {
            float cy = tray.y + (tray.height - PipSize) / 2f;
            float x = ScoredOriginX(tray) + TrayLabelWidth + index * (PipSize + PipGap);
            return new Rect(x, cy, PipSize, PipSize);
        }

        private static bool IsRosetteCell(int row, int col)
        {
            foreach (UrCell rosette in UrBoardLayout.RosetteCells)
                if (rosette.Row == row && rosette.Col == col) return true;
            return false;
        }
    }
}
