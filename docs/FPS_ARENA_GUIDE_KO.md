# FPS 아레나 제작 V2 — 재사용 가능한 범용 생성기

## 시작

1. `Tools > Rogue Dungeon Lab > FPS 아레나 제작` 또는 기존 실험실의 `FPS 아레나 제작 모드 열기`를 선택합니다.
2. 새 장면에서 시작하려면 `새 FPS 테스트 장면 만들기`를 누릅니다. 저장하지 않은 기존 장면은 먼저 저장 여부를 묻습니다. 현재 장면을 사용하려면 생성기를 지정하거나 `현재 장면에 생성기 추가`를 선택합니다.
3. 새 생성기와 새 설정 자산은 **V2 범용 생성**을 사용합니다. 기존 V1 설정은 자동으로 바꾸지 않습니다. 아래의 명시적 업그레이드 절차를 따릅니다.
4. 결투(1층), 팀전(2층), 다층(4층) 프리셋을 고르거나 각 탭의 값을 직접 조절하고 `시드로 생성 / 재생성`을 누릅니다.
5. Play 후 Game 뷰를 클릭합니다. 테스트 캐릭터는 WASD 이동, 마우스 시점, Shift 달리기, Space 점프, Esc 커서 해제, R 첫 스폰 복귀를 지원합니다.
6. `현재 설정을 새 자산으로 저장`은 시드·정규화 레시피와 콘텐츠 카탈로그 연결을 저장하고 생성기에 연결합니다. 공유 설정이 연결되어 있으면 창과 Inspector가 해당 자산을 직접 편집합니다. 프리셋·필드 편집·연결 변경·재생성은 Undo를 지원합니다.

## 여섯 탭

제작 창, 생성기 Inspector, 설정 자산 Inspector에서 같은 설정 탭을 사용합니다.

- **지형·계단**: 가로/세로, 외곽 형태, 층 수, 셀 크기, 층 높이, 층 사이 계단 수, 공통 배치 간격과 층별·범주별 최대 개수
- **엄폐**: 엄폐 밀도, 기본 엄폐의 가로/세로/높이 범위, 상자·원기둥·L자 형태 선택
- **적**: 적 밀도. 실제 적 프리팹은 프리팹 탭에서 등록
- **특수 기믹**: 기믹 밀도. 함정·상호작용 장치 등 프로젝트 프리팹과 연동
- **아이템**: 아이템 밀도. 실제 픽업과 지급 동작은 프로젝트 프리팹에서 구현
- **프리팹**: 콘텐츠 카탈로그 생성/연결, 범주별 실제 프리팹·가중치·점유 크기·Bounds 편집

생성기와 제작 창은 마지막 생성 결과의 층별 **실제 / 목표** 개수와 배치 해시를 보여 줍니다. 해당 콘텐츠 탭에서는 그 범주만 비교하고 지형/프리팹 탭에서는 전체 범주를 비교합니다. 이 결과는 마지막 생성 기록입니다. 입력을 변경해도 기존 장면은 재생성 전까지 유지됩니다.

## 지형과 시드 가변 계단

- 가로/세로: 각각 20–64셀, 셀 크기 2–5m
- 외곽 형태: 직사각형/타원/팔각형. 선택한 외곽 형태와 크기는 시드가 바뀌어도 유지
- 층 수: 1–4층, 층 간 높이 3–6m
- 계단: 층 사이 1–2개, 폭 2셀. V2는 시드 전용 난수 스트림으로 유효한 위치를 선택하므로 같은 지형에서도 시드를 바꾸면 계단 배치가 변함
- 서로 이웃한 층의 계단은 앞/뒤 구간을 번갈아 쓰고 같은 층 사이 두 계단은 떨어진 공간에 배치
- 계단 실제 단높이 ≤0.18m, 평균 기울기 ≤0.6, 층 바닥 두께 0.2m
- 계단 상부 바닥을 제거해 개구부를 확보하고 측면/하단 가드를 생성. 상부 착지 통로는 막지 않음
- 층별 중앙의 2셀 폭 통로, 각 스폰의 3×3셀 공간, 계단 착지와 연결 통로를 먼저 예약
- 계단과 콘텐츠는 서로 다른 결정적 난수 스트림을 사용. 콘텐츠 밀도를 바꾸어도 계단 위치를 다시 뽑지 않음

## 독립 밀도와 실제 개수

엄폐·적·특수 기믹·아이템에 각각 **0–100% 밀도**를 지정합니다. 0%인 범주는 배치하지 않습니다.

