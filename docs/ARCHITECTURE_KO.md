# 아키텍처

이 문서는 현재 구현을 설명합니다. 절차 생성형과 저장형 스테이지를 함께 지원할 목표 구조와 구현 순서는 [스테이지 제작·배포 통합 로드맵](STAGE_PIPELINE_ROADMAP_KO.md)을 참고합니다.

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
  → DungeonContentCatalogValidator
  → DungeonSceneBuilder
       ├─ DungeonMeshBuilder
       └─ IDungeonContentResolver → Prefab/factory 또는 fallback
  → DungeonStageInstance + DungeonLayout 호환 projection + GenerationReport

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

`DungeonBlueprint`는 GameObject나 Prefab 직접 참조를 포함하지 않으며 `DungeonBlueprintAsset`이 깊은 복사본을 Unity 자산으로 보관할 수 있습니다. 제작 메모와 생성 시각은 논리 해시에서 제외되고, 셀·방·spawn·tag 목록은 canonical 정렬 후 해시됩니다.

`RogueDungeonGenerator.GenerateWithSeed`는 `LegacyV1` 요청을 만들고, GameObject가 없는 계산 단계에서 Blueprint를 확정한 다음 `DungeonSceneBuilder`로 씬을 구축합니다. `DungeonLayout`은 기존 HUD·임시 플레이어 호환 projection으로 함께 유지하며, `DungeonMeshBuilder.Build`와 `DungeonContentSpawner.Spawn`의 기존 signature도 wrapper로 남아 있습니다.

Legacy 콘텐츠 planner는 입구·출구, 기믹, 적, 파괴물, cube/cylinder 지형지물에 고정 built-in `contentKey`를 기록합니다. StableV2는 카탈로그의 canonical 후보에서 최종 key를 고르고, 카탈로그 후보가 없으면 같은 built-in key를 기록합니다. 각 root 인스턴스에는 `DungeonSpawnIdentity`가 붙어 stable `spawnId`, key, 범주와 셀을 Blueprint 레코드에 연결합니다. 위치·스케일·세 축 회전은 계획 시점에 확정되므로 같은 Blueprint를 다시 구축할 때 난수를 소비하지 않습니다.

## StageDefinition과 Loader

`DungeonStageLoader`는 `DungeonLoadContext`에서 source와 시드를 해석합니다. Procedural은 `explicitSeed → RunSeed → FixedSeed → RandomPerLoad` 우선순위를 적용하고, SavedBlueprint는 모든 시드 입력과 recipe 변경을 무시한 채 자산의 깊은 복사본을 검증·구축합니다. 실제 사용 시드, Blueprint, 호환 layout, generated root, 구축 개수와 report는 `DungeonStageInstance`에 함께 기록됩니다.

`RogueDungeonGenerator`에 StageDefinition이 없으면 기존 settings-only `GenerateWithSeed`가 같은 Loader의 Procedural 경로를 사용합니다. Definition이 있으면 Play 시작 또는 컨텍스트 메뉴에서 두 source를 전환할 수 있습니다. `CurrentCellSize`는 활성 Blueprint grid를 기준으로 하므로 저장 맵과 현재 settings의 cellSize가 달라도 임시 플레이어가 올바른 입구에 배치됩니다.

R4까지는 `RuntimeBuild`만 지원합니다. `BakedPrefab`은 enum 계약만 선점하고 validator가 차단하며, 에디터 저장·불러오기 UI는 R5, 실제 Bake는 R6에서 연결합니다.

## 콘텐츠 카탈로그와 planning hash

`DungeonContentCatalog` entry는 다음 정보를 직렬화합니다.

- 대소문자를 구분하는 고유 `contentKey`, `DungeonSpawnCategory`, Prefab
- 양수 weight와 양 끝을 포함하는 progression 범위
- `Any`·`RoomOnly`·`CorridorOnly` 배치 조건과 필수 room tag
- 셀 footprint, 최소 간격, 무작위 yaw 범위, 균일 scale 범위
- 선택적 `WeightedDropTable`과 게임 측 `gameplayId`

생성 요청은 catalog를 직접 계속 읽지 않고 `DungeonContentCatalogPlanningSnapshot`을 깊은 복사합니다. planning hash에는 key, category, weight, progression, 배치 조건, tag, footprint·간격과 변형 규칙만 들어갑니다. Prefab, drop table, gameplay ID 같은 표현·게임 연계 참조는 제외됩니다. entry와 tag는 원본 목록을 수정하지 않은 채 ordinal canonical 순서로 기록되므로 Inspector 재정렬과 요청 생성 뒤 원본 자산 변경이 이미 캡처된 요청을 바꾸지 않습니다. Generator는 실행 직전에 snapshot 필드 검증과 hash 재계산을 반복해 요청 객체의 사후 변조도 거부합니다.

Catalog validator는 `RDL-CAT-*` 코드로 빈/중복 key, 예약 built-in key의 범주, 잘못된 범주·weight·progression·footprint·간격·변형을 검사하고, Blueprint 교차 검증은 `RDL-CONTENT-*` 코드로 key/category/progression/실제 floor cell 기준 room 조건/tag/footprint와 planning hash 차이를 검사합니다. StableV2 저장 Blueprint와 현재 catalog의 planning hash 차이는 경고로 남습니다.

## StableV2 결정성

`StableV2`는 프로젝트가 소유한 PCG32 구현과 고정 seed 파생 공식을 사용합니다. 난수 stream은 `Layout`, `Gimmick`, `Enemy`, `Destructible`, `Prop`, `Variant`로 분리되며 spawn별 변형은 범주·셀·범주 내 순번으로 만든 child stream을 사용합니다. 콘텐츠 배치 우선순위는 marker 확정 후 `Gimmick → Enemy → Destructible → Prop`으로 고정되어 있습니다. 한 범주의 후보 key나 난수 호출 수를 바꿔도 뒤 범주의 stream 출력은 밀리지 않습니다.

