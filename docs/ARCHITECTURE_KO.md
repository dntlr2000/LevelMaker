# 아키텍처

이 문서는 R5.2 승인 기준선, 검증 완료된 R6 Mesh·Prefab Bake, R7 비파괴 Stage Override, R8 RunState와 R9 프로젝트 간 패키징의 구현·통합 검증 경계를 설명합니다. 선택 후속 단계는 [스테이지 제작·배포 통합 로드맵](STAGE_PIPELINE_ROADMAP_KO.md)을 참고합니다.

## 파이프라인

```text
DungeonStageDefinition
  ├─ Procedural
  │    └─ RogueDungeonSettings + resolved seed
  │         → DungeonGenerationRequest
  │         → DungeonBlueprintGenerator
  │              ├─ LegacyV1 → 승인된 기존 layout·built-in contentKey
  │              └─ StableV2
  │                   ├─ DungeonStableRandom 독립 stream
  │                   └─ catalog planning snapshot → 최종 contentKey
  └─ SavedBlueprint
       └─ DungeonBlueprintAsset의 deep copy (재계산 없음)
  → DungeonBlueprintValidator
  → DungeonStageOverrideApplier(선택, SavedBlueprint 전용)
       ├─ source Blueprint 불변
       └─ Disable → Content → Absolute Transform → Add
            → final DungeonBlueprint + source/override/final hash
  → DungeonContentCatalogValidator
  → buildMode
       ├─ RuntimeBuild
       │    └─ DungeonSceneBuilder
       │         ├─ DungeonMeshBuilder
       │         └─ IDungeonContentResolver → Prefab/factory 또는 fallback
       └─ BakedPrefab
            └─ DungeonBakeManifest 검증 → 저장 Prefab 인스턴스
  → DungeonStageInstance + DungeonLayout 호환 projection + GenerationReport

RogueDungeonLabWindow / 스테이지 자산
  → DungeonStageAuthoringService
       ├─ 현재 Blueprint + 일치 레시피 검증 → 새 DungeonBlueprintAsset 또는 Undo 덮어쓰기
       ├─ 저장 레시피 → SerializedObject 설정 복원 → 선택적 동일 입력 절차 재생성
       ├─ 절차 원본 ↔ 저장본 provenance/hash 비교
       ├─ 저장 Blueprint 무재계산 RuntimeBuild 미리보기
       ├─ SavedBlueprint DungeonStageDefinition 생성·Generator 연결
       ├─ DungeonStageOverrideAuthoringService
       │    ├─ Override 생성·Definition 연결·Undo 편집
       │    ├─ Scene 선택 Spawn → Disable/Add/Content/Transform 기록
       │    └─ exact·semantic unique 재결합 분석 → 명시적 승인
       └─ DungeonStageBaker
            ├─ stage 전용 staging → 영속 Mesh·Prefab·manifest
            └─ fingerprint·parity 검증 → commit 또는 rollback

좌클릭 Raycast
  → DestructibleDropTarget
  → WeightedDropTable.Roll
  → DropValidationService
  → Editor Window + Runtime HUD
```

## Blueprint 생성·구축 계약

통합 로드맵의 R0·R1·R2 단계로 다음 데이터 계약과 실행 경로가 Runtime 어셈블리에 추가되었습니다.

```text
RogueDungeonSettings
  → DungeonRecipeSnapshot(원본 비변경 정규화)
  → DungeonGenerationRequest(seed + generatorVersion)

DungeonBlueprint
  ├─ grid / cells / rooms / entrance / exit
  ├─ contentKey 기반 spawn records
  ├─ canonical SHA-256
  └─ DungeonBlueprintValidator
       → 연결성, stable ID, 참조, transform, 저장 해시 검증
```

`DungeonBlueprint`는 GameObject나 Prefab 직접 참조를 포함하지 않으며 `DungeonBlueprintAsset`이 깊은 복사본을 Unity 자산으로 보관할 수 있습니다. R5.1부터 자산 래퍼는 선택적 `authoringRecipeSnapshot`도 별도 깊은 복사로 보존합니다. 이 스냅샷은 입력 복원용 제작 메타데이터이며 Blueprint 논리 데이터와 hash에는 포함되지 않습니다. 제작 메모와 생성 시각도 논리 해시에서 제외되고, 셀·방·spawn·tag 목록은 canonical 정렬 후 해시됩니다.

`RogueDungeonGenerator.GenerateWithSeed`는 `LegacyV1` 요청을 만들고, GameObject가 없는 계산 단계에서 Blueprint를 확정한 다음 `DungeonSceneBuilder`로 씬을 구축합니다. `DungeonLayout`은 기존 HUD·임시 플레이어 호환 projection으로 함께 유지하며, `DungeonMeshBuilder.Build`와 `DungeonContentSpawner.Spawn`의 기존 signature도 wrapper로 남아 있습니다.

Legacy 콘텐츠 planner는 입구·출구, 기믹, 적, 파괴물, cube/cylinder 지형지물에 고정 built-in `contentKey`를 기록합니다. StableV2는 카탈로그의 canonical 후보에서 최종 key를 고르고, 카탈로그 후보가 없으면 같은 built-in key를 기록합니다. 각 root 인스턴스에는 `DungeonSpawnIdentity`가 붙어 stable `spawnId`, key, 범주와 셀을 Blueprint 레코드에 연결합니다. 위치·스케일·세 축 회전은 계획 시점에 확정되므로 같은 Blueprint를 다시 구축할 때 난수를 소비하지 않습니다.

