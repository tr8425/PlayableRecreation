using System.Collections.Generic;
using PlayableRecreation;
using PlayableRecreation.UI;
using Roulette.Core;
using UnityEngine;
using Verse;

namespace Roulette
{
    /// <summary>
    /// 룰렛 - 뱅크롤 런. 칩 스무 닢으로 시작해 난이도가 정한 목표액까지 불리면 이긴다.
    /// 스핀 하나하나는 순수 운이지만, 판 전체는 아니다 - 어디에 얼마를 거느냐로
    /// 변동성을 고르는 것이 이 게임의 조작이다. 낮은 목표는 빨강에 걸어 밀면 되고,
    /// 높은 목표는 숫자 하나를 집는 배짱 없이는 산수가 나오지 않는다.
    ///
    /// 결과는 (시드, 순번)으로 돌리는 순간 정해진다. 휠은 구경거리다 -
    /// 원래 룰렛이 그런 물건이다.
    /// </summary>
    public class RouletteGameWorker : MiniGameWorker
    {
        private const float SpinSeconds = 2.4f;
        private const float ExtraTurns = 3f;

        private const float InfoHeight = 30f;
        private const float CellHeight = 26f;
        private const float CellGap = 2f;
        private const float RowGap = 5f;
        private const float ZeroWidth = 40f;
        private const float StakeHeight = 28f;

        private RouletteRun run;

        private BetKind betKind = BetKind.Red;
        private int betValue;
        private int stake = 5;

        private bool spinning;
        private float spinStart;
        private float now;
        private float lastNow;
        private float nextTick;
        private float tickGap;

        private RouletteEntry lastEntry;
        private bool hasLast;

        /// <summary>칩 표시는 실제 값을 천천히 따라간다. 도는 동안은 건 돈이 테이블 위에 있다.</summary>
        private float shownBankroll = RouletteRun.StartBankroll;

        private float winFlashUntil;

        private readonly List<string> logLines = new List<string>();
        private int lastLogCount = -1;

