# 테스트 계획

## R0·R1·R2·R3·R4·R5·R5.1 자동 회귀 기준

Unity `6000.5.3f1`에서 compile 성공 후 EditMode 51개와 PlayMode 3개를 자동 실행했습니다.

- Compact `12345`, Balanced `-987654321`, Chaos `20260719`의 방·floor·BFS·콘텐츠 셀 SHA-256 Golden 지문
- 같은 프리셋·시드 반복 생성 지문 일치
- 세 프리셋에 분산한 100개 시드의 모든 floor 셀 연결성
- 기존 `RogueDungeonGenerator` 공개 상태, `CurrentBlueprint`, `GenerationCompleted`, `__RogueDungeonLab_Generated` 유지
- Recipe 스냅샷 정규화, 원본 설정 비변경, 생성 관련 필드만의 해시 반영
- GenerationRequest의 시드·생성기 버전·카탈로그 해시·요청 ID 캡처
- Blueprint JSON round-trip, 깊은 복사, 목록 재정렬에 독립적인 canonical 해시
- BlueprintAsset의 독립 복사본 저장과 해시 갱신
- 연결 단절, spawn ID 중복, 저장 해시 불일치의 안정적인 오류 코드
- LegacyV1 요청에서 생성한 Blueprint의 연결성·참조·해시와 layout·콘텐츠 개수 일치
- 같은 Blueprint를 두 번 구축한 메시·계층·transform·stable spawn ID 일치
- 기존 layout 기반 `DungeonContentSpawner.Spawn` wrapper와 Blueprint 기반 구축 결과 일치
- explicit → run → fixed → random 시드 정책 우선순위와 random provider 호출 조건
- Procedural StageDefinition의 Blueprint 구축과 재로드 시 generated root 단일 유지
- SavedBlueprint가 변경된 recipe·명시 시드·run seed·random provider로 재계산되지 않음
- 누락 source, 미지원 BakedPrefab, 잘못된 generatorVersion과 손상 Blueprint의 코드 기반 차단
- 한 Generator에서 Saved Definition과 기존 settings-only `GenerateWithSeed` facade를 모두 사용
- 카탈로그 entry와 tag 재정렬에 독립적인 planning hash, Prefab 참조 제외
- 카탈로그 중복 key, 잘못된 progression·footprint·간격의 `RDL-CAT-*` 오류
- StableV2 요청 생성 뒤 원본 catalog를 바꿔도 snapshot과 Blueprint가 변하지 않음
- 같은 StableV2 설정·시드에서 catalog Inspector 순서가 달라도 같은 planning hash·Blueprint·contentKey 생성
- Enemy 후보 key 변경이 Destructible·Prop 결과를 밀지 않는 범주별 난수 독립성
- PCG32 Layout/음수 seed/child stream golden vector와 bounded 정수 범위
- 기본 Prefab resolver가 built-in key를 교체하고 transform·`DungeonSpawnIdentity`·클릭 대상 계약을 유지
- 누락 콘텐츠의 `Error`, `BuiltInFallback`, `Skip` 구축 정책
- Blueprint와 catalog의 progression·footprint 교차 검증
- StableV2 StageLoader가 catalog를 사용하고 strict 실패 때 기존 generated root를 보존
- 요청 snapshot 사후 변조·잘못된 planning entry·예약 built-in key/category 차단
- spawn room ID와 실제 floor cell의 room 소속 일치 및 실제 셀 기준 Room/Corridor 조건 검증
- Prefab target 설정 우선 보존과 catalog dropTable·gameplayId 자동 보강
- 비활성 staging root에서 구축 후 교체·활성화하고 명시적으로 소유한 합성 Mesh만 해제
- 자식 `DestructibleDropTarget` 파괴가 spawn root 전체를 제거하고 드랍 통계를 정확히 1회 누적
- 현재 Blueprint를 새 자산에 deep copy하고 제작 메모·저장 시각·논리 hash를 재임포트 후 유지
- 선택 Blueprint 덮어쓰기 후 Unity Undo가 중첩 cells·rooms·spawns 전체를 이전 상태로 복구
- 동일 결과, 다른 시드, stale 입력, 동일 provenance 결과 분기와 손상 저장본 비교 상태 분류
- 저장 Blueprint 미리보기가 recipe·시드를 재계산하지 않고 저장 hash를 구축한 뒤 절차 시드로 복귀
- SerializedObject로 생성한 SavedBlueprint StageDefinition과 Generator 연결이 AssetDatabase 재임포트 후 유지
- 검증 오류가 있는 현재 결과를 프로젝트 자산으로 만들기 전에 차단
- 현재 Blueprint와 일치하는 authoring recipe snapshot을 깊은 복사해 저장하고 재임포트 후 hash 유지
- Blueprint 덮어쓰기 Undo가 중첩 맵 데이터와 저장 레시피를 함께 이전 상태로 복구
- 저장 레시피 적용 시 구조·밀도·AnimationCurve가 복원되고 시드·드랍·런타임 옵션은 선택 정책대로 보존
- snapshot 없는 기존 R5 자산은 저장 맵 로드가 유지되고 설정 복원만 차단
- 변조된 snapshot의 recipe hash 불일치를 설정 자산 변경 전에 차단
- 저장 recipe·seed·StableV2·catalog 입력으로 절차 재생성한 Blueprint hash가 저장본과 일치

