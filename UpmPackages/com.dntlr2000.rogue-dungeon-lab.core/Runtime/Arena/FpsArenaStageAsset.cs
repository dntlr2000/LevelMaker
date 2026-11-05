using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RogueDungeonLab
{
    // A saved layout, not a generation recipe. No random state or project-specific references.
    [Serializable]
    public sealed class FpsArenaStageData
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public int width = 16, depth = 16, floors = 1;
        public float cellSize = 3, floorHeight = 4;
        public List<Vector3Int> floorCells = new List<Vector3Int>();
        public List<Vector3Int> spawns = new List<Vector3Int>();
        public List<FpsArenaStair> stairs = new List<FpsArenaStair>();
        public List<FpsArenaWall> walls = new List<FpsArenaWall>();
        public List<FpsArenaRoomWall> partitions = new List<FpsArenaRoomWall>();
        public List<FpsArenaContent> content = new List<FpsArenaContent>();

        public FpsArenaStageData Clone() { return JsonUtility.FromJson<FpsArenaStageData>(JsonUtility.ToJson(this)); }

        // 기존 세 인자 호출의 범위 보정과 기본 미터 규격을 유지한다.
        public static FpsArenaStageData Create(int width = 16, int depth = 16, int floors = 1)
        { return Create(Mathf.Clamp(width, 8, 64), Mathf.Clamp(depth, 8, 64), Mathf.Clamp(floors, 1, 4), 3, 4); }

        // 새 수동 맵의 셀/층간 실제 규격을 명시하며 잘못된 입력은 생성 전에 거부한다.
        public static FpsArenaStageData Create(int width, int depth, int floors, float cellSize, float floorHeight)
        {
            var data = new FpsArenaStageData { width = width, depth = depth, floors = floors, cellSize = cellSize, floorHeight = floorHeight };
            data.Normalized();
            for (int f = 0; f < data.floors; f++) for (int z = 0; z < data.depth; z++) for (int x = 0; x < data.width; x++) data.floorCells.Add(new Vector3Int(x, f, z));
            data.spawns.Add(new Vector3Int(1, 0, 1));
            return data;
        }

        // 절차 결과의 외곽벽까지 명시적으로 복사하며 수동 자산의 기존 벽 ID/분할은 유지한다.
        public static FpsArenaStageData Capture(FpsArenaLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var r = layout.Recipe;
            var data = new FpsArenaStageData { width = r.width, depth = r.depth, floors = r.floors, cellSize = r.cellSize, floorHeight = r.floorHeight };
            for (int f = 0; f < r.floors; f++) for (int z = 0; z < r.depth; z++) for (int x = 0; x < r.width; x++) if (layout.Floors[x, f, z]) data.floorCells.Add(new Vector3Int(x, f, z));
            data.spawns.AddRange(layout.Spawns); data.stairs.AddRange(layout.Stairs); data.walls.AddRange(layout.Walls); data.content.AddRange(layout.Content);
            AppendWalls(data.partitions, layout.RoomWalls, !layout.IsAuthored);
            AppendWalls(data.partitions, layout.BoundaryWalls, !layout.IsAuthored);
            data = data.Clone();
            if (layout.GeneratorVersion == 1)
                foreach (var p in data.content)
                {
                    float size = p.kind == FpsArenaContentKind.Item ? .5f : r.cellSize * .7f;
                    p.size = new Vector3(size, p.kind == FpsArenaContentKind.Item ? .35f : p.kind == FpsArenaContentKind.Cover ? r.coverHeight : r.obstacleHeight, size);
                }
            return data;
        }

        public bool Contains(Vector3Int c) { return c.x >= 0 && c.x < width && c.z >= 0 && c.z < depth && c.y >= 0 && c.y < floors; }

        // 구형 입력은 원본을 건드리지 않고 기존 가드를 포함한 명시적 v2 벽 데이터로 변환한다.
        public FpsArenaStageData Normalized()
        {
            if ((version != 1 && version != CurrentVersion) || width < 8 || width > 64 || depth < 8 || depth > 64 || floors < 1 || floors > 4 || !Finite(cellSize, 2, 5) || !Finite(floorHeight, 3, 6))
                throw new InvalidOperationException("수동 스테이지 크기/버전이 잘못되었습니다. 가로·세로 8–64셀, 1–4층, 셀 2–5m, 층 높이 3–6m를 사용하세요.");
            if (floorCells == null || spawns == null || stairs == null || walls == null || partitions == null || content == null) throw new InvalidOperationException("수동 스테이지 목록이 손상되었습니다.");
            var data = Clone();
            if (data.version == 1) { data.version = CurrentVersion; data.AddMissingBoundaryWalls(); }
            return data;
        }

        // 전체 층의 빈 경계만 채우고 기존 벽/ID/규격은 보존한다. ID 충돌 시 부분 수정하지 않는다.
        public int AddMissingBoundaryWalls()
        {
            if (version != CurrentVersion) throw new InvalidOperationException("편집형 벽으로 변환한 뒤 자동 배치하세요.");
            if (partitions == null || walls == null || content == null) throw new InvalidOperationException("스테이지 벽/콘텐츠 목록이 없습니다.");
            var edges = new HashSet<FpsArenaRoomEdge>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var wall in partitions)
            {
                if (wall == null || wall.lengthCells < 1 || wall.lengthCells > 64) throw new InvalidOperationException("벽 길이가 잘못되었습니다.");
                ids.Add(wall.id);
                for (int i = 0; i < wall.lengthCells; i++) { var c = wall.cell + wall.Step * i; edges.Add(new FpsArenaRoomEdge(c, c + wall.Normal)); }
            }
            foreach (var wall in walls) if (wall != null) ids.Add(wall.id);
            foreach (var item in content) if (item != null) ids.Add(item.id);
            var additions = new List<FpsArenaRoomWall>();
            foreach (var wall in FpsArenaBoundaryWalls.CreateForStage(this))
            {
                if (!edges.Add(new FpsArenaRoomEdge(wall.cell, wall.cell + wall.Normal))) continue;
                if (!ids.Add(wall.id)) throw new InvalidOperationException("자동 외곽벽 ID가 기존 오브젝트와 충돌합니다: " + wall.id);
                additions.Add(wall);
            }
            partitions.AddRange(additions); return additions.Count;
        }

        // 생성 결과의 긴 벽만 개별 편집 가능한 셀 경계로 나누고 저장된 수동 벽은 그대로 복사한다.
        private static void AppendWalls(List<FpsArenaRoomWall> destination, IEnumerable<FpsArenaRoomWall> walls, bool split)
        {
            foreach (var wall in walls)
            {
                if (!split) { destination.Add(wall); continue; }
                for (int i = 0; i < wall.lengthCells; i++) destination.Add(new FpsArenaRoomWall { id = wall.lengthCells == 1 ? wall.id : wall.id + ":" + i, cell = wall.cell + wall.Step * i, alongX = wall.alongX, lengthCells = 1, height = wall.height, thickness = wall.thickness });
            }
        }

        // 정규화한 저장 벽만 레이아웃으로 옮기므로 v2의 삭제된 외곽은 로드 시 재생성하지 않는다.
        public FpsArenaLayout ToLayout(bool validate = true)
        {
            var data = Normalized();
            var recipe = FpsArenaRecipe.CreateFlexible();
            recipe.width = width; recipe.depth = depth; recipe.floors = floors; recipe.cellSize = cellSize; recipe.floorHeight = floorHeight; recipe.shape = FpsArenaShape.Rectangle;
            var layout = new FpsArenaLayout { IsAuthored = true, Recipe = recipe, Floors = new bool[width, floors, depth], Reserved = new bool[width, floors, depth] };
            foreach (var c in data.floorCells)
            {
                if (!Contains(c) || layout.Floors[c.x, c.y, c.z]) throw new InvalidOperationException("바닥 좌표가 범위를 벗어나거나 중복됩니다: " + c);
                layout.Floors[c.x, c.y, c.z] = true;
            }
            layout.Spawns.AddRange(data.spawns); layout.Stairs.AddRange(data.stairs); layout.Walls.AddRange(data.walls); layout.Content.AddRange(data.content);
            foreach (var c in data.spawns) Reserve(layout, c);
            foreach (var s in data.stairs)
            {
                if (s == null || s.width < 1 || s.width > 4 || s.length < 2 || s.length > 16) throw new InvalidOperationException("계단 폭/길이가 잘못되었습니다.");
                for (int lane = 0; lane < s.width; lane++)
                {
                    Reserve(layout, s.BottomLane(lane)); Reserve(layout, s.TopLane(lane));
                    for (int step = 0; step < s.length; step++) { Reserve(layout, s.Cell(lane, step)); Reserve(layout, s.Cell(lane, step, 1)); }
                }
            }
            var boundaries = new List<FpsArenaRoomWall>();
            foreach (var wall in data.partitions)
            {
                if (wall == null || wall.lengthCells < 1 || wall.lengthCells > 64) throw new InvalidOperationException("벽 길이가 잘못되었습니다.");
                if (layout.HasFloor(wall.cell) && layout.HasFloor(wall.cell + wall.Normal)) layout.RoomWalls.Add(wall); else boundaries.Add(wall);
                for (int i = 0; i < wall.lengthCells; i++)
                {
                    var c = wall.cell + wall.Step * i;
                    layout.SolidRoomEdges.Add(new FpsArenaRoomEdge(c, c + wall.Normal));
                }
            }
            layout.SetBoundaryWalls(boundaries);
            if (validate) layout.Validate();
            layout.Hash = data.ComputeHash();
            return layout;
        }

        private static void Reserve(FpsArenaLayout l, Vector3Int c)
        { if (c.x >= 0 && c.x < l.Recipe.width && c.z >= 0 && c.z < l.Recipe.depth && c.y >= 0 && c.y < l.Recipe.floors) l.Reserved[c.x, c.y, c.z] = true; }

        // 명시적 벽을 포함한 정규화 v2 전체를 해시하여 삭제·변환 결과가 안정적으로 식별되게 한다.
        public string ComputeHash()
        {
            var copy = Normalized();
            copy.floorCells.Sort(CompareCell);
            // Spawn order is meaningful: the first entry is the player start.
            copy.stairs.Sort((a, b) => string.CompareOrdinal(JsonUtility.ToJson(a), JsonUtility.ToJson(b)));
            copy.walls.Sort((a, b) => string.CompareOrdinal(a.id, b.id)); copy.partitions.Sort((a, b) => string.CompareOrdinal(a.id, b.id)); copy.content.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(copy)))).Replace("-", "").ToLowerInvariant();
        }
        private static int CompareCell(Vector3Int a, Vector3Int b) { int c = a.y.CompareTo(b.y); if (c == 0) c = a.z.CompareTo(b.z); return c == 0 ? a.x.CompareTo(b.x) : c; }
        internal static bool Finite(float value, float min, float max) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max; }
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/수동 스테이지", fileName = "ManualStage")]
    public sealed class FpsArenaStageAsset : ScriptableObject
    {
        public FpsArenaStageData data = FpsArenaStageData.Create();
        public FpsArenaContentCatalog contentCatalog;
        [TextArea] public string authoringNote = "";
        [SerializeReference] public FpsArenaWallAuthoringDefaults wallDefaults;

        // 구형 자산은 필드를 초기화/저장하지 않고 기존 규격을 읽기 전용 복사본으로 제공한다.
        public FpsArenaWallAuthoringDefaults GetWallDefaults()
        { return wallDefaults != null ? wallDefaults.Clone() : new FpsArenaWallAuthoringDefaults(); }

        public FpsArenaLayout CreateLayout(FpsArenaContentCatalog catalogOverride = null)
        {
            if (data == null) throw new InvalidOperationException("수동 스테이지 데이터가 없습니다.");
            var layout = data.ToLayout();
            FpsArenaManualValidation.ValidateCatalog(layout, catalogOverride != null ? catalogOverride : contentCatalog);
            return layout;
        }
    }

    public sealed partial class FpsArenaLayout
    {
        public bool IsAuthored { get; internal set; }
    }

    public static class FpsArenaManualValidation
    {
        public static void ValidateCatalog(FpsArenaLayout layout, FpsArenaContentCatalog catalog)
        {
            if (catalog != null) catalog.Snapshot();
            foreach (var p in layout.Content)
            {
                if (string.IsNullOrEmpty(p.contentKey)) continue;
                var entry = catalog != null ? catalog.Find(p.contentKey) : null;
                if (entry == null || entry.prefab == null || entry.kind != p.kind) throw new InvalidOperationException("콘텐츠 '" + p.contentKey + "'의 같은 범주 프리팹을 카탈로그에 연결하세요. 위치: " + p.cell);
            }
        }

        // 수동 제작에 필수인 구조·겹침만 차단하고 전체 연결성은 별도 경고로 조회한다.
        public static void Validate(FpsArenaLayout layout)
        {
            var r = layout.Recipe;
            if (layout.Spawns.Count == 0) Fail("스폰 도구로 시작 위치를 지정하세요.");
            var occupied = new HashSet<Vector3Int>(); var ids = new HashSet<string>(); var stairCells = new HashSet<Vector3Int>();
            foreach (var s in layout.Stairs)
            {
                if (s.lowerFloor < 0 || s.lowerFloor + 1 >= r.floors || (int)s.direction < 0 || (int)s.direction > 3) Fail("계단의 층 또는 방향이 잘못되었습니다.");
                for (int lane = 0; lane < s.width; lane++)
                {
                    if (!layout.HasFloor(s.BottomLane(lane)) || !layout.HasFloor(s.TopLane(lane))) Fail("계단 양쪽 착지 바닥이 필요합니다: " + s.BottomLane(lane));
                    for (int step = 0; step < s.length; step++)
                    {
                        var c = s.Cell(lane, step); var upper = c + Vector3Int.up;
                        if (c.x < 0 || c.x >= r.width || c.z < 0 || c.z >= r.depth || layout.HasFloor(c) || layout.HasFloor(upper) || !stairCells.Add(c) || !stairCells.Add(upper)) Fail("계단 공간·상층 개구부 또는 다른 계단과의 겹침을 확인하세요: " + c);
                    }
                }
            }
            foreach (var spawn in layout.Spawns)
                if (!layout.HasFloor(spawn) || stairCells.Contains(spawn) || !occupied.Add(spawn)) Fail("스폰은 서로 다른 평평한 바닥에 놓으세요: " + spawn);
            var edges = new HashSet<FpsArenaRoomEdge>();
            var allWalls = new List<FpsArenaRoomWall>(layout.RoomWalls); allWalls.AddRange(layout.BoundaryWalls);
            foreach (var w in allWalls)
            {
                if (w == null || w.lengthCells < 1 || w.lengthCells > 64) Fail("벽 길이가 잘못되었습니다.");
                CheckId(ids, w.id); CheckWall(w.height, w.thickness, r.floorHeight);
                for (int i = 0; i < w.lengthCells; i++)
                {
                    var a = w.cell + w.Step * i; var b = a + w.Normal;
                    if ((!layout.HasFloor(a) && !layout.HasFloor(b)) || stairCells.Contains(a) || stairCells.Contains(b) || !edges.Add(new FpsArenaRoomEdge(a, b))) Fail("벽은 계단과 겹치지 않고 한쪽 이상에 바닥이 있는 경계에 놓으세요: " + a);
                }
            }
            foreach (var w in layout.Walls)
            {
                if (w == null) Fail("벽 데이터가 없습니다."); CheckId(ids, w.id); CheckWall(w.height, w.thickness, r.floorHeight);
                if (w.lengthCells < 1 || w.lengthCells > 64 || w.doorWidthCells < 0 || w.doorOffsetCells < 0 || (w.doorWidthCells > 0 && w.doorOffsetCells + w.doorWidthCells > w.lengthCells)) Fail("벽/출입구 길이가 잘못되었습니다.");
                foreach (var c in w.Footprint()) if (!layout.HasFloor(c) || layout.Reserved[c.x, c.y, c.z] || !occupied.Add(c)) Fail("벽이 스폰·계단·다른 구조물과 겹칩니다: " + c);
            }
            foreach (var p in layout.Content)
            {
                if (p == null) Fail("콘텐츠 데이터가 없습니다."); CheckId(ids, p.id);
                if (!Enum.IsDefined(typeof(FpsArenaContentKind), p.kind) || p.widthCells < 1 || p.widthCells > 4 || p.depthCells < 1 || p.depthCells > 4 || !FpsArenaStageData.Finite(p.size.x, .01f, p.widthCells * r.cellSize) || !FpsArenaStageData.Finite(p.size.z, .01f, p.depthCells * r.cellSize) || !FpsArenaStageData.Finite(p.size.y, .01f, r.floorHeight - .1f) || !FpsArenaStageData.Finite(p.yaw, -36000, 36000)) Fail("콘텐츠 크기·회전을 확인하세요: " + p.id);
                // Rotated bounds must remain inside the footprint advertised to collision/path checks.
                float sin = Mathf.Abs(Mathf.Sin(p.yaw * Mathf.Deg2Rad)), cos = Mathf.Abs(Mathf.Cos(p.yaw * Mathf.Deg2Rad));
                if (cos * p.size.x + sin * p.size.z > p.widthCells * r.cellSize + .001f || sin * p.size.x + cos * p.size.z > p.depthCells * r.cellSize + .001f) Fail("회전한 콘텐츠가 점유 셀을 벗어납니다. 크기를 줄이세요: " + p.cell);
                foreach (var c in p.Footprint())
                {
                    if (!layout.HasFloor(c) || layout.Reserved[c.x, c.y, c.z] || !occupied.Add(c)) Fail("콘텐츠는 스폰·계단·다른 콘텐츠와 겹치지 않는 바닥에 놓으세요: " + c);
                    if (c.x > p.cell.x && !layout.CanTraverse(c, c + Vector3Int.left) || c.z > p.cell.z && !layout.CanTraverse(c, c + new Vector3Int(0, 0, -1))) Fail("콘텐츠가 구획벽을 가로지릅니다: " + c);
                }
            }
        }

        // 필수 검사를 통과한 레이아웃의 격자 도달성을 참고용으로 계산하며 적용을 차단하지 않는다.
        public static string GetConnectivityWarning(FpsArenaLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var blocked = layout.BlockedCells();
            int unreachable = layout.FloorCount() - blocked.Count - layout.ReachableCount(blocked);
            return unreachable > 0
                ? "격자 기준으로 시작 스폰에서 갈 수 없는 바닥이 " + unreachable + "셀 있습니다. 연결성은 참고용 경고이며, 격리된 구역을 포함해 장면 적용이 가능합니다."
                : "";
        }
        private static void CheckId(HashSet<string> ids, string id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) Fail("콘텐츠/벽 ID가 비어 있거나 중복됩니다."); }
        private static void CheckWall(float height, float thickness, float floorHeight) { if (!FpsArenaStageData.Finite(height, .1f, floorHeight - .1f) || !FpsArenaStageData.Finite(thickness, .1f, 1)) Fail("벽 높이/두께가 잘못되었습니다."); }
        private static void Fail(string message) { throw new InvalidOperationException(message); }
    }
}
