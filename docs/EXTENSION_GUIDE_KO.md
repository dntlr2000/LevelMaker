# 제품화 확장 가이드

아래 항목의 통합 데이터 계약, 의존 순서와 단계별 완료 조건은 [스테이지 제작·배포 통합 로드맵](STAGE_PIPELINE_ROADMAP_KO.md)에 정리되어 있습니다.

## 현재 제공되는 프로젝트 연계 지점

- `DungeonContentCatalog`에서 key/category/Prefab, weight, progression, 방/복도 조건, footprint·간격과 변형 규칙을 정의할 수 있습니다.
- `StableV2`는 catalog planning snapshot을 사용하며, Inspector 순서와 요청 이후 원본 자산 변경에 독립적입니다.
- `DungeonPrefabContentResolver`는 직접 Prefab 참조를 사용합니다. `IDungeonContentResolver`의 factory resolution으로 DI·오브젝트 풀·프로젝트별 생성 경로를 연결할 수 있습니다.
- `DungeonLoadContext.ContentResolver`와 `MissingContentPolicyOverride`를 사용하면 Stage Definition 자산을 수정하지 않고 한 번의 로드만 교체할 수 있습니다.
- 기존 settings-only facade는 LegacyV1을 유지합니다. 제품에서 StableV2로 전환할 때는 Stage Definition의 생성기 버전을 명시적으로 변경하고 새 golden baseline을 승인합니다.
- `DungeonStageAuthoringService`는 검증된 현재 결과와 선택적 레시피의 Blueprint 저장·Undo 덮어쓰기, 저장 레시피 검증·설정 복원·정확 재생성, stale 비교·저장본 미리보기와 SavedBlueprint StageDefinition 생성을 제공합니다.
- `DungeonStageLoader.LoadSavedBlueprint`는 Editor 미리보기와 프로젝트 도구가 저장 데이터를 recipe·seed 재계산 없이 구축할 수 있는 Runtime facade입니다.
- `DungeonStageLoader.LoadSavedBlueprint`의 마지막 선택 인자인 `DungeonStageOverrides`를 사용하면 원본 Blueprint를 바꾸지 않고 검증된 Spawn 변경을 같은 RuntimeBuild 경로에 적용할 수 있습니다.
- `DungeonStageLoader.LoadProcedural`의 명시 버전 overload와 `RogueDungeonGenerator.GenerateProcedural`은 StageDefinition 없이도 LegacyV1 또는 StableV2, catalog와 누락 정책을 지정해 제작 도구의 재현 미리보기를 구축합니다.
- `DungeonBlueprintAsset.TryGetAuthoringRecipeSnapshot`은 내부 직렬화 상태를 공유하지 않는 깊은 복사본만 반환합니다. 이 데이터는 제작 편의를 위한 선택 메타데이터이므로 소비 프로젝트는 SavedBlueprint 로드의 필수 조건으로 취급하지 않습니다.
- Play의 settings-only 또는 Procedural StageDefinition 레시피는 Generator가 소유하는 `HideAndDontSave` 복제본으로 편집합니다. 외부 런타임 UI도 `RogueDungeonGenerator.ActiveRuntimeSettings`와 `CanEditActiveRuntimeRecipe`를 사용해야 하며, 원본 `settings`나 StageDefinition `recipe`를 직접 변경하지 않습니다. Procedural StageDefinition 로드는 `DungeonLoadContext.ProceduralRecipeOverride`로 같은 복제본을 전달합니다.
- Runtime-safe `DungeonBakeManifest`와 `DungeonBakeMaterialSet`, Editor-only `DungeonStageBaker`를 사용해 저장 Blueprint를 영속 Mesh·Prefab으로 Bake할 수 있습니다. BakedPrefab Loader는 런타임 생성·Mesh 구축·resolver를 다시 실행하지 않습니다.
- Runtime-safe `DungeonStageOverrides`, `DungeonStageOverridesValidator`, `DungeonStageOverrideApplier`와 `DungeonStageOverrideRebaser`는 별도 제작 UI 없이도 프로젝트 도구가 Override를 검증·합성하고 새 원본의 재결합 계획을 분석할 수 있는 데이터 API입니다.