## StageDefinition과 Loader

`DungeonStageLoader`는 `DungeonLoadContext`에서 source와 시드를 해석합니다. Procedural은 `explicitSeed → RunSeed → FixedSeed → RandomPerLoad` 우선순위를 적용하고, SavedBlueprint는 모든 시드 입력과 recipe 변경을 무시한 채 자산을 검증합니다. 선택적 `DungeonStageOverrides`가 있으면 원본을 바꾸지 않는 깊은 복사본에 적용하고, 그 최종 Blueprint를 layout 변환·콘텐츠 검증·구축 입력으로 사용합니다. 실제 사용 시드, 최종 Blueprint, 호환 layout, generated root, 구축 개수와 report는 `DungeonStageInstance`에 함께 기록됩니다.

`RogueDungeonGenerator`에 StageDefinition이 없으면 기존 settings-only `GenerateWithSeed`가 같은 Loader의 Procedural 경로를 사용합니다. Definition이 있으면 Play 시작 또는 컨텍스트 메뉴에서 두 source를 전환할 수 있습니다. `CurrentCellSize`는 활성 Blueprint grid를 기준으로 하므로 저장 맵과 현재 settings의 cellSize가 달라도 임시 플레이어가 올바른 입구에 배치됩니다.

R6에서는 기존 `RuntimeBuild`와 함께 `SavedBlueprint + BakedPrefab`을 로드할 수 있습니다. R7의 `DungeonStageDefinition.stageOverrides`도 SavedBlueprint에서만 허용하며, `Procedural + BakedPrefab`과 `Procedural + Stage Override`는 validator가 차단합니다. Baked 모드에서는 Definition의 Blueprint·Override·Catalog·Prefab과 Runtime-safe `DungeonBakeManifest` 참조가 모두 일치해야 합니다.

## R7 비파괴 Stage Override 계약

### Runtime 데이터와 canonical 적용

`DungeonStageOverrides`는 저장 Blueprint와 분리된 Runtime-safe `ScriptableObject`입니다.

- `baseBlueprint`, `baseBlueprintHash`: 변경 집합이 작성된 정확한 원본과 canonical hash
- `disabledSpawns`: 원본 Spawn을 최종 목록에서 제외
- `addedSpawns`: 사용자가 만든 완전한 `DungeonSpawnRecord`
- `contentOverrides`: 원본 stable ID의 `contentKey`만 교체
- `transformOverrides`: 원본 stable ID의 로컬 위치·세 축 회전·scale을 절대값으로 교체
- `overrideHash`: format, `baseBlueprintHash`와 네 변경 목록의 canonical SHA-256

원본을 참조하는 작업은 stable `spawnId`와 category·기존 content key·cell·room ID·variant seed를 `DungeonSpawnBindingSnapshot`으로 함께 보존합니다. 직접 Unity 자산 참조, 작업 관리용 `recordId`와 제작 메모는 논리 결과에 영향을 주지 않으므로 `overrideHash`에서 제외됩니다. 각 목록과 추가 Spawn의 tag는 ordinal canonical 순서로 기록되므로 Inspector 표시 순서만 바꿔도 hash가 변하지 않습니다.

`DungeonStageOverridesValidator`는 format, 기준 자산·hash, 저장 override hash, 중복 작업 ID·target, 사라진 target, Binding 불일치, 추가 stable ID 충돌과 transform 유효성을 검사합니다. Disable과 Content/Transform이 같은 target에 함께 존재하는 모호한 상태는 오류입니다. Content와 Transform을 같은 target에 함께 적용하는 것은 허용합니다.

입구·출구 `Marker`는 Disable/Add/Content/Transform 대상이 될 수 없습니다. R7 Transform은 위치·회전·scale만 바꾸고 Spawn의 논리 cell·room·progression을 바꾸지 않습니다. 셀과 endpoint 편집은 별도 후속 범위입니다.

`DungeonStageOverrideApplier`는 다음 순서를 지킵니다.

1. 원본 Blueprint와 Override 계약 검증
2. 원본의 깊은 복사본 생성
3. Disable → Content → Absolute Transform → Add 적용
4. 최종 Spawn을 stable ID 순으로 정렬
5. 최종 Blueprint hash 갱신과 `DungeonBlueprintValidator` 재검증

Loader는 이 결과에 `DungeonContentCatalogValidator`를 추가 적용하므로 교체·추가 콘텐츠의 category, progression, room/corridor·tag, footprint·간격과 누락 정책도 기존과 동일하게 검사됩니다. 원본 `DungeonBlueprintAsset`과 그 중첩 목록은 적용·미리보기·Bake 과정에서 수정되지 않습니다.

`DungeonStageInstance`는 실제 소비 데이터인 최종 `Blueprint` 외에도 `AppliedOverrides`, `SourceBlueprintHash`, `OverrideHash`, `FinalBlueprintHash`를 노출합니다. Override가 없는 기존 호출은 빈 override hash와 source와 같은 final hash를 사용하므로 기존 RuntimeBuild 동작을 유지합니다.

### 재결합과 Editor 경계

