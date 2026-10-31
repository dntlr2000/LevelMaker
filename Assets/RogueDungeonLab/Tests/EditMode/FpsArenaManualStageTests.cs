using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaManualStageTests
    {
        private string folder;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        [SetUp] public void SetUp() { folder = "Assets/__ManualStageTest_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7)); }
        [TearDown] public void TearDown()
        {
            Undo.ClearAll();
            foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            owned.Clear(); AssetDatabase.DeleteAsset(folder);
        }
        private T Own<T>(T obj) where T : UnityEngine.Object { owned.Add(obj); return obj; }
        private FpsArenaStageAsset Stage(int floors = 1)
        { return FpsArenaStageAuthoring.CreateAsset(folder + "/Stage.asset", FpsArenaStageData.Create(16, 16, floors)); }
        private FpsArenaGenerator Generator() { return Own(new GameObject("Manual generator test")).AddComponent<FpsArenaGenerator>(); }

        [Test]
        public void BlankStage_IsIndependentOfRandomAndSeed_AndHashesCanonicalOrder()
        {
            var asset = Stage(); var generator = Generator(); generator.manualStage = asset;
            string before = JsonUtility.ToJson(UnityEngine.Random.state), hash = generator.Plan().Hash;
            generator.seed = -123; generator.recipe.floors = 4;
            Assert.AreEqual(hash, generator.Plan().Hash); Assert.AreEqual(before, JsonUtility.ToJson(UnityEngine.Random.state));
            asset.data.floorCells.Reverse(); Assert.AreEqual(hash, generator.Plan().Hash);
            var layout = generator.Plan(); Assert.AreEqual(hash, layout.ComputeHash()); Assert.AreEqual(256, layout.ReachableCount(layout.BlockedCells()));
        }

        [Test]
        public void GeneratedLayouts_CanBeCapturedWithoutMutatingSource()
        {
            foreach (bool rooms in new[] { false, true }) foreach (int seed in new[] { 0, 71, 73125 })
            {
                var r = FpsArenaRecipe.CreateFlexible(); r.partitionRooms = rooms; r.floors = 3; r.width = r.depth = 32;
                var original = FpsArenaPlanner.Generate(r, seed); string hash = original.Hash;
                var data = FpsArenaStageData.Capture(original); var copy = data.ToLayout();
                Assert.AreEqual(original.FloorCount(), copy.FloorCount()); Assert.AreEqual(original.Content.Count, copy.Content.Count); Assert.AreEqual(original.Stairs.Count, copy.Stairs.Count);
                Assert.AreEqual(copy.Hash, copy.ComputeHash());
                for (int i = 0; i < original.Content.Count; i++) Assert.AreEqual(original.ContentPosition(original.Content[i]), copy.ContentPosition(copy.Content[i]));
                data.content.Clear(); Assert.AreEqual(hash, original.ComputeHash());
            }
        }

        [Test]
        public void LegacyCapture_RetainsObstacleCollisionAndSize()
        {
            var r = new FpsArenaRecipe { floors = 1, coverPerFloor = 1, obstaclesPerFloor = 1, itemsPerFloor = 1 };
            var l = FpsArenaStageData.Capture(FpsArenaPlanner.Generate(r, 7)).ToLayout();
            var root = Own(FpsArenaSceneBuilder.Build(l, null));
            foreach (var c in l.Content) if (c.kind == FpsArenaContentKind.Obstacle) { var box = root.transform.Find(c.id).GetComponentInChildren<BoxCollider>(); Assert.IsFalse(box.isTrigger); Assert.AreEqual(r.obstacleHeight, c.size.y); }
        }

        [Test]
        // 완전히 막힌 구획도 허용하되 문을 열면 연결성 경고가 사라지는지 확인한다.
        public void ClosedPartition_WarnsWithoutBlocking_AndDoorClearsWarning()
        {
            var d = FpsArenaStageData.Create();
            for (int z = 0; z < d.depth; z++) FpsArenaStageAuthoring.SetWall(d, new Vector3Int(7, 0, z), false, true);
            var disconnected = d.ToLayout();
            Assert.DoesNotThrow(disconnected.Validate);
            StringAssert.Contains("128셀", FpsArenaManualValidation.GetConnectivityWarning(disconnected));
            FpsArenaStageAuthoring.SetWall(d, new Vector3Int(7, 0, 5), false, false);
            var l = d.ToLayout(); Assert.AreEqual(l.FloorCount(), l.ReachableCount(l.BlockedCells()));
            Assert.IsEmpty(FpsArenaManualValidation.GetConnectivityWarning(l));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        // 연결되지 않은 층은 경고만 남기고 유효한 계단 배치 후에는 경고를 해소한다.
        public void Stairs_ConnectFloorsInEveryDirection_AndDeletionRestoresOpening(int direction)
        {
            var d = FpsArenaStageData.Create(16, 16, 2); int count = d.floorCells.Count;
            StringAssert.Contains("256셀", FpsArenaManualValidation.GetConnectivityWarning(d.ToLayout()));
            FpsArenaStageAuthoring.AddStair(d, new Vector3Int(5, 0, 5), (FpsArenaStairDirection)direction);
            var l = d.ToLayout(); Assert.AreEqual(count - 16, l.FloorCount()); Assert.AreEqual(l.FloorCount(), l.ReachableCount(l.BlockedCells()));
            Assert.IsEmpty(FpsArenaManualValidation.GetConnectivityWarning(l));
            var root = Own(FpsArenaSceneBuilder.Build(l, null)); Assert.Greater(root.GetComponentsInChildren<BoxCollider>(true).Length, 10);
            FpsArenaStageAuthoring.RemoveStair(d, 0); Assert.AreEqual(count, d.floorCells.Count);
            StringAssert.Contains("256셀", FpsArenaManualValidation.GetConnectivityWarning(d.ToLayout()));
        }

        [Test]
        // 격리 구역을 포함한 수동 자산의 반복 적용과 저장된 적용 결과 복원을 검증한다.
        public void DisconnectedStage_AppliesRepeatedly_AndRestoresBuiltSnapshot()
        {
            var stage = Stage(2); var g = Generator();
            for (int z = 0; z < stage.data.depth; z++) FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(7, 0, z), false, true);
            FpsArenaStageAuthoring.Apply(stage, g); FpsArenaStageAuthoring.Apply(stage, g);
            Assert.AreEqual(1, g.transform.childCount); Assert.IsTrue(g.CurrentLayout.IsAuthored);
            StringAssert.Contains("384셀", FpsArenaManualValidation.GetConnectivityWarning(g.CurrentLayout));
            string hash = g.CurrentLayout.Hash;
            var path = folder + "/Disconnected.prefab"; PrefabUtility.SaveAsPrefabAsset(g.gameObject, path);
            var instance = Own((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            var restored = instance.GetComponent<FpsArenaGenerator>();
            Assert.AreEqual(hash, restored.CurrentLayout.Hash); Assert.AreEqual(1, restored.transform.childCount);
            StringAssert.Contains("384셀", FpsArenaManualValidation.GetConnectivityWarning(restored.CurrentLayout));
        }

        [Test]
        // 편집기 재검사에서 연결성 경고와 필수 오류가 혼동되거나 이전 경고가 남지 않는지 확인한다.
        public void ManualWindow_SeparatesConnectivityWarningFromBlockingErrors()
        {
            var stage = Stage(2); var window = Own(ScriptableObject.CreateInstance<FpsArenaStageWindow>());
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(FpsArenaStageWindow);
            type.GetField("stage", flags).SetValue(window, stage);
            var check = type.GetMethod("Check", flags); check.Invoke(window, null);
            Assert.IsEmpty((string)type.GetField("validation", flags).GetValue(window));
            StringAssert.Contains("256셀", (string)type.GetField("connectivityWarning", flags).GetValue(window));
            stage.data.spawns[0] = new Vector3Int(-1, 0, 0);
            type.GetField("needsValidation", flags).SetValue(window, true); check.Invoke(window, null);
            StringAssert.Contains("스폰", (string)type.GetField("validation", flags).GetValue(window));
            Assert.IsEmpty((string)type.GetField("connectivityWarning", flags).GetValue(window));
        }

        [Test]
        // 수동 경로의 완화가 절차 생성 레이아웃의 연결성 필수 검사를 바꾸지 않았는지 확인한다.
        public void ProceduralLayout_StillRejectsDisconnectedFloors()
        {
            var recipe = FpsArenaRecipe.CreateFlexible(); recipe.floors = 2;
            recipe.coverDensity = recipe.enemyDensity = recipe.gimmickDensity = recipe.itemDensity = 0;
            var layout = FpsArenaPlanner.Generate(recipe, 73125);
            Assert.IsFalse(layout.IsAuthored); Assert.Greater(layout.Stairs.Count, 0);
            layout.Stairs.Clear();
            StringAssert.Contains("unreachable floor", Assert.Throws<InvalidOperationException>(layout.Validate).Message);
        }

        [Test]
        public void ContentOverlapAndRotatedFootprint_AreRejectedWithLocation()
        {
            var d = FpsArenaStageData.Create();
            d.content.Add(FpsArenaStageAuthoring.MakeContent(d, FpsArenaContentKind.Enemy, d.spawns[0]));
            StringAssert.Contains("스폰", Assert.Throws<InvalidOperationException>(() => d.ToLayout()).Message);
            var p = d.content[0]; p.cell = new Vector3Int(5, 0, 5); p.yaw = 45; p.size = new Vector3(3, 2, 3);
            StringAssert.Contains("회전", Assert.Throws<InvalidOperationException>(() => d.ToLayout()).Message);
            p.size = new Vector3(1, 2, 1); Assert.DoesNotThrow(() => d.ToLayout());
        }

        [Test]
        public void EditorEdits_UndoRedoAndDiskReload_PreserveStableIdsAndHash()
        {
            var stage = Stage(); var p = FpsArenaStageAuthoring.MakeContent(stage.data, FpsArenaContentKind.Gimmick, new Vector3Int(5, 0, 5));
            FpsArenaStageAuthoring.Edit(stage, "배치", d => d.content.Add(p));
            string placed = stage.data.ComputeHash(); Undo.IncrementCurrentGroup();
            FpsArenaStageAuthoring.Edit(stage, "이동", d => d.content[0].cell += Vector3Int.right);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(placed, stage.data.ComputeHash());
            Undo.PerformRedo(); Assert.AreEqual(p.id, stage.data.content[0].id); Assert.AreEqual(6, stage.data.content[0].cell.x);
            string moved = stage.data.ComputeHash(); AssetDatabase.SaveAssetIfDirty(stage);
            AssetDatabase.ImportAsset(folder + "/Stage.asset", ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath<FpsArenaStageAsset>(folder + "/Stage.asset");
            Assert.AreEqual(moved, reopened.CreateLayout().Hash); Assert.AreEqual(p.id, reopened.data.content[0].id);
        }

        [Test]
        public void InvalidApply_PreservesExistingRoot_AndRepeatedApplyDoesNotDuplicate()
        {
            var stage = Stage(); var g = Generator(); FpsArenaStageAuthoring.Apply(stage, g); FpsArenaStageAuthoring.Apply(stage, g);
            Assert.AreEqual(1, g.transform.childCount); var root = g.GeneratedRoot; string hash = g.CurrentLayout.Hash;
            FpsArenaStageAuthoring.Edit(stage, "잘못된 스폰", d => d.spawns[0] = new Vector3Int(-1, 0, 0));
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.Apply(stage, g));
            Assert.AreEqual(root, g.GeneratedRoot); Assert.AreEqual(hash, g.CurrentLayout.Hash); Assert.AreEqual(1, g.transform.childCount);
        }

        [Test]
        public void SerializedBuiltSnapshot_RestoresWithoutRegenerationOrCurrentAssetChanges()
        {
            var stage = Stage(); var g = Generator(); FpsArenaStageAuthoring.Apply(stage, g); string hash = g.CurrentLayout.Hash;
            stage.data.floorCells.Remove(new Vector3Int(15, 0, 15));
            var path = folder + "/Stage.prefab"; PrefabUtility.SaveAsPrefabAsset(g.gameObject, path);
            var instance = Own((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            var restored = instance.GetComponent<FpsArenaGenerator>();
            Assert.AreEqual(hash, restored.CurrentLayout.Hash); Assert.AreEqual(256, restored.CurrentLayout.FloorCount()); Assert.AreEqual(1, restored.transform.childCount);
        }

        [Test]
        public void HostCatalogOverride_ReplacesAllCategoriesWithoutChangingLayout()
        {
            var stage = Stage(); var a = Own(ScriptableObject.CreateInstance<FpsArenaContentCatalog>()); var b = Own(ScriptableObject.CreateInstance<FpsArenaContentCatalog>());
            int x = 3;
            foreach (var kind in new[] { FpsArenaContentKind.Cover, FpsArenaContentKind.Enemy, FpsArenaContentKind.Gimmick })
            {
                string key = "host." + kind; var p = FpsArenaStageAuthoring.MakeContent(stage.data, kind, new Vector3Int(x, 0, 5)); p.contentKey = key; stage.data.content.Add(p); x += 3;
                foreach (var pair in new[] { Tuple.Create(a, "A"), Tuple.Create(b, "B") })
                {
                    var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = pair.Item2 + kind;
                    var prefab = PrefabUtility.SaveAsPrefabAsset(obj, folder + "/" + obj.name + ".prefab"); UnityEngine.Object.DestroyImmediate(obj);
                    FpsArenaStageAuthoring.RegisterPrefab(pair.Item1, key, kind, prefab);
                }
            }
            stage.contentCatalog = a; var g = Generator(); g.manualStage = stage; g.Generate(); string hash = g.CurrentLayout.Hash;
            g.manualCatalogOverride = b; g.Generate(); Assert.AreEqual(hash, g.CurrentLayout.Hash);
            foreach (var identity in g.GeneratedRoot.GetComponentsInChildren<FpsArenaContentIdentity>()) Assert.IsTrue(identity.transform.GetChild(0).name.StartsWith("B", StringComparison.Ordinal));
            Assert.AreEqual(3, g.GeneratedRoot.GetComponentsInChildren<FpsArenaContentIdentity>().Length);
            b.entries.Clear(); var root = g.GeneratedRoot; Assert.Throws<InvalidOperationException>(() => g.Generate()); Assert.AreEqual(root, g.GeneratedRoot);
        }

        [Test]
        public void FailedEdit_DoesNotMutateAsset()
        {
            var stage = Stage(); string before = stage.data.ComputeHash();
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.Edit(stage, "실패", d => { d.floorCells.Clear(); throw new InvalidOperationException("test"); }));
            Assert.AreEqual(before, stage.data.ComputeHash());
        }
    }
}
