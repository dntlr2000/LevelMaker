# 스테이지 제작·배포 통합 로드맵

> 상태: R0·R1·R2·R3·R4 구현 완료, R5 대기
>
> 기준 프로젝트: Unity `6000.5.3f1`
>
> 대상: 런타임 절차 생성형 로그라이크와 에디터 저장형 스테이지를 하나의 파이프라인으로 지원

## 1. 목표

하나의 생성 로직으로 다음 두 사용 방식을 모두 지원합니다.

1. 게임 실행 중 레시피와 시드로 매번 던전을 만드는 로그라이크 모드
2. 사용자가 설정을 세밀하게 조절한 결과를 자산으로 저장하고, 검수한 동일 맵을 게임에서 사용하는 제작 모드

두 모드가 서로 다른 생성기를 갖지 않도록 `DungeonBlueprint`를 공통 결과물로 둡니다. 절차 생성은 Blueprint를 새로 계산하고, 저장형 스테이지는 저장된 Blueprint를 읽습니다. 이후의 검증·메시 생성·콘텐츠 배치는 같은 경로를 사용합니다.

전체 로드맵 완료 조건은 다음과 같습니다.

- 같은 레시피·시드·생성기 버전은 같은 Blueprint 해시를 만듭니다.
- 저장한 Blueprint는 레시피가 나중에 바뀌어도 동일한 논리 맵을 복원합니다.
- 런타임 빌드와 Bake 빌드가 같은 입구·출구·바닥·콘텐츠 ID를 가집니다.
- 파괴·처치 같은 플레이 상태는 Blueprint와 분리해 저장하고 복원할 수 있습니다.
- 기존 `RogueDungeonGenerator` 공개 API와 `__RogueDungeonLab_Generated` 루트 이름을 유지합니다.
- Runtime 어셈블리는 `UnityEditor`를 참조하지 않습니다.

## 2. 이번 설계에서 확정할 기준

- 현재 `RogueDungeonSettings`를 첫 버전의 레시피 자산으로 계속 사용합니다. 초기 단계에서 타입 이름을 바꾸지 않습니다.
- 씬에 생성된 GameObject 계층은 미리보기 결과이며 원본 데이터가 아닙니다.
- 저장형 맵의 원본은 Prefab이 아니라 `DungeonBlueprintAsset`입니다.
- Blueprint에는 Prefab 직접 참조 대신 안정적인 `contentKey`를 저장합니다. 실제 Prefab 연결은 `DungeonContentCatalog`가 담당합니다.
- 수동 수정은 생성된 계층을 직접 고치는 방식이 아니라 `DungeonStageOverrides`에 변경 내용을 기록하는 방식으로 확장합니다.
- 알고리즘 변경은 `generatorVersion`을 올립니다. 기존 결과가 필요한 자산은 구버전 경로 또는 저장된 Blueprint로 보호합니다.
- 첫 제품화 범위는 에디터에서 만든 맵 자산입니다. 최종 사용자가 게임 안에서 맵을 JSON으로 저장·공유하는 기능은 후속 단계로 분리합니다.

## 3. 지원 모드

| 사용 사례 | `sourceMode` | `buildMode` | `seedPolicy` | 원본 데이터 |
|---|---|---|---|---|
| 일반 로그라이크 런 | `Procedural` | `RuntimeBuild` | `RunSeed` | 레시피 + 런 시드 |
| 매번 완전히 새 맵 | `Procedural` | `RuntimeBuild` | `RandomPerLoad` | 레시피 + 생성된 시드 |
| 버그 재현·리플레이 | `Procedural` | `RuntimeBuild` | `FixedSeed` 또는 명시적 요청 시드 | 레시피 + 고정 시드 + 생성기 버전 |
| 저장 맵 빠른 사용 | `SavedBlueprint` | `RuntimeBuild` | 적용 안 함 | Blueprint 자산 |
| 검수 완료 맵 배포 | `SavedBlueprint` | `BakedPrefab` | 적용 안 함 | Blueprint 자산 + Bake 결과 |

저장형 모드에서는 Blueprint에 기록된 시드가 출처 정보로 남지만, 로드할 때 새 시드로 다시 계산하지 않습니다. Bake 모드는 Blueprint 해시와 Bake manifest의 해시가 같을 때만 최신 상태로 봅니다.

## 4. 목표 파이프라인

