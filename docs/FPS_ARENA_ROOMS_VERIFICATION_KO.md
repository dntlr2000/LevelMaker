# FPS Arena 방 구획 구현·검증 보고서

검증일: 2026-10-01. Unity `6000.5.3f1`. 원본 적용 위치: `E:\Unity\LevelMaker`.

## 결과와 사용법

층별 방 개수를 지정하면 연속 내부 벽과 연결 출입구를 생성한다. 출입구를 모두 막았을 때 바닥 flood 영역 수가 실제 방 개수와 같아야 하므로, 벽 끝을 돌아 다른 방으로 빠지는 구획은 허용하지 않는다. 방별 식별자·면적·중심점, 출입구의 양쪽 방과 연결 그래프를 함께 기록한다. 열린 출입구와 계단을 통해 스폰에서 모든 방에 도달할 수 있다.

`Tools > Rogue Dungeon Lab > FPS 아레나 제작`의 **지형·계단** 탭에서 **방 구획 사용**을 켜고 **층별 방 개수**를 설정한다. **방 구획 FPS 예제 장면 만들기** 버튼/메뉴 또는 `Assets/FpsArenaRoomsExample/RoomPartitions.unity`로 2층·층별 4방 예제를 열 수 있다. 설정 자산은 같은 폴더의 `RoomSettings.asset`이다. 결과 패널에서 실제/요청 방 수, 면적, 출입구 연결과 감소 이유를 확인한다. 생성기를 선택하면 Scene 뷰에도 방 이름과 연결 선이 표시된다.

| 설정 | 범위·의미 |
|---|---|
| 방 구획 사용 | V2 명시적 opt-in, 기본 OFF |
| 층별 방 개수 | 1–12, 모든 층에 같은 목표 적용 |
| 최소 방 폭 / 면적 | 4–12셀 / 16–256셀. 실제 바닥의 두 축 폭과 면적 검사 |
| 방 출입구 폭 | 1–3셀. 실제 폭 = 셀 수 × 셀 크기 |
| 구획 벽 높이 / 두께 | 2–6m 및 층 높이−0.25m 이하 / 0.15–1m |

기존 블록·원통·코너 엄폐물과 적·아이템·기믹을 방 안에 함께 배치한다. 콘텐츠 전체 footprint가 한 방에 들어가야 하며 벽 양쪽·출입구·스폰·계단·이동로 예약 영역을 피한다. 구획 벽은 실제 non-trigger BoxCollider를 가지며 출입구에는 통로를 막는 Collider가 없다.

## 설계와 호환

- 독립 시드 stream으로 층별 가장 큰 분할 가능 영역을 BSP 방식으로 나눈다. 벽은 외곽/기존 벽까지 연속으로 이어진다. 한 분할당 출입구 한 개와 양쪽 벽 구간을 만들며, 최종 연결 그래프는 층별 tree다.
- 기존 시드 계단 배치를 먼저 유지한다. 계단·랜딩 보호 사각형을 가로지르는 분할과 기존 문 접근 영역을 자르는 분할을 배제한다. 각 방의 스폰·모든 문 lane·계단 lane에서 방 중심까지 폭이 있는 경로를 예약한다.
- 최소 크기·보호 조건으로 목표 수를 만들지 못하면 실제 수와 한국어 원인을 기록한다. 20×20 지도에 최소 폭 12셀·면적 256셀·목표 12방을 지정하면 층별 1방으로 보고한다.
- 방 OFF에서는 기존 V1/V2 및 `internalWalls` 벽 조각 모드의 레이아웃·콘텐츠·해시를 보존한다. 이전 설정은 **이전 벽 조각 설정 (호환)**에 남는다. 방 ON에서는 방 구획을 우선하며 이전 벽 조각 설정은 사용하지 않는다.
- 입력 recipe 대신 정규화 사본을 사용한다. 저장된 built recipe/catalog snapshot으로 Edit/Play/domain reload에서 재구성한다. `__RogueDungeonLab_Generated`와 기존 공개 API를 유지했다.

