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

Play 모드에서 임시 캐릭터가 없을 때 `W/S`는 카메라가 실제로 바라보는 3차원 방향을 따라 이동하므로 위·아래를 바라보면 높이도 함께 변합니다. `A/D`는 카메라 기준 좌·우로 이동합니다. `Space`는 월드 위쪽 상승, `Ctrl`은 월드 아래쪽 하강이며, `Shift`를 함께 누르면 모든 키보드 이동이 가속됩니다. 우클릭 드래그는 현재 카메라 위치를 유지한 제자리 회전이며, 상하 시야는 거의 수직인 `-89°~89°`까지 움직입니다. 휠은 줌, 중클릭 드래그는 평행 이동입니다.

Play HUD는 `스테이지 설정`, `탐험`, `드랍 통계` 탭으로 구성됩니다. `스테이지 설정`은 프로젝트의 settings 또는 Procedural StageDefinition recipe를 직접 바꾸지 않고 Generator가 소유한 Play 전용 복제본을 편집합니다. 입력 시드와 프리셋뿐 아니라 스테이지 가로·세로 셀, 셀 크기, 벽 높이, 방 개수와 크기, 배치 시도, 복도 폭, 추가 연결 확률, 기믹·간격·입구 반경 및 적·파괴물·지형지물 밀도 설정을 바꿀 수 있습니다. 슬라이더나 프리셋을 변경하면 현재 활성 시드를 유지한 채 자동 재생성되며, 드래그 중 발생하는 변경은 약 0.08초 간격으로 합쳐 처리됩니다. 시드 텍스트는 타이핑 도중의 중간 숫자를 생성하지 않도록 `설정 적용 및 입력 시드로 생성` 버튼으로 확정합니다. 진행도 `AnimationCurve` 자체는 계속 에디터의 분포 탭에서 편집합니다.

Procedural StageDefinition이 별도 recipe를 사용하고 Generator settings에는 드랍·런타임 옵션이 들어 있어도, HUD의 구조 변경은 recipe 복제본에 적용되고 런타임 옵션은 함께 보존됩니다. 새 출처 로드가 실패하면 후보 복제본만 폐기하고 기존 맵과 활성 설정을 유지합니다. SavedBlueprint가 활성화되면 저장된 논리 맵을 보호하기 위해 구조와 시드 편집 탭을 비활성화하며, 탐험 탭의 재생성·새 시드는 같은 저장 Blueprint만 다시 구축합니다.

HUD 패널은 현재 해상도와 가로·세로 비율을 기준으로 크기와 UI 배율을 계산합니다. 작은 화면에서는 화면 안쪽으로 폭과 높이를 제한하고, 세로 화면에서는 가용 폭을 사용하며, 탭 내용은 항상 스크롤할 수 있습니다.

HUD의 `임시 플레이어 생성 (WASD)` 버튼을 누르면 파란 캡슐 캐릭터가 입구에 생성되고 카메라가 자동 추적합니다. `WASD` 이동, `Shift` 달리기, `Space` 점프, `R` 입구 복귀를 지원합니다. HUD의 `임시 플레이어 제거 / 자유 시점` 버튼을 누르면 캐릭터가 제거되고 자유 시점으로 돌아갑니다. 던전을 재생성하면 활성 캐릭터는 새 입구로 자동 이동합니다.

빨간 캡슐과 주황 큐브를 좌클릭하면 드랍 표본이 1회 추가됩니다. 빠른 표본 버튼은 물리 오브젝트를 소모하지 않습니다.

표본이 적을 때 기대값과 큰 차이는 자연스럽습니다. 95% 구간이 충분히 좁아진 뒤에도 기대값이 구간 밖에 있으면 테이블이나 기록 로직을 점검합니다.

## R6 배포용 Bake

Bake는 임의로 바뀌는 Procedural 입력이 아니라 검수한 저장 Blueprint에서만 시작합니다.

1. 위 제작 흐름으로 Blueprint와 `SavedBlueprint StageDefinition`을 준비합니다.
2. `스테이지 자산` 탭의 `R6 배포용 Bake > Mesh·Prefab Bake`를 펼칩니다.
3. Bake할 StageDefinition을 선택합니다. 현재 Generator에 연결한 자산이면 `현재 Generator Definition 사용`을 누를 수 있습니다. Procedural Definition을 선택하면 먼저 저장본을 만들라는 차단 안내가 표시됩니다.
4. `영속 Bake 재질 세트`를 지정합니다. 처음이라면 `기본 Bake 재질 세트 자산 생성`을 누르고 프로젝트 안의 저장 위치를 선택합니다. 이 자산과 그 8개 Material 슬롯은 사용자 입력이므로 삭제하지 않고 다른 스테이지에서도 재사용할 수 있습니다.
5. `배포용 Mesh·Prefab Bake`를 누릅니다. 기존 manifest와 Prefab이 있으면 재Bake 확인으로 바뀝니다. 성공 뒤 이전 파생 자산 정리는 비가역적이므로 Bake commit은 `Ctrl+Z` 대상이 아닙니다.
6. Bake가 성공하면 StageDefinition이 생성된 Baked Prefab과 manifest를 참조하고 `BakedPrefab` 모드가 됩니다. 각 결과의 `Ping` 버튼으로 Project 창에서 위치를 찾을 수 있습니다.
7. Blueprint, Catalog Prefab, drop/gameplay 설정, 재질·Shader 또는 builder 코드가 바뀌었다면 `최신성 다시 검사`를 누릅니다. 검증 리포트가 stale 의존성을 표시하면 재Bake해야 합니다.

재Bake는 stage 전용 staging 영역에서 새 후보를 완성한 뒤 commit합니다. 생성·검증·저장 중 오류가 나면 후보만 제거하고 기존 정상 Prefab·manifest 참조를 유지합니다. 정리 대상도 이전 manifest가 소유한다고 기록한 stage bake root 안의 파생 Mesh·Prefab·manifest뿐이며, Blueprint, settings, Catalog, Catalog Prefab, 공유 Mesh·Material은 건드리지 않습니다.

R6 MVP는 알려진 built-in/fallback 표현과 `DungeonContentCatalog`가 직접 참조하는 Prefab을 지원합니다. runtime factory, Addressables, DI·오브젝트 풀 resolver는 Bake하지 않으며 RuntimeBuild에서 사용해야 합니다. BakedPrefab Loader는 Blueprint 생성, Mesh Builder와 resolver를 실행하지 않고 manifest 검증 뒤 저장 Prefab을 인스턴스화합니다.

R6 자동 회귀는 Unity `6000.5.3f1`에서 EditMode `74/74`, PlayMode `8/8`을 통과했습니다. 분리된 임시 프로젝트에서 전용 Baked 장면을 생성한 Windows Development Player 빌드도 성공했습니다. 직접 확인하려면 `Tools > Rogue Dungeon Lab > R6 수동 검증 환경 생성`을 실행하고 `Assets/R6ManualVerification/README_KO.md`의 Play·stale·rollback 순서를 따릅니다.
