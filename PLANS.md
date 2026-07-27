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

---

# R5.2 실행 계획 — R6 Bake 착수 게이트

## 목표

Play HUD가 프로젝트의 `RogueDungeonSettings` 원본을 직접 변경하지 않도록 런타임 복제본의 소유권과 StageDefinition 절차 레시피 연결을 바로잡는다. 동시에 R6이 Runtime/Editor 경계를 깨거나 불완전한 hash로 오래된 Bake를 승인하지 않도록 manifest, 콘텐츠 실현 의존성, gameplay 구축 설정, 영속 재질과 Mesh 소유권 계약을 구현 전에 고정한다.

## 착수 기준

- R5.1의 Blueprint 저장·설정 복원·정확 재생성 경로가 구현되어 있다.
- Unity `6000.5.3f1` 현재 HEAD에서 compile, EditMode `51/51`, PlayMode `3/3`이 통과한다.
- `DungeonStageDefinition`의 `BakedPrefab` enum은 존재하지만 validator가 아직 명시적으로 차단한다.
- Runtime 어셈블리는 `UnityEditor`를 참조하지 않고 기존 settings-only facade는 `LegacyV1`을 유지한다.

## 마일스톤

### M1 — 런타임 레시피 격리와 HUD 연결

- Play 중 settings-only 또는 Procedural StageDefinition의 원본 레시피를 `HideAndDontSave` 복제본으로 분리
- `RogueDungeonGenerator`가 활성 런타임 설정의 생성·재사용·교체·해제를 소유
- HUD가 `generator.settings` 대신 활성 런타임 설정을 편집
- Procedural StageDefinition 재생성이 같은 복제본을 입력 recipe와 gameplay 설정으로 사용
- SavedBlueprint 활성 상태에서는 구조·밀도 편집이 저장 논리 맵을 바꾸지 않는다는 점을 UI에 명시
- 기존 EditMode 제작 흐름과 공개 settings 참조는 유지

### M2 — R6 데이터·소유권 계약 고정

- Runtime-safe `DungeonBakeManifest`와 Editor-only `DungeonStageBaker`의 어셈블리 경계를 문서에 확정
- `blueprintHash`, `finalBlueprintHash`, `catalogPlanningHash`, `contentRealizationHash`, `gameplayBuildConfigHash`, `materialDependencyHash`, `overrideHash`, `builderVersion` 역할 정의
- Prefab·Mesh·Material·drop table·gameplay ID·누락 정책을 포함하는 Bake 의존성 범위 정의
- built-in 표현용 영속 Bake material set과 기본 Catalog Prefab만 지원하는 R6 MVP resolver 범위 정의
- 임시 Runtime Mesh와 영속 Bake Mesh의 소유권 및 staging 후 성공 교체 규칙 정의
- RuntimeBuild와 BakedPrefab의 stable ID·Collider·클릭/drop·report 동등성 완료 조건 추가

### M3 — 저장·수명주기·성능 회귀 보강

- 원본 settings 비변경과 별도 Procedural StageDefinition recipe의 HUD 반영 PlayMode 테스트
- 저장 자산 재임포트 후 미리보기 hash, Undo 후 저장·재임포트, LegacyV1/custom catalog 정확 재생성 회귀 보강
- snapshot 필드가 없는 기존 YAML 자산 호환 검증 또는 명시적 fixture 유지
- RuntimeBuild p50/p95, 현재 스레드 allocation counter 지원 상태와 Profiler Mono 사용량 증분 기준선 캡처
- transient Mesh domain reload 위험과 실제 화면 pointer Raycast는 자동/수동 범위를 구분해 기록

### M4 — 문서와 최종 검증

- README, 아키텍처, 통합 로드맵, 사용자 가이드, 테스트 계획, 확장 가이드, 변경 로그 갱신
- R5.1 수동 검증 문구와 기준 자산 생성 도구를 현재 설정 복원 흐름에 맞춤
- Unity `6000.5.3f1` batchmode compile
- EditMode·PlayMode 전체 테스트
- Runtime의 `UnityEditor`·Unity 전역 `Random` 참조, `git diff --check`, 작업 트리 영향 확인

## 완료 기준

