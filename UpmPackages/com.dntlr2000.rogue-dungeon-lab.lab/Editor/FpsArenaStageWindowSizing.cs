using System;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed partial class FpsArenaStageWindow
    {
        [SerializeField] private float newCellSize = 3, newFloorHeight = 4;
        [SerializeField] private bool sizeExpanded = true, defaultsExpanded, batchExpanded, newSizeExpanded, showAllSizeErrors;
        private FpsArenaStageAsset sizingStage;
        private string sizingSource = "", defaultsSource = "", selectedWallSource = "";
        private int editWidth, editDepth, editFloors, wallFilter;
        private float editCellSize, editFloorHeight, selectedHeight, selectedThickness, batchHeight = 1.5f, batchThickness = .15f;
        private FpsArenaWallAuthoringDefaults defaultsDraft;
        private FpsArenaStageResizePreview resizePreview;

        // 자산 전환/Undo/외부 변경만 입력 기준선을 갱신하고 타이핑 중에는 저장 데이터를 건드리지 않는다.
        private void SyncSizingState()
        {
            bool changedStage = sizingStage != stage;
            if (changedStage) { sizingStage = stage; sizingSource = defaultsSource = selectedWallSource = ""; resizePreview = null; }
            string source = stage.data.width + "/" + stage.data.depth + "/" + stage.data.floors + "/" + stage.data.cellSize + "/" + stage.data.floorHeight;
            if (source != sizingSource) { sizingSource = source; LoadSizeDraft(); }
            var defaults = stage.GetWallDefaults(); string json = JsonUtility.ToJson(defaults);
            if (json != defaultsSource) { defaultsSource = json; defaultsDraft = defaults; }
            if (changedStage) { batchHeight = defaults.InteriorHeight(stage.data.floorHeight); batchThickness = defaults.interiorThickness; }
            floor = Mathf.Clamp(floor, 0, stage.data.floors - 1);
        }

        // 현재 규격으로 임시 입력을 되돌리는 취소는 자산/Undo/dirty 상태를 바꾸지 않는다.
        private void LoadSizeDraft()
        {
            editWidth = stage.data.width; editDepth = stage.data.depth; editFloors = stage.data.floors;
            editCellSize = stage.data.cellSize; editFloorHeight = stage.data.floorHeight; resizePreview = null;
        }

        // 입력 후보와 미터 면적/추가 바닥/거부 사유를 같은 서비스 결과로 표시한다.
        private void DrawCurrentSize()
        {
            sizeExpanded = EditorGUILayout.Foldout(sizeExpanded, "현재 스테이지 규격", true);
            if (!sizeExpanded) return;
            EditorGUI.BeginChangeCheck();
            editWidth = EditorGUILayout.DelayedIntField("가로 (셀)", editWidth);
            editDepth = EditorGUILayout.DelayedIntField("세로 (셀)", editDepth);
            editFloors = EditorGUILayout.DelayedIntField("층 수 (공통)", editFloors);
            editCellSize = EditorGUILayout.DelayedFloatField("셀 크기 (m)", editCellSize);
            editFloorHeight = EditorGUILayout.DelayedFloatField("층간 높이 (m)", editFloorHeight);
            if (EditorGUI.EndChangeCheck()) resizePreview = null;
            var data = stage.data;
            EditorGUILayout.LabelField($"현재: {data.width * data.cellSize:0.##} × {data.depth * data.cellSize:0.##}m / {data.floors}층", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"변경 후: {editWidth * editCellSize:0.##} × {editDepth * editCellSize:0.##}m / {editFloors}층", EditorStyles.wordWrappedMiniLabel);
            if (resizePreview == null) resizePreview = FpsArenaStageAuthoring.PreviewResize(stage, editWidth, editDepth, editFloors, editCellSize, editFloorHeight);
            EditorGUILayout.LabelField("추가 바닥: " + resizePreview.AddedFloorCells + "셀", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("셀 좌표와 기존 벽/콘텐츠의 실제 치수는 유지합니다. 확대 영역은 바닥만 추가하며 축소 시 범위 밖 데이터는 먼저 정리해야 합니다. 장면은 적용 버튼으로 별도 갱신합니다.", MessageType.None);
            if (resizePreview.Errors.Count > 0)
            {
                EditorGUILayout.HelpBox("규격 변경을 보류합니다: " + resizePreview.Errors.Count + "개 문제", MessageType.Warning);
                int count = showAllSizeErrors ? resizePreview.Errors.Count : Mathf.Min(8, resizePreview.Errors.Count);
                for (int i = 0; i < count; i++) EditorGUILayout.LabelField(resizePreview.Errors[i], EditorStyles.wordWrappedMiniLabel);
                if (resizePreview.Errors.Count > 8) showAllSizeErrors = EditorGUILayout.ToggleLeft("나머지 문제도 모두 표시", showAllSizeErrors);
            }
            bool same = editWidth == data.width && editDepth == data.depth && editFloors == data.floors && editCellSize == data.cellSize && editFloorHeight == data.floorHeight;
            using (new EditorGUI.DisabledScope(same || !resizePreview.CanApply))
                if (GUILayout.Button("규격 변경 적용")) Run(() => FpsArenaStageAuthoring.ResizeStage(stage, editWidth, editDepth, editFloors, editCellSize, editFloorHeight));
            using (new EditorGUI.DisabledScope(same)) if (GUILayout.Button("규격 입력 취소")) LoadSizeDraft();
            EditorGUILayout.Space();
        }

        // 새 내부벽/외곽벽의 제작 기본값만 명시적으로 저장하고 기존 배치와 해시는 보존한다.
        private void DrawWallDefaults()
        {
            defaultsExpanded = EditorGUILayout.Foldout(defaultsExpanded, "벽 배치 기본값", true);
            if (!defaultsExpanded) return;
            defaultsDraft.automaticInteriorHeight = EditorGUILayout.ToggleLeft("내부 높이: 층간 높이 − 0.25m 자동", defaultsDraft.automaticInteriorHeight);
            using (new EditorGUI.DisabledScope(defaultsDraft.automaticInteriorHeight))
            {
                float displayed = defaultsDraft.InteriorHeight(stage.data.floorHeight);
                float height = EditorGUILayout.DelayedFloatField("내부 높이 (m)", displayed);
                if (!defaultsDraft.automaticInteriorHeight) defaultsDraft.interiorHeight = height;
            }
            defaultsDraft.interiorThickness = EditorGUILayout.DelayedFloatField("내부 두께 (m)", defaultsDraft.interiorThickness);
            defaultsDraft.boundaryHeight = EditorGUILayout.DelayedFloatField("외곽 높이 (m)", defaultsDraft.boundaryHeight);
            defaultsDraft.boundaryThickness = EditorGUILayout.DelayedFloatField("외곽 두께 (m)", defaultsDraft.boundaryThickness);
            string error = FpsArenaStageAuthoring.WallDimensionError(defaultsDraft.InteriorHeight(stage.data.floorHeight), defaultsDraft.interiorThickness, stage.data.floorHeight);
            if (string.IsNullOrEmpty(error)) error = FpsArenaStageAuthoring.WallDimensionError(defaultsDraft.boundaryHeight, defaultsDraft.boundaryThickness, stage.data.floorHeight);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.HelpBox("앞으로 그리는 벽과 외곽 자동 배치에만 사용합니다. 기존 벽을 바꾸려면 선택 속성 또는 일괄 편집을 사용하세요.", MessageType.None);
            bool same = JsonUtility.ToJson(defaultsDraft) == defaultsSource;
            using (new EditorGUI.DisabledScope(same || !string.IsNullOrEmpty(error)))
                if (GUILayout.Button("벽 배치 기본값 저장")) Run(() => FpsArenaStageAuthoring.SetWallDefaults(stage, defaultsDraft));
            using (new EditorGUI.DisabledScope(same)) if (GUILayout.Button("기본값 입력 취소")) defaultsDraft = stage.GetWallDefaults();
        }

        // 두 벽 표현 모두 같은 미터 입력을 사용하며 길이와 문 구조를 변경하지 않는다.
        private void DrawSelectedWallDimensions()
        {
            var boundary = stage.data.partitions.Find(w => w.id == selectedId);
            var legacy = stage.data.walls.Find(w => w.id == selectedId);
            float height = boundary != null ? boundary.height : legacy.height;
            float thickness = boundary != null ? boundary.thickness : legacy.thickness;
            int length = boundary != null ? boundary.lengthCells : legacy.lengthCells;
            string key = selectedId + "/" + height + "/" + thickness;
            if (selectedWallSource != key) { selectedWallSource = key; selectedHeight = height; selectedThickness = thickness; }
            EditorGUILayout.LabelField("길이", length + "셀 / " + (length * stage.data.cellSize).ToString("0.##") + "m");
            selectedHeight = EditorGUILayout.DelayedFloatField("높이 (m)", selectedHeight);
            selectedThickness = EditorGUILayout.DelayedFloatField("두께 (m)", selectedThickness);
            string error = FpsArenaStageAuthoring.WallDimensionError(selectedHeight, selectedThickness, stage.data.floorHeight);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            bool same = height == selectedHeight && thickness == selectedThickness;
            using (new EditorGUI.DisabledScope(same || !string.IsNullOrEmpty(error)))
                if (GUILayout.Button("선택한 벽에 적용")) Run(() => FpsArenaStageAuthoring.SetWallDimensions(stage, selectedId, selectedHeight, selectedThickness));
            using (new EditorGUI.DisabledScope(same)) if (GUILayout.Button("벽 치수 입력 취소")) { selectedHeight = height; selectedThickness = thickness; }
        }

        // 필터별 대상 수를 먼저 표시하고 사용자가 확인한 층 범위만 일괄 변경한다.
        private void DrawWallBatch()
        {
            batchExpanded = EditorGUILayout.Foldout(batchExpanded, "벽 치수 일괄 편집", true);
            if (!batchExpanded) return;
            wallFilter = EditorGUILayout.Popup("대상 벽", wallFilter, new[] { "전체", "내부벽", "외곽벽·구멍 경계" });
            batchHeight = EditorGUILayout.DelayedFloatField("높이 (m)", batchHeight);
            batchThickness = EditorGUILayout.DelayedFloatField("두께 (m)", batchThickness);
            var filter = (FpsArenaWallSelection)wallFilter;
            int current = FpsArenaStageAuthoring.CountWalls(stage.data, floor, filter), all = FpsArenaStageAuthoring.CountWalls(stage.data, null, filter);
            EditorGUILayout.LabelField($"대상: 현재 {floor + 1}층 {current}개 / 전체 {all}개", EditorStyles.wordWrappedMiniLabel);
            string error = FpsArenaStageAuthoring.WallDimensionError(batchHeight, batchThickness, stage.data.floorHeight);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(error)))
            {
                using (new EditorGUI.DisabledScope(current == 0)) if (GUILayout.Button("현재 층 벽에 일괄 적용")) ConfirmWallBatch(floor, current, filter);
                using (new EditorGUI.DisabledScope(all == 0)) if (GUILayout.Button("전체 층 벽에 일괄 적용")) ConfirmWallBatch(null, all, filter);
            }
        }

        // 확인 대화상자의 취소는 서비스/Undo 호출 없이 종료한다.
        private void ConfirmWallBatch(int? targetFloor, int count, FpsArenaWallSelection filter)
        {
            string scope = targetFloor.HasValue ? (targetFloor.Value + 1) + "층" : "전체 층";
            if (!EditorUtility.DisplayDialog("벽 치수 일괄 변경", scope + "의 벽 " + count + "개를 높이 " + batchHeight + "m / 두께 " + batchThickness + "m로 변경합니다. ID·길이·문은 유지하며 Undo로 되돌릴 수 있습니다.", "적용", "취소")) return;
            Run(() => FpsArenaStageAuthoring.SetWallDimensionsBatch(stage, targetFloor, filter, batchHeight, batchThickness));
        }
    }
}
