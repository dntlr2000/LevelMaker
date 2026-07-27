# R7 비파괴 Stage Override 수동 검증

## 목적

이 검증 환경은 R6에서 자동 생성하는 StableV2 Saved Blueprint, 드랍 설정과
영속 Bake MaterialSet을 그대로 입력으로 사용합니다. 원본 Blueprint를 변경하지 않고
별도 `DungeonStageOverrides` 자산에 다음 네 변경을 기록합니다.

- 첫 Enemy 비활성화
- 첫 Prop의 built-in cube/cylinder 콘텐츠 교체
- 첫 Destructible의 절대 위치·회전·scale 변경
- 비어 있는 먼 floor cell에 Destructible 하나 추가

같은 원본과 Override로 RuntimeBuild와 Bake format v2 BakedPrefab을 각각 구축합니다.
생성 스크립트는 두 경로의 최종 Blueprint, source/override/final hash, Spawn identity,
Transform, 구축 개수와 Bake metadata가 일치하지 않으면 완료 전에 예외를 발생시킵니다.

## 생성 방법

Unity 메뉴에서 다음 항목을 실행합니다.

```text
Tools > Rogue Dungeon Lab > R7 수동 검증 환경 생성
```

현재 장면 저장 확인 뒤 다음 순서가 자동으로 수행됩니다.

1. R6 수동 검증 기준 자산 갱신
2. R7 Override 기준값 생성
3. RuntimeBuild·BakedPrefab StageDefinition 생성
4. Override를 반영한 format v2 Bake
5. RuntimeBuild와 BakedPrefab 실제 로드 및 동등성 검사
6. 검증 장면 저장과 재개방

Batchmode 진입점은 다음과 같습니다.

```text
-executeMethod RogueDungeonLab.Editor.R7ManualVerificationSetup.CreateAllFromBatch
```

## 생성되는 R7 자산

```text
Assets/R7ManualVerification/
├── Overrides/
│   └── R7_StageOverrides.asset
├── Stages/
│   ├── R7_RuntimeBuildStage.asset
│   ├── R7_BakedStage.asset
│   └── R7_BakedStage_Bake/
│       └── Version_.../
│           ├── Floor.asset
│           ├── Walls.asset
│           ├── Stage.prefab
│           └── BakeManifest.asset
└── Scenes/
    └── R7_StageOverrideVerification.unity
```

R6의 다음 입력은 참조만 하며 R7 파생 자산의 소유 목록에 포함하지 않습니다.

```text
Assets/R6ManualVerification/Settings/R6_BakeSettings.asset
Assets/R6ManualVerification/Blueprints/R6_SavedBlueprint_Seed73125.asset
Assets/R6ManualVerification/Materials/R6_DefaultBakeMaterialSet.asset
```

생성 메뉴를 다시 실행하면 R7 Override 자산의 네 기준 변경이 초기 상태로
덮어써집니다. 실험 결과를 보존해야 한다면 먼저 Override 자산을 다른 경로로
복제하십시오.

## 자동으로 차단되는 조건

환경 생성 중 다음 항목을 코드로 검사합니다.

- 원본 Blueprint 검증과 hash 불변
- Override stored hash와 canonical hash 일치
- disable·content·transform target Binding 일치
- 추가 Spawn ID와 원본 ID 비충돌
- Override 적용 뒤 final Blueprint 유효성
- source Blueprint hash와 Override hash가 StageInstance에 보존됨
- RuntimeBuild와 BakedPrefab의 final Blueprint hash 일치
- RuntimeBuild와 BakedPrefab의 category별 구축 개수 일치
- 모든 final Spawn의 `DungeonSpawnIdentity` ID·category·content key·cell 일치
- 모든 final Spawn의 절대 local position·rotation·scale 일치
- manifest format과 builder version이 StageOverrides v2임
- manifest의 source Blueprint·source Override·세 hash 일치
- Baked Prefab metadata의 final Blueprint hash 일치
- 현재 Bake 최신성 검증 통과

## Play 수동 확인

`R7_StageOverrideVerification` 장면을 열면 다음 두 Generator가 있습니다.

```text
R7 RuntimeBuild Generator (ACTIVE)
R7 BakedPrefab Generator (toggle after disabling Runtime)
```

처음에는 RuntimeBuild Generator만 활성 상태입니다.

1. Play에 진입합니다.
2. WASD·Shift·Space·Ctrl과 우클릭 회전으로 맵을 탐험합니다.
3. 적과 파괴물을 좌클릭해 드랍 표본이 정확히 한 번 증가하는지 확인합니다.
4. Play를 종료합니다.
5. RuntimeBuild Generator를 비활성화합니다.
6. BakedPrefab Generator를 활성화합니다.
7. 다시 Play하고 같은 맵·Override 배치·클릭 드랍을 확인합니다.

