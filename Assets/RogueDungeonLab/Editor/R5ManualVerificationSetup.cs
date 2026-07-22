using System;
using System.IO;
using RogueDungeonLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R5ManualVerificationSetup
    {
        private const string Root = "Assets/R5ManualVerification";
        private const string Settings = Root + "/Settings";
        private const string Blueprints = Root + "/Blueprints";
        private const string BlueprintReferences = Blueprints + "/Reference";
        private const string BlueprintOutput = Blueprints + "/Output";
        private const string Stages = Root + "/Stages";
        private const string StageReferences = Stages + "/Reference";
        private const string StageOutput = Stages + "/Output";
        private const string Scenes = Root + "/Scenes";

        private const string OriginalSettingsPath = Settings + "/R5_OriginalSettings.asset";
        private const string ChangedSettingsPath = Settings + "/R5_ChangedSettings.asset";
        private const string IdenticalBlueprintPath = BlueprintReferences + "/R5_Reference_Seed12345.asset";
        private const string DifferentSeedBlueprintPath = BlueprintReferences + "/R5_Reference_Seed12346.asset";
        private const string StaleBlueprintPath = BlueprintReferences + "/R5_Reference_ChangedRecipe.asset";
        private const string ProceduralStagePath = StageReferences + "/R5_Procedural_Seed12345.asset";
        private const string AlternateProceduralStagePath = StageReferences + "/R5_Procedural_Seed12346.asset";
        private const string SavedStagePath = StageReferences + "/R5_Saved_Seed12345.asset";
        private const string AuthoringScenePath = Scenes + "/R5_AuthoringVerification.unity";
        private const string SavedRuntimeScenePath = Scenes + "/R5_SavedRuntimeVerification.unity";

        // 저장 여부를 확인한 뒤 R5 검증 장면을 열고 제작 창까지 준비합니다.
        [MenuItem("Tools/Rogue Dungeon Lab/R5 수동 검증 환경 생성", priority = 3)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateAll(true);
        }

        // Batchmode에서도 동일한 기준 Blueprint·StageDefinition·검증 장면을 생성합니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // 사용자 Output 자산은 보존하면서 R5 기준 자산과 두 검증 장면을 반복 실행 가능하게 구성합니다.
        private static void CreateAll(bool openWindow)
        {
            EnsureFolders();
            RogueDungeonSettings originalSettings = CreateSettings(OriginalSettingsPath, false);
            RogueDungeonSettings changedSettings = CreateSettings(ChangedSettingsPath, true);

            DungeonBlueprint identicalBlueprint = GenerateBlueprint(originalSettings, 12345, "r5-reference-identical");
            DungeonBlueprint differentSeedBlueprint = GenerateBlueprint(originalSettings, 12346, "r5-reference-different-seed");
            DungeonBlueprint staleBlueprint = GenerateBlueprint(changedSettings, 12345, "r5-reference-stale");

            DungeonBlueprintAsset identicalAsset = CreateReferenceBlueprint(
                IdenticalBlueprintPath,
                identicalBlueprint,
                "R5 기준: 원본 recipe, StableV2, seed 12345");
            DungeonBlueprintAsset differentSeedAsset = CreateReferenceBlueprint(
                DifferentSeedBlueprintPath,
                differentSeedBlueprint,
                "R5 비교 기준: 같은 입력, seed 12346");
            DungeonBlueprintAsset staleAsset = CreateReferenceBlueprint(
                StaleBlueprintPath,
                staleBlueprint,
                "R5 비교 기준: 변경된 recipe, seed 12345");
            ValidateReferenceComparisons(
                identicalBlueprint,
                identicalAsset,
                differentSeedAsset,
                staleAsset);

            DungeonStageDefinition proceduralStage = CreateProceduralStage(
                ProceduralStagePath,
                originalSettings,
                12345);
            CreateProceduralStage(
                AlternateProceduralStagePath,
                originalSettings,
                12346);
            DungeonStageDefinition savedStage = CreateSavedStage(
                SavedStagePath,
                identicalAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadRequiredAssets(
                out originalSettings,
                out identicalAsset,
                out proceduralStage,
                out savedStage);

            CreateVerificationScene(
                SavedRuntimeScenePath,
                "R5 SAVED RUNTIME TEST - Read README_KO",
                originalSettings,
                savedStage,
                identicalAsset.blueprint,
                false);
            CreateVerificationScene(
                AuthoringScenePath,
                "R5 AUTHORING TEST - Read README_KO",
                originalSettings,
                proceduralStage,
                identicalAsset.blueprint,
                true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "R5 manual verification environment is ready. Open " +
                AuthoringScenePath + " and use the Stage Assets tab.");

            if (!openWindow) return;
            RogueDungeonLabWindow.Open();
            EditorUtility.DisplayDialog(
                "R5 수동 검증 환경 준비 완료",
                "R5_AuthoringVerification 장면과 스테이지 자산 탭을 열었습니다.\n\n" +
                "Blueprints/Reference의 세 자산으로 동일·다른 시드·stale 비교를 확인하세요.\n" +
                "직접 만든 자산은 반드시 Blueprints/Output과 Stages/Output에 저장하세요.",
                "확인");
        }

        // 검증 기준과 사용자 산출물을 분리한 R5 폴더를 상위부터 안전하게 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Settings);
            EnsureFolder(Blueprints);
            EnsureFolder(BlueprintReferences);
            EnsureFolder(BlueprintOutput);
            EnsureFolder(Stages);
            EnsureFolder(StageReferences);
            EnsureFolder(StageOutput);
            EnsureFolder(Scenes);
        }

        // 원본과 recipe hash가 다른 stale 비교용 설정을 독립 자산으로 구성합니다.
        private static RogueDungeonSettings CreateSettings(string path, bool changedRecipe)
        {
            RogueDungeonSettings settings = LoadOrCreate<RogueDungeonSettings>(path);
            settings.ApplyPreset(DungeonPreset.Balanced);
            settings.seed = 12345;
            settings.generateOnPlay = false;
            settings.contentSpacingCells = 1;
            settings.reservedEntranceRadiusCells = 2;
            if (changedRecipe)
            {
                settings.stageWidthCells += 4;
                settings.desiredRoomCount += 3;
            }
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // 지정 설정과 시드를 immutable StableV2 요청으로 캡처해 기준 Blueprint를 계산합니다.
        private static DungeonBlueprint GenerateBlueprint(
            RogueDungeonSettings settings,
            int seed,
            string requestId)
        {
            DungeonGenerationRequest request = DungeonGenerationRequest.CreateStableV2(
                settings,
                seed,
                null,
                requestId);
            DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(request).Blueprint;
            DungeonValidationReport validation = DungeonStageAuthoringService.ValidateBlueprint(
                blueprint,
                null,
                DungeonMissingContentPolicy.BuiltInFallback);
            if (!validation.IsValid)
                throw new InvalidOperationException("R5 reference Blueprint generation failed validation.");
            return blueprint;
        }

        // 기준 Blueprint 자산의 GUID는 유지하고 논리 데이터와 설명만 기준값으로 갱신합니다.
        private static DungeonBlueprintAsset CreateReferenceBlueprint(
            string path,
            DungeonBlueprint blueprint,
            string authoringNote)
        {
            DungeonBlueprintAsset asset = LoadOrCreate<DungeonBlueprintAsset>(path);
            DungeonBlueprint stored = blueprint.DeepClone();
            stored.authoringNote = authoringNote;
            stored.createdUtcTicks = asset.blueprint != null && asset.blueprint.createdUtcTicks > 0L
                ? asset.blueprint.createdUtcTicks
                : DateTime.UtcNow.Ticks;
            stored.RefreshHash();
            asset.Store(stored);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // 세 기준 자산이 UI에서 의도한 Identical·DifferentSeed·StaleInputs 상태로 분류되는지 확인합니다.
        private static void ValidateReferenceComparisons(
            DungeonBlueprint current,
            DungeonBlueprintAsset identical,
            DungeonBlueprintAsset differentSeed,
            DungeonBlueprintAsset stale)
        {
            if (DungeonStageAuthoringService.Compare(current, identical).State !=
                DungeonBlueprintComparisonState.Identical)
                throw new InvalidOperationException("R5 identical comparison reference is invalid.");
            if (DungeonStageAuthoringService.Compare(current, differentSeed).State !=
                DungeonBlueprintComparisonState.DifferentSeed)
                throw new InvalidOperationException("R5 different-seed comparison reference is invalid.");
            if (DungeonStageAuthoringService.Compare(current, stale).State !=
                DungeonBlueprintComparisonState.StaleInputs)
                throw new InvalidOperationException("R5 stale comparison reference is invalid.");
        }

        // 고정 시드 StableV2 원본을 다시 만들 수 있는 Procedural StageDefinition을 구성합니다.
        private static DungeonStageDefinition CreateProceduralStage(
            string path,
            RogueDungeonSettings settings,
            int seed)
        {
            DungeonStageDefinition stage = LoadOrCreate<DungeonStageDefinition>(path);
            SerializedObject serialized = new SerializedObject(stage);
            serialized.Update();
            serialized.FindProperty("sourceMode").intValue = (int)DungeonStageSourceMode.Procedural;
            serialized.FindProperty("buildMode").intValue = (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe").objectReferenceValue = settings;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = null;
            serialized.FindProperty("seedPolicy").intValue = (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue = seed;
            serialized.FindProperty("generatorVersion").intValue = DungeonGeneratorVersions.StableV2;
            serialized.FindProperty("contentCatalog").objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 저장 Blueprint를 재계산 없이 로드하는 RuntimeBuild StageDefinition 기준 자산을 구성합니다.
        private static DungeonStageDefinition CreateSavedStage(
            string path,
            DungeonBlueprintAsset blueprintAsset)
        {
            DungeonStageDefinition stage = LoadOrCreate<DungeonStageDefinition>(path);
            SerializedObject serialized = new SerializedObject(stage);
            serialized.Update();
            serialized.FindProperty("sourceMode").intValue = (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue = (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe").objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = blueprintAsset;
            serialized.FindProperty("seedPolicy").intValue = (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue = blueprintAsset.blueprint.seed;
            serialized.FindProperty("generatorVersion").intValue = blueprintAsset.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog").objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 자산 저장 뒤 Unity 직렬화본을 다시 읽어 장면에 잘못된 메모리 참조가 들어가지 않게 합니다.
        private static void ReloadRequiredAssets(
            out RogueDungeonSettings settings,
            out DungeonBlueprintAsset blueprint,
            out DungeonStageDefinition proceduralStage,
            out DungeonStageDefinition savedStage)
        {
            settings = AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(OriginalSettingsPath);
            blueprint = AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(IdenticalBlueprintPath);
            proceduralStage = AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(ProceduralStagePath);
            savedStage = AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(SavedStagePath);
            if (settings == null || blueprint == null || proceduralStage == null || savedStage == null)
                throw new InvalidOperationException("R5 required assets could not be reloaded.");
        }

        // 공통 실험 시스템을 갖춘 장면을 저장·재개방하고 기대 Blueprint hash까지 즉시 검증합니다.
        private static void CreateVerificationScene(
            string scenePath,
            string noteObjectName,
            RogueDungeonSettings settings,
            DungeonStageDefinition stage,
            DungeonBlueprint expectedBlueprint,
            bool selectGenerator)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject generatorObject = new GameObject("Rogue Dungeon Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            SerializedObject serializedGenerator = new SerializedObject(generator);
            serializedGenerator.Update();
            serializedGenerator.FindProperty("settings").objectReferenceValue = settings;
            serializedGenerator.FindProperty("stageDefinition").objectReferenceValue = stage;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
            generator.settings = settings;
            generator.stageDefinition = stage;

            GameObject systems = new GameObject("Rogue Dungeon Lab Systems");
            systems.AddComponent<DropValidationService>();
            RogueDungeonClickInteractor interactor = systems.AddComponent<RogueDungeonClickInteractor>();
            systems.AddComponent<RuntimeLabHUD>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<LabOrbitCamera>();
            cameraObject.transform.position = new Vector3(-35f, 45f, -35f);
            cameraObject.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            interactor.targetCamera = camera;

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            new GameObject(noteObjectName).transform.SetAsFirstSibling();

            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(interactor);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Failed to save R5 verification scene: " + scenePath);

            Scene reopened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            generator = UnityEngine.Object.FindAnyObjectByType<RogueDungeonGenerator>();
            LabOrbitCamera orbit = UnityEngine.Object.FindAnyObjectByType<LabOrbitCamera>();
            if (generator == null || generator.settings == null || generator.stageDefinition == null)
                throw new InvalidOperationException("R5 scene references were not restored after reopening: " + scenePath);

            generator.LoadStageDefinition();
            ValidateLoadedPreview(generator, expectedBlueprint, stage.sourceMode, scenePath);
            if (orbit != null) orbit.FocusBounds(generator.GeneratedBounds);
            EditorSceneManager.MarkSceneDirty(reopened);
            if (!EditorSceneManager.SaveScene(reopened, scenePath))
                throw new InvalidOperationException("Failed to save validated R5 scene: " + scenePath);
            if (selectGenerator) Selection.activeObject = generator.gameObject;
        }

        // 장면의 출처 모드와 실제 논리 hash가 기준 Blueprint와 일치하는지 확인합니다.
        private static void ValidateLoadedPreview(
            RogueDungeonGenerator generator,
            DungeonBlueprint expectedBlueprint,
            DungeonStageSourceMode expectedSourceMode,
            string scenePath)
        {
            if (generator.CurrentBlueprint == null || generator.CurrentStageInstance == null)
                throw new InvalidOperationException("R5 scene did not create a stage instance: " + scenePath);
            if (generator.CurrentStageInstance.SourceMode != expectedSourceMode)
                throw new InvalidOperationException("R5 scene loaded the wrong source mode: " + scenePath);

            string actualHash = DungeonBlueprintHasher.Compute(generator.CurrentBlueprint);
            string expectedHash = DungeonBlueprintHasher.Compute(expectedBlueprint);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("R5 scene Blueprint hash does not match its reference: " + scenePath);
            if (generator.transform.Find(DungeonStageLoader.GeneratedRootName) == null)
                throw new InvalidOperationException("R5 scene did not create the generated stage root: " + scenePath);
        }

        // 지정 ScriptableObject를 불러오거나 같은 경로에 새 자산을 생성합니다.
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Assets부터 시작하는 폴더 경로를 반복 실행에 안전하게 생성합니다.
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
