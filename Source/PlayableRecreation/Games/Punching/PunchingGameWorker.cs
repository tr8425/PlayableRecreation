using System.Collections.Generic;
using PlayableRecreation;
using PlayableRecreation.UI;
using Punching.Core;
using UnityEngine;
using Verse;

namespace Punching
{
    /// <summary>
    /// 펀칭백. 상대가 없다 - 겨루는 것은 박자다.
    ///
    /// 백이 두 박 흔들리며 부르면 정박에 주먹으로 답한다. 빨리 치는 것은 답이 아니다 -
    /// 창을 벗어난 주먹은 헛스윙이고, 헛스윙은 콤보를 끊는다.
    ///
    /// 판은 실시간 한 세트로 끝나므로 가구에 남겨둘 것이 없다 (supportsSave=false).
    /// </summary>
    public class PunchingGameWorker : MiniGameWorker
    {
        private const float ScoreboardHeight = 46f;
        private const float TapeHeight = 46f;
        private const float TapeGap = 10f;

        /// <summary>화면에 미리 보여주는 시간(초). 이 폭이 곧 눈으로 읽는 악보다.</summary>
        private const float TapeAhead = 2.2f;

        private PunchTrack track;

        /// <summary>박자는 플레이어가 시작 버튼을 누른 뒤에야 흐른다 - 튜토리얼이 위에 떠 있는
        /// 동안 세트가 몰래 시작되면 안 된다.</summary>
        private bool armed;

        private float startTime = -1f;
        private float songTime;
        private float lastNow;
        private float avgDelta = 1f / 60f;

        private int nextCue;

