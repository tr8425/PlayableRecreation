# 인계

다음 세션이 제일 먼저 읽는 문서. 지금 어디까지 왔고, 무엇을 먼저 해야 하는가.

작성 시각: 2026-09-01 · 커밋 `97accd9` 이후

> 폴더 이름 바꾸기(구 0절)는 끝났다 — `projects/PlayableRecreation` 로 개명 완료,
> Mods 의 끊어진 `RoyalGameOfUr` 링크 제거 완료, `package.py --install` 통과,
> `Mods/Playable Recreation` 설치 확인 (2026-09-01).

---

## 1. 이게 무슨 모드인가

**Playable Recreation** (`teamrostra.playablerecreation`) — 림월드 1.6.
식민자가 알아서 하던 오락 가구를 플레이어가 직접 하게 만든다. 바닐라 가구 일곱 개.

| 가구 (ThingDef) | 게임 | 네임스페이스 |
|---|---|---|
| `GameOfUrBoard` | 우르의 게임 | `Ur` |
| `ChessTable` | 체스 | `Chess` |
| `PokerTable` | 포커 (헤즈업 홀덤) | `Poker` |
| `BilliardsTable` | 나인볼 | `Billiards` |
| `HorseshoesPin` | 편자 던지기 | `Throwing` |
| `HoopstoneRing` | 후프스톤 | `Throwing` (워커 하나에 Def 둘) |
| `Telescope` | 별 보기 | `Stargazing` |

일곱 개 전부 Core 소속이다. DLC 없이 동작한다. 오디세이가 있으면 별 보기가 더 준다.

**모드 가구 지원 (2026-09-07 추가, v1.1.0)** — 전부 소프트 의존이다. `LoadFolders.xml`
의 `IfModActive` 로 `ModSupport/<모드>/` 폴더째 조건부 로드되므로, 해당 모드가 없으면
Def 도 패치도 존재하지 않는다. About.xml 의존성은 여전히 0개다.

| 지원 모드 (packageId) | 가구 | 게임 |
|---|---|---|
| VFE (`VanillaExpanded.VFECore`) | `Joy_DartsBoard` | 다트 — 신작, `Darts` |
| VFE | `Joy_PunchingBag` | 펀칭백 리듬 게임 — 신작, `Punching` |
| GloomyFurniture (`Solaris.FurnitureBase`) | `GL_ChessTable` · `GL_PokerTable` · `GL_BilliardsTable` | 기존 체스·포커·나인볼 재사용 (패치만) |
| Hospitality: Casino (`Adamas.HospitalityCasino`) | `HC_SlotMachine{Red,Blue,Green}` | 슬롯머신 — 신작, `Slots`, hasMatch=false |

신작 게임의 C# 은 본체 DLL 에 항상 실려 있고, Def 가 없으면 잠들어 있다.
룰렛(하우스전 뱅크런 설계 확정)은 3차분 — 아직 코드가 없다.

규모: C# 104 파일 · 17,700 줄. 테스트 264 개. 배포본 57 파일 · 570 KB.

---

## 2. 절대 깨면 안 되는 것

이건 취향이 아니라 이 모드가 서 있는 자리다.

1. **Harmony 패치 0 개.** 소스에 "Harmony" 가 세 번 나오는데 전부 "안 쓴다"는 주석이다
2. **바닐라 Def 를 수정하지 않는다.** `Patches/` 는 `PatchOperationAdd` 뿐 —
   가구에 컴포넌트 하나를 *더할* 뿐이다. 그래서 모드를 빼도 가구가 바닐라로 돌아온다
3. **`Core/` 와 `AI/` 는 Verse · RimWorld · UnityEngine 을 모른다.** 그래서 테스트가
   림월드 없이 돈다. 테스트 프로젝트가 이 폴더들을 `<Compile Include>` 로 끌어간다
