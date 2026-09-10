# 몰입 모드 추가 QA — 2026-09-10 R2

**판정: 기존 기능 결함 QA-01~05는 이번 재현 조건에서 모두 통과. 기록 창 하단 잘림 1건(B)은 남아 있다.** 결과 화면의 10초 해제·호버 연장과 긴 대화 툴팁도 확인했다. 제품 코드와 DLL은 수정하지 않았다. 전체 체크리스트의 미확인 항목까지 통과한 것으로 보지는 않는다.

## 대상과 실행 조건

- 요청된 [Claude artifact](https://claude.ai/code/artifact/d8d40a29-2a1d-4ebd-a2a9-bd4bce739ea4)는 실제 브라우저에서도 **Page not found**였다. HTML 셸은 받았으나 `/api/frame/…`는 403이었다. 본문을 읽었다고 간주하지 않고, 저장소의 수정 커밋 `172e7d1`·`9cd280e`와 갱신된 `QA_RealityMode.md`를 기준으로 재검사했다. [브라우저 상태](artifact-access/page-2026-09-10T10-10-58-543Z.yml), [콘솔](artifact-access/console-2026-09-10T10-10-58-142Z.log).
- RimWorld 1.6.4871 rev591, Windows, 영문 UI, 2560×1440, UI 배율 1.5. 대상 HEAD `9cd280e8b4c2d6c05e029cc95473d9080ebb319c`.
- 저장소와 설치 DLL: 305152 bytes, SHA256 `D0DAFFC9122306D4B6631925CF2B1185FF4835AC6CDB0D05A8264149007E746F`. 런타임 모듈 ID `cfd028bd-667e-40a5-8dee-a02aad746696`.
- 원본은 `Compact of Veerehenuay.rws`. SHA256 `4628EE4491EEF6F6390181C8B406566D514B0FF6BF1BA3A2E58F1E29C42694CC`. 원본에서 파생된 R1 QA 사본을 `PR_QA_R2_20260910.rws`로 분리해 사용했다. 새로운 건강한 테스트 폰, 방문객, 통행 가능한 곳의 가구·의자를 생성했으며 원본을 덮어쓰지 않았다.
- 사용자의 기본 모드 프로필에 이미 설치된 VEF → VFE Core → Hospitality Casino를 이 순서로 PR 앞에 임시 추가했다. 유효 QA 프로세스는 19:15경 시작한 PID 37512다. 처음 역순으로 넣었던 프로필은 아래의 제외 자료로 분리했다.
- 별도 임시 `PRQAObserver.dll`에서 실제 게임 메인 스레드의 Job·세션·Window를 읽고 UI와 같은 콜백을 실행했다. 이동·예약·대기는 실제 게임 틱이다. 폰 생성, 위치·욕구 준비, 배속은 QA 조작이며, 일반적인 수동 플레이 전체를 대신하는 증거로 세지 않는다.
- 체스 종료는 실제 worker에 합법 수 `f3 e5 g4 Qh4#`를 커밋했다. 양쪽 수를 지정했으므로 자연 AI 대국 결과는 아니다. 손님 초대 시작도 지정 가구 초대 콜백으로 확률을 건너뛰었으나, 대기와 혼자 놀기 만료는 강제 만료 없이 약 10400틱을 흘렸다.

## 기존 5건 재검사

| ID | 결과 | 실제 관찰과 근거 |
|---|---|---|
| QA-01 / A | PASS | 의자가 있는 새 체스판에 같은 쪽에서 접근한 식민자 두 명, 이어서 식민자+방문객 조합을 확인했다. 안정화 후 각각 `(131,0,80)`·`(130,0,81)`의 서로 다른 DiningChair와 `targetC`를 유지했다. 방향은 3·1. [식민자 로그](runtime/seat-settled.txt), [방문객 로그](runtime/guest-pair-r2.txt), [화면](screenshots/guest-distinct-seats.png). 서로 반대편 변에 앉는다는 뜻은 아니며, 이번 표본에서는 인접한 두 변에 앉았다. |
| QA-02 / A | PASS | 상대 징집 후 37틱에 창이 닫히고 판이 남았다. 실제 가구 재개 메뉴·기즈모·걸어가서 열기 모두 징집 상대에게 창을 열지 않았다. 징집 해제 후 같은 가구 메뉴로 재개하면 두 폰을 다시 부르고 589틱 후 대국이 열렸다. [메뉴 재현](runtime/resume-exact.txt), [경로별 재현](runtime/resume-paths.txt). |
| QA-03 / A | PASS | `joy=null`인 새 Villager와 실제 `LordJob_VisitColony`에서 관찰했다. 약 2시간 대기 → 같은 `PR_InviteGame`과 의자에서 약 2시간 혼자 놀기 → 대기 상태 해제와 일반 배회로 복귀했다. `Defend` 지시가 유지되는 동안에도 혼자 놀기 보고 문구와 자리를 지켰다. [시간 경과](runtime/guest-r2.txt), [만료 뒤 상태](runtime/GuestViewR2-Command81f59e53.result.txt). |
| QA-04 / B | PASS | 2칸 ON·스킬 연동 OFF에서 펀칭백·다트·아케이드의 실제 가구 메뉴는 상대 목록 없이 난이도 창으로 갔다. 난이도 선택 콜백 뒤 실제 게임 창·worker·상대 null을 확인했다. 체스·포커·편자·후프스톤·우르·당구는 계속 상대 목록이 나온다. [분기 로그](runtime/menu-gate-clear-r2.txt), [게임 시작 로그](runtime/single-workers-r2.txt). 이 3개 게임의 완주는 이번 검사 범위가 아니다. |
| QA-05 / A | PASS | 새 건강한 두 폰의 연습 체크메이트 후 실제 `StartAnotherMatch` 콜백으로 즉시 같은 상대와 새 판이 열렸다. 양쪽 PR Job도 유지됐다. 결과 화면에서 자동 해제된 뒤 New game으로 다시 부르는 경로도 통과했다. [즉시 재시작](runtime/practice-clean.txt), [해제 뒤 재시작](runtime/idle-after-hover-r2.txt). |

자리 검사에서 창은 두 폰이 가구에 닿은 뒤 열렸고, 이후 예약 의자에 정착했다. **창이 열리는 첫 프레임부터 두 폰이 최종 의자 칸에 있다는 보장은 확인하지 않았다.** 빈 인접 자리나 의자가 없는 상황의 Touch 폴백은 별도 미확인이다.

## 추가 동작과 회귀

| 항목 | 결과와 증거 |
|---|---|
| 결과 화면의 10초 해제 | 호버가 확인되지 않은 대조 표본에서 실시간 10.06초에 held가 해제됐고 결과 창은 남았다. [로그](runtime/idle-result-r2.txt). |
| 결과 창 호버 연장 | 실제 OS 마우스를 창 안에 두었을 때 `Application.isFocused=True`, UI 포인터 좌표와 함께 남은 시간이 약 10초로 반복 갱신됐다. 약 27초 동안 폰이 유지됐다. 마우스를 밖으로 옮긴 뒤에는 일반 작업으로 돌아갔고 결과 창이 남았다. [호버 로그](runtime/idle-hover-r2.txt), [후속 상태](runtime/idle-after-hover-r2.txt). 마우스 이탈부터 해제까지의 정확한 10초는 이 후속 표본에서 별도로 계측하지 않았다. |
| 예약 공존 | 포커 정원 4, 편자·후프스톤 정원 3에서 두 PR 참가자가 있는 같은 가구에 실제 바닐라 JoyGiver로 제3자 Job을 부여했다. 180틱 뒤에도 각각 Play_Poker·Play_Horseshoes·Play_Hoopstone을 유지했다. [로그](runtime/reservation-suite.txt). |
| 자연 대화 | Abrasive 상대와 약 10037틱, 게임 시간 약 4시간 동안 강제 상호작용 없이 5건을 수집했다. 모두 중립적인 대화였다. 관찰을 지속하기 위해 판 뒤집기는 껐다. [로그](runtime/talk-r2.txt). 이는 자연 발생 확인 표본이며 Abrasive의 부정적 대화 확률·평균 빈도 검증은 아니다. |
| 긴 문장과 툴팁 | 위 자연 대화 중 긴 문장이 띠 안에서 말줄임 처리됐고 실제 마우스 호버로 전문 툴팁이 나타났다. 5건 저장 중 띠에는 4줄 표시, 높이 104였다. [띠 화면](screenshots/natural-talk-truncated.png), [툴팁 화면](screenshots/natural-talk-tooltip.png), [상태](runtime/TalkViewR2-Commandc5338846.result.txt). |
| 기록 탭 | 확장 포함 12개 Def 중 승부 게임 10개의 탭이 기본 창에서 두 줄로 나뉘고 이름이 읽힌다. 다만 하단 요약은 아래 R2-01에 해당한다. |

## 새로 확인한 문제 R2-01 — 기록 창 하단 잘림 (B)

**기본 크기의 기록 창에서 ‘플레이 시간·무르기 횟수’가 표시되지 않는다.** 데이터는 남아 있고 표시 영역이 부족하다.

1. VFE·Casino 포함 승부 게임 10개를 활성화한다. 영문 UI, UI 배율 1.5에서 Records를 연다.
2. 기본 크기 `620×520`에서 Chess → All time을 본다. 탭 두 줄과 난이도 다섯 줄, 요약 중 `Shutouts …, best streak …`까지는 보인다.
3. 마지막 `Time played …, undos …` 줄은 보이지 않는다. [기본 크기 화면](screenshots/records-default-clipped.png).
4. 동일 창의 **높이만 520→600**으로 늘리면 `Time played 13m, undos 3`이 나타난다. 데이터 값은 `timeSeconds=809`, `undos=3`이다. [높이 비교 화면](screenshots/records-height-control.png), [동일 데이터 비교 로그](runtime/RecordsLarger-Command92d865fc.result.txt).

소스도 관찰과 부합한다. [`Dialog_Leaderboard.cs`](../../../Source/PlayableRecreation/Framework/UI/Dialog_Leaderboard.cs)의 `InitialSize`는 620×520으로 고정돼 있다. 탭이 두 줄이 되면 38px을 더 쓰며 `DrawSummary`의 남은 높이가 줄어든다. 요약은 `maxOneColumn=true`인 `Listing_Standard`이고 스크롤이 없다. 높이 증가는 **원인 확인용 런타임 조작**이며 제품 수정으로 적용하지 않았다.

후속 수정은 탭·난이도·요약의 실제 높이에 맞춰 공간을 확보하거나 본문 스크롤을 제공하는 쪽이다. 수정 후에는 확장 10탭, 체스 요약 마지막 줄, 작은 화면에서의 접근성을 함께 재검사해야 한다. 이번 QA에서는 미해결로 남긴다.

## 제외한 시도와 남은 범위

- 초기 QA 프로필을 Casino → VFE → VEF 역순으로 넣어 VFE `ReflectionTypeLoadException`과 AI 오류가 발생했다. 이는 **QA 준비 오류**다. 올바른 순서로 수정하고 프로세스를 완전히 재시작한 뒤 최종 기능 증거를 수집했다. [초기 전체 로그](excluded/Player.invalid-load-order.log)와 `excluded/invalid-profile-*.txt`는 제품 실패나 PASS 근거로 사용하지 않는다.
- 초기 확장 가구 검사는 통행 불가 지형에 폰을 놓아 전제가 깨졌다. `excluded/menu-gate-r2.txt`를 제외하고, 빈 땅에서 다시 실행한 `runtime/menu-gate-clear-r2.txt`와 실제 worker 시작 결과를 사용했다. 일부 원시 로그의 `\n`은 QA helper 출력 형식이다.
- [유효 프로필 전체 Player 로그](Player.R2-complete.log)는 필터링하지 않았다. 기존 모드 메타데이터, `GradientHairMaskDef`, 영어 번역 데이터 145 오류, QA 세이브의 think-node 경고가 포함돼 있다. Mono fallback은 QA 명령의 메모리 DLL 로드 때도 출력된다. **전체 붉은 로그 0건이라는 판정은 하지 않는다.** 이번 유효 기능 재현에서 PR 예외/예약 오류 스택은 관찰하지 못했다.
- 모든 게임 완주·무르기·기권·재시도의 전 조합, 자연 초대 확률, 다른 Lord/해산/관계 변화, Abrasive 부정적 대화·사교 다툼, 자리 부족 폴백은 미확인이다. 지상/궤도 설정, 사망, 방문객 우호도 등 이번 커밋의 직접 변경 범위 밖 항목은 [R1 보고서](../2026-09-10_RealityMode/REPORT.md)의 범위와 제한을 유지한다.
- `diagnostics/`는 이 세션에서 사용한 QA 콜백 소스다. 임시 폰·가구와 `Host.State` 준비에 의존하며, 제품 코드나 일반 사용자의 실행 지침이 아니다. 기록 창 비교는 `RecordsView.cs` → `RecordsLarger.cs`, 재개는 `ResumeExact.cs` → `ResumePaths.cs`, 방문객 시간 경과는 `GuestR2.cs` → `GuestViewR2.cs`가 핵심이다.

## 복원

QA 프로세스를 종료한 뒤 원래 모드 목록·PR 설정·Prefs·개인 전적을 백업에서 복원했다. 임시 관찰 DLL을 제거했고 QA 세이브는 사용자 Saves 밖으로 옮겼다. 원본 세이브와 제품 DLL 해시는 시작 때와 동일하다. [재시작 전 확인](restoration-before-restart.json).

**최종 복원 검증 완료 (19:57).** 원본 세이브를 다시 열어 Rostra·Anya 두 식민자와 일시정지를 확인했다. 모드 목록·Prefs·PR 설정·개인 전적 **4개 파일 모두 이번 QA 시작 전 백업과 바이트 단위로 일치**한다. 원본 세이브와 제품 DLL 해시도 변하지 않았다. [최종 비교 기록](restoration-final.json), [원본 화면](screenshots/restored-original-paused.png), [복원 프로필 전체 로그](Player.restored-baseline.log).

마지막 로드에서 시간이 흘러 전적이 갱신되지 않도록 `pauseOnLoad=True`를 잠시 사용한 뒤, 게임의 Options에서 원래 `False`로 돌렸다. 파일만 덮어 실행 중 설정 캐시를 어긋나게 하지 않았다. QA 브라우저 세션도 닫았다.

문서의 로컬 링크와 `git diff --check`를 확인했다. 이번 변경은 QA 문서·증거뿐이므로 제품 재빌드·재설치는 하지 않았다.