```text
DungeonStageDefinition
  ├─ Procedural
  │    RogueDungeonSettings + Seed + GeneratorVersion
  │      → DungeonGenerationRequest
  │      → DungeonBlueprintGenerator
  │      → DungeonBlueprint
  │
  └─ SavedBlueprint
       DungeonBlueprintAsset
         → DungeonBlueprint

DungeonBlueprint
  → DungeonBlueprintValidator
  → DungeonStageOverrides 적용(선택)
  → 최종 DungeonBlueprint
  ├─ RuntimeBuild → DungeonSceneBuilder + IDungeonContentResolver
  └─ BakedPrefab  → BakeManifest 검증 → Prefab 인스턴스
  → DungeonStageInstance

DungeonStageInstance + DungeonRunState
  → 파괴/처치/문/획득 상태 복원
```

의존성 순서는 다음과 같습니다.

```text
R0 회귀 기준
  → R1 Blueprint 계약
    → R2 생성/구축 분리
      → R3 StageDefinition·Loader
        ├─ R4 콘텐츠 카탈로그
        │    → R5 저장·불러오기 UI → R6 Bake → R7 수동 Override
        └─ R8 RunState

R3 + R4 + R5
  → R9 다른 프로젝트용 패키징
  → R10 런타임 사용자 맵 저장(선택)
```

## 5. 데이터 계약

### 5.1 레시피와 생성 요청

`RogueDungeonSettings`는 “어떤 분포의 맵을 만들 것인가”를 정의하는 레시피로 유지합니다. 공유 ScriptableObject를 런타임에서 직접 변경한 값이 다른 시스템에 새어 나가지 않도록 생성 직전에 정규화된 값 스냅샷을 만듭니다.

Play HUD에서 값을 바꿀 때는 원본 레시피가 아니라 `HideAndDontSave` 런타임 복제본을 편집합니다. 에디터의 제작 UI만 `SerializedObject`를 통해 원본 자산을 수정합니다.

`DungeonGenerationRequest`에는 다음 정보가 필요합니다.

| 필드 | 의미 |
|---|---|
| `recipeSnapshot` | 크기, 방·복도, 기믹, 밀도 곡선 등 생성에 실제로 쓰이는 정규화 값 |
| `seed` | 이번 Blueprint의 기준 시드 |
| `generatorVersion` | 레이아웃·배치 알고리즘 버전 |
| `catalogPlanningHash` | 콘텐츠 후보 선택에 영향을 주는 카탈로그 정의 해시 |
| `requestId` | 로그 추적용 값이며 결정 결과에는 포함하지 않음 |

레시피 해시는 Blueprint에 영향을 주는 필드만 포함합니다. `spawnDropMarkers`, 통계 초기화 여부, HUD 상태처럼 맵 구성과 무관한 값은 제외합니다. `AnimationCurve`는 키의 시간·값·접선·가중치와 wrap mode까지 정규화해 해시합니다.

시드 우선순위는 다음과 같이 고정합니다.

```text
DungeonLoadContext.explicitSeed
  → RunSeed 정책의 runSeed
  → FixedSeed 정책의 fixedSeed
  → RandomPerLoad에서 새로 만든 시드
```

실제로 사용한 시드는 항상 `DungeonStageInstance`, `GenerationReport`, 저장 로그에 기록합니다.

### 5.2 DungeonBlueprint

`DungeonBlueprint`는 GameObject를 포함하지 않는 직렬화 가능한 순수 데이터입니다. `DungeonBlueprintAsset`은 이 데이터를 Unity 프로젝트 안에 저장하는 ScriptableObject 래퍼입니다.

| 영역 | 필수 데이터 |
|---|---|
| 버전 | `formatVersion`, `generatorVersion` |
| 출처 | `seed`, `recipeHash`, `catalogPlanningHash` |
| 무결성 | `blueprintHash`, 생성 시각·작성 메모(해시 제외) |
| 그리드 | `width`, `depth`, `cellSize`, `wallHeight` |
| 셀 | 좌표, floor flag, room ID, 입구 BFS 거리; `(z, x)` 고정 순서 |
| 방 | 안정 ID, 사각 영역, 향후 사용할 room tag 목록 |
| 주요 지점 | 입구, 출구, 선택적 특수 지점 |
| 콘텐츠 | `DungeonSpawnRecord` 목록 |

벽과 기본 메시 정점은 floor 셀에서 다시 계산할 수 있으므로 Blueprint에 저장하지 않습니다. Bake 단계에서만 Mesh 자산을 파생 산출물로 저장합니다.

`DungeonSpawnRecord`는 다음 필드를 기준으로 합니다.

- `spawnId`: 저장 상태와 Override가 참조하는 안정 ID
- `category`: Enemy, Destructible, Prop, Gimmick, Marker 등
- `contentKey`: 카탈로그에서 Prefab을 찾는 문자열 키
- `instanceName`, `cell`, `localPosition`, `pitchDegrees`, `yawDegrees`, `rollDegrees`, `localScale`
- `roomId`, `progression`, 선택적 tag
- 결정적 변형 선택에 필요한 `variantIndex` 또는 `variantSeed`

