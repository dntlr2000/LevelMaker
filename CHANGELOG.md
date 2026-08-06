# Changelog

## 0.11.0

- Runtime Core에서 Input System, Lab HUD·카메라·클릭 입력·임시 플레이어를 제거하고 GUID를 보존한 `RogueDungeonLab.Samples` 선택 assembly로 분리
- 제품 캐릭터가 Sample 타입 없이 RunState pose를 제공하는 `IDungeonRunStatePlayer` 등록 계약 추가
- HUD 없는 Procedural·SavedBlueprint RuntimeBuild 예제 자산과 두 smoke scene 추가
- `RogueDungeonLab.Editor.Baking`과 `RogueDungeonLab.Editor.Packaging` Editor-only assembly 분리
- Runtime Core, Runtime Examples, Lab Sample, Bake Authoring modular/standalone, Baked Stage modular/standalone의 일곱 `.unitypackage` 출력 추가
- Baked Stage의 Blueprint·Override·Catalog·settings·material·Prefab·manifest 소유 산출물 dependency closure 수집과 Sample/Editor/Test 의존 차단 추가
- package별 SHA-256, Unity 버전, asset 목록, stage/source/final/override hash, render pipeline과 요구 package를 기록하는 JSON sidecar·index 추가
- Built-in/URP/HDRP/custom Shader 탐지, 외부 package 버전 기록과 혼합 pipeline 배포 차단 추가
- Input System 없는 Runtime 소비 프로젝트와 URP Baked 소비 프로젝트를 새로 만드는 자동 import·Windows Player smoke harness 추가
- Unity `6000.5.3f1` R9 배포 EditMode `6/6`, 전체 EditMode `95/95`, PlayMode `11/11` 통과
- 두 깨끗한 소비 프로젝트의 Windows64 Development Player 빌드 오류·경고 `0`개로 성공

## 0.10.0

- Unity Object 참조가 없는 versioned `DungeonRunState`에 stage ID, source, run seed, final Blueprint hash, 제거 spawn, 기믹 payload와 stage-local 플레이어 pose 추가
- 목록 순서·저장 시각에 독립적인 canonical state hash, JSON round-trip과 코드 기반 구조·target 검증 추가
- 기본 strict 거부, 명시적 matching-ID 재결합과 결과 재검증형 `IDungeonRunStateMigrator` hook 추가
- `IDungeonRunStateParticipant`로 Gimmick spawn별 확장 상태 캡처·복원 지원
- 메모리 저장소와 `persistentDataPath` 기반 원자적 JSON 슬롯 저장소, 경로 제한·손상 파일 차단·이전 정상 슬롯 보존 추가
- RuntimeBuild와 BakedPrefab의 비활성 staging root에 같은 상태를 적용하고 실패 시 기존 generated root를 보존
- Generator의 파괴 기록, participant·플레이어 캡처, 슬롯 save/load/delete facade와 임시 플레이어 pose 재개 연결
- Play HUD에 `런 상태` 탭과 슬롯·strict/matching-ID 정책·최근 적용 결과 표시 추가
- `DungeonStageDefinition.stageId`와 새 제작 자산의 영구 ID 자동 생성 추가
- Procedural·SavedBlueprint 전환 장면과 한국어 수동 검증 절차 추가
- canonical/JSON/원자 교체/migration/participant/rollback/RuntimeBuild-Baked parity EditMode와 클릭 파괴·run seed·Saved stage ID·플레이어 pose PlayMode 회귀 추가
- Unity `6000.5.3f1` 전체 EditMode `89/89`, PlayMode `11/11` 통과
- R8 전용 장면 Windows64 Development Player 빌드 경고 `0`, 총 `171,479,278 B`로 성공

## 0.9.0

