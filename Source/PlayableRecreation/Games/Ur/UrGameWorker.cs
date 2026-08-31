using System.Collections.Generic;
using PlayableRecreation;
using PlayableRecreation.UI;
using RoyalGameOfUr.AI;
using RoyalGameOfUr.Core;
using UnityEngine;
using Verse;

namespace RoyalGameOfUr
{
    /// <summary>
    /// 우르의 게임. 판과 주사위와 상대는 전부 여기 있다 - 프레임워크는 이 안을 들여다보지 않는다.
    /// </summary>
    public class UrGameWorker : MiniGameWorker
    {
        private const float DiceRowHeight = 42f;
        private const float BotRollDelay = 0.55f;
        private const float PassRevealDelay = 0.9f;
        private const float AutoAdvanceDelay = 0.45f;

        private UrMatch match;
        private IUrAi bot;

        /// <summary>직전 내 차례가 시작된 지점. 무르기는 여기로 되감는다.</summary>
        private UrTurnMark? undoTarget;

        private Side lastTurn;

        /// <summary>직전 한 수. 상대가 무엇을 했는지 판 위에 남겨 두는 데만 쓴다.</summary>
        private UrTrail trail;
        private float nextBotActionTime;
        private float pendingPassTime;
        private float pendingAutoMoveTime;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        private UrDifficulty Difficulty
        {
            get { return UrDifficultyInfo.Clamp(Tier); }
        }

