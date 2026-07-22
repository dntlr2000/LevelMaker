using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public enum DungeonBlueprintComparisonState
    {
        Unavailable = 0,
        InvalidCurrent = 1,
        InvalidSaved = 2,
        Identical = 3,
        DifferentSeed = 4,
        StaleInputs = 5,
        Diverged = 6
    }

    public enum DungeonStoredRecipeState
    {
        Missing = 0,
        Valid = 1,
        UnsupportedFormat = 2,
        HashMismatch = 3,
        NonCanonical = 4
    }

    public sealed class DungeonStoredRecipeValidation
    {
        public DungeonStoredRecipeState State { get; private set; }
        public string SnapshotHash { get; private set; }
        public string BlueprintRecipeHash { get; private set; }
        public bool IsValid { get { return State == DungeonStoredRecipeState.Valid; } }

        // 저장 레시피의 상태와 실제·기대 hash를 읽기 전용 결과로 묶습니다.
        public DungeonStoredRecipeValidation(
            DungeonStoredRecipeState state,
            string snapshotHash,
            string blueprintRecipeHash)
        {
            State = state;
            SnapshotHash = snapshotHash ?? string.Empty;
            BlueprintRecipeHash = blueprintRecipeHash ?? string.Empty;
        }
    }

    public sealed class DungeonBlueprintComparison
    {
        public DungeonBlueprintComparisonState State { get; private set; }
        public DungeonValidationReport CurrentValidation { get; private set; }
        public DungeonValidationReport SavedValidation { get; private set; }
        public bool IsStale
        {
            get
            {
                return State == DungeonBlueprintComparisonState.StaleInputs ||
                       State == DungeonBlueprintComparisonState.Diverged;
            }
        }

        // 비교 상태와 양쪽 검증 리포트를 하나의 읽기 전용 결과로 묶습니다.
        public DungeonBlueprintComparison(
            DungeonBlueprintComparisonState state,
            DungeonValidationReport currentValidation,
            DungeonValidationReport savedValidation)
        {
            State = state;
            CurrentValidation = currentValidation ?? new DungeonValidationReport();
            SavedValidation = savedValidation ?? new DungeonValidationReport();
        }
    }

    public sealed class DungeonStageAuthoringException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        // 저장 제작 실패 메시지와 코드 기반 검증 리포트를 함께 전달합니다.
        public DungeonStageAuthoringException(
            string message,
            DungeonValidationReport validationReport)
            : base(message)
        {
            ValidationReport = validationReport ?? new DungeonValidationReport();
        }
    }

    public static class DungeonStageAuthoringService
    {
        // Blueprint 자체와 선택적 catalog의 배치 계약을 저장·미리보기 전에 함께 검증합니다.
        public static DungeonValidationReport ValidateBlueprint(
            DungeonBlueprint blueprint,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback)
        {
            DungeonValidationReport result = DungeonBlueprintValidator.Validate(blueprint);
            if (!Enum.IsDefined(typeof(DungeonMissingContentPolicy), missingContentPolicy))
            {
                result.Add(
                    DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy,
                    DungeonValidationSeverity.Error,
                    "Missing content policy is invalid.");
            }
            if (contentCatalog != null)
                Merge(result, DungeonContentCatalogValidator.Validate(contentCatalog));
            if (blueprint != null)
            {
                Merge(
                    result,
                    DungeonContentCatalogValidator.ValidateBlueprint(
                        blueprint,
                        contentCatalog,
                        missingContentPolicy));
            }
            return result;
        }

        // BlueprintAsset의 선택적 제작 레시피가 지원 버전·hash·정규화 계약을 만족하는지 검사합니다.
        public static DungeonStoredRecipeValidation ValidateStoredRecipe(
            DungeonBlueprintAsset blueprintAsset)
        {
            string expectedHash = blueprintAsset != null && blueprintAsset.blueprint != null
                ? blueprintAsset.blueprint.recipeHash ?? string.Empty
                : string.Empty;
            DungeonRecipeSnapshot snapshot;
            try
            {
                if (blueprintAsset == null ||
                    !blueprintAsset.TryGetAuthoringRecipeSnapshot(out snapshot) ||
                    snapshot == null)
                {
                    return new DungeonStoredRecipeValidation(
                        DungeonStoredRecipeState.Missing,
                        string.Empty,
                        expectedHash);
                }
            }
            catch (Exception)
            {
                return new DungeonStoredRecipeValidation(
                    DungeonStoredRecipeState.NonCanonical,
                    string.Empty,
                    expectedHash);
            }
            if (snapshot.formatVersion != DungeonRecipeSnapshot.CurrentFormatVersion)
            {
                return new DungeonStoredRecipeValidation(
                    DungeonStoredRecipeState.UnsupportedFormat,
                    string.Empty,
                    expectedHash);
            }

            string snapshotHash = snapshot.ComputeHash();
            if (!string.Equals(snapshotHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new DungeonStoredRecipeValidation(
                    DungeonStoredRecipeState.HashMismatch,
                    snapshotHash,
                    expectedHash);
            }
            if (!IsCanonicalSnapshot(snapshot, snapshotHash))
            {
                return new DungeonStoredRecipeValidation(
                    DungeonStoredRecipeState.NonCanonical,
                    snapshotHash,
                    expectedHash);
            }
            return new DungeonStoredRecipeValidation(
                DungeonStoredRecipeState.Valid,
                snapshotHash,
                expectedHash);
        }

        // 현재 결과와 저장본의 논리 hash·seed·생성 입력 provenance를 비교해 stale 상태를 분류합니다.
        public static DungeonBlueprintComparison Compare(
            DungeonBlueprint current,
            DungeonBlueprintAsset savedAsset)
        {
            DungeonBlueprint saved = savedAsset != null ? savedAsset.blueprint : null;
            DungeonValidationReport currentValidation = DungeonBlueprintValidator.Validate(current);
            DungeonValidationReport savedValidation = DungeonBlueprintValidator.Validate(saved);
            if (current == null || saved == null)
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.Unavailable,
                    currentValidation,
                    savedValidation);
            }
            if (!currentValidation.IsValid)
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.InvalidCurrent,
                    currentValidation,
                    savedValidation);
            }
            if (!savedValidation.IsValid)
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.InvalidSaved,
                    currentValidation,
                    savedValidation);
            }

            string currentHash = DungeonBlueprintHasher.Compute(current);
            string savedHash = DungeonBlueprintHasher.Compute(saved);
            if (string.Equals(currentHash, savedHash, StringComparison.OrdinalIgnoreCase))
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.Identical,
                    currentValidation,
                    savedValidation);
            }
            if (!HasSameProvenance(current, saved))
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.StaleInputs,
                    currentValidation,
                    savedValidation);
            }
            if (current.seed != saved.seed)
            {
                return new DungeonBlueprintComparison(
                    DungeonBlueprintComparisonState.DifferentSeed,
                    currentValidation,
                    savedValidation);
            }
            return new DungeonBlueprintComparison(
                DungeonBlueprintComparisonState.Diverged,
                currentValidation,
                savedValidation);
        }

        // 검증된 현재 결과를 새 DungeonBlueprintAsset으로 생성하고 프로젝트에 즉시 저장합니다.
        public static DungeonBlueprintAsset CreateBlueprintAsset(
            DungeonBlueprint source,
            string assetPath,
            string authoringNote = "",
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback,
            DungeonRecipeSnapshot authoringRecipeSnapshot = null)
        {
            DungeonValidationReport validation = ValidateBlueprint(
                source,
                contentCatalog,
                missingContentPolicy);
            ThrowIfInvalid("현재 던전 결과를 저장할 수 없습니다.", validation);
            string normalizedPath = ValidateNewAssetPath(assetPath);
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            asset.name = Path.GetFileNameWithoutExtension(normalizedPath);
            try
            {
                asset.Store(PrepareForStorage(source, authoringNote), authoringRecipeSnapshot);
            }
            catch (Exception)
            {
                UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
            AssetDatabase.CreateAsset(asset, normalizedPath);
            Undo.RegisterCreatedObjectUndo(asset, "Create Dungeon Blueprint Asset");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // 기존 Blueprint 자산의 전체 직렬화 상태를 Undo에 기록한 뒤 검증된 현재 결과로 덮어씁니다.
        public static void OverwriteBlueprintAsset(
            DungeonBlueprintAsset target,
            DungeonBlueprint source,
            string authoringNote = "",
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback,
            DungeonRecipeSnapshot authoringRecipeSnapshot = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            DungeonValidationReport validation = ValidateBlueprint(
                source,
                contentCatalog,
                missingContentPolicy);
            ThrowIfInvalid("선택한 Blueprint를 덮어쓸 수 없습니다.", validation);
            Undo.RegisterCompleteObjectUndo(target, "Overwrite Dungeon Blueprint Asset");
            target.Store(PrepareForStorage(source, authoringNote), authoringRecipeSnapshot);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        // 저장 레시피의 생성 필드를 현재 설정 자산에 Undo 가능하게 적용하고 선택적으로 저장 시드도 복원합니다.
        public static void ApplyStoredRecipeToSettings(
            DungeonBlueprintAsset blueprintAsset,
            RogueDungeonSettings targetSettings,
            bool applySavedSeed)
        {
            if (targetSettings == null) throw new ArgumentNullException(nameof(targetSettings));
            DungeonRecipeSnapshot snapshot = GetValidatedStoredRecipe(blueprintAsset);

            Undo.RegisterCompleteObjectUndo(targetSettings, "Load Dungeon Blueprint Recipe");
            SerializedObject serialized = new SerializedObject(targetSettings);
            serialized.Update();
            ApplyRecipeSnapshot(serialized, snapshot);
            if (applySavedSeed)
                serialized.FindProperty("seed").intValue = blueprintAsset.blueprint.seed;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetSettings);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(targetSettings)))
                AssetDatabase.SaveAssets();

            string appliedHash = DungeonRecipeSnapshot.Capture(targetSettings).ComputeHash();
            if (!string.Equals(appliedHash, snapshot.ComputeHash(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Applied settings do not match the stored recipe hash.");
        }

        // 저장 레시피와 시드·생성기 버전으로 원 결과를 사전 검증한 뒤 현재 Generator에 절차 생성합니다.
        public static DungeonStageInstance ApplyStoredRecipeAndGenerate(
            RogueDungeonGenerator generator,
            DungeonBlueprintAsset blueprintAsset,
            RogueDungeonSettings targetSettings,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (targetSettings == null) throw new ArgumentNullException(nameof(targetSettings));
            if (blueprintAsset == null) throw new ArgumentNullException(nameof(blueprintAsset));
            DungeonValidationReport validation = ValidateBlueprint(
                blueprintAsset.blueprint,
                contentCatalog,
                missingContentPolicy);
            ThrowIfInvalid("저장 Blueprint 설정으로 절차 생성할 수 없습니다.", validation);
            DungeonRecipeSnapshot snapshot = GetValidatedStoredRecipe(blueprintAsset);
            VerifyStoredRecipeRegeneration(blueprintAsset, snapshot, contentCatalog);

            ApplyStoredRecipeToSettings(blueprintAsset, targetSettings, true);
            generator.GenerateProcedural(
                targetSettings,
                blueprintAsset.blueprint.seed,
                blueprintAsset.blueprint.generatorVersion,
                contentCatalog,
                missingContentPolicy,
                "editor-recipe-restore");
            string actualHash = generator.CurrentBlueprint != null
                ? DungeonBlueprintHasher.Compute(generator.CurrentBlueprint)
                : string.Empty;
            string expectedHash = DungeonBlueprintHasher.Compute(blueprintAsset.blueprint);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Regenerated Blueprint hash does not match the saved Blueprint.");
            SceneView.RepaintAll();
            return generator.CurrentStageInstance;
        }

        // 검증된 저장 Blueprint를 참조하는 RuntimeBuild StageDefinition 자산을 생성합니다.
        public static DungeonStageDefinition CreateStageDefinitionAsset(
            DungeonBlueprintAsset blueprintAsset,
            string assetPath,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback,
            bool loadOnPlay = true)
        {
            if (blueprintAsset == null) throw new ArgumentNullException(nameof(blueprintAsset));
            DungeonValidationReport validation = ValidateBlueprint(
                blueprintAsset.blueprint,
                contentCatalog,
                missingContentPolicy);
            ThrowIfInvalid("Stage Definition을 생성할 수 없습니다.", validation);
            string normalizedPath = ValidateNewAssetPath(assetPath);

            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            definition.name = Path.GetFileNameWithoutExtension(normalizedPath);
            AssetDatabase.CreateAsset(definition, normalizedPath);
            Undo.RegisterCreatedObjectUndo(definition, "Create Dungeon Stage Definition");
            SerializedObject serialized = new SerializedObject(definition);
            serialized.Update();
            serialized.FindProperty("sourceMode").intValue = (int)DungeonStageSourceMode.SavedBlueprint;
            serialized.FindProperty("buildMode").intValue = (int)DungeonStageBuildMode.RuntimeBuild;
            serialized.FindProperty("recipe").objectReferenceValue = null;
            serialized.FindProperty("savedBlueprint").objectReferenceValue = blueprintAsset;
            serialized.FindProperty("seedPolicy").intValue = (int)DungeonStageSeedPolicy.FixedSeed;
            serialized.FindProperty("fixedSeed").intValue = blueprintAsset.blueprint.seed;
            serialized.FindProperty("generatorVersion").intValue = blueprintAsset.blueprint.generatorVersion;
            serialized.FindProperty("contentCatalog").objectReferenceValue = contentCatalog;
            serialized.FindProperty("missingContentPolicy").intValue = (int)missingContentPolicy;
            serialized.FindProperty("loadOnPlay").boolValue = loadOnPlay;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        // 생성된 StageDefinition을 Generator의 직렬화 필드에 Undo 가능한 방식으로 연결합니다.
        public static void AssignStageDefinition(
            RogueDungeonGenerator generator,
            DungeonStageDefinition definition)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Undo.RecordObject(generator, "Assign Dungeon Stage Definition");
            SerializedObject serialized = new SerializedObject(generator);
            serialized.Update();
            serialized.FindProperty("stageDefinition").objectReferenceValue = definition;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(generator);
            if (generator.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        }

        // 선택한 저장본을 Generator 아래에 재계산 없이 RuntimeBuild로 미리보기합니다.
        public static DungeonStageInstance PreviewSavedBlueprint(
            RogueDungeonGenerator generator,
            DungeonBlueprintAsset blueprintAsset,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (blueprintAsset == null) throw new ArgumentNullException(nameof(blueprintAsset));
            DungeonValidationReport validation = ValidateBlueprint(
                blueprintAsset.blueprint,
                contentCatalog,
                missingContentPolicy);
            ThrowIfInvalid("저장 Blueprint를 미리보기할 수 없습니다.", validation);
            generator.LoadSavedBlueprint(
                blueprintAsset,
                contentCatalog,
                missingContentPolicy,
                "editor-preview");
            SceneView.RepaintAll();
            return generator.CurrentStageInstance;
        }

        // Generator의 Procedural StageDefinition 또는 settings facade로 지정 시드의 원본 미리보기를 복원합니다.
        public static DungeonStageInstance PreviewProcedural(
            RogueDungeonGenerator generator,
            int seed)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (generator.stageDefinition != null &&
                generator.stageDefinition.sourceMode == DungeonStageSourceMode.Procedural)
            {
                generator.LoadStageDefinitionWithSeed(seed);
            }
            else if (generator.settings != null)
            {
                generator.GenerateWithSeed(seed);
            }
            else
            {
                throw new InvalidOperationException(
                    "Procedural preview requires settings or a Procedural Stage Definition.");
            }
            SceneView.RepaintAll();
            return generator.CurrentStageInstance;
        }

        // 저장 레시피 상태를 검사하고 설정 적용에 사용할 독립 스냅샷을 반환합니다.
        private static DungeonRecipeSnapshot GetValidatedStoredRecipe(
            DungeonBlueprintAsset blueprintAsset)
        {
            if (blueprintAsset == null) throw new ArgumentNullException(nameof(blueprintAsset));
            DungeonStoredRecipeValidation validation = ValidateStoredRecipe(blueprintAsset);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "Stored authoring recipe is not usable: " + validation.State +
                    ". snapshot=" + validation.SnapshotHash +
                    ", blueprint=" + validation.BlueprintRecipeHash);
            }
            DungeonRecipeSnapshot snapshot;
            if (!blueprintAsset.TryGetAuthoringRecipeSnapshot(out snapshot) || snapshot == null)
                throw new InvalidOperationException("Stored authoring recipe is missing.");
            return snapshot;
        }

        // 스냅샷을 설정에 적용해 다시 캡처했을 때 같은 hash가 되는 canonical 데이터인지 확인합니다.
        private static bool IsCanonicalSnapshot(
            DungeonRecipeSnapshot snapshot,
            string expectedHash)
        {
            RogueDungeonSettings temporary = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                snapshot.ApplyTo(temporary);
                string normalizedHash = DungeonRecipeSnapshot.Capture(temporary).ComputeHash();
                return string.Equals(normalizedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        // 정규화 레시피의 모든 생성 필드를 SerializedObject를 통해 대상 설정에 기록합니다.
        private static void ApplyRecipeSnapshot(
            SerializedObject serialized,
            DungeonRecipeSnapshot snapshot)
        {
            serialized.FindProperty("stageWidthCells").intValue = snapshot.stageWidthCells;
            serialized.FindProperty("stageDepthCells").intValue = snapshot.stageDepthCells;
            serialized.FindProperty("cellSize").floatValue = snapshot.cellSize;
            serialized.FindProperty("wallHeight").floatValue = snapshot.wallHeight;
            serialized.FindProperty("desiredRoomCount").intValue = snapshot.desiredRoomCount;
            serialized.FindProperty("roomSizeMin").vector2IntValue = snapshot.roomSizeMin;
            serialized.FindProperty("roomSizeMax").vector2IntValue = snapshot.roomSizeMax;
            serialized.FindProperty("roomPlacementAttempts").intValue = snapshot.roomPlacementAttempts;
            serialized.FindProperty("corridorWidthCells").intValue = snapshot.corridorWidthCells;
            serialized.FindProperty("extraConnectionChance").floatValue = snapshot.extraConnectionChance;
            serialized.FindProperty("specialGimmickCount").intValue = snapshot.specialGimmickCount;
            serialized.FindProperty("contentSpacingCells").intValue = snapshot.contentSpacingCells;
            serialized.FindProperty("reservedEntranceRadiusCells").intValue = snapshot.reservedEntranceRadiusCells;
            ApplyDensitySnapshot(serialized.FindProperty("enemyProfile"), snapshot.enemyProfile);
            ApplyDensitySnapshot(serialized.FindProperty("destructibleProfile"), snapshot.destructibleProfile);
            ApplyDensitySnapshot(serialized.FindProperty("propProfile"), snapshot.propProfile);
        }

        // 중첩 밀도 프로필의 수치와 AnimationCurve를 독립 인스턴스로 직렬화합니다.
        private static void ApplyDensitySnapshot(
            SerializedProperty property,
            DungeonDensityProfileSnapshot snapshot)
        {
            DungeonDensityProfileSnapshot source = snapshot ?? new DungeonDensityProfileSnapshot();
            property.FindPropertyRelative("baseDensity").floatValue = source.baseDensity;
            property.FindPropertyRelative("overProgression").animationCurveValue =
                source.overProgression != null
                    ? source.overProgression.ToAnimationCurve()
                    : AnimationCurve.Linear(0f, 1f, 1f, 1f);
            property.FindPropertyRelative("roomBias").floatValue = source.roomBias;
            property.FindPropertyRelative("clustering").floatValue = source.clustering;
            property.FindPropertyRelative("maxCount").intValue = source.maxCount;
        }

        // 실제 설정을 바꾸기 전에 저장 레시피·시드·버전·catalog가 같은 Blueprint를 재현하는지 계산합니다.
        private static void VerifyStoredRecipeRegeneration(
            DungeonBlueprintAsset blueprintAsset,
            DungeonRecipeSnapshot snapshot,
            DungeonContentCatalog contentCatalog)
        {
            DungeonBlueprint saved = blueprintAsset.blueprint;
            if (!DungeonGeneratorVersions.IsSupported(saved.generatorVersion))
                throw new NotSupportedException("Unsupported generator version: " + saved.generatorVersion);

            RogueDungeonSettings temporary = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                snapshot.ApplyTo(temporary);
                DungeonGenerationRequest request = saved.generatorVersion == DungeonGeneratorVersions.StableV2
                    ? DungeonGenerationRequest.CreateStableV2(
                        temporary,
                        saved.seed,
                        contentCatalog,
                        "editor-recipe-verify")
                    : DungeonGenerationRequest.Create(
                        temporary,
                        saved.seed,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                        "editor-recipe-verify");
                DungeonBlueprint regenerated = DungeonBlueprintGenerator.Generate(request).Blueprint;
                string regeneratedHash = DungeonBlueprintHasher.Compute(regenerated);
                string savedHash = DungeonBlueprintHasher.Compute(saved);
                if (!string.Equals(regeneratedHash, savedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Stored recipe cannot reproduce the saved Blueprint. regenerated=" +
                        regeneratedHash + ", saved=" + savedHash);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        // 저장 시각과 제작 메모만 갱신한 깊은 복사본을 만들고 논리 hash를 다시 확정합니다.
        private static DungeonBlueprint PrepareForStorage(
            DungeonBlueprint source,
            string authoringNote)
        {
            DungeonBlueprint copy = source.DeepClone();
            copy.createdUtcTicks = DateTime.UtcNow.Ticks;
            copy.authoringNote = authoringNote ?? string.Empty;
            copy.RefreshHash();
            return copy;
        }

        // generatorVersion과 recipe/catalog planning hash가 같은 생성 provenance인지 확인합니다.
        private static bool HasSameProvenance(
            DungeonBlueprint left,
            DungeonBlueprint right)
        {
            return left.generatorVersion == right.generatorVersion &&
                   string.Equals(left.recipeHash, right.recipeHash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       left.catalogPlanningHash,
                       right.catalogPlanningHash,
                       StringComparison.OrdinalIgnoreCase);
        }

        // 새 자산 경로가 Assets 아래의 비어 있는 .asset 경로인지 검사합니다.
        private static string ValidateNewAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path is empty.", nameof(assetPath));
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(normalized), ".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Asset path must be an .asset path under Assets/.",
                    nameof(assetPath));
            }
            string folder = Path.GetDirectoryName(normalized);
            if (string.IsNullOrEmpty(folder) ||
                !AssetDatabase.IsValidFolder(folder.Replace('\\', '/')))
            {
                throw new DirectoryNotFoundException("Asset folder does not exist: " + folder);
            }
            if (AssetDatabase.AssetPathExists(normalized))
                throw new IOException("Asset path already exists: " + normalized);
            return normalized;
        }

        // 오류가 있는 검증 리포트를 authoring 예외로 변환합니다.
        private static void ThrowIfInvalid(
            string message,
            DungeonValidationReport validation)
        {
            if (validation != null && validation.IsValid) return;
            throw new DungeonStageAuthoringException(message, validation);
        }

        // 한 검증 리포트의 issue를 순서와 코드 그대로 대상 리포트에 병합합니다.
        private static void Merge(
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
    }
}
