using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public static partial class FpsArenaStageAuthoring
    {
        // 기존 생성 API는 제작 기본값 없이도 같은 스테이지를 저장한다.
        public static FpsArenaStageAsset CreateAsset(string path, FpsArenaStageData data, FpsArenaContentCatalog catalog = null)
        { return CreateAsset(path, data, catalog, null); }

        // 다른 이름으로 저장할 때 제작용 벽 기본값도 깊은 복사하여 보존한다.
        public static FpsArenaStageAsset CreateAsset(string path, FpsArenaStageData data, FpsArenaContentCatalog catalog, FpsArenaWallAuthoringDefaults wallDefaults)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("Assets 폴더의 새 .asset 경로를 선택하세요.");
            var asset = ScriptableObject.CreateInstance<FpsArenaStageAsset>();
            asset.data = data.Clone(); asset.contentCatalog = catalog; asset.wallDefaults = wallDefaults != null ? wallDefaults.Clone() : null;
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // Work on a copy: a failed edit cannot partly mutate the saved document.
        public static void Edit(FpsArenaStageAsset asset, string label, Action<FpsArenaStageData> edit)
        {
            if (asset == null || asset.data == null) throw new ArgumentNullException(nameof(asset));
            if (Application.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 스테이지를 편집하세요.");
            if (asset.data.version != FpsArenaStageData.CurrentVersion) throw new InvalidOperationException("편집형 벽으로 변환한 뒤 스테이지를 편집하세요.");
            var candidate = asset.data.Clone(); edit(candidate);
            Commit(asset, label, candidate);
        }

        // 실제 변경에만 하나의 Undo와 dirty를 기록하며 자동 저장은 하지 않는다.
        private static void Commit(FpsArenaStageAsset asset, string label, FpsArenaStageData candidate)
        {
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(asset.data)) return;
            Undo.RegisterCompleteObjectUndo(asset, label); asset.data = candidate; EditorUtility.SetDirty(asset);
        }

        // 구형 자산의 기존 가드를 명시적 벽으로 바꾸는 사용자 요청만 Undo 가능하게 반영한다.
        public static void UpgradeWalls(FpsArenaStageAsset asset)
        {
            if (asset == null || asset.data == null) throw new ArgumentNullException(nameof(asset));
            if (Application.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 스테이지를 변환하세요.");
            Commit(asset, "편집형 벽으로 변환", asset.data.Normalized());
        }

        // 전체 층의 외곽/구멍 경계 중 비어 있는 곳만 단일 Undo로 채운다.
        public static void AutoPlaceBoundaryWalls(FpsArenaStageAsset asset)
        {
            RequireEditableStage(asset);
            var defaults = asset.GetWallDefaults();
            CheckWallDimensions(defaults.boundaryHeight, defaults.boundaryThickness, asset.data.floorHeight);
            Edit(asset, "벽 자동 배치 (전체 층)", data => {
                var ids = new System.Collections.Generic.HashSet<string>(); foreach (var wall in data.partitions) ids.Add(wall.id);
                data.AddMissingBoundaryWalls();
                foreach (var wall in data.partitions) if (!ids.Contains(wall.id)) { wall.height = defaults.boundaryHeight; wall.thickness = defaults.boundaryThickness; }
            });
        }

        public static FpsArenaContent MakeContent(FpsArenaStageData data, FpsArenaContentKind kind, Vector3Int cell, FpsArenaCatalogEntry entry = null)
        {
            if (entry != null && entry.kind != kind) throw new InvalidOperationException("선택한 프리팹의 범주가 다릅니다.");
            int w = entry != null ? entry.widthCells : 1, d = entry != null ? entry.depthCells : 1;
            float h = entry != null ? entry.height : kind == FpsArenaContentKind.Cover ? 1.1f : kind == FpsArenaContentKind.Enemy ? 1.8f : .6f;
            return new FpsArenaContent { id = "manual:" + Guid.NewGuid().ToString("N"), kind = kind, cell = cell, widthCells = w, depthCells = d, size = new Vector3(w * data.cellSize * .8f, Mathf.Min(h, data.floorHeight - .1f), d * data.cellSize * .8f), contentKey = entry != null ? entry.contentKey : "" };
        }

        // 기존 벽의 ID/규격은 유지하며 한 경계를 지울 때만 필요한 긴 벽을 분할한다.
        public static void SetWall(FpsArenaStageData data, Vector3Int cell, bool alongX, bool present)
        { SetWall(data, cell, alongX, present, null); }

        // 새 벽만 제작 기본값을 사용하고 기존 벽의 ID/치수는 덮어쓰지 않는다.
        public static void SetWall(FpsArenaStageData data, Vector3Int cell, bool alongX, bool present, FpsArenaWallAuthoringDefaults defaults)
        {
            for (int i = data.partitions.Count - 1; i >= 0; i--)
            {
                var wall = data.partitions[i];
                if (!IsWallOnEdge(wall, cell, alongX)) continue;
                if (present) return;
                int offset = alongX ? cell.x - wall.cell.x : cell.z - wall.cell.z;
                data.partitions.RemoveAt(i);
                if (offset > 0) data.partitions.Add(WallSegment(wall, 0, offset, wall.id));
                if (offset + 1 < wall.lengthCells) data.partitions.Add(WallSegment(wall, offset + 1, wall.lengthCells - offset - 1, offset == 0 ? wall.id : wall.id + ":split:" + (offset + 1)));
            }
            if (!present) return;
            var normal = alongX ? new Vector3Int(0, 0, 1) : Vector3Int.right;
            bool a = data.floorCells.Contains(cell), b = data.floorCells.Contains(cell + normal);
            if (!a && !b) throw new InvalidOperationException("벽은 한쪽 이상에 바닥이 있는 경계에 놓으세요.");
            bool boundary = a != b;
            defaults = defaults ?? new FpsArenaWallAuthoringDefaults();
            float height = boundary ? defaults.boundaryHeight : defaults.InteriorHeight(data.floorHeight);
            float thickness = boundary ? defaults.boundaryThickness : defaults.interiorThickness;
            CheckWallDimensions(height, thickness, data.floorHeight);
            data.partitions.Add(new FpsArenaRoomWall { id = "wall:" + Guid.NewGuid().ToString("N"), cell = cell, alongX = alongX, lengthCells = 1, height = height, thickness = thickness });
        }

        // 긴 벽의 일부 경계를 선택/삭제할 때 동일한 경계 정규화를 사용한다.
        public static bool IsWallOnEdge(FpsArenaRoomWall wall, Vector3Int cell, bool alongX)
        {
            if (wall == null || wall.alongX != alongX || wall.cell.y != cell.y) return false;
            return alongX ? wall.cell.z == cell.z && cell.x >= wall.cell.x && cell.x < wall.cell.x + wall.lengthCells
                : wall.cell.x == cell.x && cell.z >= wall.cell.z && cell.z < wall.cell.z + wall.lengthCells;
        }

        // 삭제한 한 칸 외의 벽 구간을 원래 치수로 보존한다.
        private static FpsArenaRoomWall WallSegment(FpsArenaRoomWall source, int start, int length, string id)
        { return new FpsArenaRoomWall { id = id, cell = source.cell + source.Step * start, alongX = source.alongX, lengthCells = length, height = source.height, thickness = source.thickness }; }

        // 셀 내부 좌표에서 가장 가까운 네 경계를 고르며 동률은 상·우·하·좌 순서다.
        public static void ResolveWallEdge(Vector3Int cell, Vector2 localPoint, out Vector3Int anchor, out bool alongX)
        {
            float x = Mathf.Clamp01(localPoint.x), y = Mathf.Clamp01(localPoint.y);
            int side = 0; float closest = y;
            if (1 - x < closest) { side = 1; closest = 1 - x; }
            if (1 - y < closest) { side = 2; closest = 1 - y; }
            if (x < closest) side = 3;
            alongX = side == 0 || side == 2;
            anchor = cell + (side == 2 ? new Vector3Int(0, 0, -1) : side == 3 ? Vector3Int.left : Vector3Int.zero);
        }

        public static void AddStair(FpsArenaStageData data, Vector3Int cell, FpsArenaStairDirection direction)
        {
            if (cell.y + 1 >= data.floors) throw new InvalidOperationException("계단은 맨 위층 아래에서 배치하세요. 새 스테이지의 층 수를 2 이상으로 지정하세요.");
            var stair = new FpsArenaStair { x = cell.x, z = cell.z, lowerFloor = cell.y, direction = direction, width = 2, length = 4 };
            for (int lane = 0; lane < stair.width; lane++)
            {
                if (!data.floorCells.Contains(stair.BottomLane(lane)) || !data.floorCells.Contains(stair.TopLane(lane))) throw new InvalidOperationException("계단 앞뒤에 착지할 바닥을 남기세요. 가장자리에서 1셀 이상 안쪽에 놓으세요.");
                for (int i = 0; i < stair.length; i++)
                {
                    var c = stair.Cell(lane, i); var upper = c + Vector3Int.up;
                    if (!data.floorCells.Contains(c) || !data.floorCells.Contains(upper)) throw new InvalidOperationException("계단은 두 층의 바닥이 겹치는 빈 공간에 놓으세요.");
                    if (data.spawns.Contains(c) || data.spawns.Contains(upper) || data.content.Exists(p => Covers(p, c) || Covers(p, upper))) throw new InvalidOperationException("계단 위치의 스폰/콘텐츠를 먼저 이동하세요.");
                }
            }
            data.stairs.Add(stair);
            for (int lane = 0; lane < stair.width; lane++) for (int i = 0; i < stair.length; i++) for (int f = 0; f <= 1; f++) data.floorCells.Remove(stair.Cell(lane, i, f));
        }

        public static void RemoveStair(FpsArenaStageData data, int index)
        {
            var stair = data.stairs[index];
            for (int lane = 0; lane < stair.width; lane++) for (int i = 0; i < stair.length; i++) for (int f = 0; f <= 1; f++) { var c = stair.Cell(lane, i, f); if (!data.floorCells.Contains(c)) data.floorCells.Add(c); }
            data.stairs.RemoveAt(index);
        }
        public static bool Covers(FpsArenaContent p, Vector3Int c) { return c.y == p.cell.y && c.x >= p.cell.x && c.x < p.cell.x + p.widthCells && c.z >= p.cell.z && c.z < p.cell.z + p.depthCells; }

        public static FpsArenaGenerator Apply(FpsArenaStageAsset asset, FpsArenaGenerator generator)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 장면에 적용하세요.");
            asset.CreateLayout(); // Validate before touching the scene.
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("수동 스테이지 장면 적용");
            try
            {
                if (generator == null)
                {
                    var root = new GameObject("수동 스테이지"); Undo.RegisterCreatedObjectUndo(root, "수동 스테이지 추가");
                    generator = Undo.AddComponent<FpsArenaGenerator>(root);
                }
                var so = new SerializedObject(generator); so.Update();
                so.FindProperty("manualStage").objectReferenceValue = asset;
                so.FindProperty("manualCatalogOverride").objectReferenceValue = null;
                so.ApplyModifiedProperties();
                FpsArenaWindow.GenerateWithUndo(generator);
                EditorUtility.SetDirty(generator); EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                Undo.CollapseUndoOperations(group); return generator;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public static void RegisterPrefab(FpsArenaContentCatalog catalog, string key, FpsArenaContentKind kind, GameObject prefab)
        {
            if (catalog == null || !PrefabUtility.IsPartOfPrefabAsset(prefab)) throw new InvalidOperationException("프로젝트의 프리팹 자산과 카탈로그를 연결하세요.");
            if (string.IsNullOrWhiteSpace(key) || key != key.Trim()) throw new InvalidOperationException("앞뒤 공백 없는 콘텐츠 키를 입력하세요.");
            Vector3 size, center;
            if (!FpsArenaWindow.TryMeasurePrefabBounds(prefab, out size, out center)) throw new InvalidOperationException("프리팹의 Renderer/Collider에서 Bounds를 읽을 수 없습니다. 카탈로그 Inspector에서 직접 등록하세요.");
            var existing = catalog.Find(key);
            if (existing != null && existing.kind != kind) throw new InvalidOperationException("기존 키의 범주가 다릅니다. 새 키를 사용하세요.");
            Undo.RegisterCompleteObjectUndo(catalog, "콘텐츠 프리팹 등록/교체");
            if (existing == null) { existing = new FpsArenaCatalogEntry { contentKey = key, kind = kind }; catalog.entries.Add(existing); }
            existing.prefab = prefab; existing.authoredBounds = size; existing.authoredBoundsCenter = center;
            EditorUtility.SetDirty(catalog);
        }
    }
}
