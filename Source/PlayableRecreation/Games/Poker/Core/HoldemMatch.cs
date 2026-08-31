using System;
using System.Collections.Generic;

namespace Poker.Core
{
    public enum PokerSeat { Player = 0, Opponent = 1 }

    public enum PokerStreet { Preflop = 0, Flop = 1, Turn = 2, River = 3, Showdown = 4 }

    public enum PokerAction { Fold, Check, Call, Raise }

    public enum PokerEvent { Action, HandWon, HandSplit }

    public struct PokerLogEntry
    {
        public PokerEvent Kind;
        public int Hand;
        public PokerStreet Street;
        public PokerSeat Seat;
        public PokerAction Action;

        /// <summary>레이즈면 올린 총액, 콜이면 낸 칩.</summary>
        public int Amount;

        /// <summary>결과 줄에서 오간 팟.</summary>
        public int Pot;

        public bool Showdown;
        public HandCategory Category;
    }

    /// <summary>
    /// 일대일 노리밋 텍사스 홀덤. 둘뿐이므로 사이드 팟이 없다 -
    /// 어느 쪽도 상대가 콜할 수 없는 액수를 걸 수 없게 막아 두면 팟은 언제나 하나다.
    ///
    /// 덱은 <c>(시드, 핸드 번호)</c>로 결정된다. 창을 닫았다 열어도 같은 패가 돌아오므로
    /// 판을 다시 돌려 좋은 패를 뽑는 길이 없다.
    /// </summary>
    public sealed class HoldemMatch
    {
        public const int StartingStack = 400;
        public const int MaxHands = 24;
        private const int HandsPerLevel = 8;

        private static readonly int[] BigBlinds = { 10, 20, 40, 80 };

        private readonly int seed;
        private readonly List<PokerLogEntry> log = new List<PokerLogEntry>();

        private readonly int[] stacks = new int[2];
        private readonly int[] bets = new int[2];
        private readonly bool[] acted = new bool[2];
        private readonly int[] holeCards = new int[4];
        private readonly int[] boardCards = new int[5];
        private readonly int[] scratch = new int[7];

        private int lastRaiseSize;

        public int Pot { get; private set; }
        public int BoardCount { get; private set; }
        public PokerStreet Street { get; private set; }
        public PokerSeat ToAct { get; private set; }
        public PokerSeat Button { get; private set; }

        /// <summary>몇 번째 핸드인가. 1부터 센다.</summary>
        public int Hand { get; private set; }

        /// <summary>이번 핸드가 끝났는가. 다음 핸드는 <see cref="NextHand"/> 가 시작한다.</summary>
        public bool HandDone { get; private set; }

        public bool ShowdownReached { get; private set; }
        public PokerSeat? HandWinner { get; private set; }
        public bool IsOver { get; private set; }
        public PokerSeat? Winner { get; private set; }

        /// <summary>행동할 때마다 하나씩 는다. 저장 지점이자 되돌릴 수 없다는 표시다.</summary>
        public int Actions { get; private set; }

        public int Seed { get { return seed; } }
        public IReadOnlyList<PokerLogEntry> Log { get { return log; } }

        public int BigBlind
        {
            get
            {
                int level = Math.Min(BigBlinds.Length - 1, Math.Max(0, (Hand - 1) / HandsPerLevel));
                return BigBlinds[level];
            }
        }

        public int SmallBlind { get { return BigBlind / 2; } }

        public int Stack(PokerSeat seat) { return stacks[(int)seat]; }
        public int Bet(PokerSeat seat) { return bets[(int)seat]; }

        public int Hole(PokerSeat seat, int index) { return holeCards[(int)seat * 2 + index]; }
        public int Board(int index) { return index < BoardCount ? boardCards[index] : Cards.None; }

        public static PokerSeat Other(PokerSeat seat)
        {
            return seat == PokerSeat.Player ? PokerSeat.Opponent : PokerSeat.Player;
        }

        public HoldemMatch(int seed)
        {
            this.seed = seed;
            stacks[0] = StartingStack;
            stacks[1] = StartingStack;
            Button = PokerSeat.Player;
            Hand = 0;

            BeginHand();
        }
        // ---------- 핸드 시작 ----------

