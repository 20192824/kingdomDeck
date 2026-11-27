# CLAUDE.md

## 프로젝트
중세 판타지 덱빌딩 카드게임 "왕국의 덱" (Unity). 게임 규칙과 요구사항은 전부 `SPEC.md`에 있다. 작업 전에 항상 `SPEC.md`를 먼저 읽는다.

## 기술 스택
- Unity 6 LTS, C#
- 애니메이션: DOTween (또는 Unity 기본 기능)
- 테스트: Unity Test Framework (EditMode)
- Unity MCP: 씬 구성, 프리팹 생성, 컴포넌트 연결 같은 에디터 작업에 사용

## 폴더 구조
- `Assets/Scripts/Engine/` — 게임 규칙. 별도 asmdef, `UnityEngine` 참조 금지
- `Assets/Scripts/AI/` — AI 전략. 엔진만 참조
- `Assets/Scripts/Presentation/` — 이벤트 큐, 소환 연출, UI
- `Assets/Data/cards.json` — 카드 규칙 데이터
- `Assets/Data/CardVisuals/` — 카드별 연출 ScriptableObject
- `Assets/Prefabs/Summons/` — 소환물 프리팹
- `Assets/Tests/EditMode/` — 테스트

## 작업 규칙
- `SPEC.md`의 개발 단계 순서를 지킨다. 완료 조건을 만족하기 전에 다음 단계로 넘어가지 않는다.
- 엔진 코드는 절대 `UnityEngine`이나 연출 코드를 참조하지 않는다. 엔진은 이벤트만 내보낸다.
- 엔진이나 카드를 수정하면 반드시 테스트를 실행하고 통과시킨 뒤 작업을 끝낸다.
- 새 카드를 추가할 때는 그 카드의 단위 테스트도 같이 작성한다.
- 코드는 파일로 직접 작성하고, Unity MCP는 에디터에서만 할 수 있는 작업에 쓴다.
- 규칙이 애매하면 임의로 정하지 말고 나에게 질문한다.
- `SPEC.md`의 규칙을 바꿔야 할 것 같으면 먼저 이유를 설명하고 허락을 받는다.
- 화면 문구는 모두 한국어로 쓴다.
- 작업이 끝나면 무엇을 바꿨는지, 내가 에디터에서 직접 확인해야 할 것이 무엇인지 짧게 요약한다.

## 테스트 실행
- 에디터: Window > General > Test Runner > EditMode
- 명령줄: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
