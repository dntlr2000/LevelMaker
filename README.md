# LevelMaker
로그라이크 던전 생성기 프로토타입

## 설계 및 제품화 문서

- [현재 아키텍처](docs/ARCHITECTURE_KO.md)
- [절차 생성·저장형 스테이지 통합 로드맵](docs/STAGE_PIPELINE_ROADMAP_KO.md)
- [제품화 확장 가이드](docs/EXTENSION_GUIDE_KO.md)
- [테스트 계획](docs/TEST_PLAN_KO.md)

현재 통합 로드맵의 R0부터 R7까지 구현과 통합 검증을 완료했습니다. 기존 생성 결과와 저장 당시 레시피는 `DungeonBlueprintAsset`으로 보존하며, 별도 `DungeonStageOverrides` 자산으로 Spawn 비활성화·추가·콘텐츠 교체·절대 Transform을 기록할 수 있습니다. `DungeonStageDefinition` 하나로 절차 생성, 저장 Blueprint RuntimeBuild 또는 검수 완료 BakedPrefab 로드를 선택하며, R7 Override는 SavedBlueprint에만 적용됩니다. `DungeonContentCatalog` 또는 프로젝트별 resolver로 RuntimeBuild 콘텐츠를 연결하고, Bake는 알려진 built-in/fallback과 Catalog 직접 Prefab을 영속 자산으로 확정합니다.

## Stage Definition과 콘텐츠 카탈로그 (R3·R4)

1. `Assets > Create > Rogue Dungeon Lab > Content Catalog`로 선택적 카탈로그를 만듭니다.
2. 각 entry에 고유 `contentKey`, category, Prefab, 양수 weight, 진행도 범위, 방/복도 조건, footprint·간격과 회전·스케일 변형을 지정합니다.
3. `Assets > Create > Rogue Dungeon Lab > Stage Definition`으로 스테이지 자산을 만듭니다.
4. `Procedural`은 `recipe`, 시드 정책과 생성기 버전을 지정합니다. `generatorVersion`을 `DungeonGeneratorVersions.StableV2` 값인 `2`로 명시하면 카탈로그 기반 선택과 독립 난수 스트림을 사용합니다.
5. `SavedBlueprint`는 코드에서 `DungeonBlueprintAsset.Store`로 저장한 자산을 지정하며, 로드할 때 레시피나 새 시드로 재계산하지 않습니다.
6. `contentCatalog`와 `missingContentPolicy`를 지정한 뒤 장면의 `RogueDungeonGenerator.stageDefinition`에 연결하면 `loadOnPlay` 또는 `Load Stage Definition` 컨텍스트 메뉴로 로드됩니다.

`DungeonGeneratorVersions.Current`, 기존 settings-only `GenerateWithSeed`와 `DungeonStageLoader.LoadProcedural`은 승인된 결과를 보존하기 위해 계속 `LegacyV1`입니다. 즉 `StableV2`는 Stage Definition에서 버전을 선택하거나 코드에서 `DungeonGenerationRequest.CreateStableV2`를 호출해야 활성화됩니다.

### Content Catalog entry

- `contentKey`는 대소문자를 구분하는 고유 문자열이며 앞뒤 공백과 중복 key는 검증 오류입니다.
- `minProgression`과 `maxProgression`은 양 끝을 포함하는 `0~1` 범위입니다.
- `placement`는 `Any`, `RoomOnly`, `CorridorOnly` 중 하나입니다.
- `requiredRoomTags`, `footprintCells`, `minimumSpacingCells`, `randomizeYaw`, `yawDegreesRange`, `uniformScaleRange`가 배치와 변형에 사용됩니다.
- `dropTable`과 `gameplayId`는 선택적 게임 연계 정보입니다. 기본 Prefab resolver가 자동 보강한 클릭 대상에 전달하며, Prefab에 이미 작성된 대상의 ID·드랍 테이블은 우선 보존합니다. Prefab, drop table, gameplay ID는 planning hash에서 제외됩니다.

카탈로그는 생성 요청 시 Unity Object가 없는 planning snapshot으로 깊은 복사됩니다. entry와 room tag를 canonical 순서로 해시하므로 Inspector 목록 순서만 바꿔도 같은 `catalogPlanningHash`와 Blueprint가 유지됩니다. R4의 절차 생성에는 아직 room tag 할당 단계가 없으므로 `requiredRoomTags`가 있는 entry는 `StableV2` 절차 선택 후보에서 제외되며, 저장·외부 제작 Blueprint를 검증할 때는 tag 조건을 검사합니다.