        /// <summary>덱은 시드와 핸드 번호가 정한다. 같은 핸드는 언제나 같은 패다.</summary>
        private void Deal()
        {
            Deck deck = new Deck(seed ^ (Hand * 486187739));

            for (int i = 0; i < 4; i++) holeCards[i] = deck.Draw();
            for (int i = 0; i < 5; i++) boardCards[i] = deck.Draw();
        }

        private void BeginHand()
        {
            if (IsOver) return;

            Hand++;
            Deal();

            BoardCount = 0;
            Pot = 0;
            HandDone = false;
            ShowdownReached = false;
            HandWinner = null;
            Street = PokerStreet.Preflop;

            bets[0] = 0;
            bets[1] = 0;
            acted[0] = false;
            acted[1] = false;

            // 일대일에서는 버튼이 스몰블라인드를 내고 프리플랍에서 먼저 행동한다.
            PokerSeat small = Button;
            PokerSeat big = Other(Button);

            Post(small, SmallBlind);
            Post(big, BigBlind);

            lastRaiseSize = BigBlind;
            ToAct = small;

            // 블라인드가 스택보다 클 수 있다. 상대가 받을 수 없는 만큼은 도로 가져간다.
            ReturnExcess();

            // 블라인드로 이미 올인이 되었다면 걸 것이 없다.
            if (BettingClosed()) RunOut();
        }

        private void Post(PokerSeat seat, int amount)
        {
            int paid = Math.Min(amount, stacks[(int)seat]);
            stacks[(int)seat] -= paid;
            bets[(int)seat] += paid;
        }

        private bool BettingClosed()
        {
            return stacks[0] == 0 || stacks[1] == 0;
        }

        /// <summary>
        /// 상대가 도저히 받을 수 없는 몫은 팟에 들어가지 않는다.
        /// 이 한 줄이 일대일에 사이드 팟이 없는 이유다 - 넘치는 칩은 언제나 주인에게 돌아간다.
        /// </summary>
        private void ReturnExcess()
        {
            for (int me = 0; me < 2; me++)
            {
                int you = 1 - me;
                int covered = bets[you] + stacks[you];

                if (bets[me] <= covered) continue;

                int back = bets[me] - covered;
                bets[me] -= back;
                stacks[me] += back;
            }
        }

        // ---------- 지금 무엇을 할 수 있는가 ----------

        /// <summary>둘의 유효 스택 중 작은 쪽. 이보다 크게 걸 수는 없다.</summary>
        public int Cap
        {
            get { return Math.Min(stacks[0] + bets[0], stacks[1] + bets[1]); }
        }

        public int ToCall
        {
            get
            {
                int me = (int)ToAct;
                int you = (int)Other(ToAct);
                return Math.Min(Math.Max(0, bets[you] - bets[me]), stacks[me]);
            }
        }

        public bool CanCheck { get { return ToCall == 0; } }

        public int MinRaiseTo
        {
            get
            {
                int you = (int)Other(ToAct);
                return Math.Min(Cap, Math.Max(bets[you] + lastRaiseSize, BigBlind));
            }
        }

        public int MaxRaiseTo { get { return Cap; } }

        public bool CanRaise
        {
            get
            {
                if (HandDone || IsOver) return false;

                int me = (int)ToAct;
                int you = (int)Other(ToAct);
                return Cap > bets[you] && stacks[me] > ToCall;
            }
        }

        /// <summary>레이즈가 올인이 되는가. 창은 그때 문구를 바꾼다.</summary>
        public bool RaiseIsAllIn(int raiseTo)
        {
            int me = (int)ToAct;
            return raiseTo >= bets[me] + stacks[me];
        }

        // ---------- 행동 ----------

