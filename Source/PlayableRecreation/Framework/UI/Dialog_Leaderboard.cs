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
        private const float HeaderHeight = 26f;
        private const float RowHeight = 28f;
        private const float FooterHeight = 40f;
        private const float Pad = 10f;

        private static readonly float[] Columns = { 0.24f, 0.22f, 0.16f, 0.19f, 0.19f };

        private MiniGameDef game;
        private bool showColony;

        public override Vector2 InitialSize
        {
            get { return new Vector2(620f, 520f); }
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
                DrawGameTabs(new Rect(inRect.x, y, inRect.width, TabHeight), games);
                y += TabHeight + 6f;
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

        private void DrawGameTabs(Rect row, List<MiniGameDef> games)
        {
            float width = (row.width - (games.Count - 1) * 6f) / games.Count;

            for (int i = 0; i < games.Count; i++)
            {
                Rect tab = new Rect(row.x + i * (width + 6f), row.y, width, row.height);
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
            GUI.color = selected ? Color.white : PRTheme.Dim;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
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

        private void DrawSummary(Rect area, GameRecord record)
        {
            Listing_Standard list = new Listing_Standard();

            // 여기도 두 칸으로 나뉘어질 자리가 아니다. 넘치면 잘리는 편이
            // 소리 없이 화면 밖으로 사라지는 것보다 낫다.
            list.maxOneColumn = true;

            list.Begin(area);

            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;

            list.Label("PR.Records.Summary".Translate(
                record.wins, record.losses, record.voided, record.resigns));

            // 그 게임에서만 의미가 있는 숫자들. 이름표는 Def 가 들고 있다.
            if (game.tallyKeys != null)
            {
                for (int i = 0; i < game.tallyKeys.Count && i < GameRecord.TallyCount; i++)
                    list.Label(game.tallyKeys[i].Translate(record.Tally(i)));
            }

            list.Label("PR.Records.Streak".Translate(record.flawlessWins, record.longestWinStreak));
            list.Label("PR.Records.Time".Translate(PlayTimeText(record.totalRealSeconds), record.totalUndosUsed));

            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            list.End();
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
