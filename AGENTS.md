# Rogue Dungeon Lab — Codex 작업 규칙

## 목적

결정적 생성, 빠른 편집 반복, 드랍 통계 검증을 우선하는 Unity 던전 제작 도구다.

## 먼저 읽기

`README.md` → `docs/ARCHITECTURE_KO.md` → 관련 Runtime/Editor 코드 → 장기 작업이면 `PLANS.md`.

## 필수 제약

- Runtime 어셈블리에서 `UnityEditor`를 참조하지 않는다.
- 외부 Unity 패키지를 임의로 추가하지 않는다.
- 같은 설정과 시드는 같은 레이아웃·콘텐츠를 만들어야 한다.
- 생성 로직은 Unity 전역 `Random`에 의존하지 않는다.
- 에디터 변경은 `Undo`, `SerializedObject`, `EditorUtility.SetDirty`를 사용한다.
- `__RogueDungeonLab_Generated`와 공개 API를 이유 없이 바꾸지 않는다.
- 사용자 UI는 한국어, 식별자는 영어를 기본으로 한다.
- 관련 없는 리팩터링보다 작은 검증 가능한 변경을 선호한다.

## 완료 정의

1. Unity Console 컴파일 오류 0개.
2. 장면 자동 구성이 예외 없이 완료되고 중복 시스템을 만들지 않음.
3. 동일 시드 결과가 반복 가능함.
4. 모든 바닥 셀이 입구에서 도달 가능함.
5. 클릭 1회가 드랍 통계 1회를 추가함.
6. Edit/Play/domain reload에서 null 예외가 없음.
7. 동작 변경 시 문서 갱신.

Unity가 없는 환경에서는 실행하지 못한 검증을 명시하고 성공했다고 추정하지 않는다.
