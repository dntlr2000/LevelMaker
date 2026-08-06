# 테스트 계획

## R0·R1·R2·R3·R4·R5·R5.1·R5.2·R6·R7·R8·R9 승인 기준

R9 변경 뒤 Unity `6000.5.3f1` compile, 전체 EditMode `95/95`와 PlayMode `11/11`을 통과했습니다. R8 승인 기준이던 EditMode 89개와 PlayMode 11개도 같은 전체 실행에 포함됩니다.

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
- 누락 source, 잘못된 BakedPrefab 조합, 잘못된 generatorVersion과 손상 Blueprint의 코드 기반 차단
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
- 선택 Blueprint 덮어쓰기 후 Unity Undo가 중첩 cells·rooms·spawns 전체를 이전 상태로 복구하고 저장·강제 재임포트 뒤에도 유지
- 동일 결과, 다른 시드, stale 입력, 동일 provenance 결과 분기와 손상 저장본 비교 상태 분류
- 저장 Blueprint 미리보기가 recipe·시드를 재계산하지 않고 저장 hash를 구축한 뒤 절차 시드로 복귀
- SerializedObject로 생성한 SavedBlueprint StageDefinition과 Generator 연결이 AssetDatabase 재임포트 후 유지
- 검증 오류가 있는 현재 결과를 프로젝트 자산으로 만들기 전에 차단
- 현재 Blueprint와 일치하는 authoring recipe snapshot을 깊은 복사해 저장하고 재임포트 후 hash 유지
- Blueprint 덮어쓰기 Undo가 중첩 맵 데이터와 저장 레시피를 함께 이전 상태로 복구
- 저장 레시피 적용 시 구조·밀도·AnimationCurve가 복원되고 시드·드랍·런타임 옵션은 선택 정책대로 보존
- snapshot 없는 기존 R5 자산은 저장 맵 로드가 유지되고 설정 복원만 차단
- 변조된 snapshot의 recipe hash 불일치를 설정 자산 변경 전에 차단
- 저장 recipe·seed로 LegacyV1 Blueprint hash를 정확히 재생성
- 실제 Prefab 참조 custom catalog 자산의 recipe·seed·StableV2 입력으로 저장 Blueprint hash를 정확히 재생성
- snapshot 없는 기존 R5 자산을 강제 재임포트한 뒤 SavedBlueprint 로드는 유지하고 설정 복원만 차단
- settings-only와 Procedural StageDefinition의 Play HUD 설정이 `HideAndDontSave` 복제본에만 반영되고 원본 recipe·settings는 불변
- 별도 StageDefinition recipe의 구조 값과 Generator settings의 드랍·런타임 옵션이 같은 활성 복제본에 병합
- 입력 시드·실시간 재생성이 활성 StageDefinition, generatorVersion, catalog와 시드를 유지
- 새 recipe 후보가 Loader 성공 뒤에만 commit되고 실패한 source 전환은 기존 복제본·StageInstance·generated root를 유지
- Procedural에서 SavedBlueprint로 전환하거나 Generator를 파괴할 때 소유 런타임 복제본 정리
- 첫 StageInstance가 없을 때도 `loadOnPlay` StageDefinition이 settings-only 재생성·새 시드 fallback보다 우선
- SavedBlueprint 활성 상태의 구조·시드 편집 차단과 같은 저장 Blueprint 재구축
- Bake manifest의 source/final Blueprint, custom catalog, 완전한 재질 슬롯, 필수 의존성 hash, R6 Override 차단과 고유 owned artifact 검증
- `Procedural + BakedPrefab` 차단과 유효한 `SavedBlueprint + BakedPrefab` manifest·Prefab 허용
- Balanced seed `73125` RuntimeBuild 15회에서 동일 Blueprint hash와 시간·managed memory p50/p95 기준선 기록
- 기본 `DungeonBakeMaterialSet`의 8개 영속 Material 슬롯 생성과 재임포트 후 참조 유지
- SavedBlueprint에서 floor/wall Mesh, built-in/fallback·직접 Catalog Prefab과 manifest 영속 자산 생성
- source/final Blueprint, planning/realization/gameplay/material/override와 builder fingerprint 변경별 stale 코드
- 재Bake 성공 때만 StageDefinition 참조 commit, 의도적 실패 때 staging 정리와 이전 정상 Bake 보존
- 이전 manifest의 고유 GUID와 stage bake root를 모두 만족하는 파생 산출물만 정리하고 사용자·공유 자산 보존
- RuntimeBuild/BakedPrefab의 입구·출구, floor/wall Collider, spawn identity·transform, 클릭/drop marker와 구축 report parity
- BakedPrefab 로드 중 Blueprint 생성기, `DungeonMeshBuilder`와 콘텐츠 resolver 미실행
- Runtime assembly의 `UnityEditor` 참조 부재와 BakedPrefab 포함 Player build smoke
- Stage Override disable/add/content/절대 Transform 적용, 원본 deep-copy 불변과 목록 순서·제작 메모 독립 canonical hash
- Marker 편집, 중복 target·record ID, disable 상충 작업, 추가 stable ID 충돌과 잘못된 Transform의 코드 기반 차단
- 원본 변경 뒤 exact·ChangedExact와 semantic unique 제안, missing·ambiguous·candidate/add ID 충돌 재결합 분류
- R6 format/builder v1의 빈 Override 하위 호환과 v1 Override payload 차단
- R7 v2 manifest의 source/override/final hash, RuntimeBuild/BakedPrefab identity·Transform·report parity
- Override stale 검출과 실패 주입 재Bake 뒤 기존 Prefab·manifest·Override 입력 자산 보존
- R7 v2 BakedPrefab의 실제 Physics Raycast·클릭 파괴와 드랍 통계 정확히 1회 누적
- Runtime Core 계획이 Sample/Input System 경로 없이 Runtime assembly와 문서만 포함
- Lab Sample 계획이 Core와 분리되고 설치된 `com.unity.inputsystem` 버전을 sidecar 요구 사항으로 선언
- Runtime Examples 계획이 HUD 없는 Procedural·SavedBlueprint 장면·Definition·입력 자산만 포함
- Bake Authoring modular/standalone의 Runtime Core 포함 경계와 Editor-only assembly 참조
- Baked Stage dependency closure가 manifest 소유 Mesh·Prefab, Blueprint·Override·settings·material set을 포함하고 Sample/Editor/Test 경로를 제외
- Baked Stage modular/standalone Core 경계, stage/source/final/override hash, URP `17.5.0`과 `Universal` pipeline metadata
- 실제 Runtime Core `.unitypackage`와 JSON sidecar 생성, 파일 SHA-256·자산 목록 일치