4. **`Framework/` 는 어떤 게임도 모른다. 게임끼리도 서로 모른다.**
   여러 게임이 쓰는 순수 산수(결정론 난수, 두 막대 조준)는 `Framework/Core/AimMath.cs`
   중립 지대에 있다 — 게임이 게임을 참조하는 일은 여전히 없다
5. **플레이어의 게임 속도를 건드리지 않는다.** `TickManager.CurTimeSpeed` 를 읽지도 쓰지도
   않는다. 멈추는 것은 바닐라 `Window.forcePause` 로만 한다.
   **스페이스는 플레이어의 일시정지 키다** — 판을 진행시키는 키는 엔터다 (`Framework/UI/PRKeys.cs`)
6. **`packageId` 를 바꾸지 않는다.** 구독자 세이브가 모드를 못 찾는다
7. **클래스 이름을 함부로 바꾸지 않는다.** `Scribe_Deep` 이 세이브에 `Class=` 로 박는다.
   `*SaveData` 의 네임스페이스를 바꾸면 두던 판이 사라진다

---

## 3. 지금 상태

- 빌드 경고 0 · 오류 0
- 테스트 284 개 전부 통과 (다트 · 펀칭백 · 룰렛 Core 포함)
- **몰입 모드 2칸 1·2단계 들어감** (2026-09-09, 미출시) — 명세는
  `DESIGN_RealityMode.md`. 상대를 골라 둘이 판 앞까지 걸어가고, 판이 끝날
  때까지 거기 선다. **인게임 확인은 아직 안 했다** (§16).
  - 새 파일 `Framework/Together.cs` — 자격 판정. 종족 목록을 안 쓴다
  - 새 파일 `Framework/TogetherMatch.cs` — 진행 상태. **세이브에 안 남긴다.**
    불러온 판에는 붙잡을 이유가 없으므로 여기가 비면 두 `Hold` 가 스스로 끝난다
  - 새 파일 `Framework/TogetherToils.cs` + `JobDriver_JoinGame.cs` — 붙잡는 토일과
    상대 쪽 Job. 배고픔·피로가 임계 아래면 자리를 뜬다
  - 예약 자리 수는 `Together.SeatCount` 가 게임의 `vanillaJob.joyMaxParticipants`
    에서 가져온다. **2 로 고정하면 안 된다** — 포커 4 · 편자 3 · 망원경 1 이고,
    `ReservationManager` 는 `MaxPawns` 가 다르면 개수와 무관하게 즉시 거절한다
  - 설정은 전부 `immersionMode` 아래다. **최상위는 늘지 않았다**
  - 겹상: 기즈모와 이어 두기가 몰입 모드를 우회하던 구멍을 막았다 —
    1칸만 켜 둔 사람에게도 고쳐진다
- `Tools/verify.py` — 번역 키 562 짝, 안 쓰는 키 0, Def 참조 성함
  (ModSupport 의 Def · 패치 · 지원 모드 defName 까지 검사한다)
- `Tools/guistate.py` — GUI 전역 상태를 되돌리지 않는 메서드 0
- `Tools/package.py` — 통과 (61 파일 · 615 KB, LoadFolders.xml 과 ModSupport 포함)
- 인게임 확인: **v1.1.0 QA 완료** (2026-09-08) — VFE 다트·펀칭백·룰렛·아케이드,
  Casino 슬롯, 궤도 지표 줌, Gloomy 가구 3종, 그리고 세 모드를 전부 끈 판까지
  실제로 확인했다. 소프트 의존 경로가 양쪽에서 정상. 남은 확인거리 없음.
  스크린샷에서 잡힌 것 하나(당기기 전 릴이 7-7-7 로 서 있던 것)는 고쳤다
