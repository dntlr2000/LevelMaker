using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Editor
{
    public static class DungeonStageBakeValidationCodes
    {
        public const string NullDefinition = "RDL-BAKER-001";
        public const string DefinitionNotPersistent = "RDL-BAKER-002";
        public const string UnsupportedStageContract = "RDL-BAKER-003";
        public const string MissingBlueprint = "RDL-BAKER-004";
        public const string BlueprintNotPersistent = "RDL-BAKER-005";
        public const string CatalogPlanningMismatch = "RDL-BAKER-006";
        public const string CatalogNotPersistent = "RDL-BAKER-007";
        public const string MissingMaterialSet = "RDL-BAKER-008";
        public const string IncompleteMaterialSet = "RDL-BAKER-009";
        public const string MaterialSetNotPersistent = "RDL-BAKER-010";
        public const string NonPersistentCatalogReference = "RDL-BAKER-011";
        public const string NonPersistentRuntimeSettings = "RDL-BAKER-012";
        public const string StaleContentRealization = "RDL-BAKER-013";
        public const string StaleGameplayBuildConfig = "RDL-BAKER-014";
        public const string StaleMaterialDependency = "RDL-BAKER-015";
        public const string InvalidOwnedArtifacts = "RDL-BAKER-016";
        public const string OwnedArtifactDependencyMismatch = "RDL-BAKER-017";
        public const string MissingPersistentGeometry = "RDL-BAKER-018";
        public const string UnexpectedGeneratedMeshOwner = "RDL-BAKER-019";
        public const string MissingBakedMetadata = "RDL-BAKER-020";
        public const string BakedMetadataMismatch = "RDL-BAKER-021";
        public const string MissingSourceCatalog = "RDL-BAKER-022";
        public const string PreviousBakeCleanupFailed = "RDL-BAKER-023";
        public const string PlayerUnsafeCatalogPrefab = "RDL-BAKER-024";
    }

    public sealed class DungeonStageBakeException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        // Bake 실패 원인과 코드 기반 검증 결과를 함께 전달합니다.
        public DungeonStageBakeException(
            string message,
            DungeonValidationReport validationReport,
            Exception innerException = null)
            : base(message, innerException)
        {
            ValidationReport = validationReport ?? new DungeonValidationReport();
        }
    }

    public sealed class DungeonStageBakeOptions
    {
        public bool SimulateFailureBeforeCommit { get; set; }
    }

    public sealed class DungeonStageBakeResult
    {
        public DungeonStageDefinition Definition { get; private set; }
        public DungeonBakeManifest Manifest { get; private set; }
        public GameObject BakedPrefab { get; private set; }
        public DungeonValidationReport ValidationReport { get; private set; }
        public string OutputFolder { get; private set; }

        // 성공한 Bake의 영속 자산과 최종 검증 결과를 불변 결과로 묶습니다.
        internal DungeonStageBakeResult(
            DungeonStageDefinition definition,
            DungeonBakeManifest manifest,
            GameObject bakedPrefab,
            DungeonValidationReport validationReport,
            string outputFolder)
        {
            Definition = definition;
            Manifest = manifest;
            BakedPrefab = bakedPrefab;
            ValidationReport = validationReport ?? new DungeonValidationReport();
            OutputFolder = outputFolder ?? string.Empty;
        }
    }

    public static class DungeonStageBaker
    {
        [Serializable]
        private sealed class AssemblyDefinitionPlatformData
        {
            public string[] includePlatforms;
        }

        private const string FloorMeshRole = "floor-mesh";
        private const string WallMeshRole = "wall-mesh";
        private const string BakedPrefabRole = "baked-prefab";

        // 저장 Blueprint를 영속 Mesh와 Prefab으로 구성한 뒤 성공 시에만 StageDefinition 참조를 교체합니다.
        public static DungeonStageBakeResult Bake(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings = null,
            DungeonStageBakeOptions options = null)
        {
            DungeonValidationReport inputValidation =
                ValidateBakeInputs(definition, materialSet, runtimeSettings);
            ThrowIfInvalid("Dungeon stage Bake inputs are invalid.", inputValidation);

            options = options ?? new DungeonStageBakeOptions();
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            string bakeRoot = GetBakeRoot(definitionPath);
            EnsureFolder(bakeRoot);

            string uniqueSuffix = Guid.NewGuid().ToString("N");
            string stagingFolder = bakeRoot + "/__Staging_" + uniqueSuffix;
            string outputFolder = bakeRoot + "/Version_" + uniqueSuffix;
            EnsureFolder(stagingFolder);

            DungeonBakeManifest oldManifest = definition.bakeManifest;
            GameObject oldPrefab = definition.bakedPrefab;
            DungeonStageBuildMode oldBuildMode = definition.buildMode;
            GameObject temporaryRoot = null;
            bool committed = false;
            bool outputFolderExists = false;

            try
            {
                DungeonBlueprint blueprint = definition.savedBlueprint.blueprint.DeepClone();
                IDungeonContentResolver resolver = definition.contentCatalog != null
                    ? new DungeonPrefabContentResolver(definition.contentCatalog)
                    : null;
                temporaryRoot = new GameObject(DungeonStageLoader.GeneratedRootName);
                temporaryRoot.SetActive(false);
                DungeonSceneBuildResult buildResult = DungeonSceneBuilder.Build(
                    temporaryRoot.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(
                        runtimeSettings,
                        resolver,
                        definition.missingContentPolicy));
                DungeonValidationReport bakedBuildValidation =
                    new DungeonValidationReport();
                Merge(bakedBuildValidation, buildResult.ValidationReport);
                Merge(
                    bakedBuildValidation,
                    DungeonContentCatalogValidator.ValidateBlueprint(
                        blueprint,
                        definition.contentCatalog,
                        definition.missingContentPolicy));
                buildResult.ValidationReport = bakedBuildValidation;

                string floorMeshPath = stagingFolder + "/Floor.asset";
                string wallMeshPath = stagingFolder + "/Walls.asset";
                PersistGeometryMeshes(
                    temporaryRoot,
                    floorMeshPath,
                    wallMeshPath,
                    materialSet);
                ApplyPersistentContentMaterials(
                    temporaryRoot,
                    blueprint,
                    definition.contentCatalog,
                    materialSet);
                SanitizeTransientDropTableReferences(temporaryRoot);

                DungeonBakedStageMetadata metadata =
                    temporaryRoot.GetComponent<DungeonBakedStageMetadata>();
                if (metadata == null)
                    metadata = temporaryRoot.AddComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.Current,
                    DungeonBakeBuilderVersions.Current,
                    blueprint.blueprintHash,
                    buildResult);

                string prefabPath = stagingFolder + "/Stage.prefab";
                bool prefabSaved;
                GameObject bakedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    temporaryRoot,
                    prefabPath,
                    out prefabSaved);
                if (!prefabSaved || bakedPrefab == null)
                    throw new InvalidOperationException("Unity could not save the baked stage Prefab.");

                AssetDatabase.SaveAssets();
                string manifestPath = stagingFolder + "/BakeManifest.asset";
                DungeonBakeManifest manifest = CreateManifest(
                    definition,
                    materialSet,
                    runtimeSettings,
                    bakedPrefab,
                    floorMeshPath,
                    wallMeshPath,
                    prefabPath,
                    manifestPath);

                string moveError = AssetDatabase.MoveAsset(stagingFolder, outputFolder);
                if (!string.IsNullOrEmpty(moveError))
                    throw new IOException("Could not commit the Bake version folder: " + moveError);
                outputFolderExists = true;
                AssetDatabase.SaveAssets();

                RefreshOwnedArtifactHashes(manifest);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssets();

                DungeonValidationReport stagedValidation = ValidateManifestAndArtifacts(
                    definition,
                    manifest,
                    bakedPrefab,
                    materialSet,
                    runtimeSettings,
                    true);
                ThrowIfInvalid("The staged Bake failed validation.", stagedValidation);

                if (options.SimulateFailureBeforeCommit)
                    throw new InvalidOperationException(
                        "Simulated DungeonStageBaker failure before StageDefinition commit.");

                CommitDefinition(definition, bakedPrefab, manifest);
                DungeonValidationReport finalValidation = ValidateCurrentBake(definition);
                ThrowIfInvalid("The committed Bake failed final validation.", finalValidation);
                committed = true;
                try
                {
                    CleanupPreviousBake(
                        bakeRoot,
                        definition,
                        oldManifest,
                        oldPrefab,
                        manifest);
                }
                catch (Exception cleanupException)
                {
                    finalValidation.Add(
                        DungeonStageBakeValidationCodes.PreviousBakeCleanupFailed,
                        DungeonValidationSeverity.Warning,
                        "The new Bake is valid, but previous derived assets could not be fully cleaned: " +
                        cleanupException.Message);
                }
                // 이전 파생 자산 정리는 되돌릴 수 없으므로 삭제된 참조를 복원할 수 있는 Undo 기록도 함께 폐기합니다.
                Undo.ClearUndo(definition);
                return new DungeonStageBakeResult(
                    definition,
                    manifest,
                    bakedPrefab,
                    finalValidation,
                    outputFolder);
            }
            catch (DungeonStageBakeException)
            {
                if (!committed)
                {
                    RestoreDefinitionReferences(
                        definition,
                        oldBuildMode,
                        oldPrefab,
                        oldManifest);
                    CleanupFailedOutput(
                        outputFolderExists ? outputFolder : stagingFolder,
                        bakeRoot);
                }
                throw;
            }
            catch (Exception exception)
            {
                if (!committed)
                {
                    RestoreDefinitionReferences(
                        definition,
                        oldBuildMode,
                        oldPrefab,
                        oldManifest);
                    CleanupFailedOutput(
                        outputFolderExists ? outputFolder : stagingFolder,
                        bakeRoot);
                }
                throw new DungeonStageBakeException(
                    "Dungeon stage Bake failed before commit.",
                    inputValidation,
                    exception);
            }
            finally
            {
                if (temporaryRoot != null) Object.DestroyImmediate(temporaryRoot);
            }
        }

        // 현재 StageDefinition의 manifest와 실제 의존성 fingerprint가 최신인지 다시 계산해 검증합니다.
        public static DungeonValidationReport ValidateCurrentBake(
            DungeonStageDefinition definition)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (definition == null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.NullDefinition,
                    DungeonValidationSeverity.Error,
                    "Stage Definition is null.");
                return report;
            }

            Merge(report, DungeonStageDefinitionValidator.Validate(definition));
            if (definition.bakeManifest == null) return report;

            Merge(
                report,
                ValidateManifestAndArtifacts(
                    definition,
                    definition.bakeManifest,
                    definition.bakedPrefab,
                    definition.bakeManifest.materialSet,
                    definition.bakeManifest.sourceRuntimeSettings,
                    false));
            return report;
        }

        // 지정 경로에 파이프라인 호환 기본 재질 8개를 sub-asset으로 가진 영속 Material Set을 생성합니다.
        public static DungeonBakeMaterialSet CreateDefaultMaterialSetAsset(string assetPath)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                !normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Bake Material Set path must be located below Assets.",
                    nameof(assetPath));
            }
            if (!normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                normalizedPath += ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(normalizedPath) != null)
                throw new IOException("An asset already exists at: " + normalizedPath);

            EnsureFolder(Path.GetDirectoryName(normalizedPath));
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            AssetDatabase.CreateAsset(materialSet, normalizedPath);
            materialSet.floor = AddMaterial(
                materialSet,
                "Floor",
                new Color(0.24f, 0.27f, 0.31f));
            materialSet.wall = AddMaterial(
                materialSet,
                "Wall",
                new Color(0.11f, 0.13f, 0.17f));
            materialSet.enemy = AddMaterial(
                materialSet,
                "Enemy",
                new Color(0.85f, 0.16f, 0.18f));
            materialSet.destructible = AddMaterial(
                materialSet,
                "Destructible",
                new Color(0.92f, 0.48f, 0.12f));
            materialSet.prop = AddMaterial(
                materialSet,
                "Prop",
                new Color(0.26f, 0.48f, 0.31f));
            materialSet.gimmick = AddMaterial(
                materialSet,
                "Gimmick",
                new Color(0.15f, 0.78f, 0.9f));
            materialSet.entrance = AddMaterial(
                materialSet,
                "Entrance",
                new Color(0.25f, 0.55f, 1f));
            materialSet.exit = AddMaterial(
                materialSet,
                "Exit",
                new Color(0.95f, 0.25f, 0.85f));

            EditorUtility.SetDirty(materialSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<DungeonBakeMaterialSet>(normalizedPath);
        }

        // Bake가 지원하는 SavedBlueprint+BakedPrefab 계약과 모든 영속 입력을 사전 검증합니다.
        private static DungeonValidationReport ValidateBakeInputs(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (definition == null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.NullDefinition,
                    DungeonValidationSeverity.Error,
                    "Stage Definition is null.");
                return report;
            }
            if (!AssetDatabase.Contains(definition))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.DefinitionNotPersistent,
                    DungeonValidationSeverity.Error,
                    "Stage Definition must be a persistent project asset.");
            }
            if (definition.sourceMode != DungeonStageSourceMode.SavedBlueprint)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.UnsupportedStageContract,
                    DungeonValidationSeverity.Error,
                    "R6 Bake requires a SavedBlueprint source. RuntimeBuild is promoted to BakedPrefab only after a successful commit.");
            }
            if (definition.savedBlueprint == null ||
                definition.savedBlueprint.blueprint == null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MissingBlueprint,
                    DungeonValidationSeverity.Error,
                    "R6 Bake requires a saved Blueprint.");
            }
            else
            {
                Merge(
                    report,
                    DungeonBlueprintValidator.Validate(
                        definition.savedBlueprint.blueprint));
                if (!AssetDatabase.Contains(definition.savedBlueprint))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.BlueprintNotPersistent,
                        DungeonValidationSeverity.Error,
                        "The source Blueprint must be a persistent project asset.");
                }
            }

            ValidateCatalogInput(definition, report);
            ValidateMaterialSetInput(materialSet, report);
            if (runtimeSettings != null && !AssetDatabase.Contains(runtimeSettings))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.NonPersistentRuntimeSettings,
                    DungeonValidationSeverity.Error,
                    "Optional Bake runtime settings must be a persistent project asset.");
            }
            if (runtimeSettings != null &&
                ((runtimeSettings.enemyDropTable != null &&
                  !AssetDatabase.Contains(runtimeSettings.enemyDropTable)) ||
                 (runtimeSettings.destructibleDropTable != null &&
                  !AssetDatabase.Contains(runtimeSettings.destructibleDropTable))))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.NonPersistentRuntimeSettings,
                    DungeonValidationSeverity.Error,
                    "Bake runtime settings Drop Tables must be persistent project assets.");
            }
            return report;
        }

        // Blueprint planning hash와 원본 Catalog 및 직접 Prefab 참조의 영속성을 검증합니다.
        private static void ValidateCatalogInput(
            DungeonStageDefinition definition,
            DungeonValidationReport report)
        {
            DungeonBlueprint blueprint =
                definition.savedBlueprint != null
                    ? definition.savedBlueprint.blueprint
                    : null;
            DungeonContentCatalog catalog = definition.contentCatalog;
            if (catalog != null)
            {
                Merge(report, DungeonContentCatalogValidator.Validate(catalog));
                if (!AssetDatabase.Contains(catalog))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.CatalogNotPersistent,
                        DungeonValidationSeverity.Error,
                        "The source Content Catalog must be a persistent project asset.");
                }
                if (blueprint != null &&
                    !string.Equals(
                        blueprint.catalogPlanningHash,
                        catalog.ComputePlanningHash(),
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.CatalogPlanningMismatch,
                        DungeonValidationSeverity.Error,
                        "The source Catalog planning hash differs from the saved Blueprint.");
                }
                ValidateCatalogReferences(catalog, report);
            }
            else if (blueprint != null &&
                     RequiresSourceCatalog(blueprint.catalogPlanningHash))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MissingSourceCatalog,
                    DungeonValidationSeverity.Error,
                    "This Blueprint was planned with a custom Catalog and requires that source asset.");
            }

            if (blueprint != null)
            {
                Merge(
                    report,
                    DungeonContentCatalogValidator.ValidateBlueprint(
                        blueprint,
                        catalog,
                        definition.missingContentPolicy));
            }
        }

        // Catalog의 직접 Prefab과 Drop Table 참조가 Prefab에 저장 가능한 project asset인지 확인합니다.
        private static void ValidateCatalogReferences(
            DungeonContentCatalog catalog,
            DungeonValidationReport report)
        {
            if (catalog == null || catalog.entries == null) return;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry == null) continue;
                if ((entry.prefab != null && !AssetDatabase.Contains(entry.prefab)) ||
                    (entry.dropTable != null && !AssetDatabase.Contains(entry.dropTable)))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.NonPersistentCatalogReference,
                        DungeonValidationSeverity.Error,
                        "Catalog Prefab and Drop Table references must be persistent assets: " +
                        entry.contentKey);
                }
                if (entry.prefab != null)
                    ValidateCatalogPrefabForPlayer(entry, report);
            }
        }

        // 직접 Catalog Prefab의 missing script와 Editor-only MonoBehaviour를 Player Bake 전에 차단합니다.
        private static void ValidateCatalogPrefabForPlayer(
            DungeonContentCatalogEntry entry,
            DungeonValidationReport report)
        {
            HashSet<string> editorAssemblyNames =
                GetEditorAssemblyNames();
            Transform[] transforms =
                entry.prefab.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0;
                 transformIndex < transforms.Length;
                 transformIndex++)
            {
                GameObject current = transforms[transformIndex].gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        current) > 0)
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.PlayerUnsafeCatalogPrefab,
                        DungeonValidationSeverity.Error,
                        "Catalog Prefab contains a missing script and cannot be included in a Player Bake: " +
                        entry.contentKey);
                    return;
                }

                MonoBehaviour[] behaviours =
                    current.GetComponents<MonoBehaviour>();
                for (int behaviourIndex = 0;
                     behaviourIndex < behaviours.Length;
                     behaviourIndex++)
                {
                    MonoBehaviour behaviour = behaviours[behaviourIndex];
                    if (behaviour == null) continue;
                    string assemblyName =
                        behaviour.GetType().Assembly.GetName().Name;
                    if (!IsEditorOnlyAssembly(
                            assemblyName,
                            editorAssemblyNames))
                    {
                        continue;
                    }
                    report.Add(
                        DungeonStageBakeValidationCodes.PlayerUnsafeCatalogPrefab,
                        DungeonValidationSeverity.Error,
                        "Catalog Prefab contains an Editor-only component and cannot be included in a Player Bake: " +
                        entry.contentKey +
                        " (" +
                        behaviour.GetType().FullName +
                        ")");
                    return;
                }
            }
        }

        // 현재 Editor compilation graph에서 Player graph에도 존재하는 assembly를 제외해 Editor-only 이름만 수집합니다.
        private static HashSet<string> GetEditorAssemblyNames()
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.Ordinal);
            UnityEditor.Compilation.Assembly[] editorAssemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            for (int i = 0; i < editorAssemblies.Length; i++)
            {
                if (editorAssemblies[i] != null &&
                    !string.IsNullOrWhiteSpace(editorAssemblies[i].name))
                {
                    result.Add(editorAssemblies[i].name);
                }
            }
            UnityEditor.Compilation.Assembly[] playerAssemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.Player);
            for (int i = 0; i < playerAssemblies.Length; i++)
            {
                if (playerAssemblies[i] != null)
                    result.Remove(playerAssemblies[i].name);
            }
            return result;
        }

        // compilation 목록과 asmdef includePlatforms를 함께 사용해 Editor 전용 assembly를 판정합니다.
        private static bool IsEditorOnlyAssembly(
            string assemblyName,
            HashSet<string> knownEditorOnlyAssemblies)
        {
            if (knownEditorOnlyAssemblies != null &&
                knownEditorOnlyAssemblies.Contains(assemblyName))
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;
            if (assemblyName.EndsWith(
                    ".Editor",
                    StringComparison.OrdinalIgnoreCase) ||
                assemblyName.EndsWith(
                    "-Editor",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string assemblyDefinitionPath =
                CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(
                    assemblyName);
            if (string.IsNullOrEmpty(assemblyDefinitionPath) ||
                !File.Exists(assemblyDefinitionPath))
            {
                return false;
            }
            AssemblyDefinitionPlatformData data =
                JsonUtility.FromJson<AssemblyDefinitionPlatformData>(
                    File.ReadAllText(assemblyDefinitionPath));
            if (data == null ||
                data.includePlatforms == null ||
                data.includePlatforms.Length == 0)
            {
                return false;
            }
            for (int i = 0; i < data.includePlatforms.Length; i++)
            {
                if (!string.Equals(
                        data.includePlatforms[i],
                        "Editor",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        // Material Set의 8개 필수 슬롯과 모든 project asset 참조를 확인합니다.
        private static void ValidateMaterialSetInput(
            DungeonBakeMaterialSet materialSet,
            DungeonValidationReport report)
        {
            if (materialSet == null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MissingMaterialSet,
                    DungeonValidationSeverity.Error,
                    "A persistent Bake Material Set is required.");
                return;
            }
            if (!HasCompleteMaterialSet(materialSet))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.IncompleteMaterialSet,
                    DungeonValidationSeverity.Error,
                    "Bake Material Set must assign all 8 material slots.");
            }
            if (!AssetDatabase.Contains(materialSet) ||
                !AreMaterialsPersistent(materialSet))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MaterialSetNotPersistent,
                    DungeonValidationSeverity.Error,
                    "Bake Material Set and all assigned materials must be persistent project assets.");
            }
        }

        // geometry의 transient Mesh를 복제 자산으로 교체하고 runtime owner를 제거합니다.
        private static void PersistGeometryMeshes(
            GameObject root,
            string floorMeshPath,
            string wallMeshPath,
            DungeonBakeMaterialSet materialSet)
        {
            Transform floorTransform = root.transform.Find("Geometry/Floor");
            Transform wallTransform = root.transform.Find("Geometry/Walls");
            if (floorTransform == null || wallTransform == null)
                throw new InvalidOperationException("Generated geometry hierarchy is incomplete.");

            MeshFilter floorFilter = floorTransform.GetComponent<MeshFilter>();
            MeshFilter wallFilter = wallTransform.GetComponent<MeshFilter>();
            if (floorFilter == null || floorFilter.sharedMesh == null ||
                wallFilter == null || wallFilter.sharedMesh == null)
            {
                throw new InvalidOperationException("Generated floor or wall Mesh is missing.");
            }

            Mesh floorMesh = Object.Instantiate(floorFilter.sharedMesh);
            floorMesh.name = "Baked Dungeon Floor";
            Mesh wallMesh = Object.Instantiate(wallFilter.sharedMesh);
            wallMesh.name = "Baked Dungeon Walls";
            AssetDatabase.CreateAsset(floorMesh, floorMeshPath);
            AssetDatabase.CreateAsset(wallMesh, wallMeshPath);

            ReplaceMesh(floorTransform.gameObject, floorMesh, materialSet.floor);
            ReplaceMesh(wallTransform.gameObject, wallMesh, materialSet.wall);
            DungeonGeneratedMeshOwner owner =
                root.GetComponentInChildren<DungeonGeneratedMeshOwner>(true);
            if (owner != null)
            {
                owner.ReleaseOwnedMeshes();
                Object.DestroyImmediate(owner);
            }
        }

        // MeshFilter와 MeshCollider를 같은 영속 Mesh로 맞추고 지정 재질을 적용합니다.
        private static void ReplaceMesh(
            GameObject instance,
            Mesh mesh,
            Material material)
        {
            MeshFilter filter = instance.GetComponent<MeshFilter>();
            MeshCollider collider = instance.GetComponent<MeshCollider>();
            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            if (collider != null) collider.sharedMesh = mesh;
            if (renderer != null) renderer.sharedMaterial = material;
        }

        // Catalog 직접 Prefab은 원본 재질을 보존하고 built-in 및 generic fallback만 영속 재질로 치환합니다.
        private static void ApplyPersistentContentMaterials(
            GameObject root,
            DungeonBlueprint blueprint,
            DungeonContentCatalog catalog,
            DungeonBakeMaterialSet materialSet)
        {
            HashSet<string> directPrefabKeys = BuildDirectPrefabKeySet(catalog);
            Dictionary<string, DungeonSpawnRecord> records = BuildSpawnLookup(blueprint);
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity = identities[i];
                DungeonSpawnRecord record;
                if (identity == null ||
                    !records.TryGetValue(identity.SpawnId, out record) ||
                    directPrefabKeys.Contains(record.contentKey))
                {
                    continue;
                }

                Material material = ResolveMaterial(record, materialSet);
                Renderer[] renderers = identity.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    renderers[rendererIndex].sharedMaterial = material;
                }
            }
        }

        // runtime-only 기본 Drop Table 참조는 null로 정리해 Player에서 동일한 runtime fallback을 사용하게 합니다.
        private static void SanitizeTransientDropTableReferences(GameObject root)
        {
            DestructibleDropTarget[] targets =
                root.GetComponentsInChildren<DestructibleDropTarget>(true);
            for (int i = 0; i < targets.Length; i++)
            {
                SerializedObject serializedTarget = new SerializedObject(targets[i]);
                SerializedProperty dropTable =
                    serializedTarget.FindProperty("dropTable");
                if (dropTable == null ||
                    dropTable.objectReferenceValue == null ||
                    AssetDatabase.Contains(dropTable.objectReferenceValue))
                {
                    continue;
                }
                dropTable.objectReferenceValue = null;
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 생성 결과와 모든 source fingerprint를 기록한 manifest 자산을 staging 폴더에 만듭니다.
        private static DungeonBakeManifest CreateManifest(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings,
            GameObject bakedPrefab,
            string floorMeshPath,
            string wallMeshPath,
            string prefabPath,
            string manifestPath)
        {
            DungeonBakeFingerprints fingerprints =
                DungeonBakeFingerprintUtility.Compute(
                    definition.savedBlueprint,
                    definition.contentCatalog,
                    materialSet,
                    runtimeSettings,
                    definition.missingContentPolicy);
            DungeonBakeManifest manifest =
                ScriptableObject.CreateInstance<DungeonBakeManifest>();
            manifest.formatVersion = DungeonBakeFormat.Current;
            manifest.builderVersion = DungeonBakeBuilderVersions.Current;
            manifest.sourceBlueprint = definition.savedBlueprint;
            manifest.sourceCatalog = definition.contentCatalog;
            manifest.sourceRuntimeSettings = runtimeSettings;
            manifest.materialSet = materialSet;
            manifest.bakedPrefab = bakedPrefab;
            manifest.sourceBlueprintHash = fingerprints.SourceBlueprintHash;
            manifest.finalBlueprintHash = fingerprints.FinalBlueprintHash;
            manifest.catalogPlanningHash = fingerprints.CatalogPlanningHash;
            manifest.contentRealizationHash = fingerprints.ContentRealizationHash;
            manifest.gameplayBuildConfigHash = fingerprints.GameplayBuildConfigHash;
            manifest.materialDependencyHash = fingerprints.MaterialDependencyHash;
            manifest.overrideHash = string.Empty;
            AssetDatabase.CreateAsset(manifest, manifestPath);
            AssetDatabase.SaveAssets();

            manifest.ownedArtifacts = new List<DungeonBakeArtifactRecord>
            {
                CreateArtifactRecord(FloorMeshRole, floorMeshPath),
                CreateArtifactRecord(WallMeshRole, wallMeshPath),
                CreateArtifactRecord(BakedPrefabRole, prefabPath)
            };
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            return manifest;
        }

        // role, GUID, 현재 dependency hash로 manifest 소유 자산 레코드를 만듭니다.
        private static DungeonBakeArtifactRecord CreateArtifactRecord(
            string role,
            string assetPath)
        {
            return new DungeonBakeArtifactRecord
            {
                role = role,
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                dependencyHash =
                    AssetDatabase.GetAssetDependencyHash(assetPath).ToString()
            };
        }

        // 폴더 이동 뒤 실제 경로에서 owned artifact dependency hash를 다시 고정합니다.
        private static void RefreshOwnedArtifactHashes(DungeonBakeManifest manifest)
        {
            if (manifest == null || manifest.ownedArtifacts == null) return;
            for (int i = 0; i < manifest.ownedArtifacts.Count; i++)
            {
                DungeonBakeArtifactRecord record = manifest.ownedArtifacts[i];
                if (record == null) continue;
                string path = AssetDatabase.GUIDToAssetPath(record.assetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    record.dependencyHash =
                        AssetDatabase.GetAssetDependencyHash(path).ToString();
                }
            }
        }

        // manifest, fingerprint, persistent geometry, metadata, 소유 자산 목록을 한 번에 검증합니다.
        private static DungeonValidationReport ValidateManifestAndArtifacts(
            DungeonStageDefinition definition,
            DungeonBakeManifest manifest,
            GameObject bakedPrefab,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings,
            bool includeRuntimeContractValidation)
        {
            DungeonValidationReport report = includeRuntimeContractValidation
                ? DungeonBakeManifestValidator.Validate(
                    manifest,
                    definition != null ? definition.savedBlueprint : null,
                    bakedPrefab)
                : new DungeonValidationReport();
            if (manifest == null || definition == null) return report;

            DungeonBakeFingerprints current = DungeonBakeFingerprintUtility.Compute(
                definition.savedBlueprint,
                definition.contentCatalog,
                materialSet,
                runtimeSettings,
                definition.missingContentPolicy);
            if (!string.Equals(
                    manifest.contentRealizationHash,
                    current.ContentRealizationHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.StaleContentRealization,
                    DungeonValidationSeverity.Error,
                    "Baked content realization dependencies are stale.");
            }
            if (!string.Equals(
                    manifest.gameplayBuildConfigHash,
                    current.GameplayBuildConfigHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.StaleGameplayBuildConfig,
                    DungeonValidationSeverity.Error,
                    "Baked gameplay configuration dependencies are stale.");
            }
            if (!string.Equals(
                    manifest.materialDependencyHash,
                    current.MaterialDependencyHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.StaleMaterialDependency,
                    DungeonValidationSeverity.Error,
                    "Baked material dependencies are stale.");
            }

            ValidateOwnedArtifacts(report, manifest, bakedPrefab);
            ValidateBakedPrefab(
                report,
                manifest,
                bakedPrefab,
                includeRuntimeContractValidation);
            return report;
        }

        // manifest가 floor, wall, Prefab 세 자산만 정확히 소유하고 현재 dependency hash와 일치하는지 확인합니다.
        private static void ValidateOwnedArtifacts(
            DungeonValidationReport report,
            DungeonBakeManifest manifest,
            GameObject bakedPrefab)
        {
            Dictionary<string, Object> expected = GetExpectedOwnedArtifacts(bakedPrefab);
            if (manifest.ownedArtifacts == null ||
                manifest.ownedArtifacts.Count != expected.Count)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.InvalidOwnedArtifacts,
                    DungeonValidationSeverity.Error,
                    "Bake manifest must own exactly floor Mesh, wall Mesh, and baked Prefab.");
                return;
            }

            HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.ownedArtifacts.Count; i++)
            {
                DungeonBakeArtifactRecord record = manifest.ownedArtifacts[i];
                Object expectedAsset;
                if (record == null ||
                    !roles.Add(record.role ?? string.Empty) ||
                    !expected.TryGetValue(record.role ?? string.Empty, out expectedAsset))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.InvalidOwnedArtifacts,
                        DungeonValidationSeverity.Error,
                        "Bake manifest contains an unexpected or duplicate owned artifact role.");
                    continue;
                }

                string expectedGuid;
                long expectedLocalId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        expectedAsset,
                        out expectedGuid,
                        out expectedLocalId) ||
                    !string.Equals(
                        record.assetGuid,
                        expectedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.InvalidOwnedArtifacts,
                        DungeonValidationSeverity.Error,
                        "Owned artifact GUID does not match the baked Prefab dependency: " +
                        record.role);
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(record.assetGuid);
                string actualDependencyHash =
                    string.IsNullOrEmpty(path)
                        ? string.Empty
                        : AssetDatabase.GetAssetDependencyHash(path).ToString();
                if (!string.Equals(
                        record.dependencyHash,
                        actualDependencyHash,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonStageBakeValidationCodes.OwnedArtifactDependencyMismatch,
                        DungeonValidationSeverity.Error,
                        "Owned artifact dependency hash is stale: " + record.role);
                }
            }
        }

        // baked Prefab이 영속 floor/wall Mesh와 metadata를 갖고 transient owner를 포함하지 않는지 확인합니다.
        private static void ValidateBakedPrefab(
            DungeonValidationReport report,
            DungeonBakeManifest manifest,
            GameObject bakedPrefab,
            bool includeMetadataContractValidation)
        {
            if (bakedPrefab == null) return;
            Dictionary<string, Object> expected = GetExpectedOwnedArtifacts(bakedPrefab);
            Object floor;
            Object walls;
            if (!expected.TryGetValue(FloorMeshRole, out floor) ||
                !expected.TryGetValue(WallMeshRole, out walls) ||
                floor == null ||
                walls == null ||
                !AssetDatabase.Contains(floor) ||
                !AssetDatabase.Contains(walls))
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MissingPersistentGeometry,
                    DungeonValidationSeverity.Error,
                    "Baked Prefab floor and wall Meshes must be persistent assets.");
            }
            if (bakedPrefab.GetComponentInChildren<DungeonGeneratedMeshOwner>(true) != null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.UnexpectedGeneratedMeshOwner,
                    DungeonValidationSeverity.Error,
                    "Baked Prefab must not retain a transient generated Mesh owner.");
            }

            DungeonBakedStageMetadata metadata =
                bakedPrefab.GetComponent<DungeonBakedStageMetadata>();
            if (!includeMetadataContractValidation)
            {
                return;
            }
            if (metadata == null)
            {
                report.Add(
                    DungeonStageBakeValidationCodes.MissingBakedMetadata,
                    DungeonValidationSeverity.Error,
                    "Baked Prefab is missing DungeonBakedStageMetadata.");
            }
            else
            {
                Merge(report, metadata.Validate(manifest));
            }
        }

        // Prefab hierarchy에서 manifest가 소유해야 할 정확한 3개 자산 참조를 찾습니다.
        private static Dictionary<string, Object> GetExpectedOwnedArtifacts(
            GameObject bakedPrefab)
        {
            Dictionary<string, Object> expected =
                new Dictionary<string, Object>(StringComparer.Ordinal);
            if (bakedPrefab == null) return expected;

            Transform floor = bakedPrefab.transform.Find("Geometry/Floor");
            Transform walls = bakedPrefab.transform.Find("Geometry/Walls");
            MeshFilter floorFilter =
                floor != null ? floor.GetComponent<MeshFilter>() : null;
            MeshFilter wallFilter =
                walls != null ? walls.GetComponent<MeshFilter>() : null;
            if (floorFilter != null && floorFilter.sharedMesh != null)
                expected[FloorMeshRole] = floorFilter.sharedMesh;
            if (wallFilter != null && wallFilter.sharedMesh != null)
                expected[WallMeshRole] = wallFilter.sharedMesh;
            expected[BakedPrefabRole] = bakedPrefab;
            return expected;
        }

        // StageDefinition의 두 Bake 참조를 하나의 SerializedObject 적용으로 원자 교체합니다.
        private static void CommitDefinition(
            DungeonStageDefinition definition,
            GameObject bakedPrefab,
            DungeonBakeManifest manifest)
        {
            Undo.RecordObject(definition, "던전 스테이지 Bake 교체");
            SerializedObject serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            serializedDefinition.FindProperty("buildMode").enumValueIndex =
                (int)DungeonStageBuildMode.BakedPrefab;
            serializedDefinition.FindProperty("bakedPrefab").objectReferenceValue =
                bakedPrefab;
            serializedDefinition.FindProperty("bakeManifest").objectReferenceValue =
                manifest;
            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }

        // commit 이전 예외가 발생했을 때 기존 Prefab과 manifest 참조를 즉시 복구합니다.
        private static void RestoreDefinitionReferences(
            DungeonStageDefinition definition,
            DungeonStageBuildMode oldBuildMode,
            GameObject oldPrefab,
            DungeonBakeManifest oldManifest)
        {
            if (definition == null ||
                (definition.buildMode == oldBuildMode &&
                 definition.bakedPrefab == oldPrefab &&
                 definition.bakeManifest == oldManifest))
            {
                return;
            }
            SerializedObject serializedDefinition = new SerializedObject(definition);
            serializedDefinition.Update();
            serializedDefinition.FindProperty("buildMode").enumValueIndex =
                (int)oldBuildMode;
            serializedDefinition.FindProperty("bakedPrefab").objectReferenceValue =
                oldPrefab;
            serializedDefinition.FindProperty("bakeManifest").objectReferenceValue =
                oldManifest;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }

        // 성공한 교체 뒤 이전 manifest가 명시한 stage-local 파생 자산과 이전 manifest만 정리합니다.
        private static void CleanupPreviousBake(
            string bakeRoot,
            DungeonStageDefinition definition,
            DungeonBakeManifest oldManifest,
            GameObject oldPrefab,
            DungeonBakeManifest currentManifest)
        {
            if (oldManifest == null || oldManifest == currentManifest) return;
            string oldManifestPath = AssetDatabase.GetAssetPath(oldManifest);
            string oldVersionFolder = NormalizeAssetPath(
                Path.GetDirectoryName(oldManifestPath));
            if (!IsVersionFolderInsideBakeRoot(oldVersionFolder, bakeRoot) ||
                !string.Equals(
                    NormalizeAssetPath(oldManifestPath),
                    oldVersionFolder + "/BakeManifest.asset",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HashSet<string> protectedGuids = CollectProtectedAssetGuids(
                definition,
                currentManifest);
            AddManifestSourceGuids(protectedGuids, oldManifest);
            Dictionary<string, Object> expectedOldArtifacts =
                GetExpectedOwnedArtifacts(oldPrefab);
            if (oldManifest.ownedArtifacts != null)
            {
                for (int i = 0; i < oldManifest.ownedArtifacts.Count; i++)
                {
                    DungeonBakeArtifactRecord artifact =
                        oldManifest.ownedArtifacts[i];
                    Object expectedAsset;
                    if (artifact == null ||
                        !expectedOldArtifacts.TryGetValue(
                            artifact.role ?? string.Empty,
                            out expectedAsset) ||
                        expectedAsset == null ||
                        string.IsNullOrWhiteSpace(artifact.assetGuid) ||
                        protectedGuids.Contains(artifact.assetGuid))
                    {
                        continue;
                    }
                    string expectedGuid;
                    long expectedLocalId;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            expectedAsset,
                            out expectedGuid,
                            out expectedLocalId) ||
                        !string.Equals(
                            artifact.assetGuid,
                            expectedGuid,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string path = NormalizeAssetPath(
                        AssetDatabase.GUIDToAssetPath(artifact.assetGuid));
                    string expectedPath = GetExpectedOwnedArtifactPath(
                        oldVersionFolder,
                        artifact.role);
                    if (!string.Equals(
                            path,
                            expectedPath,
                            StringComparison.OrdinalIgnoreCase) ||
                        !AssetDatabase.IsMainAsset(expectedAsset) ||
                        AssetDatabase.LoadMainAssetAtPath(path) != expectedAsset)
                    {
                        continue;
                    }
                    AssetDatabase.DeleteAsset(path);
                }
            }

            if (IsPathInsideRoot(oldManifestPath, oldVersionFolder))
            {
                string oldManifestGuid =
                    AssetDatabase.AssetPathToGUID(oldManifestPath);
                if (!protectedGuids.Contains(oldManifestGuid))
                    AssetDatabase.DeleteAsset(oldManifestPath);
            }
            AssetDatabase.SaveAssets();
        }

        // Baker가 예약한 역할을 이전 version 폴더의 고정 파일명으로만 해석합니다.
        private static string GetExpectedOwnedArtifactPath(
            string versionFolder,
            string role)
        {
            if (string.Equals(role, FloorMeshRole, StringComparison.Ordinal))
                return versionFolder + "/Floor.asset";
            if (string.Equals(role, WallMeshRole, StringComparison.Ordinal))
                return versionFolder + "/Walls.asset";
            if (string.Equals(role, BakedPrefabRole, StringComparison.Ordinal))
                return versionFolder + "/Stage.prefab";
            return string.Empty;
        }

        // 실패한 이번 호출의 고유 staging 또는 version 폴더만 bake root 범위 안에서 제거합니다.
        private static void CleanupFailedOutput(
            string candidateFolder,
            string bakeRoot)
        {
            if (!IsPathInsideRoot(candidateFolder, bakeRoot)) return;
            if (AssetDatabase.IsValidFolder(candidateFolder))
                AssetDatabase.DeleteAsset(candidateFolder);
            AssetDatabase.SaveAssets();
        }

        // Blueprint, Catalog, Catalog 의존성, settings, Material Set, 현재 산출물을 cleanup 보호 GUID로 수집합니다.
        private static HashSet<string> CollectProtectedAssetGuids(
            DungeonStageDefinition definition,
            DungeonBakeManifest currentManifest)
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddAssetGuid(result, definition);
            AddAssetGuid(result, definition != null ? definition.savedBlueprint : null);
            AddAssetGuid(result, definition != null ? definition.recipe : null);
            AddAssetGuid(result, definition != null ? definition.contentCatalog : null);
            if (definition != null && definition.contentCatalog != null &&
                definition.contentCatalog.entries != null)
            {
                for (int i = 0; i < definition.contentCatalog.entries.Count; i++)
                {
                    DungeonContentCatalogEntry entry =
                        definition.contentCatalog.entries[i];
                    if (entry == null) continue;
                    AddAssetGuid(result, entry.prefab);
                    AddAssetGuid(result, entry.dropTable);
                }
            }
            if (currentManifest != null)
            {
                AddAssetGuid(result, currentManifest);
                AddAssetGuid(result, currentManifest.bakedPrefab);
                AddAssetGuid(result, currentManifest.materialSet);
                AddAssetGuid(result, currentManifest.sourceRuntimeSettings);
                AddMaterialSetGuids(result, currentManifest.materialSet);
                if (currentManifest.ownedArtifacts != null)
                {
                    for (int i = 0; i < currentManifest.ownedArtifacts.Count; i++)
                    {
                        DungeonBakeArtifactRecord artifact =
                            currentManifest.ownedArtifacts[i];
                        if (artifact != null &&
                            !string.IsNullOrWhiteSpace(artifact.assetGuid))
                        {
                            result.Add(artifact.assetGuid);
                        }
                    }
                }
            }
            return result;
        }

        // 이전 manifest가 참조하던 원본 Blueprint/Catalog/settings/material/shared content를 cleanup 보호 집합에 추가합니다.
        private static void AddManifestSourceGuids(
            HashSet<string> guids,
            DungeonBakeManifest manifest)
        {
            if (manifest == null) return;
            AddAssetGuid(guids, manifest.sourceBlueprint);
            AddAssetGuid(guids, manifest.sourceCatalog);
            AddAssetGuid(guids, manifest.sourceRuntimeSettings);
            AddAssetGuid(guids, manifest.materialSet);
            AddMaterialSetGuids(guids, manifest.materialSet);
            DungeonContentCatalog catalog = manifest.sourceCatalog;
            if (catalog == null || catalog.entries == null) return;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry == null) continue;
                AddAssetGuid(guids, entry.prefab);
                AddAssetGuid(guids, entry.dropTable);
            }
        }

        // 하나의 project asset GUID를 cleanup 보호 집합에 추가합니다.
        private static void AddAssetGuid(HashSet<string> guids, Object asset)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) guids.Add(guid);
        }

        // Material Set과 8개 재질이 위치한 모든 GUID를 cleanup 보호 집합에 추가합니다.
        private static void AddMaterialSetGuids(
            HashSet<string> guids,
            DungeonBakeMaterialSet materialSet)
        {
            if (materialSet == null) return;
            AddAssetGuid(guids, materialSet);
            AddAssetGuid(guids, materialSet.floor);
            AddAssetGuid(guids, materialSet.wall);
            AddAssetGuid(guids, materialSet.enemy);
            AddAssetGuid(guids, materialSet.destructible);
            AddAssetGuid(guids, materialSet.prop);
            AddAssetGuid(guids, materialSet.gimmick);
            AddAssetGuid(guids, materialSet.entrance);
            AddAssetGuid(guids, materialSet.exit);
        }

        // 새로 만든 default Material을 Material Set 자산의 sub-asset으로 추가합니다.
        private static Material AddMaterial(
            DungeonBakeMaterialSet owner,
            string name,
            Color color)
        {
            Material material = new Material(ResolveShader())
            {
                name = "Bake " + name
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.AddObjectToAsset(material, owner);
            return material;
        }

        // 현재 Render Pipeline에 맞는 Lit shader를 찾고 built-in fallback 순서로 반환합니다.
        private static Shader ResolveShader()
        {
            UnityEngine.Rendering.RenderPipelineAsset pipeline =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;
                if (pipelineName.Contains("Universal"))
                {
                    Shader universal =
                        Shader.Find("Universal Render Pipeline/Lit");
                    if (universal != null) return universal;
                }
                if (pipelineName.Contains("HDRender") ||
                    pipelineName.Contains("HighDefinition"))
                {
                    Shader highDefinition = Shader.Find("HDRP/Lit");
                    if (highDefinition != null) return highDefinition;
                }
            }
            Shader standard = Shader.Find("Standard");
            if (standard != null) return standard;
            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit != null) return unlit;
            return Shader.Find("Hidden/InternalErrorShader");
        }

        // Catalog에서 직접 Prefab으로 실체화되는 contentKey 집합을 만듭니다.
        private static HashSet<string> BuildDirectPrefabKeySet(
            DungeonContentCatalog catalog)
        {
            HashSet<string> keys =
                new HashSet<string>(StringComparer.Ordinal);
            if (catalog == null || catalog.entries == null) return keys;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry != null &&
                    entry.prefab != null &&
                    !string.IsNullOrWhiteSpace(entry.contentKey))
                {
                    keys.Add(entry.contentKey);
                }
            }
            return keys;
        }

        // Blueprint spawnId에서 원본 레코드로 가는 ordinal lookup을 만듭니다.
        private static Dictionary<string, DungeonSpawnRecord> BuildSpawnLookup(
            DungeonBlueprint blueprint)
        {
            Dictionary<string, DungeonSpawnRecord> result =
                new Dictionary<string, DungeonSpawnRecord>(StringComparer.Ordinal);
            if (blueprint == null || blueprint.spawns == null) return result;
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord record = blueprint.spawns[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.spawnId))
                    result[record.spawnId] = record;
            }
            return result;
        }

        // built-in/fallback spawn category와 marker key에 해당하는 영속 재질을 선택합니다.
        private static Material ResolveMaterial(
            DungeonSpawnRecord record,
            DungeonBakeMaterialSet materialSet)
        {
            if (record.category == DungeonSpawnCategory.Marker)
            {
                if (record.contentKey == DungeonBuiltInContentKeys.EntranceMarker)
                    return materialSet.entrance;
                if (record.contentKey == DungeonBuiltInContentKeys.ExitMarker)
                    return materialSet.exit;
                return materialSet.gimmick;
            }
            if (record.category == DungeonSpawnCategory.Enemy)
                return materialSet.enemy;
            if (record.category == DungeonSpawnCategory.Destructible)
                return materialSet.destructible;
            if (record.category == DungeonSpawnCategory.Prop)
                return materialSet.prop;
            return materialSet.gimmick;
        }

        // Material Set의 모든 필수 슬롯이 지정되었는지 확인합니다.
        private static bool HasCompleteMaterialSet(
            DungeonBakeMaterialSet materialSet)
        {
            return materialSet != null &&
                   materialSet.floor != null &&
                   materialSet.wall != null &&
                   materialSet.enemy != null &&
                   materialSet.destructible != null &&
                   materialSet.prop != null &&
                   materialSet.gimmick != null &&
                   materialSet.entrance != null &&
                   materialSet.exit != null;
        }

        // Material Set의 모든 재질이 project asset인지 확인합니다.
        private static bool AreMaterialsPersistent(
            DungeonBakeMaterialSet materialSet)
        {
            return HasCompleteMaterialSet(materialSet) &&
                   AssetDatabase.Contains(materialSet.floor) &&
                   AssetDatabase.Contains(materialSet.wall) &&
                   AssetDatabase.Contains(materialSet.enemy) &&
                   AssetDatabase.Contains(materialSet.destructible) &&
                   AssetDatabase.Contains(materialSet.prop) &&
                   AssetDatabase.Contains(materialSet.gimmick) &&
                   AssetDatabase.Contains(materialSet.entrance) &&
                   AssetDatabase.Contains(materialSet.exit);
        }

        // built-in planning hash가 아닌 Blueprint가 원본 Catalog를 요구하는지 판정합니다.
        private static bool RequiresSourceCatalog(string catalogPlanningHash)
        {
            return !string.Equals(
                       catalogPlanningHash,
                       DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                       StringComparison.Ordinal) &&
                   !string.Equals(
                       catalogPlanningHash,
                       DungeonBuiltInContentKeys.StableCatalogPlanningHash,
                       StringComparison.Ordinal);
        }

        // 정의 자산 옆에 stage별 Bake root 경로를 계산합니다.
        private static string GetBakeRoot(string definitionPath)
        {
            string directory = NormalizeAssetPath(
                Path.GetDirectoryName(definitionPath));
            string fileName = Path.GetFileNameWithoutExtension(definitionPath);
            return directory + "/" + fileName + "_Bake";
        }

        // Assets 아래 중첩 폴더를 순서대로 생성합니다.
        private static void EnsureFolder(string folderPath)
        {
            string normalized = NormalizeAssetPath(folderPath);
            if (AssetDatabase.IsValidFolder(normalized)) return;
            string parent = NormalizeAssetPath(Path.GetDirectoryName(normalized));
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            string folderName = Path.GetFileName(normalized);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // OS 경로 구분자를 Unity asset path 형식으로 정규화합니다.
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        // candidate가 stage별 bake root의 자식 asset path인지 엄격하게 확인합니다.
        private static bool IsPathInsideRoot(
            string candidate,
            string root)
        {
            string normalizedCandidate = NormalizeAssetPath(candidate);
            string normalizedRoot = NormalizeAssetPath(root);
            return !string.IsNullOrEmpty(normalizedCandidate) &&
                   !string.IsNullOrEmpty(normalizedRoot) &&
                   normalizedCandidate.StartsWith(
                       normalizedRoot + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        // 경로가 stage Bake root 바로 아래의 고유 Version 폴더인지 확인합니다.
        private static bool IsVersionFolderInsideBakeRoot(
            string candidateFolder,
            string bakeRoot)
        {
            string normalizedCandidate = NormalizeAssetPath(candidateFolder);
            string normalizedRoot = NormalizeAssetPath(bakeRoot);
            if (!IsPathInsideRoot(
                    normalizedCandidate + "/__probe",
                    normalizedRoot))
            {
                return false;
            }
            string parent = NormalizeAssetPath(
                Path.GetDirectoryName(normalizedCandidate));
            string name = Path.GetFileName(normalizedCandidate);
            return string.Equals(
                       parent,
                       normalizedRoot,
                       StringComparison.OrdinalIgnoreCase) &&
                   name.StartsWith(
                       "Version_",
                       StringComparison.Ordinal);
        }

        // 유효하지 않은 검증 결과를 코드가 보존된 Bake 예외로 변환합니다.
        private static void ThrowIfInvalid(
            string message,
            DungeonValidationReport report)
        {
            if (report != null && report.IsValid) return;
            throw new DungeonStageBakeException(message, report);
        }

        // 하위 검증 report의 issue를 순서를 보존해 목적 report에 병합합니다.
        private static void Merge(
            DungeonValidationReport destination,
            DungeonValidationReport source)
        {
            if (destination == null || source == null || source.issues == null)
                return;
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }
    }

    internal sealed class DungeonBakeFingerprints
    {
        public string SourceBlueprintHash;
        public string FinalBlueprintHash;
        public string CatalogPlanningHash;
        public string ContentRealizationHash;
        public string GameplayBuildConfigHash;
        public string MaterialDependencyHash;
    }

    internal static class DungeonBakeFingerprintUtility
    {
        // Bake manifest에 기록할 독립 fingerprint 여섯 개를 현재 Editor 의존성에서 계산합니다.
        public static DungeonBakeFingerprints Compute(
            DungeonBlueprintAsset blueprintAsset,
            DungeonContentCatalog catalog,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings,
            DungeonMissingContentPolicy missingPolicy)
        {
            DungeonBlueprint blueprint =
                blueprintAsset != null ? blueprintAsset.blueprint : null;
            return new DungeonBakeFingerprints
            {
                SourceBlueprintHash =
                    blueprint != null ? blueprint.blueprintHash ?? string.Empty : string.Empty,
                FinalBlueprintHash =
                    blueprint != null ? blueprint.blueprintHash ?? string.Empty : string.Empty,
                CatalogPlanningHash =
                    blueprint != null
                        ? blueprint.catalogPlanningHash ?? string.Empty
                        : string.Empty,
                ContentRealizationHash =
                    ComputeContentRealization(blueprint, catalog, missingPolicy),
                GameplayBuildConfigHash =
                    ComputeGameplayConfiguration(
                        catalog,
                        runtimeSettings,
                        missingPolicy),
                MaterialDependencyHash =
                    ComputeMaterialDependencies(materialSet, catalog)
            };
        }

        // spawn별 직접 Prefab, built-in, fallback/skip 결정과 Prefab dependency를 canonical SHA-256으로 묶습니다.
        private static string ComputeContentRealization(
            DungeonBlueprint blueprint,
            DungeonContentCatalog catalog,
            DungeonMissingContentPolicy missingPolicy)
        {
            CanonicalHashWriter writer = new CanonicalHashWriter();
            writer.Add("rdl-content-realization-v1");
            writer.Add(DungeonBakeBuilderVersions.Current);
            writer.Add((int)missingPolicy);

            Dictionary<string, DungeonContentCatalogEntry> entries =
                BuildCatalogLookup(catalog);
            List<DungeonSpawnRecord> spawns =
                blueprint != null && blueprint.spawns != null
                    ? new List<DungeonSpawnRecord>(blueprint.spawns)
                    : new List<DungeonSpawnRecord>();
            spawns.Sort(CompareSpawns);
            writer.Add(spawns.Count);
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonSpawnRecord record = spawns[i];
                if (record == null)
                {
                    writer.Add("<null>");
                    continue;
                }
                writer.Add(record.spawnId);
                writer.Add(record.contentKey);
                writer.Add((int)record.category);

                DungeonContentCatalogEntry entry;
                if (entries.TryGetValue(record.contentKey ?? string.Empty, out entry) &&
                    entry != null &&
                    entry.prefab != null)
                {
                    writer.Add("prefab");
                    AddPrefabRealizationReference(writer, entry.prefab);
                }
                else if (IsBuiltInKey(record.contentKey, record.category))
                {
                    writer.Add("built-in");
                    writer.Add(record.contentKey);
                }
                else
                {
                    writer.Add(
                        missingPolicy == DungeonMissingContentPolicy.BuiltInFallback
                            ? "generic-fallback"
                            : missingPolicy == DungeonMissingContentPolicy.Skip
                                ? "skip"
                                : "error");
                }
            }
            return writer.Finish();
        }

        // missing policy, marker 동작, drop table 및 gameplayId를 canonical SHA-256으로 묶습니다.
        private static string ComputeGameplayConfiguration(
            DungeonContentCatalog catalog,
            RogueDungeonSettings runtimeSettings,
            DungeonMissingContentPolicy missingPolicy)
        {
            CanonicalHashWriter writer = new CanonicalHashWriter();
            writer.Add("rdl-gameplay-build-v1");
            writer.Add(DungeonBakeBuilderVersions.Current);
            writer.Add((int)missingPolicy);
            writer.Add(runtimeSettings == null || runtimeSettings.spawnDropMarkers);
            AddEffectiveRuntimeDropTable(writer, runtimeSettings, true);
            AddEffectiveRuntimeDropTable(writer, runtimeSettings, false);

            List<DungeonContentCatalogEntry> entries =
                catalog != null && catalog.entries != null
                    ? new List<DungeonContentCatalogEntry>(catalog.entries)
                    : new List<DungeonContentCatalogEntry>();
            entries.Sort(CompareCatalogEntries);
            writer.Add(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = entries[i];
                if (entry == null)
                {
                    writer.Add("<null>");
                    continue;
                }
                writer.Add(entry.contentKey);
                writer.Add((int)entry.category);
                writer.Add(entry.gameplayId);
                AddDropTable(writer, entry.dropTable, "none");
            }
            AddCatalogPrefabGameplayDependencies(writer, entries);
            return writer.Finish();
        }

        // 명시 설정이 없을 때도 실제 Runtime 기본 Drop Table 항목 전체를 gameplay 지문에 기록합니다.
        private static void AddEffectiveRuntimeDropTable(
            CanonicalHashWriter writer,
            RogueDungeonSettings runtimeSettings,
            bool enemy)
        {
            RogueDungeonSettings temporaryDefaults = null;
            try
            {
                RogueDungeonSettings source = runtimeSettings;
                if (source == null)
                {
                    temporaryDefaults =
                        ScriptableObject.CreateInstance<RogueDungeonSettings>();
                    source = temporaryDefaults;
                }
                WeightedDropTable table = enemy
                    ? source.EffectiveEnemyDropTable
                    : source.EffectiveDestructibleDropTable;
                writer.Add(enemy ? "effective-enemy-drop" : "effective-destructible-drop");
                AddDropTable(writer, table, "runtime-default-unavailable");
            }
            finally
            {
                if (temporaryDefaults != null)
                    Object.DestroyImmediate(temporaryDefaults);
            }
        }

        // 직접 Prefab에 이미 작성된 파괴 대상의 ID·종류·Drop Table·marker 설정을 gameplay 지문에 추가합니다.
        private static void AddCatalogPrefabGameplayDependencies(
            CanonicalHashWriter writer,
            List<DungeonContentCatalogEntry> entries)
        {
            writer.Add("catalog-prefab-gameplay");
            writer.Add(entries != null ? entries.Count : 0);
            if (entries == null) return;
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                DungeonContentCatalogEntry entry = entries[entryIndex];
                writer.Add(entry != null ? entry.contentKey : "<null>");
                if (entry == null || entry.prefab == null)
                {
                    writer.Add(0);
                    continue;
                }

                Transform[] transforms =
                    entry.prefab.GetComponentsInChildren<Transform>(true);
                List<Transform> orderedTransforms =
                    new List<Transform>(transforms);
                orderedTransforms.Sort(CompareTransforms);
                int targetCount = 0;
                for (int transformIndex = 0;
                     transformIndex < orderedTransforms.Count;
                     transformIndex++)
                {
                    targetCount += orderedTransforms[transformIndex]
                        .GetComponents<DestructibleDropTarget>()
                        .Length;
                }
                writer.Add(targetCount);
                for (int transformIndex = 0;
                     transformIndex < orderedTransforms.Count;
                     transformIndex++)
                {
                    Transform transform = orderedTransforms[transformIndex];
                    DestructibleDropTarget[] targets =
                        transform.GetComponents<DestructibleDropTarget>();
                    for (int targetIndex = 0;
                         targetIndex < targets.Length;
                         targetIndex++)
                    {
                        DestructibleDropTarget target = targets[targetIndex];
                        SerializedObject serializedTarget =
                            new SerializedObject(target);
                        SerializedProperty marker =
                            serializedTarget.FindProperty("spawnMarker");
                        writer.Add(GetHierarchyPath(transform));
                        writer.Add(targetIndex);
                        writer.Add(target.TargetId);
                        writer.Add((int)target.SourceKind);
                        writer.Add(marker != null && marker.boolValue);
                        AddDropTable(
                            writer,
                            target.DropTable,
                            "prefab-target-fallback");
                    }
                }
            }
        }

        // Transform gameplay fingerprint 순서를 stable hierarchy path로 고정합니다.
        private static int CompareTransforms(Transform left, Transform right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return string.CompareOrdinal(
                GetHierarchyPath(left),
                GetHierarchyPath(right));
        }

        // Material Set과 각 slot의 GUID/local ID/dependency hash를 canonical SHA-256으로 묶습니다.
        private static string ComputeMaterialDependencies(
            DungeonBakeMaterialSet materialSet,
            DungeonContentCatalog catalog)
        {
            CanonicalHashWriter writer = new CanonicalHashWriter();
            writer.Add("rdl-material-dependency-v1");
            AddAssetReference(writer, materialSet);
            if (materialSet != null)
            {
                AddNamedAssetReference(writer, "floor", materialSet.floor);
                AddNamedAssetReference(writer, "wall", materialSet.wall);
                AddNamedAssetReference(writer, "enemy", materialSet.enemy);
                AddNamedAssetReference(
                    writer,
                    "destructible",
                    materialSet.destructible);
                AddNamedAssetReference(writer, "prop", materialSet.prop);
                AddNamedAssetReference(writer, "gimmick", materialSet.gimmick);
                AddNamedAssetReference(writer, "entrance", materialSet.entrance);
                AddNamedAssetReference(writer, "exit", materialSet.exit);
            }
            AddCatalogPrefabMaterialDependencies(writer, catalog);
            return writer.Finish();
        }

        // Catalog 직접 Prefab Renderer의 hierarchy/slot별 Material과 Shader 의존성을 canonical 순서로 추가합니다.
        private static void AddCatalogPrefabMaterialDependencies(
            CanonicalHashWriter writer,
            DungeonContentCatalog catalog)
        {
            List<DungeonContentCatalogEntry> entries =
                catalog != null && catalog.entries != null
                    ? new List<DungeonContentCatalogEntry>(catalog.entries)
                    : new List<DungeonContentCatalogEntry>();
            entries.Sort(CompareCatalogEntries);
            writer.Add("catalog-prefab-materials");
            writer.Add(entries.Count);
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                DungeonContentCatalogEntry entry = entries[entryIndex];
                if (entry == null || entry.prefab == null)
                {
                    writer.Add("<none>");
                    continue;
                }

                writer.Add(entry.contentKey);
                Renderer[] renderers =
                    entry.prefab.GetComponentsInChildren<Renderer>(true);
                List<Renderer> orderedRenderers = new List<Renderer>(renderers);
                orderedRenderers.Sort(CompareRenderers);
                writer.Add(orderedRenderers.Count);
                for (int rendererIndex = 0;
                     rendererIndex < orderedRenderers.Count;
                     rendererIndex++)
                {
                    Renderer renderer = orderedRenderers[rendererIndex];
                    writer.Add(GetHierarchyPath(renderer.transform));
                    writer.Add(renderer.GetType().FullName);
                    Material[] materials = renderer.sharedMaterials;
                    writer.Add(materials != null ? materials.Length : 0);
                    if (materials == null) continue;
                    for (int slotIndex = 0;
                         slotIndex < materials.Length;
                         slotIndex++)
                    {
                        writer.Add(slotIndex);
                        Material material = materials[slotIndex];
                        AddAssetReference(writer, material);
                        writer.Add(
                            material != null && material.shader != null
                                ? material.shader.name
                                : string.Empty);
                        AddAssetReference(
                            writer,
                            material != null ? material.shader : null);
                    }
                }
            }
        }

        // Prefab root에서 Renderer까지 sibling index를 포함한 stable hierarchy path를 계산합니다.
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Add(
                    (current.name ?? string.Empty) +
                    "#" +
                    current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        // Renderer fingerprint 순서를 stable hierarchy path와 component type으로 고정합니다.
        private static int CompareRenderers(Renderer left, Renderer right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(
                GetHierarchyPath(left.transform),
                GetHierarchyPath(right.transform));
            return result != 0
                ? result
                : string.CompareOrdinal(
                    left.GetType().FullName,
                    right.GetType().FullName);
        }

        // 이름과 project asset 참조 fingerprint를 함께 hash writer에 추가합니다.
        private static void AddNamedAssetReference(
            CanonicalHashWriter writer,
            string name,
            Object asset)
        {
            writer.Add(name);
            AddAssetReference(writer, asset);
        }

        // 직접 Prefab의 파일과 비표현·비드랍 의존성을 content realization 지문으로 기록합니다.
        private static void AddPrefabRealizationReference(
            CanonicalHashWriter writer,
            GameObject prefab)
        {
            AddAssetIdentity(writer, prefab);
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            writer.Add(ComputeAssetFileHash(prefabPath));

            HashSet<string> excludedDependencies =
                CollectPresentationAndGameplayDependencies(prefab);
            string[] dependencies = string.IsNullOrEmpty(prefabPath)
                ? Array.Empty<string>()
                : AssetDatabase.GetDependencies(prefabPath, true);
            Array.Sort(dependencies, StringComparer.Ordinal);
            List<string> realizationDependencies = new List<string>();
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyPath = dependencies[i];
                if (string.Equals(
                        dependencyPath,
                        prefabPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    excludedDependencies.Contains(dependencyPath))
                {
                    continue;
                }
                Object dependency =
                    AssetDatabase.LoadMainAssetAtPath(dependencyPath);
                if (dependency is Material ||
                    dependency is Shader ||
                    dependency is WeightedDropTable)
                {
                    continue;
                }
                realizationDependencies.Add(dependencyPath);
            }

            writer.Add(realizationDependencies.Count);
            for (int i = 0; i < realizationDependencies.Count; i++)
            {
                string dependencyPath = realizationDependencies[i];
                writer.Add(dependencyPath);
                AddAssetIdentity(
                    writer,
                    AssetDatabase.LoadMainAssetAtPath(dependencyPath));
                writer.Add(ComputeAssetFileHash(dependencyPath));
            }
        }

        // Renderer 재질과 파괴 대상 Drop Table의 전체 dependency closure를 다른 지문 책임으로 제외합니다.
        private static HashSet<string> CollectPresentationAndGameplayDependencies(
            GameObject prefab)
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (prefab == null) return result;

            Renderer[] renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
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
                    AddDependencyClosure(result, materials[materialIndex]);
                }
            }

            DestructibleDropTarget[] targets =
                prefab.GetComponentsInChildren<DestructibleDropTarget>(true);
            for (int i = 0; i < targets.Length; i++)
                AddDependencyClosure(result, targets[i].DropTable);
            return result;
        }

        // 하나의 자산과 재귀 dependency 경로를 제외 집합에 추가합니다.
        private static void AddDependencyClosure(
            HashSet<string> paths,
            Object asset)
        {
            if (paths == null || asset == null) return;
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return;
            string[] dependencies =
                AssetDatabase.GetDependencies(assetPath, true);
            for (int i = 0; i < dependencies.Length; i++)
                paths.Add(dependencies[i]);
        }

        // 자산 내용을 dependency 확장 없이 SHA-256으로 계산하고 가상 경로는 Unity dependency hash로 대체합니다.
        private static string ComputeAssetFileHash(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;
            string physicalPath = Path.GetFullPath(assetPath);
            if (!File.Exists(physicalPath))
                return AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
            byte[] bytes = File.ReadAllBytes(physicalPath);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    result.Append(
                        hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return result.ToString();
            }
        }

        // 자산의 GUID와 local file ID만 기록해 다른 지문의 dependency 내용과 겹치지 않게 합니다.
        private static void AddAssetIdentity(
            CanonicalHashWriter writer,
            Object asset)
        {
            if (asset == null)
            {
                writer.Add("<null>");
                return;
            }
            string guid;
            long localId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out guid,
                    out localId))
            {
                guid = string.Empty;
                localId = 0L;
            }
            writer.Add(guid);
            writer.Add(localId);
        }

        // GUID, local file ID, dependency hash를 사용해 project asset 참조를 fingerprint에 추가합니다.
        private static void AddAssetReference(
            CanonicalHashWriter writer,
            Object asset)
        {
            if (asset == null)
            {
                writer.Add("<null>");
                return;
            }
            string guid;
            long localId;
            string path = AssetDatabase.GetAssetPath(asset);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out guid,
                    out localId))
            {
                guid = string.Empty;
                localId = 0L;
            }
            writer.Add(guid);
            writer.Add(localId);
            writer.Add(
                string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.GetAssetDependencyHash(path).ToString());
        }

        // Drop Table 자산 참조와 entry 전체를 순서 보존 canonical 값으로 추가합니다.
        private static void AddDropTable(
            CanonicalHashWriter writer,
            WeightedDropTable table,
            string nullToken)
        {
            if (table == null)
            {
                writer.Add(nullToken);
                return;
            }
            AddAssetReference(writer, table);
            List<DropEntry> entries =
                table.entries ?? new List<DropEntry>();
            writer.Add(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                DropEntry entry = entries[i];
                if (entry == null)
                {
                    writer.Add("<null>");
                    continue;
                }
                writer.Add(entry.itemId);
                writer.Add(entry.weight);
                writer.Add(entry.minQuantity);
                writer.Add(entry.maxQuantity);
                writer.Add(entry.representsNoDrop);
                writer.Add(entry.markerColor.r);
                writer.Add(entry.markerColor.g);
                writer.Add(entry.markerColor.b);
                writer.Add(entry.markerColor.a);
            }
        }

        // Catalog entries를 contentKey lookup으로 변환합니다.
        private static Dictionary<string, DungeonContentCatalogEntry> BuildCatalogLookup(
            DungeonContentCatalog catalog)
        {
            Dictionary<string, DungeonContentCatalogEntry> result =
                new Dictionary<string, DungeonContentCatalogEntry>(
                    StringComparer.Ordinal);
            if (catalog == null || catalog.entries == null) return result;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.contentKey))
                    result[entry.contentKey] = entry;
            }
            return result;
        }

        // 정확한 built-in key/category 조합인지 판정합니다.
        private static bool IsBuiltInKey(
            string contentKey,
            DungeonSpawnCategory category)
        {
            if (category == DungeonSpawnCategory.Marker)
            {
                return contentKey == DungeonBuiltInContentKeys.EntranceMarker ||
                       contentKey == DungeonBuiltInContentKeys.ExitMarker;
            }
            if (category == DungeonSpawnCategory.Gimmick)
                return contentKey == DungeonBuiltInContentKeys.Gimmick;
            if (category == DungeonSpawnCategory.Enemy)
                return contentKey == DungeonBuiltInContentKeys.Enemy;
            if (category == DungeonSpawnCategory.Destructible)
                return contentKey == DungeonBuiltInContentKeys.Destructible;
            return category == DungeonSpawnCategory.Prop &&
                   (contentKey == DungeonBuiltInContentKeys.PropCube ||
                    contentKey == DungeonBuiltInContentKeys.PropCylinder);
        }

        // spawn fingerprint 순서를 stable ID와 content key로 고정합니다.
        private static int CompareSpawns(
            DungeonSpawnRecord left,
            DungeonSpawnRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.spawnId, right.spawnId);
            return result != 0
                ? result
                : string.CompareOrdinal(left.contentKey, right.contentKey);
        }

        // gameplay fingerprint의 Catalog entry 순서를 contentKey와 category로 고정합니다.
        private static int CompareCatalogEntries(
            DungeonContentCatalogEntry left,
            DungeonContentCatalogEntry right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.contentKey, right.contentKey);
            return result != 0
                ? result
                : ((int)left.category).CompareTo((int)right.category);
        }
    }

    internal sealed class CanonicalHashWriter
    {
        private readonly StringBuilder _builder = new StringBuilder(2048);

        // null과 일반 문자열을 길이 prefix로 구분해 canonical stream에 추가합니다.
        public void Add(string value)
        {
            string normalized = value ?? "<null>";
            _builder.Append(normalized.Length);
            _builder.Append(':');
            _builder.Append(normalized);
            _builder.Append('|');
        }

        // 정수 값을 invariant canonical 문자열로 추가합니다.
        public void Add(int value)
        {
            Add(value.ToString(CultureInfo.InvariantCulture));
        }

        // 64-bit 정수 값을 invariant canonical 문자열로 추가합니다.
        public void Add(long value)
        {
            Add(value.ToString(CultureInfo.InvariantCulture));
        }

        // bool 값을 0 또는 1 canonical 값으로 추가합니다.
        public void Add(bool value)
        {
            Add(value ? "1" : "0");
        }

        // float 값을 round-trip invariant canonical 문자열로 추가합니다.
        public void Add(float value)
        {
            Add(value.ToString("R", CultureInfo.InvariantCulture));
        }

        // 누적 canonical stream의 소문자 SHA-256 hex를 반환합니다.
        public string Finish()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(_builder.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
