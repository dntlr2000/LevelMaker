# 사용자 가이드

`Tools > Rogue Dungeon Lab > 실험실 열기`에서 장면 자동 구성을 실행합니다. 생성 탭은 크기·방·복도·기믹을, 분포 탭은 적·파괴물·지형지물 곡선을 편집합니다.

곡선 X=0은 입구, X=1은 출구, Y=1은 기본 밀도 그대로입니다. 적은 후반 상승, 파괴물은 초반 상승처럼 구성할 수 있습니다.

## 스테이지 저장과 다시 사용하기

원하는 결과가 나오면 에디터 창의 `스테이지 자산` 탭을 엽니다.

1. 실제 Prefab 카탈로그를 사용했다면 `콘텐츠 카탈로그`에 같은 자산을 지정하고 누락 정책을 선택합니다.
2. 선택적으로 제작 메모를 입력합니다. 메모와 저장 시각은 맵의 논리 hash를 바꾸지 않습니다.
3. `현재 결과를 새 Blueprint로 저장`을 눌러 `Assets` 아래에 `.asset`을 만듭니다. 현재 결과와 연결 설정의 recipe hash가 같으면 저장 당시 레시피도 함께 기록됩니다.
4. 기존 저장본을 갱신하려면 `선택 Blueprint`를 지정하고 `선택 Blueprint 덮어쓰기`를 누릅니다. 경로, 변경 전후 hash와 설정 복원 정보 포함 여부를 확인해야 진행되며, 실수한 경우 Unity Undo로 되돌릴 수 있습니다.
5. `저장본 미리보기`는 저장된 cell·room·spawn을 그대로 RuntimeBuild합니다. 현재 settings나 새 시드로 맵을 다시 계산하지 않습니다.
6. `현재 절차 설정으로 재생성`은 미리보기 전에 사용하던 시드를 현재 설정에 적용해 다시 생성합니다. 설정 자체를 저장 당시 값으로 되돌리는 버튼은 아닙니다.

## 저장 당시 설정 불러오기

선택 Blueprint에 유효한 저장 레시피가 있으면 `저장 레시피 설정 복원` 영역이 활성화됩니다.

- `레시피 설정만 불러오기 (현재 시드 유지)`: 스테이지 크기, 방·복도, 기믹·간격과 세 밀도 프로필·AnimationCurve를 현재 `RogueDungeonSettings`에 적용합니다. 현재 시드, 드랍 테이블, 드랍 마커·통계 옵션과 `generateOnPlay`는 유지합니다.
- `레시피 + 저장 시드 적용 후 절차 생성`: 위 설정과 저장 시드를 적용하고 저장 당시 generatorVersion으로 절차 생성합니다. StableV2 저장본은 현재 선택한 콘텐츠 카탈로그의 planning hash까지 같아야 활성화됩니다.

두 동작 모두 설정 자산을 바꾸기 전에 확인 창을 표시하고 Unity Undo를 지원합니다. 두 번째 동작은 실제 설정을 덮어쓰기 전에 같은 입력으로 계산한 Blueprint hash가 저장본과 일치하는지도 검사합니다. 설정 snapshot이 없는 기존 R5 자산은 맵 미리보기·StageDefinition 생성에 계속 사용할 수 있지만 설정 복원 버튼은 비활성화됩니다. snapshot 버전이나 hash가 손상된 경우에도 설정 적용만 차단됩니다.

비교 영역에는 현재 절차 결과와 저장본의 seed, generatorVersion, recipe hash, catalog hash와 Blueprint hash가 표시됩니다. 입력이 바뀐 저장본은 stale 경고, 같은 입력과 시드인데 결과가 다른 경우는 무결성 오류로 표시됩니다. 오류가 있는 데이터는 저장·미리보기·StageDefinition 생성을 할 수 없습니다.

`SavedBlueprint StageDefinition 생성`을 누르면 저장본을 참조하는 RuntimeBuild 자산이 만들어집니다. `생성 후 현재 Generator에 연결`을 켜면 현재 장면에도 바로 연결됩니다. 새 씬에서는 `RogueDungeonGenerator.stageDefinition`에 이 자산을 지정하면 되며, `Play 진입 자동 로드`가 켜져 있으면 Play 시작 시 저장 맵이 구축됩니다. 씬에 현재 settings가 없어도 geometry와 built-in 콘텐츠는 로드되며, 프로젝트별 Prefab을 쓰는 저장본은 같은 Content Catalog 또는 resolver가 필요합니다.

