using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonStageDefinitionValidationCodes
    {
        public const string NullDefinition = "RDL-STAGE-001";
        public const string InvalidSourceMode = "RDL-STAGE-002";
        public const string UnsupportedBuildMode = "RDL-STAGE-003";
        public const string MissingRecipe = "RDL-STAGE-004";
        public const string MissingSavedBlueprint = "RDL-STAGE-005";
        public const string InvalidGeneratorVersion = "RDL-STAGE-006";
        public const string MissingRunSeed = "RDL-STAGE-007";
        public const string InvalidSeedPolicy = "RDL-STAGE-008";
        public const string InvalidMissingContentPolicy = "RDL-STAGE-009";
        public const string MissingBakedPrefab = "RDL-STAGE-010";
        public const string MissingBakeManifest = "RDL-STAGE-011";
        public const string BakeCatalogMismatch = "RDL-STAGE-012";
        public const string MissingBakedMetadata = "RDL-STAGE-013";
        public const string OverridesRequireSavedBlueprint = "RDL-STAGE-014";
        public const string OverrideBaseMismatch = "RDL-STAGE-015";
        public const string BakeOverrideMismatch = "RDL-STAGE-016";
    }

    public static class DungeonStageDefinitionValidator
    {
        // RuntimeBuild와 SavedBlueprint+BakedPrefab의 필수 참조·모드 계약을 검증합니다.
        public static DungeonValidationReport Validate(DungeonStageDefinition definition)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (definition == null)
            {
                report.Add(DungeonStageDefinitionValidationCodes.NullDefinition, DungeonValidationSeverity.Error, "Stage Definition is null.");
                return report;
            }

            if (!Enum.IsDefined(typeof(DungeonStageSourceMode), definition.sourceMode))
            {
                report.Add(DungeonStageDefinitionValidationCodes.InvalidSourceMode, DungeonValidationSeverity.Error, "Stage source mode is invalid.");
            }
            if (!Enum.IsDefined(typeof(DungeonStageBuildMode), definition.buildMode))
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.UnsupportedBuildMode,
                    DungeonValidationSeverity.Error,
                    "Stage build mode is invalid.");
            }
            else if (definition.buildMode == DungeonStageBuildMode.BakedPrefab &&
                     definition.sourceMode != DungeonStageSourceMode.SavedBlueprint)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.UnsupportedBuildMode,
                    DungeonValidationSeverity.Error,
                    "BakedPrefab build mode requires a SavedBlueprint source.");
            }
            if (definition.sourceMode == DungeonStageSourceMode.Procedural && definition.recipe == null)
            {
                report.Add(DungeonStageDefinitionValidationCodes.MissingRecipe, DungeonValidationSeverity.Error, "Procedural source requires a recipe.");
            }
            if (definition.sourceMode == DungeonStageSourceMode.SavedBlueprint && definition.savedBlueprint == null)
            {
                report.Add(DungeonStageDefinitionValidationCodes.MissingSavedBlueprint, DungeonValidationSeverity.Error, "SavedBlueprint source requires a Blueprint asset.");
            }
            if (definition.stageOverrides != null &&
                definition.sourceMode != DungeonStageSourceMode.SavedBlueprint)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.OverridesRequireSavedBlueprint,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides are supported only with a SavedBlueprint source.");
            }
            if (definition.stageOverrides != null &&
                definition.sourceMode == DungeonStageSourceMode.SavedBlueprint)
            {
                Merge(
                    report,
                    DungeonStageOverridesValidator.Validate(
                        definition.stageOverrides,
                        definition.savedBlueprint));
            }
            if (definition.sourceMode == DungeonStageSourceMode.Procedural && !DungeonGeneratorVersions.IsSupported(definition.generatorVersion))
            {
                report.Add(DungeonStageDefinitionValidationCodes.InvalidGeneratorVersion, DungeonValidationSeverity.Error, "Generator version must be LegacyV1 or StableV2.");
            }
            if (!Enum.IsDefined(typeof(DungeonStageSeedPolicy), definition.seedPolicy))
            {
                report.Add(DungeonStageDefinitionValidationCodes.InvalidSeedPolicy, DungeonValidationSeverity.Error, "Stage seed policy is invalid.");
            }
            if (!Enum.IsDefined(typeof(DungeonMissingContentPolicy), definition.missingContentPolicy))
            {
                report.Add(DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy, DungeonValidationSeverity.Error, "Missing content policy is invalid.");
            }
            if (definition.buildMode == DungeonStageBuildMode.BakedPrefab)
            {
                ValidateBakedDefinition(report, definition);
            }
            else if (definition.contentCatalog != null)
            {
                Merge(report, DungeonContentCatalogValidator.Validate(definition.contentCatalog));
            }
            return report;
        }

        // Baked stage가 manifest 원본·Prefab·Catalog와 정확히 연결되고 루트 metadata를 포함하는지 검사합니다.
        private static void ValidateBakedDefinition(
            DungeonValidationReport report,
            DungeonStageDefinition definition)
        {
            if (definition.bakedPrefab == null)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.MissingBakedPrefab,
                    DungeonValidationSeverity.Error,
                    "BakedPrefab build mode requires a baked Prefab.");
            }
            if (definition.bakeManifest == null)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.MissingBakeManifest,
                    DungeonValidationSeverity.Error,
                    "BakedPrefab build mode requires a Bake manifest.");
                return;
            }

            Merge(
                report,
                DungeonBakeManifestValidator.Validate(
                    definition.bakeManifest,
                    definition.savedBlueprint,
                    definition.bakedPrefab,
                    definition.stageOverrides));
            if (definition.bakeManifest.sourceCatalog != definition.contentCatalog)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.BakeCatalogMismatch,
                    DungeonValidationSeverity.Error,
                    "Stage Definition Content Catalog does not match its Bake manifest.");
            }
            if (definition.bakeManifest.sourceOverrides !=
                definition.stageOverrides)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.BakeOverrideMismatch,
                    DungeonValidationSeverity.Error,
                    "Stage Definition Overrides do not match its Bake manifest.");
            }
            if (definition.bakedPrefab == null) return;

            DungeonBakedStageMetadata metadata =
                definition.bakedPrefab.GetComponent<DungeonBakedStageMetadata>();
            if (metadata == null)
            {
                report.Add(
                    DungeonStageDefinitionValidationCodes.MissingBakedMetadata,
                    DungeonValidationSeverity.Error,
                    "The baked Prefab root requires DungeonBakedStageMetadata.");
                return;
            }
            Merge(report, metadata.Validate(definition.bakeManifest));
        }

        // 다른 계약 검증 결과를 Stage Definition 리포트에 순서대로 병합합니다.
        private static void Merge(DungeonValidationReport destination, DungeonValidationReport source)
        {
            if (destination == null || source == null || source.issues == null) return;
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }
    }

    public sealed class DungeonStageLoadException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        // 로드 실패 원인과 코드 기반 검증 리포트를 호출자에게 함께 전달합니다.
        public DungeonStageLoadException(string message, DungeonValidationReport validationReport)
            : base(message)
        {
            ValidationReport = validationReport ?? new DungeonValidationReport();
        }
    }

    public static class DungeonStageLoader
    {
        public const string GeneratedRootName = "__RogueDungeonLab_Generated";

        // StageDefinition을 검증한 뒤 RuntimeBuild 또는 저장된 BakedPrefab 전용 경로로 분기합니다.
        public static DungeonStageInstance Load(DungeonLoadContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            DungeonValidationReport definitionValidation = DungeonStageDefinitionValidator.Validate(context.Definition);
            if (context.MissingContentPolicyOverride.HasValue &&
                !Enum.IsDefined(
                    typeof(DungeonMissingContentPolicy),
                    context.MissingContentPolicyOverride.Value))
            {
                definitionValidation.Add(
                    DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy,
                    DungeonValidationSeverity.Error,
                    "Missing content policy override is invalid.");
            }
            ThrowIfInvalid("Stage Definition is invalid.", definitionValidation);

            DungeonStageDefinition definition = context.Definition;
            if (definition.buildMode == DungeonStageBuildMode.BakedPrefab)
            {
                return LoadBakedStage(context);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            DungeonBlueprint blueprint;
            DungeonLayout layout;
            RogueDungeonSettings sourceRecipe = null;
            DungeonStageOverrideApplyResult overrideApplication = null;

            if (definition.sourceMode == DungeonStageSourceMode.Procedural)
            {
                int seed = DungeonStageSeedResolver.Resolve(context);
                sourceRecipe = context.ProceduralRecipeOverride ?? definition.recipe;
                DungeonGenerationRequest request = definition.generatorVersion == DungeonGeneratorVersions.StableV2
                    ? DungeonGenerationRequest.CreateStableV2(
                        sourceRecipe,
                        seed,
                        definition.contentCatalog,
                        context.RequestId)
                    : DungeonGenerationRequest.Create(
                        sourceRecipe,
                        seed,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                        context.RequestId);
                DungeonBlueprintGenerationResult generated = DungeonBlueprintGenerator.Generate(request);
                blueprint = generated.Blueprint;
                layout = generated.Layout;
            }
            else
            {
                DungeonBlueprint source = definition.savedBlueprint != null ? definition.savedBlueprint.blueprint : null;
                if (source == null)
                {
                    DungeonValidationReport missing = new DungeonValidationReport();
                    missing.Add(DungeonStageDefinitionValidationCodes.MissingSavedBlueprint, DungeonValidationSeverity.Error, "Saved Blueprint data is missing.");
                    throw new DungeonStageLoadException("Saved Blueprint data is missing.", missing);
                }
                overrideApplication = DungeonStageOverrideApplier.Apply(
                    definition.savedBlueprint,
                    definition.stageOverrides);
                ThrowIfInvalid(
                    "Saved Dungeon Blueprint Overrides are invalid.",
                    overrideApplication.ValidationReport);
                blueprint = overrideApplication.FinalBlueprint;
                layout = DungeonBlueprintLayoutConverter.ToLayout(blueprint);
            }

            RogueDungeonSettings runtimeSettings = context.RuntimeSettings ?? sourceRecipe;
            DungeonMissingContentPolicy missingPolicy = context.MissingContentPolicyOverride ?? definition.missingContentPolicy;
            DungeonValidationReport contentValidation = DungeonContentCatalogValidator.ValidateBlueprint(
                blueprint,
                definition.contentCatalog,
                context.ContentResolver != null ? DungeonMissingContentPolicy.BuiltInFallback : missingPolicy);
            ThrowIfInvalid("Dungeon content is invalid.", contentValidation);
            IDungeonContentResolver resolver = context.ContentResolver;
            if (resolver == null && definition.contentCatalog != null)
                resolver = new DungeonPrefabContentResolver(definition.contentCatalog);
            return BuildRuntimeStage(
                context.Parent,
                definition,
                definition.sourceMode,
                blueprint,
                layout,
                runtimeSettings,
                sourceRecipe,
                context.RequestId,
                resolver,
                missingPolicy,
                contentValidation,
                stopwatch,
                definition.stageOverrides,
                overrideApplication != null
                    ? overrideApplication.SourceBlueprintHash
                    : string.Empty,
                overrideApplication != null
                    ? overrideApplication.OverrideHash
                    : string.Empty);
        }

        // 기존 settings 기반 facade가 StageDefinition 자산 없이 같은 Loader 구축 경로를 사용하게 합니다.
        public static DungeonStageInstance LoadProcedural(
            Transform parent,
            RogueDungeonSettings recipe,
            int seed,
            RogueDungeonSettings runtimeSettings = null,
            string requestId = "")
        {
            return LoadProcedural(
                parent,
                recipe,
                seed,
                DungeonGeneratorVersions.LegacyV1,
                runtimeSettings,
                null,
                DungeonMissingContentPolicy.BuiltInFallback,
                null,
                requestId);
        }

        // 명시한 생성기 버전·catalog·누락 정책으로 StageDefinition 없이 절차 스테이지를 구축합니다.
        public static DungeonStageInstance LoadProcedural(
            Transform parent,
            RogueDungeonSettings recipe,
            int seed,
            int generatorVersion,
            RogueDungeonSettings runtimeSettings = null,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback,
            IDungeonContentResolver contentResolver = null,
            string requestId = "")
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            DungeonValidationReport setupValidation = new DungeonValidationReport();
            if (!DungeonGeneratorVersions.IsSupported(generatorVersion))
            {
                setupValidation.Add(
                    DungeonStageDefinitionValidationCodes.InvalidGeneratorVersion,
                    DungeonValidationSeverity.Error,
                    "Generator version must be LegacyV1 or StableV2.");
            }
            if (!Enum.IsDefined(typeof(DungeonMissingContentPolicy), missingContentPolicy))
            {
                setupValidation.Add(
                    DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy,
                    DungeonValidationSeverity.Error,
                    "Missing content policy is invalid.");
            }
            if (contentCatalog != null)
            {
                DungeonValidationReport catalogValidation =
                    DungeonContentCatalogValidator.Validate(contentCatalog);
                for (int i = 0; i < catalogValidation.issues.Count; i++)
                {
                    DungeonValidationIssue issue = catalogValidation.issues[i];
                    if (issue != null) setupValidation.issues.Add(issue);
                }
            }
            ThrowIfInvalid("Procedural load settings are invalid.", setupValidation);

            Stopwatch stopwatch = Stopwatch.StartNew();
            DungeonGenerationRequest request = generatorVersion == DungeonGeneratorVersions.StableV2
                ? DungeonGenerationRequest.CreateStableV2(
                    recipe,
                    seed,
                    contentCatalog,
                    requestId)
                : DungeonGenerationRequest.Create(
                    recipe,
                    seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    requestId);
            DungeonBlueprintGenerationResult generated = DungeonBlueprintGenerator.Generate(request);
            DungeonValidationReport contentValidation = DungeonContentCatalogValidator.ValidateBlueprint(
                generated.Blueprint,
                contentCatalog,
                contentResolver != null
                    ? DungeonMissingContentPolicy.BuiltInFallback
                    : missingContentPolicy);
            ThrowIfInvalid("Dungeon content is invalid.", contentValidation);

            IDungeonContentResolver resolver = contentResolver;
            if (resolver == null && contentCatalog != null)
                resolver = new DungeonPrefabContentResolver(contentCatalog);
            return BuildRuntimeStage(
                parent,
                null,
                DungeonStageSourceMode.Procedural,
                generated.Blueprint,
                generated.Layout,
                runtimeSettings ?? recipe,
                recipe,
                requestId,
                resolver,
                missingContentPolicy,
                contentValidation,
                stopwatch);
        }

        // 저장 Blueprint를 기존 공개 시그니처로 레시피나 시드 재계산 없이 즉시 구축합니다.
        public static DungeonStageInstance LoadSavedBlueprint(
            Transform parent,
            DungeonBlueprintAsset blueprintAsset,
            RogueDungeonSettings runtimeSettings = null,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback,
            IDungeonContentResolver contentResolver = null,
            string requestId = "")
        {
            return LoadSavedBlueprint(
                parent,
                blueprintAsset,
                runtimeSettings,
                contentCatalog,
                missingContentPolicy,
                contentResolver,
                requestId,
                null);
        }

        // 저장 Blueprint를 선택적 Override·catalog·resolver로 레시피나 시드 재계산 없이 구축합니다.
        public static DungeonStageInstance LoadSavedBlueprint(
            Transform parent,
            DungeonBlueprintAsset blueprintAsset,
            RogueDungeonSettings runtimeSettings,
            DungeonContentCatalog contentCatalog,
            DungeonMissingContentPolicy missingContentPolicy,
            IDungeonContentResolver contentResolver,
            string requestId,
            DungeonStageOverrides stageOverrides)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            DungeonValidationReport setupValidation = new DungeonValidationReport();
            if (blueprintAsset == null)
            {
                setupValidation.Add(
                    DungeonStageDefinitionValidationCodes.MissingSavedBlueprint,
                    DungeonValidationSeverity.Error,
                    "Saved Blueprint asset is missing.");
            }
            if (!Enum.IsDefined(typeof(DungeonMissingContentPolicy), missingContentPolicy))
            {
                setupValidation.Add(
                    DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy,
                    DungeonValidationSeverity.Error,
                    "Missing content policy is invalid.");
            }
            if (contentCatalog != null)
            {
                DungeonValidationReport catalogValidation =
                    DungeonContentCatalogValidator.Validate(contentCatalog);
                for (int i = 0; i < catalogValidation.issues.Count; i++)
                {
                    DungeonValidationIssue issue = catalogValidation.issues[i];
                    if (issue != null) setupValidation.issues.Add(issue);
                }
            }
            if (stageOverrides != null)
            {
                MergeValidation(
                    setupValidation,
                    DungeonStageOverridesValidator.Validate(
                        stageOverrides,
                        blueprintAsset));
            }
            ThrowIfInvalid("Saved Blueprint load settings are invalid.", setupValidation);

            DungeonStageOverrideApplyResult overrideApplication =
                DungeonStageOverrideApplier.Apply(
                    blueprintAsset,
                    stageOverrides);
            ThrowIfInvalid(
                "Saved Dungeon Blueprint Overrides are invalid.",
                overrideApplication.ValidationReport);
            DungeonBlueprint blueprint =
                overrideApplication.FinalBlueprint;
            DungeonLayout layout = DungeonBlueprintLayoutConverter.ToLayout(blueprint);
            DungeonValidationReport contentValidation = DungeonContentCatalogValidator.ValidateBlueprint(
                blueprint,
                contentCatalog,
                contentResolver != null
                    ? DungeonMissingContentPolicy.BuiltInFallback
                    : missingContentPolicy);
            ThrowIfInvalid("Dungeon content is invalid.", contentValidation);

            IDungeonContentResolver resolver = contentResolver;
            if (resolver == null && contentCatalog != null)
                resolver = new DungeonPrefabContentResolver(contentCatalog);
            Stopwatch stopwatch = Stopwatch.StartNew();
            return BuildRuntimeStage(
                parent,
                null,
                DungeonStageSourceMode.SavedBlueprint,
                blueprint,
                layout,
                runtimeSettings,
                null,
                requestId,
                resolver,
                missingContentPolicy,
                contentValidation,
                stopwatch,
                stageOverrides,
                overrideApplication.SourceBlueprintHash,
                overrideApplication.OverrideHash);
        }

        // 지정 부모 아래의 generated root와 소유한 동적 메시를 안전하게 제거합니다.
        public static void ClearGenerated(Transform parent)
        {
            if (parent == null) return;
            Transform existing = parent.Find(GeneratedRootName);
            if (existing != null) DestroyGeneratedRoot(existing.gameObject);
        }

        // 검증된 Blueprint를 단일 generated root에 구축하고 리포트·인스턴스를 완성합니다.
        private static DungeonStageInstance BuildRuntimeStage(
            Transform parent,
            DungeonStageDefinition definition,
            DungeonStageSourceMode sourceMode,
            DungeonBlueprint blueprint,
            DungeonLayout layout,
            RogueDungeonSettings runtimeSettings,
            RogueDungeonSettings sourceRecipe,
            string requestId,
            IDungeonContentResolver contentResolver,
            DungeonMissingContentPolicy missingContentPolicy,
            DungeonValidationReport contentValidation,
            Stopwatch stopwatch,
            DungeonStageOverrides appliedOverrides = null,
            string sourceBlueprintHash = "",
            string overrideHash = "")
        {
            DungeonValidationReport blueprintValidation = DungeonBlueprintValidator.Validate(blueprint);
            ThrowIfInvalid("Dungeon Blueprint is invalid.", blueprintValidation);

            GameObject root = new GameObject(GeneratedRootName + "_Building");
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            if (!Application.isPlaying) root.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            try
            {
                DungeonSceneBuildResult buildResult = DungeonSceneBuilder.Build(
                    root.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(runtimeSettings, contentResolver, missingContentPolicy));
                ClearGenerated(parent);
                root.name = GeneratedRootName;
                root.SetActive(true);
                stopwatch.Stop();
                GenerationReport report = CreateReport(
                    parent,
                    blueprint,
                    layout,
                    buildResult,
                    sourceRecipe,
                    stopwatch.Elapsed.TotalMilliseconds);
                AppendValidationWarnings(report, contentValidation);
                DungeonValidationReport combinedValidation = CombineValidationReports(
                    blueprintValidation,
                    contentValidation,
                    buildResult.ValidationReport);
                return new DungeonStageInstance(
                    definition,
                    sourceMode,
                    DungeonStageBuildMode.RuntimeBuild,
                    blueprint,
                    layout,
                    root,
                    runtimeSettings,
                    buildResult,
                    combinedValidation,
                    report,
                    requestId,
                    appliedOverrides,
                    sourceBlueprintHash,
                    overrideHash);
            }
            catch
            {
                DestroyGeneratedRoot(root);
                throw;
            }
        }

        // 저장 Blueprint는 데이터로 복원하고 Prefab만 복제해 생성기·resolver·SceneBuilder 없이 Baked stage를 교체합니다.
        private static DungeonStageInstance LoadBakedStage(
            DungeonLoadContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            DungeonStageDefinition definition = context.Definition;
            DungeonStageOverrideApplyResult overrideApplication =
                DungeonStageOverrideApplier.Apply(
                    definition.savedBlueprint,
                    definition.stageOverrides);
            DungeonValidationReport blueprintValidation =
                overrideApplication.ValidationReport;
            ThrowIfInvalid(
                "Saved Dungeon Blueprint Overrides are invalid.",
                blueprintValidation);
            DungeonBlueprint blueprint =
                overrideApplication.FinalBlueprint;
            if (!string.Equals(
                    overrideApplication.FinalBlueprintHash,
                    definition.bakeManifest.finalBlueprintHash,
                    StringComparison.Ordinal))
            {
                DungeonValidationReport mismatch =
                    new DungeonValidationReport();
                mismatch.Add(
                    DungeonBakeManifestValidationCodes
                        .FinalBlueprintHashMismatch,
                    DungeonValidationSeverity.Error,
                    "Baked stage final Blueprint hash is stale.");
                throw new DungeonStageLoadException(
                    "Baked stage final Blueprint hash is stale.",
                    mismatch);
            }
            DungeonLayout layout = DungeonBlueprintLayoutConverter.ToLayout(blueprint);

            GameObject staging = new GameObject(GeneratedRootName + "_BakedStaging");
            staging.transform.SetParent(context.Parent, false);
            staging.SetActive(false);
            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(
                    definition.bakedPrefab,
                    staging.transform,
                    false);
                root.SetActive(false);
                root.name = GeneratedRootName + "_Building";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                DungeonBakedStageMetadata metadata =
                    root.GetComponent<DungeonBakedStageMetadata>();
                DungeonValidationReport metadataValidation =
                    metadata != null
                        ? metadata.Validate(definition.bakeManifest)
                        : CreateMissingBakedMetadataReport();
                ThrowIfInvalid(
                    "Baked stage metadata is invalid.",
                    metadataValidation);

                DungeonSceneBuildResult buildResult = metadata.ToBuildResult();
                DungeonValidationReport combinedValidation =
                    CombineValidationReports(
                        blueprintValidation,
                        buildResult.ValidationReport);
                stopwatch.Stop();
                GenerationReport report = CreateReport(
                    context.Parent,
                    blueprint,
                    layout,
                    buildResult,
                    null,
                    stopwatch.Elapsed.TotalMilliseconds);

                root.transform.SetParent(context.Parent, false);
                ClearGenerated(context.Parent);
                root.name = GeneratedRootName;
                root.SetActive(true);
                DestroyGeneratedRoot(staging);
                staging = null;
                return new DungeonStageInstance(
                    definition,
                    DungeonStageSourceMode.SavedBlueprint,
                    DungeonStageBuildMode.BakedPrefab,
                    blueprint,
                    layout,
                    root,
                    definition.bakeManifest.sourceRuntimeSettings,
                    buildResult,
                    combinedValidation,
                    report,
                    context.RequestId,
                    definition.stageOverrides,
                    overrideApplication.SourceBlueprintHash,
                    overrideApplication.OverrideHash);
            }
            catch
            {
                if (staging != null) DestroyGeneratedRoot(staging);
                else if (root != null && root.name != GeneratedRootName)
                    DestroyGeneratedRoot(root);
                throw;
            }
        }

        // Prefab 복제본에서 metadata가 사라진 비정상 상태를 안정적인 코드로 보고합니다.
        private static DungeonValidationReport CreateMissingBakedMetadataReport()
        {
            DungeonValidationReport report = new DungeonValidationReport();
            report.Add(
                DungeonBakedStageMetadataValidationCodes.MissingMetadata,
                DungeonValidationSeverity.Error,
                "The baked Prefab root requires DungeonBakedStageMetadata.");
            return report;
        }

        // Blueprint와 실제 구축 개수에서 기존 GenerationReport 호환 데이터를 계산합니다.
        private static GenerationReport CreateReport(
            Transform parent,
            DungeonBlueprint blueprint,
            DungeonLayout layout,
            DungeonSceneBuildResult buildResult,
            RogueDungeonSettings sourceRecipe,
            double milliseconds)
        {
            ContentSpawnCounts counts = buildResult.ContentCounts;
            GenerationReport report = new GenerationReport
            {
                activeSeed = blueprint.seed,
                roomCount = layout.Rooms.Count,
                floorCellCount = layout.WalkableCellCount,
                enemyCount = counts.EnemyCount,
                destructibleCount = counts.DestructibleCount,
                propCount = counts.PropCount,
                gimmickCount = counts.GimmickCount,
                meshTriangleCount = buildResult.MeshTriangleCount,
                generationMilliseconds = milliseconds,
                worldBounds = CalculateBounds(parent, blueprint.grid)
            };

            if (sourceRecipe != null && layout.Rooms.Count < sourceRecipe.desiredRoomCount)
                report.warnings.Add(string.Format("Requested {0} rooms but placed {1}.", sourceRecipe.desiredRoomCount, layout.Rooms.Count));
            int disconnected = 0;
            foreach (Vector2Int cell in layout.EnumerateFloorCells()) if (layout.GetDistance(cell) < 0) disconnected++;
            if (disconnected > 0) report.warnings.Add(disconnected + " floor cells are disconnected.");
            if (layout.GetDistance(layout.Exit) <= 0) report.warnings.Add("Exit is not meaningfully separated from the entrance.");
            if (sourceRecipe != null && counts.GimmickCount < sourceRecipe.specialGimmickCount)
                report.warnings.Add(string.Format("Requested {0} gimmicks but placed {1} due to spacing.", sourceRecipe.specialGimmickCount, counts.GimmickCount));
            AppendValidationWarnings(report, buildResult.ValidationReport);
            return report;
        }

        private static DungeonValidationReport CombineValidationReports(params DungeonValidationReport[] reports)
        {
            DungeonValidationReport combined = new DungeonValidationReport();
            for (int reportIndex = 0; reportIndex < reports.Length; reportIndex++)
            {
                DungeonValidationReport source = reports[reportIndex];
                if (source == null || source.issues == null) continue;
                for (int issueIndex = 0; issueIndex < source.issues.Count; issueIndex++)
                {
                    DungeonValidationIssue issue = source.issues[issueIndex];
                    if (issue != null) combined.issues.Add(issue);
                }
            }
            return combined;
        }

        // 하위 검증 리포트의 이슈를 기존 순서대로 대상 리포트에 병합합니다.
        private static void MergeValidation(
            DungeonValidationReport destination,
            DungeonValidationReport source)
        {
            if (destination == null ||
                source == null ||
                source.issues == null)
            {
                return;
            }
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }

        private static void AppendValidationWarnings(GenerationReport report, DungeonValidationReport validation)
        {
            if (report == null || validation == null || validation.issues == null) return;
            for (int i = 0; i < validation.issues.Count; i++)
            {
                DungeonValidationIssue issue = validation.issues[i];
                if (issue != null && issue.severity == DungeonValidationSeverity.Warning)
                    report.warnings.Add(issue.code + ": " + issue.message);
            }
        }

        // Blueprint grid 크기를 부모 위치 기준의 기존 axis-aligned 월드 Bounds로 변환합니다.
        private static Bounds CalculateBounds(Transform parent, DungeonGridRecord grid)
        {
            if (grid == null) return new Bounds(parent.position, Vector3.one * 10f);
            Vector3 localSize = new Vector3(grid.width * grid.cellSize, grid.wallHeight, grid.depth * grid.cellSize);
            Vector3 scale = parent.lossyScale;
            Vector3 worldSize = new Vector3(
                Mathf.Abs(localSize.x * scale.x),
                Mathf.Abs(localSize.y * scale.y),
                Mathf.Abs(localSize.z * scale.z));
            Vector3 center = parent.TransformPoint(Vector3.up * (grid.wallHeight * 0.5f));
            return new Bounds(center, worldSize);
        }

        // 오류가 포함된 검증 리포트를 코드 요약이 포함된 로드 예외로 변환합니다.
        private static void ThrowIfInvalid(string message, DungeonValidationReport report)
        {
            if (report != null && report.IsValid) return;
            List<string> codes = new List<string>();
            if (report != null)
            {
                for (int i = 0; i < report.issues.Count; i++)
                {
                    DungeonValidationIssue issue = report.issues[i];
                    if (issue != null && issue.severity == DungeonValidationSeverity.Error) codes.Add(issue.code);
                }
            }
            string suffix = codes.Count > 0 ? " [" + string.Join(",", codes) + "]" : string.Empty;
            throw new DungeonStageLoadException(message + suffix, report);
        }

        // 명시적 owner가 기록한 합성 메시만 해제하고 Edit/Play 수명주기에 맞게 root를 제거합니다.
        private static void DestroyGeneratedRoot(GameObject root)
        {
            if (root == null) return;
            root.SetActive(false);
            DungeonGeneratedMeshOwner[] owners =
                root.GetComponentsInChildren<DungeonGeneratedMeshOwner>(true);
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] != null) owners[i].ReleaseOwnedMeshes();
            }

            if (Application.isPlaying)
            {
                root.name = GeneratedRootName + "_PendingDestroy";
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
