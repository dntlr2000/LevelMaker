using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed partial class RogueDungeonLabWindow
    {
        [SerializeField] private DungeonStageOverrides _selectedStageOverrides;
        [SerializeField] private bool _stageOverridesFoldout = true;
        [SerializeField] private bool _stageOverrideValidationFoldout;
        [SerializeField] private bool _stageOverrideChangesFoldout = true;
        [SerializeField] private bool _stageOverrideRebindFoldout;
        [SerializeField] private DungeonBlueprintAsset _stageOverrideRebindCandidate;
        [SerializeField] private string _stageOverrideDraftSpawnId = string.Empty;
        [SerializeField] private string _stageOverrideContentDraft = string.Empty;
        [SerializeField] private Vector3 _stageOverridePositionDraft;
        [SerializeField] private Vector3 _stageOverrideEulerDraft;
        [SerializeField] private Vector3 _stageOverrideScaleDraft = Vector3.one;
        [SerializeField] private DungeonSpawnCategory _stageOverrideAddCategory =
            DungeonSpawnCategory.Prop;
        [SerializeField] private string _stageOverrideAddContentKey =
            DungeonBuiltInContentKeys.PropCube;
        [SerializeField] private Vector2Int _stageOverrideAddCell;
        [SerializeField] private Vector3 _stageOverrideAddPosition;
        [SerializeField] private Vector3 _stageOverrideAddEuler;
        [SerializeField] private Vector3 _stageOverrideAddScale = Vector3.one;
        [NonSerialized] private DungeonStageOverrideRebindPlan _stageOverrideRebindPlan;

        // 스테이지 자산 탭에 비파괴 Override 생성·검증·선택 편집·재결합 도구를 표시합니다.
        private void DrawStageOverridesSection(DungeonValidationReport savedValidation)
        {
            Section("R7 비파괴 Stage Override");
            _stageOverridesFoldout = EditorGUILayout.Foldout(
                _stageOverridesFoldout,
                "Spawn Override 제작",
                true);
            if (!_stageOverridesFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "저장 Blueprint는 그대로 두고 Spawn 비활성화·추가·콘텐츠 교체·절대 Transform만 별도 자산에 기록합니다. 생성된 계층을 직접 수정하지 마세요.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DungeonStageOverrides selected =
                (DungeonStageOverrides)EditorGUILayout.ObjectField(
                    "Stage Override",
                    _selectedStageOverrides,
                    typeof(DungeonStageOverrides),
                    false);
            if (EditorGUI.EndChangeCheck())
                SelectStageOverrides(selected);

            DrawStageOverrideAssetActions(savedValidation);
            if (_selectedStageOverrides == null)
            {
                EditorGUILayout.HelpBox(
                    "선택 Blueprint를 기준으로 새 Stage Override를 만들거나 기존 자산을 선택하세요.",
                    MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawStageOverrideSummary();
            DungeonValidationReport validation =
                DungeonStageOverrideAuthoringService.Validate(
                    _selectedStageOverrides,
                    _selectedBlueprint);
            DrawStageOverridePreviewActions(validation);

            _stageOverrideValidationFoldout = EditorGUILayout.Foldout(
                _stageOverrideValidationFoldout,
                "Override 검증 리포트",
                true);
            if (_stageOverrideValidationFoldout)
                DrawValidationReport(validation, null);

            bool editable = validation.IsValid &&
                            _selectedStageOverrides.baseBlueprint ==
                            _selectedBlueprint;
            using (new EditorGUI.DisabledScope(!editable))
            {
                DrawSelectedStageOverrideSpawn();
                DrawAddedStageOverrideSpawn();
            }
            DrawStageOverrideChanges();
            if (!editable)
            {
                EditorGUILayout.HelpBox(
                    "원본 hash 또는 Binding이 현재 Blueprint와 맞지 않습니다. 아래 재결합 분석에서 충돌을 해소하고 명시적으로 승인해야 편집·미리보기할 수 있습니다.",
                    MessageType.Warning);
            }
            DrawStageOverrideRebind();
            EditorGUILayout.EndVertical();
        }

        // 선택 Blueprint로 새 Override 자산을 만들고 현재 Definition 연결 상태를 관리합니다.
        private void DrawStageOverrideAssetActions(
            DungeonValidationReport savedValidation)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                       _selectedBlueprint == null ||
                       savedValidation == null ||
                       !savedValidation.IsValid))
            {
                if (GUILayout.Button("새 Override 생성"))
                    CreateStageOverridesAsset();
            }

            DungeonStageDefinition definition = _generator != null
                ? _generator.stageDefinition
                : null;
            bool canAssign = definition != null &&
                             definition.sourceMode ==
                             DungeonStageSourceMode.SavedBlueprint &&
                             definition.savedBlueprint == _selectedBlueprint &&
                             (_selectedStageOverrides == null ||
                              _selectedStageOverrides.baseBlueprint ==
                              _selectedBlueprint);
            using (new EditorGUI.DisabledScope(!canAssign))
            {
                string label = definition != null &&
                               definition.stageOverrides ==
                               _selectedStageOverrides
                    ? "Definition 연결됨"
                    : _selectedStageOverrides == null
                        ? "Definition Override 연결 해제"
                    : "현재 Definition에 연결";
                if (GUILayout.Button(label) &&
                    definition.stageOverrides != _selectedStageOverrides)
                {
                    TryOverrideAuthoring(
                        "Stage Override 연결 실패",
                        delegate
                        {
                            DungeonStageOverrideAuthoringService
                                .AssignStageOverrides(
                                    definition,
                                    _selectedStageOverrides);
                            InvalidateBakeValidation();
                        });
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // 현재 선택 Blueprint를 기준으로 새 Stage Override 자산을 생성하고 선택합니다.
        private void CreateStageOverridesAsset()
        {
            string blueprintPath = AssetDatabase.GetAssetPath(_selectedBlueprint);
            string folder = string.IsNullOrEmpty(blueprintPath)
                ? "Assets"
                : System.IO.Path.GetDirectoryName(blueprintPath)
                    .Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(
                "Stage Override 생성",
                _selectedBlueprint.name + "_Overrides",
                "asset",
                "Stage Override 저장 경로를 선택하세요.",
                folder);
            if (string.IsNullOrEmpty(path)) return;

            TryOverrideAuthoring(
                "Stage Override 생성 실패",
                delegate
                {
                    SelectStageOverrides(
                        DungeonStageOverrideAuthoringService
                            .CreateStageOverridesAsset(
                                _selectedBlueprint,
                                path));
                    Selection.activeObject = _selectedStageOverrides;
                    EditorGUIUtility.PingObject(_selectedStageOverrides);
                });
        }

        // Override 선택 변경 시 Spawn 초안과 재결합 분석 캐시를 초기화합니다.
        private void SelectStageOverrides(DungeonStageOverrides selected)
        {
            _selectedStageOverrides = selected;
            _stageOverrideDraftSpawnId = string.Empty;
            _stageOverrideRebindPlan = null;
            _stageOverrideRebindCandidate = selected != null
                ? selected.baseBlueprint
                : _selectedBlueprint;
            Repaint();
        }

        // 기준 자산과 source·override·final hash를 읽기 전용 요약으로 표시합니다.
        private void DrawStageOverrideSummary()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "기준 Blueprint",
                    _selectedStageOverrides.baseBlueprint,
                    typeof(DungeonBlueprintAsset),
                    false);
            }
            DrawHashField(
                "기준 hash",
                _selectedStageOverrides.baseBlueprintHash);
            DrawHashField(
                "Override hash",
                DungeonStageOverridesHasher.Compute(_selectedStageOverrides));

            DungeonStageOverrideApplyResult result =
                DungeonStageOverrideApplier.Apply(
                    _selectedStageOverrides.baseBlueprint,
                    _selectedStageOverrides);
            DrawHashField(
                "최종 hash",
                result != null ? result.FinalBlueprintHash : string.Empty);
        }

        // 원본 또는 Override 적용 RuntimeBuild 미리보기를 실행하고 활성 상태를 안내합니다.
        private void DrawStageOverridePreviewActions(
            DungeonValidationReport validation)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                       _selectedBlueprint == null ||
                       _generator == null))
            {
                if (GUILayout.Button("원본 미리보기", GUILayout.Height(28f)))
                    PreviewSelectedBlueprint();
            }
            using (new EditorGUI.DisabledScope(
                       _generator == null ||
                       validation == null ||
                       !validation.IsValid))
            {
                if (GUILayout.Button(
                        "Override 미리보기",
                        GUILayout.Height(28f)))
                {
                    PreviewStageOverrides(
                        _stageOverrideDraftSpawnId);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (IsOverridePreviewActive())
            {
                EditorGUILayout.HelpBox(
                    "현재 generated root는 Override가 적용된 미리보기입니다. 모든 변경은 stable Spawn ID로 자산에 기록한 뒤 전체 재구축됩니다.",
                    MessageType.Info);
            }
        }

        // 현재 Scene 선택을 stable Spawn identity로 해석해 비파괴 편집 컨트롤을 표시합니다.
        private void DrawSelectedStageOverrideSpawn()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                "Scene 선택 Spawn 편집",
                EditorStyles.boldLabel);
            DungeonSpawnIdentity identity =
                DungeonStageOverrideAuthoringService.ResolveSelectedIdentity(
                    _generator,
                    Selection.activeGameObject);
            if (identity != null)
                EnsureStageOverrideSpawnDraft(identity.SpawnId);

            if (string.IsNullOrEmpty(_stageOverrideDraftSpawnId))
            {
                EditorGUILayout.HelpBox(
                    "원본 또는 Override 미리보기에서 Spawn 오브젝트를 선택하세요. Floor·Wall·generated root는 편집 대상으로 해석하지 않습니다.",
                    MessageType.None);
                return;
            }

            DungeonStageOverrideSpawnView view =
                DungeonStageOverrideAuthoringService.GetSpawnView(
                    _selectedStageOverrides,
                    _stageOverrideDraftSpawnId);
            if (view == null)
            {
                EditorGUILayout.HelpBox(
                    "선택한 stable Spawn ID가 현재 Override 기준에 없습니다.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Spawn ID",
                view.SpawnId,
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "종류 / Cell",
                view.Category + " / " + view.Cell);
            if (view.IsAdded)
            {
                EditorGUILayout.HelpBox(
                    "Override에서 추가한 Spawn입니다. 삭제하면 이 레코드는 완전히 제거됩니다.",
                    MessageType.Info);
                if (GUILayout.Button("추가 Spawn 삭제"))
                {
                    MutateStageOverride(
                        "추가 Spawn 삭제 실패",
                        view.SpawnId,
                        delegate
                        {
                            DungeonStageOverrideAuthoringService
                                .DeleteAddedSpawn(
                                    _selectedStageOverrides,
                                    view.SpawnId);
                            _stageOverrideDraftSpawnId = string.Empty;
                        });
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(
                           view.Category == DungeonSpawnCategory.Marker))
                {
                    bool disabled = EditorGUILayout.Toggle(
                        "비활성화",
                        view.IsDisabled);
                    if (disabled != view.IsDisabled)
                    {
                        MutateStageOverride(
                            "Spawn 활성 상태 변경 실패",
                            view.SpawnId,
                            delegate
                            {
                                DungeonStageOverrideAuthoringService
                                    .SetDisabled(
                                        _selectedStageOverrides,
                                        view.SpawnId,
                                        disabled);
                            });
                    }
                }
            }

            using (new EditorGUI.DisabledScope(
                       view.Category == DungeonSpawnCategory.Marker))
            {
                _stageOverrideContentDraft = EditorGUILayout.TextField(
                    "콘텐츠 key",
                    _stageOverrideContentDraft ?? string.Empty);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("콘텐츠 적용"))
                {
                    MutateStageOverride(
                        "콘텐츠 교체 실패",
                        view.SpawnId,
                        delegate
                        {
                            DungeonStageOverrideAuthoringService.SetContent(
                                _selectedStageOverrides,
                                view.SpawnId,
                                _stageOverrideContentDraft);
                        });
                }
                using (new EditorGUI.DisabledScope(
                           view.IsAdded || !view.HasContentOverride))
                {
                    if (GUILayout.Button("콘텐츠 원본 복귀"))
                    {
                        MutateStageOverride(
                            "콘텐츠 원본 복귀 실패",
                            view.SpawnId,
                            delegate
                            {
                                DungeonStageOverrideAuthoringService
                                    .ResetContent(
                                        _selectedStageOverrides,
                                        view.SpawnId);
                            });
                    }
                }
                EditorGUILayout.EndHorizontal();

                _stageOverridePositionDraft = EditorGUILayout.Vector3Field(
                    "로컬 위치",
                    _stageOverridePositionDraft);
                _stageOverrideEulerDraft = EditorGUILayout.Vector3Field(
                    "로컬 회전",
                    _stageOverrideEulerDraft);
                _stageOverrideScaleDraft = EditorGUILayout.Vector3Field(
                    "로컬 크기",
                    _stageOverrideScaleDraft);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("절대 Transform 적용"))
                {
                    MutateStageOverride(
                        "Transform 변경 실패",
                        view.SpawnId,
                        delegate
                        {
                            DungeonStageOverrideAuthoringService.SetTransform(
                                _selectedStageOverrides,
                                view.SpawnId,
                                _stageOverridePositionDraft,
                                _stageOverrideEulerDraft,
                                _stageOverrideScaleDraft);
                        });
                }
                using (new EditorGUI.DisabledScope(
                           view.IsAdded || !view.HasTransformOverride))
                {
                    if (GUILayout.Button("Transform 원본 복귀"))
                    {
                        MutateStageOverride(
                            "Transform 원본 복귀 실패",
                            view.SpawnId,
                            delegate
                            {
                                DungeonStageOverrideAuthoringService
                                    .ResetTransform(
                                        _selectedStageOverrides,
                                        view.SpawnId);
                            });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        // Scene 선택이 바뀌면 편집 초안을 해당 Spawn의 현재 합성 값으로 동기화합니다.
        private void EnsureStageOverrideSpawnDraft(string spawnId)
        {
            if (string.IsNullOrEmpty(spawnId) ||
                string.Equals(
                    _stageOverrideDraftSpawnId,
                    spawnId,
                    StringComparison.Ordinal))
            {
                return;
            }
            DungeonStageOverrideSpawnView view =
                DungeonStageOverrideAuthoringService.GetSpawnView(
                    _selectedStageOverrides,
                    spawnId);
            if (view == null) return;
            _stageOverrideDraftSpawnId = spawnId;
            _stageOverrideContentDraft = view.ContentKey;
            _stageOverridePositionDraft = view.LocalPosition;
            _stageOverrideEulerDraft = view.LocalEulerAngles;
            _stageOverrideScaleDraft = view.LocalScale;
        }

        // floor cell과 절대 Transform을 입력해 Override 전용 stable-ID Spawn을 추가합니다.
        private void DrawAddedStageOverrideSpawn()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                "수동 Spawn 추가",
                EditorStyles.boldLabel);
            _stageOverrideAddCategory =
                (DungeonSpawnCategory)EditorGUILayout.EnumPopup(
                    "종류",
                    _stageOverrideAddCategory);
            _stageOverrideAddContentKey = EditorGUILayout.TextField(
                "콘텐츠 key",
                _stageOverrideAddContentKey ?? string.Empty);
            _stageOverrideAddCell = EditorGUILayout.Vector2IntField(
                "Floor cell",
                _stageOverrideAddCell);
            _stageOverrideAddPosition = EditorGUILayout.Vector3Field(
                "로컬 위치",
                _stageOverrideAddPosition);
            _stageOverrideAddEuler = EditorGUILayout.Vector3Field(
                "로컬 회전",
                _stageOverrideAddEuler);
            _stageOverrideAddScale = EditorGUILayout.Vector3Field(
                "로컬 크기",
                _stageOverrideAddScale);

            DungeonSpawnIdentity selectedIdentity =
                DungeonStageOverrideAuthoringService.ResolveSelectedIdentity(
                    _generator,
                    Selection.activeGameObject);
            if (selectedIdentity != null &&
                GUILayout.Button("Scene 선택의 cell·위치 가져오기"))
            {
                _stageOverrideAddCell = selectedIdentity.Cell;
                _stageOverrideAddPosition =
                    selectedIdentity.transform.localPosition;
            }

            using (new EditorGUI.DisabledScope(
                       _stageOverrideAddCategory ==
                       DungeonSpawnCategory.Marker))
            {
                if (GUILayout.Button("Override Spawn 추가", GUILayout.Height(28f)))
                {
                    MutateStageOverride(
                        "Spawn 추가 실패",
                        string.Empty,
                        delegate
                        {
                            _stageOverrideDraftSpawnId =
                                DungeonStageOverrideAuthoringService
                                    .CreateAddedSpawn(
                                        _selectedStageOverrides,
                                        _stageOverrideAddCategory,
                                        _stageOverrideAddContentKey,
                                        _stageOverrideAddCell,
                                        _stageOverrideAddPosition,
                                        _stageOverrideAddEuler,
                                        _stageOverrideAddScale);
                        });
                }
            }
        }

        // 현재 Override 작업을 stable ID 순으로 나열하고 단일 작업 제거를 제공합니다.
        private void DrawStageOverrideChanges()
        {
            List<DungeonStageOverrideChangeView> changes =
                DungeonStageOverrideAuthoringService.GetChanges(
                    _selectedStageOverrides);
            _stageOverrideChangesFoldout = EditorGUILayout.Foldout(
                _stageOverrideChangesFoldout,
                "변경 목록 (" + changes.Count + ")",
                true);
            if (!_stageOverrideChangesFoldout) return;
            if (changes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "기록된 Override 변경이 없습니다.",
                    MessageType.None);
                return;
            }

            for (int i = 0; i < changes.Count; i++)
            {
                DungeonStageOverrideChangeView change = changes[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    change.Kind + " · " + change.SpawnId + "\n" +
                    change.Description,
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("제거", GUILayout.Width(54f)))
                {
                    MutateStageOverride(
                        "Override 변경 제거 실패",
                        change.SpawnId,
                        delegate
                        {
                            DungeonStageOverrideAuthoringService.RemoveChange(
                                _selectedStageOverrides,
                                change.Kind,
                                change.SpawnId);
                        });
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // 새 Blueprint 후보의 exact·유일 의미 후보·충돌 결과를 보여주고 명시적 승인 뒤에만 Binding을 갱신합니다.
        private void DrawStageOverrideRebind()
        {
            _stageOverrideRebindFoldout = EditorGUILayout.Foldout(
                _stageOverrideRebindFoldout,
                "원본 변경 재결합",
                true);
            if (!_stageOverrideRebindFoldout) return;

            EditorGUI.BeginChangeCheck();
            DungeonBlueprintAsset candidate =
                (DungeonBlueprintAsset)EditorGUILayout.ObjectField(
                    "새 원본 후보",
                    _stageOverrideRebindCandidate,
                    typeof(DungeonBlueprintAsset),
                    false);
            if (EditorGUI.EndChangeCheck())
            {
                _stageOverrideRebindCandidate = candidate;
                _stageOverrideRebindPlan = null;
            }
            if (GUILayout.Button("재결합 분석"))
            {
                _stageOverrideRebindPlan =
                    DungeonStageOverrideAuthoringService.AnalyzeRebind(
                        _selectedStageOverrides,
                        _stageOverrideRebindCandidate);
            }
            if (_stageOverrideRebindPlan == null)
            {
                EditorGUILayout.HelpBox(
                    "분석은 자산을 변경하지 않습니다. exact ID를 우선하고, 없을 때만 의미 anchor가 유일한 후보를 제안합니다.",
                    MessageType.None);
                return;
            }

            List<DungeonStageOverrideRebindEntry> entries =
                _stageOverrideRebindPlan.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry entry = entries[i];
                string proposal = string.IsNullOrEmpty(entry.ProposedSpawnId)
                    ? "해결 안 됨"
                    : entry.ProposedSpawnId;
                EditorGUILayout.LabelField(
                    entry.OperationKind + " · " +
                    entry.PreviousSpawnId + " → " + proposal +
                    " · " + entry.Status,
                    EditorStyles.wordWrappedMiniLabel);
            }
            DrawValidationReport(
                _stageOverrideRebindPlan.ValidationReport,
                null);
            using (new EditorGUI.DisabledScope(
                       !_stageOverrideRebindPlan.CanCommit))
            {
                if (GUILayout.Button(
                        "분석 결과 승인 및 재결합",
                        GUILayout.Height(28f)))
                {
                    ConfirmAndCommitStageOverrideRebind();
                }
            }
        }

        // 재결합으로 변경될 기준 hash와 작업 수를 확인받은 뒤 하나의 Undo 단위로 적용합니다.
        private void ConfirmAndCommitStageOverrideRebind()
        {
            if (_stageOverrideRebindPlan == null ||
                !_stageOverrideRebindPlan.CanCommit)
            {
                return;
            }
            bool confirmed = EditorUtility.DisplayDialog(
                "Stage Override 재결합 승인",
                "기준 Blueprint와 " +
                _stageOverrideRebindPlan.Entries.Count +
                "개 Binding 분석 결과를 적용합니다.\n\n새 기준 hash: " +
                _stageOverrideRebindPlan.CandidateBaseHash +
                "\n이 작업은 Unity Undo로 되돌릴 수 있습니다.",
                "승인 및 적용",
                "취소");
            if (!confirmed) return;
            TryOverrideAuthoring(
                "Stage Override 재결합 실패",
                delegate
                {
                    DungeonStageOverrideAuthoringService.CommitRebind(
                        _stageOverrideRebindPlan);
                    _selectedBlueprint =
                        _stageOverrideRebindPlan.CandidateBase;
                    _stageOverrideRebindCandidate = _selectedBlueprint;
                    _stageOverrideRebindPlan = null;
                    _stageOverrideDraftSpawnId = string.Empty;
                    InvalidateBakeValidation();
                });
        }

        // Override 자산 변경 후 활성 Override 미리보기를 전체 재구축하고 stable ID 선택을 복원합니다.
        private void MutateStageOverride(
            string failureTitle,
            string spawnId,
            Action mutation)
        {
            bool refreshPreview = IsOverridePreviewActive();
            TryOverrideAuthoring(
                failureTitle,
                delegate
                {
                    mutation();
                    _stageOverrideRebindPlan = null;
                    InvalidateBakeValidation();
                    string restoreSpawnId =
                        !string.IsNullOrEmpty(spawnId)
                            ? spawnId
                            : _stageOverrideDraftSpawnId;
                    _stageOverrideDraftSpawnId = string.Empty;
                    if (refreshPreview)
                        PreviewStageOverrides(restoreSpawnId);
                    else
                        EnsureStageOverrideSpawnDraft(restoreSpawnId);
                    Repaint();
                });
        }

        // 선택 Override를 RuntimeBuild 경로로 적용하고 재구축 전 stable ID 선택을 가능하면 복원합니다.
        private void PreviewStageOverrides(string preferredSpawnId)
        {
            if (_generator.CurrentStageInstance != null &&
                _generator.CurrentStageInstance.SourceMode ==
                DungeonStageSourceMode.Procedural)
            {
                CaptureProceduralComparisonSource();
            }
            TryOverrideAuthoring(
                "Stage Override 미리보기 실패",
                delegate
                {
                    DungeonStageOverrideAuthoringService.PreviewOverrides(
                        _generator,
                        _selectedBlueprint,
                        _selectedStageOverrides,
                        _stageAssetCatalog,
                        _stageAssetMissingPolicy);
                    DungeonSpawnIdentity identity =
                        DungeonStageOverrideAuthoringService
                            .FindPreviewIdentity(
                                _generator,
                                preferredSpawnId);
                    if (identity != null)
                        Selection.activeGameObject = identity.gameObject;
                    FocusGeneratedBounds();
                    RepaintAll();
                });
        }

        // generated root가 Stage Override 입력으로 구축됐는지 현재 StageInstance 메타데이터로 판정합니다.
        private bool IsOverridePreviewActive()
        {
            return DungeonStageOverrideAuthoringService
                .IsOverridePreviewActive(_generator);
        }

        // 제작 서비스 예외를 공통 대화상자로 보고하면서 에디터 GUI 흐름을 유지합니다.
        private static void TryOverrideAuthoring(
            string failureTitle,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure(failureTitle, exception);
            }
        }
    }
}
