# R4 실행 계획 — 콘텐츠 카탈로그와 Resolver

## 목표

`LegacyV1`의 결과와 기존 settings-only facade를 유지하면서, `StableV2` 절차 생성이 Inspector 목록 순서와 범주 간 난수 소비에 영향받지 않는 콘텐츠 key를 선택하도록 확장한다. `DungeonStageDefinition`은 선택적 콘텐츠 카탈로그와 누락 콘텐츠 정책을 제공하고, 런타임 SceneBuilder는 기본 Prefab resolver 또는 기존 primitive fallback으로 같은 Blueprint를 구축한다.

## 착수 기준

- R0~R3의 Blueprint, SceneBuilder, StageDefinition/Loader 경로가 구현되어 있다.
- 기존 Blueprint는 built-in `contentKey`만 기록하며 SceneBuilder가 직접 primitive를 만든다.
- 생성기 버전은 `LegacyV1`만 허용하며 콘텐츠 범주들이 일부 난수 호출 순서를 공유한다.
- Unity `6000.5.3f1` R3 기준선은 EditMode `19/19`, PlayMode `2/2`이다.

## 마일스톤

### M1 — 카탈로그 데이터와 검증

- `DungeonContentCatalog` 및 직렬화 가능한 entry 계약 추가
- contentKey canonical 정렬과 planning hash 구현
- 중복 key, 잘못된 weight/progression/footprint/간격 검증
- Blueprint key와 카탈로그의 누락 관계를 정책별로 검사

### M2 — StableV2 계획

- 프로젝트 소유 고정 PRNG와 seed 파생 공식 추가
- Layout/Gimmick/Enemy/Destructible/Prop/Variant 독립 스트림 적용
- progression, room/corridor, room tag 조건과 weight로 후보 key 선택
- Inspector entry 순서가 바뀌어도 같은 Blueprint hash를 생성
- `LegacyV1` 경로와 golden 지문 유지

### M3 — Resolver와 런타임 구축

- `IDungeonContentResolver`와 기본 catalog Prefab resolver 추가
- Prefab 인스턴스에 Blueprint transform과 `DungeonSpawnIdentity` 적용
- Error/PrimitiveFallback/Skip 누락 정책 구현
- 카탈로그가 없거나 built-in key를 사용할 때 현재 primitive 표현 유지
- StageDefinition/Loader가 카탈로그 hash, resolver와 누락 정책을 전달

### M4 — 테스트와 문서

- 카탈로그 hash 순서 독립성, 검증 코드, 누락 key 정책 테스트
- StableV2 반복성·범주 독립성·LegacyV1 회귀 테스트
- Prefab resolver 및 StageLoader 통합 테스트
- README, 아키텍처, 로드맵, 테스트 계획, 확장 가이드, 변경 로그 갱신

## 검증 증거

- Unity `6000.5.3f1` batchmode 컴파일
- EditMode 전체 테스트
- PlayMode 전체 테스트
- one-click setup, 같은 시드 재생성, Play 클릭/drop/statistics는 기존 자동 테스트와 수동 점검 범위를 구분해 보고

## 완료 결과

- R4 카탈로그·StableV2·Resolver·누락 정책과 StageLoader 연결 완료
- 통합 리뷰의 snapshot 무결성, 예약 key/category, cell-room 일치, Prefab 드랍 우선순위, staging root, Mesh 소유권 항목 보강
- Unity `6000.5.3f1` batchmode compile 성공
- EditMode `41/41`, PlayMode `3/3` 통과
- one-click setup과 직접 파괴·드랍 통계는 자동 검증, 실제 화면 포인터 Raycast는 수동 검증 범위로 기록

---

# R5 실행 계획 — 에디터 저장·불러오기 제작 흐름

## 목표

현재 Generator가 확정한 `DungeonBlueprint`를 프로젝트 자산으로 저장하고, 선택한 저장본을 검증·비교·미리보기한 뒤 `DungeonStageDefinition`까지 생성하는 첫 저장형 스테이지 MVP를 완성한다. 기존 Procedural/LegacyV1/StableV2 결과와 Runtime/Editor 어셈블리 경계는 유지한다.

