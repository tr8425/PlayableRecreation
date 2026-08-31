# 우르의 게임 (Royal Game of Ur) — RimWorld 모드 기획서

> 오락가구 **Game-of-Ur board** 를 우클릭해 **플레이어 본인이** AI 봇과 우르의 게임을 두는 모드.
> 작성일: 2026-08-30 (rev.2) / 대상: RimWorld 1.6

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

> 식민지의 우르 보드를 우클릭하면, **기원전 2500년의 실제 보드게임을 당신이 직접 둔다.**

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

## 12. 프로젝트 구조

```
RoyalGameOfUr/
├─ About/
│  ├─ About.xml                  (packageId: tr8425.royalgameofur, supportedVersions 1.6,
│  │                              외부 의존성 없음)
│  ├─ Preview.png                (640×360)
│  └─ ModIcon.png
├─ Defs/
│  ├─ JobDefs/Jobs_RGU.xml       (RGU_GoToUrBoard — 몰입 모드 전용, 기본 미사용)
│  ├─ ThoughtDefs/Thoughts_RGU.xml  (RGU_WonAtUr / RGU_LostAtUr — 기본 OFF)
│  └─ TaleDefs/Tales_RGU.xml     (RGU_WonUrMatch — 예술 작품 소재)
├─ Patches/
│  └─ Patch_GameOfUrBoard.xml    (GameOfUrBoard 에 CompProperties_UrBoard 주입 — 추가만)
├─ Languages/
│  ├─ English/Keyed/RGU.xml
│  └─ Korean/Keyed/RGU.xml
├─ Textures/RGU/                 (board, piece_p, piece_b, die_0, die_1, rosette, overlay_saved)
├─ Sounds/RGU/                   (dice_roll, piece_move, capture, rosette, win, lose)
├─ Assemblies/                   (빌드 산출물 RoyalGameOfUr.dll)
└─ Source/RoyalGameOfUr/
   ├─ RoyalGameOfUr.csproj       (net472, Krafs.Rimworld.Ref 또는 로컬 DLL 참조)
   ├─ Core/                      ★ Verse 의존 0 — 단위 테스트 대상
   │  ├─ Side.cs                 (진영 + Opponent 확장)
   │  ├─ UrBoardLayout.cs        (경로 테이블, 로제트 상수, 좌표 변환)
   │  ├─ UrMove.cs · UrGameState.cs · UrRules.cs
   │  ├─ UrDice.cs               (UrRoll · UrDice · UrDiceStream)
   │  └─ UrMatch.cs              (한 판의 진행 상태 기계 + 기보)
   ├─ AI/                        ★ Verse 의존 0
   │  ├─ IUrAi.cs · RandomAi.cs · GreedyAi.cs · ExpectiminimaxAi.cs
   │  ├─ UrEvaluator.cs
   │  └─ UrDifficulty.cs         (난이도 enum + 깊이/실수율/생성 팩토리)
   ├─ Session/
   │  ├─ GameComponent_Ur.cs     (보상 하루 한도. M6에서 세션 레지스트리 + 무효화 폴링 추가)
   │  └─ UrSession.cs · InvalidationReason.cs             (M6)
   ├─ Integration/
   │  ├─ CompUrBoard.cs          (유일한 게임 접점: 우클릭 + 기즈모)
   │  ├─ UrEntry.cs              (진입 분기: 몰입 모드 / 폰 지능 연동 / 난이도 선택)
   │  ├─ JobDriver_GoToUrBoard.cs(몰입 모드에서만 발급)
   │  ├─ UrRewards.cs            (판 종료 시 joy / XP / Thought / Tale, 하루 한도)
   │  └─ RGUDefOf.cs
   ├─ UI/
   │  ├─ Dialog_UrGame.cs · Dialog_UrDifficulty.cs
   │  ├─ UrBoardRenderer.cs · UrDiceWidget.cs
   │  ├─ UrTextures.cs           (절차적 도형 텍스처 + UrTheme 색상)
   │  ├─ UrSounds.cs             (바닐라 SoundDef 차용 — 전용 오디오는 M8)
   │  └─ Dialog_UrTutorial.cs · Dialog_UrLeaderboard.cs   (M5 / M7)
   ├─ Stats/
   │  └─ UrRecord.cs · UrPersonalStats.cs · UrColonyStats.cs
   └─ Settings/
      └─ RGUMod.cs · RGUSettings.cs · RGUDefOf.cs

Tests/RoyalGameOfUr.Tests/       (Core/AI 전용 — RimWorld 없이 실행)
```

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

