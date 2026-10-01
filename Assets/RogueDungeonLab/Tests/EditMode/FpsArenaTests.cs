using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RogueDungeonLab.Editor;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaTests
    {
        [Test]
        public void SameRecipeAndSeed_RepeatsWithoutMutatingRecipe()
        {
            var recipe = FpsArenaRecipe.Preset(FpsArenaPreset.Team);
            string before = JsonUtility.ToJson(recipe);
            var a = FpsArenaPlanner.Generate(recipe, -987654321); var b = FpsArenaPlanner.Generate(recipe, -987654321);
            Assert.AreEqual(a.Hash, b.Hash); Assert.AreEqual(before, JsonUtility.ToJson(recipe));
            Assert.AreNotEqual(a.Hash, FpsArenaPlanner.Generate(recipe, 12345).Hash);
            Assert.AreEqual(a.Hash, a.ComputeHash());
        }
        [Test]
        public void Normalize_HandlesInvalidAndNonFiniteValuesWithoutChangingSource()
        {
            var recipe = new FpsArenaRecipe { width = -1, depth = 500, floors = 99, cellSize = float.NaN, floorHeight = float.PositiveInfinity, shape = (FpsArenaShape)99, spacingCells = -4 };
            var normalized = recipe.Normalized();
            Assert.AreEqual(20, normalized.width); Assert.AreEqual(64, normalized.depth); Assert.AreEqual(4, normalized.floors);
            Assert.AreEqual(3, normalized.cellSize); Assert.AreEqual(4, normalized.floorHeight); Assert.AreEqual(FpsArenaShape.Rectangle, normalized.shape);
            Assert.AreEqual(-1, recipe.width);
            Assert.DoesNotThrow(() => FpsArenaPlanner.Generate(recipe, 1));
        }
        [Test]
        public void ShapesFloorsAndExtremeAspectRatios_HaveCompleteStairsAndConnectedFloor()
        {
            foreach (FpsArenaShape shape in Enum.GetValues(typeof(FpsArenaShape)))
            foreach (var dimensions in new[] { new Vector2Int(20,20), new Vector2Int(21,64), new Vector2Int(64,20), new Vector2Int(64,64) })
            foreach (int floors in new[] { 1, 2, 4 })
            {
                var recipe = new FpsArenaRecipe { width = dimensions.x, depth = dimensions.y, shape = shape, floors = floors, cellSize = 2, floorHeight = 6, coverPerFloor = 0, obstaclesPerFloor = 0, itemsPerFloor = 0 };
                var l = FpsArenaPlanner.Generate(recipe, 71);
                Assert.AreEqual((floors - 1) * 2, l.Stairs.Count); l.Validate();
                Assert.AreEqual(l.FloorCount(), l.ReachableCount(new HashSet<Vector3Int>()));
                foreach (var stair in l.Stairs)
                {
                    Assert.LessOrEqual(recipe.floorHeight / Mathf.CeilToInt(recipe.floorHeight / .18f), .180001f);
                    Assert.LessOrEqual(recipe.floorHeight / (stair.length * recipe.cellSize), .600001f);
                    for (int lane = 0; lane < stair.width; lane++)
                    {
                        Assert.IsTrue(l.HasFloor(stair.Bottom + Vector3Int.right * lane));
                        Assert.IsTrue(l.HasFloor(stair.Top + Vector3Int.right * lane));
                        for (int z = stair.z; z < stair.z + stair.length; z++)
                        {
                            Assert.IsTrue(FpsArenaPlanner.InShape(l.Recipe, stair.x + lane, z));
                            Assert.IsFalse(l.Floors[stair.x + lane, stair.lowerFloor + 1, z]);
                        }
                    }
                }
            }
        }
        [Test]
        public void HighDensity_LeavesSpawnsStairsAndWalkableRoutesClear()
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var l = FpsArenaPlanner.Generate(new FpsArenaRecipe { width = 20, depth = 20, floors = 4, coverPerFloor = 100, obstaclesPerFloor = 100, itemsPerFloor = 100, spacingCells = 1 }, seed);
                l.Validate(); var blocked = l.BlockedCells();
                Assert.AreEqual(l.FloorCount() - blocked.Count, l.ReachableCount(blocked));
                foreach (var spawn in l.Spawns) Assert.IsFalse(blocked.Contains(spawn));
                for (int i = 0; i < l.Content.Count; i++)
                {
                    var a = l.Content[i]; Assert.IsFalse(l.Reserved[a.cell.x, a.cell.y, a.cell.z]);
                    for (int j = 0; j < i; j++)
                    {
                        var b = l.Content[j]; if (a.cell.y != b.cell.y) continue;
                        Assert.IsTrue(Mathf.Abs(a.cell.x - b.cell.x) > 1 || Mathf.Abs(a.cell.z - b.cell.z) > 1);
                    }
                }
            }
        }
        [Test]
        public void ZeroDensity_ProducesNoContent_AndItemsCannotMoveCover()
        {
            var r = new FpsArenaRecipe { coverPerFloor = 0, obstaclesPerFloor = 0, itemsPerFloor = 0 };
            Assert.AreEqual(0, FpsArenaPlanner.Generate(r, 5).Content.Count);
            r.coverPerFloor = 10; var a = FpsArenaPlanner.Generate(r, 5); r.itemsPerFloor = 100; var b = FpsArenaPlanner.Generate(r, 5);
            var coverA = a.Content.FindAll(p => p.kind == FpsArenaContentKind.Cover);
            var coverB = b.Content.FindAll(p => p.kind == FpsArenaContentKind.Cover);
            Assert.AreEqual(coverA.Count, coverB.Count);
            for (int i = 0; i < coverA.Count; i++) Assert.AreEqual(coverA[i].cell, coverB[i].cell);
        }
        [Test]
        public void BrokenOpeningOrLanding_IsRejected()
        {
            var l = FpsArenaPlanner.Generate(new FpsArenaRecipe(), 1); var s = l.Stairs[0];
            l.Floors[s.x + 1, s.lowerFloor + 1, s.z] = true;
            Assert.Throws<InvalidOperationException>(() => l.Validate());
            l.Floors[s.x + 1, s.lowerFloor + 1, s.z] = false;
            l.Floors[s.x + 1, s.lowerFloor + 1, s.z + s.length] = false;
            Assert.Throws<InvalidOperationException>(() => l.Validate());
        }
        [Test]
        public void RepeatedEditorGeneration_ReplacesOnlyArenaOwnedRoot_AndUndoRestores()
        {
            var host = new GameObject("Arena test");
            try
            {
                var g = host.AddComponent<FpsArenaGenerator>(); g.recipe = FpsArenaRecipe.Preset(FpsArenaPreset.Duel);
                var unrelated = new GameObject("Keep me"); unrelated.transform.SetParent(host.transform);
                FpsArenaWindow.GenerateWithUndo(g); string first = g.CurrentLayout.Hash;
                UnityEditor.Undo.IncrementCurrentGroup(); g.seed++;
                FpsArenaWindow.GenerateWithUndo(g);
                Assert.AreEqual(2, host.transform.childCount); Assert.IsNotNull(unrelated);
                Assert.AreNotEqual(first, g.CurrentLayout.Hash);
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo(); Assert.AreEqual(2, host.transform.childCount);
                Assert.IsNotNull(host.transform.Find(FpsArenaGenerator.GeneratedRootName));
                Assert.AreEqual(first, g.CurrentLayout.Hash);
                UnityEditor.Undo.IncrementCurrentGroup();
                FpsArenaWindow.GenerateWithUndo(g);
                UnityEditor.Undo.FlushUndoRecordObjects(); UnityEditor.Undo.PerformUndo();
                Assert.IsNotNull(g.GeneratedRoot);
                Assert.AreEqual(first, g.CurrentLayout.Hash);
            }
            finally { UnityEditor.Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(host); }
        }
        [Test]
        public void SavedScene_RestoresMaterialsAndLayoutWithoutRegeneration()
        {
            string path = "Assets/__FpsArenaReload_" + Guid.NewGuid().ToString("N") + ".unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                var host = new GameObject("Saved arena"); var g = host.AddComponent<FpsArenaGenerator>();
                g.recipe = FpsArenaRecipe.Preset(FpsArenaPreset.Duel); g.generateOnPlay = false; g.Generate();
                string hash = g.CurrentLayout.Hash;
                Assert.IsTrue(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, path));
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                g = scene.GetRootGameObjects()[0].GetComponent<FpsArenaGenerator>();
                Assert.IsNotNull(g.CurrentLayout); Assert.AreEqual(hash, g.CurrentLayout.Hash);
                foreach (var renderer in g.GetComponentsInChildren<Renderer>()) Assert.IsNotNull(renderer.sharedMaterial);
                Assert.AreEqual(1, g.transform.childCount);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }
        }
        [Test]
        public void BuiltGeometry_HasTriggerItemsAndBoundedStairRisers()
        {
            var l = FpsArenaPlanner.Generate(new FpsArenaRecipe { floors = 2, floorHeight = 3, cellSize = 5 }, 7);
            GameObject root = FpsArenaSceneBuilder.Build(l, null);
            try
            {
                foreach (var content in root.GetComponentsInChildren<FpsArenaContentIdentity>(true))
                    Assert.AreEqual(content.Kind == FpsArenaContentKind.Item, content.GetComponent<Collider>().isTrigger);
                foreach (var stair in l.Stairs)
                {
                    var group = root.transform.Find("계단_" + stair.lowerFloor + "_" + stair.x);
                    float previous = 0;
                    foreach (Transform step in group)
                    {
                        if (!step.name.StartsWith("단_", StringComparison.Ordinal)) continue;
                        float top = step.localPosition.y + step.localScale.y * .5f;
                        Assert.LessOrEqual(top - previous, .18001f); previous = top;
                    }
                    Assert.AreEqual(l.Recipe.floorHeight, previous, .0001f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
