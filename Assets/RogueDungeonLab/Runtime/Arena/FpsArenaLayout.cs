using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace RogueDungeonLab
{
    public enum FpsArenaContentKind { Cover = 0, Obstacle = 1, Item = 2, Enemy = 3, Gimmick = 4 }

    [Serializable]
    public sealed partial class FpsArenaStair
    {
        public int lowerFloor, x, z, width = 2, length;
        public Vector3Int Bottom { get { return BottomLane(0); } }
        public Vector3Int Top { get { return TopLane(0); } }
    }

    [Serializable]
    public sealed class FpsArenaContent
    {
        public string id;
        public FpsArenaContentKind kind;
        public Vector3Int cell; // minimum footprint x/z, floor index
        public int widthCells = 1, depthCells = 1;
        public Vector3 size;
        public float yaw;
        public FpsArenaCoverShape coverShape;
        public string contentKey = "";
        public bool IsBlocking { get { return kind == FpsArenaContentKind.Cover || kind == FpsArenaContentKind.Obstacle; } }
        public IEnumerable<Vector3Int> Footprint()
        { for (int z = 0; z < depthCells; z++) for (int x = 0; x < widthCells; x++) yield return cell + new Vector3Int(x, 0, z); }
    }

    // Arena generation is independent of the immutable single-plane DungeonBlueprint contract.
    public sealed partial class FpsArenaLayout
    {
        public const int Version = 2;
        public int Seed { get; internal set; }
        public FpsArenaRecipe Recipe { get; internal set; }
        public bool[,,] Floors { get; internal set; }
        public bool[,,] Reserved { get; internal set; }
        public FpsArenaCatalogSnapshot CatalogSnapshot { get; internal set; }
        internal int[] WallCellTargets { get; set; }
        public int GeneratorVersion { get { return (int)Recipe.generatorVersion; } }
        public Vector3 ContentPosition(FpsArenaContent p)
        { return Position(p.cell) + new Vector3((p.widthCells - 1) * Recipe.cellSize * .5f, 0, (p.depthCells - 1) * Recipe.cellSize * .5f); }
        public int RequestedCount(int floor, FpsArenaContentKind kind)
        {
            if (GeneratorVersion == 1) return kind == FpsArenaContentKind.Cover ? Recipe.coverPerFloor : kind == FpsArenaContentKind.Obstacle ? Recipe.obstaclesPerFloor : kind == FpsArenaContentKind.Item ? Recipe.itemsPerFloor : 0;
            float density = kind == FpsArenaContentKind.Cover ? Recipe.coverDensity : kind == FpsArenaContentKind.Enemy ? Recipe.enemyDensity : kind == FpsArenaContentKind.Gimmick ? Recipe.gimmickDensity : kind == FpsArenaContentKind.Item ? Recipe.itemDensity : 0;
            int area = 0;
            for (int x = 0; x < Recipe.width; x++) for (int z = 0; z < Recipe.depth; z++) if (Floors[x, floor, z] && !Reserved[x, floor, z]) area++;
            return Mathf.Min(Recipe.maxContentPerCategoryPerFloor, Mathf.CeilToInt(area * density));
        }
        public int RequestedWallCells(int floor)
        {
            if (GeneratorVersion != 2 || !Recipe.internalWalls || Recipe.partitionRooms) return 0;
            if (WallCellTargets != null) return WallCellTargets[floor];
            int area = 0;
            for (int x = 0; x < Recipe.width; x++) for (int z = 0; z < Recipe.depth; z++)
                if (Floors[x, floor, z] && !Reserved[x, floor, z]) area++;
            return Mathf.CeilToInt(area * Recipe.wallDensity);
        }
        public readonly List<FpsArenaStair> Stairs = new List<FpsArenaStair>();
        public readonly List<FpsArenaWall> Walls = new List<FpsArenaWall>();
        public readonly List<FpsArenaContent> Content = new List<FpsArenaContent>();
        public readonly List<Vector3Int> Spawns = new List<Vector3Int>();
        public string Hash { get; internal set; }

        public bool HasFloor(Vector3Int c)
        { return c.x >= 0 && c.z >= 0 && c.y >= 0 && c.x < Recipe.width && c.z < Recipe.depth && c.y < Recipe.floors && Floors[c.x, c.y, c.z]; }
        public Vector3 Position(Vector3Int c)
        { return new Vector3((c.x - (Recipe.width - 1) * .5f) * Recipe.cellSize, c.y * Recipe.floorHeight, (c.z - (Recipe.depth - 1) * .5f) * Recipe.cellSize); }
        public HashSet<Vector3Int> BlockedCells()
        {
            var set = new HashSet<Vector3Int>();
            foreach (var wall in Walls) foreach (var c in wall.Footprint()) set.Add(c);
            foreach (var p in Content) if (p.IsBlocking) foreach (var c in p.Footprint()) set.Add(c);
            return set;
        }
        public int ReachableCount(HashSet<Vector3Int> blocked)
        {
            if (Spawns.Count == 0 || !HasFloor(Spawns[0]) || blocked.Contains(Spawns[0])) return 0;
            var seen = new HashSet<Vector3Int>(); var queue = new Queue<Vector3Int>();
            seen.Add(Spawns[0]); queue.Enqueue(Spawns[0]);
            while (queue.Count > 0)
            {
                Vector3Int c = queue.Dequeue();
                Visit(c + Vector3Int.right, blocked, seen, queue, c); Visit(c + Vector3Int.left, blocked, seen, queue, c);
                Visit(c + new Vector3Int(0, 0, 1), blocked, seen, queue, c); Visit(c + new Vector3Int(0, 0, -1), blocked, seen, queue, c);
                foreach (var s in Stairs)
                {
                    for (int lane = 0; lane < s.width; lane++)
                    {
                        if (c == s.BottomLane(lane)) Visit(s.TopLane(lane), blocked, seen, queue, c);
                        if (c == s.TopLane(lane)) Visit(s.BottomLane(lane), blocked, seen, queue, c);
                    }
                }
            }
            return seen.Count;
        }
        private void Visit(Vector3Int c, HashSet<Vector3Int> blocked, HashSet<Vector3Int> seen, Queue<Vector3Int> queue, Vector3Int from)
        { if (HasFloor(c) && (SolidRoomEdges.Count == 0 || CanTraverse(from,c)) && !blocked.Contains(c) && seen.Add(c)) queue.Enqueue(c); }
        public int FloorCount()
        { int n = 0; foreach (bool f in Floors) if (f) n++; return n; }
        public void Validate()
        {
            foreach (var s in Stairs)
            {
                if ((int)s.direction < 0 || (int)s.direction > 3 || (s.direction != FpsArenaStairDirection.Forward && !Recipe.randomizeStairDirections)) throw new InvalidOperationException("Arena stair direction is invalid for this recipe.");
                for (int lane = 0; lane < s.width; lane++)
                {
                    if (!HasFloor(s.BottomLane(lane)) || !HasFloor(s.TopLane(lane)))
                        throw new InvalidOperationException("Arena stair landing is missing.");
                    for (int step = 0; step < s.length; step++)
                    {
                        var c = s.Cell(lane, step);
                        if (!FpsArenaPlanner.InShape(Recipe, c.x, c.z)) throw new InvalidOperationException("Arena stair leaves the footprint.");
                    }
                }
                for (int x = s.x; x < s.x + s.FootprintWidth; x++) for (int z = s.z; z < s.z + s.FootprintDepth; z++)
                    if (Floors[x, s.lowerFloor + 1, z]) throw new InvalidOperationException("Arena stair headroom is blocked.");
            }
            var ids = new HashSet<string>(); var occupied = new HashSet<Vector3Int>();
            foreach (var wall in Walls)
            {
                if (GeneratorVersion != 2 || !Recipe.internalWalls || wall == null || string.IsNullOrEmpty(wall.id) || !ids.Add(wall.id) || wall.lengthCells < 3 || wall.lengthCells > 16 || !FinitePositive(wall.height) || wall.height > Recipe.floorHeight - .25f || wall.height < 2 || !FinitePositive(wall.thickness) || wall.thickness > 1 || wall.thickness < .15f || wall.doorWidthCells < 0 || wall.doorWidthCells > 3 || (wall.doorWidthCells > 0 && (wall.doorOffsetCells < 1 || wall.doorOffsetCells + wall.doorWidthCells >= wall.lengthCells)))
                    throw new InvalidOperationException("Arena internal wall dimensions/identity are invalid.");
                foreach (var c in wall.Footprint())
                    if (!HasFloor(c) || Reserved[c.x, c.y, c.z] || !occupied.Add(c)) throw new InvalidOperationException("Arena wall overlaps protected space or another wall.");
                for (int i = wall.doorOffsetCells; i < wall.doorOffsetCells + wall.doorWidthCells; i++)
                {
                    var c = wall.cell + wall.Step * i;
                    if (!HasFloor(c) || !Reserved[c.x, c.y, c.z]) throw new InvalidOperationException("Arena wall doorway is not protected.");
                }
            }
            foreach (var p in Content)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || !ids.Add(p.id) || p.widthCells < 1 || p.depthCells < 1 || p.widthCells > 4 || p.depthCells > 4) throw new InvalidOperationException("Arena content identity/footprint is invalid.");
                foreach (var c in p.Footprint())
                    if (!HasFloor(c) || Reserved[c.x, c.y, c.z] || !occupied.Add(c)) throw new InvalidOperationException("Arena content violates reserved space or footprint overlap.");
                if (GeneratorVersion == 2 && (!FinitePositive(p.size.x) || !FinitePositive(p.size.y) || !FinitePositive(p.size.z) || p.size.x > p.widthCells * Recipe.cellSize || p.size.z > p.depthCells * Recipe.cellSize || float.IsNaN(p.yaw) || float.IsInfinity(p.yaw))) throw new InvalidOperationException("Arena content bounds are invalid.");
            }
            var blocked = BlockedCells();
            if (ReachableCount(blocked) != FloorCount() - blocked.Count) throw new InvalidOperationException("Arena has unreachable floor cells.");
            ValidateRooms();
        }
        private static bool FinitePositive(float value) { return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value); }
        public string ComputeHash()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                string recipeJson = GeneratorVersion == 1 ? JsonUtility.ToJson(new FpsArenaLegacyRecipeSnapshot(Recipe)) : Recipe.randomizeStairDirections ? JsonUtility.ToJson(Recipe) : Recipe.partitionRooms ? JsonUtility.ToJson(JsonUtility.FromJson<FpsArenaRoomRecipeSnapshot>(JsonUtility.ToJson(Recipe))) : Recipe.internalWalls ? JsonUtility.ToJson(JsonUtility.FromJson<FpsArenaWallRecipeSnapshot>(JsonUtility.ToJson(Recipe))) : JsonUtility.ToJson(new FpsArenaV2RecipeSnapshot(Recipe));
                writer.Write(GeneratorVersion); writer.Write(Seed); writer.Write(recipeJson);
                if (GeneratorVersion == 2) writer.Write(JsonUtility.ToJson(CatalogSnapshot ?? new FpsArenaCatalogSnapshot()));
                for (int f = 0; f < Recipe.floors; f++) for (int z = 0; z < Recipe.depth; z++) for (int x = 0; x < Recipe.width; x++)
                { writer.Write(Floors[x, f, z]); writer.Write(Reserved[x, f, z]); }
                foreach (var s in Stairs) { writer.Write(s.lowerFloor); writer.Write(s.x); writer.Write(s.z); writer.Write(s.width); writer.Write(s.length); }
                if (GeneratorVersion == 2 && Recipe.randomizeStairDirections)
                { writer.Write("seeded-stair-directions-v1"); foreach (var s in Stairs) writer.Write((int)s.direction); }
                foreach (var c in Spawns) { writer.Write(c.x); writer.Write(c.y); writer.Write(c.z); }
                // No extra bytes when OFF: previously saved V2 hashes remain exact.
                if (GeneratorVersion == 2 && Recipe.internalWalls && !Recipe.partitionRooms)
                {
                    writer.Write("internal-walls-v1"); writer.Write(Walls.Count);
                    foreach (var wall in Walls) { writer.Write(wall.id); writer.Write(wall.cell.x); writer.Write(wall.cell.y); writer.Write(wall.cell.z); writer.Write(wall.alongX); writer.Write(wall.lengthCells); writer.Write(wall.doorOffsetCells); writer.Write(wall.doorWidthCells); writer.Write(wall.height); writer.Write(wall.thickness); }
                }
                if (GeneratorVersion == 2 && Recipe.partitionRooms)
                {
                    writer.Write("room-partitions-v1");
                    writer.Write(Rooms.Count); foreach(var room in Rooms) writer.Write(JsonUtility.ToJson(room));
                    writer.Write(RoomWalls.Count); foreach(var wall in RoomWalls) writer.Write(JsonUtility.ToJson(wall));
                    writer.Write(RoomDoors.Count); foreach(var door in RoomDoors) writer.Write(JsonUtility.ToJson(door));
                    writer.Write(RoomConnections.Count); foreach(var connection in RoomConnections) writer.Write(JsonUtility.ToJson(connection));
                    foreach(var report in RoomReports) writer.Write(JsonUtility.ToJson(report));
                }
                foreach (var p in Content)
                {
                    writer.Write(p.id); writer.Write((int)p.kind); writer.Write(p.cell.x); writer.Write(p.cell.y); writer.Write(p.cell.z);
                    if (GeneratorVersion == 2) { writer.Write(p.widthCells); writer.Write(p.depthCells); writer.Write(p.size.x); writer.Write(p.size.y); writer.Write(p.size.z); writer.Write(p.yaw); writer.Write((int)p.coverShape); writer.Write(p.contentKey ?? ""); }
                }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