`__RogueDungeonLab_Generated` 아래 계층은 저장 원본이 아니라 미리보기 결과입니다. 계층을 직접 고쳐도 다시 구축하면 사라지므로 Blueprint 자산을 원본으로 관리합니다.

## R7 비파괴 Stage Override

저장 Blueprint의 구조와 원본 hash를 보존하면서 Spawn만 조정하려면 `스테이지 자산` 탭의 `R7 비파괴 Stage Override > Spawn Override 제작`을 펼칩니다.

1. 검증된 `선택 Blueprint`를 지정하고 `새 Override 생성`으로 `DungeonStageOverrides` 자산을 만듭니다.
2. 같은 Blueprint를 사용하는 SavedBlueprint StageDefinition이 현재 Generator에 연결되어 있다면 `현재 Definition에 연결`을 누릅니다. Procedural Definition에는 Override를 연결할 수 없습니다.
3. `Override 미리보기`를 실행하고 Scene View에서 적·파괴물·지형지물·기믹 오브젝트를 선택합니다. 선택 도구는 generated hierarchy의 Transform을 저장하지 않고 `DungeonSpawnIdentity.SpawnId`를 찾아 Override 자산만 수정합니다.
4. 원본 Spawn은 비활성화, 콘텐츠 key 교체와 절대 로컬 위치·회전·크기 조정이 가능합니다. 수동 Spawn은 원본의 floor cell, category, 콘텐츠 key와 절대 Transform을 입력해 추가하며 Override가 만든 stable ID를 사용합니다.
5. 변경 뒤 Override 미리보기가 활성 상태라면 전체 RuntimeBuild를 다시 구축하고 가능한 경우 같은 stable ID를 다시 선택합니다. `원본 미리보기`로 적용 전 결과를 즉시 비교할 수 있습니다.
6. `변경 목록`의 단일 `제거` 버튼으로 작업 하나만 원복할 수 있습니다. Override 미리보기 중에는 최종 결과를 실수로 원본 Blueprint에 덮어쓰지 못하도록 기존 저장 버튼이 차단됩니다.

입구·출구 Marker의 비활성화·추가·콘텐츠 교체·Transform 변경은 모두 차단됩니다. R7에서는 Spawn이 가리키는 cell, floor 구조, 방, 입구와 출구를 수정하지 않습니다. 수동 추가와 콘텐츠 교체도 최종 Blueprint 및 Content Catalog 검증을 통과해야 Preview와 Bake가 활성화됩니다.

Override는 기준 `baseBlueprintHash`, 순서 독립 `overrideHash`와 적용 결과의 `finalBlueprintHash`를 구분합니다. 원본 Blueprint가 바뀌면 조용히 적용하지 않고 `원본 변경 재결합`에서 분석해야 합니다. 분석은 다음 순서로 후보를 찾습니다.

1. 같은 stable ID가 있으면 exact 후보로 표시합니다.
2. ID가 사라졌다면 category, 기존 콘텐츠 key, cell, room과 variant seed가 모두 같은 유일 후보만 제안합니다.
3. 후보 없음, 여러 후보, 서로 다른 기존 target의 후보 충돌 또는 추가 ID 충돌은 미해결 오류로 남습니다.
4. 적용 가능한 계획도 `분석 결과 승인 및 재결합` 확인을 거쳐야 기준 참조·hash와 Binding이 하나의 Undo 단위로 바뀝니다.

미해결 충돌, 저장 hash 불일치와 stale 원본이 있으면 Override Preview와 출시용 Bake가 모두 차단됩니다. 예제 자산과 RuntimeBuild/Baked 비교 장면은 `Tools > Rogue Dungeon Lab > R7 수동 검증 환경 생성`으로 만들 수 있으며 자세한 확인 순서는 [R7 수동 검증 가이드](R7_MANUAL_VERIFICATION_KO.md)를 따릅니다.

## R8 플레이 진행 저장과 복원

