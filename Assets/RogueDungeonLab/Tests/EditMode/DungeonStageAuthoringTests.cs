using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonStageAuthoringTests
    {
        private const string TempFolder = "Assets/RogueDungeonLab/Tests/TempR5Authoring";

        // 각 테스트가 독립된 빈 장면과 비어 있는 임시 자산 폴더에서 시작하도록 준비합니다.
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempFolder);
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets/RogueDungeonLab/Tests", "TempR5Authoring");
        }

        // 생성된 장면 오브젝트와 테스트 자산을 제거해 프로젝트에 중간 산출물을 남기지 않습니다.
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
            Undo.ClearAll();
        }

        // 현재 Blueprint를 새 자산으로 저장하고 재임포트해 deep copy·메모·hash가 유지되는지 검사합니다.
        [Test]
        public void CreateBlueprintAsset_PersistsValidatedDeepCopyAfterImport()
        {
            RogueDungeonSettings settings = CreateSettings(DungeonPreset.Compact);
            try
            {
                DungeonBlueprint source = CreateBlueprint(settings, 120501);
                string path = TempFolder + "/SavedBlueprint.asset";

                DungeonBlueprintAsset created = DungeonStageAuthoringService.CreateBlueprintAsset(
                    source,
                    path,
                    "R5 저장 테스트",
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    DungeonRecipeSnapshot.Capture(settings));

                Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(path));
                Assert.That(created.blueprint, Is.Not.SameAs(source));
                Assert.That(created.blueprint.blueprintHash, Is.EqualTo(source.blueprintHash));
                Assert.That(created.blueprint.authoringNote, Is.EqualTo("R5 저장 테스트"));
                Assert.That(created.blueprint.createdUtcTicks, Is.GreaterThan(0L));
                Assert.That(source.authoringNote, Is.Empty);
                Assert.That(source.createdUtcTicks, Is.Zero);
                Assert.That(created.HasAuthoringRecipeSnapshot, Is.True);
                Assert.That(created.AuthoringRecipeHash, Is.EqualTo(source.recipeHash));

                settings.stageWidthCells = 94;
                Assert.That(created.AuthoringRecipeHash, Is.EqualTo(source.recipeHash));

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                DungeonBlueprintAsset reloaded =
                    AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(path);

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.blueprint.blueprintHash, Is.EqualTo(source.blueprintHash));
                Assert.That(reloaded.blueprint.authoringNote, Is.EqualTo("R5 저장 테스트"));
                Assert.That(DungeonBlueprintValidator.Validate(reloaded.blueprint).IsValid, Is.True);
                Assert.That(reloaded.HasAuthoringRecipeSnapshot, Is.True);
                Assert.That(
                    DungeonStageAuthoringService.ValidateStoredRecipe(reloaded).State,
                    Is.EqualTo(DungeonStoredRecipeState.Valid));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // 선택 Blueprint 덮어쓰기 Undo가 저장·강제 재임포트 뒤에도 이전 전체 직렬화 상태로 영속되는지 검사합니다.
        [Test]
        public void OverwriteBlueprintAsset_SupportsUndoForNestedBlueprintData()
        {
            RogueDungeonSettings settings = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings changedSettings = CreateSettings(DungeonPreset.Balanced);
            try
            {
                DungeonBlueprint first = CreateBlueprint(settings, 220501);
                DungeonBlueprint second = CreateBlueprint(changedSettings, 220502);
                DungeonRecipeSnapshot firstRecipe = DungeonRecipeSnapshot.Capture(settings);
                DungeonRecipeSnapshot secondRecipe = DungeonRecipeSnapshot.Capture(changedSettings);
                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    first,
                    TempFolder + "/OverwriteBlueprint.asset",
                    "첫 저장",
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    firstRecipe);
                string firstHash = asset.blueprint.blueprintHash;
                string firstRecipeHash = asset.AuthoringRecipeHash;
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();

                DungeonStageAuthoringService.OverwriteBlueprintAsset(
                    asset,
                    second,
                    "두 번째 저장",
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    secondRecipe);
                Undo.FlushUndoRecordObjects();

                Assert.That(asset.blueprint.blueprintHash, Is.EqualTo(second.blueprintHash));
                Assert.That(asset.blueprint.authoringNote, Is.EqualTo("두 번째 저장"));
                Assert.That(asset.AuthoringRecipeHash, Is.EqualTo(second.recipeHash));

                Undo.PerformUndo();

                Assert.That(asset.blueprint.blueprintHash, Is.EqualTo(firstHash));
                Assert.That(asset.blueprint.authoringNote, Is.EqualTo("첫 저장"));
                Assert.That(asset.AuthoringRecipeHash, Is.EqualTo(firstRecipeHash));
                Assert.That(DungeonBlueprintValidator.Validate(asset.blueprint).IsValid, Is.True);

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    TempFolder + "/OverwriteBlueprint.asset",
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                DungeonBlueprintAsset reloaded =
                    AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(
                        TempFolder + "/OverwriteBlueprint.asset");

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.blueprint.blueprintHash, Is.EqualTo(firstHash));
                Assert.That(reloaded.blueprint.authoringNote, Is.EqualTo("첫 저장"));
                Assert.That(reloaded.AuthoringRecipeHash, Is.EqualTo(firstRecipeHash));
                Assert.That(
                    DungeonStageAuthoringService.ValidateStoredRecipe(reloaded).State,
                    Is.EqualTo(DungeonStoredRecipeState.Valid));
            }
            finally
            {
                Object.DestroyImmediate(changedSettings);
                Object.DestroyImmediate(settings);
            }
        }

        // 동일·다른 시드·stale 입력·동일 provenance 분기·손상 저장본 상태를 구분하는지 검사합니다.
        [Test]
        public void Compare_ClassifiesIdenticalSeedStaleDivergedAndInvalidStates()
        {
            RogueDungeonSettings originalSettings = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings changedSettings = CreateSettings(DungeonPreset.Balanced);
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            try
            {
                DungeonBlueprint current = CreateBlueprint(originalSettings, 320501);
                asset.Store(current);
                Assert.That(
                    DungeonStageAuthoringService.Compare(current, asset).State,
                    Is.EqualTo(DungeonBlueprintComparisonState.Identical));

                asset.Store(CreateBlueprint(originalSettings, 320502));
                Assert.That(
                    DungeonStageAuthoringService.Compare(current, asset).State,
                    Is.EqualTo(DungeonBlueprintComparisonState.DifferentSeed));

                asset.Store(CreateBlueprint(changedSettings, 320501));
                Assert.That(
                    DungeonStageAuthoringService.Compare(current, asset).State,
                    Is.EqualTo(DungeonBlueprintComparisonState.StaleInputs));

                DungeonBlueprint diverged = current.DeepClone();
                diverged.spawns[0].localPosition += Vector3.right * 0.25f;
                diverged.RefreshHash();
                asset.Store(diverged);
                Assert.That(
                    DungeonStageAuthoringService.Compare(current, asset).State,
                    Is.EqualTo(DungeonBlueprintComparisonState.Diverged));

                DungeonBlueprint invalid = current.DeepClone();
                invalid.cells.Clear();
                invalid.RefreshHash();
                asset.Store(invalid);
                Assert.That(
                    DungeonStageAuthoringService.Compare(current, asset).State,
                    Is.EqualTo(DungeonBlueprintComparisonState.InvalidSaved));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(changedSettings);
                Object.DestroyImmediate(originalSettings);
            }
        }

        // 저장본 미리보기가 새 시드 계산 없이 asset hash를 구축하고 원래 절차 시드로 복귀하는지 검사합니다.
        [Test]
        public void PreviewSavedBlueprint_BuildsStoredHashAndRestoresProceduralSource()
        {
            RogueDungeonSettings settings = CreateSettings(DungeonPreset.Compact);
            GameObject generatorObject = new GameObject("R5 Preview Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                generator.settings = settings;
                const int proceduralSeed = 420501;
                const int savedSeed = 420502;
                generator.GenerateWithSeed(proceduralSeed);
                string proceduralHash = generator.CurrentBlueprint.blueprintHash;
                DungeonBlueprint saved = CreateBlueprint(settings, savedSeed);
                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    saved,
                    TempFolder + "/PreviewBlueprint.asset");

                DungeonStageInstance preview = DungeonStageAuthoringService.PreviewSavedBlueprint(
                    generator,
                    asset);

                Assert.That(preview.SourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(preview.Definition, Is.Null);
                Assert.That(preview.RequestId, Is.EqualTo("editor-preview"));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(asset.blueprint.blueprintHash));
                Assert.That(generator.CurrentBlueprint, Is.Not.SameAs(asset.blueprint));
                Assert.That(generator.ActiveSeed, Is.EqualTo(savedSeed));
                Assert.That(generator.transform.Find(DungeonStageLoader.GeneratedRootName), Is.Not.Null);

                DungeonStageAuthoringService.PreviewProcedural(generator, proceduralSeed);

                Assert.That(generator.CurrentStageInstance.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(generator.ActiveSeed, Is.EqualTo(proceduralSeed));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(proceduralHash));
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(settings);
            }
        }

        // StageDefinition을 SerializedObject로 생성·연결하고 재임포트 뒤 Blueprint 참조가 유지되는지 검사합니다.
        [Test]
        public void CreateStageDefinitionAsset_PersistsReferenceAndAssignsGenerator()
        {
            RogueDungeonSettings settings = CreateSettings(DungeonPreset.Compact);
            GameObject generatorObject = new GameObject("R5 Definition Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                generator.settings = settings;
                DungeonBlueprintAsset blueprint = DungeonStageAuthoringService.CreateBlueprintAsset(
                    CreateBlueprint(settings, 520501),
                    TempFolder + "/DefinitionBlueprint.asset");
                string definitionPath = TempFolder + "/SavedStageDefinition.asset";

                DungeonStageDefinition definition =
                    DungeonStageAuthoringService.CreateStageDefinitionAsset(
                        blueprint,
                        definitionPath,
                        null,
                        DungeonMissingContentPolicy.Error,
                        true);
                DungeonStageAuthoringService.AssignStageDefinition(generator, definition);

                Assert.That(definition.sourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(definition.buildMode, Is.EqualTo(DungeonStageBuildMode.RuntimeBuild));
                Assert.That(definition.savedBlueprint, Is.SameAs(blueprint));
                Assert.That(definition.fixedSeed, Is.EqualTo(blueprint.blueprint.seed));
                Assert.That(definition.generatorVersion, Is.EqualTo(blueprint.blueprint.generatorVersion));
                Assert.That(definition.missingContentPolicy, Is.EqualTo(DungeonMissingContentPolicy.Error));
                Assert.That(definition.loadOnPlay, Is.True);
                Assert.That(generator.stageDefinition, Is.SameAs(definition));

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    definitionPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                DungeonStageDefinition reloaded =
                    AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(definitionPath);

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(reloaded.savedBlueprint),
                    Is.EqualTo(TempFolder + "/DefinitionBlueprint.asset"));
                generator.LoadStageDefinition();
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprint.blueprint.blueprintHash));
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(settings);
            }
        }

        // 저장 레시피만 적용하면 곡선을 포함한 생성 필드는 복원되고 시드·비생성 옵션은 유지되며 Undo가 동작하는지 검사합니다.
        [Test]
        public void ApplyStoredRecipeToSettings_RestoresGenerationFieldsPreservesOptionsAndSupportsUndo()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            try
            {
                source.enemyProfile.overProgression.preWrapMode = WrapMode.PingPong;
                source.enemyProfile.overProgression.postWrapMode = WrapMode.Loop;
                source.ClampValues();
                DungeonBlueprint blueprint = CreateBlueprint(source, 720501);
                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    blueprint,
                    TempFolder + "/RecipeRestoreBlueprint.asset",
                    string.Empty,
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    DungeonRecipeSnapshot.Capture(source));

                target.seed = 998877;
                target.spawnDropMarkers = false;
                target.resetDropStatsOnGenerate = true;
                target.generateOnPlay = false;
                string originalTargetHash = DungeonRecipeSnapshot.Capture(target).ComputeHash();
                AssetDatabase.CreateAsset(target, TempFolder + "/RecipeRestoreTarget.asset");

                DungeonStageAuthoringService.ApplyStoredRecipeToSettings(asset, target, false);
                Undo.FlushUndoRecordObjects();

                Assert.That(
                    DungeonRecipeSnapshot.Capture(target).ComputeHash(),
                    Is.EqualTo(blueprint.recipeHash));
                Assert.That(target.seed, Is.EqualTo(998877));
                Assert.That(target.spawnDropMarkers, Is.False);
                Assert.That(target.resetDropStatsOnGenerate, Is.True);
                Assert.That(target.generateOnPlay, Is.False);
                Assert.That(target.enemyProfile.overProgression.preWrapMode, Is.EqualTo(WrapMode.PingPong));
                Assert.That(target.enemyProfile.overProgression.postWrapMode, Is.EqualTo(WrapMode.Loop));

                Undo.PerformUndo();

                Assert.That(
                    DungeonRecipeSnapshot.Capture(target).ComputeHash(),
                    Is.EqualTo(originalTargetHash));
                Assert.That(target.seed, Is.EqualTo(998877));
                Assert.That(target.spawnDropMarkers, Is.False);
                Assert.That(target.resetDropStatsOnGenerate, Is.True);
                Assert.That(target.generateOnPlay, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        // 저장 레시피·시드·LegacyV1 버전으로 절차 생성했을 때 저장 Blueprint hash를 정확히 재현하는지 검사합니다.
        [Test]
        public void ApplyStoredRecipeAndGenerate_ReproducesLegacyV1BlueprintHash()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            GameObject generatorObject = new GameObject("R5 Legacy Recipe Restore Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                const int savedSeed = 780501;
                DungeonBlueprint blueprint = CreateBlueprint(source, savedSeed);
                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    blueprint,
                    TempFolder + "/LegacyRecipeBlueprint.asset",
                    string.Empty,
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    DungeonRecipeSnapshot.Capture(source));
                target.generateOnPlay = false;
                generator.settings = target;

                DungeonStageInstance instance =
                    DungeonStageAuthoringService.ApplyStoredRecipeAndGenerate(
                        generator,
                        asset,
                        target);

                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(instance.RequestId, Is.EqualTo("editor-recipe-restore"));
                Assert.That(instance.Blueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.LegacyV1));
                Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(target.seed, Is.EqualTo(savedSeed));
                Assert.That(target.generateOnPlay, Is.False);
                Assert.That(
                    DungeonRecipeSnapshot.Capture(target).ComputeHash(),
                    Is.EqualTo(blueprint.recipeHash));
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        // 저장 레시피·시드·StableV2 버전으로 절차 생성했을 때 저장 Blueprint hash를 정확히 재현하는지 검사합니다.
        [Test]
        public void ApplyStoredRecipeAndGenerate_ReproducesStableV2BlueprintHash()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Balanced);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            GameObject generatorObject = new GameObject("R5 Recipe Restore Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                const int savedSeed = 820501;
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(source, savedSeed)).Blueprint;
                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    blueprint,
                    TempFolder + "/StableRecipeBlueprint.asset",
                    string.Empty,
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback,
                    DungeonRecipeSnapshot.Capture(source));
                target.generateOnPlay = false;
                generator.settings = target;

                DungeonStageInstance instance =
                    DungeonStageAuthoringService.ApplyStoredRecipeAndGenerate(
                        generator,
                        asset,
                        target);

                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(instance.RequestId, Is.EqualTo("editor-recipe-restore"));
                Assert.That(instance.Blueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.StableV2));
                Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(target.seed, Is.EqualTo(savedSeed));
                Assert.That(target.generateOnPlay, Is.False);
                Assert.That(
                    DungeonRecipeSnapshot.Capture(target).ComputeHash(),
                    Is.EqualTo(blueprint.recipeHash));
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        // 프로젝트 자산인 custom catalog를 쓴 StableV2 저장본이 같은 catalog로 정확히 재생성되는지 검사합니다.
        [Test]
        public void ApplyStoredRecipeAndGenerate_ReproducesStableV2BlueprintHashWithCustomCatalog()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Balanced);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            GameObject generatorObject = new GameObject("R5 Catalog Recipe Restore Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                source.enemyProfile.baseDensity = 1f;
                source.enemyProfile.maxCount = 24;
                source.ClampValues();
                DungeonContentCatalog catalog = CreateCustomCatalogAsset();
                const int savedSeed = 880501;
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(
                        source,
                        savedSeed,
                        catalog,
                        "r5-custom-catalog-source")).Blueprint;
                Assert.That(ContainsSpawnContentKey(blueprint, "tests/custom-enemy"), Is.True);
                Assert.That(blueprint.catalogPlanningHash, Is.EqualTo(catalog.ComputePlanningHash()));

                DungeonBlueprintAsset asset = DungeonStageAuthoringService.CreateBlueprintAsset(
                    blueprint,
                    TempFolder + "/StableCatalogRecipeBlueprint.asset",
                    string.Empty,
                    catalog,
                    DungeonMissingContentPolicy.Error,
                    DungeonRecipeSnapshot.Capture(source));
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    TempFolder + "/CustomCatalog.asset",
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                catalog = AssetDatabase.LoadAssetAtPath<DungeonContentCatalog>(
                    TempFolder + "/CustomCatalog.asset");
                target.generateOnPlay = false;
                generator.settings = target;

                DungeonStageInstance instance =
                    DungeonStageAuthoringService.ApplyStoredRecipeAndGenerate(
                        generator,
                        asset,
                        target,
                        catalog,
                        DungeonMissingContentPolicy.Error);

                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.Procedural));
                Assert.That(instance.Blueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.StableV2));
                Assert.That(instance.Blueprint.catalogPlanningHash, Is.EqualTo(catalog.ComputePlanningHash()));
                Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(ContainsSpawnContentKey(instance.Blueprint, "tests/custom-enemy"), Is.True);
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        // snapshot 없는 기존 R5 형식 자산이 강제 재임포트 뒤에도 로드되며 설정 복원만 거부하는지 검사합니다.
        [Test]
        public void SnapshotlessLegacyBlueprintAsset_RemainsLoadableAfterForceReimport()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            GameObject generatorObject = new GameObject("R5 Snapshotless Legacy Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                DungeonBlueprint blueprint = CreateBlueprint(source, 900501);
                string path = TempFolder + "/SnapshotlessLegacyBlueprint.asset";
                DungeonStageAuthoringService.CreateBlueprintAsset(blueprint, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                DungeonBlueprintAsset reloaded =
                    AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(path);

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.HasAuthoringRecipeSnapshot, Is.False);
                Assert.That(DungeonBlueprintValidator.Validate(reloaded.blueprint).IsValid, Is.True);
                Assert.That(
                    DungeonStageAuthoringService.ValidateStoredRecipe(reloaded).State,
                    Is.EqualTo(DungeonStoredRecipeState.Missing));
                Assert.Throws<System.InvalidOperationException>(delegate
                {
                    DungeonStageAuthoringService.ApplyStoredRecipeToSettings(reloaded, target, false);
                });

                generator.settings = target;
                DungeonStageInstance instance =
                    DungeonStageAuthoringService.PreviewSavedBlueprint(generator, reloaded);

                Assert.That(instance.SourceMode, Is.EqualTo(DungeonStageSourceMode.SavedBlueprint));
                Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprint.blueprintHash));
            }
            finally
            {
                generator.ClearGenerated();
                Object.DestroyImmediate(generatorObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        // 기존 snapshot 없는 자산은 계속 유효하되 설정 복원을 거부하고, 손상 snapshot도 hash 검증으로 차단하는지 검사합니다.
        [Test]
        public void StoredRecipeValidation_DistinguishesLegacyMissingAndCorruptedSnapshots()
        {
            RogueDungeonSettings source = CreateSettings(DungeonPreset.Compact);
            RogueDungeonSettings target = CreateSettings(DungeonPreset.Chaos);
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            try
            {
                DungeonBlueprint blueprint = CreateBlueprint(source, 920501);
                asset.Store(blueprint);
                Assert.That(
                    DungeonStageAuthoringService.ValidateStoredRecipe(asset).State,
                    Is.EqualTo(DungeonStoredRecipeState.Missing));
                Assert.Throws<System.InvalidOperationException>(delegate
                {
                    DungeonStageAuthoringService.ApplyStoredRecipeToSettings(asset, target, false);
                });

                asset.Store(blueprint, DungeonRecipeSnapshot.Capture(source));
                SerializedObject serialized = new SerializedObject(asset);
                serialized.Update();
                SerializedProperty storedRecipe = serialized.FindProperty("authoringRecipeSnapshot");
                storedRecipe.FindPropertyRelative("stageWidthCells").intValue += 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    DungeonStageAuthoringService.ValidateStoredRecipe(asset).State,
                    Is.EqualTo(DungeonStoredRecipeState.HashMismatch));
                Assert.Throws<System.InvalidOperationException>(delegate
                {
                    DungeonStageAuthoringService.ApplyStoredRecipeToSettings(asset, target, true);
                });
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        // 검증 오류가 있는 Blueprint가 프로젝트 자산으로 생성되기 전에 코드 기반 예외로 차단되는지 검사합니다.
        [Test]
        public void CreateBlueprintAsset_RejectsInvalidBlueprintWithoutCreatingAsset()
        {
            string path = TempFolder + "/InvalidBlueprint.asset";
            DungeonBlueprint invalid = new DungeonBlueprint();

            DungeonStageAuthoringException exception =
                Assert.Throws<DungeonStageAuthoringException>(delegate
                {
                    DungeonStageAuthoringService.CreateBlueprintAsset(invalid, path);
                });

            Assert.That(
                exception.ValidationReport.ContainsCode(DungeonBlueprintValidationCodes.NoFloorCells),
                Is.True);
            Assert.That(AssetDatabase.AssetPathExists(path), Is.False);

            RogueDungeonSettings settings = CreateSettings(DungeonPreset.Compact);
            try
            {
                DungeonStageAuthoringException policyException =
                    Assert.Throws<DungeonStageAuthoringException>(delegate
                    {
                        DungeonStageAuthoringService.CreateBlueprintAsset(
                            CreateBlueprint(settings, 620501),
                            path,
                            string.Empty,
                            null,
                            (DungeonMissingContentPolicy)999);
                    });
                Assert.That(
                    policyException.ValidationReport.ContainsCode(
                        DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy),
                    Is.True);
                Assert.That(AssetDatabase.AssetPathExists(path), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // 실험실 창의 공개 제작 흐름에 스테이지 자산 탭이 포함됐는지 검사합니다.
        [Test]
        public void RogueDungeonLabWindow_ContainsStageAssetsTab()
        {
            FieldInfo tabsField = typeof(RogueDungeonLabWindow).GetField(
                "Tabs",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(tabsField, Is.Not.Null);
            string[] tabs = (string[])tabsField.GetValue(null);
            Assert.That(tabs, Does.Contain("스테이지 자산"));
        }

        // 테스트용 설정을 프리셋으로 정규화해 생성 가능한 상태로 만듭니다.
        private static RogueDungeonSettings CreateSettings(DungeonPreset preset)
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            settings.ApplyPreset(preset);
            settings.ClampValues();
            return settings;
        }

        // 실제 Prefab 참조와 custom enemy planning entry를 가진 프로젝트 catalog 자산을 만듭니다.
        private static DungeonContentCatalog CreateCustomCatalogAsset()
        {
            GameObject prefabSource = new GameObject("R5 Custom Enemy Prefab");
            GameObject prefab;
            try
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(
                    prefabSource,
                    TempFolder + "/CustomEnemy.prefab");
            }
            finally
            {
                Object.DestroyImmediate(prefabSource);
            }

            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            catalog.entries = new List<DungeonContentCatalogEntry>
            {
                new DungeonContentCatalogEntry
                {
                    contentKey = "tests/custom-enemy",
                    category = DungeonSpawnCategory.Enemy,
                    prefab = prefab,
                    weight = 1f,
                    minProgression = 0f,
                    maxProgression = 1f,
                    placement = DungeonContentPlacement.Any,
                    requiredRoomTags = new List<string>(),
                    footprintCells = Vector2Int.one,
                    minimumSpacingCells = 0,
                    randomizeYaw = true,
                    yawDegreesRange = new Vector2(-30f, 30f),
                    uniformScaleRange = new Vector2(0.9f, 1.1f),
                    gameplayId = "tests-custom-enemy"
                }
            };
            AssetDatabase.CreateAsset(catalog, TempFolder + "/CustomCatalog.asset");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // Blueprint spawn 목록에 지정 logical content key가 한 번 이상 기록됐는지 확인합니다.
        private static bool ContainsSpawnContentKey(
            DungeonBlueprint blueprint,
            string contentKey)
        {
            if (blueprint == null || blueprint.spawns == null) return false;
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn != null && spawn.contentKey == contentKey) return true;
            }
            return false;
        }

        // 지정 설정·시드의 승인된 LegacyV1 Blueprint를 생성합니다.
        private static DungeonBlueprint CreateBlueprint(
            RogueDungeonSettings settings,
            int seed)
        {
            return DungeonBlueprintGenerator.Generate(
                DungeonGenerationRequest.Create(
                    settings,
                    seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;
        }
    }
}
