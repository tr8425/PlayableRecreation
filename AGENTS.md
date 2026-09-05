# 에이전트 공통 지침 (Playable Recreation)

- **유저 피드백은 `Feedback/FEEDBACK.md` 에서 관리한다.** 형식·상태 규칙은 그 파일
  상단에 있다. 피드백을 다뤘으면(답글, 수정, 백로그 반영) 반드시 그 파일을 갱신한다.
- `Feedback/` 는 git 에는 올라가지만 배포에는 포함되지 않는다 — `Tools/package.py`
  가 화이트리스트 방식이므로 별도 제외 설정이 필요 없다.
- 프로젝트 제약·현재 상태는 `HANDOFF.md`, 설계 원칙은 `DESIGN.md` 참조.
- 빌드·검증·설치: `dotnet build Source/PlayableRecreation -c Release` →
  `python Tools/verify.py` → `python Tools/package.py --install` (DLL 변경은 림월드 재시작 필요).
