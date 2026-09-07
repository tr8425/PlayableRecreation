using System;
using System.Collections.Generic;
using PlayableRecreation.Core;

namespace Darts.Core
{
    public enum DartSide
    {
        Player,
        Opponent,
    }

    /// <summary>진행 기록 한 발. 자리(x, y)까지 남겨야 이어서 열었을 때 판을 다시 그릴 수 있다.</summary>
    public struct DartEntry
    {
        public int Round;
        public DartSide Side;
        public float X;
        public float Y;
        public int Points;

        /// <summary>"T20" 같은 자리 표기. 빗나가면 빈 문자열.</summary>
        public string Code;
    }

    /// <summary>
    /// 다트 한 판. Verse 를 모른다 - 규칙만 있고 그림도 입력도 없다.
    ///
    /// 라운드마다 양쪽이 3발씩 던지고, 5라운드 합계로 승부한다. 501 의 체크아웃 규칙은
    /// 일부러 뺐다 - 튜토리얼 세 쪽 안에서 끝나는 규칙이 이 창의 크기에 맞는 규칙이다.
    /// 합계가 같으면 갈릴 때까지 한 라운드씩 더 던진다.
    ///
    /// 상대의 던지기와 판 상태는 전부 (시드, 순번)과 기록에서 나온다 -
    /// 창을 닫았다 여는 식의 세이브스컴이 통하지 않는다. 던지기 게임들과 같은 원리다.
    /// </summary>
    public sealed class DartsMatch
    {
        public const int DartsPerVisit = 3;
        public const int BaseRounds = 5;

        private readonly List<DartEntry> log = new List<DartEntry>();

        public int Seed { get; private set; }
        public int Round { get; private set; }
        public DartSide Turn { get; private set; }
        public int ThrowIndex { get; private set; }

        public int TotalPlayer { get; private set; }
        public int TotalOpponent { get; private set; }

        /// <summary>이번 라운드에서 각자 던진 발 수와 그 합.</summary>
        public int VisitCountPlayer { get; private set; }
        public int VisitCountOpponent { get; private set; }
        public int VisitSumPlayer { get; private set; }
        public int VisitSumOpponent { get; private set; }

        /// <summary>닫힌 라운드 수와 그중 플레이어가 이긴(합이 더 큰) 라운드 수. 완봉 판정용.</summary>
        public int RoundsClosed { get; private set; }
        public int RoundsWonPlayer { get; private set; }

        public int Triples { get; private set; }
        public int Bulls { get; private set; }
        public int BestVisit { get; private set; }

        public bool IsOver { get; private set; }

        public IReadOnlyList<DartEntry> Log
        {
            get { return log; }
        }

        public DartSide Winner
        {
            get { return TotalPlayer >= TotalOpponent ? DartSide.Player : DartSide.Opponent; }
        }

        public DartsMatch(int seed)
        {
            Seed = seed;
            Round = 1;
            Turn = DartSide.Player;
        }

        /// <summary>
        /// 세이브에서 되살린다. 점수도 차례도 전부 기록에서 다시 계산한다 -
        /// 기록이 곧 상태이므로 두 벌이 어긋날 길이 없다.
        /// </summary>
        public static DartsMatch Restore(int seed, IEnumerable<DartEntry> entries)
        {
            DartsMatch match = new DartsMatch(seed);

            if (entries != null)
                foreach (DartEntry entry in entries)
                    match.Apply(entry);

            return match;
        }

        /// <summary>이번 라운드에서 그쪽이 아직 던질 것이 남았는가.</summary>
        public int Remaining(DartSide side)
        {
            int thrown = side == DartSide.Player ? VisitCountPlayer : VisitCountOpponent;
            return DartsPerVisit - thrown;
        }

        /// <summary>한 발. 자리로 채점까지 끝낸다.</summary>
        public DartEntry Throw(float x, float y)
        {
            DartHit hit = DartBoard.ScoreAt(x, y);

            DartEntry entry = new DartEntry
            {
                Round = Round,
                Side = Turn,
                X = x,
                Y = y,
                Points = hit.Points,
                Code = hit.Code,
            };

            Apply(entry);
            return entry;
        }

