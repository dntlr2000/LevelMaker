using System;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed partial class RogueDungeonLabWindow
    {
        [SerializeField] private DungeonBlueprintAsset _selectedBlueprint;
        [SerializeField] private DungeonContentCatalog _stageAssetCatalog;
        [SerializeField] private DungeonMissingContentPolicy _stageAssetMissingPolicy =
            DungeonMissingContentPolicy.BuiltInFallback;
        [SerializeField] private string _stageAssetAuthoringNote = string.Empty;
        [SerializeField] private bool _assignCreatedStageDefinition = true;
        [SerializeField] private bool _createdStageLoadsOnPlay = true;
        [SerializeField] private DungeonStageDefinition _lastCreatedStageDefinition;
        [NonSerialized] private DungeonBlueprint _proceduralComparisonSource;
        [NonSerialized] private string _proceduralComparisonHash = string.Empty;
        [NonSerialized] private int _previewReturnSeed;
        [NonSerialized] private bool _hasPreviewReturnSeed;
        [NonSerialized] private RogueDungeonGenerator _stageAssetBoundGenerator;
        [NonSerialized] private DungeonStageDefinition _stageAssetBoundDefinition;
        private bool _currentValidationFold = true;
        private bool _savedValidationFold = true;

        // 새 Generator 바인딩에서 기존 StageDefinition의 Blueprint·catalog·정책을 제작 UI 기본값으로 가져옵니다.
        private void BindStageAssetDefaults()
        {
            DungeonStageDefinition currentDefinition = _generator != null
                ? _generator.stageDefinition
                : null;
            bool generatorChanged = _stageAssetBoundGenerator != _generator;
            bool definitionChanged = _stageAssetBoundDefinition != currentDefinition;
            if (!generatorChanged && !definitionChanged) return;
            _stageAssetBoundGenerator = _generator;
            _stageAssetBoundDefinition = currentDefinition;
            if (generatorChanged)
            {
                _proceduralComparisonSource = null;
                _proceduralComparisonHash = string.Empty;
                _hasPreviewReturnSeed = false;
            }
            if (_generator == null || _generator.stageDefinition == null) return;

            DungeonStageDefinition definition = _generator.stageDefinition;
            _stageAssetCatalog = definition.contentCatalog;
            _stageAssetMissingPolicy = definition.missingContentPolicy;
            if (definition.sourceMode == DungeonStageSourceMode.SavedBlueprint &&
                definition.savedBlueprint != null)
            {
                _selectedBlueprint = definition.savedBlueprint;
                _stageAssetAuthoringNote = definition.savedBlueprint.blueprint != null
                    ? definition.savedBlueprint.blueprint.authoringNote ?? string.Empty
                    : string.Empty;
            }
        }

        // 현재 결과 저장, 저장본 미리보기·비교와 StageDefinition 생성 도구를 한 탭에 표시합니다.
        private void StageAssetsTab()
        {
            BindStageAssetDefaults();
            CaptureProceduralComparisonSource();
            EditorGUILayout.HelpBox(
                "현재 생성 결과를 Blueprint 자산으로 확정하고, 저장본을 검증·미리보기한 뒤 SavedBlueprint StageDefinition을 만들 수 있습니다.",
                MessageType.Info);

            DrawCurrentBlueprintSummary();
            Section("저장 대상과 콘텐츠 해석");
            EditorGUI.BeginChangeCheck();
            DungeonBlueprintAsset selected = (DungeonBlueprintAsset)EditorGUILayout.ObjectField(
                "선택 Blueprint",
                _selectedBlueprint,
                typeof(DungeonBlueprintAsset),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedBlueprint = selected;
                _stageAssetAuthoringNote = selected != null && selected.blueprint != null
                    ? selected.blueprint.authoringNote ?? string.Empty
                    : string.Empty;
            }
            _stageAssetCatalog = (DungeonContentCatalog)EditorGUILayout.ObjectField(
                "콘텐츠 카탈로그",
                _stageAssetCatalog,
                typeof(DungeonContentCatalog),
                false);
            _stageAssetMissingPolicy = (DungeonMissingContentPolicy)EditorGUILayout.EnumPopup(
                "누락 콘텐츠 정책",
                _stageAssetMissingPolicy);
            EditorGUILayout.LabelField("제작 메모");
            _stageAssetAuthoringNote = EditorGUILayout.TextArea(
                _stageAssetAuthoringNote ?? string.Empty,
                GUILayout.MinHeight(46f));

            DungeonBlueprint current = _generator.CurrentBlueprint;
            DungeonValidationReport currentValidation = current != null
                ? DungeonStageAuthoringService.ValidateBlueprint(
                    current,
                    _stageAssetCatalog,
                    _stageAssetMissingPolicy)
                : null;
            DungeonValidationReport savedValidation =
                _selectedBlueprint != null && _selectedBlueprint.blueprint != null
                    ? DungeonStageAuthoringService.ValidateBlueprint(
                        _selectedBlueprint.blueprint,
                        _stageAssetCatalog,
                        _stageAssetMissingPolicy)
                    : null;
            DungeonRecipeSnapshot currentRecipeSnapshot = CaptureRecipeForCurrentBlueprint();

            Section("Blueprint 저장");
            if (current != null)
            {
                EditorGUILayout.HelpBox(
                    currentRecipeSnapshot != null
                        ? "현재 결과와 일치하는 레시피 설정을 Blueprint에 함께 저장합니다."
                        : "현재 결과의 recipe hash와 일치하는 설정을 찾지 못했습니다. 맵은 저장할 수 있지만 설정 복원 정보는 포함되지 않습니다.",
                    currentRecipeSnapshot != null ? MessageType.Info : MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(current == null || currentValidation == null || !currentValidation.IsValid))
            {
                if (GUILayout.Button("현재 결과를 새 Blueprint로 저장", GUILayout.Height(32f)))
                    SaveCurrentAsNewBlueprint();
            }
            using (new EditorGUI.DisabledScope(
                       current == null ||
                       _selectedBlueprint == null ||
                       currentValidation == null ||
                       !currentValidation.IsValid))
            {
                if (GUILayout.Button("선택 Blueprint 덮어쓰기", GUILayout.Height(28f)))
                    ConfirmAndOverwriteBlueprint();
            }

            Section("저장본 미리보기");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                       _selectedBlueprint == null ||
                       savedValidation == null ||
                       !savedValidation.IsValid))
            {
                if (GUILayout.Button("저장본 미리보기", GUILayout.Height(30f)))
                    PreviewSelectedBlueprint();
            }
            using (new EditorGUI.DisabledScope(!CanPreviewProcedural()))
            {
                if (GUILayout.Button("현재 절차 설정으로 재생성", GUILayout.Height(30f)))
                    PreviewProceduralSource();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "현재 절차 설정으로 재생성은 미리보기 전 시드만 복원하며, 설정값 자체는 되돌리지 않습니다.",
                EditorStyles.wordWrappedMiniLabel);

            Section("저장 레시피 설정 복원");
            DrawStoredRecipeRestore(savedValidation);

            Section("절차 원본과 저장본 비교");
            DrawComparison();

            Section("StageDefinition 생성");
            _assignCreatedStageDefinition = EditorGUILayout.ToggleLeft(
                "생성 후 현재 Generator에 연결",
                _assignCreatedStageDefinition);
            _createdStageLoadsOnPlay = EditorGUILayout.ToggleLeft(
                "새 StageDefinition의 Play 진입 자동 로드",
                _createdStageLoadsOnPlay);
            using (new EditorGUI.DisabledScope(
                       _selectedBlueprint == null ||
                       savedValidation == null ||
                       !savedValidation.IsValid))
            {
                if (GUILayout.Button("SavedBlueprint StageDefinition 생성", GUILayout.Height(32f)))
                    CreateSavedStageDefinition();
            }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "현재 Generator Definition",
                    _generator.stageDefinition,
                    typeof(DungeonStageDefinition),
                    false);
                EditorGUILayout.ObjectField(
                    "마지막 생성 Definition",
                    _lastCreatedStageDefinition,
                    typeof(DungeonStageDefinition),
                    false);
            }

            DrawStageBakeSection();

            Section("검증 리포트");
            _currentValidationFold = EditorGUILayout.Foldout(
                _currentValidationFold,
                "현재 결과",
                true);
            if (_currentValidationFold)
                DrawValidationReport(currentValidation, current == null ? "현재 생성 결과가 없습니다." : null);
            _savedValidationFold = EditorGUILayout.Foldout(
                _savedValidationFold,
                "선택 저장본",
                true);
            if (_savedValidationFold)
                DrawValidationReport(savedValidation, _selectedBlueprint == null ? "Blueprint 자산을 선택하세요." : null);
        }

        // 현재 Generator Blueprint의 출처·크기·콘텐츠 개수와 논리 hash를 요약합니다.
        private void DrawCurrentBlueprintSummary()
        {
            Section("현재 생성 결과");
            DungeonBlueprint blueprint = _generator.CurrentBlueprint;
            if (blueprint == null)
            {
                EditorGUILayout.HelpBox(
                    "먼저 생성 탭에서 던전을 만들거나 StageDefinition을 로드하세요.",
                    MessageType.None);
                return;
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawBlueprintFields(blueprint);
            EditorGUILayout.EndVertical();
        }

        // 현재 Procedural 결과가 바뀔 때만 비교용 깊은 복사본과 복귀 시드를 갱신합니다.
        private void CaptureProceduralComparisonSource()
        {
            DungeonStageInstance instance = _generator != null
                ? _generator.CurrentStageInstance
                : null;
            DungeonBlueprint current = _generator != null ? _generator.CurrentBlueprint : null;
            if (instance == null ||
                instance.SourceMode != DungeonStageSourceMode.Procedural ||
                current == null) return;
            string hash = DungeonBlueprintHasher.Compute(current);
            if (string.Equals(hash, _proceduralComparisonHash, StringComparison.OrdinalIgnoreCase)) return;
            _proceduralComparisonSource = current.DeepClone();
            _proceduralComparisonHash = hash;
            _previewReturnSeed = current.seed;
            _hasPreviewReturnSeed = true;
        }

        // 현재 결과를 사용자가 선택한 새 .asset 경로에 저장하고 선택 상태를 갱신합니다.
        private void SaveCurrentAsNewBlueprint()
        {
            DungeonBlueprint current = _generator.CurrentBlueprint;
            string defaultName = current != null
                ? "DungeonBlueprint_" + current.seed
                : "DungeonBlueprint";
            string path = EditorUtility.SaveFilePanelInProject(
                "현재 결과를 새 Blueprint로 저장",
                defaultName,
                "asset",
                "Assets 아래 저장 경로를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                _selectedBlueprint = DungeonStageAuthoringService.CreateBlueprintAsset(
                    current,
                    path,
                    _stageAssetAuthoringNote,
                    _stageAssetCatalog,
                    _stageAssetMissingPolicy,
                    CaptureRecipeForCurrentBlueprint());
                Selection.activeObject = _selectedBlueprint;
                EditorGUIUtility.PingObject(_selectedBlueprint);
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("Blueprint 저장 실패", exception);
            }
        }

        // 경로와 hash가 표시된 확인 창을 거친 뒤 선택 Blueprint를 Undo 가능한 방식으로 덮어씁니다.
        private void ConfirmAndOverwriteBlueprint()
        {
            string path = AssetDatabase.GetAssetPath(_selectedBlueprint);
            string before = _selectedBlueprint.blueprint != null
                ? ShortHash(_selectedBlueprint.blueprint.blueprintHash)
                : "없음";
            string after = _generator.CurrentBlueprint != null
                ? ShortHash(DungeonBlueprintHasher.Compute(_generator.CurrentBlueprint))
                : "없음";
            DungeonRecipeSnapshot recipeSnapshot = CaptureRecipeForCurrentBlueprint();
            bool confirmed = EditorUtility.DisplayDialog(
                "Blueprint 덮어쓰기 확인",
                path + "\n\n현재 저장 hash: " + before + "\n새 결과 hash: " + after +
                "\n설정 복원 정보: " + (recipeSnapshot != null ? "함께 저장" : "제외") +
                "\n\n이 작업은 Unity Undo로 되돌릴 수 있습니다.",
                "덮어쓰기",
                "취소");
            if (!confirmed) return;
            try
            {
                DungeonStageAuthoringService.OverwriteBlueprintAsset(
                    _selectedBlueprint,
                    _generator.CurrentBlueprint,
                    _stageAssetAuthoringNote,
                    _stageAssetCatalog,
                    _stageAssetMissingPolicy,
                    recipeSnapshot);
                EditorGUIUtility.PingObject(_selectedBlueprint);
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("Blueprint 덮어쓰기 실패", exception);
            }
        }

        // 선택 Blueprint를 현재 Generator의 generated root에 RuntimeBuild로 미리보기합니다.
        private void PreviewSelectedBlueprint()
        {
            if (_generator.CurrentStageInstance != null &&
                _generator.CurrentStageInstance.SourceMode == DungeonStageSourceMode.Procedural)
            {
                CaptureProceduralComparisonSource();
            }
            try
            {
                DungeonStageAuthoringService.PreviewSavedBlueprint(
                    _generator,
                    _selectedBlueprint,
                    _stageAssetCatalog,
                    _stageAssetMissingPolicy);
                FocusGeneratedBounds();
                RepaintAll();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("저장본 미리보기 실패", exception);
            }
        }

        // 미리보기 전에 기억한 시드로 Procedural StageDefinition 또는 settings 결과를 다시 구축합니다.
        private void PreviewProceduralSource()
        {
            int seed = _hasPreviewReturnSeed
                ? _previewReturnSeed
                : _settings != null ? _settings.seed : _generator.ActiveSeed;
            try
            {
                DungeonStageAuthoringService.PreviewProcedural(_generator, seed);
                CaptureProceduralComparisonSource();
                FocusGeneratedBounds();
                RepaintAll();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("현재 절차 설정 재생성 실패", exception);
            }
        }

        // 선택 Blueprint의 저장 레시피 상태와 적용 대상, 복원·재생성 버튼을 표시합니다.
        private void DrawStoredRecipeRestore(DungeonValidationReport savedValidation)
        {
            DungeonStoredRecipeValidation recipeValidation =
                DungeonStageAuthoringService.ValidateStoredRecipe(_selectedBlueprint);
            RogueDungeonSettings targetSettings = GetRecipeRestoreTarget();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "적용 대상 설정",
                    targetSettings,
                    typeof(RogueDungeonSettings),
                    false);
            }

            MessageType recipeMessageType;
            EditorGUILayout.HelpBox(
                StoredRecipeMessage(recipeValidation, out recipeMessageType),
                recipeMessageType);
            if (recipeValidation.IsValid)
            {
                EditorGUILayout.LabelField("저장 Recipe hash", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    recipeValidation.SnapshotHash,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            if (targetSettings == null)
            {
                EditorGUILayout.HelpBox(
                    "현재 Generator에 RogueDungeonSettings가 없어 설정을 덮어쓸 수 없습니다.",
                    MessageType.Warning);
            }

            string catalogMessage;
            bool catalogMatches = HasMatchingRegenerationCatalog(out catalogMessage);
            if (recipeValidation.IsValid && !catalogMatches)
                EditorGUILayout.HelpBox(catalogMessage, MessageType.Warning);

            bool canApply = recipeValidation.IsValid &&
                            targetSettings != null &&
                            savedValidation != null &&
                            savedValidation.IsValid;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button("레시피 설정만 불러오기 (현재 시드 유지)", GUILayout.Height(28f)))
                    ConfirmAndApplyStoredRecipe(targetSettings);
            }
            using (new EditorGUI.DisabledScope(!canApply || !catalogMatches))
            {
                if (GUILayout.Button("레시피 + 저장 시드 적용 후 절차 생성", GUILayout.Height(32f)))
                    ConfirmApplyStoredRecipeAndGenerate(targetSettings);
            }
        }

        // 현재 설정 자산의 생성 필드만 덮어쓸지 확인하고 Undo 가능한 복원을 실행합니다.
        private void ConfirmAndApplyStoredRecipe(RogueDungeonSettings targetSettings)
        {
            string settingsPath = AssetDatabase.GetAssetPath(targetSettings);
            bool confirmed = EditorUtility.DisplayDialog(
                "저장 레시피 설정 불러오기",
                (string.IsNullOrEmpty(settingsPath) ? targetSettings.name : settingsPath) +
                "\n\n스테이지 생성 필드와 밀도 곡선을 저장본 값으로 덮어씁니다." +
                "\n현재 시드와 드랍·런타임 옵션은 유지하며 Unity Undo로 되돌릴 수 있습니다.",
                "설정 불러오기",
                "취소");
            if (!confirmed) return;
            try
            {
                DungeonStageAuthoringService.ApplyStoredRecipeToSettings(
                    _selectedBlueprint,
                    targetSettings,
                    false);
                _pending = false;
                if (_serialized != null && _serialized.targetObject == targetSettings)
                    _serialized.Update();
                RepaintAll();
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("저장 레시피 적용 실패", exception);
            }
        }

        // 레시피와 저장 시드를 함께 적용할지 확인하고 동일 버전·catalog의 절차 결과를 생성합니다.
        private void ConfirmApplyStoredRecipeAndGenerate(RogueDungeonSettings targetSettings)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "저장 레시피로 절차 생성",
                "현재 생성 설정과 시드를 저장본 값으로 덮어쓰고 던전을 다시 생성합니다." +
                "\n\n저장 Blueprint 자체를 미리보기하는 것이 아니라 저장 당시 입력으로 절차 생성합니다.",
                "적용 후 생성",
                "취소");
            if (!confirmed) return;
            try
            {
                DungeonStageAuthoringService.ApplyStoredRecipeAndGenerate(
                    _generator,
                    _selectedBlueprint,
                    targetSettings,
                    _stageAssetCatalog,
                    _stageAssetMissingPolicy);
                _pending = false;
                if (_serialized != null && _serialized.targetObject == targetSettings)
                    _serialized.Update();
                CaptureProceduralComparisonSource();
                FocusGeneratedBounds();
                RepaintAll();
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("저장 레시피 절차 생성 실패", exception);
            }
        }

        // 현재 결과의 recipe hash와 일치하는 활성 설정 또는 기존 저장 스냅샷을 찾아 캡처합니다.
        private DungeonRecipeSnapshot CaptureRecipeForCurrentBlueprint()
        {
            DungeonBlueprint current = _generator != null ? _generator.CurrentBlueprint : null;
            if (current == null) return null;

            DungeonStageInstance instance = _generator.CurrentStageInstance;
            DungeonRecipeSnapshot snapshot = CaptureMatchingRecipe(
                instance != null &&
                instance.Definition != null &&
                instance.Definition.sourceMode == DungeonStageSourceMode.Procedural
                    ? instance.Definition.recipe
                    : null,
                current);
            if (snapshot != null) return snapshot;

            snapshot = CaptureMatchingRecipe(_settings, current);
            if (snapshot != null) return snapshot;
            snapshot = CaptureMatchingRecipe(_generator.settings, current);
            if (snapshot != null) return snapshot;

            if (_selectedBlueprint != null &&
                _selectedBlueprint.blueprint != null &&
                string.Equals(
                    DungeonBlueprintHasher.Compute(_selectedBlueprint.blueprint),
                    DungeonBlueprintHasher.Compute(current),
                    StringComparison.OrdinalIgnoreCase) &&
                DungeonStageAuthoringService.ValidateStoredRecipe(_selectedBlueprint).IsValid &&
                _selectedBlueprint.TryGetAuthoringRecipeSnapshot(out snapshot))
            {
                return snapshot;
            }
            return null;
        }

        // 후보 설정을 정규화해 현재 Blueprint recipe hash와 같을 때만 저장용 스냅샷으로 반환합니다.
        private static DungeonRecipeSnapshot CaptureMatchingRecipe(
            RogueDungeonSettings settings,
            DungeonBlueprint current)
        {
            if (settings == null || current == null) return null;
            DungeonRecipeSnapshot snapshot = DungeonRecipeSnapshot.Capture(settings);
            return string.Equals(
                snapshot.ComputeHash(),
                current.recipeHash,
                StringComparison.OrdinalIgnoreCase)
                ? snapshot
                : null;
        }

        // 현재 생성 UI가 편집하는 설정을 우선하고 없으면 Procedural Definition의 recipe를 사용합니다.
        private RogueDungeonSettings GetRecipeRestoreTarget()
        {
            if (_settings != null) return _settings;
            if (_generator != null && _generator.settings != null) return _generator.settings;
            DungeonStageDefinition definition = _generator != null ? _generator.stageDefinition : null;
            return definition != null && definition.sourceMode == DungeonStageSourceMode.Procedural
                ? definition.recipe
                : null;
        }

        // 저장 Blueprint의 generatorVersion과 catalog planning hash를 현재 선택과 비교합니다.
        private bool HasMatchingRegenerationCatalog(out string message)
        {
            message = string.Empty;
            DungeonBlueprint blueprint = _selectedBlueprint != null
                ? _selectedBlueprint.blueprint
                : null;
            if (blueprint == null)
            {
                message = "Blueprint 자산을 선택하세요.";
                return false;
            }
            if (!DungeonGeneratorVersions.IsSupported(blueprint.generatorVersion))
            {
                message = "저장본 생성기 버전을 현재 코드가 지원하지 않습니다.";
                return false;
            }

            try
            {
                string currentCatalogHash = blueprint.generatorVersion == DungeonGeneratorVersions.StableV2
                    ? _stageAssetCatalog != null
                        ? _stageAssetCatalog.ComputePlanningHash()
                        : DungeonBuiltInContentKeys.StableCatalogPlanningHash
                    : DungeonBuiltInContentKeys.LegacyCatalogPlanningHash;
                if (string.Equals(
                    currentCatalogHash,
                    blueprint.catalogPlanningHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                message = "현재 콘텐츠 카탈로그 hash가 저장본과 달라 동일 절차 결과를 재현할 수 없습니다.";
                return false;
            }
            catch (Exception exception)
            {
                message = "콘텐츠 카탈로그 hash를 계산할 수 없습니다: " + exception.Message;
                return false;
            }
        }

        // 저장 레시피 검증 상태를 기존 자산 호환 안내 또는 손상 오류 메시지로 변환합니다.
        private static string StoredRecipeMessage(
            DungeonStoredRecipeValidation validation,
            out MessageType messageType)
        {
            switch (validation.State)
            {
                case DungeonStoredRecipeState.Valid:
                    messageType = MessageType.Info;
                    return "저장 당시 레시피 설정이 있으며 Blueprint recipe hash와 일치합니다.";
                case DungeonStoredRecipeState.UnsupportedFormat:
                    messageType = MessageType.Error;
                    return "저장 레시피 버전을 현재 코드가 지원하지 않아 설정에 적용할 수 없습니다.";
                case DungeonStoredRecipeState.HashMismatch:
                    messageType = MessageType.Error;
                    return "저장 레시피 hash가 Blueprint 출처 hash와 달라 설정 적용을 차단했습니다.";
                case DungeonStoredRecipeState.NonCanonical:
                    messageType = MessageType.Error;
                    return "저장 레시피 값이 현재 정규화 규칙과 맞지 않아 설정 적용을 차단했습니다.";
                default:
                    messageType = MessageType.Warning;
                    return "이 자산은 설정 스냅샷이 없는 기존 R5 Blueprint입니다. 맵 미리보기와 로드는 가능하지만 설정 복원은 지원하지 않습니다.";
            }
        }

        // 저장본 자산 옆에 SavedBlueprint RuntimeBuild용 StageDefinition을 만들고 선택적으로 Generator에 연결합니다.
        private void CreateSavedStageDefinition()
        {
            string blueprintPath = AssetDatabase.GetAssetPath(_selectedBlueprint);
            string folder = string.IsNullOrEmpty(blueprintPath)
                ? "Assets"
                : System.IO.Path.GetDirectoryName(blueprintPath).Replace('\\', '/');
            string defaultName = _selectedBlueprint.name + "_StageDefinition";
            string path = EditorUtility.SaveFilePanelInProject(
                "SavedBlueprint StageDefinition 생성",
                defaultName,
                "asset",
                "StageDefinition 저장 경로를 선택하세요.",
                folder);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                DungeonStageDefinition definition =
                    DungeonStageAuthoringService.CreateStageDefinitionAsset(
                        _selectedBlueprint,
                        path,
                        _stageAssetCatalog,
                        _stageAssetMissingPolicy,
                        _createdStageLoadsOnPlay);
                _lastCreatedStageDefinition = definition;
                if (_assignCreatedStageDefinition)
                    DungeonStageAuthoringService.AssignStageDefinition(_generator, definition);
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("StageDefinition 생성 실패", exception);
            }
        }

        // 기억한 Procedural 원본과 선택 저장본의 provenance·hash 차이를 한국어 상태와 필드 표로 표시합니다.
        private void DrawComparison()
        {
            DungeonBlueprint source = _proceduralComparisonSource ?? _generator.CurrentBlueprint;
            DungeonBlueprintComparison comparison = DungeonStageAuthoringService.Compare(
                source,
                _selectedBlueprint);
            MessageType messageType;
            string message = ComparisonMessage(comparison.State, out messageType);
            EditorGUILayout.HelpBox(message, messageType);
            if (source == null || _selectedBlueprint == null || _selectedBlueprint.blueprint == null) return;

            DungeonBlueprint saved = _selectedBlueprint.blueprint;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawComparisonRow("", "절차 원본", "저장본");
            DrawComparisonRow("시드", source.seed.ToString(), saved.seed.ToString());
            DrawComparisonRow("생성기 버전", source.generatorVersion.ToString(), saved.generatorVersion.ToString());
            DrawComparisonRow("Recipe hash", source.recipeHash, saved.recipeHash);
            DrawComparisonRow("Catalog hash", source.catalogPlanningHash, saved.catalogPlanningHash);
            DrawComparisonRow("Blueprint hash", source.blueprintHash, saved.blueprintHash);
            EditorGUILayout.EndVertical();
        }

        // 양쪽 Blueprint 필드를 동일한 두 열에 배치해 차이를 빠르게 비교합니다.
        private static void DrawComparisonRow(string label, string current, string saved)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110f));
            EditorGUILayout.SelectableLabel(current ?? string.Empty, GUILayout.Height(18f));
            EditorGUILayout.SelectableLabel(saved ?? string.Empty, GUILayout.Height(18f));
            EditorGUILayout.EndHorizontal();
        }

        // 비교 enum을 stale 심각도에 맞는 한국어 안내와 HelpBox 종류로 변환합니다.
        private static string ComparisonMessage(
            DungeonBlueprintComparisonState state,
            out MessageType messageType)
        {
            switch (state)
            {
                case DungeonBlueprintComparisonState.Identical:
                    messageType = MessageType.Info;
                    return "절차 원본과 저장본의 논리 결과가 같습니다.";
                case DungeonBlueprintComparisonState.DifferentSeed:
                    messageType = MessageType.Info;
                    return "생성 입력은 같지만 시드가 다른 별도 결과입니다.";
                case DungeonBlueprintComparisonState.StaleInputs:
                    messageType = MessageType.Warning;
                    return "현재 recipe, catalog 또는 생성기 버전이 저장 시점과 달라 저장본이 stale 상태입니다.";
                case DungeonBlueprintComparisonState.Diverged:
                    messageType = MessageType.Error;
                    return "같은 provenance와 시드인데 Blueprint 결과가 달라졌습니다. 알고리즘 버전 또는 저장 무결성을 점검하세요.";
                case DungeonBlueprintComparisonState.InvalidCurrent:
                    messageType = MessageType.Error;
                    return "현재 절차 결과의 검증 오류로 비교할 수 없습니다.";
                case DungeonBlueprintComparisonState.InvalidSaved:
                    messageType = MessageType.Error;
                    return "선택 저장본의 검증 오류로 비교할 수 없습니다.";
                default:
                    messageType = MessageType.None;
                    return "현재 결과와 Blueprint 자산을 준비하면 비교할 수 있습니다.";
            }
        }

        // Blueprint의 주요 저장 메타데이터와 구성 개수를 에디터 표로 표시합니다.
        private static void DrawBlueprintFields(DungeonBlueprint blueprint)
        {
            EditorGUILayout.LabelField("시드 / 생성기 버전", blueprint.seed + " / " + blueprint.generatorVersion);
            if (blueprint.grid != null)
                EditorGUILayout.LabelField("그리드 / 셀 크기", blueprint.grid.width + "×" + blueprint.grid.depth + " / " + blueprint.grid.cellSize.ToString("0.###") + "m");
            int roomCount = blueprint.rooms != null ? blueprint.rooms.Count : 0;
            int cellCount = blueprint.cells != null ? blueprint.cells.Count : 0;
            int spawnCount = blueprint.spawns != null ? blueprint.spawns.Count : 0;
            EditorGUILayout.LabelField("방 / 셀 / Spawn", roomCount + " / " + cellCount + " / " + spawnCount);
            DrawHashField("Recipe hash", blueprint.recipeHash);
            DrawHashField("Catalog hash", blueprint.catalogPlanningHash);
            DrawHashField("Blueprint hash", blueprint.blueprintHash);
            if (blueprint.createdUtcTicks > 0L)
            {
                try
                {
                    DateTime local = new DateTime(blueprint.createdUtcTicks, DateTimeKind.Utc).ToLocalTime();
                    EditorGUILayout.LabelField("저장 시각", local.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                catch (ArgumentOutOfRangeException)
                {
                    EditorGUILayout.LabelField("저장 시각", "잘못된 timestamp");
                }
            }
        }

        // 긴 hash를 잘리지 않은 선택 가능 문자열로 표시해 복사와 외부 비교를 지원합니다.
        private static void DrawHashField(string label, string hash)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110f));
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(hash) ? "없음" : hash,
                GUILayout.Height(18f));
            EditorGUILayout.EndHorizontal();
        }

        // 검증 issue의 code·메시지·선택적 셀과 spawn ID를 HelpBox 목록으로 표시합니다.
        private static void DrawValidationReport(
            DungeonValidationReport report,
            string unavailableMessage)
        {
            if (report == null)
            {
                EditorGUILayout.HelpBox(unavailableMessage ?? "검증할 데이터가 없습니다.", MessageType.None);
                return;
            }
            if (report.issues.Count == 0)
            {
                EditorGUILayout.HelpBox("검증 오류와 경고가 없습니다.", MessageType.Info);
                return;
            }
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue == null) continue;
                string location = issue.hasCell ? " cell=" + issue.cell : string.Empty;
                if (!string.IsNullOrEmpty(issue.spawnId)) location += " spawn=" + issue.spawnId;
                EditorGUILayout.HelpBox(
                    issue.code + ": " + issue.message + location,
                    issue.severity == DungeonValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        // Procedural 원본으로 복귀할 수 있는 settings 또는 Procedural Definition이 있는지 확인합니다.
        private bool CanPreviewProcedural()
        {
            return _generator != null &&
                   (_generator.settings != null ||
                    (_generator.stageDefinition != null &&
                     _generator.stageDefinition.sourceMode == DungeonStageSourceMode.Procedural));
        }

        // Scene View가 방금 구축한 던전 Bounds를 프레임하도록 요청합니다.
        private void FocusGeneratedBounds()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) view.Frame(_generator.GeneratedBounds, false);
        }

        // 전체 hash를 툴팁 대신 표 안에서 식별 가능한 12자리로 줄입니다.
        private static string ShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return "없음";
            return hash.Length <= 12 ? hash : hash.Substring(0, 12);
        }

        // 제작 예외를 Console과 사용자 대화상자에 함께 보고합니다.
        private static void ShowAuthoringFailure(string title, Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(title, exception.Message, "확인");
        }
    }
}
