using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonBakeFormat
    {
        public const int Current = 1;
    }

    public static class DungeonBakeBuilderVersions
    {
        public const int Current = 1;

        // 현재 Runtime이 검증할 수 있는 Bake builder 버전인지 확인합니다.
        public static bool IsSupported(int version)
        {
            return version == Current;
        }
    }

    [Serializable]
    public sealed class DungeonBakeArtifactRecord
    {
        public string role = string.Empty;
        public string assetGuid = string.Empty;
        public string dependencyHash = string.Empty;
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Bake Manifest", fileName = "DungeonBakeManifest")]
    public sealed class DungeonBakeManifest : ScriptableObject
    {
        [Header("버전")]
        [Min(1)] public int formatVersion = DungeonBakeFormat.Current;
        [Min(1)] public int builderVersion = DungeonBakeBuilderVersions.Current;

        [Header("원본")]
        public DungeonBlueprintAsset sourceBlueprint;
        public DungeonContentCatalog sourceCatalog;
        public RogueDungeonSettings sourceRuntimeSettings;
        public DungeonBakeMaterialSet materialSet;

        [Header("파생 결과")]
        public GameObject bakedPrefab;
        public List<DungeonBakeArtifactRecord> ownedArtifacts =
            new List<DungeonBakeArtifactRecord>();

        [Header("무결성")]
        public string sourceBlueprintHash = string.Empty;
        public string finalBlueprintHash = string.Empty;
        public string catalogPlanningHash = string.Empty;
        public string contentRealizationHash = string.Empty;
        public string gameplayBuildConfigHash = string.Empty;
        public string materialDependencyHash = string.Empty;
        public string overrideHash = string.Empty;
    }

    public static class DungeonBakeManifestValidationCodes
    {
        public const string NullManifest = "RDL-BAKE-001";
        public const string InvalidFormatVersion = "RDL-BAKE-002";
        public const string InvalidBuilderVersion = "RDL-BAKE-003";
        public const string MissingSourceBlueprint = "RDL-BAKE-004";
        public const string InvalidSourceBlueprint = "RDL-BAKE-005";
        public const string SourceBlueprintHashMismatch = "RDL-BAKE-006";
        public const string MissingFinalBlueprintHash = "RDL-BAKE-007";
        public const string FinalBlueprintHashMismatch = "RDL-BAKE-008";
        public const string CatalogPlanningHashMismatch = "RDL-BAKE-009";
        public const string MissingContentRealizationHash = "RDL-BAKE-010";
        public const string MissingGameplayBuildConfigHash = "RDL-BAKE-011";
        public const string MissingMaterialDependencyHash = "RDL-BAKE-012";
        public const string MissingBakedPrefab = "RDL-BAKE-013";
        public const string ExpectedSourceMismatch = "RDL-BAKE-014";
        public const string ExpectedPrefabMismatch = "RDL-BAKE-015";
        public const string MissingSourceCatalog = "RDL-BAKE-016";
        public const string MissingMaterialSet = "RDL-BAKE-017";
        public const string MissingOwnedArtifacts = "RDL-BAKE-018";
        public const string InvalidOwnedArtifact = "RDL-BAKE-019";
        public const string DuplicateOwnedArtifact = "RDL-BAKE-020";
        public const string UnsupportedOverrideHash = "RDL-BAKE-021";
        public const string IncompleteMaterialSet = "RDL-BAKE-022";
    }

    public static class DungeonBakeManifestValidator
    {
        // Runtime-safe manifest 필드와 저장 Blueprint의 논리 hash가 서로 일치하는지 검사합니다.
        public static DungeonValidationReport Validate(
            DungeonBakeManifest manifest,
            DungeonBlueprintAsset expectedSource = null,
            GameObject expectedPrefab = null)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (manifest == null)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.NullManifest,
                    DungeonValidationSeverity.Error,
                    "Bake manifest is null.");
                return report;
            }

            if (manifest.formatVersion != DungeonBakeFormat.Current)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.InvalidFormatVersion,
                    DungeonValidationSeverity.Error,
                    "Bake manifest format version is unsupported.");
            }
            if (!DungeonBakeBuilderVersions.IsSupported(manifest.builderVersion))
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.InvalidBuilderVersion,
                    DungeonValidationSeverity.Error,
                    "Bake builder version is unsupported.");
            }

            DungeonBlueprint blueprint =
                manifest.sourceBlueprint != null ? manifest.sourceBlueprint.blueprint : null;
            if (blueprint == null)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.MissingSourceBlueprint,
                    DungeonValidationSeverity.Error,
                    "Bake manifest requires a saved source Blueprint.");
            }
            else
            {
                DungeonValidationReport blueprintReport =
                    DungeonBlueprintValidator.Validate(blueprint);
                if (!blueprintReport.IsValid)
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.InvalidSourceBlueprint,
                        DungeonValidationSeverity.Error,
                        "Bake manifest source Blueprint is invalid.");
                }
                Merge(report, blueprintReport);

                if (!string.Equals(
                        manifest.sourceBlueprintHash,
                        blueprint.blueprintHash,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.SourceBlueprintHashMismatch,
                        DungeonValidationSeverity.Error,
                        "Bake manifest source Blueprint hash is stale.");
                }
                if (!string.Equals(
                        manifest.catalogPlanningHash,
                        blueprint.catalogPlanningHash,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.CatalogPlanningHashMismatch,
                        DungeonValidationSeverity.Error,
                        "Bake manifest catalog planning hash does not match its Blueprint.");
                }
            }
            if (manifest.sourceCatalog != null)
            {
                DungeonValidationReport catalogReport =
                    DungeonContentCatalogValidator.Validate(manifest.sourceCatalog);
                Merge(report, catalogReport);
                if (catalogReport.IsValid &&
                    !string.Equals(
                        manifest.catalogPlanningHash,
                        manifest.sourceCatalog.ComputePlanningHash(),
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.CatalogPlanningHashMismatch,
                        DungeonValidationSeverity.Error,
                        "Bake manifest catalog planning hash is stale.");
                }
            }
            if (blueprint != null &&
                RequiresSourceCatalog(blueprint.catalogPlanningHash) &&
                manifest.sourceCatalog == null)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.MissingSourceCatalog,
                    DungeonValidationSeverity.Error,
                    "A custom catalog Blueprint requires its source Catalog in the Bake manifest.");
            }

            RequireHash(
                report,
                manifest.finalBlueprintHash,
                DungeonBakeManifestValidationCodes.MissingFinalBlueprintHash,
                "Bake manifest final Blueprint hash is missing.");
            RequireHash(
                report,
                manifest.contentRealizationHash,
                DungeonBakeManifestValidationCodes.MissingContentRealizationHash,
                "Bake manifest content realization hash is missing.");
            RequireHash(
                report,
                manifest.gameplayBuildConfigHash,
                DungeonBakeManifestValidationCodes.MissingGameplayBuildConfigHash,
                "Bake manifest gameplay build config hash is missing.");
            RequireHash(
                report,
                manifest.materialDependencyHash,
                DungeonBakeManifestValidationCodes.MissingMaterialDependencyHash,
                "Bake manifest material dependency hash is missing.");

            if (manifest.materialSet == null)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.MissingMaterialSet,
                    DungeonValidationSeverity.Error,
                    "Bake manifest requires a persistent material set.");
            }
            else if (!HasCompleteMaterialSet(manifest.materialSet))
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.IncompleteMaterialSet,
                    DungeonValidationSeverity.Error,
                    "Bake material set must assign floor, wall, and every built-in content material.");
            }
            if (!string.IsNullOrEmpty(manifest.overrideHash))
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.UnsupportedOverrideHash,
                    DungeonValidationSeverity.Error,
                    "Bake format v1 does not support stage overrides.");
            }
            if (blueprint != null &&
                !string.Equals(
                    manifest.finalBlueprintHash,
                    blueprint.blueprintHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.FinalBlueprintHashMismatch,
                    DungeonValidationSeverity.Error,
                    "Bake format v1 requires the final Blueprint hash to match its source.");
            }
            if (manifest.bakedPrefab == null)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.MissingBakedPrefab,
                    DungeonValidationSeverity.Error,
                    "Bake manifest requires a baked Prefab.");
            }
            if (expectedSource != null && manifest.sourceBlueprint != expectedSource)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.ExpectedSourceMismatch,
                    DungeonValidationSeverity.Error,
                    "Bake manifest belongs to a different source Blueprint.");
            }
            if (expectedPrefab != null && manifest.bakedPrefab != expectedPrefab)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.ExpectedPrefabMismatch,
                    DungeonValidationSeverity.Error,
                    "Bake manifest belongs to a different Prefab.");
            }
            ValidateOwnedArtifacts(report, manifest.ownedArtifacts);
            return report;
        }

        // R6 MVP가 생성하는 geometry와 모든 built-in 콘텐츠 범주에 영속 재질 참조가 있는지 확인합니다.
        private static bool HasCompleteMaterialSet(DungeonBakeMaterialSet materialSet)
        {
            return materialSet != null &&
                   materialSet.floor != null &&
                   materialSet.wall != null &&
                   materialSet.enemy != null &&
                   materialSet.destructible != null &&
                   materialSet.prop != null &&
                   materialSet.gimmick != null &&
                   materialSet.entrance != null &&
                   materialSet.exit != null;
        }

        // Built-in planning 계약이 아닌 Blueprint가 원본 Catalog 참조를 보존해야 하는지 판정합니다.
        private static bool RequiresSourceCatalog(string catalogPlanningHash)
        {
            return !string.Equals(
                       catalogPlanningHash,
                       DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                       StringComparison.Ordinal) &&
                   !string.Equals(
                       catalogPlanningHash,
                       DungeonBuiltInContentKeys.StableCatalogPlanningHash,
                       StringComparison.Ordinal);
        }

        // Baker 소유 산출물 목록이 비어 있거나 추적에 필요한 필드가 빠졌는지 검사합니다.
        private static void ValidateOwnedArtifacts(
            DungeonValidationReport report,
            List<DungeonBakeArtifactRecord> ownedArtifacts)
        {
            if (ownedArtifacts == null || ownedArtifacts.Count == 0)
            {
                report.Add(
                    DungeonBakeManifestValidationCodes.MissingOwnedArtifacts,
                    DungeonValidationSeverity.Error,
                    "Bake manifest must record its owned derived artifacts.");
                return;
            }

            HashSet<string> assetGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ownedArtifacts.Count; i++)
            {
                DungeonBakeArtifactRecord artifact = ownedArtifacts[i];
                if (artifact == null ||
                    string.IsNullOrWhiteSpace(artifact.role) ||
                    string.IsNullOrWhiteSpace(artifact.assetGuid) ||
                    string.IsNullOrWhiteSpace(artifact.dependencyHash))
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.InvalidOwnedArtifact,
                        DungeonValidationSeverity.Error,
                        "Bake manifest contains an incomplete owned artifact record.");
                    return;
                }

                if (!assetGuids.Add(artifact.assetGuid))
                {
                    report.Add(
                        DungeonBakeManifestValidationCodes.DuplicateOwnedArtifact,
                        DungeonValidationSeverity.Error,
                        "Bake manifest must record each owned asset GUID only once.");
                    return;
                }
            }
        }

        // 필수 Bake hash가 비어 있을 때 안정적인 코드의 검증 오류를 추가합니다.
        private static void RequireHash(
            DungeonValidationReport report,
            string value,
            string code,
            string message)
        {
            if (!string.IsNullOrWhiteSpace(value)) return;
            report.Add(code, DungeonValidationSeverity.Error, message);
        }

        // 하위 데이터 검증 결과를 manifest 검증 리포트에 병합합니다.
        private static void Merge(
            DungeonValidationReport destination,
            DungeonValidationReport source)
        {
            if (destination == null || source == null || source.issues == null) return;
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }
    }
}