`StableV2`는 opt-in입니다. `DungeonGeneratorVersions.Current`, 기존 `RogueDungeonGenerator.GenerateWithSeed`와 settings-only loader facade는 계속 `LegacyV1`을 사용합니다. Stage Definition의 `generatorVersion`을 `StableV2`로 지정하거나 `DungeonGenerationRequest.CreateStableV2`를 호출해야 새 경로가 선택됩니다.

## Resolver와 구축 정책

`DungeonSceneBuilder`는 확정된 spawn을 stable 순서로 정렬하고 먼저 `IDungeonContentResolver.TryResolve`를 호출합니다. 기본 `DungeonPrefabContentResolver`는 catalog의 ordinal key를 직접 Prefab으로 연결하며, `DungeonContentResolution`은 Prefab 또는 프로젝트 factory와 선택적 drop table·gameplay ID를 보관할 수 있습니다. 해석된 오브젝트에도 Blueprint transform과 `DungeonSpawnIdentity`가 적용됩니다. 기존 Prefab target의 ID·drop table은 우선 보존하고, target이 없거나 필드가 비어 있을 때 catalog·런타임 기본값으로 보강합니다.

Catalog나 resolver가 반환하지 않은 알려진 built-in key는 누락 정책과 무관하게 기존 primitive로 구축됩니다. 그 밖의 해석 실패에는 `Error`가 생성 부작용 전에 중단하고, `BuiltInFallback`이 category별 임시 표현을 만들며, `Skip`이 경고와 함께 해당 spawn을 생략합니다. `DungeonLoadContext.ContentResolver`와 `MissingContentPolicyOverride`를 사용하면 Stage Definition 자산을 바꾸지 않고 실행별 resolver와 정책을 주입할 수 있습니다.

레이아웃 단계는 GameObject를 만들지 않습니다. 방은 겹침 없는 사각형으로 배치하고 가장 가까운 미연결 방을 L자 복도로 연결한 뒤 확률적으로 루프를 추가합니다.

진행도는 `distanceFromEntrance / distanceToExit`이며, 각 밀도 프로필은 기본 셀 확률 × 진행도 곡선 × 방/복도 보정 × 결정적 군집 보정으로 평가됩니다. LegacyV1은 기존 Perlin 계산을 유지하고 StableV2는 셀 기반 child stream 값을 사용합니다.

바닥은 셀당 quad, 벽은 floor-to-void 경계 box를 각각 하나의 합성 메시로 만듭니다. 클릭 대상만 개별 GameObject입니다. Loader는 새 root를 비활성 상태에서 완성한 뒤 이전 root를 비활성·정리하고 교체본을 활성화합니다. 합성 Mesh는 이름 추측 대신 `DungeonGeneratedMeshOwner`가 기록한 정확한 참조만 해제하므로 Prefab의 공유 Mesh는 건드리지 않습니다.

합성 바닥·벽에는 정적 `MeshCollider`를 함께 생성합니다. HUD에서 만드는 `PrototypePlayerController`는 런타임 전용 `CharacterController`를 사용하며, 입구 위치 생성·카메라 기준 이동·중력·점프·추락 복귀를 담당합니다. `LabOrbitCamera`는 캐릭터가 없을 때 카메라의 실제 정면·오른쪽 축으로 `WASD`를 처리하므로 `W/S` 이동에는 시선의 높이 성분도 포함됩니다. `Space`/`Ctrl`은 별도의 월드 수직 이동입니다. 자유 시점 우클릭 회전은 카메라 위치를 고정하고 회전 중심을 재계산하며, 캐릭터가 활성화되면 기존처럼 해당 Transform을 중심으로 공전 추적합니다. 이 런타임 흐름은 `UnityEditor`를 참조하지 않습니다.

`RuntimeLabHUD`는 설정·탐험·통계를 탭으로 분리합니다. 설정 탭은 `RogueDungeonSettings`의 구조·콘텐츠 수치에 직접 바인딩합니다. 슬라이더와 프리셋 변경은 다음 `Update`에 요청 하나로 합쳐지고 0.08초 제한 주기로 `ClampValues`와 `RegenerateActiveSeed`를 호출하므로, 결정적 시드는 유지하면서 드래그 중 결과를 갱신합니다. 시드 텍스트만 명시적인 생성 버튼에서 확정합니다. 패널은 기준 해상도에 대한 제한 배율과 화면 비율별 논리 영역을 계산하며, 실제 픽셀 영역을 카메라·클릭 입력 차단에도 동일하게 사용합니다. 각 탭 내용은 독립적으로 접근 가능한 스크롤 영역 안에 배치됩니다.

드랍 대시보드는 기대 확률, 관측 확률, 편차와 Wilson 95% 신뢰구간을 계산합니다. 테이블 정의가 바뀌면 이전 표본을 새 기대값과 비교하지 않도록 해당 통계를 초기화합니다.

## Unity 6000.5 직렬화

역할별 Runtime 파일의 공개 API와 로직은 유지하되, `MonoBehaviour`와 `ScriptableObject`마다 타입명과 같은 `partial` 연결 파일을 둡니다. Unity가 안정적인 `MonoScript` 자산을 생성하므로 장면 저장, 설정 에셋, Play/Edit 전환과 도메인 재로드 뒤에도 참조가 유지됩니다.

드랍 정의는 항목 정규화 후 해시를 계산합니다. 내부 정규화를 사용자 편집으로 오인해 첫 통계 표본을 초기화하지 않습니다.
