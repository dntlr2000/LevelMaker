using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueDungeonLab
{
    public static class DungeonLayoutGenerator
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // 기존 설정 자산을 정규화한 스냅샷으로 변환해 legacy layout을 생성합니다.
        public static DungeonLayout Generate(RogueDungeonSettings settings, int seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ClampValues();
            return Generate(DungeonRecipeSnapshot.Capture(settings), seed);
        }

        // GameObject나 ScriptableObject 변경 없이 레시피 스냅샷에서 legacy layout을 생성합니다.
        public static DungeonLayout Generate(DungeonRecipeSnapshot recipe, int seed)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            DungeonLayout layout = new DungeonLayout(recipe.stageWidthCells, recipe.stageDepthCells);
            System.Random random = new System.Random(seed);
            PlaceRooms(layout, recipe, random);
            EnsureFallbackRoom(layout, recipe);
            ConnectRooms(layout, recipe, random);
            CalculateDistances(layout);
            ChooseExit(layout);
            return layout;
        }

        // 설정을 정규화한 뒤 프로젝트 소유 PCG32 Layout 스트림으로 StableV2 레이아웃을 생성합니다.
        public static DungeonLayout GenerateStableV2(RogueDungeonSettings settings, int seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ClampValues();
            return GenerateStableV2(DungeonRecipeSnapshot.Capture(settings), seed);
        }

        // 난수 알고리즘·순회 순서·호출 순서가 모두 StableV2 계약인 GameObject 없는 레이아웃 경로입니다.
        public static DungeonLayout GenerateStableV2(DungeonRecipeSnapshot recipe, int seed)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            DungeonLayout layout = new DungeonLayout(recipe.stageWidthCells, recipe.stageDepthCells);
            DungeonStableRandom random = DungeonStableRandomStreams.Create(seed, DungeonStableRandomStreams.Layout);
            PlaceRoomsStableV2(layout, recipe, random);
            EnsureFallbackRoom(layout, recipe);
            ConnectRoomsStableV2(layout, recipe, random);
            CalculateDistances(layout);
            ChooseExit(layout);
            return layout;
        }

        // 겹치지 않는 사각 방을 목표 개수와 시도 제한 안에서 배치합니다.
        private static void PlaceRooms(DungeonLayout layout, DungeonRecipeSnapshot settings, System.Random random)
        {
            int attempts = 0;
            int totalAttempts = settings.desiredRoomCount * settings.roomPlacementAttempts;
            while (layout.Rooms.Count < settings.desiredRoomCount && attempts++ < totalAttempts)
            {
                int rw = random.Next(settings.roomSizeMin.x, settings.roomSizeMax.x + 1);
                int rd = random.Next(settings.roomSizeMin.y, settings.roomSizeMax.y + 1);
                int maxX = layout.Width - rw - 1;
                int maxZ = layout.Depth - rd - 1;
                if (maxX <= 1 || maxZ <= 1) break;
                RectInt candidate = new RectInt(random.Next(1, maxX + 1), random.Next(1, maxZ + 1), rw, rd);
                if (!CanPlace(candidate, layout.Rooms, layout.Width, layout.Depth)) continue;
                int id = layout.Rooms.Count;
                layout.AddRoom(candidate);
                for (int x = candidate.xMin; x < candidate.xMax; x++)
                for (int z = candidate.yMin; z < candidate.yMax; z++)
                    layout.SetFloor(new Vector2Int(x, z), id, false);
            }
        }

        // Legacy와 같은 배치 규칙을 쓰되 오직 StableV2 Layout 스트림만 소비합니다.
        private static void PlaceRoomsStableV2(DungeonLayout layout, DungeonRecipeSnapshot settings, DungeonStableRandom random)
        {
            int attempts = 0;
            int totalAttempts = settings.desiredRoomCount * settings.roomPlacementAttempts;
            while (layout.Rooms.Count < settings.desiredRoomCount && attempts++ < totalAttempts)
            {
                int rw = random.NextInt(settings.roomSizeMin.x, settings.roomSizeMax.x + 1);
                int rd = random.NextInt(settings.roomSizeMin.y, settings.roomSizeMax.y + 1);
                int maxX = layout.Width - rw - 1;
                int maxZ = layout.Depth - rd - 1;
                if (maxX <= 1 || maxZ <= 1) break;
                RectInt candidate = new RectInt(random.NextInt(1, maxX + 1), random.NextInt(1, maxZ + 1), rw, rd);
                if (!CanPlace(candidate, layout.Rooms, layout.Width, layout.Depth)) continue;
                int id = layout.Rooms.Count;
                layout.AddRoom(candidate);
                for (int x = candidate.xMin; x < candidate.xMax; x++)
                for (int z = candidate.yMin; z < candidate.yMax; z++)
                    layout.SetFloor(new Vector2Int(x, z), id, false);
            }
        }

        // 후보 방이 외곽 여백과 기존 방 사이 한 셀 간격을 지키는지 확인합니다.
        private static bool CanPlace(RectInt candidate, IReadOnlyList<RectInt> rooms, int width, int depth)
        {
            RectInt padded = new RectInt(candidate.xMin - 1, candidate.yMin - 1, candidate.width + 2, candidate.height + 2);
            if (padded.xMin < 0 || padded.yMin < 0 || padded.xMax >= width || padded.yMax >= depth) return false;
            for (int i = 0; i < rooms.Count; i++) if (padded.Overlaps(rooms[i])) return false;
            return true;
        }

        // 모든 무작위 배치가 실패했을 때 중앙에 최소 fallback 방을 만듭니다.
        private static void EnsureFallbackRoom(DungeonLayout layout, DungeonRecipeSnapshot settings)
        {
            if (layout.Rooms.Count > 0) return;
            int rw = Mathf.Clamp(settings.roomSizeMin.x, 3, layout.Width - 4);
            int rd = Mathf.Clamp(settings.roomSizeMin.y, 3, layout.Depth - 4);
            RectInt room = new RectInt((layout.Width - rw) / 2, (layout.Depth - rd) / 2, rw, rd);
            layout.AddRoom(room);
            for (int x = room.xMin; x < room.xMax; x++)
            for (int z = room.yMin; z < room.yMax; z++)
                layout.SetFloor(new Vector2Int(x, z), 0, false);
        }

        // 가장 가까운 미연결 방 연결과 확률적 추가 루프로 전체 방을 연결합니다.
        private static void ConnectRooms(DungeonLayout layout, DungeonRecipeSnapshot settings, System.Random random)
        {
            if (layout.Rooms.Count <= 1) return;
            List<int> unconnected = new List<int>();
            for (int i = 1; i < layout.Rooms.Count; i++) unconnected.Add(i);
            int current = 0;
            while (unconnected.Count > 0)
            {
                Vector2Int from = Center(layout.Rooms[current]);
                int nearestListIndex = 0;
                int nearestDistance = int.MaxValue;
                for (int i = 0; i < unconnected.Count; i++)
                {
                    int d = Manhattan(from, Center(layout.Rooms[unconnected[i]]));
                    if (d < nearestDistance) { nearestDistance = d; nearestListIndex = i; }
                }
                int next = unconnected[nearestListIndex];
                CarveConnection(layout, from, Center(layout.Rooms[next]), settings.corridorWidthCells, random.NextDouble() < 0.5);
                current = next;
                unconnected.RemoveAt(nearestListIndex);
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (random.NextDouble() > settings.extraConnectionChance) continue;
                int other = random.Next(0, layout.Rooms.Count);
                if (other == i) other = (other + 1) % layout.Rooms.Count;
                CarveConnection(layout, Center(layout.Rooms[i]), Center(layout.Rooms[other]), settings.corridorWidthCells, random.NextDouble() < 0.5);
            }
        }

        // Legacy와 같은 연결 규칙을 쓰되 StableV2 Layout 스트림 외의 호출 수와 완전히 분리합니다.
        private static void ConnectRoomsStableV2(DungeonLayout layout, DungeonRecipeSnapshot settings, DungeonStableRandom random)
        {
            if (layout.Rooms.Count <= 1) return;
            List<int> unconnected = new List<int>();
            for (int i = 1; i < layout.Rooms.Count; i++) unconnected.Add(i);
            int current = 0;
            while (unconnected.Count > 0)
            {
                Vector2Int from = Center(layout.Rooms[current]);
                int nearestListIndex = 0;
                int nearestDistance = int.MaxValue;
                for (int i = 0; i < unconnected.Count; i++)
                {
                    int distance = Manhattan(from, Center(layout.Rooms[unconnected[i]]));
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestListIndex = i;
                    }
                }
                int next = unconnected[nearestListIndex];
                CarveConnection(layout, from, Center(layout.Rooms[next]), settings.corridorWidthCells, random.NextDouble01() < 0.5);
                current = next;
                unconnected.RemoveAt(nearestListIndex);
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (random.NextDouble01() > settings.extraConnectionChance) continue;
                int other = random.NextInt(0, layout.Rooms.Count);
                if (other == i) other = (other + 1) % layout.Rooms.Count;
                CarveConnection(layout, Center(layout.Rooms[i]), Center(layout.Rooms[other]), settings.corridorWidthCells, random.NextDouble01() < 0.5);
            }
        }

        // 두 지점을 수평 또는 수직 우선 L자 복도로 연결합니다.
        private static void CarveConnection(DungeonLayout layout, Vector2Int from, Vector2Int to, int width, bool horizontalFirst)
        {
            Vector2Int corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            CarveLine(layout, from, corner, width);
            CarveLine(layout, corner, to, width);
        }

        // 지정 폭의 직선 floor 셀을 두 지점 사이에 새깁니다.
        private static void CarveLine(DungeonLayout layout, Vector2Int from, Vector2Int to, int width)
        {
            int minOffset = -(width / 2);
            int maxOffset = minOffset + width - 1;
            if (from.x != to.x)
            {
                for (int x = Mathf.Min(from.x, to.x); x <= Mathf.Max(from.x, to.x); x++)
                for (int o = minOffset; o <= maxOffset; o++) layout.SetFloor(new Vector2Int(x, from.y + o), -1, true);
            }
            else
            {
                for (int z = Mathf.Min(from.y, to.y); z <= Mathf.Max(from.y, to.y); z++)
                for (int o = minOffset; o <= maxOffset; o++) layout.SetFloor(new Vector2Int(from.x + o, z), -1, true);
            }
        }

        // 첫 방 중심을 입구로 두고 모든 floor 셀의 BFS 거리를 계산합니다.
        private static void CalculateDistances(DungeonLayout layout)
        {
            layout.Entrance = Center(layout.Rooms[0]);
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(layout.Entrance);
            layout.SetDistance(layout.Entrance, 0);
            int max = 0;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                int nextDistance = layout.GetDistance(cell) + 1;
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = cell + Directions[i];
                    if (!layout.IsFloor(next) || layout.GetDistance(next) >= 0) continue;
                    layout.SetDistance(next, nextDistance);
                    max = Mathf.Max(max, nextDistance);
                    queue.Enqueue(next);
                }
            }
            layout.MaxDistance = max;
        }

        // 입구에서 가장 먼 방 중심을 우선 출구로 선택합니다.
        private static void ChooseExit(DungeonLayout layout)
        {
            Vector2Int farthest = layout.Entrance;
            int farthestDistance = -1;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Vector2Int center = Center(layout.Rooms[i]);
                int d = layout.GetDistance(center);
                if (d > farthestDistance) { farthestDistance = d; farthest = center; }
            }
            if (farthestDistance <= 0)
            {
                foreach (Vector2Int cell in layout.EnumerateFloorCells())
                {
                    int d = layout.GetDistance(cell);
                    if (d > farthestDistance) { farthestDistance = d; farthest = cell; }
                }
            }
            layout.Exit = farthest;
            layout.MaxDistance = Mathf.Max(1, layout.GetDistance(layout.Exit));
        }

        // 사각 방의 정수 셀 중심을 반환합니다.
        private static Vector2Int Center(RectInt r) { return new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2); }
        // 두 셀 사이 Manhattan 거리를 계산합니다.
        private static int Manhattan(Vector2Int a, Vector2Int b) { return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); }
    }

    internal static class PrototypeMaterials
    {
        private static readonly Dictionary<Color32, Material> Custom = new Dictionary<Color32, Material>();
        private static Material s_floor, s_wall, s_enemy, s_breakable, s_prop, s_gimmick, s_entrance, s_exit;
        public static Material Floor { get { return s_floor != null ? s_floor : (s_floor = Create("Lab Floor", new Color(0.24f, 0.27f, 0.31f))); } }
        public static Material Wall { get { return s_wall != null ? s_wall : (s_wall = Create("Lab Wall", new Color(0.11f, 0.13f, 0.17f))); } }
        public static Material Enemy { get { return s_enemy != null ? s_enemy : (s_enemy = Create("Lab Enemy", new Color(0.85f, 0.16f, 0.18f))); } }
        public static Material Breakable { get { return s_breakable != null ? s_breakable : (s_breakable = Create("Lab Breakable", new Color(0.92f, 0.48f, 0.12f))); } }
        public static Material Prop { get { return s_prop != null ? s_prop : (s_prop = Create("Lab Prop", new Color(0.26f, 0.48f, 0.31f))); } }
        public static Material Gimmick { get { return s_gimmick != null ? s_gimmick : (s_gimmick = Create("Lab Gimmick", new Color(0.15f, 0.78f, 0.9f))); } }
        public static Material Entrance { get { return s_entrance != null ? s_entrance : (s_entrance = Create("Lab Entrance", new Color(0.25f, 0.55f, 1f))); } }
        public static Material Exit { get { return s_exit != null ? s_exit : (s_exit = Create("Lab Exit", new Color(0.95f, 0.25f, 0.85f))); } }
        public static Material ForColor(Color color)
        {
            Color32 key = color;
            Material m;
            if (!Custom.TryGetValue(key, out m) || m == null) { m = Create("Lab Drop", color); Custom[key] = m; }
            return m;
        }
        private static Material Create(string name, Color color)
        {
            Shader shader = ResolveShader();
            Material m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            return m;
        }
        private static Shader ResolveShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string n = pipeline.GetType().Name;
                if (n.Contains("Universal")) { Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (s != null) return s; }
                if (n.Contains("HDRender") || n.Contains("HighDefinition")) { Shader s = Shader.Find("HDRP/Lit"); if (s != null) return s; }
            }
            Shader standard = Shader.Find("Standard"); if (standard != null) return standard;
            Shader unlit = Shader.Find("Unlit/Color"); if (unlit != null) return unlit;
            return Shader.Find("Hidden/InternalErrorShader");
        }
    }

    public static class DungeonMeshBuilder
    {
        // 기존 DungeonLayout API를 유지하면서 공통 수치 기반 메시 구축으로 전달합니다.
        public static int Build(Transform parent, DungeonLayout layout, RogueDungeonSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Build(parent, layout, settings.cellSize, settings.wallHeight);
        }

        // Blueprint를 legacy layout projection으로 변환해 동일한 합성 메시를 구축합니다.
        public static int Build(Transform parent, DungeonBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (blueprint.grid == null) throw new ArgumentException("Blueprint grid is missing.", nameof(blueprint));
            DungeonLayout layout = DungeonBlueprintLayoutConverter.ToLayout(blueprint);
            return Build(parent, layout, blueprint.grid.cellSize, blueprint.grid.wallHeight);
        }

        // layout 셀과 월드 배율로 floor·wall 메시와 정적 collider를 생성합니다.
        private static int Build(Transform parent, DungeonLayout layout, float cellSize, float wallHeight)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            GameObject root = new GameObject("Geometry"); root.transform.SetParent(parent, false);
            Mesh floor = BuildFloor(layout, cellSize);
            Mesh walls = BuildWalls(layout, cellSize, wallHeight);
            DungeonGeneratedMeshOwner owner = root.AddComponent<DungeonGeneratedMeshOwner>();
            owner.hideFlags = HideFlags.HideInInspector;
            owner.Initialize(floor, walls);
            CreateMeshObject("Floor", root.transform, floor, PrototypeMaterials.Floor);
            CreateMeshObject("Walls", root.transform, walls, PrototypeMaterials.Wall);
            return (floor.triangles.Length + walls.triangles.Length) / 3;
        }

        // 모든 floor 셀을 기존 x-z 순서의 quad 목록으로 합칩니다.
        private static Mesh BuildFloor(DungeonLayout layout, float cellSize)
        {
            List<Vector3> v = new List<Vector3>(layout.WalkableCellCount * 4);
            List<Vector3> n = new List<Vector3>(layout.WalkableCellCount * 4);
            List<Vector2> uv = new List<Vector2>(layout.WalkableCellCount * 4);
            List<int> t = new List<int>(layout.WalkableCellCount * 6);
            float h = cellSize * 0.5f;
            foreach (Vector2Int cell in layout.EnumerateFloorCells())
            {
                Vector3 c = layout.CellToLocalPosition(cell, cellSize); int s = v.Count;
                v.Add(c + new Vector3(-h, 0, -h)); v.Add(c + new Vector3(-h, 0, h)); v.Add(c + new Vector3(h, 0, h)); v.Add(c + new Vector3(h, 0, -h));
                n.Add(Vector3.up); n.Add(Vector3.up); n.Add(Vector3.up); n.Add(Vector3.up);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            Mesh mesh = new Mesh { name = "Generated Dungeon Floor", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }

        // floor-to-void 경계마다 기존 box 벽을 추가해 하나의 메시로 합칩니다.
        private static Mesh BuildWalls(DungeonLayout layout, float cellSize, float wallHeight)
        {
            List<Vector3> v = new List<Vector3>(); List<Vector3> n = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> t = new List<int>();
            float h = cellSize * 0.5f; float thickness = Mathf.Max(0.12f, cellSize * 0.08f);
            foreach (Vector2Int cell in layout.EnumerateFloorCells())
            {
                Vector3 c = layout.CellToLocalPosition(cell, cellSize);
                if (!layout.IsFloor(cell + Vector2Int.up)) AddBox(v, n, uv, t, c + new Vector3(0, wallHeight * 0.5f, h), new Vector3(cellSize + thickness, wallHeight, thickness));
                if (!layout.IsFloor(cell + Vector2Int.down)) AddBox(v, n, uv, t, c + new Vector3(0, wallHeight * 0.5f, -h), new Vector3(cellSize + thickness, wallHeight, thickness));
                if (!layout.IsFloor(cell + Vector2Int.right)) AddBox(v, n, uv, t, c + new Vector3(h, wallHeight * 0.5f, 0), new Vector3(thickness, wallHeight, cellSize + thickness));
                if (!layout.IsFloor(cell + Vector2Int.left)) AddBox(v, n, uv, t, c + new Vector3(-h, wallHeight * 0.5f, 0), new Vector3(thickness, wallHeight, cellSize + thickness));
            }
            Mesh mesh = new Mesh { name = "Generated Dungeon Walls", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }

        // 중심과 크기로 정의한 box의 여섯 면을 메시 버퍼에 추가합니다.
        private static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            AddFace(v,n,uv,t,c+new Vector3(-h.x,-h.y,h.z),c+new Vector3(h.x,-h.y,h.z),c+new Vector3(h.x,h.y,h.z),c+new Vector3(-h.x,h.y,h.z),Vector3.forward);
            AddFace(v,n,uv,t,c+new Vector3(h.x,-h.y,-h.z),c+new Vector3(-h.x,-h.y,-h.z),c+new Vector3(-h.x,h.y,-h.z),c+new Vector3(h.x,h.y,-h.z),Vector3.back);
            AddFace(v,n,uv,t,c+new Vector3(h.x,-h.y,h.z),c+new Vector3(h.x,-h.y,-h.z),c+new Vector3(h.x,h.y,-h.z),c+new Vector3(h.x,h.y,h.z),Vector3.right);
            AddFace(v,n,uv,t,c+new Vector3(-h.x,-h.y,-h.z),c+new Vector3(-h.x,-h.y,h.z),c+new Vector3(-h.x,h.y,h.z),c+new Vector3(-h.x,h.y,-h.z),Vector3.left);
            AddFace(v,n,uv,t,c+new Vector3(-h.x,h.y,h.z),c+new Vector3(h.x,h.y,h.z),c+new Vector3(h.x,h.y,-h.z),c+new Vector3(-h.x,h.y,-h.z),Vector3.up);
            AddFace(v,n,uv,t,c+new Vector3(-h.x,-h.y,-h.z),c+new Vector3(h.x,-h.y,-h.z),c+new Vector3(h.x,-h.y,h.z),c+new Vector3(-h.x,-h.y,h.z),Vector3.down);
        }
        // 네 꼭짓점과 법선으로 quad 한 면과 두 삼각형을 추가합니다.
        private static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int s = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
            t.Add(s); t.Add(s+1); t.Add(s+2); t.Add(s); t.Add(s+2); t.Add(s+3);
        }
        // 합성 메시 오브젝트에 렌더링 구성과 플레이어 이동용 정적 충돌체를 함께 설정합니다.
        private static void CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            MeshCollider collider = go.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
        }
    }

    public struct ContentSpawnCounts { public int EnemyCount, DestructibleCount, PropCount, GimmickCount; }

    public static class DungeonContentSpawner
    {
        // 기존 layout 기반 API를 보존하면서 데이터 전용 planner와 레코드 기반 builder로 전달합니다.
        public static ContentSpawnCounts Spawn(Transform parent, DungeonLayout layout, RogueDungeonSettings settings, int seed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ClampValues();
            DungeonRecipeSnapshot recipe = DungeonRecipeSnapshot.Capture(settings);
            DungeonContentPlan plan = DungeonContentPlanner.Plan(layout, recipe, seed);
            return DungeonContentSceneBuilder.Build(parent, plan.Spawns, settings);
        }

        // Blueprint 기반 호출자가 확정된 spawn 레코드만으로 콘텐츠를 재구축하게 합니다.
        public static ContentSpawnCounts Spawn(Transform parent, DungeonBlueprint blueprint, RogueDungeonSettings settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return DungeonContentSceneBuilder.Build(parent, blueprint.spawns, settings);
        }
    }

    [DisallowMultipleComponent]
    public sealed partial class RogueDungeonGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = DungeonStageLoader.GeneratedRootName;
        public RogueDungeonSettings settings;
        public DungeonStageDefinition stageDefinition;
        private DungeonLayout _layout;
        private DungeonBlueprint _blueprint;
        private DungeonStageInstance _stageInstance;
        private RogueDungeonSettings _activeRuntimeSettings;
        private GenerationReport _report;
        private int _activeSeed;
        private bool _hasGenerated;
        public event Action<GenerationReport> GenerationCompleted;
        public DungeonLayout CurrentLayout { get { return _layout; } }
        public DungeonBlueprint CurrentBlueprint { get { return _blueprint; } }
        public DungeonStageInstance CurrentStageInstance { get { return _stageInstance; } }
        public GenerationReport LastReport { get { return _report; } }
        public int ActiveSeed { get { return _activeSeed; } }
        public float CurrentCellSize
        {
            get
            {
                if (_blueprint != null && _blueprint.grid != null) return _blueprint.grid.cellSize;
                return settings != null ? settings.cellSize : 3f;
            }
        }
        public Bounds GeneratedBounds { get { return _report != null ? _report.worldBounds : CalculateBounds(); } }

        // Play 진입 시 StageDefinition을 우선하고, 없으면 기존 settings 자동 생성 계약을 유지합니다.
        private void Start()
        {
            if (!Application.isPlaying) return;
            if (stageDefinition != null && stageDefinition.loadOnPlay)
            {
                LoadStageDefinition();
                return;
            }
            if (settings != null && settings.generateOnPlay) GenerateWithSeed(settings.seed);
        }

        // 기존 settings 자산에 기록된 시드로 절차 던전을 생성합니다.
        [ContextMenu("Generate From Settings Seed")]
        public void GenerateFromSettings()
        {
            if (settings != null) GenerateWithSeed(settings.seed);
            else UnityEngine.Debug.LogWarning("Assign RogueDungeonSettings first.", this);
        }

        // 현재 출처와 활성 시드를 유지해 절차 맵 또는 저장 맵을 다시 구축합니다.
        [ContextMenu("Regenerate Active Seed")]
        public void RegenerateActiveSeed()
        {
            if (IsStageDefinitionActive())
            {
                if (stageDefinition.sourceMode == DungeonStageSourceMode.SavedBlueprint) LoadStageDefinition();
                else LoadStageDefinitionWithSeed(_activeSeed);
                return;
            }
            if (settings != null) GenerateWithSeed(_hasGenerated ? _activeSeed : settings.seed);
        }

        // 절차 출처에는 새 임의 시드를 적용하고 저장 출처에는 저장된 Blueprint를 다시 구축합니다.
        [ContextMenu("Generate New Random Seed")]
        public void GenerateNewSeed()
        {
            if (stageDefinition != null && (IsStageDefinitionActive() || settings == null))
            {
                if (stageDefinition.sourceMode == DungeonStageSourceMode.SavedBlueprint) LoadStageDefinition();
                else LoadStageDefinitionWithSeed(DungeonStageSeedResolver.CreateRandomSeed());
                return;
            }
            if (settings != null) GenerateWithSeed(DungeonStageSeedResolver.CreateRandomSeed());
            else UnityEngine.Debug.LogWarning("Assign RogueDungeonSettings or DungeonStageDefinition first.", this);
        }

        // 지정 시드와 LegacyV1 요청을 Blueprint로 확정한 뒤 메시·콘텐츠와 드랍 검증 서비스를 생성합니다.
        public void GenerateWithSeed(int seed)
        {
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning("Assign RogueDungeonSettings first.", this);
                return;
            }
            settings.ClampValues();
            DungeonStageInstance instance = DungeonStageLoader.LoadProcedural(transform, settings, seed, settings);
            ApplyStageInstance(instance);
        }

        // 할당된 StageDefinition의 seed policy 또는 저장 Blueprint를 사용해 스테이지를 로드합니다.
        [ContextMenu("Load Stage Definition")]
        public void LoadStageDefinition()
        {
            LoadStageDefinitionInternal(null);
        }

        // Procedural StageDefinition에는 명시 시드를 우선 적용하고 SavedBlueprint에는 저장 시드를 유지합니다.
        public void LoadStageDefinitionWithSeed(int seed)
        {
            LoadStageDefinitionInternal(seed);
        }

        // Loader가 소유한 generated root 정리 경로로 기존 공개 API를 유지합니다.
        [ContextMenu("Clear Generated Dungeon")]
        public void ClearGenerated()
        {
            DungeonStageLoader.ClearGenerated(transform);
        }

        // 현재 구축에 사용한 설정 또는 기본 테이블에서 드랍 검증용 테이블을 반환합니다.
        public WeightedDropTable GetEffectiveDropTable(DropSourceKind kind)
        {
            RogueDungeonSettings active = _activeRuntimeSettings != null ? _activeRuntimeSettings : settings;
            if (active == null) return kind == DropSourceKind.Enemy ? RuntimeDropTables.Enemy : RuntimeDropTables.Destructible;
            return kind == DropSourceKind.Enemy ? active.EffectiveEnemyDropTable : active.EffectiveDestructibleDropTable;
        }

        // StageDefinition 로드 문맥을 만들고 선택적 명시 시드와 함께 Loader를 실행합니다.
        private void LoadStageDefinitionInternal(int? explicitSeed)
        {
            if (stageDefinition == null)
            {
                UnityEngine.Debug.LogWarning("Assign DungeonStageDefinition first.", this);
                return;
            }
            RogueDungeonSettings runtimeSettings = settings != null
                ? settings
                : stageDefinition.sourceMode == DungeonStageSourceMode.Procedural ? stageDefinition.recipe : null;
            DungeonLoadContext context = new DungeonLoadContext(stageDefinition, transform, runtimeSettings)
            {
                ExplicitSeed = explicitSeed
            };
            ApplyStageInstance(DungeonStageLoader.Load(context));
        }

        // 새 StageInstance를 기존 공개 상태와 드랍 서비스·완료 이벤트에 반영합니다.
        private void ApplyStageInstance(DungeonStageInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _stageInstance = instance;
            _layout = instance.Layout;
            _blueprint = instance.Blueprint;
            _report = instance.Report;
            _activeSeed = instance.ActiveSeed;
            _activeRuntimeSettings = instance.RuntimeSettings;
            _hasGenerated = true;
            ConfigureDropValidation(instance.ActiveSeed, instance.RuntimeSettings);
            Action<GenerationReport> handler = GenerationCompleted;
            if (handler != null) handler(_report);
        }

        // 생성과 별도인 드랍 난수 스트림을 활성 시드로 초기화하고 선택적으로 통계를 지웁니다.
        private void ConfigureDropValidation(int seed, RogueDungeonSettings runtimeSettings)
        {
            DropValidationService service = DropValidationService.Active;
            if (service == null) service = FindAnyObjectByType<DropValidationService>();
            if (service == null) service = gameObject.AddComponent<DropValidationService>();
            service.SetRandomSeed(unchecked(seed ^ 0x5F3759DF));
            if (runtimeSettings != null && runtimeSettings.resetDropStatsOnGenerate) service.ResetStatistics();
        }

        // 현재 인스턴스가 이 컴포넌트에 할당된 StageDefinition에서 로드되었는지 확인합니다.
        private bool IsStageDefinitionActive()
        {
            return stageDefinition != null && _stageInstance != null && _stageInstance.Definition == stageDefinition;
        }

        // 생성 전 Gizmo에도 사용할 설정 또는 저장 Blueprint 크기에서 예상 Bounds를 계산합니다.
        private Bounds CalculateBounds()
        {
            DungeonGridRecord grid = _blueprint != null ? _blueprint.grid : null;
            if (grid == null && stageDefinition != null && stageDefinition.savedBlueprint != null && stageDefinition.savedBlueprint.blueprint != null)
                grid = stageDefinition.savedBlueprint.blueprint.grid;
            float width = grid != null ? grid.width * grid.cellSize : settings != null ? settings.stageWidthCells * settings.cellSize : 10f;
            float depth = grid != null ? grid.depth * grid.cellSize : settings != null ? settings.stageDepthCells * settings.cellSize : 10f;
            float height = grid != null ? grid.wallHeight : settings != null ? settings.wallHeight : 10f;
            return new Bounds(transform.position + Vector3.up * (height * 0.5f), new Vector3(width, height, depth));
        }

        // 선택된 생성기에서 현재 또는 예상 던전 Bounds를 Scene Gizmo로 표시합니다.
        private void OnDrawGizmosSelected()
        {
            if (settings == null && stageDefinition == null && _blueprint == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.65f);
            Gizmos.DrawWireCube(GeneratedBounds.center, GeneratedBounds.size);
        }
    }
}
