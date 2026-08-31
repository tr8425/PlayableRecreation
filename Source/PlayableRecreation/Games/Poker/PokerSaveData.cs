using Poker.Core;
using PlayableRecreation;
using Verse;

namespace Poker
{
    /// <summary>
    /// 테이블에 남는 판. 카드는 저장하지 않는다 - 덱은 <c>(시드, 핸드 번호)</c>가 정하므로
    /// 돌아오면 같은 패가 그대로 깔린다. 마음에 안 드는 손을 창을 닫아 무를 수 없는 이유다.
    /// </summary>
    public class PokerSaveData : MiniGameSaveData
    {
        public int seed;
        public int hand = 1;
        public bool buttonIsOpponent;
        public int playerStack = HoldemMatch.StartingStack;
        public int opponentStack = HoldemMatch.StartingStack;
        public int pot;
        public int playerBet, opponentBet;
        public bool playerActed, opponentActed;
        public int street;
        public bool toActIsOpponent;
        public int lastRaise = 10;
        public int boardCount;

        /// <summary>핸드가 끝난 자리에서 저장되었는가. 그러면 되살릴 때 다음 핸드부터 시작한다.</summary>
        public bool freshHand;

        /// <summary>지금까지 오간 행동 수. 상대의 시드가 여기서 나오므로 반드시 남겨야 한다.</summary>
        public int actions;

        /// <summary>창이 들고 있던 집계. 다시 세면 이어 열기 전의 판이 통째로 지워진다.</summary>
        public int peakStack = HoldemMatch.StartingStack;
        public int lowStack = HoldemMatch.StartingStack;
        public int handsWon;
        public int showdowns;

        public PokerSaveData()
        {
        }

        public PokerSaveData(HoldemMatch match)
        {
            seed = match.Seed;
            actions = match.Actions;
            playerStack = match.Stack(PokerSeat.Player);
            opponentStack = match.Stack(PokerSeat.Opponent);

            // 이미 끝난 핸드를 그대로 담아 두면, 돌아왔을 때 팟도 없는 죽은 판 위에 앉게 된다.
            // 다음 핸드의 시작점만 적어 두면 그만이다 - 버튼은 넘어가고 카드는 시드가 다시 낸다.
            if (match.HandDone)
            {
                freshHand = true;
                hand = match.Hand + 1;
                buttonIsOpponent = !match.ButtonIsOpponent;
                return;
            }

            hand = match.Hand;
            buttonIsOpponent = match.ButtonIsOpponent;
            pot = match.Pot;
            playerBet = match.Bet(PokerSeat.Player);
            opponentBet = match.Bet(PokerSeat.Opponent);
            playerActed = match.ActedBy(PokerSeat.Player);
            opponentActed = match.ActedBy(PokerSeat.Opponent);
            street = match.StreetIndex;
            toActIsOpponent = match.ToActIsOpponent;
            lastRaise = match.LastRaiseSize;
            boardCount = match.BoardCount;
        }

        public HoldemMatch ToMatch()
        {
            if (freshHand)
                return HoldemMatch.RestoreAtHand(seed, hand, buttonIsOpponent,
                                                 playerStack, opponentStack, actions);

            return HoldemMatch.Restore(seed, hand, buttonIsOpponent, playerStack, opponentStack,
                                       pot, playerBet, opponentBet, playerActed, opponentActed,
                                       street, toActIsOpponent, lastRaise, boardCount, actions);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref hand, "hand", 1);
            Scribe_Values.Look(ref buttonIsOpponent, "buttonIsOpponent", false);
            Scribe_Values.Look(ref playerStack, "playerStack", HoldemMatch.StartingStack);
            Scribe_Values.Look(ref opponentStack, "opponentStack", HoldemMatch.StartingStack);
            Scribe_Values.Look(ref pot, "pot", 0);
            Scribe_Values.Look(ref playerBet, "playerBet", 0);
            Scribe_Values.Look(ref opponentBet, "opponentBet", 0);
            Scribe_Values.Look(ref playerActed, "playerActed", false);
            Scribe_Values.Look(ref opponentActed, "opponentActed", false);
            Scribe_Values.Look(ref street, "street", 0);
            Scribe_Values.Look(ref toActIsOpponent, "toActIsOpponent", false);
            Scribe_Values.Look(ref lastRaise, "lastRaise", 10);
            Scribe_Values.Look(ref boardCount, "boardCount", 0);
            Scribe_Values.Look(ref freshHand, "freshHand", false);
            Scribe_Values.Look(ref actions, "actions", 0);

            Scribe_Values.Look(ref peakStack, "peakStack", HoldemMatch.StartingStack);
            Scribe_Values.Look(ref lowStack, "lowStack", HoldemMatch.StartingStack);
            Scribe_Values.Look(ref handsWon, "handsWon", 0);
            Scribe_Values.Look(ref showdowns, "showdowns", 0);
        }
    }
}
