using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonBakedStageMetadataValidationCodes
    {
        public const string MissingMetadata = "RDL-BAKED-001";
        public const string InvalidFormatVersion = "RDL-BAKED-002";
        public const string InvalidBuilderVersion = "RDL-BAKED-003";
        public const string FinalBlueprintHashMismatch = "RDL-BAKED-004";
        public const string InvalidBuildCounts = "RDL-BAKED-005";
        public const string InvalidBuildIssue = "RDL-BAKED-006";
    }

    [DisallowMultipleComponent]
    public sealed class DungeonBakedStageMetadata : MonoBehaviour
    {
        [SerializeField] private int formatVersion = DungeonBakeFormat.Current;
        [SerializeField] private int builderVersion = DungeonBakeBuilderVersions.Current;
        [SerializeField] private string finalBlueprintHash = string.Empty;
        [SerializeField] private int meshTriangleCount;
        [SerializeField] private int enemyCount;
        [SerializeField] private int destructibleCount;
        [SerializeField] private int propCount;
        [SerializeField] private int gimmickCount;
        [SerializeField] private int resolvedContentCount;
        [SerializeField] private int builtInFallbackCount;
        [SerializeField] private int skippedContentCount;
        [SerializeField] private List<DungeonValidationIssue> buildIssues =
            new List<DungeonValidationIssue>();

        public int FormatVersion { get { return formatVersion; } }
        public int BuilderVersion { get { return builderVersion; } }
        public string FinalBlueprintHash { get { return finalBlueprintHash; } }
        public int MeshTriangleCount { get { return meshTriangleCount; } }
        public int EnemyCount { get { return enemyCount; } }
        public int DestructibleCount { get { return destructibleCount; } }
        public int PropCount { get { return propCount; } }
        public int GimmickCount { get { return gimmickCount; } }
        public int ResolvedContentCount { get { return resolvedContentCount; } }
        public int BuiltInFallbackCount { get { return builtInFallbackCount; } }
        public int SkippedContentCount { get { return skippedContentCount; } }

        // Baker가 직렬화할 버전·Blueprint·구축 통계를 Prefab 루트에 복사합니다.
        public void Configure(
            int configuredFormatVersion,
            int configuredBuilderVersion,
            string configuredFinalBlueprintHash,
            DungeonSceneBuildResult buildResult)
        {
            formatVersion = configuredFormatVersion;
            builderVersion = configuredBuilderVersion;
            finalBlueprintHash = configuredFinalBlueprintHash ?? string.Empty;
            meshTriangleCount = buildResult.MeshTriangleCount;
            enemyCount = buildResult.ContentCounts.EnemyCount;
            destructibleCount = buildResult.ContentCounts.DestructibleCount;
            propCount = buildResult.ContentCounts.PropCount;
            gimmickCount = buildResult.ContentCounts.GimmickCount;
            resolvedContentCount = buildResult.ResolvedContentCount;
            builtInFallbackCount = buildResult.BuiltInFallbackCount;
            skippedContentCount = buildResult.SkippedContentCount;
            buildIssues = CloneIssues(
                buildResult.ValidationReport != null
                    ? buildResult.ValidationReport.issues
                    : null);
        }

        // 저장된 직렬화 필드를 런타임 GenerationReport와 동일한 구축 결과 구조로 복원합니다.
        public DungeonSceneBuildResult ToBuildResult()
        {
            DungeonValidationReport validationReport = new DungeonValidationReport
            {
                issues = CloneIssues(buildIssues)
            };
            return new DungeonSceneBuildResult
            {
                MeshTriangleCount = meshTriangleCount,
                ContentCounts = new ContentSpawnCounts
                {
                    EnemyCount = enemyCount,
                    DestructibleCount = destructibleCount,
                    PropCount = propCount,
                    GimmickCount = gimmickCount
                },
                ResolvedContentCount = resolvedContentCount,
                BuiltInFallbackCount = builtInFallbackCount,
                SkippedContentCount = skippedContentCount,
                ValidationReport = validationReport
            };
        }

        // Prefab metadata가 manifest 버전·최종 Blueprint와 일치하고 통계가 유효한지 검사합니다.
        public DungeonValidationReport Validate(DungeonBakeManifest manifest)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (!DungeonBakeFormat.IsSupported(formatVersion) ||
                (manifest != null && formatVersion != manifest.formatVersion))
            {
                report.Add(
                    DungeonBakedStageMetadataValidationCodes.InvalidFormatVersion,
                    DungeonValidationSeverity.Error,
                    "Baked stage metadata format version is unsupported or does not match its manifest.");
            }
            if (!DungeonBakeBuilderVersions.IsSupported(builderVersion) ||
                (manifest != null && builderVersion != manifest.builderVersion))
            {
                report.Add(
                    DungeonBakedStageMetadataValidationCodes.InvalidBuilderVersion,
                    DungeonValidationSeverity.Error,
                    "Baked stage metadata builder version is unsupported or does not match its manifest.");
            }
            if (manifest == null ||
                !string.Equals(
                    finalBlueprintHash,
                    manifest.finalBlueprintHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonBakedStageMetadataValidationCodes.FinalBlueprintHashMismatch,
                    DungeonValidationSeverity.Error,
                    "Baked stage metadata Blueprint hash does not match its manifest.");
            }
            if (HasNegativeBuildCount())
            {
                report.Add(
                    DungeonBakedStageMetadataValidationCodes.InvalidBuildCounts,
                    DungeonValidationSeverity.Error,
                    "Baked stage metadata contains a negative build count.");
            }
            ValidateIssues(report);
            return report;
        }

        // 모든 저장 통계가 음수가 아닌지 한 번에 확인합니다.
        private bool HasNegativeBuildCount()
        {
            return meshTriangleCount < 0 ||
                   enemyCount < 0 ||
                   destructibleCount < 0 ||
                   propCount < 0 ||
                   gimmickCount < 0 ||
                   resolvedContentCount < 0 ||
                   builtInFallbackCount < 0 ||
                   skippedContentCount < 0;
        }

        // Bake 성공 결과에는 오류가 아닌 완전한 경고 레코드만 남도록 검사합니다.
        private void ValidateIssues(DungeonValidationReport report)
        {
            if (buildIssues == null) return;
            for (int i = 0; i < buildIssues.Count; i++)
            {
                DungeonValidationIssue issue = buildIssues[i];
                if (issue != null &&
                    issue.severity == DungeonValidationSeverity.Warning &&
                    !string.IsNullOrWhiteSpace(issue.code))
                {
                    continue;
                }
                report.Add(
                    DungeonBakedStageMetadataValidationCodes.InvalidBuildIssue,
                    DungeonValidationSeverity.Error,
                    "Baked stage metadata contains an invalid build issue.");
                return;
            }
        }

        // Unity 직렬화 객체를 공유하지 않도록 검증 이슈 목록을 깊은 복사합니다.
        private static List<DungeonValidationIssue> CloneIssues(
            List<DungeonValidationIssue> source)
        {
            List<DungeonValidationIssue> clone = new List<DungeonValidationIssue>();
            if (source == null) return clone;
            for (int i = 0; i < source.Count; i++)
            {
                DungeonValidationIssue issue = source[i];
                if (issue == null)
                {
                    clone.Add(null);
                    continue;
                }
                clone.Add(new DungeonValidationIssue
                {
                    code = issue.code ?? string.Empty,
                    severity = issue.severity,
                    message = issue.message ?? string.Empty,
                    hasCell = issue.hasCell,
                    cell = issue.cell,
                    spawnId = issue.spawnId ?? string.Empty
                });
            }
            return clone;
        }
    }
}