        private NoteJudge lastJudge = NoteJudge.Pending;
        private float judgeUntil;
        private float impactTime = -999f;
        private float wobbleTime = -999f;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return track != null ? track.JudgedCount : 0; }
        }

        public override int Rounds
        {
            get { return track != null ? track.JudgedCount : 0; }
        }

        public override bool IsOver
        {
            get { return track != null && track.Done(songTime); }
        }

        public override bool PlayerWon
        {
            get { return track != null && track.Score >= track.TargetScore; }
        }

        /// <summary>미스도 헛스윙도 없는 세트.</summary>
        public override bool Flawless
        {
            get { return track != null && track.Flawless; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            track = PunchTrack.Generate(seed, Tier);

            armed = false;
            startTime = -1f;
            songTime = 0f;
            lastNow = 0f;
            nextCue = 0;
            lastJudge = NoteJudge.Pending;
            judgeUntil = 0f;
            impactTime = -999f;
            wobbleTime = -999f;
        }

        /// <summary>실시간 한 세트라 저장이 없다. 여기 왔다면 무언가 꼬인 것이니 새로 친다.</summary>
        public override void Resume(MiniGameSaveData data)
        {
            StartNew(Rand.Int);
        }

        // ---------- 진행 ----------

        public override void Tick(float now)
        {
            if (track == null) return;

            if (lastNow > 0f) avgDelta = Mathf.Lerp(avgDelta, Mathf.Min(0.1f, now - lastNow), 0.1f);
            lastNow = now;

            if (!armed) return;

            songTime = now - startTime;

            // 끝을 판정하기 전에 남은 발부터 정산한다. 프레임이 크게 튀어 세트 끝을
            // 한 번에 지나가 버리면, 안 친 발들이 판정되지 않은 채로 남아 완봉이 된다.
            track.AdvanceMisses(songTime, Slack);

            if (track.Done(songTime)) return;

            // 콜 박마다 낮은 소리. 너무 늦게 온 프레임에는 소리를 걸러야 귀가 안 헷갈린다.
            IReadOnlyList<float> cues = track.Cues;
            while (nextCue < cues.Count && cues[nextCue] * track.BeatSeconds <= songTime)
            {
                if (songTime - cues[nextCue] * track.BeatSeconds < 0.15f) PRSounds.Play(PunchingSounds.Cue);
                nextCue++;
            }
        }

        private float Slack
        {
            get { return Mathf.Clamp(avgDelta * 0.5f, 0f, 0.06f); }
        }

        private void Punch(float now)
        {
            if (track == null || track.Done(songTime)) return;

            // 첫 주먹은 시작 신호다. 박자가 여기서부터 흐른다.
            if (!armed)
            {
                armed = true;
                startTime = now;
                return;
            }

            // 첫 콜이 오기 전의 주먹은 준비 자세로 친다. 벌하지 않는다.
            if (songTime < PunchTrack.LeadInBeats * track.BeatSeconds - 0.5f) return;

            NoteJudge judge = track.RegisterHit(songTime, Slack);

            lastJudge = judge;
            judgeUntil = now + 0.5f;

            if (judge == NoteJudge.Miss)
            {
                wobbleTime = now;
                PRSounds.Play(PunchingSounds.Whiff);
                return;
            }

            impactTime = now;
            PRSounds.Play(PunchingSounds.Hit);
            if (track.LastHitKo) PRSounds.Play(PunchingSounds.Ko);
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (!PRKeys.ActionPressed()) return;
            if (!ActionEnabled) return;

            Punch(Time.realtimeSinceStartup);
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get
            {
                return (armed ? "PBG.Btn.Punch" : "PBG.Btn.Start").Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get { return track != null && !track.Done(songTime); }
        }

        public override void DoAction()
        {
            if (ActionEnabled) Punch(Time.realtimeSinceStartup);
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            if (track == null) return;

            DrawScoreboard(new Rect(area.x, area.y, area.width, ScoreboardHeight));

            Rect bagArea = new Rect(area.x, area.y + ScoreboardHeight, area.width,
                                    area.height - ScoreboardHeight - TapeHeight - TapeGap);
            DrawBag(bagArea);

            DrawTape(new Rect(area.x, area.yMax - TapeHeight, area.width, TapeHeight));
        }

        private void DrawScoreboard(Rect row)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = PunchingTheme.Perfect;
            Widgets.Label(new Rect(row.x, row.y, row.width / 2f, row.height), track.Score.ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(row.center.x, row.y, row.width / 2f, row.height),
                "PBG.Scoreboard.Target".Translate(track.TargetScore).ToString());

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = track.Combo > 0 ? PunchingTheme.Perfect : PRTheme.Dim;
            Widgets.Label(row, "PBG.Scoreboard.Combo".Translate(track.Combo).ToString());

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawBag(Rect area)
        {
            Widgets.DrawMenuSection(area);

            float now = Time.realtimeSinceStartup;

            // 클릭도 주먹이다. 버튼까지 내려갈 필요 없다.
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && area.Contains(Event.current.mousePosition) && ActionEnabled)
            {
                Punch(now);
                Event.current.Use();
            }

            Vector2 pivot = new Vector2(area.center.x, area.y + 14f);
            float ropeLength = Mathf.Min(90f, area.height * 0.22f);
            float bagWidth = 84f;
            float bagHeight = Mathf.Min(150f, area.height * 0.42f);

            // 백은 박자에 맞춰 흔들린다. 이 흔들림이 소리 없는 메트로놈이다.
            float phase = songTime / track.BeatSeconds;
            float sway = Mathf.Sin(phase * Mathf.PI) * 16f;

            // 주먹이 꽂히면 밀려나고, 헛스윙이면 민망하게 떨린다.
            float sinceImpact = now - impactTime;
            if (sinceImpact < 0.25f) sway += 30f * (1f - sinceImpact / 0.25f);

            float sinceWobble = now - wobbleTime;
            if (sinceWobble < 0.3f) sway += Mathf.Sin(sinceWobble * 60f) * 5f * (1f - sinceWobble / 0.3f);

            Vector2 bagTop = new Vector2(pivot.x + sway, pivot.y + ropeLength);

            Widgets.DrawLine(pivot, bagTop, PunchingTheme.Rope, 3f);

            Rect bag = new Rect(bagTop.x - bagWidth / 2f, bagTop.y, bagWidth, bagHeight);
            GUI.color = PunchingTheme.Bag;
            GUI.DrawTexture(bag, PRTextures.Dot);
            GUI.color = PunchingTheme.BagSeam;
            Widgets.DrawBoxSolid(new Rect(bag.x + 8f, bag.y + bagHeight * 0.28f, bagWidth - 16f, 2f),
                PunchingTheme.BagSeam);
            Widgets.DrawBoxSolid(new Rect(bag.x + 8f, bag.y + bagHeight * 0.72f, bagWidth - 16f, 2f),
                PunchingTheme.BagSeam);
            GUI.color = Color.white;

            // 판정 글자. 반 박쯤 떠 있다가 사라진다.
            if (now < judgeUntil && lastJudge != NoteJudge.Pending)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = lastJudge == NoteJudge.Perfect ? PunchingTheme.Perfect
                    : lastJudge == NoteJudge.Good ? PunchingTheme.Good : PunchingTheme.Miss;

                string word = lastJudge == NoteJudge.Perfect ? "PBG.Judge.Perfect".Translate().ToString()
                    : lastJudge == NoteJudge.Good ? "PBG.Judge.Good".Translate().ToString()
                    : "PBG.Judge.Miss".Translate().ToString();

                Widgets.Label(new Rect(area.center.x - 100f, bag.y - 34f, 200f, 28f), word);

                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /// <summary>다가오는 박의 띠. 오른쪽에서 와서 왼쪽 선에 닿는 순간이 정박이다.</summary>
        private void DrawTape(Rect area)
        {
            Widgets.DrawBoxSolid(area, PunchingTheme.TapeBack);

            float hitX = area.x + area.width * 0.18f;
            float perSecond = area.width * 0.75f / TapeAhead;

            // 콜 박은 흐릿한 눈금으로.
            IReadOnlyList<float> cues = track.Cues;
            for (int i = 0; i < cues.Count; i++)
            {
                float x = hitX + (cues[i] * track.BeatSeconds - songTime) * perSecond;
                if (x < area.x || x > area.xMax) continue;

                Widgets.DrawBoxSolid(new Rect(x - 1f, area.y + area.height * 0.3f, 2f, area.height * 0.4f),
                    PunchingTheme.TapeCue);
            }

            // 쳐야 할 박.
            IReadOnlyList<PunchNote> notes = track.Notes;
            for (int i = 0; i < notes.Count; i++)
            {
                PunchNote note = notes[i];
                if (note.Judge == NoteJudge.Perfect || note.Judge == NoteJudge.Good) continue;

                float x = hitX + (note.Beat * track.BeatSeconds - songTime) * perSecond;
                if (x < area.x - 20f || x > area.xMax + 20f) continue;

                float size = note.Ko ? 20f : 12f;
                GUI.color = note.Judge == NoteJudge.Miss ? PunchingTheme.Miss
                    : note.Ko ? PunchingTheme.NoteKo : PunchingTheme.Note;
                GUI.DrawTexture(new Rect(x - size / 2f, area.center.y - size / 2f, size, size), PRTextures.Dot);
            }

            GUI.color = Color.white;

            // 정박 선은 눈금 위에 그린다.
            Widgets.DrawBoxSolid(new Rect(hitX - 1.5f, area.y + 4f, 3f, area.height - 8f), PunchingTheme.TapeLine);
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (track == null) return string.Empty;

                if (track.Done(songTime))
                {
                    return (track.Score >= track.TargetScore ? "PBG.Status.Win" : "PBG.Status.Lose")
                        .Translate(track.Score, track.TargetScore).ToString();
                }

                if (!armed) return "PBG.Status.Armed".Translate().ToString();

                if (songTime < PunchTrack.LeadInBeats * track.BeatSeconds - 0.2f)
                    return "PBG.Status.Ready".Translate().ToString();

                return "PBG.Status.Playing".Translate().ToString();
            }
        }

        public override void FillTallies(int[] tallies)
        {
            if (track == null || tallies.Length < 3) return;

            tallies[0] = track.MaxCombo;
            tallies[1] = track.Perfects;
            tallies[2] = track.Score;
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            switch (page)
            {
                case 0: DrawFigureBag(area); break;
                case 1: DrawFigureTape(area); break;
                default: DrawFigureScore(area); break;
            }
        }

        /// <summary>1쪽 - 백. 흔들림이 부르고 주먹이 답한다.</summary>
        private void DrawFigureBag(Rect area)
        {
            Vector2 pivot = new Vector2(area.center.x, area.y + 20f);
            Vector2 bagTop = new Vector2(pivot.x + 14f, pivot.y + 60f);

            Widgets.DrawLine(pivot, bagTop, PunchingTheme.Rope, 3f);

            Rect bag = new Rect(bagTop.x - 38f, bagTop.y, 76f, 120f);
            GUI.color = PunchingTheme.Bag;
            GUI.DrawTexture(bag, PRTextures.Dot);
            GUI.color = Color.white;
        }

        /// <summary>2쪽 - 띠. 점이 선에 닿는 순간이 정박이다.</summary>
        private void DrawFigureTape(Rect area)
        {
            Rect tape = new Rect(area.x + 30f, area.center.y - TapeHeight / 2f, area.width - 60f, TapeHeight);
            Widgets.DrawBoxSolid(tape, PunchingTheme.TapeBack);

            float hitX = tape.x + tape.width * 0.18f;
            Widgets.DrawBoxSolid(new Rect(hitX - 1.5f, tape.y + 4f, 3f, tape.height - 8f), PunchingTheme.TapeLine);

            float[] offsets = { 0f, 0.28f, 0.52f, 0.64f, 0.76f, 0.88f };
            for (int i = 0; i < offsets.Length; i++)
            {
                bool ko = i == offsets.Length - 1;
                float size = ko ? 20f : 12f;
                float x = hitX + tape.width * 0.72f * offsets[i];

                GUI.color = ko ? PunchingTheme.NoteKo : PunchingTheme.Note;
                GUI.DrawTexture(new Rect(x - size / 2f, tape.center.y - size / 2f, size, size), PRTextures.Dot);
            }

            GUI.color = Color.white;
        }

        /// <summary>3쪽 - 점수표.</summary>
        private void DrawFigureScore(Rect area)
        {
            Listing_Standard list = new Listing_Standard();
            list.maxOneColumn = true;
            list.Begin(new Rect(area.x + area.width * 0.16f, area.y + 24f, area.width * 0.68f, area.height - 24f));

            list.Label("PBG.Tut.Score.Judge".Translate(PunchTrack.PerfectScore, PunchTrack.GoodScore));
            list.Label("PBG.Tut.Score.Ko".Translate(PunchTrack.KoMultiplier));
            list.Label("PBG.Tut.Score.Target".Translate());

            list.End();
        }
    }
}
