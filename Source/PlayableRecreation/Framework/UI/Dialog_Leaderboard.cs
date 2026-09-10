using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 개인 전적. 상대가 언제나 AI 이므로 의미 있는 지표는 식민자 랭킹이 아니라
    /// 플레이어 본인의 난이도별 성적이다.
    ///
    /// 나의 통산은 세이브와 무관한 파일에, 이 식민지는 세이브 안에 쌓인다.
    /// 온라인 랭킹·계정 연동은 없다.
    /// </summary>
    public class Dialog_Leaderboard : Window
    {
        private const float TabHeight = 32f;

        /// <summary>
        /// 탭 하나가 이보다 좁아지면 줄을 늘린다. 확장까지 켜면 승부 게임이 열이라,
        /// 한 줄에 밀어 넣으면 탭이 56픽셀이 되고 "Arcade machine" 같은 이름이 접히면서
        /// 윗부분이 잘려 나간다. 이름을 줄이는 대신 줄을 늘린다.
        /// </summary>
        private const float TabMinWidth = 92f;

        private const float TabGap = 6f;
        private const float HeaderHeight = 26f;
        private const float RowHeight = 28f;
        private const float FooterHeight = 40f;
        private const float Pad = 10f;

        private static readonly float[] Columns = { 0.24f, 0.22f, 0.16f, 0.19f, 0.19f };

        private MiniGameDef game;
        private bool showColony;
        private Vector2 summaryScroll;

        public override Vector2 InitialSize
        {
            get
            {
                const float width = 620f;

                float height = ContentHeight(width - Margin * 2f) + Margin * 2f;

                // 화면보다 큰 창은 아무것도 못 읽게 만든다. 거기서 모자라는 몫은 요약이 굴려서 받는다.
                return new Vector2(width, Mathf.Clamp(height, 520f, Verse.UI.screenHeight - 80f));
            }
        }

        public Dialog_Leaderboard(MiniGameDef game)
        {
            this.game = game ?? FirstEnabled;

            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            // 읽는 창이다. 규칙을 읽거나 전적을 보는 동안 콜로니가 굴러가서는 안 된다.
            forcePause = Current.ProgramState == ProgramState.Playing;
        }

        private static List<MiniGameDef> Enabled
        {
            get
            {
                List<MiniGameDef> games = new List<MiniGameDef>();

                foreach (MiniGameDef def in DefDatabase<MiniGameDef>.AllDefsListForReading)
                    if (def.hasMatch && PRMod.Settings.IsEnabled(def)) games.Add(def);

                return games;
            }
        }

        private static MiniGameDef FirstEnabled
        {
            get
            {
                List<MiniGameDef> games = Enabled;
                return games.Count > 0 ? games[0] : null;
            }
        }

        private GameRecord Shown
        {
            get
            {
                if (!showColony) return RecordStore.For(game);

                GameComponent_Recreation component = GameComponent_Recreation.Current;
                return component != null ? component.ColonyRecord(game) : RecordStore.For(game);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (game == null)
            {
                Widgets.Label(inRect, "PR.Records.NoGames".Translate());
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "PR.Records.Title".Translate());
            Text.Font = GameFont.Small;

            float y = inRect.y + 38f;

            List<MiniGameDef> games = Enabled;
            if (games.Count > 1)
            {
                float tabs = GameTabsHeight(inRect.width, games.Count);
                DrawGameTabs(new Rect(inRect.x, y, inRect.width, tabs), games);
                y += tabs + TabGap;
            }

            DrawScopeTabs(new Rect(inRect.x, y, inRect.width, TabHeight));
            y += TabHeight + Pad;

            DrawTableHeader(new Rect(inRect.x, y, inRect.width, HeaderHeight));
            y += HeaderHeight;

            GameRecord record = Shown;

            for (int tier = 0; tier < game.difficultyCount; tier++)
            {
                DrawTierRow(new Rect(inRect.x, y, inRect.width, RowHeight), record, tier);
                y += RowHeight;
            }

            y += Pad;
            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += Pad;

            DrawSummary(new Rect(inRect.x, y, inRect.width, Mathf.Max(0f, inRect.yMax - FooterHeight - y)), record);
            DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight + 6f, inRect.width, FooterHeight - 6f));
        }

        /// <summary>
        /// 요약은 작은 글씨로 쓴다. 언어가 작은 글씨를 못 쓰거나 플레이어가 껐으면
        /// 바닐라가 조용히 보통 글씨로 돌린다 — 자리를 잴 때도 같은 것을 봐야 한다.
        /// </summary>
        private static GameFont SummaryFont
        {
            get { return Text.TinyFontSupported ? GameFont.Tiny : GameFont.Small; }
        }

        /// <summary>
        /// 창을 열기 전에 내용이 얼마나 되는지 미리 잰다. 확장까지 켜면 게임 탭이 두 줄이 되고,
        /// 그만큼 아래가 밀려서 마지막 요약 줄이 창 밖으로 나갔다 (R2-01).
        /// 탭을 바꿔도 안 잘리도록 켜져 있는 게임 중 가장 긴 것에 맞춘다.
        /// </summary>
        private float ContentHeight(float width)
        {
            List<MiniGameDef> games = Enabled;

            float height = 38f;                                              // 제목
            if (games.Count > 1) height += GameTabsHeight(width, games.Count) + TabGap;
            height += TabHeight + Pad;                                       // 나의 통산 · 이 식민지
            height += HeaderHeight;
            height += Pad + Pad;                                             // 표와 요약 사이의 줄
            height += FooterHeight;

            // 켜져 있지 않은 게임에서 열렸어도 그 게임은 보여 준다.
            if (game != null && !games.Contains(game)) games.Add(game);

            float line = Text.LineHeightOf(SummaryFont) + 2f;                // Listing 의 줄 간격
            float body = 0f;

            foreach (MiniGameDef def in games)
            {
                int tallies = def.tallyKeys != null
                    ? Mathf.Min(def.tallyKeys.Count, GameRecord.TallyCount)
                    : 0;

                // 난이도 줄 + 통산·집계·연승·시간
                body = Mathf.Max(body, def.difficultyCount * RowHeight + (3 + tallies) * line);
            }

            // 딱 맞게 자르면 번역 한 줄이 접히는 순간 다시 잘린다. 한 뼘 남겨 둔다.
            return height + body + Pad;
        }

        /// <summary>한 줄에 몇 개까지 놓을 것인가. 한 개 밑으로는 안 내려간다.</summary>
        private static int TabsPerRow(float width, int count)
        {
            int fits = Mathf.FloorToInt((width + TabGap) / (TabMinWidth + TabGap));
            return Mathf.Clamp(fits, 1, count);
        }

        /// <summary>탭이 차지할 높이. 그린 뒤에 알면 늦으므로 자리를 잡을 때 먼저 묻는다.</summary>
        private static float GameTabsHeight(float width, int count)
        {
            int perRow = TabsPerRow(width, count);
            int rows = Mathf.CeilToInt(count / (float)perRow);

            return rows * TabHeight + (rows - 1) * TabGap;
        }

        private void DrawGameTabs(Rect area, List<MiniGameDef> games)
        {
            int perRow = TabsPerRow(area.width, games.Count);
            float width = (area.width - (perRow - 1) * TabGap) / perRow;

            for (int i = 0; i < games.Count; i++)
            {
                int col = i % perRow;
                int row = i / perRow;

                Rect tab = new Rect(
                    area.x + col * (width + TabGap),
                    area.y + row * (TabHeight + TabGap),
                    width, TabHeight);

                if (TabButton(tab, games[i].LabelCap, games[i] == game)) game = games[i];
            }
        }

        private void DrawScopeTabs(Rect row)
        {
            float half = (row.width - 8f) / 2f;

            if (TabButton(new Rect(row.x, row.y, half, row.height), "PR.Records.Tab.Player".Translate(), !showColony))
                showColony = false;

            if (TabButton(new Rect(row.x + half + 8f, row.y, half, row.height),
                          "PR.Records.Tab.Colony".Translate(), showColony))
                showColony = true;
        }

        private static bool TabButton(Rect rect, string label, bool selected)
        {
            if (selected)
            {
                Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.09f));
                GUI.color = PRTheme.ActiveTurn;
                Widgets.DrawBox(rect, 1);
                GUI.color = Color.white;
            }
            else if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = selected ? Color.white : PRTheme.Dim;

            // 줄을 늘려도 안 들어가는 이름이 있다. 그때는 잘라 쓰고 통째로는 툴팁에 남긴다.
            string shown = label.Truncate(rect.width - 8f);
            Widgets.Label(rect, shown);
            if (shown != label && Mouse.IsOver(rect)) TooltipHandler.TipRegion(rect, label);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            return Widgets.ButtonInvisible(rect);
        }

        private void DrawTableHeader(Rect row)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;

            DrawCells(row,
                "PR.Records.Col.Difficulty".Translate(),
                "PR.Records.Col.Record".Translate(),
                "PR.Records.Col.WinRate".Translate(),
                "PR.Records.Col.Fastest".Translate(),
                "PR.Records.Col.Streak".Translate());

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawTierRow(Rect row, GameRecord record, int tier)
        {
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

            float rate = record.WinRateAt(tier);
            int fastest = record.FastestWinAt(tier);
            int streak = record.BestStreakAt(tier);

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            bool cleared = component != null && component.IsCleared(game, tier);

            string label = game.TierLabel(tier);
            if (cleared) label = label + "  V";

            DrawCells(row,
                label,
                record.WinsAt(tier) + " - " + record.LossesAt(tier),
                rate < 0f ? "-" : (rate * 100f).ToString("0") + "%",
                fastest == 0 ? "-" : "PR.Records.Rounds".Translate(fastest).ToString(),
                streak == 0 ? "-" : streak.ToString());
        }

        private static void DrawCells(Rect row, params string[] cells)
        {
            float x = row.x;

            for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            {
                float width = row.width * Columns[i];
                Text.Anchor = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                Widgets.Label(new Rect(x, row.y, width - 8f, row.height), cells[i]);
                x += width;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private List<string> SummaryLines(GameRecord record)
        {
            List<string> lines = new List<string>();

            lines.Add("PR.Records.Summary".Translate(
                record.wins, record.losses, record.voided, record.resigns).Resolve());

            // 그 게임에서만 의미가 있는 숫자들. 이름표는 Def 가 들고 있다.
            if (game.tallyKeys != null)
            {
                for (int i = 0; i < game.tallyKeys.Count && i < GameRecord.TallyCount; i++)
                    lines.Add(game.tallyKeys[i].Translate(record.Tally(i)).Resolve());
            }

            lines.Add("PR.Records.Streak".Translate(record.flawlessWins, record.longestWinStreak).Resolve());
            lines.Add("PR.Records.Time".Translate(
                PlayTimeText(record.totalRealSeconds), record.totalUndosUsed).Resolve());

            return lines;
        }

        private void DrawSummary(Rect area, GameRecord record)
        {
            List<string> lines = SummaryLines(record);

            Text.Font = SummaryFont;
            GUI.color = PRTheme.Dim;

            // 굴림대 자리를 미리 빼고 잰다. 굴리지 않게 되면 그만큼 여유가 생길 뿐이다.
            float narrow = area.width - 20f;
            float needed = 0f;
            foreach (string line in lines) needed += Text.CalcHeight(line, narrow) + 2f;

            // 창을 내용에 맞춰 키워도 화면이 작으면 모자랄 수 있다.
            // 그때는 마지막 줄을 잘라 버리는 대신 굴려서 읽게 한다.
            bool scroll = needed > area.height;
            Rect inner = scroll ? new Rect(0f, 0f, narrow, needed) : area;

            if (scroll) Widgets.BeginScrollView(area, ref summaryScroll, inner);

            Listing_Standard list = new Listing_Standard();

            // 여기는 두 칸으로 나뉘어질 자리가 아니다.
            list.maxOneColumn = true;

            list.Begin(inner);
            foreach (string line in lines) list.Label(line);
            list.End();

            if (scroll) Widgets.EndScrollView();

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawFooter(Rect footer)
        {
            const float width = 150f;

            if (Widgets.ButtonText(new Rect(footer.x, footer.y, width, footer.height),
                                   "PR.Records.Reset".Translate()))
                ConfirmReset();

            if (Widgets.ButtonText(new Rect(footer.xMax - width, footer.y, width, footer.height),
                                   "PR.Records.Close".Translate()))
                Close();
        }

        private void ConfirmReset()
        {
            string scope = showColony
                ? "PR.Records.Tab.Colony".Translate().ToString()
                : "PR.Records.Tab.Player".Translate().ToString();

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "PR.Records.Reset.Confirm".Translate(game.label, scope), ResetNow, true));
        }

        private void ResetNow()
        {
            if (showColony)
            {
                GameComponent_Recreation component = GameComponent_Recreation.Current;
                if (component != null) component.ColonyRecord(game).Reset();
            }
            else
            {
                RecordStore.Reset(game);
            }
        }

        private static string PlayTimeText(int seconds)
        {
            int hours = seconds / 3600;
            int minutes = seconds % 3600 / 60;

            return hours > 0
                ? "PR.Records.HoursMinutes".Translate(hours, minutes).ToString()
                : "PR.Records.Minutes".Translate(minutes).ToString();
        }
    }
}
