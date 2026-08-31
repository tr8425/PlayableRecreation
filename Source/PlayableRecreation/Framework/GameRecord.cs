using System.Collections.Generic;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 게임 하나의 전적. 같은 클래스를 "나의 통산"(세이브 무관 파일)과 "이 식민지"(세이브 내부)가 함께 쓴다.
    ///
    /// 승패·연승·최단 기록은 어느 게임에나 있으므로 프레임워크가 센다.
    /// 그 게임에서만 의미가 있는 숫자 세 개는 <see cref="Tallies"/> 에 담고, 이름표는 Def 가 들고 있다.
    /// </summary>
    public sealed class GameRecord : IExposable
    {
        /// <summary>난이도 단계의 상한. 기록 배열의 길이이기도 하다.</summary>
        public const int MaxTiers = 5;

        /// <summary>게임별 집계 칸 수.</summary>
        public const int TallyCount = 3;

        public int wins, losses, resigns, voided;
        public int currentStreak, longestWinStreak;
        public int flawlessWins, totalUndosUsed;
        public int totalRealSeconds;

        private List<int> tallies = NewList(TallyCount);
        private List<int> winsByTier = NewList(MaxTiers);
        private List<int> lossesByTier = NewList(MaxTiers);
        private List<int> fastestWinByTier = NewList(MaxTiers);
        private List<int> streakByTier = NewList(MaxTiers);
        private List<int> bestStreakByTier = NewList(MaxTiers);

        public int Played
        {
            get { return wins + losses; }
        }

        public int Tally(int index) { return Read(tallies, index, TallyCount); }
        public int WinsAt(int tier) { return Read(winsByTier, tier, MaxTiers); }
        public int LossesAt(int tier) { return Read(lossesByTier, tier, MaxTiers); }

        /// <summary>그 난이도의 최소 진행량 승리. 아직 이긴 적이 없으면 0.</summary>
        public int FastestWinAt(int tier) { return Read(fastestWinByTier, tier, MaxTiers); }

        public int BestStreakAt(int tier) { return Read(bestStreakByTier, tier, MaxTiers); }

        /// <summary>승률(0~1). 판이 없으면 -1 을 돌려 "–" 로 표시하게 한다.</summary>
        public float WinRateAt(int tier)
        {
            int played = WinsAt(tier) + LossesAt(tier);
            return played == 0 ? -1f : (float)WinsAt(tier) / played;
        }

        public void Record(in MatchResult result)
        {
            int tier = Clamp(result.Tier, MaxTiers);

            if (result.Tallies != null)
                for (int i = 0; i < TallyCount && i < result.Tallies.Length; i++)
                    Bump(tallies, i, TallyCount, result.Tallies[i]);

            totalUndosUsed += result.Undos;
            totalRealSeconds += Rounded(result.RealSeconds);

            if (result.Resigned) resigns++;

            if (result.Won)
            {
                wins++;
                Bump(winsByTier, tier, MaxTiers, 1);

                currentStreak++;
                if (currentStreak > longestWinStreak) longestWinStreak = currentStreak;

                Bump(streakByTier, tier, MaxTiers, 1);
                if (Read(streakByTier, tier, MaxTiers) > Read(bestStreakByTier, tier, MaxTiers))
                    Write(bestStreakByTier, tier, MaxTiers, Read(streakByTier, tier, MaxTiers));

                int fastest = Read(fastestWinByTier, tier, MaxTiers);
                if (fastest == 0 || result.Rounds < fastest)
                    Write(fastestWinByTier, tier, MaxTiers, result.Rounds);

                if (result.Flawless) flawlessWins++;
            }
            else
            {
                losses++;
                Bump(lossesByTier, tier, MaxTiers, 1);
                currentStreak = 0;
                Write(streakByTier, tier, MaxTiers, 0);
            }
        }

        /// <summary>무효화로 사라진 판. 승패에는 넣지 않는다.</summary>
        public void RecordVoid()
        {
            voided++;
        }

        public void Reset()
        {
            wins = losses = resigns = voided = 0;
            currentStreak = longestWinStreak = 0;
            flawlessWins = totalUndosUsed = totalRealSeconds = 0;

            tallies = NewList(TallyCount);
            winsByTier = NewList(MaxTiers);
            lossesByTier = NewList(MaxTiers);
            fastestWinByTier = NewList(MaxTiers);
            streakByTier = NewList(MaxTiers);
            bestStreakByTier = NewList(MaxTiers);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref wins, "wins", 0);
            Scribe_Values.Look(ref losses, "losses", 0);
            Scribe_Values.Look(ref resigns, "resigns", 0);
            Scribe_Values.Look(ref voided, "voided", 0);
            Scribe_Values.Look(ref currentStreak, "currentStreak", 0);
            Scribe_Values.Look(ref longestWinStreak, "longestWinStreak", 0);
            Scribe_Values.Look(ref flawlessWins, "flawlessWins", 0);
            Scribe_Values.Look(ref totalUndosUsed, "totalUndosUsed", 0);
            Scribe_Values.Look(ref totalRealSeconds, "totalRealSeconds", 0);

            Scribe_Collections.Look(ref tallies, "tallies", LookMode.Value);
            Scribe_Collections.Look(ref winsByTier, "winsByTier", LookMode.Value);
            Scribe_Collections.Look(ref lossesByTier, "lossesByTier", LookMode.Value);
            Scribe_Collections.Look(ref fastestWinByTier, "fastestWinByTier", LookMode.Value);
            Scribe_Collections.Look(ref streakByTier, "streakByTier", LookMode.Value);
            Scribe_Collections.Look(ref bestStreakByTier, "bestStreakByTier", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tallies = Fix(tallies, TallyCount);
                winsByTier = Fix(winsByTier, MaxTiers);
                lossesByTier = Fix(lossesByTier, MaxTiers);
                fastestWinByTier = Fix(fastestWinByTier, MaxTiers);
                streakByTier = Fix(streakByTier, MaxTiers);
                bestStreakByTier = Fix(bestStreakByTier, MaxTiers);
            }
        }

        // ---------- 고정 길이 배열 유틸 ----------

        private static List<int> NewList(int length)
        {
            List<int> list = new List<int>(length);
            for (int i = 0; i < length; i++) list.Add(0);
            return list;
        }

        private static List<int> Fix(List<int> list, int length)
        {
            if (list == null) return NewList(length);
            while (list.Count < length) list.Add(0);
            return list;
        }

        private static int Clamp(int index, int length)
        {
            if (index < 0) return 0;
            return index >= length ? length - 1 : index;
        }

        private static int Read(List<int> list, int index, int length)
        {
            index = Clamp(index, length);
            return list != null && index < list.Count ? list[index] : 0;
        }

        private static void Write(List<int> list, int index, int length, int value)
        {
            index = Clamp(index, length);
            if (list != null && index < list.Count) list[index] = value;
        }

        private static void Bump(List<int> list, int index, int length, int delta)
        {
            Write(list, index, length, Read(list, index, length) + delta);
        }

        private static int Rounded(float seconds)
        {
            return seconds <= 0f ? 0 : (int)(seconds + 0.5f);
        }
    }
}
