# Changelog

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