R5.2 실행 결과는 compile 성공, EditMode `58/58`, PlayMode `7/7` 통과입니다. Balanced seed `73125`, warmup 3회 뒤 15회 RuntimeBuild의 이 환경 기준 시간은 p50 `7.390 ms`, p95 `7.744 ms`였습니다. Unity Mono가 `GC.GetAllocatedBytesForCurrentThread()`를 지원하지 않아 thread allocation은 p50/p95 모두 `0 B`와 `supported=False`로 기록했고, 대체 관측치인 `Profiler.GetMonoUsedSizeLong()` 증분은 p50 `2,252,800 B`, p95 `2,269,184 B`였습니다. 이 값은 절대 성능 제한이 아니라 같은 환경·설정에서 이후 R6 전후 회귀를 비교할 기준선입니다.

R6 실행 결과는 compile 성공, EditMode `74/74`, PlayMode `8/8` 통과입니다. RuntimeBuild/BakedPrefab parity, 실제 Physics Raycast·마우스 클릭에 의한 Baked target 파괴와 드랍 1회, stale fingerprint, 재Bake 성공·실패 rollback·Undo 안전성, 소유 범위 변조 방어와 Player 비포함 assembly 분류를 포함합니다.

R7 추가 회귀는 순수 Override 계약 EditMode `6/6`, Bake v1/v2 호환·동등성·stale/rollback EditMode `3/3`, Override v2 Baked 클릭/drop PlayMode `1/1`을 각각 통과했습니다. 이를 포함한 전체 R0~R7 회귀는 EditMode `83/83`, PlayMode `9/9` 통과이며 결과 파일은 `Logs/R7FullEditMode.xml`, `Logs/R7FullPlayMode.xml`입니다.

분리된 임시 프로젝트에서 `R6ManualVerificationSetup.CreateAllFromBatch`로 SavedBlueprint·영속 Mesh·Prefab·manifest·Baked 검증 Scene을 만든 뒤 `R6PlayerBuildSmoke.BuildFromBatch` Windows Development Player 빌드가 성공했습니다. 총 빌드 크기는 `172,288,233 B`이며 `Logs/R6PlayerBuildSmoke.log`에 기록했습니다. `VerifyRollbackFromBatch`도 통과했고 로그는 `Logs/R6RollbackVerification.log`입니다.

