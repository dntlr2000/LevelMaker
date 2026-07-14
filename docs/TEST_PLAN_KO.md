# 테스트 계획

- 신규 Unity 6 프로젝트 import 후 컴파일 오류 0개
- 장면 자동 구성 3회 후 Generator/Systems/Camera/Light 중복 없음
- 자동 생성 설정·드랍 테이블을 Unity 재시작 후 다시 로드 가능
- 동일 설정·시드의 방 수, 셀 수, 콘텐츠 위치 동일
- 모든 floor cell의 BFS distance가 0 이상
- 최소 12×12 및 최대 96×96 극단 설정에서 예외 없음
- 곡선 0 구간에서 해당 콘텐츠 억제
- maxCount와 contentSpacingCells 준수
- Play 좌클릭 1회가 attempts +1
- 빠른 표본 1,000회가 attempts에 정확히 1,000회 반영
- HUD 버튼 클릭이 뒤의 대상 파괴를 유발하지 않음
- Nothing 100% 테이블에서 마커 없음
- 모든 가중치 0에서 예외 없는 invalid no-drop
- 10,000회 표본에서 관측치가 기대치 근처로 수렴
- 반복 재생성 후 동적 Mesh 누수 없음
