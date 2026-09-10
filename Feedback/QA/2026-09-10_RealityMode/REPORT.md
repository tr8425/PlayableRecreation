# 몰입 모드 2칸 인게임 QA — 2026-09-10

**판정: 배포 보류.** 시작·중단·재개에서 기능 실패 5건을 재현했다. 설정 창의 원래 열 넘김 문제는 지상과 실제 궤도 맵에서 재현되지 않았다. 제품 코드와 DLL은 수정하지 않았다.

## 실행 조건과 증거의 범위

- RimWorld 1.6.4871 rev591, Windows, 영문 UI. 원본 `Compact of Veerehenuay.rws`에서 출발했다.
- 원본 세이브 SHA256: `4628EE4491EEF6F6390181C8B406566D514B0FF6BF1BA3A2E58F1E29C42694CC`.
- 저장소/설치 DLL 동일: 2026-09-10 16:56 빌드, 303104 bytes, SHA256 `DC45AF3BA07309E224E2442C9DD0EB420FF7F2AD8D24489C92111D9F998FE6E8`.
- 기본 프로필은 사용자가 재시작한 Harmony·Core/DLC·Playable Recreation·Playable Instrument·Character Editor·GloomyFurniture. 확장 검사 때만 VEF·VFE Core·Hospitality Casino를 추가하고 재시작했다.
- 테스트 식민자, 실제 방문객과 `LordJob_VisitColony`, 가구·의자를 임시로 생성했다. 별도 QA 세이브만 저장했다. 원본 식민자의 죽음이나 원본 세이브 덮어쓰기는 없었다.
- 임시 별도 `PRQAObserver.dll`을 통해 게임 메인 스레드에서 실제 Job·Window·세션을 관찰하고 UI와 같은 콜백을 실행했다. 원래 PR DLL은 바꾸지 않았다. 경로 이동과 Job 진행은 실제 게임 틱이다. 시간 배속·욕구·테스트 폰 생성은 QA 조작이다.
- 체스 완주는 실제 worker의 합법 수 커밋으로 `f3 e5 g4 Qh4#`를 진행했다. 양쪽 수를 QA가 지정했으므로 자연 AI 대국·사람의 수동 완주로 세지 않는다. 손님 초대는 제공된 개발자 `ForceInvite` 및 지정 가구 초대 콜백으로 확률을 건너뛰었다.
- 화면 크기 축소는 실제 Window의 높이를 제한해 렌더링한 검사다. 마우스로 가능한 최소 높이를 측정한 검사는 아니다. 스크린샷용 상대 메뉴는 마우스 이탈 자동 닫힘을 잠시 막고 위치를 고정했다.

## 재현된 실패

| ID | 등급 / 체크리스트 | 관찰과 재현 | 근거 |
|---|---|---|---|
| QA-01 | A / §2 마주 보기 | 의자를 둔 체스판에서 Rostra와 방문객이 **같은 칸 `(152,0,121)`, 같은 방향 `rot=3`**으로 겹친 채 대국 시작. 각각의 `Touch` 도착만 확인하고 서로 다른 자리를 확보하지 않는다. | [화면](screenshots/chess-overlap.png), `runtime/MatchState-*.result.txt` |
| QA-02 | A / §2-1·§7 이어 두기 | 2인 판 중 상대 징집 → 창은 닫히고 판은 남는다. 주도 폰이 판 옆에 있을 때 가구의 실제 **Pick up where you left off** 메뉴 실행 → 징집 상대인데 즉시 창이 열리고 다시 닫힌다. | [로그](runtime/resume-exact.txt). `GameEntry.OpenSession`의 근거리 `Resume` 경로가 상대 재호출을 건너뛴다. |
| QA-03 | A / §5 혼자 놀기 전환 | 방문객은 5000틱 대기 후 `Play_Chess`를 받지만 곧 `Wait_Wander/GotoWander`로 떠난다. 의자 2개를 붙인 새 판에서도 재현. `needs.joy == null`이며 바닐라 `JoyUtility.JoyTickCheckEnd`가 일반 여가 Job을 `InterruptForced`로 끝낸다. | [2시간 관찰](runtime/guest-wait.txt), [의자 재현](runtime/guest-chair-repro.txt). `TogetherInvite.PlayAlone`의 “여가 욕구가 없어도 Job 자체는 돈다” 가정이 틀림. |
| QA-04 | B / §2-3 펀칭백 | 상대 목록은 나오지만 정원 1 때문에 두 번째 예약이 거절된다. 한 폰은 `PR_GoToGame`, 다른 폰은 일반 작업. 창 없이 2500틱 대기 후 취소. 새 건강한 두 폰에서도 `CanReserve=True,False`. | [독립 재현](runtime/punch-clean.txt), [시간초과](runtime/punch-together.txt). 단순 1×1 크기보다 **정원 1**이 원인. 다트·아케이드도 정원 1임을 확인했으나 해당 가구의 2인 재현은 별도 필요. |
| QA-05 | A / §7 연습 다시 두기 | 연습 체스 체크메이트 뒤 실제 **New game** 콜백 → 둘 다 기존 PR Job을 유지한 채 pending 2500틱 후 취소. 새 건강한 두 폰, 강제 대화 없이 재현. | [로그](runtime/practice-clean.txt). 바닐라 `TryTakeOrderedJob`은 현재와 같은 Job이면 교체 없이 true를 반환한다. 기존 Hold에서 도착 통지가 다시 발생하지 않는 흐름과 부합한다. |

