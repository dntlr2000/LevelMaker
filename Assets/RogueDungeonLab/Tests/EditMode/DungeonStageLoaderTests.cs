using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonStageLoaderTests
    {
        // Procedural 시드가 explicit, run, fixed, random 순서로 선택되고 provider가 필요한 때만 호출되는지 검사합니다.
        [Test]
        public void SeedResolver_FollowsExplicitRunFixedAndRandomPolicies()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject parent = new GameObject("R3 Seed Parent");
            try
            {
                definition.recipe = settings;
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.seedPolicy = DungeonStageSeedPolicy.RunSeed;
                DungeonLoadContext context = new DungeonLoadContext(definition, parent.transform, settings)
                {
                    ExplicitSeed = 333,
                    RunSeed = 222
                };

                Assert.That(DungeonStageSeedResolver.Resolve(context), Is.EqualTo(333));
                context.ExplicitSeed = null;
                Assert.That(DungeonStageSeedResolver.Resolve(context), Is.EqualTo(222));

                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 111;
                Assert.That(DungeonStageSeedResolver.Resolve(context), Is.EqualTo(111));

                int randomCalls = 0;
                definition.seedPolicy = DungeonStageSeedPolicy.RandomPerLoad;
                context.RandomSeedProvider = delegate
                {
                    randomCalls++;
                    return 444;
                };
                Assert.That(DungeonStageSeedResolver.Resolve(context), Is.EqualTo(444));
                Assert.That(randomCalls, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(settings);
            }
        }

        // Procedural Definition이 Blueprint를 구축하고 재로드 시 generated root를 하나만 유지하는지 검사합니다.
        [Test]
        public void StageLoader_ProceduralBuildsAndReplacesSingleGeneratedRoot()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject parent = new GameObject("R3 Procedural Parent");
            try
            {
                settings.ApplyPreset(DungeonPreset.Compact);
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 24680;
                definition.recipe = settings;

                DungeonLoadContext firstContext = new DungeonLoadContext(definition, parent.transform, settings)
                {
                    RequestId = "procedural-first"
                };
                DungeonStageInstance first = DungeonStageLoader.Load(firstContext);
                DungeonBlueprint expected = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.Create(
                        settings,
                        24680,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;

                Assert.That(first.Definition, Is.SameAs(definition));
                Assert.That(first.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(first.ActiveSeed, Is.EqualTo(24680));
                Assert.That(first.Blueprint.blueprintHash, Is.EqualTo(expected.blueprintHash));
                Assert.That(first.ValidationReport.IsValid, Is.True);
                Assert.That(first.Report.activeSeed, Is.EqualTo(24680));
                Assert.That(first.RequestId, Is.EqualTo("procedural-first"));
                Assert.That(CountGeneratedRoots(parent.transform), Is.EqualTo(1));

                DungeonLoadContext secondContext = new DungeonLoadContext(definition, parent.transform, settings)
                {
                    ExplicitSeed = 97531
                };
                DungeonStageInstance second = DungeonStageLoader.Load(secondContext);

                Assert.That(first.Root == null, Is.True);
                Assert.That(second.ActiveSeed, Is.EqualTo(97531));
                Assert.That(second.Blueprint.blueprintHash, Is.Not.EqualTo(first.Blueprint.blueprintHash));
                Assert.That(CountGeneratedRoots(parent.transform), Is.EqualTo(1));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(settings);
            }
        }

        // SavedBlueprint 로드가 현재 레시피·모든 시드 입력·random provider를 무시하고 저장 데이터를 깊은 복사하는지 검사합니다.
        [Test]
        public void StageLoader_SavedBlueprintDoesNotRegenerateFromRecipeOrSeed()
        {
            RogueDungeonSettings sourceSettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            RogueDungeonSettings changedSettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject parent = new GameObject("R3 Saved Parent");
            try
            {
                sourceSettings.ApplyPreset(DungeonPreset.Compact);
                sourceSettings.cellSize = 4.25f;
                const int savedSeed = 112233;
                DungeonBlueprint saved = CreateBlueprint(sourceSettings, savedSeed);
                asset.Store(saved);
                string storedHash = asset.blueprint.blueprintHash;
                int storedWidth = asset.blueprint.grid.width;

                changedSettings.ApplyPreset(DungeonPreset.Chaos);
                changedSettings.cellSize = 6f;
                definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.RandomPerLoad;
                definition.fixedSeed = -1;
                definition.recipe = changedSettings;
                definition.savedBlueprint = asset;
                DungeonLoadContext context = new DungeonLoadContext(definition, parent.transform)
                {
                    ExplicitSeed = 999999,
                    RunSeed = 888888,
                    RandomSeedProvider = delegate
                    {
                        Assert.Fail("SavedBlueprint load must not request a random seed.");
                        return 0;
                    }
                };

                DungeonStageInstance instance = DungeonStageLoader.Load(context);

                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(instance.ActiveSeed, Is.EqualTo(savedSeed));
                Assert.That(instance.Report.activeSeed, Is.EqualTo(savedSeed));
                Assert.That(instance.Blueprint, Is.Not.SameAs(asset.blueprint));
                Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(storedHash));
                Assert.That(instance.Blueprint.grid.width, Is.EqualTo(storedWidth));
                Assert.That(instance.Blueprint.grid.cellSize, Is.EqualTo(4.25f));
                Assert.That(instance.Layout.Width, Is.EqualTo(storedWidth));
                Assert.That(instance.RuntimeSettings, Is.Null);

                instance.Blueprint.cells[0].distanceFromEntrance = 999;
                Assert.That(asset.blueprint.cells[0].distanceFromEntrance, Is.Not.EqualTo(999));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(changedSettings);
                Object.DestroyImmediate(sourceSettings);
            }
        }

        // 누락 source, 미지원 Bake, 생성기 버전과 손상 저장본이 안정적인 코드로 로드를 차단하는지 검사합니다.
        [Test]
        public void StageDefinitionValidation_BlocksInvalidRuntimeSources()
        {
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            DungeonBlueprintAsset emptyAsset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            GameObject parent = new GameObject("R3 Invalid Parent");
            try
            {
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.BakedPrefab;
                definition.recipe = null;
                definition.generatorVersion = 999;
                DungeonValidationReport procedural = DungeonStageDefinitionValidator.Validate(definition);
                Assert.That(procedural.ContainsCode(DungeonStageDefinitionValidationCodes.UnsupportedBuildMode), Is.True);
                Assert.That(procedural.ContainsCode(DungeonStageDefinitionValidationCodes.MissingRecipe), Is.True);
                Assert.That(procedural.ContainsCode(DungeonStageDefinitionValidationCodes.InvalidGeneratorVersion), Is.True);

                definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.savedBlueprint = emptyAsset;
                DungeonStageLoadException exception = Assert.Throws<DungeonStageLoadException>(delegate
                {
                    DungeonStageLoader.Load(new DungeonLoadContext(definition, parent.transform));
                });
                Assert.That(exception.ValidationReport.ContainsCode(DungeonBlueprintValidationCodes.NoFloorCells), Is.True);
                Assert.That(parent.transform.Find(DungeonStageLoader.GeneratedRootName), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(emptyAsset);
                Object.DestroyImmediate(definition);
            }
        }

        // Generator가 Saved Definition을 facade 상태에 반영하고 기존 GenerateWithSeed 호출로 다시 절차 경로를 사용할 수 있는지 검사합니다.
        [Test]
        public void RogueDungeonGenerator_SupportsDefinitionAndLegacyFacadeTogether()
        {
            RogueDungeonSettings sourceSettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            RogueDungeonSettings legacySettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject generatorObject = new GameObject("R3 Facade Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                sourceSettings.ApplyPreset(DungeonPreset.Compact);
                sourceSettings.cellSize = 4.5f;
                asset.Store(CreateBlueprint(sourceSettings, 515151));
                legacySettings.ApplyPreset(DungeonPreset.Balanced);
                legacySettings.cellSize = 2.25f;

                definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                definition.savedBlueprint = asset;
                generator.settings = legacySettings;
                generator.stageDefinition = definition;
                int completed = 0;
                generator.GenerationCompleted += delegate { completed++; };

                generator.LoadStageDefinitionWithSeed(999999);

                Assert.That(generator.CurrentStageInstance.Definition, Is.SameAs(definition));
                Assert.That(generator.CurrentStageInstance.SourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(generator.ActiveSeed, Is.EqualTo(515151));
                Assert.That(generator.CurrentCellSize, Is.EqualTo(4.5f));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(asset.blueprint.blueprintHash));
                Assert.That(completed, Is.EqualTo(1));

                generator.GenerateWithSeed(777777);

                Assert.That(generator.CurrentStageInstance.Definition, Is.Null);
                Assert.That(generator.CurrentStageInstance.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(generator.ActiveSeed, Is.EqualTo(777777));
                Assert.That(generator.CurrentCellSize, Is.EqualTo(2.25f));
                Assert.That(completed, Is.EqualTo(2));
                Assert.That(generator.transform.Find(DungeonStageLoader.GeneratedRootName), Is.Not.Null);
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(legacySettings);
                Object.DestroyImmediate(sourceSettings);
            }
        }

        // 테스트 설정과 시드에서 built-in LegacyV1 Blueprint를 생성합니다.
        private static DungeonBlueprint CreateBlueprint(RogueDungeonSettings settings, int seed)
        {
            return DungeonBlueprintGenerator.Generate(
                DungeonGenerationRequest.Create(
                    settings,
                    seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;
        }

        // 부모 바로 아래에 존재하는 고정 이름 generated root 개수를 계산합니다.
        private static int CountGeneratedRoots(Transform parent)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == DungeonStageLoader.GeneratedRootName) count++;
            }
            return count;
        }
    }
}