### Resolver와 누락 정책

기본 `DungeonPrefabContentResolver`는 카탈로그의 key를 Prefab으로 해석합니다. Catalog나 custom resolver가 반환하지 않아도 알려진 built-in key는 기존 primitive로 구축됩니다. 그 밖의 해석되지 않은 key는 다음 정책을 따릅니다.

- `Error`: 생성 부작용 전에 오류로 중단
- `BuiltInFallback`: category별 임시 표현으로 생성
- `Skip`: 경고를 남기고 해당 콘텐츠를 생성하지 않음

기본값은 기존 자산 호환을 위한 `BuiltInFallback = 0`입니다. 다른 프로젝트의 DI·오브젝트 풀·자체 생성 경로는 `IDungeonContentResolver`를 구현하고 로드 문맥에서 교체할 수 있습니다.

```csharp
DungeonLoadContext context = new DungeonLoadContext(stageDefinition, parent)
{
    ContentResolver = projectResolver,
    MissingContentPolicyOverride = DungeonMissingContentPolicy.Error
};
DungeonStageInstance instance = DungeonStageLoader.Load(context);
```

`SavedBlueprint + BakedPrefab`은 R6·R7 배포 경로입니다. Runtime-safe `DungeonBakeManifest`는 저장 Blueprint, 선택적 Stage Override, custom catalog, 완전한 영속 재질 세트, source/final·planning·realization·gameplay·material·override hash와 Baker 소유 산출물 목록을 검증합니다. Editor-only Baker가 최종 Blueprint의 floor/wall Mesh와 Prefab을 저장하고, 런타임 Loader는 Blueprint 생성, Mesh Builder 또는 콘텐츠 resolver를 다시 실행하지 않고 검증된 Prefab을 인스턴스화합니다. `Procedural + BakedPrefab`과 `Procedural + Stage Override`는 명시적으로 차단됩니다.

## 스테이지 자산 제작 (R5·R5.1·R6·R7)

`Tools > Rogue Dungeon Lab > 실험실 열기`의 `스테이지 자산` 탭에서 다음 흐름을 사용합니다.

1. 생성 또는 분포 탭에서 원하는 결과를 확정합니다.
2. 실제 Prefab을 쓴다면 저장본과 함께 사용할 콘텐츠 카탈로그와 누락 정책을 지정합니다.
3. 제작 메모를 입력하고 `현재 결과를 새 Blueprint로 저장`을 누릅니다. 현재 결과의 `recipeHash`와 연결 설정이 같으면 정규화 레시피도 함께 저장됩니다.
4. 기존 자산을 갱신할 때는 Blueprint를 선택하고 `선택 Blueprint 덮어쓰기`의 hash·경로·설정 복원 정보 포함 여부를 확인합니다. 덮어쓰기는 Unity Undo를 지원합니다.
5. `저장본 미리보기`는 레시피나 새 시드 계산 없이 저장 데이터를 RuntimeBuild합니다. `현재 절차 설정으로 재생성`은 미리보기 전 시드를 사용하지만 설정값 자체를 되돌리지는 않습니다.
6. `레시피 설정만 불러오기 (현재 시드 유지)`로 현재 설정 자산의 생성 필드와 밀도 곡선만 복원할 수 있습니다. 시드·드랍 테이블·런타임 옵션은 유지되며 Unity Undo를 지원합니다.
7. `레시피 + 저장 시드 적용 후 절차 생성`은 저장 당시 generatorVersion과 catalog hash까지 맞을 때 입력을 적용하고 원 Blueprint hash를 재현합니다.
8. 비교 영역에서 seed, generatorVersion, recipe/catalog/blueprint hash와 stale 상태를 확인합니다.
9. `SavedBlueprint StageDefinition 생성`으로 새 씬에서 Play 진입 시 로드할 자산을 만듭니다. 선택하면 현재 Generator에도 즉시 연결됩니다.

검증 오류가 있는 현재 결과나 저장본은 저장·미리보기·StageDefinition 생성을 진행할 수 없습니다. 저장 레시피 버전·hash·정규화가 맞지 않으면 설정 복원을 별도로 차단합니다. 설정 스냅샷이 없는 기존 R5 자산은 맵 미리보기와 로드는 그대로 가능하고 설정 복원 버튼만 비활성화됩니다. 저장 시각·제작 메모·선택적 제작 레시피는 논리 Blueprint hash에 영향을 주지 않습니다. 생성된 `__RogueDungeonLab_Generated` 계층은 계속 미리보기 산출물이므로 직접 수정하지 않습니다.

