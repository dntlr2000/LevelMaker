# Rogue Dungeon Lab - Lab Sample

실험용 인게임 HUD, 자유 카메라, 클릭 상호작용과 임시 플레이어를 제공하는 선택 package입니다. Runtime Core와 Input System을 요구합니다. 0.14.0에는 Editor-only FPS 아레나 제작 창(지형·엄폐·적·특수 기믹·아이템·프리팹 6개 탭)도 포함됩니다. `Tools > Rogue Dungeon Lab > FPS 아레나 제작`에서 시작합니다.

0.15.0: `Tools > Rogue Dungeon Lab > 스테이지 직접 설계`에서 빈 판을 만들거나 생성 결과를 복사합니다. 층별 격자에서 바닥·벽·문·계단·시작 스폰·구조물·적·기믹·아이템을 편집하고 저장합니다. 선택 도구로 콘텐츠를 드래그하고 Ctrl+Z/Redo로 되돌립니다. 검사 후 장면에 적용하고 테스트 플레이어를 연결한 뒤 Play로 확인하세요. 초안은 연결 오류가 있어도 저장되지만 장면 적용은 검사를 통과해야 합니다.

다른 프로젝트의 프리팹은 `프로젝트 프리팹 등록·교체`에서 같은 콘텐츠 키로 등록하세요. 카탈로그를 복제하여 Generator의 `호스트 카탈로그 교체`에 연결하면 스테이지 자산을 수정하지 않고 교체됩니다. `IFpsArenaContentInitializer`는 기존 게임 시스템 연결용 hook입니다.

제품 장면에서 Lab 컴포넌트를 배치하지 않으면 빌더 HUD는 표시되지 않습니다. Core-only 제품에는 이 package를 설치하지 않아도 됩니다.