- 아케이드는 게임이 아니라 **추첨함**이다 (`MiniGameDef.randomPick`). workerClass 가 없고,
  난이도만 고르게 한 뒤 `GameEntry.Launch` 가 그때 로드된 승부 게임 중 하나를 뽑아 연다.
  전적·숙련도·저장은 전부 뽑힌 게임 쪽에 쌓인다 — 그래서 추첨함에는 tallyKeys 도
  기록 버튼도 없다 (`verify.py` 가 이 예외를 안다). 가구에 남은 판이 다른 게임의
  것이어도 이어 할 수 있게 `MiniGameDef.Accepts` 가 받아 준다
- 펀칭백 난이도는 1차 QA "쉽다" 피드백으로 조였다 (2026-09-07) — BPM 66~126
  (구 66~106), 판정 창 Perfect 0.07 · Good 0.16 (구 0.09/0.20, 티어 불변 철학 유지),
  목표 점유율 0.55~0.88, 상위 티어 연타 비중·세트 길이(12+티어×2 패턴) 증가.
  헛스윙 무벌점("점수는 안 잃고 콤보만 깨진다")은 튜토리얼 문구와 함께 유지
- 룰렛은 뱅크롤 런이다 — 칩 20 시작, 난이도 = 목표 배수(×1.5~×5 → 30/40/60/80/100),
  테이블 리밋 10 (이게 없으면 ×1.5 와 ×2 가 "올인 한 방"으로 같은 난이도가 된다).
  최적 전략 몬테카를로 승률 65/47/31/23/18% — 사다리 단조 확인
- 슬롯 칩은 세이브 지갑이다 — `GameComponent_Recreation` 의 범용 카운터
  (`GetCounter`/`SetCounter`, `Slots/…` 키)에 저장. 은 200 → 칩 20 충전,
  천 닢 도달 시 콜로니당 한 번 `PR_SlotsThousandClub` 생각 + 연출.
  은을 도로 내주는 길은 일부러 없다 (환전소 금지)
  - **지갑 값을 창이 복사해 들고 있으면 안 된다.** 창은 동시에 여럿 열 수 있어서
    (`GameEntry` 에 단일 창 가드가 없다) 복사본을 쓰면 나중에 쓰는 창이 앞선 창의
    칩을 덮어쓴다. 읽기·쓰기를 매번 카운터로 통과시킬 것 (`Credits`/`AddTokens`)
  - 배당은 당기는 순간 지갑에 반영한다. 릴이 멈출 때 주면 도중에 창이 닫히거나
    습격이 판을 끊었을 때 판돈만 나가고 배당이 증발한다
  - 은은 바닐라 거래와 같은 잣대로만 먹는다 — 안개·금지·(거주구역도 창고도 아닌 것)
    제외. 안 그러면 고대 위험 안의 은까지 원격으로 소비된다
  - 천 닢 도장은 지갑이 천 닢에 닿는 순간(`AddTokens`) 찍고, 연출만 릴이 멈춘 뒤로
    민다. 릴이 멈출 때 판정하면 그 사이 창을 닫거나 옆 기계에서 한 닢을 써서 999가
    된 사람은 영영 그 문 앞을 지나간다
  - 도는 동안 화면에 거는 칩·최고치는 당길 때 찍어 둔 사본이다. 살아 있는 지갑에서
    배당을 빼는 식이면 그 사이 다른 창이 쓴 만큼 어긋나고, 최고치는 릴보다 먼저
    결과를 흘린다
- `Dialog_MiniGame.RecordResult` 의 "손도 대지 않은 판" 잣대는 **기권에만** 댄다.
  규칙대로 끝난 판은 한 수 만에 끝났어도 남긴다 (룰렛의 한 방 승리).
  그 잣대는 `MiniGameWorker.HasProgress` 가 답한다 — 기본은 `Rounds > 1` 이고,
  룰렛은 스핀 하나부터 진행된 판이다(안 그러면 지고 창 닫기가 공짜 재시도다).
  `Rounds` 는 `GameRecord` 의 '최단 승리'로 그대로 쓰이니 부풀려 넘기지 말 것