- 반복 재Bake 뒤 사용자 자산이 없는 이전 `Version_<guid>` 폴더를 안전하게 제거해 빈 폴더와 `.meta` 누적 방지
- Runtime-safe `DungeonStageOverrides`에 SavedBlueprint 기준 비활성화·추가·콘텐츠 교체·절대 Transform 작업과 Marker 보호 계약 추가
- 목록 순서·제작 메모에 독립적인 canonical Override hash, 원본 깊은 복사 적용과 source/override/final Blueprint hash 분리
- stable ID exact 우선, 의미 anchor 유일 후보 제안, missing·ambiguous·candidate/add ID 충돌 리포트와 명시적 Undo 재결합 승인 추가
- `DungeonStageDefinition`, RuntimeBuild Loader와 저장본 미리보기에 선택적 Stage Override 적용
- Bake format/builder v1 읽기 호환을 유지하고 v2 manifest에 Override 자산·hash와 최종 Blueprint hash 기록
- Baker가 합성된 최종 Blueprint로 Mesh·Prefab을 만들고 Override/final stale, 실패 재Bake rollback과 사용자 Override 자산 보존 검증
- 스테이지 자산 탭에 Override 생성·Definition 연결, 원본/적용 미리보기, Scene stable ID 선택 편집, 수동 Spawn과 변경 목록 UI 추가
- Override 미리보기 중 원본 Blueprint 저장 차단과 generated hierarchy 직접 편집 금지 안내 추가
- R7 전용 RuntimeBuild/BakedPrefab Definition·v2 Bake·동등성 검증 Scene 생성 메뉴와 한국어 수동 가이드 추가
- Unity `6000.5.3f1`에서 Override 계약 EditMode `6/6`, v1/v2 Bake·stale·rollback EditMode `3/3`, v2 Baked 클릭/drop PlayMode `1/1`과 전체 EditMode `83/83`, PlayMode `9/9` 통과
- R7 검증 Scene 재개방 뒤 RuntimeBuild/BakedPrefab final hash·stable identity parity 확인
- Windows64 Development Player 빌드 경고 `0`개로 성공, 총 크기 `172,176,046 B`; 실제 HUD/Scene 육안과 Play 중 script/domain reload는 수동 확인 범위

## 0.8.0

- Editor-only `DungeonStageBaker`로 SavedBlueprint 기반 floor/wall Mesh, 콘텐츠 Prefab과 `DungeonBakeManifest` 영속 자산 생성
- 알려진 built-in/fallback과 Content Catalog 직접 Prefab을 지원하고 runtime factory·Addressables·DI resolver Bake 차단
- stage 전용 staging/commit, 실패 rollback과 manifest GUID·bake root 기반 이전 파생 산출물 정리
- source/final Blueprint, planning/realization/gameplay/material/override hash와 builder version의 Editor stale 재검증
- `SavedBlueprint + BakedPrefab` Loader가 Blueprint 생성, Mesh Builder와 resolver를 실행하지 않고 Prefab을 로드하도록 연결
- `스테이지 자산` 탭에 영속 MaterialSet 생성·선택, Bake/재Bake 확인, 최신성 리포트와 Prefab·manifest Ping UI 추가
- Catalog Prefab missing script·Editor-only component Player 안전성 검사, 직접 Prefab 클릭 대상 gameplay fingerprint와 Runtime 기본 DropTable canonical fingerprint 보강
- 재Bake의 비가역 자산 정리 뒤 삭제된 참조를 복원하지 않도록 안전하지 않은 StageDefinition Undo 기록 제거
- R6 전용 SavedBlueprint·DropTable·MaterialSet·BakedPrefab·검증 Scene 생성 및 실패 주입 rollback 메뉴·batchmode 진입점 추가
- Unity `6000.5.3f1` compile, EditMode `74/74`, PlayMode `8/8` 통과
- 분리된 임시 프로젝트에서 Baked 검증 장면 Windows Development Player 빌드 성공, 총 크기 `172,288,233 B`

## 0.7.2

- Play HUD가 프로젝트 settings와 Procedural StageDefinition recipe 대신 Generator 소유 `HideAndDontSave` 복제본을 편집하도록 변경
- 별도 StageDefinition recipe의 구조 설정과 Generator settings의 드랍·런타임 옵션을 합쳐 같은 활성 시드·생성기 버전으로 재생성
- 후보 런타임 설정을 Loader 성공 뒤에만 commit하고 실패한 source 전환은 기존 맵·활성 복제본을 유지
- SavedBlueprint 활성 상태의 구조·시드 편집을 차단하고 같은 저장 논리 맵 재구축만 허용
- Runtime-safe `DungeonBakeManifest`, `DungeonBakeMaterialSet`과 StageDefinition의 R6 Bake 참조 계약 추가
- custom catalog, 완전한 재질 슬롯, source/final·planning·realization·gameplay·material hash, R6 Override 차단과 고유 `ownedArtifacts` 검증 추가
- 덮어쓰기 Undo 후 저장·강제 재임포트, LegacyV1/custom catalog StableV2 정확 재생성과 snapshot 없는 기존 R5 자산 회귀 보강
- RuntimeBuild 시간, thread allocation counter 지원 상태와 Profiler Mono 사용량 증분 p50/p95 기준선 테스트 추가
- Unity `6000.5.3f1` batchmode compile, EditMode `58/58`, PlayMode `7/7` 통과; thread allocation counter 미지원 상태와 Profiler Mono 사용량 증분 기준선 기록

