using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// 게임 하나. 프레임워크는 창을 띄우고 Rect 를 하나 건네줄 뿐, 그 안에서 무슨 일이 일어나는지 모른다.
    ///
    /// 필수는 다섯 가지다 — 시작하고, 매 프레임 갱신하고, 그리고, 끝났는지 답하고, 결과를 낸다.
    /// 나머지는 전부 선택이다. 무르기가 없는 게임은 <see cref="CanUndo"/> 를 그냥 두면 된다.
    /// </summary>
    public abstract class MiniGameWorker
    {
        public MiniGameDef def;

        /// <summary>어느 가구에서, 누가, 몇 단계로, 연습인지.</summary>
        public Thing Board { get; private set; }
        public Pawn SeatedPawn { get; private set; }

        /// <summary>맞은편에 앉은 사람. 2칸이 아니면 없다 — 그때 상대는 AI 다.</summary>
        public Pawn OpponentPawn { get; private set; }

        public int Tier { get; private set; }
        public bool Practice { get; private set; }

        /// <summary>
        /// 기보에 적히는 이름. 예전에는 "당신" 과 "상대" 두 낱말뿐이었는데,
        /// 2칸이 들어오면서 판 앞에 진짜 두 사람이 앉게 됐다. 앉은 사람이 있으면
        /// 이름으로 적는다. 없으면 예전 낱말로 돌아간다.
        /// </summary>
        public string YouLabel
        {
            get
            {
                return SeatedPawn != null
                    ? SeatedPawn.LabelShortCap
                    : "PR.Side.You".Translate().ToString();
            }
        }

        public string OpponentLabel
        {
            get
            {
                return OpponentPawn != null
                    ? OpponentPawn.LabelShortCap
                    : "PR.Side.Opponent".Translate().ToString();
            }
        }

        /// <summary>
        /// 지금 판을 남겨둬도 되는 지점인가. 창은 이 값이 바뀔 때마다 가구 위의 판을 갱신한다.
        /// 우르는 턴이 시작하는 순간, 던지는 게임은 이닝이 끝나는 순간이다.
        /// 되돌아가는 숫자를 주면 안 된다 — 늘어나기만 해야 한다.
        /// </summary>
        public abstract int SavePoint { get; }

        /// <summary>기록에 남는 진행량. 우르는 턴 수, 던지는 게임은 이닝 수.</summary>
        public abstract int Rounds { get; }

        /// <summary>
        /// 기권을 전적에 남길 만큼 판이 진행됐는가. 첫 수를 두기 전에 창을 닫은 것은
        /// 판을 접은 것이 아니라 앉지 않은 것이라, 패배로 적지 않는다.
        ///
        /// 대개는 라운드가 둘 이상이면 진행된 것이다. 한 라운드 안에서 이미 잃을 것을
        /// 잃는 게임(룰렛의 첫 스핀)은 이 값을 따로 답해야 공짜 재시도가 생기지 않는다.
        /// </summary>
        public virtual bool HasProgress
        {
            get { return Rounds > 1; }
        }

        public abstract bool IsOver { get; }
        public abstract bool PlayerWon { get; }

        /// <summary>창 하단 왼쪽에 그대로 나가는 한 줄.</summary>
        public abstract string StatusText { get; }

        public void Bind(Thing board, Pawn seatedPawn, Pawn opponentPawn, int tier, bool practice)
        {
            Board = board;
            SeatedPawn = seatedPawn;
            OpponentPawn = opponentPawn;
            Tier = def != null ? def.ClampTier(tier) : tier;
            Practice = practice;
        }

        // ---------- 수명 ----------

        public abstract void StartNew(int seed);

        /// <summary>가구에 남겨둔 판을 이어 둔다. 저장 시점의 상태가 그대로 복원되어야 한다.</summary>
        public abstract void Resume(MiniGameSaveData data);

        /// <summary>지금 상태를 저장 가능한 형태로 떠낸다. 저장을 지원하지 않으면 null.</summary>
        public virtual MiniGameSaveData MakeSaveData()
        {
            return null;
        }

        // ---------- 진행 ----------

        /// <summary>실시간 갱신. 게임 시간이 멈춰 있어도 돈다.</summary>
        public virtual void Tick(float now)
        {
        }

        /// <summary>판과 조작 전부. 이 Rect 안은 게임의 것이다.</summary>
        public abstract void DrawPlayArea(Rect area);

        /// <summary>창이 키 입력을 먼저 넘겨준다. 쓰지 않으면 비워둔다.</summary>
        public virtual void HandleShortcuts()
        {
        }

        // ---------- 큰 버튼 ----------

        /// <summary>판 아래 가운데 버튼의 문구. null 이면 버튼을 그리지 않는다.</summary>
        public virtual string ActionLabel
        {
            get { return null; }
        }

        public virtual bool ActionEnabled
        {
            get { return false; }
        }

        public virtual void DoAction()
        {
        }

        // ---------- 튜토리얼 ----------

        /// <summary>
        /// 튜토리얼 한 쪽의 그림. 문구는 프레임워크가 def.tutorialKeyPrefix 로 찾아 넣는다.
        /// 판이 시작되기 전에도 불릴 수 있으므로 진행 상태에 기대면 안 된다.
        /// </summary>
        public virtual void DrawTutorialFigure(Rect area, int page)
        {
        }

        // ---------- 설정 ----------

        /// <summary>이 게임만의 설정. 모드 설정 창의 게임별 구획에 그대로 들어간다.</summary>
        public virtual void DoSettings(Listing_Standard list)
        {
        }

        // ---------- 진행 기록 ----------

        public virtual IReadOnlyList<string> Log
        {
            get { return null; }
        }

        // ---------- 무르기 ----------

        public virtual bool CanUndo
        {
            get { return false; }
        }

        public virtual void Undo()
        {
        }

        // ---------- 결과 ----------

        /// <summary>게임별 집계 3종. def.tallyKeys 와 같은 순서로 채운다.</summary>
        public virtual void FillTallies(int[] tallies)
        {
        }

        /// <summary>한 번도 실점하지 않은 승리인가. 기록의 '완봉' 칸이 된다.</summary>
        public virtual bool Flawless
        {
            get { return false; }
        }

        /// <summary>
        /// 지금 지고 있나. **판세를 말할 수 없는 게임은 null 을 준다** — <c>HasProgress</c> 와 같은 패턴이다.
        ///
        /// 프레임워크는 이 값이 참일 때 무슨 일이 일어나는지만 알고, 그게 이 게임의 무엇인지는
        /// 모른다(제약 4). 지금은 강한 공격성을 가진 상대가 판을 엎을지 재는 데에만 쓰인다.
        /// </summary>
        public virtual bool? Losing
        {
            get { return null; }
        }

        public MatchResult BuildResult(bool won, bool resigned, int undos, float realSeconds)
        {
            MatchResult result = new MatchResult
            {
                Tier = Tier,
                Won = won,
                Resigned = resigned,
                Rounds = Rounds,
                Undos = undos,
                RealSeconds = realSeconds,
                Flawless = won && Flawless,
                Tallies = new int[GameRecord.TallyCount],
            };

            FillTallies(result.Tallies);
            return result;
        }
    }

    /// <summary>한 판이 끝났을 때 기록에 넘기는 요약.</summary>
    public struct MatchResult
    {
        public int Tier;
        public bool Won;
        public bool Resigned;
        public bool Flawless;
        public int Rounds;
        public int Undos;
        public float RealSeconds;
        public int[] Tallies;
    }
}
