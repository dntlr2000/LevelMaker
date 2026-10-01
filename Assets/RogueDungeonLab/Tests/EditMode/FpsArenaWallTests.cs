using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaWallTests
    {
        [Serializable] private sealed class BaselineCase { public string recipeJson; public int seed; public string hash; }
        [Serializable] private sealed class Baselines { public List<BaselineCase> cases; }
        private static FpsArenaRecipe WallRecipe()
        {
            var r = FpsArenaRecipe.CreateFlexible(); r.width = r.depth = 40; r.floors = 2;
            r.internalWalls = true; r.spacingCells = 1; return r;
        }
        [Test]
        public void WallsOff_ExactlyMatches36PreChangeUnityHashes_IncludingLegacyMissingFields()
        {
            var data = JsonUtility.FromJson<Baselines>(File.ReadAllText(Application.dataPath + "/RogueDungeonLab/Tests/EditMode/FpsArenaWallsOffBaseline.json"));
            Assert.AreEqual(36, data.cases.Count);
            foreach (var c in data.cases)
            {
                var r = JsonUtility.FromJson<FpsArenaRecipe>(c.recipeJson);
                Assert.IsFalse(r.internalWalls); Assert.AreEqual(c.hash, FpsArenaPlanner.Generate(r, c.seed).Hash);
                r.wallDensity = .35f; r.wallMinLengthCells = 16; r.wallMaxLengthCells = 16; r.wallHeight = 6; r.wallThickness = 1;
                if (r.generatorVersion == FpsArenaGeneratorVersion.LegacyV1) r.internalWalls = true;
                var l = FpsArenaPlanner.Generate(r, c.seed); Assert.IsEmpty(l.Walls); Assert.AreEqual(c.hash, l.Hash);
            }
        }
        [Test]
        public void WallsRepeatWithoutChangingSourceOrStairs_AndContentDensityDoesNotMoveWalls()
        {
            var r = WallRecipe(); string source = JsonUtility.ToJson(r);
            var a = FpsArenaPlanner.Generate(r, -7); var b = FpsArenaPlanner.Generate(r, -7);
            Assert.AreEqual(a.Hash, b.Hash); Assert.AreEqual(source, JsonUtility.ToJson(r)); Assert.IsNotEmpty(a.Walls);
            Assert.IsNotEmpty(a.Content.Where(c => c.kind == FpsArenaContentKind.Cover));
            r.enemyDensity = r.gimmickDensity = r.itemDensity = 1;
            var dense = FpsArenaPlanner.Generate(r, -7);
            CollectionAssert.AreEqual(a.Walls.Select(w => JsonUtility.ToJson(w)), dense.Walls.Select(w => JsonUtility.ToJson(w)));
            r.internalWalls = false; var off = FpsArenaPlanner.Generate(r, -7);
            CollectionAssert.AreEqual(a.Stairs.Select(s => JsonUtility.ToJson(s)), off.Stairs.Select(s => JsonUtility.ToJson(s)));
            Assert.AreNotEqual(a.Hash, FpsArenaPlanner.Generate(WallRecipe(), 71).Hash);
        }
        [Test]
        public void WallsAndMaximumContent_144ExtremeMultiFloorSeedProfilesRemainConnectedAndDisjoint()
        {
            int profiles = 0;
            foreach (FpsArenaShape shape in Enum.GetValues(typeof(FpsArenaShape)))
            foreach (var dimensions in new[] { new Vector2Int(20,20), new Vector2Int(21,64), new Vector2Int(64,20), new Vector2Int(64,64) })
            foreach (int floors in new[] { 1, 2, 4 }) foreach (int seed in new[] { -7, 0, 73125, int.MaxValue })
            {
                var r = WallRecipe(); r.width = dimensions.x; r.depth = dimensions.y; r.shape = shape; r.floors = floors;
                r.cellSize = 2; r.floorHeight = 6; r.wallHeight = 6; r.wallDensity = .35f;
                r.wallMinLengthCells = seed == 0 ? 3 : 8; r.wallMaxLengthCells = 16; r.wallDoorWidthCells = seed == 0 ? 3 : 1;
                r.coverDensity = r.enemyDensity = r.gimmickDensity = r.itemDensity = 1;
                var l = FpsArenaPlanner.Generate(r, seed); l.Validate(); profiles++;
                var occupied = new HashSet<Vector3Int>();
                foreach (var w in l.Walls) foreach (var c in w.Footprint()) { Assert.IsTrue(occupied.Add(c)); Assert.IsFalse(l.Reserved[c.x,c.y,c.z]); }
                foreach (var p in l.Content) foreach (var c in p.Footprint()) Assert.IsTrue(occupied.Add(c));
                // Conservative full content footprints cover solid authored enemy/gimmick prefabs too.
                Assert.AreEqual(l.FloorCount() - occupied.Count, l.ReachableCount(occupied), "Disconnected profile " + shape + "/" + dimensions + "/" + floors + "/" + seed);
                foreach (var s in l.Stairs) for (int lane = 0; lane < s.width; lane++)
                { Assert.IsFalse(occupied.Contains(s.BottomLane(lane))); Assert.IsFalse(occupied.Contains(s.TopLane(lane))); }
                for (int f = 0; f < floors; f++) Assert.LessOrEqual(l.Walls.Count(w => w.cell.y == f), r.maxWallRunsPerFloor);
            }
            Assert.AreEqual(144, profiles);
        }
        [Test]
        public void WallBoundsAndDoorways_HaveExactSolidCollidersAndClearCapsuleApproaches()
        {
            var l = FpsArenaPlanner.Generate(WallRecipe(), 73125); var root = FpsArenaSceneBuilder.Build(l, null); root.SetActive(true);
            try
            {
                Physics.SyncTransforms();
                foreach (var w in l.Walls)
                {
                    var group = root.transform.Find(w.id); Assert.IsNotNull(group);
                    Assert.AreEqual(w.doorWidthCells > 0 ? 2 : 1, group.childCount);
                    foreach (var span in w.SolidSpans())
                    {
                        var collider = group.Find("내부 벽_구간_" + span.x).GetComponent<BoxCollider>(); Assert.IsFalse(collider.isTrigger);
                        Vector3 expected = w.alongX ? new Vector3(span.y * l.Recipe.cellSize,w.height,w.thickness) : new Vector3(w.thickness,w.height,span.y * l.Recipe.cellSize);
                        Assert.Less(Vector3.Distance(expected, collider.bounds.size), .001f);
                    }
                    for (int i = w.doorOffsetCells; i < w.doorOffsetCells + w.doorWidthCells; i++)
                    {
                        Vector3 foot = l.Position(w.cell + w.Step * i) + Vector3.up * .06f;
                        Assert.IsFalse(Physics.CheckCapsule(foot + Vector3.up * .35f, foot + Vector3.up * 1.45f, .35f, ~0, QueryTriggerInteraction.Ignore), "Door blocked: " + w.id);
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void WallsWithAuthoredPrefabs_AllFourCategoriesHaveDisjointActualColliderBounds()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); GameObject root = null;
            try
            {
                foreach (var kind in new[] { FpsArenaContentKind.Cover,FpsArenaContentKind.Enemy,FpsArenaContentKind.Gimmick,FpsArenaContentKind.Item })
                    catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "wall-test:" + kind, kind = kind, prefab = prefab, widthCells = 2, depthCells = 2, height = 2, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                var r = WallRecipe(); r.coverDensity = r.enemyDensity = r.gimmickDensity = r.itemDensity = .15f;
                // Reserve room for every category: extreme densities can exhaust safe slots
                // before later categories, which is the existing V2 capacity contract.
                r.maxContentPerCategoryPerFloor = 4;
                var l = FpsArenaPlanner.Generate(r,73125,catalog); root = FpsArenaSceneBuilder.Build(l,null,catalog); root.transform.position = Vector3.right * 1000; root.SetActive(true); Physics.SyncTransforms();
                var contentColliders = root.GetComponentsInChildren<FpsArenaContentIdentity>().SelectMany(identity => identity.GetComponentsInChildren<Collider>()).ToArray();
                foreach (var kind in new[] { FpsArenaContentKind.Cover,FpsArenaContentKind.Enemy,FpsArenaContentKind.Gimmick,FpsArenaContentKind.Item }) Assert.IsTrue(l.Content.Any(p => p.kind == kind), "Missing test category: " + kind);
                foreach (var wall in l.Walls) foreach (var collider in root.transform.Find(wall.id).GetComponentsInChildren<BoxCollider>())
                    foreach (var content in contentColliders) Assert.IsFalse(collider.bounds.Intersects(content.bounds), "Authored prefab overlaps wall: " + wall.id);
                for (int i = 0; i < contentColliders.Length; i++) for (int j = i + 1; j < contentColliders.Length; j++) Assert.IsFalse(contentColliders[i].bounds.Intersects(contentColliders[j].bounds));
                var occupied = l.BlockedCells(); foreach(var p in l.Content) foreach(var c in p.Footprint()) occupied.Add(c);
                Assert.AreEqual(l.FloorCount() - occupied.Count,l.ReachableCount(occupied));
            }
            finally { if(root != null) Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(catalog); }
        }
        [Test]
        public void WallSettings_UndoAssetReloadSceneReopenAndRepeatedSetupPreserveBuiltSnapshot()
        {
            string folder = "Assets/__WallPersistence_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7)); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var settings = ScriptableObject.CreateInstance<FpsArenaSettings>(); settings.recipe = WallRecipe(); settings.seed = 71;
                AssetDatabase.CreateAsset(settings, folder + "/Settings.asset");
                var so = new SerializedObject(settings); so.Update(); so.FindProperty("recipe.wallThickness").floatValue = .55f; so.ApplyModifiedProperties(); EditorUtility.SetDirty(settings);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(.35f, settings.recipe.wallThickness, .001f);
                so.Update(); so.FindProperty("recipe.wallThickness").floatValue = .55f; so.ApplyModifiedProperties(); EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(folder + "/Settings.asset", ImportAssetOptions.ForceUpdate);
                settings = AssetDatabase.LoadAssetAtPath<FpsArenaSettings>(folder + "/Settings.asset"); Assert.IsTrue(settings.recipe.internalWalls); Assert.AreEqual(.55f, settings.recipe.wallThickness, .001f);
                var host = new GameObject("Saved wall arena"); var g = host.AddComponent<FpsArenaGenerator>(); g.generateOnPlay = false; g.settings = settings;
                FpsArenaWindow.GenerateWithUndo(g); FpsArenaWindow.GenerateWithUndo(g); Assert.AreEqual(1, host.transform.childCount);
                string builtHash = g.CurrentLayout.Hash; int walls = g.CurrentLayout.Walls.Count;
                Assert.Greater(walls, 0); Assert.IsTrue(EditorSceneManager.SaveScene(host.scene, folder + "/Arena.unity"));
                settings.recipe.internalWalls = false; EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
                var scene = EditorSceneManager.OpenScene(folder + "/Arena.unity", OpenSceneMode.Single);
                g = scene.GetRootGameObjects().First(o => o.GetComponent<FpsArenaGenerator>() != null).GetComponent<FpsArenaGenerator>();
                Assert.AreEqual(builtHash, g.CurrentLayout.Hash); Assert.AreEqual(walls, g.CurrentLayout.Walls.Count);
                Assert.AreEqual(1, g.transform.childCount); Assert.IsEmpty(g.Plan().Walls); Assert.AreNotEqual(builtHash, g.Plan().Hash);
                g.enabled = false; g.enabled = true; Assert.AreEqual(builtHash, g.CurrentLayout.Hash);
            }
            finally { Undo.ClearAll(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); AssetDatabase.DeleteAsset(folder); }
        }
        [Test]
        public void WallNormalizationAndZeroDensity_HandleNonFiniteValuesAndShortPartitions()
        {
            var r = WallRecipe(); r.wallDensity = float.NaN; r.wallHeight = float.PositiveInfinity; r.wallThickness = float.NaN;
            r.wallMinLengthCells = 16; r.wallMaxLengthCells = 3; r.wallDoorWidthCells = 99; r.maxWallRunsPerFloor = 0;
            var n = r.Normalized(); Assert.AreEqual(16,n.wallMaxLengthCells); Assert.AreEqual(3,n.wallDoorWidthCells); Assert.AreEqual(1,n.maxWallRunsPerFloor);
            Assert.That(n.wallHeight, Is.InRange(2,n.floorHeight - .25f)); Assert.DoesNotThrow(() => FpsArenaPlanner.Generate(r, 7));
            r.wallDensity = 0; Assert.IsEmpty(FpsArenaPlanner.Generate(r, 7).Walls);
            r = WallRecipe(); r.wallMinLengthCells = r.wallMaxLengthCells = 3; r.wallDoorWidthCells = 3;
            var shortWalls = FpsArenaPlanner.Generate(r, 7); Assert.IsNotEmpty(shortWalls.Walls); Assert.IsTrue(shortWalls.Walls.All(w => w.doorWidthCells == 0)); shortWalls.Validate();
        }
    }
}
