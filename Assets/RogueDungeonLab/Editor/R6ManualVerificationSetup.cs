using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueDungeonLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R6ManualVerificationSetup
    {
        private const int Seed = 73125;
        private const string Root = "Assets/R6ManualVerification";
        private const string SettingsFolder = Root + "/Settings";
        private const string DropTablesFolder = Root + "/DropTables";
        private const string BlueprintsFolder = Root + "/Blueprints";
        private const string StagesFolder = Root + "/Stages";
        private const string MaterialsFolder = Root + "/Materials";
        private const string ScenesFolder = Root + "/Scenes";

        private const string SettingsPath =
            SettingsFolder + "/R6_BakeSettings.asset";
        private const string EnemyDropTablePath =
            DropTablesFolder + "/R6_EnemyDrops.asset";
        private const string DestructibleDropTablePath =
            DropTablesFolder + "/R6_DestructibleDrops.asset";
        private const string BlueprintPath =
            BlueprintsFolder + "/R6_SavedBlueprint_Seed73125.asset";
        private const string StagePath =
            StagesFolder + "/R6_BakedStage.asset";
        private const string MaterialSetPath =
            MaterialsFolder + "/R6_DefaultBakeMaterialSet.asset";
        private const string ScenePath =
            ScenesFolder + "/R6_BakedStageVerification.unity";

        // 현재 장면 저장 여부를 확인한 뒤 R6 전용 자산을 Bake하고 검증 장면을 엽니다.
        [MenuItem("Tools/Rogue Dungeon Lab/R6 수동 검증 환경 생성", priority = 4)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateAll(true);
        }

        // Batchmode에서 같은 R6 전용 입력, Bake 산출물과 검증 장면을 구성합니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // 정상 Bake가 준비되었는지 확인한 뒤 commit 직전 실패가 이전 결과를 보존하는지 검사합니다.
        [MenuItem("Tools/Rogue Dungeon Lab/R6 재Bake 실패 보존 확인", priority = 5)]
        public static void VerifyRollback()
        {
            VerifyRollbackInternal(true);
        }

        // Batchmode에서 의도적으로 실패한 재Bake의 rollback 계약을 검사합니다.
        public static void VerifyRollbackFromBatch()
        {
            VerifyRollbackInternal(false);
        }

        // 전용 입력을 기준값으로 갱신하고 성공한 Bake만 참조한 장면을 반복 실행 가능하게 저장합니다.
        private static void CreateAll(bool showDialog)
        {
            EnsureFolders();
            WeightedDropTable enemyDrops = CreateDropTable(
                EnemyDropTablePath,
                "R6 Enemy Token",
                new Color(0.95f, 0.2f, 0.25f));
            WeightedDropTable destructibleDrops = CreateDropTable(
                DestructibleDropTablePath,
                "R6 Crate Token",
                new Color(1f, 0.55f, 0.12f));
            RogueDungeonSettings settings = CreateSettings(
                enemyDrops,
                destructibleDrops);
            DungeonBlueprintAsset blueprint = CreateSavedBlueprint(settings);
            DungeonStageDefinition stage = CreateSavedStage(blueprint);
            DungeonBakeMaterialSet materialSet = LoadOrCreateMaterialSet();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadRequiredAssets(
                out settings,
                out blueprint,
                out stage,
                out materialSet);

            DungeonStageBakeResult bakeResult = DungeonStageBaker.Bake(
                stage,
                materialSet,
                settings);
            RequireValid(
                bakeResult.ValidationReport,
                "R6 수동 검증 Bake 결과가 유효하지 않습니다.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadRequiredAssets(
                out settings,
                out blueprint,
                out stage,
                out materialSet);
            RequireValid(
                DungeonStageBaker.ValidateCurrentBake(stage),
                "저장 후 R6 Bake 최신성 검증에 실패했습니다.");

            CreateVerificationScene(settings, stage, blueprint);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "R6 manual verification environment is ready. Open " +
                ScenePath + " and press Play.");
            if (!showDialog) return;
            RogueDungeonLabWindow.Open();
            EditorUtility.DisplayDialog(
                "R6 수동 검증 환경 준비 완료",
                "R6_BakedStageVerification 장면과 실험실을 열었습니다.\n\n" +
                "Play에서 BakedPrefab 탐험·클릭 드랍을 확인하고, 스테이지 자산 탭의 R6 영역에서 최신성을 다시 검사하세요.\n" +
                "재Bake rollback은 별도 'R6 재Bake 실패 보존 확인' 메뉴로 검사할 수 있습니다.",
                "확인");
        }

        // 기존 정상 참조와 build mode를 기록하고 실패 주입 뒤 모두 그대로인지 검증합니다.
        private static void VerifyRollbackInternal(bool showDialog)
        {
            DungeonStageDefinition stage =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(StagePath);
            DungeonBakeMaterialSet materialSet =
                AssetDatabase.LoadAssetAtPath<DungeonBakeMaterialSet>(MaterialSetPath);
            RogueDungeonSettings settings =
                AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(SettingsPath);
            if (stage == null || materialSet == null || settings == null)
            {
                throw new InvalidOperationException(
                    "먼저 'R6 수동 검증 환경 생성'을 실행하세요.");
            }

            RequireValid(
                DungeonStageBaker.ValidateCurrentBake(stage),
                "rollback 검사 전에 정상 Bake가 필요합니다.");
            DungeonBakeManifest previousManifest = stage.bakeManifest;
            GameObject previousPrefab = stage.bakedPrefab;
            DungeonStageBuildMode previousBuildMode = stage.buildMode;
            bool failedAsExpected = false;
            try
            {
                DungeonStageBaker.Bake(
                    stage,
                    materialSet,
                    settings,
                    new DungeonStageBakeOptions
                    {
                        SimulateFailureBeforeCommit = true
                    });
            }
            catch (DungeonStageBakeException)
            {
                failedAsExpected = true;
            }

            if (!failedAsExpected)
            {
                throw new InvalidOperationException(
                    "의도적 재Bake 실패가 예외로 보고되지 않았습니다.");
            }
            if (stage.bakeManifest != previousManifest ||
                stage.bakedPrefab != previousPrefab ||
                stage.buildMode != previousBuildMode)
            {
                throw new InvalidOperationException(
                    "실패한 재Bake가 이전 정상 StageDefinition 참조를 변경했습니다.");
            }

            RequireValid(
                DungeonStageBaker.ValidateCurrentBake(stage),
                "실패 rollback 뒤 이전 Bake가 더 이상 유효하지 않습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log(
                "R6 simulated rebake failure preserved the previous valid Prefab and manifest.");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "R6 재Bake 실패 보존 확인 완료",
                    "commit 직전 실패를 주입했고 기존 Prefab, manifest, build mode와 최신성 검증이 모두 유지되었습니다.",
                    "확인");
            }
        }

        // R6 전용 입력과 장면 폴더를 상위부터 안전하게 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(SettingsFolder);
            EnsureFolder(DropTablesFolder);
            EnsureFolder(BlueprintsFolder);
            EnsureFolder(StagesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(ScenesFolder);
        }

        // 적 또는 파괴물 클릭이 항상 하나의 식별 가능한 드랍을 만들도록 영속 테이블을 갱신합니다.
        private static WeightedDropTable CreateDropTable(
            string path,
            string itemId,
            Color markerColor)
        {
            WeightedDropTable table = LoadOrCreate<WeightedDropTable>(path);
            Undo.RegisterCompleteObjectUndo(table, "R6 수동 검증 드랍 테이블 갱신");
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

        // 고정 StableV2 맵에 적과 파괴물이 충분히 배치되도록 전용 설정을 기준값으로 갱신합니다.
        private static RogueDungeonSettings CreateSettings(
            WeightedDropTable enemyDrops,
            WeightedDropTable destructibleDrops)
        {
            RogueDungeonSettings settings =
                LoadOrCreate<RogueDungeonSettings>(SettingsPath);
            Undo.RegisterCompleteObjectUndo(settings, "R6 수동 검증 설정 갱신");
            settings.ApplyPreset(DungeonPreset.Balanced);
            settings.seed = Seed;
            settings.generateOnPlay = false;
            settings.stageWidthCells = 48;
            settings.stageDepthCells = 40;
            settings.desiredRoomCount = 14;
            settings.contentSpacingCells = 0;
            settings.reservedEntranceRadiusCells = 2;
            settings.enemyProfile.baseDensity = 0.16f;
            settings.enemyProfile.maxCount = 48;
            settings.destructibleProfile.baseDensity = 0.14f;
            settings.destructibleProfile.maxCount = 44;
            settings.propProfile.baseDensity = 0.06f;
            settings.propProfile.maxCount = 36;
            settings.enemyDropTable = enemyDrops;
            settings.destructibleDropTable = destructibleDrops;
            settings.spawnDropMarkers = true;
            settings.resetDropStatsOnGenerate = true;
            settings.ClampValues();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // 고정 설정과 시드로 계산한 StableV2 Blueprint를 전용 자산에 깊은 복사해 저장합니다.
        private static DungeonBlueprintAsset CreateSavedBlueprint(
            RogueDungeonSettings settings)
        {
            DungeonGenerationRequest request = DungeonGenerationRequest.CreateStableV2(
                settings,
                Seed,
                null,
                "r6-manual-bake");
            DungeonBlueprint blueprint =
                DungeonBlueprintGenerator.Generate(request).Blueprint;
            DungeonValidationReport validation =
                DungeonStageAuthoringService.ValidateBlueprint(
                    blueprint,
                    null,
                    DungeonMissingContentPolicy.BuiltInFallback);
            RequireValid(validation, "R6 기준 Blueprint 생성 검증에 실패했습니다.");
            RequireManualContent(blueprint);

            DungeonBlueprintAsset asset =
                LoadOrCreate<DungeonBlueprintAsset>(BlueprintPath);
            Undo.RegisterCompleteObjectUndo(asset, "R6 수동 검증 Blueprint 갱신");
            DungeonBlueprint stored = blueprint.DeepClone();
            stored.authoringNote =
                "R6 수동 검증: StableV2 built-in, seed " + Seed;
            stored.createdUtcTicks =
                asset.blueprint != null && asset.blueprint.createdUtcTicks > 0L
                    ? asset.blueprint.createdUtcTicks
                    : DateTime.UtcNow.Ticks;
            stored.RefreshHash();
            asset.Store(stored, DungeonRecipeSnapshot.Capture(settings));
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // 기준 Blueprint에 클릭 검증용 적과 파괴물이 실제로 포함됐는지 확인합니다.
        private static void RequireManualContent(DungeonBlueprint blueprint)
        {
            int enemyCount = 0;
            int destructibleCount = 0;
            if (blueprint != null && blueprint.spawns != null)
            {
                for (int i = 0; i < blueprint.spawns.Count; i++)
                {
                    DungeonSpawnRecord spawn = blueprint.spawns[i];
                    if (spawn == null) continue;
                    if (spawn.category == DungeonSpawnCategory.Enemy)
                        enemyCount++;
                    else if (spawn.category == DungeonSpawnCategory.Destructible)
                        destructibleCount++;
                }
            }
            if (enemyCount < 1 || destructibleCount < 1)
            {
                throw new InvalidOperationException(
                    "R6 기준 Blueprint에는 적과 파괴물이 각각 하나 이상 필요합니다.");
            }
        }

        // 기존 정상 Bake 참조는 보존하면서 SavedBlueprint 입력 필드만 기준값으로 갱신합니다.
        private static DungeonStageDefinition CreateSavedStage(
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageDefinition stage =
                LoadOrCreate<DungeonStageDefinition>(StagePath);
            Undo.RegisterCompleteObjectUndo(stage, "R6 수동 검증 StageDefinition 갱신");
            bool hasCommittedBake =
                stage.bakedPrefab != null && stage.bakeManifest != null;
            SerializedObject serialized = new SerializedObject(stage);
            serialized.Update();
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue =
                (int)(hasCommittedBake
                    ? DungeonStageBuildMode.BakedPrefab
                    : DungeonStageBuildMode.RuntimeBuild);
            serialized.FindProperty("recipe").objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = blueprint;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue = Seed;
            serialized.FindProperty("generatorVersion").intValue =
                blueprint.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog").objectReferenceValue = null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        // 기존 기본 재질 세트는 사용자 색상 변경을 보존해 재사용하고 없을 때만 새 자산을 만듭니다.
        private static DungeonBakeMaterialSet LoadOrCreateMaterialSet()
        {
            DungeonBakeMaterialSet materialSet =
                AssetDatabase.LoadAssetAtPath<DungeonBakeMaterialSet>(
                    MaterialSetPath);
            if (materialSet == null)
            {
                materialSet =
                    DungeonStageBaker.CreateDefaultMaterialSetAsset(
                        MaterialSetPath);
            }
            if (!HasCompleteMaterialSet(materialSet))
            {
                throw new InvalidOperationException(
                    "R6 기본 Bake MaterialSet의 8개 슬롯이 모두 필요합니다.");
            }
            return materialSet;
        }

        // geometry와 여섯 built-in 범주의 영속 재질 슬롯이 모두 연결됐는지 확인합니다.
        private static bool HasCompleteMaterialSet(
            DungeonBakeMaterialSet materialSet)
        {
            return materialSet != null &&
                   materialSet.floor != null &&
                   materialSet.wall != null &&
                   materialSet.enemy != null &&
                   materialSet.destructible != null &&
                   materialSet.prop != null &&
                   materialSet.gimmick != null &&
                   materialSet.entrance != null &&
                   materialSet.exit != null;
        }

        // 저장 뒤 Unity가 재임포트한 네 입력 자산을 다시 읽고 누락을 즉시 차단합니다.
        private static void ReloadRequiredAssets(
            out RogueDungeonSettings settings,
            out DungeonBlueprintAsset blueprint,
            out DungeonStageDefinition stage,
            out DungeonBakeMaterialSet materialSet)
        {
            settings =
                AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(SettingsPath);
            blueprint =
                AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(BlueprintPath);
            stage =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(StagePath);
            materialSet =
                AssetDatabase.LoadAssetAtPath<DungeonBakeMaterialSet>(
                    MaterialSetPath);
            if (settings == null ||
                blueprint == null ||
                stage == null ||
                materialSet == null)
            {
                throw new InvalidOperationException(
                    "R6 수동 검증 필수 자산을 재임포트할 수 없습니다.");
            }
        }

        // Baked StageDefinition과 공통 탐험 시스템을 연결하고 실제 Prefab 로드까지 확인한 장면을 저장합니다.
        private static void CreateVerificationScene(
            RogueDungeonSettings settings,
            DungeonStageDefinition stage,
            DungeonBlueprintAsset blueprint)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("R6 수동 검증 장면 구성");

            GameObject generatorObject =
                CreateSceneObject("Rogue Dungeon Generator");
            RogueDungeonGenerator generator =
                Undo.AddComponent<RogueDungeonGenerator>(generatorObject);
            SerializedObject serializedGenerator = new SerializedObject(generator);
            serializedGenerator.Update();
            serializedGenerator.FindProperty("settings").objectReferenceValue = settings;
            serializedGenerator.FindProperty("stageDefinition").objectReferenceValue =
                stage;
            serializedGenerator.ApplyModifiedProperties();
            generator.settings = settings;
            generator.stageDefinition = stage;

            GameObject systems =
                CreateSceneObject("Rogue Dungeon Lab Systems");
            Undo.AddComponent<DropValidationService>(systems);
            RogueDungeonClickInteractor interactor =
                Undo.AddComponent<RogueDungeonClickInteractor>(systems);
            Undo.AddComponent<RuntimeLabHUD>(systems);

            GameObject cameraObject = CreateSceneObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = Undo.AddComponent<Camera>(cameraObject);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            Undo.AddComponent<AudioListener>(cameraObject);
            Undo.AddComponent<LabOrbitCamera>(cameraObject);
            cameraObject.transform.position = new Vector3(-35f, 45f, -35f);
            cameraObject.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            SerializedObject serializedInteractor = new SerializedObject(interactor);
            serializedInteractor.Update();
            serializedInteractor.FindProperty("targetCamera").objectReferenceValue =
                camera;
            serializedInteractor.ApplyModifiedProperties();

            GameObject lightObject = CreateSceneObject("Directional Light");
            Light light = Undo.AddComponent<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject note =
                CreateSceneObject("R6 BAKED PREFAB TEST - Read README_KO");
            note.transform.SetAsFirstSibling();
            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(interactor);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    "R6 수동 검증 장면을 저장하지 못했습니다.");
            }

            Scene reopened = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            generator =
                UnityEngine.Object.FindAnyObjectByType<RogueDungeonGenerator>();
            LabOrbitCamera orbit =
                UnityEngine.Object.FindAnyObjectByType<LabOrbitCamera>();
            if (generator == null ||
                !HasExpectedAssetPath(generator.settings, SettingsPath) ||
                !HasExpectedAssetPath(generator.stageDefinition, StagePath))
            {
                throw new InvalidOperationException(
                    "R6 장면 재개방 뒤 Generator 자산 참조가 복원되지 않았습니다.");
            }

            stage = generator.stageDefinition;
            blueprint =
                AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(
                    BlueprintPath);
            generator.LoadStageDefinition();
            ValidateLoadedBakedStage(generator, stage, blueprint);
            if (orbit != null) orbit.FocusBounds(generator.GeneratedBounds);
            EditorSceneManager.MarkSceneDirty(reopened);
            if (!EditorSceneManager.SaveScene(reopened, ScenePath))
            {
                throw new InvalidOperationException(
                    "검증한 R6 수동 장면을 저장하지 못했습니다.");
            }
            Selection.activeObject = generator.gameObject;
        }

        // 재임포트로 Unity wrapper가 교체돼도 기대 project asset 경로를 참조하는지 확인합니다.
        private static bool HasExpectedAssetPath(
            UnityEngine.Object asset,
            string expectedPath)
        {
            return asset != null &&
                   string.Equals(
                       AssetDatabase.GetAssetPath(asset),
                       expectedPath,
                       StringComparison.OrdinalIgnoreCase);
        }

        // 새 장면 GameObject를 만들고 한 Undo 그룹에 생성 작업을 등록합니다.
        private static GameObject CreateSceneObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "R6 수동 검증 오브젝트 생성");
            return gameObject;
        }

        // 장면이 SavedBlueprint+BakedPrefab 인스턴스와 영속 geometry를 실제로 사용했는지 검증합니다.
        private static void ValidateLoadedBakedStage(
            RogueDungeonGenerator generator,
            DungeonStageDefinition stage,
            DungeonBlueprintAsset blueprint)
        {
            DungeonStageInstance instance =
                generator != null ? generator.CurrentStageInstance : null;
            if (instance == null ||
                instance.SourceMode != DungeonStageSourceMode.SavedBlueprint ||
                instance.BuildMode != DungeonStageBuildMode.BakedPrefab)
            {
                throw new InvalidOperationException(
                    "R6 장면이 SavedBlueprint+BakedPrefab 경로를 로드하지 않았습니다.");
            }
            if (!string.Equals(
                    instance.Blueprint.blueprintHash,
                    blueprint.blueprint.blueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "R6 BakedPrefab의 Blueprint hash가 저장 원본과 다릅니다.");
            }

            GameObject root = instance.Root;
            if (root == null ||
                root.name != DungeonStageLoader.GeneratedRootName ||
                generator.transform.Find(DungeonStageLoader.GeneratedRootName) == null)
            {
                throw new InvalidOperationException(
                    "R6 BakedPrefab generated root를 찾을 수 없습니다.");
            }
            if (root.GetComponentInChildren<DungeonGeneratedMeshOwner>(true) != null)
            {
                throw new InvalidOperationException(
                    "R6 BakedPrefab에 transient Mesh 소유자가 남아 있습니다.");
            }
            RequirePersistentGeometry(root, "Geometry/Floor");
            RequirePersistentGeometry(root, "Geometry/Walls");

            GenerationReport report = generator.LastReport;
            if (report == null ||
                report.enemyCount < 1 ||
                report.destructibleCount < 1)
            {
                throw new InvalidOperationException(
                    "R6 검증 장면에는 클릭할 적과 파괴물이 각각 하나 이상 필요합니다.");
            }
            RequireValid(
                DungeonStageBaker.ValidateCurrentBake(stage),
                "장면 로드 뒤 R6 Bake 최신성 검증에 실패했습니다.");
        }

        // 지정 geometry 노드의 Mesh와 MeshCollider가 같은 영속 Mesh 자산을 참조하는지 확인합니다.
        private static void RequirePersistentGeometry(
            GameObject root,
            string hierarchyPath)
        {
            Transform target = root != null
                ? root.transform.Find(hierarchyPath)
                : null;
            MeshFilter filter = target != null
                ? target.GetComponent<MeshFilter>()
                : null;
            MeshCollider collider = target != null
                ? target.GetComponent<MeshCollider>()
                : null;
            if (filter == null ||
                collider == null ||
                filter.sharedMesh == null ||
                collider.sharedMesh != filter.sharedMesh ||
                !AssetDatabase.Contains(filter.sharedMesh))
            {
                throw new InvalidOperationException(
                    hierarchyPath +
                    "가 같은 영속 Mesh를 렌더링과 충돌에 사용하지 않습니다.");
            }
        }

        // 오류 issue를 코드와 함께 묶어 유효하지 않은 검증 결과를 즉시 예외로 바꿉니다.
        private static void RequireValid(
            DungeonValidationReport report,
            string message)
        {
            if (report != null && report.IsValid) return;
            StringBuilder detail = new StringBuilder(message);
            if (report != null && report.issues != null)
            {
                for (int i = 0; i < report.issues.Count; i++)
                {
                    DungeonValidationIssue issue = report.issues[i];
                    if (issue == null) continue;
                    detail.Append("\n");
                    detail.Append(issue.code);
                    detail.Append(": ");
                    detail.Append(issue.message);
                }
            }
            throw new InvalidOperationException(detail.ToString());
        }

        // 지정 ScriptableObject를 불러오거나 같은 전용 경로에 새 자산을 생성합니다.
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Assets부터 시작하는 폴더 경로를 상위부터 반복 실행에 안전하게 생성합니다.
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