원본 Blueprint hash가 달라지면 RuntimeBuild 미리보기와 Bake는 자동으로 변경 집합을 옮기지 않고 오류로 중단합니다. `DungeonStageOverrideRebaser.Analyze`는 자산을 변경하지 않은 채 다음 상태를 계산합니다.

1. 같은 stable ID를 먼저 찾고 Binding 의미 anchor가 그대로인지 `Exact` 또는 `ChangedExact`로 구분
2. ID가 사라졌을 때 category·content key·cell·room ID·variant seed가 정확히 일치하는 후보가 하나뿐이면 `UniqueSuggestion`
3. 후보 없음, 복수 후보, 서로 다른 target의 동일 후보 점유 또는 추가 ID 충돌은 미해결 오류

해결 가능한 계획도 자동 commit하지 않습니다. Editor-only `DungeonStageOverrideAuthoringService.CommitRebind`가 승인 직전 계획을 다시 계산해 동일성을 확인하고, 사용자의 확인 뒤 Binding·기준 자산·base hash·override hash를 하나의 Unity Undo 단위로 갱신합니다.

Editor 제작 UI는 generated hierarchy를 원본으로 취급하지 않습니다. 선택한 `DungeonSpawnIdentity`의 stable ID로 Override 자산을 수정하고 전체 RuntimeBuild 미리보기를 다시 구축합니다. 추가 Spawn ID는 Editor에서 한 번 생성한 `override:v1:` GUID를 자산에 보존하며 Runtime applier는 난수나 새 ID를 만들지 않습니다. `SerializedObject`, Undo, `EditorUtility.SetDirty`와 AssetDatabase 저장은 Editor 서비스에만 있고 Runtime의 데이터·hash·validator·applier·rebaser는 `UnityEditor`를 참조하지 않습니다.

## R5.2 런타임 레시피 격리와 R6·R7 Bake 계약

### 현재 R5.2 구현

Play의 Generator는 settings-only 레시피와 Procedural StageDefinition 레시피를 프로젝트 자산 그대로 편집하지 않고 `HideAndDontSave` 런타임 복제본으로 사용합니다. 활성 StageDefinition source가 바뀌거나 Generator가 해제될 때 복제본의 소유권도 Generator 수명주기에 맞춰 교체·해제합니다. 새 source의 복제본은 후보 상태로 Loader에 전달하고 구축 성공 뒤에만 활성 소유권으로 승격합니다. 검증·구축이 실패하면 후보만 폐기하고 기존 StageInstance, generated root와 활성 복제본을 유지합니다. Generator는 현재 복제본을 `ActiveRuntimeSettings`, 편집 가능 여부를 `CanEditActiveRuntimeRecipe`로 노출하고 Procedural StageDefinition 로드는 `DungeonLoadContext.ProceduralRecipeOverride`로 같은 복제본을 전달합니다. Runtime HUD는 이 활성 복제본을 편집하고 같은 시드로 재생성하므로 Play 중 구조·밀도 변경은 실제 Procedural Blueprint에 반영되지만 원본 `RogueDungeonSettings` 자산은 바뀌지 않습니다. SavedBlueprint는 계속 저장 논리 데이터를 무재계산 로드하며 HUD 설정 편집으로 Blueprint를 변형하지 않습니다.

R5.2는 R6이 사용할 두 Runtime 타입도 먼저 고정했고 R7이 기존 manifest를 버전 확장했습니다.

- `DungeonBakeManifest`: source/final Blueprint, planning/realization/gameplay/material/override hash, builder version, baked Prefab과 Baker 소유 파생 자산 레코드를 직렬화하는 Runtime-safe `ScriptableObject`
- `DungeonBakeMaterialSet`: floor·wall과 built-in 콘텐츠 범주가 사용할 영속 `Material` 참조를 직렬화하는 Runtime-safe `ScriptableObject`
- `DungeonBakeManifestValidator`: `UnityEditor` 없이 format/builder version, 저장 Blueprint·선택적 Override 무결성, custom catalog, source/final·필수 의존성 hash, 완전한 재질 슬롯, Prefab 참조와 고유 owned artifact를 검사하는 기본 검증기

`DungeonBakeManifest`와 `DungeonBakeMaterialSet`은 Runtime 어셈블리에 있으므로 `DungeonStageDefinition`, Loader와 Player build가 직접 참조할 수 있습니다. Asset GUID/dependency hash 계산, `AssetDatabase`, `PrefabUtility`, 저장 경로와 Undo는 여기에 들어가지 않습니다.

### R6·R7 구현 경계

Editor-only `DungeonStageBaker`는 다음 동작을 담당합니다.

- 저장된 SavedBlueprint에 검증된 Override를 적용한 최종 Blueprint에서 floor/wall Mesh와 known built-in/fallback·Catalog 직접 Prefab을 영속 자산으로 생성
- Prefab·Mesh·Material·drop table 의존성 fingerprint와 manifest 필드 계산
- stage 전용 staging/commit, 실패 rollback과 이전 manifest 소유 파생 산출물 정리
- 제작 UI의 Bake/재Bake와 `ValidateCurrentBake` 최신성 리포트

