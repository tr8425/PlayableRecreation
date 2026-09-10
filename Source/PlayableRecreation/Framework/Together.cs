using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 몰입 모드 2칸 — "상대를 골라 둘이 함께 둔다"의 자격 판정.
    ///
    /// 종족 목록을 쓰지 않는다. 무엇인지가 아니라 **할 수 있는지**를 묻는다.
    /// 목록으로 적으면 새 모드가 나올 때마다 틀리고, 우리가 모르는 폰은 전부 막힌다.
    ///
    /// 여가 욕구(<c>needs.joy</c>)로 묻지 않는 이유가 있다. 바닐라 Joy NeedDef 는
    /// colonistsOnly 이고 developmentalStageFilter 가 Adult(13세 이상)라,
    /// 어린이·방문객·죄수·노예는 needs.joy 가 아예 null 이다. 그걸로 거르면
    /// 우리가 넣기로 한 두 부류가 그대로 잘려 나간다.
    /// </summary>
    public static class Together
    {
        /// <summary>2칸이 켜져 있는가. 부모(몰입 모드)가 꺼져 있으면 이것도 꺼진 것이다.</summary>
        public static bool Enabled
        {
            get
            {
                PRSettings settings = PRMod.Settings;
                return settings != null && settings.immersionMode && settings.playTogether;
            }
        }

        /// <summary>
        /// 이 게임이 상대를 앉힐 만한가. 승부가 없으면 상대를 골라도 뜻이 없고,
        /// <b>가구 정원이 하나면 애초에 둘이 설 수 없다.</b>
        ///
        /// 정원은 <see cref="SeatCount"/> 가 바닐라 Job 에서 그대로 가져온다. 펀칭백 ·
        /// 다트 · 아케이드가 1 이다 — 예약이 둘째 폰을 거절하므로, 상대를 고르게 해 두면
        /// 목록은 뜨는데 아무 일도 안 일어나고 2500틱 뒤 조용히 취소된다(QA-04).
        /// <b>크기가 아니라 정원이 기준이다</b> — 1×1 이라서가 아니라 자리가 하나라서다.
        /// </summary>
        public static bool AppliesTo(MiniGameDef game)
        {
            return Enabled && game != null && game.hasMatch && SeatCount(game) >= 2;
        }

        /// <summary>
        /// 이 가구를 몇 명이 나눠 쓸 수 있는가. **바닐라와 같은 값이어야 한다.**
        ///
        /// <c>ReservationManager</c> 는 같은 대상·같은 레이어에 걸린 예약의 <c>MaxPawns</c> 가
        /// 지금 요청과 다르면 **개수와 무관하게 즉시 거절**한다. 그래서 2 로 고정하면
        /// 포커(4)·편자(3)·망원경(1) 같은 가구에서 바닐라 폰이 그 가구를 아예 못 쓰게 된다.
        /// 값은 게임이 이미 들고 있는 <see cref="MiniGameDef.vanillaJob"/> 에서 가져온다.
        /// </summary>
        public static int SeatCount(MiniGameDef game)
        {
            if (game != null && game.vanillaJob != null && game.vanillaJob.joyMaxParticipants > 0)
                return game.vanillaJob.joyMaxParticipants;

            return 2;
        }

        /// <summary>가구에서 게임을 얻어 같은 값을 구한다. 두 JobDriver 가 같은 수를 써야 예약이 붙는다.</summary>
        public static int SeatCount(Thing board)
        {
            if (board == null) return 2;

            CompMiniGame comp = board.TryGetComp<CompMiniGame>();
            return SeatCount(comp != null ? comp.Game : null);
        }

        /// <summary>자격이 없는 이유. 자격이 있으면 null 이다.</summary>
        public enum Refusal
        {
            None,
            TooYoung,       // 아기·유아
            ChildrenOff,    // 어린이인데 설정이 꺼져 있다
            VisitorsOff,    // 방문객인데 설정이 꺼져 있다
            Incapable,      // 의식·조작·시각
            TooFar,         // 거리 상한 밖이거나 길이 없다
            Drafted,        // 징집돼 있다
            Downed,         // 쓰러졌거나 제정신이 아니다
            InGame,         // 이미 다른 판에 붙잡혀 있다
            Busy            // 그 밖에 부를 수 없는 상태
        }

        /// <summary>
        /// 세 가지만 묻는다.
        /// 1. 우리 Job 을 받아 걸어올 수 있나  2. 앉아서 판을 다룰 수 있나  3. 아기가 아닌가
        /// 통과하면 종족을 묻지 않는다 — 바닐라 메카노이드는 2번에서 저절로 떨어지고,
        /// 여가를 아는 메카를 넣는 모드가 있으면 그 모드가 값을 올렸을 테니 통과하는 게 맞다.
        /// </summary>
        public static Refusal Judge(Pawn candidate, Pawn initiator, Thing board, MiniGameDef game)
        {
            if (candidate == null || board == null) return Refusal.Busy;
            if (candidate == initiator) return Refusal.Busy;
            if (!candidate.Spawned || candidate.Dead || candidate.Destroyed) return Refusal.Busy;
            if (board.Map == null || candidate.Map != board.Map) return Refusal.TooFar;

            PRSettings settings = PRMod.Settings;

            // 3. 아기가 아닌가 — 0~3세(HumanlikeBaby)는 DevelopmentalStage.Baby 다.
            //    어린이·프리틴(3~13, Child)은 통과한다.
            DevelopmentalStage stage = candidate.DevelopmentalStage;
            if (stage == DevelopmentalStage.Newborn || stage == DevelopmentalStage.Baby)
                return Refusal.TooYoung;

            if (stage == DevelopmentalStage.Child && !settings.playTogetherChildren)
                return Refusal.ChildrenOff;

            // 손님인가 — 우리 팩션이 아니면서 적대가 아닌 사람.
            if (!IsColonyMember(candidate))
            {
                if (!settings.playTogetherVisitors) return Refusal.VisitorsOff;
                if (candidate.HostileTo(Faction.OfPlayer)) return Refusal.Busy;
            }

            // 2. 앉아서 판을 다룰 수 있나.
            if (!CanHandleBoard(candidate, game)) return Refusal.Incapable;

            // 1. Job 을 받아 걸어올 수 있나.
            if (candidate.Downed || candidate.InMentalState) return Refusal.Downed;

            // 징집된 사람은 부르지 않는다. 명령을 받아 서 있는 사람을 판 앞으로 끌어오면
            // 징집이 곧바로 되찾아 가므로, 붙잡히지도 않고 창만 열렸다 닫힌다(QA-02).
            if (candidate.Drafted) return Refusal.Drafted;

            if (candidate.jobs == null) return Refusal.Busy;
            if (InAnotherGame(candidate, board)) return Refusal.InGame;
            if (TogetherMatch.HeldElsewhere(candidate, board)) return Refusal.InGame;

            if (!candidate.CanReach(board, PathEndMode.Touch, Danger.Some)) return Refusal.TooFar;
            if (!WithinRange(candidate, board, settings.playTogetherRange)) return Refusal.TooFar;

            return Refusal.None;
        }

        public static bool Qualifies(Pawn candidate, Pawn initiator, Thing board, MiniGameDef game)
        {
            return Judge(candidate, initiator, board, game) == Refusal.None;
        }

        /// <summary>거절 사유를 사람이 읽는 한 줄로. 회색 항목 뒤에 붙는다.</summary>
        public static string ReasonText(Refusal refusal)
        {
            switch (refusal)
            {
                case Refusal.TooYoung:    return "PR.Together.Why.TooYoung".Translate();
                case Refusal.ChildrenOff: return "PR.Together.Why.ChildrenOff".Translate();
                case Refusal.VisitorsOff: return "PR.Together.Why.VisitorsOff".Translate();
                case Refusal.Incapable:   return "PR.Together.Why.Incapable".Translate();
                case Refusal.TooFar:      return "PR.Together.Why.TooFar".Translate();
                case Refusal.Drafted:     return "PR.Together.Why.Drafted".Translate();
                case Refusal.Downed:      return "PR.Together.Why.Downed".Translate();
                case Refusal.InGame:      return "PR.Together.Why.InGame".Translate();
                default:                  return "PR.Together.Why.Busy".Translate();
            }
        }

        /// <summary>
        /// 상대 후보. 자격이 있는 사람이 앞에, 없는 사람이 이유와 함께 뒤에 온다.
        /// 자격 없는 사람도 보여 주는 이유는 "왜 저 사람은 안 되는가"가 질문이 되기 때문이다.
        /// </summary>
        public static List<Pawn> Candidates(Pawn initiator, Thing board, MiniGameDef game)
        {
            List<Pawn> found = new List<Pawn>();
            if (board == null || board.Map == null) return found;

            IReadOnlyList<Pawn> all = board.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn pawn = all[i];
                if (pawn == initiator) continue;
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) continue;
                if (pawn.IsPrisoner || pawn.IsSlave) continue;       // 범위 밖 (§10.1)
                if (pawn.Dead || pawn.Destroyed) continue;
                if (!IsColonyMember(pawn) && pawn.HostileTo(Faction.OfPlayer)) continue;

                found.Add(pawn);
            }

            // 좋아하는 사람이 먼저 뜨도록 의견순으로 세운다 (§15 다듬기).
            // 식구가 열다섯이 넘어가면 목록이 그냥 이름 더미가 되는데,
            // 맞은편에 누구를 앉힐지는 원래 관계로 고르는 일이다.
            found.SortByDescending(delegate (Pawn pawn) { return Opinion(pawn, initiator); });

            // 이 가구 앞에서 기다리고 있는 손님은 의견과 무관하게 맨 위다.
            // 그 사람 때문에 목록을 연 것이므로 찾게 만들면 안 된다.
            Pawn waiting = TogetherInvite.WaitingAt(board);
            if (waiting != null && found.Remove(waiting)) found.Insert(0, waiting);

            return found;
        }

        /// <summary>상대가 이 사람을 어떻게 보는가. 의견을 모르는 사이는 0 이다.</summary>
        private static int Opinion(Pawn pawn, Pawn about)
        {
            if (pawn == null || about == null || pawn == about) return 0;
            if (pawn.relations == null || !pawn.RaceProps.Humanlike) return 0;

            return pawn.relations.OpinionOf(about);
        }

        // ---------- 조각 ----------

        private static bool IsColonyMember(Pawn pawn)
        {
            return pawn.Faction != null && pawn.Faction.IsPlayer;
        }

        /// <summary>의식 · 조작 · 시각. 게임이 무엇을 더 요구하는지는 게임이 안다.</summary>
        private static bool CanHandleBoard(Pawn pawn, MiniGameDef game)
        {
            if (pawn.health == null || pawn.health.capacities == null) return false;

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness)) return false;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)) return false;

            // 바닐라 메카노이드는 여기서 떨어진다 (intelligence: ToolUser).
            return pawn.RaceProps != null && pawn.RaceProps.Humanlike;
        }

        private static bool WithinRange(Pawn pawn, Thing board, int range)
        {
            if (range <= 0) return true;
            return pawn.Position.InHorDistOf(board.Position, range);
        }

        /// <summary>이미 다른 가구의 판에 앉아 있는 사람은 두 판을 동시에 두지 않는다.</summary>
        private static bool InAnotherGame(Pawn pawn, Thing board)
        {
            GameComponent_Recreation component = GameComponent_Recreation.Current;
            if (component == null) return false;

            return component.IsPlayingElsewhere(pawn, board);
        }
    }
}