## 0.7.1

- `DungeonBlueprintAsset`에 Blueprint 논리 hash와 분리된 선택적 authoring recipe snapshot을 추가하고 기존 snapshot 없는 R5 자산 호환 유지
- `DungeonRecipeSnapshot`의 깊은 복사, AnimationCurve·밀도 프로필 복원과 생성 필드 역적용 API 추가
- 새 Blueprint 저장·덮어쓰기 시 현재 결과의 `recipeHash`와 일치하는 설정을 함께 보존하고 snapshot 버전·hash·정규화 검증 추가
- `스테이지 자산` 탭에 현재 시드를 유지하는 설정 복원과 저장 시드·generatorVersion·catalog로 정확히 재생성하는 두 동작 추가
- 설정 적용에 확인, `SerializedObject`, 전체 Undo와 dirty/save 처리를 사용하고 드랍 테이블·런타임 옵션 보존
- `절차 원본으로 복귀`를 `현재 절차 설정으로 재생성`으로 변경해 시드만 복원하는 기존 동작을 명확화
- StageDefinition 없이 명시 버전·catalog로 절차 구축하는 Runtime loader와 Generator facade 추가
- Unity `6000.5.3f1` batchmode compile 성공, EditMode `51/51`, PlayMode `3/3` 통과

## 0.7.0

- 에디터 실험실에 `스테이지 자산` 탭을 추가해 현재 결과 새 Blueprint 저장, 선택 자산 덮어쓰기와 제작 메모를 연결
- `DungeonStageAuthoringService`에 코드 기반 저장 검증, 전체 중첩 데이터 Undo, AssetDatabase 저장과 StageDefinition `SerializedObject` 생성을 추가
- 절차 원본과 저장본의 seed·generatorVersion·recipe/catalog/blueprint hash 비교 및 `Identical`·`DifferentSeed`·`StaleInputs`·`Diverged` 상태 표시 추가
- 저장 Blueprint를 recipe·seed 재계산 없이 구축하는 `DungeonStageLoader.LoadSavedBlueprint`와 Generator facade 추가
- 저장본 미리보기, 이전 절차 시드 복귀와 SavedBlueprint RuntimeBuild StageDefinition 생성·Generator 연결 추가
- Blueprint·StageDefinition 강제 재임포트, 덮어쓰기 Undo, stale 분류와 무재계산 미리보기 회귀 테스트 추가
- Unity `6000.5.3f1` compile 성공, EditMode `48/48`, PlayMode `3/3` 통과; 실제 Unity 프로세스 완전 재시작과 화면 포인터 Raycast는 수동 검증 범위로 유지

## 0.6.0

- `DungeonContentCatalog` entry에 content key, category, Prefab, weight, progression, 방/복도 조건, room tag, footprint·간격, yaw·scale 변형과 선택적 drop/gameplay 연계 필드를 추가
- Unity Object가 없는 catalog planning snapshot과 entry/tag 순서 독립 canonical SHA-256을 추가하고 Prefab·drop table·gameplay ID를 planning hash에서 제외
- opt-in `StableV2` 생성기에 고정 PCG32와 Layout/Gimmick/Enemy/Destructible/Prop/Variant 독립 stream, spawn child stream과 고정 범주 우선순위를 추가
- `DungeonGeneratorVersions.Current`, 기존 settings-only `GenerateWithSeed`와 legacy loader facade는 승인된 LegacyV1 결과를 유지
- `IDungeonContentResolver`, 기본 `DungeonPrefabContentResolver`, Prefab/factory resolution과 `Error`·`BuiltInFallback`·`Skip` 누락 정책을 추가
- `DungeonStageDefinition`의 content catalog·누락 정책과 `DungeonLoadContext`의 custom resolver·정책 override를 RuntimeBuild에 연결
- catalog/Blueprint 교차 검증, strict 실패 전 기존 root 보존, resolver 결과와 stable identity 검증을 추가
- Prefab 드랍 설정 우선순위, 자식 클릭 대상의 루트 제거, 비활성 staging root 교체, 명시적 합성 Mesh 소유권과 요청 snapshot 무결성 검증을 보강
- Play만으로 R4를 확인할 수 있는 전용 Prefab·DropTable·Catalog·Stage Definition·Scene과 반복 생성 Editor 메뉴를 추가
- Unity `6000.5.3f1` compile 성공, EditMode `41/41`, PlayMode `3/3` 통과; 직접 파괴·드랍 통계 1회 누적은 자동 검증했고 실제 화면 포인터 Raycast는 수동 범위로 유지

