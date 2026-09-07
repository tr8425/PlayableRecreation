using System.Collections.Generic;
using System.Globalization;
using PlayableRecreation;
using Roulette.Core;
using Verse;

namespace Roulette
{
    /// <summary>
    /// 가구에 남는 룰렛 판. 저장하는 것은 시드·목표액과 스핀 기록뿐이다 -
    /// 뱅크롤도 승패도 기록에서 다시 계산되므로 두 벌이 어긋날 길이 없다.
    ///
    /// 기록은 한 줄에 한 스핀씩 문자열로 접어 둔다. 다트와 같은 이유다 -
    /// 구조체를 그대로 저장하려면 Core 가 Verse 를 알아야 한다.
    /// </summary>
    public class RouletteSaveData : MiniGameSaveData
    {
        public int seed;
        public int target;
        public List<string> log = new List<string>();

        public RouletteSaveData()
        {
        }

        public RouletteSaveData(RouletteRun run)
        {
            seed = run.Seed;
            target = run.Target;
            for (int i = 0; i < run.Log.Count; i++) log.Add(Encode(run.Log[i]));
        }

        public RouletteRun ToRun()
        {
            List<RouletteEntry> entries = new List<RouletteEntry>();

            if (log != null)
            {
                for (int i = 0; i < log.Count; i++)
                {
                    RouletteEntry entry;
                    if (TryDecode(log[i], out entry)) entries.Add(entry);
                }
            }

            return RouletteRun.Restore(seed, target, entries);
        }

        private static string Encode(RouletteEntry entry)
        {
            return string.Join("|", new[]
            {
                entry.Index.ToString(CultureInfo.InvariantCulture),
                ((int)entry.Kind).ToString(CultureInfo.InvariantCulture),
                entry.Value.ToString(CultureInfo.InvariantCulture),
                entry.Stake.ToString(CultureInfo.InvariantCulture),
                entry.Pocket.ToString(CultureInfo.InvariantCulture),
                entry.Net.ToString(CultureInfo.InvariantCulture),
            });
        }

        /// <summary>못 읽는 줄은 버린다. 한 줄이 상해도 나머지 판은 살아남아야 한다.</summary>
        private static bool TryDecode(string text, out RouletteEntry entry)
        {
            entry = new RouletteEntry();
            if (text.NullOrEmpty()) return false;

            string[] parts = text.Split('|');
            if (parts.Length != 6) return false;

            int index, kind, value, stake, pocket, net;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out kind)) return false;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out stake)) return false;
            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out pocket)) return false;
            if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out net)) return false;

            if (kind < (int)BetKind.Straight || kind > (int)BetKind.Dozen3) return false;

            entry = new RouletteEntry
            {
                Index = index,
                Kind = (BetKind)kind,
                Value = value,
                Stake = stake,
                Pocket = pocket,
                Net = net,
            };

            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed", 0);
            Scribe_Values.Look(ref target, "target", RouletteRun.StartBankroll);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);

            if (log == null) log = new List<string>();
        }
    }
}