        private void Apply(DartEntry entry)
        {
            if (IsOver) return;

            log.Add(entry);
            ThrowIndex++;

            if (entry.Side == DartSide.Player)
            {
                TotalPlayer += entry.Points;
                VisitCountPlayer++;
                VisitSumPlayer += entry.Points;

                if (entry.Code.Length > 0 && entry.Code[0] == 'T') Triples++;
                if (entry.Code == "25" || entry.Code == "50") Bulls++;
            }
            else
            {
                TotalOpponent += entry.Points;
                VisitCountOpponent++;
                VisitSumOpponent += entry.Points;
            }

            // 플레이어가 3발을 다 던지면 상대 차례. 상대까지 다 던지면 라운드가 닫힌다.
            if (Turn == DartSide.Player && VisitCountPlayer >= DartsPerVisit)
                Turn = DartSide.Opponent;

            if (VisitCountPlayer >= DartsPerVisit && VisitCountOpponent >= DartsPerVisit)
                CloseRound();
        }

        private void CloseRound()
        {
            RoundsClosed++;
            if (VisitSumPlayer > VisitSumOpponent) RoundsWonPlayer++;
            if (VisitSumPlayer > BestVisit) BestVisit = VisitSumPlayer;

            // 정규 라운드를 다 던졌으면 승부를 본다. 동점이면 한 라운드 더.
            if (Round >= BaseRounds && TotalPlayer != TotalOpponent)
            {
                IsOver = true;
                return;
            }

            Round++;
            Turn = DartSide.Player;
            VisitCountPlayer = 0;
            VisitCountOpponent = 0;
            VisitSumPlayer = 0;
            VisitSumOpponent = 0;
        }
    }

    /// <summary>
    /// 상대의 다트. 어디를 노릴지가 난이도의 절반이다 - 손이 흔들리는 상대가
    /// 트리플 20 을 노리면 옆의 1 과 5 에 꽂힌다. 그래서 하수는 넓은 한가운데를 노린다.
    /// </summary>
    public static class DartsAi
    {
        /// <summary>
        /// 흩어짐(판 단위). 불을 노리는 한 흩어짐은 점수를 거의 못 바꾼다 -
        /// 판 어디에 맞아도 싱글 평균이 나오기 때문이다. 그래서 저티어의 차이는
        /// 흩어짐이 아니라 조준(불 → 트리플 19 → 트리플 20)이 만든다.
        /// 방문당 기대 점수: 38 / 46 / 50 / 59 / 74.
        /// </summary>
        public static float SigmaFor(int tier)
        {
            switch (tier)
            {
                case 0: return 0.36f;
                case 1: return 0.13f;
                case 2: return 0.13f;
                case 3: return 0.10f;
                default: return 0.075f;
            }
        }

        /// <summary>난이도별 조준점. 하수는 불 근처, 중수는 트리플 19, 고수는 트리플 20.</summary>
        public static void AimFor(int tier, out float x, out float y)
        {
            const float tripleMid = (DartBoard.TripleInner + DartBoard.TripleOuter) * 0.5f;

            if (tier <= 1)
            {
                x = 0f;
                y = 0f;
                return;
            }

            int index = tier == 2 ? Array.IndexOf(DartBoard.Sectors, 19) : 0;
            float angle = DartBoard.SectorCenterAngle(index);

            x = (float)Math.Sin(angle) * tripleMid;
            y = (float)Math.Cos(angle) * tripleMid;
        }

        /// <summary>상대의 한 발. (시드, 순번)으로 결정되므로 다시 열어도 같은 자리에 꽂힌다.</summary>
        public static void BotDart(int seed, int throwIndex, int tier, out float x, out float y)
        {
            float aimX, aimY;
            AimFor(tier, out aimX, out aimY);

            float u1 = AimMath.Uniform(seed, throwIndex * 2);
            float u2 = AimMath.Uniform(seed, throwIndex * 2 + 1);
            if (u1 < 1e-6f) u1 = 1e-6f;

            double radius = SigmaFor(tier) * Math.Sqrt(-2.0 * Math.Log(u1));
            double angle = 2.0 * Math.PI * u2;

            x = aimX + (float)(radius * Math.Cos(angle));
            y = aimY + (float)(radius * Math.Sin(angle));
        }
    }
}
