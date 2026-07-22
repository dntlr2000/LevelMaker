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
- `DungeonStageLoader.LoadProcedural`의 명시 버전 overload와 `RogueDungeonGenerator.GenerateProcedural`은 StageDefinition 없이도 LegacyV1 또는 StableV2, catalog와 누락 정책을 지정해 제작 도구의 재현 미리보기를 구축합니다.
- `DungeonBlueprintAsset.TryGetAuthoringRecipeSnapshot`은 내부 직렬화 상태를 공유하지 않는 깊은 복사본만 반환합니다. 이 데이터는 제작 편의를 위한 선택 메타데이터이므로 소비 프로젝트는 SavedBlueprint 로드의 필수 조건으로 취급하지 않습니다.

`dropTable`과 `gameplayId`는 catalog entry에 보존되지만 planning hash에는 포함되지 않습니다. 기본 Prefab resolver는 이 값을 SceneBuilder에 전달합니다. Prefab에 `DestructibleDropTarget`이 없으면 root에 추가해 catalog 값을 적용하고, 이미 target이 있으면 Prefab에 작성한 ID·드랍 테이블을 우선하며 비어 있는 값만 catalog·런타임 설정으로 보강합니다. 프로젝트별 resolver도 같은 resolution metadata를 반환할 수 있습니다. R4 절차 생성기는 아직 room tag를 배정하지 않으므로 tag가 필요한 entry는 후속 room metadata 단계 전까지 절차 선택 후보가 아닙니다.

## 다음 확장 순서

1. R6에서 검증된 저장 Blueprint만 대상으로 Mesh·콘텐츠 Prefab과 manifest를 Bake합니다.
2. 방 메타데이터와 그래프를 추가해 Boss/Shop/Key/Lock/Secret 제약과 catalog `requiredRoomTags`를 절차 생성에 연결합니다.
3. 층별 grid와 층 간 edge로 다층 던전을 구현합니다.
4. 논리 연결성과 별도로 NavMesh 도달성을 검사합니다.
5. 큰 맵은 chunk mesh, greedy wall meshing, 오브젝트 풀, Jobs/Burst 순으로 계측 후 최적화합니다.
6. 드랍은 guaranteed, independent Bernoulli, weighted group, luck, pity 규칙으로 확장합니다.
7. GenerationReport와 통계를 CSV/JSON으로 내보내 회귀 비교에 사용합니다.