Runtime의 StageDefinition validator와 Loader는 `SavedBlueprint + BakedPrefab`의 저장 manifest·Prefab·Override 관계를 검사하고 Prefab 인스턴스화 경로를 사용합니다. Baked Loader는 순수 Override applier로 최종 논리 Blueprint와 layout·report 출처를 복원하지만 Blueprint 생성기, Mesh Builder와 resolver는 재실행하지 않습니다. manifest에 기록된 `ownedArtifacts`만 Baker 소유로 보며 Blueprint·Override·settings·catalog, Catalog Prefab과 공유 Mesh·Material은 정리 대상에서 제외합니다.

RuntimeBuild/BakedPrefab parity, 실패 주입 재Bake와 BakedPrefab Player build smoke를 R6 통합 검증으로 실행했습니다. Unity `6000.5.3f1`에서 EditMode `74/74`, PlayMode `8/8`과 분리된 임시 프로젝트의 Windows Development Player 빌드가 통과했으며 상세 결과는 테스트 문서에 기록합니다.

R7 통합 검증에서는 전용 Override와 v2 Bake 검증 장면을 생성하고 장면 재개방 뒤 RuntimeBuild/BakedPrefab의 final hash와 stable identity parity를 다시 확인했습니다. 같은 Unity 버전에서 전체 EditMode `83/83`, PlayMode `9/9`, 경고 `0`개의 Windows64 Development Player 빌드가 통과했으며 빌드 크기는 `172,176,046 B`입니다. 실제 HUD/Scene 육안과 Play 중 script/domain reload는 자동 검증이 아닌 수동 확인 범위로 유지합니다.

## R5 저장 제작 계약

`DungeonStageAuthoringService`는 Editor 어셈블리에만 존재하며 `DungeonBlueprintValidator`와 `DungeonContentCatalogValidator`가 오류를 보고한 데이터의 저장·미리보기·StageDefinition 생성을 차단합니다. 새 저장은 현재 Blueprint를 깊은 복사하고 `createdUtcTicks`와 `authoringNote`를 기록한 뒤 canonical hash를 다시 확정합니다. 제작 메모와 저장 시각은 논리 hash에서 제외됩니다.

기존 자산 덮어쓰기는 전체 `DungeonBlueprintAsset`을 `Undo.RegisterCompleteObjectUndo`로 기록하므로 중첩된 cell·room·spawn 목록까지 복구할 수 있습니다. StageDefinition 생성과 Generator 연결은 `SerializedObject`를 사용하고 `EditorUtility.SetDirty`, AssetDatabase 저장과 씬 dirty 처리를 수행합니다.

비교 상태는 다음처럼 분류합니다.

- `Identical`: canonical Blueprint 결과가 같음
- `DifferentSeed`: recipe·catalog·generatorVersion은 같고 seed만 다름
- `StaleInputs`: recipe, catalog 또는 generatorVersion이 현재 원본과 다름
- `Diverged`: provenance와 seed가 같은데 논리 결과가 달라 알고리즘/무결성 점검 필요
- `InvalidCurrent`·`InvalidSaved`: 코드 기반 검증 오류로 비교와 제작 차단

저장본 미리보기는 `DungeonStageLoader.LoadSavedBlueprint`를 사용합니다. 이 경로는 asset의 깊은 복사본을 검증해 RuntimeBuild하며 recipe나 새 seed로 Blueprint를 재계산하지 않습니다. 에디터의 `현재 절차 설정으로 재생성`은 미리보기 전 기억한 seed로 Procedural StageDefinition 또는 기존 settings facade를 다시 실행하며 설정값을 복구하지 않는다는 의미로 이름을 명확히 했습니다. 생성된 StageDefinition은 `SavedBlueprint + RuntimeBuild`를 직렬화하므로 새 씬에서도 같은 Loader 경로를 사용합니다.

## R5.1 저장 레시피 복원 계약

새 저장과 덮어쓰기는 현재 Blueprint의 `recipeHash`와 실제 연결 설정에서 캡처한 `DungeonRecipeSnapshot.ComputeHash()`가 같을 때만 레시피를 함께 보존합니다. 저장 레시피는 별도 깊은 복사이며 구조 수치, 세 밀도 프로필과 AnimationCurve의 키·접선·가중치·wrap mode를 포함합니다. 설정 snapshot을 제공하지 않은 기존 API 호출과 기존 R5 자산은 `HasAuthoringRecipeSnapshot == false`로 유지되어 SavedBlueprint 로드 결과가 바뀌지 않습니다.

`DungeonStageAuthoringService.ValidateStoredRecipe`는 snapshot format, Blueprint `recipeHash` 일치와 현재 `ClampValues` 규칙으로 재캡처 가능한 canonical 값인지 검사합니다. 실패한 snapshot은 설정 적용만 차단하며 저장 Blueprint 미리보기·RuntimeBuild 자체에는 관여하지 않습니다.

`ApplyStoredRecipeToSettings`는 `SerializedObject`와 전체 Undo를 사용해 생성 필드만 덮어씁니다. 시드는 사용자가 저장 시드 적용을 선택한 경우에만 바뀌며, drop table, drop marker, 통계 초기화와 `generateOnPlay`는 보존합니다. 정확 재생성은 실제 설정 변경 전에 저장 snapshot·seed·generatorVersion·catalog로 Blueprint를 계산해 저장 hash와 비교합니다. 검증 성공 뒤 `DungeonStageLoader.LoadProcedural`의 명시 버전 overload와 `RogueDungeonGenerator.GenerateProcedural`을 사용하므로 LegacyV1과 StableV2를 모두 재현합니다.

