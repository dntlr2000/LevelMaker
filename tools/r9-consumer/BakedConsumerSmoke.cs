using System;
using System.IO;
using RogueDungeonLab;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RogueDungeonLabConsumerVerification
{
    public static class BakedConsumerSmoke
    {
        private const string Root = "Assets/R9ConsumerBaked";
        private const string ScenePath = Root + "/BakedStageSmoke.unity";
        private const string RendererPath = Root + "/R9UniversalRenderer.asset";
        private const string PipelinePath = Root + "/R9UniversalPipeline.asset";

        // 배포 묶음의 manifest 최신성·Baked loader 계약을 검증하고 Windows Player를 빌드합니다.
        public static void VerifyAndBuild()
        {
            EnsureFolder(Root);
            DungeonStageDefinition definition = FindBakedStage();
            RequireCurrentManifest(definition);
            ValidateBakedLoad(definition);
            EnsureUrpPipeline();
            CreateScene(definition);
            BuildPlayer(ScenePath, ResolveBuildPath("R9BakedConsumer.exe"));
            Debug.Log("R9 Baked Stage consumer smoke succeeded.");
        }

        // 소비 프로젝트로 가져온 유일한 SavedBlueprint+BakedPrefab Definition을 찾습니다.
        private static DungeonStageDefinition FindBakedStage()
        {
            string[] guids = AssetDatabase.FindAssets("t:DungeonStageDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                DungeonStageDefinition candidate =
                    AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                if (candidate != null &&
                    candidate.sourceMode == DungeonStageSourceMode.SavedBlueprint &&
                    candidate.buildMode == DungeonStageBuildMode.BakedPrefab &&
                    candidate.bakedPrefab != null &&
                    candidate.bakeManifest != null)
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException(
                "No imported Baked StageDefinition was found.");
        }

        // Runtime-safe 검증과 Editor 전체 fingerprint 검증이 모두 현재 상태인지 확인합니다.
        private static void RequireCurrentManifest(
            DungeonStageDefinition definition)
        {
            DungeonValidationReport runtime =
                DungeonBakeManifestValidator.Validate(
                    definition.bakeManifest,
                    definition.savedBlueprint,
                    definition.bakedPrefab,
                    definition.stageOverrides);
            DungeonValidationReport editor =
                DungeonStageBaker.ValidateCurrentBake(definition);
            if (!runtime.IsValid || !editor.IsValid)
            {
                throw new InvalidOperationException(
                    "Imported Baked manifest is stale or invalid.");
            }
        }

        // Baked 경로가 저장 hash와 stable identity를 유지하며 transient mesh 없이 로드되는지 확인합니다.
        private static void ValidateBakedLoad(
            DungeonStageDefinition definition)
        {
            GameObject host = new GameObject("R9 Baked Consumer Loader Host");
            try
            {
                DungeonStageInstance instance = DungeonStageLoader.Load(
                    new DungeonLoadContext(definition, host.transform));
                DungeonBakedStageMetadata metadata =
                    instance != null && instance.Root != null
                        ? instance.Root.GetComponent<DungeonBakedStageMetadata>()
                        : null;
                if (instance == null ||
                    instance.BuildMode != DungeonStageBuildMode.BakedPrefab ||
                    instance.Root == null ||
                    metadata == null ||
                    !string.Equals(
                        instance.FinalBlueprintHash,
                        definition.bakeManifest.finalBlueprintHash,
                        StringComparison.Ordinal) ||
                    instance.Root.GetComponentInChildren<DungeonGeneratedMeshOwner>(true) != null)
                {
                    throw new InvalidOperationException(
                        "Imported Baked stage did not preserve its runtime contract.");
                }
                DungeonSpawnIdentity[] identities =
                    instance.Root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
                if (identities.Length == 0)
                    throw new InvalidOperationException("Imported Baked stage has no stable spawn identities.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // sidecar가 요구한 URP에 최소 Renderer·Pipeline 자산을 연결해 Player shader stripping을 활성화합니다.
        private static void EnsureUrpPipeline()
        {
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        // HUD와 Sample 컴포넌트 없이 Baked Definition만 로드하는 제품형 장면을 저장합니다.
        private static void CreateScene(DungeonStageDefinition definition)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject generatorObject = new GameObject("R9 Baked Generator");
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

        // Baked smoke scene을 Windows64 Development Player로 빌드하고 오류·경고를 차단합니다.
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
                    "Baked consumer Player build failed: " +
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
