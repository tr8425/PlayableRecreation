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

        /// <summary>
        /// 판 앞에 있던 사람이 없어졌다 — 죽음·납치·추방·캐러밴·맵 이탈·적대화.
        /// 값을 여럿으로 쪼개지 않는다. 번역 키만 늘고 플레이어가 얻는 것은 같다.
        /// 문구에서 누가 없는지만 말한다.
        /// </summary>
        OpponentGone,
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

            // 폰이라는 축. 둘이 두던 판에서만 잔다 - 혼자 두던 판은 예전대로
            // 앉았던 사람이 떠나도 가구 위에 남는다. 다른 사람이 이어 두면 그만이기 때문이다.
            //
            // 다만 둘인 판에서는 **상대만 보지 않는다.** 앉았던 쪽이 죽은 판을 이어 두면
            // 머리글도 전적도 두 사람 보상도 전부 없는 사람을 다루게 된다.
            if (settings.invalidateOnOpponentGone && session.opponentPawn != null
                && (Missing(session.seatedPawn, board) || Missing(session.opponentPawn, board)))
                return InvalidationReason.OpponentGone;

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

        /// <summary>
        /// 이 참가자가 없어졌는가.
        ///
        /// <b>null 은 없어진 것이 아니다.</b> 몰입 모드를 끄고 두면 <c>seatedPawn</c> 이
        /// 애초에 없고, 2칸이 아니면 <c>opponentPawn</c> 이 늘 null 이다. 없던 자리를
        /// 사라졌다고 읽으면 그런 판이 전부 무효화된다.
        ///
        /// <b>다운은 여기 넣지 않는다.</b> 다운된 폰은 여전히 <c>Spawned</c> 이고 같은 맵에
        /// 있으므로 이 식에 걸리지 않는다. 다운은 사라진 것이 아니라 자리를 뜬 것이고,
        /// 일어나면 이어 둘 수 있어야 한다.
        /// </summary>
        private static bool Missing(Pawn pawn, Thing board)
        {
            if (pawn == null) return false;
            if (pawn.Dead || pawn.Destroyed || !pawn.Spawned) return true;
            if (pawn.Map != board.Map) return true;

            return pawn.HostileTo(Faction.OfPlayer);
        }

        /// <summary>없어진 참가자. 문구와 생각이 누구를 말할지 여기서 정해진다.</summary>
        public static Pawn MissingParticipant(GameSession session)
        {
            if (session == null || !session.BoardAlive || session.opponentPawn == null) return null;

            if (Missing(session.opponentPawn, session.board)) return session.opponentPawn;
            if (Missing(session.seatedPawn, session.board)) return session.seatedPawn;

            // 여기까지 왔다면 이미 사라진 판정이 난 뒤에 상태가 또 바뀐 것이다.
            // 문구에 이름이 반드시 들어가야 하므로 아무도 못 고르는 채로 돌려주지 않는다.
            return session.opponentPawn ?? session.seatedPawn;
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
        /// 남은 사람에게 기억을 남긴다. 판이 사라진 것보다 맞은편이 사라진 것이 더 큰 일이다.
        /// 손님도 받는다 — 한 판 두던 상대가 없어진 것은 손님에게도 같은 일이기 때문이다.
        /// </summary>
        private static void RememberTheOther(GameSession session, Pawn gone)
        {
            if (!PRMod.Settings.playThought) return;

            Remember(session.seatedPawn, gone);
            Remember(session.opponentPawn, gone);
        }

        private static void Remember(Pawn pawn, Pawn gone)
        {
            if (pawn == null || pawn == gone || pawn.Dead) return;
            if (pawn.needs == null || pawn.needs.mood == null) return;

            pawn.needs.mood.thoughts.memories.TryGainMemory(PRDefOf.PR_OpponentGone);
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
            Pawn gone = reason == InvalidationReason.OpponentGone ? MissingParticipant(session) : null;

            // 이 이유만 문구에 이름이 들어간다. 누가 없는지가 곧 이야기이기 때문이다.
            string message = gone != null
                ? "PR.Invalidation.OpponentGone.Msg".Translate(gone.LabelShortCap)
                : ("PR.Invalidation." + reason + ".Msg").Translate();

            if (gone != null) RememberTheOther(session, gone);

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
