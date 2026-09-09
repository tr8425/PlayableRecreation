using System.Collections.Generic;
using Darts.Core;
using PlayableRecreation;
using PlayableRecreation.Core;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Darts
{
    /// <summary>
    /// 다트. 던지기 게임들과 같은 두 막대 조작이지만, 어디를 노릴지를 판을 눌러 직접 고른다 -
    /// 트리플 20 을 노릴지 안전한 불을 노릴지가 이 게임의 절반이다.
    ///
    /// 조준·세기 막대와 결정론 난수는 중립 지대의 산수(AimMath)를 쓴다 -
    /// 던지기 게임들과 손맛은 같지만 서로를 모른다. 판정만 다르다 -
    /// 거리 하나가 아니라 자리(섹터 × 링)가 점수다.
    /// </summary>
    public class DartsGameWorker : MiniGameWorker
    {
        private enum Stage
        {
            Aim,
            Power,
            Reveal,
            Opponent,
        }

        private const float RevealSeconds = 0.9f;
        private const float ScoreboardHeight = 46f;
        private const float BarHeight = 26f;
        private const float BarGap = 10f;

        /// <summary>
        /// 정타 구간의 기준 반지름(판 단위). 두 막대 모두 초록 안이면 이 안에 꽂힌다 -
        /// 노린 섹터를 크게 벗어나지 않는 정도이고, 트리플이냐 싱글이냐는 그 안의 정밀도가 가른다.
        /// </summary>
        private const float ZoneRadius = 0.12f;

        private const float DefaultAimY = (DartBoard.TripleInner + DartBoard.TripleOuter) * 0.5f;

        private DartsMatch match;

        private Stage stage;
        private float sweep;
        private float sweepDirection = 1f;
        private float lastTime;
        private float stageUntil;

        private float lockedLateral;
        private float aimX;
        private float aimY = DefaultAimY;

        /// <summary>이번 발의 정타 구간. [0]이 좌우, [1]이 세기.</summary>
        private readonly float[] zoneCenter = new float[2];
        private readonly float[] zoneHalf = new float[2];

        private DartEntry lastEntry;
        private bool hasLast;

        /// <summary>판에 그려 둘 라운드. 라운드가 넘어가면 마지막 발을 보여준 뒤 치운다.</summary>
        private int drawnRound = -1;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return match != null ? match.Round : 0; }
        }

        public override int Rounds
        {
            get { return match != null ? match.Round : 0; }
        }

        public override bool IsOver
        {
            get { return match != null && match.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return match != null && match.IsOver && match.Winner == DartSide.Player; }
        }

        /// <summary>모든 라운드를 이긴(합이 더 큰) 승리.</summary>
        public override bool Flawless
        {
            get { return match != null && match.RoundsClosed > 0 && match.RoundsWonPlayer == match.RoundsClosed; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            match = new DartsMatch(seed);
            ResetRound();
        }

        public override void Resume(MiniGameSaveData data)
        {
            DartsSaveData saved = data as DartsSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            match = saved.ToMatch();
            ResetRound();
        }

        public override MiniGameSaveData MakeSaveData()
        {
            return match != null && !match.IsOver ? new DartsSaveData(match) : null;
        }

        private void ResetRound()
        {
            drawnRound = match.Round;

            hasLast = false;
            sweep = 0f;
            sweepDirection = 1f;
            lastTime = 0f;
            lastLogCount = -1;

            stage = match.Turn == DartSide.Opponent ? Stage.Opponent : Stage.Aim;
            stageUntil = 0f;

            RollZones();
        }

        /// <summary>
        /// 이번 발의 초록 구간을 뽑는다. (시드, 순번)에서 나오므로
        /// 창을 닫았다 열어도 같은 자리다 - 마음에 안 드는 구간을 무를 길은 없다.
        /// </summary>
        private void RollZones()
        {
            for (int axis = 0; axis < 2; axis++)
            {
                zoneHalf[axis] = AimMath.ZoneHalf(match.Seed, match.ThrowIndex, axis, Tier, ZoneRadius);
                zoneCenter[axis] = AimMath.ZoneCenter(match.Seed, match.ThrowIndex, axis, Tier, zoneHalf[axis]);
            }
        }

        // ---------- 진행 ----------

        public override void Tick(float now)
        {
            if (match == null || match.IsOver) return;

            float delta = lastTime <= 0f ? 0f : Mathf.Min(0.1f, now - lastTime);
            lastTime = now;

            switch (stage)
            {
                case Stage.Aim:
                case Stage.Power:
                    Sweep(delta);
                    break;

                case Stage.Reveal:
                    if (now >= stageUntil) NextThrower();
                    break;

                case Stage.Opponent:
                    if (stageUntil <= 0f) stageUntil = now + Mathf.Max(0.2f, PRMod.Settings.botThinkSeconds);
                    else if (now >= stageUntil) OpponentThrow(now);
                    break;
            }
        }

        private void Sweep(float delta)
        {
            sweep += sweepDirection * AimMath.BarSpeedFor(Tier) * delta;

            if (sweep >= 1f) { sweep = 1f; sweepDirection = -1f; }
            else if (sweep <= 0f) { sweep = 0f; sweepDirection = 1f; }
        }

        /// <summary>막대를 멈춘다. 초록 구간에서 벗어난 만큼이 조준점에서 밀려나는 오차가 된다.</summary>
        private void Lock(float now)
        {
            int axis = stage == Stage.Aim ? 0 : 1;
            float error = AimMath.AxisError(sweep, zoneCenter[axis], zoneHalf[axis], ZoneRadius);

            if (stage == Stage.Aim)
            {
                lockedLateral = error;
                stage = Stage.Power;
                sweep = 0f;
                sweepDirection = 1f;
                PRSounds.Play(DartsSounds.Lock);
                return;
            }

            // 조준 오차는 좌우로, 세기 오차는 위아래로 - 세게 던지면 노린 곳보다 위에 꽂힌다.
            Resolve(aimX + lockedLateral, aimY + error, now);
        }

        private void OpponentThrow(float now)
        {
            float x, y;
            DartsAi.BotDart(match.Seed, match.ThrowIndex, Tier, out x, out y);
            Resolve(x, y, now);
        }

        private void Resolve(float x, float y, float now)
        {
            lastEntry = match.Throw(x, y);
            hasLast = true;

            bool big = lastEntry.Points >= 40 || (lastEntry.Code.Length > 0 && lastEntry.Code[0] == 'T')
                       || lastEntry.Code == "50";
            PRSounds.Play(big ? DartsSounds.Big : DartsSounds.Land);

            stage = Stage.Reveal;
            stageUntil = now + RevealSeconds;
        }

        private void NextThrower()
        {
            if (match.IsOver) return;

            // 라운드가 넘어갔으면 이제 판을 치운다 - 마지막 한 발을 보여준 뒤다.
            if (match.Round != drawnRound) drawnRound = match.Round;

            sweep = 0f;
            sweepDirection = 1f;
            stageUntil = 0f;
            stage = match.Turn == DartSide.Opponent ? Stage.Opponent : Stage.Aim;

            RollZones();
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (!PRKeys.ActionPressed()) return;
            if (!ActionEnabled) return;

            Lock(Time.realtimeSinceStartup);
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get
            {
                // 막대 문구는 던지기 게임들과 같은 말이라 키도 같이 쓴다.
                if (stage == Stage.Aim) return "THR.Btn.LockAim".Translate().ToString();
                if (stage == Stage.Power) return "THR.Btn.Throw".Translate().ToString();
                return "THR.Btn.Waiting".Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get { return match != null && !match.IsOver && (stage == Stage.Aim || stage == Stage.Power); }
        }

        public override void DoAction()
        {
            if (ActionEnabled) Lock(Time.realtimeSinceStartup);
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            float barsHeight = BarHeight * 2f + BarGap;

            DrawScoreboard(new Rect(area.x, area.y, area.width, ScoreboardHeight));

            Rect boardArea = new Rect(area.x, area.y + ScoreboardHeight, area.width,
                                      area.height - ScoreboardHeight - barsHeight - BarGap);
            DrawBoard(boardArea);

            float y = area.yMax - barsHeight;
            DrawBar(new Rect(area.x, y, area.width, BarHeight), "THR.Bar.Aim".Translate(),
                    stage == Stage.Aim, zoneCenter[0], zoneHalf[0]);
            DrawBar(new Rect(area.x, y + BarHeight + BarGap, area.width, BarHeight),
                    "THR.Bar.Power".Translate(), stage == Stage.Power, zoneCenter[1], zoneHalf[1]);
        }

        private void DrawScoreboard(Rect row)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = DartsTheme.PlayerMark;
            Widgets.Label(new Rect(row.x, row.y, row.width / 2f, row.height), match.TotalPlayer.ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = DartsTheme.OpponentMark;
            Widgets.Label(new Rect(row.center.x, row.y, row.width / 2f, row.height),
                match.TotalOpponent.ToString());

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = PRTheme.Dim;
            Widgets.Label(row, match.Round > DartsMatch.BaseRounds
                ? "DRT.Scoreboard.Extra".Translate(match.Round).ToString()
                : "DRT.Scoreboard".Translate(match.Round, DartsMatch.BaseRounds).ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawBoard(Rect area)
        {
            float size = Mathf.Min(area.width, area.height);
            Rect box = new Rect(area.center.x - size / 2f, area.center.y - size / 2f, size, size);

            Widgets.DrawMenuSection(box);

            Vector2 center = box.center;
            float radius = size * 0.40f;

            // 판을 눌러 조준점을 옮긴다. 막대가 도는 동안에는 못 옮긴다 - 이미 던지는 중이다.
            if (stage == Stage.Aim && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Vector2 mouse = Event.current.mousePosition;
                float bx = (mouse.x - center.x) / radius;
                float by = (center.y - mouse.y) / radius;

                if (bx * bx + by * by <= 1.1f * 1.1f)
                {
                    Vector2 aim = Vector2.ClampMagnitude(new Vector2(bx, by), 0.99f);
                    aimX = aim.x;
                    aimY = aim.y;
                    Event.current.Use();
                }
            }

            float textureRadius = radius * DartsTextures.Margin;
            GUI.DrawTexture(new Rect(center.x - textureRadius, center.y - textureRadius,
                textureRadius * 2f, textureRadius * 2f), DartsTextures.Board);

            DrawNumbers(center, radius);

            // 이번 라운드에 꽂힌 것들. 내 것은 점, 상대 것은 고리다.
            IReadOnlyList<DartEntry> entries = match.Log;
            float mark = Mathf.Max(8f, size * 0.032f);

            for (int i = 0; i < entries.Count; i++)
            {
                DartEntry entry = entries[i];
                if (entry.Round != drawnRound) continue;

                bool latest = hasLast && i == entries.Count - 1 && stage == Stage.Reveal;
                float drawn = latest ? mark * 1.5f : mark;

                Vector2 point = center + new Vector2(entry.X, -entry.Y) * radius;

                GUI.color = entry.Side == DartSide.Player ? DartsTheme.PlayerMark : DartsTheme.OpponentMark;
                GUI.DrawTexture(new Rect(point.x - drawn / 2f, point.y - drawn / 2f, drawn, drawn),
                    entry.Side == DartSide.Player ? PRTextures.Dot : PRTextures.Ring);
            }

            GUI.color = Color.white;

            if (stage == Stage.Aim || stage == Stage.Power) DrawCrosshair(center, radius);
        }

        private void DrawNumbers(Vector2 center, float radius)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = DartsTheme.Numbers;

            float ring = radius * 1.13f;

            for (int i = 0; i < 20; i++)
            {
                float angle = DartBoard.SectorCenterAngle(i);
                float x = center.x + Mathf.Sin(angle) * ring;
                float y = center.y - Mathf.Cos(angle) * ring;

                Widgets.Label(new Rect(x - 16f, y - 10f, 32f, 20f), DartBoard.Sectors[i].ToString());
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawCrosshair(Vector2 center, float radius)
        {
            Vector2 point = center + new Vector2(aimX, -aimY) * radius;

            GUI.color = DartsTheme.Crosshair;
            Widgets.DrawBoxSolid(new Rect(point.x - 7f, point.y - 1f, 14f, 2f), DartsTheme.Crosshair);
            Widgets.DrawBoxSolid(new Rect(point.x - 1f, point.y - 7f, 2f, 14f), DartsTheme.Crosshair);
            GUI.color = Color.white;
        }

        /// <summary>좌우 · 세기 막대. 던지기 게임들과 같은 생김새라 색도 같이 쓴다.</summary>
        private void DrawBar(Rect row, string label, bool live, float center, float half)
        {
            Rect labelRect = new Rect(row.x, row.y, 60f, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = live ? Color.white : PRTheme.Dim;
            Widgets.Label(labelRect, label);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            Rect bar = new Rect(labelRect.xMax + 6f, row.y + 6f, row.width - labelRect.width - 6f, row.height - 12f);
            Widgets.DrawBoxSolid(bar, DartsTheme.BarBack);

            Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * (center - half), bar.y,
                bar.width * half * 2f, bar.height), DartsTheme.BarSweet);

            if (!live) return;

            float x = bar.x + bar.width * sweep;
            Widgets.DrawBoxSolid(new Rect(x - 2f, bar.y - 3f, 4f, bar.height + 6f), DartsTheme.BarMarker);
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (match == null) return string.Empty;

                if (match.IsOver)
                {
                    return match.Winner == DartSide.Player
                        ? "DRT.Status.WinPlayer".Translate(match.TotalPlayer, match.TotalOpponent).ToString()
                        : "DRT.Status.WinOpponent".Translate(match.TotalOpponent, match.TotalPlayer).ToString();
                }

                switch (stage)
                {
                    case Stage.Aim:
                        return "DRT.Status.Aim".Translate().ToString();
                    case Stage.Power:
                        return "DRT.Status.Power".Translate().ToString();
                    case Stage.Opponent:
                        return "DRT.Status.Opponent".Translate().ToString();
                    default:
                    {
                        if (!hasLast) return string.Empty;

                        bool mine = lastEntry.Side == DartSide.Player;

                        if (lastEntry.Points == 0)
                            return (mine ? "DRT.Status.Miss" : "DRT.Status.MissThem").Translate().ToString();

                        return (mine ? "DRT.Status.Hit" : "DRT.Status.HitThem")
                            .Translate(lastEntry.Code, lastEntry.Points).ToString();
                    }
                }
            }
        }

        public override IReadOnlyList<string> Log
        {
            get
            {
                if (match == null) return log;

                if (match.Log.Count != lastLogCount)
                {
                    lastLogCount = match.Log.Count;
                    log.Clear();
                    for (int i = 0; i < match.Log.Count; i++) log.Add(Format(match.Log[i]));
                }

                return log;
            }
        }

        private string Format(DartEntry entry)
        {
            string side = entry.Side == DartSide.Player
                ? "PR.Side.You".Translate().ToString()
                : "PR.Side.Opponent".Translate().ToString();

            string tail = entry.Points > 0
                ? entry.Code + "  +" + entry.Points
                : "DRT.Miss".Translate().ToString();

            return string.Format("{0,3}  {1}  {2}", entry.Round, side, tail);
        }

        public override void FillTallies(int[] tallies)
        {
            if (match == null || tallies.Length < 3) return;

            tallies[0] = match.Triples;
            tallies[1] = match.Bulls;
            tallies[2] = match.BestVisit;
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            switch (page)
            {
                case 0: DrawFigureBoard(area); break;
                case 1: DrawFigureBars(area); break;
                default: DrawFigureScore(area); break;
            }
        }

        /// <summary>1쪽 - 판. 섹터 숫자와 링이 곧 규칙이다.</summary>
        private void DrawFigureBoard(Rect area)
        {
            float size = Mathf.Min(area.width, area.height) * 0.9f;
            Rect box = new Rect(area.center.x - size / 2f, area.center.y - size / 2f, size, size);

            Widgets.DrawMenuSection(box);

            Vector2 center = box.center;
            float radius = size * 0.38f;
            float textureRadius = radius * DartsTextures.Margin;

            GUI.DrawTexture(new Rect(center.x - textureRadius, center.y - textureRadius,
                textureRadius * 2f, textureRadius * 2f), DartsTextures.Board);

            DrawNumbers(center, radius);
        }

        /// <summary>2쪽 - 멈춰 세운 두 막대. 구간이 가운데 있지 않다는 것까지 보여준다.</summary>
        private void DrawFigureBars(Rect area)
        {
            float y = area.center.y - BarHeight;
            float half = ZoneRadius / AimMath.MaxAxisError * 0.25f;
            float savedSweep = sweep;
            Stage savedStage = stage;

            sweep = 0.63f;
            stage = Stage.Aim;
            DrawBar(new Rect(area.x + 40f, y, area.width - 80f, BarHeight), "THR.Bar.Aim".Translate(),
                    true, 0.62f, half);

            sweep = 0.34f;
            stage = Stage.Power;
            DrawBar(new Rect(area.x + 40f, y + BarHeight + BarGap, area.width - 80f, BarHeight),
                    "THR.Bar.Power".Translate(), true, 0.38f, half);

            sweep = savedSweep;
            stage = savedStage;
        }

        /// <summary>3쪽 - 점수표.</summary>
        private void DrawFigureScore(Rect area)
        {
            Listing_Standard list = new Listing_Standard();
            list.maxOneColumn = true;
            list.Begin(new Rect(area.x + area.width * 0.16f, area.y + 24f, area.width * 0.68f, area.height - 24f));

            list.Label("DRT.Tut.Score.Rings".Translate());
            list.Label("DRT.Tut.Score.Bull".Translate());
            list.Label("DRT.Tut.Score.Rounds".Translate(DartsMatch.BaseRounds, DartsMatch.DartsPerVisit));

            list.End();
        }
    }
}