절차 생성 spawn ID는 생성기 버전, 범주, 셀, 셀 안의 순번을 고정 해시해 만듭니다. 사용자가 추가한 레코드는 생성 시 한 번 만든 GUID를 자산에 계속 보존합니다. 목록 인덱스 자체를 영구 ID로 사용하지 않습니다.

Blueprint에는 Scene 경로, Asset GUID, Prefab 직접 참조를 넣지 않습니다. 그래야 다른 프로젝트로 옮기거나 런타임 JSON으로 변환해도 논리 데이터가 유지됩니다.

### 5.3 DungeonStageDefinition

`DungeonStageDefinition`은 게임이 “어떤 스테이지를 어떤 방식으로 로드할지” 선택하는 ScriptableObject입니다.

필수 필드는 다음과 같습니다.

- `sourceMode`: `Procedural` 또는 `SavedBlueprint`
- `buildMode`: `RuntimeBuild` 또는 `BakedPrefab`
- `seedPolicy`: `RandomPerLoad`, `RunSeed`, `FixedSeed`
- `recipe`: Procedural일 때 필수
- `savedBlueprint`: SavedBlueprint일 때 필수
- `contentCatalog`: 실제 콘텐츠 해석에 사용
- `bakedPrefab`, `bakeManifest`: BakedPrefab일 때 필수
- `fixedSeed`: FixedSeed일 때 사용
- `missingContentPolicy`: 오류, primitive fallback, 건너뛰기 중 하나

유효성 규칙은 다음과 같습니다.

- `Procedural + BakedPrefab` 조합은 허용하지 않습니다. 먼저 결과를 Blueprint로 저장해야 합니다.
- `SavedBlueprint`에서는 `seedPolicy`를 무시합니다.
- Bake manifest의 Blueprint·카탈로그·빌더 버전 해시가 다르면 stale 오류로 처리합니다.
- 출시용 설정에서는 필수 `contentKey` 누락을 오류로, 실험실에서는 primitive fallback 경고로 처리할 수 있습니다.

### 5.4 콘텐츠 카탈로그와 Resolver

R4에서 “배치 결정”과 “Unity 오브젝트 생성”을 다음과 같이 분리했습니다.

```text
DungeonContentPlanner
  → List<DungeonSpawnRecord>
  → IDungeonContentResolver.TryResolve(spawn record)
  → DungeonSceneBuilder가 Prefab 또는 fallback 생성
```

`DungeonContentCatalog` entry의 첫 계약은 다음과 같습니다.

- 고유 `contentKey`
- category
- Prefab
- 선택 weight
- 허용 progression 범위
- 방/복도 조건과 room tag 조건
- footprint와 최소 간격
- 회전·스케일 변형 규칙
- 선택적 드랍 테이블 또는 게임 측 식별자

카탈로그 항목과 room tag는 계산 전에 ordinal canonical 순서로 정규화합니다. Inspector에서 항목 순서를 바꾼 것만으로 결과가 변하지 않으며, 중복 키는 `RDL-CAT-*` 검증 오류로 처리합니다. planning snapshot에는 Unity Object를 넣지 않고 Prefab·drop table·gameplay ID를 planning hash에서 제외합니다. Blueprint에는 최종 선택된 key가 기록되므로 저장 맵을 다시 열 때 가중치 추첨을 반복하지 않습니다.

다른 프로젝트가 Addressables, 자체 DI, 오브젝트 풀을 쓰더라도 코어를 고치지 않고 `IDungeonContentResolver` 구현을 `DungeonLoadContext.ContentResolver`로 주입할 수 있습니다. 기본 구현은 직접 Prefab 참조를 사용하며, `Error`·`BuiltInFallback`·`Skip` 누락 정책과 실행별 override를 제공합니다.

### 5.5 결정적 난수와 버전

`StableV2`는 프로젝트 소유 PCG32와 고정 seed 파생 공식을 사용하고 레이아웃·콘텐츠 범주의 난수 stream을 분리합니다.

```text
baseSeed
  ├─ Layout
  ├─ Gimmick
  ├─ Enemy
  ├─ Destructible
  ├─ Prop
  └─ Variant
```

