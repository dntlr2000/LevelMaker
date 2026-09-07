using System;
using System.IO;
using RogueDungeonLab;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace RogueDungeonLabUpmConsumerVerification
{
    public static class UpmBakingConsumerSmoke
    {
        private const string CorePackageId =
            "com.dntlr2000.rogue-dungeon-lab.core";
        private const string BakingPackageId =
            "com.dntlr2000.rogue-dungeon-lab.baking";
        private const string Root = "Assets/R9_1BakingConsumer";
        private const string SettingsPath = Root + "/RuntimeSettings.asset";
        private const string BlueprintPath = Root + "/SavedBlueprint.asset";
        private const string DefinitionPath = Root + "/BakedStage.asset";
        private const string MaterialSetPath = Root + "/BakeMaterials.asset";

        // UPM Baking에서 실제 Bake·modular 내보내기와 standalone 차단 계약을 검증합니다.
        public static void Verify()
        {
            RequireRegisteredPackage(CorePackageId, "0.13.0");
            RequireRegisteredPackage(BakingPackageId, "0.13.0");
            EnsureFolder(Root);
            RogueDungeonSettings settings = CreateSettings();
            DungeonBlueprintAsset blueprint = CreateBlueprint(settings);
            DungeonStageDefinition definition = CreateDefinition(blueprint);
            DungeonBakeMaterialSet materialSet =
                DungeonStageBaker.CreateDefaultMaterialSetAsset(
                    MaterialSetPath);
            AssetDatabase.SaveAssets();

            DungeonStageBakeResult bake = DungeonStageBaker.Bake(
                definition,
                materialSet,
                settings);
            if (bake == null ||
                bake.BakedPrefab == null ||
                bake.Manifest == null ||
                !bake.ValidationReport.IsValid)
            {
                throw new InvalidOperationException(
                    "UPM Baking did not create a valid persistent stage.");
            }

            ValidateModularDistribution(definition);
            ValidateLegacyStandaloneBoundaries(definition);
            Debug.Log("R9.1 UPM Baking consumer smoke succeeded.");
        }

        // Package Manager 등록 목록에서 기대 ID와 버전을 확인합니다.
        private static void RequireRegisteredPackage(
            string packageId,
            string expectedVersion)
        {
            PackageManagerInfo[] packages =
                PackageManagerInfo.GetAllRegisteredPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                if (!string.Equals(
                        packages[i].name,
                        packageId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!string.Equals(
                        packages[i].version,
                        expectedVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        packageId + " version mismatch: " + packages[i].version);
                }
                return;
            }
            throw new InvalidOperationException(
                "Required UPM package is not registered: " + packageId);
        }

        // 작은 SavedBlueprint Bake에 사용할 결정적 레시피 자산을 생성합니다.
        private static RogueDungeonSettings CreateSettings()
        {
            RogueDungeonSettings settings =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.seed = 91301;
            settings.stageWidthCells = 24;
            settings.stageDepthCells = 24;
            settings.desiredRoomCount = 7;
            settings.generateOnPlay = false;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // Core 생성 API로 Baker 입력용 Blueprint 자산을 생성합니다.
        private static DungeonBlueprintAsset CreateBlueprint(
            RogueDungeonSettings settings)
        {
            DungeonGenerationRequest request =
                DungeonGenerationRequest.Create(
                    settings,
                    settings.seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    "r9.1-upm-baking-consumer");
            DungeonBlueprintGenerationResult generated =
                DungeonBlueprintGenerator.Generate(request);
            DungeonValidationReport validation =
                DungeonBlueprintValidator.Validate(generated.Blueprint);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "Generated Baking consumer Blueprint is invalid.");
            }

            DungeonBlueprintAsset asset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            AssetDatabase.CreateAsset(asset, BlueprintPath);
            asset.Store(generated.Blueprint, request.recipeSnapshot);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // SavedBlueprint RuntimeBuild 상태의 Definition을 Bake 입력으로 생성합니다.
        private static DungeonStageDefinition CreateDefinition(
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            definition.stageId = "r9.1-upm-baked-stage-v1";
            definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
            definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
            definition.savedBlueprint = blueprint;
            definition.missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback;
            definition.loadOnPlay = true;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // UPM Core를 제외한 stage 자산만 `.unitypackage`로 내보내고 sidecar 요구사항을 확인합니다.
        private static void ValidateModularDistribution(
            DungeonStageDefinition definition)
        {
            DungeonDistributionPlan plan =
                DungeonDistributionExporter.PlanBakedStage(definition);
            if (!plan.IsValid)
            {
                throw new InvalidOperationException(
                    "UPM modular Baked Stage plan is invalid: " +
                    Describe(plan.ValidationReport));
            }
            if (!ContainsRequiredPackage(plan, CorePackageId))
            {
                throw new InvalidOperationException(
                    "UPM modular Baked Stage did not declare Runtime Core.");
            }
            for (int i = 0; i < plan.AssetPaths.Count; i++)
            {
                if (plan.AssetPaths[i].StartsWith(
                        "Packages/",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Modular export contains a package-cache asset: " +
                        plan.AssetPaths[i]);
                }
            }

            string outputPath = ResolveOutputPath();
            DungeonDistributionMetadata metadata =
                DungeonDistributionExporter.Export(plan, outputPath);
            if (!File.Exists(outputPath) ||
                !File.Exists(outputPath + ".json") ||
                metadata.packageSha256.Length != 64)
            {
                throw new InvalidOperationException(
                    "UPM modular Baked Stage files are incomplete.");
            }
        }

        // UPM cache를 재포장하는 legacy Core·standalone 계획이 설명 가능한 코드로 차단되는지 확인합니다.
        private static void ValidateLegacyStandaloneBoundaries(
            DungeonStageDefinition definition)
        {
            DungeonDistributionPlan core =
                DungeonDistributionExporter.PlanRuntimeCore();
            DungeonDistributionPlan authoring =
                DungeonDistributionExporter.PlanBakeAuthoring();
            DungeonDistributionPlan standalone =
                DungeonDistributionExporter.PlanBakedStage(definition, true);
            if (core.IsValid ||
                authoring.IsValid ||
                standalone.IsValid ||
                !ContainsIssue(
                    core.ValidationReport,
                    DungeonDistributionValidationCodes.UnityPackageCannotEmbedUpmSource) ||
                !ContainsIssue(
                    authoring.ValidationReport,
                    DungeonDistributionValidationCodes.UnityPackageCannotEmbedUpmSource) ||
                !ContainsIssue(
                    standalone.ValidationReport,
                    DungeonDistributionValidationCodes.UnityPackageCannotEmbedUpmSource))
            {
                throw new InvalidOperationException(
                    "UPM legacy standalone boundary was not enforced.");
            }
        }

        // 배포 계획이 특정 package ID를 요구사항으로 기록했는지 확인합니다.
        private static bool ContainsRequiredPackage(
            DungeonDistributionPlan plan,
            string packageId)
        {
            for (int i = 0; i < plan.RequiredPackages.Count; i++)
            {
                if (string.Equals(
                        plan.RequiredPackages[i].packageId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // 코드 기반 검증 리포트에 지정 문제 코드가 있는지 확인합니다.
        private static bool ContainsIssue(
            DungeonValidationReport report,
            string code)
        {
            if (report == null || report.issues == null) return false;
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue != null &&
                    string.Equals(issue.code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // 실패 로그에 검증 코드와 메시지를 한 줄로 요약합니다.
        private static string Describe(DungeonValidationReport report)
        {
            if (report == null || report.issues == null) return "<null>";
            string result = string.Empty;
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue == null) continue;
                if (result.Length > 0) result += " | ";
                result += issue.code + ": " + issue.message;
            }
            return result;
        }

        // 명령줄에서 E: 검증용 modular package 출력 경로를 읽습니다.
        private static string ResolveOutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        "-rdlOutputPath",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(arguments[i + 1]);
                }
            }
            throw new InvalidOperationException(
                "R9.1 UPM verification requires -rdlOutputPath.");
        }

        // Assets 아래 검증 폴더를 부모부터 생성합니다.
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
