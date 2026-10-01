using System;
using System.Reflection;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaEditorFlexibleTests
    {
        private static object InvokeEditorHelper(string name, params object[] args)
        {
            var method = typeof(FpsArenaWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, name);
            return method.Invoke(null, args);
        }

        [Test]
        public void ExplicitUpgrade_PreservesBuiltResultAndSeed_AndUndoRestoresLegacyRecipe()
        {
            var host = new GameObject("Upgrade editor test");
            try
            {
                var generator = host.AddComponent<FpsArenaGenerator>(); generator.seed = 412;
                generator.recipe = new FpsArenaRecipe { floors = 1, coverPerFloor = 7, obstaclesPerFloor = 3, itemsPerFloor = 2 };
                string before = JsonUtility.ToJson(generator.recipe);
                FpsArenaWindow.GenerateWithUndo(generator); string hash = generator.CurrentLayout.Hash;
                Undo.IncrementCurrentGroup();
                InvokeEditorHelper("UpgradeRecipe", generator);
                Assert.AreEqual(FpsArenaGeneratorVersion.FlexibleV2, generator.recipe.generatorVersion);
                Assert.AreEqual(412, generator.seed);
                Assert.AreEqual(hash, generator.CurrentLayout.Hash, "Upgrade changes inputs without replacing the built scene.");
                Assert.AreEqual(1, generator.CurrentLayout.GeneratorVersion);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.AreEqual(before, JsonUtility.ToJson(generator.recipe));
                Assert.AreEqual(hash, generator.CurrentLayout.Hash);
            }
            finally { Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void ExplicitSharedSettingsUpgrade_LeavesInlineRecipeUntouched()
        {
            var host = new GameObject("Shared settings upgrade test"); var settings = ScriptableObject.CreateInstance<FpsArenaSettings>();
            try
            {
                var generator = host.AddComponent<FpsArenaGenerator>(); generator.settings = settings;
                settings.seed = -125; settings.recipe = new FpsArenaRecipe { width = 24, depth = 32, floors = 3 };
                string inline = JsonUtility.ToJson(generator.recipe);
                InvokeEditorHelper("UpgradeRecipe", settings);
                Assert.AreEqual(FpsArenaGeneratorVersion.FlexibleV2, settings.recipe.generatorVersion);
                Assert.AreEqual(-125, settings.seed); Assert.AreEqual(24, settings.recipe.width); Assert.AreEqual(32, settings.recipe.depth);
                Assert.AreEqual(inline, JsonUtility.ToJson(generator.recipe));
            }
            finally { Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void BoundsMeasurement_UsesRootLocalRendererAndColliderSizeAndCenter()
        {
            var root = new GameObject("Offset bounds test");
            try
            {
                root.transform.position = new Vector3(10, 5, 4); root.transform.rotation = Quaternion.Euler(0, 90, 0); root.transform.localScale = new Vector3(2, 3, 4);
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(3, 1, -2); child.transform.localScale = new Vector3(2, 4, 6);
                object[] args = { root, Vector3.zero, Vector3.zero };
                Assert.IsTrue((bool)InvokeEditorHelper("TryMeasurePrefabBounds", args));
                Assert.Less(Vector3.Distance(new Vector3(2, 4, 6), (Vector3)args[1]), .0001f);
                Assert.Less(Vector3.Distance(new Vector3(3, 1, -2), (Vector3)args[2]), .0001f);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void BoundsMeasurement_UsesConservativeScaledSpheresCapsulesAndControllers()
        {
            var root = new GameObject("Scaled rounded collider bounds test");
            try
            {
                for (int kind = 0; kind < 3; kind++)
                {
                    var child = new GameObject("Collider"); child.transform.SetParent(root.transform, false); child.transform.localPosition = new Vector3(3, 1, -2);
                    Vector3 expected;
                    if (kind == 0)
                    {
                        var sphere = child.AddComponent<SphereCollider>(); sphere.radius = .5f; child.transform.localScale = new Vector3(4, 1, 2); expected = Vector3.one * 4;
                    }
                    else
                    {
                        child.transform.localScale = new Vector3(2, 1, 1); expected = new Vector3(2, 6, 2);
                        if (kind == 1) { var capsule = child.AddComponent<CapsuleCollider>(); capsule.radius = .5f; capsule.height = 6; capsule.direction = 1; }
                        else { var controller = child.AddComponent<CharacterController>(); controller.radius = .5f; controller.height = 6; controller.center = Vector3.zero; }
                    }
                    object[] args = { root, Vector3.zero, Vector3.zero };
                    Assert.IsTrue((bool)InvokeEditorHelper("TryMeasurePrefabBounds", args));
                    Assert.Less(Vector3.Distance(expected, (Vector3)args[1]), .0001f);
                    Assert.Less(Vector3.Distance(new Vector3(3, 1, -2), (Vector3)args[2]), .0001f);
                    UnityEngine.Object.DestroyImmediate(child);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void GenerateWithUndo_UsesActualPrefabFromSharedSettingsCatalog()
        {
            string prefabPath = "Assets/__FpsArenaEditorPrefab_" + Guid.NewGuid().ToString("N") + ".prefab";
            var host = new GameObject("Catalog editor test"); var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var settings = ScriptableObject.CreateInstance<FpsArenaSettings>(); var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>();
            var inlineCatalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>();
            try
            {
                source.name = "Editor catalog enemy"; var prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "test:enemy", kind = FpsArenaContentKind.Enemy, prefab = prefab, authoredBounds = Vector3.one, authoredBoundsCenter = Vector3.zero });
                Assert.AreEqual("", (string)InvokeEditorHelper("CatalogValidationMessage", catalog));
                var generator = host.AddComponent<FpsArenaGenerator>(); generator.contentCatalog = inlineCatalog; generator.settings = settings;
                settings.contentCatalog = catalog; settings.recipe = FpsArenaRecipe.CreateFlexible(); settings.recipe.floors = 1;
                settings.recipe.coverDensity = 0; settings.recipe.gimmickDensity = 0; settings.recipe.itemDensity = 0; settings.recipe.enemyDensity = .02f;
                FpsArenaWindow.GenerateWithUndo(generator);
                int enemies = 0;
                foreach (var identity in generator.GeneratedRoot.GetComponentsInChildren<FpsArenaContentIdentity>(true))
                {
                    if (identity.Kind != FpsArenaContentKind.Enemy) continue;
                    enemies++; Assert.AreEqual("test:enemy", identity.ContentKey); Assert.IsNotNull(identity.transform.Find(prefab.name));
                }
                Assert.Greater(enemies, 0);
                catalog.entries.Add(new FpsArenaCatalogEntry { contentKey = "test:enemy", prefab = prefab });
                StringAssert.Contains("중복", (string)InvokeEditorHelper("CatalogValidationMessage", catalog));
            }
            finally
            {
                Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(settings); UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(inlineCatalog);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }
    }
}
