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
        /// <summary>판을 실제로 굴리는 클래스. <see cref="MiniGameWorker"/> 파생. 추첨함에는 없다.</summary>
        public Type workerClass;

        /// <summary>
        /// 자기 판이 없는 항목. 난이도만 고르게 하고, 그 난이도로 다른 게임 하나를 뽑아 준다 -
        /// 아케이드 기계처럼 "무엇이 걸릴지 모르는" 자리를 위한 것이다.
        ///
        /// 프레임워크는 무엇이 뽑히는지 모른다. 그때 로드되어 있는 승부 게임들이 곧 뽑기 통이다.
        /// </summary>
        public bool randomPick;

        /// <summary>이 게임이 붙는 가구. 실제 부착은 XML 패치가 하고, 이 값은 조회용이다.</summary>
        public string targetThing;

        /// <summary>
        /// 승부인가. false 면 이기고 지는 것이 없다 - 기록도, 무르기도, 기권도, 상대도 없다.
        /// 그냥 앉아서 하는 것이다. 프레임워크는 그런 항목도 받는다.
        /// </summary>
        public bool hasMatch = true;

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

        /// <summary>
        /// 배경 이야기가 **이 오락을 직접 말하는** 사람들. 그 사람에게만, 이 게임에서만
        /// 연동 스킬 레벨을 얹어 준다.
        ///
        /// 체스 마스터의 바닐라 보상은 지능 2 다. 연구원 배경이 지능 6 을 주므로,
        /// 스킬만 보면 **체스는 연구원이 더 잘 둔다.** 그 사람의 이름이 '체스 마스터'인데도.
        /// 여기 있는 목록이 그 한 칸을 메운다.
        ///
        /// 반대로 공학자·연구자처럼 오락을 말하지 않는 배경은 넣지 않는다 — 그런 사람은
        /// 지능이 알아서 올라가므로 이미 반영되어 있고, 목록에 넣기 시작하면 배경 이야기
        /// 전체를 우리가 다시 채점하게 된다.
        /// </summary>
        public List<BackstoryAffinity> backstoryAffinities;

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
            if (workerClass == null) return null;

            MiniGameWorker worker = (MiniGameWorker)Activator.CreateInstance(workerClass);
            worker.def = this;
            return worker;
        }

        /// <summary>
        /// 이 가구에 남은 판이 이 항목의 것인가. 추첨함은 자기가 뽑아 준 게임의 판도 자기 것으로 친다 -
        /// 그러지 않으면 아케이드에 두던 체스가 다음에 열 때 사라진다.
        /// </summary>
        public bool Accepts(MiniGameDef played)
        {
            return played != null && (played == this || randomPick);
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

            if (randomPick)
            {
                if (workerClass != null)
                    yield return "randomPick has no board of its own; drop workerClass";
                if (!hasMatch)
                    yield return "randomPick needs hasMatch - it only ever picks games that have one";
                if (difficultyCount < 2)
                    yield return "randomPick needs more than one difficulty; it is the only thing it asks";
            }
            else if (workerClass == null)
                yield return "workerClass is null";
            else if (!typeof(MiniGameWorker).IsAssignableFrom(workerClass))
                yield return workerClass.Name + " is not a MiniGameWorker";

            if (difficultyCount < 1 || difficultyCount > GameRecord.MaxTiers)
                yield return "difficultyCount must be 1.." + GameRecord.MaxTiers;

            if (!hasMatch && difficultyCount != 1)
                yield return "hasMatch=false requires difficultyCount 1";

            if (!hasMatch && supportsUndo)
                yield return "hasMatch=false cannot support undo";

            if (tallyKeys != null && tallyKeys.Count > GameRecord.TallyCount)
                yield return "tallyKeys holds at most " + GameRecord.TallyCount + " entries";

            if (backstoryAffinities != null && linkedSkill == null)
                yield return "backstoryAffinities needs linkedSkill - there is nothing to add levels to";

            if (backstoryAffinities != null)
                foreach (BackstoryAffinity affinity in backstoryAffinities)
                {
                    if (affinity == null || affinity.backstory == null)
                        yield return "backstoryAffinities holds an entry with no backstory";
                    else if (affinity.levels <= 0)
                        yield return affinity.backstory.defName + " affinity must add at least one level";
                }
        }
    }

    /// <summary>
    /// "이 배경은 이 오락을 직접 말한다" 한 줄. <see cref="MiniGameDef.backstoryAffinities"/> 를 본다.
    ///
    /// 배경은 Def 참조로 둔다 — 오타를 게임이 로딩할 때 바로 잡아 주기 때문이다.
    /// 그래서 여기 적을 수 있는 것은 반드시 로드되어 있는 배경뿐이다(코어 · 켜 둔 DLC).
    /// </summary>
    public class BackstoryAffinity
    {
        public BackstoryDef backstory;

        /// <summary>연동 스킬에 얹는 레벨. 0~20 자를 쓰므로 4 면 한 단계쯤 올라간다.</summary>
        public int levels = 4;
    }
}
