using System;
using System.IO;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonDistributionTests
    {
        private const string R7BakedStagePath =
            "Assets/R7ManualVerification/Stages/R7_BakedStage.asset";

        // Runtime Core 계획이 기존 어셈블리 이름을 유지하고 Sample·Input System을 포함하지 않는지 확인합니다.
        [Test]
        public void RuntimeCorePlan_IsIndependentFromLabSampleAndInputSystem()
        {
            DungeonDistributionPlan plan =
                DungeonDistributionExporter.PlanRuntimeCore();

            Assert.That(plan.IsValid, Is.True, Describe(plan.ValidationReport));
            Assert.That(
                ContainsPath(
                    plan,
                    "Assets/RogueDungeonLab/Runtime/RogueDungeonLab.Runtime.asmdef"),
                Is.True);
            Assert.That(
                ContainsPrefix(plan, "Assets/RogueDungeonLab/Samples/"),
                Is.False);
            Assert.That(
                ContainsPrefix(plan, "Assets/RogueDungeonLab/Editor/"),
                Is.False);

            string asmdef = File.ReadAllText(
                "Assets/RogueDungeonLab/Runtime/RogueDungeonLab.Runtime.asmdef");
            StringAssert.Contains("\"name\": \"RogueDungeonLab.Runtime\"", asmdef);
            StringAssert.DoesNotContain("Unity.InputSystem", asmdef);
        }

        // 선택 Sample 계획만 Input System 요구사항과 HUD·플레이어 스크립트를 갖는지 확인합니다.
        [Test]
        public void LabSamplePlan_DeclaresOptionalInputSystemDependency()
        {
            DungeonDistributionPlan plan =
                DungeonDistributionExporter.PlanLabSample();

            Assert.That(plan.IsValid, Is.True, Describe(plan.ValidationReport));
            Assert.That(
                ContainsPath(
                    plan,
                    "Assets/RogueDungeonLab/Samples/Lab/RuntimeLabHUD.cs"),
                Is.True);
            Assert.That(
                ContainsPath(
                    plan,
                    "Assets/RogueDungeonLab/Samples/Lab/PrototypePlayerController.cs"),
                Is.True);
            Assert.That(plan.RequiredPackages.Count, Is.EqualTo(1));
            Assert.That(
                plan.RequiredPackages[0].packageId,
                Is.EqualTo(DungeonDistributionExporter.InputSystemPackageId));
        }

        // Runtime 예제 package에 두 source Definition과 HUD 없는 두 smoke scene이 모두 포함되는지 확인합니다.
        [Test]
        public void RuntimeExamplesPlan_ContainsProceduralAndSavedCoreScenes()
        {
            DungeonDistributionPlan plan =
                DungeonDistributionExporter.PlanRuntimeExamples();

            Assert.That(plan.IsValid, Is.True, Describe(plan.ValidationReport));
            Assert.That(
                ContainsPath(
                    plan,
                    R9RuntimeExamplesSetup.ProceduralStagePath),
                Is.True);
            Assert.That(
                ContainsPath(
                    plan,
                    R9RuntimeExamplesSetup.SavedStagePath),
                Is.True);
            Assert.That(
                ContainsPath(
                    plan,
                    R9RuntimeExamplesSetup.ProceduralScenePath),
                Is.True);
            Assert.That(
                ContainsPath(
                    plan,
                    R9RuntimeExamplesSetup.SavedScenePath),
                Is.True);
            Assert.That(
                ContainsPrefix(plan, "Assets/RogueDungeonLab/Samples/"),
                Is.False);
        }

        // Bake 제작 패키지가 Sample 없이 독립되고 standalone 선택에서만 Runtime Core를 포함하는지 확인합니다.
        [Test]
        public void BakeAuthoringPlan_SeparatesPlayerAndEditorBoundaries()
        {
            DungeonDistributionPlan modular =
                DungeonDistributionExporter.PlanBakeAuthoring();
            DungeonDistributionPlan standalone =
                DungeonDistributionExporter.PlanBakeAuthoring(true);

            Assert.That(modular.IsValid, Is.True, Describe(modular.ValidationReport));
            Assert.That(standalone.IsValid, Is.True, Describe(standalone.ValidationReport));
            Assert.That(
                ContainsPrefix(modular, "Assets/RogueDungeonLab/Runtime/"),
                Is.False);
            Assert.That(
                ContainsPrefix(modular, "Assets/RogueDungeonLab/Samples/"),
                Is.False);
            Assert.That(
                ContainsPath(
                    modular,
                    "Assets/RogueDungeonLab/Editor/Baking/DungeonStageBaker.cs"),
                Is.True);
            Assert.That(
                ContainsPath(
                    standalone,
                    "Assets/RogueDungeonLab/Runtime/RogueDungeonLab.Runtime.asmdef"),
                Is.True);
        }

        // 최신 R7 Baked Stage의 논리·파생·Catalog 의존성이 포함되고 개발 전용 경로는 제외되는지 확인합니다.
        [Test]
        public void BakedStagePlan_CollectsRuntimeDependenciesAndSupportsStandalone()
        {
            DungeonStageDefinition definition =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                    R7BakedStagePath);
            Assert.That(definition, Is.Not.Null);

            DungeonDistributionPlan modular =
                DungeonDistributionExporter.PlanBakedStage(definition);
            DungeonDistributionPlan standalone =
                DungeonDistributionExporter.PlanBakedStage(definition, true);

            Assert.That(modular.IsValid, Is.True, Describe(modular.ValidationReport));
            Assert.That(standalone.IsValid, Is.True, Describe(standalone.ValidationReport));
            Assert.That(ContainsPath(modular, R7BakedStagePath), Is.True);
            Assert.That(
                ContainsPath(
                    modular,
                    AssetDatabase.GetAssetPath(definition.bakeManifest)),
                Is.True);
            Assert.That(
                ContainsPath(
                    modular,
                    AssetDatabase.GetAssetPath(definition.bakedPrefab)),
                Is.True);
            Assert.That(
                ContainsPrefix(modular, "Assets/RogueDungeonLab/Runtime/"),
                Is.False);
            Assert.That(
                ContainsPrefix(modular, "Assets/RogueDungeonLab/Samples/"),
                Is.False);
            Assert.That(
                ContainsPrefix(modular, "Assets/RogueDungeonLab/Editor/"),
                Is.False);
            Assert.That(
                ContainsPath(
                    standalone,
                    "Assets/RogueDungeonLab/Runtime/RogueDungeonLab.Runtime.asmdef"),
                Is.True);
            Assert.That(
                ContainsRequiredPackage(
                    modular,
                    "com.unity.render-pipelines.universal"),
                Is.True,
                "The R7 material set uses URP/Lit and must declare URP for consumers.");
            Assert.That(modular.RenderPipeline, Is.EqualTo("Universal"));
            Assert.That(
                standalone.PackageId.EndsWith(
                    "-standalone",
                    StringComparison.Ordinal),
                Is.True);
        }

        // 실제 Core unitypackage와 JSON sidecar가 생성되고 metadata hash가 파일 내용과 일치하는지 확인합니다.
        [Test]
        public void Export_WritesUnityPackageAndMetadataSidecar()
        {
            string folder = Path.GetFullPath("Logs/R9DistributionTests");
            Directory.CreateDirectory(folder);
            string packagePath = Path.Combine(
                folder,
                "RuntimeCore.unitypackage");
            DungeonDistributionPlan plan =
                DungeonDistributionExporter.PlanRuntimeCore();

            DungeonDistributionMetadata metadata =
                DungeonDistributionExporter.Export(plan, packagePath);

            Assert.That(File.Exists(packagePath), Is.True);
            Assert.That(new FileInfo(packagePath).Length, Is.GreaterThan(0));
            Assert.That(File.Exists(packagePath + ".json"), Is.True);
            Assert.That(metadata.packageSha256, Has.Length.EqualTo(64));
            string json = File.ReadAllText(packagePath + ".json");
            StringAssert.Contains(metadata.packageSha256, json);
            StringAssert.Contains("RogueDungeonLab.Runtime.asmdef", json);
        }

        // 계획의 canonical asset path 목록에 정확한 경로가 있는지 확인합니다.
        private static bool ContainsPath(
            DungeonDistributionPlan plan,
            string expected)
        {
            if (plan == null || string.IsNullOrEmpty(expected)) return false;
            string normalized = expected.Replace('\\', '/');
            for (int i = 0; i < plan.AssetPaths.Count; i++)
            {
                if (string.Equals(
                        plan.AssetPaths[i],
                        normalized,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // 계획에 지정 접두사 아래의 자산이 하나라도 포함되는지 확인합니다.
        private static bool ContainsPrefix(
            DungeonDistributionPlan plan,
            string prefix)
        {
            if (plan == null || string.IsNullOrEmpty(prefix)) return false;
            for (int i = 0; i < plan.AssetPaths.Count; i++)
            {
                if (plan.AssetPaths[i].StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // 계획이 지정 Unity package ID를 설치 요구사항으로 기록했는지 확인합니다.
        private static bool ContainsRequiredPackage(
            DungeonDistributionPlan plan,
            string packageId)
        {
            if (plan == null || string.IsNullOrEmpty(packageId)) return false;
            for (int i = 0; i < plan.RequiredPackages.Count; i++)
            {
                if (string.Equals(
                        plan.RequiredPackages[i].packageId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // 실패 assertion에 코드와 메시지를 남기도록 검증 리포트를 한 줄로 요약합니다.
        private static string Describe(DungeonValidationReport report)
        {
            if (report == null || report.issues == null) return "<null>";
            string result = string.Empty;
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue == null) continue;
                if (result.Length > 0) result += " | ";
                result += issue.code + ": " + issue.message;
            }
            return result;
        }
    }
}
