using System;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class FpsArenaSceneBuilder
    {
        public static GameObject Build(FpsArenaLayout l, Transform parent)
        { return Build(l, parent, null); }
        public static GameObject Build(FpsArenaLayout l, Transform parent, FpsArenaContentCatalog catalog)
        {
            if (l == null) throw new ArgumentNullException(nameof(l));
            l.Validate();
            var root = new GameObject(FpsArenaGenerator.GeneratedRootName + "_Staging");
            root.SetActive(false); root.transform.SetParent(parent, false);
            try
            {
                var r = l.Recipe;
                for (int f = 0; f < r.floors; f++)
                {
                    var floor = new GameObject("층_" + (f + 1)); floor.transform.SetParent(root.transform, false);
                    // Merge consecutive floor cells into strips; shared primitive meshes and bounded cached prototype materials.
                    for (int z = 0; z < r.depth; z++)
                    {
                        int x = 0;
                        while (x < r.width)
                        {
                            if (!l.Floors[x, f, z]) { x++; continue; }
                            int start = x; while (x < r.width && l.Floors[x, f, z]) x++;
                            Vector3 p = l.Position(new Vector3Int(start, f, z)); p.x += (x - start - 1) * r.cellSize * .5f;
                            Box("바닥", floor.transform, p - Vector3.up * .1f, new Vector3((x - start) * r.cellSize, .2f, r.cellSize), new Color(.24f + f * .06f, .30f, .36f));
                        }
                    }
                    for (int z = 0; z < r.depth; z++) for (int x = 0; x < r.width; x++)
                    {
                        if (!FpsArenaPlanner.InShape(r, x, z)) continue;
                        Vector3 p = l.Position(new Vector3Int(x, f, z));
                        Boundary(l, floor.transform, p, x, z, 1, 0); Boundary(l, floor.transform, p, x, z, -1, 0);
                        Boundary(l, floor.transform, p, x, z, 0, 1); Boundary(l, floor.transform, p, x, z, 0, -1);
                    }
                }
                foreach (var s in l.Stairs)
                {
                    var group = new GameObject("계단_" + s.lowerFloor + "_" + s.x + (l.GeneratorVersion == 2 ? "_" + s.z : "")); group.transform.SetParent(root.transform, false);
                    Vector3 origin = l.Position(s.Cell(0,0));
                    if (r.randomizeStairDirections && l.GeneratorVersion == 2)
                    { group.transform.localPosition=origin;group.transform.localRotation=Quaternion.Euler(0,90*(int)s.direction,0);origin=Vector3.zero; }
                    float length = s.length * r.cellSize, width = s.width * r.cellSize;
                    int steps = Mathf.CeilToInt(r.floorHeight / .18f);
                    float tread = length / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float height = r.floorHeight * (i + 1) / steps;
                        Box("단_" + i, group.transform, origin + new Vector3((s.width - 1) * r.cellSize * .5f, height * .5f, -r.cellSize * .5f + tread * (i + .5f)), new Vector3(width, height, tread), new Color(.55f, .58f, .62f));
                    }
                    // Guard upper opening's sides and low end, never the upper landing.
                    for (int side = 0; side < 2; side++)
                        Box("개구부_측면가드", group.transform, origin + new Vector3(-r.cellSize * .5f + side * width, r.floorHeight + .6f, (s.length - 1) * r.cellSize * .5f), new Vector3(.12f, 1.2f, length), new Color(.8f, .65f, .18f));
                    Box("개구부_하단가드", group.transform, origin + new Vector3((s.width - 1) * r.cellSize * .5f, r.floorHeight + .6f, -r.cellSize * .5f), new Vector3(width, 1.2f, .12f), new Color(.8f, .65f, .18f));
                }
                foreach(var wall in l.RoomWalls)
                {
                    Vector3 size = wall.alongX ? new Vector3(wall.lengthCells*r.cellSize,wall.height,wall.thickness) : new Vector3(wall.thickness,wall.height,wall.lengthCells*r.cellSize);
                    Box(wall.id,root.transform,wall.Position(l)+Vector3.up*wall.height*.5f,size,new Color(.55f,.49f,.39f));
                }
                foreach(var room in l.Rooms)
                {
                    var marker=new GameObject("방_"+(room.floor+1)+"_"+(room.index+1)); marker.transform.SetParent(root.transform,false); marker.transform.localPosition=l.Position(room.anchor);
                }
                foreach(var door in l.RoomDoors)
                {
                    var marker=new GameObject(door.id); marker.transform.SetParent(root.transform,false); marker.transform.localPosition=door.Position(l);
                }
                foreach (var wall in l.Walls)
                {
                    var group = new GameObject(wall.id); group.transform.SetParent(root.transform, false);
                    group.transform.localPosition = l.Position(wall.cell);
                    foreach (var span in wall.SolidSpans())
                    {
                        Vector3 offset = (Vector3)wall.Step * ((span.x + (span.y - 1) * .5f) * r.cellSize) + Vector3.up * wall.height * .5f;
                        Vector3 size = wall.alongX ? new Vector3(span.y * r.cellSize, wall.height, wall.thickness) : new Vector3(wall.thickness, wall.height, span.y * r.cellSize);
                        Box("내부 벽_구간_" + span.x, group.transform, offset, size, new Color(.55f, .49f, .39f));
                    }
                }
                foreach (var p in l.Content)
                {
                    if (l.GeneratorVersion == 2) { BuildContent(l, p, root.transform, catalog); continue; }
                    bool item = p.kind == FpsArenaContentKind.Item;
                    float height = item ? .35f : p.kind == FpsArenaContentKind.Cover ? r.coverHeight : r.obstacleHeight;
                    float size = item ? .5f : r.cellSize * .7f;
                    var obj = Box(p.id, root.transform, l.Position(p.cell) + Vector3.up * height * .5f, new Vector3(size, height, size), item ? Color.cyan : p.kind == FpsArenaContentKind.Cover ? new Color(.3f, .6f, .45f) : new Color(.7f, .4f, .3f));
                    obj.AddComponent<FpsArenaContentIdentity>().Initialize(p);
                    if (item) obj.GetComponent<BoxCollider>().isTrigger = true;
                }
                for (int i = 0; i < l.Spawns.Count; i++)
                {
                    var spawn = new GameObject("스폰_" + i); spawn.transform.SetParent(root.transform, false);
                    spawn.transform.localPosition = l.Position(l.Spawns[i]) + Vector3.up * .08f;
                }
                root.AddComponent<FpsArenaVisuals>().Capture();
                return root;
            }
            catch { if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); throw; }
        }
        private static void BuildContent(FpsArenaLayout l, FpsArenaContent p, Transform parent, FpsArenaContentCatalog catalog)
        {
            var wrapper = new GameObject(p.id); wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = l.ContentPosition(p); wrapper.transform.localRotation = Quaternion.Euler(0, p.yaw, 0);
            var identity = wrapper.AddComponent<FpsArenaContentIdentity>(); identity.Initialize(p);
            if (!string.IsNullOrEmpty(p.contentKey))
            {
                var entry = catalog != null ? catalog.Find(p.contentKey) : null;
                if (entry == null || entry.prefab == null || entry.kind != p.kind) throw new InvalidOperationException("Arena prefab mapping is missing or has changed: " + p.contentKey);
                Vector3 bounds = entry.authoredBounds, center = entry.authoredBoundsCenter;
                if (!FinitePositive(bounds.x) || !FinitePositive(bounds.y) || !FinitePositive(bounds.z) || !Finite(center.x) || !Finite(center.y) || !Finite(center.z)) throw new InvalidOperationException("Arena prefab authored bounds are invalid: " + p.contentKey);
                GameObject instance = UnityEngine.Object.Instantiate(entry.prefab, wrapper.transform, false);
                instance.name = entry.prefab.name;
                // A uniform fit preserves authored aspect ratio and avoids max-axis
                // Sphere/Capsule/CharacterController inflation outside the planned footprint.
                float fit = Mathf.Min(p.size.x / bounds.x, Mathf.Min(p.size.y / bounds.y, p.size.z / bounds.z));
                if (!FinitePositive(fit)) throw new InvalidOperationException("Arena prefab fit overflowed: " + p.contentKey);
                Vector3 scale = Vector3.one * fit;
                Vector3 offset = Vector3.up * (bounds.y * fit * .5f) - Vector3.Scale(center, scale);
                if (!Finite(offset.x) || !Finite(offset.y) || !Finite(offset.z)) throw new InvalidOperationException("Arena prefab bounds center overflowed: " + p.contentKey);
                instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = scale;
                instance.transform.localPosition = offset;
            }
            else if (p.kind == FpsArenaContentKind.Cover)
            {
                Color color = new Color(.3f, .6f, .45f);
                if (p.coverShape == FpsArenaCoverShape.Cylinder)
                {
                    GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cylinder.name = "원통 엄폐"; cylinder.transform.SetParent(wrapper.transform, false);
                    cylinder.transform.localPosition = Vector3.up * p.size.y * .5f; cylinder.transform.localScale = new Vector3(p.size.x, p.size.y * .5f, p.size.z);
                    Collider primitiveCollider = cylinder.GetComponent<Collider>(); primitiveCollider.enabled = false;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(primitiveCollider); else UnityEngine.Object.DestroyImmediate(primitiveCollider);
                    var collider = cylinder.AddComponent<MeshCollider>(); collider.sharedMesh = cylinder.GetComponent<MeshFilter>().sharedMesh; collider.convex = true;
                    cylinder.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.ForColor(color);
                }
                else if (p.coverShape == FpsArenaCoverShape.Corner)
                {
                    float thickness = Mathf.Min(p.size.x, p.size.z) * .35f;
                    Box("ㄱ자 엄폐 X", wrapper.transform, new Vector3(0, p.size.y * .5f, (-p.size.z + thickness) * .5f), new Vector3(p.size.x, p.size.y, thickness), color);
                    Box("ㄱ자 엄폐 Z", wrapper.transform, new Vector3((-p.size.x + thickness) * .5f, p.size.y * .5f, 0), new Vector3(thickness, p.size.y, p.size.z), color);
                }
                else Box("상자 엄폐", wrapper.transform, Vector3.up * p.size.y * .5f, p.size, color);
            }
            else
            {
                Color color = p.kind == FpsArenaContentKind.Enemy ? new Color(.9f, .3f, .3f) : p.kind == FpsArenaContentKind.Gimmick ? new Color(.7f, .4f, .9f) : Color.cyan;
                GameObject marker = Box(p.kind == FpsArenaContentKind.Enemy ? "적 연동 표식" : p.kind == FpsArenaContentKind.Gimmick ? "기믹 연동 표식" : "아이템 연동 표식", wrapper.transform, Vector3.up * p.size.y * .5f, p.size, color);
                marker.GetComponent<BoxCollider>().isTrigger = true;
            }
            var context = new FpsArenaSpawnContext(l, p, identity);
            foreach (var component in wrapper.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IFpsArenaContentInitializer initializer) initializer.InitializeArenaContent(context);
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool FinitePositive(float value) { return value > 0 && Finite(value); }
        private static void Boundary(FpsArenaLayout l, Transform parent, Vector3 p, int x, int z, int dx, int dz)
        {
            if (FpsArenaPlanner.InShape(l.Recipe, x + dx, z + dz)) return;
            float size = l.Recipe.cellSize;
            Box("외곽가드", parent, p + new Vector3(dx * size * .5f, .75f, dz * size * .5f), new Vector3(dx == 0 ? size : .15f, 1.5f, dz == 0 ? size : .15f), new Color(.4f, .43f, .48f));
        }
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.ForColor(color); return go;
        }
    }
}
