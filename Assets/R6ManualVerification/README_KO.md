# R6 수동 검증 환경

이 폴더는 `SavedBlueprint + BakedPrefab` 제작·로드와 안전한 재Bake를 확인하기 위한 전용 재생성 영역입니다. 다른 프로젝트 자산, 공유 Blueprint·Catalog·Prefab·Material은 입력으로 사용하거나 정리하지 않습니다.

## 환경 만들기

Unity 메뉴에서 다음 명령을 실행합니다.

```text
Tools > Rogue Dungeon Lab > R6 수동 검증 환경 생성
```

Batchmode에서는 다음 메서드를 호출할 수 있습니다.

```text
RogueDungeonLab.Editor.R6ManualVerificationSetup.CreateAllFromBatch
```

명령은 다음 전용 자산을 만들거나 같은 GUID로 갱신합니다.

```text
Assets/R6ManualVerification/
├─ Settings/R6_BakeSettings.asset
├─ DropTables/R6_EnemyDrops.asset
├─ DropTables/R6_DestructibleDrops.asset
├─ Blueprints/R6_SavedBlueprint_Seed73125.asset
├─ Stages/R6_BakedStage.asset
├─ Materials/R6_DefaultBakeMaterialSet.asset
└─ Scenes/R6_BakedStageVerification.unity
```

StageDefinition 옆의 stage 전용 Bake root에는 버전별 floor/wall Mesh, Prefab과 manifest가 생성됩니다. 정상 재Bake가 commit되면 이전 manifest가 소유한 파생 자산만 정리됩니다.

이 생성기는 Settings, DropTable, Blueprint와 StageDefinition을 고정 기준값으로 갱신합니다. 기본 MaterialSet이 이미 있으면 사용자 색상 변경을 보존합니다. 개인 제작 자산은 `Assets/R6ManualVerification` 안에 저장하지 마세요.

## 기본 로드와 플레이 확인

1. 생성 뒤 열린 `R6_BakedStageVerification` 장면에서 Generator의 Stage Definition이 `R6_BakedStage`인지 확인합니다.
2. Play를 누릅니다.
3. 던전이 `__RogueDungeonLab_Generated` 하나로 로드되고 Console 오류가 없는지 확인합니다.
4. HUD에서 임시 플레이어를 만들고 `WASD`, `Shift`, `Space`, `R`로 입구부터 던전을 탐험합니다.
5. 빨간 적과 주황 파괴물을 클릭합니다. 각각 `R6 Enemy Token`, `R6 Crate Token` 드랍 표본과 마커가 기록되어야 합니다.
6. Play를 종료한 뒤 `Tools > Rogue Dungeon Lab > 실험실 열기`의 `스테이지 자산` 탭을 엽니다.
7. `R6 배포용 Bake`를 펼쳐 `현재 Generator Definition 사용`을 누르고 `최신성 다시 검사`를 실행합니다.
8. 오류가 없어야 하며 Baked Prefab과 Bake Manifest의 `Ping` 버튼이 실제 프로젝트 자산을 가리켜야 합니다.

환경 생성 명령 자체도 장면을 저장하기 전에 다음 항목을 검사합니다.

- StageInstance source가 `SavedBlueprint`
- build mode가 `BakedPrefab`
- 저장 원본과 로드 결과의 Blueprint hash 일치
- floor/wall Renderer와 Collider가 같은 영속 Mesh 자산 사용
- transient `DungeonGeneratedMeshOwner` 부재
- 적과 파괴물 각각 1개 이상
- 현재 manifest와 전체 dependency fingerprint 유효

## stale와 재Bake 확인

1. `R6_DefaultBakeMaterialSet`의 Material sub-asset 하나를 선택해 색상을 바꿉니다.
2. 스테이지 자산 탭에서 `최신성 다시 검사`를 누릅니다.
3. material dependency stale 오류가 표시되는지 확인합니다.
4. `재Bake (기존 정상 결과 보존)`을 실행합니다.
5. 최신성 검증이 다시 통과하고 변경한 색상이 Baked Prefab에 반영되는지 확인합니다.
6. 환경 생성 메뉴를 다시 실행해도 같은 입력 자산 GUID를 유지하면서 새 정상 Bake와 장면이 만들어져야 합니다.

## 실패 rollback 확인

정상 환경을 만든 뒤 다음 메뉴를 실행합니다.

```text
Tools > Rogue Dungeon Lab > R6 재Bake 실패 보존 확인
```

Batchmode 진입점은 다음과 같습니다.

```text
RogueDungeonLab.Editor.R6ManualVerificationSetup.VerifyRollbackFromBatch
```

이 명령은 새 후보를 만든 뒤 StageDefinition commit 직전에 의도적으로 실패시킵니다. 다음 조건을 자동 확인합니다.

- 예외가 정상적으로 보고됨
- 기존 Baked Prefab 참조 유지
- 기존 Bake Manifest 참조 유지
- `BakedPrefab` build mode 유지
- 실패 뒤 기존 Bake 최신성 검증 통과

실패 후보 staging/output만 정리되며 기존 정상 Bake와 사용자 입력 자산은 유지됩니다.

## Player build smoke와 별도 확인 범위

환경 생성 메뉴 자체는 Player를 만들지 않습니다. 같은 전용 Baked Scene으로 Windows Development Player까지 확인하려면 batchmode에서 다음 메서드를 호출합니다.

```text
RogueDungeonLab.Editor.R6PlayerBuildSmoke.BuildFromBatch
```

R6 통합 검증에서는 분리된 임시 프로젝트의 빌드가 성공했습니다. 다른 소비 프로젝트로 Bake 묶음을 옮기는 R9B 패키징 검증은 별도 단계입니다.