## 콘텐츠 카탈로그와 planning hash

`DungeonContentCatalog` entry는 다음 정보를 직렬화합니다.

- 대소문자를 구분하는 고유 `contentKey`, `DungeonSpawnCategory`, Prefab
- 양수 weight와 양 끝을 포함하는 progression 범위
- `Any`·`RoomOnly`·`CorridorOnly` 배치 조건과 필수 room tag
- 셀 footprint, 최소 간격, 무작위 yaw 범위, 균일 scale 범위
- 선택적 `WeightedDropTable`과 게임 측 `gameplayId`

생성 요청은 catalog를 직접 계속 읽지 않고 `DungeonContentCatalogPlanningSnapshot`을 깊은 복사합니다. planning hash에는 key, category, weight, progression, 배치 조건, tag, footprint·간격과 변형 규칙만 들어갑니다. Prefab, drop table, gameplay ID 같은 표현·게임 연계 참조는 제외됩니다. entry와 tag는 원본 목록을 수정하지 않은 채 ordinal canonical 순서로 기록되므로 Inspector 재정렬과 요청 생성 뒤 원본 자산 변경이 이미 캡처된 요청을 바꾸지 않습니다. Generator는 실행 직전에 snapshot 필드 검증과 hash 재계산을 반복해 요청 객체의 사후 변조도 거부합니다.

Catalog validator는 `RDL-CAT-*` 코드로 빈/중복 key, 예약 built-in key의 범주, 잘못된 범주·weight·progression·footprint·간격·변형을 검사하고, Blueprint 교차 검증은 `RDL-CONTENT-*` 코드로 key/category/progression/실제 floor cell 기준 room 조건/tag/footprint와 planning hash 차이를 검사합니다. StableV2 저장 Blueprint와 현재 catalog의 planning hash 차이는 경고로 남습니다.

planning hash는 Blueprint 생성 결정성의 입력이지 Bake 최신성의 전체 증명이 아닙니다. R5.2의 manifest 계약은 다음 후반 의존성을 분리합니다.

| Manifest 값 | 의미 |
|---|---|
| `sourceBlueprintHash` | 저장 원본 Blueprint |
| `overrideHash` | 적용한 비파괴 변경 집합. format v1과 v2의 Override 없는 Bake에서는 빈 값, v2 Override Bake에서는 canonical hash |
| `finalBlueprintHash` | Override 적용 뒤 실제 구축한 논리 결과. v1과 v2의 Override 없는 Bake에서는 source와 동일 |
| `catalogPlanningHash` | 후보 key 선택에 사용한 출처 |
| `contentRealizationHash` | 최종 key를 해석한 resolver 버전, 직접 Prefab identity·hierarchy·component와 source Mesh 의존성 |
| `gameplayBuildConfigHash` | 누락 정책, gameplay ID, canonical drop table과 drop marker 구축 설정 |
| `materialDependencyHash` | 영속 Bake material set, Prefab Renderer Material과 Shader 의존성 |
| `builderVersion` | hierarchy, Collider, component와 생성 Mesh 포맷 규칙 |

Editor-only `DungeonStageBaker`는 위 값을 계산해 Runtime manifest에 문자열·자산 참조로 저장합니다. R6 format/builder v1은 `sourceOverrides == null`, 빈 `overrideHash`, source와 같은 final hash 계약으로 계속 지원합니다. 새 Baker는 Stage Override를 표현할 수 있는 format/builder v2를 기록합니다. v2도 Override가 없으면 빈 hash와 source=final을 요구하고, Override가 있으면 정확한 `sourceOverrides` 참조·canonical hash·순수 적용 결과의 final hash가 모두 일치해야 합니다.

하나의 광범위한 Asset dependency hash를 planning 또는 realization 값으로 재사용하지 않고, 최종 Blueprint의 Spawn 실현, Prefab 구조·gameplay 직렬화 값·Material/Shader를 각 필드의 canonical projection으로 분리합니다. Runtime은 `AssetDatabase`로 재계산하지 않고 지원되는 v1/v2 형식, builder version과 저장된 참조 관계를 검사합니다. `DungeonStageBaker.ValidateCurrentBake`는 현재 source·Override·final과 프로젝트 의존성을 다시 fingerprint해 모든 값을 비교하므로 제작 UI의 stale 판정에 사용합니다.

## StableV2 결정성

`StableV2`는 프로젝트가 소유한 PCG32 구현과 고정 seed 파생 공식을 사용합니다. 난수 stream은 `Layout`, `Gimmick`, `Enemy`, `Destructible`, `Prop`, `Variant`로 분리되며 spawn별 변형은 범주·셀·범주 내 순번으로 만든 child stream을 사용합니다. 콘텐츠 배치 우선순위는 marker 확정 후 `Gimmick → Enemy → Destructible → Prop`으로 고정되어 있습니다. 한 범주의 후보 key나 난수 호출 수를 바꿔도 뒤 범주의 stream 출력은 밀리지 않습니다.

`StableV2`는 opt-in입니다. `DungeonGeneratorVersions.Current`, 기존 `RogueDungeonGenerator.GenerateWithSeed`와 settings-only loader facade는 계속 `LegacyV1`을 사용합니다. Stage Definition의 `generatorVersion`을 `StableV2`로 지정하거나 `DungeonGenerationRequest.CreateStableV2`를 호출해야 새 경로가 선택됩니다.

