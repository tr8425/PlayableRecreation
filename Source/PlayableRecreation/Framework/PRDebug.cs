using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// QA 용 개발자 도구. **개발자 모드에서만 보인다** — 바닐라가 이 특성을 그때만 훑는다.
    ///
    /// 여기 있는 것은 전부 <b>기다림을 건너뛰는 것</b>이다. 손님이 청하는 확률은 게임 안 한
    /// 시간마다 8% 이고 기다림은 두 시간이라, 그대로 두면 체크리스트 5장 하나에 게임 안
    /// 하루가 든다. 확률과 시간은 그대로 두고 여기서만 앞당긴다 — 검사하는 대상을
    /// 검사하기 편하게 고치면 그건 다른 것을 검사한 것이 된다.
    ///
    /// 문구는 영어로 둔다. 개발자 메뉴는 게임 언어의 글꼴을 쓰므로, 영어로 놀 때
    /// 한글이 네모로 보이는 자리다. 주석은 늘 하던 대로 한국어다.
    /// </summary>
    public static class PRDebug
    {
        private const string Cat = "Playable Recreation";

        [DebugAction(Cat, "Invite a guest now", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InviteNow()
        {
            string why = TogetherInvite.ForceInvite();

            if (why == null) Messages.Message("PR debug: a guest was invited.",
                MessageTypeDefOf.TaskCompletion, false);
            else Messages.Message("PR debug: could not invite — " + why,
                MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "End the wait here (play alone)", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EndWaitHere()
        {
            foreach (Thing thing in TargetsUnderMouse())
            {
                if (!TogetherInvite.ExpireNow(thing)) continue;

                Messages.Message("PR debug: wait ended at " + thing.LabelCap + ".",
                    MessageTypeDefOf.TaskCompletion, false);
                return;
            }

            Messages.Message("PR debug: nobody is waiting there.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Dump 2-seat state", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpState()
        {
            string text = "[Playable Recreation] 2-seat state\n"
                + TogetherMatch.Describe() + "\n"
                + TogetherInvite.Describe() + "\n"
                + Sessions();

            Log.Message(text);
            Messages.Message("PR debug: state written to the log.", MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Cat, "Backstory affinity of this pawn", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AffinityOf(Pawn pawn)
        {
            if (pawn == null) return;

            string text = "[Playable Recreation] backstory affinity — " + pawn.LabelShortCap + "\n";

            List<BackstoryDef> mine = pawn.story != null ? pawn.story.AllBackstories : null;
            text += "  backstories: " + (mine == null || mine.Count == 0 ? "(none)" : Names(mine)) + "\n";

            bool any = false;
            foreach (MiniGameDef game in DefDatabase<MiniGameDef>.AllDefsListForReading)
            {
                BackstoryAffinity affinity = GameEntry.AffinityFor(game, pawn);
                if (affinity == null) continue;

                any = true;
                text += "  " + game.defName + ": +" + affinity.levels
                    + " (" + affinity.backstory.defName + ")"
                    + ", suggested tier " + GameEntry.TierForPawn(game, pawn)
                    + " of " + game.difficultyCount + "\n";
            }

            if (!any) text += "  no game names any of these backstories\n";

            Log.Message(text.TrimEnd('\n'));
            Messages.Message("PR debug: affinity written to the log.", MessageTypeDefOf.TaskCompletion, false);
        }

        // ---------- 조각 ----------

        private static string Sessions()
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return "  sessions: (no component)";

            List<Thing> boards = new List<Thing>();
            List<Map> maps = Find.Maps;

            for (int i = 0; i < maps.Count; i++)
            {
                List<Thing> all = maps[i].listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
                for (int j = 0; j < all.Count; j++)
                    if (component.SessionFor(all[j]) != null) boards.Add(all[j]);
            }

            if (boards.Count == 0) return "  sessions: (none)";

            string text = "";
            for (int i = 0; i < boards.Count; i++)
            {
                GameSession session = component.SessionFor(boards[i]);
                text += "  session: " + boards[i].ToStringSafe()
                    + " — " + session.game.ToStringSafe()
                    + ", seated " + session.seatedPawn.ToStringSafe()
                    + ", opponent " + session.opponentPawn.ToStringSafe()
                    + ", tier " + session.tier + "\n";
            }

            return text.TrimEnd('\n');
        }

        private static string Names(List<BackstoryDef> list)
        {
            string text = "";
            for (int i = 0; i < list.Count; i++)
                text += (i > 0 ? ", " : "") + list[i].defName;

            return text;
        }

        /// <summary>커서 밑의 것들. ToolMap 은 좌표만 주므로 우리가 직접 집는다.</summary>
        private static IEnumerable<Thing> TargetsUnderMouse()
        {
            Map map = Find.CurrentMap;

            // 우리 네임스페이스에도 UI 가 있어 가린다. 바닐라 쪽을 온전한 이름으로 부른다.
            IntVec3 cell = Verse.UI.MouseCell();

            if (map == null || !cell.InBounds(map)) yield break;

            List<Thing> here = cell.GetThingList(map);
            for (int i = 0; i < here.Count; i++) yield return here[i];
        }
    }
}