`dropTable`과 `gameplayId`는 catalog entry에 보존되지만 planning hash에는 포함되지 않습니다. 기본 Prefab resolver는 이 값을 SceneBuilder에 전달합니다. Prefab에 `DestructibleDropTarget`이 없으면 root에 추가해 catalog 값을 적용하고, 이미 target이 있으면 Prefab에 작성한 ID·드랍 테이블을 우선하며 비어 있는 값만 catalog·런타임 설정으로 보강합니다. 프로젝트별 resolver도 같은 resolution metadata를 반환할 수 있습니다. R4 절차 생성기는 아직 room tag를 배정하지 않으므로 tag가 필요한 entry는 후속 room metadata 단계 전까지 절차 선택 후보가 아닙니다.

## R6 Bake 연계 계약

`catalogPlanningHash`는 콘텐츠 key 선택만 고정합니다. Prefab, Mesh, Material, `dropTable`, `gameplayId`, 누락 정책과 drop marker 구축 설정은 별도의 `contentRealizationHash`, `gameplayBuildConfigHash`, `materialDependencyHash`로 fingerprint해야 합니다. `sourceBlueprintHash`, `finalBlueprintHash`와 `builderVersion`까지 모두 맞아야 최신 Bake입니다. R6 format v1은 Override를 지원하지 않으므로 `overrideHash`가 비어 있지 않으면 validator가 차단하며, R7에서 형식·builder 버전과 함께 활성화합니다.

R6 MVP에서 Bake할 수 있는 resolver는 다음 두 종류입니다.

1. `DungeonBakeMaterialSet`의 영속 Material을 사용하는 known built-in/fallback 표현
2. `DungeonContentCatalog`가 직접 참조하는 Prefab

Runtime factory, Addressables, DI나 오브젝트 풀 resolver는 계속 RuntimeBuild에서 사용할 수 있지만 R6 MVP Bake 입력으로는 사용할 수 없습니다. 프로젝트가 이를 Bake해야 할 때는 `UnityEditor` 영역의 명시적 bake adapter를 제공하고 adapter 버전과 의존성을 `contentRealizationHash`에 포함해야 합니다. 런타임 resolver 실패를 Baker가 임의 primitive로 대체해서는 안 됩니다.

Editor Baker는 stage 전용 staging 영역에 Mesh·Prefab·manifest 후보를 만들고 검증 성공 뒤에만 활성 StageDefinition 참조를 교체합니다. 파생 산출물의 소유권은 manifest의 `ownedArtifacts`에 기록된 GUID와 stage bake root를 함께 만족할 때만 인정합니다. Blueprint·settings·catalog, Catalog Prefab과 공유 Mesh·Material은 입력 자산이므로 정리하지 않습니다. 실패한 Bake는 staging만 제거하고 기존 정상 Prefab·manifest를 유지해야 합니다.

RuntimeBuild와 BakedPrefab은 같은 최종 Blueprint hash, 입구·출구·floor/Collider, spawn ID·category·contentKey·transform, 클릭/drop과 구축 report를 가져야 합니다. BakedPrefab은 이를 만족하면서 런타임 Blueprint 생성, Mesh Builder와 resolver를 실행하지 않는 배포 경로입니다.

## R7 Stage Override 연계 계약

`DungeonStageOverrides`는 하나의 Saved `DungeonBlueprintAsset`과 그 canonical `baseBlueprintHash`를 기준으로 합니다. 다음 네 가지 Spawn 변경만 기록합니다.

- 원본 Spawn 비활성화
- 수동 Spawn 추가
- 원본 Spawn의 콘텐츠 key 교체
- 원본 Spawn의 절대 로컬 위치·회전·크기 교체

입구·출구 Marker와 cell·room·floor 구조는 R7 편집 대상이 아닙니다. 추가 Spawn은 원본 Blueprint의 floor cell을 참조하고 충돌하지 않는 `override:v1:<guid>` stable ID를 가져야 합니다. Override operation 목록 표시 순서와 제작 메모는 canonical `overrideHash`를 바꾸지 않으며, applier는 원본의 깊은 복사본에만 작업을 적용해 별도의 `finalBlueprintHash`를 계산합니다.

Runtime 경로에서 직접 적용할 때는 다음처럼 호출할 수 있습니다.

```csharp
DungeonStageOverrideApplyResult applied =
    DungeonStageOverrideApplier.Apply(savedBlueprint, stageOverrides);

if (!applied.IsValid)
{
    // applied.ValidationReport의 code 기반 오류를 사용자에게 표시합니다.
}

DungeonStageInstance instance = DungeonStageLoader.LoadSavedBlueprint(
    parent,
    savedBlueprint,
    runtimeSettings,
    contentCatalog,
    missingPolicy,
    contentResolver,
    requestId,
    stageOverrides);
```

