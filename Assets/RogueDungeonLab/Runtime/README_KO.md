# Rogue Dungeon Lab Runtime Core

이 폴더만 다른 Unity `6000.5` 프로젝트의 `Assets/RogueDungeonLab/Runtime`으로
복사하거나 `rogue-dungeon-lab-runtime-core.unitypackage`를 가져오면 절차 생성,
Blueprint, RuntimeBuild/BakedPrefab Loader, Override, Bake Runtime 계약과 RunState를
사용할 수 있습니다.

- 외부 Unity package 의존성 없음
- `UnityEditor` 및 Unity 전역 `Random` 참조 없음
- `RogueDungeonLab.Runtime` 어셈블리 이름 유지
- 실험실 HUD, 자유 카메라, 클릭 입력과 임시 플레이어는 포함하지 않음

제품 장면에서는 `RogueDungeonGenerator`에 `DungeonStageDefinition`을 연결하거나
코드에서 `DungeonStageLoader.Load`를 호출합니다. 제품 플레이어의 위치까지
RunState에 저장하려면 `IDungeonRunStatePlayer`를 구현하고 Generator에 등록합니다.