`R7ManualVerificationSetup.CreateAllFromBatch`는 전용 Override·RuntimeBuild/BakedPrefab Definition·v2 Bake·검증 Scene 생성을 완료했고, 장면 재개방 뒤 RuntimeBuild/BakedPrefab의 final hash와 stable spawn identity parity를 확인했습니다. Windows64 Development Player 빌드는 경고 `0`개, 총 크기 `172,176,046 B`로 성공했습니다. 로그는 `Logs/R7ManualSetup.log`, `Logs/R7PlayerBuildSmoke.log`입니다.

EditMode는 기존 생성·Blueprint·Loader·Catalog·자산 제작 회귀와 함께 manifest 계약, LegacyV1/custom catalog StableV2 정확 재생성, Undo 저장·재임포트, snapshot 없는 자산 호환을 확인합니다. PlayMode는 기존 카메라·임시 플레이어·클릭 드랍 외에 원본 비변경, 별도 StageDefinition recipe, source 전환 수명주기, 실패 rollback, SavedBlueprint 편집 차단과 첫 로드 전 Definition 우선을 확인합니다. 실제 Unity 프로세스 종료 후 재시작, Play 중 script/domain reload, HUD/Scene 육안 확인, 마우스 포인터 Raycast와 HUD 위 클릭 차단은 수동 검증 항목입니다.

## 전체 기능 검증 목록

아래는 자동 회귀 외에 수동·통합·제품화 단계까지 포함한 전체 체크리스트입니다. 체크리스트에 있다는 사실만으로 이번 R7 자동 실행에서 확인했다는 의미는 아닙니다.

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
- Play 중 script/domain reload 뒤 런타임 설정 복제본이 중복·유실되지 않고 재생성 또는 안전 복구
- R6 manifest가 source/final Blueprint, custom catalog, 완전한 영속 재질 세트와 고유 owned artifact를 모두 요구
- 스테이지 자산 탭에서 SavedBlueprint Definition, 영속 MaterialSet을 선택하고 Bake·재Bake·최신성 검사·결과 Ping이 동작
- Procedural Definition과 불완전한 MaterialSet은 Bake 버튼과 한국어 오류 안내로 차단
- R6 재Bake 실패 시 staging만 제거되고 이전 정상 Prefab·manifest와 사용자·공유 자산이 유지
- 성공한 재Bake는 사용자 자산이 없는 이전 빈 `Version_<guid>` 폴더까지 제거하고 현재 Bake와 비소유 자산은 유지
- RuntimeBuild/BakedPrefab의 입구·출구·floor/Collider, stable spawn ID, 클릭/drop과 구축 report 동등
- BakedPrefab을 연결한 빈 Player 씬에서 생성기·Mesh Builder·resolver 호출 없이 `__RogueDungeonLab_Generated` 하나를 로드
- Stage Override Preview에서 Scene Spawn 선택이 stable ID로 해석되고 generated hierarchy 직접 수정 없이 전체 재구축
- disable/add/content/절대 Transform을 하나씩 적용·원복하며 원본 Blueprint JSON과 hash가 유지
- Override Preview 중 원본 Blueprint 새 저장·덮어쓰기 버튼이 차단되고 원본 미리보기로 돌아오면 다시 활성화
- Marker 편집과 disabled Spawn의 content/Transform 변경이 자산을 손상시키기 전에 한국어 오류로 차단
- 원본 덮어쓰기 뒤 재결합 분석이 exact/unique/missing/ambiguous/collision을 구분하고 승인 전에는 자산을 바꾸지 않음
- 미해결 재결합, stale base hash와 잘못된 Override hash가 Preview와 Bake를 모두 차단
- R7 v2 최신 Bake 뒤 Override 변경이 source가 아닌 override/final stale 코드로 표시되고 재Bake로 해소
- R7 수동 검증 장면에서 RuntimeBuild와 Baked Generator를 각각 활성화해 같은 최종 Spawn과 클릭/drop 동작 확인
- RunState 제거·기믹 목록 순서와 저장 시각에 독립적인 canonical hash, JSON round-trip과 본문 변조 탐지
- 슬롯 ID 경로 제한, 메모리 저장소와 JSON 저장소 round-trip, 임시 파일 교체 실패 때 이전 정상 슬롯 보존
- 손상 JSON·state hash, 중복 제거 ID·participant key, 잘못된 pose와 Marker/Prop 제거 요청의 코드 기반 차단
- stage ID·source·run seed·final Blueprint hash 엄격 정책과 final hash matching-ID 재결합
- 명시적 migrator만 이전 format·target을 변환하고 변환 결과를 현재 target과 canonical hash로 재검증
- 비활성 후보 root의 Enemy·Destructible 제거와 Gimmick participant payload 복원
- RunState 검증·participant 실패 시 기존 generated root와 StageInstance 보존
- 같은 SavedBlueprint RunState의 RuntimeBuild/BakedPrefab 제거 결과 parity
- 클릭 파괴가 현재 Generator RunState에 stable ID를 1회 기록
- Procedural 슬롯이 저장한 run seed와 제거 대상·stage-local 플레이어 pose를 PlayMode에서 재개
- SavedBlueprint 슬롯이 영구 Stage ID를 확인하고 다른 ID를 기존 root 보존 상태로 거부
- 새 StageDefinition 자산의 영구 stage ID가 SerializedObject 생성·재임포트 뒤 유지

