using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    [Serializable]
    public sealed class FpsArenaWall
    {
        public string id;
        public Vector3Int cell;
        public bool alongX;
        public int lengthCells, doorOffsetCells, doorWidthCells;
        public float height, thickness;
        public Vector3Int Step { get { return alongX ? Vector3Int.right : new Vector3Int(0, 0, 1); } }
        public IEnumerable<Vector2Int> SolidSpans()
        {
            if (doorWidthCells == 0) { yield return new Vector2Int(0, lengthCells); yield break; }
            yield return new Vector2Int(0, doorOffsetCells);
            yield return new Vector2Int(doorOffsetCells + doorWidthCells, lengthCells - doorOffsetCells - doorWidthCells);
        }
        public IEnumerable<Vector3Int> Footprint()
        { foreach (var span in SolidSpans()) for (int i = span.x; i < span.x + span.y; i++) yield return cell + Step * i; }
    }

    public static partial class FpsArenaPlanner
    {
        private const ulong WallStreamV2 = 0x4152454E41574C32UL;
        private static void PlaceInternalWalls(FpsArenaLayout l, int floor)
        {
            var r = l.Recipe;
            int target = l.RequestedWallCells(floor);
            if (target == 0) return;
            var candidates = new List<Vector3Int>();
            for (int z = 1; z < r.depth - 1; z++) for (int x = 1; x < r.width - 1; x++)
                if (l.HasFloor(new Vector3Int(x, floor, z))) candidates.Add(new Vector3Int(x, floor, z));
            var rng = DungeonStableRandomStreams.Create(l.Seed, WallStreamV2 + (ulong)floor);
            Shuffle(candidates, rng);
            var blocked = l.BlockedCells(); int cells = 0, runs = 0;
            foreach (var c in candidates)
            {
                int length = rng.NextInt(r.wallMinLengthCells, r.wallMaxLengthCells + 1);
                // Short partitions have clear end bypasses; longer ones also have an open doorway.
                int door = length >= r.wallDoorWidthCells + 2 ? r.wallDoorWidthCells : 0;
                var wall = new FpsArenaWall {
                    id = "arena:v2:wall:" + floor + ":" + c.x + ":" + c.z,
                    cell = c, alongX = rng.NextInt(0, 2) == 0, lengthCells = length,
                    doorWidthCells = door, doorOffsetCells = door > 0 ? rng.NextInt(1, length - door) : 0,
                    height = r.wallHeight, thickness = r.wallThickness
                };
                if (!WallFits(l, wall, blocked)) continue;
                foreach (var solid in wall.Footprint()) blocked.Add(solid);
                // Validate the actual obstruction before accepting it, including all stair links.
                if (l.ReachableCount(blocked) != l.FloorCount() - blocked.Count)
                { foreach (var solid in wall.Footprint()) blocked.Remove(solid); continue; }
                l.Walls.Add(wall);
                // Keep the entire doorway and one cell on each approach free of all content.
                var perpendicular = wall.alongX ? new Vector3Int(0, 0, 1) : Vector3Int.right;
                for (int i = wall.doorOffsetCells; i < wall.doorOffsetCells + door; i++)
                    for (int side = -1; side <= 1; side++)
                    { var d = c + wall.Step * i + perpendicular * side; l.Reserved[d.x, d.y, d.z] = true; }
                cells += length - door;
                if (++runs >= r.maxWallRunsPerFloor || cells >= target) break;
            }
        }
        private static bool WallFits(FpsArenaLayout l, FpsArenaWall wall, HashSet<Vector3Int> occupied)
        {
            int width = wall.alongX ? wall.lengthCells : 1, depth = wall.alongX ? 1 : wall.lengthCells;
            // A walkable ring around the whole partition guarantees an end bypass.
            for (int z = -1; z <= depth; z++) for (int x = -1; x <= width; x++)
                if (!l.HasFloor(wall.cell + new Vector3Int(x, 0, z))) return false;
            foreach (var c in wall.Footprint()) if (l.Reserved[c.x, c.y, c.z]) return false;
            for (int z = -l.Recipe.spacingCells; z < depth + l.Recipe.spacingCells; z++)
                for (int x = -l.Recipe.spacingCells; x < width + l.Recipe.spacingCells; x++)
                    if (occupied.Contains(wall.cell + new Vector3Int(x, 0, z))) return false;
            return true;
        }
    }

    // Exact pre-wall V2 JsonUtility field order. OFF ignores every wall setting.
    [Serializable] internal sealed class FpsArenaV2RecipeSnapshot
    {
        public int width, depth; public FpsArenaShape shape; public int floors;
        public float cellSize, floorHeight; public int stairsPerFloor, coverPerFloor, obstaclesPerFloor, itemsPerFloor;
        public float coverHeight, obstacleHeight; public int spacingCells;
        public FpsArenaGeneratorVersion generatorVersion;
        public float coverDensity, enemyDensity, gimmickDensity, itemDensity;
        public int maxContentPerCategoryPerFloor, coverMinWidthCells, coverMaxWidthCells, coverMinDepthCells, coverMaxDepthCells;
        public float coverMinHeight, coverMaxHeight; public FpsArenaCoverShapes coverShapes;
        public FpsArenaV2RecipeSnapshot(FpsArenaRecipe r)
        {
            width = r.width; depth = r.depth; shape = r.shape; floors = r.floors; cellSize = r.cellSize; floorHeight = r.floorHeight;
            stairsPerFloor = r.stairsPerFloor; coverPerFloor = r.coverPerFloor; obstaclesPerFloor = r.obstaclesPerFloor; itemsPerFloor = r.itemsPerFloor;
            coverHeight = r.coverHeight; obstacleHeight = r.obstacleHeight; spacingCells = r.spacingCells; generatorVersion = r.generatorVersion;
            coverDensity = r.coverDensity; enemyDensity = r.enemyDensity; gimmickDensity = r.gimmickDensity; itemDensity = r.itemDensity;
            maxContentPerCategoryPerFloor = r.maxContentPerCategoryPerFloor; coverMinWidthCells = r.coverMinWidthCells; coverMaxWidthCells = r.coverMaxWidthCells;
            coverMinDepthCells = r.coverMinDepthCells; coverMaxDepthCells = r.coverMaxDepthCells; coverMinHeight = r.coverMinHeight; coverMaxHeight = r.coverMaxHeight; coverShapes = r.coverShapes;
        }
    }

    // Exact pre-room recipe schema for previously saved wall-run results.
    [Serializable] internal sealed class FpsArenaWallRecipeSnapshot
    {
        public int width, depth; public FpsArenaShape shape; public int floors;
        public float cellSize, floorHeight; public int stairsPerFloor, coverPerFloor, obstaclesPerFloor, itemsPerFloor;
        public float coverHeight, obstacleHeight; public int spacingCells;
        public FpsArenaGeneratorVersion generatorVersion;
        public float coverDensity, enemyDensity, gimmickDensity, itemDensity;
        public int maxContentPerCategoryPerFloor, coverMinWidthCells, coverMaxWidthCells, coverMinDepthCells, coverMaxDepthCells;
        public float coverMinHeight, coverMaxHeight; public FpsArenaCoverShapes coverShapes;
        public bool internalWalls; public float wallDensity;
        public int wallMinLengthCells, wallMaxLengthCells; public float wallHeight, wallThickness;
        public int wallDoorWidthCells, maxWallRunsPerFloor;
    }
}
