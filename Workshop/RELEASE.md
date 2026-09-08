# 배포

창작마당에 올리는 절차. 처음 한 번만 하는 것과, 낼 때마다 하는 것으로 나눈다.

---

## 왜 저장소를 그대로 올리면 안 되는가

림월드의 업로더는 **모드 폴더를 통째로** 올린다 (`SetItemContent(RootDir)`). 이 저장소가
곧 모드 폴더이므로, 링크된 채로 올리면 이런 것들이 따라 올라간다.

| | 용량 | 문제 |
|---|---|---|
| `.git/` | 3.3 MB | **커밋 기록이 통째로 공개된다.** 저자 이름을 바꾼 의미가 없어진다 |
| `Tests/` | 11 MB | 빌드 산출물. 게임은 쳐다보지도 않는다 |
| `Source/`, `Tools/`, `Workshop/` | 1.5 MB | 올릴 이유가 없다 |

그래서 올릴 것만 담은 폴더를 따로 굽는다. **614 KB, 61 개 파일** (1.1.0 기준).

```
python Tools/package.py            dist/Playable Recreation 에 굽는다
python Tools/package.py --install  굽고 나서 Mods 폴더로 복사한다
```

굽기 전에 이것들을 본다 — 여기서 걸리는 것은 전부 창작마당에서 걸릴 것들이다.

- `About.xml` 이 파싱되고 `name` · `author` · `packageId` · `modVersion` · `supportedVersions` 가 있는가
- `Assemblies/PlayableRecreation.dll` 이 있고, **어떤 `.cs` 보다도 새것인가** (빌드를 잊었는가)
- `Preview.png` 이 있고 1 MB 이하인가 (스팀의 한계)
- 창작마당 항목을 새로 만드는가, 기존 것을 갱신하는가

---

## 처음 한 번만

### 1. 개발용 심볼릭 링크를 없앤다

`Mods/RoyalGameOfUr` 는 이 저장소를 가리키던 링크다. 저장소 폴더 이름이 바뀌었으므로
이제는 끊어진 링크다. 그리고 링크는 애초에 배포에 못 쓴다 -
링크와 설치본이 동시에 있으면 **같은 packageId 가 둘**이 되어 림월드가 하나를 버린다.

```
rm "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/RoyalGameOfUr"
```

`--install` 은 이 링크가 남아 있으면 멈추고 알려 준다.

### 2. 구운 폴더를 설치한다

```
python Tools/package.py --install
```

`Mods/Playable Recreation` 에 들어간다. 앞으로 인게임 확인도 이쪽으로 한다 —
코드를 고쳤으면 `dotnet build -c Release` 다음에 이 한 줄이다.

### 3. 스팀 창작마당 약관에 동의한다

<https://steamcommunity.com/sharedfiles/workshoplegalagreement>

동의하지 않은 계정으로 올리면 항목은 만들어지지만 **본인 말고는 아무도 못 본다.**
림월드도 업로드 확인 창에서 이 주소를 알려 준다.

---

## 낼 때마다

### 1. 게이트를 통과시킨다

```
cd Source/PlayableRecreation && dotnet build -c Release   # 경고 0 개여야 한다
cd ../.. && dotnet test Tests/PlayableRecreation.Tests          # 전부 통과
python Tools/verify.py                                     # Def · 번역 키
python Tools/guistate.py                                   # GUI 전역 상태 복원
```

### 2. 판 번호를 올린다

`About/About.xml` 의 `<modVersion>`. 고친 것이 있으면 올린다.

### 3. 굽고 설치한다

```
python Tools/package.py --install
```

### 4. 림월드에서 올린다

1. 림월드 실행 → 메인 메뉴 → **Mods**
2. 목록에서 **Playable Recreation** 선택
3. 아래쪽 **Upload to Steam Workshop** — 이미 올린 뒤라면 **Update on Steam Workshop**
4. 확인 창이 약관을 알려 준다. 예

올라가면 `About/PublishedFileId.txt` 가 **설치본 쪽에** 생긴다.

### 5. 항목 번호를 저장소로 되가져온다 (첫 업로드 직후 딱 한 번)

이 파일이 "이 폴더는 창작마당의 저 항목이다"라는 유일한 표시다.
**잃어버리면 같은 항목을 갱신할 수 없고, 다음 업로드가 새 항목을 만든다.**

```
cp "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/Playable Recreation/About/PublishedFileId.txt" About/
git add About/PublishedFileId.txt && git commit -m "창작마당 항목 번호"
```

`--install` 은 설치본에 이 파일이 있고 저장소에 없으면 알아서 가져온다.
그래도 커밋은 직접 해야 한다.

### 6. 창작마당 페이지를 손본다

스팀 페이지에서 하는 일이다. 림월드가 해 주지 않는다.

- **설명** — 언어별로 나뉘어 있다. 스팀 페이지의 언어 탭마다 해당 파일을 통째로 붙여
  넣는다. BBCode 그대로 먹는다
  - 영어: `Workshop/description_en.txt`
  - 한국어: `Workshop/description_ko.txt`
- **스크린샷** — `Workshop/screenshots/` 가 원본이다. 캐러셀에 올린 뒤, 설명문에 남아 있는
  `{{...}}` 자리를 그 이미지의 업로드 URL(`[img]...[/img]`)로 바꾼다. 지금 남은 자리는
  `{{SS6_MOD}}` 하나 — `mod_collage.jpg` (다른 모드 가구 다섯 창을 한 판에 모은 것) 이다.
  모드 지원은 낱장으로 걸지 않는다. 다섯 장을 따로 걸면 "저 모드가 있어야 하나" 로 읽힌다
- **태그** — 림월드가 `supportedVersions` 에서 `1.6` 을 붙여 준다. `Mod` 같은 나머지는 직접
- **공개 범위** — 페이지에서 확인한다. 비공개로 올려서 확인한 뒤 공개로 돌리는 편이 안전하다
- **변경 기록** — 갱신할 때마다 무엇이 바뀌었는지 한 줄

---

## 절대 하지 말 것

**`packageId` 를 바꾸지 마라.** 구독자의 세이브가 모드를 못 찾는다. 지금 값은
`teamrostra.playablerecreation` 이고, 이건 이제 고정이다.

**`About/PublishedFileId.txt` 를 지우지 마라.** 같은 항목을 갱신할 길이 사라진다.

**`Assemblies/PlayableRecreation.dll` 을 `.gitignore` 에 넣지 마라.** 배포에 필요하다.
심볼 파일(`.pdb`)만 제외한다.

---

## 낼 준비가 되었는지

- [ ] 빌드 경고 0 개
- [ ] 테스트 전부 통과
- [ ] `verify.py` · `guistate.py` 통과
- [ ] 창을 실제로 열어 봤다 — 본체 일곱, 그리고 깔아 둔 모드가 있다면 그쪽 가구도
- [ ] 설명문에 `{{...}}` 자리가 남아 있지 않다 (전부 업로드 URL 로 바뀌었다)
- [ ] 판을 저장하고 다시 열어 봤다
- [ ] 한국어와 영어 양쪽으로 화면을 읽어 봤다
- [ ] `<modVersion>` 을 올렸다
- [ ] `python Tools/package.py` 가 통과한다
