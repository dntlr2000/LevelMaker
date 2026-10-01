# FPS Arena 내부 벽 — 로컬 적용·검증 보고서

작업일: 2026-10-01. 원본: `E:\Unity\LevelMaker`. Unity: `6000.5.3f1`.

## 적용 결과와 사용법

원본 프로젝트에 V2 내부 벽 기능을 직접 적용했다. ZIP 설치는 필요 없다.

`Tools > Rogue Dungeon Lab > FPS 아레나 제작 > 지형·계단 > 내부 벽 생성`을 켜고 벽 점유 목표·길이 범위·높이·두께·출입구 폭·층별 상한을 조절한 뒤 `시드로 생성 / 재생성`을 누른다. 블록 엄폐와 네 콘텐츠 범주는 기존 탭에서 함께 설정한다. 설정 자산 저장과 Unity 장면 저장을 지원한다.

`Tools > Rogue Dungeon Lab > 내부 벽 FPS 예제 장면 만들기`로 새 예제를 만들거나 `Assets/FpsArenaWallsExample/InternalWalls.unity`를 연다. 제공 예제는 시드 73125, 팔각형 32×32, 2층, 벽 점유 목표 20%, 길이 6–12셀, 높이 3m, 두께 0.35m, 출입구 폭 2셀이다. 벽 40개, 콘텐츠 86개, Collider 567개가 생성된다. 예제의 Play 시 자동 생성은 꺼져 있어 저장 결과를 그대로 사용한다.

벽 기본값은 OFF이다. OFF에서는 기존 V1/V2의 배치·콘텐츠 ID·해시가 유지된다. LegacyV1은 벽 설정을 무시하고 명시적인 V2 업그레이드 후 벽을 사용할 수 있다.

## 실제 적용 파일

| 구분 | 파일 |
|---|---|
| 벽 설정·정규화 | `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaSettings.cs` |
| 벽 DTO·전용 시드 stream·V2 호환 hash snapshot | `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaWalls.cs`와 `.meta` |
| 배치·콘텐츠 겹침 방지 | `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaFlexiblePlanner.cs` |
| 벽 목록·차단 셀·연결성·hash·검증 | `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaLayout.cs` |
| 실제 벽 구간·BoxCollider | `Assets/RogueDungeonLab/Runtime/Arena/FpsArenaSceneBuilder.cs` |
| 한국어 설정·결과·예제 메뉴 | `Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs` |
| 회귀 | `Tests/EditMode/FpsArenaWallTests.cs`, `FpsArenaWallsOffBaseline.json`, `Tests/PlayMode/FpsArenaWallTraversalTests.cs`, `FpsArenaTraversalTests.cs` (앞에 `Assets/RogueDungeonLab/`) 및 새 파일 `.meta` |
| UPM | Core `Runtime/Arena`의 대응 5개 소스와 새 `.meta`, Lab `Editor/FpsArenaWindow.cs` |
| 바로 실행할 예제 | `Assets/FpsArenaWallsExample/WallSettings.asset`, `InternalWalls.unity` 및 `.meta` |
| 재검증 도구 | `tools/ArenaWallsUnityValidation.cs`, `tools/verify-arena-walls-static.py` |
| 문서 | `README.md`, `CHANGELOG.md`, `PLANS.md`, `docs/ARCHITECTURE_KO.md`, `FPS_ARENA_GUIDE_KO.md`, `TEST_PLAN_KO.md`, 이 보고서 |

전체 적용 파일의 변경 전/후 SHA-256과 새 파일 여부는 `E:\CodexValidation\ArenaWalls_20261001\Rollback\AppliedFiles.json`에 기록한다. 기존 미커밋 V2·배포 코드·드랍 테이블·장면·ProjectSettings 및 사용자 수정은 보존했다. git commit/push/reset을 실행하지 않았고 사용자 Unity 프로세스를 종료하지 않았다. `E:\Unity\RoguelitePvP` 파일·프로세스에는 접근하거나 변경하지 않았다.

## 안전 동선과 결정성

- 전용 wall stream으로 계단·착지 접근로 예약 후 벽을 배치한다. 콘텐츠 밀도를 바꿔도 벽과 계단 배치는 유지된다.
- 모든 solid 벽 셀은 계단 개구부·착지 접근로·스폰·기존 예약 통로를 피한다. 벽 사이와 콘텐츠 사이에 공통 셀 간격을 적용한다.
- 벽 전체 외곽의 1셀 우회 띠를 확보한다. 충분히 긴 벽은 열린 출입구와 양쪽 1셀 접근 공간을 추가로 예약한다. 가장 좁은 출입구는 2m이다.
- 벽 후보와 후속 엄폐를 수락하기 전에 차단 셀의 전체 BFS 연결성을 검사한다. 적·아이템·기믹·엄폐 전체 footprint도 벽과 겹치지 않는다.
- 높이는 층 간 높이 − 0.25m 이하, 두께는 0.15–1m로 제한한다. 실제 solid 구간마다 BoxCollider를 생성하며 출입구에는 Collider를 만들지 않는다.
- OFF에서는 기존 필드 순서의 V2 snapshot을 사용하고 추가 벽 hash 바이트를 쓰지 않는다. 마지막 built recipe와 catalog snapshot으로 저장 벽과 구조를 복원한다.