층별 목표 개수는 다음 순서로 계산합니다.

1. 해당 층의 바닥에서 예약 통로·스폰·계단 여유 공간을 뺀 셀 수를 계산
2. 비예약 바닥 셀 수 × 해당 범주 밀도를 올림
3. `층별·범주별 최대 개수`로 제한

공통 최대 개수는 기본 100, 허용 범위 1–1000입니다. 큰 맵이나 가벼운 콘텐츠를 위한 고급 안전 한도이며 네 범주에 각각 적용됩니다. 밀도는 목표 개수 계산 비율입니다. 넓은 엄폐 한 개가 여러 셀을 차지하므로 밀도와 실제 바닥 점유율은 같지 않습니다.

배치 순서는 **엄폐 → 적 → 특수 기믹 → 아이템**입니다. 범주마다 난수 스트림과 목표 밀도가 분리되어 있습니다. 공간은 공유하므로 앞선 범주의 점유 크기나 밀도를 늘리면 뒤 범주에 남는 배치 공간이 줄 수 있습니다. 목표를 맞추기 위해 통로·계단·스폰·간격을 침범하지 않습니다. 밀도가 높아도 실제 개수가 목표보다 작을 수 있으며 창의 층별 비교로 확인합니다.

## 엄폐 크기와 형태

기본 엄폐는 시드로 아래 범위에서 폭·깊이·높이를 선택합니다.

- 가로/세로 점유 범위: 각각 1–4셀, 최소 ≤ 최대
- 높이 범위: 0.5–2.5m, 최소 ≤ 최대
- 형태: 상자, 원기둥, 모서리(L자)를 하나 이상 선택
- 여러 셀을 차지하는 엄폐는 전체 사각 footprint와 주변의 걸을 수 있는 셀 및 간격을 검사
- 논리 연결성 검사는 엄폐 footprint 전체를 차단 셀로 취급. 실제 L자나 원기둥 모양보다 보수적으로 확인

형태를 모두 끄면 정규화 시 상자로 대체합니다. 범위의 최소와 최대를 같게 하면 해당 크기를 고정할 수 있습니다.

카탈로그에 **엄폐 범주의 실제 프리팹**을 등록하면 해당 범주는 카탈로그 항목의 점유 크기·높이·크기 배율을 사용합니다. 엄폐 탭의 기본 primitive 크기/형태 범위는 그 범주에 프리팹 항목이 없을 때 적용됩니다.

## 실제 게임 프리팹 카탈로그

1. **프리팹** 탭에서 `새 콘텐츠 카탈로그 만들기`를 누르거나 `Assets > Create > Rogue Dungeon Lab > FPS Arena 콘텐츠 카탈로그`로 자산을 만듭니다.
2. 생성기 또는 공유 설정의 `콘텐츠 카탈로그`에 연결합니다. 공유 설정이 있으면 그 설정의 카탈로그를 사용합니다.
3. `프리팹 항목 추가`로 항목을 만들고 아래 필드를 채웁니다. 카탈로그 자산 Inspector에서도 같은 한국어 편집기를 사용합니다.
4. `프리팹에서 Bounds 읽기`로 원본 크기와 중심을 계산하고, 실제 애니메이션/충돌 범위가 모두 포함되는지 검수합니다.
5. 생성 후 적·기믹·아이템이 임시 표식 대신 등록된 프리팹으로 만들어지는지 확인합니다.

### 항목 필드

- **고유 콘텐츠 키**: 카탈로그 전체에서 고유한 문자열. 대소문자를 구분하며 앞뒤 공백/빈 문자열 금지
- **범주**: 엄폐/적/특수 기믹/아이템. V1의 `Obstacle` 값은 지원하지 않음
- **프리팹**: 프로젝트의 실제 Prefab 자산. AI·픽업·기믹 컴포넌트가 작성된 프리팹 연결 가능
- **선택 가중치**: 유한한 양수. 같은 범주 내에서 상대 가중치로 선택
- **가로/세로 점유**: 각각 1–4셀. 배치·겹침·예약 통로 검사에 사용
- **배치 높이**: 0m 초과–6m. 층간 여유를 위해 생성 시 최대 `층 높이 - 0.25m`로 제한
- **크기 배율 범위**: 0.25–1, 최소 ≤ 최대. 선택된 배율은 가로/세로 점유 공간의 실제 배치 크기에 적용
- **원본 Bounds 크기/중심**: 프리팹 루트 로컬 좌표 기준. 자식 Renderer와 Collider까지 포함해야 함

