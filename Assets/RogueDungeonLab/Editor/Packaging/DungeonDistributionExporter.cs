using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace RogueDungeonLab.Editor
{
    public enum DungeonDistributionPackageKind
    {
        RuntimeCore,
        RuntimeExamples,
        LabSample,
        BakeAuthoring,
        BakedStage
    }

    [Serializable]
    public sealed class DungeonDistributionPackageDependency
    {
        public string packageId = string.Empty;
        public string version = string.Empty;
    }

    [Serializable]
    public sealed class DungeonDistributionMetadata
    {
        public int formatVersion = 1;
        public string packageKind = string.Empty;
        public string packageId = string.Empty;
        public string unityVersion = string.Empty;
        public long createdUtcTicks;
        public string packageSha256 = string.Empty;
        public string stageId = string.Empty;
        public string sourceBlueprintHash = string.Empty;
        public string finalBlueprintHash = string.Empty;
        public string overrideHash = string.Empty;
        public string renderPipeline = string.Empty;
        public List<DungeonDistributionPackageDependency> requiredPackages =
            new List<DungeonDistributionPackageDependency>();
        public List<string> assetPaths = new List<string>();
    }

    public sealed class DungeonDistributionPlan
    {
        public DungeonDistributionPackageKind Kind { get; private set; }
        public string PackageId { get; private set; }
        public DungeonStageDefinition StageDefinition { get; private set; }
        public DungeonValidationReport ValidationReport { get; private set; }
        public string RenderPipeline { get; private set; }
        public IReadOnlyList<string> AssetPaths { get; private set; }
        public IReadOnlyList<DungeonDistributionPackageDependency> RequiredPackages { get; private set; }
        public bool IsValid { get { return ValidationReport != null && ValidationReport.IsValid; } }

        // 정렬된 배포 자산과 검증 결과를 변경 불가능한 계획으로 묶습니다.
        internal DungeonDistributionPlan(
            DungeonDistributionPackageKind kind,
            string packageId,
            DungeonStageDefinition stageDefinition,
            DungeonValidationReport validationReport,
            string renderPipeline,
            List<string> assetPaths,
            List<DungeonDistributionPackageDependency> requiredPackages)
        {
            Kind = kind;
            PackageId = packageId ?? string.Empty;
            StageDefinition = stageDefinition;
            ValidationReport = validationReport ?? new DungeonValidationReport();
            RenderPipeline = renderPipeline ?? string.Empty;
            AssetPaths = (assetPaths ?? new List<string>()).AsReadOnly();
            RequiredPackages =
                (requiredPackages ??
                 new List<DungeonDistributionPackageDependency>()).AsReadOnly();
        }
    }

    public static class DungeonDistributionValidationCodes
    {
        public const string NullStageDefinition = "RDL-DIST-001";
        public const string UnsupportedStage = "RDL-DIST-002";
        public const string StaleBake = "RDL-DIST-003";
        public const string MissingAsset = "RDL-DIST-004";
        public const string AssetOutsideProject = "RDL-DIST-005";
        public const string SampleDependency = "RDL-DIST-006";
        public const string EditorDependency = "RDL-DIST-007";
        public const string TestDependency = "RDL-DIST-008";
        public const string EmptyPackage = "RDL-DIST-009";
        public const string MissingOwnedArtifact = "RDL-DIST-010";
        public const string MixedRenderPipelines = "RDL-DIST-011";
    }

    public sealed class DungeonDistributionException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        // 배포 차단 사유와 코드 기반 검증 결과를 호출자에게 함께 전달합니다.
        public DungeonDistributionException(
            string message,
            DungeonValidationReport validationReport,
            Exception innerException = null)
            : base(message, innerException)
        {
            ValidationReport = validationReport ?? new DungeonValidationReport();
        }
    }

    public static class DungeonDistributionExporter
    {
        public const string RuntimeCoreFolder = "Assets/RogueDungeonLab/Runtime";
        public const string LabSampleFolder = "Assets/RogueDungeonLab/Samples";
        public const string RuntimeExamplesFolder =
            "Assets/RogueDungeonLab/Examples/RuntimeBuild";
        public const string BakeAuthoringFolder = "Assets/RogueDungeonLab/Editor/Baking";
        public const string PackagingAuthoringFolder = "Assets/RogueDungeonLab/Editor/Packaging";
        public const string InputSystemPackageId = "com.unity.inputsystem";

        // Input System이나 실험용 HUD 없이 복사 가능한 Runtime 코어 패키지 계획을 만듭니다.
        public static DungeonDistributionPlan PlanRuntimeCore()
        {
            DungeonValidationReport report = new DungeonValidationReport();
            List<string> paths = CollectFolderAssets(RuntimeCoreFolder, report);
            ValidateNonEmpty(report, paths);
            return CreatePlan(
                DungeonDistributionPackageKind.RuntimeCore,
                "rogue-dungeon-lab-runtime-core",
                null,
                report,
                paths,
                new List<DungeonDistributionPackageDependency>());
        }

        // 코어와 별도로 설치할 HUD·카메라·임시 플레이어 Input System Sample 계획을 만듭니다.
        public static DungeonDistributionPlan PlanLabSample()
        {
            DungeonValidationReport report = new DungeonValidationReport();
            List<string> paths = CollectFolderAssets(LabSampleFolder, report);
            ValidateNonEmpty(report, paths);
            List<DungeonDistributionPackageDependency> packages =
                new List<DungeonDistributionPackageDependency>();
            AddRequiredPackage(packages, InputSystemPackageId, string.Empty);
            return CreatePlan(
                DungeonDistributionPackageKind.LabSample,
                "rogue-dungeon-lab-lab-sample",
                null,
                report,
                paths,
                packages);
        }

        // Procedural·SavedBlueprint RuntimeBuild 자산과 HUD 없는 smoke scene 예제 계획을 만듭니다.
        public static DungeonDistributionPlan PlanRuntimeExamples()
        {
            DungeonValidationReport report = new DungeonValidationReport();
            List<string> paths = CollectFolderAssets(
                RuntimeExamplesFolder,
                report);
            ValidateNonEmpty(report, paths);
            return CreatePlan(
                DungeonDistributionPackageKind.RuntimeExamples,
                "rogue-dungeon-lab-runtime-examples",
                null,
                report,
                paths,
                new List<DungeonDistributionPackageDependency>());
        }

        // Baker와 배포 도구만 포함하고 실험실 UI를 제외한 제작 도구 패키지 계획을 만듭니다.
        public static DungeonDistributionPlan PlanBakeAuthoring(
            bool includeRuntimeCore = false)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            List<string> paths = CollectFolderAssets(BakeAuthoringFolder, report);
            MergeUnique(
                paths,
                CollectFolderAssets(PackagingAuthoringFolder, report));
            if (includeRuntimeCore)
            {
                MergeUnique(
                    paths,
                    CollectFolderAssets(RuntimeCoreFolder, report));
            }
            ValidateNonEmpty(report, paths);
            return CreatePlan(
                DungeonDistributionPackageKind.BakeAuthoring,
                includeRuntimeCore
                    ? "rogue-dungeon-lab-bake-authoring-standalone"
                    : "rogue-dungeon-lab-bake-authoring",
                null,
                report,
                paths,
                new List<DungeonDistributionPackageDependency>());
        }

        // 최신 BakedPrefab과 manifest, 제작 입력 및 Catalog 의존성을 Player용 묶음으로 계획합니다.
        public static DungeonDistributionPlan PlanBakedStage(
            DungeonStageDefinition definition,
            bool includeRuntimeCore = false)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (definition == null)
            {
                report.Add(
                    DungeonDistributionValidationCodes.NullStageDefinition,
                    DungeonValidationSeverity.Error,
                    "A StageDefinition is required for Baked stage export.");
                return CreatePlan(
                    DungeonDistributionPackageKind.BakedStage,
                    "rogue-dungeon-lab-baked-stage",
                    null,
                    report,
                    new List<string>(),
                    new List<DungeonDistributionPackageDependency>());
            }

            if (definition.sourceMode != DungeonStageSourceMode.SavedBlueprint ||
                definition.buildMode != DungeonStageBuildMode.BakedPrefab ||
                definition.bakedPrefab == null ||
                definition.bakeManifest == null)
            {
                report.Add(
                    DungeonDistributionValidationCodes.UnsupportedStage,
                    DungeonValidationSeverity.Error,
                    "Baked stage export requires SavedBlueprint + BakedPrefab with a manifest.");
            }

            DungeonValidationReport bakeReport =
                DungeonStageBaker.ValidateCurrentBake(definition);
            MergeReport(report, bakeReport);
            if (!bakeReport.IsValid)
            {
                report.Add(
                    DungeonDistributionValidationCodes.StaleBake,
                    DungeonValidationSeverity.Error,
                    "The Baked stage is stale or invalid and must be rebuilt before export.");
            }

            List<string> roots = CollectStageRoots(definition, report);
            List<string> allDependencies =
                CollectDependencyClosure(roots, report);
            List<DungeonDistributionPackageDependency> packages =
                CollectRequiredPackages(allDependencies);
            AddStagePresentationPackages(definition, packages);
            string renderPipeline = DetectRenderPipeline(definition);
            if (string.Equals(
                    renderPipeline,
                    "Mixed",
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonDistributionValidationCodes.MixedRenderPipelines,
                    DungeonValidationSeverity.Error,
                    "The Bake material set mixes Built-in, URP, HDRP, or custom pipeline shaders.");
            }
            List<string> paths = FilterBakedRuntimeAssets(
                allDependencies,
                includeRuntimeCore,
                report);
            AddOwnedArtifacts(definition.bakeManifest, paths, report);
            if (includeRuntimeCore)
            {
                MergeUnique(
                    paths,
                    CollectFolderAssets(RuntimeCoreFolder, report));
            }
            ValidateNonEmpty(report, paths);

            string stageId = string.IsNullOrWhiteSpace(definition.stageId)
                ? "stage"
                : SanitizeIdentifier(definition.stageId);
            return CreatePlan(
                DungeonDistributionPackageKind.BakedStage,
                "rogue-dungeon-lab-stage-" + stageId +
                (includeRuntimeCore ? "-standalone" : string.Empty),
                definition,
                report,
                paths,
                packages);
        }

        // 검증된 계획을 .unitypackage와 SHA-256이 포함된 JSON sidecar로 내보냅니다.
        public static DungeonDistributionMetadata Export(
            DungeonDistributionPlan plan,
            string outputPath)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!plan.IsValid)
            {
                throw new DungeonDistributionException(
                    "Dungeon distribution plan is invalid.",
                    plan.ValidationReport);
            }

            string normalizedOutput = NormalizeOutputPath(outputPath);
            string directory = Path.GetDirectoryName(normalizedOutput);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("An output directory is required.", nameof(outputPath));
            Directory.CreateDirectory(directory);

            try
            {
                AssetDatabase.ExportPackage(
                    ToArray(plan.AssetPaths),
                    normalizedOutput,
                    ExportPackageOptions.Default);
                if (!File.Exists(normalizedOutput))
                    throw new IOException("Unity did not create the requested package.");
                DungeonDistributionMetadata metadata =
                    CreateMetadata(plan, normalizedOutput);
                File.WriteAllText(
                    normalizedOutput + ".json",
                    JsonUtility.ToJson(metadata, true),
                    new UTF8Encoding(false));
                return metadata;
            }
            catch (DungeonDistributionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new DungeonDistributionException(
                    "Dungeon package export failed: " + exception.Message,
                    plan.ValidationReport,
                    exception);
            }
        }

        // 계획과 실제 package 파일에서 재현 가능한 인계 metadata를 생성합니다.
        private static DungeonDistributionMetadata CreateMetadata(
            DungeonDistributionPlan plan,
            string packagePath)
        {
            DungeonDistributionMetadata metadata =
                new DungeonDistributionMetadata
                {
                    packageKind = plan.Kind.ToString(),
                    packageId = plan.PackageId,
                    unityVersion = Application.unityVersion,
                    createdUtcTicks = DateTime.UtcNow.Ticks,
                    packageSha256 = ComputeFileSha256(packagePath),
                    renderPipeline = plan.RenderPipeline,
                    assetPaths = new List<string>(plan.AssetPaths)
                };
            for (int i = 0; i < plan.RequiredPackages.Count; i++)
            {
                DungeonDistributionPackageDependency dependency =
                    plan.RequiredPackages[i];
                metadata.requiredPackages.Add(
                    new DungeonDistributionPackageDependency
                    {
                        packageId = dependency.packageId,
                        version = dependency.version
                    });
            }
            if (plan.StageDefinition != null)
            {
                DungeonBakeManifest manifest =
                    plan.StageDefinition.bakeManifest;
                metadata.stageId = plan.StageDefinition.stageId ?? string.Empty;
                if (manifest != null)
                {
                    metadata.sourceBlueprintHash =
                        manifest.sourceBlueprintHash ?? string.Empty;
                    metadata.finalBlueprintHash =
                        manifest.finalBlueprintHash ?? string.Empty;
                    metadata.overrideHash =
                        manifest.overrideHash ?? string.Empty;
                }
            }
            return metadata;
        }

        // 생성된 파일을 SHA-256 소문자 16진 문자열로 계산합니다.
        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        // StageDefinition과 manifest가 직접 소유하거나 검증에 요구하는 루트 자산을 수집합니다.
        private static List<string> CollectStageRoots(
            DungeonStageDefinition definition,
            DungeonValidationReport report)
        {
            List<string> roots = new List<string>();
            AddAssetPath(roots, definition, report);
            AddAssetPath(roots, definition.savedBlueprint, report);
            AddAssetPath(roots, definition.stageOverrides, report);
            AddAssetPath(roots, definition.contentCatalog, report);
            AddAssetPath(roots, definition.bakedPrefab, report);
            AddAssetPath(roots, definition.bakeManifest, report);
            DungeonBakeManifest manifest = definition.bakeManifest;
            if (manifest != null)
            {
                AddAssetPath(roots, manifest.sourceBlueprint, report);
                AddAssetPath(roots, manifest.sourceOverrides, report);
                AddAssetPath(roots, manifest.sourceCatalog, report);
                AddAssetPath(roots, manifest.sourceRuntimeSettings, report);
                AddAssetPath(roots, manifest.materialSet, report);
                AddAssetPath(roots, manifest.bakedPrefab, report);
            }
            roots.Sort(StringComparer.Ordinal);
            return roots;
        }

        // manifest의 GUID 기반 파생 자산이 dependency closure에 빠져도 명시적으로 포함합니다.
        private static void AddOwnedArtifacts(
            DungeonBakeManifest manifest,
            List<string> paths,
            DungeonValidationReport report)
        {
            if (manifest == null || manifest.ownedArtifacts == null) return;
            for (int i = 0; i < manifest.ownedArtifacts.Count; i++)
            {
                DungeonBakeArtifactRecord record = manifest.ownedArtifacts[i];
                string path = record != null
                    ? AssetDatabase.GUIDToAssetPath(record.assetGuid)
                    : string.Empty;
                if (string.IsNullOrEmpty(path) ||
                    !File.Exists(Path.GetFullPath(path)))
                {
                    report.Add(
                        DungeonDistributionValidationCodes.MissingOwnedArtifact,
                        DungeonValidationSeverity.Error,
                        "A manifest-owned Bake artifact cannot be resolved: " +
                        (record != null ? record.role : "<null>"));
                    continue;
                }
                AddUnique(paths, NormalizeAssetPath(path));
            }
            paths.Sort(StringComparer.Ordinal);
        }

        // 루트 자산의 Unity dependency closure를 정렬된 project/package 경로로 확장합니다.
        private static List<string> CollectDependencyClosure(
            List<string> roots,
            DungeonValidationReport report)
        {
            if (roots == null || roots.Count == 0)
                return new List<string>();
            string[] dependencies =
                AssetDatabase.GetDependencies(roots.ToArray(), true);
            List<string> result = new List<string>();
            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = NormalizeAssetPath(dependencies[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonDistributionValidationCodes.AssetOutsideProject,
                        DungeonValidationSeverity.Error,
                        "A stage dependency is outside Assets or Packages: " + path);
                    continue;
                }
                AddUnique(result, path);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        // 소비 프로젝트가 이미 코어를 설치했다는 계약에 따라 제작·Sample·Test를 차단하고 Runtime을 선택 포함합니다.
        private static List<string> FilterBakedRuntimeAssets(
            List<string> dependencies,
            bool includeRuntimeCore,
            DungeonValidationReport report)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < dependencies.Count; i++)
            {
                string path = dependencies[i];
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;
                if (path.StartsWith(
                        LabSampleFolder + "/",
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonDistributionValidationCodes.SampleDependency,
                        DungeonValidationSeverity.Error,
                        "A production Baked stage depends on a Lab Sample asset: " + path);
                    continue;
                }
                if (path.StartsWith(
                        "Assets/RogueDungeonLab/Editor/",
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonDistributionValidationCodes.EditorDependency,
                        DungeonValidationSeverity.Error,
                        "A production Baked stage depends on an Editor asset: " + path);
                    continue;
                }
                if (path.StartsWith(
                        "Assets/RogueDungeonLab/Tests/",
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonDistributionValidationCodes.TestDependency,
                        DungeonValidationSeverity.Error,
                        "A production Baked stage depends on a Test asset: " + path);
                    continue;
                }
                if (!includeRuntimeCore &&
                    (string.Equals(
                         path,
                         RuntimeCoreFolder,
                         StringComparison.Ordinal) ||
                     path.StartsWith(
                         RuntimeCoreFolder + "/",
                         StringComparison.Ordinal)))
                {
                    continue;
                }
                AddUnique(result, path);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        // package 경로 dependency를 설치 가능한 Unity package ID와 현재 버전으로 축약합니다.
        private static List<DungeonDistributionPackageDependency> CollectRequiredPackages(
            List<string> dependencyPaths)
        {
            List<DungeonDistributionPackageDependency> result =
                new List<DungeonDistributionPackageDependency>();
            for (int i = 0; i < dependencyPaths.Count; i++)
            {
                string path = dependencyPaths[i];
                if (!path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;
                PackageManagerInfo package =
                    PackageManagerInfo.FindForAssetPath(path);
                if (package == null ||
                    string.IsNullOrWhiteSpace(package.name) ||
                    IsImplicitEngineModule(package.name))
                {
                    continue;
                }
                AddRequiredPackage(result, package.name, package.version);
            }
            result.Sort(
                delegate(
                    DungeonDistributionPackageDependency left,
                    DungeonDistributionPackageDependency right)
                {
                    return string.CompareOrdinal(left.packageId, right.packageId);
                });
            return result;
        }

        // Bake 재질 세트와 실제 Prefab Renderer의 Shader package를 dependency API 누락과 무관하게 기록합니다.
        private static void AddStagePresentationPackages(
            DungeonStageDefinition definition,
            List<DungeonDistributionPackageDependency> packages)
        {
            if (definition == null || packages == null) return;
            DungeonBakeManifest manifest = definition.bakeManifest;
            if (manifest != null && manifest.materialSet != null)
            {
                DungeonBakeMaterialSet set = manifest.materialSet;
                AddMaterialPackage(packages, set.floor);
                AddMaterialPackage(packages, set.wall);
                AddMaterialPackage(packages, set.enemy);
                AddMaterialPackage(packages, set.destructible);
                AddMaterialPackage(packages, set.prop);
                AddMaterialPackage(packages, set.gimmick);
                AddMaterialPackage(packages, set.entrance);
                AddMaterialPackage(packages, set.exit);
            }
            AddRendererPackages(packages, definition.bakedPrefab);
            DungeonContentCatalog catalog = manifest != null
                ? manifest.sourceCatalog
                : definition.contentCatalog;
            if (catalog == null || catalog.entries == null) return;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry != null)
                    AddRendererPackages(packages, entry.prefab);
            }
            packages.Sort(
                delegate(
                    DungeonDistributionPackageDependency left,
                    DungeonDistributionPackageDependency right)
                {
                    return string.CompareOrdinal(left.packageId, right.packageId);
                });
        }

        // Bake 재질 세트의 Shader 이름과 package 경로에서 BuiltIn·Universal·HDRP·Custom 호환성을 판정합니다.
        private static string DetectRenderPipeline(
            DungeonStageDefinition definition)
        {
            if (definition == null ||
                definition.bakeManifest == null ||
                definition.bakeManifest.materialSet == null)
            {
                return "NotApplicable";
            }
            DungeonBakeMaterialSet set =
                definition.bakeManifest.materialSet;
            HashSet<string> pipelines =
                new HashSet<string>(StringComparer.Ordinal);
            AddMaterialPipeline(pipelines, set.floor);
            AddMaterialPipeline(pipelines, set.wall);
            AddMaterialPipeline(pipelines, set.enemy);
            AddMaterialPipeline(pipelines, set.destructible);
            AddMaterialPipeline(pipelines, set.prop);
            AddMaterialPipeline(pipelines, set.gimmick);
            AddMaterialPipeline(pipelines, set.entrance);
            AddMaterialPipeline(pipelines, set.exit);
            if (pipelines.Count == 0) return "Unknown";
            if (pipelines.Count > 1) return "Mixed";
            foreach (string pipeline in pipelines) return pipeline;
            return "Unknown";
        }

        // 하나의 Material Shader를 이름과 package ID에 따라 배포 파이프라인 집합에 추가합니다.
        private static void AddMaterialPipeline(
            HashSet<string> pipelines,
            Material material)
        {
            if (pipelines == null || material == null || material.shader == null)
                return;
            string shaderName = material.shader.name ?? string.Empty;
            string shaderPath = NormalizeAssetPath(
                AssetDatabase.GetAssetPath(material.shader));
            if (shaderName.StartsWith(
                    "Universal Render Pipeline/",
                    StringComparison.Ordinal) ||
                shaderPath.StartsWith(
                    "Packages/com.unity.render-pipelines.universal/",
                    StringComparison.Ordinal))
            {
                pipelines.Add("Universal");
                return;
            }
            if (shaderName.StartsWith("HDRP/", StringComparison.Ordinal) ||
                shaderName.StartsWith(
                    "HDRenderPipeline/",
                    StringComparison.Ordinal) ||
                shaderPath.StartsWith(
                    "Packages/com.unity.render-pipelines.high-definition/",
                    StringComparison.Ordinal))
            {
                pipelines.Add("HighDefinition");
                return;
            }
            if (string.IsNullOrEmpty(shaderPath) ||
                !shaderPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                pipelines.Add(
                    shaderName.StartsWith("Custom/", StringComparison.Ordinal)
                        ? "Custom"
                        : "BuiltIn");
                return;
            }
            pipelines.Add("Custom");
        }

        // Prefab의 모든 Renderer 재질이 요구하는 외부 render-pipeline package를 기록합니다.
        private static void AddRendererPackages(
            List<DungeonDistributionPackageDependency> packages,
            GameObject root)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                if (materials == null) continue;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    AddMaterialPackage(packages, materials[materialIndex]);
                }
            }
        }

        // Material Shader가 외부 Unity package 자산이면 해당 package ID와 설치 버전을 추가합니다.
        private static void AddMaterialPackage(
            List<DungeonDistributionPackageDependency> packages,
            Material material)
        {
            if (material == null || material.shader == null) return;
            string shaderPath = NormalizeAssetPath(
                AssetDatabase.GetAssetPath(material.shader));
            if (!shaderPath.StartsWith("Packages/", StringComparison.Ordinal))
                return;
            PackageManagerInfo package =
                PackageManagerInfo.FindForAssetPath(shaderPath);
            if (package == null ||
                string.IsNullOrWhiteSpace(package.name) ||
                IsImplicitEngineModule(package.name))
            {
                return;
            }
            AddRequiredPackage(packages, package.name, package.version);
        }

        // UnityEngine에 항상 포함되는 com.unity.modules 계열만 별도 설치 요구사항에서 제외합니다.
        private static bool IsImplicitEngineModule(string packageId)
        {
            return !string.IsNullOrEmpty(packageId) &&
                   packageId.StartsWith(
                       "com.unity.modules.",
                       StringComparison.Ordinal);
        }

        // 중복 package ID를 만들지 않고 현재 프로젝트에서 확인한 버전을 기록합니다.
        private static void AddRequiredPackage(
            List<DungeonDistributionPackageDependency> packages,
            string packageId,
            string fallbackVersion)
        {
            if (packages == null || string.IsNullOrWhiteSpace(packageId)) return;
            for (int i = 0; i < packages.Count; i++)
            {
                if (string.Equals(
                        packages[i].packageId,
                        packageId,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            PackageManagerInfo installed = FindRegisteredPackage(packageId);
            packages.Add(
                new DungeonDistributionPackageDependency
                {
                    packageId = packageId,
                    version = installed != null
                        ? installed.version
                        : (fallbackVersion ?? string.Empty)
                });
        }

        // 등록된 Unity package 목록에서 정확한 package ID를 찾습니다.
        private static PackageManagerInfo FindRegisteredPackage(string packageId)
        {
            PackageManagerInfo[] packages =
                PackageManagerInfo.GetAllRegisteredPackages();
            for (int i = 0; i < packages.Length; i++)
            {
                if (string.Equals(
                        packages[i].name,
                        packageId,
                        StringComparison.Ordinal))
                {
                    return packages[i];
                }
            }
            return null;
        }

        // 지정 Assets 폴더 아래의 폴더 자체와 모든 파일 자산을 GUID 기준으로 수집합니다.
        private static List<string> CollectFolderAssets(
            string folder,
            DungeonValidationReport report)
        {
            List<string> result = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                report.Add(
                    DungeonDistributionValidationCodes.MissingAsset,
                    DungeonValidationSeverity.Error,
                    "Distribution source folder is missing: " + folder);
                return result;
            }
            AddUnique(result, folder);
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (!string.IsNullOrEmpty(path)) AddUnique(result, path);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        // Unity Object가 영속 Assets 자산이면 중복 없이 루트 목록에 추가합니다.
        private static void AddAssetPath(
            List<string> paths,
            UnityEngine.Object asset,
            DungeonValidationReport report)
        {
            if (asset == null) return;
            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                report.Add(
                    DungeonDistributionValidationCodes.MissingAsset,
                    DungeonValidationSeverity.Error,
                    "A required stage reference is not a persistent Assets object: " + asset.name);
                return;
            }
            AddUnique(paths, path);
        }

        // 유효한 배포 자산이 하나도 없으면 package 생성을 오류로 차단합니다.
        private static void ValidateNonEmpty(
            DungeonValidationReport report,
            List<string> paths)
        {
            if (paths != null && paths.Count > 0) return;
            report.Add(
                DungeonDistributionValidationCodes.EmptyPackage,
                DungeonValidationSeverity.Error,
                "The distribution package contains no assets.");
        }

        // 하위 검증 리포트의 모든 문제를 배포 리포트에 보존합니다.
        private static void MergeReport(
            DungeonValidationReport destination,
            DungeonValidationReport source)
        {
            if (destination == null || source == null || source.issues == null) return;
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }

        // 동일 경로를 한 번만 유지하며 두 자산 목록을 병합합니다.
        private static void MergeUnique(
            List<string> destination,
            List<string> source)
        {
            if (destination == null || source == null) return;
            for (int i = 0; i < source.Count; i++)
                AddUnique(destination, source[i]);
            destination.Sort(StringComparer.Ordinal);
        }

        // 경로 목록에 ordinal 기준 중복이 없을 때만 값을 추가합니다.
        private static void AddUnique(
            List<string> paths,
            string value)
        {
            if (paths == null || string.IsNullOrEmpty(value)) return;
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], value, StringComparison.Ordinal))
                    return;
            }
            paths.Add(value);
        }

        // 목록과 package dependency를 canonical 순서로 정리해 계획을 생성합니다.
        private static DungeonDistributionPlan CreatePlan(
            DungeonDistributionPackageKind kind,
            string packageId,
            DungeonStageDefinition definition,
            DungeonValidationReport report,
            List<string> paths,
            List<DungeonDistributionPackageDependency> packages)
        {
            paths.Sort(StringComparer.Ordinal);
            packages.Sort(
                delegate(
                    DungeonDistributionPackageDependency left,
                    DungeonDistributionPackageDependency right)
                {
                    return string.CompareOrdinal(left.packageId, right.packageId);
                });
            return new DungeonDistributionPlan(
                kind,
                packageId,
                definition,
                report,
                DetectRenderPipeline(definition),
                paths,
                packages);
        }

        // Unity package API가 요구하는 배열로 읽기 전용 경로 목록을 복사합니다.
        private static string[] ToArray(IReadOnlyList<string> values)
        {
            string[] result = new string[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }

        // 출력 경로를 절대 .unitypackage 파일 경로로 정규화합니다.
        private static string NormalizeOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output path is required.", nameof(outputPath));
            string path = outputPath.Trim();
            if (!path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                path += ".unitypackage";
            return Path.GetFullPath(path);
        }

        // Unity asset 경로 구분자를 운영체제와 무관한 슬래시로 통일합니다.
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        // Stage ID를 파일·package ID에 안전한 소문자 ASCII 식별자로 축약합니다.
        private static string SanitizeIdentifier(string value)
        {
            StringBuilder result = new StringBuilder();
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                bool accepted =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' || character == '_';
                result.Append(accepted ? character : '-');
            }
            string sanitized = result.ToString().Trim('-');
            return string.IsNullOrEmpty(sanitized) ? "stage" : sanitized;
        }
    }
}
