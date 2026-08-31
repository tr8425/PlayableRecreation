# 직접 하는 오락 (Playable Recreation) — RimWorld 모드 기획서

> 오락 가구를 우클릭해 **플레이어 본인이** AI 상대와 그 게임을 하는 모드.
> 우르의 게임 · 편자 던지기 · 후프스톤.
> 작성일: 2026-08-30 (rev.3) / 대상: RimWorld 1.6

**rev.3 변경 요약**
- 인게임 계층을 **게임을 모르는 프레임워크**와 **게임별 워커**로 분리 (§12)
- 편자막대 · 후프스톤 추가 — 워커 하나에 Def 둘
- 모드 이름과 packageId 변경: `tr8425.playablerecreation` (Workshop 미배포 상태라 안전)
- 이 문서의 §1~§11 은 **우르 한 게임의 명세**로 읽는다. 프레임워크가 무엇을 가져갔는지는 §12

**rev.2 변경 요약**
- 진입 방식을 **RimChess 관습**으로 확정 (폰 선택 → 보드 우클릭 → 직접 플레이)
- `maxTechLevelToBuild = Neolithic` 은 **문제 아님**으로 판단 → 관련 해제 옵션 및 리스크 항목 삭제
- **폰의 작업(Job)과 분리** → 전용 JobDriver·joy 틱 지급 폐기. `forcePause` 기반 순수 미니게임으로 전환
- 상대는 **AI 봇 고정**, 난이도는 **플레이어가 선택** → 폰 대 폰 대국 및 지적 스킬 자동 연동 폐기(선택 옵션으로만 잔존)
- **멀티플레이어 범위 외** (서버 스택 필요) → 관련 설계 삭제

---

## 0. 사전 조사

### 0.1 동일 컨셉 모드 존재 여부 → **없음 (신규 영역)**

| 모드 | ID | 성격 | 우리와의 관계 |
|---|---|---|---|
| **RimChess - BETA** | `3495837714` | 폰 선택 → 체스 테이블 우클릭 → "Play chess..." 직접 플레이 | **채택할 진입 관습의 원본** |
| **[WG] RimPoker** | `3741656058` | 텍사스 홀덤/스터드/블랙잭 직접 플레이 | 미니게임 UI/룰 엔진 레퍼런스 |
| **Game of Ur on Tables** | `2546734176` | 우르 보드를 테이블 위에 배치만 허용 | 배치 전용. 플레이 불가. 병행 사용 가능(호환 목표) |
| **[WG] RimtoChess** | `3750476989` | 오토배틀러 미니게임 | 장르 다름 |
| **Pawns Play Poker** | `927867598` | 폰이 알아서 포커 침 (joy 가구) | 상호작용 없음 |

**결론**: 체스·포커는 이미 "직접 플레이" 모드가 있으나, **우르는 바닐라 가구가 존재함에도 플레이 가능한 모드가 전무**하다. 룰이 체스보다 단순하고 주사위 기반이라 AI 설계도 명확하다. **틈새로서 성립.**

### 0.2 바닐라 기반 사실 (실측 — RimWorld 1.6.4871 rev590, Core)

```xml
<!-- Data/Core/Defs/ThingDefs_Buildings/Buildings_Joy.xml -->
<ThingDef ParentName="FurnitureWithQualityBase">
  <defName>GameOfUrBoard</defName>
  <label>Game-of-Ur board</label>
  <rotatable>true</rotatable>
  <maxTechLevelToBuild>Neolithic</maxTechLevelToBuild>
  <building><joyKind>Gaming_Cerebral</joyKind><paintable>true</paintable></building>
  <statBases><MaxHitPoints>90</MaxHitPoints><WorkToBuild>6000</WorkToBuild>
             <Beauty>2</Beauty><JoyGainFactor>0.8</JoyGainFactor></statBases>
  <stuffCategories>Metallic / Woody / Stony</stuffCategories>
  <costStuffCount>35</costStuffCount>
  <passability>PassThroughOnly</passability>
  <designationCategory>Joy</designationCategory>
</ThingDef>
```

```xml
<!-- Data/Core/Defs/JobDefs/Jobs_Joy.xml — 참고용. 본 모드는 이 Job을 사용하지 않는다. -->
<JobDef>
  <defName>Play_GameOfUr</defName>
  <driverClass>JobDriver_SitFacingBuilding</driverClass>
  <joyDuration>4000</joyDuration>
  <joyMaxParticipants>2</joyMaxParticipants>
  <joySkill>Intellectual</joySkill>
  <joyXpPerTick>0.0015</joyXpPerTick>
  <joyKind>Gaming_Cerebral</joyKind>
</JobDef>
```

**설계 반영 사항**

| 관찰 | 판단 |
|---|---|
| `maxTechLevelToBuild = Neolithic` (부족민만 건설) | **문제 아님.** 교역상·폐허·퀘스트 보상으로 흔히 획득해 설치하는 물건이다. Def를 건드리지 않는다 |
| `joyDuration 4000`(약 66초), `joyXpPerTick` | **미사용.** 본 모드는 폰의 Job이 아니라 플레이어의 미니게임이다. 바닐라 Job/JoyGiver를 **전혀 건드리지 않고 그대로 둔다** (폰은 여전히 자율적으로 우르를 둔다) |
| `joyMaxParticipants 2` (2인 대국 전제) | **미사용.** 상대는 항상 AI 봇 |
| `GameOfUrBoard` 는 ThingDef 하나로 단일 | `ThingComp` 주입 지점이 명확 — Harmony 불필요 |

> **핵심 원칙**: 바닐라 우르 Job/JoyGiver는 **손대지 않는다.** 폰의 자율 여가는 그대로 굴러가고,
> 그 위에 "플레이어가 직접 두는 창"을 얹기만 한다. 충돌 면적 최소화.

### 0.3 개발 환경 (실측)

| 항목 | 값 |
|---|---|
| RimWorld | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld` · **1.6.4871 rev590** |
| DLC | Core / Royalty / Ideology / Biotech / Anomaly / **Odyssey** (전부 보유) |
| 참조 DLL | `RimWorldWin64_Data/Managed/Assembly-CSharp.dll` 외 |
| Harmony | 워크샵 `2009463077` **설치됨** (`1.4` / `1.5` / `Current` 폴더 구조) |
| 부분 소스 | `RimWorld/Source/{RimWorld,Verse}` — `JobDriver_Wait`, `ToilFailConditions` 등 샘플 포함 |
| 워크샵 모드 | 1119개 설치됨 → **호환성 테스트는 별도 클린 프로필 권장** |

---

## 1. 모드 개요

### 1.1 한 줄 정의

> 식민지의 오락 가구를 우클릭하면, **식민자가 하던 그 게임을 당신이 직접 한다.**
>
> 우르의 게임(기원전 2500년의 보드게임) · 편자 던지기 · 후프스톤.

### 1.2 핵심 경험 (Core Loop)

```
식민자 선택 → 우르 보드 우클릭 → "우르의 게임 두기"
        ↓
   난이도 선택 (초보 / 견습 / 숙련 / 상급 / 명인)   ※ 이전 선택 기억
        ↓
   게임 창 오픈 · 게임 시간 강제 일시정지
        ↓
   주사위 굴림 → 말 이동 → 로제트/잡기 → AI 봇 턴
        ↓
   승리/패배 → 전적 기록 (+ 선택 시 소량 joy/XP)
