using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonStageBakerTests
    {
        private const string TempFolder =
            "Assets/RogueDungeonLab/Tests/TempR6StageBaker";

        // 각 테스트가 비어 있는 Scene과 독립된 임시 asset 폴더에서 시작하도록 준비합니다.
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempFolder);
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/RogueDungeonLab/Tests",
                    "TempR6StageBaker");
            }
        }

        // 생성 Scene과 이번 테스트의 project asset만 제거해 다음 테스트와 격리합니다.
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
            Undo.ClearAll();
        }

        // 정상 Bake가 영속 Mesh·재질·metadata·정확한 소유 목록과 Runtime load 가능한 참조를 만드는지 검증합니다.
        [Test]
        public void Bake_CreatesPersistentLoadablePrefabAndExactOwnedArtifacts()
        {
            BakeFixture fixture = CreateBuiltInFixture("Normal");
            fixture.Definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
            EditorUtility.SetDirty(fixture.Definition);
            AssetDatabase.SaveAssets();

            DungeonStageBakeResult result = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            Assert.That(result.Definition, Is.SameAs(fixture.Definition));
            Assert.That(
                fixture.Definition.buildMode,
                Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
            Assert.That(result.Manifest, Is.SameAs(fixture.Definition.bakeManifest));
            Assert.That(result.BakedPrefab, Is.SameAs(fixture.Definition.bakedPrefab));
            Assert.That(result.ValidationReport.IsValid, Is.True, FormatIssues(result.ValidationReport));
            Assert.That(AssetDatabase.IsValidFolder(result.OutputFolder), Is.True);
            Assert.That(result.Manifest.ownedArtifacts, Has.Count.EqualTo(3));

            Transform floor = result.BakedPrefab.transform.Find("Geometry/Floor");
            Transform walls = result.BakedPrefab.transform.Find("Geometry/Walls");
            Assert.That(floor, Is.Not.Null);
            Assert.That(walls, Is.Not.Null);
            Assert.That(
                AssetDatabase.Contains(floor.GetComponent<MeshFilter>().sharedMesh),
                Is.True);
            Assert.That(
                AssetDatabase.Contains(walls.GetComponent<MeshFilter>().sharedMesh),
                Is.True);
            AssertSameAsset(
                fixture.MaterialSet.floor,
                floor.GetComponent<MeshRenderer>().sharedMaterial);
            AssertSameAsset(
                fixture.MaterialSet.wall,
                walls.GetComponent<MeshRenderer>().sharedMaterial);
            Assert.That(
                result.BakedPrefab.GetComponentInChildren<DungeonGeneratedMeshOwner>(true),
                Is.Null);
            Assert.That(
                result.BakedPrefab.GetComponent<DungeonBakedStageMetadata>(),
                Is.Not.Null);

            GameObject parent = new GameObject("R6 Baker Load Parent");
            try
            {
                DungeonStageInstance instance = DungeonStageLoader.Load(
                    new DungeonLoadContext(
                        fixture.Definition,
                        parent.transform));
                Assert.That(
                    instance.BuildMode,
                    Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
                Assert.That(
                    instance.Blueprint.blueprintHash,
                    Is.EqualTo(fixture.Blueprint.blueprint.blueprintHash));
                Assert.That(instance.Root, Is.Not.Null);
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
            }
        }

        // 재Bake가 새 version으로 원자 교체되고 이전 manifest 소유 자산만 삭제하는지 검증합니다.
        [Test]
        public void Bake_RebakeReplacesDerivedAssetsAndPreservesSharedInputs()
        {
            BakeFixture fixture = CreateBuiltInFixture("Rebake");
            DungeonStageBakeResult first = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            DungeonBakeManifest firstManifest = first.Manifest;
            GameObject firstPrefab = first.BakedPrefab;
            string firstManifestPath = AssetDatabase.GetAssetPath(firstManifest);
            string firstPrefabPath = AssetDatabase.GetAssetPath(firstPrefab);
            string blueprintPath = AssetDatabase.GetAssetPath(fixture.Blueprint);
            string settingsPath = AssetDatabase.GetAssetPath(fixture.Settings);
            string materialSetPath = AssetDatabase.GetAssetPath(fixture.MaterialSet);

            DungeonStageBakeResult second = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            Assert.That(second.Manifest, Is.Not.SameAs(firstManifest));
            Assert.That(second.BakedPrefab, Is.Not.SameAs(firstPrefab));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(firstManifestPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(firstPrefabPath), Is.Null);
            Assert.That(
                AssetDatabase.IsValidFolder(first.OutputFolder),
                Is.False,
                "An empty previous Bake version folder must not accumulate.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<DungeonBlueprintAsset>(blueprintPath),
                Is.SameAs(fixture.Blueprint));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(settingsPath),
                Is.SameAs(fixture.Settings));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<DungeonBakeMaterialSet>(materialSetPath),
                Is.SameAs(fixture.MaterialSet));
            Assert.That(
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition).IsValid,
                Is.True);
        }

        // commit 직전 주입 실패가 기존 refs와 이전 정상 Bake 자산을 그대로 보존하는지 검증합니다.
        [Test]
        public void Bake_InjectedBeforeCommitFailurePreservesPreviousBake()
        {
            BakeFixture fixture = CreateBuiltInFixture("Failure");
            DungeonStageBakeResult first = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            DungeonBakeManifest oldManifest = first.Manifest;
            GameObject oldPrefab = first.BakedPrefab;
            string oldManifestPath = AssetDatabase.GetAssetPath(oldManifest);
            string oldPrefabPath = AssetDatabase.GetAssetPath(oldPrefab);

            DungeonStageBakeException exception =
                Assert.Throws<DungeonStageBakeException>(delegate
                {
                    DungeonStageBaker.Bake(
                        fixture.Definition,
                        fixture.MaterialSet,
                        fixture.Settings,
                        new DungeonStageBakeOptions
                        {
                            SimulateFailureBeforeCommit = true
                        });
                });

            Assert.That(exception, Is.Not.Null);
            Assert.That(fixture.Definition.bakeManifest, Is.SameAs(oldManifest));
            Assert.That(fixture.Definition.bakedPrefab, Is.SameAs(oldPrefab));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(oldManifestPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(oldPrefabPath), Is.Not.Null);
            Assert.That(
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition).IsValid,
                Is.True);
        }

        // 변조된 이전 manifest가 stage 폴더 안의 비소유 사용자 자산을 삭제하지 못하는지 검증합니다.
        [Test]
        public void Bake_RebakeIgnoresTamperedOwnedArtifactGuid()
        {
            BakeFixture fixture = CreateBuiltInFixture("TamperedCleanup");
            DungeonStageBakeResult first = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            string sharedPath = first.OutputFolder + "/UserSharedDropTable.asset";
            WeightedDropTable sharedTable =
                ScriptableObject.CreateInstance<WeightedDropTable>();
            sharedTable.entries.Add(new DropEntry
            {
                itemId = "Shared",
                weight = 1f,
                minQuantity = 1,
                maxQuantity = 1
            });
            AssetDatabase.CreateAsset(sharedTable, sharedPath);
            AssetDatabase.SaveAssets();
            first.Manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
            {
                role = "floor-mesh",
                assetGuid = AssetDatabase.AssetPathToGUID(sharedPath),
                dependencyHash =
                    AssetDatabase.GetAssetDependencyHash(sharedPath).ToString()
            });
            EditorUtility.SetDirty(first.Manifest);
            AssetDatabase.SaveAssets();

            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            Assert.That(
                AssetDatabase.LoadAssetAtPath<WeightedDropTable>(sharedPath),
                Is.Not.Null,
                "A tampered manifest must not expand Baker cleanup ownership.");
        }

        // 저장 Blueprint와 Material dependency 변경이 현재 Bake의 stale 상태로 보고되는지 검증합니다.
        [Test]
        public void ValidateCurrentBake_DetectsBlueprintAndMaterialStaleness()
        {
            BakeFixture fixture = CreateBuiltInFixture("Stale");
            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            fixture.MaterialSet.floor.SetColor(
                "_Color",
                new Color(0.73f, 0.14f, 0.42f));
            EditorUtility.SetDirty(fixture.MaterialSet.floor);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                AssetDatabase.GetAssetPath(fixture.MaterialSet),
                ImportAssetOptions.ForceUpdate);

            DungeonValidationReport staleMaterial =
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition);
            Assert.That(
                staleMaterial.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleMaterialDependency),
                Is.True,
                FormatIssues(staleMaterial));

            fixture.Blueprint.blueprint.seed++;
            fixture.Blueprint.blueprint.RefreshHash();
            EditorUtility.SetDirty(fixture.Blueprint);
            AssetDatabase.SaveAssets();
            DungeonValidationReport staleBlueprint =
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition);
            Assert.That(
                staleBlueprint.ContainsCode(
                    DungeonBakeManifestValidationCodes.SourceBlueprintHashMismatch),
                Is.True,
                FormatIssues(staleBlueprint));
        }

        // custom Catalog Prefab dependency 변경이 planning과 독립적인 realization stale로 감지되는지 검증합니다.
        [Test]
        public void ValidateCurrentBake_DetectsCatalogPrefabDependencyStaleness()
        {
            BakeFixture fixture = CreateCatalogFixture("CatalogStale");
            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            string originalFingerprint =
                fixture.Definition.bakeManifest.contentRealizationHash;

            GameObject prefabContents =
                PrefabUtility.LoadPrefabContents(
                    AssetDatabase.GetAssetPath(fixture.CatalogPrefab));
            try
            {
                prefabContents.AddComponent<BoxCollider>();
                PrefabUtility.SaveAsPrefabAsset(
                    prefabContents,
                    AssetDatabase.GetAssetPath(fixture.CatalogPrefab));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DungeonValidationReport stale =
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition);
            Assert.That(
                stale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleContentRealization),
                Is.True,
                FormatIssues(stale));
            Assert.That(
                fixture.Definition.bakeManifest.contentRealizationHash,
                Is.EqualTo(originalFingerprint));
        }

        // asmdef가 Editor 전용인 컴포넌트 assembly를 Player 안전성 분류기가 감지하는지 검증합니다.
        [Test]
        public void PlayerSafetyClassifier_RecognizesEditorOnlyAssembly()
        {
            MethodInfo classifier = typeof(DungeonStageBaker).GetMethod(
                "IsEditorOnlyAssembly",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(classifier, Is.Not.Null);
            bool isEditorOnly = (bool)classifier.Invoke(
                null,
                new object[]
                {
                    typeof(R6EditorOnlyBakeTestComponent)
                        .Assembly.GetName().Name,
                    new HashSet<string>()
                });
            Assert.That(
                isEditorOnly,
                Is.True,
                "Editor-only asmdef must not be considered Player-safe.");
        }

        // 직접 Prefab에 작성된 클릭 대상 설정 변경이 gameplay dependency stale로 보고되는지 검증합니다.
        [Test]
        public void ValidateCurrentBake_DetectsCatalogPrefabGameplayStaleness()
        {
            BakeFixture fixture = CreateCatalogFixture("PrefabGameplayStale");
            string prefabPath =
                AssetDatabase.GetAssetPath(fixture.CatalogPrefab);
            GameObject prefabContents =
                PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                DestructibleDropTarget target =
                    prefabContents.AddComponent<DestructibleDropTarget>();
                target.Configure(
                    "OriginalPrefabTarget",
                    DropSourceKind.Enemy,
                    fixture.Settings.enemyDropTable,
                    true);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            AssetDatabase.SaveAssets();

            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                DestructibleDropTarget target =
                    prefabContents.GetComponent<DestructibleDropTarget>();
                target.Configure(
                    "ChangedPrefabTarget",
                    DropSourceKind.Enemy,
                    fixture.Settings.enemyDropTable,
                    false);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DungeonValidationReport stale =
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition);
            Assert.That(
                stale.ContainsCode(
                    DungeonStageBakeValidationCodes.StaleGameplayBuildConfig),
                Is.True,
                FormatIssues(stale));
        }

        // 현재 Bake 검증이 같은 manifest 오류를 중복 issue로 합치지 않는지 확인합니다.
        [Test]
        public void ValidateCurrentBake_DoesNotDuplicateRuntimeContractIssues()
        {
            BakeFixture fixture = CreateBuiltInFixture("NoDuplicateIssues");
            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            fixture.Definition.bakeManifest.sourceBlueprintHash =
                "tampered-source-hash";
            EditorUtility.SetDirty(fixture.Definition.bakeManifest);
            AssetDatabase.SaveAssets();

            DungeonValidationReport report =
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition);

            Assert.That(
                CountIssues(
                    report,
                    DungeonBakeManifestValidationCodes.SourceBlueprintHashMismatch),
                Is.EqualTo(1),
                FormatIssues(report));
        }

        // 비가역적인 이전 파생 자산 정리 뒤 Undo가 삭제된 Prefab·manifest 참조를 되살리지 않는지 검증합니다.
        [Test]
        public void Bake_RebakeClearsUnsafeDefinitionUndoRecord()
        {
            BakeFixture fixture = CreateBuiltInFixture("UndoSafety");
            DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);
            DungeonStageBakeResult current = DungeonStageBaker.Bake(
                fixture.Definition,
                fixture.MaterialSet,
                fixture.Settings);

            Undo.PerformUndo();

            Assert.That(fixture.Definition.bakedPrefab, Is.SameAs(current.BakedPrefab));
            Assert.That(fixture.Definition.bakeManifest, Is.SameAs(current.Manifest));
            Assert.That(
                fixture.Definition.buildMode,
                Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
            Assert.That(
                DungeonStageBaker.ValidateCurrentBake(fixture.Definition).IsValid,
                Is.True);
        }

        // 같은 저장 Blueprint의 RuntimeBuild와 BakedPrefab이 구조·stable identity·클릭 대상·report 계약을 보존하는지 검증합니다.
        [Test]
        public void Bake_RuntimeBuildAndBakedPrefabPreserveLogicalGameplayParity()
        {
            BakeFixture fixture = CreateBuiltInFixture("Parity");
            GameObject runtimeParent = new GameObject("R6 Runtime Parity Parent");
            GameObject bakedParent = new GameObject("R6 Baked Parity Parent");
            try
            {
                fixture.Definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                EditorUtility.SetDirty(fixture.Definition);
                AssetDatabase.SaveAssets();
                DungeonStageInstance runtimeInstance = DungeonStageLoader.Load(
                    new DungeonLoadContext(
                        fixture.Definition,
                        runtimeParent.transform,
                        fixture.Settings));

                DungeonStageBaker.Bake(
                    fixture.Definition,
                    fixture.MaterialSet,
                    fixture.Settings);
                DungeonStageInstance bakedInstance = DungeonStageLoader.Load(
                    new DungeonLoadContext(
                        fixture.Definition,
                        bakedParent.transform));

                Assert.That(
                    bakedInstance.Blueprint.blueprintHash,
                    Is.EqualTo(runtimeInstance.Blueprint.blueprintHash));
                Assert.That(
                    bakedInstance.Layout.Entrance,
                    Is.EqualTo(runtimeInstance.Layout.Entrance));
                Assert.That(
                    bakedInstance.Layout.Exit,
                    Is.EqualTo(runtimeInstance.Layout.Exit));
                Assert.That(
                    bakedInstance.Layout.WalkableCellCount,
                    Is.EqualTo(runtimeInstance.Layout.WalkableCellCount));
                Assert.That(
                    runtimeInstance.Root.GetComponentInChildren<DungeonGeneratedMeshOwner>(true),
                    Is.Not.Null);
                Assert.That(
                    bakedInstance.Root.GetComponentInChildren<DungeonGeneratedMeshOwner>(true),
                    Is.Null);

                AssertGeometryOwnership(runtimeInstance.Root, false);
                AssertGeometryOwnership(bakedInstance.Root, true);
                AssertSpawnAndGameplayParity(runtimeInstance.Root, bakedInstance.Root);
                AssertBuildResultParity(
                    runtimeInstance.BuildResult,
                    bakedInstance.BuildResult);
                AssertReportParity(runtimeInstance.Report, bakedInstance.Report);
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(bakedParent.transform);
                DungeonStageLoader.ClearGenerated(runtimeParent.transform);
                Object.DestroyImmediate(bakedParent);
                Object.DestroyImmediate(runtimeParent);
            }
        }

        // 기본 Material Set 생성 API가 하나의 영속 asset 안에 8개 완성 재질을 만드는지 검증합니다.
        [Test]
        public void CreateDefaultMaterialSetAsset_CreatesPersistentCompleteSet()
        {
            string path = TempFolder + "/DefaultBakeMaterials.asset";

            DungeonBakeMaterialSet materialSet =
                DungeonStageBaker.CreateDefaultMaterialSetAsset(path);

            Assert.That(materialSet, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(materialSet), Is.True);
            Assert.That(materialSet.floor, Is.Not.Null);
            Assert.That(materialSet.wall, Is.Not.Null);
            Assert.That(materialSet.enemy, Is.Not.Null);
            Assert.That(materialSet.destructible, Is.Not.Null);
            Assert.That(materialSet.prop, Is.Not.Null);
            Assert.That(materialSet.gimmick, Is.Not.Null);
            Assert.That(materialSet.entrance, Is.Not.Null);
            Assert.That(materialSet.exit, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(materialSet.floor), Is.True);
        }

        // floor와 wall의 filter·collider가 같은 Mesh를 사용하며 Bake 여부에 맞는 자산 소유권인지 검사합니다.
        private static void AssertGeometryOwnership(
            GameObject root,
            bool expectedPersistent)
        {
            Transform floor = root.transform.Find("Geometry/Floor");
            Transform walls = root.transform.Find("Geometry/Walls");
            Assert.That(floor, Is.Not.Null);
            Assert.That(walls, Is.Not.Null);
            AssertGeometryObject(floor, expectedPersistent);
            AssertGeometryObject(walls, expectedPersistent);
        }

        // 하나의 geometry object가 filter와 collider에서 동일한 유효 Mesh를 참조하는지 검사합니다.
        private static void AssertGeometryObject(
            Transform geometry,
            bool expectedPersistent)
        {
            MeshFilter filter = geometry.GetComponent<MeshFilter>();
            MeshCollider collider = geometry.GetComponent<MeshCollider>();
            Assert.That(filter, Is.Not.Null);
            Assert.That(collider, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));
            Assert.That(
                AssetDatabase.Contains(filter.sharedMesh),
                Is.EqualTo(expectedPersistent));
        }

        // stable spawn ID별 category·key·cell·transform과 파괴 가능한 gameplay 설정을 두 계층에서 비교합니다.
        private static void AssertSpawnAndGameplayParity(
            GameObject runtimeRoot,
            GameObject bakedRoot)
        {
            Dictionary<string, DungeonSpawnIdentity> runtimeSpawns =
                BuildIdentityLookup(runtimeRoot);
            Dictionary<string, DungeonSpawnIdentity> bakedSpawns =
                BuildIdentityLookup(bakedRoot);
            Assert.That(bakedSpawns.Count, Is.EqualTo(runtimeSpawns.Count));
            foreach (KeyValuePair<string, DungeonSpawnIdentity> pair in runtimeSpawns)
            {
                DungeonSpawnIdentity bakedIdentity;
                Assert.That(
                    bakedSpawns.TryGetValue(pair.Key, out bakedIdentity),
                    Is.True,
                    "Baked Prefab is missing spawn ID: " + pair.Key);
                DungeonSpawnIdentity runtimeIdentity = pair.Value;
                Assert.That(bakedIdentity.ContentKey, Is.EqualTo(runtimeIdentity.ContentKey));
                Assert.That(bakedIdentity.Category, Is.EqualTo(runtimeIdentity.Category));
                Assert.That(bakedIdentity.Cell, Is.EqualTo(runtimeIdentity.Cell));
                Assert.That(
                    Vector3.Distance(
                        bakedIdentity.transform.localPosition,
                        runtimeIdentity.transform.localPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        bakedIdentity.transform.localRotation,
                        runtimeIdentity.transform.localRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(
                        bakedIdentity.transform.localScale,
                        runtimeIdentity.transform.localScale),
                    Is.LessThan(0.0001f));
                AssertDropTargetParity(runtimeIdentity, bakedIdentity);
            }
        }

        // 한 계층의 DungeonSpawnIdentity를 중복 없는 stable ID lookup으로 변환합니다.
        private static Dictionary<string, DungeonSpawnIdentity> BuildIdentityLookup(
            GameObject root)
        {
            Dictionary<string, DungeonSpawnIdentity> result =
                new Dictionary<string, DungeonSpawnIdentity>(StringComparer.Ordinal);
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity = identities[i];
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.SpawnId, Is.Not.Empty);
                Assert.That(result.ContainsKey(identity.SpawnId), Is.False);
                result.Add(identity.SpawnId, identity);
            }
            return result;
        }

        // Enemy·Destructible의 대상 ID, 종류, Drop Table과 marker 정책이 Prefab 직렬화 뒤에도 같은지 검사합니다.
        private static void AssertDropTargetParity(
            DungeonSpawnIdentity runtimeIdentity,
            DungeonSpawnIdentity bakedIdentity)
        {
            DestructibleDropTarget runtimeTarget =
                runtimeIdentity.GetComponentInChildren<DestructibleDropTarget>(true);
            DestructibleDropTarget bakedTarget =
                bakedIdentity.GetComponentInChildren<DestructibleDropTarget>(true);
            Assert.That(bakedTarget == null, Is.EqualTo(runtimeTarget == null));
            if (runtimeTarget == null) return;

            Assert.That(bakedTarget.TargetId, Is.EqualTo(runtimeTarget.TargetId));
            Assert.That(bakedTarget.SourceKind, Is.EqualTo(runtimeTarget.SourceKind));
            Assert.That(
                bakedTarget.DropTable == null,
                Is.EqualTo(runtimeTarget.DropTable == null));
            if (runtimeTarget.DropTable != null)
                AssertSameAsset(runtimeTarget.DropTable, bakedTarget.DropTable);
            Assert.That(
                ReadSpawnMarker(bakedTarget),
                Is.EqualTo(ReadSpawnMarker(runtimeTarget)));
        }

        // private 직렬화 marker 정책을 SerializedObject로 읽어 gameplay parity를 비교합니다.
        private static bool ReadSpawnMarker(DestructibleDropTarget target)
        {
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty marker = serializedTarget.FindProperty("spawnMarker");
            Assert.That(marker, Is.Not.Null);
            return marker.boolValue;
        }

        // SceneBuilder와 metadata 복원 결과의 Mesh·category·해석 통계를 비교합니다.
        private static void AssertBuildResultParity(
            DungeonSceneBuildResult runtimeResult,
            DungeonSceneBuildResult bakedResult)
        {
            Assert.That(bakedResult.MeshTriangleCount, Is.EqualTo(runtimeResult.MeshTriangleCount));
            Assert.That(
                bakedResult.ContentCounts.EnemyCount,
                Is.EqualTo(runtimeResult.ContentCounts.EnemyCount));
            Assert.That(
                bakedResult.ContentCounts.DestructibleCount,
                Is.EqualTo(runtimeResult.ContentCounts.DestructibleCount));
            Assert.That(
                bakedResult.ContentCounts.PropCount,
                Is.EqualTo(runtimeResult.ContentCounts.PropCount));
            Assert.That(
                bakedResult.ContentCounts.GimmickCount,
                Is.EqualTo(runtimeResult.ContentCounts.GimmickCount));
            Assert.That(
                bakedResult.ResolvedContentCount,
                Is.EqualTo(runtimeResult.ResolvedContentCount));
            Assert.That(
                bakedResult.BuiltInFallbackCount,
                Is.EqualTo(runtimeResult.BuiltInFallbackCount));
            Assert.That(
                bakedResult.SkippedContentCount,
                Is.EqualTo(runtimeResult.SkippedContentCount));
        }

        // 시간만 제외한 기존 GenerationReport 호환 필드와 경고 순서를 비교합니다.
        private static void AssertReportParity(
            GenerationReport runtimeReport,
            GenerationReport bakedReport)
        {
            Assert.That(bakedReport.activeSeed, Is.EqualTo(runtimeReport.activeSeed));
            Assert.That(bakedReport.roomCount, Is.EqualTo(runtimeReport.roomCount));
            Assert.That(bakedReport.floorCellCount, Is.EqualTo(runtimeReport.floorCellCount));
            Assert.That(bakedReport.enemyCount, Is.EqualTo(runtimeReport.enemyCount));
            Assert.That(
                bakedReport.destructibleCount,
                Is.EqualTo(runtimeReport.destructibleCount));
            Assert.That(bakedReport.propCount, Is.EqualTo(runtimeReport.propCount));
            Assert.That(bakedReport.gimmickCount, Is.EqualTo(runtimeReport.gimmickCount));
            Assert.That(
                bakedReport.meshTriangleCount,
                Is.EqualTo(runtimeReport.meshTriangleCount));
            Assert.That(bakedReport.worldBounds.center, Is.EqualTo(runtimeReport.worldBounds.center));
            Assert.That(bakedReport.worldBounds.size, Is.EqualTo(runtimeReport.worldBounds.size));
            Assert.That(bakedReport.warnings, Is.EqualTo(runtimeReport.warnings));
        }

        // built-in Blueprint, settings, Material Set, StageDefinition의 영속 테스트 fixture를 만듭니다.
        private static BakeFixture CreateBuiltInFixture(string suffix)
        {
            RogueDungeonSettings settings =
                CreateSettingsAsset(suffix);
            DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                DungeonGenerationRequest.Create(
                    settings,
                    6200 + suffix.Length,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                    "r6-baker-" + suffix)).Blueprint;
            DungeonBlueprintAsset blueprintAsset =
                CreateBlueprintAsset(blueprint, suffix);
            DungeonBakeMaterialSet materialSet =
                DungeonStageBaker.CreateDefaultMaterialSetAsset(
                    TempFolder + "/" + suffix + "Materials.asset");
            DungeonStageDefinition definition =
                CreateDefinitionAsset(
                    blueprintAsset,
                    null,
                    suffix);
            return new BakeFixture
            {
                Settings = settings,
                Blueprint = blueprintAsset,
                MaterialSet = materialSet,
                Definition = definition
            };
        }

        // 직접 Prefab entry를 실제 spawn에 사용하는 custom Catalog fixture를 만듭니다.
        private static BakeFixture CreateCatalogFixture(string suffix)
        {
            RogueDungeonSettings settings =
                CreateSettingsAsset(suffix);
            settings.enemyProfile.baseDensity = 0.5f;
            settings.enemyProfile.maxCount = 80;
            EditorUtility.SetDirty(settings);

            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            source.name = suffix + " Enemy";
            GameObject catalogPrefab;
            try
            {
                catalogPrefab = PrefabUtility.SaveAsPrefabAsset(
                    source,
                    TempFolder + "/" + suffix + "Enemy.prefab");
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            DungeonContentCatalog catalog =
                ScriptableObject.CreateInstance<DungeonContentCatalog>();
            catalog.entries.Add(new DungeonContentCatalogEntry
            {
                contentKey = "test.enemy." + suffix,
                category = DungeonSpawnCategory.Enemy,
                prefab = catalogPrefab,
                weight = 1f,
                minProgression = 0f,
                maxProgression = 1f,
                placement = DungeonContentPlacement.Any,
                footprintCells = Vector2Int.one,
                uniformScaleRange = Vector2.one,
                gameplayId = "test-gameplay-" + suffix
            });
            AssetDatabase.CreateAsset(
                catalog,
                TempFolder + "/" + suffix + "Catalog.asset");
            AssetDatabase.SaveAssets();

            DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                DungeonGenerationRequest.CreateStableV2(
                    settings,
                    6300 + suffix.Length,
                    catalog,
                    "r6-catalog-baker-" + suffix)).Blueprint;
            DungeonBlueprintAsset blueprintAsset =
                CreateBlueprintAsset(blueprint, suffix);
            DungeonBakeMaterialSet materialSet =
                DungeonStageBaker.CreateDefaultMaterialSetAsset(
                    TempFolder + "/" + suffix + "Materials.asset");
            DungeonStageDefinition definition =
                CreateDefinitionAsset(
                    blueprintAsset,
                    catalog,
                    suffix);
            return new BakeFixture
            {
                Settings = settings,
                Blueprint = blueprintAsset,
                MaterialSet = materialSet,
                Definition = definition,
                Catalog = catalog,
                CatalogPrefab = catalogPrefab
            };
        }

        // Compact 설정을 project asset으로 생성하고 테스트용 drop table을 영속 참조로 연결합니다.
        private static RogueDungeonSettings CreateSettingsAsset(string suffix)
        {
            RogueDungeonSettings settings =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            settings.ApplyPreset(DungeonPreset.Compact);
            WeightedDropTable enemyDrops =
                CreateDropTableAsset(suffix + "EnemyDrops");
            WeightedDropTable destructibleDrops =
                CreateDropTableAsset(suffix + "DestructibleDrops");
            settings.enemyDropTable = enemyDrops;
            settings.destructibleDropTable = destructibleDrops;
            AssetDatabase.CreateAsset(
                settings,
                TempFolder + "/" + suffix + "Settings.asset");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // 하나의 유효 entry를 가진 영속 Drop Table 자산을 만듭니다.
        private static WeightedDropTable CreateDropTableAsset(string name)
        {
            WeightedDropTable table =
                ScriptableObject.CreateInstance<WeightedDropTable>();
            table.entries.Add(new DropEntry
            {
                itemId = "Gold",
                weight = 1f,
                minQuantity = 1,
                maxQuantity = 1,
                markerColor = Color.yellow
            });
            AssetDatabase.CreateAsset(
                table,
                TempFolder + "/" + name + ".asset");
            return table;
        }

        // 검증된 Blueprint를 deep-copy 저장하는 영속 BlueprintAsset을 만듭니다.
        private static DungeonBlueprintAsset CreateBlueprintAsset(
            DungeonBlueprint blueprint,
            string suffix)
        {
            DungeonBlueprintAsset asset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            asset.Store(blueprint);
            AssetDatabase.CreateAsset(
                asset,
                TempFolder + "/" + suffix + "Blueprint.asset");
            AssetDatabase.SaveAssets();
            return asset;
        }

        // SavedBlueprint+BakedPrefab 계약으로 설정된 영속 StageDefinition을 만듭니다.
        private static DungeonStageDefinition CreateDefinitionAsset(
            DungeonBlueprintAsset blueprint,
            DungeonContentCatalog catalog,
            string suffix)
        {
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
            definition.buildMode = DungeonStageBuildMode.BakedPrefab;
            definition.savedBlueprint = blueprint;
            definition.contentCatalog = catalog;
            definition.missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback;
            AssetDatabase.CreateAsset(
                definition,
                TempFolder + "/" + suffix + "Stage.asset");
            AssetDatabase.SaveAssets();
            return definition;
        }

        // validation issue 코드와 메시지를 테스트 실패 문구로 정리합니다.
        private static string FormatIssues(DungeonValidationReport report)
        {
            if (report == null || report.issues == null)
                return "No validation report.";
            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue == null) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(issue.code);
                builder.Append(": ");
                builder.Append(issue.message);
            }
            return builder.ToString();
        }

        // 검증 리포트에서 지정 코드가 등장한 횟수를 셉니다.
        private static int CountIssues(
            DungeonValidationReport report,
            string code)
        {
            int count = 0;
            if (report == null || report.issues == null) return count;
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue != null && issue.code == code) count++;
            }
            return count;
        }

        // Unity reimport 뒤 객체 wrapper가 달라도 GUID와 local file ID가 같은 자산인지 검증합니다.
        private static void AssertSameAsset(Object expected, Object actual)
        {
            string expectedGuid;
            long expectedLocalId;
            string actualGuid;
            long actualLocalId;
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    expected,
                    out expectedGuid,
                    out expectedLocalId),
                Is.True);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    actual,
                    out actualGuid,
                    out actualLocalId),
                Is.True);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid));
            Assert.That(actualLocalId, Is.EqualTo(expectedLocalId));
        }

        private sealed class BakeFixture
        {
            public RogueDungeonSettings Settings;
            public DungeonBlueprintAsset Blueprint;
            public DungeonBakeMaterialSet MaterialSet;
            public DungeonStageDefinition Definition;
            public DungeonContentCatalog Catalog;
            public GameObject CatalogPrefab;
        }
    }
}