## 실제 적용 파일

| 파일·위치 | 변경 |
|---|---|
| `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaRoomPlanner.cs` | 새 BSP 분할, 보호 영역, 방·문 그래프, 방 내부 이동로 |
| 같은 폴더 `FpsArenaRooms.cs` | 새 방/벽/문/report DTO, 경계 이동 판정, 닫힌 방 flood 검증 |
| 같은 폴더 `FpsArenaSettings.cs`, `FpsArenaFlexiblePlanner.cs`, `FpsArenaLayout.cs`, `FpsArenaSceneBuilder.cs`, `FpsArenaWalls.cs` | 설정·콘텐츠 배치·연결성·장면 Collider·OFF/이전 해시 snapshot |
| `Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs` | 한국어 설정·결과, Scene 표시, 예제 메뉴 |
| `Assets/RogueDungeonLab/Tests/EditMode/FpsArenaRoomTests.cs`, `FpsArenaRoomsOffBaseline.json` 및 `.meta` | 7개 테스트와 변경 직전 Unity 해시 24개 |
| `Assets/RogueDungeonLab/Tests/PlayMode/FpsArenaRoomTraversalTests.cs` 및 `.meta` | 물리 문·벽·계단·인접 통로 검사 2개 |
| `UpmPackages/...core/Runtime/Arena`, `UpmPackages/...lab/Editor/FpsArenaWindow.cs` | 해당 canonical 소스·GUID byte parity |
| `Assets/FpsArenaRoomsExample` 및 `.meta` | 새 설정 자산과 완성된 2층 장면 |
| `tools/ArenaRoomsUnityValidation.cs`, `tools/verify-arena-rooms-static.py` | 재실행 가능한 Unity helper와 정적 검사 |
| `README.md`, `CHANGELOG.md`, `PLANS.md`, `docs/ARCHITECTURE_KO.md`, `docs/FPS_ARENA_GUIDE_KO.md`, `docs/TEST_PLAN_KO.md`, 이 보고서 | 동작·사용법·검증 문서 |

파일별 전/후 SHA-256은 `E:\CodexValidation\ArenaRooms_20261001\Rollback\AppliedFiles.json`에 기록한다. 이전 벽 예제·보고서와 다른 사용자 편집 파일을 보존했다. git commit/push/reset 및 사용자 Unity 강제 종료를 수행하지 않았다. `E:\Unity\RoguelitePvP` 파일·프로세스에는 변경을 하지 않았다.

## 실제 Unity 검증

증거 폴더: `E:\CodexValidation\ArenaRooms_20261001\Evidence`.

