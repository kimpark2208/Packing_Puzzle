# 유니티 작업 워크플로 개선 제안

`skill-observations/log.md` 기반. 2026-10-01 사용자 승인으로 1~3번 memory에 반영 완료(feedback_playmode_frame_step.md, feedback_commit_diff_check.md, feedback_ui_area_name_confusion.md). 4번은 기존 project_default_font.md에 이미 포함되어 있어 별도 반영 없음.

## 1. Play Mode 자동 테스트 전 프레임 강제 진행
execute_code로 씬 전환이나 GameObject 활성화 직후 런타임 상태(Start() 결과 등)를 검증할 때는,
확인 코드를 돌리기 전에 항상 아래를 먼저 실행:
```csharp
EditorApplication.Step();
```
Game View를 Show/Focus/Repaint하는 것만으로는 프레임이 진행되지 않을 수 있음 — 매번 이 한 줄을 테스트 루틴 맨 앞에 넣는 걸 기본값으로.

## 2. 커밋 메시지 작성 전 diff 확인 의무화
`git commit`하기 전에 반드시 `git diff --stat` 또는 `git show --stat HEAD`(amend 시)로 실제 변경 파일 목록을 확인한 뒤 메시지를 작성. 대화 맥락만으로 "오늘 한 일"을 재구성하지 않는다.

## 3. 유사 이름 UI 영역 작업 전 하이라키 확인
"트레이슬롯area" 같은 지칭이 나오면, 비슷한 이름(SlotArea 등)이 프로젝트에 이미 있는지 먼저 `find_gameobjects`/하이라키 조회로 확인한 뒤 작업 시작. 이름이 애매하면 작업 전 재확인 질문.

## 4. 새 TMP 오브젝트 생성 시 기본 폰트 체크리스트화
`AddComponent<TextMeshProUGUI>()` 호출 직후 바로 `font = 성곡세미세리프 SDF` 설정을 세트로 묶어서 습관화 (임시/디버그 UI 포함 예외 없음).

---
*이미 memory 파일(존댓말, 코드 작업 승인, 기본 폰트)에 기록된 항목은 여기서는 Unity MCP 테스트/커밋 워크플로 관련 신규 항목만 추림.*
