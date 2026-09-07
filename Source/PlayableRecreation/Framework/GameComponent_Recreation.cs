using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 세이브에 함께 직렬화되는 모드 상태 — 게임별 클리어 기록(숙련도), 가구에 남겨둔 판,
    /// 이 식민지의 전적. GameComponent 는 별도 Def 없이 자동 등록된다.
    /// </summary>
    public class GameComponent_Recreation : GameComponent
    {
        /// <summary>무효화 폴링 주기(틱). 1초에 한 번이면 충분하다.</summary>
        private const int CheckInterval = 60;

        private Dictionary<string, int> clearedMasks = new Dictionary<string, int>();
        private Dictionary<string, int> lastRetryTicks = new Dictionary<string, int>();
        private Dictionary<string, GameRecord> colonyRecords = new Dictionary<string, GameRecord>();
        private Dictionary<string, int> counters = new Dictionary<string, int>();
        private List<GameSession> sessions = new List<GameSession>();

        /// <summary>지금 창이 열려 있는 판. 두는 중에는 절대 무효화하지 않는다.</summary>
        public GameSession ActiveSession;

        public GameComponent_Recreation(Game game)
        {
        }

        public static GameComponent_Recreation Current
        {
            get
            {
                Game game = Verse.Current.Game;
                return game != null ? game.GetComponent<GameComponent_Recreation>() : null;
            }
        }

        public GameRecord ColonyRecord(MiniGameDef game)
        {
            if (game == null) return new GameRecord();

            GameRecord record;
            if (!colonyRecords.TryGetValue(game.defName, out record) || record == null)
            {
                record = new GameRecord();
                colonyRecords[game.defName] = record;
            }

            return record;
        }

        // ---------- 게임이 남기는 작은 숫자들 ----------

        /// <summary>
        /// 세이브에 함께 저장되는 이름 붙은 정수 - 슬롯의 칩 지갑 같은 것들.
        /// 프레임워크는 열쇠의 뜻을 모른다. 게임이 자기 접두사로 알아서 쓴다.
        /// </summary>
        public int GetCounter(string key, int fallback = 0)
        {
            int value;
            return counters.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetCounter(string key, int value)
        {
            counters[key] = value;
        }

        // ---------- 숙련도 ----------

        /// <summary>클리어한 난이도 수. 그대로 숙련도 단계이자 생각의 stage 인덱스가 된다.</summary>
        public int MasteryTier(MiniGameDef game)
        {
            if (game == null) return 0;

            int mask;
            if (!clearedMasks.TryGetValue(game.defName, out mask)) return 0;

            int count = 0;
            for (int i = 0; i < game.difficultyCount; i++) if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        public bool IsCleared(MiniGameDef game, int tier)
        {
            if (game == null) return false;

            int mask;
            return clearedMasks.TryGetValue(game.defName, out mask) && (mask & (1 << tier)) != 0;
        }

        /// <summary>처음 클리어했으면 true. 같은 난이도를 다시 이겨도 보상은 늘지 않는다.</summary>
        public bool RecordClear(MiniGameDef game, int tier)
        {
            if (game == null || tier < 0 || tier >= game.difficultyCount) return false;

            int mask;
            clearedMasks.TryGetValue(game.defName, out mask);
            if ((mask & (1 << tier)) != 0) return false;

            clearedMasks[game.defName] = mask | (1 << tier);
            return true;
        }

        // ---------- 재시도 쿨다운 ----------

        /// <summary>
        /// 재시도는 게임 내 하루 한 번, 게임 종류마다 따로 센다.
        /// 세션이 아니라 게임 단위로 세어 세션 저장을 꺼둔 사람에게만 무제한이 되는 구멍을 막는다.
        /// </summary>
        public bool RetryReady(MiniGameDef game)
        {
            if (Find.TickManager == null || game == null) return true;

            int last;
            if (!lastRetryTicks.TryGetValue(game.defName, out last)) return true;

            return Find.TickManager.TicksGame - last >= GenDate.TicksPerDay;
        }

        public void MarkRetry(MiniGameDef game)
        {
            if (Find.TickManager == null || game == null) return;
            lastRetryTicks[game.defName] = Find.TickManager.TicksGame;
        }

        // ---------- 가구에 남겨둔 판 ----------

        public GameSession SessionFor(Thing board)
        {
            if (board == null) return null;

            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i].board == board) return sessions[i];

            return null;
        }

        public void Register(GameSession session)
        {
            if (session == null || sessions.Contains(session)) return;

            // 한 가구에 판은 하나뿐이다.
            GameSession existing = SessionFor(session.board);
            if (existing != null) sessions.Remove(existing);

            sessions.Add(session);
        }

        public void Remove(GameSession session)
        {
            if (session == null) return;

            sessions.Remove(session);
            if (ActiveSession == session) ActiveSession = null;
        }

        public override void GameComponentTick()
        {
            if (sessions.Count == 0) return;
            if (Find.TickManager.TicksGame % CheckInterval != 0) return;

            for (int i = sessions.Count - 1; i >= 0; i--)
            {
                GameSession session = sessions[i];
                if (session == ActiveSession) continue;

                InvalidationReason? reason = Invalidation.Check(session);
                if (!reason.HasValue) continue;

                sessions.RemoveAt(i);
                ColonyRecord(session.game).RecordVoid();     // 세이브와 함께 저장된다

                // 통산 기록은 별도 파일이라 여기서 바로 써주지 않으면 종료 시 사라진다.
                RecordStore.For(session.game).RecordVoid();
                RecordStore.Save();

                Invalidation.Notify(session, reason.Value);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref clearedMasks, "clearedMasks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastRetryTicks, "lastRetryTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref colonyRecords, "colonyRecords", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref counters, "counters", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref sessions, "sessions", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (clearedMasks == null) clearedMasks = new Dictionary<string, int>();
                if (lastRetryTicks == null) lastRetryTicks = new Dictionary<string, int>();
                if (colonyRecords == null) colonyRecords = new Dictionary<string, GameRecord>();
                if (counters == null) counters = new Dictionary<string, int>();
                if (sessions == null) sessions = new List<GameSession>();

                // 참조가 끊긴(가구나 게임 정의가 사라진) 판은 조용히 정리한다.
                sessions.RemoveAll(delegate (GameSession s)
                {
                    return s == null || s.board == null || s.game == null;
                });
            }
        }
    }
}