R7 Override가 제작자가 만든 정적 변형이라면 R8 RunState는 한 번의 플레이에서 변한 진행만 저장합니다. Play HUD의 `런 상태` 탭에서 다음 순서로 사용합니다.

1. Procedural 또는 SavedBlueprint 스테이지를 열고 적·파괴물을 제거합니다.
2. 임시 플레이어 위치도 저장하려면 `탐험` 탭에서 플레이어를 만든 뒤 원하는 위치로 이동합니다.
3. `런 상태` 탭에 영문·숫자·`-`·`_` 조합의 슬롯 ID를 입력합니다.
4. `슬롯 저장`을 누르면 현재 기믹 participant 상태와 플레이어 pose까지 캡처해 JSON으로 저장합니다. `현재 상태 캡처`는 파일을 쓰지 않고 메모리 상태만 갱신합니다.
5. 다른 seed를 만들거나 같은 저장 맵을 재구축한 뒤 `슬롯 불러오기`를 누릅니다. Procedural 저장은 저장한 run seed를 사용하고 SavedBlueprint 저장은 영구 Stage ID를 확인합니다.
6. 더 이상 필요 없는 저장은 `슬롯 삭제`로 지웁니다.

기본 `엄격 거부`는 stage ID, source mode, run seed와 final Blueprint hash가 모두 같아야 합니다. 하나라도 다르면 현재 맵을 유지한 채 실패합니다. `일치 ID만`은 stage·source·seed가 같고 final hash만 달라졌을 때 현재 Blueprint에도 존재하는 stable spawn ID와 participant만 다시 적용합니다. 자동 migration이 아니므로 출시 세이브 형식을 바꿀 때는 코드에서 `IDungeonRunStateMigrator`를 명시적으로 제공해야 합니다.

제거 진행은 Enemy와 Destructible만 기록합니다. 입구·출구 Marker, Prop를 제거 상태로 넣거나 기믹 payload를 Gimmick이 아닌 spawn에 적용하면 검증 오류입니다. 프로젝트 기믹은 해당 spawn 아래의 `MonoBehaviour`에서 `IDungeonRunStateParticipant`를 구현하고 안정적인 `RunStateKey`, 문자열 캡처와 복원 메서드를 제공하면 됩니다.

기본 저장 위치는 `Application.persistentDataPath/RogueDungeonLab/RunStates/<slot>.json`입니다. 다른 계정·클라우드 저장소를 쓰려면 `RogueDungeonGenerator.SetRunStateStore`로 `IDungeonRunStateStore` 구현을 주입합니다.

직접 확인할 수 있는 장면과 절차는 `Tools > Rogue Dungeon Lab > R8 수동 검증 환경 생성` 및 [R8 수동 검증 가이드](R8_MANUAL_VERIFICATION_KO.md)에 있습니다.

다음 자유 시점·임시 플레이어·Play HUD는 R9에서 선택 `RogueDungeonLab.Samples`로 분리되었습니다. 원본 실험실에는 계속 포함되지만 Runtime Core만 가져온 제품 프로젝트에는 포함되지 않습니다.

Play 모드에서 임시 캐릭터가 없을 때 `W/S`는 카메라가 실제로 바라보는 3차원 방향을 따라 이동하므로 위·아래를 바라보면 높이도 함께 변합니다. `A/D`는 카메라 기준 좌·우로 이동합니다. `Space`는 월드 위쪽 상승, `Ctrl`은 월드 아래쪽 하강이며, `Shift`를 함께 누르면 모든 키보드 이동이 가속됩니다. 우클릭 드래그는 현재 카메라 위치를 유지한 제자리 회전이며, 상하 시야는 거의 수직인 `-89°~89°`까지 움직입니다. 휠은 줌, 중클릭 드래그는 평행 이동입니다.

