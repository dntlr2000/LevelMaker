using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public static class R6PlayerBuildSmoke
    {
        private const string VerificationSceneRoot =
            "Assets/R6ManualVerification/Scenes";

        // R6 기준 자산과 Baked 장면을 준비한 뒤 해당 장면 하나로 Windows Player를 빌드합니다.
        public static void BuildFromBatch()
        {
            R6ManualVerificationSetup.CreateAllFromBatch();
            string scenePath = FindBakedVerificationScene();
            string outputPath = ResolveOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null ||
                report.summary.result != BuildResult.Succeeded)
            {
                string result = report != null
                    ? report.summary.result.ToString()
                    : "No build report";
                throw new InvalidOperationException(
                    "R6 Baked Player build smoke failed: " + result);
            }

            Debug.Log(
                "R6 Baked Player build smoke succeeded: " +
                outputPath +
                " (" +
                report.summary.totalSize +
                " bytes)");
        }

        // 수동 검증 폴더에서 이름에 Baked가 포함된 Scene을 안정적인 경로 순서로 선택합니다.
        private static string FindBakedVerificationScene()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { VerificationSceneRoot });
            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            Array.Sort(paths, StringComparer.Ordinal);

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].IndexOf(
                        "Baked",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return paths[i];
                }
            }
            throw new InvalidOperationException(
                "R6 Baked verification Scene was not generated below " +
                VerificationSceneRoot +
                ".");
        }

        // 프로젝트의 Logs 아래에 git 작업 트리를 오염시키지 않는 빌드 출력 경로를 계산합니다.
        private static string ResolveOutputPath()
        {
            return Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Logs",
                    "R6PlayerBuildSmoke",
                    "R6PlayerBuildSmoke.exe"));
        }
    }
}
