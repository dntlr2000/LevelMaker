# R9 다른 Unity 프로젝트용 패키지 가이드

R9 배포본은 실험실 전체를 한 번에 복사하지 않고 제품 런타임, 선택 Sample, 제작 도구와 Baked 스테이지를 독립 설치 단위로 나눕니다. 모든 결과는 Unity `6000.5`용 `.unitypackage`이며 같은 이름의 `.unitypackage.json` sidecar가 SHA-256, 생성 Unity 버전, 포함 자산, 렌더 파이프라인과 추가 Unity package 버전을 기록합니다.

## 패키지 생성

원본 LevelMaker 프로젝트에서 다음 메뉴를 실행합니다.

```text
Tools > Rogue Dungeon Lab > R9 배포 패키지 생성
```

기본 출력은 `Distribution/RogueDungeonLab/R9`입니다. 실험실의 `스테이지 자산 > R6·R7 배포용 Bake > R9 다른 프로젝트용 패키지`에서는 Core, 예제, Sample, 제작 도구 또는 현재 선택한 Baked Stage를 따로 내보낼 수 있습니다. Baked Stage가 stale이거나 `SavedBlueprint + BakedPrefab` 계약을 만족하지 않으면 내보내기 전에 차단됩니다.

CI 또는 batchmode에서는 다음 진입점을 사용합니다.

```text
-executeMethod RogueDungeonLab.Editor.R9PackageVerificationSetup.ExportAllFromBatch
```

## 설치 조합

| 목적 | 가져올 파일 | 사전 조건 |
|---|---|---|
| 절차형 또는 SavedBlueprint RuntimeBuild | `rogue-dungeon-lab-runtime-core.unitypackage` | Unity 6000.5 기본 Physics·UI·JSON 모듈 |
| Core 사용 예제 두 장면 | Core 뒤 `rogue-dungeon-lab-runtime-examples.unitypackage` | 추가 package 없음 |
| 실험실 Play HUD·카메라·클릭·임시 플레이어 | Core 뒤 `rogue-dungeon-lab-lab-sample.unitypackage` | sidecar 버전의 `com.unity.inputsystem` |
| 다른 프로젝트에서 Bake/배포 도구 사용 | Core + `rogue-dungeon-lab-bake-authoring.unitypackage` | 또는 standalone 제작 도구 하나 |
| 제품에 Baked 스테이지 추가 | Core + `rogue-dungeon-lab-stage-<stage-id>.unitypackage` | stage sidecar의 render pipeline/package |
| Core 없는 프로젝트에 Baked 스테이지 한 번에 추가 | `rogue-dungeon-lab-stage-<stage-id>-standalone.unitypackage` | stage sidecar의 render pipeline/package |

`bake-authoring-standalone`과 `stage-...-standalone`은 각각 Runtime Core를 포함합니다. 같은 프로젝트에서 modular Core와 standalone을 중복 설치하지 마십시오. `.unitypackage`는 UPM registry package가 아니므로 Unity의 `Assets > Import Package > Custom Package`로 가져옵니다.

## 제품 장면과 HUD 경계

Runtime Core의 `RogueDungeonLab.Runtime` 어셈블리는 `UnityEditor`, Input System, `RuntimeLabHUD`, `LabOrbitCamera`, `PrototypePlayerController`와 입력용 `RogueDungeonClickInteractor`를 참조하지 않습니다. 제품 장면에는 `RogueDungeonGenerator`와 `DungeonStageDefinition`만 둘 수 있으며, Lab Sample을 설치했더라도 Sample 컴포넌트를 장면 또는 Prefab에 직접 넣지 않으면 HUD가 표시되지 않습니다.

임시 플레이어 대신 제품 캐릭터의 pose를 R8 RunState에 저장하려면 캐릭터가 `IDungeonRunStatePlayer`를 구현하고 활성 Generator에 등록해야 합니다.

```csharp
generator.RegisterRunStatePlayer(productPlayer);
```

해제 또는 파괴 시에는 `UnregisterRunStatePlayer`를 호출합니다. 제품 전투 시스템이 Enemy·Destructible을 제거할 때는 기존과 같이 stable identity를 `RecordSpawnRemoved`에 한 번 전달합니다.

## Baked Stage 가져오기

1. stage sidecar의 `unityVersion`, `renderPipeline`, `requiredPackages`를 확인합니다.
2. 요구 render pipeline을 같은 major/minor 버전으로 설치하고 프로젝트의 Graphics/Quality pipeline asset을 연결합니다.
3. modular 조합이면 Runtime Core를 먼저, Baked Stage를 나중에 가져옵니다. standalone은 Stage package 하나만 가져옵니다.
4. 가져온 `DungeonStageDefinition`을 `RogueDungeonGenerator.stageDefinition`에 연결합니다.
5. Play 또는 Player에서 Loader가 manifest, source/final Blueprint hash, Override와 Prefab metadata를 검사한 뒤 Baked Prefab을 인스턴스화하는지 확인합니다.

배포 도구는 StageDefinition, Blueprint, Override, Catalog, settings, material set, Catalog Prefab과 manifest 소유 Mesh·Prefab의 dependency closure를 수집합니다. Sample·Editor·Tests 자산에 대한 제품 Stage 의존은 `RDL-DIST-006`~`008`로 차단하며, Built-in/URP/HDRP/custom 재질이 한 Stage에 섞이면 `RDL-DIST-011`로 차단합니다.

## Sidecar와 무결성

패키지를 전달할 때 `.unitypackage`와 `.unitypackage.json`을 함께 전달합니다. 수신자는 파일 SHA-256을 sidecar의 `packageSha256`과 비교하고, Baked Stage라면 `stageId`, `sourceBlueprintHash`, `finalBlueprintHash`, `overrideHash`도 릴리스 기록에 보존합니다. `PACKAGE_INDEX_KO.md`에는 한 번의 출력에서 생성된 전체 package ID와 hash가 요약됩니다.

## 깨끗한 소비 프로젝트 자동 검증

PowerShell 검증은 timestamp별 새 프로젝트를 `Logs/R9ConsumerVerification` 아래에 만들며 기존 프로젝트를 삭제하지 않습니다.

```powershell
powershell.exe -ExecutionPolicy Bypass -File tools/verify-r9-packages.ps1
```

첫 프로젝트는 Input System 없이 Core와 Runtime Examples를 가져와 Procedural·SavedBlueprint RuntimeBuild를 검사하고 HUD 없는 Windows64 Development Player를 빌드합니다. 두 번째 프로젝트는 sidecar의 render pipeline package, standalone Bake Authoring과 modular Baked Stage를 가져와 Runtime manifest와 Editor fingerprint, final hash·stable identity·저장 Mesh 계약을 검사한 뒤 Player를 빌드합니다. 각 Unity 호출은 15분 제한을 가지며 성공하면 `VERIFICATION_SUMMARY_<timestamp>.json`을 남깁니다.

현재 기준 실행은 Unity `6000.5.3f1`에서 두 Player 빌드 모두 오류·경고 `0`개로 성공했습니다. 프로젝트 전체 회귀 결과는 EditMode `95/95`, PlayMode `11/11`입니다.
