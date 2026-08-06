using System;
using System.IO;
using RogueDungeonLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLabConsumerVerification
{
    public static class RuntimeConsumerSmoke
    {
        private const string Root = "Assets/R9ConsumerRuntime";
        private const string SettingsPath = Root + "/RuntimeSettings.asset";
        private const string BlueprintPath = Root + "/SavedBlueprint.asset";
        private const string ProceduralPath = Root + "/ProceduralStage.asset";
        private const string SavedPath = Root + "/SavedStage.asset";
        private const string ScenePath = Root + "/RuntimeBuildSmoke.unity";
        private const string ExampleRoot =
            "Assets/RogueDungeonLab/Examples/RuntimeBuild";

        // Input System 없는 소비 프로젝트에서 절차·저장 RuntimeBuild를 검증하고 Windows Player를 빌드합니다.
        public static void VerifyAndBuild()
        {
            EnsureFolder(Root);
            RequireNoSampleAssembly();
            ValidateImportedExamples();
            RogueDungeonSettings settings = CreateSettings();
            DungeonBlueprintAsset blueprint = CreateBlueprint(settings);
            DungeonStageDefinition procedural = CreateProcedural(settings);
            DungeonStageDefinition saved = CreateSaved(blueprint);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateLoad(procedural, DungeonStageSourceMode.Procedural);
            ValidateLoad(saved, DungeonStageSourceMode.SavedBlueprint);
            CreateScene(procedural);
            BuildPlayer(ScenePath, ResolveBuildPath("R9RuntimeConsumer.exe"));
            Debug.Log("R9 Runtime Core consumer smoke succeeded.");
        }

        // 실험실 Sample 어셈블리가 Core-only 소비 프로젝트에 들어오지 않았는지 확인합니다.
        private static void RequireNoSampleAssembly()
        {
            System.Reflection.Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        "RogueDungeonLab.Samples",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Runtime Core consumer unexpectedly loaded the Lab Sample assembly.");
                }
            }
        }

        // 별도 Runtime Examples package의 Procedural·Saved Definition과 두 scene을 실제 로드합니다.
        private static void ValidateImportedExamples()
        {
            DungeonStageDefinition procedural =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                    ExampleRoot +
                    "/Stages/R9_ProceduralRuntimeStage.asset");
            DungeonStageDefinition saved =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                    ExampleRoot +
                    "/Stages/R9_SavedRuntimeStage.asset");
            if (procedural == null ||
                saved == null ||
                !File.Exists(
                    ExampleRoot +
                    "/Scenes/R9_ProceduralRuntimeBuild.unity") ||
                !File.Exists(
                    ExampleRoot +
                    "/Scenes/R9_SavedBlueprintRuntimeBuild.unity"))
            {
                throw new InvalidOperationException(
                    "Runtime Examples package is incomplete.");
            }
            ValidateLoad(procedural, DungeonStageSourceMode.Procedural);
            ValidateLoad(saved, DungeonStageSourceMode.SavedBlueprint);
        }

        // 작은 결정적 RuntimeBuild 레시피 자산을 생성하거나 같은 경로에서 갱신합니다.
        private static RogueDungeonSettings CreateSettings()
        {
            RogueDungeonSettings settings =
                AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.seed = 91001;
            settings.stageWidthCells = 24;
            settings.stageDepthCells = 24;
            settings.desiredRoomCount = 7;
            settings.generateOnPlay = false;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // Core 생성 API만 사용해 저장형 Blueprint 자산을 만듭니다.
        private static DungeonBlueprintAsset CreateBlueprint(
            RogueDungeonSettings settings)
        {
            DungeonGenerationRequest request =
                DungeonGenerationRequest.Create(
                    settings,
                    settings.seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    "r9-consumer-runtime");
            DungeonBlueprintGenerationResult generated =
                DungeonBlueprintGenerator.Generate(request);
            DungeonValidationReport validation =
                DungeonBlueprintValidator.Validate(generated.Blueprint);
            if (!validation.IsValid)
                throw new InvalidOperationException("Generated consumer Blueprint is invalid.");

            DungeonBlueprintAsset asset =
                AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(BlueprintPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
                AssetDatabase.CreateAsset(asset, BlueprintPath);
            }
            asset.Store(generated.Blueprint, request.recipeSnapshot);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // 고정 seed의 Procedural RuntimeBuild Definition을 생성합니다.
        private static DungeonStageDefinition CreateProcedural(
            RogueDungeonSettings settings)
        {
            DungeonStageDefinition definition =
                LoadOrCreateDefinition(ProceduralPath);
            definition.stageId = "r9-consumer-procedural-v1";
            definition.sourceMode = DungeonStageSourceMode.Procedural;
            definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
            definition.recipe = settings;
            definition.savedBlueprint = null;
            definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
            definition.fixedSeed = settings.seed;
            definition.generatorVersion = DungeonGeneratorVersions.LegacyV1;
            definition.missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback;
            definition.loadOnPlay = true;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // 저장 Blueprint를 재계산하지 않는 Saved RuntimeBuild Definition을 생성합니다.
        private static DungeonStageDefinition CreateSaved(
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageDefinition definition =
                LoadOrCreateDefinition(SavedPath);
            definition.stageId = "r9-consumer-saved-v1";
            definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
            definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
            definition.recipe = null;
            definition.savedBlueprint = blueprint;
            definition.stageOverrides = null;
            definition.contentCatalog = null;
            definition.bakedPrefab = null;
            definition.bakeManifest = null;
            definition.missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback;
            definition.loadOnPlay = true;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // 지정 경로의 Definition을 재사용하거나 새 영속 자산으로 생성합니다.
        private static DungeonStageDefinition LoadOrCreateDefinition(string path)
        {
            DungeonStageDefinition definition =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(path);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        // 소비 프로젝트에서 두 source가 같은 코어 Loader로 유효한 root를 만드는지 확인합니다.
        private static void ValidateLoad(
            DungeonStageDefinition definition,
            DungeonStageSourceMode expectedSource)
        {
            GameObject host = new GameObject("R9 Consumer Loader Host");
            try
            {
                DungeonStageInstance instance = DungeonStageLoader.Load(
                    new DungeonLoadContext(definition, host.transform));
                if (instance == null ||
                    instance.Root == null ||
                    instance.Blueprint == null ||
                    instance.SourceMode != expectedSource ||
                    !instance.ValidationReport.IsValid)
                {
                    throw new InvalidOperationException(
                        "Runtime Core consumer load did not produce a valid stage.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // HUD 없는 제품형 장면에 Generator와 기본 Camera·Light만 구성합니다.
        private static void CreateScene(DungeonStageDefinition definition)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject generatorObject = new GameObject("R9 Runtime Generator");
            RogueDungeonGenerator generator =
                generatorObject.AddComponent<RogueDungeonGenerator>();
            generator.stageDefinition = definition;

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(-28f, 34f, -28f);
            camera.transform.rotation = Quaternion.Euler(48f, 45f, 0f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // 단일 smoke scene을 Windows64 Development Player로 빌드하고 오류·경고를 차단합니다.
        private static void BuildPlayer(string scenePath, string outputPath)
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            UnityEditor.Build.Reporting.BuildReport report =
                BuildPipeline.BuildPlayer(options);
            if (report.summary.result !=
                    UnityEditor.Build.Reporting.BuildResult.Succeeded ||
                report.summary.totalErrors != 0 ||
                report.summary.totalWarnings != 0)
            {
                throw new InvalidOperationException(
                    "Runtime consumer Player build failed: " +
                    report.summary.result + ", errors=" +
                    report.summary.totalErrors + ", warnings=" +
                    report.summary.totalWarnings);
            }
        }

        // 명령줄 -rdlBuildPath가 있으면 사용하고 없으면 프로젝트 Build 폴더를 선택합니다.
        private static string ResolveBuildPath(string defaultName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        "-rdlBuildPath",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(arguments[i + 1]);
                }
            }
            string folder = Path.GetFullPath("Build");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, defaultName);
        }

        // Assets 아래 폴더를 부모부터 생성합니다.
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