Bounds 읽기는 Renderer, Box/Sphere/Capsule/MeshCollider 및 CharacterController를 지원하며 비활성 자식도 포함합니다. 외부 Transform의 위치/회전/scale을 제거한 루트 로컬 좌표로 계산합니다. 비균일한 자식 scale에서도 Sphere는 가장 큰 축의 반지름, Capsule/CharacterController는 수직 축의 높이와 나머지 축의 최대 반지름을 반영합니다. 임의의 Transform shear(비균일 부모 scale과 자식 회전의 조합)는 정확한 물리 변환 계약이 아니므로 피하거나 실제 Collider Bounds를 따로 검수합니다. 런타임에 확장되는 애니메이션, 지원하지 않는 Collider 또는 스크립트 생성 geometry는 필요하면 크기와 중심을 직접 보강합니다.

빌더는 계획된 각 축의 크기를 원본 Bounds 크기로 나눈 비율 중 **가장 작은 값을 균일 scale**로 적용합니다. 모델의 종횡비와 Sphere/Capsule 충돌 형태를 보존하면서 전체 Bounds를 계획 공간 안에 맞춥니다. 원본 Bounds 중심으로 위치를 보정해 실제 축소된 Bounds의 밑면이 바닥에 닿고 footprint 중앙에 맞도록 합니다. 중심 피벗과 바닥 피벗 모두 크기와 중심을 정확히 입력해야 합니다. 프리팹 루트의 원래 회전/scale은 정규화하고 자식 Transform은 유지합니다. 균일 fitting 때문에 실제 모델은 계획된 높이·가로·세로 중 일부를 덜 채울 수 있습니다. 실제 외형과 Collider를 Game 뷰에서 검수합니다. 회전은 계획된 0°/180°를 사용해 가로/세로 footprint를 유지합니다.

카탈로그 목록 순서는 선택과 논리 배치 해시에 영향을 주지 않습니다. 키·범주·가중치·점유 크기·배치 높이·크기 배율은 planning snapshot에 들어갑니다. 실제 Prefab 참조와 원본 Bounds·재질은 논리 planning 해시에서 제외됩니다. 같은 planning 해시라도 프리팹이나 Bounds를 변경하면 구축 외형이 달라질 수 있으므로 재생성/검수가 필요합니다.

카탈로그에서 중복 키, 누락 프리팹, 잘못된 범주·가중치·치수를 경고합니다. 잘못된 카탈로그는 생성 전에 수정해야 합니다. 특정 범주에 항목이 없으면 기본 엄폐 또는 적·기믹·아이템 표식으로 생성합니다.

## 게임 동작 연결

실제 적 AI·NavMesh 생성·전투·팀 밸런싱·함정 규칙·픽업 지급은 프로젝트 코드의 책임입니다. 생성기는 실제 Prefab 인스턴스와 위치·ID·키를 제공하며 기존 게임 컴포넌트를 보존합니다.

- 각 콘텐츠 wrapper의 `FpsArenaContentIdentity`에서 `StableId`, `Kind`, `Cell`, `ContentKey`, `PlannedSize`를 읽음
- 프리팹 컴포넌트가 `IFpsArenaContentInitializer`를 구현하면 `InitializeArenaContent(FpsArenaSpawnContext)`가 비활성 staging 중 호출됨
- 초기화 문맥은 layout, 계획 content, wrapper identity를 제공. 제품 AI·픽업·기믹을 연결하는 데 사용
- 카탈로그가 없는 적·기믹·아이템은 비차단 trigger 표식이며 자체 게임 동작이 없음
- 카탈로그 프리팹의 Collider/AI가 실제 이동에 주는 영향은 제품별 검증 대상. 정적 생성기 연결성 검사는 엄폐를 차단 셀로 취급
- 사용자 프리팹의 재질은 prototype 색상 복원에서 제외되며 원래 작성 재질을 유지

## V1 보존과 명시적 업그레이드

기존 V1 레시피의 생성기 버전은 그대로 유지됩니다. 기본 `new FpsArenaRecipe()`도 기존 호출과 저장 설정을 위해 V1입니다. 새 생성기/설정 자산과 새 프리셋은 `CreateFlexible()`을 사용합니다.

V1 설정을 열면 **V1 기존 결과 보존** 안내가 표시됩니다. V1은 고정 계단 위치, 셀 폭 70%의 충돌 상자 엄폐/장애물, 층별 최대 목표 0–100개와 기존 아이템 trigger 표식을 유지합니다. 적 탭에서도 기존 필드는 **V1 장애물**로 표시하며 적 AI 수로 이름만 바꾸지 않습니다. 기믹·프리팹과 V2 프리셋은 업그레이드 전 사용할 수 없습니다.

