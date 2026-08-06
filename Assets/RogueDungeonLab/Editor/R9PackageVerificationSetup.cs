using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public static class R9PackageVerificationSetup
    {
        private const string BakedStagePath =
            "Assets/R7ManualVerification/Stages/R7_BakedStage.asset";
        private const string OutputFolder =
            "Distribution/RogueDungeonLab/R9";

        // R9 핵심·Sample·제작·Baked Stage 배포 묶음을 생성하고 출력 폴더를 엽니다.
        [MenuItem(
            "Tools/Rogue Dungeon Lab/R9 배포 패키지 생성",
            priority = 8)]
        public static void ExportAllAndReveal()
        {
            ExportAll(false);
            EditorUtility.RevealInFinder(
                Path.GetFullPath(OutputFolder));
        }

        // Batchmode에서 대화상자 없이 같은 R9 package와 sidecar를 생성합니다.
        public static void ExportAllFromBatch()
        {
            ExportAll(true);
        }

        // R7 기준 Bake를 필요할 때만 복구한 뒤 일곱 가지 설치 단위와 index를 내보냅니다.
        private static void ExportAll(bool batchMode)
        {
            R9RuntimeExamplesSetup.CreateAllFromBatch();
            DungeonStageDefinition bakedStage =
                RequireCurrentBakedStage();
            string outputFolder = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(outputFolder);

            List<DungeonDistributionMetadata> metadata =
                new List<DungeonDistributionMetadata>();
            Export(
                DungeonDistributionExporter.PlanRuntimeCore(),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanRuntimeExamples(),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanLabSample(),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanBakeAuthoring(),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanBakeAuthoring(true),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanBakedStage(bakedStage),
                outputFolder,
                metadata);
            Export(
                DungeonDistributionExporter.PlanBakedStage(
                    bakedStage,
                    true),
                outputFolder,
                metadata);
            WriteIndex(outputFolder, metadata);

            Debug.Log(
                "R9 distribution packages are ready: " + outputFolder);
            if (batchMode) return;
            EditorUtility.DisplayDialog(
                "R9 배포 패키지 생성 완료",
                metadata.Count +
                "개 package와 JSON sidecar를 생성했습니다.\n\n" +
                outputFolder,
                "확인");
        }

        // R7 검증 Bake가 없거나 stale일 때만 기존 수동 setup을 실행해 기준 자산을 복구합니다.
        private static DungeonStageDefinition RequireCurrentBakedStage()
        {
            DungeonStageDefinition definition =
                AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                    BakedStagePath);
            DungeonValidationReport validation = definition != null
                ? DungeonStageBaker.ValidateCurrentBake(definition)
                : null;
            if (definition == null || validation == null || !validation.IsValid)
            {
                R7ManualVerificationSetup.CreateAllFromBatch();
                definition =
                    AssetDatabase.LoadAssetAtPath<DungeonStageDefinition>(
                        BakedStagePath);
                validation = definition != null
                    ? DungeonStageBaker.ValidateCurrentBake(definition)
                    : null;
            }
            if (definition == null || validation == null || !validation.IsValid)
            {
                throw new InvalidOperationException(
                    "R9 package verification requires a current R7 Baked Stage.");
            }
            return definition;
        }

        // 하나의 유효 계획을 package 파일로 생성하고 index용 metadata를 누적합니다.
        private static void Export(
            DungeonDistributionPlan plan,
            string outputFolder,
            List<DungeonDistributionMetadata> metadata)
        {
            if (plan == null || !plan.IsValid)
            {
                throw new DungeonDistributionException(
                    "R9 distribution plan is invalid: " +
                    (plan != null ? plan.PackageId : "<null>"),
                    plan != null
                        ? plan.ValidationReport
                        : new DungeonValidationReport());
            }
            string packagePath = Path.Combine(
                outputFolder,
                plan.PackageId + ".unitypackage");
            metadata.Add(
                DungeonDistributionExporter.Export(
                    plan,
                    packagePath));
        }

        // 생성된 package ID·SHA-256·요구 package를 사람이 읽을 수 있는 인덱스로 기록합니다.
        private static void WriteIndex(
            string outputFolder,
            List<DungeonDistributionMetadata> metadata)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Rogue Dungeon Lab R9 packages");
            builder.AppendLine();
            builder.AppendLine("Unity: `" + Application.unityVersion + "`");
            builder.AppendLine();
            for (int i = 0; i < metadata.Count; i++)
            {
                DungeonDistributionMetadata item = metadata[i];
                builder.AppendLine("## " + item.packageId);
                builder.AppendLine();
                builder.AppendLine("- 종류: `" + item.packageKind + "`");
                builder.AppendLine("- SHA-256: `" + item.packageSha256 + "`");
                builder.AppendLine("- 자산 수: " + item.assetPaths.Count);
                if (!string.IsNullOrEmpty(item.renderPipeline) &&
                    !string.Equals(
                        item.renderPipeline,
                        "NotApplicable",
                        StringComparison.Ordinal))
                {
                    builder.AppendLine(
                        "- 렌더 파이프라인: `" +
                        item.renderPipeline + "`");
                }
                if (item.requiredPackages.Count == 0)
                {
                    builder.AppendLine("- 추가 Unity package: 없음");
                }
                else
                {
                    builder.AppendLine("- 추가 Unity package:");
                    for (int packageIndex = 0;
                         packageIndex < item.requiredPackages.Count;
                         packageIndex++)
                    {
                        DungeonDistributionPackageDependency dependency =
                            item.requiredPackages[packageIndex];
                        builder.AppendLine(
                            "  - `" + dependency.packageId + "@" +
                            dependency.version + "`");
                    }
                }
                builder.AppendLine();
            }
            File.WriteAllText(
                Path.Combine(outputFolder, "PACKAGE_INDEX_KO.md"),
                builder.ToString(),
                new UTF8Encoding(false));
        }
    }
}
