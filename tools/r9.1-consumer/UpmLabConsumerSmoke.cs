using System;
using RogueDungeonLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace RogueDungeonLabUpmConsumerVerification
{
    public static class UpmLabConsumerSmoke
    {
        private const string CorePackageId =
            "com.dntlr2000.rogue-dungeon-lab.core";
        private const string LabPackageId =
            "com.dntlr2000.rogue-dungeon-lab.lab";
        private const string ScenePath = "Assets/R9_1LabConsumer.unity";

        // Core·Lab·Input System 등록과 선택 Lab 컴포넌트의 scene 직렬화를 확인합니다.
        public static void Verify()
        {
            RequireRegisteredPackage(CorePackageId, "0.13.0");
            RequireRegisteredPackage(LabPackageId, "0.13.0");
            RequireRegisteredPackage("com.unity.inputsystem", "1.19.0");
            RequireAssembly("RogueDungeonLab.Runtime");
            RequireAssembly("RogueDungeonLab.Samples");
            CreateLabScene();
            Debug.Log("R9.1 UPM Lab consumer smoke succeeded.");
        }

        // Package Manager 등록 목록에서 기대 ID와 버전을 확인합니다.
        private static void RequireRegisteredPackage(
            string packageId,
            string expectedVersion)
        {
            PackageManagerInfo[] packages =
                PackageManagerInfo.GetAllRegisteredPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                if (!string.Equals(
                        packages[i].name,
                        packageId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!string.Equals(
                        packages[i].version,
                        expectedVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        packageId + " version mismatch: " + packages[i].version);
                }
                return;
            }
            throw new InvalidOperationException(
                "Required UPM package is not registered: " + packageId);
        }

        // 현재 AppDomain에 지정 Runtime 또는 Sample assembly가 로드되었는지 확인합니다.
        private static void RequireAssembly(string assemblyName)
        {
            System.Reflection.Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        assemblyName,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            throw new InvalidOperationException(
                "Required assembly is not loaded: " + assemblyName);
        }

        // 자유 카메라·HUD·클릭·임시 플레이어 타입을 실제 scene 컴포넌트로 구성합니다.
        private static void CreateLabScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject generatorObject = new GameObject("R9.1 Lab Generator");
            generatorObject.AddComponent<RogueDungeonGenerator>();
            generatorObject.AddComponent<DropValidationService>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<LabOrbitCamera>();
            cameraObject.AddComponent<RogueDungeonClickInteractor>();

            GameObject hudObject = new GameObject("R9.1 Lab HUD");
            hudObject.AddComponent<RuntimeLabHUD>();

            GameObject playerObject = new GameObject("R9.1 Prototype Player");
            playerObject.AddComponent<CharacterController>();
            playerObject.AddComponent<PrototypePlayerController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (scene.GetRootGameObjects().Length != 4)
            {
                throw new InvalidOperationException(
                    "Lab consumer scene did not preserve all optional roots.");
            }
        }
    }
}