1. 이전 결과를 유지해야 하면 업그레이드하지 않고 V1로 재생성합니다.
2. 범용 기능을 사용하려면 `V2 범용 생성으로 업그레이드 (Undo 가능)`를 누릅니다.
3. 업그레이드는 레시피만 바꾸고 시드·연결 자산과 현재 구축 장면은 유지합니다. 기존 엄폐/장애물/아이템 목표에서 V2 초기 밀도를 추정하므로 밀도 값을 다시 검수합니다. 이전 장애물 목표는 초기 적 밀도 계산에만 사용됩니다.
4. `시드로 생성 / 재생성`을 누르면 새 계단과 콘텐츠가 만들어집니다. 기존 V1 배치 해시를 재현하는 작업은 아닙니다.
5. 업그레이드와 재생성은 별도 Undo 단계로 되돌릴 수 있습니다.

## 런타임 API와 저장

```csharp
var recipe = FpsArenaRecipe.CreateFlexible();
recipe.coverDensity = .035f;
recipe.enemyDensity = .02f;
recipe.gimmickDensity = .01f;
recipe.itemDensity = .015f;
FpsArenaLayout layout = FpsArenaPlanner.Generate(recipe, seed, catalog);
GameObject staging = FpsArenaSceneBuilder.Build(layout, parent, catalog);
// staging은 비활성 상태입니다. 호출자가 성공한 후보를 교체/활성화합니다.
```

`FpsArenaPlanner.Generate`는 GameObject 없는 논리 결과를 만들고 카탈로그 planning snapshot을 보관합니다. `FpsArenaSceneBuilder.Build(layout, parent, catalog = null)`는 계획된 키를 실제 카탈로그 Prefab으로 해석합니다. 실제 프리팹 키가 있는 layout에는 맞는 카탈로그를 넘겨야 합니다. 기존 두 인수 Build 호출은 카탈로그 없는 layout에 그대로 사용할 수 있습니다.

`FpsArenaGenerator.Plan()`과 `Generate()`는 `ActiveCatalog`를 사용합니다. `ActiveCatalog`는 공유 설정이 있으면 그 설정의 카탈로그, 없으면 생성기 자체 카탈로그입니다. 제작 창의 `GenerateWithUndo`도 같은 카탈로그를 전달합니다.

`FpsArenaGenerator.CurrentLayout`, `GeneratedRoot`, `Generated` 이벤트를 통해 게임에 연결합니다. 셀 좌표는 `(x, 층 인덱스, z)`이고 `layout.Position(cell)`은 stage-local 위치를 반환합니다. 여러 셀 콘텐츠의 footprint 중심은 `layout.ContentPosition(content)`로 얻습니다. 월드 변환은 생성기 Transform으로 수행합니다. FPS 테스트 캐릭터는 회전 없는 균일 scale=1 생성기를 전제로 합니다.

생성은 성공한 후보만 `__FpsArena_Generated` root와 교체하며 다른 이름의 자식은 보존합니다. 같은 정규화 설정·카탈로그 planning 입력·시드는 같은 계단·배치·ID·해시를 만듭니다. Unity 전역 Random에 의존하지 않습니다.

장면에는 마지막 생성 레시피·시드·카탈로그 planning snapshot과 생성 geometry가 저장됩니다. `Play 시 생성`을 꺼도 저장 기록에서 논리 메타데이터를 복원합니다. 현재 입력 설정이나 카탈로그가 바뀌어도 재생성 전에는 기존 geometry의 기록을 사용합니다. 기본 prototype 재질은 저장한 색상에서 scene/domain reload 후 복원하고 사용자 프리팹 재질은 보존합니다. 배포 조명/재질 품질 검수는 별도 단계입니다.

## 기존 로그라이크와 배포 경계

기존 로그라이크의 LegacyV1/StableV2, `DungeonBlueprint` v1, StageDefinition, Override, Bake, RunState 및 `__RogueDungeonLab_Generated` 계약은 변경하지 않습니다. FPS 아레나의 V1/V2 버전 구분은 별도의 생성기 계약입니다.

아레나 저장은 설정/카탈로그 자산 + Unity 장면입니다. 기존 단층 Blueprint/Bake/RunState UI에 다층 아레나를 직접 넣는 기능은 포함하지 않습니다. 제품의 플레이 진행 저장은 별도로 연결해야 합니다.

