using System.Collections.Generic;
using System.Globalization;
using PlayableRecreation;
using Throwing.Core;
using Verse;

namespace Throwing
{
    /// <summary>
    /// 가구에 남는 던지기 판. 이닝 중간에 닫아도 그 자리가 남는다 -
    /// 상대의 다음 발은 (시드, 순번)으로 이미 정해져 있고,
    /// 이번 이닝에 던진 것들은 기록에 들어 있다.
    ///
    /// 기록은 한 줄에 한 발씩 문자열로 접어 둔다. 구조체를 그대로 저장하려면
    /// Core 가 Verse 를 알아야 하는데, 그것이 이 프로젝트가 하지 않기로 한 일이다.
    /// </summary>
    public class ThrowSaveData : MiniGameSaveData
    {
        public int seed;
        public int inning = 1;
        public int throwIndex;
        public int scorePlayer;
        public int scoreOpponent;
        public int ringers;
        public bool opponentTurn;
        public List<string> log = new List<string>();

        public ThrowSaveData()
        {
        }

        public ThrowSaveData(ThrowMatch match)
        {
            seed = match.Seed;
            inning = match.Inning;
            throwIndex = match.ThrowIndex;
            scorePlayer = match.ScorePlayer;
            scoreOpponent = match.ScoreOpponent;
            ringers = match.Ringers;
            opponentTurn = match.Turn == ThrowSide.Opponent;

            for (int i = 0; i < match.Log.Count; i++) log.Add(Encode(match.Log[i]));
        }

        public ThrowMatch ToMatch(ThrowRules rules)
        {
            return ThrowMatch.Restore(rules, seed, inning, throwIndex, scorePlayer, scoreOpponent,
                                      ringers, opponentTurn ? ThrowSide.Opponent : ThrowSide.Player,
                                      Decode());
        }

        private List<ThrowEntry> Decode()
        {
            List<ThrowEntry> entries = new List<ThrowEntry>();
            if (log == null) return entries;

            for (int i = 0; i < log.Count; i++)
            {
                ThrowEntry entry;
                if (TryDecode(log[i], out entry)) entries.Add(entry);
            }

            return entries;
        }

        private static string Encode(ThrowEntry entry)
        {
            return string.Join("|", new[]
            {
                entry.Inning.ToString(CultureInfo.InvariantCulture),
                entry.Side == ThrowSide.Player ? "P" : "O",
                entry.Distance.ToString("R", CultureInfo.InvariantCulture),
                entry.Points.ToString(CultureInfo.InvariantCulture),
                entry.Ringer ? "1" : "0",
            });
        }

        /// <summary>못 읽는 줄은 버린다. 한 줄이 상해도 나머지 판은 살아남아야 한다.</summary>
        private static bool TryDecode(string text, out ThrowEntry entry)
        {
            entry = new ThrowEntry();
            if (text.NullOrEmpty()) return false;

            string[] parts = text.Split('|');
            if (parts.Length != 5) return false;

            int inning, points;
            float distance;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out inning)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out distance)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out points)) return false;

            entry = new ThrowEntry
            {
                Inning = inning,
                Side = parts[1] == "O" ? ThrowSide.Opponent : ThrowSide.Player,
                Distance = distance,
                Points = points,
                Ringer = parts[4] == "1",
            };

            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref inning, "inning", 1);
            Scribe_Values.Look(ref throwIndex, "throwIndex", 0);
            Scribe_Values.Look(ref scorePlayer, "scorePlayer", 0);
            Scribe_Values.Look(ref scoreOpponent, "scoreOpponent", 0);
            Scribe_Values.Look(ref ringers, "ringers", 0);
            Scribe_Values.Look(ref opponentTurn, "opponentTurn", false);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);

            if (log == null) log = new List<string>();
        }
    }
}
