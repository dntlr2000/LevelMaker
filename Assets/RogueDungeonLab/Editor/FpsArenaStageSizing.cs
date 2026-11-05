using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public enum FpsArenaWallSelection { All, Interior, Boundary }

    // 규격 변경은 후보와 진단만 만들며 명시적 적용 전에는 원본을 수정하지 않는다.
    public sealed class FpsArenaStageResizePreview
    {
        private readonly List<string> errors = new List<string>();
        public IReadOnlyList<string> Errors { get { return errors; } }
        public int AddedFloorCells { get; internal set; }
        public bool CanApply { get { return errors.Count == 0 && Candidate != null; } }
        internal FpsArenaStageData Candidate;

        // 범위/치수 문제를 UI에서 항목별로 확인할 수 있게 누적한다.
        internal void Add(string message) { errors.Add(message); }
    }

    public static partial class FpsArenaStageAuthoring
    {
        // 새 규격을 미리 계산하고 범위를 벗어나는 모든 배치와 치수 문제를 수집한다.
        public static FpsArenaStageResizePreview PreviewResize(FpsArenaStageAsset asset, int width, int depth, int floors, float cellSize, float floorHeight)
        {
            var result = new FpsArenaStageResizePreview();
            if (asset == null || asset.data == null) { result.Add("수동 스테이지를 연결하세요."); return result; }
            if (asset.data.version != FpsArenaStageData.CurrentVersion) { result.Add("편집형 벽으로 변환한 뒤 규격을 변경하세요."); return result; }
            if (width < 8 || width > 64 || depth < 8 || depth > 64 || floors < 1 || floors > 4
                || !FiniteRange(cellSize, 2, 5) || !FiniteRange(floorHeight, 3, 6))
            { result.Add("가로·세로 8–64셀, 1–4층, 셀 2–5m, 층간 높이 3–6m를 사용하세요."); return result; }
            var source = asset.data;
            if (source.floorCells == null || source.spawns == null || source.stairs == null || source.partitions == null || source.walls == null || source.content == null)
            { result.Add("스테이지 배치 목록이 손상되었습니다."); return result; }
            var candidate = source.Clone();
            candidate.width = width; candidate.depth = depth; candidate.floors = floors; candidate.cellSize = cellSize; candidate.floorHeight = floorHeight;
            var floorCells = new HashSet<Vector3Int>(candidate.floorCells);
            for (int f = 0; f < floors; f++) for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
            {
                if (f < source.floors && z < source.depth && x < source.width) continue;
                var cell = new Vector3Int(x, f, z);
                if (floorCells.Add(cell)) { candidate.floorCells.Add(cell); result.AddedFloorCells++; }
            }
            foreach (var cell in candidate.floorCells) if (!candidate.Contains(cell)) result.Add(At("바닥", "", cell) + "새 격자 범위를 벗어납니다. 바닥 지우기로 먼저 정리하세요.");
            foreach (var cell in candidate.spawns) if (!HasFloor(candidate, floorCells, cell)) result.Add(At("스폰", "", cell) + "새 범위의 바닥 위로 이동하세요.");
            foreach (var stair in candidate.stairs)
            {
                if (stair == null) { result.Add("계단 데이터가 없습니다."); continue; }
                bool outside = stair.lowerFloor < 0 || stair.lowerFloor + 1 >= floors;
                for (int lane = 0; lane < stair.width; lane++)
                {
                    outside |= !HasFloor(candidate, floorCells, stair.BottomLane(lane)) || !HasFloor(candidate, floorCells, stair.TopLane(lane));
                    for (int step = 0; step < stair.length; step++) outside |= !candidate.Contains(stair.Cell(lane, step)) || !candidate.Contains(stair.Cell(lane, step, 1));
                }
                if (outside) result.Add(At("계단", "", new Vector3Int(stair.x, stair.lowerFloor, stair.z)) + "계단 또는 양쪽 착지점이 새 범위를 벗어납니다. 먼저 이동/삭제하세요.");
            }
            foreach (var wall in candidate.partitions)
            {
                if (wall == null) { result.Add("경계벽 데이터가 없습니다."); continue; }
                bool outside = wall.lengthCells < 1 || wall.lengthCells > 64;
                for (int i = 0; i < wall.lengthCells && i < 64; i++)
                {
                    var cell = wall.cell + wall.Step * i;
                    outside |= !HasFloor(candidate, floorCells, cell) && !HasFloor(candidate, floorCells, cell + wall.Normal);
                }
                if (outside) result.Add(At("벽", wall.id, wall.cell) + "새 범위의 바닥 경계에 남을 수 없습니다. 먼저 이동/삭제하세요.");
                AddWallDimensionIssue(result, wall.id, wall.cell, wall.height, wall.thickness, floorHeight);
            }
            foreach (var wall in candidate.walls)
            {
                if (wall == null) { result.Add("이전 벽 조각 데이터가 없습니다."); continue; }
                bool outside = wall.lengthCells < 1 || wall.lengthCells > 64;
                if (!outside) foreach (var cell in wall.Footprint()) outside |= !HasFloor(candidate, floorCells, cell);
                if (outside) result.Add(At("이전 벽 조각", wall.id, wall.cell) + "새 범위의 바닥을 벗어납니다. 먼저 이동/삭제하세요.");
                AddWallDimensionIssue(result, wall.id, wall.cell, wall.height, wall.thickness, floorHeight);
            }
            foreach (var content in candidate.content)
            {
                if (content == null) { result.Add("콘텐츠 데이터가 없습니다."); continue; }
                bool outside = false;
                foreach (var cell in content.Footprint()) outside |= !HasFloor(candidate, floorCells, cell);
                if (outside) result.Add(At("콘텐츠", content.id, content.cell) + "점유 셀이 새 범위의 바닥을 벗어납니다. 먼저 이동/삭제하세요.");
                float sin = Mathf.Abs(Mathf.Sin(content.yaw * Mathf.Deg2Rad)), cos = Mathf.Abs(Mathf.Cos(content.yaw * Mathf.Deg2Rad));
                if (!FiniteRange(content.size.x, .01f, content.widthCells * cellSize) || !FiniteRange(content.size.z, .01f, content.depthCells * cellSize)
                    || !FiniteRange(content.size.y, .01f, floorHeight - .1f)
                    || cos * content.size.x + sin * content.size.z > content.widthCells * cellSize + .001f
                    || sin * content.size.x + cos * content.size.z > content.depthCells * cellSize + .001f)
                    result.Add(At("콘텐츠", content.id, content.cell) + "실제 치수가 새 셀/층 높이를 초과합니다. 치수 또는 점유 크기를 먼저 수정하세요.");
            }
            result.Candidate = candidate;
            if (result.Errors.Count == 0)
            {
                try { candidate.ToLayout(); }
                catch (Exception e) { result.Add("구조 검사: " + e.Message); }
            }
            return result;
        }

        // 미리보기와 동일한 후보를 다시 검사하여 성공할 때만 하나의 Undo로 교체한다.
        public static void ResizeStage(FpsArenaStageAsset asset, int width, int depth, int floors, float cellSize, float floorHeight)
        {
            RequireEditableStage(asset);
            var preview = PreviewResize(asset, width, depth, floors, cellSize, floorHeight);
            if (!preview.CanApply) throw new InvalidOperationException(string.Join("\n", preview.Errors));
            Commit(asset, "수동 스테이지 규격 변경", preview.Candidate);
        }

        // 저장되지 않은 구형 기본값은 조회만으로 dirty 처리하지 않고 명시적 변경에만 Undo를 남긴다.
        public static void SetWallDefaults(FpsArenaStageAsset asset, FpsArenaWallAuthoringDefaults defaults)
        {
            RequireEditableStage(asset);
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            CheckWallDimensions(defaults.InteriorHeight(asset.data.floorHeight), defaults.interiorThickness, asset.data.floorHeight);
            CheckWallDimensions(defaults.boundaryHeight, defaults.boundaryThickness, asset.data.floorHeight);
            if (JsonUtility.ToJson(asset.GetWallDefaults()) == JsonUtility.ToJson(defaults)) return;
            Undo.RegisterCompleteObjectUndo(asset, "벽 배치 기본값 변경");
            asset.wallDefaults = defaults.Clone(); EditorUtility.SetDirty(asset);
        }

        // 선택한 경계벽 또는 이전 벽 조각의 ID/길이/문을 유지하며 높이와 두께만 바꾼다.
        public static void SetWallDimensions(FpsArenaStageAsset asset, string id, float height, float thickness)
        {
            RequireEditableStage(asset); CheckWallDimensions(height, thickness, asset.data.floorHeight);
            Edit(asset, "선택한 벽 치수 변경", data => {
                var wall = data.partitions.Find(w => w != null && w.id == id);
                var legacy = data.walls.Find(w => w != null && w.id == id);
                if ((wall == null) == (legacy == null)) throw new InvalidOperationException("선택한 벽 ID가 없거나 중복됩니다.");
                if (wall != null) { wall.height = height; wall.thickness = thickness; }
                else { legacy.height = height; legacy.thickness = thickness; }
                data.ToLayout();
            });
        }

        // 현재 층 또는 전체 층의 필터 대상 수를 확인/취소 UI에 제공한다.
        public static int CountWalls(FpsArenaStageData data, int? floor, FpsArenaWallSelection filter)
        {
            var cells = new HashSet<Vector3Int>(data.floorCells); int count = 0;
            foreach (var wall in data.partitions) if (wall != null && Matches(data, cells, wall, floor, filter)) count++;
            if (filter != FpsArenaWallSelection.Boundary) foreach (var wall in data.walls) if (wall != null && (!floor.HasValue || wall.cell.y == floor.Value)) count++;
            return count;
        }

        // 일괄 편집은 대상이 없으면 무변경이며 모든 벽을 검증한 뒤 한 번에 반영한다.
        public static void SetWallDimensionsBatch(FpsArenaStageAsset asset, int? floor, FpsArenaWallSelection filter, float height, float thickness)
        {
            RequireEditableStage(asset); CheckWallDimensions(height, thickness, asset.data.floorHeight);
            if (!Enum.IsDefined(typeof(FpsArenaWallSelection), filter) || (floor.HasValue && (floor.Value < 0 || floor.Value >= asset.data.floors))) throw new ArgumentException("벽 일괄 편집 범위가 잘못되었습니다.");
            if (CountWalls(asset.data, floor, filter) == 0) return;
            Edit(asset, floor.HasValue ? "현재 층 벽 치수 변경" : "전체 층 벽 치수 변경", data => {
                var cells = new HashSet<Vector3Int>(data.floorCells);
                foreach (var wall in data.partitions) if (wall != null && Matches(data, cells, wall, floor, filter)) { wall.height = height; wall.thickness = thickness; }
                if (filter != FpsArenaWallSelection.Boundary) foreach (var wall in data.walls) if (wall != null && (!floor.HasValue || wall.cell.y == floor.Value)) { wall.height = height; wall.thickness = thickness; }
                data.ToLayout();
            });
        }

        // 미터 입력을 자동 보정하지 않고 UI와 서비스가 같은 허용 범위를 안내한다.
        public static string WallDimensionError(float height, float thickness, float floorHeight)
        {
            if (!FiniteRange(height, .1f, floorHeight - .1f)) return "벽 높이는 0.1–" + (floorHeight - .1f).ToString("0.##") + "m여야 합니다.";
            if (!FiniteRange(thickness, .1f, 1)) return "벽 두께는 0.1–1m여야 합니다.";
            return "";
        }

        // 모든 새 쓰기 서비스가 Play/구형 자산 잠금을 공통으로 따른다.
        private static void RequireEditableStage(FpsArenaStageAsset asset)
        {
            if (asset == null || asset.data == null) throw new ArgumentNullException(nameof(asset));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 스테이지를 편집하세요.");
            if (asset.data.version != FpsArenaStageData.CurrentVersion) throw new InvalidOperationException("편집형 벽으로 변환한 뒤 스테이지를 편집하세요.");
        }

        // 경계벽은 구간 중 하나라도 한쪽 바닥 경계이면 외곽 필터에 포함한다.
        private static bool Matches(FpsArenaStageData data, HashSet<Vector3Int> cells, FpsArenaRoomWall wall, int? floor, FpsArenaWallSelection filter)
        {
            if (floor.HasValue && wall.cell.y != floor.Value) return false;
            if (filter == FpsArenaWallSelection.All) return true;
            bool boundary = false;
            for (int i = 0; i < wall.lengthCells; i++) { var c = wall.cell + wall.Step * i; boundary |= HasFloor(data, cells, c) != HasFloor(data, cells, c + wall.Normal); }
            return filter == FpsArenaWallSelection.Boundary ? boundary : !boundary;
        }

        // 음수 외곽 anchor와 일반 바닥 좌표를 구분하여 범위 안 실제 바닥만 인정한다.
        private static bool HasFloor(FpsArenaStageData data, HashSet<Vector3Int> cells, Vector3Int cell)
        { return data.Contains(cell) && cells.Contains(cell); }

        // NaN/Infinity도 입력 오류로 취급하여 잘못된 자산을 만들지 않는다.
        private static bool FiniteRange(float value, float min, float max)
        { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max; }

        // 목록 진단은 사용자용 층 번호와 원본 ID/셀 좌표를 함께 표시한다.
        private static string At(string kind, string id, Vector3Int cell)
        { return kind + (string.IsNullOrEmpty(id) ? "" : " [" + id + "]") + " · " + (cell.y + 1) + "층 · X=" + cell.x + ", Z=" + cell.z + ": "; }

        // 벽의 치수 오류를 규격 변경의 전체 진단 목록에 추가한다.
        private static void AddWallDimensionIssue(FpsArenaStageResizePreview preview, string id, Vector3Int cell, float height, float thickness, float floorHeight)
        { string error = WallDimensionError(height, thickness, floorHeight); if (!string.IsNullOrEmpty(error)) preview.Add(At("벽", id, cell) + error); }

        // 쓰기 서비스는 UI와 동일한 진단을 예외로 반환하며 원본 변경 전에 중단한다.
        private static void CheckWallDimensions(float height, float thickness, float floorHeight)
        { string error = WallDimensionError(height, thickness, floorHeight); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error); }
    }
}
