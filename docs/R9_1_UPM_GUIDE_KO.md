# R9.1 UPM·Git URL 패키지 가이드

R9.1은 기존 R9 `.unitypackage` 일곱 단위를 없애지 않고, 반복 설치와 버전 고정이
쉬운 Unity Package Manager 배포 경로를 추가합니다. 저장소의 개발 원본은 계속
`Assets/RogueDungeonLab`이며, 다음 세 폴더는 배포를 위해 결정적으로 동기화한
추적 가능 사본입니다.

| package ID | 저장소 경로 | 포함 범위 |
|---|---|---|
| `com.dntlr2000.rogue-dungeon-lab.core` | `UpmPackages/com.dntlr2000.rogue-dungeon-lab.core` | Runtime Core, `RuntimeBuild Examples` Sample |
| `com.dntlr2000.rogue-dungeon-lab.lab` | `UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab` | 선택 HUD, 자유 카메라, 클릭 입력, 임시 플레이어 |
| `com.dntlr2000.rogue-dungeon-lab.baking` | `UpmPackages/com.dntlr2000.rogue-dungeon-lab.baking` | Editor-only Baker와 modular Baked Stage 배포 도구 |

현재 package 버전은 `0.13.0`, 최소 Unity 표기는 `6000.5`입니다. Core는 Input
System·Lab HUD·Editor 코드를 포함하지 않습니다. Lab은 Core와 Input System을,
Baking은 Core를 직접 요구하며 두 선택 package를 Player에 모두 넣을 필요는 없습니다.

## 어떤 설치 방식을 선택할까

- 같은 PC에서 개발 중이면 `로컬 디스크 package`가 가장 빠릅니다.
- Git 저장소에 push한 팀·CI 환경이면 `Git URL + commit/tag 고정`을 권장합니다.
- 인터넷이 완전히 없는 수신자에게 한 번 전달하면 기존 R9 `.unitypackage` 또는 세
  UPM 폴더의 전체 복사가 적합합니다.
- 완성한 개별 Baked Stage는 계속 `.unitypackage`와 JSON sidecar로 전달합니다.
  UPM은 공통 코드·제작 도구의 버전 관리, Stage 묶음은 프로젝트별 제작 자산의
  무결성 기록을 담당합니다.

## UPM 원본 동기화

Runtime이나 Lab, Baker/Packaging 코드를 바꾼 뒤 원본 저장소에서 실행합니다.

```text
Tools > Rogue Dungeon Lab > R9.1 UPM 패키지 동기화
```

실험실의 `스테이지 자산 > R6·R7 배포용 Bake > R9 다른 프로젝트용 패키지`
영역에 있는 같은 이름의 버튼도 사용할 수 있습니다. batchmode 진입점은 다음과
같습니다.

```text
-executeMethod RogueDungeonLab.Editor.RogueDungeonUpmPackageExporter.SyncFromBatch
```

동기화기는 정확히 세 package 직계 폴더만 다시 만들며 다른 `UpmPackages` 항목을
삭제하지 않습니다. Runtime·Sample·Lab·Baking 원본과 `.meta`는 byte 단위로
복사하고, manifest·README·CHANGELOG·LICENSE 안내는 고정 내용과 GUID로
생성합니다. Baking 사본에는 저장소 유지보수 전용 동기화기 자체를 넣지 않습니다.
동기화 뒤 `UpmPackages` 변경도 소스 변경과 함께 commit해야 합니다.

## 로컬 디스크에서 설치

소비 프로젝트의 `Window > Package Management > Package Manager`에서 `+` 메뉴의
`Install package from disk`를 선택하고 원하는 폴더의 `package.json`을 고릅니다.
Lab 또는 Baking을 쓸 때는 Core도 소비 프로젝트의 직접 dependency로 설치하십시오.

`Packages/manifest.json`을 직접 관리한다면 다음처럼 절대 또는 프로젝트 기준 상대
`file:` 경로를 사용할 수 있습니다.

```json
{
  "dependencies": {
    "com.dntlr2000.rogue-dungeon-lab.core": "file:E:/Unity/LevelMaker/UpmPackages/com.dntlr2000.rogue-dungeon-lab.core",
    "com.dntlr2000.rogue-dungeon-lab.lab": "file:E:/Unity/LevelMaker/UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab"
  }
}
```

Baking 소비 프로젝트는 위 Lab 줄 대신
`com.dntlr2000.rogue-dungeon-lab.baking` package root를 지정합니다. 폴더를 다른
PC로 복사할 때는 package 내부의 `.meta`, `Samples~`, README와 manifest를 포함해
폴더 전체를 전달해야 합니다.

## Git URL로 설치

현재 저장소 원격 주소를 기준으로 한 URL 형식은 다음과 같습니다. `<revision>`에는
push된 commit SHA 또는 실제로 만든 tag를 넣습니다. 존재하지 않는 `v0.13.0` tag를
가정하지 말고, 릴리스 tag를 만들기 전에는 commit SHA로 고정하십시오.

```text
https://github.com/dntlr2000/LevelMaker.git?path=/UpmPackages/com.dntlr2000.rogue-dungeon-lab.core#<revision>
https://github.com/dntlr2000/LevelMaker.git?path=/UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab#<revision>
https://github.com/dntlr2000/LevelMaker.git?path=/UpmPackages/com.dntlr2000.rogue-dungeon-lab.baking#<revision>
```

