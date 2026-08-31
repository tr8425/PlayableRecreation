using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    /// <summary>남겨둔 판이 흐트러진 이유. 그대로 번역 키의 일부가 된다.</summary>
    public enum InvalidationReason
    {
        Combat,
        Cleaning,
        Repair,
        Damaged,
        Moved,
        Destroyed,
        Expired,
    }

    /// <summary>
    /// "판은 진짜 물건이다" 를 지키는 감시자.
    ///
    /// 두는 중인 판은 검사하지 않는다. 흐트러졌는지는 언제나 돌아왔을 때 알게 된다.
    /// Harmony 패치 없이 폴링으로만 감지해 다른 모드와 충돌하지 않는다.
    /// </summary>
    public static class Invalidation
    {
        /// <summary>청소 감지 반경. 가구 주변 한 뼘만 본다.</summary>
        public const float FilthRadius = 3.9f;

        /// <summary>전투 감지 반경.</summary>
        public const float CombatRadius = 12f;

        public static InvalidationReason? Check(GameSession session)
        {
            if (session == null) return null;

            // 가구가 사라진 것은 설정과 무관하게 판이 없어진 것이다.
            if (!session.BoardAlive) return InvalidationReason.Destroyed;

            PRSettings settings = PRMod.Settings;
            if (!settings.invalidateSessions) return null;

            Thing board = session.board;

            if (settings.invalidateOnMove && session.boardPosition.IsValid
                && board.Position != session.boardPosition)
                return InvalidationReason.Moved;

            if (board.def.useHitPoints)
            {
                if (settings.invalidateOnDamage && board.HitPoints < session.boardHitPoints)
                    return InvalidationReason.Damaged;

                if (settings.invalidateOnRepair && board.HitPoints > session.boardHitPoints)
                    return InvalidationReason.Repair;
            }

            if (settings.invalidateOnCombat && HostileNearby(board))
                return InvalidationReason.Combat;

            // 오물이 줄었다 = 누가 쓸고 지나갔다. 늘어난 것은 무효화가 아니라 기준선만 올린다.
            if (settings.invalidateOnCleaning)
            {
                int filth = CountNearbyFilth(board);
                if (filth < session.nearbyFilth) return InvalidationReason.Cleaning;
                session.nearbyFilth = filth;
            }

            if (settings.invalidateOnExpiry && Find.TickManager != null)
            {
                int limit = Mathf.Max(1, settings.sessionExpiryDays) * GenDate.TicksPerDay;
                if (Find.TickManager.TicksGame - session.lastPlayedTick > limit)
                    return InvalidationReason.Expired;
            }

            return null;
        }

        public static int CountNearbyFilth(Thing board)
        {
            if (board == null || !board.Spawned) return 0;

            Map map = board.Map;
            if (map == null) return 0;

            int count = 0;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(board.Position, FilthRadius, true))
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Filth) count++;
            }

            return count;
        }

        /// <summary>
        /// 가구 근처에 적대적인 자가 서 있는가.
        /// attackTargetsCache 대신 폰 목록을 직접 훑어 버전 간 API 변화에 영향을 받지 않는다.
        /// </summary>
        private static bool HostileNearby(Thing board)
        {
            Map map = board.Map;
            if (map == null) return false;

            Faction player = Faction.OfPlayer;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Dead || pawn.Downed) continue;
                if (!pawn.HostileTo(player)) continue;
                if (pawn.Position.InHorDistOf(board.Position, CombatRadius)) return true;
            }

            return false;
        }

        /// <summary>
        /// 무효화를 알린다. 편지가 아니라 상단 토스트 한 줄 — 스팸이 되지 않게.
        /// 다만 이 시스템을 처음 겪는 판에는 왜 이런 일이 벌어졌는지 편지로 한 번만 설명한다.
        /// </summary>
        public static void Notify(GameSession session, InvalidationReason reason)
        {
            string message = ("PR.Invalidation." + reason + ".Msg").Translate();
            Thing board = session != null && session.BoardAlive ? session.board : null;

            if (board != null) Messages.Message(message, board, MessageTypeDefOf.NeutralEvent, false);
            else Messages.Message(message, MessageTypeDefOf.NeutralEvent, false);

            if (PRMod.Settings.invalidationLetterSent) return;

            PRMod.Settings.invalidationLetterSent = true;
            PRMod.Settings.Write();

            Find.LetterStack.ReceiveLetter(
                "PR.Invalidation.Letter.Label".Translate(),
                "PR.Invalidation.Letter.Text".Translate(message),
                LetterDefOf.NeutralEvent,
                board != null ? (LookTargets)board : LookTargets.Invalid);
        }
    }
}