- 스트림 ID와 seed 파생 공식을 상수로 고정합니다.
- `UnityEngine.Random`, `string.GetHashCode()`, 실행 환경에 따라 달라지는 해시를 사용하지 않습니다.
- 새 범주를 추가해도 기존 범주의 결과가 바뀌지 않아야 합니다.
- 드랍 추첨 난수는 스테이지 생성 난수와 별도입니다.
- 기존 결과는 `LegacyV1`, 새 PRNG 경로는 opt-in `StableV2`로 버전 구분합니다. 기본 `Current`와 legacy facade는 계속 LegacyV1입니다.
- 알고리즘을 바꾸면 버전을 올리고, 같은 버전 안에서는 순회 순서와 정렬 규칙도 계약으로 봅니다.
- marker 뒤의 콘텐츠 범주 우선순위는 `Gimmick → Enemy → Destructible → Prop`으로 고정합니다.

### 5.6 저장, Bake, Override, RunState

에디터 저장 흐름은 다음 순서로 고정합니다.

1. 현재 설정을 스냅샷으로 만들고 Blueprint를 메모리에서 생성
2. 연결성, 입구·출구, spawn ID, content key를 검증
3. 검증 성공 결과를 `DungeonBlueprintAsset`에 deep copy
4. 레시피·카탈로그·Blueprint 해시와 버전을 기록
5. 저장한 자산에서 다시 미리보기를 구축하고 원본 해시와 비교

`__RogueDungeonLab_Generated`를 Prefab으로 바로 저장하지 않습니다. 이 루트는 계속 `DontSaveInEditor | DontSaveInBuild` 미리보기로 유지합니다.

Bake는 저장된 Blueprint에서만 실행합니다.

- floor/wall Mesh 자산 생성
- 콘텐츠 Prefab 배치 결과 생성
- `BakeManifest`에 Blueprint 해시, catalog hash, builder version 기록
- 재실행 시 이전 파생 산출물을 안전하게 교체
- 원본이 바뀌면 에디터와 빌드 검증에서 stale 표시

수동 편집은 다음 형태의 `DungeonStageOverrides`로 시작합니다.

- spawn 비활성화: `disabledSpawnIds`
- spawn 추가: `addedSpawns`
- 콘텐츠 교체: `spawnId + replacementContentKey`
- 위치·회전 조정: `spawnId + transform override`
- 후속 단계의 셀 추가·삭제와 입구·출구 변경

Override는 `baseBlueprintHash`를 기록합니다. 원본이 재생성되어 참조 ID가 사라지면 자동으로 조용히 버리지 않고 충돌 목록을 보여줍니다.

`DungeonRunState`는 플레이 중 변하는 값만 저장합니다.

- `blueprintHash`와 stage instance ID
- 처치·파괴·획득된 `spawnId` 집합
- 문·기믹 상태
- 플레이어 위치와 선택적 체크포인트
- 절차형 런의 run seed

GameObject 참조를 저장하지 않습니다. Blueprint 해시가 다른 RunState는 기본적으로 복원을 거부하고 명시적 migration이 있을 때만 변환합니다.

## 6. 공개 API 호환 전략

현재 소비 코드가 깨지지 않도록 `RogueDungeonGenerator`를 호환 facade로 남깁니다.

유지할 항목은 다음과 같습니다.

- `GenerateWithSeed(int)`
- `GenerateFromSettings()`
- `RegenerateActiveSeed()`
- `GenerateNewSeed()`
- `ClearGenerated()`
- `CurrentLayout`, `LastReport`, `ActiveSeed`, `GeneratedBounds`
- `GenerationCompleted`
- `__RogueDungeonLab_Generated`

새 내부 흐름은 다음과 같이 연결합니다.

```text
GenerateWithSeed(seed)
  → 기존 settings에서 DungeonGenerationRequest 생성
  → GenerateBlueprint(request)
  → BuildBlueprint(blueprint)
  → CurrentLayout 호환 projection 갱신
  → 기존 GenerationReport와 GenerationCompleted 발행
```

추가 API 후보는 다음과 같습니다.

- `DungeonBlueprint CurrentBlueprint`
- `DungeonBlueprint GenerateBlueprint(int seed)`
- `void BuildBlueprint(DungeonBlueprint blueprint)`
- `DungeonStageInstance DungeonStageLoader.Load(DungeonLoadContext context)`
- `DungeonValidationReport DungeonBlueprintValidator.Validate(...)`

`PrototypePlayerController`가 사용하는 `CurrentLayout`과 `GenerationCompleted`, `RuntimeLabHUD`와 에디터 창의 기존 버튼은 각 마일스톤마다 회귀 검증합니다. 새 StageDefinition이 없는 기존 씬은 지금과 같은 settings 기반 동작을 유지합니다.

## 7. 구현 로드맵

각 단계는 앞 단계의 테스트가 통과한 뒤 진행합니다. 한 단계에서 데이터 계약과 UI, 대규모 파일 이동을 동시에 하지 않습니다.

