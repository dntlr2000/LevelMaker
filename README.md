# LevelMaker
로그라이크 던전 생성기 프로토타입

## 설계 및 제품화 문서

- [현재 아키텍처](docs/ARCHITECTURE_KO.md)
- [절차 생성·저장형 스테이지 통합 로드맵](docs/STAGE_PIPELINE_ROADMAP_KO.md)
- [제품화 확장 가이드](docs/EXTENSION_GUIDE_KO.md)
- [테스트 계획](docs/TEST_PLAN_KO.md)

현재 통합 로드맵의 R0·R1·R2·R3·R4가 완료되었고 R5가 다음 단계입니다. 기존 생성 결과를 유지한 채 `DungeonStageDefinition` 하나로 절차 생성과 저장 Blueprint RuntimeBuild를 선택하고, `DungeonContentCatalog` 또는 프로젝트별 resolver로 실제 콘텐츠를 연결할 수 있습니다. R5에서는 현재 결과를 Blueprint 자산으로 저장·선택하는 에디터 제작 UI를 추가합니다.

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

`BakedPrefab` 모드는 R6 범위이므로 현재 validator가 명시적으로 차단합니다. 현재 결과를 에디터 버튼으로 저장·선택하는 제작 UI는 R5에서 추가됩니다.

## R4 검증 상태

Unity `6000.5.3f1` batchmode compile과 EditMode `41/41`, PlayMode `3/3`이 통과했습니다. PlayMode 자동 검증은 임시 플레이어 흐름, 저장 Blueprint의 cell size 입구 배치, 자식 클릭 대상의 루트 제거와 드랍 통계 1회 누적을 포함합니다. 실제 화면 포인터 Raycast와 HUD 입력 차단은 수동 확인 범위입니다.

### R4 수동 검증 장면

[`Assets/R4ManualVerification/Scenes/R4_ManualVerification.unity`](../R4ManualVerification/Scenes/R4_ManualVerification.unity)를 열고 Play를 누르면 StableV2, Catalog Prefab, 클릭 파괴와 드랍 통계를 바로 확인할 수 있습니다. 전체 자산과 장면을 기준 상태로 다시 만들려면 `Tools > Rogue Dungeon Lab > R4 수동 검증 환경 생성`을 실행합니다. Stage Definition 교체 순서는 [R4 수동 검증 안내](../R4ManualVerification/README_KO.md)를 참고합니다.

## Play 모드 조작

- 자유 시점: 카메라의 실제 3차원 시선 기준 `WASD` 이동, `Space` 월드 상승, `Ctrl` 월드 하강, `Shift` 가속, 우클릭 드래그 제자리 회전, 마우스 휠 줌, 중클릭 드래그 이동. 상하 회전 범위는 거의 수직인 `-89°~89°`입니다.
- 임시 캐릭터: HUD의 `임시 플레이어 생성 (WASD)` 버튼으로 입구에 생성
- 캐릭터 조작: `WASD` 이동, `Shift` 달리기, `Space` 점프, `R` 입구 복귀
- 캐릭터가 활성화되면 카메라가 자동으로 추적하며, HUD의 제거 버튼으로 자유 시점에 돌아갑니다.
- 적과 파괴 가능한 오브젝트는 기존과 같이 좌클릭하여 드랍 표본을 기록합니다.

## Play HUD

- `스테이지 설정`: 시드, 스테이지 크기, 셀·벽 크기, 방·복도, 기믹·간격과 콘텐츠별 밀도·방 선호도·군집도·최대 개수를 조절합니다. 슬라이더와 프리셋은 활성 시드를 유지한 채 자동 재생성되므로 드래그 중 변화를 확인할 수 있습니다.
- 숫자를 입력하는 시드 필드는 타이핑 중간값으로 생성하지 않으며 `설정 적용 및 입력 시드로 생성` 버튼으로 확정합니다.
- `탐험`: 현재/새 시드 재생성, 임시 플레이어 생성·제거와 최근 생성 결과 확인
- `드랍 통계`: 적·파괴물 빠른 표본, 통계 초기화와 관측 결과 확인
- 패널은 해상도에 따라 `0.75~1.5배`로 조절되고, 가로 화면에서는 제한된 폭, 세로 화면에서는 가용 폭을 사용합니다. 모든 탭 내용은 세로 스크롤을 지원합니다.
