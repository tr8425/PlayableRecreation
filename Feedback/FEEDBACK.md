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

### 답글 어투

작성자(Rostra)가 쓰는 투를 따른다. `@이름` 으로 시작하고 그 뒤는 소문자로,
두세 줄 안에 끝낸다. 목록·굵은 글씨·판매 문구는 쓰지 않는다.

디테일은 적당히. 무엇이 열리는지까지는 말하되 게임마다 규칙을 늘어놓지 않는다 —
읽는 사람이 궁금하면 설명문을 본다.

**칭찬 답글에 업데이트 홍보를 얹지 않는다.** 특히 이번처럼 남의 모드가 있어야
쓸 수 있는 기능이면 더 그렇다. 그 모드를 안 쓰는 사람에게는 알릴 값어치가 없고,
고맙다는 말에 광고를 붙인 꼴이 된다. 새 소식은 변경 기록과 설명문이 맡는다.
업데이트를 언급할 자리는 그것을 요청한 사람의 댓글이다 (galesdeloscien 처럼).

> @ferny fixed both in the latest update. turns out the tutorial was opening
> behind the game window the whole time. thanks for the report!

## 백로그 (피드백에서 파생된 작업)

| 우선순위 | 작업 | 근거 | 상태 |
|---|---|---|---|
| 1 | 모드 가구 지원 1차 — VFE 다트(5라운드×3다트 합계전) + Gloomy 체스·포커·당구 패치 (**본체 내장**, 소프트 의존) | galesdeloscien 09-05 | **QA 통과** (2026-09-08) — 다트 인게임 확인, 큰 문제 없음. v1.1.0 묶음으로 재업로드 대기 |
| 2 | 별 보기 단독판 (package.py --variant 분리, 완전 포크 지양) | HeroCooky 09-05 | 요청 1건 — 2~3건 쌓이면 착수 |
| 3 | 모드 가구 지원 2차 — 펀칭백(VFE, 리듬 콜앤리스폰스)·슬롯(Casino, hasMatch=false) | VFE·Casino 조사 파생 | **QA 통과** (2026-09-08) — 1차와 함께 v1.1.0 으로 낸다 |
| 4 | 룰렛 (VFE, 하우스전 뱅크롤 런) | VFE 조사 파생 | **QA 통과** (2026-09-08) — 목표 배수 ×1.5~×5, 테이블 리밋 10 |
| 5 | 아케이드 머신 게임 (VFE Joy_Arcade) | VFE 조사 파생 | **QA 통과** (2026-09-08) — 새 게임 대신 **무작위 추첨함**으로. 난이도만 고르면 기계가 승부 게임 하나를 뽑는다. 테트리스(상표)·격투게임(규모) 안은 폐기 |
| 6 | **대결 상대를 식민자 중에서 고르기** — 난이도는 고른 폰의 능력치에서 뽑는다 | ferny 09-09 | 신규 — 배관 절반은 이미 있다 (`GameEntry.TierForPawn` 이 linkedSkill+열정을 티어로 바꾼다). 남는 것은 상대 폰을 고르는 자리, 헤더 표기(`PR.Header.Opponent` 가 지금은 난이도 이름만 찍는다), 상대 폰을 어디까지 붙잡아 둘지 |

## 항목

### 2026-09-09 · LifeIsAbxtch · 창작마당 댓글 — `신규`
> "Rostra you should listen to Ferny he's the professional"
- 09-03 에 자동 검사로 숨겨진 댓글을 남긴 그 계정이다 (아래 `관찰중` 항목). 프로필은
  친구 3명·배지 3개에 게임 목록도 비어 있다 — 활동이 거의 없는 계정이다.
- 적대적이지 않다. ferny 편을 드는 농담이고, 게다가 맞는 말이다.
- 답글 문안 (달지 않고 넘어가도 된다):
  > @LifeIsAbxtch ha, no argument there

### 2026-09-09 · ferny · 창작마당 댓글 — `신규` `백로그`
> "I really wish you actually had to select one of the other colonists to be your
> opponent. It feels currently like it sorta reverses the immersion it brings to
> the table by having you play against invisible AIs and not actual people in
> your colony"
- **누구인가**: 프로필 소개가 "progression lead, vanilla expanded writer" 다. 우리가
  이번 판에 지원을 붙인 바로 그 Vanilla Expanded 쪽 사람이고, 창작마당 항목 115개에
  림월드 모딩 디스코드를 프로필에 걸어 두었다. 09-03 대문자·튜토리얼 순서 버그를
  잡아 준 것도 이 사람이다 — 두 번째 피드백이고, 이번엔 버그가 아니라 설계 지적이다.
- 판단: **멀티플레이 이야기가 아니다.** "people in your colony" 는 식민자를 가리킨다.
  상대가 이름 없는 AI 라서, 이 모드가 만들어 낸 몰입을 스스로 깎는다는 말이다.
  맞는 지적이다 — 지금 헤더는 상대 자리에 난이도 이름만 찍는다.
- 실현 가능성: 난이도를 능력치에서 뽑는 배관은 이미 있다 (`GameEntry.TierForPawn`).
  새로 만들 것은 상대 폰을 고르는 자리와 헤더 표기, 그리고 상대 폰을 어디까지
  붙잡아 둘 것인가(예약·이동)다. 백로그 6번.
