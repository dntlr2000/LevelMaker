# Changelog

## 0.14.0

- Flexible FPS Arena V2: seeded stairs, varied cover footprints/shapes, independent enemy/gimmick/item densities, actual prefab catalog and initialization hooks.
- Preserve saved V1 recipes/hashes until explicit upgrade; add portable Lab Editor authoring tabs.

## 0.13.0

- 절차 생성 직후 검증·Build 전에 실행되는 결정적 `IDungeonBlueprintPostprocessor` 계약 추가
- RunSeed, request ID와 런타임 후처리 override를 전달하는 StageDefinition facade 추가
- Core·Lab·Baking package 버전 및 결정적 UPM 동기화 갱신

## 0.12.0

- Runtime Core, 선택 Lab Sample, Editor-only Baking Tools의 UPM/Git URL 설치 구조 추가
- 개발 원본 GUID와 assembly 경계를 보존하는 결정적 동기화 추가
- 기존 R9 `.unitypackage` 배포 경로 유지