## 착수 기준

- R4 수동 검증과 Unity `6000.5.3f1` compile, EditMode `41/41`, PlayMode `3/3`이 승인되었다.
- `DungeonBlueprintAsset.Store`, `DungeonStageDefinition`, SavedBlueprint `DungeonStageLoader` 경로가 존재한다.
- 기존 에디터 창에는 생성·분포·드랍·가이드 탭만 있고 저장 제작 UI는 없다.
- 작업 트리의 `Assets/R4ManualVerification/Materials` 변경은 사용자 검증 결과이므로 수정하지 않는다.

## 마일스톤

### M1 — Editor authoring 서비스

- 현재 Blueprint의 저장 전 코드 기반 검증
- 새 `DungeonBlueprintAsset` 생성과 메모·생성 시각 보존
- 선택 자산 덮어쓰기와 복합 직렬화 데이터 Undo 지원
- 현재 결과/저장본 provenance·hash 비교와 stale 상태 분류
- 저장 Blueprint 기반 `DungeonStageDefinition` 생성 및 선택적 Generator 연결

### M2 — 미리보기와 UI

- 에디터 창에 `스테이지 자산` 탭 추가
- 현재 결과 새 저장, 선택 Blueprint 덮어쓰기 확인
- 저장본 RuntimeBuild 미리보기와 절차 원본 복귀
- Blueprint·catalog 교차 검증 오류/경고 표시
- seed, generatorVersion, recipe/catalog/blueprint hash 비교 표시

### M3 — 영속성과 회귀 테스트

- 새 저장 자산의 deep-copy/hash 검증
- 덮어쓰기 후 Undo 복구 검증
- AssetDatabase save/import 뒤 Blueprint와 StageDefinition 참조 유지
- 저장본 미리보기가 레시피 재계산 없이 동일 hash를 구축하는지 검증
- stale/다른 seed/동일 결과 상태 분류 검증
- one-click setup, 동일 시드, 클릭/drop/statistics 기존 회귀 유지

### M4 — 문서와 최종 검증

- README, 아키텍처, 로드맵, 테스트 계획, 확장 가이드, 변경 로그 갱신
- Unity `6000.5.3f1` batchmode compile
- EditMode 및 PlayMode 전체 테스트
- Unity 재시작 자체와 실제 화면 포인터 클릭은 자동 범위와 구분해 보고

## 완료 기준

1. 사용자가 현재 결과를 새 Blueprint로 저장하거나 기존 자산에 확인 후 덮어쓸 수 있다.
2. 저장본을 즉시 미리보기하고 현재 절차 결과와 hash/provenance 차이를 확인할 수 있다.
3. SavedBlueprint RuntimeBuild용 StageDefinition을 생성해 새 씬에서 사용할 수 있다.
4. 검증 오류가 있는 Blueprint 저장·미리보기·StageDefinition 생성을 차단한다.
5. Undo, SerializedObject, `EditorUtility.SetDirty`와 AssetDatabase 저장을 사용한다.
6. Unity 6.5 compile과 전체 자동 회귀가 통과한다.

## 완료 결과

- 현재 생성 결과의 새 Blueprint 저장, 검증 후 덮어쓰기, 복합 데이터 Undo를 구현했다.
- 저장본 미리보기와 절차 생성 복귀, hash/provenance 비교 및 stale 상태 표시를 구현했다.
- SavedBlueprint RuntimeBuild용 `DungeonStageDefinition` 생성과 Generator 연결을 구현했다.
- 잘못된 Blueprint, catalog 참조, 누락 콘텐츠 정책은 저장·미리보기·StageDefinition 생성 전에 차단한다.
- Unity `6000.5.3f1` batchmode compile에 성공했다.
- EditMode `48/48`, PlayMode `3/3`이 통과했다.
- 실제 Unity 재시작 후 UI 조작과 화면 포인터 클릭은 수동 확인 범위로 남겼다.

---

# R5.1 실행 계획 — 저장 레시피 복원 보완

## 목표