```

**한 판 = 실시간 3~8분.** 그동안 콜로니는 완전히 멈춰 있으므로 방치 위험이 없다.

### 1.3 설계 원칙

| # | 원칙 | 의미 |
|---|---|---|
| P1 | **바닐라 불간섭** | 기존 Def를 **수정하지 않는다.** `ThingComp` 주입 + 신규 Def만. 모드 제거 시 세이브 정상 복구 |
| P2 | **엔진과 게임 분리** | 룰/AI는 `Verse` 의존 0인 순수 C#. 단위 테스트 가능, RimWorld 버전 변화에 강함 |
| P3 | **플레이어의 게임** | 폰의 Job이 아니다. 두는 사람은 당신이고 난이도도 당신이 고른다. **시간은 멈추지 않는다**(rev.3) — 멈추면 두는 도중 습격이 올 수 없어 '판을 내던지고 일어서는' 순간이 구조적으로 사라진다. 대신 위협이 나타나면 창을 강제로 닫아 방치를 막는다 |
| P4 | **세이브스컴 방지** | 주사위는 결정론적 시드 스트림. 무르기해도 같은 눈이 나온다 |
| P5 | **판은 물리적 물건** | 판을 저장해두고 나갔다면, 전투·청소·수리가 판을 흐트러뜨린다 (§7) |
| P6 | **콜로니 영향 최소** | 기본값은 보상 없음에 가깝다. 미니게임이 본편 밸런스를 흔들지 않는다 |

---

## 2. 기술 스택

| 항목 | 선택 |
|---|---|
| 타겟 | RimWorld **1.6** (1.5 하위호환은 `LoadFolders.xml`로 후순위 검토) |
| 언어 | C# / **.NET Framework 4.7.2** |
| 패치 | **Harmony 패치 0개.** 진입점은 `ThingComp` + XML `PatchOperationAdd` 로만 구성 → **Harmony 의존성 자체를 걸지 않는다.** M6에서 정밀 감지가 필요해지면 그때 추가 |
| UI | RimWorld `Window` / `Widgets` / `Verse.Text` 기본 API. 외부 UI 라이브러리 없음 |
| 저장 | `IExposable` + `GameComponent`(세션) / `ModSettings` + config 파일(개인 전적) |
| 네트워크 | **없음.** 전부 로컬. 멀티플레이/온라인 랭킹은 **범위 외** |
| packageId | `tr8425.royalgameofur` (예정) |

---

## 3. 명세 1 — 상호작용 (플레이 진입)

### 3.1 진입 방식: RimChess 관습 채택

```
[식민자 1명 선택] → [우르 보드에 우클릭] → 플로트 메뉴 "우르의 게임 두기"
```

RimChess의 `Play chess...` 와 동일한 조작 관습. 이미 학습된 UX라 별도 안내가 필요 없다.

**보조 진입로 2종**

| # | 경로 | 조작 | 용도 |
|---|---|---|---|
| **E1** | **주 진입로** | 식민자 선택 → 보드 우클릭 → `우르의 게임 두기` | 기본 |
| **E2** | 보드 Gizmo | 보드만 클릭 → 툴바 버튼 `우르의 게임 두기` | 폰 선택이 귀찮을 때 |
| **E3** | 이어하기 | 저장된 세션이 있는 보드 → Gizmo `중단된 판 이어 두기 (12수)` | §7 세션 시스템 |

### 3.2 구현: Harmony 없이 `ThingComp`

`GameOfUrBoard` 에 XML 패치로 컴포넌트를 주입하고, 컴포넌트가 메뉴/기즈모를 제공한다.

```xml
<!-- Patches/Patch_GameOfUrBoard.xml -->
<Operation Class="PatchOperationAdd">
  <xpath>/Defs/ThingDef[defName="GameOfUrBoard"]</xpath>
  <value>
    <comps>
      <li Class="RoyalGameOfUr.CompProperties_UrBoard" />
    </comps>
  </value>
</Operation>
```

```csharp
public class CompUrBoard : ThingComp {
    // E1 : 폰 선택 + 보드 우클릭
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn) { ... }
    // E2/E3 : 보드 선택 시 기즈모
    public override IEnumerable<Gizmo> CompGetGizmosExtra() { ... }
    // 중단된 판 표시 오버레이
    public override void PostDraw() { ... }
}
```

> `FloatMenuMakerMap` 를 Harmony로 패치하지 않는다 → 설치된 1119개 모드와의 메뉴 패치 충돌 위험 제거.

### 3.3 우클릭 옵션 가시성 규칙

| 상황 | 표시 |
|---|---|
| 정상 | `우르의 게임 두기` |
| 보드가 미완성 프레임/청사진 | 비표시 |
| 보드가 파괴 임박(HP 10% 미만) | `우르의 게임 두기 (판이 부서져 있음)` — 비활성 |
| 이 보드에 다른 세션이 저장돼 있음 | `중단된 판 이어 두기 (12수)` + `새 판 시작 (기존 판 폐기)` 두 줄 |
| 폰 미선택 상태(E2) | 기즈모로만 제공 |

- 폰의 도달 가능성·조작 능력·예약 상태는 **검사하지 않는다.** 폰이 걸어가는 Job이 아니기 때문.
- 선택된 폰은 "누가 뒀는지" 기록용으로만 쓰인다(§8).

### 3.4 시퀀스

```
CompUrBoard.CompFloatMenuOptions
  └→ [세션 확보]
       ├ 기존 세션 유효 → 복원
       └ 없음 → Dialog_UrDifficulty (난이도 선택, 이전 값 기본 선택)
                  └→ UrSession 신규 생성 (시드 = 현재 tick 해시)
  └→ Find.WindowStack.Add(new Dialog_UrGame(session, board))
       ├ Window.forcePause = true        ← 창이 열려 있는 동안 게임 시간 정지
       └ absorbInputAroundWindow = false  ← 뒤쪽 콜로니 화면은 계속 보임
```

**Job 없음. 폰 이동 없음. 틱 처리 없음.** 창이 전부다.

> *선택 옵션* `몰입 모드`(기본 OFF): 켜면 선택한 폰이 보드 앞까지 걸어가 앉은 뒤 창이 열린다.
> 이때만 최소한의 `RGU_SitAtUrBoard` Job(바닐라 `JobDriver_SitFacingBuilding` 재사용)을 발급한다.
> 끄면(기본) 창이 즉시 열린다.

### 3.5 창을 닫으면?

- **판은 사라지지 않는다.** 세션이 보드에 저장되고, 보드 위에 작은 오버레이 아이콘으로 "중단된 판 있음" 표시.
- 창이 닫히면 일시정지가 풀리고 콜로니 시간이 다시 흐른다.
- 나중에 E3로 이어 둔다 — **단, 그 사이 콜로니에서 일이 벌어지면 판이 망가진다(§7).**

---

## 4. 명세 2-A — 게임 룰

채택 룰셋: **Finkel 룰 (핀켈 복원안)** — 가장 널리 통용되며 학습 자료가 풍부.

### 4.1 보드

20칸. 좌측 3×4 블록(12) + 중앙 다리 1×2(2) + 우측 3×2 블록(6).

```
        c0    c1    c2    c3    c4    c5    c6    c7
 r0   [ ✦ ][    ][    ][    ]              [ ✦ ][    ]   ← AI 봇 진영
 r1   [    ][    ][    ][ ✦ ][    ][    ][    ][    ]   ← 중앙 = 공유 전장
 r2   [ ✦ ][    ][    ][    ]              [ ✦ ][    ]   ← 플레이어 진영

 ✦ = 로제트(rosette) 5개 : (r0,c0) (r2,c0) (r1,c3) (r0,c6) (r2,c6)