**M0 ~ M8 전 구간 완료 (2026-08-31)**

| | 산출물 |
|---|---|
| **M0 뼈대** | `About.xml` · `Patches/Patch_GameOfUrBoard.xml` · `CompUrBoard`(우클릭 + 기즈모) · `RGUMod`/`RGUSettings` · 한/영 번역 |
| **M1 룰 엔진** | `Core/`(`Verse` 의존 0). 랜덤 대 랜덤 10,000판 무결 |
| **M2 UI** | 원클릭 말 조작, 합법수/잡기 강조, 주사위 위젯, 자동 패스, 수순 로그, `forcePause` |
| **M3 AI** | 난이도 5단계 + Expectiminimax. 명인 vs 견습 92.7% · 1수 1.62ms |
| **M4 마감** | 사운드 8종 · 몰입 모드 Job · 폰 지능 연동 · 설정 창 |
| **M5 튜토리얼** | `Dialog_UrTutorial` 6쪽(목표/내 길/주사위/로제트/잡기·안전칸/정확 골인) — 매 쪽 실제 보드 도식. 첫 대국 시 자동 1회. 연습 모드(초보 고정·미기록). 칸 툴팁 · 이동 강조 · 잡힐 확률(기본 OFF) |
| **M6 세션/무효화** | `UrSession`(턴 시작 시점 자동 저장) · `UrInvalidation` 7종 · `GameComponent_Ur` 60틱 폴링 · 무르기(같은 눈 재현) · 재시도(하루 1회) · 기권 · 최초 1회 설명 편지 |
| **M7 리더보드** | `UrRecord`/`UrRecords` — 나의 통산은 `Config/RoyalGameOfUr_Records.xml`, 이 식민지는 세이브 내부. `Dialog_UrLeaderboard` 2탭 |
| **M8 배포** | `About/Preview.png`(640×360) · `About/ModIcon.png` · Workshop 설명(한/영) · 점검 스크립트 |

**보상 구조 (오너 방침 반영)**

폰에게 반복 플레이를 강요하지 않는다. **플레이어가 각 난이도를 처음 깨는 것**만이 숙련도를 올리고,
숙련도는 식민자가 (바닐라 여가로) 우르를 둘 때 얻는 여가·지적 XP를 단계당 +4%, 최대 +20% 늘린다.
식민자가 얻는 생각 `RGU_PlayedUr` 의 문구도 숙련도 단계를 따라 6단계로 바뀐다.

**검증**

```
dotnet build -c Release   경고 0 · 오류 0
dotnet test               89개 통과 (룰 R1~R11 · 주사위 비트 고정 · 10,000판 시뮬 · 되감기/복원 6종)
XML                       8종 유효
번역 키                    156개 한·영 완전 일치 · 미사용 키 0 · 코드가 쓰는 키 누락 0
```

**인게임 검증**: RimWorld 1.6 실환경에서 우클릭 → 난이도 선택 → 대국 진행 **동작 확인**(초보).
M5~M7에서 추가된 튜토리얼·세션 무효화·리더보드는 **아직 인게임 미확인** — 배포 전 클린 프로필 QA 필요.

**남은 일 (배포 직전)**

1. 클린 프로필(Core만) QA — 세이브/로드 후 이어두기, 무효화 트리거 3종(청소·수리·전투) 실증
2. Steam Workshop 업로드

개발용 정션: `RimWorld\Mods\RoyalGameOfUr` → 이 저장소 (해제하려면 그 폴더만 삭제)
웹 플레이테스트 벤치: `Tools/WebPreview/index.html` — 룰·AI 이식본. 주사위는 C#과 **비트 단위 일치**(테스트로 고정)

**빌드 방법**

```
# 모드 어셈블리 (Assemblies\RoyalGameOfUr.dll 로 출력)
dotnet build Source/RoyalGameOfUr -c Release
# RimWorld 경로가 다르면
dotnet build Source/RoyalGameOfUr -c Release -p:RimWorldDir="D:\...\RimWorld"

# 룰 엔진 · AI 테스트 (RimWorld 불필요)
dotnet test Tests/RoyalGameOfUr.Tests
```