## Resolver와 구축 정책

`DungeonSceneBuilder`는 확정된 spawn을 stable 순서로 정렬하고 먼저 `IDungeonContentResolver.TryResolve`를 호출합니다. 기본 `DungeonPrefabContentResolver`는 catalog의 ordinal key를 직접 Prefab으로 연결하며, `DungeonContentResolution`은 Prefab 또는 프로젝트 factory와 선택적 drop table·gameplay ID를 보관할 수 있습니다. 해석된 오브젝트에도 Blueprint transform과 `DungeonSpawnIdentity`가 적용됩니다. 기존 Prefab target의 ID·drop table은 우선 보존하고, target이 없거나 필드가 비어 있을 때 catalog·런타임 기본값으로 보강합니다.

Catalog나 resolver가 반환하지 않은 알려진 built-in key는 누락 정책과 무관하게 기존 primitive로 구축됩니다. 그 밖의 해석 실패에는 `Error`가 생성 부작용 전에 중단하고, `BuiltInFallback`이 category별 임시 표현을 만들며, `Skip`이 경고와 함께 해당 spawn을 생략합니다. `DungeonLoadContext.ContentResolver`와 `MissingContentPolicyOverride`를 사용하면 Stage Definition 자산을 바꾸지 않고 실행별 resolver와 정책을 주입할 수 있습니다.

R6·R7 Baker는 이 RuntimeBuild resolver 전체를 Bake하려 하지 않습니다. Editor에서 결정적으로 재현할 수 있는 known built-in 표현과 직접 `DungeonContentCatalog` Prefab만 지원합니다. built-in floor·wall·범주 표현은 `DungeonBakeMaterialSet`의 영속 Material을 사용하며 현재 `HideAndDontSave` 미리보기 Material을 저장하지 않습니다. factory, Addressables, DI·오브젝트 풀 resolver는 RuntimeBuild 전용이고, 별도 Editor bake adapter 계약이 생기기 전에는 Bake를 오류로 차단합니다.

재Bake는 활성 자산을 직접 덮는 방식이 아니라 stage 전용 staging에서 후보 Mesh·Prefab·manifest를 만든 뒤 source·Override·final hash와 parity 검증에 성공한 결과만 commit합니다. commit 뒤 정리는 이전 manifest의 정확한 role·GUID, 예약 파일명, main asset과 stage bake root를 모두 만족하는 파생 자산에만 적용합니다. 실패하면 staging만 제거하고 이전 정상 manifest와 Prefab 참조를 유지합니다. 이전 파생 파일 삭제는 Unity Undo로 복구할 수 없으므로 commit 확인 뒤 StageDefinition의 해당 Undo 기록도 제거해 삭제된 참조가 되살아나는 상태를 막습니다. 사용자 소유 Stage Override 자산은 보호 참조에 포함되며 Baker 소유 산출물로 삭제하지 않습니다.

RuntimeBuild/BakedPrefab parity는 동일 source·Override에서 계산한 최종 Blueprint hash, 입구·출구, floor 셀과 floor/wall Collider, `DungeonSpawnIdentity`의 stable ID·category·contentKey·transform, 클릭 대상 gameplay ID·drop table, drop marker 정책과 구축 report를 비교합니다. BakedPrefab 로드는 생성기·Mesh Builder·resolver를 재실행하지 않고 Prefab을 인스턴스화하지만 `__RogueDungeonLab_Generated`, `DungeonStageInstance`와 `GenerationCompleted`의 소비자 계약은 유지해야 합니다.

에디터의 `스테이지 자산` 탭은 저장된 `SavedBlueprint StageDefinition`, 사용자 소유 `DungeonBakeMaterialSet`과 선택적 gameplay settings를 Baker 입력으로 전달합니다. gameplay 입력은 현재 Generator의 활성 settings, 기존 manifest의 `sourceRuntimeSettings`, Generator 원본 settings 순으로 해결하므로 재Bake는 기존 출처를 우선 보존하고 SavedBlueprint 첫 Bake도 장면 설정을 사용할 수 있습니다. 기본 재질 세트 생성 도구는 입력 자산을 명시적으로 만들 뿐 Baker 소유 산출물로 등록하지 않습니다.

commit된 StageDefinition은 저장 Blueprint와 함께 Baked Prefab·manifest를 참조하고 `BakedPrefab` build mode를 사용합니다. Loader는 Runtime-safe manifest 검증 뒤 Prefab만 새 비활성 generated root 후보로 인스턴스화하고, 성공한 후보를 기존 root와 교체합니다. floor/wall Mesh는 프로젝트 자산이므로 `DungeonGeneratedMeshOwner`의 transient Mesh 해제 대상이 아닙니다.

레이아웃 단계는 GameObject를 만들지 않습니다. 방은 겹침 없는 사각형으로 배치하고 가장 가까운 미연결 방을 L자 복도로 연결한 뒤 확률적으로 루프를 추가합니다.

진행도는 `distanceFromEntrance / distanceToExit`이며, 각 밀도 프로필은 기본 셀 확률 × 진행도 곡선 × 방/복도 보정 × 결정적 군집 보정으로 평가됩니다. LegacyV1은 기존 Perlin 계산을 유지하고 StableV2는 셀 기반 child stream 값을 사용합니다.

