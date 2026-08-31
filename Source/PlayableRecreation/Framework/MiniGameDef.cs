using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 오락 가구 하나를 "직접 할 수 있는 것"으로 만드는 정의.
    ///
    /// 프레임워크가 아는 것은 여기까지다 — 진입, 창 껍데기, 세션, 기록, 숙련도.
    /// 턴도 주사위도 상대도 프레임워크의 개념이 아니다. 그 안쪽은 전부 워커의 몫이다.
    /// </summary>
    public class MiniGameDef : Def
    {
        /// <summary>판을 실제로 굴리는 클래스. <see cref="MiniGameWorker"/> 파생.</summary>
        public Type workerClass;

        /// <summary>이 게임이 붙는 가구. 실제 부착은 XML 패치가 하고, 이 값은 조회용이다.</summary>
        public string targetThing;

        /// <summary>난이도 단계 수. 1이면 난이도 선택 창을 건너뛴다.</summary>
        public int difficultyCount = 5;

        /// <summary>난이도 설명 번역 키의 앞자리. "&lt;prefix&gt;.T0.Desc" 로 조립된다.</summary>
        public string difficultyKeyPrefix;

        /// <summary>기록 화면 아래쪽에 뜨는 게임별 집계 3종의 번역 키.</summary>
        public List<string> tallyKeys = new List<string>();

        /// <summary>튜토리얼 쪽수. 0이면 도움말 버튼이 뜨지 않는다.</summary>
        public int tutorialPages;

        /// <summary>튜토리얼 문구 번역 키의 앞자리. "&lt;prefix&gt;.P1.Title" 로 조립된다.</summary>
        public string tutorialKeyPrefix;

        /// <summary>무르기를 지원하는가. 던지는 게임처럼 되돌릴 수 없는 판은 false.</summary>
        public bool supportsUndo;

        /// <summary>중단한 판을 가구에 남겨둘 수 있는가.</summary>
        public bool supportsSave = true;

        /// <summary>진행 기록 패널을 오른쪽에 띄운다.</summary>
        public bool showLog = true;

        /// <summary>식민자가 이 가구로 여가를 즐길 때 쓰는 바닐라 Job. 숙련도 보너스를 여기에 얹는다.</summary>
        public JobDef vanillaJob;

        /// <summary>우르를 둔 식민자가 얻는 기분 생각. 단계는 플레이어 숙련도를 따라간다.</summary>
        public ThoughtDef playThought;

        /// <summary>플레이어가 이겼을 때 남기는 이야기.</summary>
        public TaleDef wonTale;

        /// <summary>'폰 지능 연동' 이 기준으로 삼는 스킬. 없으면 항상 중간 난이도.</summary>
        public SkillDef linkedSkill;

        /// <summary>창 크기.</summary>
        public Vector2 windowSize = new Vector2(980f, 720f);

        private MiniGameWorker cachedWorker;

        /// <summary>정의 검사·조회용 인스턴스. 판을 굴리는 데 쓰지 말 것.</summary>
        public MiniGameWorker WorkerSample
        {
            get { return cachedWorker ?? (cachedWorker = MakeWorker()); }
        }

        public MiniGameWorker MakeWorker()
        {
            MiniGameWorker worker = (MiniGameWorker)Activator.CreateInstance(workerClass);
            worker.def = this;
            return worker;
        }

        /// <summary>난이도 표시 이름. 단계 이름은 게임을 가리지 않으므로 프레임워크가 들고 있다.</summary>
        public string TierLabel(int tier)
        {
            return ("PR.Difficulty.T" + Mathf.Clamp(tier, 0, GameRecord.MaxTiers - 1) + ".Label")
                .Translate().ToString();
        }

        public string TierDesc(int tier)
        {
            if (difficultyKeyPrefix.NullOrEmpty()) return string.Empty;
            return (difficultyKeyPrefix + ".T" + Mathf.Clamp(tier, 0, difficultyCount - 1) + ".Desc")
                .Translate().ToString();
        }

        public int ClampTier(int tier)
        {
            return Mathf.Clamp(tier, 0, Mathf.Max(0, difficultyCount - 1));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;

            if (workerClass == null)
                yield return "workerClass is null";
            else if (!typeof(MiniGameWorker).IsAssignableFrom(workerClass))
                yield return workerClass.Name + " is not a MiniGameWorker";

            if (difficultyCount < 1 || difficultyCount > GameRecord.MaxTiers)
                yield return "difficultyCount must be 1.." + GameRecord.MaxTiers;

            if (tallyKeys != null && tallyKeys.Count > GameRecord.TallyCount)
                yield return "tallyKeys holds at most " + GameRecord.TallyCount + " entries";
        }
    }
}
