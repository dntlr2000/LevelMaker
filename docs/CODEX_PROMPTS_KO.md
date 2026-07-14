# Codex 프롬프트

## 최초 점검

```text
/plan
@AGENTS.md, @README.md, @docs/ARCHITECTURE_KO.md와 Assets/RogueDungeonLab을 읽어라.
Unity 6000.5에서 발생할 수 있는 컴파일/API/lifecycle 문제를 점검하고 실제 문제만 최소 패치해라.
외부 패키지 금지, Runtime의 UnityEditor 참조 금지, 공개 API와 결정성 유지.
가능하면 batchmode 컴파일, 장면 자동 구성, 동일 시드, Play 클릭, +1000 드랍 표본을 검증해라.
마지막에 변경 파일, 원인, 실행한 검증, 실행 못한 검증을 보고해라.
```

## 프리팹 카탈로그

```text
/plan
DungeonContentSpawner에 선택적 SpawnCatalog ScriptableObject를 추가해라.
entry는 category, prefab, weight, progression 범위, room/corridor 조건, footprint를 가진다.
카탈로그가 없으면 기존 primitive fallback을 유지하고 동일 시드 결정을 보장해라.
에디터에서 카탈로그와 유효 항목 수를 표시하고 문서를 갱신해라.
```

## 방 그래프 규칙

```text
/plan
RoomMetadata와 room graph를 추가해 Entrance, Combat, Shop, Boss, Secret, Key, Lock을 배정해라.
Boss는 입구에서 그래프 거리 4 이상, Shop은 Boss 이전, Key는 Lock 이전 도달 집합에 있어야 한다.
100개 시드 검증과 fallback warning을 추가하고 기존 grid 연결성을 유지해라.
```

## 드랍 확장

```text
/plan
legacy weighted choice를 호환 유지하면서 guaranteed, independent Bernoulli, weighted groups, progression quantity curve, luck를 추가해라.
각 규칙의 이론 등장 확률과 평균 수량을 계산하고 HUD에서 분리 표시해라.
legacy 회귀, 0/100% 경계, 수량 곡선 테스트를 추가해라.
```

## 리뷰

```text
/review
결정성 파괴, Runtime/Editor 경계, Edit/Play/domain reload lifecycle, 동적 Mesh 누수, 확률/Wilson 계산, Undo/dirty, 큰 맵 성능을 우선 검토해라.
스타일 취향은 제외하고 재현 가능한 finding만 파일/위치/영향/최소 수정안과 함께 보고해라.
```