- 아케이드에서 '새 판'은 하던 게임이 아니라 **그 자리**를 다시 연다
  (`Dialog_MiniGame.Origin` → 가구의 `CompMiniGame.Game`). 뽑힌 게임을 그대로
  다시 열면 그 기계는 두 번째 판부터 추첨함이 아니게 된다

**창작마당에 올라가 있다.** id 3794530553, 2026-09-03 공개, v1.0.0.
`About/PublishedFileId.txt` 커밋됨. 언어별 설명은 `Workshop/description_en.txt` ·
`description_ko.txt` — 웹에서 언어별로 잘라 넣는다 (한도는 언어마다 별도 8,000자).

---

## 4. 다음에 할 일

1. **v1.1.0 재업로드** — QA 는 닫혔다. 남은 것은 올리는 일뿐이다.
   재업로드는 웹의 언어별 설명을 건드리지 않으므로 설명은 직접 붙여 넣어야 한다.
   순서: 림월드에서 Update on Steam Workshop → 스크린샷(`mod_collage.jpg` ·
   `surface_zoomed.jpg`)을 캐러셀에 올리고 → 설명문의 `{{SS6_MOD}}` 자리를 그
   업로드 URL 로 바꾸고 → 언어 탭마다 붙여 넣고 → 변경 기록에
   `Workshop/changenotes_1.1.0.txt` 를 쓴다
2. **저장소 공개 상태에 유의** — 포트폴리오 목적으로 열어 두었다(2026-09-08).
   8/31 커밋 세 개(`87e21e4` · `6bcec96` · `754a954`)가 `tr8425@gmail.com` 으로
   서명되어 있어 커밋 목록에서 보인다. 나머지는 `Team Rostra <tr8425@naver.com>`.
   가리려면 히스토리를 다시 써야 하고 해시가 전부 바뀐다 — 아직 판단 보류

### 손대면 좋을 것 (급하지 않음)

- **언어 추가 (중국어 간체 · 러시아어)** — v1.1.0 을 이미 낸 뒤에 나온 이야기라
  다음 판으로 미뤘다 (2026-09-08). 재보다 만 것을 적어 둔다.
  - 분량: 키 539 짝, 영어 쪽 53,299 자 · 한국어 쪽 49,266 자. 한 언어당 5만 자쯤이다.
    여기에 `About.xml` 의 `description` 과 창작마당 설명(언어 탭)이 더 붙는다
  - 폴더 이름은 `Languages/ChineseSimplified (简体中文)` · `Languages/Russian (Русский)`.
    구독 모드들을 훑어 보면 괄호 없는 `Russian` 꼴도 돌아다니지만, 우리가 쓰는
    `Korean (한국어)` 와 같은 괄호 꼴이 림월드가 자기 언어팩에 쓰는 이름이다
  - **진짜 문제는 분량이 아니라 검수다.** 읽지 못하는 언어는 QA 를 할 수 없다.
    기계 번역을 그대로 실으면 다트·룰렛·우르의 용어와 튜토리얼 문장이 조용히
    어긋나고, 그 상태로 창작마당에 올라간다. 번역을 받을 사람을 구하든지,
    아니면 UI 라벨만 먼저 옮기고 튜토리얼·생각 문구는 영어로 두는 식으로
    범위를 자르는 쪽이 낫다
  - `Tools/verify.py` 는 지금 ko·en 두 짝만 맞춰 본다. 언어가 늘면 그쪽도 같이 고칠 것
- **몰입 모드 2칸 3·4단계** — 3단계는 상대가 사라진 판의 무효화 · 생각 · 방문객
  우호도, 4단계는 관계 반영 · 마주 보게 세우기 · 성격 두 조각이다. 명세는
  `DESIGN_RealityMode.md` §15. **그 전에 1·2단계를 인게임에서 볼 것** — §16 의
  가정 여섯 중 3번은 코드로 답이 나왔고 나머지는 아직 안 봤다. 특히 D6(붙잡아
  두면 대화가 정말 뜨는가)이 아니면 §7.2 의 대화 띠가 늘 비어 있게 된다
