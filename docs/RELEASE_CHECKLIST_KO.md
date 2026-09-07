# Rogue Dungeon Lab 릴리즈 경계 점검표

이 문서는 범용 Core·Lab·Baking 패키지와 특정 게임용 Stage 저작물을 분리하기 위한
릴리즈 전 점검 기준입니다.

## 저장소에 유지하는 범위

- `Assets/RogueDungeonLab/Runtime`: 제품 런타임과 범용 생성·로드 API
- `Assets/RogueDungeonLab/Samples/Lab`: 선택형 HUD, 자유 카메라와 임시 플레이어
- `Assets/RogueDungeonLab/Editor/Baking`: Editor-only Bake 도구
- `Assets/RogueDungeonLab/Editor/Packaging`: 범용 패키징 도구
- `Assets/RogueDungeonLab/Examples/RuntimeBuild`: HUD 없는 Runtime 예제
- `UpmPackages`: 위 원본에서 결정적으로 동기화한 Core·Lab·Baking 배포 사본
- `Distribution/RogueDungeonLab`: 범용 R9 legacy 배포 인덱스와 sidecar

## 별도로 보관하는 범위

특정 게임이나 임무에 종속된 다음 자료는 범용 저장소에 두지 않습니다.

- 게임 고유 Recipe, Catalog, Content Key와 Prefab
- 제품 Stage Definition, Blueprint, Override, Preview Scene과 Bake 결과
- 소비 게임의 Binder·Bootstrap 계약과 제품 전용 검증 코드
- 제품별 `.unitypackage`, sidecar와 handoff 문서

제품 Stage를 보존할 때는 `.meta`를 포함한 저작 폴더 전체를 독립 참조 샘플이나 제품
저장소로 옮깁니다. 활성 Stage Definition이 참조하는 Bake 버전은 함께 보관하고,
참조가 끝난 이전 Bake는 `Historical`처럼 명확히 구분합니다. `.unitypackage`는 Git
소스 대신 별도 릴리즈 첨부물로 전달합니다.

## 자동 경계 검사

저장소 루트에서 다음 명령을 실행합니다.

```powershell
pwsh -NoProfile -File tools/verify-release-boundaries.ps1
```

검사는 알려진 제품 전용 경로가 남아 있지 않은지, Runtime·UPM·Build Settings에 제품
전용 토큰이 유입되지 않았는지 확인합니다. 새 제품 샘플을 만들었다면 스크립트의
금지 경로와 토큰 목록도 함께 갱신합니다.

## 최종 패키지 검증

1. `Tools > Rogue Dungeon Lab > R9.1 UPM 패키지 동기화`를 실행합니다.
2. 원본과 UPM 사본의 byte parity·GUID·tree hash 테스트를 실행합니다.
3. 전체 EditMode와 PlayMode 테스트를 실행합니다.
4. Core-only, Lab, Baking의 깨끗한 소비 프로젝트 smoke를 실행합니다.
5. `ProjectSettings/EditorBuildSettings.asset`에 제품 전용 Preview Scene이 활성화되지
   않았는지 확인합니다.
6. Git 상태에서 의도하지 않은 생성 자산과 `.unitypackage`가 없는지 확인합니다.

검증 프로젝트·로그·캐시는 `E:\CodexValidation`, TEMP/TMP는 `E:\CodexTemp`만
사용합니다.