바닥은 셀당 quad, 벽은 floor-to-void 경계 box를 각각 하나의 합성 메시로 만듭니다. 클릭 대상만 개별 GameObject입니다. Loader는 새 root를 비활성 상태에서 완성한 뒤 이전 root를 비활성·정리하고 교체본을 활성화합니다. 합성 Mesh는 이름 추측 대신 `DungeonGeneratedMeshOwner`가 기록한 정확한 참조만 해제하므로 Prefab의 공유 Mesh는 건드리지 않습니다.

합성 바닥·벽에는 정적 `MeshCollider`를 함께 생성합니다. 선택 Lab Sample의 HUD가 만드는 `PrototypePlayerController`는 런타임 전용 `CharacterController`를 사용하며, 입구 위치 생성·카메라 기준 이동·중력·점프·추락 복귀를 담당합니다. `LabOrbitCamera`는 캐릭터가 없을 때 카메라의 실제 정면·오른쪽 축으로 `WASD`를 처리하므로 `W/S` 이동에는 시선의 높이 성분도 포함됩니다. `Space`/`Ctrl`은 별도의 월드 수직 이동입니다. 자유 시점 우클릭 회전은 카메라 위치를 고정하고 회전 중심을 재계산하며, 캐릭터가 활성화되면 기존처럼 해당 Transform을 중심으로 공전 추적합니다. 이 Sample 흐름도 `UnityEditor`를 참조하지 않지만 Input System은 Sample assembly에만 한정됩니다.

`RuntimeLabHUD`는 설정·탐험·런 상태·통계를 탭으로 분리합니다. 설정 탭은 Generator가 제공하는 활성 `HideAndDontSave` 런타임 설정 복제본의 구조·콘텐츠 수치에 바인딩합니다. settings-only와 Procedural StageDefinition은 각각 해당 원본에서 만든 복제본을 사용하며, SavedBlueprint 상태에서는 구조·밀도 편집과 자동 절차 재생성을 제공하지 않습니다. 슬라이더와 프리셋 변경은 다음 `Update`에 요청 하나로 합쳐지고 0.08초 제한 주기로 `ClampValues`와 `RegenerateActiveSeed`를 호출하므로, 원본 자산과 결정적 시드는 유지하면서 드래그 중 결과를 갱신합니다. 시드 텍스트만 명시적인 생성 버튼에서 확정합니다. 패널은 기준 해상도에 대한 제한 배율과 화면 비율별 논리 영역을 계산하며, 실제 픽셀 영역을 카메라·클릭 입력 차단에도 동일하게 사용합니다. 각 탭 내용은 독립적으로 접근 가능한 스크롤 영역 안에 배치됩니다.

드랍 대시보드는 기대 확률, 관측 확률, 편차와 Wilson 95% 신뢰구간을 계산합니다. 테이블 정의가 바뀌면 이전 표본을 새 기대값과 비교하지 않도록 해당 통계를 초기화합니다.

## R8 RunState 계약

R8은 R7 `DungeonStageOverrides`와 소유권을 분리합니다.

- Override: 제작자가 저장 Blueprint에 붙이는 정적·배포 가능한 변형
- RunState: 플레이 세션에서 발생한 제거·기믹·플레이어 진행

`DungeonRunState`는 Unity Object를 참조하지 않는 versioned DTO입니다. canonical hash에는 `formatVersion`, `stageId`, source mode, run seed, final Blueprint hash, ordinal 정렬한 제거 spawn ID, spawn ID·participant key·payload로 정렬한 기믹 상태와 stage-local 플레이어 pose를 포함합니다. 저장 시각과 기존 `stateHash`는 hash 입력에서 제외합니다.

`DungeonRunStateTargetFactory`는 현재 StageInstance의 final Blueprint와 stable spawn 범주를 migration·검증용 target으로 만듭니다. 명시적인 `DungeonStageDefinition.stageId`를 우선하고, 기존 자산의 fallback은 SavedBlueprint source hash 또는 seed와 분리한 Procedural generator version·recipe hash·catalog planning hash입니다. 새 제작 Definition에는 영구 GUID 형식 ID를 부여합니다.

검증과 적용 순서는 다음과 같습니다.

1. 상태 형식·중복·유한 pose·저장 hash 검사
2. stage ID, source mode, run seed와 final Blueprint hash 비교
3. 제거 대상이 Enemy/Destructible, payload 대상이 Gimmick인지 확인
4. 후보 hierarchy의 `DungeonSpawnIdentity` 중복과 `IDungeonRunStateParticipant.RunStateKey` 확인
5. participant payload 복원
6. 제거 대상을 활성화 전에 비활성화·파괴
7. 성공한 후보만 기존 `__RogueDungeonLab_Generated`와 교체

RuntimeBuild는 `DungeonSceneBuilder.Build` 직후의 비활성 root, BakedPrefab은 manifest 검증 뒤 아직 staging 아래에 있는 비활성 복제 root에 같은 `DungeonRunStateApplier`를 실행합니다. 오류나 participant 예외가 있으면 후보 root만 폐기하므로 기존 StageInstance와 generated root는 유지됩니다. `DungeonStageInstance.RunStateApplyResult`는 target, 실제 재결박된 상태, migration/best-effort 여부와 적용 개수를 기록합니다.

