# 제품화 확장 가이드

1. `DungeonContentSpawner`의 primitive 생성부를 `SpawnCatalog` 프리팹 팩토리로 교체합니다.
2. 방 메타데이터와 그래프를 추가해 Boss/Shop/Key/Lock/Secret 제약을 검증합니다.
3. 층별 grid와 층 간 edge로 다층 던전을 구현합니다.
4. 논리 연결성과 별도로 NavMesh 도달성을 검사합니다.
5. 큰 맵은 chunk mesh, greedy wall meshing, 오브젝트 풀, Jobs/Burst 순으로 계측 후 최적화합니다.
6. 드랍은 guaranteed, independent Bernoulli, weighted group, luck, pity 규칙으로 확장합니다.
7. GenerationReport와 통계를 CSV/JSON으로 내보내 회귀 비교에 사용합니다.
8. 실험 결과를 실제 씬에 고정할 때는 별도 Bake 명령으로 Mesh asset과 metadata를 저장합니다.