현재 진행 상태는 `R0 완료 → R1 완료 → R2 완료 → R3 완료 → R4 완료 → R5 대기`입니다. Unity `6000.5.3f1`에서 compile 성공, EditMode `41/41`, PlayMode `3/3`을 통과했습니다.

### R0 — 현재 동작 지문과 회귀 기준 고정 (완료)

목적은 리팩터링 전에 “같은 결과”의 기준을 만드는 것입니다.

- EditMode 테스트 어셈블리 추가
- Compact/Balanced/Chaos와 고정 시드 묶음의 방, floor 셀, 입구·출구, 콘텐츠 위치 지문 기록
- 100개 시드 연결성 검사
- 기존 PlayMode 임시 플레이어 테스트 유지
- 생성 루트 이름, 공개 API, 드랍 표본 동작 검사
- Unity `6000.5.3f1` batchmode 컴파일·테스트 로그 보관

완료 기준: 코드 구조를 바꾸기 전 반복 실행에서 모든 지문이 동일하며 현재 테스트가 통과합니다.

구현 결과: 세 프리셋 Golden 지문, 같은 시드 반복성, 100개 시드 연결성, 기존 facade·완료 이벤트·generated root 계약을 EditMode 테스트로 고정했습니다.

### R1 — Blueprint·요청·검증 계약 추가 (완료)

- `DungeonRecipeSnapshot`, `DungeonGenerationRequest` 추가
- `DungeonBlueprint`와 cell/room/spawn record 추가
- `DungeonBlueprintAsset` ScriptableObject 추가
- canonical 정렬과 Blueprint SHA-256 계산 추가
- `DungeonBlueprintValidator`와 코드 기반 오류 ID 추가
- `formatVersion = 1`, `generatorVersion = LegacyV1` 정의
- 직렬화 round-trip과 손상 데이터 검증 테스트 추가

완료 기준: GameObject 참조가 없는 Blueprint 테스트 데이터를 복제·직렬화하고 같은 데이터가 같은 해시를 냅니다. 이 단계에서는 기존 화면 결과가 바뀌지 않습니다.

구현 결과: RecipeSnapshot·GenerationRequest, Blueprint/Asset 레코드, canonical SHA-256, JSON 깊은 복사와 오류 코드 기반 validator를 추가했습니다. 기존 `GenerateWithSeed` 경로는 변경하지 않았습니다.

### R2 — 계산과 씬 구축 분리 (완료)

- `DungeonLayoutGenerator` 결과를 Blueprint layout record로 변환
- `DungeonContentSpawner`에서 `DungeonContentPlanner`를 분리
- 카탈로그 도입 전에도 planner가 현재 primitive에 대응하는 고정 built-in `contentKey`를 기록
- `DungeonSceneBuilder`가 Blueprint만 보고 geometry와 콘텐츠를 생성하도록 변경
- 기존 `DungeonMeshBuilder.Build`와 `DungeonContentSpawner.Spawn`은 당분간 호환 wrapper로 유지
- `RogueDungeonGenerator.GenerateWithSeed`를 새 파이프라인 facade로 전환
- mesh 수명, Edit/Play/domain reload 검증

완료 기준: LegacyV1의 R0 지문, 기존 HUD, 클릭 드랍, 임시 플레이어가 모두 유지됩니다. 같은 Blueprint를 두 번 빌드했을 때 논리 결과와 spawn ID가 같습니다.

구현 결과: LegacyV1 난수 호출 순서를 보존하는 `DungeonContentPlanner`, layout·spawn을 확정하는 `DungeonBlueprintGenerator`, 레코드만으로 geometry와 built-in 콘텐츠를 재구축하는 `DungeonSceneBuilder`를 추가했습니다. 기존 Build/Spawn signature는 wrapper로 유지했고 `GenerateWithSeed`를 새 facade로 전환했습니다. 세 Golden 지문, 동일 Blueprint 이중 구축, stable identity, wrapper 동등성과 기존 PlayMode 흐름을 Unity에서 검증했습니다.

### R3 — StageDefinition과 두 소스 모드 연결 (완료)

- `DungeonStageDefinition`, source/build/seed enum 추가
- `DungeonStageLoader`와 `DungeonLoadContext`, `DungeonStageInstance` 추가
- Procedural → RuntimeBuild 경로 연결
- SavedBlueprint → RuntimeBuild 경로 연결
- 명시 시드와 run seed 우선순위 구현
- 저장 Blueprint 로드 시 레시피 재계산을 하지 않는 테스트 추가

완료 기준: 한 씬에서 Definition만 바꿔 랜덤 절차 맵과 저장 맵을 선택할 수 있습니다. 기존 `RogueDungeonGenerator`만 있는 씬도 정상 동작합니다.