## 0.5.0

- Added `DungeonStageDefinition` with Procedural/SavedBlueprint sources, RuntimeBuild/BakedPrefab modes, and RandomPerLoad/RunSeed/FixedSeed policies
- Added `DungeonLoadContext`, deterministic seed precedence, coded StageDefinition validation, and `DungeonStageLoadException`
- Added `DungeonStageLoader` and `DungeonStageInstance` with validated deep-copy loading, generated-root replacement, mesh cleanup, and compatible generation reports
- Connected `RogueDungeonGenerator` to optional StageDefinition loading while preserving the existing settings-only `GenerateWithSeed` facade
- Added `CurrentStageInstance` and Blueprint-derived `CurrentCellSize` so saved stages remain playable when their grid scale differs from current settings
- Added R3 tests for both source modes, seed precedence, non-regeneration of saved data, invalid-source blocking, facade compatibility, and saved-stage player placement
- Verified Unity 6000.5.3f1 EditMode `19/19` and PlayMode `2/2`

## 0.4.0

- Added the LegacyV1 `DungeonBlueprintGenerator` and data-only `DungeonContentPlanner` while preserving the approved random-call order and fingerprints
- Added `DungeonSceneBuilder`, built-in content keys, and `DungeonSpawnIdentity` for repeatable scene reconstruction from finalized spawn records
- Switched `RogueDungeonGenerator.GenerateWithSeed` to the request → Blueprint → scene-build facade and exposed `CurrentBlueprint`
- Kept `DungeonMeshBuilder.Build` and `DungeonContentSpawner.Spawn` legacy signatures as compatibility wrappers
- Added R2 validation for generated Blueprint integrity, stable two-build hierarchy/identities, and wrapper parity
- Verified Unity 6000.5.3f1 EditMode `14/14` and PlayMode `1/1`

## 0.3.0

- Added R0 EditMode regression coverage for three approved preset/seed fingerprints, 100-seed connectivity, and the existing generator facade/root contract
- Added normalized `DungeonRecipeSnapshot` and versioned `DungeonGenerationRequest` contracts without mutating source settings
- Added serializable `DungeonBlueprint`, `DungeonBlueprintAsset`, cell/room/spawn records, and canonical SHA-256 hashing
- Added Blueprint JSON deep-copy/round-trip support and coded validation for connectivity, identity, references, transforms, and stored-hash integrity
- Kept the current `GenerateWithSeed` scene pipeline unchanged until the R2 calculation/build split

## 0.2.2

- Added live Play HUD regeneration for stage sliders and presets while preserving the active seed
- Coalesced continuous drag changes into a throttled 0.08-second regeneration cycle
- Kept typed seed changes explicit so incomplete numeric input does not trigger unwanted generations
- Added PlayMode coverage for automatic settings application and active-seed preservation

## 0.2.1

- Expanded free-camera pitch from the downward-only 15°–85° range to -89°–89°
- Split the Play HUD into stage settings, exploration, and drop statistics tabs
- Added runtime controls for seed, presets, stage geometry, rooms, corridors, gimmicks, spacing, and content density profiles
- Made the HUD scale and fit landscape, portrait, compact, and ultrawide resolutions with scrollable tab content
- Added PlayMode coverage for upward camera pitch and responsive panel bounds

## 0.2.0

- Added WASD movement, Space/Ctrl elevation, and Shift acceleration for the free laboratory camera
- Changed free-camera right-drag to rotate in place and aligned WASD with the full 3D view direction
- Added a Play HUD button that spawns one temporary CharacterController at the dungeon entrance
- Added camera-relative movement, sprint, jump, entrance respawn, follow camera, and regeneration recovery
- Added generated floor and wall MeshColliders for playable traversal
- Added Korean control hints and updated usage, architecture, and test documentation

## 0.1.1

- Added the existing Unity Input System assembly reference for Unity 6000.5 projects
- Replaced removed `GetInstanceID` calls with `GetEntityId`
- Replaced deprecated scene lookup APIs with Unity 6000.5 equivalents
- Added matching partial script assets for Unity component and ScriptableObject serialization
- Fixed the first drop sample being discarded after default entry normalization

## 0.1.0

- Deterministic room/corridor layout and BFS progression
- Combined floor/wall meshes
- Curve-driven contents and special gimmicks
- One-click editor setup and live regeneration
- Click destruction, weighted drops, Monte Carlo sampling, Wilson intervals
- Codex instructions, skill, prompts, architecture and test docs