Package Manager의 `Install package from git URL`에 하나씩 넣거나 다음처럼 manifest에
직접 기록할 수 있습니다.

```json
{
  "dependencies": {
    "com.dntlr2000.rogue-dungeon-lab.core": "https://github.com/dntlr2000/LevelMaker.git?path=/UpmPackages/com.dntlr2000.rogue-dungeon-lab.core#<revision>",
    "com.dntlr2000.rogue-dungeon-lab.baking": "https://github.com/dntlr2000/LevelMaker.git?path=/UpmPackages/com.dntlr2000.rogue-dungeon-lab.baking#<revision>"
  }
}
```

Lab/Baking의 package manifest에는 Core `0.13.0` 계약이 있지만 이 custom package가
Unity 기본 registry에 게시된 것은 아닙니다. 따라서 Git 설치에서도 Core URL을
소비 프로젝트의 직접 dependency로 같이 기록해야 합니다. 한 프로젝트의 세 URL은
동일 revision으로 맞춰야 원본과 동기화 버전이 어긋나지 않습니다. 비공개 GitHub
저장소라면 Unity를 실행하는 사용자·CI가 해당 저장소 인증 권한을 가져야 합니다.

Unity 공식 문서의 [Git dependency와 subfolder `?path=` 형식](https://docs.unity3d.com/cn/6000.0/Manual/upm-ui-giturl.html),
[package manifest와 Samples~ 필드](https://docs.unity3d.com/cn/6000.0/Manual/upm-manifestPkg.html),
[custom package 공유 방식](https://docs.unity3d.com/cn/6000.0/Manual/cus-share.html)을
기준으로 구성했습니다.

## Core Sample 가져오기

Core 설치 뒤 Package Manager에서 `Rogue Dungeon Lab - Runtime Core`를 선택하고
`Samples`의 `RuntimeBuild Examples`를 Import합니다. Unity는 이를 소비 프로젝트의
`Assets/Samples` 아래로 복사합니다. 두 예제는 다음을 검증합니다.

- 고정 seed `Procedural + RuntimeBuild`
- 저장된 `SavedBlueprint + RuntimeBuild`
- Lab HUD·Input System 없이 Generator·Camera·Light만 둔 제품형 scene

## Baking과 Baked Stage 전달

1. Core와 Baking UPM package를 제작 프로젝트에 직접 설치합니다.
2. SavedBlueprint Definition과 영속 재질 세트로 `DungeonStageBaker.Bake`를 실행합니다.
3. `DungeonDistributionExporter.PlanBakedStage(definition)`의 modular 계획을 내보냅니다.
4. 수신 프로젝트에는 같은 revision의 Core UPM package를 설치한 뒤 Baked Stage
   `.unitypackage`와 sidecar가 요구하는 render pipeline package를 가져옵니다.

설치된 UPM package cache를 다른 `.unitypackage` 안에 다시 넣는 것은 지원하지
않습니다. 따라서 UPM 소비 프로젝트의 `Core 포함 독립 묶음`과 Core/Baking 자체의
legacy 재포장은 `RDL-DIST-012`로 내보내기 전에 차단됩니다. standalone Baked Stage가
필요하면 개발 원본 LevelMaker 저장소에서 기존 R9 메뉴로 생성하십시오.

## 중복 설치 금지

UPM package는 기존 개발 원본의 공개 assembly 이름과 자산 GUID를 의도적으로
보존합니다. 한 소비 프로젝트에 다음을 동시에 넣으면 assembly/GUID가 중복됩니다.

- `Assets/RogueDungeonLab/Runtime`과 Core UPM
- legacy Lab Sample `.unitypackage`와 Lab UPM
- legacy Bake Authoring `.unitypackage`와 Baking UPM

기존 `.unitypackage` 설치에서 UPM으로 전환할 때는 먼저 버전 관리에서 안전한 지점을
만든 뒤 legacy 폴더를 제거하고 UPM을 설치하십시오. 제작 자산의 Script GUID가
보존되므로 정상 자산 참조는 이어지지만, 제품별 custom 코드와 Prefab은 전환 뒤
컴파일·Play·Player build를 다시 확인해야 합니다.

## 자동 검증

R9.1 전용 검증은 기존 프로젝트나 C 드라이브에 임시 project를 만들지 않습니다.

```powershell
powershell.exe -ExecutionPolicy Bypass -File tools/verify-r9.1-upm-packages.ps1
```

기본 출력은 `E:\CodexValidation\RogueDungeonLabR91\Run_<timestamp>`, Unity TEMP/TMP는
`E:\CodexTemp`입니다. 스크립트는 다음을 새 프로젝트 세 개에서 확인합니다.

1. Core-only: Sample 두 source 로드, Lab assembly 부재, HUD 없는 Windows Player build
2. Lab: Core·Lab·Input System 등록과 HUD·카메라·클릭·임시 플레이어 직렬화
3. Baking: 실제 영속 Bake, Core 요구사항이 있는 modular Stage package와 sidecar 생성,
   UPM cache를 합치려는 legacy standalone 계획의 `RDL-DIST-012` 차단

패키지 자체의 manifest 경계, 원본 byte parity, GUID와 반복 동기화 tree hash는
`RogueDungeonUpmPackageTests` EditMode 테스트가 담당합니다.
