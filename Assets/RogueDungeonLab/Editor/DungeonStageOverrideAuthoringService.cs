using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public enum DungeonStageOverrideChangeKind
    {
        Disabled = 0,
        Added = 1,
        Content = 2,
        Transform = 3
    }

    public sealed class DungeonStageOverrideChangeView
    {
        public DungeonStageOverrideChangeKind Kind { get; private set; }
        public string SpawnId { get; private set; }
        public string Description { get; private set; }

        // Override 변경 목록 한 행에 필요한 종류·ID·설명을 읽기 전용으로 묶습니다.
        public DungeonStageOverrideChangeView(
            DungeonStageOverrideChangeKind kind,
            string spawnId,
            string description)
        {
            Kind = kind;
            SpawnId = spawnId ?? string.Empty;
            Description = description ?? string.Empty;
        }
    }

    public sealed class DungeonStageOverrideSpawnView
    {
        public string SpawnId { get; internal set; }
        public bool IsAdded { get; internal set; }
        public bool IsDisabled { get; internal set; }
        public DungeonSpawnCategory Category { get; internal set; }
        public string BaseContentKey { get; internal set; }
        public string ContentKey { get; internal set; }
        public Vector2Int Cell { get; internal set; }
        public Vector3 LocalPosition { get; internal set; }
        public Vector3 LocalEulerAngles { get; internal set; }
        public Vector3 LocalScale { get; internal set; }
        public bool HasContentOverride { get; internal set; }
        public bool HasTransformOverride { get; internal set; }

        // 선택 Spawn 편집기에 표시할 안전한 기본값을 만듭니다.
        internal DungeonStageOverrideSpawnView()
        {
            SpawnId = string.Empty;
            BaseContentKey = string.Empty;
            ContentKey = string.Empty;
            LocalScale = Vector3.one;
        }
    }

    public static class DungeonStageOverrideAuthoringService
    {
        private const string BaseBlueprintProperty = "baseBlueprint";
        private const string BaseHashProperty = "baseBlueprintHash";
        private const string DisabledSpawnsProperty = "disabledSpawns";
        private const string AddedSpawnsProperty = "addedSpawns";
        private const string ContentOverridesProperty = "contentOverrides";
        private const string TransformOverridesProperty = "transformOverrides";

        // 검증된 저장 Blueprint를 기준으로 새 Override 자산을 만들고 즉시 Undo와 저장 대상으로 등록합니다.
        public static DungeonStageOverrides CreateStageOverridesAsset(
            DungeonBlueprintAsset baseBlueprint,
            string assetPath,
            string authoringNote = "")
        {
            if (baseBlueprint == null || baseBlueprint.blueprint == null)
                throw new ArgumentNullException(nameof(baseBlueprint));
            DungeonValidationReport blueprintValidation =
                DungeonBlueprintValidator.Validate(baseBlueprint.blueprint);
            if (!blueprintValidation.IsValid)
                throw new InvalidOperationException(
                    "검증 오류가 있는 Blueprint에는 Stage Override를 만들 수 없습니다.");

            string normalizedPath = NormalizeNewAssetPath(assetPath);
            DungeonStageOverrides overrides =
                ScriptableObject.CreateInstance<DungeonStageOverrides>();
            overrides.name = System.IO.Path.GetFileNameWithoutExtension(normalizedPath);
            AssetDatabase.CreateAsset(overrides, normalizedPath);
            Undo.RegisterCreatedObjectUndo(overrides, "던전 Stage Override 생성");

            SerializedObject serialized = new SerializedObject(overrides);
            serialized.Update();
            serialized.FindProperty(BaseBlueprintProperty).objectReferenceValue =
                baseBlueprint;
            serialized.FindProperty(BaseHashProperty).stringValue =
                DungeonBlueprintHasher.Compute(baseBlueprint.blueprint);
            serialized.FindProperty("authoringNote").stringValue =
                authoringNote ?? string.Empty;
            serialized.ApplyModifiedProperties();
            overrides.RefreshHash();
            EditorUtility.SetDirty(overrides);
            AssetDatabase.SaveAssetIfDirty(overrides);
            return overrides;
        }

        // StageDefinition의 Override 참조를 SerializedObject로 원자적이고 Undo 가능하게 교체합니다.
        public static void AssignStageOverrides(
            DungeonStageDefinition definition,
            DungeonStageOverrides overrides)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (overrides != null &&
                definition.savedBlueprint != null &&
                overrides.baseBlueprint != definition.savedBlueprint)
            {
                throw new InvalidOperationException(
                    "StageDefinition과 Stage Override가 서로 다른 Blueprint를 참조합니다.");
            }

            Undo.RecordObject(definition, "던전 Stage Override 연결");
            SerializedObject serialized = new SerializedObject(definition);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty("stageOverrides");
            if (property == null)
                throw new InvalidOperationException(
                    "DungeonStageDefinition.stageOverrides 필드를 찾을 수 없습니다.");
            property.objectReferenceValue = overrides;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)))
                AssetDatabase.SaveAssetIfDirty(definition);
        }

        // 선택한 원본과 Override를 Generator의 RuntimeBuild 경로로 전체 재구축해 미리보기합니다.
        public static DungeonStageInstance PreviewOverrides(
            RogueDungeonGenerator generator,
            DungeonBlueprintAsset baseBlueprint,
            DungeonStageOverrides overrides,
            DungeonContentCatalog contentCatalog = null,
            DungeonMissingContentPolicy missingContentPolicy =
                DungeonMissingContentPolicy.BuiltInFallback)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            ValidatePairOrThrow(baseBlueprint, overrides);
            generator.LoadSavedBlueprint(
                baseBlueprint,
                contentCatalog,
                missingContentPolicy,
                "editor-override-preview",
                overrides);
            SceneView.RepaintAll();
            return generator.CurrentStageInstance;
        }

        // 현재 StageInstance가 Override 입력으로 구축된 결과인지 데이터 참조로 판정합니다.
        public static bool IsOverridePreviewActive(RogueDungeonGenerator generator)
        {
            return generator != null &&
                   generator.CurrentStageInstance != null &&
                   generator.CurrentStageInstance.AppliedOverrides != null;
        }

        // 현재 Override를 선택 Blueprint에 대해 코드 기반 리포트로 검증합니다.
        public static DungeonValidationReport Validate(
            DungeonStageOverrides overrides,
            DungeonBlueprintAsset expectedBase = null)
        {
            return DungeonStageOverridesValidator.Validate(
                overrides,
                expectedBase,
                true);
        }

        // 현재 Override와 후보 원본을 변경하지 않고 exact·유일 후보 재결합 계획을 계산합니다.
        public static DungeonStageOverrideRebindPlan AnalyzeRebind(
            DungeonStageOverrides overrides,
            DungeonBlueprintAsset candidateBase)
        {
            return DungeonStageOverrideRebaser.Analyze(
                overrides,
                candidateBase);
        }

        // 사용자가 승인한 재결합 계획의 모든 Binding과 기준 원본을 하나의 Undo 단위로 반영합니다.
        public static void CommitRebind(
            DungeonStageOverrideRebindPlan approvedPlan)
        {
            if (approvedPlan == null)
                throw new ArgumentNullException(nameof(approvedPlan));
            if (approvedPlan.StageOverrides == null ||
                approvedPlan.CandidateBase == null ||
                approvedPlan.CandidateBase.blueprint == null)
            {
                throw new InvalidOperationException(
                    "재결합 계획의 Override 또는 후보 Blueprint가 없습니다.");
            }
            if (!approvedPlan.CanCommit)
                throw new InvalidOperationException(
                    "미해결 재결합 충돌이 있어 계획을 적용할 수 없습니다.");

            DungeonStageOverrideRebindPlan fresh =
                DungeonStageOverrideRebaser.Analyze(
                    approvedPlan.StageOverrides,
                    approvedPlan.CandidateBase);
            if (!fresh.CanCommit || !PlansEquivalent(approvedPlan, fresh))
            {
                throw new InvalidOperationException(
                    "분석 뒤 Override 또는 후보 Blueprint가 변경되었습니다. 재결합 분석을 다시 실행하세요.");
            }

            DungeonStageOverrides overrides = approvedPlan.StageOverrides;
            Undo.RegisterCompleteObjectUndo(
                overrides,
                "던전 Stage Override 재결합 승인");
            SerializedObject serialized = new SerializedObject(overrides);
            serialized.Update();
            ApplyRebindEntries(
                serialized.FindProperty(DisabledSpawnsProperty),
                fresh.Entries,
                DungeonStageOverrideOperationKind.Disable);
            ApplyRebindEntries(
                serialized.FindProperty(ContentOverridesProperty),
                fresh.Entries,
                DungeonStageOverrideOperationKind.Content);
            ApplyRebindEntries(
                serialized.FindProperty(TransformOverridesProperty),
                fresh.Entries,
                DungeonStageOverrideOperationKind.Transform);
            serialized.FindProperty(BaseBlueprintProperty).objectReferenceValue =
                fresh.CandidateBase;
            serialized.FindProperty(BaseHashProperty).stringValue =
                fresh.CandidateBaseHash;
            serialized.ApplyModifiedProperties();
            overrides.RefreshHash();
            EditorUtility.SetDirty(overrides);
            AssetDatabase.SaveAssetIfDirty(overrides);
        }

        // 현재 generated root 아래의 선택 오브젝트를 가장 가까운 stable Spawn identity로 해석합니다.
        public static DungeonSpawnIdentity ResolveSelectedIdentity(
            RogueDungeonGenerator generator,
            GameObject selectedObject)
        {
            if (generator == null ||
                generator.CurrentStageInstance == null ||
                generator.CurrentStageInstance.Root == null ||
                selectedObject == null)
            {
                return null;
            }

            DungeonSpawnIdentity identity =
                selectedObject.GetComponentInParent<DungeonSpawnIdentity>();
            if (identity == null) return null;
            Transform root = generator.CurrentStageInstance.Root.transform;
            return identity.transform == root || identity.transform.IsChildOf(root)
                ? identity
                : null;
        }

        // stable Spawn ID로 현재 generated root의 새 identity를 찾아 재구축 뒤 선택 복원을 지원합니다.
        public static DungeonSpawnIdentity FindPreviewIdentity(
            RogueDungeonGenerator generator,
            string spawnId)
        {
            if (generator == null ||
                generator.CurrentStageInstance == null ||
                generator.CurrentStageInstance.Root == null ||
                string.IsNullOrEmpty(spawnId))
            {
                return null;
            }

            DungeonSpawnIdentity[] identities =
                generator.CurrentStageInstance.Root
                    .GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                if (identities[i] != null &&
                    string.Equals(
                        identities[i].SpawnId,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return identities[i];
                }
            }
            return null;
        }

        // 원본과 Override 목록을 합쳐 선택 Spawn의 현재 비파괴 편집 상태를 계산합니다.
        public static DungeonStageOverrideSpawnView GetSpawnView(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (overrides == null ||
                overrides.baseBlueprint == null ||
                overrides.baseBlueprint.blueprint == null ||
                string.IsNullOrEmpty(spawnId))
            {
                return null;
            }

            DungeonSpawnRecord added = FindSpawn(overrides.addedSpawns, spawnId);
            if (added != null)
                return CreateAddedSpawnView(added);

            DungeonSpawnRecord source =
                FindSpawn(overrides.baseBlueprint.blueprint.spawns, spawnId);
            if (source == null) return null;

            DungeonStageOverrideSpawnView view = CreateBaseSpawnView(source);
            view.IsDisabled = FindDisabledIndex(overrides, spawnId) >= 0;
            int contentIndex = FindContentIndex(overrides, spawnId);
            if (contentIndex >= 0)
            {
                view.HasContentOverride = true;
                view.ContentKey =
                    overrides.contentOverrides[contentIndex].replacementContentKey ??
                    string.Empty;
            }
            int transformIndex = FindTransformIndex(overrides, spawnId);
            if (transformIndex >= 0)
            {
                DungeonSpawnTransformOverride transform =
                    overrides.transformOverrides[transformIndex];
                view.HasTransformOverride = true;
                view.LocalPosition = transform.localPosition;
                view.LocalEulerAngles = new Vector3(
                    transform.pitchDegrees,
                    transform.yawDegrees,
                    transform.rollDegrees);
                view.LocalScale = transform.localScale;
            }
            return view;
        }

        // 모든 Override 작업을 사용자가 제거·선택할 수 있는 안정적인 표시 순서로 변환합니다.
        public static List<DungeonStageOverrideChangeView> GetChanges(
            DungeonStageOverrides overrides)
        {
            List<DungeonStageOverrideChangeView> changes =
                new List<DungeonStageOverrideChangeView>();
            if (overrides == null) return changes;

            for (int i = 0; i < overrides.disabledSpawns.Count; i++)
            {
                DungeonSpawnDisableOverride operation = overrides.disabledSpawns[i];
                string id = BindingId(operation != null ? operation.binding : null);
                changes.Add(new DungeonStageOverrideChangeView(
                    DungeonStageOverrideChangeKind.Disabled,
                    id,
                    "원본 Spawn 비활성화"));
            }
            for (int i = 0; i < overrides.addedSpawns.Count; i++)
            {
                DungeonSpawnRecord added = overrides.addedSpawns[i];
                changes.Add(new DungeonStageOverrideChangeView(
                    DungeonStageOverrideChangeKind.Added,
                    added != null ? added.spawnId : string.Empty,
                    added != null
                        ? "추가 Spawn · " + added.category + " · " +
                          (added.contentKey ?? string.Empty)
                        : "손상된 추가 Spawn"));
            }
            for (int i = 0; i < overrides.contentOverrides.Count; i++)
            {
                DungeonSpawnContentOverride operation =
                    overrides.contentOverrides[i];
                changes.Add(new DungeonStageOverrideChangeView(
                    DungeonStageOverrideChangeKind.Content,
                    BindingId(operation != null ? operation.binding : null),
                    "콘텐츠 교체 → " +
                    (operation != null
                        ? operation.replacementContentKey ?? string.Empty
                        : string.Empty)));
            }
            for (int i = 0; i < overrides.transformOverrides.Count; i++)
            {
                DungeonSpawnTransformOverride operation =
                    overrides.transformOverrides[i];
                changes.Add(new DungeonStageOverrideChangeView(
                    DungeonStageOverrideChangeKind.Transform,
                    BindingId(operation != null ? operation.binding : null),
                    operation != null
                        ? "절대 Transform · P " + operation.localPosition +
                          " · R " + new Vector3(
                              operation.pitchDegrees,
                              operation.yawDegrees,
                              operation.rollDegrees) +
                          " · S " + operation.localScale
                        : "손상된 Transform Override"));
            }
            changes.Sort(CompareChanges);
            return changes;
        }

        // 원본 Spawn의 활성 상태를 disable 작업의 추가·삭제로 기록합니다.
        public static void SetDisabled(
            DungeonStageOverrides overrides,
            string spawnId,
            bool disabled)
        {
            DungeonSpawnRecord source = RequireBaseSpawn(overrides, spawnId);
            if (source.category == DungeonSpawnCategory.Marker)
                throw new InvalidOperationException(
                    "R7 Spawn 편집 단계에서는 입구·출구 Marker를 비활성화할 수 없습니다.");

            Mutate(
                overrides,
                disabled ? "던전 Spawn 비활성화" : "던전 Spawn 다시 활성화",
                delegate(SerializedObject serialized)
                {
                    SerializedProperty list =
                        serialized.FindProperty(DisabledSpawnsProperty);
                    int index = FindBindingIndex(list, spawnId);
                    if (disabled)
                    {
                        if (index < 0)
                        {
                            index = list.arraySize;
                            list.InsertArrayElementAtIndex(index);
                            SerializedProperty operation =
                                list.GetArrayElementAtIndex(index);
                            operation.FindPropertyRelative("recordId").stringValue =
                                CreateOperationRecordId();
                            WriteBinding(
                                operation.FindPropertyRelative("binding"),
                                source);
                        }
                        DeleteAllBindings(
                            serialized.FindProperty(ContentOverridesProperty),
                            spawnId);
                        DeleteAllBindings(
                            serialized.FindProperty(TransformOverridesProperty),
                            spawnId);
                    }
                    else if (!disabled && index >= 0)
                    {
                        list.DeleteArrayElementAtIndex(index);
                    }
                });
        }

        // 선택 Spawn의 콘텐츠 key를 추가 Spawn 본문 또는 원본 대상 content 작업으로 기록합니다.
        public static void SetContent(
            DungeonStageOverrides overrides,
            string spawnId,
            string replacementContentKey)
        {
            if (string.IsNullOrWhiteSpace(replacementContentKey))
                throw new ArgumentException(
                    "대체 콘텐츠 key를 입력하세요.",
                    nameof(replacementContentKey));

            int addedIndex = FindAddedIndex(overrides, spawnId);
            if (addedIndex >= 0)
            {
                Mutate(
                    overrides,
                    "추가 던전 Spawn 콘텐츠 변경",
                    delegate(SerializedObject serialized)
                    {
                        SerializedProperty added =
                            serialized.FindProperty(AddedSpawnsProperty)
                                .GetArrayElementAtIndex(addedIndex);
                        added.FindPropertyRelative("contentKey").stringValue =
                            replacementContentKey.Trim();
                    });
                return;
            }

            DungeonSpawnRecord source = RequireBaseSpawn(overrides, spawnId);
            if (source.category == DungeonSpawnCategory.Marker)
                throw new InvalidOperationException(
                    "R7 Spawn 편집 단계에서는 입구·출구 Marker 콘텐츠를 교체할 수 없습니다.");
            string replacement = replacementContentKey.Trim();
            if (FindDisabledIndex(overrides, spawnId) >= 0 &&
                !string.Equals(
                    replacement,
                    source.contentKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "비활성화된 Spawn의 콘텐츠는 교체할 수 없습니다. 먼저 Spawn을 다시 활성화하세요.");
            }
            Mutate(
                overrides,
                "던전 Spawn 콘텐츠 교체",
                delegate(SerializedObject serialized)
                {
                    SerializedProperty list =
                        serialized.FindProperty(ContentOverridesProperty);
                    int index = FindBindingIndex(list, spawnId);
                    if (string.Equals(
                            replacement,
                            source.contentKey,
                            StringComparison.Ordinal))
                    {
                        if (index >= 0) list.DeleteArrayElementAtIndex(index);
                        return;
                    }
                    if (index < 0)
                    {
                        index = list.arraySize;
                        list.InsertArrayElementAtIndex(index);
                        list.GetArrayElementAtIndex(index)
                            .FindPropertyRelative("recordId").stringValue =
                            CreateOperationRecordId();
                    }
                    SerializedProperty operation = list.GetArrayElementAtIndex(index);
                    WriteBinding(operation.FindPropertyRelative("binding"), source);
                    operation.FindPropertyRelative("replacementContentKey")
                        .stringValue = replacement;
                });
        }

        // 선택 Spawn의 콘텐츠 교체 작업만 제거하고 원본 key로 복귀합니다.
        public static void ResetContent(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (FindAddedIndex(overrides, spawnId) >= 0)
                throw new InvalidOperationException(
                    "추가 Spawn은 원본 콘텐츠가 없으므로 유효한 콘텐츠 key를 직접 지정해야 합니다.");
            RequireBaseSpawn(overrides, spawnId);
            RemoveBindingOperation(
                overrides,
                ContentOverridesProperty,
                spawnId,
                "던전 Spawn 콘텐츠 교체 해제");
        }

        // 선택 Spawn의 절대 로컬 Transform을 추가 Spawn 본문 또는 transform 작업으로 기록합니다.
        public static void SetTransform(
            DungeonStageOverrides overrides,
            string spawnId,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            ValidateTransform(localPosition, localEulerAngles, localScale);
            int addedIndex = FindAddedIndex(overrides, spawnId);
            if (addedIndex >= 0)
            {
                Mutate(
                    overrides,
                    "추가 던전 Spawn Transform 변경",
                    delegate(SerializedObject serialized)
                    {
                        SerializedProperty added =
                            serialized.FindProperty(AddedSpawnsProperty)
                                .GetArrayElementAtIndex(addedIndex);
                        WriteSpawnTransform(
                            added,
                            localPosition,
                            localEulerAngles,
                            localScale);
                    });
                return;
            }

            DungeonSpawnRecord source = RequireBaseSpawn(overrides, spawnId);
            if (source.category == DungeonSpawnCategory.Marker)
                throw new InvalidOperationException(
                    "R7 Spawn 편집 단계에서는 입구·출구 Marker Transform을 변경할 수 없습니다.");
            Vector3 sourceEuler = new Vector3(
                source.pitchDegrees,
                source.yawDegrees,
                source.rollDegrees);
            bool isOriginal =
                source.localPosition == localPosition &&
                sourceEuler == localEulerAngles &&
                source.localScale == localScale;
            if (FindDisabledIndex(overrides, spawnId) >= 0 &&
                !isOriginal)
            {
                throw new InvalidOperationException(
                    "비활성화된 Spawn의 Transform은 변경할 수 없습니다. 먼저 Spawn을 다시 활성화하세요.");
            }

            Mutate(
                overrides,
                "던전 Spawn Transform 변경",
                delegate(SerializedObject serialized)
                {
                    SerializedProperty list =
                        serialized.FindProperty(TransformOverridesProperty);
                    int index = FindBindingIndex(list, spawnId);
                    if (isOriginal)
                    {
                        if (index >= 0) list.DeleteArrayElementAtIndex(index);
                        return;
                    }
                    if (index < 0)
                    {
                        index = list.arraySize;
                        list.InsertArrayElementAtIndex(index);
                        list.GetArrayElementAtIndex(index)
                            .FindPropertyRelative("recordId").stringValue =
                            CreateOperationRecordId();
                    }
                    SerializedProperty operation = list.GetArrayElementAtIndex(index);
                    WriteBinding(operation.FindPropertyRelative("binding"), source);
                    operation.FindPropertyRelative("localPosition").vector3Value =
                        localPosition;
                    operation.FindPropertyRelative("pitchDegrees").floatValue =
                        localEulerAngles.x;
                    operation.FindPropertyRelative("yawDegrees").floatValue =
                        localEulerAngles.y;
                    operation.FindPropertyRelative("rollDegrees").floatValue =
                        localEulerAngles.z;
                    operation.FindPropertyRelative("localScale").vector3Value =
                        localScale;
                });
        }

        // 선택 원본 Spawn의 transform 작업만 제거해 Blueprint의 절대 Transform으로 복귀합니다.
        public static void ResetTransform(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (FindAddedIndex(overrides, spawnId) >= 0)
                throw new InvalidOperationException(
                    "추가 Spawn은 원본 Transform이 없으므로 유효한 값을 직접 지정해야 합니다.");
            RequireBaseSpawn(overrides, spawnId);
            RemoveBindingOperation(
                overrides,
                TransformOverridesProperty,
                spawnId,
                "던전 Spawn Transform 변경 해제");
        }

        // 지정 floor cell과 절대 Transform으로 수동 Spawn을 추가하고 충돌하지 않는 영구 ID를 부여합니다.
        public static string CreateAddedSpawn(
            DungeonStageOverrides overrides,
            DungeonSpawnCategory category,
            string contentKey,
            Vector2Int cell,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            RequirePair(overrides);
            if (category == DungeonSpawnCategory.Marker)
                throw new InvalidOperationException(
                    "R7 Spawn 편집 단계에서는 Marker 추가를 지원하지 않습니다.");
            if (string.IsNullOrWhiteSpace(contentKey))
                throw new ArgumentException("추가 Spawn 콘텐츠 key를 입력하세요.");
            ValidateTransform(localPosition, localEulerAngles, localScale);

            DungeonCellRecord floor = FindFloorCell(
                overrides.baseBlueprint.blueprint,
                cell);
            if (floor == null)
                throw new InvalidOperationException(
                    "추가 Spawn cell은 원본 Blueprint의 floor cell이어야 합니다.");

            string guid = Guid.NewGuid().ToString("N");
            string spawnId = "override:v1:" + guid;
            int maxDistance = MaxFloorDistance(overrides.baseBlueprint.blueprint);
            int variantSeed = unchecked((int)uint.Parse(
                guid.Substring(0, 8),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture));

            DungeonSpawnRecord added = new DungeonSpawnRecord
            {
                spawnId = spawnId,
                category = category,
                contentKey = contentKey.Trim(),
                instanceName = contentKey.Trim() + "_Manual",
                cell = cell,
                localPosition = localPosition,
                pitchDegrees = localEulerAngles.x,
                yawDegrees = localEulerAngles.y,
                rollDegrees = localEulerAngles.z,
                localScale = localScale,
                roomId = floor.roomId ?? string.Empty,
                progression = maxDistance > 0
                    ? Mathf.Clamp01((float)floor.distanceFromEntrance / maxDistance)
                    : 0f,
                tags = new List<string>(),
                variantSeed = variantSeed
            };

            Mutate(
                overrides,
                "수동 던전 Spawn 추가",
                delegate(SerializedObject serialized)
                {
                    SerializedProperty list =
                        serialized.FindProperty(AddedSpawnsProperty);
                    int index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    WriteSpawn(list.GetArrayElementAtIndex(index), added);
                });
            return spawnId;
        }

        // 수동 추가 Spawn과 그 ID를 잘못 참조하는 잔여 작업을 하나의 Undo 단위로 제거합니다.
        public static void DeleteAddedSpawn(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            int addedIndex = FindAddedIndex(overrides, spawnId);
            if (addedIndex < 0)
                throw new InvalidOperationException("삭제할 추가 Spawn을 찾을 수 없습니다.");
            Mutate(
                overrides,
                "수동 던전 Spawn 삭제",
                delegate(SerializedObject serialized)
                {
                    serialized.FindProperty(AddedSpawnsProperty)
                        .DeleteArrayElementAtIndex(addedIndex);
                    DeleteAllBindings(
                        serialized.FindProperty(DisabledSpawnsProperty),
                        spawnId);
                    DeleteAllBindings(
                        serialized.FindProperty(ContentOverridesProperty),
                        spawnId);
                    DeleteAllBindings(
                        serialized.FindProperty(TransformOverridesProperty),
                        spawnId);
                });
        }

        // 변경 목록의 단일 작업을 종류에 맞는 직렬화 목록에서 Undo 가능하게 제거합니다.
        public static void RemoveChange(
            DungeonStageOverrides overrides,
            DungeonStageOverrideChangeKind kind,
            string spawnId)
        {
            switch (kind)
            {
                case DungeonStageOverrideChangeKind.Disabled:
                    RemoveBindingOperation(
                        overrides,
                        DisabledSpawnsProperty,
                        spawnId,
                        "던전 Spawn 비활성화 해제");
                    break;
                case DungeonStageOverrideChangeKind.Added:
                    DeleteAddedSpawn(overrides, spawnId);
                    break;
                case DungeonStageOverrideChangeKind.Content:
                    RemoveBindingOperation(
                        overrides,
                        ContentOverridesProperty,
                        spawnId,
                        "던전 Spawn 콘텐츠 교체 해제");
                    break;
                case DungeonStageOverrideChangeKind.Transform:
                    RemoveBindingOperation(
                        overrides,
                        TransformOverridesProperty,
                        spawnId,
                        "던전 Spawn Transform 변경 해제");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // 모든 자산 변경을 Undo·SerializedObject·hash 갱신·SetDirty·저장 순서로 처리합니다.
        private static void Mutate(
            DungeonStageOverrides overrides,
            string undoName,
            Action<SerializedObject> mutation)
        {
            RequirePair(overrides);
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            Undo.RegisterCompleteObjectUndo(overrides, undoName);
            SerializedObject serialized = new SerializedObject(overrides);
            serialized.Update();
            mutation(serialized);
            serialized.ApplyModifiedProperties();
            overrides.RefreshHash();
            EditorUtility.SetDirty(overrides);
            AssetDatabase.SaveAssetIfDirty(overrides);
        }

        // 지정 원본과 Override 참조·저장 hash 조합이 미리보기에 적합한지 검사합니다.
        private static void ValidatePairOrThrow(
            DungeonBlueprintAsset baseBlueprint,
            DungeonStageOverrides overrides)
        {
            if (baseBlueprint == null || baseBlueprint.blueprint == null)
                throw new ArgumentNullException(nameof(baseBlueprint));
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            if (overrides.baseBlueprint != baseBlueprint)
                throw new InvalidOperationException(
                    "선택 Blueprint와 Stage Override의 기준 Blueprint가 다릅니다.");
            string currentHash = DungeonBlueprintHasher.Compute(baseBlueprint.blueprint);
            if (!string.Equals(
                    overrides.baseBlueprintHash,
                    currentHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "원본 Blueprint가 변경되어 Stage Override 재결합이 필요합니다.");
            }
        }

        // Override와 기준 Blueprint가 모두 존재하는지 확인하고 기준 쌍을 반환합니다.
        private static void RequirePair(DungeonStageOverrides overrides)
        {
            if (overrides == null) throw new ArgumentNullException(nameof(overrides));
            if (overrides.baseBlueprint == null ||
                overrides.baseBlueprint.blueprint == null)
            {
                throw new InvalidOperationException(
                    "Stage Override에 기준 Blueprint가 연결되어 있지 않습니다.");
            }
        }

        // 원본 Blueprint에서 지정 stable ID를 찾아 작업 Binding 생성에 사용합니다.
        private static DungeonSpawnRecord RequireBaseSpawn(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            RequirePair(overrides);
            DungeonSpawnRecord source = FindSpawn(
                overrides.baseBlueprint.blueprint.spawns,
                spawnId);
            if (source == null)
                throw new InvalidOperationException(
                    "원본 Blueprint에서 Spawn ID를 찾을 수 없습니다: " + spawnId);
            return source;
        }

        // Spawn 목록에서 ordinal ID가 같은 첫 레코드를 반환합니다.
        private static DungeonSpawnRecord FindSpawn(
            List<DungeonSpawnRecord> spawns,
            string spawnId)
        {
            if (spawns == null || string.IsNullOrEmpty(spawnId)) return null;
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonSpawnRecord record = spawns[i];
                if (record != null &&
                    string.Equals(
                        record.spawnId,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return record;
                }
            }
            return null;
        }

        // 현재 Override의 수동 추가 Spawn 배열에서 stable ID 위치를 찾습니다.
        private static int FindAddedIndex(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (overrides == null || overrides.addedSpawns == null) return -1;
            for (int i = 0; i < overrides.addedSpawns.Count; i++)
            {
                DungeonSpawnRecord record = overrides.addedSpawns[i];
                if (record != null &&
                    string.Equals(
                        record.spawnId,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 현재 Override의 disable 작업에서 stable ID 위치를 찾습니다.
        private static int FindDisabledIndex(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (overrides == null || overrides.disabledSpawns == null) return -1;
            for (int i = 0; i < overrides.disabledSpawns.Count; i++)
            {
                DungeonSpawnDisableOverride operation = overrides.disabledSpawns[i];
                if (operation != null &&
                    string.Equals(
                        BindingId(operation.binding),
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 현재 Override의 content 작업에서 stable ID 위치를 찾습니다.
        private static int FindContentIndex(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (overrides == null || overrides.contentOverrides == null) return -1;
            for (int i = 0; i < overrides.contentOverrides.Count; i++)
            {
                DungeonSpawnContentOverride operation =
                    overrides.contentOverrides[i];
                if (operation != null &&
                    string.Equals(
                        BindingId(operation.binding),
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 현재 Override의 transform 작업에서 stable ID 위치를 찾습니다.
        private static int FindTransformIndex(
            DungeonStageOverrides overrides,
            string spawnId)
        {
            if (overrides == null || overrides.transformOverrides == null) return -1;
            for (int i = 0; i < overrides.transformOverrides.Count; i++)
            {
                DungeonSpawnTransformOverride operation =
                    overrides.transformOverrides[i];
                if (operation != null &&
                    string.Equals(
                        BindingId(operation.binding),
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 직렬화 작업 배열에서 binding.spawnId가 같은 첫 인덱스를 찾습니다.
        private static int FindBindingIndex(
            SerializedProperty list,
            string spawnId)
        {
            if (list == null || !list.isArray) return -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty binding =
                    list.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("binding");
                SerializedProperty id =
                    binding != null
                        ? binding.FindPropertyRelative("spawnId")
                        : null;
                if (id != null &&
                    string.Equals(
                        id.stringValue,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 직렬화 작업 배열에서 영구 record ID가 같은 항목을 찾습니다.
        private static int FindRecordIndex(
            SerializedProperty list,
            string recordId)
        {
            if (list == null || !list.isArray) return -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty property =
                    list.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("recordId");
                if (property != null &&
                    string.Equals(
                        property.stringValue,
                        recordId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // 직렬화 작업 배열에서 지정 ID를 참조하는 모든 항목을 역순으로 제거합니다.
        private static void DeleteAllBindings(
            SerializedProperty list,
            string spawnId)
        {
            if (list == null || !list.isArray) return;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty binding =
                    list.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("binding");
                SerializedProperty id =
                    binding != null
                        ? binding.FindPropertyRelative("spawnId")
                        : null;
                if (id != null &&
                    string.Equals(
                        id.stringValue,
                        spawnId,
                        StringComparison.Ordinal))
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }
        }

        // 단일 Binding 작업을 공통 직렬화 변경 경로로 제거합니다.
        private static void RemoveBindingOperation(
            DungeonStageOverrides overrides,
            string propertyName,
            string spawnId,
            string undoName)
        {
            Mutate(
                overrides,
                undoName,
                delegate(SerializedObject serialized)
                {
                    SerializedProperty list =
                        serialized.FindProperty(propertyName);
                    int index = FindBindingIndex(list, spawnId);
                    if (index >= 0) list.DeleteArrayElementAtIndex(index);
                });
        }

        // 원본 Spawn의 재결합 Binding 필드를 SerializedProperty에 복사합니다.
        private static void WriteBinding(
            SerializedProperty binding,
            DungeonSpawnRecord source)
        {
            if (binding == null || source == null)
                throw new ArgumentNullException(
                    binding == null ? nameof(binding) : nameof(source));
            binding.FindPropertyRelative("spawnId").stringValue =
                source.spawnId ?? string.Empty;
            binding.FindPropertyRelative("category").intValue =
                (int)source.category;
            binding.FindPropertyRelative("contentKey").stringValue =
                source.contentKey ?? string.Empty;
            binding.FindPropertyRelative("cell").vector2IntValue =
                source.cell;
            binding.FindPropertyRelative("roomId").stringValue =
                source.roomId ?? string.Empty;
            binding.FindPropertyRelative("variantSeed").intValue =
                source.variantSeed;
        }

        // 런타임 rebind 계획의 제안 Binding을 기존 operation 직렬화 필드에 복사합니다.
        private static void WriteBinding(
            SerializedProperty binding,
            DungeonSpawnBindingSnapshot source)
        {
            if (binding == null || source == null)
                throw new ArgumentNullException(
                    binding == null ? nameof(binding) : nameof(source));
            binding.FindPropertyRelative("spawnId").stringValue =
                source.spawnId ?? string.Empty;
            binding.FindPropertyRelative("category").intValue =
                (int)source.category;
            binding.FindPropertyRelative("contentKey").stringValue =
                source.contentKey ?? string.Empty;
            binding.FindPropertyRelative("cell").vector2IntValue =
                source.cell;
            binding.FindPropertyRelative("roomId").stringValue =
                source.roomId ?? string.Empty;
            binding.FindPropertyRelative("variantSeed").intValue =
                source.variantSeed;
        }

        // 지정 operation 종류의 rebind 제안을 record ID로 찾아 기존 배열에 적용합니다.
        private static void ApplyRebindEntries(
            SerializedProperty list,
            List<DungeonStageOverrideRebindEntry> entries,
            DungeonStageOverrideOperationKind operationKind)
        {
            if (list == null || entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry entry = entries[i];
                if (entry == null ||
                    entry.OperationKind != operationKind ||
                    entry.ProposedBinding == null)
                {
                    continue;
                }
                int index = FindRecordIndex(list, entry.RecordId);
                if (index < 0)
                    throw new InvalidOperationException(
                        "재결합할 Override operation을 찾을 수 없습니다: " +
                        entry.RecordId);
                WriteBinding(
                    list.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("binding"),
                    entry.ProposedBinding);
            }
        }

        // 사용자가 본 계획과 적용 직전 재분석 결과가 항목별로 동일한지 확인합니다.
        private static bool PlansEquivalent(
            DungeonStageOverrideRebindPlan approved,
            DungeonStageOverrideRebindPlan fresh)
        {
            if (approved == null ||
                fresh == null ||
                approved.StageOverrides != fresh.StageOverrides ||
                approved.CandidateBase != fresh.CandidateBase ||
                !string.Equals(
                    approved.CandidateBaseHash,
                    fresh.CandidateBaseHash,
                    StringComparison.Ordinal) ||
                approved.Entries == null ||
                fresh.Entries == null ||
                approved.Entries.Count != fresh.Entries.Count)
            {
                return false;
            }
            for (int i = 0; i < approved.Entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry left = approved.Entries[i];
                DungeonStageOverrideRebindEntry right = fresh.Entries[i];
                if (left == null || right == null)
                {
                    if (!ReferenceEquals(left, right)) return false;
                    continue;
                }
                if (left.OperationKind != right.OperationKind ||
                    left.Status != right.Status ||
                    !string.Equals(
                        left.RecordId,
                        right.RecordId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        left.PreviousSpawnId,
                        right.PreviousSpawnId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        left.ProposedSpawnId,
                        right.ProposedSpawnId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        // 수동 추가 Spawn 전체 필드를 Blueprint 직렬화 계약과 같은 이름으로 기록합니다.
        private static void WriteSpawn(
            SerializedProperty target,
            DungeonSpawnRecord source)
        {
            target.FindPropertyRelative("spawnId").stringValue =
                source.spawnId ?? string.Empty;
            target.FindPropertyRelative("category").intValue =
                (int)source.category;
            target.FindPropertyRelative("contentKey").stringValue =
                source.contentKey ?? string.Empty;
            target.FindPropertyRelative("instanceName").stringValue =
                source.instanceName ?? string.Empty;
            target.FindPropertyRelative("cell").vector2IntValue =
                source.cell;
            WriteSpawnTransform(
                target,
                source.localPosition,
                new Vector3(
                    source.pitchDegrees,
                    source.yawDegrees,
                    source.rollDegrees),
                source.localScale);
            target.FindPropertyRelative("roomId").stringValue =
                source.roomId ?? string.Empty;
            target.FindPropertyRelative("progression").floatValue =
                source.progression;
            SerializedProperty tags = target.FindPropertyRelative("tags");
            tags.arraySize = source.tags != null ? source.tags.Count : 0;
            for (int i = 0; i < tags.arraySize; i++)
                tags.GetArrayElementAtIndex(i).stringValue =
                    source.tags[i] ?? string.Empty;
            target.FindPropertyRelative("variantSeed").intValue =
                source.variantSeed;
        }

        // Spawn 레코드의 절대 위치·Euler·scale을 직렬화 필드에 기록합니다.
        private static void WriteSpawnTransform(
            SerializedProperty target,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            target.FindPropertyRelative("localPosition").vector3Value =
                localPosition;
            target.FindPropertyRelative("pitchDegrees").floatValue =
                localEulerAngles.x;
            target.FindPropertyRelative("yawDegrees").floatValue =
                localEulerAngles.y;
            target.FindPropertyRelative("rollDegrees").floatValue =
                localEulerAngles.z;
            target.FindPropertyRelative("localScale").vector3Value =
                localScale;
        }

        // 원본 Spawn 레코드를 수정되지 않은 선택 편집 화면 값으로 변환합니다.
        private static DungeonStageOverrideSpawnView CreateBaseSpawnView(
            DungeonSpawnRecord source)
        {
            return new DungeonStageOverrideSpawnView
            {
                SpawnId = source.spawnId ?? string.Empty,
                Category = source.category,
                BaseContentKey = source.contentKey ?? string.Empty,
                ContentKey = source.contentKey ?? string.Empty,
                Cell = source.cell,
                LocalPosition = source.localPosition,
                LocalEulerAngles = new Vector3(
                    source.pitchDegrees,
                    source.yawDegrees,
                    source.rollDegrees),
                LocalScale = source.localScale
            };
        }

        // 수동 추가 Spawn 레코드를 원본 없는 선택 편집 화면 값으로 변환합니다.
        private static DungeonStageOverrideSpawnView CreateAddedSpawnView(
            DungeonSpawnRecord added)
        {
            return new DungeonStageOverrideSpawnView
            {
                SpawnId = added.spawnId ?? string.Empty,
                IsAdded = true,
                Category = added.category,
                BaseContentKey = string.Empty,
                ContentKey = added.contentKey ?? string.Empty,
                Cell = added.cell,
                LocalPosition = added.localPosition,
                LocalEulerAngles = new Vector3(
                    added.pitchDegrees,
                    added.yawDegrees,
                    added.rollDegrees),
                LocalScale = added.localScale
            };
        }

        // 원본 Blueprint에서 지정 좌표의 floor cell 레코드를 찾습니다.
        private static DungeonCellRecord FindFloorCell(
            DungeonBlueprint blueprint,
            Vector2Int coordinate)
        {
            if (blueprint == null || blueprint.cells == null) return null;
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord cell = blueprint.cells[i];
                if (cell != null &&
                    (cell.flags & DungeonCellFlags.Floor) != 0 &&
                    cell.coordinate == coordinate)
                {
                    return cell;
                }
            }
            return null;
        }

        // 추가 Spawn progression 계산에 사용할 floor 최대 BFS 거리를 찾습니다.
        private static int MaxFloorDistance(DungeonBlueprint blueprint)
        {
            int max = 0;
            if (blueprint == null || blueprint.cells == null) return max;
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord cell = blueprint.cells[i];
                if (cell != null &&
                    (cell.flags & DungeonCellFlags.Floor) != 0)
                {
                    max = Mathf.Max(max, cell.distanceFromEntrance);
                }
            }
            return max;
        }

        // 위치·회전은 유한하고 scale은 유한한 양수인지 편집 전에 검사합니다.
        private static void ValidateTransform(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (!IsFinite(localPosition) ||
                !IsFinite(localEulerAngles) ||
                !IsFinite(localScale) ||
                localScale.x <= 0f ||
                localScale.y <= 0f ||
                localScale.z <= 0f)
            {
                throw new ArgumentException(
                    "Spawn Transform은 유한한 값과 양수 scale을 사용해야 합니다.");
            }
        }

        // Vector3의 모든 축이 NaN·Infinity가 아닌지 확인합니다.
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        // 단일 float가 NaN·Infinity가 아닌지 확인합니다.
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Binding이 없거나 손상되어도 변경 리포트가 중단되지 않도록 ID를 안전하게 읽습니다.
        private static string BindingId(DungeonSpawnBindingSnapshot binding)
        {
            return binding != null ? binding.spawnId ?? string.Empty : string.Empty;
        }

        // 각 Override operation을 재결합 계획에서 안정적으로 식별할 영구 record ID를 만듭니다.
        private static string CreateOperationRecordId()
        {
            return "override-operation:v1:" + Guid.NewGuid().ToString("N");
        }

        // 변경 목록을 stable ID와 종류 순으로 정렬합니다.
        private static int CompareChanges(
            DungeonStageOverrideChangeView left,
            DungeonStageOverrideChangeView right)
        {
            int result = string.CompareOrdinal(left.SpawnId, right.SpawnId);
            return result != 0
                ? result
                : ((int)left.Kind).CompareTo((int)right.Kind);
        }

        // 새 Override가 Assets 아래의 아직 존재하지 않는 .asset 경로인지 검사합니다.
        private static string NormalizeNewAssetPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Stage Override 경로는 Assets 아래의 .asset 파일이어야 합니다.",
                    nameof(assetPath));
            }
            if (AssetDatabase.LoadMainAssetAtPath(normalized) != null)
                throw new InvalidOperationException(
                    "선택 경로에 이미 자산이 존재합니다: " + normalized);
            string directory =
                System.IO.Path.GetDirectoryName(normalized)
                    .Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
                throw new InvalidOperationException(
                    "Stage Override 저장 폴더가 존재하지 않습니다: " + directory);
            return normalized;
        }
    }
}
