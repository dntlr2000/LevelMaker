using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static partial class FpsArenaPlanner
    {
        public static FpsArenaLayout Generate(FpsArenaRecipe source, int seed)
        { return Generate(source, seed, null); }
        public static FpsArenaLayout Generate(FpsArenaRecipe source, int seed, FpsArenaContentCatalog catalog)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return GenerateSnapshot(source, seed, source.Normalized().generatorVersion == FpsArenaGeneratorVersion.FlexibleV2 && catalog != null ? catalog.Snapshot() : null);
        }
        public static FpsArenaLayout GenerateSnapshot(FpsArenaRecipe source, int seed, FpsArenaCatalogSnapshot snapshot)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Normalized().generatorVersion != FpsArenaGeneratorVersion.FlexibleV2) return GenerateLegacy(source, seed);
            return GenerateFlexible(source, seed, snapshot);
        }
        private const ulong ContentStream = 0x4152454E415F5631UL;
        private static FpsArenaLayout GenerateLegacy(FpsArenaRecipe source, int seed)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var r = source.Normalized();
            var layout = new FpsArenaLayout { Recipe = r, Seed = seed, Floors = new bool[r.width, r.floors, r.depth], Reserved = new bool[r.width, r.floors, r.depth] };
            for (int f = 0; f < r.floors; f++) for (int z = 0; z < r.depth; z++) for (int x = 0; x < r.width; x++)
                layout.Floors[x, f, z] = InShape(r, x, z);
            int run = Mathf.CeilToInt(r.floorHeight / (r.cellSize * .6f));
            for (int f = 0; f < r.floors - 1; f++) for (int i = 0; i < r.stairsPerFloor; i++)
            {
                var s = new FpsArenaStair { lowerFloor = f, x = i == 0 ? r.width / 3 : r.width * 2 / 3, z = f % 2 == 0 ? Mathf.Max(3, r.depth / 4 - run / 2) : r.depth - run - Mathf.Max(3, r.depth / 4 - run / 2), length = run };
                layout.Stairs.Add(s);
                for (int floor = f; floor <= f + 1; floor++)
                {
                    Reserve(layout, floor, s.x - 1, s.z - 2, s.width + 2, s.length + 4);
                    for (int x = s.x; x < s.x + s.width; x++) for (int z = s.z; z < s.z + s.length; z++) layout.Floors[x, floor, z] = false;
                }
            }
            for (int f = 0; f < r.floors; f++)
            {
                // Two-cell-wide circulation spine remains clear on every floor.
                Reserve(layout, f, 0, r.depth / 2 - 1, r.width, 2);
                Reserve(layout, f, r.width / 3, 0, 2, r.depth);
                Reserve(layout, f, r.width * 2 / 3, 0, 2, r.depth);
                var a = new Vector3Int(r.width / 2 - 1, f, r.depth / 2 - 3);
                var b = new Vector3Int(r.width / 2, f, r.depth / 2 + 3);
                layout.Spawns.Add(a); layout.Spawns.Add(b);
                Reserve(layout, f, a.x - 1, a.z - 1, 3, 3); Reserve(layout, f, b.x - 1, b.z - 1, 3, 3);
            }
            // Check structural connectivity before considering content.
            if (layout.ReachableCount(new HashSet<Vector3Int>()) != layout.FloorCount()) throw new InvalidOperationException("Arena structure is disconnected.");
            for (int f = 0; f < r.floors; f++)
            {
                Place(layout, f, FpsArenaContentKind.Cover, r.coverPerFloor);
                Place(layout, f, FpsArenaContentKind.Obstacle, r.obstaclesPerFloor);
                Place(layout, f, FpsArenaContentKind.Item, r.itemsPerFloor);
            }
            layout.Validate(); layout.Hash = layout.ComputeHash(); return layout;
        }
        public static bool InShape(FpsArenaRecipe r, int x, int z)
        {
            if (x < 0 || z < 0 || x >= r.width || z >= r.depth) return false;
            float nx = Mathf.Abs((x + .5f) * 2 / r.width - 1), nz = Mathf.Abs((z + .5f) * 2 / r.depth - 1);
            if (r.shape == FpsArenaShape.Ellipse) return nx * nx + nz * nz <= 1;
            if (r.shape == FpsArenaShape.Octagon) return nx + nz <= 1.55f;
            return true;
        }
        private static void Reserve(FpsArenaLayout l, int f, int x0, int z0, int width, int depth)
        {
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(l.Recipe.width, x0 + width); x++)
                for (int z = Mathf.Max(0, z0); z < Mathf.Min(l.Recipe.depth, z0 + depth); z++) l.Reserved[x, f, z] = true;
        }
        private static void Place(FpsArenaLayout l, int floor, FpsArenaContentKind kind, int requested)
        {
            if (requested == 0) return;
            var r = l.Recipe; var candidates = new List<Vector3Int>();
            for (int z = 1; z < r.depth - 1; z++) for (int x = 1; x < r.width - 1; x++)
            {
                var c = new Vector3Int(x, floor, z);
                if (!l.HasFloor(c) || l.Reserved[x, floor, z]) continue;
                bool clearance = true;
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                    if (!l.HasFloor(c + new Vector3Int(dx, 0, dz))) clearance = false;
                if (clearance) candidates.Add(c);
            }
            var rng = DungeonStableRandomStreams.Create(l.Seed, ContentStream + (ulong)(floor * 3 + (int)kind));
            for (int i = candidates.Count - 1; i > 0; i--) { int j = rng.NextInt(0, i + 1); var temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp; }
            int count = 0;
            foreach (var c in candidates)
            {
                bool spaced = true;
                foreach (var existing in l.Content)
                    if (existing.cell.y == floor && Mathf.Abs(existing.cell.x - c.x) <= r.spacingCells && Mathf.Abs(existing.cell.z - c.z) <= r.spacingCells) { spaced = false; break; }
                if (!spaced) continue;
                // The complete eight-neighbor ring is floor, and spacing >= 1 keeps it
                // free of blockers. Removing this cell cannot disconnect that ring.
                l.Content.Add(new FpsArenaContent { id = "arena:v1:" + floor + ":" + kind + ":" + c.x + ":" + c.z, kind = kind, cell = c });
                if (++count == requested) break;
            }
        }
    }
}