## 비파괴 Stage Override 제작 (R7)

`스테이지 자산` 탭의 `R7 비파괴 Stage Override` 영역은 저장 Blueprint를 바꾸지 않고 제작 변형을 별도 자산에 기록합니다.

1. 검증된 Blueprint를 선택하고 `새 Override 생성`을 눌러 `DungeonStageOverrides` 자산을 만듭니다.
2. 같은 Blueprint를 사용하는 SavedBlueprint StageDefinition이 있다면 `현재 Definition에 연결`로 Override를 연결합니다. Procedural Definition에는 연결할 수 없습니다.
3. `원본 미리보기`와 `Override 미리보기`로 적용 전후를 비교합니다. Override 미리보기는 레시피나 시드를 다시 계산하지 않고 전체 RuntimeBuild를 재구축합니다.
4. 미리보기의 Spawn 오브젝트를 Scene에서 선택해 비활성화, `contentKey` 교체 또는 로컬 위치·세 축 회전·scale의 `절대 Transform`을 기록합니다. 이 Transform 작업은 Spawn의 논리 cell을 바꾸지 않습니다.
5. `수동 Spawn 추가`에서 category, `contentKey`, 원본의 floor cell과 절대 Transform을 입력하면 `override:v1:` stable ID를 가진 레코드가 추가됩니다. 추가 레코드의 콘텐츠와 Transform은 본문을 직접 갱신하고 삭제할 수 있습니다.
6. 변경 목록에서 개별 작업을 제거하면 원본 상태로 돌아갑니다. 생성된 hierarchy를 직접 옮기거나 삭제한 결과는 원본 데이터가 아니며 저장되지 않습니다.

입구·출구 `Marker`는 R7 Spawn 단계에서 비활성화·추가·콘텐츠 교체·Transform 변경을 모두 금지합니다. 셀, 입구·출구와 지형 구조 변경은 현재 범위가 아닙니다. 콘텐츠 교체와 추가는 최종 Blueprint의 category, floor, progression, room 조건, footprint와 간격 검증을 통과해야 합니다.

Override는 기준 Blueprint 참조와 `baseBlueprintHash`를 보존합니다. 변경 목록은 표시 순서와 무관하게 canonical `overrideHash`를 만들고, 적용 결과는 별도의 `finalBlueprintHash`를 가집니다. 원본 Blueprint가 덮어쓰기나 재생성으로 바뀌면 미리보기와 Bake를 조용히 계속하지 않습니다.

`원본 변경 재결합`은 자산을 바꾸지 않고 다음 순서로 분석합니다.

- 먼저 같은 stable Spawn ID를 찾고 저장된 category·content key·cell·room·variant 의미 anchor를 비교합니다.
- ID가 사라졌을 때만 의미 anchor가 정확히 일치하는 유일 후보를 제안합니다.
- 후보가 없거나 여러 개이거나, 서로 다른 작업이 같은 후보에 결합되거나, 추가 Spawn ID가 새 원본과 충돌하면 승인을 차단합니다.
- 해결 가능한 분석 결과도 `분석 결과 승인 및 재결합` 확인 후에만 기준 자산·hash와 Binding을 하나의 Unity Undo 단위로 갱신합니다.

## 배포용 BakedPrefab 만들기 (R6·R7)

`스테이지 자산` 탭의 접을 수 있는 `R6·R7 배포용 Bake` 영역을 사용합니다.

1. `SavedBlueprint StageDefinition`을 선택하거나 `현재 Generator Definition 사용`을 누릅니다. Procedural Definition은 Bake할 수 없습니다.
2. 재사용할 `DungeonBakeMaterialSet`을 지정합니다. 없다면 `기본 Bake 재질 세트 자산 생성`으로 8개 영속 재질 슬롯이 채워진 자산을 만듭니다.
3. `배포용 Mesh·Prefab Bake`를 누르고 확인합니다. 기존 결과가 있으면 버튼이 재Bake로 바뀝니다.
4. 성공한 Bake만 StageDefinition의 Prefab·manifest 참조와 `BakedPrefab` 모드에 commit됩니다. 실패한 후보는 staging에서 폐기되고 이전 정상 Bake는 유지됩니다. 이전 파생 자산 정리는 비가역적이므로 Bake commit 자체는 `Ctrl+Z` 대상이 아닙니다.
5. `최신성 다시 검사`로 Blueprint, Stage Override, 최종 Blueprint, Catalog Prefab·게임플레이 설정, 재질·Shader와 builder 계약의 현재 fingerprint를 manifest와 비교합니다. stale 또는 오류가 있으면 원인을 고친 뒤 재Bake합니다.
6. Prefab과 manifest 필드의 `Ping`으로 생성 자산을 확인하고, 이 StageDefinition을 Generator에 연결해 Play에서 로드합니다.

