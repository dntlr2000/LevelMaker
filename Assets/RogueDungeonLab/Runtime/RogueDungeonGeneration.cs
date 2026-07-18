using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueDungeonLab
{
    public static class DungeonLayoutGenerator
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static DungeonLayout Generate(RogueDungeonSettings settings, int seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ClampValues();
            DungeonLayout layout = new DungeonLayout(settings.stageWidthCells, settings.stageDepthCells);
            System.Random random = new System.Random(seed);
            PlaceRooms(layout, settings, random);
            EnsureFallbackRoom(layout, settings);
            ConnectRooms(layout, settings, random);
            CalculateDistances(layout);
            ChooseExit(layout);
            return layout;
        }

        private static void PlaceRooms(DungeonLayout layout, RogueDungeonSettings settings, System.Random random)
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

        private static bool CanPlace(RectInt candidate, IReadOnlyList<RectInt> rooms, int width, int depth)
        {
            RectInt padded = new RectInt(candidate.xMin - 1, candidate.yMin - 1, candidate.width + 2, candidate.height + 2);
            if (padded.xMin < 0 || padded.yMin < 0 || padded.xMax >= width || padded.yMax >= depth) return false;
            for (int i = 0; i < rooms.Count; i++) if (padded.Overlaps(rooms[i])) return false;
            return true;
        }

        private static void EnsureFallbackRoom(DungeonLayout layout, RogueDungeonSettings settings)
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

        private static void ConnectRooms(DungeonLayout layout, RogueDungeonSettings settings, System.Random random)
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

        private static void CarveConnection(DungeonLayout layout, Vector2Int from, Vector2Int to, int width, bool horizontalFirst)
        {
            Vector2Int corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            CarveLine(layout, from, corner, width);
            CarveLine(layout, corner, to, width);
        }

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

        private static Vector2Int Center(RectInt r) { return new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2); }
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
        public static int Build(Transform parent, DungeonLayout layout, RogueDungeonSettings settings)
        {
            GameObject root = new GameObject("Geometry"); root.transform.SetParent(parent, false);
            Mesh floor = BuildFloor(layout, settings.cellSize);
            Mesh walls = BuildWalls(layout, settings.cellSize, settings.wallHeight);
            CreateMeshObject("Floor", root.transform, floor, PrototypeMaterials.Floor);
            CreateMeshObject("Walls", root.transform, walls, PrototypeMaterials.Wall);
            return (floor.triangles.Length + walls.triangles.Length) / 3;
        }

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
        public static ContentSpawnCounts Spawn(Transform parent, DungeonLayout layout, RogueDungeonSettings settings, int seed)
        {
            System.Random random = new System.Random(unchecked(seed * 486187739 + 17));
            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (Vector2Int cell in layout.EnumerateFloorCells()) if (!IsReserved(cell, layout, settings.reservedEntranceRadiusCells)) candidates.Add(cell);
            Transform contents = NewRoot("Contents", parent), markers = NewRoot("Stage Markers", contents), gimmicks = NewRoot("Special Gimmicks", contents), enemies = NewRoot("Enemies", contents), breakables = NewRoot("Destructibles", contents), props = NewRoot("Terrain Props", contents);
            CreateMarker("Entrance", layout.Entrance, layout, settings, markers, PrototypeMaterials.Entrance);
            CreateMarker("Exit", layout.Exit, layout, settings, markers, PrototypeMaterials.Exit);
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            ContentSpawnCounts counts = new ContentSpawnCounts();
            counts.GimmickCount = SpawnGimmicks(gimmicks, candidates, occupied, layout, settings, random);
            counts.EnemyCount = SpawnProfile(candidates, occupied, layout, settings, settings.enemyProfile, random, seed + 101, delegate(Vector2Int c, int i) { CreateEnemy(c, i, layout, settings, enemies); });
            counts.DestructibleCount = SpawnProfile(candidates, occupied, layout, settings, settings.destructibleProfile, random, seed + 211, delegate(Vector2Int c, int i) { CreateBreakable(c, i, layout, settings, breakables, random); });
            counts.PropCount = SpawnProfile(candidates, occupied, layout, settings, settings.propProfile, random, seed + 307, delegate(Vector2Int c, int i) { CreateProp(c, i, layout, settings, props, random); });
            return counts;
        }

        private static int SpawnGimmicks(Transform parent, List<Vector2Int> candidates, HashSet<Vector2Int> occupied, DungeonLayout layout, RogueDungeonSettings settings, System.Random random)
        {
            int requested = Mathf.Min(settings.specialGimmickCount, candidates.Count), count = 0;
            for (int i = 0; i < requested; i++)
            {
                float target = (i + 1f) / (requested + 1f), bestScore = float.MaxValue; Vector2Int best = default(Vector2Int); bool found = false;
                for (int c = 0; c < candidates.Count; c++)
                {
                    Vector2Int cell = candidates[c]; if (!CanOccupy(cell, occupied, settings.contentSpacingCells)) continue;
                    float score = Mathf.Abs(layout.GetProgression(cell) - target) + (layout.GetRoomId(cell) >= 0 ? 0f : 0.18f) + (float)random.NextDouble() * 0.06f;
                    if (score < bestScore) { bestScore = score; best = cell; found = true; }
                }
                if (!found) break;
                occupied.Add(best); CreateGimmick(best, count++, layout, settings, parent, random);
            }
            return count;
        }

        private static int SpawnProfile(List<Vector2Int> candidates, HashSet<Vector2Int> occupied, DungeonLayout layout, RogueDungeonSettings settings, DensityProfile profile, System.Random random, int noiseSeed, Action<Vector2Int,int> spawn)
        {
            List<Vector2Int> shuffled = new List<Vector2Int>(candidates); Shuffle(shuffled, random); int count = 0;
            for (int i = 0; i < shuffled.Count; i++)
            {
                if (profile.maxCount > 0 && count >= profile.maxCount) break;
                Vector2Int cell = shuffled[i]; if (!CanOccupy(cell, occupied, settings.contentSpacingCells)) continue;
                float noise = Mathf.PerlinNoise((cell.x + noiseSeed * 0.013f) * 0.17f, (cell.y - noiseSeed * 0.019f) * 0.17f);
                float p = profile.EvaluateProbability(layout.GetProgression(cell), layout.GetRoomId(cell) >= 0, noise);
                if (random.NextDouble() > p) continue;
                occupied.Add(cell); spawn(cell, count++);
            }
            return count;
        }

        private static bool IsReserved(Vector2Int c, DungeonLayout layout, int entranceRadius)
        {
            int a = Mathf.Abs(c.x-layout.Entrance.x)+Mathf.Abs(c.y-layout.Entrance.y), b = Mathf.Abs(c.x-layout.Exit.x)+Mathf.Abs(c.y-layout.Exit.y);
            return a <= entranceRadius || b <= 1;
        }
        private static bool CanOccupy(Vector2Int c, HashSet<Vector2Int> occupied, int spacing)
        {
            if (occupied.Contains(c)) return false;
            for (int x=-spacing;x<=spacing;x++) for(int z=-spacing;z<=spacing;z++) if(occupied.Contains(new Vector2Int(c.x+x,c.y+z))) return false;
            return true;
        }
        private static void CreateEnemy(Vector2Int cell, int index, DungeonLayout layout, RogueDungeonSettings settings, Transform parent)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name=string.Format("Enemy_{0:000}",index); go.transform.SetParent(parent,false);
            float scale=Mathf.Clamp(settings.cellSize*0.32f,0.65f,1.25f); go.transform.localScale=Vector3.one*scale; go.transform.localPosition=layout.CellToLocalPosition(cell,settings.cellSize)+Vector3.up*scale; go.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.Enemy;
            go.AddComponent<DestructibleDropTarget>().Configure(go.name,DropSourceKind.Enemy,settings.EffectiveEnemyDropTable,settings.spawnDropMarkers);
        }
        private static void CreateBreakable(Vector2Int cell,int index,DungeonLayout layout,RogueDungeonSettings settings,Transform parent,System.Random random)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=string.Format("Breakable_{0:000}",index); go.transform.SetParent(parent,false);
            Vector3 scale=new Vector3(Mathf.Clamp(settings.cellSize*0.32f,0.65f,1.3f),Mathf.Clamp(settings.cellSize*0.4f,0.75f,1.5f),Mathf.Clamp(settings.cellSize*0.32f,0.65f,1.3f)); go.transform.localScale=scale; go.transform.localPosition=layout.CellToLocalPosition(cell,settings.cellSize)+Vector3.up*(scale.y*0.5f); go.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360f,0); go.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.Breakable;
            go.AddComponent<DestructibleDropTarget>().Configure(go.name,DropSourceKind.Destructible,settings.EffectiveDestructibleDropTable,settings.spawnDropMarkers);
        }
        private static void CreateProp(Vector2Int cell,int index,DungeonLayout layout,RogueDungeonSettings settings,Transform parent,System.Random random)
        {
            PrimitiveType type=random.NextDouble()<0.5?PrimitiveType.Cylinder:PrimitiveType.Cube; GameObject go=GameObject.CreatePrimitive(type); go.name=string.Format("TerrainProp_{0:000}",index); go.transform.SetParent(parent,false);
            float b=Mathf.Clamp(settings.cellSize*(0.14f+(float)random.NextDouble()*0.12f),0.3f,1.2f), h=b*(0.8f+(float)random.NextDouble()*1.2f); go.transform.localScale=new Vector3(b,h,b); go.transform.localPosition=layout.CellToLocalPosition(cell,settings.cellSize)+Vector3.up*(type==PrimitiveType.Cylinder?h:h*0.5f); go.transform.localRotation=Quaternion.Euler((float)random.NextDouble()*8f,(float)random.NextDouble()*360f,(float)random.NextDouble()*8f); go.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.Prop; Collider col=go.GetComponent<Collider>(); if(col!=null)col.enabled=false;
        }
        private static void CreateGimmick(Vector2Int cell,int index,DungeonLayout layout,RogueDungeonSettings settings,Transform parent,System.Random random)
        {
            GameObject root=new GameObject(string.Format("SpecialGimmick_{0:00}",index)); root.transform.SetParent(parent,false); root.transform.localPosition=layout.CellToLocalPosition(cell,settings.cellSize); root.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360f,0);
            GameObject b=GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.transform.SetParent(root.transform,false); b.transform.localPosition=Vector3.up*0.12f; b.transform.localScale=new Vector3(0.85f,0.12f,0.85f); b.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.Gimmick; DisableCollider(b);
            GameObject core=GameObject.CreatePrimitive(PrimitiveType.Sphere); core.transform.SetParent(root.transform,false); core.transform.localPosition=Vector3.up*0.9f; core.transform.localScale=Vector3.one*0.65f; core.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.Gimmick; DisableCollider(core);
        }
        private static void CreateMarker(string name,Vector2Int cell,DungeonLayout layout,RogueDungeonSettings settings,Transform parent,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=layout.CellToLocalPosition(cell,settings.cellSize)+Vector3.up*0.08f; go.transform.localScale=new Vector3(1.15f,0.08f,1.15f); go.GetComponent<Renderer>().sharedMaterial=material; DisableCollider(go);
        }
        private static void DisableCollider(GameObject go){Collider c=go.GetComponent<Collider>();if(c!=null)c.enabled=false;}
        private static Transform NewRoot(string name,Transform parent){GameObject go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static void Shuffle<T>(IList<T> list,System.Random random){for(int i=list.Count-1;i>0;i--){int j=random.Next(0,i+1);T x=list[i];list[i]=list[j];list[j]=x;}}
    }

    [DisallowMultipleComponent]
    public sealed partial class RogueDungeonGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = "__RogueDungeonLab_Generated";
        public RogueDungeonSettings settings;
        private DungeonLayout _layout; private GenerationReport _report; private int _activeSeed; private bool _hasGenerated;
        public event Action<GenerationReport> GenerationCompleted;
        public DungeonLayout CurrentLayout { get { return _layout; } }
        public GenerationReport LastReport { get { return _report; } }
        public int ActiveSeed { get { return _activeSeed; } }
        public Bounds GeneratedBounds { get { return _report != null ? _report.worldBounds : CalculateBounds(); } }

        private void Start(){if(Application.isPlaying&&settings!=null&&settings.generateOnPlay)GenerateWithSeed(settings.seed);}
        [ContextMenu("Generate From Settings Seed")] public void GenerateFromSettings(){if(settings!=null)GenerateWithSeed(settings.seed);else UnityEngine.Debug.LogWarning("Assign RogueDungeonSettings first.",this);}
        [ContextMenu("Regenerate Active Seed")] public void RegenerateActiveSeed(){if(settings!=null)GenerateWithSeed(_hasGenerated?_activeSeed:settings.seed);}
        [ContextMenu("Generate New Random Seed")] public void GenerateNewSeed(){GenerateWithSeed(unchecked(Environment.TickCount*397^Guid.NewGuid().GetHashCode()));}

        // 지정 시드로 레이아웃, 메시, 콘텐츠와 드랍 검증 서비스를 생성합니다.
        public void GenerateWithSeed(int seed)
        {
            if(settings==null){UnityEngine.Debug.LogWarning("Assign RogueDungeonSettings first.",this);return;}
            settings.ClampValues(); Stopwatch sw=Stopwatch.StartNew(); ClearGenerated(); _activeSeed=seed; _layout=DungeonLayoutGenerator.Generate(settings,seed);
            GameObject root=new GameObject(GeneratedRootName);root.transform.SetParent(transform,false);if(!Application.isPlaying)root.hideFlags=HideFlags.DontSaveInBuild|HideFlags.DontSaveInEditor;
            int triangles=DungeonMeshBuilder.Build(root.transform,_layout,settings);ContentSpawnCounts counts=DungeonContentSpawner.Spawn(root.transform,_layout,settings,seed);
            DropValidationService service=DropValidationService.Active;if(service==null)service=FindAnyObjectByType<DropValidationService>();if(service==null)service=gameObject.AddComponent<DropValidationService>();service.SetRandomSeed(unchecked(seed^0x5F3759DF));if(settings.resetDropStatsOnGenerate)service.ResetStatistics();
            sw.Stop();_report=BuildReport(seed,triangles,counts,sw.Elapsed.TotalMilliseconds);_hasGenerated=true;Action<GenerationReport> h=GenerationCompleted;if(h!=null)h(_report);
        }

        [ContextMenu("Clear Generated Dungeon")]
        public void ClearGenerated()
        {
            Transform existing=transform.Find(GeneratedRootName);if(existing==null)return;existing.gameObject.SetActive(false);
            MeshFilter[] filters=existing.GetComponentsInChildren<MeshFilter>(true);for(int i=0;i<filters.Length;i++){Mesh mesh=filters[i].sharedMesh;if(mesh==null||!mesh.name.StartsWith("Generated Dungeon",StringComparison.Ordinal))continue;filters[i].sharedMesh=null;if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            if(Application.isPlaying){existing.name=GeneratedRootName+"_PendingDestroy";Destroy(existing.gameObject);}else DestroyImmediate(existing.gameObject);
        }

        public WeightedDropTable GetEffectiveDropTable(DropSourceKind kind){if(settings==null)return kind==DropSourceKind.Enemy?RuntimeDropTables.Enemy:RuntimeDropTables.Destructible;return kind==DropSourceKind.Enemy?settings.EffectiveEnemyDropTable:settings.EffectiveDestructibleDropTable;}
        private GenerationReport BuildReport(int seed,int triangles,ContentSpawnCounts c,double ms)
        {
            GenerationReport r=new GenerationReport{activeSeed=seed,roomCount=_layout.Rooms.Count,floorCellCount=_layout.WalkableCellCount,enemyCount=c.EnemyCount,destructibleCount=c.DestructibleCount,propCount=c.PropCount,gimmickCount=c.GimmickCount,meshTriangleCount=triangles,generationMilliseconds=ms,worldBounds=CalculateBounds()};
            if(_layout.Rooms.Count<settings.desiredRoomCount)r.warnings.Add(string.Format("Requested {0} rooms but placed {1}.",settings.desiredRoomCount,_layout.Rooms.Count));int disconnected=0;foreach(Vector2Int cell in _layout.EnumerateFloorCells())if(_layout.GetDistance(cell)<0)disconnected++;if(disconnected>0)r.warnings.Add(disconnected+" floor cells are disconnected.");if(_layout.GetDistance(_layout.Exit)<=0)r.warnings.Add("Exit is not meaningfully separated from the entrance.");if(c.GimmickCount<settings.specialGimmickCount)r.warnings.Add(string.Format("Requested {0} gimmicks but placed {1} due to spacing.",settings.specialGimmickCount,c.GimmickCount));return r;
        }
        private Bounds CalculateBounds(){if(settings==null)return new Bounds(transform.position,Vector3.one*10f);Vector3 size=new Vector3(settings.stageWidthCells*settings.cellSize,settings.wallHeight,settings.stageDepthCells*settings.cellSize);return new Bounds(transform.position+Vector3.up*(settings.wallHeight*0.5f),size);}
        private void OnDrawGizmosSelected(){if(settings==null)return;Gizmos.color=new Color(0.2f,0.8f,1f,0.65f);Gizmos.DrawWireCube(GeneratedBounds.center,GeneratedBounds.size);}
    }
}