1. Play HUD 변경이 원본 settings와 StageDefinition recipe 자산을 변경하지 않는다.
2. 별도 Procedural StageDefinition recipe를 사용하는 HUD 변경이 활성 시드를 유지한 채 실제 Blueprint에 반영된다.
3. settings-only, SavedBlueprint와 기존 에디터 제작 경로가 회귀하지 않는다.
4. R6 manifest가 Runtime-safe이며 planning hash와 표현·gameplay 의존성 hash를 구분한다.
5. R6 재Bake는 소유한 파생 자산만 staging 후 교체하고 실패 시 기존 정상 Bake를 유지하도록 계약된다.
6. RuntimeBuild/BakedPrefab 동등성, 영속 재질과 transient/persistent Mesh 소유권이 R6 완료 기준에 포함된다.
7. Unity 6.5 compile과 전체 자동 회귀가 통과하고 실행하지 못한 수동 검증이 명시된다.

## 완료 결과

- Play HUD는 settings-only와 Procedural StageDefinition 원본을 변경하지 않는 Generator 소유 `HideAndDontSave` 복제본을 사용한다.
- 후보 복제본은 Loader 성공 뒤에만 commit하며 실패한 source 전환은 기존 StageInstance·generated root·활성 설정을 유지한다.
- SavedBlueprint 구조·시드 편집 차단, source 전환·소유자 파괴와 첫 로드 전 StageDefinition 우선 경로를 PlayMode로 고정했다.
- Runtime-safe `DungeonBakeManifest`·`DungeonBakeMaterialSet`과 R6 hash·material·owned artifact·resolver·staging/parity 계약을 코드와 문서로 고정했다.
- Unity `6000.5.3f1` compile, EditMode `58/58`, PlayMode `7/7`을 통과했다.
- Balanced seed `73125` RuntimeBuild 15회는 시간 p50 `7.390 ms`, p95 `7.744 ms`; Profiler Mono 사용량 증분 p50 `2,252,800 B`, p95 `2,269,184 B`였다. Unity Mono의 thread allocation counter는 `supported=False`였다.
- 실제 Play 중 script/domain reload, HUD 화면 안내와 화면 포인터 Raycast는 수동 미실행 항목으로 남겼다.

---

# R6 실행 계획 — SavedBlueprint 영구 Bake와 BakedPrefab 로드

## 목표

검증된 `DungeonBlueprintAsset`을 Editor 전용 Baker로 영구 Mesh와 Prefab으로 변환하고,
`DungeonStageDefinition`의 `BakedPrefab` 모드가 런타임 생성기·MeshBuilder·Resolver를 호출하지
않고 그 결과를 로드하도록 한다. R5.2에서 확정한 manifest/hash/소유권 계약을 실제 제작
흐름으로 연결하며, 실패한 재Bake가 마지막 정상 Bake를 훼손하지 않게 한다.

## 착수 기준

- 사용자가 R5.2 수동 확인을 통과로 판정했다.
- Unity `6000.5.3f1` 기준 R5.2 compile, EditMode `58/58`, PlayMode `7/7`이 통과했다.
- 현재 작업 트리의 R5.2 변경은 R6 기준선이므로 보존한다.

## 마일스톤

### M1 — Runtime BakedPrefab 계약과 로더

- `SavedBlueprint + BakedPrefab`만 허용하고 `Procedural + BakedPrefab`을 명시적으로 거부
- StageDefinition과 manifest의 Blueprint, Catalog, Prefab, MaterialSet 연결 일치 검증
- Baked Prefab에 저장된 빌드 메타데이터로 `DungeonSceneBuildResult`와 report 복원
- Prefab 인스턴스화만 수행하며 Blueprint 생성, `DungeonMeshBuilder`, Resolver는 호출하지 않음
- `__RogueDungeonLab_Generated`, `DungeonStageInstance`, `GenerationCompleted` 호환 유지

### M2 — Editor DungeonStageBaker와 무결성 지문

- 저장 Blueprint와 직접 Catalog Prefab/built-in fallback만 지원하는 R6 Baker 구현
- floor/wall Mesh를 `.asset`으로 영속화하고 persistent MaterialSet을 적용
- source/final Blueprint, planning, realization, gameplay, material, override, builder hash 기록
- Prefab·Mesh·manifest의 GUID와 dependency hash를 `ownedArtifacts`에 정확히 기록
- stage별 staging에서 완성·검증 후 StageDefinition 참조를 원자적으로 교체
- 실패 시 기존 정상 Bake를 유지하고, 정리 시 manifest 소유 자산과 stage bake root만 대상으로 제한

### M3 — 제작 UI와 검증 자산

- `스테이지 자산` 탭에서 MaterialSet 선택, Bake, stale 검사, 결과 선택 기능 제공
- 기본 persistent MaterialSet 생성 도구 제공
- R6 수동 검증 StageDefinition과 안내를 기존 R5 검증 흐름에 추가
- 정상 Bake, 재Bake, 강제 실패 rollback, 공유 자산 보호, stale hash를 EditMode에서 검증

