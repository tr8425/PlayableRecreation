# 인계

다음 세션이 제일 먼저 읽는 문서. 지금 어디까지 왔고, 무엇을 먼저 해야 하는가.

작성 시각: 2026-08-31 23:5x · 커밋 `c730506`

---

## 0. 먼저 할 일 — 폴더 이름 바꾸기가 아직 안 끝났다

저장소 안의 이름은 전부 정리됐다. **폴더 이름만 남았다.**
세션이 그 폴더를 작업 디렉터리로 잡고 있어 세션 안에서는 못 바꾼다.

cmd.exe 에서, Claude Code 를 종료한 뒤:

```bat
cd /d C:\Users\tr842\orca\projects
ren RoyalGameOfUr PlayableRecreation

rmdir "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RoyalGameOfUr"

cd PlayableRecreation
python Tools\package.py --install
```

`rmdir` 에 `/s` 를 붙이지 말 것. 그냥 `rmdir` 이면 정션만 지워지고 원본은 안 건드린다.

**이미 끝냈다면** 이 절은 지워도 된다. 확인하는 법 — `Tools/package.py --install` 이
불평 없이 통과하고, 림월드 모드 목록에 `Playable Recreation` 이 하나만 뜬다.

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

규모: C# 117 파일 · 18,800 줄. 테스트 239 개. 배포본 432 KB.

---

## 2. 절대 깨면 안 되는 것

이건 취향이 아니라 이 모드가 서 있는 자리다.

1. **Harmony 패치 0 개.** 소스에 "Harmony" 가 세 번 나오는데 전부 "안 쓴다"는 주석이다
2. **바닐라 Def 를 수정하지 않는다.** `Patches/` 는 `PatchOperationAdd` 뿐 —
   가구에 컴포넌트 하나를 *더할* 뿐이다. 그래서 모드를 빼도 가구가 바닐라로 돌아온다
3. **`Core/` 와 `AI/` 는 Verse · RimWorld · UnityEngine 을 모른다.** 그래서 테스트가
   림월드 없이 돈다. 테스트 프로젝트가 이 폴더들을 `<Compile Include>` 로 끌어간다
4. **`Framework/` 는 어떤 게임도 모른다. 게임끼리도 서로 모른다**
5. **플레이어의 게임 속도를 건드리지 않는다.** `TickManager.CurTimeSpeed` 를 읽지도 쓰지도
   않는다. 멈추는 것은 바닐라 `Window.forcePause` 로만 한다.
   **스페이스는 플레이어의 일시정지 키다** — 판을 진행시키는 키는 엔터다 (`Framework/UI/PRKeys.cs`)
6. **`packageId` 를 바꾸지 않는다.** 구독자 세이브가 모드를 못 찾는다
7. **클래스 이름을 함부로 바꾸지 않는다.** `Scribe_Deep` 이 세이브에 `Class=` 로 박는다.
   `*SaveData` 의 네임스페이스를 바꾸면 두던 판이 사라진다

---

## 3. 지금 상태

- 빌드 경고 0 · 오류 0
- 테스트 239 개 전부 통과
- `Tools/verify.py` — 번역 키 408 짝, 안 쓰는 키 0, Def 참조 성함
- `Tools/guistate.py` — GUI 전역 상태를 되돌리지 않는 메서드 0
- `Tools/package.py` — 통과 (36 파일 · 432 KB)
- 인게임 확인: 일곱 창 전부 열리고 돌아간다 (스크린샷으로 확인)

**창작마당에는 아직 안 올렸다.** `About/PublishedFileId.txt` 가 없다.

---

## 4. 다음에 할 일

1. **폴더 이름** (0절)
2. **창작마당 업로드** — 절차는 `Workshop/RELEASE.md` 에 전부 있다.
   첫 업로드 뒤 `About/PublishedFileId.txt` 를 저장소로 가져와 커밋하는 것을 잊지 말 것.
   그게 없으면 다음 업로드가 같은 항목을 갱신하지 못하고 새 항목을 만든다
3. **인게임 QA 남은 것** — 판을 저장하고 다시 여는 것, 무효화 조건(전투·청소·수리·손상·이동·만료),
   오디세이 소행성/궤도의 하늘과 지표 두 모드, 한국어·영어 양쪽 화면 읽기

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
Languages/       English · Korean. Keyed 408 키씩
Patches/         PatchOperationAdd 일곱 개. 이 파일이 모드의 진입점이다
Source/PlayableRecreation/
  Framework/     게임을 모르는 층. Dialog_MiniGame · MiniGameWorker · 저장 · 전적 · 숙련
  Framework/UI/  PRTextures(절차적) · PRContent(파일) · PRKeys · PRTheme
  Games/<이름>/  Core(Verse 0) · AI(Verse 0) · 나머지는 창 그리는 코드
Tests/PlayableRecreation.Tests/   239 개. net9.0 · xUnit. 림월드 불필요
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
