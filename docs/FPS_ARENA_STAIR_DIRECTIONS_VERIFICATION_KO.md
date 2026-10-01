# FPS Arena 계단 상승 방향 수정·검증

2026-10-01, Unity `6000.5.3f1`. `E:\Unity\LevelMaker`에 직접 적용했다. 수용된 방 BSP 방식과 원본 방 예제/설정 자산은 유지했다.

## 변경과 사용

기존 계단은 위치만 시드로 고르고 발판·난간·개구부·랜딩이 모두 +Z 상승/X 폭을 전제했다. 계단마다 +Z/+X/−Z/−X 진행축과 그에 직교하는 폭축을 기록하고, 회전된 기하가 양층 바닥·외곽·스폰·다른 계단/착지 조건을 만족하는 후보에서 시드로 선택하도록 수정했다. 이전 앞/뒤 band 제약은 방향 모드에서 사용하지 않는다. 방향을 선택한 후 발판과 난간만 돌리는 방식이 아니므로 개구부·착지·콘텐츠/방문 보호와 층간 그래프도 같은 회전을 따른다.

`Tools > Rogue Dungeon Lab > FPS 아레나 제작 > 지형·계단`에서 **시드별 계단 방향 사용**을 확인하고 재생성한다. `FpsArenaRecipe.CreateFlexible()`, V2 preset과 새 편집 입력의 기본값은 ON이다. 결과 패널에서 실제 상승 방향/시작/도착, 층간 유효 방향과 후보 수를 확인한다. 계단 1–2개마다 네 방향을 강제로 배분하지 않으므로 한 시드에서 둘 다 같은 방향이어도 유효하다.

기존 설정의 방 수·크기·밀도·시드 등의 값과 저장 장면 bytes는 변경하지 않았다. 마지막 생성 결과는 기존 built recipe로 정확히 복원하고, 사용자가 재생성할 때 새 입력 옵션을 적용한다. Unity는 기존 편집 입력 자산의 누락된 새 필드를 현재 `CreateFlexible()` 초기값 ON으로 채울 수 있다. 정확한 이전 배치/hash 재생성이 필요하면 옵션 OFF를 지정한다. 누락 필드가 OFF인 legacy JSON 및 built snapshot의 원래 hash는 보존한다. LegacyV1은 그대로 +Z이며 명시적인 V2 업그레이드 후 방향 기능을 사용한다.

기존 `FpsArenaStair.x/z`는 모든 회전에서 footprint의 최소 좌표로 유지한다. `Bottom`/`Top`과 기존 공개 생성/Build 시그니처는 유지했다. 새 회전 계단을 사용하는 코드에서는 `Forward`, `LaneStep`, `Cell`, `BottomLane(lane)`, `TopLane(lane)`을 사용한다. 양쪽 lane 모두 층간 그래프 링크이며 회전된 `ProtectedBounds`로 방/벽/콘텐츠 접근을 보호한다. 생성 root 이름과 외부 패키지는 변경하지 않았다.

## 실제 적용 파일

| 파일 | 변경 |
|---|---|
| `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaStairDirections.cs` 및 `.meta` | 새 방향 enum, 진행/폭/footprint/랜딩 좌표와 보호 bounds, 방향 report, 이전 room hash snapshot |
| 같은 폴더 `FpsArenaSettings.cs` | 방향 옵션과 새 V2 기본값 |
| `FpsArenaFlexiblePlanner.cs` | 유효한 회전 후보와 시드 선택, 회전된 개구부/예약/랜딩 경로 |
| `FpsArenaLayout.cs`, `FpsArenaRooms.cs` | 양 lane 층간 연결, 회전 기하 검증, 호환 hash/방향 bytes |
| `FpsArenaRoomPlanner.cs` | 기존 BSP에서 계단 예약 bounds와 방별 랜딩 경로만 회전 대응 |
| `FpsArenaSceneBuilder.cs` | 실제 발판·난간 그룹의 위치와 yaw 적용 |
| `Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs` | 한국어 옵션과 실제/유효 방향 결과 |
| `Tests/EditMode/FpsArenaStairDirectionTests.cs`, `FpsArenaStairDirectionsLegacyBaseline.json` 및 `.meta` | 5개 새 테스트와 변경 직전 room Unity hash 18개 |
| `Tests/PlayMode/FpsArenaStairDirectionTraversalTests.cs` 및 `.meta` | 실제 CharacterController 144계단 양 lane 이동 |
| 기존 `FpsArenaFlexibleTests.cs`, `FpsArenaWallTests.cs`, `FpsArenaRoomTraversalTests.cs`, `FpsArenaTraversalTests.cs` | 고정축 assertions/이동을 방향별 lane/진행축으로 일반화 |
| `UpmPackages/...core/Runtime/Arena`, `...lab/Editor/FpsArenaWindow.cs` | 대응 canonical source·GUID 정확한 복사 |
| `tools/ArenaStairDirectionsUnityValidation.cs`, `tools/verify-arena-stair-directions-static.py` | 실제 Unity 재현 helper와 정적 검사 |
| README, CHANGELOG, PLANS, architecture/guide/test plan, 이 보고서 | 동작·호환·사용법·검증 |