- **별 보기 라벨 겹침** — 궤도 물체가 21 개쯤 되면 이름표가 서로 겹친다. 자리 자체는
  이제 하늘에 고루 퍼진다(`SkyMath.Scatter`). 가까운 것만 이름을 띄우거나 마우스를
  올렸을 때만 보이게 하는 식이 남았다
- **DESIGN.md §1~§11** 은 우르 한 게임이던 시절의 명세다. `GameComponent_Ur` 처럼
  지금 없는 이름이 남아 있다. §12 가 현재 구조다

---

## 5. 어디에 무엇이 있는가

```
About/           About.xml · Preview.png (640x360, 영문) · ModIcon.png
Assemblies/      PlayableRecreation.dll  ← 커밋한다. 배포에 필요하다
Defs/            MiniGameDefs 가 핵심. 게임 하나 = MiniGameDef 하나
Languages/       English · Korean. Keyed 539 키씩
LoadFolders.xml  본체("/") + ModSupport 조건부 로드. 배포에 반드시 포함 (package.py 가 챙긴다)
ModSupport/      VFE · Gloomy · Casino. 각각 Defs/Patches/Languages 미니 트리
Patches/         PatchOperationAdd 일곱 개. 이 파일이 모드의 진입점이다
Source/PlayableRecreation/
  Framework/     게임을 모르는 층. Dialog_MiniGame · MiniGameWorker · 저장 · 전적 · 숙련
  Framework/UI/  PRTextures(절차적) · PRContent(파일) · PRKeys · PRTheme
  Games/<이름>/  Core(Verse 0) · AI(Verse 0) · 나머지는 창 그리는 코드
Tests/PlayableRecreation.Tests/   264 개. net9.0 · xUnit. 림월드 불필요
Textures/PR/     체스 기물 6 · 카드 무늬 4. 전부 흰색 RGB + 알파 실루엣
Tools/           verify.py · guistate.py · package.py · make_art.py · WebPreview/
Workshop/        description.txt (창작마당에 붙여넣을 글) · RELEASE.md (절차)
dist/            package.py 가 굽는 곳. .gitignore 됨
```

---

## 6. 자주 쓰는 명령

```bash
cd Source/PlayableRecreation && dotnet build -c Release   # 경고 0 개여야 한다
dotnet test Tests/PlayableRecreation.Tests
python Tools/verify.py
python Tools/guistate.py
python Tools/package.py --install     # 굽고 Mods 에 설치
python Tools/make_art.py              # Preview.png · ModIcon.png 다시 굽기
```

---

## 7. 알아 두면 시간을 아끼는 것들

**기억(memory)은 안 따라온다.** 세션 메모리는 프로젝트 경로로 색인된다.
폴더 이름을 바꾸면 새 폴더는 빈 기억으로 시작한다. 그래서 남길 것은 전부 이 문서와
`DEVLOG.md` 와 `DESIGN.md` 에 적혀 있다.

**Bash 툴에서 파이썬 힙독(`<<'PY'`)에 백슬래시를 넣지 말 것.** `'\\'` 가 `'\'` 로
찌그러져 문법 오류가 난다. 한글도 깨진다. 여러 줄 스크립트는 Write 툴로 파일에 쓰고
`python <경로>` 로 돌리는 편이 안전하다.

**`sed -i` 로 같은 문자열을 여러 군데 바꾸지 말 것.** 한 번 당했다 —
`<tutorialPages>4</tutorialPages>` 를 세 게임에서 동시에 바꿔 버렸다.
앵커를 길게 잡고 파이썬으로 하는 편이 낫다.

**인게임 증거는 `Player.log` 에 있다.**
`C:\Users\tr842\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
체스 기물이 안 보이던 버그를 여기 있던 `null texture passed to GUI.DrawTexture`
5087 줄이 풀었다.
