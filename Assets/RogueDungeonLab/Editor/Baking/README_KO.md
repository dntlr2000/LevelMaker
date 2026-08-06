# Rogue Dungeon Lab Bake Authoring

`DungeonStageBaker`와 R9 배포 도구는 Editor 전용 제작 패키지입니다. Runtime Core를
먼저 설치한 프로젝트에서 SavedBlueprint StageDefinition을 BakedPrefab으로 만들고
최신성을 검사할 때만 필요합니다. 완성된 게임 Player에는 이 폴더가 포함되지
않습니다.

원본 LevelMaker 저장소에서는 통합 실험실의 `스테이지 자산` 탭이 이 API를
호출합니다. 이 독립 제작 패키지만 가져온 프로젝트에서는 custom Editor 도구나
batch script에서 `DungeonStageBaker.Bake`, `ValidateCurrentBake`와
`DungeonDistributionExporter.PlanBakedStage`/`Export`를 호출합니다.
