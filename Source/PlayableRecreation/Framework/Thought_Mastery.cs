using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 그 게임을 한 식민자가 얻는 기분 생각. 문구가 고정이 아니라 **플레이어의 숙련도 단계**를
    /// 따라간다 — 아무것도 못 깼으면 "어떻게 하는지도 모르고", 전부 깼으면 "마스터했다".
    /// </summary>
    public class Thought_Mastery : Thought_Memory
    {
        private static Dictionary<ThoughtDef, MiniGameDef> ownerCache;

        public override int CurStageIndex
        {
            get
            {
                if (def.stages == null || def.stages.Count == 0) return 0;
                return Mathf.Clamp(Mastery.CurrentTier(OwnerOf(def)), 0, def.stages.Count - 1);
            }
        }

        /// <summary>이 생각을 자기 것이라고 선언한 게임을 찾는다.</summary>
        public static MiniGameDef OwnerOf(ThoughtDef thought)
        {
            if (ownerCache == null)
            {
                ownerCache = new Dictionary<ThoughtDef, MiniGameDef>();

                foreach (MiniGameDef game in DefDatabase<MiniGameDef>.AllDefsListForReading)
                    if (game.playThought != null) ownerCache[game.playThought] = game;
            }

            MiniGameDef owner;
            return ownerCache.TryGetValue(thought, out owner) ? owner : null;
        }
    }
}
