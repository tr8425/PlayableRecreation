using System.Collections.Generic;
using System.Globalization;
using Darts.Core;
using PlayableRecreation;
using PlayableRecreation.Core;
using Verse;

namespace Darts
{
    /// <summary>
    /// 가구에 남는 다트 판. 저장하는 것은 시드와 발 기록뿐이다 -
    /// 점수도 차례도 기록에서 다시 계산되므로 두 벌이 어긋날 길이 없다.
    ///
    /// 기록은 한 줄에 한 발씩 문자열로 접어 둔다. 구조체를 그대로 저장하려면
    /// Core 가 Verse 를 알아야 하는데, 그것이 이 프로젝트가 하지 않기로 한 일이다.
    /// </summary>
    public class DartsSaveData : MiniGameSaveData
    {
        public int seed;
        public List<string> log = new List<string>();

        public DartsSaveData()
        {
        }

        public DartsSaveData(DartsMatch match)
        {
            seed = match.Seed;
            for (int i = 0; i < match.Log.Count; i++) log.Add(Encode(match.Log[i]));
        }

        public DartsMatch ToMatch()
        {
            List<DartEntry> entries = new List<DartEntry>();

            if (log != null)
            {
                for (int i = 0; i < log.Count; i++)
                {
                    DartEntry entry;
                    if (TryDecode(log[i], out entry)) entries.Add(entry);
                }
            }

            return DartsMatch.Restore(seed, entries);
        }

        private static string Encode(DartEntry entry)
        {
            return string.Join("|", new[]
            {
                entry.Round.ToString(CultureInfo.InvariantCulture),
                entry.Side == DartSide.Player ? "P" : "O",
                entry.X.ToString("R", CultureInfo.InvariantCulture),
                entry.Y.ToString("R", CultureInfo.InvariantCulture),
                entry.Points.ToString(CultureInfo.InvariantCulture),
                entry.Code ?? string.Empty,
            });
        }

        /// <summary>못 읽는 줄은 버린다. 한 줄이 상해도 나머지 판은 살아남아야 한다.</summary>
        private static bool TryDecode(string text, out DartEntry entry)
        {
            entry = new DartEntry();
            if (text.NullOrEmpty()) return false;

            string[] parts = text.Split('|');
            if (parts.Length != 6) return false;

            int round, points;
            float x, y;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out round)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return false;
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return false;
            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out points)) return false;

            entry = new DartEntry
            {
                Round = round,
                Side = parts[1] == "O" ? DartSide.Opponent : DartSide.Player,
                X = x,
                Y = y,
                Points = points,
                Code = parts[5],
            };

            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);

            if (log == null) log = new List<string>();
        }
    }
}
