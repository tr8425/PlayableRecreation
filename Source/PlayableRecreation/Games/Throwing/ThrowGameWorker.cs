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

        /// <summary>방금 던진 쪽. 결과만 띄우면 그것이 누구 것인지 알 수 없다.</summary>
        private ThrowSide lastThrower;

        /// <summary>이번 이닝에 떨어진 것들. 거리만 저장되므로 각도는 여기서 붙인다.</summary>
        private readonly List<Landed> landed = new List<Landed>();
        private int drawnInning = -1;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        private struct Landed
        {
            public ThrowSide Side;
            public float Distance;
            public float Angle;
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

            RestoreLanded();
        }

        /// <summary>
        /// 이어서 연 판이면 이번 이닝에 이미 떨어진 것들이 있다.
        /// 각도는 저장하지 않는다 - (시드, 순번)에서 나오므로 다시 뽑으면 같은 자리에 놓인다.
        /// </summary>
        private void RestoreLanded()
        {
            IReadOnlyList<ThrowEntry> entries = match.Log;

            int first = entries.Count;
            while (first > 0 && entries[first - 1].Inning == match.Inning) first--;

            int index = match.ThrowIndex - (entries.Count - first);

            for (int i = first; i < entries.Count; i++)
            {
                landed.Add(new Landed
                {
                    Side = entries[i].Side,
                    Distance = entries[i].Distance,
                    Angle = ThrowAim.Uniform(match.Seed, index * 7 + 3) * Mathf.PI * 2f,
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

        /// <summary>막대를 멈춘다. 가운데에서 벗어난 만큼이 그대로 오차가 된다.</summary>
        private void Lock(float now)
        {
            float error = (sweep - 0.5f) * 2f * ThrowAim.MaxAxisError;

            if (stage == Stage.Aim)
            {
                lockedLateral = error;
                stage = Stage.Power;
                sweep = 0f;
                sweepDirection = 1f;
                PRSounds.Play(ThrowSounds.Lock);
                return;
            }

            Resolve(ThrowAim.Distance(lockedLateral, error), now);
        }

        private void OpponentThrow(float now)
        {
            float distance = ThrowAim.BotThrow(match.Seed, match.ThrowIndex, ThrowAim.SigmaFor(Tier));
            Resolve(distance, now);
        }

        private void Resolve(float distance, float now)
        {
            ThrowSide thrower = match.Turn;

            landed.Add(new Landed
            {
                Side = thrower,
                Distance = distance,
                Angle = ThrowAim.Uniform(match.Seed, match.ThrowIndex * 7 + 3) * Mathf.PI * 2f,
            });

            lastDistance = distance;
            lastWasRinger = distance <= rules.RingerRadius;
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
            DrawBar(new Rect(area.x, y, area.width, BarHeight), "THR.Bar.Aim".Translate(), stage == Stage.Aim);
            DrawBar(new Rect(area.x, y + BarHeight + BarGap, area.width, BarHeight),
                    "THR.Bar.Power".Translate(), stage == Stage.Power);
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

            float mark = Mathf.Max(8f, size * 0.045f);

            for (int i = 0; i < landed.Count; i++)
            {
                Landed item = landed[i];
                float distance = Mathf.Min(item.Distance, outer) * scale;

                Vector2 point = center + new Vector2(Mathf.Cos(item.Angle), Mathf.Sin(item.Angle)) * distance;

                GUI.color = item.Side == ThrowSide.Player ? ThrowTheme.PlayerMark : ThrowTheme.OpponentMark;
                GUI.DrawTexture(new Rect(point.x - mark / 2f, point.y - mark / 2f, mark, mark),
                    item.Side == ThrowSide.Player ? PRTextures.Dot : PRTextures.Ring);
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

        /// <summary>좌우 · 세기 막대. 가운데에 가까울수록 잘 던진 것이다.</summary>
        private void DrawBar(Rect row, string label, bool live)
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

            // 가운데의 좁은 구간이 정타다.
            float sweet = bar.width * (rules.RingerRadius / ThrowAim.MaxAxisError) * 0.5f;
            Widgets.DrawBoxSolid(new Rect(bar.center.x - sweet / 2f, bar.y, sweet, bar.height), ThrowTheme.BarSweet);

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

        /// <summary>2쪽 - 멈춰 세운 두 막대.</summary>
        private void DrawFigureBars(Rect area)
        {
            float y = area.center.y - BarHeight;
            float savedSweep = sweep;
            Stage savedStage = stage;

            sweep = 0.5f;
            stage = Stage.Aim;
            DrawBar(new Rect(area.x + 40f, y, area.width - 80f, BarHeight), "THR.Bar.Aim".Translate(), true);

            sweep = 0.32f;
            stage = Stage.Power;
            DrawBar(new Rect(area.x + 40f, y + BarHeight + BarGap, area.width - 80f, BarHeight),
                    "THR.Bar.Power".Translate(), true);

            sweep = savedSweep;
            stage = savedStage;
        }

        /// <summary>3쪽 - 점수표.</summary>
        private void DrawFigureScore(Rect area)
        {
            Listing_Standard list = new Listing_Standard();
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
