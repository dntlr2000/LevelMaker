# Changelog

## 0.6.0

- `DungeonContentCatalog` entry에 content key, category, Prefab, weight, progression, 방/복도 조건, room tag, footprint·간격, yaw·scale 변형과 선택적 drop/gameplay 연계 필드를 추가
- Unity Object가 없는 catalog planning snapshot과 entry/tag 순서 독립 canonical SHA-256을 추가하고 Prefab·drop table·gameplay ID를 planning hash에서 제외
- opt-in `StableV2` 생성기에 고정 PCG32와 Layout/Gimmick/Enemy/Destructible/Prop/Variant 독립 stream, spawn child stream과 고정 범주 우선순위를 추가
- `DungeonGeneratorVersions.Current`, 기존 settings-only `GenerateWithSeed`와 legacy loader facade는 승인된 LegacyV1 결과를 유지
- `IDungeonContentResolver`, 기본 `DungeonPrefabContentResolver`, Prefab/factory resolution과 `Error`·`BuiltInFallback`·`Skip` 누락 정책을 추가
- `DungeonStageDefinition`의 content catalog·누락 정책과 `DungeonLoadContext`의 custom resolver·정책 override를 RuntimeBuild에 연결
- catalog/Blueprint 교차 검증, strict 실패 전 기존 root 보존, resolver 결과와 stable identity 검증을 추가
- Prefab 드랍 설정 우선순위, 자식 클릭 대상의 루트 제거, 비활성 staging root 교체, 명시적 합성 Mesh 소유권과 요청 snapshot 무결성 검증을 보강
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