전체 파일별 적용 전/후 SHA-256은 `E:\CodexValidation\ArenaStairDirections_20261001\Rollback\AppliedFiles.json`에 기록한다. 기존 사용자 편집을 백업/보존했으며 git commit/push/reset과 사용자 Unity 강제 종료를 하지 않았다. `E:\Unity\RoguelitePvP` 파일·프로세스에는 변경을 하지 않았다.

## 실제 검증

증거 위치: `E:\CodexValidation\ArenaStairDirections_20261001\Evidence`.

| 검사 | 결과·증거 |
|---|---|
| 실제 원본 컴파일 | 종료 코드 0, 컴파일 오류 0 (`OriginalCompile.log`) |
| 최종 전체 EditMode | **151/151 통과**, 실패/skip 0 (`FullEditMode.xml`, 239.71초) |
| 최종 전체 PlayMode | **18/18 통과**, 실패/skip 0 (`FullPlayMode.xml`, 11.27초) |
| 이전 결과 호환 | 변경 직전 room 18개 + 기존 room OFF 24개 + wall OFF 36개 = **78개 Unity hash 일치**. 원본 2층 8방 장면 hash `a57ffb3b9d6ba01e4191ed3cf5a72e75b4e34f6adde2742d9e1fead5b4e0f3f8` 복원 |
| 재현성·다양성 | 24시드 각각 동일 입력 반복 hash 일치, 입력 불변, 각 층 네 방향 관측, 콘텐츠 밀도 변경 뒤 계단 위치/방향 불변 |
| 구조·방문·겹침 | **288프로필**: 3형상 × 4크기 × 2/4층 × 1/2계단 × 열린/이전 벽/방구획 × 2시드. 회전 개구부, 모든 랜딩, 콘텐츠 전체를 장애물로 본 연결성. 기존 room 216프로필 회귀도 최종 suite에서 통과 |
| 실제 기하·충돌 | 실제 발판/난간 yaw와 진행/폭축 일치, 3개 개구부 가드, 양 lane 랜딩 capsule 여유, flight 내부의 외부 바닥/벽/다른 계단/콘텐츠 Collider 침입 없음 |
| CharacterController | 3형상 × 4시드 × 2극단 셀/층 높이 profile의 **144계단**, 양 lane 총 288통로에서 상승/하강 및 수평·수직 도착 위치 확인. 기존 방 출입구·물리 인접 통로 회귀도 통과 |
| 저장·Undo·복원 | SerializedObject 방향 토글/Undo, 설정 저장/import, 반복 root 1개, 입력과 built snapshot 분리, 장면 재열기/enable 복원 |
| 실제 domain reload | 새 프로세스의 방향 장면 재열기 → Play 진입 → Play 중 script reload → Edit 복귀, 동일 hash/root 복원과 오류 0 (`PlayDomainReload.json`) |
| Editor UI | 실제 EditorWindow 6탭 GUI event 검사 오류 0 (`EditorUi.json`). 데스크톱 캡처/사용자 전경 앱 제어 없음 |
| 생성 화면 | **4시드(0/1/71/73125), 3층·층별 4방**, 실제 GPU Overview/LowerDirections/Flight 12장. 4시드 하부 방향과 overview/flight를 직접 확인 (`RenderedSeeds.json`, PNG). 청록 화살표는 검증 overlay이며 저장 geometry에는 없음 |
| 64시드 방향 조사 | 4층·2계단의 **384계단**, 모든 층간 네 방향 관측 (`DirectionSurvey.json`). 이 조사에서 유효 방향 mask 제한은 없었음 |
| 정적 검사 | Runtime UnityEditor/전역 Random 없음, Core/Lab source·GUID parity, 정확한 이전 room snapshot 순서와 한국어 방향 UI 검사 통과 |

