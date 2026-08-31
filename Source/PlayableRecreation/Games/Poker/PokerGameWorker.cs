using System.Collections.Generic;
using Poker.Core;
using PlayableRecreation;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Poker
{
    /// <summary>
    /// 일대일 홀덤. 상대는 손의 세기를 공식으로 재지 않고 끝까지 돌려 보는데,
    /// 그 표본을 프레임워크가 주는 <c>Tick</c> 안에서 조금씩 뽑는다 -
    /// 체스가 한 깊이씩 깊어지고 나인볼이 후보 샷을 두 개씩 재 보는 그 자리다.
    /// </summary>
    public class PokerGameWorker : MiniGameWorker
    {
        private enum Stage { Player, Thinking, HandOver }

        /// <summary>한 프레임에 돌려 보는 표본 수. 창이 끊기지 않을 만큼만.</summary>
        private const int RolloutsPerFrame = 90;

        private const float SeatHeight = 96f;
        private const float ControlHeight = 78f;
        private const float CardWidth = 48f;
        private const float CardHeight = 68f;
        private const float HandPause = 2.4f;

        private HoldemMatch match;
        private PokerPlanner planner;

        private Stage stage;
        private float thinkUntil;
        private float handShownUntil;
        private int raiseTo;

        private int peakStack = HoldemMatch.StartingStack;
        private int lowStack = HoldemMatch.StartingStack;
        private int handsWon;
        private int showdowns;

        private readonly List<string> log = new List<string>();
        private int lastLogCount = -1;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return match != null ? match.Hand * 4096 + match.Actions : 0; }
        }

        public override int Rounds
        {
            get { return match != null ? match.Hand : 0; }
        }

        public override bool IsOver
        {
            get { return match != null && match.IsOver; }
        }

        public override bool PlayerWon
        {
            get { return match != null && match.Winner.HasValue && match.Winner.Value == PokerSeat.Player; }
        }

        /// <summary>한 번도 밑돌지 않고 이긴 판. 앞서기 시작해서 그대로 끝냈다는 뜻이다.</summary>
        public override bool Flawless
        {
            get { return lowStack >= HoldemMatch.StartingStack; }
        }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            match = new HoldemMatch(seed);
            Reset();
            BeginTurn(0f);
        }

        public override void Resume(MiniGameSaveData data)
        {
            PokerSaveData saved = data as PokerSaveData;
            if (saved == null) { StartNew(Rand.Int); return; }

            match = saved.ToMatch();
            Reset();

            // 이어서 연 판의 집계는 저장된 것을 쓴다. 다시 세면 지나온 핸드가 통째로 지워진다.
            peakStack = Mathf.Max(saved.peakStack, peakStack);
            handsWon = saved.handsWon;
            showdowns = saved.showdowns;

            // 집계를 남기지 않던 예전 세이브는 여기서 걸린다 - 모르는 것을 무결점으로 쳐 주지 않는다.
            lowStack = Mathf.Min(saved.lowStack, lowStack);

            BeginTurn(0f);
        }

        public override MiniGameSaveData MakeSaveData()
        {
            if (match == null || match.IsOver) return null;

            return new PokerSaveData(match)
            {
                peakStack = peakStack,
                lowStack = lowStack,
                handsWon = handsWon,
                showdowns = showdowns,
            };
        }

        private void Reset()
        {
            planner = null;
            thinkUntil = 0f;
            handShownUntil = 0f;
            lastLogCount = -1;

            int stack = match.Stack(PokerSeat.Player) + match.Bet(PokerSeat.Player);

            peakStack = stack;
            lowStack = Mathf.Min(stack, HoldemMatch.StartingStack);
            handsWon = 0;
            showdowns = 0;
        }

        // ---------- 진행 ----------

        private void BeginTurn(float now)
        {
            if (match.IsOver) return;

            if (match.HandDone)
            {
                stage = Stage.HandOver;
                handShownUntil = now + HandPause;
                if (match.ShowdownReached) showdowns++;
                if (match.HandWinner == PokerSeat.Player) handsWon++;
                return;
            }

            raiseTo = match.CanRaise ? match.MinRaiseTo : 0;

            if (match.ToAct == PokerSeat.Opponent)
            {
                planner = new PokerPlanner(match, Tier, match.Seed ^ (match.Actions * 7919 + match.Hand));
                thinkUntil = now + Mathf.Max(0.25f, PRMod.Settings.botThinkSeconds);
                stage = Stage.Thinking;
                return;
            }

            planner = null;
            stage = Stage.Player;
        }

        public override void Tick(float now)
        {
            if (match == null || match.IsOver) return;

            if (stage == Stage.HandOver)
            {
                if (!PokerSettings.AutoNextHand || now < handShownUntil) return;
                NextHand(now);
                return;
            }

            if (stage != Stage.Thinking) return;

            // 뜸들이는 동안 표본을 조금씩 늘린다. 한 프레임에 몰아 하면 창이 끊긴다.
            if (planner != null && !planner.Done) { planner.Step(RolloutsPerFrame); return; }
            if (now < thinkUntil) return;

            int amount;
            PokerAction action = planner != null ? planner.Decide(out amount) : Fallback(out amount);

            Apply(action, amount, now);
        }

        private PokerAction Fallback(out int amount)
        {
            amount = 0;
            return match.CanCheck ? PokerAction.Check : PokerAction.Fold;
        }

        private void Apply(PokerAction action, int amount, float now)
        {
            match.Act(action, amount);

            switch (action)
            {
                case PokerAction.Fold: PRSounds.Play(PokerSounds.Fold); break;
                case PokerAction.Raise: PRSounds.Play(PokerSounds.Raise); break;
                case PokerAction.Call: PRSounds.Play(PokerSounds.Chips); break;
                default: PRSounds.Play(PokerSounds.Deal); break;
            }

            if (match.HandDone && match.HandWinner == PokerSeat.Player) PRSounds.Play(PokerSounds.Win);

            Track();
            BeginTurn(now);
        }

        private void NextHand(float now)
        {
            match.NextHand();
            Track();
            PRSounds.Play(PokerSounds.Deal);
            BeginTurn(now);
        }

        private void Track()
        {
            int stack = match.Stack(PokerSeat.Player) + match.Bet(PokerSeat.Player);
            if (stack > peakStack) peakStack = stack;
            if (stack < lowStack) lowStack = stack;
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            if (match == null) return;

            Widgets.DrawBoxSolid(area, PokerTheme.Rail);
            Rect felt = area.ContractedBy(6f);
            Widgets.DrawBoxSolid(felt, PokerTheme.Felt);

            Rect inner = felt.ContractedBy(10f);

            Rect top = new Rect(inner.x, inner.y, inner.width, SeatHeight);
            Rect bottom = new Rect(inner.x, inner.yMax - ControlHeight - SeatHeight, inner.width, SeatHeight);
            Rect middle = new Rect(inner.x, top.yMax, inner.width, bottom.y - top.yMax);
            Rect controls = new Rect(inner.x, inner.yMax - ControlHeight, inner.width, ControlHeight);

            DrawSeat(top, PokerSeat.Opponent);
            DrawBoard(middle);
            DrawSeat(bottom, PokerSeat.Player);
            DrawControls(controls);
        }

        private bool OpponentFaceUp
        {
            get
            {
                if (match.ShowdownReached && match.HandDone) return true;
                return PokerSettings.RevealFolded && match.HandDone;
            }
        }

        private void DrawSeat(Rect row, PokerSeat seat)
        {
            bool mine = seat == PokerSeat.Player;
            bool faceUp = mine || OpponentFaceUp;

            // 카드 두 장은 가운데, 스택과 베팅은 양옆에.
            float cardsWidth = CardWidth * 2f + 8f;
            Rect cards = new Rect(row.center.x - cardsWidth * 0.5f,
                                  row.center.y - CardHeight * 0.5f, cardsWidth, CardHeight);

            for (int i = 0; i < 2; i++)
            {
                Rect box = new Rect(cards.x + i * (CardWidth + 8f), cards.y, CardWidth, CardHeight);
                DrawCard(box, match.Hole(seat, i), !faceUp);
            }

            Text.Anchor = TextAnchor.MiddleLeft;

            string who = mine ? "PR.Side.You".Translate().ToString() : "PR.Side.Opponent".Translate().ToString();
            string button = match.Button == seat ? "  " + "POK.Label.Button".Translate() : string.Empty;

            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(row.x, row.y + 4f, 220f, 22f), who + button);
            GUI.color = PokerTheme.Chip;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(row.x, row.y + 26f, 220f, 30f), match.Stack(seat).ToString());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            int bet = match.Bet(seat);
            if (bet > 0)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = PokerTheme.PotChip;
                Widgets.Label(new Rect(row.xMax - 200f, row.y + 26f, 200f, 30f),
                    "POK.Label.Bet".Translate(bet));
                GUI.color = Color.white;
            }

            // 이번 핸드를 가져간 쪽에 표시를 남긴다.
            if (match.HandDone && match.HandWinner == seat)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = PokerTheme.Winner;
                Widgets.Label(new Rect(row.xMax - 200f, row.y + 2f, 200f, 24f), "POK.Label.Took".Translate());
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawBoard(Rect area)
        {
            float width = CardWidth * 5f + 8f * 4f;
            Rect cards = new Rect(area.center.x - width * 0.5f, area.center.y - CardHeight * 0.5f + 8f,
                                  width, CardHeight);

            for (int i = 0; i < 5; i++)
            {
                Rect box = new Rect(cards.x + i * (CardWidth + 8f), cards.y, CardWidth, CardHeight);

                if (i < match.BoardCount) DrawCard(box, match.Board(i), false);
                else Widgets.DrawBoxSolid(box, PokerTheme.Empty);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = PokerTheme.PotChip;
            Widgets.Label(new Rect(area.x, cards.y - 34f, area.width, 30f), "POK.Label.Pot".Translate(match.Pot));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (PokerSettings.ShowHandName && match.BoardCount > 0)
            {
                GUI.color = PRTheme.Dim;
                Widgets.Label(new Rect(area.x, cards.yMax + 2f, area.width, 22f),
                    "POK.Label.YourHand".Translate(CategoryName(match.ScoreOf(PokerSeat.Player))));
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>카드 한 장. 숫자는 글자로, 무늬는 알파 한 장에 색을 입혀 그린다.</summary>
        private static void DrawCard(Rect box, int card, bool faceDown)
        {
            if (card < 0) { Widgets.DrawBoxSolid(box, PokerTheme.Empty); return; }

            Widgets.DrawBoxSolid(box, PokerTheme.CardEdge);
            Rect face = box.ContractedBy(1.5f);

            if (faceDown)
            {
                Widgets.DrawBoxSolid(face, PokerTheme.CardBack);
                GUI.color = PokerTheme.CardBackMark;
                GUI.DrawTexture(face.ContractedBy(face.width * 0.28f), PRTextures.Ring);
                GUI.color = Color.white;
                return;
            }

            Widgets.DrawBoxSolid(face, PokerTheme.CardFace);

            int suit = Cards.Suit(card);
            Color ink = PokerTheme.InkFor(suit);

            GUI.color = ink;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(face.x + 4f, face.y - 1f, 22f, 22f),
                Cards.RankLetters[Cards.Rank(card)].ToString());

            float pip = face.width * 0.52f;
            GUI.DrawTexture(new Rect(face.center.x - pip * 0.5f, face.center.y - pip * 0.34f, pip, pip),
                PokerTheme.Suits[suit]);
            GUI.color = Color.white;
        }

        // ---------- 조작 ----------

        private void DrawControls(Rect area)
        {
            if (match.IsOver) return;

            if (stage == Stage.HandOver) { DrawNextHand(area); return; }
            if (stage != Stage.Player) { DrawWaiting(area); return; }

            Rect slider = new Rect(area.x, area.y, area.width, 30f);
            Rect buttons = new Rect(area.x, area.y + 36f, area.width, area.height - 36f);

            if (match.CanRaise) DrawRaiseSlider(slider);

            float width = (buttons.width - 16f) / 3f;

            Rect fold = new Rect(buttons.x, buttons.y, width, buttons.height);
            Rect call = new Rect(fold.xMax + 8f, buttons.y, width, buttons.height);
            Rect raise = new Rect(call.xMax + 8f, buttons.y, width, buttons.height);

            if (Widgets.ButtonText(fold, "POK.Btn.Fold".Translate())) Player(PokerAction.Fold, 0);

            string callLabel = match.CanCheck
                ? "POK.Btn.Check".Translate().ToString()
                : "POK.Btn.Call".Translate(match.ToCall).ToString();

            if (Widgets.ButtonText(call, callLabel))
                Player(match.CanCheck ? PokerAction.Check : PokerAction.Call, 0);

            if (match.CanRaise)
            {
                string raiseLabel = match.RaiseIsAllIn(raiseTo)
                    ? "POK.Btn.AllIn".Translate(raiseTo).ToString()
                    : "POK.Btn.Raise".Translate(raiseTo).ToString();

                if (Widgets.ButtonText(raise, raiseLabel)) Player(PokerAction.Raise, raiseTo);
            }
            else
            {
                GUI.color = PRTheme.Dim;
                Widgets.ButtonText(raise, "POK.Btn.Raise.None".Translate(), true, false, false);
                GUI.color = Color.white;
            }
        }

        private void DrawRaiseSlider(Rect row)
        {
            const float quickWidth = 62f;

            Rect quick = new Rect(row.xMax - quickWidth * 3f - 12f, row.y, quickWidth * 3f + 12f, row.height);
            Rect bar = new Rect(row.x, row.y + 4f, row.width - quick.width - 10f, row.height - 8f);

            int low = match.MinRaiseTo;
            int high = match.MaxRaiseTo;

            raiseTo = Mathf.Clamp(raiseTo, low, high);

            if (high > low)
            {
                float picked = Widgets.HorizontalSlider(bar, raiseTo, low, high, true, null, null, null, 5f);
                raiseTo = Mathf.Clamp(Mathf.RoundToInt(picked / 5f) * 5, low, high);
            }
            else
            {
                Widgets.DrawBoxSolid(bar, PokerTheme.Empty);
            }

            int pot = match.Pot + match.Bet(PokerSeat.Player) + match.Bet(PokerSeat.Opponent);
            int highest = Mathf.Max(match.Bet(PokerSeat.Player), match.Bet(PokerSeat.Opponent));

            if (Widgets.ButtonText(new Rect(quick.x, row.y, quickWidth, row.height), "POK.Btn.Half".Translate()))
                raiseTo = Mathf.Clamp(highest + pot / 2, low, high);

            if (Widgets.ButtonText(new Rect(quick.x + quickWidth + 6f, row.y, quickWidth, row.height),
                                   "POK.Btn.PotBet".Translate()))
                raiseTo = Mathf.Clamp(highest + pot, low, high);

            if (Widgets.ButtonText(new Rect(quick.x + quickWidth * 2f + 12f, row.y, quickWidth, row.height),
                                   "POK.Btn.Max".Translate()))
                raiseTo = high;
        }

        private void DrawNextHand(Rect area)
        {
            Rect button = new Rect(area.center.x - 120f, area.y + 36f, 240f, area.height - 36f);

            if (Widgets.ButtonText(button, "POK.Btn.Next".Translate()))
                NextHand(Time.realtimeSinceStartup);
        }

        private void DrawWaiting(Rect area)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = PRTheme.Dim;
            Widgets.Label(area, "POK.Status.Thinking".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void Player(PokerAction action, int amount)
        {
            if (stage != Stage.Player) return;
            Apply(action, amount, Time.realtimeSinceStartup);
        }

        public override void HandleShortcuts()
        {
            if (Event.current.type != EventType.KeyDown) return;
            if (Event.current.keyCode != KeyCode.Space) return;

            if (stage == Stage.HandOver) { NextHand(Time.realtimeSinceStartup); Event.current.Use(); return; }
            if (stage != Stage.Player) return;

            Player(match.CanCheck ? PokerAction.Check : PokerAction.Call, 0);
            Event.current.Use();
        }

        /// <summary>큰 버튼은 쓰지 않는다. 포커의 선택은 셋이라 판 안에 두었다.</summary>
        public override string ActionLabel
        {
            get { return null; }
        }

        // ---------- 문자열 ----------

        private static string CategoryName(int score)
        {
            return ("POK.Hand." + HandEval.Category(score)).Translate().ToString();
        }

        private static string StreetName(PokerStreet street)
        {
            return ("POK.Street." + street).Translate().ToString();
        }

        public override string StatusText
        {
            get
            {
                if (match == null) return string.Empty;

                if (match.IsOver)
                {
                    if (!match.Winner.HasValue) return "POK.Status.DrawMatch".Translate().ToString();

                    return (match.Winner.Value == PokerSeat.Player
                        ? "POK.Status.WinMatch" : "POK.Status.LoseMatch")
                        .Translate(match.Stack(PokerSeat.Player), match.Stack(PokerSeat.Opponent)).ToString();
                }

                if (stage == Stage.HandOver)
                {
                    if (!match.HandWinner.HasValue) return "POK.Status.Split".Translate().ToString();

                    bool mine = match.HandWinner.Value == PokerSeat.Player;

                    if (!match.ShowdownReached)
                        return (mine ? "POK.Status.WonFold" : "POK.Status.LostFold").Translate().ToString();

                    return (mine ? "POK.Status.WonHand" : "POK.Status.LostHand")
                        .Translate(CategoryName(match.ScoreOf(match.HandWinner.Value))).ToString();
                }

                if (stage == Stage.Thinking) return "POK.Status.Thinking".Translate().ToString();

                return "POK.Status.Your".Translate(StreetName(match.Street), match.BigBlind).ToString();
            }
        }

        public override IReadOnlyList<string> Log
        {
            get
            {
                if (match == null) return log;
                if (match.Log.Count == lastLogCount) return log;

                lastLogCount = match.Log.Count;
                log.Clear();

                for (int i = 0; i < match.Log.Count; i++) log.Add(Format(match.Log[i]));

                return log;
            }
        }

        private static string Format(PokerLogEntry entry)
        {
            string side = entry.Seat == PokerSeat.Player
                ? "PR.Side.You".Translate().ToString()
                : "PR.Side.Opponent".Translate().ToString();

            if (entry.Kind == PokerEvent.HandSplit)
                return string.Format("{0,3}  {1}", entry.Hand, "POK.Log.Split".Translate(entry.Pot));

            if (entry.Kind == PokerEvent.HandWon)
            {
                string how = entry.Showdown
                    ? "POK.Log.WonShowdown".Translate(side, entry.Pot,
                        ("POK.Hand." + entry.Category).Translate()).ToString()
                    : "POK.Log.WonFold".Translate(side, entry.Pot).ToString();

                return string.Format("{0,3}  {1}", entry.Hand, how);
            }

            string what;
            switch (entry.Action)
            {
                case PokerAction.Fold: what = "POK.Log.Fold".Translate().ToString(); break;
                case PokerAction.Check: what = "POK.Log.Check".Translate().ToString(); break;
                case PokerAction.Call: what = "POK.Log.Call".Translate(entry.Amount).ToString(); break;
                default: what = "POK.Log.Raise".Translate(entry.Amount).ToString(); break;
            }

            return string.Format("{0,3}  {1,-5} {2}  {3}", entry.Hand, StreetName(entry.Street), side, what);
        }

        public override void FillTallies(int[] tallies)
        {
            if (match == null || tallies.Length < 3) return;

            tallies[0] = handsWon;
            tallies[1] = showdowns;
            tallies[2] = peakStack;
        }

        public override void DoSettings(Listing_Standard list)
        {
            PokerSettings.DoSettings(list);
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            Widgets.DrawBoxSolid(area, PokerTheme.Felt);

            float width = CardWidth * 5f + 8f * 4f;
            float x = area.center.x - width * 0.5f;

            // 1쪽: 내 두 장. 2쪽: 보드 다섯 장. 3쪽 이후: 완성된 손 한 벌.
            if (page == 0)
            {
                DrawCard(new Rect(area.center.x - CardWidth - 4f, area.center.y - CardHeight * 0.5f,
                                  CardWidth, CardHeight), Cards.Of(12, Cards.Spades), false);
                DrawCard(new Rect(area.center.x + 4f, area.center.y - CardHeight * 0.5f,
                                  CardWidth, CardHeight), Cards.Of(12, Cards.Hearts), false);
                return;
            }

            int[] board = { Cards.Of(12, Cards.Clubs), Cards.Of(5, Cards.Diamonds), Cards.Of(0, Cards.Hearts),
                            Cards.Of(9, Cards.Spades), Cards.Of(3, Cards.Clubs) };

            int shown = page == 1 ? 3 : 5;

            for (int i = 0; i < 5; i++)
            {
                Rect box = new Rect(x + i * (CardWidth + 8f), area.center.y - CardHeight * 0.5f,
                                    CardWidth, CardHeight);

                if (i < shown) DrawCard(box, board[i], false);
                else Widgets.DrawBoxSolid(box, PokerTheme.Empty);
            }
        }
    }
}