Play HUD는 `스테이지 설정`, `탐험`, `런 상태`, `드랍 통계` 탭으로 구성됩니다. `스테이지 설정`은 프로젝트의 settings 또는 Procedural StageDefinition recipe를 직접 바꾸지 않고 Generator가 소유한 Play 전용 복제본을 편집합니다. 입력 시드와 프리셋뿐 아니라 스테이지 가로·세로 셀, 셀 크기, 벽 높이, 방 개수와 크기, 배치 시도, 복도 폭, 추가 연결 확률, 기믹·간격·입구 반경 및 적·파괴물·지형지물 밀도 설정을 바꿀 수 있습니다. 슬라이더나 프리셋을 변경하면 현재 활성 시드를 유지한 채 자동 재생성되며, 드래그 중 발생하는 변경은 약 0.08초 간격으로 합쳐 처리됩니다. 시드 텍스트는 타이핑 도중의 중간 숫자를 생성하지 않도록 `설정 적용 및 입력 시드로 생성` 버튼으로 확정합니다. 진행도 `AnimationCurve` 자체는 계속 에디터의 분포 탭에서 편집합니다.

Procedural StageDefinition이 별도 recipe를 사용하고 Generator settings에는 드랍·런타임 옵션이 들어 있어도, HUD의 구조 변경은 recipe 복제본에 적용되고 런타임 옵션은 함께 보존됩니다. 새 출처 로드가 실패하면 후보 복제본만 폐기하고 기존 맵과 활성 설정을 유지합니다. SavedBlueprint가 활성화되면 저장된 논리 맵을 보호하기 위해 구조와 시드 편집 탭을 비활성화하며, 탐험 탭의 재생성·새 시드는 같은 저장 Blueprint만 다시 구축합니다.

HUD 패널은 현재 해상도와 가로·세로 비율을 기준으로 크기와 UI 배율을 계산합니다. 작은 화면에서는 화면 안쪽으로 폭과 높이를 제한하고, 세로 화면에서는 가용 폭을 사용하며, 탭 내용은 항상 스크롤할 수 있습니다.

HUD의 `임시 플레이어 생성 (WASD)` 버튼을 누르면 파란 캡슐 캐릭터가 입구에 생성되고 카메라가 자동 추적합니다. `WASD` 이동, `Shift` 달리기, `Space` 점프, `R` 입구 복귀를 지원합니다. HUD의 `임시 플레이어 제거 / 자유 시점` 버튼을 누르면 캐릭터가 제거되고 자유 시점으로 돌아갑니다. 일반 재생성에서는 활성 캐릭터가 새 입구로 이동하고, RunState를 불러온 재생성에서는 저장된 플레이어 pose가 있으면 그 위치와 회전을 우선 복원합니다.

빨간 캡슐과 주황 큐브를 좌클릭하면 드랍 표본이 1회 추가됩니다. 빠른 표본 버튼은 물리 오브젝트를 소모하지 않습니다.

표본이 적을 때 기대값과 큰 차이는 자연스럽습니다. 95% 구간이 충분히 좁아진 뒤에도 기대값이 구간 밖에 있으면 테이블이나 기록 로직을 점검합니다.

## R6·R7 배포용 Bake

Bake는 임의로 바뀌는 Procedural 입력이 아니라 검수한 저장 Blueprint에서만 시작합니다.

1. 위 제작 흐름으로 Blueprint와 `SavedBlueprint StageDefinition`을 준비합니다.
2. `스테이지 자산` 탭의 `R6·R7 배포용 Bake > Mesh·Prefab Bake`를 펼칩니다.
3. Bake할 StageDefinition을 선택합니다. 현재 Generator에 연결한 자산이면 `현재 Generator Definition 사용`을 누를 수 있습니다. Procedural Definition을 선택하면 먼저 저장본을 만들라는 차단 안내가 표시됩니다.
4. `영속 Bake 재질 세트`를 지정합니다. 처음이라면 `기본 Bake 재질 세트 자산 생성`을 누르고 프로젝트 안의 저장 위치를 선택합니다. 이 자산과 그 8개 Material 슬롯은 사용자 입력이므로 삭제하지 않고 다른 스테이지에서도 재사용할 수 있습니다.
5. `배포용 Mesh·Prefab Bake`를 누릅니다. 기존 manifest와 Prefab이 있으면 재Bake 확인으로 바뀝니다. 성공 뒤 이전 파생 자산 정리는 비가역적이므로 Bake commit은 `Ctrl+Z` 대상이 아닙니다.
6. Bake가 성공하면 StageDefinition이 생성된 Baked Prefab과 manifest를 참조하고 `BakedPrefab` 모드가 됩니다. 각 결과의 `Ping` 버튼으로 Project 창에서 위치를 찾을 수 있습니다.
7. Blueprint, Stage Override, Catalog Prefab, drop/gameplay 설정, 재질·Shader 또는 builder 코드가 바뀌었다면 `최신성 다시 검사`를 누릅니다. 검증 리포트가 stale 의존성을 표시하면 재Bake해야 합니다.