R6·R7 Baker는 known built-in/fallback과 Content Catalog의 직접 Prefab만 Bake합니다. runtime factory, Addressables, DI·오브젝트 풀 resolver는 RuntimeBuild 전용입니다. floor/wall Mesh와 Baked Prefab은 Baker 소유 파생 자산이지만 Blueprint, Stage Override, settings, Catalog, Catalog Prefab과 `DungeonBakeMaterialSet`은 사용자 입력 자산이므로 재Bake 정리 대상이 아닙니다.

기존 R6 Bake manifest와 metadata의 format/builder v1은 Override가 없고 source/final Blueprint hash가 같은 계약으로 계속 로드됩니다. 새 Baker는 format/builder v2를 기록합니다. v2에 Override가 없으면 빈 `overrideHash`와 source=final을 유지하고, Override가 있으면 선택 자산 참조와 `overrideHash`, 적용 뒤 `finalBlueprintHash`가 모두 일치해야 최신입니다. BakedPrefab Loader도 순수 Override applier로 최종 논리 Blueprint를 복원하지만 생성기·Mesh Builder·resolver는 다시 실행하지 않습니다.

## 검증 상태

R5.2는 Play HUD가 settings 또는 Procedural StageDefinition recipe 원본 대신 Generator 소유 `HideAndDontSave` 복제본을 편집하도록 바꿨습니다. 새 출처는 Loader 성공 뒤에만 활성 복제본으로 승격되므로 실패한 전환은 기존 맵과 설정을 보존합니다. SavedBlueprint에서는 구조·시드 편집을 차단하고 같은 저장본 재구축만 허용합니다.

Unity `6000.5.3f1` batchmode compile, EditMode `58/58`, PlayMode `7/7`이 통과했습니다. Balanced seed `73125` RuntimeBuild 15회 기준 시간은 p50 `7.390 ms`, p95 `7.744 ms`이고, Unity Mono가 thread allocation counter를 지원하지 않아 Profiler의 Mono 사용량 증분 p50 `2,252,800 B`, p95 `2,269,184 B`를 대체 기준선으로 기록했습니다. 저장 제작 회귀는 Undo 후 저장·강제 재임포트, LegacyV1 및 실제 Prefab custom catalog StableV2의 정확 재생성, snapshot 없는 기존 자산 로드를 포함합니다. 실제 Unity 프로세스 완전 재시작, Play domain reload 중 transient Mesh 관찰, HUD 화면과 포인터 Raycast는 수동 확인 범위입니다.

R6 통합 결과는 Unity `6000.5.3f1` batchmode compile 성공, EditMode `74/74`, PlayMode `8/8` 통과입니다. 실제 Physics Raycast와 마우스 입력으로 BakedPrefab 파괴 대상의 드랍 통계가 정확히 1회 증가하는 경로, RuntimeBuild/BakedPrefab 구조·stable identity·Collider·report parity, stale fingerprint, 실패 재Bake rollback과 안전한 이전 산출물 정리를 포함합니다.

원본 작업 트리와 분리한 임시 프로젝트에서 `R6ManualVerificationSetup`으로 전용 SavedBlueprint·MaterialSet·Baked Prefab·manifest·Scene을 생성하고, 그 Baked 장면 하나로 Windows Development Player 빌드도 성공했습니다. 총 빌드 크기는 `172,288,233 B`였으며 로그는 `Logs/R6PlayerBuildSmoke.log`, 실패 주입 rollback 로그는 `Logs/R6RollbackVerification.log`입니다. 실제 화면에서의 HUD 배치와 Unity Play 중 script/domain reload는 계속 수동 확인 범위입니다.