```

### 4.2 경로 (플레이어 = 하단 r2 기준 — AI 봇은 r0로 미러)

| idx | 칸 | 특성 |
|---|---|---|
| 1 | r2c3 | 자기 진영 (안전) |
| 2 | r2c2 | 자기 진영 |
| 3 | r2c1 | 자기 진영 |
| **4** | **r2c0** | **✦ 로제트 → 추가 턴** |
| 5 | r1c0 | 공유 전장 (잡기 가능) |
| 6 | r1c1 | 공유 전장 |
| 7 | r1c2 | 공유 전장 |
| **8** | **r1c3** | **✦ 로제트 → 추가 턴 + 잡기 면역(안전칸)** |
| 9 | r1c4 | 공유 전장 |
| 10 | r1c5 | 공유 전장 |
| 11 | r1c6 | 공유 전장 |
| 12 | r1c7 | 공유 전장 |
| 13 | r2c7 | 자기 진영 (안전) |
| **14** | **r2c6** | **✦ 로제트 → 추가 턴** |
| 15 | OFF | 골인 |

- **자기 진영(idx 1~4, 13~14)**: 상대 말이 들어올 수 없음 → 절대 안전
- **공유 전장(idx 5~12)**: 잡기 발생 구간
- **중앙 로제트(idx 8)**: 공유 구간이지만 **잡기 면역**

### 4.3 말·주사위

- 말: 각 진영 **7개**. 시작 시 전부 보드 밖(대기열).
- 주사위: **4면체 주사위 4개**, 각 0 또는 1 → 합 0~4.

| 눈 | 0 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|---|
| 확률 | 1/16 (6.25%) | 4/16 (25%) | 6/16 (37.5%) | 4/16 (25%) | 1/16 (6.25%) |

### 4.4 룰 목록 (구현 체크리스트)

| # | 룰 | 세부 |
|---|---|---|
| R1 | **눈이 0** | 턴 상실, 즉시 상대 턴 |
| R2 | **투입** | 대기 말을 경로 idx = 눈(1~4) 위치로 진입 |
| R3 | **이동** | 보드 위 말을 idx → idx + 눈 |
| R4 | **자기 말 중첩 금지** | 도착칸에 자기 말 있으면 그 수는 불법 |
| R5 | **잡기** | 도착칸(idx 5~12)에 상대 말 → 상대 말을 대기열로 되돌림 |
| R6 | **안전칸** | idx 8(중앙 로제트) 위의 말은 잡히지 않음 → 그 칸으로의 이동 자체가 불법 |
| R7 | **추가 턴** | 로제트(idx **4 / 8 / 14**) 착지 시 한 번 더 굴림 |
| R8 | **골인은 정확히** | idx + 눈 == **15** 여야 골인. 15 초과는 불법 |
| R9 | **합법수 없음** | 모든 수가 불법이면 자동 패스 (알림 표시) |
| R10 | **승리** | 7개 전부 골인시킨 쪽 승리 |
| R11 | **선공** | 플레이어 선공 (설정: 랜덤 선공) |

### 4.5 룰 엔진 인터페이스 (순수 C#, Verse 의존 0)

```csharp
namespace RoyalGameOfUr.Core;

public enum Side { Player, Bot }

public readonly struct UrMove {          // FromIndex 0 = 대기열에서 투입
    public readonly int  FromIndex;
    public readonly int  ToIndex;        // 15 = 골인
    public readonly bool IsCapture;
    public readonly bool GrantsExtraTurn;
}

public sealed class UrGameState {        // 경량 · 복사 가능(값 시맨틱)
    public byte[] BoardP, BoardB;        // 길이 15, 각 idx 점유 여부
    public int    WaitingP, WaitingB;    // 대기 말 수
    public int    ScoredP, ScoredB;      // 골인 말 수
    public Side   Turn;
    public int    LastRoll;

    public UrGameState Clone();
}

public static class UrRules {
    public static List<UrMove> LegalMoves(in UrGameState s, int roll);
    public static UrGameState  Apply(in UrGameState s, in UrMove m, out bool extraTurn);
    public static Side?        Winner(in UrGameState s);
}

public sealed class UrDiceStream {       // ★ 결정론적: 시드 + 순번으로 완전 재현
    public UrDiceStream(int seed);
    public int Roll(int sequenceIndex);  // 순수 함수. 되감아도 같은 눈
}
```

> **P4(세이브스컴 방지)의 구현 핵심**: 주사위는 `Rand` 가 아니라 `(seed, sequenceIndex)` 해시로 결정된다.
> 무르기(§7.3)로 수를 되돌리면 `sequenceIndex` 도 되돌아가 **같은 눈이 다시 나온다.** 운은 재추첨되지 않는다.

---

## 5. 명세 2-B — AI 봇

상대는 **항상 AI 봇**. 난이도는 **플레이어가 고른다.**

### 5.1 난이도 5단계

| 난이도 | 내부 Tier | 알고리즘 | 탐색 깊이 | 실수율 | 체감 |
|---|---|---|---|---|---|
| **초보** | T0 | Random + 골인만 우선 | – | – | 룰 배우는 중 |
| **견습** | T1 | Greedy (1수 평가) | 1 | 15% | 잡기·로제트는 챙김 |
| **숙련** | T2 | Expectiminimax | 2 | 8% | 함정을 놓기 시작 |
| **상급** | T3 | Expectiminimax | 3 | 3% | 확률 계산이 붙음 |
| **명인** | T4 | Expectiminimax + 위협 인지 | 4~5 | 0% | 실수 없음 |

- **실수율** = 그 확률로 최선수 대신 2순위 수를 둔다. 하위 난이도가 기계적으로 느껴지지 않게 하는 장치.
- 난이도는 **판 시작 시 선택**하고, 마지막 선택값을 모드 설정에 기억한다.
- 판 도중 난이도 변경 불가 (전적 무결성).
- *선택 옵션* `폰 지능 연동`(기본 OFF): 켜면 우클릭한 폰의 지적 스킬로 난이도를 자동 결정한다.
  매핑: `0–3→초보 / 4–7→견습 / 8–11→숙련 / 12–15→상급 / 16–20→명인` (열정 `Minor` +1, `Major` +2 가산)

**실측 (2026-08-31, M3 완료 시점)**

| 대전 | 승률 | 비고 |
|---|---|---|
| 명인 vs 초보 | **96.0%** | 100판, 선공 교대 |
| 명인 vs 견습 | **92.7%** | 150판, 선공 교대 |
| 숙련 vs 초보 | **94.5%** | 200판, 선공 교대 |

명인(depth 4) 1수 판단 **평균 1.62 ms** — 50ms 예산의 3% 수준. 가지치기 없이도 여유가 커서
Star1 최적화와 depth 5 반복 심화는 **당장 불필요**하다. 상태가 구조체 + 비트마스크라 탐색 중 힙 할당이 0인 덕.

> 착수 전에는 "우르는 운 게임이라 실력차가 승률로 잘 안 나타날 것"으로 봤으나, 실제로는
> 평가함수의 **잡힐 확률 항(Exposure)** 이 결정적이었다. 안전을 계산하지 않는 견습은
> 공유 구간에 말을 계속 노출시켜 92.7%까지 벌어진다.

### 5.2 평가 함수 (T1 이상 공용)

```
Eval(s, me) = Material(me) − Material(opp) + Exposure(me) − Exposure(opp)

Material(side):
  f(대기 말)      = 0
  f(idx i 위 말)  = 4 + i·2                  // 전진 가치
  f(골인 말)      = 100
  + 30   중앙 로제트(idx 8) 점유             // 안전 + 요충지 봉쇄
  + 12   로제트(idx 4 / 14) 점유
  +  8   상대가 공유 구간에 말이 없을 때의 진입 자유도

Exposure(side) = −Σ_(side의 공유구간 말 p) f(p) × P(상대가 다음 턴에 p를 잡음)
  ※ 안전칸(idx 8) 위의 말은 제외 — 애초에 잡히지 않는다(R6)
  ※ P는 주사위 분포(1/4/6/4/1 ÷16)로 정확히 계산 — 근사 없음
  ※ 투입은 경로 1~4 까지만 닿으므로 공유 구간(5~12) 위협에서 제외된다
  ※ 양쪽 노출을 모두 계산해 **대칭**으로 만든다. 한쪽만 넣으면 탐색이 상대 턴을
    평가할 때 관점이 뒤틀려 상대의 위험을 과소평가한다