구현 결과: `DungeonStageDefinition`, `DungeonLoadContext`, 시드 resolver, validator, `DungeonStageLoader`, `DungeonStageInstance`를 추가했습니다. Procedural은 요청을 새로 계산하고 SavedBlueprint는 자산을 deep copy한 뒤 검증만 하며 재계산하지 않습니다. Generator는 선택적 Definition과 기존 settings-only facade를 함께 지원하고, 저장 grid의 cellSize로 임시 플레이어를 배치합니다.

R4 전 중간 점검에서 Procedural/SavedBlueprint source 전환, 저장 hash 불변과 플레이 입구를 기준선으로 승인한 뒤 R4를 진행했습니다.

### R4 — 콘텐츠 카탈로그와 Resolver (완료)

- `DungeonContentCatalog`와 entry 검증 추가
- `IDungeonContentResolver` 및 기본 Prefab resolver 추가
- 현재 primitive용 built-in `contentKey`와 fallback 추가
- 범주별 독립 RNG와 `StableV2` 도입
- 카탈로그 canonical 정렬·hash 추가
- 누락 key, 중복 key, footprint, progression 조건 테스트 추가

완료 기준: 같은 V2 요청은 카탈로그 Inspector 순서를 바꿔도 같은 Blueprint를 만들며, 다른 프로젝트가 resolver만 교체할 수 있습니다. LegacyV1은 계속 선택 가능합니다.

구현 결과: `DungeonContentCatalog`와 Unity Object 없는 planning snapshot, canonical SHA-256, `RDL-CAT-*`·`RDL-CONTENT-*` 검증을 추가했습니다. `DungeonPrefabContentResolver`와 Prefab/factory resolution, 세 가지 누락 정책, `DungeonLoadContext`의 custom resolver override를 RuntimeBuild에 연결했습니다. `StableV2`는 PCG32의 Layout/Gimmick/Enemy/Destructible/Prop/Variant 독립 stream과 고정 범주 우선순위를 사용하며 명시적으로 선택할 때만 활성화됩니다. 요청 snapshot 무결성, 예약 key/category, 실제 cell-room 일치, Prefab 드랍 설정 우선순위, 비활성 staging root와 명시적 합성 Mesh 소유권도 검증합니다. Unity `6000.5.3f1`에서 compile, EditMode `41/41`와 PlayMode `3/3`을 통과했습니다.

### R5 — 에디터 저장·불러오기 제작 흐름 (대기)

에디터 창에 `스테이지 자산` 탭을 추가합니다.

- `현재 결과를 새 Blueprint로 저장`
- `선택 Blueprint 덮어쓰기`
- `저장본 미리보기`
- `절차 원본과 저장본 비교`
- `StageDefinition 생성`
- 검증 오류·경고와 stale 상태 표시
- 덮어쓰기 확인, `Undo`, `SerializedObject`, `EditorUtility.SetDirty` 적용
- Unity 재시작과 Play/Edit 전환 후 참조 유지 검증

완료 기준: 사용자가 설정을 드래그해 결과를 고른 뒤 자산으로 저장하고, 새 씬에서 저장 자산만으로 동일 맵을 탐험할 수 있습니다. 여기까지가 첫 저장형 스테이지 MVP입니다.

### R6 — Bake와 배포용 파생 자산

- `DungeonStageBaker`와 `BakeManifest` 추가
- floor/wall Mesh를 프로젝트 자산으로 저장
- 콘텐츠와 marker를 포함한 Prefab 생성
- 같은 Blueprint 재Bake와 stale 파생 자산 정리
- `SavedBlueprint + BakedPrefab` 로드 경로 추가
- Editor 전용 API 격리와 Player build smoke test 추가

완료 기준: 런타임 생성 코드를 실행하지 않고도 검수한 맵 Prefab을 로드할 수 있으며 Blueprint와 Bake 결과의 해시 불일치를 탐지합니다.

### R7 — 비파괴 수동 편집

- `DungeonStageOverrides` 자산 추가
- spawn 제거·추가·교체·transform 조정 도구 추가
- Preview 선택 도구가 generated hierarchy가 아니라 Override를 수정하도록 구현
- 원본 재생성 시 ID 재결합과 충돌 리포트 추가
- 셀 편집은 spawn override가 안정화된 뒤 별도 하위 단계로 추가

완료 기준: 원본 Blueprint를 보존하면서 수동 수정본을 재생성·Bake할 수 있고, 해결되지 않은 충돌이 있으면 출시용 Bake를 막습니다.

### R8 — 런 상태 저장과 복원

