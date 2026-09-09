using System.Collections.Generic;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 모드 설정. 프레임워크가 아는 항목만 필드로 두고, 게임별 항목은 이름표를 붙여 자루에 담는다.
    ///
    /// 자루를 쓰는 이유는 순서 때문이다 — ModSettings 는 Def 가 로드되기 전에 읽히므로
    /// 게임별 설정을 타입으로 나눠 두면 불러올 시점에 그 타입이 아직 없다.
    /// </summary>
    public class PRSettings : ModSettings
    {
        // ---------- 플레이 ----------

        /// <summary>
        /// 창이 열려 있는 동안 게임 시간을 강제로 멈춘다.
        ///
        /// 기본값은 **끔**이다. 멈춰 두면 하는 도중에 습격이 올 수 없어
        /// "판을 내던지고 일어서는" 순간이 구조적으로 사라지기 때문.
        /// 대신 위협이 나타나면 창이 스스로 닫히므로 콜로니를 방치하게 되지는 않는다.
        /// </summary>
        public bool pauseWhilePlaying = false;

        /// <summary>켜면 선택한 폰이 가구까지 걸어간 뒤 창이 열린다.</summary>
        public bool immersionMode = false;

        // ---------- 몰입 모드 2칸: 둘이 함께 두기 ----------
        //
        // 전부 immersionMode 아래에 있다. 부모가 꺼져 있으면 설정 창에 그려지지도 않고
        // 코드에서도 Together.Enabled 하나로만 묻는다.

        /// <summary>상대를 골라 둘이 함께 둔다. immersionMode 가 켜져 있어야 한다.</summary>
        public bool playTogether = false;

        /// <summary>상대를 부를 수 있는 최대 거리(칸). 없으면 맵 반대편 사람을 기다리게 된다.</summary>
        public int playTogetherRange = 30;

        /// <summary>어린이도 상대가 된다. 아기·유아는 이 설정과 무관하게 안 된다.</summary>
        public bool playTogetherChildren = true;

        /// <summary>방문객도 상대가 된다. 끄면 우호도 항목도 뜻을 잃는다.</summary>
        public bool playTogetherVisitors = true;

        public bool sounds = true;

        /// <summary>켜면 난이도 선택 창 대신 우클릭한 폰의 스킬로 상대를 정한다.</summary>
        public bool linkToPawnSkill = false;

        /// <summary>AI "생각 중" 연출의 기준 시간(초). 난이도가 높을수록 짧아진다.</summary>
        public float botThinkSeconds = 0.75f;

        // ---------- 숙련도 ----------

        /// <summary>난이도를 깰수록 식민자의 그 여가·학습이 아주 조금씩 좋아진다.</summary>
        public bool masteryBonus = true;

        /// <summary>그 게임을 한 식민자에게 숙련도 단계별 기분 생각을 준다.</summary>
        public bool playThought = true;

        // ---------- 세션 ----------

        /// <summary>중단한 판을 가구에 저장해 이어 할 수 있게 한다.</summary>
        public bool saveSessions = true;

        /// <summary>전투·청소·수리 등이 일어나면 저장된 판이 흐트러진다.</summary>
        public bool invalidateSessions = true;

        public bool invalidateOnCombat = true;
        public bool invalidateOnCleaning = true;
        public bool invalidateOnRepair = true;
        public bool invalidateOnDamage = true;
        public bool invalidateOnMove = true;
        public bool invalidateOnExpiry = false;

        /// <summary>마지막 수로부터 이 일수가 지나면 판을 잊는다. invalidateOnExpiry 가 켜져 있을 때만.</summary>
        public int sessionExpiryDays = 3;

        /// <summary>최초 무효화 때 시스템을 설명하는 편지를 한 번 보냈는지.</summary>
        public bool invalidationLetterSent = false;

        // ---------- 무르기 ----------

        public int undoLimit = 3;

        /// <summary>완벽 이상 난이도에서는 무르기를 막는다.</summary>
        public bool noUndoOnHardDifficulty = true;

        // ---------- 게임별 ----------

        private List<string> disabledGames = new List<string>();
        private Dictionary<string, bool> flags = new Dictionary<string, bool>();
        private Dictionary<string, int> numbers = new Dictionary<string, int>();
        private Dictionary<string, float> reals = new Dictionary<string, float>();

        public bool IsEnabled(MiniGameDef game)
        {
            return game != null && !disabledGames.Contains(game.defName);
        }

        public void SetEnabled(MiniGameDef game, bool enabled)
        {
            if (game == null) return;

            if (enabled) disabledGames.Remove(game.defName);
            else if (!disabledGames.Contains(game.defName)) disabledGames.Add(game.defName);
        }

        public bool GetBool(string key, bool fallback)
        {
            bool value;
            return flags.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetBool(string key, bool value) { flags[key] = value; }

        public int GetInt(string key, int fallback)
        {
            int value;
            return numbers.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetInt(string key, int value) { numbers[key] = value; }

        public float GetFloat(string key, float fallback)
        {
            float value;
            return reals.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetFloat(string key, float value) { reals[key] = value; }

        // ---------- 콜로니 영향 ----------

        public void DisableColonyImpact()
        {
            masteryBonus = false;
            playThought = false;
        }

        public bool HasColonyImpact
        {
            get { return masteryBonus || playThought; }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref pauseWhilePlaying, "pauseWhilePlaying", false);
            Scribe_Values.Look(ref immersionMode, "immersionMode", false);

            Scribe_Values.Look(ref playTogether, "playTogether", false);
            Scribe_Values.Look(ref playTogetherRange, "playTogetherRange", 30);
            Scribe_Values.Look(ref playTogetherChildren, "playTogetherChildren", true);
            Scribe_Values.Look(ref playTogetherVisitors, "playTogetherVisitors", true);
            Scribe_Values.Look(ref sounds, "sounds", true);

            Scribe_Values.Look(ref linkToPawnSkill, "linkToPawnSkill", false);
            Scribe_Values.Look(ref botThinkSeconds, "botThinkSeconds", 0.75f);

            Scribe_Values.Look(ref masteryBonus, "masteryBonus", true);
            Scribe_Values.Look(ref playThought, "playThought", true);

            Scribe_Values.Look(ref saveSessions, "saveSessions", true);
            Scribe_Values.Look(ref invalidateSessions, "invalidateSessions", true);
            Scribe_Values.Look(ref invalidateOnCombat, "invalidateOnCombat", true);
            Scribe_Values.Look(ref invalidateOnCleaning, "invalidateOnCleaning", true);
            Scribe_Values.Look(ref invalidateOnRepair, "invalidateOnRepair", true);
            Scribe_Values.Look(ref invalidateOnDamage, "invalidateOnDamage", true);
            Scribe_Values.Look(ref invalidateOnMove, "invalidateOnMove", true);
            Scribe_Values.Look(ref invalidateOnExpiry, "invalidateOnExpiry", false);
            Scribe_Values.Look(ref sessionExpiryDays, "sessionExpiryDays", 3);
            Scribe_Values.Look(ref invalidationLetterSent, "invalidationLetterSent", false);

            Scribe_Values.Look(ref undoLimit, "undoLimit", 3);
            Scribe_Values.Look(ref noUndoOnHardDifficulty, "noUndoOnHardDifficulty", true);

            Scribe_Collections.Look(ref disabledGames, "disabledGames", LookMode.Value);
            Scribe_Collections.Look(ref flags, "gameFlags", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref numbers, "gameNumbers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref reals, "gameReals", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (disabledGames == null) disabledGames = new List<string>();
                if (flags == null) flags = new Dictionary<string, bool>();
                if (numbers == null) numbers = new Dictionary<string, int>();
                if (reals == null) reals = new Dictionary<string, float>();
            }
        }
    }
}