### M4 — 동등성·회귀·문서

- RuntimeBuild/BakedPrefab의 Blueprint hash, 입·출구, floor collider, stable spawn identity,
  클릭/drop marker, build count/report 동등성 검증
- Baked 경로가 생성기·MeshBuilder·Resolver에 의존하지 않는 회귀 테스트 추가
- Unity `6000.5.3f1` batchmode compile, 전체 EditMode·PlayMode 실행
- 가능한 Player build smoke test 수행
- README, 아키텍처, 사용자·확장·테스트 가이드, 로드맵, CHANGELOG 갱신

## 완료 기준

1. 저장 Blueprint를 한 번의 Bake로 영구 Mesh/Prefab/manifest로 만들 수 있다.
2. 같은 입력의 재Bake는 같은 무결성 지문과 게임플레이 구성을 만든다.
3. 실패한 재Bake 뒤에도 기존 StageDefinition의 Prefab·manifest 참조와 파일이 정상이다.
4. BakedPrefab 로드는 런타임 절차 생성이나 transient Mesh 생성 없이 완료된다.
5. RuntimeBuild와 BakedPrefab의 핵심 구조·콘텐츠·클릭/drop·report가 동등하다.
6. Runtime 어셈블리는 `UnityEditor`를 참조하지 않고 Unity 전역 `Random`을 사용하지 않는다.
7. Unity 6.5 compile과 자동 테스트가 통과하며, 자동화하지 못한 화면 검증은 별도로 보고한다.

## 완료 결과

- `SavedBlueprint + BakedPrefab` 전용 Runtime Loader, root metadata와 `DungeonStageInstance.BuildMode`를 구현했으며 Procedural+Baked 조합을 차단했다.
- Editor-only Baker가 영속 floor/wall Mesh, built-in/fallback·직접 Catalog Prefab, manifest와 전체 dependency fingerprint를 stage별 staging에서 생성하고 성공 뒤에만 commit한다.
- 이전 산출물 정리는 정확한 role·GUID·예약 파일명·main asset과 stage bake root를 모두 확인하며, 실패 주입 재Bake는 이전 Prefab·manifest·build mode를 보존한다.
- Bake UI, 기본 MaterialSet 생성, 최신성 리포트와 결과 Ping, R6 전용 자산·장면·rollback 수동 검증 도구와 Player build batch 진입점을 추가했다.
- RuntimeBuild/BakedPrefab의 Blueprint·입출구·Collider·stable spawn identity·클릭/drop·report parity와 Baked 경로의 생성기·Mesh Builder·resolver 미호출을 자동 검증했다.
- Unity `6000.5.3f1` compile, EditMode `74/74`, PlayMode `8/8`을 통과했다.
- 분리된 임시 프로젝트에서 전용 Baked Scene Windows Development Player 빌드가 성공했으며 총 크기는 `172,288,233 B`였다. 실패 주입 rollback batch 검증도 통과했다.
- 실제 화면 HUD 배치와 Play 중 script/domain reload는 수동 확인 범위로 유지한다.

---

# R7 실행 계획 — 비파괴 Stage Override 제작

## 목표

저장된 `DungeonBlueprintAsset`을 직접 변경하거나 `__RogueDungeonLab_Generated` 계층을
수동 저장하지 않고도 spawn 비활성화·추가·콘텐츠 교체·transform 조정을
`DungeonStageOverrides` 자산으로 보존한다. 같은 원본과 Override는 RuntimeBuild와
BakedPrefab에서 같은 최종 Blueprint와 게임플레이 결과를 만들어야 하며, 원본이
바뀌어 해결되지 않은 stable ID 충돌이 남으면 Preview와 출시용 Bake를 차단한다.

## 착수 기준

- R6 수동 검증 환경의 SavedBlueprint·영속 Mesh·BakedPrefab 생성과 최신성 검사가 통과했다.
- commit 직전 실패를 주입한 재Bake가 이전 정상 Prefab·manifest를 보존했다.
- R6 Bake format v1 자산은 Override가 없는 기존 경로로 계속 로드할 수 있어야 한다.

## 마일스톤

### M1 — Runtime Override 데이터·hash·적용 계약

- Runtime-safe `DungeonStageOverrides`와 disable/add/replace/transform 레코드 추가
- `baseBlueprintHash`와 컬렉션 순서에 독립적인 canonical `overrideHash` 계산
- 원본 stable spawn ID, 추가 ID, 중복 명령과 transform 유효성을 코드 기반 리포트로 검증
- 원본 deep clone에만 Override를 적용하고 최종 Blueprint hash를 다시 계산하는 순수 applier 구현
- StageDefinition과 RuntimeBuild Loader가 SavedBlueprint Override를 선택적으로 적용