재Bake는 stage 전용 staging 영역에서 새 후보를 완성한 뒤 commit합니다. 생성·검증·저장 중 오류가 나면 후보만 제거하고 기존 정상 Prefab·manifest 참조를 유지합니다. 정리 대상도 이전 manifest가 소유한다고 기록한 stage bake root 안의 파생 Mesh·Prefab·manifest뿐이며, Blueprint, settings, Catalog, Catalog Prefab, 공유 Mesh·Material은 건드리지 않습니다.

R6 MVP는 알려진 built-in/fallback 표현과 `DungeonContentCatalog`가 직접 참조하는 Prefab을 지원합니다. runtime factory, Addressables, DI·오브젝트 풀 resolver는 Bake하지 않으며 RuntimeBuild에서 사용해야 합니다. BakedPrefab Loader는 Blueprint 생성, Mesh Builder와 resolver를 실행하지 않고 manifest 검증 뒤 저장 Prefab을 인스턴스화합니다.

기존 R6 format/builder v1 manifest는 Override가 없고 source/final Blueprint hash가 같은 경우 계속 로드됩니다. 새 Bake는 v2를 기록하며, StageDefinition과 manifest의 Override 자산 참조, `overrideHash`와 적용 뒤 `finalBlueprintHash`가 모두 일치해야 합니다. Baked 로드는 순수 Override applier로 최종 논리 Blueprint와 report를 복원하지만 생성기·Mesh Builder·resolver를 다시 실행하지 않습니다.

R6 자동 회귀는 Unity `6000.5.3f1`에서 EditMode `74/74`, PlayMode `8/8`을 통과했습니다. 분리된 임시 프로젝트에서 전용 Baked 장면을 생성한 Windows Development Player 빌드도 성공했습니다. 직접 확인하려면 `Tools > Rogue Dungeon Lab > R6 수동 검증 환경 생성`을 실행하고 `Assets/R6ManualVerification/README_KO.md`의 Play·stale·rollback 순서를 따릅니다.

## R9 다른 프로젝트로 내보내기

전체 기준 배포본은 다음 메뉴로 생성합니다.

```text
Tools > Rogue Dungeon Lab > R9 배포 패키지 생성
```

특정 스테이지만 내보낼 때는 실험실의 `스테이지 자산` 탭에서 `R6·R7 배포용 Bake`를 펼치고 그 안의 `R9 다른 프로젝트용 패키지`를 사용합니다. Baked Stage는 먼저 `최신성 다시 검사`가 통과해야 합니다. 출력 폴더의 `.unitypackage.json`을 열어 요구 Unity package와 render pipeline을 확인한 뒤 수신 프로젝트에 설치합니다.

제품에서 절차형 또는 저장형 RuntimeBuild만 필요하면 Runtime Core만 가져옵니다. 예제는 선택이며, 실험실 HUD·카메라·임시 플레이어도 Lab Sample과 Input System을 명시적으로 추가한 프로젝트에만 들어옵니다. Core만 사용한 제품 장면에는 스테이지 빌더 HUD가 표시되지 않습니다.

Baked Stage는 다음 중 하나로 설치합니다.

1. 여러 Stage가 Core 하나를 공유하면 `runtime-core`를 한 번 가져온 뒤 각 modular `stage-<id>`를 가져옵니다.
2. 독립 전달물이 필요하면 `stage-<id>-standalone` 하나를 가져옵니다.

가져온 뒤 `DungeonStageDefinition`을 제품 장면의 `RogueDungeonGenerator`에 연결합니다. 제품 캐릭터의 위치를 RunState에 포함하려면 Sample 플레이어 대신 `IDungeonRunStatePlayer`를 구현해 Generator에 등록합니다. 자세한 파일명, 제작 도구 조합, sidecar hash와 자동 smoke 절차는 [R9 패키지 가이드](R9_PACKAGE_GUIDE_KO.md)를 참고합니다.