UPM Core에는 아레나 런타임, Lab에는 FPS 테스트 캐릭터와 Editor 전용 제작 창·카탈로그 Inspector를 원본 bytes로 동기화합니다. Lab의 아레나 편집기는 Runtime/Core와 Samples만 참조하는 Editor 전용 assembly에 들어갑니다. Core만 사용하는 제품에는 테스트 캐릭터와 제작 창이 따라오지 않습니다. 배포 사본은 공식 UPM 동기화기로 재생성하며 사용자 프리팹 자산은 해당 제품 프로젝트에서 관리합니다.

## V2 검증과 인계

최초 V2 클라우드 번들은 구현·정적 점검 결과만 포함했습니다. 이후 별도 Windows 사본에서 실행한 실제 V2 검증 결과를 문서 끝에 기록했습니다. 아래 이전 V1 기록과 구분합니다.

새 EditMode 회귀:

- `FpsArenaFlexibleTests`: V2 결정성, 시드 가변 계단, 폭/깊이/높이·형태 범위, 독립 밀도/제로 밀도, footprint·연결성, 카탈로그 planning/실제 prefab 및 실패 보존 등 런타임 계약
- `FpsArenaEditorFlexibleTests`: 명시적 V1 업그레이드/Undo/기존 구축 결과 유지, 공유 설정 원본 선택, root-local Bounds 크기/중심, 공유 설정의 실제 카탈로그를 사용한 제작 창 생성 경로
- 기존 `FpsArenaTests`와 `FpsArenaTraversalTests`: V1 및 기존 다층/계단/저장/Undo/실제 캐릭터 충돌 회귀

검증 사본에서 Unity 6000.5.3f1로 확인합니다.

1. Compile → 새 FPS EditMode → 기존 FPS EditMode/PlayMode → R7 fixture 자동 구성 → 전체 EditMode/PlayMode
2. 창/생성기/설정 자산의 여섯 탭을 실제로 조작. 네 범주 밀도 0%, 다른 시드의 계단, 엄폐 극단 크기와 여러 형태, 실제/목표 표시 확인
3. 실제 엄폐·적·기믹·아이템 Prefab을 연결하고 Bounds, 충돌, 프리팹 컴포넌트 초기화와 작성 재질 유지 확인
4. 카탈로그 항목 추가/수정/삭제/연결 변경과 V1 업그레이드 Undo/Redo, 설정·카탈로그 저장/재임포트
5. 장면 저장/완전 재시작, generateOnPlay on/off, Play script/domain reload, 두 번 생성 후 root 단일 유지
6. 극단 셀 크기/층 높이의 계단 상승·하강·머리 여유, 스폰/통로, 실제 프리팹 Collider, 마우스 잠금/Esc, 제품 픽업/AI 동작
7. 기존 클릭 1회/드랍 1회와 로그라이크 golden 결과 유지

정적 스크립트는 C# 컴파일이나 Unity Physics/UI 검증을 대체하지 않습니다. V1 형상 sweep은 V2 새 위치 선택 알고리즘의 실행 검증과도 구분합니다.

## 이전 V1 로컬 검증 사본 기록 (2026-09-30)

아래는 V2 확장 전 V1 구현을 원본과 분리한 사본에서 Unity 6000.5.3f1로 검증한 기록입니다. V2의 새 계단·밀도·프리팹·탭 UI가 통과했다는 뜻이 아닙니다.

- 컴파일 오류 0개. 신규 EditMode 9/9, 신규 PlayMode 1/1 통과.
- R7ManualVerificationSetup.CreateAllFromBatch로 R6/R7 fixture를 재생성한 후 전체 EditMode 110/110, 전체 PlayMode 12/12 통과(실패/건너뜀 0개).
- CharacterController가 셀 크기/층 높이 (2m/6m), (5m/3m)의 두 프로필에서 두 계단의 양쪽 lane을 각각 오르고 내려왔습니다.
- 실제 제작 창의 장면 생성/재생성 경로를 호출하고 Undo/Redo 및 생성 root 단일 유지, 설정 자산 저장을 확인했습니다.
- 장면 저장 후 Unity를 완전히 종료하고 새 프로세스에서 다시 열어 저장 배치 hash, 설정 자산 참조, 404개 표면 색상과 재질 복원을 확인했습니다. 현재 입력 설정을 변경해도 재생성 전에는 저장된 배치를 유지했습니다.
- generateOnPlay=false 상태에서 Play 중 실제 script/domain reload 후 같은 배치 hash와 root를 유지하고 Edit 모드로 복귀했습니다. 런타임 오류 0개.
- GPU 렌더로 전투장 전체, 계단과 플레이어 시점 PNG를 남겼습니다. 초기 비동기 셰이더 컴파일 중 색상 이상은 검증 렌더에서 동기 컴파일 후 재확인했습니다.