- `DungeonRunState` DTO와 저장 서비스 인터페이스 추가
- 적 처치, 파괴물 파괴, 기믹 상태를 spawn ID로 기록
- StageInstance 구축 뒤 상태 재적용
- Blueprint hash 불일치 정책과 migration hook 추가
- 절차형 run seed와 저장형 stage ID 복원 테스트 추가

완료 기준: 재실행 후 같은 절차 맵 또는 저장 맵을 열고 이미 제거된 대상과 플레이어 진행 상태를 정확히 복원합니다.

### R9 — 다른 Unity 프로젝트용 패키징

- 코어 생성·Blueprint·Loader와 실험실 HUD/임시 플레이어 의존성 분리
- Input System 의존 코드는 선택적 Sample 또는 별도 assembly로 이동
- 기존 `RogueDungeonLab.Runtime` 참조가 깨지지 않는 migration 경로 준비
- UPM 구조 또는 복사 가능한 `Assets/RogueDungeonLab` 배포 구조 확정
- 카탈로그·Blueprint·StageDefinition 예제와 통합 가이드 작성
- Built-in/URP/HDRP 재질 resolver 점검
- 소비 프로젝트용 최소 smoke scene과 Player build 테스트 제공

완료 기준: 새 Unity `6000.5` 프로젝트가 코어만 가져와도 컴파일되고, 필요한 프로젝트는 Sample을 추가해 현재 실험실 기능까지 사용할 수 있습니다.

### R10 — 런타임 사용자 제작 맵 저장(선택)

에디터 제작 맵과 별도로 최종 플레이어가 게임 안에서 만든 맵을 저장해야 할 때만 진행합니다.

- Unity Object 참조가 없는 Blueprint JSON DTO
- `persistentDataPath` 저장소 추상화
- format migration과 크기·범위·content key whitelist 검증
- 원자적 저장과 손상 파일 복구
- 사용자 파일과 개발 자산을 구분하는 UI

완료 기준: 빌드된 게임에서 저장·재시작·불러오기 round-trip이 가능하며, 잘못된 파일이 임의 Prefab이나 경로를 로드하지 못합니다.

## 8. 예정 파일 구조

새 타입은 역할별 파일로 추가하고, 기존 대형 파일 분리는 동작 지문을 확보한 뒤 진행합니다. Unity `6000.5` 직렬화 안정성을 위해 모든 MonoBehaviour와 ScriptableObject는 타입명과 같은 연결 파일을 유지합니다.

```text
Assets/RogueDungeonLab/
├─ Runtime/
│  ├─ Blueprint/
│  │  ├─ DungeonBlueprint.cs
│  │  ├─ DungeonBlueprintAsset.cs
│  │  └─ DungeonBlueprintValidator.cs
│  ├─ Generation/
│  │  ├─ DungeonGenerationRequest.cs
│  │  ├─ DungeonBlueprintGenerator.cs
│  │  ├─ DungeonContentPlanner.cs
│  │  └─ StableDungeonRandom.cs
│  ├─ Building/
│  │  ├─ DungeonSceneBuilder.cs
│  │  └─ DungeonStageInstance.cs
│  ├─ Content/
│  │  ├─ DungeonContentCatalog.cs
│  │  └─ IDungeonContentResolver.cs
│  ├─ Loading/
│  │  ├─ DungeonStageDefinition.cs
│  │  └─ DungeonStageLoader.cs
│  └─ State/
│     └─ DungeonRunState.cs
├─ Editor/
│  ├─ Authoring/
│  │  └─ DungeonStageAssetPanel.cs
│  └─ Baking/
│     ├─ DungeonStageBaker.cs
│     └─ BakeManifest.cs
└─ Tests/
   ├─ EditMode/
   └─ PlayMode/
```

실제 이동 시 기존 `.meta` GUID를 보존하고, assembly를 옮기는 직렬화 타입에는 필요한 migration attribute와 장면 재로드 검증을 함께 적용합니다.

## 9. 검증 매트릭스