두 Generator를 동시에 활성화하면 같은 위치에 두 스테이지가 겹치므로 시각 검증과
Raycast 대상 판별이 어려워집니다. 반드시 하나만 활성화하십시오.

## Editor Override 편집 확인

실험실의 `스테이지 자산` 탭에서 다음 순서로 확인합니다.

1. `R6_SavedBlueprint_Seed73125`를 선택 Blueprint로 지정합니다.
2. `R7_StageOverrides`를 선택합니다.
3. `원본 + Override 미리보기`를 실행합니다.
4. Scene 또는 Hierarchy에서 `DungeonSpawnIdentity`가 있는 Spawn을 선택합니다.
5. 비활성화, 콘텐츠 key 또는 절대 Transform을 Override에 기록합니다.
6. 미리보기를 갱신하고 같은 `spawnId`의 새 오브젝트가 선택되는지 확인합니다.
7. 변경 목록에서 작업을 제거하고 원본 상태로 복귀하는지 확인합니다.
8. 추가 Spawn을 만들고 stable ID가 `override:` namespace에 저장되는지 확인합니다.

Generated hierarchy의 Transform을 직접 바꾸는 것은 저장 경로가 아닙니다. 직접 변경한
값은 다음 전체 미리보기 재구축에서 사라져야 하며, Bake 입력에도 포함되면 안 됩니다.

## 원본 저장 차단 확인

Override 적용 미리보기 상태에서 다음 두 버튼이 비활성화되는지 확인합니다.

```text
현재 결과를 새 Blueprint로 저장
선택 Blueprint 덮어쓰기
```

Override 미리보기의 `CurrentBlueprint`는 최종 논리 결과이므로 이 버튼이 활성화되면
원본과 비파괴 변경의 경계가 무너집니다. 원본 Blueprint hash는 Override 편집,
미리보기와 Bake 전후에 같아야 합니다.

## stale과 재Bake 확인

1. 기준 R7 Bake의 최신성 검사를 실행하고 오류가 없는지 확인합니다.
2. `R7_StageOverrides`에서 콘텐츠 또는 Transform 값을 변경합니다.
3. 기존 Baked Definition의 최신성 검사에 Override 또는 final hash stale 오류가
   표시되는지 확인합니다.
4. RuntimeBuild 미리보기에는 새 변경이 나타나는지 확인합니다.
5. `재Bake`를 실행합니다.
6. 새 manifest의 `overrideHash`와 `finalBlueprintHash`가 갱신되고 최신성 검사가
   다시 통과하는지 확인합니다.

Bake commit은 파생 파일을 교체하므로 `Ctrl+Z` 복귀 대상이 아닙니다. 실패한 재Bake는
이전 정상 Prefab·manifest를 보존해야 합니다.

## 재결합 확인

원본 Blueprint가 바뀌어 `baseBlueprintHash`가 달라지면 Preview와 Bake가 차단되어야
합니다.

1. Override의 기준과 다른 Saved Blueprint를 재결합 후보로 지정합니다.
2. 재결합 분석을 실행합니다.
3. exact ID, 의미 Binding 유일 후보, 누락·다중 후보가 구분되는지 확인합니다.
4. 분석과 후보 확인만으로 Override 자산의 target ID와 base hash가 바뀌지 않는지
   확인합니다.
5. 미해결 항목은 변경 목록에서 제거하거나 원본을 수정해 해결합니다.
6. `재결합 적용` 확인을 누른 뒤에만 Binding과 base hash가 하나의 Undo 단위로
   갱신되는지 확인합니다.

추가 Spawn ID가 새 원본 ID와 충돌하거나 대상이 사라져 유일 후보를 찾지 못하면
재결합 적용과 Bake가 계속 차단되어야 합니다.

## 완료 판정

다음 조건이 모두 만족되면 R7 수동 검증을 통과로 판정할 수 있습니다.

- 원본 Blueprint 자산과 hash가 변경되지 않음
- 네 Override 종류가 Preview에 반영되고 재구축 후 유지됨
- generated hierarchy 직접 변경은 재구축 뒤 사라짐
- Override Preview 중 Blueprint 저장·덮어쓰기가 차단됨
- RuntimeBuild와 BakedPrefab의 배치와 클릭 드랍이 같음
- Override 변경이 기존 Bake를 stale로 만들고 재Bake로 해소됨
- 재결합 분석은 자산을 바꾸지 않고 명시적 적용만 Undo 가능한 변경을 만듦
- unresolved 충돌 상태에서 Preview와 Bake가 차단됨
