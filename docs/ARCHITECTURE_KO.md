# 아키텍처

## 파이프라인

```text
RogueDungeonSettings
  → DungeonLayoutGenerator
  → DungeonLayout(bool grid, rooms, BFS distance)
  → DungeonMeshBuilder + DungeonContentSpawner
  → GenerationReport

좌클릭 Raycast
  → DestructibleDropTarget
  → WeightedDropTable.Roll
  → DropValidationService
  → Editor Window + Runtime HUD
```

레이아웃 단계는 GameObject를 만들지 않습니다. 방은 겹침 없는 사각형으로 배치하고 가장 가까운 미연결 방을 L자 복도로 연결한 뒤 확률적으로 루프를 추가합니다.

진행도는 `distanceFromEntrance / distanceToExit`이며, 각 밀도 프로필은 기본 셀 확률 × 진행도 곡선 × 방/복도 보정 × 결정적 Perlin 군집 보정으로 평가됩니다.

바닥은 셀당 quad, 벽은 floor-to-void 경계 box를 각각 하나의 합성 메시로 만듭니다. 클릭 대상만 개별 GameObject입니다.

드랍 대시보드는 기대 확률, 관측 확률, 편차와 Wilson 95% 신뢰구간을 계산합니다. 테이블 정의가 바뀌면 이전 표본을 새 기대값과 비교하지 않도록 해당 통계를 초기화합니다.

## Unity 6000.5 직렬화

역할별 Runtime 파일의 공개 API와 로직은 유지하되, `MonoBehaviour`와 `ScriptableObject`마다 타입명과 같은 `partial` 연결 파일을 둡니다. Unity가 안정적인 `MonoScript` 자산을 생성하므로 장면 저장, 설정 에셋, Play/Edit 전환과 도메인 재로드 뒤에도 참조가 유지됩니다.

드랍 정의는 항목 정규화 후 해시를 계산합니다. 내부 정규화를 사용자 편집으로 오인해 첫 통계 표본을 초기화하지 않습니다.
