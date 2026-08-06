# R8 런 상태 수동 검증

장면:

`Assets/R8ManualVerification/Scenes/R8_RunStateVerification.unity`

## Procedural 재개

1. 장면을 열고 `R8 Procedural Generator (ACTIVE)`만 활성 상태인지 확인합니다.
2. Play를 누르고 HUD `탐험` 탭에서 임시 플레이어를 만듭니다.
3. 적 또는 주황 파괴물을 2개 이상 좌클릭하고 캐릭터를 입구에서 떨어진 곳으로 이동합니다.
4. `런 상태` 탭에서 슬롯 ID `slot-1`, 정책 `엄격 거부`를 선택하고 `슬롯 저장`을 누릅니다.
5. `스테이지 설정` 또는 `탐험` 탭에서 새 시드로 생성합니다.
6. `런 상태` 탭의 `슬롯 불러오기`를 누릅니다.

확인 결과:

- 저장 당시 seed로 돌아옵니다.
- 제거했던 stable spawn이 다시 나타나지 않습니다.
- 임시 플레이어가 저장한 위치와 회전으로 돌아옵니다.
- `최근 복원`이 `정확 일치`로 표시됩니다.

## SavedBlueprint 재개

1. Play를 종료합니다.
2. `R8 Procedural Generator (ACTIVE)`를 비활성화하고 `R8 Saved Generator (enable after disabling Procedural)`를 활성화합니다.
3. Play를 다시 누른 뒤 적·파괴물을 제거하고 `saved-1` 슬롯에 저장합니다.
4. `탐험` 탭에서 현재 시드를 재생성해 저장 맵을 원본 상태로 다시 구축합니다.
5. `런 상태` 탭에서 `saved-1`을 불러옵니다.

확인 결과:

- `r8-manual-saved-v1` Stage ID와 저장 Blueprint seed가 유지됩니다.
- 제거했던 대상이 stable spawn ID 기준으로 다시 제거됩니다.
- SavedBlueprint의 구조·시드 설정 탭은 계속 편집 불가 상태입니다.

## 불일치 정책

- 기본 `엄격 거부`는 stage ID, source, seed 또는 final Blueprint hash가 다르면 기존 정상 맵을 유지하고 불러오기를 실패시킵니다.
- `일치 ID만`은 stage ID·source·seed가 같고 final hash만 달라졌을 때만 존재하는 stable ID 상태를 재결합합니다.
- 제품 세이브 migration은 UI의 일치-ID 정책과 별개로 `IDungeonRunStateMigrator`를 명시적으로 전달해야 합니다.

저장 파일은 기본적으로 `Application.persistentDataPath/RogueDungeonLab/RunStates/<slot>.json`에 기록됩니다.