초기 저장/복원 EditMode 테스트는 batch TestRunner의 저장되지 않은 장면에 Additive 장면을 만드는 문제로 실패했습니다. 테스트 준비/정리만 Single 장면 방식으로 수정한 뒤 위 전체 테스트를 통과했습니다. 제품 Runtime/Editor 구현은 클라우드 번들과 같습니다.

제작 창 버튼의 실제 동작 메서드는 자동 검증했지만, 직접 마우스/키보드로 설정 UI를 조작하거나 커서 잠금/Esc/키 입력 감각을 확인하지는 않았습니다. Windows standalone Player 빌드와 배포 조명/재질 품질 검수도 미실행입니다. 검증 번들에는 실행 XML/JSON과 실제 렌더 PNG가 포함됩니다.


## V2 로컬 Unity 검증 결과 (2026-09-30)

Unity 6000.5.3f1의 별도 사본에서 다음을 실제 실행했습니다.

- 컴파일 오류 0개. FPS V1/V2·Editor·UPM 집중 EditMode 35/35, 계단 PlayMode 2/2 통과.
- R6/R7 fixture 재생성 후 전체 EditMode 132/132, 전체 PlayMode 13/13 통과. 실패/건너뜀 0개. V1 대비 새 EditMode 22개와 PlayMode 1개를 포함합니다.
- V1 두 인수 공개 메서드의 delegate/reflection 호환과 원래 golden hash, V2 동일 시드 결정성·시드별 계단 변화, 0/최대 밀도, 전체 엄폐 footprint, 실제 프리팹 생성·초기화·균일 fitting·재질·Collider 보존 통과.
- V2 4층의 각 계단·양쪽 lane을 두 시드에서 실제 CharacterController로 상승/하강했습니다. V1 극단 프로필 검사도 통과했습니다.
- 실제 창의 여섯 탭 OnGUI를 배치 Unity GUI 이벤트로 모두 그렸습니다. 오류와 의도하지 않은 설정 변경이 없었습니다. 탭별 값 변경의 Undo/Redo, 설정/카탈로그/장면 저장을 확인했습니다.
- Unity 완전 종료 후 새 프로세스에서 697개 렌더러, 39개 실제 프리팹의 재질·Collider·초기화 상태와 저장된 배치 hash를 복원했습니다. 입력 설정/카탈로그를 바꿔도 재생성 전에는 저장된 built snapshot이 유지됐습니다.
- generateOnPlay=false의 Play script/domain reload 및 Edit 복귀 통과. 배치 hash·동일 root·사용자 재질이 보존됐고 런타임 오류 0개입니다.
- 깨끗한 Core-only, Core+Lab 0.14.0 소비 프로젝트의 로컬 UPM 설치·컴파일·V1 API/golden·V2 생성 통과. Core-only에 Input/Lab/아레나 Editor가 없고, Lab의 독립 6탭 제작 창과 테스트 장면이 작동했습니다.
- GPU 렌더 프리뷰와 실행 XML/JSON/로그를 별도 검증 폴더에 보존했습니다. 보라색 capsule은 검증용 실제 Prefab 자산입니다.

직접 마우스/키보드로 조작감을 확인하거나 standalone Player를 빌드하지는 않았습니다. 실제 게임 AI·NavMesh·픽업·기믹 동작과 배포 성능/조명 품질은 제품별 검증 범위입니다.

## 내부 벽으로 공간 나누기 (2026-10-01)

