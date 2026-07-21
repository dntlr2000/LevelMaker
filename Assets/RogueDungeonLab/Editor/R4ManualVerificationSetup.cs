using System;
using System.Collections.Generic;
using System.IO;
using RogueDungeonLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R4ManualVerificationSetup
    {
        private const string Root = "Assets/R4ManualVerification";
        private const string Prefabs = Root + "/Prefabs";
        private const string Materials = Root + "/Materials";
        private const string DropTables = Root + "/DropTables";
        private const string Catalogs = Root + "/Catalogs";
        private const string Settings = Root + "/Settings";
        private const string Stages = Root + "/Stages";
        private const string Scenes = Root + "/Scenes";

        private const string CatalogDropPath = DropTables + "/R4_CatalogDrop.asset";
        private const string PrefabDropPath = DropTables + "/R4_PrefabDrop.asset";
        private const string SettingsPath = Settings + "/R4_ManualSettings.asset";
        private const string AutoCatalogPath = Catalogs + "/R4_AutoTargetCatalog.asset";
        private const string AuthoredCatalogPath = Catalogs + "/R4_AuthoredTargetCatalog.asset";
        private const string MissingCatalogPath = Catalogs + "/R4_MissingContentCatalog.asset";
        private const string AutoStagePath = Stages + "/R4_StableV2_AutoTarget.asset";
        private const string AuthoredStagePath = Stages + "/R4_StableV2_AuthoredTarget.asset";
        private const string MissingErrorStagePath = Stages + "/R4_Missing_Error.asset";
        private const string MissingFallbackStagePath = Stages + "/R4_Missing_Fallback.asset";
        private const string MissingSkipStagePath = Stages + "/R4_Missing_Skip.asset";
        private const string ScenePath = Scenes + "/R4_ManualVerification.unity";

        [MenuItem("Tools/Rogue Dungeon Lab/R4 수동 검증 환경 생성", priority = 2)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateAll(true);
        }

        // Batchmode에서도 같은 자산과 검증 장면을 생성할 수 있는 공개 진입점입니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // 사용자 드랍 자산을 보존해 정리하고 R4 Prefab·Catalog·Stage·Scene 전체를 구성합니다.
        private static void CreateAll(bool showDialog)
        {
            EnsureFolders();
            MoveExistingAsset(Root + "/R4_CatalogDrop.asset", CatalogDropPath);
            MoveExistingAsset(Root + "/DropTable.asset", PrefabDropPath);

            WeightedDropTable catalogDrop = CreateDropTable(
                CatalogDropPath,
                "CatalogDrop",
                new Color(1f, 0.82f, 0.15f, 1f));
            WeightedDropTable prefabDrop = CreateDropTable(
                PrefabDropPath,
                "PrefabDrop",
                new Color(1f, 0.2f, 0.75f, 1f));

            Material autoEnemyMaterial = CreateMaterial(
                Materials + "/R4_Enemy_Auto.mat",
                new Color(0.1f, 0.85f, 1f, 1f));
            Material authoredEnemyMaterial = CreateMaterial(
                Materials + "/R4_Enemy_Authored.mat",
                new Color(0.95f, 0.2f, 0.85f, 1f));
            Material breakableMaterial = CreateMaterial(
                Materials + "/R4_Breakable.mat",
                new Color(1f, 0.55f, 0.08f, 1f));

            GameObject autoEnemy = CreateAutoEnemyPrefab(
                Prefabs + "/R4_Enemy_AutoTarget.prefab",
                autoEnemyMaterial);
            GameObject authoredEnemy = CreateAuthoredEnemyPrefab(
                Prefabs + "/R4_Enemy_AuthoredTarget.prefab",
                authoredEnemyMaterial,
                prefabDrop);
            GameObject breakable = CreateBreakablePrefab(
                Prefabs + "/R4_Breakable_AutoTarget.prefab",
                breakableMaterial);

            RogueDungeonSettings settings = CreateSettings(catalogDrop);
            DungeonContentCatalog autoCatalog = CreateCatalog(
                AutoCatalogPath,
                autoEnemy,
                breakable,
                catalogDrop);
            DungeonContentCatalog authoredCatalog = CreateCatalog(
                AuthoredCatalogPath,
                authoredEnemy,
                breakable,
                catalogDrop);
            DungeonContentCatalog missingCatalog = CreateMissingCatalog();

            DungeonStageDefinition autoStage = CreateStage(
                AutoStagePath,
                settings,
                autoCatalog,
                DungeonMissingContentPolicy.Error);
            CreateStage(
                AuthoredStagePath,
                settings,
                authoredCatalog,
                DungeonMissingContentPolicy.Error);
            CreateStage(
                MissingErrorStagePath,
                settings,
                missingCatalog,
                DungeonMissingContentPolicy.Error);
            CreateStage(
                MissingFallbackStagePath,
                settings,
                missingCatalog,
                DungeonMissingContentPolicy.BuiltInFallback);
            CreateStage(
                MissingSkipStagePath,
                settings,
                missingCatalog,
                DungeonMissingContentPolicy.Skip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            settings = AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(SettingsPath);
            autoStage = AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(AutoStagePath);
            if (settings == null || autoStage == null)
                throw new InvalidOperationException("R4 manual settings or default stage could not be reloaded.");
            CreateVerificationScene(settings, autoStage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "R4 manual verification environment is ready. Open " +
                ScenePath + " and press Play.");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "R4 수동 검증 환경 준비 완료",
                    "R4_ManualVerification 장면이 열렸습니다.\n\n" +
                    "Play를 누른 뒤 청록색 적과 주황색 파괴물을 클릭하세요.\n" +
                    "추가 정책 검증은 Generator의 Stage Definition만 교체하면 됩니다.",
                    "확인");
            }
        }

        // 수동 검증용 하위 폴더를 반복 실행에도 안전하게 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Prefabs);
            EnsureFolder(Materials);
            EnsureFolder(DropTables);
            EnsureFolder(Catalogs);
            EnsureFolder(Settings);
            EnsureFolder(Stages);
            EnsureFolder(Scenes);
        }

        // 아직 대상 경로가 비어 있을 때만 사용자가 만든 기존 자산을 GUID 보존 이동합니다.
        private static void MoveExistingAsset(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null ||
                AssetDatabase.LoadMainAssetAtPath(destination) != null) return;
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Failed to move R4 manual asset: " + error);
        }

        // 항상 하나의 확정 드랍 항목을 갖는 검증용 테이블을 만들거나 기준값으로 복구합니다.
        private static WeightedDropTable CreateDropTable(
            string path,
            string itemId,
            Color markerColor)
        {
            WeightedDropTable table = LoadOrCreate<WeightedDropTable>(path);
            table.entries = new List<DropEntry>
            {
                new DropEntry
                {
                    itemId = itemId,
                    weight = 1f,
                    minQuantity = 1,
                    maxQuantity = 1,
                    representsNoDrop = false,
                    markerColor = markerColor
                }
            };
            EditorUtility.SetDirty(table);
            return table;
        }

        // 현재 렌더 파이프라인에서 사용할 수 있는 Lit 계열 Material 자산을 구성합니다.
        private static Material CreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                                Shader.Find("HDRP/Lit") ??
                                Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("No supported Lit shader was found for R4 manual materials.");
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // target이 없는 청록색 적 Prefab을 만들어 catalog 자동 보강 경로를 검증합니다.
        private static GameObject CreateAutoEnemyPrefab(string path, Material material)
        {
            GameObject root = new GameObject("R4 Enemy Auto Target");
            try
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body With Collider";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up;
                body.GetComponent<Renderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 자식 target의 고유 ID·드랍 테이블이 catalog보다 우선하는 자홍색 적 Prefab을 만듭니다.
        private static GameObject CreateAuthoredEnemyPrefab(
            string path,
            Material material,
            WeightedDropTable prefabDrop)
        {
            GameObject root = new GameObject("R4 Enemy Authored Target");
            try
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Authored Drop Target";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up;
                body.GetComponent<Renderer>().sharedMaterial = material;
                DestructibleDropTarget target = body.AddComponent<DestructibleDropTarget>();
                target.Configure("authored-enemy", DropSourceKind.Enemy, prefabDrop, true);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // target이 없는 주황색 파괴물 Prefab을 만들어 Destructible 자동 보강을 검증합니다.
        private static GameObject CreateBreakablePrefab(string path, Material material)
        {
            GameObject root = new GameObject("R4 Breakable Auto Target");
            try
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body With Collider";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up * 0.6f;
                body.transform.localScale = Vector3.one * 1.2f;
                body.GetComponent<Renderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // 검증 장면에서 콘텐츠가 충분히 보이도록 별도의 고밀도 설정 자산을 구성합니다.
        private static RogueDungeonSettings CreateSettings(WeightedDropTable catalogDrop)
        {
            RogueDungeonSettings settings = LoadOrCreate<RogueDungeonSettings>(SettingsPath);
            settings.ApplyPreset(DungeonPreset.Balanced);
            settings.seed = 12345;
            settings.contentSpacingCells = 0;
            settings.reservedEntranceRadiusCells = 2;
            settings.enemyProfile.baseDensity = 0.12f;
            settings.enemyProfile.maxCount = 45;
            settings.destructibleProfile.baseDensity = 0.1f;
            settings.destructibleProfile.maxCount = 40;
            settings.propProfile.baseDensity = 0.04f;
            settings.propProfile.maxCount = 40;
            settings.enemyDropTable = catalogDrop;
            settings.destructibleDropTable = catalogDrop;
            settings.spawnDropMarkers = true;
            settings.resetDropStatsOnGenerate = true;
            settings.generateOnPlay = false;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // 같은 logical key를 서로 다른 Prefab 표현에 연결하는 정상 catalog를 구성합니다.
        private static DungeonContentCatalog CreateCatalog(
            string path,
            GameObject enemyPrefab,
            GameObject breakablePrefab,
            WeightedDropTable catalogDrop)
        {
            DungeonContentCatalog catalog = LoadOrCreate<DungeonContentCatalog>(path);
            catalog.formatVersion = DungeonContentCatalog.CurrentFormatVersion;
            catalog.entries = new List<DungeonContentCatalogEntry>
            {
                CreateEntry(
                    "manual/enemy",
                    DungeonSpawnCategory.Enemy,
                    enemyPrefab,
                    catalogDrop,
                    "catalog-enemy"),
                CreateEntry(
                    "manual/destructible",
                    DungeonSpawnCategory.Destructible,
                    breakablePrefab,
                    catalogDrop,
                    "catalog-destructible")
            };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // 세 누락 정책이 같은 Blueprint key를 다르게 처리하도록 Prefab 없는 catalog를 만듭니다.
        private static DungeonContentCatalog CreateMissingCatalog()
        {
            DungeonContentCatalog catalog = LoadOrCreate<DungeonContentCatalog>(MissingCatalogPath);
            catalog.formatVersion = DungeonContentCatalog.CurrentFormatVersion;
            catalog.entries = new List<DungeonContentCatalogEntry>
            {
                CreateEntry(
                    "manual/missing-enemy",
                    DungeonSpawnCategory.Enemy,
                    null,
                    null,
                    "missing-enemy")
            };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // 공통 결정성·배치 필드를 채운 catalog entry를 만듭니다.
        private static DungeonContentCatalogEntry CreateEntry(
            string contentKey,
            DungeonSpawnCategory category,
            GameObject prefab,
            WeightedDropTable dropTable,
            string gameplayId)
        {
            return new DungeonContentCatalogEntry
            {
                contentKey = contentKey,
                category = category,
                prefab = prefab,
                weight = 1f,
                minProgression = 0f,
                maxProgression = 1f,
                placement = DungeonContentPlacement.Any,
                requiredRoomTags = new List<string>(),
                footprintCells = Vector2Int.one,
                minimumSpacingCells = 0,
                randomizeYaw = true,
                yawDegreesRange = new Vector2(0f, 360f),
                uniformScaleRange = Vector2.one,
                dropTable = dropTable,
                gameplayId = gameplayId
            };
        }

        // 고정 시드 StableV2 RuntimeBuild Stage Definition을 정책별로 구성합니다.
        private static DungeonStageDefinition CreateStage(
            string path,
            RogueDungeonSettings settings,
            DungeonContentCatalog catalog,
            DungeonMissingContentPolicy policy)
        {
            DungeonStageDefinition stage = LoadOrCreate<DungeonStageDefinition>(path);
            stage.sourceMode = DungeonStageSourceMode.Procedural;
            stage.buildMode = DungeonStageBuildMode.RuntimeBuild;
            stage.recipe = settings;
            stage.savedBlueprint = null;
            stage.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
            stage.fixedSeed = 12345;
            stage.generatorVersion = DungeonGeneratorVersions.StableV2;
            stage.contentCatalog = catalog;
            stage.missingContentPolicy = policy;
            stage.loadOnPlay = true;
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 필요한 시스템을 갖춘 독립 장면을 만들고 기본 AutoTarget Stage를 연결합니다.
        private static void CreateVerificationScene(
            RogueDungeonSettings settings,
            DungeonStageDefinition stage)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject generatorObject = new GameObject("Rogue Dungeon Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            SerializedObject serializedGenerator = new SerializedObject(generator);
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
            LabOrbitCamera orbit = cameraObject.AddComponent<LabOrbitCamera>();
            cameraObject.transform.position = new Vector3(-35f, 45f, -35f);
            cameraObject.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            interactor.targetCamera = camera;

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject notes = new GameObject("R4 MANUAL TEST - Read README_KO");
            notes.transform.SetAsFirstSibling();

            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(interactor);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save R4 manual verification scene.");

            Scene reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            generator = UnityEngine.Object.FindAnyObjectByType<RogueDungeonGenerator>();
            orbit = UnityEngine.Object.FindAnyObjectByType<LabOrbitCamera>();
            if (generator == null || generator.settings == null || generator.stageDefinition == null)
                throw new InvalidOperationException("R4 scene references were not restored after reopening.");
            generator.LoadStageDefinitionWithSeed(12345);
            ValidateLoadedPreview(generator);
            if (orbit != null) orbit.FocusBounds(generator.GeneratedBounds);
            EditorSceneManager.MarkSceneDirty(reopened);
            if (!EditorSceneManager.SaveScene(reopened, ScenePath))
                throw new InvalidOperationException("Failed to save the validated R4 manual scene.");
            Selection.activeObject = generator.gameObject;
        }

        // 저장 전에 StableV2와 두 custom 범주가 실제로 구축됐는지 확인해 빈 검증 장면 생성을 차단합니다.
        private static void ValidateLoadedPreview(RogueDungeonGenerator generator)
        {
            if (generator == null ||
                generator.CurrentBlueprint == null ||
                generator.CurrentBlueprint.generatorVersion != DungeonGeneratorVersions.StableV2)
            {
                throw new InvalidOperationException(
                    "R4 manual scene failed to load a StableV2 Blueprint.");
            }
            GenerationReport report = generator.LastReport;
            if (report == null || report.enemyCount < 1 || report.destructibleCount < 1)
            {
                throw new InvalidOperationException(
                    "R4 manual scene must contain at least one enemy and one destructible.");
            }
            Transform generated = generator.transform.Find(DungeonStageLoader.GeneratedRootName);
            if (generated == null)
            {
                throw new InvalidOperationException(
                    "R4 manual scene did not create the generated stage root.");
            }
        }

        // 지정 ScriptableObject를 불러오거나 동일 경로에 새 자산을 생성합니다.
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Assets부터 시작하는 폴더 경로를 상위부터 순서대로 생성합니다.
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
