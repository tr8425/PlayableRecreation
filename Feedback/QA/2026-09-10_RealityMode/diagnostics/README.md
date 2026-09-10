# 재현용 QA 콜백

이 파일들은 별도 임시 디렉터리에서 컴파일해 이미 실행 중인 게임 메인 스레드에 전달한 기록이다. 제품 DLL에는 포함하지 않았다. `Host.State`에는 앞선 준비 단계의 테스트 폰·가구 참조가 필요하므로 이 소스만으로 재현 환경 전체가 자동 구성되지는 않는다. 원본 세이브에 직접 실행하지 않는다.

- `ResumeExact.cs`: 가구의 실제 이어 두기 메뉴 콜백으로 QA-02 재현.
- `GuestChairRepro.cs`: 의자가 있는 판으로 QA-03 재검사.
- `PunchClean.cs`: 건강한 새 폰 두 명으로 QA-04의 두 번째 예약 실패 재검사.
- `PracticeClean.cs`: 건강한 새 폰, 합법 수 체크메이트, 실제 New game 콜백으로 QA-05 재검사.
- `ReservationSuite.cs`: 2인 대국과 제3자의 같은 가구 vanilla JoyGiver 공존.
- `FinalRuntime.cs`, `FinalContinue.cs`, `InviteFilters.cs`: 취소 정리·강제 바닐라 대화·솔로 맵 이탈·후보 제외·방문 기록 청소 범위.
- `FinishLegal.cs`: 실제 체스 worker에 합법 수를 넣은 완주. 자연 AI 경기 아님.
- `fixture-errors/`: QA 작성 중 잘못 지정한 수로 실패한 호출. PR 제품 예외로 분류하지 않음.

이전 `runtime/critical-suite.txt` 및 초기 `MatchState`는 탐색 기록이다. 실제 이어 두기 경로 판정에는 `runtime/resume-exact.txt`, 연습 재시작 판정에는 `runtime/practice-clean.txt`를 우선한다.