1. `Tools > Rogue Dungeon Lab > FPS 아레나 제작`의 `지형·계단` 탭에서 V2의 `내부 벽 생성`을 켭니다. 저장된 V1은 기존 결과를 보존하며 명시적인 V2 업그레이드 후 사용할 수 있습니다.
2. `벽 점유 목표 (%)`는 계단·스폰·예약 통로를 뺀 바닥 셀 중 solid 벽이 점유할 목표 비율(0–35%)입니다. 안전 공간과 층별 상한 때문에 실제 값은 낮아질 수 있고, 마지막 벽 한 개를 온전히 배치하므로 목표를 소폭 넘을 수도 있습니다.
3. 길이는 3–16셀 범위이며 X/Z 방향을 시드로 고릅니다. 높이는 2m 이상, 층 간 높이 − 0.25m 이하로 정규화합니다. 두께는 0.15–1m, 출입구 폭은 1–3셀, 층별 최대 벽 수는 1–40개입니다. 최소 셀 크기 2m이므로 가장 좁은 출입구도 2m입니다.
4. 출입구와 양쪽 접근 공간, 벽 끝의 1셀 우회 띠를 보장합니다. 길이가 출입구와 양쪽 벽 구간을 담기에는 짧으면 출입구 없이 끝 우회로를 사용합니다. 출입구에는 문이나 문틀 Collider가 생성되지 않습니다.
5. `엄폐` 탭의 블록 엄폐, `적`·`특수 기믹`·`아이템` 및 실제 프리팹과 함께 사용합니다. 벽 점유 셀과 출입구를 포함한 보호 공간을 피해서 콘텐츠를 배치합니다.
6. `시드로 생성 / 재생성`을 실행합니다. 지형 탭의 결과에서 전체 벽 수와 층별 실제 점유/목표를 확인하세요. 설정 변경은 재생성 후 반영됩니다. `현재 설정을 새 자산으로 저장`과 Unity 장면 저장으로 벽 설정과 마지막 생성 결과를 보존합니다.

바로 확인하려면 `Tools > Rogue Dungeon Lab > 내부 벽 FPS 예제 장면 만들기` 또는 제작 창의 같은 버튼을 사용하세요. 새 장면을 만들기 전에 현재 장면 저장 여부를 확인하는 기존 흐름을 사용합니다. 로컬에 적용된 `Assets/FpsArenaWallsExample/InternalWalls.unity`도 열고 Play할 수 있습니다. WASD 이동, Shift 달리기, Space 점프, R 스폰 복귀, 클릭으로 시점 잠금, Esc로 해제합니다.

벽 OFF는 기존 V2와 같은 배치·콘텐츠 ID·해시입니다. 비활성 상태에서 벽 치수 값을 변경해도 결과는 변하지 않습니다. 벽 ON은 전용 시드 난수 흐름으로 결정되고 콘텐츠 밀도를 바꿔도 벽/계단 배치는 유지됩니다. `layout.Walls`, `layout.RequestedWallCells(floor)`, `wall.Footprint()`, `wall.SolidSpans()`로 구조를 읽을 수 있습니다.

```csharp
var recipe = FpsArenaRecipe.CreateFlexible();
recipe.internalWalls = true;
recipe.wallDensity = .12f;
recipe.wallMinLengthCells = 5;
recipe.wallMaxLengthCells = 10;
recipe.wallHeight = 3;
recipe.wallThickness = .35f;
recipe.wallDoorWidthCells = 2;
recipe.maxWallRunsPerFloor = 20;
var layout = FpsArenaPlanner.Generate(recipe, 73125, catalog);
```

벽은 직선 파티션과 열린 출입구입니다. 외곽에 붙는 폐쇄 방, 교차 벽, 닫히는 문, 벽 프리팹, NavMesh 자동 Bake는 제공하지 않습니다. 카탈로그 프리팹은 실제 Renderer/Collider를 포함하는 정확한 authored Bounds를 설정해야 합니다. 생성기 변환은 기존 FPS 테스트 캐릭터의 scale=1 전제를 유지합니다. 세부 증거·한계·롤백은 `docs/FPS_ARENA_WALLS_VERIFICATION_KO.md`를 참고하세요.

## 방 개수로 스테이지 구획하기 (현재 주 기능, 2026-10-01)

`Tools > Rogue Dungeon Lab > FPS 아레나 제작 > 지형·계단`에서 `방 구획 사용`을 켜고 `층별 방 개수`(1–12)를 지정합니다. 모든 층에 같은 요청 개수를 적용하며 각 층의 계단·외곽에 맞춰 실제 구획을 만듭니다. 방별 개별 override는 현재 제공하지 않습니다.

