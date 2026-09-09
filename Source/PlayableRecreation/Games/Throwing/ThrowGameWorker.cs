using System.Collections.Generic;
using PlayableRecreation;
using PlayableRecreation.UI;
using Throwing.Core;
using UnityEngine;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 편자막대와 후프스톤. 판도 주사위도 없고, 좌우와 세기를 맞춰 던진다.
    ///
    /// 우르와 아무것도 공유하지 않는데도 같은 창에 들어간다는 것이
    /// 프레임워크가 턴이나 주사위를 개념으로 갖고 있지 않다는 증거다.
    /// </summary>
    public class ThrowGameWorker : MiniGameWorker
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

        private ThrowRules rules;
        private ThrowMatch match;

        private Stage stage;
        private float sweep;
        private float sweepDirection = 1f;
        private float lastTime;
        private float stageUntil;

        private float lockedLateral;
        private float lastDistance = -1f;
        private bool lastWasRinger;

        /// <summary>이번 발의 정타 구간. [0]이 좌우, [1]이 세기.</summary>
        private readonly float[] zoneCenter = new float[2];
        private readonly float[] zoneHalf = new float[2];

        /// <summary>방금 던진 쪽. 결과만 띄우면 그것이 누구 것인지 알 수 없다.</summary>
        private ThrowSide lastThrower;

        /// <summary>이번 이닝에 떨어진 것들.</summary>
        private readonly List<Landed> landed = new List<Landed>();
        private int drawnInning = -1;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        private struct Landed
        {
            public ThrowSide Side;

            /// <summary>말뚝 기준 착지점(게임 단위, +x 오른쪽 · +y 아래). 크기가 곧 잰 거리다.</summary>
            public Vector2 Offset;

            /// <summary>착지 방향. 링거를 말뚝에 씌울 때의 회전과 자리에 쓴다.</summary>
            public float Angle;

            public bool Ringer;
        }

        private ThrowRulesExtension Extension
        {
            get { return def.GetModExtension<ThrowRulesExtension>(); }
        }

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return match != null ? match.Inning : 0; }
        }

        public override int Rounds
        {
            get { return match != null ? match.Inning : 0; }
        }

        public override bool IsOver
        {
            get { return match != null && match.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return match != null && match.IsOver && match.Winner == ThrowSide.Player; }
        }

        /// <summary>상대가 한 점도 못 낸 승리.</summary>
        public override bool Flawless
        {
            get { return match != null && match.ScoreOpponent == 0; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            rules = Extension != null ? Extension.ToRules() : ThrowRules.Horseshoes();
            match = new ThrowMatch(rules, seed, ThrowSide.Player);

            ResetRound();
        }

        public override void Resume(MiniGameSaveData data)
        {
            rules = Extension != null ? Extension.ToRules() : ThrowRules.Horseshoes();

            ThrowSaveData saved = data as ThrowSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            match = saved.ToMatch(rules);
            ResetRound();
        }

        public override MiniGameSaveData MakeSaveData()
        {
            return match != null && !match.IsOver ? new ThrowSaveData(match) : null;
        }

        private void ResetRound()
        {
            landed.Clear();
            drawnInning = match.Inning;

            lastDistance = -1f;
            lastWasRinger = false;
            sweep = 0f;
            sweepDirection = 1f;
            lastTime = 0f;
            lastLogCount = -1;

            stage = match.Turn == ThrowSide.Opponent ? Stage.Opponent : Stage.Aim;
            stageUntil = 0f;

            RollZones();
            RestoreLanded();
        }

        /// <summary>
        /// 이번 발의 초록 구간을 뽑는다. (시드, 순번)에서 나오므로
        /// 창을 닫았다 열어도 같은 자리다 - 마음에 안 드는 구간을 무를 길은 없다.
        /// </summary>
        private void RollZones()
        {
            for (int axis = 0; axis < 2; axis++)
            {
                zoneHalf[axis] = ThrowAim.ZoneHalf(match.Seed, match.ThrowIndex, axis, Tier, rules.RingerRadius);
                zoneCenter[axis] = ThrowAim.ZoneCenter(match.Seed, match.ThrowIndex, axis, Tier, zoneHalf[axis]);
            }
        }

        /// <summary>
        /// 이어서 연 판이면 이번 이닝에 이미 떨어진 것들이 있다.
        /// 저장되는 것은 거리뿐이라 방향은 (시드, 순번)에서 결정론으로 다시 뽑는다.
        /// </summary>
        private void RestoreLanded()
        {
            IReadOnlyList<ThrowEntry> entries = match.Log;

            int first = entries.Count;
            while (first > 0 && entries[first - 1].Inning == match.Inning) first--;

            int index = match.ThrowIndex - (entries.Count - first);

            for (int i = first; i < entries.Count; i++)
            {
                float angle = ThrowAim.Uniform(match.Seed, index * 7 + 3) * Mathf.PI * 2f;

                landed.Add(new Landed
                {
                    Side = entries[i].Side,
                    Offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * entries[i].Distance,
                    Angle = angle,
                    Ringer = entries[i].Ringer,
                });

                index++;
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
                    if (now >= stageUntil) NextThrower(now);
                    break;

                case Stage.Opponent:
                    if (stageUntil <= 0f) stageUntil = now + Mathf.Max(0.2f, PRMod.Settings.botThinkSeconds);
                    else if (now >= stageUntil) OpponentThrow(now);
                    break;
            }
        }

        private void Sweep(float delta)
        {
            sweep += sweepDirection * ThrowAim.BarSpeedFor(Tier) * delta;

            if (sweep >= 1f) { sweep = 1f; sweepDirection = -1f; }
            else if (sweep <= 0f) { sweep = 0f; sweepDirection = 1f; }
        }

        /// <summary>막대를 멈춘다. 초록 구간에서 벗어난 만큼이 그대로 오차가 된다.</summary>
        private void Lock(float now)
        {
            int axis = stage == Stage.Aim ? 0 : 1;
            float error = ThrowAim.AxisError(sweep, zoneCenter[axis], zoneHalf[axis], rules.RingerRadius);

            if (stage == Stage.Aim)
            {
                lockedLateral = error;
                stage = Stage.Power;
                sweep = 0f;
                sweepDirection = 1f;
                PRSounds.Play(ThrowSounds.Lock);
                return;
            }

            // 조준 오차는 좌우로, 세기 오차는 앞뒤로 - 세게 던지면 말뚝 너머(위)에 떨어진다.
            Resolve(ThrowAim.Distance(lockedLateral, error), new Vector2(lockedLateral, -error), now);
        }

        private void OpponentThrow(float now)
        {
            float distance = ThrowAim.BotThrow(match.Seed, match.ThrowIndex, ThrowAim.SigmaFor(Tier));
            float angle = ThrowAim.Uniform(match.Seed, match.ThrowIndex * 7 + 3) * Mathf.PI * 2f;

            Resolve(distance, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, now);
        }

        private void Resolve(float distance, Vector2 offset, float now)
        {
            ThrowSide thrower = match.Turn;
            bool ringer = distance <= rules.RingerRadius;

            landed.Add(new Landed
            {
                Side = thrower,
                Offset = offset,
                Angle = ThrowAim.Uniform(match.Seed, match.ThrowIndex * 7 + 3) * Mathf.PI * 2f,
                Ringer = ringer,
            });

            lastDistance = distance;
            lastWasRinger = ringer;
            lastThrower = thrower;

            match.Throw(distance);

            PRSounds.Play(lastWasRinger ? ThrowSounds.Ringer : ThrowSounds.Land);

            stage = Stage.Reveal;
            stageUntil = now + RevealSeconds;
        }

        private void NextThrower(float now)
        {
            if (match.IsOver) return;

            // 이닝이 넘어갔으면 이제 과녁을 치운다 — 마지막 한 발을 보여준 뒤다.
            if (match.Inning != drawnInning)
            {
                landed.Clear();
                drawnInning = match.Inning;
            }

            sweep = 0f;
            sweepDirection = 1f;
            stageUntil = 0f;
            stage = match.Turn == ThrowSide.Opponent ? Stage.Opponent : Stage.Aim;

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
                if (stage == Stage.Aim) return "THR.Btn.LockAim".Translate().ToString();
                if (stage == Stage.Power) return "THR.Btn.Throw".Translate().ToString();
                return "THR.Btn.Waiting".Translate().ToString();
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
            if (ActionEnabled) Lock(Time.realtimeSinceStartup);
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            float barsHeight = BarHeight * 2f + BarGap;

            DrawScoreboard(new Rect(area.x, area.y, area.width, ScoreboardHeight));

            Rect target = new Rect(area.x, area.y + ScoreboardHeight, area.width,
                                   area.height - ScoreboardHeight - barsHeight - BarGap);
            DrawTarget(target);

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
            GUI.color = ThrowTheme.PlayerMark;
            Widgets.Label(new Rect(row.x, row.y, row.width / 2f, row.height), match.ScorePlayer.ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = ThrowTheme.OpponentMark;
            Widgets.Label(new Rect(row.center.x, row.y, row.width / 2f, row.height),
                match.ScoreOpponent.ToString());

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = PRTheme.Dim;
            Widgets.Label(row, "THR.Scoreboard".Translate(match.Inning, rules.TargetScore));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>위에서 내려다본 과녁. 가운데가 막대(또는 고리)이고 등고선 두 개가 점수 경계다.</summary>
        private void DrawTarget(Rect area)
        {
            float size = Mathf.Min(area.width, area.height);
            Rect box = new Rect(area.center.x - size / 2f, area.center.y - size / 2f, size, size);

            Widgets.DrawMenuSection(box);

            Vector2 center = box.center;
            float outer = rules.ScoreRadius * 1.9f;
            float scale = size * 0.46f / outer;

            DrawCircle(center, rules.ScoreRadius * scale, ThrowTheme.ScoreRing);
            DrawCircle(center, rules.RingerRadius * scale, ThrowTheme.RingerRing);

            // 한가운데의 막대 또는 고리.
            float pin = Mathf.Max(6f, rules.RingerRadius * scale * 0.55f);
            GUI.color = ThrowTheme.Pin;
            GUI.DrawTexture(new Rect(center.x - pin / 2f, center.y - pin / 2f, pin, pin),
                rules.PerThrowScoring ? PRTextures.Ring : PRTextures.Dot);
            GUI.color = Color.white;

            // 편자는 편자 모양으로, 후프스톤의 돌은 점으로.
            bool shoes = !rules.PerThrowScoring;
            float mark = Mathf.Max(8f, size * 0.045f) * (shoes ? 1.35f : 1f);

            for (int i = 0; i < landed.Count; i++)
            {
                Landed item = landed[i];

                Vector2 offset = item.Offset;
                if (offset.magnitude > outer) offset *= outer / offset.magnitude;

                Vector2 point;
                float facing;

                if (item.Ringer)
                {
                    // 꽂힌 것은 잰 거리와 상관없이 막대의 것이다.
                    // 편자는 말뚝을 감싸고, 후프스톤의 돌은 고리에 걸린 자리에 놓는다.
                    Vector2 direction = new Vector2(Mathf.Cos(item.Angle), Mathf.Sin(item.Angle));
                    point = center + (shoes ? Vector2.zero : direction * pin * 0.45f);
                    facing = item.Angle;
                }
                else
                {
                    point = center + offset * scale;
                    // 말뚝을 향해 미끄러져 온 것처럼, 트인 쪽이 말뚝을 본다.
                    facing = Mathf.Atan2(center.y - point.y, center.x - point.x);
                }

                Rect spot = new Rect(point.x - mark / 2f, point.y - mark / 2f, mark, mark);

                GUI.color = item.Side == ThrowSide.Player ? ThrowTheme.PlayerMark : ThrowTheme.OpponentMark;
                GUI.DrawTexture(spot, shoes
                    ? ThrowTextures.For(item.Side == ThrowSide.Player, facing)
                    : item.Side == ThrowSide.Player ? PRTextures.Dot : PRTextures.Ring);
            }

            GUI.color = Color.white;
        }

        private static void DrawCircle(Vector2 center, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                PRTextures.Outline);
            GUI.color = Color.white;
        }

        /// <summary>좌우 · 세기 막대. 초록 구간에 가까울수록 잘 던진 것이다.</summary>
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
            Widgets.DrawBoxSolid(bar, ThrowTheme.BarBack);

            // 좁은 초록 구간이 정타다. 발마다 옮겨 다닌다.
            Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * (center - half), bar.y,
                bar.width * half * 2f, bar.height), ThrowTheme.BarSweet);

            if (!live) return;

            float x = bar.x + bar.width * sweep;
            Widgets.DrawBoxSolid(new Rect(x - 2f, bar.y - 3f, 4f, bar.height + 6f), ThrowTheme.BarMarker);
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (match == null) return string.Empty;

                if (match.IsOver)
                {
                    return match.Winner == ThrowSide.Player
                        ? "THR.Status.WinPlayer".Translate(match.ScorePlayer, match.ScoreOpponent).ToString()
                        : "THR.Status.WinOpponent".Translate(match.ScoreOpponent, match.ScorePlayer).ToString();
                }

                switch (stage)
                {
                    case Stage.Aim:
                        return "THR.Status.Aim".Translate().ToString();
                    case Stage.Power:
                        return "THR.Status.Power".Translate().ToString();
                    case Stage.Opponent:
                        return "THR.Status.Opponent".Translate().ToString();
                    default:
                    {
                        if (lastDistance < 0f) return string.Empty;

                        bool mine = lastThrower == ThrowSide.Player;

                        if (lastWasRinger)
                            return (mine ? "THR.Status.Ringer" : "THR.Status.RingerThem")
                                .Translate(RingerName).ToString();

                        return (mine ? "THR.Status.Landed" : "THR.Status.LandedThem")
                            .Translate(lastDistance.ToString("0.00")).ToString();
                    }
                }
            }
        }

        private string RingerName
        {
            get
            {
                ThrowRulesExtension extension = Extension;
                return extension != null ? extension.ringerKey.Translate().ToString() : string.Empty;
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

        private string Format(ThrowEntry entry)
        {
            string side = entry.Side == ThrowSide.Player
                ? "PR.Side.You".Translate().ToString()
                : "PR.Side.Opponent".Translate().ToString();

            string tail = entry.Ringer
                ? RingerName
                : entry.Distance.ToString("0.00");

            if (entry.Points > 0) tail = tail + "  +" + entry.Points;

            return string.Format("{0,3}  {1}  {2}", entry.Inning, side, tail);
        }

        public override void FillTallies(int[] tallies)
        {
            if (match == null || tallies.Length < 3) return;

            tallies[0] = match.Ringers;
            tallies[1] = match.ScorePlayer;
            tallies[2] = match.ScoreOpponent;
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (rules == null) rules = Extension != null ? Extension.ToRules() : ThrowRules.Horseshoes();

            switch (page)
            {
                case 0: DrawFigureTarget(area); break;
                case 1: DrawFigureBars(area); break;
                default: DrawFigureScore(area); break;
            }
        }

        /// <summary>1쪽 - 과녁과 두 등고선.</summary>
        private void DrawFigureTarget(Rect area)
        {
            float size = Mathf.Min(area.width, area.height) * 0.9f;
            Rect box = new Rect(area.center.x - size / 2f, area.center.y - size / 2f, size, size);

            Widgets.DrawMenuSection(box);

            Vector2 center = box.center;
            float scale = size * 0.46f / (rules.ScoreRadius * 1.9f);

            DrawCircle(center, rules.ScoreRadius * scale, ThrowTheme.ScoreRing);
            DrawCircle(center, rules.RingerRadius * scale, ThrowTheme.RingerRing);

            float pin = Mathf.Max(6f, rules.RingerRadius * scale * 0.55f);
            GUI.color = ThrowTheme.Pin;
            GUI.DrawTexture(new Rect(center.x - pin / 2f, center.y - pin / 2f, pin, pin),
                rules.PerThrowScoring ? PRTextures.Ring : PRTextures.Dot);
            GUI.color = Color.white;
        }

        /// <summary>2쪽 - 멈춰 세운 두 막대. 구간이 가운데 있지 않다는 것까지 보여준다.</summary>
        private void DrawFigureBars(Rect area)
        {
            float y = area.center.y - BarHeight;
            float half = rules.RingerRadius / ThrowAim.MaxAxisError * 0.25f;
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

            ThrowRulesExtension extension = Extension;
            string ringer = extension != null ? extension.ringerKey.Translate().ToString() : string.Empty;

            list.Label("THR.Tut.Score.Ringer".Translate(ringer, rules.RingerPoints));
            list.Label((rules.PerThrowScoring ? "THR.Tut.Score.Rim" : "THR.Tut.Score.Near")
                .Translate(rules.NearPoints));
            list.Label("THR.Tut.Score.Target".Translate(rules.TargetScore, rules.ThrowsPerInning));

            list.End();
        }
    }
}
