# R4 실행 계획 — 콘텐츠 카탈로그와 Resolver

## 목표

`LegacyV1`의 결과와 기존 settings-only facade를 유지하면서, `StableV2` 절차 생성이 Inspector 목록 순서와 범주 간 난수 소비에 영향받지 않는 콘텐츠 key를 선택하도록 확장한다. `DungeonStageDefinition`은 선택적 콘텐츠 카탈로그와 누락 콘텐츠 정책을 제공하고, 런타임 SceneBuilder는 기본 Prefab resolver 또는 기존 primitive fallback으로 같은 Blueprint를 구축한다.

## 착수 기준

- R0~R3의 Blueprint, SceneBuilder, StageDefinition/Loader 경로가 구현되어 있다.
- 기존 Blueprint는 built-in `contentKey`만 기록하며 SceneBuilder가 직접 primitive를 만든다.
- 생성기 버전은 `LegacyV1`만 허용하며 콘텐츠 범주들이 일부 난수 호출 순서를 공유한다.
- Unity `6000.5.3f1` R3 기준선은 EditMode `19/19`, PlayMode `2/2`이다.

## 마일스톤

### M1 — 카탈로그 데이터와 검증

- `DungeonContentCatalog` 및 직렬화 가능한 entry 계약 추가
- contentKey canonical 정렬과 planning hash 구현
- 중복 key, 잘못된 weight/progression/footprint/간격 검증
- Blueprint key와 카탈로그의 누락 관계를 정책별로 검사

### M2 — StableV2 계획

- 프로젝트 소유 고정 PRNG와 seed 파생 공식 추가
- Layout/Gimmick/Enemy/Destructible/Prop/Variant 독립 스트림 적용
- progression, room/corridor, room tag 조건과 weight로 후보 key 선택
- Inspector entry 순서가 바뀌어도 같은 Blueprint hash를 생성
- `LegacyV1` 경로와 golden 지문 유지

### M3 — Resolver와 런타임 구축

- `IDungeonContentResolver`와 기본 catalog Prefab resolver 추가
- Prefab 인스턴스에 Blueprint transform과 `DungeonSpawnIdentity` 적용
- Error/PrimitiveFallback/Skip 누락 정책 구현
- 카탈로그가 없거나 built-in key를 사용할 때 현재 primitive 표현 유지
- StageDefinition/Loader가 카탈로그 hash, resolver와 누락 정책을 전달

### M4 — 테스트와 문서

- 카탈로그 hash 순서 독립성, 검증 코드, 누락 key 정책 테스트
- StableV2 반복성·범주 독립성·LegacyV1 회귀 테스트
- Prefab resolver 및 StageLoader 통합 테스트
- README, 아키텍처, 로드맵, 테스트 계획, 확장 가이드, 변경 로그 갱신

## 검증 증거

- Unity `6000.5.3f1` batchmode 컴파일
- EditMode 전체 테스트
- PlayMode 전체 테스트
- one-click setup, 같은 시드 재생성, Play 클릭/drop/statistics는 기존 자동 테스트와 수동 점검 범위를 구분해 보고

## 완료 결과

- R4 카탈로그·StableV2·Resolver·누락 정책과 StageLoader 연결 완료
- 통합 리뷰의 snapshot 무결성, 예약 key/category, cell-room 일치, Prefab 드랍 우선순위, staging root, Mesh 소유권 항목 보강
- Unity `6000.5.3f1` batchmode compile 성공
- EditMode `41/41`, PlayMode `3/3` 통과
- one-click setup과 직접 파괴·드랍 통계는 자동 검증, 실제 화면 포인터 Raycast는 수동 검증 범위로 기록
