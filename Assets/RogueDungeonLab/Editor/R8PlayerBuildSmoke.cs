using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public static class R8PlayerBuildSmoke
    {
        private const string VerificationScenePath =
            "Assets/R8ManualVerification/Scenes/" +
            "R8_RunStateVerification.unity";

        // R8 RunState HUD와 Procedural·SavedDefinition 참조를 포함한 장면으로 Windows Player를 빌드합니다.
        public static void BuildFromBatch()
        {
            R8ManualVerificationSetup.CreateAllFromBatch();
            SceneAsset scene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    VerificationScenePath);
            if (scene == null)
            {
                throw new InvalidOperationException(
                    "R8 verification Scene was not generated at " +
                    VerificationScenePath +
                    ".");
            }

            string outputPath = ResolveOutputPath();
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath));
            BuildPlayerOptions options =
                new BuildPlayerOptions
                {
                    scenes =
                        new[] { VerificationScenePath },
                    locationPathName = outputPath,
                    target =
                        BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };
            BuildReport report =
                BuildPipeline.BuildPlayer(options);
            if (report == null ||
                report.summary.result !=
                    BuildResult.Succeeded ||
                report.summary.totalErrors != 0 ||
                report.summary.totalSize == 0 ||
                !File.Exists(outputPath))
            {
                string result = report != null
                    ? report.summary.result.ToString()
                    : "No build report";
                int errors = report != null
                    ? report.summary.totalErrors
                    : 0;
                throw new InvalidOperationException(
                    "R8 Player build smoke failed: " +
                    result +
                    ", errors=" +
                    errors +
                    ".");
            }

            Debug.Log(
                "R8 Player build smoke succeeded: " +
                outputPath +
                " (" +
                report.summary.totalSize +
                " bytes, warnings=" +
                report.summary.totalWarnings +
                ")");
        }

        // 빌드 산출물을 프로젝트의 비소스 Logs 폴더 아래 고정 경로로 계산합니다.
        private static string ResolveOutputPath()
        {
            return Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Logs",
                    "R8PlayerBuildSmoke",
                    "R8PlayerBuildSmoke.exe"));
        }
    }
}