```

### 5.3 연출

- AI 턴은 즉시 처리하지 않고 **0.4~0.9초 "생각 중…"** 연출 후 표시. (난이도가 높을수록 짧게 = 빠른 두뇌)
- 주사위 굴림 애니메이션 0.6초 + 사운드.
- 잡기 발생 시 화면 흔들림(약) + 전용 사운드.

---

## 6. 명세 3 — 튜토리얼

### 6.1 최초 진입 온보딩 (6단계 오버레이)

창을 처음 열면 자동 실행. 각 단계는 보드의 해당 영역을 하이라이트 + 화살표.

| # | 제목 | 내용 |
|---|---|---|
| 1 | 목표 | "말 7개를 모두 반대편으로 통과시키면 승리합니다." |
| 2 | 내 길 | 내 경로 14칸을 순차 점등 애니메이션으로 보여줌 |
| 3 | 주사위 | 4개 주사위 = 0~4. **0이면 턴을 잃습니다.** 확률표 표시 |
| 4 | 로제트 ✦ | "꽃무늬 칸에 도착하면 **한 번 더** 굴립니다." |
| 5 | 잡기 & 안전칸 | 중앙 8칸에서 상대 말을 밟으면 처음으로 되돌립니다. **단 중앙 ✦ 위의 말은 못 잡습니다.** |
| 6 | 정확히 나가기 | "골인은 딱 맞는 눈이 필요합니다. 넘치면 못 나갑니다." |

- 각 단계 `건너뛰기` / `다시 보기` 제공. 완료 여부는 **모드 설정(전역)** 에 저장 → 세이브를 새로 파도 다시 안 뜸.
- 창 우상단 `?` 버튼으로 언제든 재실행.

### 6.2 연습 모드 (Practice)

- 진입: 메인 창의 `연습` 버튼 또는 난이도 선택 창의 `연습으로 시작`
- 특징: **초보 AI / 무제한 무르기 / 전적 미기록 / 보상 없음 / 무효화 없음**
- 튜토리얼 직후 자동 권유: "연습으로 한 판 해보시겠습니까?"

### 6.3 상시 학습 보조 (설정으로 개별 토글)

| 보조 | 기본값 | 설명 |
|---|---|---|
| 합법수 하이라이트 | ON | 굴림 후 이동 가능한 말/도착칸을 초록 테두리로 |
| 잡기 예고 | ON | 잡기 가능한 수는 빨간 아이콘 |
| 위험 경고 | OFF | 이 수를 두면 다음 턴에 잡힐 확률(%) 표시 — 상급자용 |
| 칸 툴팁 | ON | 칸 호버 시 "idx 8 · 중앙 로제트 · 안전 · 추가 턴" |
| 수순 로그 | ON | 우측 패널에 기보 텍스트 누적 |

---

## 7. 명세 4 — 세션 저장 / 리트라이 / 무효화 *(후순위)*

> 요구사항: 진행 중 상태 저장 + 리트라이 + **전투·청소·수리 등이 있었다면 저장 상태를 망가뜨리고 간단한 알림으로 초기화를 설명**.

### 7.1 컨셉: "판은 진짜 물건이다"

세션은 세이브 슬롯이 아니라 **보드 위에 실제로 놓인 말의 배치**다.
누가 방을 쓸고 지나가면 말이 쓸려나가고, 총알이 날아오면 판이 뒤집힌다.

**중요**: 창이 열려 있는 동안은 게임 시간이 멈춰 있으므로 무효화가 절대 발생하지 않는다.
무효화는 **오직 "판을 저장해두고 창을 닫은 뒤 콜로니를 굴린 구간"에서만** 일어난다.
→ 플레이 중 갑자기 판이 사라지는 억울함이 구조적으로 불가능하다.

### 7.2 세션 데이터 모델

```csharp
public sealed class UrSession : IExposable {
    public int          boardThingId;      // 소속 보드
    public int          seed;              // 주사위 시드 (결정론)
    public int          diceSequenceIndex; // 현재 굴림 순번
    public UrGameState  state;
    public List<UrMove> history;           // 기보 = 무르기/리플레이 원본
    public int          difficultyTier;    // 판 시작 시 고정
    public int          seatedPawnId;      // 우클릭한 폰 (기록용, -1 가능)
    public int          startTick, lastPlayedTick;
    public int          undosUsed, retriesUsed;

