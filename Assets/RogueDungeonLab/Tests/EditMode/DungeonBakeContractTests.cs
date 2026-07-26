using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonBakeContractTests
    {
        // Runtime-safe manifest가 완전한 hash 계약을 승인하고 원본 변경을 stale 오류로 감지하는지 검사합니다.
        [Test]
        public void BakeManifestValidator_AcceptsCompleteContractAndDetectsStaleSource()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset source = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonBakeManifest manifest = ScriptableObject.CreateInstance<DungeonBakeManifest>();
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            Material testMaterial = null;
            GameObject bakedPrefab = new GameObject("R5.2 Baked Contract Prefab");
            try
            {
                testMaterial = CreateTestMaterial();
                FillMaterialSet(materialSet, testMaterial);
                DungeonGenerationRequest request = DungeonGenerationRequest.Create(
                    settings,
                    5206,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    "R5.2-bake-contract");
                DungeonBlueprint blueprint =
                    DungeonBlueprintGenerator.Generate(request).Blueprint;
                source.Store(blueprint);

                manifest.sourceBlueprint = source;
                manifest.bakedPrefab = bakedPrefab;
                manifest.materialSet = materialSet;
                manifest.sourceBlueprintHash = source.blueprint.blueprintHash;
                manifest.finalBlueprintHash = source.blueprint.blueprintHash;
                manifest.catalogPlanningHash = source.blueprint.catalogPlanningHash;
                manifest.contentRealizationHash = "realization-v1";
                manifest.gameplayBuildConfigHash = "gameplay-v1";
                manifest.materialDependencyHash = "materials-v1";
                manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "prefab",
                    assetGuid = "test-prefab-guid",
                    dependencyHash = "test-prefab-dependency"
                });

                DungeonValidationReport valid =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(valid.IsValid, Is.True, FormatIssues(valid));

                manifest.materialSet = null;
                DungeonValidationReport missingMaterials =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    missingMaterials.ContainsCode(
                        DungeonBakeManifestValidationCodes.MissingMaterialSet),
                    Is.True);
                manifest.materialSet = materialSet;

                materialSet.floor = null;
                DungeonValidationReport incompleteMaterials =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    incompleteMaterials.ContainsCode(
                        DungeonBakeManifestValidationCodes.IncompleteMaterialSet),
                    Is.True);
                materialSet.floor = testMaterial;

                manifest.ownedArtifacts.Clear();
                DungeonValidationReport missingArtifacts =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    missingArtifacts.ContainsCode(
                        DungeonBakeManifestValidationCodes.MissingOwnedArtifacts),
                    Is.True);
                manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "prefab",
                    assetGuid = "test-prefab-guid",
                    dependencyHash = "test-prefab-dependency"
                });
                manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "duplicate",
                    assetGuid = "test-prefab-guid",
                    dependencyHash = "test-prefab-dependency"
                });
                DungeonValidationReport duplicateArtifacts =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    duplicateArtifacts.ContainsCode(
                        DungeonBakeManifestValidationCodes.DuplicateOwnedArtifact),
                    Is.True);
                manifest.ownedArtifacts.RemoveAt(manifest.ownedArtifacts.Count - 1);

                manifest.overrideHash = "unsupported-r6-override";
                manifest.finalBlueprintHash = "different-final-blueprint";
                DungeonValidationReport unsupportedOverride =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    unsupportedOverride.ContainsCode(
                        DungeonBakeManifestValidationCodes.UnsupportedOverrideHash),
                    Is.True);
                Assert.That(
                    unsupportedOverride.ContainsCode(
                        DungeonBakeManifestValidationCodes.FinalBlueprintHashMismatch),
                    Is.True);
                manifest.overrideHash = string.Empty;
                manifest.finalBlueprintHash = source.blueprint.blueprintHash;

                source.blueprint.seed++;
                source.blueprint.RefreshHash();
                DungeonValidationReport stale =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    stale.ContainsCode(
                        DungeonBakeManifestValidationCodes.SourceBlueprintHashMismatch),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(bakedPrefab);
                Object.DestroyImmediate(testMaterial);
                Object.DestroyImmediate(materialSet);
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(settings);
            }
        }

        // custom planning hash를 가진 Blueprint가 원본 Catalog 참조 없이 최신 Bake로 승인되지 않는지 검사합니다.
        [Test]
        public void BakeManifestValidator_RequiresSourceCatalogForCustomPlanningHash()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonBlueprintAsset source = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonBakeManifest manifest = ScriptableObject.CreateInstance<DungeonBakeManifest>();
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            Material testMaterial = null;
            GameObject bakedPrefab = new GameObject("R5.2 Custom Catalog Baked Prefab");
            try
            {
                testMaterial = CreateTestMaterial();
                FillMaterialSet(materialSet, testMaterial);
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(
                        settings,
                        5207,
                        catalog,
                        "R5.2-custom-catalog-bake-contract")).Blueprint;
                source.Store(blueprint);

                manifest.sourceBlueprint = source;
                manifest.bakedPrefab = bakedPrefab;
                manifest.materialSet = materialSet;
                manifest.sourceBlueprintHash = blueprint.blueprintHash;
                manifest.finalBlueprintHash = blueprint.blueprintHash;
                manifest.catalogPlanningHash = blueprint.catalogPlanningHash;
                manifest.contentRealizationHash = "realization-v1";
                manifest.gameplayBuildConfigHash = "gameplay-v1";
                manifest.materialDependencyHash = "materials-v1";
                manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "prefab",
                    assetGuid = "test-custom-prefab-guid",
                    dependencyHash = "test-custom-prefab-dependency"
                });

                DungeonValidationReport missingCatalog =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(
                    missingCatalog.ContainsCode(
                        DungeonBakeManifestValidationCodes.MissingSourceCatalog),
                    Is.True);

                manifest.sourceCatalog = catalog;
                DungeonValidationReport valid =
                    DungeonBakeManifestValidator.Validate(manifest, source, bakedPrefab);
                Assert.That(valid.IsValid, Is.True, FormatIssues(valid));
            }
            finally
            {
                Object.DestroyImmediate(bakedPrefab);
                Object.DestroyImmediate(testMaterial);
                Object.DestroyImmediate(materialSet);
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // R6에서도 절차 소스를 직접 BakedPrefab으로 로드하지 못하고 먼저 Blueprint 저장을 요구하는지 검사합니다.
        [Test]
        public void StageDefinitionValidator_RejectsProceduralBakedMode()
        {
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            RogueDungeonSettings recipe =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            try
            {
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.BakedPrefab;
                definition.recipe = recipe;

                DungeonValidationReport report =
                    DungeonStageDefinitionValidator.Validate(definition);
                Assert.That(
                    report.ContainsCode(
                        DungeonStageDefinitionValidationCodes.UnsupportedBuildMode),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(definition);
            }
        }

        // 검증 리포트의 코드와 메시지를 테스트 실패 문구로 정리합니다.
        private static string FormatIssues(DungeonValidationReport report)
        {
            if (report == null || report.issues == null) return "No validation report.";
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue == null) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(issue.code).Append(": ").Append(issue.message);
            }
            return builder.ToString();
        }

        // Unity 자산 저장 여부와 독립적으로 재질 슬롯 계약을 검사할 임시 Material을 만듭니다.
        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        // 하나의 임시 Material을 R6 MVP의 모든 필수 Bake 재질 슬롯에 채웁니다.
        private static void FillMaterialSet(
            DungeonBakeMaterialSet materialSet,
            Material material)
        {
            materialSet.floor = material;
            materialSet.wall = material;
            materialSet.enemy = material;
            materialSet.destructible = material;
            materialSet.prop = material;
            materialSet.gimmick = material;
            materialSet.entrance = material;
            materialSet.exit = material;
        }
    }
}
