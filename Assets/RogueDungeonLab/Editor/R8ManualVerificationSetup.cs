using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R8ManualVerificationSetup
    {
        private const string Root =
            "Assets/R8ManualVerification";
        private const string SettingsFolder =
            Root + "/Settings";
        private const string BlueprintsFolder =
            Root + "/Blueprints";
        private const string StagesFolder =
            Root + "/Stages";
        private const string ScenesFolder =
            Root + "/Scenes";
        private const string SettingsPath =
            SettingsFolder + "/R8_RunStateSettings.asset";
        private const string BlueprintPath =
            BlueprintsFolder +
            "/R8_SavedBlueprint_Seed82468.asset";
        private const string ProceduralStagePath =
            StagesFolder +
            "/R8_ProceduralStage.asset";
        private const string SavedStagePath =
            StagesFolder + "/R8_SavedStage.asset";
        private const string ScenePath =
            ScenesFolder +
            "/R8_RunStateVerification.unity";
        private const string ProceduralStageId =
            "r8-manual-procedural-v1";
        private const string SavedStageId =
            "r8-manual-saved-v1";

        // 현재 장면 저장 여부를 확인한 뒤 R8 전용 자산과 수동 검증 장면을 생성합니다.
        [MenuItem(
            "Tools/Rogue Dungeon Lab/R8 수동 검증 환경 생성",
            priority = 7)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            CreateAll(true);
        }

        // Batchmode에서 대화상자 없이 같은 R8 자산과 장면을 반복 생성합니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // 소형 레시피·저장 Blueprint·두 StageDefinition과 전환형 검증 장면을 구성합니다.
        private static void CreateAll(bool showDialog)
        {
            EnsureFolders();
            RogueDungeonSettings settings =
                CreateSettings();
            DungeonBlueprintAsset blueprint =
                CreateBlueprint(settings);
            DungeonStageDefinition procedural =
                CreateProceduralStage(settings);
            DungeonStageDefinition saved =
                CreateSavedStage(blueprint);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            settings =
                RequireAsset<RogueDungeonSettings>(
                    SettingsPath);
            blueprint =
                RequireAsset<DungeonBlueprintAsset>(
                    BlueprintPath);
            procedural =
                RequireAsset<DungeonStageDefinition>(
                    ProceduralStagePath);
            saved =
                RequireAsset<DungeonStageDefinition>(
                    SavedStagePath);
            ValidateStageContracts(
                blueprint,
                procedural,
                saved);
            CreateVerificationScene(
                settings,
                procedural,
                saved);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "R8 manual verification environment is ready: " +
                ScenePath);

            if (!showDialog) return;
            EditorUtility.DisplayDialog(
                "R8 수동 검증 환경 준비 완료",
                "R8_RunStateVerification 장면을 열었습니다.\n\n" +
                "Play → 탐험 탭에서 플레이어 생성 → 적/상자 파괴 및 이동 → 런 상태 탭에서 slot-1 저장 → 새 시드/재생성 → 슬롯 불러오기를 확인하세요.\n\n" +
                "SavedBlueprint 확인은 Procedural Generator를 끄고 Saved Generator를 켠 뒤 Play를 다시 시작합니다.",
                "확인");
        }

        // 충분한 적·파괴물과 한 기믹을 가진 R8 소형 검증 레시피를 갱신합니다.
        private static RogueDungeonSettings CreateSettings()
        {
            RogueDungeonSettings settings =
                LoadOrCreate<RogueDungeonSettings>(
                    SettingsPath);
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.seed = 82468;
            settings.stageWidthCells = 28;
            settings.stageDepthCells = 28;
            settings.desiredRoomCount = 8;
            settings.contentSpacingCells = 0;
            settings.enemyProfile.baseDensity = 0.3f;
            settings.enemyProfile.maxCount = 16;
            settings.destructibleProfile.baseDensity = 0.3f;
            settings.destructibleProfile.maxCount = 16;
            settings.propProfile.baseDensity = 0.04f;
            settings.specialGimmickCount = 1;
            settings.generateOnPlay = false;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // 고정 seed StableV2 결과를 정확 재개 가능한 저장 Blueprint 자산으로 갱신합니다.
        private static DungeonBlueprintAsset CreateBlueprint(
            RogueDungeonSettings settings)
        {
            DungeonBlueprint blueprint =
                DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest
                        .CreateStableV2(
                            settings,
                            82468,
                            null,
                            "r8-manual-saved"))
                    .Blueprint;
            DungeonBlueprintAsset asset =
                LoadOrCreate<DungeonBlueprintAsset>(
                    BlueprintPath);
            asset.Store(
                blueprint,
                DungeonRecipeSnapshot.Capture(settings));
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // 임의 seed 재생성과 RunState seed 복귀를 확인할 Procedural Definition을 갱신합니다.
        private static DungeonStageDefinition
            CreateProceduralStage(
                RogueDungeonSettings settings)
        {
            DungeonStageDefinition stage =
                LoadOrCreate<DungeonStageDefinition>(
                    ProceduralStagePath);
            SerializedObject serialized =
                new SerializedObject(stage);
            serialized.Update();
            serialized.FindProperty("stageId").stringValue =
                ProceduralStageId;
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.Procedural;
            serialized.FindProperty("buildMode").intValue =
                (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe")
                .objectReferenceValue = settings;
            serialized.FindProperty("savedBlueprint")
                .objectReferenceValue = null;
            serialized.FindProperty("stageOverrides")
                .objectReferenceValue = null;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue =
                82468;
            serialized.FindProperty("generatorVersion")
                .intValue = DungeonGeneratorVersions.StableV2;
            serialized.FindProperty("contentCatalog")
                .objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy")
                .intValue =
                (int)DungeonMissingContentPolicy
                    .BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue =
                true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 영구 stage ID로 저장형 상태 복원을 확인할 SavedBlueprint Definition을 갱신합니다.
        private static DungeonStageDefinition CreateSavedStage(
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageDefinition stage =
                LoadOrCreate<DungeonStageDefinition>(
                    SavedStagePath);
            SerializedObject serialized =
                new SerializedObject(stage);
            serialized.Update();
            serialized.FindProperty("stageId").stringValue =
                SavedStageId;
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue =
                (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe")
                .objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint")
                .objectReferenceValue = blueprint;
            serialized.FindProperty("stageOverrides")
                .objectReferenceValue = null;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue =
                blueprint.blueprint.seed;
            serialized.FindProperty("generatorVersion")
                .intValue =
                blueprint.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog")
                .objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy")
                .intValue =
                (int)DungeonMissingContentPolicy
                    .BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue =
                true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 두 Definition의 영구 ID와 저장 Blueprint 참조·hash가 생성 뒤 유지되는지 검사합니다.
        private static void ValidateStageContracts(
            DungeonBlueprintAsset blueprint,
            DungeonStageDefinition procedural,
            DungeonStageDefinition saved)
        {
            if (procedural.stageId != ProceduralStageId ||
                saved.stageId != SavedStageId)
            {
                throw new InvalidOperationException(
                    "R8 manual Stage IDs were not persisted.");
            }
            if (saved.savedBlueprint != blueprint ||
                blueprint.blueprint == null ||
                !DungeonBlueprintValidator
                    .Validate(blueprint.blueprint)
                    .IsValid)
            {
                throw new InvalidOperationException(
                    "R8 manual SavedBlueprint contract is invalid.");
            }
        }

        // Procedural과 Saved Generator를 토글해 같은 HUD 저장 흐름을 확인할 장면을 저장합니다.
        private static void CreateVerificationScene(
            RogueDungeonSettings settings,
            DungeonStageDefinition procedural,
            DungeonStageDefinition saved)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            RogueDungeonGenerator proceduralGenerator =
                CreateGenerator(
                    "R8 Procedural Generator (ACTIVE)",
                    settings,
                    procedural,
                    true);
            CreateGenerator(
                "R8 Saved Generator (enable after disabling Procedural)",
                settings,
                saved,
                false);
            CreateCommonSystems(out LabOrbitCamera orbit);
            new GameObject(
                "README - Play, destroy and move, save slot, regenerate, then load")
                .transform.SetAsFirstSibling();

            proceduralGenerator.LoadStageDefinition();
            if (orbit != null)
                orbit.FocusBounds(
                    proceduralGenerator.GeneratedBounds);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    ScenePath))
            {
                throw new InvalidOperationException(
                    "Failed to save R8 verification scene.");
            }

            Scene reopened = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            RogueDungeonGenerator found =
                GameObject.Find(
                        "R8 Procedural Generator (ACTIVE)")
                    ?.GetComponent<RogueDungeonGenerator>();
            if (found == null ||
                found.stageDefinition == null ||
                found.stageDefinition.stageId !=
                    ProceduralStageId)
            {
                throw new InvalidOperationException(
                    "R8 verification scene references did not survive reopening.");
            }
            Selection.activeObject = found.gameObject;
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(
                reopened,
                ScenePath);
        }

        // 설정과 StageDefinition을 SerializedObject로 연결한 Generator를 만듭니다.
        private static RogueDungeonGenerator CreateGenerator(
            string name,
            RogueDungeonSettings settings,
            DungeonStageDefinition definition,
            bool active)
        {
            GameObject root = new GameObject(name);
            RogueDungeonGenerator generator =
                root.AddComponent<RogueDungeonGenerator>();
            SerializedObject serialized =
                new SerializedObject(generator);
            serialized.Update();
            serialized.FindProperty("settings")
                .objectReferenceValue = settings;
            serialized.FindProperty("stageDefinition")
                .objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            generator.settings = settings;
            generator.stageDefinition = definition;
            EditorUtility.SetDirty(generator);
            root.SetActive(active);
            return generator;
        }

        // HUD·드랍 서비스·카메라·조명과 클릭 상호작용을 장면에 구성합니다.
        private static void CreateCommonSystems(
            out LabOrbitCamera orbit)
        {
            GameObject systems =
                new GameObject("Rogue Dungeon Lab Systems");
            systems.AddComponent<DropValidationService>();
            RuntimeLabHUD hud =
                systems.AddComponent<RuntimeLabHUD>();
            RogueDungeonClickInteractor interactor =
                systems.AddComponent<
                    RogueDungeonClickInteractor>();

            GameObject cameraObject =
                new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera =
                cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();
            orbit =
                cameraObject.AddComponent<LabOrbitCamera>();
            cameraObject.transform.position =
                new Vector3(-36f, 42f, -36f);
            cameraObject.transform.rotation =
                Quaternion.Euler(48f, 45f, 0f);
            interactor.targetCamera = camera;

            GameObject lightObject =
                new GameObject("Directional Light");
            Light light =
                lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation =
                Quaternion.Euler(50f, -30f, 0f);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(interactor);
        }

        // R8 검증 폴더 계층을 반복 실행에 안전하게 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(SettingsFolder);
            EnsureFolder(BlueprintsFolder);
            EnsureFolder(StagesFolder);
            EnsureFolder(ScenesFolder);
        }

        // 지정 ScriptableObject를 불러오거나 같은 경로에 새 자산을 생성합니다.
        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name =
                Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // 지정 경로의 필수 자산을 불러오고 없으면 즉시 오류를 냅니다.
        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException(
                    "Required R8 asset is missing: " + path);
            return asset;
        }

        // Assets부터 시작하는 폴더 경로를 상위부터 생성합니다.
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                current = next;
            }
        }
    }
}