동일 32×32 설정에서 64시드의 실제 계단 방향 빈도:

| 층간 | +Z | +X | −Z | −X |
|---|---:|---:|---:|---:|
| 1→2층 | 35 | 28 | 31 | 34 |
| 2→3층 | 32 | 40 | 25 | 31 |
| 3→4층 | 22 | 32 | 41 | 33 |

화면의 1→2층 계단은 시드 0에서 +X/+X, 시드 1에서 +Z/−X, 시드 71에서 +X/+Z, 시드 73125에서 −X/+Z다. 각 화면은 원본처럼 블록 엄폐물과 방 출입구를 함께 생성한다.

초기 집중 검증은 기하/78개 hash/288프로필/다양성 검사를 통과했으나 기존 설정 자산의 새 필드가 OFF일 것이라는 테스트 가정 한 개가 실패했다. Unity 편집 입력 초기값 ON과 저장 built snapshot OFF를 구분해 Undo/저장 검사를 보완했다. 방 geometry 복원 hash는 처음부터 일치했으며 최종 suite는 모두 통과했다. 초기 XML/log도 추적용으로 남겼다.

## 방향 제한과 한계

방향은 형상 안의 연속 양층 바닥/랜딩 고리, 스폰·기존 계단 예약 및 다른 flight 분리를 통과해야 한다. 유효 후보 mask와 수를 표시하며 mask가 제한되면 외곽/양층 바닥/보호 영역 때문임을 report로 안내한다. 계단의 run은 층 높이/셀 크기로 정하므로 길고 좁은 공간에서는 일부 축이 불가능할 수 있다. 유효한 방향이 있어도 모든 시드/각 층에 네 방향을 균등 분배하거나 서로 다른 방향 두 개를 강제하지 않는다. 지원 범위의 288프로필과 64시드 조사는 정상 생성했다.

새 계단 위치/방향은 방 보호 조건에도 영향을 주므로 재생성 시 방의 실제 구획이 바뀔 수 있다. BSP 알고리즘은 유지했으며, 목표 방 수 미달 시 기존 실제/요청/원인 report를 따른다. 이전 저장 결과는 재생성 전까지 유지한다.

수동 마우스/키 입력 플레이, Windows standalone Player 빌드, 새 UPM 소비 프로젝트 설치, 제품별 캐릭터/prefab/NavMesh 검증은 수행하지 않았다. 테스트 캐릭터는 높이 1.8m·반경 0.35m·stepOffset 0.3m·slopeLimit 50°이며 생성 계단의 riser는 0.18m 이하이다. 사용자 캐릭터 크기와 authored Collider가 다르면 별도 확인해야 한다.

## 백업과 복구 가능한 정리

작업 직전 **797파일**의 정확한 백업은 `E:\CodexValidation\ArenaStairDirections_20261001\Rollback\Original`, 전체 체크섬은 `Manifest.json`이다. 당시 미커밋 상태와 diff를 보존했다.

`Restore-StairDirectionChanges.ps1` 기본 실행은 현재 적용 파일과 백업 SHA-256만 확인한다. `-Apply`는 이번 대상만 작업 전 bytes로 복원하고 새 파일/교체 버전은 recovery 폴더에 보존한다. 이후 사용자 편집이 있으면 해시 불일치로 중단한다. 실제 롤백은 실행하지 않았다.

임시 Unity 검증 프로젝트와 Library 캐시는 `E:\CodexTemp\ArenaStairDirections_20261001_ValidationProject.zip`에 보관한다. 전체 ZIP CRC·파일 수·SHA-256 확인 후 이 작업의 임시 사본만 정리하고 `ArchiveVerified.json`, `Cleanup.json`에 기록한다. 결과·화면·XML/log·보고서·정확한 롤백 백업은 남긴다.