| 검증 | 결과·증거 |
|---|---|
| 실제 원본 컴파일 | 종료 코드 0, 컴파일 오류 0 (`OriginalCompile.log`) |
| 최종 전체 EditMode | **146/146 통과**, 실패/skip 0 (`FullEditMode.xml`, 104.77초) |
| 최종 전체 PlayMode | **17/17 통과**, 실패/skip 0 (`FullPlayMode.xml`, 4.63초) |
| OFF / 이전 벽 해시 | 변경 직전 Unity에서 캡처한 V1/V2 × 벽 OFF/ON × 3형상 × 1/4층 **24개 해시 정확히 일치**. 미사용 새 설정을 바꿔도 일치 |
| 정확한 목표 방 수 | 32×32 직사각형에서 1/2/3/4/6/8/12방 × 시드 0/71/−7. 닫힌 문 flood 수와 연결 tree 검사 |
| 다층·형상·시드 | **216개 프로필**: 3형상 × 3크기 × 1/2/4층 × 2/4/8/12방 × 2시드. 최소 실제 폭·면적, 콘텐츠를 모두 장애물로 본 연결성, 방 경계 끝 우회 차단, 감소 report 검사 |
| 실제 Collider / 이동 | 벽 치수·콘텐츠 Bounds 비겹침, 모든 문 lane CharacterController 통과, solid 벽 차단, 4층 모든 계단 lane 상승/하강. 열린 인접 통로 1,000개 이상과 닫힌 경계 20개 이상을 CapsuleCast로 대조 |
| 설정·Undo·재열기 | SerializedObject 수정/Undo, 자산 저장/import, 장면 저장/재열기, 원본 설정 변경 뒤 built snapshot 복원, 반복 생성 root 1개 |
| Play/domain reload | Play 진입 → Play 중 `RequestScriptReload` → Edit 복귀. 방 8개·같은 hash·root 1개 복원, 오류 0 (`PlayDomainReload.json`) |
| Editor UI | 실제 EditorWindow의 6탭과 스크롤 결과 패널에 GUI 이벤트 전달, 오류 0 (`EditorUi.json`, `EditorUi.log`). 숨김 창 데스크톱 캡처는 증거에서 제외 |
| 실제 생성 화면 | GPU `Camera.Render`로 `Overview.png`, `UpperFloor.png`, `LowerFloor.png`, `Doorway.png`, `RoomGraph.png` 생성 후 직접 확인. graph 그림의 R1–R4/청록 선은 검증 overlay이며 저장 장면에는 없음 |
| 새 프로세스 저장 장면 복원 | 방 8개, 문 6개, 벽 구간 12개, 콘텐츠 90개, Collider 499개. 해시 `a57ffb3b9d6ba01e4191ed3cf5a72e75b4e34f6adde2742d9e1fead5b4e0f3f8` (`Reopen.json`) |
| 정적 검사 | Runtime의 UnityEditor/전역 Random 참조 없음, Core/Lab source·GUID byte parity, 이전 벽 snapshot 순서, 한국어 방 UI·경계 wiring 검사 통과 |

초기 PlayMode 테스트의 예약어 변수명 컴파일 오류는 수정했다. 최종 전체 회귀·원본 컴파일·render·reload 로그에는 해당 오류가 없으며 초기 로그는 추적용으로 남겼다.

## 한계

모든 층에 동일한 목표 방 수를 적용한다. 층별 개별 목표는 제공하지 않는다. 작은/좁은 지도와 높은 목표에서는 최소 크기·계단·스폰·기존 문 보호 때문에 방 수가 줄 수 있다. 연결 그래프는 열린 통로 tree이며 여닫는 문 오브젝트·복도 폭 전용 제어·NavMesh 자동 Bake는 포함하지 않는다. 보호 영역 때문에 콘텐츠 배치량도 목표보다 적을 수 있다.

마우스·키보드 수동 플레이, 데스크톱 창의 시각적 조작, Windows standalone Player 빌드와 새 UPM 소비 프로젝트 설치는 이번 작업에서 수행하지 않았다. UI 저장/재열기·Undo, 생성 화면과 물리 이동은 위 자동 검증으로 확인했다. 사용자 캐릭터/prefab의 실제 크기·Collider는 별도 확인 대상이다.

## 롤백과 정리

정확한 작업 직전 백업: `E:\CodexValidation\ArenaRooms_20261001\Rollback\Original`. 전체 SHA-256 목록: `Manifest.json`. 당시 미커밋 상태도 보존했다.

`Restore-RoomChanges.ps1` 기본 실행은 적용 파일과 백업 해시만 검사한다. `-Apply` 실행은 이번 작업 대상만 정확한 작업 전 bytes로 복원하고, 새 파일과 교체되는 현재 버전은 `ReplacedAfterRestore_<시간>`에 보존한다. 이후 사용자 편집이 있으면 해시 불일치로 중단한다. 실제 롤백은 실행하지 않았다.

임시 검증 프로젝트·Library 캐시는 `E:\CodexTemp\ArenaRooms_20261001_ValidationProject.zip`에 보존한다. 전체 ZIP CRC·파일 수·SHA-256 검증 후 이 작업의 임시 사본만 정리하며 `Evidence\ArchiveVerified.json`, `Cleanup.json`에 기록한다. 최종 예제·보고서·로그/XML/생성 화면·롤백 백업은 남긴다.
