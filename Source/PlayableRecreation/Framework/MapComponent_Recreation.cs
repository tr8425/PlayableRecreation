using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 식민자가 바닐라 여가로 그 게임을 하는 동안 숙련도 보너스와 기분 생각을 얹는다.
    ///
    /// 바닐라 Job/JoyGiver/스탯은 하나도 수정하지 않고 "덧붙이기"만 한다.
    /// Harmony 패치도 쓰지 않는다 — 주기 스캔이면 충분하고, 그쪽이 호환성이 훨씬 낫다.
    /// MapComponent 는 별도 Def 없이 자동 등록된다.
    /// </summary>
    public class MapComponent_Recreation : MapComponent
    {
        private static Dictionary<JobDef, MiniGameDef> byJob;

        public MapComponent_Recreation(Map map) : base(map)
        {
        }

        private static Dictionary<JobDef, MiniGameDef> ByJob
        {
            get
            {
                if (byJob == null)
                {
                    byJob = new Dictionary<JobDef, MiniGameDef>();

                    foreach (MiniGameDef game in DefDatabase<MiniGameDef>.AllDefsListForReading)
                        if (game.vanillaJob != null) byJob[game.vanillaJob] = game;
                }

                return byJob;
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % Mastery.IntervalTicks != 0) return;
            if (ByJob.Count == 0) return;

            PRSettings settings = PRMod.Settings;
            if (!settings.masteryBonus && !settings.playThought) return;

            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.needs == null) continue;

                JobDef jobDef = pawn.CurJobDef;
                if (jobDef == null) continue;

                MiniGameDef game;
                if (!ByJob.TryGetValue(jobDef, out game)) continue;

                int tier = Mastery.CurrentTier(game);

                if (settings.masteryBonus && tier > 0) ApplyBonus(pawn, jobDef, tier);

                if (settings.playThought && game.playThought != null && pawn.needs.mood != null)
                    pawn.needs.mood.thoughts.memories.TryGainMemory(game.playThought);
            }
        }

        /// <summary>
        /// 바닐라 수치는 건드리지 않고 그 위에 얹기만 한다.
        /// 획득량은 JobDef 가 이미 들고 있는 값(joySkill · joyXpPerTick)에서 그대로 가져온다.
        /// </summary>
        private static void ApplyBonus(Pawn pawn, JobDef jobDef, int tier)
        {
            float scale = Mastery.BonusPerTier * tier * Mastery.IntervalTicks;

            if (pawn.needs.joy != null && jobDef.joyKind != null)
            {
                float factor = 1f;

                Thing building = pawn.CurJob != null ? pawn.CurJob.targetA.Thing : null;
                if (building != null) factor = building.GetStatValue(StatDefOf.JoyGainFactor);

                pawn.needs.joy.GainJoy(Mastery.BaseJoyPerTick * factor * scale, jobDef.joyKind);
            }

            if (pawn.skills != null && jobDef.joySkill != null && jobDef.joyXpPerTick > 0f)
                pawn.skills.Learn(jobDef.joySkill, jobDef.joyXpPerTick * scale);
        }
    }
}
