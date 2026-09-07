using System.Collections.Generic;

namespace Roulette.Core
{
    /// <summary>진행 기록 한 스핀. 어디에 얼마를 걸어 몇 번이 나왔고 얼마가 오갔는지.</summary>
    public struct RouletteEntry
    {
        public int Index;
        public BetKind Kind;

        /// <summary>스트레이트가 집은 숫자. 다른 종류는 0.</summary>
        public int Value;

        public int Stake;
        public int Pocket;

        /// <summary>순변화. 지면 -Stake, 이기면 +Stake × 배수.</summary>
        public int Net;
    }

    /// <summary>
    /// 룰렛 한 판 - 뱅크롤 런. 칩 스무 닢으로 시작해 목표액에 닿으면 이기고,
    /// 다 잃으면 진다. 순수 운을 승부로 만드는 것은 목표 배수다 - 배수가 높을수록
    /// 짝수배당으로는 산수가 안 나오고, 변동성을 스스로 골라야 한다.
    ///
    /// 상대는 없다. 하우스는 수학이고, 수학은 생각하는 시간이 필요 없다.
    /// </summary>
    public sealed class RouletteRun
    {
        public const int StartBankroll = 20;

        /// <summary>
        /// 테이블 리밋. 이것이 없으면 ×1.5 도 ×2 도 "짝수배당 올인 한 방"이라
        /// 난이도 사다리가 무너진다. 한도가 있어야 높은 목표는 여러 번 이겨야 하고,
        /// 변동성(다즌 · 스트레이트)을 고르는 결정이 살아난다.
        /// </summary>
        public const int MaxStake = 10;

        private readonly List<RouletteEntry> log = new List<RouletteEntry>();

        public int Seed { get; private set; }
        public int Target { get; private set; }

        public int Bankroll { get; private set; }
        public int Peak { get; private set; }
        public int Spins { get; private set; }

        public bool IsOver { get; private set; }
        public bool Won { get; private set; }

        /// <summary>한 번이라도 시작액 아래로 내려갔는가. 완봉의 반대 증거다.</summary>
        public bool DippedBelowStart { get; private set; }

        public int BestWin { get; private set; }
        public int StraightHits { get; private set; }

        public IReadOnlyList<RouletteEntry> Log
        {
            get { return log; }
        }

        public RouletteRun(int seed, int target)
        {
            Seed = seed;
            Target = target;
            Bankroll = StartBankroll;
            Peak = StartBankroll;
        }

        /// <summary>난이도가 곧 목표액이다. ×1.5 부터 ×5 까지.</summary>
        public static int TargetFor(int tier)
        {
            switch (tier)
            {
                case 0: return 30;
                case 1: return 40;
                case 2: return 60;
                case 3: return 80;
                default: return 100;
            }
        }

        /// <summary>세이브에서 되살린다. 상태는 전부 기록의 재생이므로 두 벌이 어긋날 길이 없다.</summary>
        public static RouletteRun Restore(int seed, int target, IEnumerable<RouletteEntry> entries)
        {
            RouletteRun run = new RouletteRun(seed, target);

            if (entries != null)
                foreach (RouletteEntry entry in entries)
                    run.Apply(entry);

            return run;
        }

        /// <summary>한 스핀. 건 돈은 [1, min(뱅크롤, 테이블 리밋)]로 눌러 담고, 채점까지 끝낸다.</summary>
        public RouletteEntry Spin(BetKind kind, int value, int stake)
        {
            if (stake < 1) stake = 1;
            if (stake > MaxStake) stake = MaxStake;
            if (stake > Bankroll) stake = Bankroll;

            int pocket = RouletteWheel.PocketAt(Seed, Spins);
            bool won = RouletteWheel.Wins(kind, value, pocket);

            RouletteEntry entry = new RouletteEntry
            {
                Index = Spins,
                Kind = kind,
                Value = kind == BetKind.Straight ? value : 0,
                Stake = stake,
                Pocket = pocket,
                Net = won ? stake * RouletteWheel.NetMultiplier(kind) : -stake,
            };

            Apply(entry);
            return entry;
        }

        private void Apply(RouletteEntry entry)
        {
            if (IsOver) return;

            log.Add(entry);
            Spins++;

            Bankroll += entry.Net;

            if (entry.Net > 0)
            {
                if (entry.Net > BestWin) BestWin = entry.Net;
                if (entry.Kind == BetKind.Straight) StraightHits++;
            }

            if (Bankroll > Peak) Peak = Bankroll;
            if (Bankroll < StartBankroll) DippedBelowStart = true;

            if (Bankroll >= Target)
            {
                IsOver = true;
                Won = true;
            }
            else if (Bankroll <= 0)
            {
                IsOver = true;
                Won = false;
            }
        }
    }
}
