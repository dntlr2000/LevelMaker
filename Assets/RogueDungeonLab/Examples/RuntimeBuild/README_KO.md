# RuntimeBuild Core 예제

이 예제는 `RogueDungeonLab.Runtime` Core만 사용하며 실험실 HUD, 자유 카메라,
클릭 입력과 임시 플레이어를 포함하지 않습니다.

- `Scenes/R9_ProceduralRuntimeBuild.unity`: 고정 seed 절차 생성
- `Scenes/R9_SavedBlueprintRuntimeBuild.unity`: 저장 Blueprint를 재계산 없이 구축
- `Stages`: 두 장면이 사용하는 StageDefinition
- `Settings`, `Blueprints`: 예제 입력 자산

Runtime Core package를 먼저 가져온 뒤 Runtime Examples package를 가져오십시오.
장면의 `RogueDungeonGenerator.stageDefinition`만 제품의 Definition으로 바꾸면 같은
구조로 연계할 수 있습니다.