`DungeonStageDefinition.stageOverrides`도 SavedBlueprint source에서만 유효합니다. Procedural source와 Override의 조합은 검증 단계에서 차단됩니다. RuntimeBuild는 검증·합성된 최종 Blueprint를 SceneBuilder에 전달하고, BakedPrefab은 manifest의 source/override/final hash를 검증한 뒤 저장 Prefab만 인스턴스화합니다.

원본이 바뀌었을 때 `DungeonStageOverrideRebaser.Analyze`는 자산을 수정하지 않습니다. 같은 ID를 우선하고, ID가 없을 때 category·content key·cell·room·variant seed가 같은 유일 후보만 제안합니다. `ChangedExact`와 `UniqueSuggestion`도 자동 commit하지 말고 사용자의 명시적 승인 뒤 Editor 서비스에서 Undo 가능한 `CommitRebind`를 호출해야 합니다. Missing, Ambiguous, 서로 다른 이전 target의 CandidateCollision과 AddedIdCollision은 Preview와 Bake를 차단해야 합니다.

Bake format/builder v1은 Override가 없고 source/final hash가 같은 R6 계약으로 계속 읽습니다. 새 Baker가 기록하는 v2 manifest는 `sourceOverrides`, `overrideHash`와 `finalBlueprintHash`를 요구하며 현재 Override 또는 최종 합성 결과가 달라지면 stale입니다. 프로젝트별 CI는 `DungeonStageBaker.ValidateCurrentBake` 결과에서 source·override·final stale 코드를 각각 실패로 취급해야 합니다.

에디터 확장은 다음 공개 진입점을 사용할 수 있습니다.

```csharp
DungeonStageBakeResult result = DungeonStageBaker.Bake(
    savedStageDefinition,
    persistentMaterialSet,
    optionalRuntimeSettings);

DungeonValidationReport freshness =
    DungeonStageBaker.ValidateCurrentBake(savedStageDefinition);
```

`Bake` 입력의 StageDefinition은 프로젝트에 저장된 `SavedBlueprint`여야 합니다. 반환 결과의 `Manifest`, `BakedPrefab`, `ValidationReport`와 `OutputFolder`는 별도 제작 UI나 CI 보고에 사용할 수 있습니다. 기본 재질 세트가 필요하면 `DungeonStageBaker.CreateDefaultMaterialSetAsset(path)`를 사용하되, 생성된 세트는 공유 가능한 사용자 입력 자산으로 관리하고 manifest `ownedArtifacts`에 편입하지 않습니다.

## 제품화 경로

- 저장 맵을 생성 코드 없이 사용: `R5.2 → R6`
- 다른 프로젝트에서 RuntimeBuild 코어만 사용: `R5.2 → R9A`
- 다른 프로젝트로 BakedPrefab까지 배포: `R5.2 → R6 → R9B`
- 수동으로 spawn을 편집한 제작 변형: 구현된 `R6 → R7` 경로 사용
- 실제 게임 세이브/재개: `R3 이후 R8`, 최종 Blueprint·spawn ID 계약과 결합

`R9A`는 생성·Blueprint·Loader·RuntimeBuild와 선택 Sample의 assembly/패키지 분리를 담당하므로 R6을 기다릴 필요가 없습니다. `R9B`는 manifest, Bake material set, Baked Prefab과 의존 자산 수집을 담당하므로 R6 완료가 선행 조건입니다.

## 선택 backlog

다음 항목은 R6 착수 조건이나 MVP 완료 조건이 아닙니다. 실제 제품 요구가 확정될 때 독립 마일스톤으로 진행합니다.

1. R7의 spawn Override 뒤 셀 추가·삭제와 입구·출구 변경
2. 방 메타데이터와 그래프를 이용한 Boss/Shop/Key/Lock/Secret 제약 및 `requiredRoomTags` 절차 생성 연결
3. 층별 grid와 층 간 edge를 사용하는 다층 던전
4. 논리 연결성과 별도의 NavMesh 도달성 검사
5. 계측 뒤 적용하는 chunk mesh, greedy wall meshing, 오브젝트 풀, Jobs/Burst
6. guaranteed, independent Bernoulli, weighted group, luck, pity 드랍 규칙
7. GenerationReport와 통계의 CSV/JSON 내보내기
8. R10의 빌드된 게임 내 사용자 제작 맵 저장·공유
