using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed partial class RogueDungeonLabWindow
    {
        [SerializeField] private DungeonStageDefinition _bakeStageDefinition;
        [SerializeField] private DungeonBakeMaterialSet _bakeMaterialSet;
        [SerializeField] private bool _stageBakeFoldout = true;
        [NonSerialized] private DungeonStageDefinition _bakeValidatedDefinition;
        [NonSerialized] private DungeonValidationReport _bakeValidation;
        [NonSerialized] private string _bakeValidationFailure = string.Empty;

        // 스테이지 자산 탭에 SavedBlueprint 전용 Bake 입력, 실행, 최신성 검증과 결과 참조를 표시합니다.
        private void DrawStageBakeSection()
        {
            Section("R6·R7 배포용 Bake");
            bool wasOpen = _stageBakeFoldout;
            _stageBakeFoldout = EditorGUILayout.Foldout(
                _stageBakeFoldout,
                "Mesh·Prefab Bake",
                true);
            if (!_stageBakeFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "저장된 Blueprint와 선택적 Stage Override에서 영속 Mesh와 Prefab을 만듭니다. Procedural 결과는 먼저 SavedBlueprint StageDefinition으로 확정해야 합니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DungeonStageDefinition selectedDefinition =
                (DungeonStageDefinition)EditorGUILayout.ObjectField(
                    "Bake StageDefinition",
                    ResolveBakeStageDefinition(),
                    typeof(DungeonStageDefinition),
                    false);
            if (EditorGUI.EndChangeCheck())
            {
                AssignBakeStageDefinition(selectedDefinition);
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(
                       _generator == null || _generator.stageDefinition == null))
            {
                if (GUILayout.Button("현재 Generator Definition 사용"))
                    AssignBakeStageDefinition(_generator.stageDefinition);
            }
            using (new EditorGUI.DisabledScope(_lastCreatedStageDefinition == null))
            {
                if (GUILayout.Button("마지막 생성 Definition 사용"))
                    AssignBakeStageDefinition(_lastCreatedStageDefinition);
            }
            EditorGUILayout.EndHorizontal();

            DungeonStageDefinition definition = _bakeStageDefinition;
            DrawBakeSourceMessage(definition);

            EditorGUI.BeginChangeCheck();
            _bakeMaterialSet = (DungeonBakeMaterialSet)EditorGUILayout.ObjectField(
                "영속 Bake 재질 세트",
                ResolveBakeMaterialSet(definition),
                typeof(DungeonBakeMaterialSet),
                false);
            if (EditorGUI.EndChangeCheck()) InvalidateBakeValidation();
            DrawBakeMaterialMessage(definition, _bakeMaterialSet);

            if (GUILayout.Button("기본 Bake 재질 세트 자산 생성"))
                CreateDefaultBakeMaterialSet(definition);

            RogueDungeonSettings runtimeSettings = ResolveBakeRuntimeSettings(definition);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "게임플레이 설정 출처",
                    runtimeSettings,
                    typeof(RogueDungeonSettings),
                    false);
            }
            EditorGUILayout.LabelField(
                runtimeSettings != null
                    ? "드랍 테이블과 마커 정책을 Bake 지문과 Prefab에 반영합니다."
                    : "설정 출처가 없으면 Baker의 기본 게임플레이 구성을 사용합니다.",
                EditorStyles.wordWrappedMiniLabel);

            bool canBake = CanBakeStage(definition, _bakeMaterialSet);
            using (new EditorGUI.DisabledScope(!canBake))
            {
                string bakeLabel = HasCommittedBake(definition)
                    ? "재Bake (기존 정상 결과 보존)"
                    : "배포용 Mesh·Prefab Bake";
                if (GUILayout.Button(bakeLabel, GUILayout.Height(32f)))
                    ConfirmAndBakeStage(definition, _bakeMaterialSet, runtimeSettings);
            }

            DrawBakeResultReferences(definition);

            EditorGUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Manifest·최신성 검증", EditorStyles.boldLabel);
            if (GUILayout.Button("최신성 다시 검사", GUILayout.Width(130f)))
                RefreshBakeValidation(definition);
            EditorGUILayout.EndHorizontal();

            if ((!wasOpen || _bakeValidatedDefinition != definition) && definition != null)
                RefreshBakeValidation(definition);
            if (!string.IsNullOrEmpty(_bakeValidationFailure))
                EditorGUILayout.HelpBox(_bakeValidationFailure, MessageType.Error);
            DrawValidationReport(
                _bakeValidation,
                definition == null
                    ? "검증할 StageDefinition을 선택하세요."
                    : "최신성 다시 검사를 실행하세요.");
            EditorGUILayout.EndVertical();
        }

        // 현재 Generator의 Definition을 초기 선택으로 사용하고 기존 manifest 입력을 재질 기본값으로 연결합니다.
        private DungeonStageDefinition ResolveBakeStageDefinition()
        {
            if (_bakeStageDefinition != null) return _bakeStageDefinition;
            DungeonStageDefinition current = _generator != null
                ? _generator.stageDefinition
                : null;
            if (current != null) AssignBakeStageDefinition(current);
            return _bakeStageDefinition;
        }

        // Bake 대상 변경 시 기존 manifest의 재질을 채우고 캐시된 최신성 결과를 폐기합니다.
        private void AssignBakeStageDefinition(DungeonStageDefinition definition)
        {
            _bakeStageDefinition = definition;
            _bakeMaterialSet = definition != null && definition.bakeManifest != null
                ? definition.bakeManifest.materialSet
                : null;
            InvalidateBakeValidation();
        }

        // 사용자가 별도 재질을 고르지 않았을 때 현재 manifest에 기록된 영속 재질 세트를 재사용합니다.
        private DungeonBakeMaterialSet ResolveBakeMaterialSet(
            DungeonStageDefinition definition)
        {
            if (_bakeMaterialSet != null) return _bakeMaterialSet;
            if (definition == null || definition.bakeManifest == null) return null;
            _bakeMaterialSet = definition.bakeManifest.materialSet;
            return _bakeMaterialSet;
        }

        // 활성 설정, 기존 Bake 출처, Generator settings 순으로 게임플레이 구축 입력을 해결합니다.
        private RogueDungeonSettings ResolveBakeRuntimeSettings(
            DungeonStageDefinition definition)
        {
            RogueDungeonSettings active = _generator != null
                ? _generator.ActiveRuntimeSettings
                : null;
            if (active != null) return active;
            RogueDungeonSettings committed =
                definition != null && definition.bakeManifest != null
                    ? definition.bakeManifest.sourceRuntimeSettings
                    : null;
            if (committed != null) return committed;
            return _generator != null ? _generator.settings : null;
        }

        // Bake 가능한 SavedBlueprint 자산인지 검사하고 차단 이유를 한국어로 안내합니다.
        private static void DrawBakeSourceMessage(DungeonStageDefinition definition)
        {
            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    "Bake할 StageDefinition을 선택하세요.",
                    MessageType.None);
                return;
            }
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)))
            {
                EditorGUILayout.HelpBox(
                    "프로젝트에 저장된 StageDefinition 자산만 Bake할 수 있습니다.",
                    MessageType.Error);
                return;
            }
            if (definition.sourceMode == DungeonStageSourceMode.Procedural)
            {
                EditorGUILayout.HelpBox(
                    "Procedural + BakedPrefab 조합은 지원하지 않습니다. 결과를 Blueprint로 저장한 뒤 SavedBlueprint StageDefinition을 만드세요.",
                    MessageType.Error);
                return;
            }
            if (definition.sourceMode != DungeonStageSourceMode.SavedBlueprint)
            {
                EditorGUILayout.HelpBox(
                    "지원하지 않는 스테이지 출처입니다.",
                    MessageType.Error);
                return;
            }
            if (definition.savedBlueprint == null ||
                definition.savedBlueprint.blueprint == null)
            {
                EditorGUILayout.HelpBox(
                    "SavedBlueprint StageDefinition에 유효한 Blueprint 자산이 필요합니다.",
                    MessageType.Error);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "프로젝트 자산을 만드는 Bake는 Edit 모드에서만 실행할 수 있습니다.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Bake는 알려진 built-in/fallback과 Content Catalog가 직접 참조하는 Prefab만 지원합니다. R7 Stage Override가 연결되면 검증된 최종 Blueprint를 Bake합니다.",
                MessageType.None);
            if (definition.stageOverrides != null)
            {
                DungeonStageOverrideApplyResult application =
                    DungeonStageOverrideApplier.Apply(
                        definition.savedBlueprint,
                        definition.stageOverrides);
                EditorGUILayout.HelpBox(
                    application.IsValid
                        ? "Stage Override가 유효합니다. 원본 Blueprint는 보존되고 최종 hash가 별도로 Bake됩니다."
                        : "Stage Override에 충돌이 있어 Preview와 Bake를 진행할 수 없습니다. R7 편집 영역의 검증 리포트를 확인하세요.",
                    application.IsValid
                        ? MessageType.Info
                        : MessageType.Error);
            }
        }

        // Edit 모드, 저장 자산, SavedBlueprint와 완전한 재질 입력이 준비되었을 때만 Bake 버튼을 허용합니다.
        private static bool CanBakeStage(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet)
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   definition != null &&
                   !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)) &&
                   definition.sourceMode == DungeonStageSourceMode.SavedBlueprint &&
                   definition.savedBlueprint != null &&
                   definition.savedBlueprint.blueprint != null &&
                   IsBakeMaterialSetComplete(materialSet) &&
                   (definition.stageOverrides == null ||
                    DungeonStageOverrideApplier.Apply(
                        definition.savedBlueprint,
                        definition.stageOverrides).IsValid);
        }

        // 재질 세트의 8개 필수 슬롯과 현재 commit된 Bake 입력과의 차이를 제작 전에 안내합니다.
        private static void DrawBakeMaterialMessage(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet)
        {
            if (materialSet == null)
            {
                EditorGUILayout.HelpBox(
                    "floor, wall과 built-in 범주에 사용할 영속 Bake 재질 세트가 필요합니다.",
                    MessageType.Warning);
                return;
            }
            if (!IsBakeMaterialSetComplete(materialSet))
            {
                EditorGUILayout.HelpBox(
                    "재질 세트의 floor, wall, enemy, destructible, prop, gimmick, entrance, exit 슬롯을 모두 지정하세요.",
                    MessageType.Error);
                return;
            }
            if (definition != null &&
                definition.bakeManifest != null &&
                definition.bakeManifest.materialSet != null &&
                definition.bakeManifest.materialSet != materialSet)
            {
                EditorGUILayout.HelpBox(
                    "현재 Bake와 다른 재질 세트를 선택했습니다. 새 재질을 반영하려면 재Bake하세요.",
                    MessageType.Warning);
            }
        }

        // built-in geometry와 콘텐츠를 저장하는 데 필요한 모든 Material 참조가 있는지 확인합니다.
        private static bool IsBakeMaterialSetComplete(
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

        // Prefab과 manifest가 모두 연결된 StageDefinition을 재Bake 대상으로 판정합니다.
        private static bool HasCommittedBake(DungeonStageDefinition definition)
        {
            return definition != null &&
                   definition.bakedPrefab != null &&
                   definition.bakeManifest != null;
        }

        // 저장 위치를 선택해 8개 범주 슬롯이 채워진 영속 기본 재질 세트를 생성합니다.
        private void CreateDefaultBakeMaterialSet(DungeonStageDefinition definition)
        {
            string folder = ResolveBakeAssetFolder(definition);
            string defaultName = definition != null
                ? definition.name + "_BakeMaterialSet"
                : "DungeonBakeMaterialSet";
            string path = EditorUtility.SaveFilePanelInProject(
                "기본 Bake 재질 세트 생성",
                defaultName,
                "asset",
                "공유하거나 재사용할 영속 Bake 재질 세트의 저장 경로를 선택하세요.",
                folder);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                _bakeMaterialSet = DungeonStageBaker.CreateDefaultMaterialSetAsset(path);
                InvalidateBakeValidation();
                Selection.activeObject = _bakeMaterialSet;
                EditorGUIUtility.PingObject(_bakeMaterialSet);
                Repaint();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure("기본 Bake 재질 세트 생성 실패", exception);
            }
        }

        // StageDefinition 또는 Blueprint 옆 폴더를 자산 생성 대화상자의 시작 위치로 계산합니다.
        private static string ResolveBakeAssetFolder(
            DungeonStageDefinition definition)
        {
            string sourcePath = definition != null
                ? AssetDatabase.GetAssetPath(definition)
                : string.Empty;
            if (string.IsNullOrEmpty(sourcePath) &&
                definition != null &&
                definition.savedBlueprint != null)
            {
                sourcePath = AssetDatabase.GetAssetPath(definition.savedBlueprint);
            }
            if (string.IsNullOrEmpty(sourcePath)) return "Assets";
            string folder = Path.GetDirectoryName(sourcePath);
            return string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
        }

        // 신규 Bake와 재Bake의 영향 범위를 확인받은 뒤 staging 기반 Baker를 실행합니다.
        private void ConfirmAndBakeStage(
            DungeonStageDefinition definition,
            DungeonBakeMaterialSet materialSet,
            RogueDungeonSettings runtimeSettings)
        {
            bool rebake = HasCommittedBake(definition);
            string message = rebake
                ? "새 후보를 stage 전용 staging 영역에서 검증한 뒤에만 참조를 교체합니다.\n실패하면 현재 정상 Prefab과 manifest는 유지됩니다.\n성공 뒤 이전 파생 자산 정리는 비가역적이므로 Ctrl+Z 대상이 아닙니다.\n\n재Bake하시겠습니까?"
                : "저장 Blueprint에서 영속 floor/wall Mesh, 콘텐츠 Prefab과 manifest를 만듭니다.\n성공하면 StageDefinition이 BakedPrefab 모드와 새 산출물을 참조합니다.\nBake commit은 파생 자산을 만들기 때문에 Ctrl+Z 대상이 아닙니다.\n\nBake하시겠습니까?";
            if (!EditorUtility.DisplayDialog(
                rebake ? "스테이지 재Bake 확인" : "스테이지 Bake 확인",
                    message,
                    rebake ? "재Bake" : "Bake",
                    "취소"))
            {
                return;
            }

            try
            {
                DungeonStageBakeResult result = DungeonStageBaker.Bake(
                    definition,
                    materialSet,
                    runtimeSettings);
                _bakeStageDefinition = result.Definition;
                _bakeMaterialSet = result.Manifest != null
                    ? result.Manifest.materialSet
                    : materialSet;
                _bakeValidatedDefinition = result.Definition;
                _bakeValidation = result.ValidationReport;
                Selection.activeObject = result.BakedPrefab != null
                    ? result.BakedPrefab
                    : (UnityEngine.Object)result.Manifest;
                if (Selection.activeObject != null)
                    EditorGUIUtility.PingObject(Selection.activeObject);
                RepaintAll();
            }
            catch (Exception exception)
            {
                ShowAuthoringFailure(rebake ? "재Bake 실패" : "Bake 실패", exception);
                RefreshBakeValidation(definition);
            }
        }

        // 현재 commit된 Prefab과 manifest를 읽기 전용 필드와 Ping 버튼으로 표시합니다.
        private static void DrawBakeResultReferences(
            DungeonStageDefinition definition)
        {
            GameObject prefab = definition != null ? definition.bakedPrefab : null;
            DungeonBakeManifest manifest = definition != null
                ? definition.bakeManifest
                : null;
            DrawBakeAssetReference(
                "Stage Overrides",
                definition != null ? definition.stageOverrides : null);
            DrawBakeAssetReference("Baked Prefab", prefab);
            DrawBakeAssetReference("Bake Manifest", manifest);
            if (definition != null && manifest != null &&
                manifest.materialSet != null &&
                definition.buildMode == DungeonStageBuildMode.BakedPrefab)
            {
                EditorGUILayout.LabelField(
                    "StageDefinition은 배포용 BakedPrefab 로드 모드입니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        // 자산 참조를 수정 불가 ObjectField와 별도 Ping 버튼으로 짧게 표시합니다.
        private static void DrawBakeAssetReference(
            string label,
            UnityEngine.Object asset)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    label,
                    asset,
                    typeof(UnityEngine.Object),
                    false);
            }
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(46f)))
                    EditorGUIUtility.PingObject(asset);
            }
            EditorGUILayout.EndHorizontal();
        }

        // 현재 프로젝트 의존성을 다시 fingerprint해 manifest 최신성과 참조 무결성을 갱신합니다.
        private void RefreshBakeValidation(DungeonStageDefinition definition)
        {
            _bakeValidatedDefinition = definition;
            _bakeValidationFailure = string.Empty;
            try
            {
                _bakeValidation = definition != null
                    ? DungeonStageBaker.ValidateCurrentBake(definition)
                    : null;
            }
            catch (Exception exception)
            {
                _bakeValidation = null;
                _bakeValidationFailure =
                    "Bake 최신성을 검사할 수 없습니다: " + exception.Message;
                Debug.LogException(exception);
            }
        }

        // Bake 입력이 바뀌면 이전 대상에서 계산한 최신성 리포트를 더 이상 표시하지 않습니다.
        private void InvalidateBakeValidation()
        {
            _bakeValidatedDefinition = null;
            _bakeValidation = null;
            _bakeValidationFailure = string.Empty;
        }
    }
}
