using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 창 껍데기. 판 자체는 워커가 그리고, 이 창은 그 바깥의 모든 것을 맡는다 —
    /// 머리글, 진행 기록, 큰 버튼, 도구 줄, 상태 줄, 그리고 습격이 왔을 때 판을 끊는 일.
    ///
    /// 연출 타이밍은 실시간 기준으로 돈다. 게임이 멈춰 있어도 동작한다.
    /// </summary>
    public class Dialog_MiniGame : Window
    {
        private const float HeaderHeight = 58f;
        private const float FooterHeight = 34f;
        private const float LogWidth = 250f;
        private const float ButtonHeight = 38f;
        private const float ToolRowHeight = 30f;
        private const float Pad = 12f;
        private const float LogLineHeight = 20f;

        /// <summary>위협 감시 주기(실시간 초). 매 프레임 맵을 훑을 필요는 없다.</summary>
        private const float ThreatCheckInterval = 0.5f;

        private readonly MiniGameDef game;
        private readonly Thing board;
        private readonly Pawn seatedPawn;
        private readonly int tier;

        /// <summary>연습 판은 숙련도로도 전적으로도 인정하지 않는다 - 낮은 단계를 반복해 깨는 길을 막는다.</summary>
        private readonly bool practice;

        private readonly MiniGameWorker worker;
        private GameSession session;

        private bool matchFinished;
        private bool resigned;
        private int undosUsed;

        private float openedAt;
        private int lastSavePoint = int.MinValue;

        private float nextThreatCheck;
        private bool threatAtOpen;

        private Vector2 logScroll;
        private int lastLogCount;

        public override Vector2 InitialSize
        {
            get { return game.windowSize; }
        }

        /// <summary>새 판.</summary>
        public Dialog_MiniGame(MiniGameDef game, Thing board, Pawn seatedPawn, int tier, bool practice)
        {
            this.game = game;
            this.board = board;
            this.seatedPawn = seatedPawn;
            this.tier = game.ClampTier(tier);
            this.practice = practice;

            Configure();

            worker = game.MakeWorker();
            worker.Bind(board, seatedPawn, this.tier, practice);
            worker.StartNew(NewSeed());
        }

        /// <summary>가구에 남겨둔 판을 이어 한다.</summary>
        public Dialog_MiniGame(GameSession resumed)
        {
            session = resumed;
            game = resumed.game;
            board = resumed.board;
            seatedPawn = resumed.seatedPawn;
            tier = game.ClampTier(resumed.tier);
            practice = resumed.practice;
            undosUsed = resumed.undosUsed;

            Configure();

            worker = game.MakeWorker();
            worker.Bind(board, seatedPawn, tier, practice);
            worker.Resume(resumed.data);
        }

        private void Configure()
        {
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;   // 뒤쪽 콜로니 화면을 계속 볼 수 있게
            preventCameraMotion = false;
            draggable = true;
            forcePause = PRMod.Settings.pauseWhilePlaying;
        }

        private static int NewSeed()
        {
            int seed = Environment.TickCount;
            if (Find.TickManager != null) seed ^= Find.TickManager.TicksGame * 7919;
            return seed;
        }

        // ---------- 창 수명 ----------

        /// <summary>처음 여는 사람에게는 규칙 안내를 먼저 한 번 보여준다.</summary>
        public override void PreOpen()
        {
            base.PreOpen();

            openedAt = Time.realtimeSinceStartup;

            // 열 때 이미 싸우고 있었다면 그 위협으로는 끊지 않는다. 새로 닥친 것만 판을 끊는다.
            threatAtOpen = ThreatPresent();
            nextThreatCheck = openedAt + ThreatCheckInterval;

            if (game.tutorialPages > 0 && !Dialog_Tutorial.SeenFor(game))
                Find.WindowStack.Add(new Dialog_Tutorial(game, null));
        }

        public override void PostClose()
        {
            base.PostClose();

            AccumulatePlayTime();

            // 이긴 그 프레임에 바로 창을 닫아도 기록은 남아야 한다.
            if (game.hasMatch && worker != null && worker.IsOver) FinishMatch();

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return;

            component.ActiveSession = null;

            // 끝났거나 기권한 판은 남기지 않는다. 하던 판은 가구 위에 그대로 놓아둔다.
            if (worker == null || worker.IsOver || matchFinished) component.Remove(session);
            else SaveSession(component);
        }

        private void AccumulatePlayTime()
        {
            if (session == null) return;

            session.realSeconds += Mathf.Max(0f, Time.realtimeSinceStartup - openedAt);
            openedAt = Time.realtimeSinceStartup;
        }

        /// <summary>판을 가구 위에 놓아둔다. 저장 지점은 게임이 정한다.</summary>
        private void SaveSession(GameComponent_Recreation component)
        {
            if (component == null || worker == null || worker.IsOver) return;

            if (!PRMod.Settings.saveSessions || !game.supportsSave)
            {
                component.Remove(session);
                session = null;
                return;
            }

            if (board == null) return;

            if (session == null)
                session = new GameSession(board, seatedPawn, game, tier, practice);

            session.undosUsed = undosUsed;
            session.CaptureFrom(worker);

            if (session.data == null) return;   // 저장을 지원하지 않는 게임

            component.Register(session);
        }

        // ---------- 습격이 판을 끊는다 ----------

        private bool ThreatPresent()
        {
            Map map = board != null ? board.Map : Find.CurrentMap;
            return map != null && GenHostility.AnyHostileActiveThreatToPlayer(map);
        }

        /// <summary>
        /// 시간이 흐르는 동안 적대 위협이 나타나면 창을 닫는다.
        /// 이것이 이 모드의 그 순간이자, 동시에 시간을 멈추지 않고도 콜로니를 지키는 안전장치다.
        /// </summary>
        private void CheckThreat(float now)
        {
            if (now < nextThreatCheck) return;
            nextThreatCheck = now + ThreatCheckInterval;

            if (!ThreatPresent())
            {
                threatAtOpen = false;      // 싸움이 끝났다. 다음 습격은 판을 끊는다.
                return;
            }

            if (threatAtOpen) return;

            bool kept = PRMod.Settings.saveSessions && game.supportsSave && board != null;
            TaggedString message = (kept ? "PR.Interrupted.Saved" : "PR.Interrupted.Lost").Translate();

            if (board != null) Messages.Message(message, board, MessageTypeDefOf.ThreatBig, false);
            else Messages.Message(message, MessageTypeDefOf.ThreatBig, false);

            Close();
        }

        // ---------- 진행 ----------

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (worker == null) return;

            // 판은 Tick 에서 끝날 수도, 판을 클릭하는 순간 끝날 수도 있다.
            // 정리는 어느 쪽이든 다음 프레임의 여기서 한 번만 일어난다.
            if (game.hasMatch && worker.IsOver) { FinishMatch(); return; }

            float now = Time.realtimeSinceStartup;

            // 시간이 멈춰 있으면 습격이 생길 수도 없으므로 검사하지 않는다.
            if (!forcePause) CheckThreat(now);

            worker.Tick(now);
            TrackSavePoint();
        }

        /// <summary>게임이 알려준 안전한 지점마다 가구 위의 판을 갱신해 둔다. 별도 저장 버튼은 없다.</summary>
        private void TrackSavePoint()
        {
            int point = worker.SavePoint;
            if (point == lastSavePoint) return;

            lastSavePoint = point;

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return;

            SaveSession(component);
            component.ActiveSession = session;
        }

        // ---------- 그리기 ----------

        public override void DoWindowContents(Rect inRect)
        {
            worker.HandleShortcuts();

            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight));

            float bodyTop = inRect.y + HeaderHeight + Pad;
            float bodyHeight = inRect.height - HeaderHeight - Pad - FooterHeight;

            IReadOnlyList<string> log = game.showLog ? worker.Log : null;
            float playWidth = inRect.width;

            if (log != null)
            {
                DrawLog(new Rect(inRect.xMax - LogWidth, bodyTop, LogWidth, bodyHeight), log);
                playWidth = inRect.width - LogWidth - Pad;
            }

            DrawBody(new Rect(inRect.x, bodyTop, playWidth, bodyHeight));
            DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight));
        }

        private void DrawHeader(Rect header)
        {
            string title = game.LabelCap;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(header.x, header.y, header.width - 40f, 32f), title);
            Text.Font = GameFont.Small;

            if (practice)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = PRTheme.Paused;
                Widgets.Label(new Rect(header.x + Text.CalcSize(title).x + 24f, header.y, 160f, 32f),
                    "PR.Window.Practice".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect line = new Rect(header.x, header.y + 32f, header.width, 24f);

            string you = seatedPawn != null
                ? "PR.Header.YouNamed".Translate(seatedPawn.LabelShortCap).ToString()
                : "PR.Header.You".Translate().ToString();

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = PRTheme.Dim;
            Widgets.Label(line, you);

            Text.Anchor = TextAnchor.MiddleRight;
            if (game.hasMatch) Widgets.Label(line, "PR.Header.Opponent".Translate(game.TierLabel(tier)));

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawBody(Rect area)
        {
            float controls = ButtonHeight + ToolRowHeight + Pad;
            Rect play = new Rect(area.x, area.y, area.width, area.height - controls);
            Rect buttonRow = new Rect(area.x, play.yMax + Pad, area.width, ButtonHeight);
            Rect toolRow = new Rect(area.x, buttonRow.yMax + 4f, area.width, ToolRowHeight - 4f);

            worker.DrawPlayArea(play);
            DrawActionButton(buttonRow);
            DrawToolRow(toolRow);
        }

        private void DrawActionButton(Rect row)
        {
            const float width = 230f;
            Rect button = new Rect(row.x + (row.width - width) / 2f, row.y, width, row.height);

            if (game.hasMatch && worker.IsOver)
            {
                if (Widgets.ButtonText(button, "PR.Btn.NewMatch".Translate())) StartAnotherMatch();
                return;
            }

            string label = worker.ActionLabel;
            if (label.NullOrEmpty()) return;

            if (worker.ActionEnabled)
            {
                if (Widgets.ButtonText(button, label)) worker.DoAction();
                return;
            }

            GUI.color = PRTheme.Dim;
            Widgets.ButtonText(button, label, true, false, false);
            GUI.color = Color.white;
        }

        /// <summary>무르기 · 재시도 · 기권 · 기록 · 도움말.</summary>
        private void DrawToolRow(Rect row)
        {
            const float gap = 6f;
            float x = row.x;

            if (game.hasMatch && !worker.IsOver)
            {
                if (game.supportsUndo
                    && SmallButton(ref x, row, "PR.Btn.Undo".Translate(), UndoAvailable, UndoTooltip()))
                    UndoNow();

                if (SmallButton(ref x, row, "PR.Btn.Retry".Translate(), RetryAvailable, RetryTooltip()))
                    ConfirmRetry();

                if (SmallButton(ref x, row, "PR.Btn.Resign".Translate(), true, "PR.Btn.Resign.Tip".Translate()))
                    ConfirmResign();
            }

            float right = row.xMax;

            if (game.tutorialPages > 0)
            {
                Rect help = new Rect(right - row.height, row.y, row.height, row.height);
                right = help.x - gap;

                TooltipHandler.TipRegion(help, "PR.Btn.Help.Tip".Translate());
                if (Widgets.ButtonText(help, "?")) Find.WindowStack.Add(new Dialog_Tutorial(game, null));
            }

            if (!game.hasMatch) return;

            Rect records = new Rect(right - 90f, row.y, 90f, row.height);
            TooltipHandler.TipRegion(records, "PR.Btn.Records.Tip".Translate());
            if (Widgets.ButtonText(records, "PR.Btn.Records".Translate()))
                Find.WindowStack.Add(new Dialog_Leaderboard(game));
        }

        private static bool SmallButton(ref float x, Rect row, string label, bool enabled, string tooltip)
        {
            const float width = 88f;
            const float gap = 6f;

            Rect button = new Rect(x, row.y, width, row.height);
            x += width + gap;

            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(button, tooltip);

            if (enabled) return Widgets.ButtonText(button, label);

            GUI.color = PRTheme.Dim;
            Widgets.ButtonText(button, label, true, false, false);
            GUI.color = Color.white;
            return false;
        }

        private void DrawLog(Rect area, IReadOnlyList<string> log)
        {
            Widgets.DrawMenuSection(area);
            Rect inner = area.ContractedBy(8f);

            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 20f), "PR.Log.Title".Translate());
            GUI.color = Color.white;

            Rect view = new Rect(inner.x, inner.y + 22f, inner.width, inner.height - 22f);
            Rect content = new Rect(0f, 0f, view.width - 18f, log.Count * LogLineHeight + 4f);

            if (log.Count != lastLogCount)
            {
                lastLogCount = log.Count;
                logScroll.y = Mathf.Max(0f, content.height - view.height);
            }

            Widgets.BeginScrollView(view, ref logScroll, content);
            for (int i = 0; i < log.Count; i++)
                Widgets.Label(new Rect(0f, i * LogLineHeight, content.width, LogLineHeight), log[i]);
            Widgets.EndScrollView();

            Text.Font = GameFont.Small;
        }

        private void DrawFooter(Rect footer)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = worker.IsOver || resigned ? PRTheme.ActiveTurn : Color.white;
            Widgets.Label(new Rect(footer.x, footer.y, footer.width - 200f, footer.height),
                resigned ? "PR.Status.Resigned".Translate().ToString() : worker.StatusText);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = forcePause ? PRTheme.Paused : PRTheme.Running;
            Widgets.Label(new Rect(footer.xMax - 190f, footer.y, 190f, footer.height),
                (forcePause ? "PR.Window.Paused" : "PR.Window.Running").Translate());
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ---------- 무르기 · 재시도 · 기권 ----------

        private bool UndoAvailable
        {
            get
            {
                if (worker.IsOver || !worker.CanUndo) return false;
                if (practice) return true;
                if (PRMod.Settings.noUndoOnHardDifficulty && tier >= 3) return false;
                return undosUsed < PRMod.Settings.undoLimit;
            }
        }

        private string UndoTooltip()
        {
            if (practice) return "PR.Btn.Undo.Practice".Translate();

            if (PRMod.Settings.noUndoOnHardDifficulty && tier >= 3)
                return "PR.Btn.Undo.Blocked".Translate();

            return "PR.Btn.Undo.Tip".Translate(
                Mathf.Max(0, PRMod.Settings.undoLimit - undosUsed), PRMod.Settings.undoLimit);
        }

        private void UndoNow()
        {
            if (!UndoAvailable) return;

            worker.Undo();
            if (!practice) undosUsed++;

            PRSounds.Play(PRSounds.Reject);
            lastSavePoint = worker.SavePoint;
        }

        /// <summary>
        /// 재시도는 게임 내 하루 한 번. 연습 판은 제한이 없다.
        /// 쿨다운은 세션이 아니라 GameComponent 가 들고 있다 - 세션 저장을 꺼도 제한이 유지되어야 한다.
        /// </summary>
        private bool RetryAvailable
        {
            get
            {
                if (worker.IsOver) return false;
                if (practice) return true;

                GameComponent_Recreation component = GameComponent_Recreation.Current;
                return component == null || component.RetryReady(game);
            }
        }

        private string RetryTooltip()
        {
            return RetryAvailable ? "PR.Btn.Retry.Tip".Translate() : "PR.Btn.Retry.Cooldown".Translate();
        }

        private void ConfirmRetry()
        {
            if (!RetryAvailable) return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "PR.Confirm.Retry".Translate(), RetryNow, false));
        }

        private void RetryNow()
        {
            // 버린 판은 기권패로 남는다 - 불리해질 때마다 처음부터 다시 하는 길을 막는다.
            RecordResult(false, true);

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component != null && !practice) component.MarkRetry(game);

            undosUsed = 0;
            resigned = false;
            matchFinished = false;
            lastSavePoint = int.MinValue;
            lastLogCount = 0;

            worker.StartNew(NewSeed());
        }

        private void ConfirmResign()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "PR.Confirm.Resign".Translate(), ResignNow, false));
        }

        private void ResignNow()
        {
            if (worker.IsOver) return;

            resigned = true;
            matchFinished = true;
            RecordResult(false, true);

            PRSounds.Play(PRSounds.Lose);
            DropSession();
            Close();
        }

        private void StartAnotherMatch()
        {
            Close(false);

            if (practice) Find.WindowStack.Add(new Dialog_MiniGame(game, board, seatedPawn, tier, true));
            else GameEntry.StartNew(game, board, seatedPawn);
        }

        private void DropSession()
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return;

            component.Remove(session);
            session = null;
        }

        // ---------- 판이 끝났다 ----------

        private void FinishMatch()
        {
            if (matchFinished) return;
            matchFinished = true;

            bool won = worker.PlayerWon;
            PRSounds.Play(won ? PRSounds.Win : PRSounds.Lose);

            RecordResult(won, false);
            DropSession();

            if (!won || practice) return;

            // 이 난이도를 처음 깼다면 숙련도가 한 단계 오른다.
            Mastery.RecordClear(game, tier);

            if (game.wonTale != null && seatedPawn != null && board != null)
                TaleRecorder.RecordTale(game.wonTale, seatedPawn, board.def);
        }

        private void RecordResult(bool won, bool byResignation)
        {
            if (!game.hasMatch || practice || worker == null || worker.Rounds <= 1) return;

            AccumulatePlayTime();

            float seconds = session != null ? session.realSeconds : Time.realtimeSinceStartup - openedAt;
            MatchResult result = worker.BuildResult(won, byResignation, undosUsed, seconds);

            RecordStore.Record(game, in result);

            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component != null) component.ColonyRecord(game).Record(in result);

            if (session != null) session.realSeconds = 0f;
        }
    }
}
