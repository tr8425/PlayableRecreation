using System;
using System.Collections.Generic;

namespace Throwing.Core
{
    /// <summary>진행 기록 한 줄.</summary>
    public struct ThrowEntry
    {
        public int Inning;
        public ThrowSide Side;
        public float Distance;
        public int Points;
        public bool Ringer;
    }

    /// <summary>
    /// 던지는 게임 한 판. Verse 를 모른다 - 규칙만 있고 그림도 입력도 없다.
    ///
    /// 이닝마다 양쪽이 번갈아 던지고, 이닝이 끝나면 채점한다.
    /// 승부는 이닝 끝에서만 난다 - 마지막 한 발을 남겨두고 판이 끝나는 일은 없다.
    /// </summary>
    public sealed class ThrowMatch
    {
        private readonly ThrowRules rules;
        private readonly List<float> playerThrows = new List<float>();
        private readonly List<float> opponentThrows = new List<float>();
        private readonly List<ThrowEntry> log = new List<ThrowEntry>();

        public int Seed { get; private set; }
        public int Inning { get; private set; }
        public int ScorePlayer { get; private set; }
        public int ScoreOpponent { get; private set; }
        public int Ringers { get; private set; }
        public ThrowSide Turn { get; private set; }

        /// <summary>지금까지 던진 총 횟수. 상대의 흩어짐을 시드에서 뽑는 데 쓴다.</summary>
        public int ThrowIndex { get; private set; }

        public ThrowRules Rules
        {
            get { return rules; }
        }

        public IReadOnlyList<ThrowEntry> Log
        {
            get { return log; }
        }

        public IReadOnlyList<float> PlayerThrows
        {
            get { return playerThrows; }
        }

        public IReadOnlyList<float> OpponentThrows
        {
            get { return opponentThrows; }
        }

        public bool IsOver { get; private set; }

        public ThrowSide Winner
        {
            get { return ScorePlayer >= ScoreOpponent ? ThrowSide.Player : ThrowSide.Opponent; }
        }

        public ThrowMatch(ThrowRules rules, int seed, ThrowSide first)
        {
            this.rules = rules;
            Seed = seed;
            Inning = 1;
            Turn = first;
        }

        /// <summary>세이브에서 되살린다. 이닝 경계에서만 저장하므로 던지던 중간은 없다.</summary>
        public static ThrowMatch Restore(ThrowRules rules, int seed, int inning, int throwIndex,
                                         int scorePlayer, int scoreOpponent, int ringers, ThrowSide turn)
        {
            ThrowMatch match = new ThrowMatch(rules, seed, turn)
            {
                Inning = inning,
                ThrowIndex = throwIndex,
                ScorePlayer = scorePlayer,
                ScoreOpponent = scoreOpponent,
                Ringers = ringers,
            };

            match.CheckOver();
            return match;
        }

        /// <summary>이번 이닝에서 그쪽이 아직 던질 것이 남았는가.</summary>
        public int Remaining(ThrowSide side)
        {
            List<float> thrown = side == ThrowSide.Player ? playerThrows : opponentThrows;
            return rules.ThrowsPerInning - thrown.Count;
        }

        /// <summary>한 발. 거리는 0(정중앙)에서 커질수록 빗나간 것이다.</summary>
        public void Throw(float distance)
        {
            if (IsOver) return;

            distance = distance < 0f ? 0f : distance;
            bool ringer = distance <= rules.RingerRadius;

            List<float> thrown = Turn == ThrowSide.Player ? playerThrows : opponentThrows;
            thrown.Add(distance);
            ThrowIndex++;

            if (ringer) Ringers += Turn == ThrowSide.Player ? 1 : 0;

            int points = 0;

            if (rules.PerThrowScoring)
            {
                points = ringer ? rules.RingerPoints : distance <= rules.ScoreRadius ? rules.NearPoints : 0;
                Add(Turn, points);
            }

            log.Add(new ThrowEntry
            {
                Inning = Inning,
                Side = Turn,
                Distance = distance,
                Points = points,
                Ringer = ringer,
            });

            // 한 발씩 번갈아 던진다. 한쪽이 다 던졌으면 남은 쪽이 이어서 던진다.
            ThrowSide other = Turn == ThrowSide.Player ? ThrowSide.Opponent : ThrowSide.Player;
            if (Remaining(other) > 0) Turn = other;

            if (Remaining(ThrowSide.Player) == 0 && Remaining(ThrowSide.Opponent) == 0) CloseInning();
        }

        /// <summary>이닝을 닫고 채점한다.</summary>
        private void CloseInning()
        {
            if (!rules.PerThrowScoring) ScoreProximity();

            playerThrows.Clear();
            opponentThrows.Clear();

            Inning++;
            Turn = ThrowSide.Player;

            CheckOver();
        }

        /// <summary>
        /// 편자막대식 채점. 꽂힌 것은 그것대로 점수가 되고,
        /// 나머지는 상대의 가장 가까운 것보다 안쪽에 있는 것만 인정한다 - 한 이닝에 한쪽만 얻는다.
        /// </summary>
        private void ScoreProximity()
        {
            int playerRingers = CountRingers(playerThrows);
            int opponentRingers = CountRingers(opponentThrows);

            Add(ThrowSide.Player, playerRingers * rules.RingerPoints);
            Add(ThrowSide.Opponent, opponentRingers * rules.RingerPoints);

            float playerBest = BestNonRinger(playerThrows);
            float opponentBest = BestNonRinger(opponentThrows);

            if (playerBest > rules.ScoreRadius && opponentBest > rules.ScoreRadius) return;
            if (Math.Abs(playerBest - opponentBest) < 0.0001f) return;

            bool playerCloser = playerBest < opponentBest;
            List<float> winner = playerCloser ? playerThrows : opponentThrows;
            float threshold = playerCloser ? opponentBest : playerBest;

            int points = 0;
            for (int i = 0; i < winner.Count; i++)
                if (winner[i] > rules.RingerRadius && winner[i] < threshold && winner[i] <= rules.ScoreRadius)
                    points += rules.NearPoints;

            Add(playerCloser ? ThrowSide.Player : ThrowSide.Opponent, points);
        }

        private int CountRingers(List<float> thrown)
        {
            int count = 0;
            for (int i = 0; i < thrown.Count; i++) if (thrown[i] <= rules.RingerRadius) count++;
            return count;
        }

        private float BestNonRinger(List<float> thrown)
        {
            float best = float.MaxValue;
            for (int i = 0; i < thrown.Count; i++)
                if (thrown[i] > rules.RingerRadius && thrown[i] < best) best = thrown[i];
            return best;
        }

        private void Add(ThrowSide side, int points)
        {
            if (points <= 0) return;

            if (side == ThrowSide.Player) ScorePlayer += points;
            else ScoreOpponent += points;
        }

        private void CheckOver()
        {
            IsOver = ScorePlayer >= rules.TargetScore || ScoreOpponent >= rules.TargetScore;
        }
    }
}