R7 통합 결과는 Unity `6000.5.3f1`에서 전체 EditMode `83/83`, PlayMode `9/9` 통과입니다. `R7ManualVerificationSetup`으로 전용 Override·RuntimeBuild/BakedPrefab Definition·v2 Bake·검증 Scene을 생성한 뒤 장면을 다시 열어 두 경로의 `finalBlueprintHash`와 stable spawn identity가 일치하는 것도 확인했습니다. Windows64 Development Player 빌드는 경고 `0`개, 총 크기 `172,176,046 B`로 성공했습니다. 근거는 `Logs/R7ManualSetup.log`, `Logs/R7FullEditMode.xml`, `Logs/R7FullPlayMode.xml`, `Logs/R7PlayerBuildSmoke.log`입니다. 실제 HUD/Scene 화면의 배치·선택 편집 감각과 Play 중 script/domain reload는 수동 확인 범위로 남습니다.

### R4 수동 검증 장면

[`Assets/R4ManualVerification/Scenes/R4_ManualVerification.unity`](Assets/R4ManualVerification/Scenes/R4_ManualVerification.unity)를 열고 Play를 누르면 StableV2, Catalog Prefab, 클릭 파괴와 드랍 통계를 바로 확인할 수 있습니다. 전체 자산과 장면을 기준 상태로 다시 만들려면 `Tools > Rogue Dungeon Lab > R4 수동 검증 환경 생성`을 실행합니다. Stage Definition 교체 순서는 [R4 수동 검증 안내](Assets/R4ManualVerification/README_KO.md)를 참고합니다.

### R6 수동 검증 환경

`Tools > Rogue Dungeon Lab > R6 수동 검증 환경 생성`은 전용 SavedBlueprint, 영속 DropTable·MaterialSet, BakedPrefab·manifest와 검증 장면을 반복 생성합니다. Play 탐험, 클릭 드랍, stale 재Bake와 실패 rollback 절차는 [R6 수동 검증 안내](Assets/R6ManualVerification/README_KO.md)를 참고합니다.

### R7 수동 검증 환경

`Tools > Rogue Dungeon Lab > R7 수동 검증 환경 생성`은 전용 Stage Override, RuntimeBuild/BakedPrefab Definition, v2 Bake와 비교 장면을 반복 생성합니다. 원본/Override 미리보기, Scene stable ID 선택 편집, 두 구축 모드 전환과 클릭/drop 확인 절차는 [R7 수동 검증 안내](docs/R7_MANUAL_VERIFICATION_KO.md)를 참고합니다.

## Play 모드 조작

- 자유 시점: 카메라의 실제 3차원 시선 기준 `WASD` 이동, `Space` 월드 상승, `Ctrl` 월드 하강, `Shift` 가속, 우클릭 드래그 제자리 회전, 마우스 휠 줌, 중클릭 드래그 이동. 상하 회전 범위는 거의 수직인 `-89°~89°`입니다.
- 임시 캐릭터: HUD의 `임시 플레이어 생성 (WASD)` 버튼으로 입구에 생성
- 캐릭터 조작: `WASD` 이동, `Shift` 달리기, `Space` 점프, `R` 입구 복귀
- 캐릭터가 활성화되면 카메라가 자동으로 추적하며, HUD의 제거 버튼으로 자유 시점에 돌아갑니다.
- 적과 파괴 가능한 오브젝트는 기존과 같이 좌클릭하여 드랍 표본을 기록합니다.

## Play HUD

- `스테이지 설정`: Play 전용 복제 설정에서 시드, 스테이지 크기, 셀·벽 크기, 방·복도, 기믹·간격과 콘텐츠별 밀도·방 선호도·군집도·최대 개수를 조절합니다. 원본 settings와 StageDefinition recipe는 바뀌지 않으며, 슬라이더와 프리셋은 활성 시드를 유지한 채 자동 재생성됩니다. SavedBlueprint 모드에서는 저장 논리 맵 보호를 위해 이 편집을 차단합니다.
- 숫자를 입력하는 시드 필드는 타이핑 중간값으로 생성하지 않으며 `설정 적용 및 입력 시드로 생성` 버튼으로 확정합니다.
- `탐험`: 현재/새 시드 재생성, 임시 플레이어 생성·제거와 최근 생성 결과 확인
- `드랍 통계`: 적·파괴물 빠른 표본, 통계 초기화와 관측 결과 확인
- 패널은 해상도에 따라 `0.75~1.5배`로 조절되고, 가로 화면에서는 제한된 폭, 세로 화면에서는 가용 폭을 사용합니다. 모든 탭 내용은 세로 스크롤을 지원합니다.