R5.1 구현 시점의 실행 결과는 compile 성공, EditMode `51/51`, PlayMode `3/3` 통과입니다. EditMode는 R5 자산 생성·덮어쓰기 Undo·재임포트·stale 비교·미리보기·StageDefinition 참조와 R5.1 저장 레시피 영속성·복원·손상 차단·정확 재생성을 포함합니다. PlayMode 세 테스트는 기존 임시 플레이어·카메라·HUD 자동 재생성, settings와 cellSize가 다른 저장 Blueprint의 입구 배치, 자식 클릭 대상의 루트 제거와 드랍 통계 1회 누적을 확인합니다. 실제 Unity 프로세스 종료 후 재시작, 마우스 포인터 Raycast와 HUD 위 클릭 차단은 수동 검증 항목입니다.

## 전체 기능 검증 목록

아래는 자동 회귀 외에 수동·통합·제품화 단계까지 포함한 전체 체크리스트입니다. 체크리스트에 있다는 사실만으로 이번 R5.1 자동 실행에서 확인했다는 의미는 아닙니다.

- 신규 Unity 6 프로젝트 import 후 컴파일 오류 0개
- 장면 자동 구성 3회 후 Generator/Systems/Camera/Light 중복 없음
- 자동 생성 설정·드랍 테이블을 Unity 재시작 후 다시 로드 가능
- 동일 설정·시드의 방 수, 셀 수, 콘텐츠 위치 동일
- Procedural 시드 정책이 explicit → run → fixed → random 우선순위를 따름
- SavedBlueprint 로드가 현재 recipe와 seed 입력에 영향받지 않고 저장 hash를 유지
- StageDefinition 재로드 후 `__RogueDungeonLab_Generated` root가 하나만 존재
- 손상된 저장 Blueprint는 기존 정상 root를 교체하기 전에 로드가 차단됨
- StableV2는 StageDefinition 또는 `CreateStableV2`에서 명시적으로 선택할 때만 활성화되고 기존 facade는 LegacyV1 유지
- Content Catalog의 key/category/progression/room 조건/footprint와 Blueprint가 일치
- custom `IDungeonContentResolver`와 실행별 missing policy override가 프로젝트 생성 경로를 사용
- 스테이지 자산 탭에서 새 Blueprint 저장, 확인 후 덮어쓰기, 저장본 미리보기와 현재 절차 설정 재생성이 동작
- 저장 레시피 설정만 불러오기와 설정·저장 시드 절차 재생성이 확인·Undo·catalog hash 정책을 따름
- recipe/catalog/generatorVersion 변경 시 저장본 stale 경고가 표시되고 동일 provenance 결과 분기는 오류로 구분
- 생성한 SavedBlueprint StageDefinition을 빈 씬 Generator에 연결해 Play 진입 시 동일 blueprintHash를 로드
- 모든 floor cell의 BFS distance가 0 이상
- 최소 12×12 및 최대 96×96 극단 설정에서 예외 없음
- 곡선 0 구간에서 해당 콘텐츠 억제
- maxCount와 contentSpacingCells 준수
- Play 좌클릭 1회가 attempts +1
- 빠른 표본 1,000회가 attempts에 정확히 1,000회 반영
- HUD 버튼 클릭이 뒤의 대상 파괴를 유발하지 않음
- 임시 캐릭터가 없을 때 W/S 입력이 카메라의 실제 3차원 시선축을 따라 높이까지 이동하고 Space/Ctrl로 월드 상승·하강하며 Shift에서 속도가 증가
- 우클릭 드래그로 카메라 피치가 기존 15도 제한을 넘어 위쪽으로 회전하고 -89~89도 범위를 벗어나지 않음
- 상승·하강 또는 WASD 이동 후 자유 시점 우클릭 회전이 카메라 위치를 바꾸지 않음
- Play HUD 설정 탭에서 입력한 정수 시드가 생성 버튼 1회로 설정과 결과에 반영
- Play HUD 슬라이더 또는 프리셋 변경이 별도 버튼 없이 활성 시드를 유지한 채 자동 재생성되고 연속 드래그 요청이 제한 주기로 합쳐짐
- 320×180, 360×800, 3840×1080에서 HUD 패널이 화면 경계를 벗어나지 않고 모든 탭을 스크롤 가능
- Play HUD의 생성 버튼 1회로 입구에 임시 캐릭터가 하나만 생성됨
- 반복 생성 버튼 입력에도 임시 캐릭터가 중복 생성되지 않음
- 생성된 Floor와 Walls에 유효한 MeshCollider가 있고 캐릭터가 바닥과 벽을 통과하지 않음
- 임시 캐릭터의 WASD 이동, Shift 달리기, Space 점프, R 입구 복귀가 동작
- 임시 캐릭터 활성 중 카메라가 캐릭터를 추적하고 자유 카메라 WASD 이동은 중지
- 던전 재생성 후 임시 캐릭터가 새 입구로 이동하며 null 예외 없음
- 임시 캐릭터 제거 후 카메라 추적이 해제되고 WASD 자유 시점 이동으로 복귀
- Nothing 100% 테이블에서 마커 없음
- 모든 가중치 0에서 예외 없는 invalid no-drop
- 10,000회 표본에서 관측치가 기대치 근처로 수렴
- 반복 재생성 후 동적 Mesh 누수 없음
