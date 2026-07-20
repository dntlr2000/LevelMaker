using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonEditorSetupTests
    {
        // 한 클릭 장면 구성을 두 번 실행해도 생성기·시스템·카메라·조명을 중복 생성하지 않는지 검사합니다.
        [Test]
        public void SceneSetup_CreateOrRepairSceneIsIdempotent()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Type setupType = Type.GetType(
                "RogueDungeonLab.Editor.RogueDungeonLabSceneSetup, RogueDungeonLab.Editor",
                true);
            MethodInfo method = setupType.GetMethod(
                "CreateOrRepairScene",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            RogueDungeonGenerator first = (RogueDungeonGenerator)method.Invoke(null, new object[] { false });
            RogueDungeonGenerator second = (RogueDungeonGenerator)method.Invoke(null, new object[] { false });

            Assert.That(second, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<RogueDungeonGenerator>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DropValidationService>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<RogueDungeonClickInteractor>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<RuntimeLabHUD>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<LabOrbitCamera>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>().Length, Is.EqualTo(1));
            Assert.That(CountDirectionalLights(), Is.EqualTo(1));
            Assert.That(first.transform.Find(DungeonStageLoader.GeneratedRootName), Is.Not.Null);

            first.ClearGenerated();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static int CountDirectionalLights()
        {
            Light[] lights = Object.FindObjectsByType<Light>();
            int count = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) count++;
            }
            return count;
        }
    }
}