예약 수를 일괄 2로 바꾸면 이미 통과한 포커·편자·후프스톤의 바닐라 공존이 깨질 수 있다. QA-04는 게임별 2인 허용 여부 또는 예약 구조를 정하고 재검사해야 한다.

## 추가 관찰과 명세 차이

- **종료 직후 자동 해제 불일치 (§2, A 기준):** 체크메이트 후 180틱이 지나도 결과 창과 두 PR Job이 유지된다. 사용자가 창을 닫으면 120틱 뒤 둘 다 일반 작업으로 돌아간다. [화면](screenshots/finished-still-held.png), [로그](runtime/finish-release.txt). 현재 UI는 New game 버튼이 있는 결과 화면이므로, 자동 닫힘 명세를 유지할지 결과 표시와 폰 해제를 분리할지 결정이 필요하다.
- **기록 창의 게임 탭 잘림 (B):** 확장 포함 승부 게임 10개에서 기본 창 크기인데 `Arcade machine` 등의 글자가 여러 줄로 감기고 윗부분이 잘린다. 기록 본문은 보인다. [화면](screenshots/leaderboard-tabs.png).
- **긴 대화 잘림 (B):** 실제 바닐라 모욕을 강제로 발생시켜 띠에 수집되는 것은 확인했다. 긴 문장 뒷부분은 한 줄 높이에서 잘려 보인다. [화면](screenshots/talk-insult.png), [전체 문장](runtime/final-runtime.txt). Abrasive의 자연 발생률을 통과 처리한 것은 아니다.
- **대화 빈도 기준 재검토:** 같은 팩션 대국을 1배속 701틱(게임 시간 약 16.8분) 관찰했으나 자연 대화 0건. 설치 게임의 Normal 상호작용 MTB는 6600틱이다. 체크리스트의 “게임 시간 약 1분에 한 줄” 기대값과 맞지 않는다. 장시간 빈도·가독성은 미확인.
- **상대 사망 반응 시간:** 창 닫힘 37틱, 판 제거와 `PR_OpponentGone` 생각 확인 75틱. 제거·생각 기능은 동작하지만 엄격한 60틱 이내 기준은 이번 표본에서 넘었다. UI 폴링과 세션 청소 시점 차이가 있는지 재검토한다.

## 체크리스트별 결과

`부분`은 실제 확인한 범위만 통과이며 전체 항목 통과를 뜻하지 않는다.