### M2 — R6 Bake format 하위 호환과 R7 Bake 연계

- Bake format v1의 빈 Override·source=final 계약을 계속 검증·로드
- Bake format v2에 source Override 자산·override hash·최종 Blueprint hash 기록
- Baker가 원본이 아니라 검증된 최종 Blueprint로 Mesh·Prefab·metadata를 생성
- Override 또는 최종 Blueprint 변경을 stale로 탐지하고 재Bake rollback·소유권 규칙 유지
- RuntimeBuild/BakedPrefab의 최종 Blueprint·spawn identity·클릭/drop·report parity 검증

### M3 — Preview 선택 기반 비파괴 편집 UI

- StageDefinition용 Override 자산 생성·연결과 Undo 가능한 수정 서비스 추가
- 선택한 `DungeonSpawnIdentity`를 disable/replace/transform Override로 기록
- 새 spawn을 명시적 stable ID와 category/content/transform으로 추가·삭제
- Override 적용 Preview를 전체 재구축하고 generated hierarchy 직접 편집 금지 안내
- 원본 hash 변경 시 exact stable ID 재결합, 미해결 ID·중복 추가 ID 충돌 리포트와 수동 정리 도구 제공
- Override Preview 중 원본 Blueprint 덮어쓰기와 혼동되는 기존 저장 동작 차단

### M4 — 수동 검증·회귀·문서

- disable/add/replace/transform과 canonical hash·deep-copy 원본 보존 EditMode 테스트
- base hash 변경 뒤 재결합 성공·실패와 미해결 충돌 Bake 차단 테스트
- R6 v1 manifest 하위 호환, R7 v2 stale·재Bake·rollback 테스트
- 실제 클릭/drop PlayMode와 R7 수동 검증 장면·안내 추가
- Unity `6000.5.3f1` compile, 전체 EditMode·PlayMode, 가능한 Player build smoke 실행
- README, 아키텍처, 사용자·테스트·확장 가이드, 로드맵과 CHANGELOG 갱신

## 완료 기준

1. 원본 Blueprint hash와 자산 데이터는 모든 Override 편집·Preview·Bake 뒤에도 변하지 않는다.
2. 같은 원본과 같은 Override는 컬렉션 표시 순서와 무관하게 같은 override/final hash를 만든다.
3. disable/add/replace/transform 결과가 RuntimeBuild와 BakedPrefab에서 동일하다.
4. 원본 변경 뒤 exact stable ID 재결합은 명시적 승인으로만 base hash를 갱신한다.
5. 사라진 target ID, 중복 추가 ID 또는 상충 명령이 있으면 Preview와 Bake가 오류로 차단된다.
6. R6 format v1 Bake는 계속 로드되고 R7 format v2 Bake는 Override stale을 탐지한다.
7. 실패한 재Bake 뒤 기존 정상 Bake와 사용자·공유·Override 자산이 보존된다.
8. Runtime은 `UnityEditor`와 Unity 전역 `Random`을 참조하지 않고 Unity 6.5 회귀가 통과한다.

## 완료 결과

- Runtime-safe Override 데이터·canonical hash·validator·deep-copy applier·명시적 재결합과 SavedBlueprint RuntimeBuild 연결을 완료했다.
- Bake format/builder v1 하위 호환을 유지하면서 Override-aware v2 manifest·Baker·BakedPrefab Loader와 stale·rollback·소유권 검증을 연결했다.
- Stage Override 생성·Definition 연결, 원본/적용 미리보기, Scene stable ID 선택 편집, 수동 Spawn과 변경 목록 UI를 구현했다.
- Unity `6000.5.3f1`에서 전체 EditMode `83/83`, PlayMode `9/9`이 통과했다.
- R7 검증 장면을 다시 연 뒤 RuntimeBuild/BakedPrefab의 final hash와 stable spawn identity parity를 확인했다.
- Windows64 Development Player 빌드는 경고 `0`개, 총 크기 `172,176,046 B`로 성공했다.
- 검증 근거는 `Logs/R7ManualSetup.log`, `Logs/R7FullEditMode.xml`, `Logs/R7FullPlayMode.xml`, `Logs/R7PlayerBuildSmoke.log`에 남겼다.
- 실제 HUD/Scene 육안과 Play 중 script/domain reload는 수동 확인 범위로 남겼다.