        public void Act(PokerAction action, int raiseTo)
        {
            if (HandDone || IsOver) return;

            PokerSeat seat = ToAct;
            int me = (int)seat;
            int you = (int)Other(seat);

            switch (action)
            {
                case PokerAction.Fold:
                    Record(seat, PokerAction.Fold, 0);
                    Actions++;
                    Collect();
                    Award(Other(seat), false, HandCategory.HighCard);
                    return;

                case PokerAction.Check:
                    if (!CanCheck) return;
                    Record(seat, PokerAction.Check, 0);
                    acted[me] = true;
                    break;

                case PokerAction.Call:
                {
                    int paid = ToCall;
                    stacks[me] -= paid;
                    bets[me] += paid;
                    Record(seat, PokerAction.Call, paid);
                    acted[me] = true;
                    break;
                }

                case PokerAction.Raise:
                {
                    if (!CanRaise) return;

                    int target = Math.Max(MinRaiseTo, Math.Min(MaxRaiseTo, raiseTo));
                    int paid = target - bets[me];

                    stacks[me] -= paid;
                    bets[me] += paid;

                    lastRaiseSize = Math.Max(BigBlind, bets[me] - bets[you]);

                    Record(seat, PokerAction.Raise, target);
                    acted[me] = true;
                    acted[you] = false;   // 올린 만큼 상대는 다시 결정해야 한다
                    break;
                }
            }

            Actions++;
            Advance();
        }

        private void Advance()
        {
            ReturnExcess();

            if (BettingClosed())
            {
                // 올인이 걸렸다. 콜이 맞으면 그대로 끝이고,
                // 아직 안 맞았다면 낼 수 있는 쪽에게만 마지막 결정이 남는다.
                if (bets[0] == bets[1]) { CloseStreet(); return; }

                int owes = bets[0] < bets[1] ? 0 : 1;
                if (stacks[owes] > 0 && !acted[owes]) { ToAct = (PokerSeat)owes; return; }

                CloseStreet();
                return;
            }

            if (acted[0] && acted[1] && bets[0] == bets[1]) { CloseStreet(); return; }

            ToAct = Other(ToAct);
        }

        private void Collect()
        {
            Pot += bets[0] + bets[1];
            bets[0] = 0;
            bets[1] = 0;
        }

        private void CloseStreet()
        {
            Collect();

            acted[0] = false;
            acted[1] = false;
            lastRaiseSize = BigBlind;

            if (Street == PokerStreet.River) { Showdown(); return; }

            if (BettingClosed()) { RunOut(); return; }

            Street = (PokerStreet)((int)Street + 1);
            BoardCount = Street == PokerStreet.Flop ? 3 : BoardCount + 1;

            // 플랍부터는 버튼이 마지막에 행동한다.
            ToAct = Other(Button);
        }

        /// <summary>더 걸 것이 없다. 남은 보드를 전부 깔고 승부를 본다.</summary>
        private void RunOut()
        {
            Collect();
            BoardCount = 5;
            Street = PokerStreet.River;
            Showdown();
        }

        private void Showdown()
        {
            ShowdownReached = true;
            Street = PokerStreet.Showdown;

            int playerScore = ScoreOf(PokerSeat.Player);
            int opponentScore = ScoreOf(PokerSeat.Opponent);

            if (playerScore == opponentScore)
            {
                int half = Pot / 2;
                int odd = Pot - half * 2;

                stacks[0] += half;
                stacks[1] += half;

                // 홀수 칩은 블라인드를 더 많이 낸 쪽, 즉 버튼이 아닌 쪽이 가져간다.
                stacks[(int)Other(Button)] += odd;

                log.Add(new PokerLogEntry
                {
                    Kind = PokerEvent.HandSplit,
                    Hand = Hand,
                    Pot = Pot,
                    Showdown = true,
                    Category = HandEval.Category(playerScore),
                });

                Pot = 0;
                HandWinner = null;
                HandDone = true;
                CheckOver();
                return;
            }

            PokerSeat winner = playerScore > opponentScore ? PokerSeat.Player : PokerSeat.Opponent;
            Award(winner, true, HandEval.Category(Math.Max(playerScore, opponentScore)));
        }

        public int ScoreOf(PokerSeat seat)
        {
            scratch[0] = Hole(seat, 0);
            scratch[1] = Hole(seat, 1);
            for (int i = 0; i < 5; i++) scratch[2 + i] = i < BoardCount ? boardCards[i] : Cards.None;

            return HandEval.Score(scratch, 2 + BoardCount);
        }

        private void Award(PokerSeat seat, bool showdown, HandCategory category)
        {
            log.Add(new PokerLogEntry
            {
                Kind = PokerEvent.HandWon,
                Hand = Hand,
                Seat = seat,
                Pot = Pot,
                Showdown = showdown,
                Category = category,
            });

            stacks[(int)seat] += Pot;
            Pot = 0;

            HandWinner = seat;
            HandDone = true;

            CheckOver();
        }