R5 Blueprint 자산이 확정된 맵 결과뿐 아니라 그 결과를 만든 정규화 레시피도 선택적으로 보존하게 한다. 사용자는 `스테이지 자산` 탭에서 저장 당시 설정을 현재 `RogueDungeonSettings`에 Undo 가능하게 불러오거나, 저장 시드까지 적용해 절차 결과를 즉시 재생성할 수 있어야 한다.

## 호환 기준

- 기존 R5 Blueprint 자산은 레시피 스냅샷이 없는 정상 자산으로 계속 미리보기·로드할 수 있다.
- 선택적 제작 메타데이터는 Blueprint 논리 hash와 SavedBlueprint RuntimeBuild 결과를 바꾸지 않는다.
- 저장 스냅샷 hash가 Blueprint의 `recipeHash`와 다르면 설정 적용을 차단한다.
- 드랍 테이블, 런타임 플래그처럼 맵 생성에 영향을 주지 않는 설정은 덮어쓰지 않는다.
- 작업 트리의 R4 Material과 `Assets/R5ManualVerification` 변경은 사용자 검증 결과이므로 수정하지 않는다.

## 마일스톤

### M1 — 레시피 스냅샷 영속성

- `DungeonRecipeSnapshot` deep copy와 `RogueDungeonSettings` 역적용 구현
- `DungeonBlueprintAsset`에 선택적 authoring recipe snapshot과 존재 여부 저장
- 새 저장·덮어쓰기 시 현재 설정 snapshot과 `recipeHash` 일치 검증
- 기존 snapshot 없는 자산의 RuntimeBuild 호환 유지

### M2 — 설정 복원 UI

- 저장 레시피 상태와 hash를 `스테이지 자산` 탭에 표시
- `저장본 설정을 UI에 불러오기`로 설정 자산만 Undo 가능하게 갱신
- `설정 + 저장 시드 적용 후 절차 생성`으로 한 번만 재생성
- 기존 `절차 원본으로 복귀` 문구를 현재 설정으로 재생성한다는 의미로 명확화

### M3 — 검증과 문서

- 곡선·구조·밀도 설정 round-trip과 비생성 필드 보존 테스트
- 자산 재임포트, 덮어쓰기 Undo, snapshot 누락·hash 불일치 차단 테스트
- 같은 저장 snapshot·시드가 원 Blueprint hash를 재현하는지 검사
- README, 아키텍처, 사용자 가이드, 로드맵, 테스트 계획과 변경 로그 갱신
- Unity `6000.5.3f1` compile 및 전체 EditMode·PlayMode 회귀

## 완료 기준

1. 새 Blueprint 저장본은 저장 당시 정규화 레시피를 독립된 깊은 복사본으로 보존한다.
2. 저장 설정을 적용하면 모든 생성 필드와 AnimationCurve가 복원되고 recipe hash가 일치한다.
3. 설정과 저장 시드로 재생성한 결과가 저장 Blueprint의 논리 hash와 일치한다.
4. 기존 snapshot 없는 R5 자산은 로드 가능하며 설정 복원 버튼만 명확히 비활성화된다.
5. 설정 자산 변경은 확인과 Unity Undo를 지원하고 비생성 필드를 보존한다.
6. Unity 6.5 compile과 전체 자동 회귀가 통과한다.

## 완료 결과

- `DungeonBlueprintAsset`에 논리 Blueprint와 분리된 선택적 authoring recipe snapshot을 추가했다.
- 새 저장·덮어쓰기는 현재 결과와 recipe hash가 같은 설정만 함께 보존하며 기존 snapshot 없는 R5 자산은 그대로 지원한다.
- 설정만 불러오기와 설정·저장 시드 절차 재생성을 확인·SerializedObject·Undo 흐름으로 구현했다.
- 구조·밀도·AnimationCurve는 복원하고 시드·드랍·런타임 옵션은 선택 정책에 따라 보존한다.
- StableV2를 포함해 저장 generatorVersion·catalog가 같은 입력이 원 Blueprint hash를 재현하는지 변경 전에 검증한다.
- Unity `6000.5.3f1` batchmode compile에 성공했다.
- EditMode `51/51`, PlayMode `3/3`이 통과했다.
- 실제 Unity 프로세스 완전 재시작과 화면 포인터 Raycast는 수동 확인 범위로 유지했다.
