using UnityEngine;

namespace RogueDungeonLab
{
    public sealed class DungeonStageInstance
    {
        public DungeonStageDefinition Definition { get; private set; }
        public DungeonStageSourceMode SourceMode { get; private set; }
        public DungeonStageBuildMode BuildMode { get; private set; }
        public DungeonBlueprint Blueprint { get; private set; }
        public DungeonLayout Layout { get; private set; }
        public GameObject Root { get; private set; }
        public RogueDungeonSettings RuntimeSettings { get; private set; }
        public DungeonSceneBuildResult BuildResult { get; private set; }
        public DungeonValidationReport ValidationReport { get; private set; }
        public GenerationReport Report { get; private set; }
        public string RequestId { get; private set; }
        public DungeonStageOverrides AppliedOverrides { get; private set; }
        public string SourceBlueprintHash { get; private set; }
        public string OverrideHash { get; private set; }
        public string FinalBlueprintHash { get; private set; }
        public int ActiveSeed { get { return Blueprint != null ? Blueprint.seed : 0; } }

        // 로드 출처와 구축 결과를 이후 플레이·상태 시스템이 참조할 하나의 인스턴스로 묶습니다.
        internal DungeonStageInstance(
            DungeonStageDefinition definition,
            DungeonStageSourceMode sourceMode,
            DungeonStageBuildMode buildMode,
            DungeonBlueprint blueprint,
            DungeonLayout layout,
            GameObject root,
            RogueDungeonSettings runtimeSettings,
            DungeonSceneBuildResult buildResult,
            DungeonValidationReport validationReport,
            GenerationReport report,
            string requestId,
            DungeonStageOverrides appliedOverrides = null,
            string sourceBlueprintHash = "",
            string overrideHash = "")
        {
            Definition = definition;
            SourceMode = sourceMode;
            BuildMode = buildMode;
            Blueprint = blueprint;
            Layout = layout;
            Root = root;
            RuntimeSettings = runtimeSettings;
            BuildResult = buildResult;
            ValidationReport = validationReport;
            Report = report;
            RequestId = requestId ?? string.Empty;
            AppliedOverrides = appliedOverrides;
            SourceBlueprintHash = !string.IsNullOrEmpty(sourceBlueprintHash)
                ? sourceBlueprintHash
                : blueprint != null
                    ? blueprint.blueprintHash ?? string.Empty
                    : string.Empty;
            OverrideHash = overrideHash ?? string.Empty;
            FinalBlueprintHash = blueprint != null
                ? blueprint.blueprintHash ?? string.Empty
                : string.Empty;
        }
    }
}