        private void Record(PokerSeat seat, PokerAction action, int amount)
        {
            log.Add(new PokerLogEntry
            {
                Kind = PokerEvent.Action,
                Hand = Hand,
                Street = Street,
                Seat = seat,
                Action = action,
                Amount = amount,
            });
        }

        // ---------- 판의 끝 ----------

        private void CheckOver()
        {
            if (stacks[0] <= 0 || stacks[1] <= 0)
            {
                IsOver = true;
                Winner = stacks[0] > stacks[1] ? PokerSeat.Player : PokerSeat.Opponent;
                return;
            }

            if (Hand < MaxHands) return;

            // 정해진 핸드를 다 쳤다. 칩이 많은 쪽이 가져간다.
            IsOver = true;
            Winner = stacks[0] == stacks[1]
                ? (PokerSeat?)null
                : (stacks[0] > stacks[1] ? PokerSeat.Player : PokerSeat.Opponent);
        }

        public void NextHand()
        {
            if (IsOver || !HandDone) return;

            Button = Other(Button);
            BeginHand();
        }

        // ---------- 세이브 ----------

        /// <summary>
        /// 한 수 한 수를 그대로 되살린다. 덱은 시드가 정하므로 카드는 저장하지 않는다 -
        /// 판을 중간에 닫아 마음에 안 드는 패를 무르는 길을 이렇게 막는다.
        /// </summary>
        public static HoldemMatch Restore(int seed, int hand, bool buttonIsOpponent,
                                          int playerStack, int opponentStack,
                                          int pot, int playerBet, int opponentBet,
                                          bool playerActed, bool opponentActed,
                                          int street, bool toActIsOpponent,
                                          int lastRaise, int boardCount, int actions)
        {
            HoldemMatch match = new HoldemMatch(seed, hand, buttonIsOpponent);

            match.stacks[0] = playerStack;
            match.stacks[1] = opponentStack;
            match.Pot = pot;
            match.bets[0] = playerBet;
            match.bets[1] = opponentBet;
            match.acted[0] = playerActed;
            match.acted[1] = opponentActed;
            match.Street = (PokerStreet)Math.Max(0, Math.Min(3, street));
            match.ToAct = toActIsOpponent ? PokerSeat.Opponent : PokerSeat.Player;
            match.lastRaiseSize = Math.Max(1, lastRaise);
            match.BoardCount = Math.Max(0, Math.Min(5, boardCount));

            // 상대의 시드가 여기서 나온다. 이 숫자를 잃으면 같은 자리에서 다른 결정이 나오고,
            // 그러면 창을 닫았다 여는 것만으로 상대의 수를 다시 굴릴 수 있게 된다.
            match.Actions = Math.Max(0, actions);

            return match;
        }

        /// <summary>
        /// 핸드가 끝난 자리에서 되살린다. 그 자리는 이어 둘 수 있는 상태가 아니므로 -
        /// 팟은 이미 넘어갔고 보드는 다 깔려 있다 - 다음 핸드를 새로 돌리는 것이 맞다.
        /// </summary>
        public static HoldemMatch RestoreAtHand(int seed, int hand, bool buttonIsOpponent,
                                                int playerStack, int opponentStack, int actions)
        {
            HoldemMatch match = new HoldemMatch(seed, 1, buttonIsOpponent);

            match.stacks[0] = playerStack;
            match.stacks[1] = opponentStack;
            match.Hand = Math.Max(0, hand - 1);
            match.BeginHand();
            match.Actions = Math.Max(0, actions);

            return match;
        }

        /// <summary>되살리기 전용. 블라인드를 다시 내지 않고 판만 세운다.</summary>
        private HoldemMatch(int seed, int hand, bool buttonIsOpponent)
        {
            this.seed = seed;
            Hand = Math.Max(1, hand);
            Button = buttonIsOpponent ? PokerSeat.Opponent : PokerSeat.Player;

            Deal();
        }

        public int StreetIndex { get { return (int)Street; } }
        public bool ButtonIsOpponent { get { return Button == PokerSeat.Opponent; } }
        public bool ToActIsOpponent { get { return ToAct == PokerSeat.Opponent; } }
        public bool ActedBy(PokerSeat seat) { return acted[(int)seat]; }
        public int LastRaiseSize { get { return lastRaiseSize; } }
    }
}
