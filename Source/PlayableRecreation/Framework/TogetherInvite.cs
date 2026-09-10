using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PlayableRecreation
{
    /// <summary>
    /// 손님이 **먼저** 판 앞에 앉는다.
    ///
    /// 2칸은 여태 전부 플레이어가 청해야 시작됐다. 그러면 손님은 상대가 될 수 있는
    /// 사람이지 상대를 청하는 사람이 아니다 — 앉아서 기다리는 그림 하나가 그 차이를 만든다.
    ///
    /// <b>퀘스트로 만들지 않는다.</b> 퀘스트는 큰 편지 · 퀘스트 탭 · 기한 · 보상을 함께
    /// 데려오는 형식이라, 체스 한 판 청하는 일이 그 틀에 들어가면 숙제가 된다. 스토리텔러도
    /// IncidentDef 도 쓰지 않는다 — <see cref="GameComponent_Recreation"/> 이 이미 돌고 있고,
    /// 필요한 것은 그 위의 주사위 하나뿐이다.
    ///
    /// <b>아무도 안 오면 혼자 논다.</b> 일어나서 가 버리면 청한 적도 없는 일이 되지만,
    /// 혼자 두는 손님은 그 자리에 남아 "저 사람 아직 저기 있네" 가 된다. 혼자 두는 방법은
    /// 우리가 만들지 않는다 — 그 가구의 바닐라 여가 Job 이 이미 그것이다.
    /// </summary>
    public static class TogetherInvite
    {
        /// <summary>주사위를 굴리는 주기(틱). 2500 틱이 게임 안의 한 시간이다.</summary>
        private const int RollInterval = GenDate.TicksPerHour;

        /// <summary>한 시간마다의 기본 확률. 하루 이틀 머무는 손님이 한두 번 청할 만큼이다.</summary>
        private const float ChancePerHour = 0.08f;

        /// <summary>기다려 주는 시간. 넘으면 혼자 둔다.</summary>
        private const int WaitTicks = GenDate.TicksPerHour * 2;

        private sealed class Seat
        {
            public MiniGameDef game;
            public Thing board;
            public Pawn guest;
            public int startedTick;
        }

        private static readonly List<Seat> waiting = new List<Seat>();

        private static readonly List<Thing> tmpBoards = new List<Thing>();
        private static readonly List<Pawn> tmpGuests = new List<Pawn>();

        public static bool Enabled
        {
            get
            {
                PRSettings settings = PRMod.Settings;
                return Together.Enabled && settings != null
                    && settings.playTogetherVisitors && settings.playTogetherInvites
                    && settings.playTogetherInviteScale > 0;
            }
        }

        // ---------- 바깥에서 묻는 것 ----------

        /// <summary>이 가구 앞에서 상대를 기다리는 손님. 없으면 null.</summary>
        public static Pawn WaitingAt(Thing board)
        {
            if (board == null) return null;

            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].board == board) return waiting[i].guest;

            return null;
        }

        /// <summary>이 사람이 어딘가에서 상대를 기다리고 있는가. 목록에서 맨 앞에 세우는 근거다.</summary>
        public static bool IsWaiting(Pawn pawn)
        {
            if (pawn == null) return false;

            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].guest == pawn) return true;

            return false;
        }

        /// <summary>
        /// 이 가구에 걸린 청을 지운다. 실제로 판이 잡혔거나(그러면 곧 새 Job 이 온다)
        /// 더는 뜻이 없을 때 부른다. 기다리던 Job 은 다음 검사에서 스스로 끝난다.
        /// </summary>
        public static void Cancel(Thing board)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
                if (waiting[i].board == board) waiting.RemoveAt(i);
        }

        /// <summary>새 판을 시작하거나 불러올 때. <see cref="TogetherMatch.Reset"/> 과 같은 이유다.</summary>
        public static void Reset()
        {
            waiting.Clear();
        }

        // ---------- 틱 ----------

        /// <summary>
        /// <see cref="GameComponent_Recreation"/> 이 1초마다 부른다. 두 가지를 한다 —
        /// 기다림이 끝난 청을 치우고, 한 시간에 한 번 새 청을 굴린다.
        /// </summary>
        public static void Tick()
        {
            Sweep();

            if (!Enabled) return;
            if (Find.TickManager.TicksGame % RollInterval != 0) return;

            float chance = ChancePerHour * (PRMod.Settings.playTogetherInviteScale / 100f);
            if (!Rand.Chance(chance)) return;

            TryInvite();
        }

        /// <summary>
        /// 끝난 청을 치운다. **기다리던 Job 이 아니라 여기가 시간을 잰다** — Job 쪽에 두면
        /// 손님이 도중에 다른 일을 받았을 때 아무도 그 청을 지우지 않는다.
        /// </summary>
        private static void Sweep()
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                Seat invite = waiting[i];

                bool lost = invite.guest == null || !invite.guest.Spawned
                    || invite.guest.Dead || invite.guest.Downed || invite.guest.InMentalState
                    || invite.board == null || !invite.board.Spawned
                    || invite.guest.Map != invite.board.Map;

                bool timedOut = Find.TickManager.TicksGame - invite.startedTick > WaitTicks;

                // 우리 Job 을 놓았으면 더는 기다리는 것이 아니다. 판이 잡혀 JoinGame 으로
                // 바뀐 경우도 여기로 온다 - 그때는 이미 Cancel 이 지운 뒤다.
                bool still = !lost && invite.guest.CurJobDef == PRDefOf.PR_InviteGame;

                if (!lost && !timedOut && still) continue;

                waiting.RemoveAt(i);

                // 아무도 안 왔으면 혼자 둔다. 자리를 뜨는 것이 아니라 그 자리에 남는다.
                if (timedOut && !lost && still) PlayAlone(invite.guest, invite.board, invite.game);
            }
        }

        // ---------- 개발자 도구가 부르는 것 (PRDebug) ----------

        /// <summary>
        /// 주사위를 건너뛰고 지금 당장 청하게 한다. QA 에서 한 시간을 기다리지 않으려고 둔다.
        /// 성공이면 null, 아니면 왜 안 됐는지 한 줄.
        /// </summary>
        public static string ForceInvite()
        {
            if (!Enabled) return "invites are off in mod settings";

            List<Map> maps = Find.Maps;
            if (maps == null || maps.Count == 0) return "no maps";

            for (int i = 0; i < maps.Count; i++)
                if (TryInviteOn(maps[i])) return null;

            return "no free board with a match, or no idle qualifying guest";
        }

        /// <summary>기다림을 지금 끝낸다 — 혼자 두기로 넘어가는 자리를 바로 본다.</summary>
        public static bool ExpireNow(Thing board)
        {
            for (int i = 0; i < waiting.Count; i++)
            {
                if (waiting[i].board != board) continue;

                waiting[i].startedTick = Find.TickManager.TicksGame - WaitTicks - 1;
                Sweep();
                return true;
            }

            return false;
        }

        /// <summary>지금 누가 어디서 기다리는가. 로그로 뽑아 본다.</summary>
        public static string Describe()
        {
            if (waiting.Count == 0) return "  waiting: (none)";

            string text = "";
            for (int i = 0; i < waiting.Count; i++)
            {
                Seat seat = waiting[i];
                int held = Find.TickManager.TicksGame - seat.startedTick;

                text += "  waiting: " + seat.guest.ToStringSafe()
                    + " at " + seat.board.ToStringSafe()
                    + " (" + seat.game.ToStringSafe() + ", " + held + "/" + WaitTicks + " ticks"
                    + ", job=" + (seat.guest.CurJobDef != null ? seat.guest.CurJobDef.defName : "none") + ")\n";
            }

            return text.TrimEnd('\n');
        }

        // ---------- 청하기 ----------

        private static void TryInvite()
        {
            List<Map> maps = Find.Maps;
            if (maps == null) return;

            for (int i = 0; i < maps.Count; i++)
                if (TryInviteOn(maps[i])) return;
        }

        private static bool TryInviteOn(Map map)
        {
            CollectBoards(map);
            if (tmpBoards.Count == 0) { tmpBoards.Clear(); return false; }

            Thing board = tmpBoards.RandomElement();
            tmpBoards.Clear();

            MiniGameDef game = GameOn(board);
            if (game == null) return false;

            CollectGuests(map, board, game);
            if (tmpGuests.Count == 0) { tmpGuests.Clear(); return false; }

            Pawn guest = tmpGuests.RandomElement();
            tmpGuests.Clear();

            return Invite(game, board, guest);
        }

        /// <summary>청할 만한 가구. 비어 있고, 승부가 있고, 두던 판이 남아 있지 않아야 한다.</summary>
        private static void CollectBoards(Map map)
        {
            tmpBoards.Clear();

            List<Thing> all = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            if (all == null) return;

            GameComponent_Recreation component = GameComponent_Recreation.Current;

            for (int i = 0; i < all.Count; i++)
            {
                Thing board = all[i];
                if (!board.Spawned) continue;

                MiniGameDef game = GameOn(board);
                if (game == null || !Together.AppliesTo(game)) continue;

                // 이미 누가 기다리고 있거나 판이 잡혀 있으면 그 가구는 지금 남의 것이다.
                if (WaitingAt(board) != null) continue;
                if (component != null && component.SessionFor(board) != null) continue;
                if (map.reservationManager.IsReservedByAnyoneOf(board, Faction.OfPlayer)) continue;

                tmpBoards.Add(board);
            }
        }

        /// <summary>
        /// 청할 만한 손님. 자격은 <see cref="Together.Judge"/> 가 이미 아는 것이고,
        /// 여기서 더 보는 것은 "지금 한가한가" 와 "떠나는 길이 아닌가" 둘뿐이다.
        /// </summary>
        private static void CollectGuests(Map map, Thing board, MiniGameDef game)
        {
            tmpGuests.Clear();

            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn pawn = all[i];

                // 식구는 여기 오지 않는다. 식구에게는 바닐라 여가가 이미 있고,
                // 비어 있던 것은 **손님이 먼저 청하는 그림** 하나뿐이었다.
                if (pawn.Faction == null || pawn.Faction.IsPlayer) continue;
                if (pawn.IsPrisoner || pawn.IsSlave) continue;
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) continue;

                // 저쪽 가구에서 이미 기다리는 사람. 가구 쪽 검사로는 안 걸린다.
                if (IsWaiting(pawn)) continue;

                if (!Together.Qualifies(pawn, null, board, game)) continue;
                if (!Idle(pawn)) continue;

                tmpGuests.Add(pawn);
            }
        }

        /// <summary>
        /// 지금 붙잡아도 되는가. 하던 일이 가볍게 끊기는 일이어야 하고,
        /// 떠나는 길이면 앉히지 않는다 — 앉자마자 일어서는 그림이 되기 때문이다.
        /// </summary>
        private static bool Idle(Pawn pawn)
        {
            if (pawn.jobs == null) return false;
            if (pawn.Drafted) return false;

            Job current = pawn.CurJob;
            if (current != null && !current.def.casualInterruptible) return false;

            PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
            if (duty != null && Leaving(duty.def)) return false;

            return true;
        }

        /// <summary>
        /// 떠나는 지시인가. DefOf 로 두지 않고 이름으로 한 번만 찾는다 —
        /// 지시 이름은 확장팩마다 늘어나므로 못 찾아도 조용히 지나가야 한다.
        /// </summary>
        private static bool Leaving(DutyDef duty)
        {
            if (!exitDutiesLookedUp)
            {
                exitDutiesLookedUp = true;
                AddExitDuty("ExitMapBest");
                AddExitDuty("ExitMapRandom");
                AddExitDuty("ExitMapBestAndDefendSelf");
                AddExitDuty("TravelOrLeave");
            }

            return exitDuties.Contains(duty);
        }

        private static void AddExitDuty(string name)
        {
            DutyDef def = DefDatabase<DutyDef>.GetNamedSilentFail(name);
            if (def != null) exitDuties.Add(def);
        }

        private static readonly HashSet<DutyDef> exitDuties = new HashSet<DutyDef>();
        private static bool exitDutiesLookedUp;

        private static bool Invite(MiniGameDef game, Thing board, Pawn guest)
        {
            Job job = JobMaker.MakeJob(PRDefOf.PR_InviteGame, board);
            if (!guest.jobs.TryTakeOrderedJob(job, JobTag.Misc)) return false;

            waiting.Add(new Seat
            {
                game = game,
                board = board,
                guest = guest,
                startedTick = Find.TickManager.TicksGame
            });

            Messages.Message("PR.Together.Invite".Translate(guest.LabelShortCap, board.LabelCap),
                board, MessageTypeDefOf.NeutralEvent, false);

            return true;
        }

        // ---------- 혼자 두기 ----------

        /// <summary>
        /// 아무도 안 왔다. 그 가구의 바닐라 여가 Job 을 그대로 준다 — 손님에게 여가 욕구가
        /// 없어도(바닐라 Joy 는 colonistsOnly 다) Job 자체는 돈다. 우리가 만들 것은 없다.
        /// </summary>
        private static void PlayAlone(Pawn guest, Thing board, MiniGameDef game)
        {
            if (game == null || game.vanillaJob == null || guest.jobs == null) return;

            IntVec3 spot = SitSpot(guest, board);
            if (!spot.IsValid) return;

            Job job = JobMaker.MakeJob(game.vanillaJob, board, spot);
            guest.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>
        /// 앉을 자리. 바닐라 <c>JoyGiver_InteractBuildingSitAdjacent</c> 와 같은 규칙이다 —
        /// 가구에 **상하좌우로** 붙은 칸이어야 하고, 의자가 있으면 그쪽이 먼저다.
        /// 서 있던 자리가 이미 조건에 맞는 경우가 대부분이라 그것부터 본다.
        /// </summary>
        private static IntVec3 SitSpot(Pawn guest, Thing board)
        {
            IntVec3 here = guest.Position;
            if (Sittable(guest, board, here, true)) return here;

            IntVec3 fallback = IntVec3.Invalid;

            foreach (IntVec3 cell in GenAdjFast.AdjacentCellsCardinal(board))
            {
                if (Sittable(guest, board, cell, true)) return cell;
                if (!fallback.IsValid && Sittable(guest, board, cell, false)) fallback = cell;
            }

            return fallback;
        }

        private static bool Sittable(Pawn guest, Thing board, IntVec3 cell, bool needChair)
        {
            if (!cell.IsValid || !cell.InBounds(board.Map)) return false;
            if (!cell.AdjacentToCardinal(board.Position)) return false;
            if (cell.IsForbidden(guest) || !guest.CanReserveSittableOrSpot(cell)) return false;

            if (!needChair) return true;

            Building edifice = cell.GetEdifice(board.Map);
            return edifice != null && edifice.def.building != null && edifice.def.building.isSittable;
        }

        private static MiniGameDef GameOn(Thing board)
        {
            CompMiniGame comp = board.TryGetComp<CompMiniGame>();
            return comp != null ? comp.Game : null;
        }
    }
}
