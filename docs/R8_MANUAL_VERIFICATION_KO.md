# R8 런 상태 저장·복원 수동 검증

## 환경 만들기

Unity 메뉴에서 다음을 실행합니다.

```text
Tools > Rogue Dungeon Lab > R8 수동 검증 환경 생성
```

Batchmode에서는 다음 진입점을 사용할 수 있습니다.

```text
-executeMethod RogueDungeonLab.Editor.R8ManualVerificationSetup.CreateAllFromBatch
```

생성되는 핵심 자산은 다음과 같습니다.

```text
Assets/R8ManualVerification/
├─ Settings/R8_RunStateSettings.asset
├─ Blueprints/R8_SavedBlueprint_Seed82468.asset
├─ Stages/R8_ProceduralStage.asset
├─ Stages/R8_SavedStage.asset
└─ Scenes/R8_RunStateVerification.unity
```

Procedural Definition은 `r8-manual-procedural-v1`, Saved Definition은 `r8-manual-saved-v1`의 영구 Stage ID를 사용합니다.

## 승인 절차

### 1. 절차형 run seed

1. Procedural Generator만 활성화하고 Play에 진입합니다.
2. 임시 플레이어를 만든 뒤 적·파괴물을 제거하고 위치를 옮깁니다.
3. `런 상태` 탭에서 `slot-1`에 저장합니다.
4. 새 seed로 생성한 뒤 `slot-1`을 엄격 정책으로 불러옵니다.
5. 원 seed, 제거 대상과 플레이어 pose가 복원되는지 확인합니다.

### 2. SavedBlueprint stage ID

1. Play를 종료하고 Procedural Generator를 끈 뒤 Saved Generator를 켭니다.
2. 다시 Play에 진입해 대상을 제거하고 `saved-1`에 저장합니다.
3. 현재 저장 맵을 재생성한 뒤 `saved-1`을 불러옵니다.
4. 같은 Stage ID에서 대상이 복원되고 구조 설정은 계속 잠겨 있는지 확인합니다.

### 3. 실패 안전성

1. 정상 상태를 저장한 뒤 StageDefinition의 `stageId`를 임시로 다른 값으로 바꿉니다.
2. 엄격 정책으로 같은 슬롯을 불러옵니다.
3. 오류가 표시되고 현재 `__RogueDungeonLab_Generated`가 사라지거나 빈 맵으로 교체되지 않는지 확인합니다.
4. Stage ID를 원래 값으로 되돌립니다.

### 4. 화면과 수명 주기

- 16:9, 좁은 Game View와 세로 비율에서 네 탭과 버튼을 모두 스크롤해 접근합니다.
- 저장 직후 script/domain reload를 수행하고 슬롯을 다시 불러옵니다.
- 임시 플레이어가 없는 저장은 입구 시작을 유지하고, 플레이어가 있는 저장만 pose를 복원하는지 확인합니다.

## 자동 검증과의 경계

자동 EditMode는 canonical hash, JSON 손상, 실제 파일 교체 실패 시 이전 슬롯 보존, 엄격·matching-ID·migration, participant payload, RuntimeBuild/BakedPrefab parity와 후보 root rollback을 검사합니다. PlayMode는 클릭 파괴, procedural seed, SavedBlueprint stage ID와 플레이어 pose 재개를 검사합니다.

수동 범위는 실제 HUD 가독성, 사용자 토글 흐름과 Play 중 script/domain reload의 체감 확인입니다.