| 절 | 결과 | 확인 범위 / 남은 점 |
|---|---|---|
| 0 준비 | PASS | 설치 DLL 일치, 개발자 모드, 별도 QA 가구·식민자·배경·방문객 준비 |
| 1 설정 | 부분 PASS | 지상 끝까지 스크롤·재열기·작은 높이에서 마지막 Stargazing까지 표시. 실제 Orbit 맵에서도 작은 창 재열기 확인. 방문객·성격 하위 줄 숨김 확인. 확장 포함 끝까지 표시. 기록 창 탭은 위 B 이슈. 확장별 모든 옵션 조작·최소 높이 전체 조합은 미확인 |
| 2 기본 | 부분 / FAIL | 의견순 및 회색 부적격 후순위·이유, 두 폰 이동, 양쪽 도착 후 열림, 상대 이름·Job 보고 문구 PASS. 겹침 QA-01. 결과 자동 닫힘 명세 불일치. 완주마다 전적 +1 확인 |
| 2-1 탈출 | 부분 / FAIL | 상대 배고픔 5% → 37틱 뒤 식사, 창 닫힘·판 보존 PASS. 징집 중단도 PASS, 재개는 QA-02. 사망 판 제거·생각 PASS(75틱). 혼자 판은 폰 `DeSpawn` 후 229틱에도 유지. 실제 캐러밴 편성부터 출발까지는 미확인 |
| 2-2 예약 | PASS(대상 범위) | 포커 4인·편자/후프스톤 3인: 2인 PR 대국 중 제3자의 실제 vanilla JoyGiver가 같은 가구 Job을 만들고 180틱 진행. 망원경/슬롯은 `hasMatch=false`, `Together.AppliesTo=false`; 2인 대상으로 분기하지 않음 |
| 2-3 1×1 | FAIL | QA-04. 펀칭백 두 번째 폰 예약 실패 |
| 3 대화 | 부분 | 식민자 1배속 약 17분 자연 대화 0건. 실제 바닐라 Insult 강제 발생 → 띠 수집·표시 PASS, 긴 문장 잘림. 방문객 대국 띠 높이 0 PASS. Abrasive 자연 관찰·장시간 빈도는 미확인 |
| 4 방문객 결과 | 부분 PASS | 후보 목록·완주 우호도 +4, 같은 방문의 압승 결과로 적용값 -6 교체, 방문객 기분/사회 기억 확인. 낮은 tier 런타임 계산 표본 1/1/-1. 방문 팩션 전원 맵 이탈 후 캐시 청소로 적용값 0 확인. 낮은 난이도 실제 완주·자연 다음 무리 도착은 미확인 |
| 5 방문객 대기 | 부분 / FAIL | 실제 VisitColony/Defend Lord 아래 약 5000틱 대기 유지, 공지·Job 보고·메뉴 맨 위 waiting·선택 후 대국 PASS. 이후 혼자 놀기는 QA-03. 후보 수집에서 중복 대기 손님·대기/식민자 예약 가구·ExitMapRandom 손님 제외 확인. 초대 OFF·빈도 0 거절 PASS. 저장/로드 후 기다림 해제 PASS. 자연 확률 초대·다른 Lord 종류는 미확인 |
| 6 배경 친화 | 부분 PASS | 런타임 체스 마스터/블랙잭 플레이어 +6, 동일 스킬 5에서 tier 1→2. 다른 게임에는 태그 없음. 아마추어 천문학자 +6, 망원경은 난이도 1개라 tier 0 유지. 배경별 모든 메뉴 화면·연동 OFF 선택 창은 완전 재검사하지 않음 |
| 7 보강 회귀 | 부분 / FAIL | 저장/불러오기 후 pending/live/waiting 없음, 세션 폰이 새 맵의 현재 객체 참조 PASS. 둘 다 도중 작업 취소 후 148틱 표본에서 자격 None/None 및 pending 없음. 60틱 정확 경계는 미계측. 연습 재시작 QA-05, 못 오는 상대 재개 QA-02. 서로 다른 원본 세이브를 메인메뉴로 교차 로드하는 흐름은 미확인 |
| 8 혼자 회귀 | 부분 PASS | 기본 7 + 확장 5 **총 12개 진입점**: 2칸 OFF로 실제 창·올바른 worker·상대 null 확인, 가능한 동작 1회와 닫기. 슬롯/펀칭백은 해당 저장 지원에 따라 세션 없음. 체스 전적·완주·저장 로드 확인. 모든 게임 완주·무르기·기권·재시도·숙련도 전 조합과 과거 버전 UI 완전 동일성은 미확인 |
| 9 기록 | 완료 | 본 보고서, 피드백 백로그, 설계 §20.5, 전체 Player 로그 보관 |

## 화면과 로그

- [지상 설정](screenshots/settings-ground.png), [작은 설정 창](screenshots/settings-small.png), [궤도 맵 설정](screenshots/settings-orbit.png), [확장 포함 마지막 구획](screenshots/settings-extensions.png).
- [방문객 대기 상대 목록](screenshots/guest-waiting-menu.png), [대국 머리글](screenshots/chess-arrived.png).
- `runtime/`는 실제 관찰 기록이다. `diagnostics/`의 QA 콜백 소스는 재현 근거이며 게임에 설치하는 제품 소스가 아니다.
- `Player.phase1-complete.log`, `Player.extensions-complete.log` 등은 전체 로그다. `Player.initial.log`는 사용자가 재시작하기 전 자료이고, `phase1`/`phase1-prev`도 종료·재시작 경계 확인용으로 보관한다.
- **붉은 로그 0건으로 통과 처리하지 않음.** 시작 때 `GradientHairMaskDef` 누락, 영어 번역 데이터 145 오류 및 다른 설치 모드의 메타데이터 경고가 이미 있다. 확장 프로필로 QA 세이브를 읽을 때 `Could not find think node with key ...`도 나타났다. 이번 PR 플레이에서 별도의 PR 예외/예약 오류 스택은 관찰하지 못했다. Mono fallback 로그 일부는 QA 명령 DLL의 메모리 로드에 수반된다.
- QA helper 컴파일/호출 실패는 제품 결함으로 세지 않았다. 콜백 내부에서 포착된 실패 결과 파일과 성공 재현은 구별해야 한다.

## 복원

**복원 완료.** [최종 해시와 확인 기록](restoration.txt). 원래 모드 목록(사용자 재시작 이후), PR 설정, Prefs, 개인 전적을 백업에서 복원했다. 관찰 DLL을 제거하고 재시작했다. QA 세이브는 사용자 Saves 밖 임시 보관으로 옮겼고, 원본 세이브를 다시 열어 일시정지했다. 원본 파일의 SHA256은 시작 때와 동일하다. 마지막 기본 프로필 로그도 `Player.restored-baseline.log`에 보관했다.

복원 직후 설정 3개와 전적 파일 모두 백업과 바이트 단위로 일치했다. **원본 세이브를 마지막으로 다시 열어 시간이 흐르는 동안**, 제품의 기존 판 무효화가 체스 전적의 `voided`를 9→10으로 한 번 갱신했다. 최종 비교에서 그 한 필드 외 승패·기권·숙련도 등 전적은 백업과 동일하다. QA 대국의 승패 결과는 남아 있지 않다. 실행 중인 전적 캐시와 파일을 어긋나게 만들지 않기 위해 이 정상 실행 결과를 파일만 덮어 지우지는 않았다.
