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
    }

    public static class DungeonStageDefinitionValidator
    {
        // RuntimeBuild source와 R4 생성기·콘텐츠 카탈로그 계약을 검증합니다.
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
            if (definition.buildMode != DungeonStageBuildMode.RuntimeBuild)
            {
                report.Add(DungeonStageDefinitionValidationCodes.UnsupportedBuildMode, DungeonValidationSeverity.Error, "R4 supports RuntimeBuild only.");
            }
            if (definition.sourceMode == DungeonStageSourceMode.Procedural && definition.recipe == null)
            {
                report.Add(DungeonStageDefinitionValidationCodes.MissingRecipe, DungeonValidationSeverity.Error, "Procedural source requires a recipe.");
            }
            if (definition.sourceMode == DungeonStageSourceMode.SavedBlueprint && definition.savedBlueprint == null)
            {
                report.Add(DungeonStageDefinitionValidationCodes.MissingSavedBlueprint, DungeonValidationSeverity.Error, "SavedBlueprint source requires a Blueprint asset.");
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
            if (definition.contentCatalog != null)
            {
                Merge(report, DungeonContentCatalogValidator.Validate(definition.contentCatalog));
            }
            return report;
        }

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

        // StageDefinition의 Procedural 또는 SavedBlueprint 소스를 해석해 하나의 RuntimeBuild 인스턴스를 만듭니다.
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

            Stopwatch stopwatch = Stopwatch.StartNew();
            DungeonStageDefinition definition = context.Definition;
            DungeonBlueprint blueprint;
            DungeonLayout layout;
            RogueDungeonSettings sourceRecipe = null;

            if (definition.sourceMode == DungeonStageSourceMode.Procedural)
            {
                int seed = DungeonStageSeedResolver.Resolve(context);
                sourceRecipe = definition.recipe;
                DungeonGenerationRequest request = definition.generatorVersion == DungeonGeneratorVersions.StableV2
                    ? DungeonGenerationRequest.CreateStableV2(
                        definition.recipe,
                        seed,
                        definition.contentCatalog,
                        context.RequestId)
                    : DungeonGenerationRequest.Create(
                        definition.recipe,
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
                blueprint = source.DeepClone();
                DungeonValidationReport savedValidation = DungeonBlueprintValidator.Validate(blueprint);
                ThrowIfInvalid("Saved Dungeon Blueprint is invalid.", savedValidation);
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
                stopwatch);
        }

        // 기존 settings 기반 facade가 StageDefinition 자산 없이 같은 Loader 구축 경로를 사용하게 합니다.
        public static DungeonStageInstance LoadProcedural(
            Transform parent,
            RogueDungeonSettings recipe,
            int seed,
            RogueDungeonSettings runtimeSettings = null,
            string requestId = "")
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            Stopwatch stopwatch = Stopwatch.StartNew();
            DungeonGenerationRequest request = DungeonGenerationRequest.Create(
                recipe,
                seed,
                DungeonGeneratorVersions.LegacyV1,
                DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                requestId);
            DungeonBlueprintGenerationResult generated = DungeonBlueprintGenerator.Generate(request);
            return BuildRuntimeStage(
                parent,
                null,
                DungeonStageSourceMode.Procedural,
                generated.Blueprint,
                generated.Layout,
                runtimeSettings ?? recipe,
                recipe,
                requestId,
                null,
                DungeonMissingContentPolicy.BuiltInFallback,
                new DungeonValidationReport(),
                stopwatch);
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
            Stopwatch stopwatch)
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
                    blueprint,
                    layout,
                    root,
                    runtimeSettings,
                    buildResult,
                    combinedValidation,
                    report,
                    requestId);
            }
            catch
            {
                DestroyGeneratedRoot(root);
                throw;
            }
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
