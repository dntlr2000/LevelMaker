using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Editor
{
    public static class R7ManualVerificationSetup
    {
        private const string R6Root = "Assets/R6ManualVerification";
        private const string R6SettingsPath =
            R6Root + "/Settings/R6_BakeSettings.asset";
        private const string R6BlueprintPath =
            R6Root + "/Blueprints/R6_SavedBlueprint_Seed73125.asset";
        private const string R6MaterialSetPath =
            R6Root + "/Materials/R6_DefaultBakeMaterialSet.asset";

        private const string Root = "Assets/R7ManualVerification";
        private const string OverridesFolder = Root + "/Overrides";
        private const string StagesFolder = Root + "/Stages";
        private const string ScenesFolder = Root + "/Scenes";
        private const string OverridesPath =
            OverridesFolder + "/R7_StageOverrides.asset";
        private const string RuntimeStagePath =
            StagesFolder + "/R7_RuntimeBuildStage.asset";
        private const string BakedStagePath =
            StagesFolder + "/R7_BakedStage.asset";
        private const string ScenePath =
            ScenesFolder + "/R7_StageOverrideVerification.unity";

        private const string RuntimeGeneratorName =
            "R7 RuntimeBuild Generator (ACTIVE)";
        private const string BakedGeneratorName =
            "R7 BakedPrefab Generator (toggle after disabling Runtime)";

        // 현재 장면 저장 여부를 확인한 뒤 R6 기준 입력에서 R7 전용 자산과 수동 검증 장면을 만듭니다.
        [MenuItem(
            "Tools/Rogue Dungeon Lab/R7 수동 검증 환경 생성",
            priority = 6)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            CreateAll(true);
        }

        // Batchmode에서 대화상자 없이 같은 R7 자산·Bake·장면과 hash 검증을 수행합니다.
        public static void CreateAllFromBatch()
        {
            CreateAll(false);
        }

        // R6 기준 자산을 갱신한 뒤 R7 Override, 두 Definition, v2 Bake와 장면을 반복 실행 가능하게 구성합니다.
        private static void CreateAll(bool showDialog)
        {
            R6ManualVerificationSetup.CreateAllFromBatch();
            EnsureFolders();

            RogueDungeonSettings settings =
                RequireAsset<RogueDungeonSettings>(R6SettingsPath);
            DungeonBlueprintAsset blueprint =
                RequireAsset<DungeonBlueprintAsset>(R6BlueprintPath);
            DungeonBakeMaterialSet materialSet =
                RequireAsset<DungeonBakeMaterialSet>(R6MaterialSetPath);
            DungeonStageOverrides stageOverrides =
                CreateStageOverrides(blueprint);
            DungeonStageOverrideApplyResult expected =
                RequireAppliedBlueprint(blueprint, stageOverrides);
            DungeonStageDefinition runtimeStage =
                CreateStageDefinition(
                    RuntimeStagePath,
                    blueprint,
                    stageOverrides,
                    false);
            DungeonStageDefinition bakedStage =
                CreateStageDefinition(
                    BakedStagePath,
                    blueprint,
                    stageOverrides,
                    true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadRequiredAssets(
                out settings,
                out blueprint,
                out materialSet,
                out stageOverrides,
                out runtimeStage,
                out bakedStage);
            expected = RequireAppliedBlueprint(blueprint, stageOverrides);

            DungeonStageBakeResult bakeResult = DungeonStageBaker.Bake(
                bakedStage,
                materialSet,
                settings);
            RequireValid(
                bakeResult.ValidationReport,
                "R7 v2 Bake 결과가 유효하지 않습니다.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadRequiredAssets(
                out settings,
                out blueprint,
                out materialSet,
                out stageOverrides,
                out runtimeStage,
                out bakedStage);
            expected = RequireAppliedBlueprint(blueprint, stageOverrides);
            ValidateBakedMetadata(
                bakedStage,
                blueprint,
                stageOverrides,
                expected);
            CreateVerificationScene(
                settings,
                blueprint,
                stageOverrides,
                runtimeStage,
                bakedStage,
                expected);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "R7 manual verification environment is ready. Open " +
                ScenePath + " and press Play.");
            if (!showDialog) return;

            RogueDungeonLabWindow.Open();
            EditorUtility.DisplayDialog(
                "R7 수동 검증 환경 준비 완료",
                "R7_StageOverrideVerification 장면과 실험실을 열었습니다.\n\n" +
                "처음에는 RuntimeBuild Generator가 활성 상태입니다. Baked 결과를 볼 때는 Runtime Generator를 비활성화하고 Baked Generator를 활성화하세요.\n" +
                "재실행하면 R7 전용 Override 기준값은 초기화됩니다.",
                "확인");
        }

        // 기준 Blueprint의 네 범주 Spawn을 선택해 disable·content·절대 transform·추가 작업을 직렬화합니다.
        private static DungeonStageOverrides CreateStageOverrides(
            DungeonBlueprintAsset blueprintAsset)
        {
            if (blueprintAsset == null || blueprintAsset.blueprint == null)
                throw new InvalidOperationException(
                    "R7 기준 Blueprint 데이터가 없습니다.");
            DungeonBlueprint blueprint = blueprintAsset.blueprint;
            DungeonSpawnRecord enemy =
                FindFirstSpawn(blueprint, DungeonSpawnCategory.Enemy);
            DungeonSpawnRecord prop =
                FindFirstSpawn(blueprint, DungeonSpawnCategory.Prop);
            DungeonSpawnRecord destructible =
                FindFirstSpawn(blueprint, DungeonSpawnCategory.Destructible);
            if (enemy == null || prop == null || destructible == null)
            {
                throw new InvalidOperationException(
                    "R7 기준 Blueprint에는 Enemy, Prop, Destructible이 각각 하나 이상 필요합니다.");
            }

            DungeonCellRecord addedCell =
                FindUnoccupiedFloorCell(blueprint);
            if (addedCell == null)
                throw new InvalidOperationException(
                    "R7 수동 추가 Spawn에 사용할 빈 floor cell을 찾지 못했습니다.");
            DungeonSpawnRecord added =
                CreateAddedDestructible(blueprint, addedCell);
            string replacementKey =
                string.Equals(
                    prop.contentKey,
                    DungeonBuiltInContentKeys.PropCylinder,
                    StringComparison.Ordinal)
                    ? DungeonBuiltInContentKeys.PropCube
                    : DungeonBuiltInContentKeys.PropCylinder;

            DungeonStageOverrides stageOverrides =
                LoadOrCreate<DungeonStageOverrides>(OverridesPath);
            Undo.RegisterCompleteObjectUndo(
                stageOverrides,
                "R7 수동 검증 Override 갱신");
            SerializedObject serialized =
                new SerializedObject(stageOverrides);
            serialized.Update();
            serialized.FindProperty("formatVersion").intValue =
                DungeonStageOverrideFormat.CurrentVersion;
            serialized.FindProperty("baseBlueprint").objectReferenceValue =
                blueprintAsset;
            serialized.FindProperty("baseBlueprintHash").stringValue =
                DungeonBlueprintHasher.Compute(blueprint);
            serialized.FindProperty("authoringNote").stringValue =
                "R7 수동 검증 기준: enemy disable, prop replacement, " +
                "destructible absolute transform, added destructible";

            SerializedProperty disabled =
                serialized.FindProperty("disabledSpawns");
            disabled.arraySize = 1;
            SerializedProperty disabledEntry =
                disabled.GetArrayElementAtIndex(0);
            disabledEntry.FindPropertyRelative("recordId").stringValue =
                "r7-manual-disable-enemy";
            WriteBinding(
                disabledEntry.FindPropertyRelative("binding"),
                enemy);

            SerializedProperty content =
                serialized.FindProperty("contentOverrides");
            content.arraySize = 1;
            SerializedProperty contentEntry =
                content.GetArrayElementAtIndex(0);
            contentEntry.FindPropertyRelative("recordId").stringValue =
                "r7-manual-replace-prop";
            WriteBinding(
                contentEntry.FindPropertyRelative("binding"),
                prop);
            contentEntry.FindPropertyRelative("replacementContentKey")
                .stringValue = replacementKey;

            SerializedProperty transforms =
                serialized.FindProperty("transformOverrides");
            transforms.arraySize = 1;
            SerializedProperty transformEntry =
                transforms.GetArrayElementAtIndex(0);
            transformEntry.FindPropertyRelative("recordId").stringValue =
                "r7-manual-transform-destructible";
            WriteBinding(
                transformEntry.FindPropertyRelative("binding"),
                destructible);
            transformEntry.FindPropertyRelative("localPosition").vector3Value =
                destructible.localPosition + new Vector3(0.4f, 0.2f, 0.25f);
            transformEntry.FindPropertyRelative("pitchDegrees").floatValue =
                destructible.pitchDegrees;
            transformEntry.FindPropertyRelative("yawDegrees").floatValue =
                destructible.yawDegrees + 45f;
            transformEntry.FindPropertyRelative("rollDegrees").floatValue =
                destructible.rollDegrees;
            transformEntry.FindPropertyRelative("localScale").vector3Value =
                destructible.localScale * 1.15f;

            SerializedProperty addedSpawns =
                serialized.FindProperty("addedSpawns");
            addedSpawns.arraySize = 1;
            WriteSpawn(
                addedSpawns.GetArrayElementAtIndex(0),
                added);
            serialized.ApplyModifiedProperties();
            stageOverrides.RefreshHash();
            EditorUtility.SetDirty(stageOverrides);
            AssetDatabase.SaveAssetIfDirty(stageOverrides);

            RequireValid(
                DungeonStageOverridesValidator.Validate(
                    stageOverrides,
                    blueprintAsset,
                    true),
                "R7 기준 Override 검증에 실패했습니다.");
            return stageOverrides;
        }

        // RuntimeBuild 또는 BakedPrefab 전용 Definition을 같은 SavedBlueprint+Override 입력으로 갱신합니다.
        private static DungeonStageDefinition CreateStageDefinition(
            string path,
            DungeonBlueprintAsset blueprint,
            DungeonStageOverrides stageOverrides,
            bool baked)
        {
            DungeonStageDefinition definition =
                LoadOrCreate<DungeonStageDefinition>(path);
            bool preserveCommittedBake =
                baked &&
                definition.bakedPrefab != null &&
                definition.bakeManifest != null;
            Undo.RegisterCompleteObjectUndo(
                definition,
                baked
                    ? "R7 Baked StageDefinition 갱신"
                    : "R7 Runtime StageDefinition 갱신");
            SerializedObject serialized = new SerializedObject(definition);
            serialized.Update();
            serialized.FindProperty("stageId").stringValue = baked
                ? "r7-manual-baked-v1"
                : "r7-manual-runtime-v1";
            serialized.FindProperty("sourceMode").intValue =
                (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue =
                (int)(preserveCommittedBake
                    ? DungeonStageBuildMode.BakedPrefab
                    : DungeonStageBuildMode.RuntimeBuild);
            serialized.FindProperty("recipe").objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint").objectReferenceValue =
                blueprint;
            serialized.FindProperty("stageOverrides").objectReferenceValue =
                stageOverrides;
            serialized.FindProperty("seedPolicy").intValue =
                (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue =
                blueprint.blueprint.seed;
            serialized.FindProperty("generatorVersion").intValue =
                blueprint.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog").objectReferenceValue =
                null;
            serialized.FindProperty("missingContentPolicy").intValue =
                (int)DungeonMissingContentPolicy.BuiltInFallback;
            serialized.FindProperty("loadOnPlay").boolValue = true;
            if (!baked)
            {
                serialized.FindProperty("bakedPrefab").objectReferenceValue =
                    null;
                serialized.FindProperty("bakeManifest").objectReferenceValue =
                    null;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        // Override 적용 결과가 원본과 분리된 유효 Blueprint와 저장 hash를 생성하는지 검사합니다.
        private static DungeonStageOverrideApplyResult RequireAppliedBlueprint(
            DungeonBlueprintAsset blueprint,
            DungeonStageOverrides stageOverrides)
        {
            DungeonStageOverrideApplyResult result =
                DungeonStageOverrideApplier.Apply(
                    blueprint,
                    stageOverrides,
                    true);
            RequireValid(
                result.ValidationReport,
                "R7 Override 적용 결과가 유효하지 않습니다.");
            if (!result.IsValid ||
                result.FinalBlueprint == null ||
                string.IsNullOrEmpty(result.FinalBlueprintHash) ||
                !string.Equals(
                    result.SourceBlueprintHash,
                    stageOverrides.baseBlueprintHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    result.OverrideHash,
                    stageOverrides.overrideHash,
                    StringComparison.Ordinal) ||
                string.Equals(
                    result.FinalBlueprintHash,
                    result.SourceBlueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "R7 source/override/final hash 계약이 적용 결과에 반영되지 않았습니다.");
            }
            return result;
        }

        // v2 manifest·Prefab metadata가 동일한 원본·Override·최종 hash를 기록했는지 검사합니다.
        private static void ValidateBakedMetadata(
            DungeonStageDefinition bakedStage,
            DungeonBlueprintAsset blueprint,
            DungeonStageOverrides stageOverrides,
            DungeonStageOverrideApplyResult expected)
        {
            RequireValid(
                DungeonStageBaker.ValidateCurrentBake(bakedStage),
                "R7 Baked Stage 최신성 검증에 실패했습니다.");
            DungeonBakeManifest manifest = bakedStage.bakeManifest;
            GameObject prefab = bakedStage.bakedPrefab;
            if (manifest == null ||
                prefab == null ||
                manifest.formatVersion !=
                DungeonBakeFormat.StageOverridesV2 ||
                manifest.builderVersion !=
                DungeonBakeBuilderVersions.StageOverridesV2 ||
                manifest.sourceBlueprint != blueprint ||
                manifest.sourceOverrides != stageOverrides ||
                !string.Equals(
                    manifest.sourceBlueprintHash,
                    expected.SourceBlueprintHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.overrideHash,
                    expected.OverrideHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.finalBlueprintHash,
                    expected.FinalBlueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "R7 v2 manifest의 원본·Override·최종 Blueprint metadata가 일치하지 않습니다.");
            }

            RequireValid(
                DungeonBakeManifestValidator.Validate(
                    manifest,
                    blueprint,
                    prefab,
                    stageOverrides),
                "R7 v2 manifest Runtime 계약 검증에 실패했습니다.");
            DungeonBakedStageMetadata metadata =
                prefab.GetComponent<DungeonBakedStageMetadata>();
            if (metadata == null ||
                metadata.FormatVersion !=
                DungeonBakeFormat.StageOverridesV2 ||
                metadata.BuilderVersion !=
                DungeonBakeBuilderVersions.StageOverridesV2 ||
                !string.Equals(
                    metadata.FinalBlueprintHash,
                    expected.FinalBlueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "R7 Baked Prefab metadata의 형식 또는 final Blueprint hash가 다릅니다.");
            }
            RequireValid(
                metadata.Validate(manifest),
                "R7 Baked Prefab metadata 검증에 실패했습니다.");
        }

        // 두 Generator와 공통 탐험 시스템을 만들고 RuntimeBuild/Baked 결과를 실제 로드해 비교한 장면을 저장합니다.
        private static void CreateVerificationScene(
            RogueDungeonSettings settings,
            DungeonBlueprintAsset blueprint,
            DungeonStageOverrides stageOverrides,
            DungeonStageDefinition runtimeStage,
            DungeonStageDefinition bakedStage,
            DungeonStageOverrideApplyResult expected)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("R7 수동 검증 장면 구성");

            RogueDungeonGenerator runtimeGenerator =
                CreateGenerator(
                    RuntimeGeneratorName,
                    settings,
                    runtimeStage);
            RogueDungeonGenerator bakedGenerator =
                CreateGenerator(
                    BakedGeneratorName,
                    settings,
                    bakedStage);
            CreateCommonSceneSystems(out _);
            CreateSceneObject(
                "R7 TEST: Runtime active / Baked inactive - read docs");
            Undo.CollapseUndoOperations(undoGroup);
            AssignGeneratorAssets(
                runtimeGenerator,
                settings,
                runtimeStage);
            AssignGeneratorAssets(
                bakedGenerator,
                settings,
                bakedStage);

            runtimeGenerator.ClearGenerated();
            bakedGenerator.ClearGenerated();
            bakedGenerator.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "R7 수동 검증 장면을 저장하지 못했습니다.");

            Scene reopened = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            runtimeGenerator = FindGenerator(RuntimeGeneratorName);
            bakedGenerator = FindGenerator(BakedGeneratorName);
            LabOrbitCamera orbit =
                UnityEngine.Object.FindAnyObjectByType<LabOrbitCamera>();
            if (runtimeGenerator == null ||
                bakedGenerator == null ||
                !HasExpectedAssetPath(
                    runtimeGenerator.settings,
                    R6SettingsPath) ||
                !HasExpectedAssetPath(
                    runtimeGenerator.stageDefinition,
                    RuntimeStagePath) ||
                !HasExpectedAssetPath(
                    bakedGenerator.stageDefinition,
                    BakedStagePath))
            {
                throw new InvalidOperationException(
                    "R7 장면 재개방 뒤 Generator 자산 참조가 복원되지 않았습니다.");
            }

            blueprint =
                RequireAsset<DungeonBlueprintAsset>(R6BlueprintPath);
            stageOverrides =
                RequireAsset<DungeonStageOverrides>(OverridesPath);
            expected =
                RequireAppliedBlueprint(blueprint, stageOverrides);
            runtimeGenerator.gameObject.SetActive(true);
            bakedGenerator.gameObject.SetActive(true);
            runtimeGenerator.LoadStageDefinition();
            bakedGenerator.LoadStageDefinition();
            ValidateLoadedStage(
                runtimeGenerator,
                runtimeGenerator.stageDefinition,
                blueprint,
                stageOverrides,
                expected,
                DungeonStageBuildMode.RuntimeBuild);
            ValidateLoadedStage(
                bakedGenerator,
                bakedGenerator.stageDefinition,
                blueprint,
                stageOverrides,
                expected,
                DungeonStageBuildMode.BakedPrefab);
            ValidateStageParity(
                runtimeGenerator.CurrentStageInstance,
                bakedGenerator.CurrentStageInstance,
                expected.FinalBlueprint);
            if (orbit != null)
                orbit.FocusBounds(runtimeGenerator.GeneratedBounds);

            runtimeGenerator.ClearGenerated();
            bakedGenerator.ClearGenerated();
            bakedGenerator.gameObject.SetActive(false);
            Selection.activeObject = runtimeGenerator.gameObject;
            EditorSceneManager.MarkSceneDirty(reopened);
            if (!EditorSceneManager.SaveScene(reopened, ScenePath))
                throw new InvalidOperationException(
                    "검증 완료 R7 장면을 저장하지 못했습니다.");
        }

        // 현재 StageInstance의 source/override/final hash와 실제 Spawn identity가 기대 최종 Blueprint와 같은지 확인합니다.
        private static void ValidateLoadedStage(
            RogueDungeonGenerator generator,
            DungeonStageDefinition definition,
            DungeonBlueprintAsset source,
            DungeonStageOverrides stageOverrides,
            DungeonStageOverrideApplyResult expected,
            DungeonStageBuildMode expectedBuildMode)
        {
            DungeonStageInstance instance =
                generator != null ? generator.CurrentStageInstance : null;
            if (instance == null ||
                instance.Definition != definition ||
                instance.SourceMode !=
                DungeonStageSourceMode.SavedBlueprint ||
                instance.BuildMode != expectedBuildMode ||
                instance.AppliedOverrides != stageOverrides ||
                !string.Equals(
                    instance.SourceBlueprintHash,
                    expected.SourceBlueprintHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    instance.OverrideHash,
                    expected.OverrideHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    instance.FinalBlueprintHash,
                    expected.FinalBlueprintHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    instance.Blueprint.blueprintHash,
                    expected.FinalBlueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    expectedBuildMode +
                    " StageInstance의 Override metadata 또는 final hash가 다릅니다.");
            }
            if (source == null ||
                !string.Equals(
                    DungeonBlueprintHasher.Compute(source.blueprint),
                    stageOverrides.baseBlueprintHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "R7 Preview 또는 Load가 원본 Blueprint를 변경했습니다.");
            }
            ValidateSpawnIdentities(
                instance.Root,
                expected.FinalBlueprint);
        }

        // RuntimeBuild와 BakedPrefab의 Blueprint·identity·report 논리 결과가 동일한지 비교합니다.
        private static void ValidateStageParity(
            DungeonStageInstance runtime,
            DungeonStageInstance baked,
            DungeonBlueprint expectedBlueprint)
        {
            if (runtime == null ||
                baked == null ||
                expectedBlueprint == null ||
                !string.Equals(
                    runtime.FinalBlueprintHash,
                    baked.FinalBlueprintHash,
                    StringComparison.Ordinal) ||
                runtime.BuildResult.ContentCounts.EnemyCount !=
                baked.BuildResult.ContentCounts.EnemyCount ||
                runtime.BuildResult.ContentCounts.DestructibleCount !=
                baked.BuildResult.ContentCounts.DestructibleCount ||
                runtime.BuildResult.ContentCounts.PropCount !=
                baked.BuildResult.ContentCounts.PropCount ||
                runtime.BuildResult.ContentCounts.GimmickCount !=
                baked.BuildResult.ContentCounts.GimmickCount)
            {
                throw new InvalidOperationException(
                    "R7 RuntimeBuild와 BakedPrefab의 final Blueprint 또는 구축 개수가 다릅니다.");
            }

            List<string> runtimeSignatures =
                CaptureIdentitySignatures(runtime.Root);
            List<string> bakedSignatures =
                CaptureIdentitySignatures(baked.Root);
            if (runtimeSignatures.Count != bakedSignatures.Count ||
                runtimeSignatures.Count != expectedBlueprint.spawns.Count)
            {
                throw new InvalidOperationException(
                    "R7 RuntimeBuild와 BakedPrefab의 Spawn identity 수가 다릅니다.");
            }
            for (int i = 0; i < runtimeSignatures.Count; i++)
            {
                if (!string.Equals(
                        runtimeSignatures[i],
                        bakedSignatures[i],
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "R7 RuntimeBuild와 BakedPrefab의 Spawn identity 또는 Transform이 다릅니다.");
                }
            }
        }

        // 최종 Blueprint의 모든 Spawn이 generated root의 stable identity와 절대 로컬 Transform으로 실현됐는지 검사합니다.
        private static void ValidateSpawnIdentities(
            GameObject root,
            DungeonBlueprint expected)
        {
            if (root == null || expected == null || expected.spawns == null)
                throw new InvalidOperationException(
                    "R7 Spawn identity 검증 입력이 없습니다.");
            Dictionary<string, DungeonSpawnIdentity> identities =
                BuildIdentityLookup(root);
            if (identities.Count != expected.spawns.Count)
                throw new InvalidOperationException(
                    "R7 generated identity 수가 final Blueprint Spawn 수와 다릅니다.");
            for (int i = 0; i < expected.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = expected.spawns[i];
                DungeonSpawnIdentity identity;
                if (spawn == null ||
                    !identities.TryGetValue(
                        spawn.spawnId,
                        out identity) ||
                    identity.Category != spawn.category ||
                    identity.Cell != spawn.cell ||
                    !string.Equals(
                        identity.ContentKey,
                        spawn.contentKey,
                        StringComparison.Ordinal) ||
                    !Approximately(
                        identity.transform.localPosition,
                        spawn.localPosition) ||
                    !Approximately(
                        identity.transform.localScale,
                        spawn.localScale) ||
                    Quaternion.Angle(
                        identity.transform.localRotation,
                        Quaternion.Euler(
                            spawn.pitchDegrees,
                            spawn.yawDegrees,
                            spawn.rollDegrees)) > 0.01f)
                {
                    throw new InvalidOperationException(
                        "R7 final Blueprint Spawn이 identity에 정확히 반영되지 않았습니다: " +
                        (spawn != null ? spawn.spawnId : "null"));
                }
            }
        }

        // generated root의 모든 identity를 stable ID lookup으로 만들고 중복을 차단합니다.
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
                if (identity == null ||
                    string.IsNullOrWhiteSpace(identity.SpawnId) ||
                    result.ContainsKey(identity.SpawnId))
                {
                    throw new InvalidOperationException(
                        "R7 generated root에 비어 있거나 중복된 Spawn identity가 있습니다.");
                }
                result.Add(identity.SpawnId, identity);
            }
            return result;
        }

        // 개별 Transform 검증을 마친 identity·콘텐츠·cell 서명을 stable ID 순으로 캡처합니다.
        private static List<string> CaptureIdentitySignatures(
            GameObject root)
        {
            Dictionary<string, DungeonSpawnIdentity> lookup =
                BuildIdentityLookup(root);
            List<string> ids = new List<string>(lookup.Keys);
            ids.Sort(StringComparer.Ordinal);
            List<string> signatures = new List<string>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                DungeonSpawnIdentity identity = lookup[ids[i]];
                signatures.Add(
                    identity.SpawnId + "|" +
                    (int)identity.Category + "|" +
                    identity.ContentKey + "|" +
                    identity.Cell.x + "," + identity.Cell.y);
            }
            return signatures;
        }

        // 공통 Drop·HUD·카메라·조명 시스템을 수동 장면에 구성합니다.
        private static void CreateCommonSceneSystems(
            out LabOrbitCamera orbit)
        {
            GameObject systems =
                CreateSceneObject("Rogue Dungeon Lab Systems");
            Undo.AddComponent<DropValidationService>(systems);
            RogueDungeonClickInteractor interactor =
                Undo.AddComponent<RogueDungeonClickInteractor>(systems);
            Undo.AddComponent<RuntimeLabHUD>(systems);

            GameObject cameraObject =
                CreateSceneObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = Undo.AddComponent<Camera>(cameraObject);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            Undo.AddComponent<AudioListener>(cameraObject);
            orbit = Undo.AddComponent<LabOrbitCamera>(cameraObject);
            cameraObject.transform.position =
                new Vector3(-35f, 45f, -35f);
            cameraObject.transform.rotation =
                Quaternion.Euler(50f, 45f, 0f);
            SerializedObject serializedInteractor =
                new SerializedObject(interactor);
            serializedInteractor.Update();
            serializedInteractor.FindProperty("targetCamera")
                .objectReferenceValue = camera;
            serializedInteractor.ApplyModifiedProperties();
            EditorUtility.SetDirty(interactor);

            GameObject lightObject =
                CreateSceneObject("Directional Light");
            Light light = Undo.AddComponent<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation =
                Quaternion.Euler(50f, -30f, 0f);
        }

        // 지정 설정과 Definition을 SerializedObject로 연결한 장면 Generator를 만듭니다.
        private static RogueDungeonGenerator CreateGenerator(
            string name,
            RogueDungeonSettings settings,
            DungeonStageDefinition definition)
        {
            GameObject gameObject = CreateSceneObject(name);
            RogueDungeonGenerator generator =
                Undo.AddComponent<RogueDungeonGenerator>(gameObject);
            AssignGeneratorAssets(generator, settings, definition);
            return generator;
        }

        // Undo 생성 그룹 전후에도 Generator의 설정·Definition 참조가 유지되도록 직렬화 필드를 다시 고정합니다.
        private static void AssignGeneratorAssets(
            RogueDungeonGenerator generator,
            RogueDungeonSettings settings,
            DungeonStageDefinition definition)
        {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));
            SerializedObject serialized = new SerializedObject(generator);
            serialized.Update();
            serialized.FindProperty("settings").objectReferenceValue =
                settings;
            serialized.FindProperty("stageDefinition").objectReferenceValue =
                definition;
            serialized.ApplyModifiedProperties();
            generator.settings = settings;
            generator.stageDefinition = definition;
            EditorUtility.SetDirty(generator);
        }

        // 기준 Blueprint에서 지정 범주의 stable ID 순 첫 Spawn을 선택합니다.
        private static DungeonSpawnRecord FindFirstSpawn(
            DungeonBlueprint blueprint,
            DungeonSpawnCategory category)
        {
            DungeonSpawnRecord selected = null;
            if (blueprint == null || blueprint.spawns == null) return null;
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn == null || spawn.category != category) continue;
                if (selected == null ||
                    string.CompareOrdinal(
                        spawn.spawnId,
                        selected.spawnId) < 0)
                {
                    selected = spawn;
                }
            }
            return selected;
        }

        // 입·출구와 기존 Spawn을 피하면서 입구에서 가장 먼 빈 floor cell을 선택합니다.
        private static DungeonCellRecord FindUnoccupiedFloorCell(
            DungeonBlueprint blueprint)
        {
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            if (blueprint.spawns != null)
            {
                for (int i = 0; i < blueprint.spawns.Count; i++)
                {
                    DungeonSpawnRecord spawn = blueprint.spawns[i];
                    if (spawn != null) occupied.Add(spawn.cell);
                }
            }

            DungeonCellRecord selected = null;
            if (blueprint.cells == null) return null;
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord cell = blueprint.cells[i];
                if (cell == null ||
                    (cell.flags & DungeonCellFlags.Floor) == 0 ||
                    cell.coordinate == blueprint.entrance ||
                    cell.coordinate == blueprint.exit ||
                    occupied.Contains(cell.coordinate))
                {
                    continue;
                }
                if (selected == null ||
                    cell.distanceFromEntrance >
                    selected.distanceFromEntrance)
                {
                    selected = cell;
                }
            }
            return selected;
        }

        // 빈 floor cell에 배치할 결정적인 R7 추가 Destructible 레코드를 만듭니다.
        private static DungeonSpawnRecord CreateAddedDestructible(
            DungeonBlueprint blueprint,
            DungeonCellRecord cell)
        {
            int maxDistance = 0;
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord candidate = blueprint.cells[i];
                if (candidate != null)
                    maxDistance = Mathf.Max(
                        maxDistance,
                        candidate.distanceFromEntrance);
            }
            float cellSize = blueprint.grid.cellSize;
            Vector3 center = new Vector3(
                (cell.coordinate.x - blueprint.grid.width * 0.5f + 0.5f) *
                cellSize,
                0.45f,
                (cell.coordinate.y - blueprint.grid.depth * 0.5f + 0.5f) *
                cellSize);
            return new DungeonSpawnRecord
            {
                spawnId =
                    "override:v1:r7-manual-added-destructible",
                category = DungeonSpawnCategory.Destructible,
                contentKey = DungeonBuiltInContentKeys.Destructible,
                instanceName = "R7_Added_Destructible",
                cell = cell.coordinate,
                localPosition = center,
                pitchDegrees = 0f,
                yawDegrees = 25f,
                rollDegrees = 0f,
                localScale = Vector3.one * 0.9f,
                roomId = cell.roomId ?? string.Empty,
                progression = maxDistance > 0
                    ? Mathf.Clamp01(
                        cell.distanceFromEntrance /
                        (float)maxDistance)
                    : 0f,
                tags = new List<string> { "r7-manual-added" },
                variantSeed = 77001
            };
        }

        // 재결합용 Binding Snapshot의 모든 필드를 원본 Spawn에서 직렬화합니다.
        private static void WriteBinding(
            SerializedProperty target,
            DungeonSpawnRecord source)
        {
            target.FindPropertyRelative("spawnId").stringValue =
                source.spawnId ?? string.Empty;
            target.FindPropertyRelative("category").intValue =
                (int)source.category;
            target.FindPropertyRelative("contentKey").stringValue =
                source.contentKey ?? string.Empty;
            target.FindPropertyRelative("cell").vector2IntValue =
                source.cell;
            target.FindPropertyRelative("roomId").stringValue =
                source.roomId ?? string.Empty;
            target.FindPropertyRelative("variantSeed").intValue =
                source.variantSeed;
        }

        // 추가 Spawn의 Blueprint 논리 필드 전체를 직렬화 배열 요소에 기록합니다.
        private static void WriteSpawn(
            SerializedProperty target,
            DungeonSpawnRecord source)
        {
            target.FindPropertyRelative("spawnId").stringValue =
                source.spawnId ?? string.Empty;
            target.FindPropertyRelative("category").intValue =
                (int)source.category;
            target.FindPropertyRelative("contentKey").stringValue =
                source.contentKey ?? string.Empty;
            target.FindPropertyRelative("instanceName").stringValue =
                source.instanceName ?? string.Empty;
            target.FindPropertyRelative("cell").vector2IntValue =
                source.cell;
            target.FindPropertyRelative("localPosition").vector3Value =
                source.localPosition;
            target.FindPropertyRelative("pitchDegrees").floatValue =
                source.pitchDegrees;
            target.FindPropertyRelative("yawDegrees").floatValue =
                source.yawDegrees;
            target.FindPropertyRelative("rollDegrees").floatValue =
                source.rollDegrees;
            target.FindPropertyRelative("localScale").vector3Value =
                source.localScale;
            target.FindPropertyRelative("roomId").stringValue =
                source.roomId ?? string.Empty;
            target.FindPropertyRelative("progression").floatValue =
                source.progression;
            SerializedProperty tags =
                target.FindPropertyRelative("tags");
            tags.arraySize =
                source.tags != null ? source.tags.Count : 0;
            for (int i = 0; i < tags.arraySize; i++)
                tags.GetArrayElementAtIndex(i).stringValue =
                    source.tags[i] ?? string.Empty;
            target.FindPropertyRelative("variantSeed").intValue =
                source.variantSeed;
        }

        // 장면 내부에서 이름이 같은 Generator를 inactive 오브젝트까지 포함해 찾습니다.
        private static RogueDungeonGenerator FindGenerator(string name)
        {
            RogueDungeonGenerator[] generators =
                UnityEngine.Object.FindObjectsByType<RogueDungeonGenerator>(
                    FindObjectsInactive.Include);
            for (int i = 0; i < generators.Length; i++)
            {
                if (generators[i] != null &&
                    string.Equals(
                        generators[i].gameObject.name,
                        name,
                        StringComparison.Ordinal))
                {
                    return generators[i];
                }
            }
            return null;
        }

        // 재임포트 뒤에도 참조가 기대 project asset 경로를 가리키는지 확인합니다.
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

        // 두 Vector3가 저장·Prefab 직렬화 허용 오차 안에서 같은지 확인합니다.
        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }

        // R6 기준과 R7 전용 입력 자산을 저장 후 다시 읽고 누락을 즉시 차단합니다.
        private static void ReloadRequiredAssets(
            out RogueDungeonSettings settings,
            out DungeonBlueprintAsset blueprint,
            out DungeonBakeMaterialSet materialSet,
            out DungeonStageOverrides stageOverrides,
            out DungeonStageDefinition runtimeStage,
            out DungeonStageDefinition bakedStage)
        {
            settings =
                RequireAsset<RogueDungeonSettings>(R6SettingsPath);
            blueprint =
                RequireAsset<DungeonBlueprintAsset>(R6BlueprintPath);
            materialSet =
                RequireAsset<DungeonBakeMaterialSet>(R6MaterialSetPath);
            stageOverrides =
                RequireAsset<DungeonStageOverrides>(OverridesPath);
            runtimeStage =
                RequireAsset<DungeonStageDefinition>(RuntimeStagePath);
            bakedStage =
                RequireAsset<DungeonStageDefinition>(BakedStagePath);
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

        // 지정 경로의 필수 자산을 읽고 없으면 생성 순서 오류를 보고합니다.
        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException(
                    "필수 자산을 불러올 수 없습니다: " + path);
            return asset;
        }

        // 지정 ScriptableObject를 불러오거나 R7 전용 경로에 새 자산으로 생성합니다.
        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // 새 장면 GameObject를 만들고 현재 Undo 그룹에 생성 작업을 등록합니다.
        private static GameObject CreateSceneObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                "R7 수동 검증 오브젝트 생성");
            return gameObject;
        }

        // R7 전용 자산과 장면 폴더를 상위부터 반복 실행에 안전하게 생성합니다.
        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(OverridesFolder);
            EnsureFolder(StagesFolder);
            EnsureFolder(ScenesFolder);
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
