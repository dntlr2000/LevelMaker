using System;
using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonBakedStageLoaderTests
    {
        // BakedPrefab 로드가 resolver를 호출하지 않고 저장 통계·Blueprint·Prefab 계층을 복원하는지 검사합니다.
        [Test]
        public void Load_BakedPrefabInstantiatesOnlyPrefabAndRestoresMetadata()
        {
            BakedFixture fixture = new BakedFixture();
            GameObject parent = new GameObject("R6 Baked Parent");
            try
            {
                ThrowingResolver resolver = new ThrowingResolver();
                DungeonLoadContext context =
                    new DungeonLoadContext(fixture.Definition, parent.transform)
                    {
                        ContentResolver = resolver,
                        RequestId = "r6-baked-load"
                    };

                DungeonStageInstance instance = DungeonStageLoader.Load(context);

                Assert.That(resolver.CallCount, Is.Zero);
                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(instance.BuildMode, Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
                Assert.That(instance.Blueprint, Is.Not.SameAs(fixture.BlueprintAsset.blueprint));
                Assert.That(
                    instance.Blueprint.blueprintHash,
                    Is.EqualTo(fixture.BlueprintAsset.blueprint.blueprintHash));
                Assert.That(instance.Root.name, Is.EqualTo(DungeonStageLoader.GeneratedRootName));
                Assert.That(instance.Root.transform.Find("Baked Sentinel"), Is.Not.Null);
                Assert.That(instance.BuildResult.MeshTriangleCount, Is.EqualTo(321));
                Assert.That(instance.BuildResult.ContentCounts.EnemyCount, Is.EqualTo(2));
                Assert.That(instance.BuildResult.ContentCounts.DestructibleCount, Is.EqualTo(3));
                Assert.That(instance.BuildResult.ResolvedContentCount, Is.EqualTo(4));
                Assert.That(instance.Report.meshTriangleCount, Is.EqualTo(321));
                Assert.That(instance.Report.enemyCount, Is.EqualTo(2));
                Assert.That(instance.Report.warnings, Has.Some.Contains("RDL-TEST-WARNING"));
                Assert.That(instance.RequestId, Is.EqualTo("r6-baked-load"));
                Assert.That(instance.RuntimeSettings, Is.SameAs(fixture.Settings));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                UnityEngine.Object.DestroyImmediate(parent);
                fixture.Dispose();
            }
        }

        // Procedural+BakedPrefab 조합이 생성기나 Prefab 복제 전에 명시적인 build mode 오류로 거부되는지 검사합니다.
        [Test]
        public void Load_RejectsProceduralBakedMode()
        {
            BakedFixture fixture = new BakedFixture();
            GameObject parent = new GameObject("R6 Procedural Baked Parent");
            try
            {
                fixture.Definition.sourceMode = DungeonStageSourceMode.Procedural;
                fixture.Definition.recipe = fixture.Settings;

                DungeonStageLoadException exception =
                    Assert.Throws<DungeonStageLoadException>(delegate
                    {
                        DungeonStageLoader.Load(
                            new DungeonLoadContext(
                                fixture.Definition,
                                parent.transform));
                    });

                Assert.That(
                    exception.ValidationReport.ContainsCode(
                        DungeonStageDefinitionValidationCodes.UnsupportedBuildMode),
                    Is.True);
                Assert.That(
                    parent.transform.Find(DungeonStageLoader.GeneratedRootName),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                fixture.Dispose();
            }
        }

        // Prefab 루트 metadata 누락과 manifest Prefab 불일치가 안정적인 검증 코드로 차단되는지 검사합니다.
        [Test]
        public void Load_RejectsMissingMetadataAndManifestMismatch()
        {
            BakedFixture fixture = new BakedFixture();
            GameObject parent = new GameObject("R6 Invalid Baked Parent");
            GameObject otherPrefab = new GameObject("Other Baked Prefab");
            try
            {
                UnityEngine.Object.DestroyImmediate(
                    fixture.Template.GetComponent<DungeonBakedStageMetadata>());
                DungeonStageLoadException missingMetadata =
                    Assert.Throws<DungeonStageLoadException>(delegate
                    {
                        DungeonStageLoader.Load(
                            new DungeonLoadContext(
                                fixture.Definition,
                                parent.transform));
                    });
                Assert.That(
                    missingMetadata.ValidationReport.ContainsCode(
                        DungeonStageDefinitionValidationCodes.MissingBakedMetadata),
                    Is.True);

                DungeonBakedStageMetadata metadata =
                    fixture.Template.AddComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.Current,
                    DungeonBakeBuilderVersions.Current,
                    fixture.BlueprintAsset.blueprint.blueprintHash,
                    fixture.BuildResult);
                fixture.Manifest.bakedPrefab = otherPrefab;
                DungeonStageLoadException mismatch =
                    Assert.Throws<DungeonStageLoadException>(delegate
                    {
                        DungeonStageLoader.Load(
                            new DungeonLoadContext(
                                fixture.Definition,
                                parent.transform));
                    });
                Assert.That(
                    mismatch.ValidationReport.ContainsCode(
                        DungeonBakeManifestValidationCodes.ExpectedPrefabMismatch),
                    Is.True);
                Assert.That(
                    parent.transform.Find(DungeonStageLoader.GeneratedRootName),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherPrefab);
                UnityEngine.Object.DestroyImmediate(parent);
                fixture.Dispose();
            }
        }

        // 잘못된 재로드가 기존 generated root를 보존하고 성공한 재로드만 단일 root로 교체하는지 검사합니다.
        [Test]
        public void Load_BakedFailurePreservesExistingGeneratedRoot()
        {
            BakedFixture fixture = new BakedFixture();
            GameObject parent = new GameObject("R6 Baked Rollback Parent");
            try
            {
                DungeonStageInstance first = DungeonStageLoader.Load(
                    new DungeonLoadContext(fixture.Definition, parent.transform));
                GameObject firstRoot = first.Root;
                DungeonBakedStageMetadata metadata =
                    fixture.Template.GetComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.Current,
                    DungeonBakeBuilderVersions.Current,
                    "stale-final-blueprint",
                    fixture.BuildResult);

                DungeonStageLoadException exception =
                    Assert.Throws<DungeonStageLoadException>(delegate
                    {
                        DungeonStageLoader.Load(
                            new DungeonLoadContext(
                                fixture.Definition,
                                parent.transform));
                    });

                Assert.That(
                    exception.ValidationReport.ContainsCode(
                        DungeonBakedStageMetadataValidationCodes.FinalBlueprintHashMismatch),
                    Is.True);
                Assert.That(firstRoot == null, Is.False);
                Assert.That(
                    parent.transform.Find(DungeonStageLoader.GeneratedRootName).gameObject,
                    Is.SameAs(firstRoot));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                UnityEngine.Object.DestroyImmediate(parent);
                fixture.Dispose();
            }
        }

        private sealed class ThrowingResolver : IDungeonContentResolver
        {
            public int CallCount { get; private set; }

            // Baked 경로에서 호출되면 즉시 테스트를 실패시키도록 resolver 접근을 기록합니다.
            public bool TryResolve(
                DungeonSpawnRecord record,
                out DungeonContentResolution resolution)
            {
                CallCount++;
                resolution = null;
                throw new InvalidOperationException(
                    "BakedPrefab load must not call a content resolver.");
            }
        }

        private sealed class BakedFixture : IDisposable
        {
            public RogueDungeonSettings Settings { get; private set; }
            public DungeonBlueprintAsset BlueprintAsset { get; private set; }
            public DungeonBakeMaterialSet MaterialSet { get; private set; }
            public Material Material { get; private set; }
            public GameObject Template { get; private set; }
            public DungeonBakeManifest Manifest { get; private set; }
            public DungeonStageDefinition Definition { get; private set; }
            public DungeonSceneBuildResult BuildResult { get; private set; }

            // 완전한 in-memory SavedBlueprint+BakedPrefab 계약을 테스트마다 새로 구성합니다.
            public BakedFixture()
            {
                Settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
                Settings.ApplyPreset(DungeonPreset.Compact);
                BlueprintAsset =
                    ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
                BlueprintAsset.Store(
                    DungeonBlueprintGenerator.Generate(
                        DungeonGenerationRequest.Create(
                            Settings,
                            60601,
                            DungeonGeneratorVersions.LegacyV1,
                            DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                            "r6-baked-fixture")).Blueprint);

                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                Assert.That(shader, Is.Not.Null);
                Material = new Material(shader);
                MaterialSet =
                    ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
                FillMaterialSet(MaterialSet, Material);

                DungeonValidationReport buildValidation =
                    new DungeonValidationReport();
                buildValidation.Add(
                    "RDL-TEST-WARNING",
                    DungeonValidationSeverity.Warning,
                    "Serialized Baker warning.");
                BuildResult = new DungeonSceneBuildResult
                {
                    MeshTriangleCount = 321,
                    ContentCounts = new ContentSpawnCounts
                    {
                        EnemyCount = 2,
                        DestructibleCount = 3,
                        PropCount = 5,
                        GimmickCount = 1
                    },
                    ResolvedContentCount = 4,
                    BuiltInFallbackCount = 7,
                    SkippedContentCount = 1,
                    ValidationReport = buildValidation
                };

                Template = new GameObject("R6 Baked Template");
                Template.SetActive(false);
                new GameObject("Baked Sentinel").transform.SetParent(
                    Template.transform,
                    false);
                DungeonBakedStageMetadata metadata =
                    Template.AddComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.Current,
                    DungeonBakeBuilderVersions.Current,
                    BlueprintAsset.blueprint.blueprintHash,
                    BuildResult);

                Manifest = ScriptableObject.CreateInstance<DungeonBakeManifest>();
                Manifest.sourceBlueprint = BlueprintAsset;
                Manifest.sourceRuntimeSettings = Settings;
                Manifest.materialSet = MaterialSet;
                Manifest.bakedPrefab = Template;
                Manifest.sourceBlueprintHash =
                    BlueprintAsset.blueprint.blueprintHash;
                Manifest.finalBlueprintHash =
                    BlueprintAsset.blueprint.blueprintHash;
                Manifest.catalogPlanningHash =
                    BlueprintAsset.blueprint.catalogPlanningHash;
                Manifest.contentRealizationHash = "r6-test-realization";
                Manifest.gameplayBuildConfigHash = "r6-test-gameplay";
                Manifest.materialDependencyHash = "r6-test-material";
                Manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "prefab",
                    assetGuid = "r6-test-prefab-guid",
                    dependencyHash = "r6-test-prefab-dependency"
                });

                Definition =
                    ScriptableObject.CreateInstance<DungeonStageDefinition>();
                Definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                Definition.buildMode = DungeonStageBuildMode.BakedPrefab;
                Definition.savedBlueprint = BlueprintAsset;
                Definition.bakedPrefab = Template;
                Definition.bakeManifest = Manifest;
            }

            // Fixture가 만든 UnityEngine.Object를 의존 역순으로 정리합니다.
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Definition);
                UnityEngine.Object.DestroyImmediate(Manifest);
                UnityEngine.Object.DestroyImmediate(Template);
                UnityEngine.Object.DestroyImmediate(MaterialSet);
                UnityEngine.Object.DestroyImmediate(Material);
                UnityEngine.Object.DestroyImmediate(BlueprintAsset);
                UnityEngine.Object.DestroyImmediate(Settings);
            }

            // 하나의 임시 Material을 모든 R6 Bake 필수 슬롯에 배치합니다.
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
}