        private static readonly string[] DozenKeys = { "RLT.Bet.Dozen1", "RLT.Bet.Dozen2", "RLT.Bet.Dozen3" };

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return run != null ? run.Spins : 0; }
        }

        /// <summary>돌린 횟수. 기록의 '최단 승리'가 이 값을 그대로 쓰므로 부풀리지 않는다.</summary>
        public override int Rounds
        {
            get { return run != null ? run.Spins : 0; }
        }

        /// <summary>
        /// 룰렛은 한 번만 돌려도 판이 끝난 것이다 - 열 닢을 걸고 잃은 사람이 창을 닫아
        /// 없던 일로 만들 수는 없다. 그래서 라운드 둘이 아니라 스핀 하나를 기준으로 답한다.
        /// </summary>
        public override bool HasProgress
        {
            get { return run != null && run.Spins > 0; }
        }

        /// <summary>공이 도는 동안은 아직 끝이 아니다 - 마지막 스핀의 연출을 지키는 문이다.</summary>
        public override bool IsOver
        {
            get { return run != null && run.IsOver && !spinning; }
        }

        public override bool PlayerWon
        {
            get { return run != null && run.IsOver && run.Won; }
        }

        /// <summary>한 번도 시작액 아래로 내려가지 않은 완주.</summary>
        public override bool Flawless
        {
            get { return run != null && run.Won && !run.DippedBelowStart; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            run = new RouletteRun(seed, RouletteRun.TargetFor(Tier));
            ResetView(RouletteRun.StartBankroll);
        }

        public override void Resume(MiniGameSaveData data)
        {
            RouletteSaveData saved = data as RouletteSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            run = saved.ToRun();
            ResetView(run.Bankroll);

            if (run.Log.Count > 0)
            {
                lastEntry = run.Log[run.Log.Count - 1];
                hasLast = true;
            }
        }

        private void ResetView(int bankroll)
        {
            spinning = false;
            hasLast = false;
            shownBankroll = bankroll;
            winFlashUntil = 0f;
            lastNow = 0f;
            lastLogCount = -1;
            stake = Mathf.Clamp(stake, 1, RouletteRun.MaxStake);
        }

        /// <summary>
        /// 끝난 판도 떠낸다 - 마지막 스핀의 연출 중에 창이 닫히면 저장으로 남았다가,
        /// 다시 여는 순간 결과가 확정되는 길이다.
        /// </summary>
        public override MiniGameSaveData MakeSaveData()
        {
            return run != null && run.Spins > 0 ? new RouletteSaveData(run) : null;
        }

        // ---------- 진행 ----------

        public override void Tick(float timeNow)
        {
            now = timeNow;
            float delta = lastNow <= 0f ? 0f : Mathf.Min(0.1f, now - lastNow);
            lastNow = now;

            // 도는 동안 표시할 값: 스핀 전 뱅크롤에서 건 돈을 뺀 것 - 칩은 테이블 위에 있다.
            float shownTarget = run != null ? run.Bankroll : 0f;
            if (spinning) shownTarget = run.Bankroll - lastEntry.Net - lastEntry.Stake;

            float gap = Mathf.Abs(shownTarget - shownBankroll);
            if (gap > 0.001f)
                shownBankroll = Mathf.MoveTowards(shownBankroll, shownTarget, delta * Mathf.Max(8f, gap * 2.2f));

            if (!spinning) return;

            // 공이 칸을 넘는 소리. 감속하며 뜸해진다.
            if (now >= nextTick)
            {
                PRSounds.Play(RouletteSounds.Tick);
                tickGap *= 1.45f;
                nextTick = now + tickGap;
            }

            if (now - spinStart >= SpinSeconds) Land();
        }

        private void StartSpin()
        {
            if (run == null || run.IsOver || spinning) return;

            lastEntry = run.Spin(betKind, betValue, stake);
            hasLast = true;

            spinning = true;
            spinStart = now;
            tickGap = 0.10f;
            nextTick = now + 0.15f;

            PRSounds.Play(RouletteSounds.Spin);
        }

        private void Land()
        {
            spinning = false;

            // 다음 스핀을 위해 건 돈을 남은 칩에 맞춘다.
            stake = Mathf.Clamp(stake, 1, Mathf.Max(1, Mathf.Min(run.Bankroll, RouletteRun.MaxStake)));

            if (lastEntry.Net <= 0) return;

            winFlashUntil = now + 1.1f;
            PRSounds.Play(lastEntry.Kind == BetKind.Straight ? RouletteSounds.Big : RouletteSounds.Win);
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (!PRKeys.ActionPressed()) return;
            if (!ActionEnabled) return;

            StartSpin();
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get
            {
                return (spinning ? "RLT.Btn.Spinning" : "RLT.Btn.Spin").Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get { return run != null && !run.IsOver && !spinning; }
        }

        public override void DoAction()
        {
            if (ActionEnabled) StartSpin();
        }

        private bool CanBet
        {
            get { return run != null && !run.IsOver && !spinning; }
        }

        private void SelectBet(BetKind kind, int value)
        {
            if (!CanBet) return;
            if (betKind == kind && betValue == value) return;

            betKind = kind;
            betValue = value;
            PRSounds.Play(RouletteSounds.Pick);
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            DrawInfoRow(new Rect(area.x, area.y, area.width, InfoHeight));

            float gridHeight = CellHeight * 3f + CellGap * 2f;
            float below = RowGap + gridHeight + RowGap + CellHeight + RowGap + CellHeight + RowGap + StakeHeight;
            float wheelHeight = Mathf.Clamp(area.height - InfoHeight - below, 140f, 240f);

            DrawWheel(new Rect(area.x, area.y + InfoHeight, area.width, wheelHeight));

            float y = area.y + InfoHeight + wheelHeight + RowGap;
            Rect table = new Rect(area.x, y, area.width, gridHeight + RowGap + CellHeight + RowGap + CellHeight);
            DrawTable(table, true);

            DrawStakeRow(new Rect(area.x, table.yMax + RowGap, area.width, StakeHeight));
        }

        private void DrawInfoRow(Rect row)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = RouletteTheme.Bankroll;
            Widgets.Label(new Rect(row.x, row.y, row.width / 2f, row.height),
                "RLT.Bankroll".Translate(Mathf.FloorToInt(shownBankroll)).ToString());

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(row.center.x, row.y, row.width / 2f, row.height),
                "RLT.Target".Translate(run != null ? run.Target : 0).ToString());

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawWheel(Rect area)
        {
            float size = Mathf.Min(area.width, area.height);
            Rect box = new Rect(area.center.x - size / 2f, area.center.y - size / 2f, size, size);

            Vector2 center = box.center;
            float radius = size * 0.48f;

            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                RouletteTextures.Wheel);

            // 공. 감속하며 여분의 바퀴를 풀고 정해진 칸에 멈춘다.
            if (hasLast)
            {
                float final = RouletteWheel.WheelIndexOf(lastEntry.Pocket) * RouletteWheel.SectorAngle;
                float angle = final;

                if (spinning)
                {
                    float t = Mathf.Clamp01((now - spinStart) / SpinSeconds);
                    float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                    angle = final + (1f - ease) * ExtraTurns * Mathf.PI * 2f;
                }

                float ballRadius = radius * RouletteTextures.BallRadius;
                Vector2 ball = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * ballRadius;
                float dot = Mathf.Max(7f, size * 0.045f);

                GUI.color = RouletteTheme.Ball;
                GUI.DrawTexture(new Rect(ball.x - dot / 2f, ball.y - dot / 2f, dot, dot), PRTextures.Dot);
                GUI.color = Color.white;
            }

            // 허브 가운데의 결과. 휠의 숫자는 읽으라고 있는 것이 아니다.
            if (hasLast && !spinning)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = PocketColor(lastEntry.Pocket);
                Widgets.Label(new Rect(center.x - 40f, center.y - 20f, 80f, 40f),
                    lastEntry.Pocket.ToString());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (now < winFlashUntil && !spinning)
                Widgets.DrawBoxSolid(box, RouletteTheme.WinFlash);
        }

        private static Color PocketColor(int pocket)
        {
            if (pocket == 0) return RouletteTheme.PocketGreen;
            return RouletteWheel.IsRed(pocket) ? RouletteTheme.PocketRed : RouletteTheme.PocketBlack;
        }

        /// <summary>숫자판 - 0 · 3×12 그리드 · 다즌 · 짝수배당. 판의 배치가 곧 배당표다.</summary>
        private void DrawTable(Rect area, bool interactive)
        {
            float gridWidth = area.width - ZeroWidth - CellGap;
            float cellWidth = (gridWidth - CellGap * 11f) / 12f;
            float gridX = area.x + ZeroWidth + CellGap;
            float gridHeight = CellHeight * 3f + CellGap * 2f;

            // 0 은 그리드 왼쪽에 세로로 길게 - 실제 테이블의 자리다.
            DrawCell(new Rect(area.x, area.y, ZeroWidth, gridHeight), "0",
                RouletteTheme.PocketGreen, BetKind.Straight, 0, interactive);

            for (int column = 0; column < 12; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    int number = column * 3 + (3 - row);
                    Rect cell = new Rect(gridX + column * (cellWidth + CellGap),
                                         area.y + row * (CellHeight + CellGap), cellWidth, CellHeight);

                    DrawCell(cell, number.ToString(), PocketColor(number),
                        BetKind.Straight, number, interactive);
                }
            }

            // 다즌 - 각각 네 열을 어깨에 얹는다.
            float dozenY = area.y + gridHeight + RowGap;
            float dozenWidth = (gridWidth - CellGap * 2f) / 3f;

            for (int i = 0; i < 3; i++)
            {
                Rect cell = new Rect(gridX + i * (dozenWidth + CellGap), dozenY, dozenWidth, CellHeight);
                DrawCell(cell, DozenKeys[i].Translate().ToString(), RouletteTheme.Felt,
                    BetKind.Dozen1 + i, 0, interactive);
            }

            // 짝수배당 넷.
            float evenY = dozenY + CellHeight + RowGap;
            float evenWidth = (gridWidth - CellGap * 3f) / 4f;

            DrawCell(new Rect(gridX, evenY, evenWidth, CellHeight),
                "RLT.Bet.Red".Translate().ToString(), RouletteTheme.PocketRed, BetKind.Red, 0, interactive);
            DrawCell(new Rect(gridX + (evenWidth + CellGap), evenY, evenWidth, CellHeight),
                "RLT.Bet.Black".Translate().ToString(), RouletteTheme.PocketBlack, BetKind.Black, 0, interactive);
            DrawCell(new Rect(gridX + (evenWidth + CellGap) * 2f, evenY, evenWidth, CellHeight),
                "RLT.Bet.Odd".Translate().ToString(), RouletteTheme.Felt, BetKind.Odd, 0, interactive);
            DrawCell(new Rect(gridX + (evenWidth + CellGap) * 3f, evenY, evenWidth, CellHeight),
                "RLT.Bet.Even".Translate().ToString(), RouletteTheme.Felt, BetKind.Even, 0, interactive);
        }

        private void DrawCell(Rect cell, string label, Color back, BetKind kind, int value, bool interactive)
        {
            bool selected = betKind == kind && betValue == value;
            bool landed = hasLast && !spinning && kind == BetKind.Straight && value == lastEntry.Pocket;

            if (selected) Widgets.DrawBoxSolid(cell.ExpandedBy(2f), RouletteTheme.Select);
            else if (landed) Widgets.DrawBoxSolid(cell.ExpandedBy(2f), RouletteTheme.Ball);

            Widgets.DrawBoxSolid(cell, back);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = RouletteTheme.CellText;
            Widgets.Label(cell, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (interactive && Widgets.ButtonInvisible(cell)) SelectBet(kind, value);
        }

        /// <summary>건 돈 조절 줄. 왼쪽이 조절, 오른쪽이 지금 걸려 있는 것.</summary>
        private void DrawStakeRow(Rect row)
        {
            float x = row.x;

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(x, row.y, 44f, row.height), "RLT.Stake".Translate().ToString());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            x += 48f;

            int max = run != null ? Mathf.Max(1, Mathf.Min(run.Bankroll, RouletteRun.MaxStake)) : 1;

            if (StakeButton(ref x, row, "-5")) stake = Mathf.Max(1, stake - 5);
            if (StakeButton(ref x, row, "-1")) stake = Mathf.Max(1, stake - 1);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = RouletteTheme.Bankroll;
            Widgets.Label(new Rect(x, row.y, 36f, row.height), stake.ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            x += 38f;

            if (StakeButton(ref x, row, "+1")) stake = Mathf.Min(max, stake + 1);
            if (StakeButton(ref x, row, "+5")) stake = Mathf.Min(max, stake + 5);
            if (StakeButton(ref x, row, "RLT.Btn.Max".Translate().ToString())) stake = max;

            stake = Mathf.Clamp(stake, 1, max);

            // 지금 걸려 있는 것 - 어디에, 얼마를, 몇 배 배당인지.
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(x, row.y, row.xMax - x, row.height),
                "RLT.Bet.Current".Translate(BetLabel(betKind, betValue), stake,
                    RouletteWheel.NetMultiplier(betKind)).ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private bool StakeButton(ref float x, Rect row, string label)
        {
            const float width = 34f;
            Rect button = new Rect(x, row.y, width, row.height);
            x += width + 4f;

            if (!CanBet)
            {
                GUI.color = PRTheme.Dim;
                Widgets.ButtonText(button, label, true, false, false);
                GUI.color = Color.white;
                return false;
            }

            return Widgets.ButtonText(button, label);
        }

        private static string BetLabel(BetKind kind, int value)
        {
            switch (kind)
            {
                case BetKind.Straight: return value.ToString();
                case BetKind.Red: return "RLT.Bet.Red".Translate().ToString();
                case BetKind.Black: return "RLT.Bet.Black".Translate().ToString();
                case BetKind.Odd: return "RLT.Bet.Odd".Translate().ToString();
                case BetKind.Even: return "RLT.Bet.Even".Translate().ToString();
                case BetKind.Dozen1: return "RLT.Bet.Dozen1".Translate().ToString();
                case BetKind.Dozen2: return "RLT.Bet.Dozen2".Translate().ToString();
                default: return "RLT.Bet.Dozen3".Translate().ToString();
            }
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (run == null) return string.Empty;

                if (run.IsOver && !spinning)
                {
                    return run.Won
                        ? "RLT.Status.Won".Translate(run.Bankroll, run.Spins).ToString()
                        : "RLT.Status.Bust".Translate(run.Spins).ToString();
                }

                if (spinning) return "RLT.Status.Spinning".Translate().ToString();

                if (hasLast)
                {
                    return lastEntry.Net > 0
                        ? "RLT.Status.Hit".Translate(lastEntry.Pocket, lastEntry.Net).ToString()
                        : "RLT.Status.Miss".Translate(lastEntry.Pocket, lastEntry.Stake).ToString();
                }

                return "RLT.Status.Idle".Translate().ToString();
            }
        }

        public override IReadOnlyList<string> Log
        {
            get
            {
                if (run == null) return logLines;

                // 도는 동안 마지막 줄을 숨긴다 - 결과는 공이 멈춘 뒤의 것이다.
                int visible = run.Log.Count - (spinning ? 1 : 0);

                if (visible != lastLogCount)
                {
                    lastLogCount = visible;
                    logLines.Clear();
                    for (int i = 0; i < visible; i++) logLines.Add(Format(run.Log[i]));
                }

                return logLines;
            }
        }

        private static string Format(RouletteEntry entry)
        {
            string net = entry.Net > 0 ? "+" + entry.Net : entry.Net.ToString();
            return string.Format("{0,3}  {1}×{2}  →  {3}  {4}",
                entry.Index + 1, BetLabel(entry.Kind, entry.Value), entry.Stake, entry.Pocket, net);
        }

        public override void FillTallies(int[] tallies)
        {
            if (run == null || tallies.Length < 3) return;

            tallies[0] = run.BestWin;
            tallies[1] = run.StraightHits;
            tallies[2] = run.Peak;
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (page == 0)
            {
                // 휠 하나. 공은 17 에 멈춰 있다 - 어느 칸이든 상관없는 그림이다.
                float size = Mathf.Min(area.width, area.height) * 0.8f;
                Vector2 center = area.center;
                float radius = size * 0.5f;

                GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, size, size),
                    RouletteTextures.Wheel);

                float angle = RouletteWheel.WheelIndexOf(17) * RouletteWheel.SectorAngle;
                Vector2 ball = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle))
                               * radius * RouletteTextures.BallRadius;

                GUI.color = RouletteTheme.Ball;
                GUI.DrawTexture(new Rect(ball.x - 5f, ball.y - 5f, 10f, 10f), PRTextures.Dot);
                GUI.color = Color.white;
                return;
            }

            if (page == 1)
            {
                float height = CellHeight * 3f + CellGap * 2f + (RowGap + CellHeight) * 2f;
                DrawTable(new Rect(area.x + 16f, area.center.y - height / 2f,
                                   area.width - 32f, height), false);
                return;
            }

            Listing_Standard list = new Listing_Standard();
            list.maxOneColumn = true;
            list.Begin(new Rect(area.x + area.width * 0.12f, area.y + 24f, area.width * 0.76f, area.height - 24f));

            list.Label("RLT.Tut.Pay.Straight".Translate());
            list.Label("RLT.Tut.Pay.Dozen".Translate());
            list.Label("RLT.Tut.Pay.Even".Translate());
            list.Label("RLT.Tut.Pay.Zero".Translate());
            list.Gap(8f);
            list.Label("RLT.Tut.Pay.Run".Translate(RouletteRun.StartBankroll, RouletteRun.MaxStake));

            list.End();
        }
    }
}
