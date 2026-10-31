using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public static class FpsArenaStageAuthoring
    {
        public static FpsArenaStageAsset CreateAsset(string path, FpsArenaStageData data, FpsArenaContentCatalog catalog = null)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("Assets 폴더의 새 .asset 경로를 선택하세요.");
            var asset = ScriptableObject.CreateInstance<FpsArenaStageAsset>();
            asset.data = data.Clone(); asset.contentCatalog = catalog;
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // Work on a copy: a failed edit cannot partly mutate the saved document.
        public static void Edit(FpsArenaStageAsset asset, string label, Action<FpsArenaStageData> edit)
        {
            if (asset == null || asset.data == null) throw new ArgumentNullException(nameof(asset));
            if (Application.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 스테이지를 편집하세요.");
            var candidate = asset.data.Clone(); edit(candidate);
            if (JsonUtility.ToJson(candidate) == JsonUtility.ToJson(asset.data)) return;
            Undo.RegisterCompleteObjectUndo(asset, label); asset.data = candidate; EditorUtility.SetDirty(asset);
        }

        public static FpsArenaContent MakeContent(FpsArenaStageData data, FpsArenaContentKind kind, Vector3Int cell, FpsArenaCatalogEntry entry = null)
        {
            if (entry != null && entry.kind != kind) throw new InvalidOperationException("선택한 프리팹의 범주가 다릅니다.");
            int w = entry != null ? entry.widthCells : 1, d = entry != null ? entry.depthCells : 1;
            float h = entry != null ? entry.height : kind == FpsArenaContentKind.Cover ? 1.1f : kind == FpsArenaContentKind.Enemy ? 1.8f : .6f;
            return new FpsArenaContent { id = "manual:" + Guid.NewGuid().ToString("N"), kind = kind, cell = cell, widthCells = w, depthCells = d, size = new Vector3(w * data.cellSize * .8f, Mathf.Min(h, data.floorHeight - .1f), d * data.cellSize * .8f), contentKey = entry != null ? entry.contentKey : "" };
        }

        public static void SetWall(FpsArenaStageData data, Vector3Int cell, bool alongX, bool present)
        {
            data.partitions.RemoveAll(w => w.cell == cell && w.alongX == alongX);
            if (present) data.partitions.Add(new FpsArenaRoomWall { id = "wall:" + Guid.NewGuid().ToString("N"), cell = cell, alongX = alongX, lengthCells = 1, height = data.floorHeight - .25f, thickness = .25f });
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