- 답글 문안 (게시하면 `답변완료` 로 바꾼다):
  > @ferny yeah, fair hit. having to pick an actual colonist to sit across from
  > you is the version I want too, and the difficulty could come straight off
  > their stats (intellectual for the board games, shooting for the throwing
  > ones) instead of a menu - half that plumbing is already in there. going on
  > the list.
  >
  > if you meant wider than the colony: another faction I'd look at, real-time
  > multiplayer I don't see happening (leaderboard-ish, maybe).

### 2026-09-08 · Bones · 창작마당 댓글 — `답변완료`
> "This is a true gem."
- 칭찬. 답글은 ContourJeans64 와 한 댓글로 묶어서 달았다 (아래).

### 2026-09-05 · ContourJeans64 · 창작마당 댓글 — `답변완료`
> "I really enjoyed this mod,"
- 칭찬. 09-05 에 달렸는데 대장에 빠져 있던 것을 지금 채운다.
- **Bones 와 한 댓글로 묶었다.** 둘 다 질문 없는 칭찬이라 거의 같은 인사를 두 번
  연달아 다는 꼴이 된다.
- 초안에 붙였던 "업데이트 나왔다" 한 줄은 빼고 감사만 하고 끝냈다. 판단 근거는
  아래 어투 규칙에 적어 두었다.
- 조치: 2026-09-08 답글 완료. 본인이 "YAY" 로 답했다 — 요청한 사람에게 닿았다.

### 2026-09-08 · 개발자 QA (2차) · 인게임 — `수정배포`
> "사진도 찍어왔고, 잠깐 플레이에서는 큰 버그를 찾지 못했어"
> "gloomy, 모드없음 둘 다 정상작동한다"
- 확인한 것: VFE 다트·펀칭백·룰렛·아케이드, Casino 슬롯, 궤도 지표 줌,
  Gloomy 가구 3종 진입, 그리고 **세 모드를 전부 끈 판**. 전부 정상 동작.
  소프트 의존 경로가 양쪽(켠 판·끈 판)에서 확인됐으므로 v1.1.0 QA 는 닫혔다.
- 스크린샷에서 하나 잡혔다 — 아무것도 당기지 않은 슬롯머신의 첫 얼굴이 7-7-7 이었다.
  릴 배열의 기본값이 0-0-0 이라 잭팟 줄이 걸린 채로 문이 열렸다. BAR·금·딸기로 세워 두었다.
- 창작마당 캐러셀에는 모드 지원 다섯 창을 낱장으로 걸지 않고 `mod_collage.jpg` 한 장으로
  묶었다 (`Tools/make_mod_collage.py`). 낱장이면 "저 모드가 있어야 하나" 로 읽힌다.

### 2026-09-05 · galesdeloscien · 창작마당 댓글 — `수정배포` `백로그`
> "oh man, pls make a vfe integration plss!!!! this mod is so pawsome fr,
> here's a nugget :nuggetofrogue:"
- 판단: VFE(Vanilla Furniture Expanded) 가구 연동 요청. 조사 결과 VFE 엔 기존 7게임을
  얹을 가구가 없고, 게임성 가구(다트판·룰렛 테이블·아케이드)는 새 게임을 요구한다.
- 1차 조치: 답글 완료. 별도 애드온 모드 + 다트 우선 계획 수립. VE 계열 12개 모듈 스캔 —
  우리가 패치하는 바닐라 가구 7종 def 를 건드리는 패치 없음(공존 무결) 확인.
- **2026-09-08 v1.1.0 으로 배포됨.** 애드온이 아니라 본체 내장 + 소프트 의존으로 갔다
  (VFE 다트·펀칭백·룰렛·아케이드, Gloomy 3종, Casino 슬롯).
  이 사람이 이번 판의 직접적인 계기다 — 배포 답글을 따로 단다.
- 조치: 2026-09-08 답글 완료.
  > @galesdeloscien done, it's in the update that just went up. with VFE loaded
  > the darts board, punching bag, roulette table and arcade machine all open now
  > (the arcade has no game of its own - you pick a difficulty and it rolls you a
  > random one). GloomyFurniture and Hospitality: Casino got a few too.
  >
  > thanks for the nudge, and for the nugget

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
- 2026-09-09 현재도 그대로 숨겨져 있다. 같은 계정이 그날 멀쩡한 댓글을 새로 달았으니
  차단당한 계정은 아니고, 저 한 줄이 검사에 걸린 것으로 보인다.
- 조치: 대기. 2026-09-10 까지 그대로면 삭제하고 이 항목을 `기각` 으로 바꾼다.

### 2026-09-03 · ferny · 창작마당 댓글 — `수정배포`
> "Game names are not capitalized and the tutorials are showing after the gameplay and not before. Cool mod so far though"
- 판단: ① 이름 대문자 — 문두·단독 위치는 LabelCap 이 맞다(문장 중간은 소문자 유지)
  ② 튜토리얼 — PreOpen 에서 스택에 넣어 게임 창 밑에 깔리던 창 순서 버그.
- 조치: 92b6d55 로 수정, 2026-09-03 재업로드, 답글 완료.
