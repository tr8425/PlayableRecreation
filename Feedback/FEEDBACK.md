# 유저 피드백 대장

> 창작마당(id 3794530553) 등에서 들어온 피드백을 한 곳에서 추적한다.
> 이 폴더는 git 에는 올라가지만 **배포에는 포함되지 않는다** — Tools/package.py 가
> 화이트리스트 방식이라 목록에 없는 폴더는 굽지 않는다.

## 규칙 (codex · claude 공용)

- 새 피드백은 "항목" 절 **맨 위**(최신순)에 추가한다. 항목 삭제는 하지 않는다.
- 상태 값: `신규` `답변완료` `수정배포` `백로그` `관찰중` `기각`
- 조치(커밋·답글·백로그 반영)가 생기면 해당 항목을 갱신하고 상태를 바꾼다.
- 피드백에서 작업이 파생되면 "백로그" 표에도 반영한다.
- 세션을 마치기 전, 대응 안 된 `신규` 항목이 남았는지 확인한다.

## 백로그 (피드백에서 파생된 작업)

| 우선순위 | 작업 | 근거 | 상태 |
|---|---|---|---|
| 1 | 모드 가구 지원 1차 — VFE 다트(5라운드×3다트 합계전) + Gloomy 체스·포커·당구 패치 (**본체 내장**, 소프트 의존) | galesdeloscien 09-05 | **구현 완료** (2026-09-07) — 인게임 QA 후 목요일 저녁 v1.1.0 재업로드 |
| 2 | 별 보기 단독판 (package.py --variant 분리, 완전 포크 지양) | HeroCooky 09-05 | 요청 1건 — 2~3건 쌓이면 착수 |
| 3 | 모드 가구 지원 2차 — 펀칭백(VFE, 리듬 콜앤리스폰스)·슬롯(Casino, hasMatch=false) | VFE·Casino 조사 파생 | **구현 완료** (2026-09-07) — 릴리스 묶음은 QA 결과 보고 1차와 같이 낼지 결정 |
| 4 | 룰렛 (VFE, 하우스전 뱅크롤 런) | VFE 조사 파생 | **구현 완료** (2026-09-07) — 목표 배수 ×1.5~×5, 테이블 리밋 10. 인게임 QA 대기 |
| 5 | 아케이드 머신 게임 (VFE Joy_Arcade) | VFE 조사 파생 | 보류 — 테트리스는 상표 리스크로 제외, 브레이크아웃형 유력. 림 캐릭터 격투게임 안은 아이디어 보관 |

## 항목

### 2026-09-05 · galesdeloscien · 창작마당 댓글 — `답변완료` `백로그`
> "oh man, pls make a vfe integration plss!!!! this mod is so pawsome fr"
- 판단: VFE(Vanilla Furniture Expanded) 가구 연동 요청. 조사 결과 VFE 엔 기존 7게임을
  얹을 가구가 없고, 게임성 가구(다트판·룰렛 테이블·아케이드)는 새 게임을 요구한다.
- 조치: 답글 완료. 별도 애드온 모드 + 다트 우선 계획 수립. VE 계열 12개 모듈 스캔 —
  우리가 패치하는 바닐라 가구 7종 def 를 건드리는 패치 없음(공존 무결) 확인.

### 2026-09-05 · Amor · 창작마당 댓글 — `답변완료`
> "FINALLY more places to play Game of Ur"
- 칭찬. 답글 완료 ("Yeaaaah, enjoy playing Ur").

### 2026-09-05 · HeroCooky · 창작마당 댓글 — `답변완료` `백로그`
> "is there any way to make the stargazing one a stand-alone? 'Cause that is the best feature here, ngl."
- 판단: 별 보기가 차별화 포인트라는 두 번째 신호. 단독 모드 요청 1건째.
- 조치: "설정에서 6게임을 끄면 사실상 단독판" 안내 + 단독판은 기획 검토 약속으로 답글.

### 2026-09-03 · Stegosaurus TTV · 창작마당 댓글 — `답변완료`
> "This probably goes perfect with shift perspective"
- 판단: Perspective Shift(WASD 빙의 모드) 조합 추천 = 몰입 플레이층에 소구한다는 신호.
- 조치: immersion mode 설정 안내로 답글 완료.

### 2026-09-03 · LifeIsAbxtch · 창작마당 댓글 — `관찰중`
- 자동 콘텐츠 검사로 숨김 처리. 본문은 작성자 외 열람 불가(페이지 소스에도 없음).
- 조치: 대기. 2026-09-10 까지 그대로면 삭제하고 이 항목을 `기각` 으로 바꾼다.

### 2026-09-03 · ferny · 창작마당 댓글 — `수정배포`
> "Game names are not capitalized and the tutorials are showing after the gameplay and not before. Cool mod so far though"
- 판단: ① 이름 대문자 — 문두·단독 위치는 LabelCap 이 맞다(문장 중간은 소문자 유지)
  ② 튜토리얼 — PreOpen 에서 스택에 넣어 게임 창 밑에 깔리던 창 순서 버그.
- 조치: 92b6d55 로 수정, 2026-09-03 재업로드, 답글 완료.