        /// <summary>난이도가 높을수록 생각하는 연출을 짧게 - 빠른 두뇌.</summary>
        private float BotMoveDelay
        {
            get { return Mathf.Max(0f, PRMod.Settings.botThinkSeconds - 0.08f * Tier); }
        }

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return match != null ? match.TurnCount : 0; }
        }

        public override int Rounds
        {
            get { return match != null ? match.TurnCount : 0; }
        }

        public override bool IsOver
        {
            get { return match != null && match.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return match != null && match.Winner == Side.Player; }
        }

        /// <summary>한 번도 잡히지 않고 거둔 승리.</summary>
        public override bool Flawless
        {
            get { return match != null && match.Captures(Side.Bot) == 0; }
        }

        public override bool CanUndo
        {
            get { return match != null && !match.IsOver && undoTarget.HasValue; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            // 선공 랜덤은 게임 난수를 건드리지 않도록 매치 시드에서 뽑는다.
            Side first = UrSettings.RandomFirstPlayer && (seed & 1) == 0 ? Side.Bot : Side.Player;

            match = new UrMatch(seed, first);
            bot = UrDifficultyInfo.Create(Difficulty, new System.Random(seed ^ 0x5F3759DF));

            undoTarget = null;
            ResetTiming();
        }

        public override void Resume(MiniGameSaveData data)
        {
            UrSaveData saved = data as UrSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            match = saved.ToMatch();
            bot = UrDifficultyInfo.Create(Difficulty, new System.Random(match.Seed ^ 0x5F3759DF));

            undoTarget = null;
            ResetTiming();
        }

        public override MiniGameSaveData MakeSaveData()
        {
            return match != null && !match.IsOver ? new UrSaveData(match) : null;
        }

        private void ResetTiming()
        {
            lastTurn = match.Turn;
            trail = UrTrail.None;
            nextBotActionTime = 0f;
            pendingPassTime = 0f;
            pendingAutoMoveTime = 0f;
            lastLogCount = -1;
        }

        // ---------- 진행 ----------

        public override void Tick(float now)
        {
            if (match == null || match.IsOver) return;

            if (match.Turn != lastTurn)
            {
                lastTurn = match.Turn;
                nextBotActionTime = now + BotRollDelay;
                pendingPassTime = 0f;
                pendingAutoMoveTime = 0f;
            }

            if (match.Turn == Side.Bot) UpdateBot(now);
            else UpdatePlayer(now);
        }

        private void UpdateBot(float now)
        {
            if (now < nextBotActionTime) return;

            switch (match.Phase)
            {
                case UrPhase.AwaitingRoll:
                    RollNow();
                    nextBotActionTime = now + BotMoveDelay;
                    break;

                case UrPhase.MustPass:
                    PassNow();
                    nextBotActionTime = now + BotRollDelay;
                    break;

                case UrPhase.AwaitingMove:
                    int index = bot.ChooseMove(in match.State, match.Roll.Total,
                                               match.LegalMoves, match.LegalCount);
                    PlayLegalMove(index);
                    nextBotActionTime = now + BotRollDelay;
                    break;
            }
        }

        private void UpdatePlayer(float now)
        {
            // 둘 수 없는 눈은 잠깐 보여준 뒤 자동으로 넘긴다.
            if (match.Phase == UrPhase.MustPass)
            {
                if (pendingPassTime <= 0f) pendingPassTime = now + PassRevealDelay;
                else if (now >= pendingPassTime) { PassNow(); pendingPassTime = 0f; }
                return;
            }

            if (match.Phase == UrPhase.AwaitingMove
                && UrSettings.AutoAdvanceSingleMove
                && match.LegalCount == 1)
            {
                if (pendingAutoMoveTime <= 0f) pendingAutoMoveTime = now + AutoAdvanceDelay;
                else if (now >= pendingAutoMoveTime) { PlayLegalMove(0); pendingAutoMoveTime = 0f; }
            }
        }

        private void RollNow()
        {
            if (match.Phase != UrPhase.AwaitingRoll) return;
            match.RollDice();
            PRSounds.Play(UrSounds.Roll);
        }

        private void PassNow()
        {
            if (match.Phase != UrPhase.MustPass) return;
            match.Pass();
            PRSounds.Play(UrSounds.Pass);
        }

        private void PlayLegalMove(int index)
        {
            if (match.Phase != UrPhase.AwaitingMove) return;
            if (index < 0 || index >= match.LegalCount) return;

            bool byPlayer = match.Turn == Side.Player;
            UrTurnMark mark = match.MarkTurnStart();

            // 적용하면 합법수 목록이 비워지므로 사운드 판정용으로 미리 붙잡아 둔다.
            UrMove move = match.LegalMoves[index];
            match.PlayMove(index);

            trail = new UrTrail
            {
                Has = true,
                Side = byPlayer ? Side.Player : Side.Bot,
                From = move.From,
                To = move.To,
            };

            if (byPlayer) undoTarget = mark;

            if (move.IsCapture) PRSounds.Play(UrSounds.Capture);
            else if (move.IsBearOff) PRSounds.Play(UrSounds.BearOff);
            else if (move.GrantsExtraTurn) PRSounds.Play(UrSounds.Rosette);
            else PRSounds.Play(UrSounds.Move);
        }

        public override void Undo()
        {
            if (!CanUndo) return;

            match.RewindTo(undoTarget.Value);
            undoTarget = null;

            // 무른 수는 없던 일이 된다. 자취만 남겨 두면 있지도 않은 수를 가리킨다.
            trail = UrTrail.None;

            PRSounds.Play(UrSounds.Pass);
            ResetTiming();
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (Event.current.type != EventType.KeyDown) return;
            if (Event.current.keyCode != KeyCode.Space) return;
            if (match == null || match.IsOver) return;
            if (match.Turn != Side.Player || match.Phase != UrPhase.AwaitingRoll) return;

            RollNow();
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get
            {
                return ActionEnabled
                    ? "RGU.Btn.Roll".Translate().ToString()
                    : "RGU.Btn.Waiting".Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get
            {
                return match != null && !match.IsOver
                       && match.Turn == Side.Player && match.Phase == UrPhase.AwaitingRoll;
            }
        }

        public override void DoAction()
        {
            RollNow();
        }

        public override void DrawPlayArea(Rect area)
        {
            Rect boardArea = new Rect(area.x, area.y, area.width, area.height - DiceRowHeight);
            Rect diceRect = new Rect(area.x, boardArea.yMax, area.width, DiceRowHeight);

            bool interactive = !match.IsOver && match.Turn == Side.Player;

            int clicked = UrBoardRenderer.Draw(boardArea, match, interactive, trail);
            if (clicked >= 0) PlayLegalMove(clicked);

            UrDiceWidget.Draw(diceRect, match.Roll, match.RollRevealed);
        }

        public override void DoSettings(Listing_Standard list)
        {
            UrSettings.DoSettings(list);
        }

        public override void FillTallies(int[] tallies)
        {
            if (match == null || tallies.Length < 3) return;

            tallies[0] = match.Captures(Side.Player);
            tallies[1] = match.Captures(Side.Bot);
            tallies[2] = match.RosetteLandings(Side.Player);
        }

        // ---------- 문자열 ----------

        public override string StatusText
        {
            get
            {
                if (match == null) return string.Empty;

                if (match.IsOver)
                {
                    return match.Winner == Side.Player
                        ? "RGU.Status.WinPlayer".Translate().ToString()
                        : "RGU.Status.WinBot".Translate().ToString();
                }

                if (match.Turn == Side.Bot) return "RGU.Status.BotThinking".Translate().ToString();

                switch (match.Phase)
                {
                    case UrPhase.AwaitingRoll:
                        return "RGU.Status.Roll".Translate().ToString();
                    case UrPhase.MustPass:
                        return "RGU.Status.NoMove".Translate(match.Roll.Total).ToString();
                    case UrPhase.AwaitingMove:
                        return "RGU.Status.PickMove".Translate(match.Roll.Total, match.LegalCount).ToString();
                    default:
                        return string.Empty;
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
                    for (int i = 0; i < match.Log.Count; i++) log.Add(FormatLog(match.Log[i]));
                }

                return log;
            }
        }

        private static string FormatLog(UrLogEntry entry)
        {
            string side = entry.Side == Side.Player
                ? "PR.Side.You".Translate().ToString()
                : "PR.Side.Opponent".Translate().ToString();

            if (entry.Passed)
                return string.Format("{0,3}  {1}  {2}  {3}",
                    entry.Turn, side, entry.Roll, "RGU.Log.Pass".Translate().ToString());

            string from = entry.Move.IsEntry
                ? "RGU.Log.Entry".Translate().ToString()
                : entry.Move.From.ToString();

            string to = entry.Move.IsBearOff
                ? "RGU.Log.BearOff".Translate().ToString()
                : entry.Move.To.ToString();

            string tag = string.Empty;
            if (entry.Move.IsCapture) tag = "  " + "RGU.Log.Capture".Translate().ToString();
            else if (entry.Move.GrantsExtraTurn) tag = "  " + "RGU.Log.Extra".Translate().ToString();

            return string.Format("{0,3}  {1}  {2}  {3}->{4}{5}", entry.Turn, side, entry.Roll, from, to, tag);
        }

        // ---------- 튜토리얼 도식 ----------

        private const float DiagramCell = 40f;

        private static readonly Color Focus = new Color(0.30f, 0.46f, 0.34f);
        private static readonly Color FocusStrong = new Color(0.34f, 0.58f, 0.38f);
        private static readonly Color Danger = new Color(0.45f, 0.22f, 0.20f);
        private static readonly Color SafeTint = new Color(0.24f, 0.34f, 0.46f);

        public override void DrawTutorialFigure(Rect area, int page)
        {
            switch (page)
            {
                case 0: DrawGoal(area); break;
                case 1: DrawPath(area); break;
                case 2: DrawDiceChart(area); break;
                case 3: DrawRosettes(area); break;
                case 4: DrawCapture(area); break;
                default: DrawBearOff(area); break;
            }
        }

        /// <summary>1쪽 - 판 전체와 양쪽 말 모양.</summary>
        private static void DrawGoal(Rect area)
        {
            Dictionary<UrCell, Side> pieces = new Dictionary<UrCell, Side>();
            pieces[UrBoardLayout.CellOf(Side.Player, 2)] = Side.Player;
            pieces[UrBoardLayout.CellOf(Side.Player, 6)] = Side.Player;
            pieces[UrBoardLayout.CellOf(Side.Bot, 3)] = Side.Bot;
            pieces[UrBoardLayout.CellOf(Side.Bot, 10)] = Side.Bot;

            UrBoardRenderer.DrawDiagram(area, DiagramCell, null, pieces, null);
        }

        /// <summary>2쪽 - 내 경로 1~14 를 번호와 함께.</summary>
        private static void DrawPath(Rect area)
        {
            Dictionary<UrCell, Color> tint = new Dictionary<UrCell, Color>();
            for (int i = 1; i <= UrBoardLayout.PathLength; i++)
                tint[UrBoardLayout.CellOf(Side.Player, i)] = UrBoardLayout.IsRosette(i) ? FocusStrong : Focus;

            UrBoardRenderer.DrawDiagram(area, DiagramCell, tint, null, Side.Player);
        }

        /// <summary>3쪽 - 주사위 눈 분포 막대.</summary>
        private static void DrawDiceChart(Rect area)
        {
            const float rowHeight = 30f;
            const float labelWidth = 40f;
            const float countWidth = 62f;

            float height = UrDice.DiceCount * rowHeight + rowHeight;
            float y = area.y + Mathf.Max(0f, (area.height - height) / 2f);
            float x = area.x + area.width * 0.18f;
            float width = area.width * 0.64f;

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(x, y, width, rowHeight), "RGU.Tut.DiceChart".Translate());
            GUI.color = Color.white;
            y += rowHeight;

            float barMax = width - labelWidth - countWidth;

            for (int roll = 0; roll <= UrDice.DiceCount; roll++)
            {
                float p = (float)UrDice.Probability[roll];
                Rect row = new Rect(x, y + roll * rowHeight, width, rowHeight);

                Widgets.Label(new Rect(row.x, row.y, labelWidth, rowHeight), roll.ToString());

                Rect bar = new Rect(row.x + labelWidth, row.y + 7f, barMax * (p / 0.375f), rowHeight - 14f);
                Widgets.DrawBoxSolid(bar, roll == 0 ? UrTheme.EmptySlot : FocusStrong);

                GUI.color = PRTheme.Dim;
                Widgets.Label(new Rect(row.xMax - countWidth, row.y, countWidth, rowHeight),
                    (p * 100f).ToString("0.0") + "%");
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>4쪽 - 로제트 5칸.</summary>
        private static void DrawRosettes(Rect area)
        {
            Dictionary<UrCell, Color> tint = new Dictionary<UrCell, Color>();
            foreach (UrCell cell in UrBoardLayout.RosetteCells) tint[cell] = FocusStrong;

            UrBoardRenderer.DrawDiagram(area, DiagramCell, tint, null, null);
        }

        /// <summary>5쪽 - 공유 구간, 안전칸, 잡기 한 수.</summary>
        private static void DrawCapture(Rect area)
        {
            Dictionary<UrCell, Color> tint = new Dictionary<UrCell, Color>();
            for (int i = UrBoardLayout.SharedFirst; i <= UrBoardLayout.SharedLast; i++)
                tint[UrBoardLayout.CellOf(Side.Player, i)] = Danger;

            tint[UrBoardLayout.CellOf(Side.Player, UrBoardLayout.SafeIndex)] = SafeTint;

            Dictionary<UrCell, Side> pieces = new Dictionary<UrCell, Side>();
            pieces[UrBoardLayout.CellOf(Side.Player, 5)] = Side.Player;
            pieces[UrBoardLayout.CellOf(Side.Bot, 7)] = Side.Bot;
            pieces[UrBoardLayout.CellOf(Side.Bot, UrBoardLayout.SafeIndex)] = Side.Bot;

            UrBoardRenderer.DrawDiagram(area, DiagramCell, tint, pieces, null);
        }

        /// <summary>6쪽 - 마지막 두 칸과 정확한 눈.</summary>
        private static void DrawBearOff(Rect area)
        {
            Dictionary<UrCell, Color> tint = new Dictionary<UrCell, Color>();
            tint[UrBoardLayout.CellOf(Side.Player, 13)] = Focus;
            tint[UrBoardLayout.CellOf(Side.Player, 14)] = FocusStrong;

            Dictionary<UrCell, Side> pieces = new Dictionary<UrCell, Side>();
            pieces[UrBoardLayout.CellOf(Side.Player, 14)] = Side.Player;
            pieces[UrBoardLayout.CellOf(Side.Player, 13)] = Side.Player;

            UrBoardRenderer.DrawDiagram(area, DiagramCell, tint, pieces, Side.Player);
        }
    }
}
