using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests.EditMode
{
    public sealed class DungeonStageOverrideBakeCompatibilityTests
    {
        private const string TempFolder =
            "Assets/RogueDungeonLab/Tests/TempR7OverrideBakeCompatibility";
        private const string RoomId = "room-r7-bake";
        private const string DisabledEnemyId = "r7:enemy:disable";
        private const string KeptEnemyId = "r7:enemy:keep";
        private const string ReplacedCrateId = "r7:crate:replace";
        private const string TransformedPropId = "r7:prop:transform";
        private const string AddedCrateId = "r7:crate:added";

        // 영속 Baker fixture가 사용할 전용 폴더를 빈 상태로 준비합니다.
        [SetUp]
        public void SetUp()
        {
            DeleteTempFolder();
            AssetDatabase.CreateFolder(
                "Assets/RogueDungeonLab/Tests",
                "TempR7OverrideBakeCompatibility");
        }

        // 테스트가 생성한 Bake 산출물과 입력 자산을 전용 폴더 단위로 정리합니다.
        [TearDown]
        public void TearDown()
        {
            DeleteTempFolder();
        }

        // R6 format/builder v1의 빈 Override manifest가 R7 Runtime에서도 검증·로드되고 v1 Override만 계속 거부되는지 검사합니다.
        [Test]
        public void LegacyV1Manifest_LoadsWithoutOverridesAndRejectsOverridePayload()
        {
            DungeonBlueprintAsset sourceAsset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            sourceAsset.Store(CreateBlueprint());
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            FillMaterialSet(materialSet, material);
            GameObject template = new GameObject("R6 Legacy Baked Template");
            template.SetActive(false);
            DungeonBakeManifest manifest =
                ScriptableObject.CreateInstance<DungeonBakeManifest>();
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            DungeonStageOverrides stageOverrides = null;
            GameObject parent = new GameObject("R6 Legacy Load Parent");
            try
            {
                DungeonSceneBuildResult buildResult =
                    CreateBuildResult(sourceAsset.blueprint);
                DungeonBakedStageMetadata metadata =
                    template.AddComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.LegacyV1,
                    DungeonBakeBuilderVersions.LegacyV1,
                    sourceAsset.blueprint.blueprintHash,
                    buildResult);

                manifest.formatVersion = DungeonBakeFormat.LegacyV1;
                manifest.builderVersion =
                    DungeonBakeBuilderVersions.LegacyV1;
                manifest.sourceBlueprint = sourceAsset;
                manifest.sourceOverrides = null;
                manifest.materialSet = materialSet;
                manifest.bakedPrefab = template;
                manifest.sourceBlueprintHash =
                    sourceAsset.blueprint.blueprintHash;
                manifest.finalBlueprintHash =
                    sourceAsset.blueprint.blueprintHash;
                manifest.catalogPlanningHash =
                    sourceAsset.blueprint.catalogPlanningHash;
                manifest.contentRealizationHash =
                    "r6-legacy-realization";
                manifest.gameplayBuildConfigHash =
                    "r6-legacy-gameplay";
                manifest.materialDependencyHash =
                    "r6-legacy-material";
                manifest.overrideHash = string.Empty;
                manifest.ownedArtifacts.Add(
                    new DungeonBakeArtifactRecord
                    {
                        role = "baked-prefab",
                        assetGuid = "r6-legacy-prefab-guid",
                        dependencyHash = "r6-legacy-prefab-dependency"
                    });

                definition.sourceMode =
                    DungeonStageSourceMode.SavedBlueprint;
                definition.buildMode =
                    DungeonStageBuildMode.BakedPrefab;
                definition.savedBlueprint = sourceAsset;
                definition.stageOverrides = null;
                definition.bakedPrefab = template;
                definition.bakeManifest = manifest;

                DungeonValidationReport legacyValidation =
                    DungeonBakeManifestValidator.Validate(
                        manifest,
                        sourceAsset,
                        template);
                DungeonStageInstance loaded =
                    DungeonStageLoader.Load(
                        new DungeonLoadContext(
                            definition,
                            parent.transform));

                Assert.That(legacyValidation.IsValid, Is.True);
                Assert.That(
                    loaded.BuildMode,
                    Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
                Assert.That(loaded.AppliedOverrides, Is.Null);
                Assert.That(loaded.OverrideHash, Is.Empty);
                Assert.That(
                    loaded.FinalBlueprintHash,
                    Is.EqualTo(sourceAsset.blueprint.blueprintHash));

                stageOverrides =
                    CreateOverrides(sourceAsset);
                manifest.sourceOverrides = stageOverrides;
                manifest.overrideHash = stageOverrides.overrideHash;
                DungeonValidationReport unsupported =
                    DungeonBakeManifestValidator.Validate(
                        manifest,
                        sourceAsset,
                        template);
                Assert.That(unsupported.IsValid, Is.False);
                Assert.That(
                    unsupported.ContainsCode(
                        DungeonBakeManifestValidationCodes
                            .UnsupportedOverrideHash),
                    Is.True);
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(template);
                if (stageOverrides != null)
                    Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(materialSet);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sourceAsset);
            }
        }

        // R7 Override를 실제 v2로 Bake한 뒤 RuntimeBuild와 BakedPrefab의 최종 Blueprint·identity·report 계약을 비교합니다.
        [Test]
        public void OverrideBake_CreatesV2ManifestAndPreservesRuntimeBakedParity()
        {
            PersistentFixture fixture = CreatePersistentFixture();
            GameObject runtimeParent =
                new GameObject("R7 Runtime Override Parent");
            GameObject bakedParent =
                new GameObject("R7 Baked Override Parent");
            try
            {
                fixture.Definition.buildMode =
                    DungeonStageBuildMode.RuntimeBuild;
                EditorUtility.SetDirty(fixture.Definition);
                AssetDatabase.SaveAssets();
                DungeonStageInstance runtime =
                    DungeonStageLoader.Load(
                        new DungeonLoadContext(
                            fixture.Definition,
                            runtimeParent.transform,
                            fixture.Settings));

                DungeonStageBakeResult bake =
                    DungeonStageBaker.Bake(
                        fixture.Definition,
                        fixture.MaterialSet,
                        fixture.Settings);
                DungeonStageInstance baked =
                    DungeonStageLoader.Load(
                        new DungeonLoadContext(
                            fixture.Definition,
                            bakedParent.transform));
                DungeonStageOverrideApplyResult expected =
                    DungeonStageOverrideApplier.Apply(
                        fixture.Blueprint,
                        fixture.StageOverrides);

                Assert.That(expected.IsValid, Is.True);
                Assert.That(
                    bake.Manifest.formatVersion,
                    Is.EqualTo(DungeonBakeFormat.StageOverridesV2));
                Assert.That(
                    bake.Manifest.builderVersion,
                    Is.EqualTo(
                        DungeonBakeBuilderVersions.StageOverridesV2));
                Assert.That(
                    bake.Manifest.sourceBlueprint,
                    Is.SameAs(fixture.Blueprint));
                Assert.That(
                    bake.Manifest.sourceOverrides,
                    Is.SameAs(fixture.StageOverrides));
                Assert.That(
                    bake.Manifest.sourceBlueprintHash,
                    Is.EqualTo(expected.SourceBlueprintHash));
                Assert.That(
                    bake.Manifest.overrideHash,
                    Is.EqualTo(expected.OverrideHash));
                Assert.That(
                    bake.Manifest.finalBlueprintHash,
                    Is.EqualTo(expected.FinalBlueprintHash));
                Assert.That(
                    DungeonBakeManifestValidator.Validate(
                        bake.Manifest,
                        fixture.Blueprint,
                        bake.BakedPrefab,
                        fixture.StageOverrides).IsValid,
                    Is.True);
                Assert.That(
                    DungeonStageBaker.ValidateCurrentBake(
                        fixture.Definition).IsValid,
                    Is.True);

                AssertStageInstanceHashes(
                    runtime,
                    expected,
                    fixture.StageOverrides);
                AssertStageInstanceHashes(
                    baked,
                    expected,
                    fixture.StageOverrides);
                Assert.That(
                    runtime.Blueprint.blueprintHash,
                    Is.EqualTo(baked.Blueprint.blueprintHash));
                Assert.That(
                    runtime.Blueprint.blueprintHash,
                    Is.Not.EqualTo(
                        fixture.Blueprint.blueprint.blueprintHash));
                AssertSpawnParity(runtime.Root, baked.Root);
                AssertBuildResultParity(
                    runtime.BuildResult,
                    baked.BuildResult);
                AssertReportParity(runtime.Report, baked.Report);
                Assert.That(
                    runtime.Root.GetComponentInChildren<
                        DungeonGeneratedMeshOwner>(true),
                    Is.Not.Null);
                Assert.That(
                    baked.Root.GetComponentInChildren<
                        DungeonGeneratedMeshOwner>(true),
                    Is.Null);

                Assert.That(
                    FindIdentity(runtime.Root, DisabledEnemyId),
                    Is.Null);
                Assert.That(
                    FindIdentity(baked.Root, DisabledEnemyId),
                    Is.Null);
                Assert.That(
                    FindIdentity(runtime.Root, AddedCrateId),
                    Is.Not.Null);
                Assert.That(
                    FindIdentity(baked.Root, AddedCrateId),
                    Is.Not.Null);
                Assert.That(
                    FindIdentity(
                        baked.Root,
                        ReplacedCrateId).ContentKey,
                    Is.EqualTo(
                        "custom/destructible/replacement"));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(bakedParent.transform);
                DungeonStageLoader.ClearGenerated(runtimeParent.transform);
                Object.DestroyImmediate(bakedParent);
                Object.DestroyImmediate(runtimeParent);
            }
        }

        // Override 변경의 stale 코드와 실패한 v2 재Bake 뒤 이전 산출물·Definition·Override 보존을 검사합니다.
        [Test]
        public void OverrideBake_StaleChangeAndFailedRebakePreservePreviousBake()
        {
            PersistentFixture fixture = CreatePersistentFixture();
            DungeonStageBakeResult first =
                DungeonStageBaker.Bake(
                    fixture.Definition,
                    fixture.MaterialSet,
                    fixture.Settings);
            GameObject previousPrefab = first.BakedPrefab;
            DungeonBakeManifest previousManifest = first.Manifest;
            string previousPrefabPath =
                AssetDatabase.GetAssetPath(previousPrefab);
            string previousManifestPath =
                AssetDatabase.GetAssetPath(previousManifest);
            string bakeRoot =
                Path.GetDirectoryName(first.OutputFolder)
                    .Replace('\\', '/');
            string[] previousVersionFolders =
                AssetDatabase.GetSubFolders(bakeRoot);
            DungeonStageOverrides originalOverrides =
                fixture.Definition.stageOverrides;

            DungeonSpawnTransformOverride transform =
                fixture.StageOverrides.transformOverrides[0];
            transform.yawDegrees += 17f;
            fixture.StageOverrides.RefreshHash();
            string changedOverrideHash =
                fixture.StageOverrides.overrideHash;
            EditorUtility.SetDirty(fixture.StageOverrides);
            AssetDatabase.SaveAssets();

            DungeonValidationReport stale =
                DungeonStageBaker.ValidateCurrentBake(
                    fixture.Definition);
            Assert.That(stale.IsValid, Is.False);
            Assert.That(
                stale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleStageOverrides),
                Is.True);
            Assert.That(
                stale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleFinalBlueprint),
                Is.True);

            Assert.Throws<DungeonStageBakeException>(
                delegate
                {
                    DungeonStageBaker.Bake(
                        fixture.Definition,
                        fixture.MaterialSet,
                        fixture.Settings,
                        new DungeonStageBakeOptions
                        {
                            SimulateFailureBeforeCommit = true
                        });
                });

            Assert.That(
                fixture.Definition.stageOverrides,
                Is.SameAs(originalOverrides));
            Assert.That(
                fixture.StageOverrides.overrideHash,
                Is.EqualTo(changedOverrideHash));
            Assert.That(
                fixture.Definition.bakedPrefab,
                Is.SameAs(previousPrefab));
            Assert.That(
                fixture.Definition.bakeManifest,
                Is.SameAs(previousManifest));
            Assert.That(
                fixture.Definition.buildMode,
                Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
            Assert.That(
                AssetDatabase.GetAssetPath(previousPrefab),
                Is.EqualTo(previousPrefabPath));
            Assert.That(
                AssetDatabase.GetAssetPath(previousManifest),
                Is.EqualTo(previousManifestPath));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    previousPrefabPath),
                Is.SameAs(previousPrefab));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<DungeonBakeManifest>(
                    previousManifestPath),
                Is.SameAs(previousManifest));
            Assert.That(
                AssetDatabase.GetSubFolders(bakeRoot),
                Is.EqualTo(previousVersionFolders));

            DungeonValidationReport stillStale =
                DungeonStageBaker.ValidateCurrentBake(
                    fixture.Definition);
            Assert.That(
                stillStale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleStageOverrides),
                Is.True);
            Assert.That(
                stillStale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleFinalBlueprint),
                Is.True);
        }

        // 전용 테스트 폴더가 있으면 Baker 파생 폴더까지 함께 삭제합니다.
        private static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Blueprint·Override·Settings·MaterialSet·Definition을 모두 영속 프로젝트 자산으로 구성합니다.
        private static PersistentFixture CreatePersistentFixture()
        {
            DungeonBlueprintAsset blueprintAsset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            blueprintAsset.Store(CreateBlueprint());
            AssetDatabase.CreateAsset(
                blueprintAsset,
                TempFolder + "/R7Blueprint.asset");

            DungeonStageOverrides stageOverrides =
                CreateOverrides(blueprintAsset);
            AssetDatabase.CreateAsset(
                stageOverrides,
                TempFolder + "/R7Overrides.asset");

            WeightedDropTable enemyDrops =
                CreateDropTable("R7EnemyDrops", "EnemyToken");
            WeightedDropTable destructibleDrops =
                CreateDropTable(
                    "R7DestructibleDrops",
                    "CrateToken");
            RogueDungeonSettings settings =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.enemyDropTable = enemyDrops;
            settings.destructibleDropTable = destructibleDrops;
            AssetDatabase.CreateAsset(
                settings,
                TempFolder + "/R7Settings.asset");

            DungeonBakeMaterialSet materialSet =
                DungeonStageBaker.CreateDefaultMaterialSetAsset(
                    TempFolder + "/R7Materials.asset");
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            definition.sourceMode =
                DungeonStageSourceMode.SavedBlueprint;
            definition.buildMode =
                DungeonStageBuildMode.RuntimeBuild;
            definition.savedBlueprint = blueprintAsset;
            definition.stageOverrides = stageOverrides;
            definition.missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback;
            AssetDatabase.CreateAsset(
                definition,
                TempFolder + "/R7Stage.asset");
            EditorUtility.SetDirty(stageOverrides);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new PersistentFixture
            {
                Blueprint = blueprintAsset,
                StageOverrides = stageOverrides,
                Settings = settings,
                MaterialSet = materialSet,
                Definition = definition
            };
        }

        // 보장 드랍 하나를 가진 영속 DropTable 자산을 만듭니다.
        private static WeightedDropTable CreateDropTable(
            string assetName,
            string itemId)
        {
            WeightedDropTable table =
                ScriptableObject.CreateInstance<WeightedDropTable>();
            table.entries.Add(
                new DropEntry
                {
                    itemId = itemId,
                    weight = 1f,
                    minQuantity = 1,
                    maxQuantity = 1
                });
            AssetDatabase.CreateAsset(
                table,
                TempFolder + "/" + assetName + ".asset");
            return table;
        }

        // 4x3 연결 floor와 각 Override 종류의 대상 spawn을 가진 Blueprint를 만듭니다.
        private static DungeonBlueprint CreateBlueprint()
        {
            DungeonBlueprint blueprint = new DungeonBlueprint
            {
                formatVersion = DungeonBlueprintFormat.CurrentVersion,
                generatorVersion = DungeonGeneratorVersions.LegacyV1,
                seed = 71717,
                recipeHash = "r7-bake-recipe",
                catalogPlanningHash =
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                grid = new DungeonGridRecord
                {
                    width = 4,
                    depth = 3,
                    cellSize = 3f,
                    wallHeight = 3.2f
                },
                entrance = new Vector2Int(0, 0),
                exit = new Vector2Int(3, 2),
                rooms = new List<DungeonRoomRecord>
                {
                    new DungeonRoomRecord
                    {
                        roomId = RoomId,
                        bounds = new RectInt(0, 0, 4, 3)
                    }
                }
            };
            for (int z = 0; z < 3; z++)
            for (int x = 0; x < 4; x++)
            {
                blueprint.cells.Add(
                    new DungeonCellRecord
                    {
                        coordinate = new Vector2Int(x, z),
                        flags = DungeonCellFlags.Floor,
                        roomId = RoomId,
                        distanceFromEntrance = x + z
                    });
            }

            blueprint.spawns.Add(
                CreateSpawn(
                    "r7:marker:entrance",
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.EntranceMarker,
                    blueprint.entrance,
                    1));
            blueprint.spawns.Add(
                CreateSpawn(
                    "r7:marker:exit",
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.ExitMarker,
                    blueprint.exit,
                    2));
            blueprint.spawns.Add(
                CreateSpawn(
                    DisabledEnemyId,
                    DungeonSpawnCategory.Enemy,
                    DungeonBuiltInContentKeys.Enemy,
                    new Vector2Int(1, 0),
                    101));
            blueprint.spawns.Add(
                CreateSpawn(
                    KeptEnemyId,
                    DungeonSpawnCategory.Enemy,
                    DungeonBuiltInContentKeys.Enemy,
                    new Vector2Int(2, 0),
                    102));
            blueprint.spawns.Add(
                CreateSpawn(
                    ReplacedCrateId,
                    DungeonSpawnCategory.Destructible,
                    DungeonBuiltInContentKeys.Destructible,
                    new Vector2Int(1, 1),
                    201));
            blueprint.spawns.Add(
                CreateSpawn(
                    TransformedPropId,
                    DungeonSpawnCategory.Prop,
                    DungeonBuiltInContentKeys.PropCube,
                    new Vector2Int(2, 1),
                    301));
            blueprint.RefreshHash();
            Assert.That(
                DungeonBlueprintValidator.Validate(blueprint).IsValid,
                Is.True);
            return blueprint;
        }

        // 지정 ID·범주·셀을 가진 유효 spawn 레코드를 만듭니다.
        private static DungeonSpawnRecord CreateSpawn(
            string spawnId,
            DungeonSpawnCategory category,
            string contentKey,
            Vector2Int cell,
            int variantSeed)
        {
            return new DungeonSpawnRecord
            {
                spawnId = spawnId,
                category = category,
                contentKey = contentKey,
                instanceName = spawnId,
                cell = cell,
                localPosition = new Vector3(
                    cell.x * 3f,
                    category == DungeonSpawnCategory.Marker
                        ? 0.08f
                        : 0.5f,
                    cell.y * 3f),
                localScale = Vector3.one,
                roomId = RoomId,
                progression = (cell.x + cell.y) / 5f,
                tags = new List<string>(),
                variantSeed = variantSeed
            };
        }

        // Disable·Add·Replace·Transform을 가진 유효 Stage Override를 만듭니다.
        private static DungeonStageOverrides CreateOverrides(
            DungeonBlueprintAsset blueprintAsset)
        {
            DungeonStageOverrides stageOverrides =
                ScriptableObject.CreateInstance<DungeonStageOverrides>();
            stageOverrides.baseBlueprint = blueprintAsset;
            stageOverrides.baseBlueprintHash =
                blueprintAsset.blueprint.blueprintHash;
            stageOverrides.disabledSpawns.Add(
                new DungeonSpawnDisableOverride
                {
                    recordId = "disable-enemy",
                    binding = DungeonSpawnBindingSnapshot.Capture(
                        FindSpawn(
                            blueprintAsset.blueprint,
                            DisabledEnemyId))
                });
            stageOverrides.addedSpawns.Add(
                CreateSpawn(
                    AddedCrateId,
                    DungeonSpawnCategory.Destructible,
                    DungeonBuiltInContentKeys.Destructible,
                    new Vector2Int(2, 2),
                    702));
            stageOverrides.contentOverrides.Add(
                new DungeonSpawnContentOverride
                {
                    recordId = "replace-crate",
                    binding = DungeonSpawnBindingSnapshot.Capture(
                        FindSpawn(
                            blueprintAsset.blueprint,
                            ReplacedCrateId)),
                    replacementContentKey =
                        "custom/destructible/replacement"
                });
            stageOverrides.transformOverrides.Add(
                new DungeonSpawnTransformOverride
                {
                    recordId = "transform-prop",
                    binding = DungeonSpawnBindingSnapshot.Capture(
                        FindSpawn(
                            blueprintAsset.blueprint,
                            TransformedPropId)),
                    localPosition =
                        new Vector3(6.4f, 0.8f, 3.2f),
                    pitchDegrees = 8f,
                    yawDegrees = 40f,
                    rollDegrees = -3f,
                    localScale =
                        new Vector3(1.2f, 1.4f, 0.9f)
                });
            stageOverrides.RefreshHash();
            return stageOverrides;
        }

        // Blueprint에서 지정 stable ID의 spawn을 찾고 fixture 오류를 즉시 드러냅니다.
        private static DungeonSpawnRecord FindSpawn(
            DungeonBlueprint blueprint,
            string spawnId)
        {
            DungeonSpawnRecord spawn = blueprint.spawns.Find(
                value =>
                    value != null &&
                    string.Equals(
                        value.spawnId,
                        spawnId,
                        StringComparison.Ordinal));
            Assert.That(spawn, Is.Not.Null);
            return spawn;
        }

        // Blueprint spawn 수에서 Baked metadata용 category count를 계산합니다.
        private static DungeonSceneBuildResult CreateBuildResult(
            DungeonBlueprint blueprint)
        {
            ContentSpawnCounts counts = new ContentSpawnCounts();
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn == null) continue;
                switch (spawn.category)
                {
                    case DungeonSpawnCategory.Enemy:
                        counts.EnemyCount++;
                        break;
                    case DungeonSpawnCategory.Destructible:
                        counts.DestructibleCount++;
                        break;
                    case DungeonSpawnCategory.Prop:
                        counts.PropCount++;
                        break;
                    case DungeonSpawnCategory.Gimmick:
                        counts.GimmickCount++;
                        break;
                }
            }
            return new DungeonSceneBuildResult
            {
                ContentCounts = counts,
                ValidationReport = new DungeonValidationReport()
            };
        }

        // 하나의 Material을 Runtime manifest가 요구하는 여덟 슬롯에 배치합니다.
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

        // StageInstance가 같은 source·Override·최종 hash를 노출하는지 검사합니다.
        private static void AssertStageInstanceHashes(
            DungeonStageInstance instance,
            DungeonStageOverrideApplyResult expected,
            DungeonStageOverrides stageOverrides)
        {
            Assert.That(instance.AppliedOverrides, Is.SameAs(stageOverrides));
            Assert.That(
                instance.SourceBlueprintHash,
                Is.EqualTo(expected.SourceBlueprintHash));
            Assert.That(
                instance.OverrideHash,
                Is.EqualTo(expected.OverrideHash));
            Assert.That(
                instance.FinalBlueprintHash,
                Is.EqualTo(expected.FinalBlueprintHash));
        }

        // stable ID별 identity·transform과 클릭 대상 설정이 두 구축 모드에서 같은지 검사합니다.
        private static void AssertSpawnParity(
            GameObject runtimeRoot,
            GameObject bakedRoot)
        {
            Dictionary<string, DungeonSpawnIdentity> runtime =
                BuildIdentityLookup(runtimeRoot);
            Dictionary<string, DungeonSpawnIdentity> baked =
                BuildIdentityLookup(bakedRoot);
            Assert.That(baked.Count, Is.EqualTo(runtime.Count));
            foreach (
                KeyValuePair<string, DungeonSpawnIdentity> pair
                in runtime)
            {
                DungeonSpawnIdentity bakedIdentity;
                Assert.That(
                    baked.TryGetValue(pair.Key, out bakedIdentity),
                    Is.True,
                    pair.Key);
                DungeonSpawnIdentity runtimeIdentity = pair.Value;
                Assert.That(
                    bakedIdentity.ContentKey,
                    Is.EqualTo(runtimeIdentity.ContentKey));
                Assert.That(
                    bakedIdentity.Category,
                    Is.EqualTo(runtimeIdentity.Category));
                Assert.That(
                    bakedIdentity.Cell,
                    Is.EqualTo(runtimeIdentity.Cell));
                Assert.That(
                    Vector3.Distance(
                        bakedIdentity.transform.localPosition,
                        runtimeIdentity.transform.localPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        bakedIdentity.transform.localRotation,
                        runtimeIdentity.transform.localRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(
                        bakedIdentity.transform.localScale,
                        runtimeIdentity.transform.localScale),
                    Is.LessThan(0.0001f));
                AssertDropTargetParity(
                    runtimeIdentity,
                    bakedIdentity);
            }
        }

        // 한 구축 root의 identity를 중복 없는 ordinal stable ID lookup으로 만듭니다.
        private static Dictionary<string, DungeonSpawnIdentity>
            BuildIdentityLookup(GameObject root)
        {
            Dictionary<string, DungeonSpawnIdentity> result =
                new Dictionary<string, DungeonSpawnIdentity>(
                    StringComparer.Ordinal);
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity = identities[i];
                Assert.That(identity.SpawnId, Is.Not.Empty);
                Assert.That(result.ContainsKey(identity.SpawnId), Is.False);
                result.Add(identity.SpawnId, identity);
            }
            return result;
        }

        // 지정 stable ID의 identity를 root에서 찾고 없으면 null을 반환합니다.
        private static DungeonSpawnIdentity FindIdentity(
            GameObject root,
            string spawnId)
        {
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                if (string.Equals(
                        identities[i].SpawnId,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return identities[i];
                }
            }
            return null;
        }

        // 파괴 가능한 대상의 ID·종류·영속 DropTable 참조가 두 구축 모드에서 같은지 검사합니다.
        private static void AssertDropTargetParity(
            DungeonSpawnIdentity runtimeIdentity,
            DungeonSpawnIdentity bakedIdentity)
        {
            DestructibleDropTarget runtimeTarget =
                runtimeIdentity.GetComponentInChildren<
                    DestructibleDropTarget>(true);
            DestructibleDropTarget bakedTarget =
                bakedIdentity.GetComponentInChildren<
                    DestructibleDropTarget>(true);
            Assert.That(
                bakedTarget == null,
                Is.EqualTo(runtimeTarget == null));
            if (runtimeTarget == null) return;
            Assert.That(
                bakedTarget.TargetId,
                Is.EqualTo(runtimeTarget.TargetId));
            Assert.That(
                bakedTarget.SourceKind,
                Is.EqualTo(runtimeTarget.SourceKind));
            Assert.That(
                bakedTarget.DropTable,
                Is.SameAs(runtimeTarget.DropTable));
        }

        // 구축 통계의 category와 resolver 결과 수가 두 모드에서 같은지 검사합니다.
        private static void AssertBuildResultParity(
            DungeonSceneBuildResult runtime,
            DungeonSceneBuildResult baked)
        {
            Assert.That(
                baked.ContentCounts.EnemyCount,
                Is.EqualTo(runtime.ContentCounts.EnemyCount));
            Assert.That(
                baked.ContentCounts.DestructibleCount,
                Is.EqualTo(runtime.ContentCounts.DestructibleCount));
            Assert.That(
                baked.ContentCounts.PropCount,
                Is.EqualTo(runtime.ContentCounts.PropCount));
            Assert.That(
                baked.ContentCounts.GimmickCount,
                Is.EqualTo(runtime.ContentCounts.GimmickCount));
            Assert.That(
                baked.ResolvedContentCount,
                Is.EqualTo(runtime.ResolvedContentCount));
            Assert.That(
                baked.BuiltInFallbackCount,
                Is.EqualTo(runtime.BuiltInFallbackCount));
            Assert.That(
                baked.SkippedContentCount,
                Is.EqualTo(runtime.SkippedContentCount));
        }

        // 실행 시간만 제외한 기존 GenerationReport 호환 필드를 비교합니다.
        private static void AssertReportParity(
            GenerationReport runtime,
            GenerationReport baked)
        {
            Assert.That(baked.activeSeed, Is.EqualTo(runtime.activeSeed));
            Assert.That(baked.roomCount, Is.EqualTo(runtime.roomCount));
            Assert.That(
                baked.floorCellCount,
                Is.EqualTo(runtime.floorCellCount));
            Assert.That(baked.enemyCount, Is.EqualTo(runtime.enemyCount));
            Assert.That(
                baked.destructibleCount,
                Is.EqualTo(runtime.destructibleCount));
            Assert.That(baked.propCount, Is.EqualTo(runtime.propCount));
            Assert.That(
                baked.gimmickCount,
                Is.EqualTo(runtime.gimmickCount));
            Assert.That(
                baked.meshTriangleCount,
                Is.EqualTo(runtime.meshTriangleCount));
            Assert.That(
                baked.worldBounds.center,
                Is.EqualTo(runtime.worldBounds.center));
            Assert.That(
                baked.worldBounds.size,
                Is.EqualTo(runtime.worldBounds.size));
            Assert.That(baked.warnings, Is.EqualTo(runtime.warnings));
        }

        private sealed class PersistentFixture
        {
            public DungeonBlueprintAsset Blueprint;
            public DungeonStageOverrides StageOverrides;
            public RogueDungeonSettings Settings;
            public DungeonBakeMaterialSet MaterialSet;
            public DungeonStageDefinition Definition;
        }
    }
}
