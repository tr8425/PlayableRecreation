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
- `Tools/verify.py` — 번역 키 526 짝, 안 쓰는 키 0, Def 참조 성함
  (ModSupport 의 Def · 패치 · 지원 모드 defName 까지 검사한다)
- `Tools/guistate.py` — GUI 전역 상태를 되돌리지 않는 메서드 0
- `Tools/package.py` — 통과 (59 파일 · 605 KB, LoadFolders.xml 과 ModSupport 포함)
- 인게임 확인: 기존 일곱 창은 확인 완료. **신작 다섯(다트·펀칭백·슬롯·룰렛·Gloomy 패치)은
  인게임 확인 전이다** — v1.1.0 재업로드 전에 반드시 확인할 것
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
- `Dialog_MiniGame.RecordResult` 의 `Rounds <= 1` 잣대는 **기권에만** 댄다.
  규칙대로 끝난 판은 한 수 만에 끝났어도 남긴다 (룰렛의 한 방 승리).
  `Rounds` 는 `GameRecord` 의 '최단 승리'로 그대로 쓰이니 부풀려 넘기지 말 것

**창작마당에 올라가 있다.** id 3794530553, 2026-09-03 공개, v1.0.0.
`About/PublishedFileId.txt` 커밋됨. 언어별 설명은 `Workshop/description_en.txt` ·
`description_ko.txt` — 웹에서 언어별로 잘라 넣는다 (한도는 언어마다 별도 8,000자).

---

## 4. 다음에 할 일

1. **v1.1.0 인게임 QA** — 다트(조준 클릭 + 두 막대, 세이브·복원), 펀칭백(리듬 판정,
   시작 게이트), 슬롯(릴 연출 · 지갑 · 충전), 룰렛(베팅 클릭 · 휠 연출 · 세이브·복원),
   Gloomy 가구 3종에서 기존 게임 진입. VFE·Gloomy·Casino
   를 켠 판과 끈 판 양쪽에서 (끈 쪽은 로그에 빨간 줄이 없어야 한다)
2. **v1.1.0 재업로드** — 목요일 저녁 소프트 런칭으로 결정됨. 재업로드는 웹의 언어별
   설명을 건드리지 않는다. 갱신 노트에 "Mod support" 요지를 쓸 것
3. **창작마당 설명·스크린샷** — 설명문에 Mod support 절 추가(언어별 파일 갱신),
   모드 가구 이미지는 바닐라 12장 **뒤에** "Requires ..." 뱃지를 이미지에 구워 붙인다

### 손대면 좋을 것 (급하지 않음)

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
Languages/       English · Korean. Keyed 526 키씩
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