## 실제 Unity 검증 증거

증거 폴더: `E:\CodexValidation\ArenaWalls_20261001\Evidence`.

| 검증 | 결과와 근거 |
|---|---|
| 원본 직접 컴파일 | 컴파일 오류 0, Unity 정상 종료 코드 0 (`OriginalCompile.log`) |
| 전체 EditMode | **139/139 통과**, 실패/건너뜀 0 (`EditModeVerified.xml`). 초기 전체 138/138도 통과 (`EditMode.xml`) |
| 전체 PlayMode | **15/15 통과**, 실패/건너뜀 0 (`PlayMode.xml`) |
| 벽 OFF/레거시 | 변경 전 원본을 Unity로 실행해 캡처한 36개 V1/V2 hash와 정확히 일치 (`BeforeWalls.json`, EditMode fixture) |
| 다층·다시드 연결성 | 3형상 × 4크기 × 3층 수 × 4시드 = **144조합**. 벽 35%와 네 범주 최대 밀도에서 벽·모든 콘텐츠 footprint의 무겹침 및 전체 연결성 통과 |
| 실제 이동·콜라이더 | CharacterController의 출입구/벽 끝 우회/solid 차단, 모든 열린 이웃 셀 Physics CapsuleCast, 벽 ON 4층 계단 양 lane 상승·하강 통과 |
| 저장·UI | SerializedObject 편집·Undo·자산 저장/재임포트·장면 재열기, 반복 생성 root 1개, built snapshot 복원 통과 |
| 실제 script/domain reload | Play 진입 → Play 중 RequestScriptReload → hash/root/벽 복원 → Edit 복귀, 오류 0 (`PlayDomainReload.json`, `DomainReload.log`) |
| 새 Unity 프로세스 재열기 | 해시 `9a2a4cffd0dd1d771ac27d2960b9357c19399286a7572ea16d52430082965f8d`, 벽 40·콘텐츠 86·Collider 567 복원 (`Reopen.json`) |
| 실제 생성 화면 | GPU Camera.Render의 `Overview.png`, `UpperFloor.png`, `LowerFloor.png`, `Doorway.png`를 직접 검토 |
| 실제 제작 창 | 6개 탭의 실제 OnGUI·화면 PNG, GUI 예외 0 (`EditorTab0.png`–`EditorTab5.png`, `EditorUi.log`) |
| 정적 경계 | Runtime의 UnityEditor/전역 Random 미참조, 원본/Core/Lab 대응 소스와 GUID byte parity, V2 OFF 필드 순서 검사 통과 |

추가된 실제 네 범주 프리팹 검사에서 처음에는 높은 밀도의 앞 범주가 안전 슬롯을 모두 소비해 뒤 범주가 0개가 되었다. 모든 범주를 실제 생성해서 Collider Bounds를 비교하도록 테스트의 층별 범주 상한을 4개로 조정했다. 제품의 기존 V2 배치/용량 정책은 바꾸지 않았다. 초기 실패 XML도 증거 폴더에 보존한다.

## 한계

벽은 직선 파티션·열린 출입구이며 닫히는 문, 외곽에 붙는 폐쇄 방, 교차 벽, 벽 프리팹 및 NavMesh 자동 Bake는 제공하지 않는다. 벽 점유 목표는 마지막 벽 한 개 단위로 종료하므로 실제 점유가 목표보다 약간 높을 수 있다. 안전 공간이나 높은 앞 범주 밀도로 뒤 범주의 실제 개수는 목표보다 낮을 수 있다.

제품 프리팹은 정확한 authored Renderer/Collider Bounds를 지정해야 한다. 런타임 AI/픽업/기믹 동작과 제품 캐릭터 크기는 제품별 검증 대상이다. 실제 마우스로 슬라이더를 조작하거나 키 입력/커서 잠금 감각을 수동 시험하지 않았고, Windows standalone Player 빌드와 새로운 UPM 소비 프로젝트 설치는 이번 작업에서 실행하지 않았다. UI 렌더/저장 경로, 실제 Physics/CharacterController 및 도메인 복원은 위 증거로 검증했다.

## 롤백과 임시 검증 사본 보관

정확한 변경 전 원본: `E:\CodexValidation\ArenaWalls_20261001\Rollback\Original`.
전체 SHA-256 목록: `Rollback\Manifest.json`. 기존 git 상태와 diff도 함께 보존한다.

`Rollback\Restore-WallChanges.ps1`은 기본 실행에서 적용 파일과 백업 SHA-256만 검사한다. `-Apply`를 명시하면 이번 작업 파일만 변경 전 bytes로 복원하며 새 파일과 교체된 현재 버전은 `ReplacedAfterRestore_<시각>`에 보존한다. 이후 사용자 편집이 있으면 복원을 중단한다. 이 작업에서는 실제 롤백을 실행하지 않았다.

임시 검증 프로젝트·Library 캐시는 체크섬 검증한 `E:\CodexTemp\ArenaWalls_20261001_ValidationProject.zip`으로 보관하고 실행 사본을 정리한다. 롤백 원본·최종 결과·Unity 로그/XML/PNG는 별도로 남긴다.
