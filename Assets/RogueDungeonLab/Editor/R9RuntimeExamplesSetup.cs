using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R9RuntimeExamplesSetup
    {
        public const string Root =
            "Assets/RogueDungeonLab/Examples/RuntimeBuild";
        public const string SettingsPath =
            Root + "/Settings/R9_RuntimeExampleSettings.asset";
        public const string BlueprintPath =
            Root + "/Blueprints/R9_RuntimeExampleBlueprint.asset";
        public const string ProceduralStagePath =
            Root + "/Stages/R9_ProceduralRuntimeStage.asset";
        public const string SavedStagePath =
            Root + "/Stages/R9_SavedRuntimeStage.asset";
        public const string ProceduralScenePath =
            Root + "/Scenes/R9_ProceduralRuntimeBuild.unity";
        public const string SavedScenePath =
            Root + "/Scenes/R9_SavedBlueprintRuntimeBuild.unity";

        // 현재 장면 저장 여부를 확인한 뒤 R9 RuntimeBuild Core 예제 두 장면을 생성합니다.
        [MenuItem(
            "Tools/Rogue Dungeon Lab/R9 Runtime Core 예제 생성",
            priority = 9)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            CreateAll(true);
        }

        // Batchmode에서 대화상자 없이 같은 Core 예제 자산과 장면을 반복 생성합니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // Core-only 설정·Blueprint·두 Definition·두 smoke scene을 결정적으로 갱신합니다.
        private static void CreateAll(bool openProceduralScene)
        {
            EnsureFolders();
            RogueDungeonSettings settings = CreateSettings();
            DungeonBlueprintAsset blueprint = CreateBlueprint(settings);
            DungeonStageDefinition procedural =
                CreateProceduralStage(settings);
            DungeonStageDefinition saved = CreateSavedStage(blueprint);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CreateScene(ProceduralScenePath, procedural, "Procedural");
            CreateScene(SavedScenePath, saved, "Saved Blueprint");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            procedural = AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                ProceduralStagePath);
            saved = AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                SavedStagePath);
            blueprint = AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(
                BlueprintPath);
            ValidateExamples(procedural, saved, blueprint);
            if (openProceduralScene)
                EditorSceneManager.OpenScene(ProceduralScenePath);
            Debug.Log("R9 Runtime Core examples are ready: " + Root);
        }

        // 작고 빠른 고정 seed Core 예제 설정을 생성하거나 같은 자산에서 갱신합니다.
        private static RogueDungeonSettings CreateSettings()
        {
            RogueDungeonSettings settings =
                LoadOrCreate<RogueDungeonSettings>(SettingsPath);
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.seed = 91001;
            settings.stageWidthCells = 26;
            settings.stageDepthCells = 26;
            settings.desiredRoomCount = 8;
            settings.specialGimmickCount = 1;
            settings.generateOnPlay = false;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // 예제 설정과 seed에서 계산한 Blueprint와 제작 레시피 스냅샷을 저장합니다.
        private static DungeonBlueprintAsset CreateBlueprint(
            RogueDungeonSettings settings)
        {
            DungeonGenerationRequest request =
                DungeonGenerationRequest.Create(
                    settings,
                    settings.seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    "r9-runtime-example");
            DungeonBlueprintGenerationResult result =
                DungeonBlueprintGenerator.Generate(request);
            DungeonValidationReport validation =
                DungeonBlueprintValidator.Validate(result.Blueprint);
            RequireValid(validation, "R9 예제 Blueprint가 유효하지 않습니다.");
            DungeonBlueprintAsset asset =
                LoadOrCreate<DungeonBlueprintAsset>(BlueprintPath);
            asset.Store(result.Blueprint, request.recipeSnapshot);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // 고정 seed로 계산하는 Procedural RuntimeBuild Definition을 갱신합니다.
        private static DungeonStageDefinition CreateProceduralStage(
            RogueDungeonSettings settings)
        {
            DungeonStageDefinition definition =
                LoadOrCreate<DungeonStageDefinition>(ProceduralStagePath);
            SerializedObject serialized = new SerializedObject(definition);
            serialized.Update();
            serialized.FindProperty("stageId").stringValue =
                "r9-example-procedural-v1";
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.Procedural;
            serialized.FindProperty("buildMode").intValue =
                (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe").objectReferenceValue = settings;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = null;
            serialized.FindProperty("stageOverrides").objectReferenceValue = null;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue = settings.seed;
            serialized.FindProperty("generatorVersion").intValue =
                DungeonGeneratorVersions.LegacyV1;
            serialized.FindProperty("contentCatalog").objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("bakedPrefab").objectReferenceValue = null;
            serialized.FindProperty("bakeManifest").objectReferenceValue = null;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // 저장 Blueprint를 재계산 없이 구축하는 Saved RuntimeBuild Definition을 갱신합니다.
        private static DungeonStageDefinition CreateSavedStage(
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageDefinition definition =
                LoadOrCreate<DungeonStageDefinition>(SavedStagePath);
            SerializedObject serialized = new SerializedObject(definition);
            serialized.Update();
            serialized.FindProperty("stageId").stringValue =
                "r9-example-saved-v1";
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue =
                (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe").objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = blueprint;
            serialized.FindProperty("stageOverrides").objectReferenceValue = null;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue =
                blueprint.blueprint.seed;
            serialized.FindProperty("generatorVersion").intValue =
                blueprint.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog").objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("bakedPrefab").objectReferenceValue = null;
            serialized.FindProperty("bakeManifest").objectReferenceValue = null;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // HUD 없는 제품형 Generator·Camera·Light 장면을 지정 Definition으로 저장합니다.
        private static void CreateScene(
            string scenePath,
            DungeonStageDefinition definition,
            string label)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject generatorObject =
                new GameObject("R9 " + label + " Generator");
            RogueDungeonGenerator generator =
                generatorObject.AddComponent<RogueDungeonGenerator>();
            generator.stageDefinition = definition;

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(-30f, 36f, -30f);
            camera.transform.rotation = Quaternion.Euler(48f, 45f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // 두 Definition이 RuntimeBuild이고 저장 Blueprint hash가 유효한지 최종 확인합니다.
        private static void ValidateExamples(
            DungeonStageDefinition procedural,
            DungeonStageDefinition saved,
            DungeonBlueprintAsset blueprint)
        {
            if (procedural == null ||
                procedural.sourceMode != DungeonStageSourceMode.Procedural ||
                procedural.buildMode != DungeonStageBuildMode.RuntimeBuild ||
                saved == null ||
                saved.sourceMode != DungeonStageSourceMode.SavedBlueprint ||
                saved.buildMode != DungeonStageBuildMode.RuntimeBuild ||
                blueprint == null ||
                blueprint.blueprint == null)
            {
                throw new InvalidOperationException(
                    "R9 Runtime Core example contract is incomplete.");
            }
            RequireValid(
                DungeonBlueprintValidator.Validate(blueprint.blueprint),
                "R9 저장 예제 Blueprint 검증에 실패했습니다.");
        }

        // 검증 오류 코드가 하나라도 있으면 예제 생성을 즉시 중단합니다.
        private static void RequireValid(
            DungeonValidationReport report,
            string message)
        {
            if (report != null && report.IsValid) return;
            throw new InvalidOperationException(message);
        }

        // 지정 타입의 영속 자산을 재사용하거나 새 ScriptableObject로 생성합니다.
        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // 예제의 Settings·Blueprints·Stages·Scenes 폴더를 부모부터 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root + "/Settings");
            EnsureFolder(Root + "/Blueprints");
            EnsureFolder(Root + "/Stages");
            EnsureFolder(Root + "/Scenes");
        }

        // Assets 경로의 각 세그먼트를 검사해 없는 폴더만 생성합니다.
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