- 최소 방 폭은 4–12셀, 최소 실제 바닥 면적은 16–256셀입니다. 타원/팔각형 외곽과 계단 개구부를 뺀 실제 footprint의 가로·세로 span과 면적을 검사합니다.
- 출입구 폭은 1–3셀(최소 2m), 구획벽 높이는 2m 이상이며 층 간 높이 − 0.25m 이하, 두께는 0.15–1m입니다.
- 구획벽은 외곽 또는 기존 구획벽까지 이어집니다. 방 끝의 우회 틈은 만들지 않습니다. 방 사이에는 열린 출입구가 있으며 모든 방과 계단으로 이동할 수 있습니다.
- 방 생성 결과에서 층별 실제/요청 개수·방별 면적·출입구 연결을 확인합니다. 계단·스폰·최소 방 크기·기존 문 보호 때문에 추가 절단선을 찾지 못하면 실제 개수와 이유를 표시합니다. 안전 조건을 무시해 개수만 채우지 않습니다.
- 적·아이템·엄폐·기믹과 프리팹은 한 방 안에 배치하며 벽 양쪽 공간, 출입구와 방별 이동 경로를 침범하지 않습니다. 안전 공간이 많은 작은 방에서는 콘텐츠 실제 수가 목표보다 낮을 수 있습니다.
- 생성기를 Scene에서 선택하면 방 이름·면적과 문을 통한 연결선이 표시됩니다. 방 개수는 문을 모두 막았을 때의 바닥 연결 영역 수이며, 문을 열면 하나의 연결된 이동 그래프가 됩니다.

바로 시작하려면 `Tools > Rogue Dungeon Lab > 방 구획 FPS 예제 장면 만들기` 또는 `Assets/FpsArenaRoomsExample/RoomPartitions.unity`를 사용하세요. 공유 설정 자산 저장, 장면 저장, Undo 및 같은 시드 재생성을 지원합니다. 현재 입력 변경과 마지막 저장 생성 결과는 분리됩니다.

```csharp
var recipe = FpsArenaRecipe.CreateFlexible();
recipe.partitionRooms = true;
recipe.roomsPerFloor = 4;
recipe.roomMinWidthCells = 4;
recipe.roomMinAreaCells = 32;
recipe.roomDoorWidthCells = 2;
var layout = FpsArenaPlanner.Generate(recipe, 73125, catalog);
foreach (var report in layout.RoomReports)
    Debug.Log($"{report.floor + 1}층 {report.actual}/{report.requested}: {report.reason}");
// layout.Rooms / RoomWalls / RoomDoors / RoomConnections
// layout.RoomAt(cell), CountClosedRooms(floor), CanTraverse(from, to)
```

기존 벽 조각 설정은 삭제하지 않고 `이전 벽 조각 설정 (호환)`에 보존했습니다. 방 구획이 켜져 있으면 이전 벽 조각 값은 사용하지 않습니다. 방 구획이 OFF인 저장 설정은 이전 벽 조각 또는 열린 아레나의 배치와 해시를 유지합니다. LegacyV1은 명시적인 V2 업그레이드가 필요합니다. 닫히는 문 오브젝트, NavMesh 자동 Bake 및 제품 AI/픽업 동작은 별도 구현 대상입니다. 검증·화면·롤백은 `docs/FPS_ARENA_ROOMS_VERIFICATION_KO.md`를 참고하세요.

## 시드별 계단 상승 방향

`Tools > Rogue Dungeon Lab > FPS 아레나 제작 > 지형·계단`에서 `시드별 계단 방향 사용`을 확인하고 재생성합니다. 새 V2 preset/recipe에는 기본으로 켜집니다. 계단 위치뿐 아니라 +Z/+X/−Z/−X 중 유효한 상승 방향을 시드로 선택합니다. 같은 입력과 시드는 같은 방향과 배치입니다. 발판·난간·개구부·착지 여유·방 연결 예약 공간도 함께 회전합니다.

마지막 생성 결과에 각 층간 계단의 방향/시작/도착, 유효 방향과 후보 수를 표시합니다. 한 층에 계단 1–2개이므로 한 번의 생성에서 네 방향을 모두 강제하지 않습니다. 외곽·양층 바닥·스폰·다른 계단 보호로 후보 방향이 제한되면 이유를 표시합니다. 방 수가 줄어드는 경우 기존 방 report를 확인합니다.

이전 저장 장면의 마지막 geometry는 재생성 전까지 기존 built snapshot으로 복원합니다. Unity 편집 입력에 새 기본값이 나타날 수 있으며, 방향 옵션 OFF는 정확한 이전 시드 배치/hash를 재생성합니다. 기존 방/벽/엄폐 설정 값은 변경하지 않습니다. 코드에서는 `stair.Forward`, `LaneStep`, `BottomLane(lane)`, `TopLane(lane)`을 사용하세요. 고정 `Vector3.forward` 또는 `Bottom + Vector3Int.right * lane`은 회전 계단에 맞지 않습니다. 실제 검증과 한계는 `docs/FPS_ARENA_STAIR_DIRECTIONS_VERIFICATION_KO.md`에 기록합니다.
