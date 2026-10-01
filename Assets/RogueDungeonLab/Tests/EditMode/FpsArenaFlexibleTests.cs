using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaFlexibleInitializerProbe : MonoBehaviour, IFpsArenaContentInitializer
    {
        public string initializedId;
        public void InitializeArenaContent(FpsArenaSpawnContext context) { initializedId = context.Content.id; }
    }
    public sealed class FpsArenaFlexibleTests
    {
        private static FpsArenaRecipe Empty(int floors = 2)
        { var r = FpsArenaRecipe.CreateFlexible(); r.floors = floors; r.coverDensity = r.enemyDensity = r.gimmickDensity = r.itemDensity = 0; return r; }
        private static string StairKey(FpsArenaLayout l)
        { return string.Join(";", l.Stairs.Select(s => s.lowerFloor + ":" + s.x + ":" + s.z)); }
        [Test]
        public void LegacyTwoArgumentPublicSignatures_RemainAvailableForDelegates()
        {
            Func<FpsArenaRecipe,int,FpsArenaLayout> plan = FpsArenaPlanner.Generate;
            Func<FpsArenaLayout,Transform,GameObject> build = FpsArenaSceneBuilder.Build;
            Assert.IsNotNull(plan); Assert.IsNotNull(build);
            Assert.IsNotNull(typeof(FpsArenaPlanner).GetMethod("Generate",new[] {typeof(FpsArenaRecipe),typeof(int)}));
            Assert.IsNotNull(typeof(FpsArenaSceneBuilder).GetMethod("Build",new[] {typeof(FpsArenaLayout),typeof(Transform)}));
        }
        [Test]
        public void LegacyRecipeAndSavedMissingVersion_RetainValidatedV1Golden()
        {
            const string golden = "61f0193622aa04090432f7480b0376d2bca098c200270bb113540c175ecfe7b0";
            var legacy = new FpsArenaRecipe();
            Assert.AreEqual(1, (int)legacy.generatorVersion);
            Assert.AreEqual(golden, FpsArenaPlanner.Generate(legacy, 73125).Hash);
            string json = "{\"width\":28,\"depth\":28,\"shape\":2,\"floors\":2,\"cellSize\":3,\"floorHeight\":4,\"stairsPerFloor\":2,\"coverPerFloor\":16,\"obstaclesPerFloor\":8,\"itemsPerFloor\":6,\"coverHeight\":1.1,\"obstacleHeight\":2.2,\"spacingCells\":2}";
            var restored = JsonUtility.FromJson<FpsArenaRecipe>(json);
            Assert.AreEqual(golden, FpsArenaPlanner.Generate(restored, 73125).Hash);
            Assert.AreEqual(golden, FpsArenaPlanner.Generate(legacy, 73125).Hash);
            Assert.AreEqual(2, (int)legacy.Upgraded().generatorVersion); Assert.AreEqual(1, (int)legacy.generatorVersion);
            Assert.AreNotEqual(golden, FpsArenaPlanner.Generate(legacy.Upgraded(), 73125).Hash);
        }
        [Test]
        public void FlexibleSameSeed_RepeatsAndManySeeds_MoveStairs()
        {
            var r = Empty(4); var arrangements = new HashSet<string>(); string before = JsonUtility.ToJson(r);
            for (int seed = 0; seed < 24; seed++)
            { var a = FpsArenaPlanner.Generate(r, seed); var b = FpsArenaPlanner.Generate(r, seed); Assert.AreEqual(a.Hash, b.Hash); arrangements.Add(StairKey(a)); a.Validate(); }
            Assert.Greater(arrangements.Count, 12); Assert.AreEqual(before, JsonUtility.ToJson(r));
        }
        [Test]
        public void FlexibleExtremeShapes_HaveClearBothLaneLandingsAndCompleteConnectivity()
        {
            foreach (var shape in (FpsArenaShape[])Enum.GetValues(typeof(FpsArenaShape)))
            foreach (var dimensions in new[] { new Vector2Int(20,20), new Vector2Int(21,64), new Vector2Int(64,20), new Vector2Int(64,64) })
            foreach (int seed in new[] { 0, 1, -173, int.MaxValue })
            {
                var r = Empty(4); r.width = dimensions.x; r.depth = dimensions.y; r.shape = shape; r.cellSize = 2; r.floorHeight = 6;
                var l = FpsArenaPlanner.Generate(r, seed); Assert.AreEqual(6, l.Stairs.Count); l.Validate();
                foreach (var s in l.Stairs) for (int lane = 0; lane < s.width; lane++)
                { Assert.IsTrue(l.HasFloor(s.BottomLane(lane))); Assert.IsTrue(l.HasFloor(s.TopLane(lane))); }
                Assert.AreEqual(l.FloorCount(), l.ReachableCount(new HashSet<Vector3Int>()));
            }
        }
        [Test]
        public void IndependentZeroAndMaxDensities_AffectEachActualCategory()
        {
            var r = Empty(1); r.width = r.depth = 40; r.spacingCells = 1;
            Assert.IsEmpty(FpsArenaPlanner.Generate(r, 11).Content);
            foreach (var kind in new[] { FpsArenaContentKind.Cover, FpsArenaContentKind.Enemy, FpsArenaContentKind.Gimmick, FpsArenaContentKind.Item })
            {
                r.coverDensity = kind == FpsArenaContentKind.Cover ? 1 : 0; r.enemyDensity = kind == FpsArenaContentKind.Enemy ? 1 : 0; r.gimmickDensity = kind == FpsArenaContentKind.Gimmick ? 1 : 0; r.itemDensity = kind == FpsArenaContentKind.Item ? 1 : 0;
                var l = FpsArenaPlanner.Generate(r, 11); Assert.IsNotEmpty(l.Content); Assert.IsTrue(l.Content.All(c => c.kind == kind)); Assert.LessOrEqual(l.Content.Count, r.maxContentPerCategoryPerFloor); l.Validate();
            }
        }
        [Test]
        public void DensityStreams_DoNotMoveStructuralStairsOrEarlierCover()
        {
            var r = Empty(4); r.coverDensity = .08f; var a = FpsArenaPlanner.Generate(r, 83);
            r.enemyDensity = r.gimmickDensity = r.itemDensity = 1; var b = FpsArenaPlanner.Generate(r, 83);
            Assert.AreEqual(StairKey(a), StairKey(b));
            CollectionAssert.AreEqual(a.Content.Where(p => p.kind == FpsArenaContentKind.Cover).Select(p => p.id + ":" + p.size).ToArray(), b.Content.Where(p => p.kind == FpsArenaContentKind.Cover).Select(p => p.id + ":" + p.size).ToArray());
        }
        [Test]
        public void CoverShapesAndWidthDepthHeight_VaryAndFullFootprintIsReservedFromOthers()
        {
            var r = Empty(1); r.width = r.depth = 48; r.coverDensity = .3f; r.coverMinWidthCells = r.coverMinDepthCells = 1; r.coverMaxWidthCells = r.coverMaxDepthCells = 4; r.spacingCells = 1;
            var shapes = new HashSet<FpsArenaCoverShape>(); var sizes = new HashSet<Vector2Int>(); var heights = new HashSet<float>();
            for (int seed = 0; seed < 6; seed++)
            {
                var l = FpsArenaPlanner.Generate(r, seed); l.Validate();
                foreach (var p in l.Content) { shapes.Add(p.coverShape); sizes.Add(new Vector2Int(p.widthCells,p.depthCells)); heights.Add(p.size.y); Assert.That(p.size.y, Is.InRange(r.coverMinHeight,r.coverMaxHeight)); foreach (var cell in p.Footprint()) Assert.IsTrue(l.BlockedCells().Contains(cell)); }
            }
            Assert.AreEqual(3, shapes.Count); Assert.Greater(sizes.Count, 8); Assert.Greater(heights.Count, 8);
        }
        [Test]
        public void MaxAllDensities_LeaveClearConnectedWalkableCells()
        {
            for (int seed = 0; seed < 8; seed++)
            {
                var r = Empty(4); r.width = r.depth = 20; r.spacingCells = 1; r.coverDensity = r.enemyDensity = r.gimmickDensity = r.itemDensity = 1;
                var l = FpsArenaPlanner.Generate(r, seed); l.Validate(); var blocked = l.BlockedCells(); Assert.AreEqual(l.FloorCount() - blocked.Count, l.ReachableCount(blocked));
                foreach (var p in l.Content) foreach (var cell in p.Footprint()) Assert.IsFalse(l.Reserved[cell.x,cell.y,cell.z]);
            }
        }
        [Test]
        public void CatalogSelection_IsStableAcrossEntryOrderAndInstantiatesRealPrefabs()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "Actual Enemy Prefab"; prefab.AddComponent<FpsArenaFlexibleInitializerProbe>();
            var gimmick = GameObject.CreatePrimitive(PrimitiveType.Cube); gimmick.name = "Actual Gimmick Prefab";
            var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); GameObject root = null;
            try
            {
                catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "enemy:test", kind = FpsArenaContentKind.Enemy, prefab = prefab, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "gimmick:test", kind = FpsArenaContentKind.Gimmick, prefab = gimmick, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                var r = Empty(1); r.enemyDensity = r.gimmickDensity = .04f;
                var a = FpsArenaPlanner.Generate(r, 71, catalog); catalog.entries.Reverse(); var b = FpsArenaPlanner.Generate(r, 71, catalog); Assert.AreEqual(a.Hash,b.Hash);
                root = FpsArenaSceneBuilder.Build(a,null,catalog);
                foreach (var identity in root.GetComponentsInChildren<FpsArenaContentIdentity>(true))
                {
                    Assert.IsNotEmpty(identity.ContentKey); Assert.AreEqual(1,identity.transform.childCount);
                    Assert.AreEqual(identity.Kind == FpsArenaContentKind.Enemy ? prefab.name : gimmick.name, identity.transform.GetChild(0).name);
                    if (identity.Kind == FpsArenaContentKind.Enemy) Assert.AreEqual(identity.StableId, identity.GetComponentInChildren<FpsArenaFlexibleInitializerProbe>(true).initializedId);
                }
                Assert.IsNotEmpty(root.GetComponentsInChildren<FpsArenaContentIdentity>(true));
            }
            finally { if (root != null) Object.DestroyImmediate(root); Object.DestroyImmediate(catalog); Object.DestroyImmediate(prefab); Object.DestroyImmediate(gimmick); }
        }
        [Test]
        public void CatalogValidation_RejectsDuplicatesInvalidBoundsAndMissingMappings()
        {
            var prefab = new GameObject("Prefab"); var c = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); GameObject root = null;
            try
            {
                var e = new FpsArenaCatalogEntry { contentKey = "test", prefab = prefab }; c.entries.Add(e); c.entries.Add(e); Assert.Throws<InvalidOperationException>(() => c.Snapshot()); c.entries.RemoveAt(1);
                e.authoredBounds = Vector3.zero; Assert.Throws<InvalidOperationException>(() => c.Snapshot()); e.authoredBounds = Vector3.one;
                var r = Empty(1); r.enemyDensity = .05f; var l = FpsArenaPlanner.Generate(r,10,c); Assert.Throws<InvalidOperationException>(() => root = FpsArenaSceneBuilder.Build(l,null));
                e.prefab = null; Assert.Throws<InvalidOperationException>(() => c.Snapshot());
            }
            finally { if(root!=null) Object.DestroyImmediate(root); Object.DestroyImmediate(c); Object.DestroyImmediate(prefab); }
        }
        [Test]
        public void SerializedRecipeAndCatalogSnapshot_RoundTripWithoutAssetReferences()
        {
            var r = Empty(4); r.coverDensity = .1f; r.enemyDensity = .2f; r.gimmickDensity = .3f;
            var snapshot = new FpsArenaCatalogSnapshot(); snapshot.entries.Add(new FpsArenaCatalogSpec { contentKey = "enemy:test", kind = FpsArenaContentKind.Enemy, weight = 1, widthCells = 1, depthCells = 2, height = 1.8f, minSizeFactor = .7f, maxSizeFactor = 1 });
            var a = FpsArenaPlanner.GenerateSnapshot(r,123,snapshot); var b = FpsArenaPlanner.GenerateSnapshot(JsonUtility.FromJson<FpsArenaRecipe>(JsonUtility.ToJson(r)),123,snapshot.Clone()); Assert.AreEqual(a.Hash,b.Hash);
            snapshot.entries[0].weight = 9; Assert.AreEqual(a.Hash,a.ComputeHash()); Assert.AreNotEqual(a.Hash,FpsArenaPlanner.GenerateSnapshot(r,123,snapshot).Hash);
        }
        [Test]
        public void PrefabMaterialsAndFittedBounds_ArePreservedAfterActivation()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); GameObject root = null;
            Material material = new Material(prefab.GetComponent<Renderer>().sharedMaterial); material.color = Color.magenta; prefab.GetComponent<Renderer>().sharedMaterial = material;
            prefab.AddComponent<FpsArenaContentIdentity>(); // A nested marker must not shadow catalog ownership.
            try
            {
                catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "enemy:material", kind = FpsArenaContentKind.Enemy, prefab = prefab, widthCells = 2, depthCells = 1, height = 1.5f, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                var r = Empty(1); r.enemyDensity = .05f; var l = FpsArenaPlanner.Generate(r,7,catalog); root = FpsArenaSceneBuilder.Build(l,null,catalog); root.SetActive(true);
                foreach(var identity in root.GetComponentsInChildren<FpsArenaContentIdentity>(true))
                {
                    if (string.IsNullOrEmpty(identity.ContentKey)) continue;
                    Assert.AreSame(material, identity.GetComponentInChildren<Renderer>(true).sharedMaterial);
                    var child = identity.transform.GetChild(0); float fit = Mathf.Min(identity.PlannedSize.x,Mathf.Min(identity.PlannedSize.y,identity.PlannedSize.z));
                    Assert.AreEqual(Vector3.one * fit,child.localScale);
                    Assert.AreEqual(fit * .5f,child.localPosition.y,.0001f);
                    Assert.IsFalse(identity.GetComponentInChildren<Collider>(true).isTrigger);
                }
            }
            finally { if(root!=null) Object.DestroyImmediate(root); Object.DestroyImmediate(catalog); Object.DestroyImmediate(prefab); Object.DestroyImmediate(material); }
        }
        [Test]
        public void InvalidCatalogRegeneration_PreservesPreviousRootAndMetadata()
        {
            var host = new GameObject("Generator rollback"); var prefab = new GameObject("Enemy"); var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>();
            try
            {
                var e = new FpsArenaCatalogEntry { contentKey = "enemy:rollback", prefab = prefab }; catalog.entries.Add(e);
                var g = host.AddComponent<FpsArenaGenerator>(); g.recipe = Empty(1); g.recipe.enemyDensity = .03f; g.contentCatalog = catalog; g.Generate();
                var root = g.GeneratedRoot; string hash = g.CurrentLayout.Hash; e.prefab = null;
                Assert.Throws<InvalidOperationException>(()=>g.Generate()); Assert.AreSame(root,g.GeneratedRoot); Assert.AreEqual(hash,g.CurrentLayout.Hash); Assert.AreEqual(1,host.transform.childCount);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(prefab); Object.DestroyImmediate(catalog); }
        }
        [Test]
        public void SavedScene_RestoresBuiltCatalogSnapshotDespiteChangedInputCatalog()
        {
            string folder = "Assets/__ArenaV2Serialization_" + Guid.NewGuid().ToString("N");
            UnityEditor.AssetDatabase.CreateFolder("Assets",folder.Substring("Assets/".Length));
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            GameObject source = null;
            try
            {
                source = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(source,folder+"/Enemy.prefab"); Object.DestroyImmediate(source); source = null;
                var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "enemy:saved", prefab = prefab, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                UnityEditor.AssetDatabase.CreateAsset(catalog,folder+"/Catalog.asset");
                var host = new GameObject("V2 saved arena"); var g = host.AddComponent<FpsArenaGenerator>(); g.generateOnPlay = false; g.recipe = Empty(1); g.recipe.enemyDensity = .04f; g.contentCatalog = catalog; g.Generate(); string builtHash = g.CurrentLayout.Hash;
                Assert.IsTrue(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(host.scene,folder+"/Arena.unity"));
                catalog.entries[0].weight = 19; catalog.entries[0].widthCells = 3; UnityEditor.EditorUtility.SetDirty(catalog); UnityEditor.AssetDatabase.SaveAssets();
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(folder+"/Arena.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
                g = scene.GetRootGameObjects().First(o=>o.GetComponent<FpsArenaGenerator>()!=null).GetComponent<FpsArenaGenerator>();
                Assert.AreEqual(builtHash,g.CurrentLayout.Hash); Assert.AreNotEqual(builtHash,g.Plan().Hash); Assert.AreEqual(1,g.transform.childCount);
                Assert.IsNotEmpty(g.GeneratedRoot.GetComponentsInChildren<FpsArenaContentIdentity>(true));
            }
            finally
            {
                if(source!=null)Object.DestroyImmediate(source);
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single); UnityEditor.AssetDatabase.DeleteAsset(folder);
            }
        }
        [Test]
        public void Normalization_HandlesNonFiniteAndInvertedV2Ranges()
        {
            var r = Empty(); r.coverDensity = float.NaN; r.enemyDensity = float.PositiveInfinity; r.gimmickDensity = -1; r.itemDensity = 2; r.coverMinWidthCells = 4; r.coverMaxWidthCells = 1; r.coverMinHeight = 2; r.coverMaxHeight = .5f; r.coverShapes = 0;
            var n = r.Normalized(); Assert.AreEqual(4,n.coverMaxWidthCells); Assert.AreEqual(2,n.coverMaxHeight); Assert.AreEqual(0,n.gimmickDensity); Assert.AreEqual(1,n.itemDensity); Assert.AreEqual(FpsArenaCoverShapes.Box,n.coverShapes); Assert.DoesNotThrow(() => FpsArenaPlanner.Generate(r,1));
        }
        [Test]
        public void SceneBuilder_CoverShapesHaveSolidCollidersAndMarkersAreTriggers()
        {
            var r = Empty(1); r.width = r.depth = 40; r.coverDensity = .2f; r.enemyDensity = r.gimmickDensity = r.itemDensity = .04f;
            var root = FpsArenaSceneBuilder.Build(FpsArenaPlanner.Generate(r, 1),null);
            try { foreach (var identity in root.GetComponentsInChildren<FpsArenaContentIdentity>(true)) foreach (var collider in identity.GetComponentsInChildren<Collider>(true)) Assert.AreEqual(identity.Kind != FpsArenaContentKind.Cover,collider.isTrigger); }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
