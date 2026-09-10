using System.Collections.Generic;
using Billiards.Core;
using PlayableRecreation;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Billiards
{
    /// <summary>
    /// 나인볼. 규칙이 아니라 물리로 굴러가는 첫 게임이다 -
    /// 프레임워크가 매 프레임 주는 <c>Tick</c> 안에서 공을 굴리고, 상대의 조준도 거기서 나눠 계산한다.
    /// </summary>
    public class BilliardsGameWorker : MiniGameWorker
    {
        private enum Stage
        {
            Aim,
            Power,
            Rolling,
            Opponent,
        }

        private const float ScoreHeight = 40f;
        private const float PowerHeight = 24f;
        private const float PowerCycle = 1.45f;

        /// <summary>한 프레임에 재 보는 후보 샷 수. 창이 끊기지 않을 만큼만.</summary>
        private const int PreviewsPerFrame = 2;

        private NineBall match;
        private PoolPlanner planner;

        private Stage stage;
        private float aimAngle;
        private float power;
        private float powerDirection = 1f;
        private float lastTime;
        private float thinkUntil;

        private Rect tableRect;
        private Vector2 pointer;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return match != null ? match.Resolved : 0; }
        }

        public override int Rounds
        {
            get { return match != null ? match.Shots : 0; }
        }

        public override bool IsOver
        {
            get { return match != null && match.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return match != null && match.IsOver && match.Winner == PoolSide.Player; }
        }

        /// <summary>상대가 한 개도 못 넣은 승리.</summary>
        public override bool Flawless
        {
            get { return match != null && match.PocketedBy(PoolSide.Opponent) == 0; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            match = new NineBall(seed, PoolSide.Player);
            Reset();
            BeginTurn(0f);
        }

        public override void Resume(MiniGameSaveData data)
        {
            BilliardsSaveData saved = data as BilliardsSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            match = saved.ToMatch();
            Reset();
            BeginTurn(0f);
        }

        public override MiniGameSaveData MakeSaveData()
        {
            // 굴러가는 중에는 저장하지 않는다. SavePoint 가 안 바뀌므로 여기 오지도 않는다.
            return match != null && !match.IsOver && !match.Moving ? new BilliardsSaveData(match) : null;
        }

        private void Reset()
        {
            planner = null;
            aimAngle = 0f;
            power = 0f;
            powerDirection = 1f;
            lastTime = 0f;
            thinkUntil = 0f;
            lastLogCount = -1;
        }

        // ---------- 진행 ----------

        public override void Tick(float now)
        {
            if (match == null || match.IsOver) return;

            float delta = lastTime <= 0f ? 0f : Mathf.Min(0.05f, now - lastTime);
            lastTime = now;

            switch (stage)
            {
                case Stage.Power:
                    Oscillate(delta);
                    break;

                case Stage.Rolling:
                    match.Advance(delta);
                    if (!match.Moving) Settled(now);
                    break;

                case Stage.Opponent:
                    // 뜸들이는 동안 후보 샷을 조금씩 재 본다. 한 프레임에 몰아 하면 창이 끊긴다.
                    if (planner != null && !planner.Done) planner.Step(PreviewsPerFrame);
                    if (now >= thinkUntil && (planner == null || planner.Done)) OpponentShoot();
                    break;
            }
        }

        private void Oscillate(float delta)
        {
            power += powerDirection * delta / PowerCycle * 2f;

            if (power >= 1f) { power = 1f; powerDirection = -1f; }
            else if (power <= 0f) { power = 0f; powerDirection = 1f; }
        }

        /// <summary>공이 멈췄다. 규칙은 이미 적용되었고, 여기서는 다음 차례를 준비한다.</summary>
        private void Settled(float now)
        {
            IReadOnlyList<PoolLogEntry> entries = match.Log;
            if (entries.Count > 0)
            {
                PoolLogEntry last = entries[entries.Count - 1];
                if (last.Foul) PRSounds.Play(BilliardsSounds.Foul);
                else if (last.Pocketed > 0) PRSounds.Play(BilliardsSounds.Pocket);
            }

            BeginTurn(now);
        }

        private void BeginTurn(float now)
        {
            if (match.IsOver) return;

            power = 0f;
            powerDirection = 1f;

            if (match.Turn == PoolSide.Opponent)
            {
                planner = new PoolPlanner(match, Tier, match.Seed ^ (match.Shots * 7919));
                thinkUntil = now + Mathf.Max(0.25f, PRMod.Settings.botThinkSeconds);
                stage = Stage.Opponent;
                return;
            }

            planner = null;
            stage = Stage.Aim;
        }

        private void OpponentShoot()
        {
            Vec2 shot = planner != null ? planner.BestShot : new Vec2(-1.4f, 0f);
            planner = null;

            match.Shoot(shot);
            PRSounds.Play(BilliardsSounds.Strike);
            stage = Stage.Rolling;
        }

        private void PlayerShoot()
        {
            float speed = PoolTable.MaxShotSpeed * (0.18f + 0.82f * power);

            match.Shoot(Vec2.FromAngle(aimAngle, speed));
            PRSounds.Play(BilliardsSounds.Strike);
            stage = Stage.Rolling;
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (!PRKeys.ActionPressed()) return;
            if (!ActionEnabled) return;

            DoAction();
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get
            {
                switch (stage)
                {
                    case Stage.Aim: return "BIL.Btn.Power".Translate().ToString();
                    case Stage.Power: return "BIL.Btn.Strike".Translate().ToString();
                    case Stage.Opponent: return "BIL.Btn.Waiting".Translate().ToString();
                    default: return "BIL.Btn.Rolling".Translate().ToString();
                }
            }
        }

        public override bool ActionEnabled
        {
            get
            {
                return match != null && !match.IsOver && (stage == Stage.Aim || stage == Stage.Power);
            }
        }

        public override void DoAction()
        {
            if (!ActionEnabled) return;

            if (stage == Stage.Aim)
            {
                stage = Stage.Power;
                power = 0f;
                powerDirection = 1f;
                PRSounds.Play(BilliardsSounds.Lock);
                return;
            }

            PlayerShoot();
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            DrawScoreboard(new Rect(area.x, area.y, area.width, ScoreHeight));

            Rect field = new Rect(area.x, area.y + ScoreHeight, area.width,
                                  area.height - ScoreHeight - PowerHeight - 8f);
            LayoutTable(field);

            if (Event.current.type != EventType.Layout) pointer = Event.current.mousePosition;
            if (stage == Stage.Aim) TrackPointer();

            DrawTable();
            DrawBalls();
            if (stage == Stage.Aim || stage == Stage.Power) DrawAim();

            if (stage == Stage.Aim && Widgets.ButtonInvisible(tableRect)) DoAction();

            DrawPower(new Rect(area.x, area.yMax - PowerHeight, area.width, PowerHeight));
        }

        /// <summary>2:1 비율을 지키면서 주어진 자리에 최대한 크게 앉힌다.</summary>
        private void LayoutTable(Rect field)
        {
            float scale = Mathf.Min(field.width / PoolTable.Width, field.height / PoolTable.Height);
            float width = PoolTable.Width * scale;
            float height = PoolTable.Height * scale;

            tableRect = new Rect(field.center.x - width * 0.5f, field.center.y - height * 0.5f, width, height);
        }

        private Vector2 ToScreen(Vec2 point)
        {
            float scale = tableRect.width / PoolTable.Width;
            return new Vector2(tableRect.x + point.X * scale, tableRect.y + point.Y * scale);
        }

        private Vec2 ToTable(Vector2 screen)
        {
            float scale = tableRect.width / PoolTable.Width;
            return new Vec2((screen.x - tableRect.x) / scale, (screen.y - tableRect.y) / scale);
        }

        private float Scale
        {
            get { return tableRect.width / PoolTable.Width; }
        }

        private void TrackPointer()
        {
            Ball cue = match.Balls[0];
            if (cue.Pocketed) return;

            Vec2 delta = ToTable(pointer) - cue.Pos;
            if (delta.SquareLength < 1e-6f) return;

            aimAngle = delta.Angle;
        }

        private void DrawScoreboard(Rect row)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            GUI.color = BilliardsTheme.Numbers[1];
            Widgets.Label(new Rect(row.x, row.y, row.width * 0.5f, row.height),
                match.PocketedBy(PoolSide.Player).ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = BilliardsTheme.Numbers[2];
            Widgets.Label(new Rect(row.center.x, row.y, row.width * 0.5f, row.height),
                match.PocketedBy(PoolSide.Opponent).ToString());

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = PRTheme.Dim;
            Widgets.Label(row, "BIL.Scoreboard".Translate(match.Shots, match.LowestBall));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawTable()
        {
            Rect rail = tableRect.ExpandedBy(Scale * PoolTable.BallRadius * 1.6f);
            Widgets.DrawBoxSolid(rail, BilliardsTheme.Rail);
            Widgets.DrawBoxSolid(tableRect, BilliardsTheme.Felt);

            GUI.color = BilliardsTheme.FeltEdge;
            Widgets.DrawBox(tableRect, 1);
            GUI.color = Color.white;

            float pocket = PoolTable.PocketRadius * Scale;
            GUI.color = BilliardsTheme.Pocket;

            for (int i = 0; i < PoolTable.Pockets.Length; i++)
            {
                Vector2 at = ToScreen(PoolTable.Pockets[i]);
                GUI.DrawTexture(new Rect(at.x - pocket, at.y - pocket, pocket * 2f, pocket * 2f), PRTextures.Dot);
            }

            GUI.color = Color.white;
        }

        private void DrawBalls()
        {
            float radius = PoolTable.BallRadius * Scale;
            int lowest = match.LowestBall;

            for (int i = 0; i < match.Balls.Length; i++)
            {
                Ball ball = match.Balls[i];
                if (ball.Pocketed) continue;

                Vector2 at = ToScreen(ball.Pos);
                Rect box = new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f);

                GUI.color = BilliardsTheme.Numbers[ball.Number];
                GUI.DrawTexture(box, PRTextures.Dot);

                // 이번에 반드시 먼저 맞혀야 하는 공은 테두리로 표시한다.
                if (ball.Number == lowest && lowest > 0)
                {
                    GUI.color = PRTheme.ActiveTurn;
                    GUI.DrawTexture(box.ExpandedBy(radius * 0.45f), PRTextures.Outline);
                }

                GUI.color = Color.white;

                if (ball.Number == 0 || radius < 7f) continue;

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = ball.Number == 8 ? Color.white : new Color(0f, 0f, 0f, 0.75f);
                Widgets.Label(box, ball.Number.ToString());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        /// <summary>조준선과 고스트볼. 어디에 닿을지를 보여줄 뿐, 그 뒤는 물리가 정한다.</summary>
        private void DrawAim()
        {
            Ball cue = match.Balls[0];
            if (cue.Pocketed) return;

            Vec2 direction = Vec2.FromAngle(aimAngle, 1f);
            int hit;
            float distance = PoolSim.Trace(match.Balls, cue.Pos, direction, 0, out hit);

            Vec2 contact = cue.Pos + direction * distance;

            Vector2 from = ToScreen(cue.Pos);
            Vector2 to = ToScreen(contact);

            Widgets.DrawLine(from, to, BilliardsTheme.AimLine, 1.5f);

            float radius = PoolTable.BallRadius * Scale;
            GUI.color = BilliardsTheme.GhostBall;
            GUI.DrawTexture(new Rect(to.x - radius, to.y - radius, radius * 2f, radius * 2f), PRTextures.Dot);
            GUI.color = Color.white;
        }

        private void DrawPower(Rect row)
        {
            Rect label = new Rect(row.x, row.y, 56f, row.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = stage == Stage.Power ? Color.white : PRTheme.Dim;
            Widgets.Label(label, "BIL.Bar.Power".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect bar = new Rect(label.xMax + 6f, row.y + 6f, row.width - label.width - 6f, row.height - 12f);
            Widgets.DrawBoxSolid(bar, BilliardsTheme.PowerBack);

            if (stage != Stage.Power) return;

            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * power, bar.height), BilliardsTheme.PowerFill);
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (match == null) return string.Empty;

                if (match.IsOver)
                {
                    return match.Winner == PoolSide.Player
                        ? "BIL.Status.WinPlayer".Translate().ToString()
                        : "BIL.Status.WinOpponent".Translate().ToString();
                }

                switch (stage)
                {
                    case Stage.Aim:
                        return "BIL.Status.Aim".Translate(match.LowestBall).ToString();
                    case Stage.Power:
                        return "BIL.Status.Power".Translate().ToString();
                    case Stage.Opponent:
                        return "BIL.Status.Opponent".Translate().ToString();
                    default:
                        return "BIL.Status.Rolling".Translate().ToString();
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

        private string Format(PoolLogEntry entry)
        {
            string side = entry.Side == PoolSide.Player ? YouLabel : OpponentLabel;

            string tail;
            if (entry.Scratch) tail = "BIL.Log.Scratch".Translate().ToString();
            else if (entry.Foul) tail = "BIL.Log.Foul".Translate().ToString();
            else if (entry.Pocketed > 0) tail = "BIL.Log.Pocketed".Translate(entry.Pocketed).ToString();
            else tail = "BIL.Log.Miss".Translate().ToString();

            return string.Format("{0,3}  {1}  {2}", entry.Shot, side, tail);
        }

        public override void FillTallies(int[] tallies)
        {
            if (match == null || tallies.Length < 3) return;

            tallies[0] = match.PocketedBy(PoolSide.Player);
            tallies[1] = match.PocketedBy(PoolSide.Opponent);
            tallies[2] = match.Fouls(PoolSide.Player);
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (match == null) match = new NineBall(1, PoolSide.Player);

            LayoutTable(area.ContractedBy(12f));
            DrawTable();

            if (page == 0)
            {
                DrawBalls();
                return;
            }

            if (page == 1)
            {
                // 고스트볼 한 장면. 큐볼에서 1번을 지나 구멍으로 이어지는 선.
                DrawBalls();

                Ball cue = match.Balls[0];
                Ball target = match.Balls[match.LowestBall];
                Vec2 pocket = PoolTable.Pockets[2];

                Vec2 ghost = target.Pos + (target.Pos - pocket).Normalized * (PoolTable.BallRadius * 2f);
                float radius = PoolTable.BallRadius * Scale;

                Widgets.DrawLine(ToScreen(cue.Pos), ToScreen(ghost), BilliardsTheme.AimLine, 1.5f);
                Widgets.DrawLine(ToScreen(target.Pos), ToScreen(pocket), PRTheme.ActiveTurn, 1.5f);

                Vector2 at = ToScreen(ghost);
                GUI.color = BilliardsTheme.GhostBall;
                GUI.DrawTexture(new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f), PRTextures.Dot);
                GUI.color = Color.white;
                return;
            }

            DrawBalls();
        }
    }
}
