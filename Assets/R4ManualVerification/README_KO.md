# R4 수동 검증 환경

이 폴더는 실제 프로젝트 자산과 분리된 R4 전용 Play 검증 환경입니다.

## 바로 시작하기

1. `Scenes/R4_ManualVerification.unity`를 엽니다.
2. Console을 비웁니다.
3. Play를 누릅니다.
4. 청록색 적과 주황색 파괴물을 좌클릭합니다.
5. HUD의 `드랍 통계`에서 클릭 한 번마다 Attempts가 정확히 1 증가하고 `CatalogDrop`이 기록되는지 확인합니다.
6. HUD 위를 클릭할 때 뒤의 대상이 파괴되지 않는지 확인합니다.

장면과 자산을 다시 기준 상태로 복구하려면 다음 메뉴를 실행합니다.

```text
Tools > Rogue Dungeon Lab > R4 수동 검증 환경 생성
```

이 메뉴는 현재 수정된 장면의 저장 여부를 먼저 묻고, R4 검증 장면을 새로 구성해 엽니다.

## 폴더 구조

```text
R4ManualVerification/
├── Catalogs/
│   ├── R4_AutoTargetCatalog.asset
│   ├── R4_AuthoredTargetCatalog.asset
│   └── R4_MissingContentCatalog.asset
├── DropTables/
│   ├── R4_CatalogDrop.asset
│   └── R4_PrefabDrop.asset
├── Materials/
├── Prefabs/
│   ├── R4_Enemy_AutoTarget.prefab
│   ├── R4_Enemy_AuthoredTarget.prefab
│   └── R4_Breakable_AutoTarget.prefab
├── Scenes/
│   └── R4_ManualVerification.unity
├── Settings/
│   └── R4_ManualSettings.asset
└── Stages/
    ├── R4_StableV2_AutoTarget.asset
    ├── R4_StableV2_AuthoredTarget.asset
    ├── R4_Missing_Error.asset
    ├── R4_Missing_Fallback.asset
    └── R4_Missing_Skip.asset
```

## Stage Definition별 예상 결과

장면의 `Rogue Dungeon Generator`를 선택하고 `Stage Definition`만 교체한 뒤 Play합니다.

### R4_StableV2_AutoTarget

- 기본 연결 상태입니다.
- 청록색 적과 주황색 파괴물이 생성됩니다.
- Prefab에 target이 없으므로 런타임 root에 자동 보강됩니다.
- 클릭 결과는 항상 `CatalogDrop`입니다.

### R4_StableV2_AuthoredTarget

- 자홍색 적의 자식에 작성된 `DestructibleDropTarget`을 사용합니다.
- 적 클릭 결과는 Catalog보다 Prefab 설정이 우선해 항상 `PrefabDrop`입니다.
- 자식 target을 클릭해도 적 root 전체가 제거되어야 합니다.
- 주황색 파괴물은 계속 `CatalogDrop`을 사용합니다.

### R4_Missing_Error

- 누락 Prefab 때문에 로드를 차단해야 합니다.
- 정상 스테이지를 먼저 만든 뒤 이 Definition으로 재로드하면 기존 generated root가 유지되어야 합니다.
- Console에서 `RDL-CONTENT-008`을 확인합니다.

### R4_Missing_Fallback

- 누락 Enemy가 category별 임시 primitive로 생성되어야 합니다.
- 스테이지 전체는 정상적으로 로드되어야 합니다.

### R4_Missing_Skip

- 누락 Enemy spawn만 생략되어야 합니다.
- geometry와 다른 built-in 콘텐츠는 정상적으로 생성되어야 합니다.

## 결정성 확인

모든 Stage Definition은 `StableV2`, Fixed Seed `12345`입니다.

1. 기본 장면에서 현재 시드 재생성을 세 번 실행합니다.
2. 방·복도·입구·출구와 콘텐츠 위치가 동일한지 확인합니다.
3. `R4_StableV2_AutoTarget`과 `R4_StableV2_AuthoredTarget`을 번갈아 사용합니다.
4. 두 Catalog는 logical key와 planning 필드가 같으므로 외형과 드랍 설정만 달라지고 spawn 위치는 같아야 합니다.

## 알려진 R4 제한

- `requiredRoomTags`가 있는 entry는 절차 생성 방에 태그를 할당하는 후속 단계 전까지 자동 선택되지 않습니다.
- `BakedPrefab`은 R6 범위이므로 현재 validator가 차단합니다.
- 실제 화면 포인터 Raycast와 HUD 클릭 차단은 이 장면에서 사람이 직접 확인해야 합니다.