기본 `Reject` 정책은 네 fingerprint가 모두 정확히 같아야 합니다. `ApplyMatchingSpawnIds`는 stage·source·seed가 같은 상태에서 final hash 불일치만 경고로 낮추고 현재 target과 scene에 존재하는 ID만 남긴 새 상태로 재결박합니다. `IDungeonRunStateMigrator`가 제공되면 불일치나 이전 format을 명시적으로 변환한 뒤 기본 엄격 정책으로 결과를 다시 검증합니다.

`RogueDungeonGenerator`는 활성 상태를 소유합니다. `DestructibleDropTarget`이 실제 파괴 직전에 `RecordSpawnRemoved`를 호출하며, 외부 전투 시스템도 같은 API를 호출해야 합니다. 저장 시 현재 Gimmick 하위 participant와 등록된 `IDungeonRunStatePlayer` pose를 캡처합니다. Lab Sample의 `PrototypePlayerController`는 이 인터페이스를 구현하며, 복원된 저장 pose를 일반 입구 이동보다 우선합니다.

`IDungeonRunStateStore`는 `Save`, `TryLoad`, `Delete`, `Exists`만 정의합니다. 기본 JSON 구현은 영숫자·하이픈·밑줄 슬롯을 `persistentDataPath` 아래에 저장하고, 임시 파일을 UTF-8로 flush한 뒤 기존 파일이 있으면 `File.Replace`로 교체합니다. parse·canonical hash가 손상된 파일은 Loader에 전달하지 않습니다. 테스트나 제품 저장 계층은 메모리·계정·클라우드 구현을 주입할 수 있습니다.

## R9 assembly와 배포 경계

R9는 기능 단위가 아니라 제품 포함 여부를 기준으로 assembly를 나눕니다.

| assembly | 플랫폼 | 역할 | 외부 package |
|---|---|---|---|
| `RogueDungeonLab.Runtime` | Editor/Player | 생성, Blueprint, RuntimeBuild/Baked Loader, Override, RunState | 없음 |
| `RogueDungeonLab.Samples` | Editor/Player | Lab HUD, 카메라, 클릭 입력, 임시 플레이어 | `Unity.InputSystem` |
| `RogueDungeonLab.Editor.Baking` | Editor | Mesh·Prefab Bake와 전체 fingerprint 검증 | 없음 |
| `RogueDungeonLab.Editor.Packaging` | Editor | package 계획, dependency closure, sidecar와 export | Baking + Runtime |
| `RogueDungeonLab.Editor` | Editor | 기존 통합 실험실/제작 UI | 위 assembly 전체 |

기존 Sample MonoBehaviour 파일은 `.meta` GUID를 유지한 채 이동했으므로 원본 프로젝트의 장면·Prefab 참조는 보존됩니다. Core는 Sample 구체 타입 대신 `IDungeonRunStatePlayer`만 알고, Sample 플레이어가 등록/해제를 담당합니다. 따라서 제품이 Core만 가져오면 Input System과 Lab HUD가 assembly나 Player 장면에 들어오지 않습니다.

`DungeonDistributionExporter`는 정렬된 자산 목록을 기반으로 Runtime Core, Runtime Examples, Lab Sample, Bake Authoring과 Baked Stage 계획을 만듭니다. Baked 계획은 StageDefinition에서 Blueprint·Override·Catalog·settings·material set·Prefab·manifest 소유 산출물까지 `AssetDatabase` dependency closure를 수집합니다. modular 계획은 Core를 제외하고 standalone 계획은 같은 Core 경로를 병합합니다.

package shader는 `AssetDatabase.GetDependencies`만으로 누락될 수 있어 Material과 Renderer의 Shader 경로도 별도로 검사합니다. `com.unity.modules.*`는 Unity 기본 모듈로 처리하고, 그 밖의 package는 sidecar `requiredPackages`에 설치 버전을 기록합니다. MaterialSet 또는 Prefab이 Built-in·URP·HDRP·custom 파이프라인을 혼합하면 배포 전 오류로 중단합니다.

Editor main UI와 Tests는 어떤 제품 Baked Stage dependency에도 허용하지 않습니다. 생성된 `.unitypackage.json`은 package 파일 SHA-256, Unity 버전, asset path, stage/source/final/override hash와 render pipeline을 인계 계약으로 보존합니다.

## Unity 6000.5 직렬화

역할별 Runtime 파일의 공개 API와 로직은 유지하되, `MonoBehaviour`와 `ScriptableObject`마다 타입명과 같은 `partial` 연결 파일을 둡니다. Unity가 안정적인 `MonoScript` 자산을 생성하므로 장면 저장, 설정 에셋, Play/Edit 전환과 도메인 재로드 뒤에도 참조가 유지됩니다.

드랍 정의는 항목 정규화 후 해시를 계산합니다. 내부 정규화를 사용자 편집으로 오인해 첫 통계 표본을 초기화하지 않습니다.

R5.1 자산 회귀는 Blueprint·선택 레시피와 StageDefinition을 프로젝트에 저장한 뒤 강제 재임포트해 중첩 데이터와 자산 참조가 유지되는지 검사합니다. 설정 적용·Undo, 기존 snapshot 없는 자산, 손상 snapshot 차단과 StableV2 동일 hash 재생성도 자동 검증합니다. 실제 Unity 프로세스 완전 종료·재시작은 수동 검증으로 별도 유지합니다.