| 영역 | 자동 검증 |
|---|---|
| 결정성 | 같은 recipe snapshot + seed + generatorVersion → 같은 Blueprint hash |
| 버전 | LegacyV1 지문 유지, StableV2 내부 반복성 유지 |
| 연결성 | 모든 floor 셀의 BFS 거리 0 이상 |
| 저장 | Blueprint asset 직렬화 round-trip 후 hash 동일 |
| 모드 분리 | SavedBlueprint 로드는 recipe 변경의 영향을 받지 않음 |
| 콘텐츠 | spawn ID 중복 없음, floor 밖 배치 없음, contentKey 검증 |
| 카탈로그 | entry/tag 재정렬로 planning hash·V2 결과가 바뀌지 않음, 중복 key·progression·footprint 오류 |
| 난수 | PCG32 golden vector, child stream 반복성, 범주별 독립성 |
| 해석 | Prefab resolver identity 유지, Error/BuiltInFallback/Skip 정책 |
| 구축 | 동일 Blueprint의 RuntimeBuild 결과가 동일 |
| Bake | manifest hash 일치, 원본 변경 시 stale 탐지 |
| 상태 | 파괴·처치 상태가 spawn ID로 복원됨 |
| 호환 | 기존 generator API, HUD, 카메라, 임시 플레이어, 클릭 드랍 정상 |
| 생명주기 | Edit/Play/domain reload에서 null 예외와 Mesh 누수 없음 |
| 경계 | Runtime assembly의 `UnityEditor` 참조 0개 |
| 배포 | 빈 Unity 6000.5 프로젝트 import와 Player build 성공 |

성능은 R0에서 현재 p50/p95 생성 시간과 할당량을 먼저 기록합니다. 측정 기준 없이 임의 제한 시간을 정하지 않고, 각 단계에서 같은 맵 크기의 회귀 비율을 비교합니다.

## 10. Migration과 rollback 원칙

- 기존 설정 자산과 장면은 자동으로 새 모드를 강제하지 않습니다.
- StageDefinition이 없는 생성기는 legacy settings 흐름을 사용합니다.
- `DungeonGeneratorVersions.Current`와 기존 settings-only facade는 LegacyV1을 유지하며 StableV2를 자동 강제하지 않습니다.
- 새 직렬화 필드는 명시적 기본값과 `formatVersion` migration을 가집니다.
- Blueprint migration은 원본을 바로 덮지 않고 복사본 생성 또는 확인 가능한 Undo 단위로 실행합니다.
- Bake Prefab과 Mesh는 파생 산출물이므로 원본 Blueprint가 있으면 재생성할 수 있어야 합니다.
- 마일스톤별 facade를 유지해 문제가 생기면 해당 내부 경로만 이전 구현으로 되돌릴 수 있게 합니다.
- 사용자가 직접 수정한 settings, Blueprint, catalog 자산은 자동 정리 대상으로 보지 않습니다.

## 11. 주요 위험과 대응

| 위험 | 대응 |
|---|---|
| 리팩터링으로 같은 시드 결과가 변함 | R0 지문, LegacyV1, generatorVersion |
| 공유 settings를 Play HUD가 변경해 원본 오염 | 생성 요청 snapshot과 런타임 복제본 사용 |
| 생성 계층 직접 수정이 재생성 때 사라짐 | 읽기 전용 preview + Override 자산 |
| 다른 프로젝트에서 Prefab 연결이 끊김 | Blueprint의 contentKey + 프로젝트별 resolver/catalog |
| 카탈로그 변경으로 저장 맵 외형이 달라짐 | catalog hash와 missing/stale 정책 |
| Bake와 논리 데이터가 어긋남 | BakeManifest 검증 |
| Input System 없는 프로젝트가 컴파일 실패 | R9에서 코어와 선택 Sample assembly 분리 |
| 오래된 세이브가 다른 맵에 적용됨 | RunState의 blueprintHash 확인과 migration hook |

## 12. 완료된 구현 묶음과 다음 단계

R0부터 R4까지의 기반 구현 묶음은 완료되었습니다.

1. 현재 결과 지문을 만드는 EditMode 테스트
2. `DungeonRecipeSnapshot`과 `DungeonGenerationRequest`
3. `DungeonBlueprint` 레코드와 hash/validator
4. Blueprint 메모리 round-trip 테스트
5. `DungeonContentPlanner`와 `DungeonSceneBuilder`의 계산/구축 분리
6. `GenerateWithSeed`의 Blueprint facade 전환과 `CurrentBlueprint`
7. 동일 Blueprint 재구축 및 기존 생성 화면·공개 API의 Unity 회귀 검증
8. `DungeonStageDefinition`과 두 source를 해석하는 `DungeonStageLoader`
9. 시드 정책, 저장본 비재계산과 `DungeonStageInstance` 수명주기 검증
10. `DungeonContentCatalog` planning snapshot·canonical hash와 교차 validator
11. opt-in `StableV2` PCG32 독립 stream과 canonical 콘텐츠 선택
12. Prefab/factory resolver, 세 가지 누락 정책과 load context override

다음 구현은 R5의 에디터 저장·불러오기 제작 UI입니다. 현재 결과를 새 `DungeonBlueprintAsset`으로 저장하거나 선택 자산에 덮어쓰고, 저장본 미리보기와 StageDefinition 생성을 연결합니다. 실제 Bake는 계속 R6 범위입니다.