R8 전용 환경은 `R8ManualVerificationSetup.CreateAllFromBatch`로 생성하고 장면 재개방 뒤 두 Definition 참조와 stage ID를 확인합니다. 실제 HUD 가독성, Generator 토글과 Play 중 script/domain reload는 [R8 수동 검증 절차](R8_MANUAL_VERIFICATION_KO.md)를 따릅니다.

Windows64 Development Player는 `R8PlayerBuildSmoke.BuildFromBatch`가 R8 전용 장면 하나를 대상으로 경고 `0`, 오류 `0`, 총 `171,479,278 B`로 성공했습니다. 로그와 NUnit 결과는 `Logs/R8PlayerBuildSmoke.log`, `Logs/R8FullEditMode.xml`, `Logs/R8FullPlayMode.xml`에 기록했습니다.

## R9 패키지·소비 프로젝트 검증

`R9PackageVerificationSetup.ExportAllFromBatch`는 `Distribution/RogueDungeonLab/R9`에 일곱 package와 각각의 JSON sidecar, `PACKAGE_INDEX_KO.md`를 생성했습니다. 배포 계획 전용 EditMode는 `Logs/R9DistributionEditModeFinal.xml`에서 `6/6` 통과했습니다.

`tools/verify-r9-packages.ps1`의 첫 깨끗한 프로젝트는 Unity 기본 Physics·UI·JSON 모듈만 가진 manifest에서 Runtime Core와 Runtime Examples를 가져왔습니다. `RogueDungeonLab.Samples`가 로드되지 않은 상태로 예제 및 새 Procedural·SavedBlueprint Definition을 실제 로드했고, HUD 없는 Windows64 Development Player를 오류 `0`, 경고 `0`으로 빌드했습니다. 결과는 `Logs/R9ConsumerVerification/RuntimeImport_20260807_001715.log`, `RuntimeExamplesImport_20260807_001715.log`, `RuntimeBuild_20260807_001715.log`입니다.

두 번째 깨끗한 프로젝트는 Stage sidecar가 선언한 URP `17.5.0`을 manifest에 넣고 standalone Bake Authoring과 modular Baked Stage를 가져왔습니다. Runtime manifest와 Editor 전체 fingerprint, final Blueprint hash, stable spawn identity, Baked root에 transient `DungeonGeneratedMeshOwner`가 없는 계약을 검사했습니다. 최소 URP Renderer/Pipeline asset을 Graphics·Quality에 연결한 Windows64 Development Player가 오류 `0`, 경고 `0`으로 성공했으며 근거는 `BakedAuthoringImport_20260807_001715.log`, `BakedStageImport_20260807_001715.log`, `BakedBuild_20260807_001715.log`와 `VERIFICATION_SUMMARY_20260807_001715.json`입니다.

최종 빌드 폴더 크기는 Runtime `152,943,647 B`(238 files), Baked `162,951,039 B`(279 files)입니다. 이 수치는 기능 제한이 아니라 동일 Unity·플랫폼에서 산출물이 실제 생성됐다는 인계 기록입니다.

최종 원본 프로젝트 회귀는 `Logs/R9FullEditModeFinal.xml`의 EditMode `95/95`, `Logs/R9FullPlayModeFinal.xml`의 PlayMode `11/11`입니다. 실제 다른 제품 프로젝트의 커스텀 render feature·Addressables/DI adapter, Lab Sample HUD의 다양한 해상도 육안과 Play 중 script/domain reload는 제품별 수동 확인 범위입니다.