    // 무효화 감시 스냅샷
    public int          boardHitPointsSnapshot;
    public int          roomFilthCountSnapshot;
    public IntVec3      boardPositionSnapshot;
}
```

- 보관 주체: `GameComponent_Ur` (세이브 파일에 함께 직렬화). 동시 세션은 보통 0~2개 → 성능 부담 없음.
- 저장 시점: **매 수마다 자동**. 별도 "저장" 버튼 없음(수동 저장은 곧 세이브스컴 유도).

### 7.3 리트라이 3종

| 기능 | 동작 | 제한 | 주사위 |
|---|---|---|---|
| **무르기 (Undo)** | 직전 1수 취소. `history` pop + `diceSequenceIndex` 되감기 | 판당 **3회**(설정 0~99) · 상급 이상 난이도엔 금지(설정) | **동일한 눈 재현** (P4) |
| **재시도 (Retry)** | 현재 판을 처음부터. **새 시드** 발급 | 게임 내 하루 **1회** | 완전히 새 주사위 |
| **기권 (Resign)** | 판 종료, 패배 기록 | 무제한 | – |

- 재시도로 폐기된 판은 리더보드에 **기권패**로 기록(설정으로 '미기록' 전환 가능).
- 연습 모드에서는 셋 다 무제한 + 미기록.

### 7.4 ★ 무효화(Invalidation) 트리거

`GameComponent_Ur.GameComponentTick()` 에서 **60틱(1초)마다** 저장된(비활성) 세션만 검사.
Harmony 패치 없이 폴링으로 감지 → 버전/모드 호환성 최상.

| 코드 | 트리거 | 감지 방법 | 알림 문구 |
|---|---|---|---|
| `Combat` | 보드가 있는 맵에 적대 세력 교전 발생 / 보드 반경 12칸 내 발포·폭발 | `map.attackTargetsCache` 적대 타겟 존재 + 반경 체크 | *"전투 중에 우르 판이 뒤집혔습니다. 저장된 판이 초기화되었습니다."* |
| `Cleaning` | 보드가 있는 **방(Room)의 오물 개수 감소** = 누가 청소함 | `room.ContainedAndAdjacentThings` 오물 카운트 스냅샷 비교 | *"청소하다 우르 말들이 쓸려나갔습니다. 저장된 판이 초기화되었습니다."* |
| `Repair` | 보드 **HP 증가**(수리 완료) 또는 같은 방 건물 수리/건설 프레임 완료 | `board.HitPoints > snapshot` | *"수리 작업 중 우르 판이 흐트러졌습니다. 저장된 판이 초기화되었습니다."* |
| `Damaged` | 보드 **HP 감소**(피격·화재) | `board.HitPoints < snapshot` | *"우르 판이 손상되어 말 배치를 알아볼 수 없게 되었습니다."* |
| `Moved` | 보드 위치 변경 / 재설치 | `board.Position != snapshot` | *"우르 판을 옮기면서 말들이 쏟아졌습니다."* |
| `Destroyed` | 보드 파괴·해체 | `board.Destroyed` 또는 `!board.Spawned` | *"우르 판이 사라져 저장된 판이 없어졌습니다."* |
| `Expired` | 마지막 수로부터 **3일** 경과 | `lastPlayedTick` 비교 | *"오래 방치된 우르 판을 아무도 기억하지 못합니다."* |

> rev.1의 `PawnLost` 트리거는 **삭제**. 폰이 판을 붙들고 있지 않으므로(Job 없음) 폰의 생사·징집과 판은 무관하다.

**알림 규격**

- `Messages.Message(text, board, MessageTypeDefOf.NeutralEvent)` — 화면 상단 토스트. **Letter(편지) 아님** (스팸 방지).
- 클릭하면 해당 보드로 카메라 이동.
- 동일 세션에 대해 **1회만** 발생 (무효화 즉시 세션 파기).
- **최초 1회에 한해** 편지(Letter)로 이 시스템 자체를 설명한다 → "왜 내 판이 사라졌지?" 불만 예방.

**무효화 시 처리**

1. 세션 파기, 보드 오버레이 아이콘 제거
2. 해당 판은 **무효 경기** → 리더보드 승/패에 미반영 (`무효` 카운터만 +1)
3. 위 문구로 토스트 1회

**설정**: `세션 무효화 사용` 마스터 스위치(기본 ON) + 트리거별 개별 토글(기본: `Expired` 만 OFF).

---

## 8. 명세 5 — 개인 리더보드 *(후순위)*

### 8.1 두 층위

| 층위 | 범위 | 저장 위치 | 우선 |
|---|---|---|---|
| **나의 통산** | **세이브 무관, 플레이어 본인 누적** | `GenFilePaths.ConfigFolderPath/RoyalGameOfUr_Records.xml` | **주** |
| **이 식민지** | 현재 세이브의 판별 기록 (어느 식민자 이름으로 뒀는지 포함) | `GameComponent_Ur` (세이브에 포함) | 부 |

> 상대가 AI 봇으로 고정되므로 "식민자 랭킹"은 의미가 약하다. **플레이어 본인의 난이도별 전적**이 핵심 지표.

### 8.2 기록 항목

```csharp
public sealed class UrRecord : IExposable {
    public int   wins, losses, resigns, voided;   // voided = 무효화된 판
    public int[] winsByTier, lossesByTier;        // 난이도별 전적 (초보~명인)
    public int   fastestWinTurns;                 // 최소 턴 승리
    public int   longestWinStreak, currentStreak;
    public int   totalCaptures, totalCapturesTaken;
    public int   rosetteLandings;
    public int   perfectWins;                     // 한 번도 안 잡히고 승리
    public int   totalUndosUsed;
    public long  totalRealSecondsPlayed;
}
```

### 8.3 UI — `Dialog_UrLeaderboard`

```
┌─ 우르의 게임 · 기록 ─────────────────────────────────┐
│ [ 나의 통산 ]  [ 이 식민지 ]                          │
├─────────────────────────────────────────────────────┤
│  난이도    전적       승률    최속승   최다연승        │
│  초보      14–2       88%     19수      9             │
│  견습      21–9       70%     22수      6             │
│  숙련      12–15      44%     26수      3             │
│  상급       3–11      21%     31수      1             │
│  명인       0–4        0%      –        0             │
├─────────────────────────────────────────────────────┤
│  통산 50–41 · 무효 3 · 잡은 말 187 · 무패승 2회        │
│  누적 플레이 4시간 12분                                │
│                        [ 기록 초기화 ]   [ 닫기 ]     │
└─────────────────────────────────────────────────────┘
```

- 진입: 게임 창 내 `기록` 버튼 / 보드 Gizmo `기록 보기` / 모드 설정 창
- **온라인 랭킹·계정 연동 없음** — 로컬 파일 하나로 끝
- 확장 여지(후순위): 업적 배지 8종 (첫 승, 무패승, 10연승, 명인 격파 등)

---

## 9. UI 레이아웃

`Dialog_UrGame : Window` — `InitialSize` 약 900×620, `doCloseX = true`, **`forcePause = true`**, `absorbInputAroundWindow = false`.

```
┌─ 우르의 게임 · 숙련 ────────────────────────────────────── [?] [X] ┐
│  당신 (아이린)                 vs                    AI 봇 (숙련)  │
│  대기 ●●●○○○○  골인 3                     대기 ●●●●●○○  골인 2    │
├───────────────────────────────────────────┬──────────────────────┤
│                                           │  ▸ 수순              │
│    [✦][ ][ ][ ]        [✦][ ]             │  12. 당신   4→8 ✦    │
│    [ ][●][ ][✦][ ][○][ ][ ]               │  13. 당신   투입→2   │
│    [✦][ ][○][ ]        [✦][●]             │  14. 봇     6→8 ✕잡힘│
│                                           │  15. 당신   패스(0)  │
│         ┌──────────────┐                  │                      │
│         │  ▲ ▲ △ ▲     │  = 3            │  ▸ 이번 턴           │
│         └──────────────┘                  │  주사위 3·이동 2가지 │
│         [ 주사위 굴리기 ]                   │                      │
├───────────────────────────────────────────┴──────────────────────┤
│ [무르기 2/3] [재시도] [기권] [연습] [기록]     ⏸ 일시정지 · 저장됨 │
└──────────────────────────────────────────────────────────────────┘
```

- 클릭 흐름: `주사위 굴리기` → 합법수 하이라이트 → **말 클릭** → 도착칸 미리보기 → 확정
- 합법수가 1개뿐이면 `자동 진행` 옵션(기본 ON)으로 원클릭
- 키보드: `Space`=굴리기, `1~7`=말 선택, `Ctrl+Z`=무르기, `Esc`=닫기
- 색맹 대응: 말 구분을 색 + **모양(● / ○)** 이중 인코딩
- 하단 우측에 **일시정지 중임을 항상 표시** → 플레이어가 콜로니 걱정을 안 하게

---

## 10. RimWorld 통합 (최소 접점)

### 10.1 접점 목록 — 이게 전부다

| 접점 | 내용 | Def 수정 |
|---|---|---|
| `GameOfUrBoard` 에 `CompUrBoard` 주입 | 우클릭 메뉴 / 기즈모 / 오버레이 | **추가만** (PatchOperationAdd) |
| `GameComponent_Ur` | 세션 보관 + 무효화 폴링 | 신규 |
| `Dialog_*` 3종 | 게임 / 튜토리얼 / 기록 | 신규 |
| `RGUMod : Mod` | 설정 창 | 신규 |
| `RGU_GoToUrBoard` JobDef + JobDriver | 몰입 모드(기본 OFF)에서만 발급. 보드까지 걸어가 마주 본 뒤 창을 연다 | 신규 |
| `GameComponent_Ur` | 보상 하루 한도(폰당 2판)를 세이브에 기록. Def 없이 자동 등록 | 신규 |
| `RGU_WonAtUr` / `RGU_LostAtUr` ThoughtDef | 승패 기분. **기본 OFF** | 신규 |
| `RGU_WonUrMatch` TaleDef | 승리 시 기록 → 예술 작품 소재 | 신규 |
| 사운드 | 바닐라 SoundDef 8종을 `GetNamedSilentFail` 로 조회해 차용. 못 찾으면 무음 | **Def 추가 없음** |

**바닐라 `Play_GameOfUr` Job / JoyGiver / ThingDef 스탯은 일절 수정하지 않는다.**
폰은 평소처럼 알아서 우르를 두고 joy를 얻는다. 모드는 그 위에 창을 얹을 뿐.

### 10.2 보상 (기본 최소, 전부 설정 토글)

게임 시간이 멈춘 상태에서 플레이하므로 **시간 비례 보상은 성립하지 않는다.** 판 종료 시 일회성 보상만 준다.

| 항목 | 기본값 | 값 |
|---|---|---|
| 우클릭한 폰에게 joy | **ON** | 승리 `+0.25` / 패배 `+0.15` (Gaming_Cerebral) |
| 지적 XP | **ON** | 승리 `30` / 패배 `10` — 바닐라 1회 여가(약 6 XP) 대비 완만 |
| 하루 보상 한도 | – | **폰당 하루 2판**까지만. 초과분은 보상 0 (파밍 차단) |
| 기분 버프 `RGU_WonAtUr` | **OFF** | +3, 1일 |
| 기분 페널티 `RGU_LostAtUr` | **OFF** | −1, 0.5일 |
| Tale `RGU_WonUrMatch` | ON | 승리 시 기록 (예술 작품 소재) |

- 폰을 선택하지 않고 E2(기즈모)로 시작하면 **보상 없음**. 순수 미니게임.
- 연습 모드는 항상 보상 없음.
- `보상 전부 끄기` 원클릭 스위치 제공 (P6).

### 10.3 호환성

| 대상 | 판정 |
|---|---|
| Game of Ur on Tables (`2546734176`) | **호환 예상** — `ThingComp` 는 배치 위치와 무관 |
| RimChess / RimPoker | **무충돌** — 다른 Def, 다른 창 |
| 우르 보드를 추가/수정하는 모드 | `PatchOperationAdd` 가 `GameOfUrBoard` 에만 적용 → 모드 추가 보드는 대상 외(향후 확장 가능) |
| Zetrith's Multiplayer | **범위 외.** 서버 스택이 필요한 별개 작업. About 설명에 미지원 명시 |
| 세이브 안전성 | 모드 제거 시 `GameComponent`/`ThingComp` 데이터만 유실, 보드는 바닐라로 정상 복귀 |

---

## 11. 모드 설정 (`Dialog_ModSettings`)

| 그룹 | 항목 | 기본값 |
|---|---|---|
| **플레이** | 창 열면 게임 시간 강제 일시정지 | **ON** |
| | 몰입 모드 (폰이 보드까지 걸어간 뒤 시작) | OFF |
| | 합법수 1개일 때 자동 진행 | ON |
| | 선공 랜덤 | OFF (플레이어 선공) |
| **AI** | 기본 난이도 | 견습 (마지막 선택값 기억) |
| | 폰 지능으로 난이도 자동 결정 | OFF |
| | AI 사고 연출 시간 | 0.6초 |
| **학습** | 합법수 하이라이트 / 잡기 예고 / 칸 툴팁 / 수순 로그 | ON |
| | 위험 확률 표시 | OFF |
| | 튜토리얼 다시 보기 | (버튼) |
| **세션** | 세션 무효화 사용 | ON |
| | └ 트리거별 토글 7종 | `Expired` 만 OFF |
| | 세션 만료 일수 | 3일 |
| | 판당 무르기 횟수 | 3 |
| | 상급 이상 난이도에서 무르기 금지 | ON |
| **보상** | 판 종료 시 joy 지급 / 지적 XP 지급 | ON |
| | 승리 기분 버프 / 패배 기분 페널티 | OFF |
| | **보상 전부 끄기** | (원클릭 스위치) |
| **기록** | 개인 통산 기록 초기화 | (버튼, 확인 필요) |

---

## 12. 구조 — 프레임워크와 게임

우르 하나로 끝낼 계획이었으나, 같은 창을 편자막대·후프스톤에도 쓰기로 하면서
**인게임 계층을 게임을 모르는 프레임워크와 게임별 워커로 갈랐다.**

### 12.1 이음매

프레임워크는 **수명만** 소유한다. 게임은 **Rect 하나 안의 전부**를 소유한다.

| 프레임워크가 소유 | 게임이 소유 |
|---|---|
| 진입 — 우클릭 · 기즈모 · 걸어가기 Job | 판 그리기와 조작 전부 (`Rect` 하나 받고 그 안은 자유) |
| 창 껍데기 — 일시정지, 위협 인터럽트, 도구 줄, 상태 줄 | 규칙 · AI · 연출 |
| 세션 저장 · 이어하기 · 무효화 7종 | 자기 상태 직렬화(`MiniGameSaveData` 파생) |
| 난이도 선택 창, 기록, 숙련도, 튜토리얼 쪽 넘김 | 난이도별 동작, 튜토리얼 쪽 그림 |

**턴도 주사위도 상대도 프레임워크의 개념이 아니다.** 그래서 실시간 조준 게임인 편자막대가
턴제 보드게임인 우르와 같은 창에 들어간다 — 구현체 둘이 서로 아무것도 공유하지 않는데도.

`MiniGameWorker` 의 필수는 다섯이다: 시작하고(`StartNew`), 갱신하고(`Tick`), 그리고(`DrawPlayArea`),
끝났는지 답하고(`IsOver`/`PlayerWon`), 진행량을 낸다(`Rounds`/`SavePoint`).
무르기 · 로그 · 저장 · 튜토리얼 · 설정은 전부 선택이다 — 편자막대는 무르기를 그냥 두었다.

게임별 설정은 `PRSettings` 의 이름표 자루(`GetBool("PR_Ur.highlight", …)`)에 실린다.
ModSettings 는 Def 가 로드되기 **전에** 읽히므로, 게임별 설정을 타입으로 나누면 불러올 시점에 그 타입이 없다.

**승부가 아닌 항목**은 `hasMatch=false` 하나로 갈린다. 끄면 이기고 지는 것도, 전적도,
무르기·재시도·기권도, 난이도 선택도 사라진다 — 프레임워크는 창만 열어 준다.
망원경이 그 첫 사례이고, 프레임워크는 여전히 "별"이라는 말을 모른다.

### 12.2 폴더

```
RoyalGameOfUr/                     (저장소 이름. 모드 이름은 Playable Recreation)
├─ About/                          (packageId: tr8425.playablerecreation)
├─ Defs/
│  ├─ MiniGameDefs/MiniGames_PR.xml   (PR_Ur · PR_Chess · PR_Poker · PR_Billiards
│  │                                    PR_Horseshoes · PR_Hoopstone · PR_Stargazing)
│  ├─ JobDefs/Jobs_PR.xml             (PR_GoToGame — 몰입 모드 전용)
│  ├─ ThoughtDefs/Thoughts_PR.xml     (게임마다 6단계, 플레이어 숙련도를 따라감)
│  └─ TaleDefs/Tales_PR.xml           (PR_WonMatch — 다섯이 공용)
├─ Patches/Patch_Recreation.xml    (일곱 가구에 CompProperties_MiniGame 주입 — 추가만)
├─ Textures/PR/Chess/*.png         (기물 실루엣 6종. Tools/make_pieces.py 가 만든다)
├─ Textures/PR/Cards/*.png         (카드 무늬 4종. Tools/card_suits.py 가 만든다)
├─ Languages/{English,Korean}/Keyed/{PR,RGU,CHS,POK,BIL,THR,STG}.xml
├─ Assemblies/PlayableRecreation.dll
└─ Source/PlayableRecreation/
   ├─ Framework/                   ★ 게임을 하나도 모른다
   │  ├─ MiniGameDef.cs · MiniGameWorker.cs · MiniGameSaveData.cs
   │  ├─ CompMiniGame.cs · GameEntry.cs · JobDriver_GoToGame.cs · PRDefOf.cs
   │  ├─ GameSession.cs · Invalidation.cs · GameComponent_Recreation.cs
   │  ├─ MapComponent_Recreation.cs · Mastery.cs · Thought_Mastery.cs
   │  ├─ GameRecord.cs · RecordStore.cs · PRSettings.cs · PRMod.cs
   │  └─ UI/ Dialog_MiniGame · Dialog_Difficulty · Dialog_Leaderboard
   │         Dialog_Tutorial · PRTheme · PRTextures
   └─ Games/                       ★ 서로를 모른다
      ├─ Ur/
      │  ├─ Core/                  ★ Verse 의존 0 — 단위 테스트 대상
      │  ├─ AI/                    ★ Verse 의존 0
      │  ├─ UrGameWorker.cs · UrSaveData.cs · UrSettings.cs
      │  └─ UrBoardRenderer.cs · UrDiceWidget.cs · UrTextures.cs · UrSounds.cs
      ├─ Chess/
      │  ├─ Core/                  ★ Verse 의존 0 — 판 · 규칙 · 평가 · 탐색 · 기보 · Zobrist
      │  └─ ChessGameWorker.cs · ChessSaveData.cs · ChessSettings.cs · ChessTheme.cs
      ├─ Poker/
      │  ├─ Core/                  ★ Verse 의존 0 — Cards · HandEval · HoldemMatch · PokerAi
      │  └─ PokerGameWorker.cs · PokerSaveData.cs · PokerSettings.cs · PokerTheme.cs
      ├─ Billiards/
      │  ├─ Core/                  ★ Verse 의존 0 — Vec2 · PoolTable · PoolSim · NineBall · PoolAi
      │  └─ BilliardsGameWorker.cs · BilliardsSaveData.cs · BilliardsTheme.cs
      ├─ Stargazing/               (승부가 아닌 첫 항목)
      │  ├─ Core/                  ★ Verse 의존 0 — SkyMath · StarField · Constellations
      │  └─ StargazingWorker.cs · SkyWatch.cs · StargazingComponent.cs
      │     StargazingSettings.cs · StarTheme.cs · Dialog_NameConstellation.cs
      └─ Throwing/                 (편자막대 · 후프스톤 — 워커 하나, Def 둘)
         ├─ Core/                  ★ Verse 의존 0 — ThrowRules · ThrowMatch · ThrowAim
         └─ ThrowGameWorker.cs · ThrowRulesExtension.cs · ThrowSaveData.cs · ThrowTheme.cs

Tests/RoyalGameOfUr.Tests/         (Core/AI 전용 — RimWorld 없이 실행)
```

### 12.3 게임을 하나 더 붙이려면

1. `MiniGameWorker` 파생 하나 (+ 저장을 지원하면 `MiniGameSaveData` 파생 하나)
2. `MiniGameDef` 하나 — workerClass, 난이도 수, 튜토리얼 쪽수, 집계 이름표, 바닐라 여가 Job
3. `Patches/` 에 그 가구로 `CompProperties_MiniGame` 한 줄
4. 번역 키 한 벌

프레임워크는 손대지 않는다. 후프스톤은 2번과 3번만으로 만들어졌다 — 편자막대와 같은 워커에
`ThrowRulesExtension` 값만 달리 주었다(이닝당 던지기 3회, 15점 선취, 던질 때마다 채점).

**이음매는 성격이 다른 게임들로 검증되었다.** 나인볼은 규칙이 아니라 물리로 굴러가고,
체스는 상대가 오래 생각하고, 포커는 상대가 승률을 시뮬레이션으로 잰다. 셋 다 프레임워크를
**한 줄도 고치지 않고** 붙었다 — 공을 굴리는 것도, 탐색을 한 깊이씩 훑는 것도,
표본을 프레임당 90개씩 뽑는 것도 워커가 `Tick(now)` 안에서 예산을 나눠 쓸 뿐이다.

프레임워크가 **딱 한 번** 늘어난 것은 망원경 때문이다. 이길 수 없는 항목이 있다는 사실은
게임의 성질이 아니라 프레임워크의 개념이므로, `hasMatch` 플래그 하나로 받았다.

---

## 13. 개발 로드맵

| 마일스톤 | 산출물 | 완료 기준 |
|---|---|---|
| **M0 뼈대** ✅ | About.xml, csproj, XML 패치, `CompUrBoard`, `Dialog_UrGame` | 폰 선택 → 보드 우클릭 → "우르의 게임 두기" → **빈 창이 뜨고 시간이 멈춘다** · 빌드 경고 0 |
| **M1 룰 엔진** ✅ | `Core/*` + xUnit 테스트 57개 | 경로/로제트/잡기/추가턴/안전칸/정확골인 **전부 통과**. 랜덤 대 랜덤 10,000판 무한루프·예외 0 |
| **M2 UI** ✅ | `UrMatch` · `Dialog_UrGame` · `UrBoardRenderer` · `UrDiceWidget` · 절차적 텍스처 | 마우스만으로 한 판 완주. 원클릭 조작 · 합법수 강조 · 자동 패스 · 수순 로그 |
| **M3 AI** ✅ | T0~T4 · `UrEvaluator` · `ExpectiminimaxAi` · `Dialog_UrDifficulty` | **명인 vs 견습 92.7%** · 1수 판단 **1.62ms** (예산 50ms) |
| **M4 마감** ✅ | 판 종료 보상 · 사운드 · 몰입 모드 Job · 폰 지능 연동 · 설정 창 3그룹 | 빌드 경고 0 · DefOf 5종 defName 일치 · 번역 키 64개 한/영 일치 |
| **M5 튜토리얼** ✅ | 6쪽 온보딩(`Dialog_UrTutorial`), 연습 모드, 학습 보조 3종 | 첫 대국 시 자동 1회 노출 · 도식 6종 · 칸 툴팁 · 잡힐 확률 |
| **M6 세션/무효화** ✅ | `UrSession` · `UrInvalidation` · 60틱 폴링 · 무르기/재시도/기권 | 턴 시작 시점 자동 저장 · 무효화 7종 · 되감기 테스트 6종 통과 |
| **M7 리더보드** ✅ | `UrRecord` · `UrRecords`(config 파일) · `Dialog_UrLeaderboard` | 나의 통산(세이브 무관) + 이 식민지 2탭 |
| **M8 배포** ✅ | Preview.png · ModIcon.png · About 설명 · 점검 스크립트 | 빌드 경고 0 · 테스트 89개 · 번역 키 156개 한·영 일치 |
| **M9 프레임워크** ✅ | `Framework/` 분리 · 우르 이식 · 편자막대 · 후프스톤 | 프레임워크가 게임을 참조하지 않음 · 게임끼리 서로 참조하지 않음 · 테스트 103개 · 번역 키 198개 |
| **M10 나인볼** ✅ | 결정론적 물리(1/480초 고정 스텝) · 고스트볼 조준 · 후보 샷 예행 AI | 프레임워크 무수정 · 물리 테스트 22개 · 프레임당 후보 2개만 재 보므로 창이 끊기지 않음 |
| **M11 체스** ✅ | 0x88 판 · 합법수 생성 · 알파베타+정지탐색 · SAN 기보 · Zobrist 되풀이 | **perft 5개 위치 전부 공표치 일치** · 테스트 35개 · 프레임당 한 깊이씩 훑어 뜸들이는 사이에 깊어짐 |
| **M12 포커** ✅ | 일대일 노리밋 홀덤 · 7장 값매김 · 몬테카를로 승률 AI | 250판 무작위 대국에서 **칩 총합 불변** · 테스트 32개 · 사이드 팟 없음(받을 수 없는 몫은 반환) |
| **M13 별 보기** ✅ | 시드 하늘 · 위도/경도/시각 투영 · 별자리 잇기 · 오디세이 궤도 물체 | 프레임워크에 `hasMatch` **한 줄 추가** · 테스트 13개 · 위도가 보이는 별을 실제로 가름 |

**의존 관계**: M1은 M0과 병행 가능(Verse 무관). M3은 M1 필수. M6/M7은 M2 이후 어디든.
**rev.1 대비**: JobDriver·joy 틱·폰 대 폰이 사라져 M4가 크게 가벼워졌다.

---

## 14. 리스크 & 미결 사항

| # | 리스크 | 영향 | 대응 |
|---|---|---|---|
| R1 | Expectiminimax depth 5가 응답 지연 | 하 | 일시정지 상태라 프레임 예산 여유. 반복 심화 + 50ms 상한 |
| R2 | 청소 감지(오물 카운트 폴링)의 오탐/미탐 | 하 | **구현: 보드 반경 3.9칸의 오물 수를 60틱마다 비교, 줄었으면 청소로 판정.** 늘어난 경우는 기준선만 올린다. 여전히 Harmony 0 |
| R3 | 1119개 설치 모드 환경에서의 충돌 | 중 | **클린 프로필**(Core + Harmony + 본 모드)로 개발·QA |
| R4 | 우르 보드가 없는 콜로니는 진입 불가 | 하 | 교역·폐허·퀘스트로 흔히 획득 가능. Workshop 설명에 "보드가 필요합니다" 한 줄만 명시 |
| R5 | `forcePause` 로 창을 열어둔 채 방치 시 세이브 타이밍 혼선 | 하 | 창 열린 채 세이브 시 세션도 함께 직렬화되므로 안전. 테스트 케이스로 명시 |

**확정 사항 (2026-08-31)**

| 항목 | 결정 |
|---|---|
| 룰셋 | **Finkel 룰 단일.** Masters 룰은 1차 릴리스 제외. 경로 테이블은 `UrBoardLayout` 한 곳에 상수로 모아둬 나중에 교체 가능 |
| 말 개수 | **7개 고정** |
| 창 내부 보드 아트 | **바닐라 톤 손그림.** 맵 위 가구는 바닐라 `Things/Building/Joy/GameOfUr` 그대로 사용 |
| 보상 기본값 | **소량 ON.** ~~판 종료 보상~~ → 오너 방침(2026-08-31)에 따라 **숙련도 방식으로 교체**: 난이도를 처음 깰 때마다 영구 +4%(최대 +20%)로 식민자의 우르 여가·지적 XP 증가. 폰에게 반복 플레이를 강요하지 않는다 |
| 생각(Thought) | `RGU_PlayedUr` 6단계. 문구가 **플레이어 숙련도**를 따라 '어떻게 하는지도 모르고 가지고 놀았어' → '나는 이 게임을 마스터했다' 로 바뀐다 |
| 착수 범위 | **M0 + M1 동시 착수** |

---

## 15. 현재 상태

**M0 ~ M13 완료 (2026-08-31)**

| | 산출물 |
|---|---|
| **M0 뼈대** | `About.xml` · 가구 패치 · 우클릭 + 기즈모 · 모드 설정 · 한/영 번역 |
| **M1 룰 엔진** | `Games/Ur/Core/`(`Verse` 의존 0). 랜덤 대 랜덤 10,000판 무결 |
| **M2 UI** | 원클릭 말 조작, 합법수/잡기 강조, 주사위 위젯, 자동 패스, 수순 로그 |
| **M3 AI** | 난이도 5단계 + Expectiminimax. 명인 vs 견습 92.7% · 1수 1.62ms |
| **M4 마감** | 사운드 · 몰입 모드 Job · 폰 스킬 연동 · 설정 창 |
| **M5 튜토리얼** | 6쪽 온보딩 — 매 쪽 실제 도식. 게임마다 처음 한 번 자동. 연습 판(최저 단계·미기록) |
| **M6 세션/무효화** | `GameSession`(게임이 정한 지점에서 자동 저장) · 무효화 7종 · 60틱 폴링 · 무르기 · 재시도(하루 1회) · 기권 |
| **M7 리더보드** | `GameRecord`/`RecordStore` — 나의 통산은 `Config/PlayableRecreation_Records.xml`, 이 식민지는 세이브 내부. 게임 탭 + 범위 탭 |
| **M8 배포** | `About/Preview.png`(640×360, 5면 구성) · `ModIcon.png` · Workshop 설명(한/영) · 점검 스크립트 |
| **M9 프레임워크** | 인게임 계층을 `Framework/` 와 `Games/` 로 분리. 우르 이식 + 편자막대 + 후프스톤 |
| **M10 나인볼** | 물리로 굴러가는 첫 게임. 공은 `Tick(now)` 안에서 구르고, 상대의 조준은 프레임당 후보 2개씩 |
| **M11 체스** | 상대가 오래 생각하는 첫 게임. 반복 심화를 프레임당 한 깊이씩. 기물 그림은 `Tools/make_pieces.py` 가 만든다 |
| **M12 포커** | 상대가 자기 손을 시뮬레이션으로 재는 첫 게임. 표본을 프레임당 90개씩 |
| **M13 별 보기** | 이길 수 없는 첫 항목. 하늘은 세계 시드가, 보이는 것은 위도·시각·날씨가 정한다 |

**게임 셋**

| | 가구 | 규칙 | 연동 스킬 | 무르기 |
|---|---|---|---|---|
| 우르의 게임 | `GameOfUrBoard` | 필켈 복원 룰. 말 7개, 4면 주사위 4개 | 지적 | ○ |
| 체스 | `ChessTable` | 표준 룰 전부(캐슬링·앙파상·승격·스테일메이트·50수·3회 반복) | 지적 | ○ (두 수씩) |
| 포커 | `PokerTable` | 일대일 노리밋 홀덤. 24핸드, 블라인드는 8핸드마다 두 배 | 지적 | ✕ |
| 나인볼 | `BilliardsTable` | 낮은 번호를 먼저 맞힌다. 9번을 정당하게 넣으면 승 | 사격 | ✕ |
| 편자 던지기 | `HorseshoesPin` | 이닝당 2번, 21점 선취. 이닝이 끝나면 한쪽만 득점 | 사격 | ✕ |
| 후프스톤 | `HoopstoneRing` | 이닝당 3번, 15점 선취. 던질 때마다 채점 | 사격 | ✕ |
| 별 보기 | `Telescope` | **승부 아님.** 하늘을 보고 별자리에 이름을 붙인다 | — | — |

던지기 두 종은 **워커 하나에 Def 둘**이다. 규칙 차이는 `ThrowRulesExtension` 값 네 개가 전부다.
상대의 던지기는 `(시드, 순번)`으로 결정되므로 이어 던져도 같은 결과가 나온다 — 우르의 주사위와 같은 원리(P4).

**포커**의 위험한 곳은 AI 가 아니라 두 군데였다 — 손의 값매김(킥커 하나를 빠뜨려도 게임은 잘 돌아간다)과
올인 처리다. 앞은 등급·킥커·트립 두 벌까지 케이스로 묶었고, 뒤는 **칩 총합 불변**으로 잡았다.
250판을 무작위로 돌려 매 수마다 `스택+팟+베팅 == 800` 을 확인한다. 이 검사가 블라인드가 스택보다 클 때
받을 수 없는 칩을 돌려주지 않아 무한 체크에 빠지던 버그를 잡아냈다.

**별 보기**는 이 모드에서 유일하게 이길 수 없는 항목이다. 별은 `World.info.Seed` 가 정하므로
저장할 것이 없고, 무엇이 보이는지는 `WorldGrid.LongLatOf(map.Tile)` 의 실제 위도·경도와
`GenLocalDate` 의 시각·계절, `skyManager.CurSkyGlow` 와 강수량이 정한다. 고도·방위 계산은
진짜 천문 공식이고 값만 이 행성의 것이다 — 그래서 북쪽 끝에서는 남쪽 별이 아예 뜨지 않고,
극 근처의 별은 지지 않고 하루 종일 돈다. 중력선이 움직이면 타일이 바뀌고 하늘도 따라 바뀐다.
플레이어가 이은 별자리는 가구가 아니라 `GameComponent` 에 남는다 — 하늘은 세계의 것이기 때문이다.

**나인볼**은 규칙이 아니라 물리다. 1/480초 고정 스텝이라 같은 샷은 언제나 같은 결과를 낳고,
상대는 후보 샷을 **같은 시뮬레이터에 미리 넣어 보고** 고른다 — 재 본 것과 치는 것이 정확히 같다.
**체스**의 위험은 AI가 아니라 수 생성의 정확성이었고, 그것은 perft 로 증명된다(5개 위치, 전부 공표치 일치).
난이도는 탐색 깊이 1~5 와 "일부러 최선을 비켜 둘 확률" 0.55~0.0 으로 만든다.

**보상 구조 (오너 방침 반영)**

폰에게 반복 플레이를 강요하지 않는다. **플레이어가 각 난이도를 처음 깨는 것**만이 숙련도를 올리고,
숙련도는 식민자가 (바닐라 여가로) **그 가구를 쓸 때** 얻는 여가·스킬 XP를 단계당 +4%, 최대 +20% 늘린다.
획득량은 바닐라 `JobDef` 가 이미 들고 있는 `joySkill`·`joyXpPerTick` 에서 그대로 읽어 쓴다.
숙련도는 **게임마다 따로** 쌓인다 — 우르를 마스터해도 편자는 처음부터다.

**검증**

```
dotnet build -c Release   경고 0 · 오류 0
dotnet test               205개 통과 (우르 89 + 던지기 14 + 당구 22 + 체스 35 + 포커 32 + 하늘 13)
                          체스 35개 중 20개가 perft — 다섯 위치의 노드 수가 공표치와 일치
XML                       23종 유효
번역 키                    385개 한·영 완전 일치 · 미사용 키 0 · 코드가 쓰는 키 누락 0
```

**인게임 검증**: 우르는 RimWorld 1.6 실환경에서 우클릭 → 난이도 선택 → 대국 진행 **동작 확인**(초보).
튜토리얼 · 세션 무효화 · 리더보드 · **프레임워크 분리 이후 전 구간** · **던지기 · 나인볼 · 체스 ·
포커 · 별 보기**는 **아직 인게임 미확인** — 배포 전 클린 프로필 QA 필요.
별 보기는 특히 그렇다. 위도·시각·날씨·오디세이 궤도 물체는 전부 실제 게임 상태를 읽으므로
단위 테스트가 닿지 않는다 — 하늘 계산이 맞다는 것과 그 값이 실제로 들어온다는 것은 다른 문제다.

**남은 일 (배포 직전)**

1. 클린 프로필(Core만) QA
   - 일곱 가구 우클릭 · 기즈모 · 인스펙트 문자열
   - 세이브/로드 후 이어하기 (우르 = 턴, 던지기 = 이닝, 나인볼 = 샷 정산, 체스 = 한 수, 포커 = 한 액션)
   - 텍스처가 실제로 로드되는지(`Textures/PR/Chess/`, `Textures/PR/Cards/`)
   - **별 보기**: 위도가 다른 두 정착지의 하늘이 실제로 다른가 · 낮/밤/비에 보이는 별이 바뀌는가 ·
     별자리를 이어 이름 붙인 뒤 세이브/로드로 남는가 · 망원경을 부숴도 남는가
   - **오디세이**: 궤도 물체가 뜨는가 · 소행성/정거장에서 행성과 지표 보기가 되는가 ·
     중력선으로 이동한 뒤 하늘이 바뀌는가 · 우주 맵에서 지평선 없이 보이는가
   - 무효화 트리거 3종(청소 · 수리 · 전투) 실증
   - 게임별 튜토리얼 자동 1회, 게임별 숙련도 누적, 게임 on/off 토글
   - 한국어 오타 육안 확인 — 점검 스크립트는 키 짝만 보지 문장은 못 본다
2. Steam Workshop 업로드

개발용 정션: `RimWorld\Mods\RoyalGameOfUr` → 이 저장소 (해제하려면 그 폴더만 삭제)
웹 플레이테스트 벤치: `Tools/WebPreview/index.html` — 우르 룰·AI 이식본. 주사위는 C#과 **비트 단위 일치**

**빌드 방법**

```
# 모드 어셈블리 (Assemblies\PlayableRecreation.dll 로 출력)
dotnet build Source/PlayableRecreation -c Release
# RimWorld 경로가 다르면
dotnet build Source/PlayableRecreation -c Release -p:RimWorldDir="D:\...\RimWorld"

# 룰 엔진 · AI 테스트 (RimWorld 불필요)
dotnet test Tests/RoyalGameOfUr.Tests
```
