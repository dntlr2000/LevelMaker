using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static partial class FpsArenaPlanner
    {
        private const ulong StairStreamV2 = 0x4152454E41535432UL;
        private const ulong ContentStreamV2 = 0x4152454E41435432UL;
        private static FpsArenaLayout GenerateFlexible(FpsArenaRecipe source, int seed, FpsArenaCatalogSnapshot snapshot)
        {
            var r = source.Normalized();
            var l = new FpsArenaLayout { Recipe = r, Seed = seed, Floors = new bool[r.width, r.floors, r.depth], Reserved = new bool[r.width, r.floors, r.depth], CatalogSnapshot = snapshot != null ? snapshot.Clone() : new FpsArenaCatalogSnapshot() };
            ValidateSnapshot(l.CatalogSnapshot);
            for (int f = 0; f < r.floors; f++) for (int z = 0; z < r.depth; z++) for (int x = 0; x < r.width; x++) l.Floors[x, f, z] = InShape(r, x, z);
            for (int f = 0; f < r.floors; f++)
            {
                Reserve(l, f, 0, r.depth / 2 - 1, r.width, 2);
                Reserve(l, f, r.width / 2 - 1, 0, 2, r.depth);
                var a = new Vector3Int(r.width / 2 - 1, f, r.depth / 2 - 3);
                var b = new Vector3Int(r.width / 2, f, r.depth / 2 + 3);
                l.Spawns.Add(a); l.Spawns.Add(b);
                Reserve(l, f, a.x - 1, a.z - 1, 3, 3); Reserve(l, f, b.x - 1, b.z - 1, 3, 3);
            }
            int run = Mathf.CeilToInt(r.floorHeight / (r.cellSize * .6f));
            for (int f = 0; f < r.floors - 1; f++)
            {
                if (r.randomizeStairDirections) { PlaceDirectionalStairs(l, f, run); continue; }
                var candidates = new List<FpsArenaStair>();
                for (int z = 2; z < r.depth - run - 1; z++) for (int x = 1; x < r.width - 2; x++)
                {
                    // Alternate front/back bands prevents adjacent-storey stair overlaps;
                    // placement inside each band genuinely varies with the structural seed.
                    if (f % 2 == 0 ? z + run > r.depth / 2 - 1 : z < r.depth / 2 + 1) continue;
                    var s = new FpsArenaStair { lowerFloor = f, x = x, z = z, length = run };
                    if (StairFits(l, s)) candidates.Add(s);
                }
                var rng = DungeonStableRandomStreams.Create(seed, StairStreamV2 + (ulong)f);
                Shuffle(candidates, rng);
                FpsArenaStair first = null, second = null;
                foreach (var a in candidates)
                {
                    if (r.stairsPerFloor == 1) { first = a; break; }
                    foreach (var b in candidates)
                        if (StairsSeparated(a, b)) { first = a; second = b; break; }
                    if (first != null) break;
                }
                if (first == null) throw new InvalidOperationException("Arena cannot fit the requested stair landings.");
                AddStair(l, first); if (second != null) AddStair(l, second);
            }
            if (l.ReachableCount(new HashSet<Vector3Int>()) != l.FloorCount()) throw new InvalidOperationException("Arena V2 structure is disconnected.");
            if (r.partitionRooms) PartitionArenaRooms(l);
            else foreach (var stair in l.Stairs) for (int lane = 0; lane < stair.width; lane++)
            {
                ReserveWalkRoute(l, stair.BottomLane(lane));
                ReserveWalkRoute(l, stair.TopLane(lane));
            }
            if (r.internalWalls && !r.partitionRooms)
            {
                var targets = new int[r.floors];
                for (int f = 0; f < r.floors; f++) targets[f] = l.RequestedWallCells(f);
                l.WallCellTargets = targets;
                for (int f = 0; f < r.floors; f++) PlaceInternalWalls(l, f);
            }
            foreach (var kind in new[] { FpsArenaContentKind.Cover, FpsArenaContentKind.Enemy, FpsArenaContentKind.Gimmick, FpsArenaContentKind.Item })
                for (int f = 0; f < r.floors; f++) PlaceFlexible(l, f, kind, l.RequestedCount(f, kind));
            l.Validate(); l.Hash = l.ComputeHash(); return l;
        }
        private static void ValidateSnapshot(FpsArenaCatalogSnapshot snapshot)
        {
            if (snapshot.entries == null) throw new InvalidOperationException("Arena catalog snapshot entries are missing.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in snapshot.entries)
                if (s == null || string.IsNullOrWhiteSpace(s.contentKey) || s.contentKey != s.contentKey.Trim() || !keys.Add(s.contentKey) || s.kind == FpsArenaContentKind.Obstacle || !Enum.IsDefined(typeof(FpsArenaContentKind), s.kind) || !Positive(s.weight) || s.widthCells < 1 || s.widthCells > 4 || s.depthCells < 1 || s.depthCells > 4 || !Positive(s.height) || s.height > 6 || !Positive(s.minSizeFactor) || !Positive(s.maxSizeFactor) || s.minSizeFactor < .25f || s.maxSizeFactor > 1 || s.minSizeFactor > s.maxSizeFactor) throw new InvalidOperationException("Arena catalog snapshot is invalid.");
            snapshot.entries.Sort((a,b) => StringComparer.Ordinal.Compare(a.contentKey, b.contentKey));
        }
        private static bool Positive(float v) { return v > 0 && !float.IsNaN(v) && !float.IsInfinity(v); }
        private static bool StairFits(FpsArenaLayout l, FpsArenaStair s)
        {
            for (int f = s.lowerFloor; f <= s.lowerFloor + 1; f++)
            for (int z = s.z - 1; z <= s.z + s.FootprintDepth; z++) for (int x = s.x - 1; x <= s.x + s.FootprintWidth; x++)
                if (!l.HasFloor(new Vector3Int(x, f, z))) return false;
            for (int z = s.z; z < s.z + s.FootprintDepth; z++) for (int x = s.x; x < s.x + s.FootprintWidth; x++)
                if (l.Reserved[x, s.lowerFloor, z] || l.Reserved[x, s.lowerFloor + 1, z]) return false;
            return true;
        }
        private static bool StairsSeparated(FpsArenaStair a, FpsArenaStair b)
        { return a.x + a.FootprintWidth + 1 < b.x || b.x + b.FootprintWidth + 1 < a.x || a.z + a.FootprintDepth + 1 < b.z || b.z + b.FootprintDepth + 1 < a.z; }
        private static void PlaceDirectionalStairs(FpsArenaLayout l, int floor, int run)
        {
            var r=l.Recipe;var candidates=new List<FpsArenaStair>();int mask=0;
            for(int q=0;q<4;q++)
            {
                int width=(q&1)==0?2:run,depth=(q&1)==0?run:2;
                for(int z=1;z<=r.depth-depth-1;z++)for(int x=1;x<=r.width-width-1;x++)
                {
                    var s=new FpsArenaStair { lowerFloor=floor,x=x,z=z,length=run,direction=(FpsArenaStairDirection)q };
                    if(StairFits(l,s)){candidates.Add(s);mask|=1<<q;}
                }
            }
            var rng=DungeonStableRandomStreams.Create(l.Seed,StairStreamV2+(ulong)floor);Shuffle(candidates,rng);
            FpsArenaStair first=null,second=null;
            foreach(var a in candidates)
            {
                if(r.stairsPerFloor==1){first=a;break;}
                foreach(var b in candidates)if(StairsSeparated(a,b)){first=a;second=b;break;}
                if(first!=null)break;
            }
            if(first==null)throw new InvalidOperationException("Arena cannot fit the requested directional stairs with clear landings and openings.");
            l.StairDirectionReports.Add(new FpsArenaStairDirectionReport { lowerFloor=floor,validDirectionsMask=mask,candidates=candidates.Count,reason=mask==15?"":"외곽과 양층 바닥, 스폰·기존 계단·착지 보호 영역을 통과하는 방향만 후보로 사용합니다." });
            AddStair(l,first);if(second!=null)AddStair(l,second);
        }
        private static void AddStair(FpsArenaLayout l, FpsArenaStair s)
        {
            l.Stairs.Add(s);
            for (int f = s.lowerFloor; f <= s.lowerFloor + 1; f++)
            {
                var bounds=s.ProtectedBounds;
                Reserve(l, f, bounds.x, bounds.y, bounds.width, bounds.height);
                for (int xx = s.x; xx < s.x + s.FootprintWidth; xx++) for (int z = s.z; z < s.z + s.FootprintDepth; z++) l.Floors[xx, f, z] = false;
            }
        }
        private static void ReserveWalkRoute(FpsArenaLayout l, Vector3Int start)
        {
            var target = l.Spawns[start.y * 2]; var queue = new Queue<Vector3Int>(); var previous = new Dictionary<Vector3Int, Vector3Int>();
            queue.Enqueue(start); previous.Add(start, start);
            var offsets = new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            while (queue.Count > 0 && !previous.ContainsKey(target))
            {
                var current = queue.Dequeue();
                foreach (var offset in offsets)
                {
                    var next = current + offset;
                    if (l.HasFloor(next) && !previous.ContainsKey(next)) { previous.Add(next, current); queue.Enqueue(next); }
                }
            }
            if (!previous.ContainsKey(target)) throw new InvalidOperationException("Arena stair has no floor-local landing route.");
            // Reserve a broad walkable margin around the routed centerline; route after
            // all openings exist, so another seeded stair cannot cut a straight shortcut.
            for (var current = target; ; current = previous[current])
            {
                Reserve(l, current.y, current.x - 1, current.z - 1, 3, 3);
                if (current == start) break;
            }
        }
        private static void Shuffle<T>(List<T> items, DungeonStableRandom rng)
        { for (int i = items.Count - 1; i > 0; i--) { int j = rng.NextInt(0, i + 1); T temp = items[i]; items[i] = items[j]; items[j] = temp; } }
        private static void PlaceFlexible(FpsArenaLayout l, int floor, FpsArenaContentKind kind, int requested)
        {
            if (requested == 0) return;
            var r = l.Recipe; var candidates = new List<Vector3Int>();
            for (int z = 1; z < r.depth - 1; z++) for (int x = 1; x < r.width - 1; x++) if (l.Floors[x, floor, z] && !l.Reserved[x, floor, z]) candidates.Add(new Vector3Int(x, floor, z));
            var rng = DungeonStableRandomStreams.Create(l.Seed, ContentStreamV2 + (ulong)(floor * 16 + (int)kind));
            Shuffle(candidates, rng); var occupied = new HashSet<Vector3Int>();
            foreach (var wall in l.Walls) if (wall.cell.y == floor) foreach (var c in wall.Footprint()) occupied.Add(c);
            foreach (var old in l.Content) if (old.cell.y == floor) foreach (var c in old.Footprint()) occupied.Add(c);
            int count = 0;
            foreach (var c in candidates)
            {
                var spec = SelectSpec(l.CatalogSnapshot, kind, rng);
                int width = spec != null ? spec.widthCells : kind == FpsArenaContentKind.Cover ? rng.NextInt(r.coverMinWidthCells, r.coverMaxWidthCells + 1) : 1;
                int depth = spec != null ? spec.depthCells : kind == FpsArenaContentKind.Cover ? rng.NextInt(r.coverMinDepthCells, r.coverMaxDepthCells + 1) : 1;
                if (!ContentFits(l, c, width, depth, occupied)) continue;
                float height = spec != null ? Mathf.Min(spec.height, r.floorHeight - .25f) : kind == FpsArenaContentKind.Cover ? Mathf.Lerp(r.coverMinHeight, r.coverMaxHeight, rng.NextFloat01()) : kind == FpsArenaContentKind.Enemy ? 1.8f : kind == FpsArenaContentKind.Gimmick ? .8f : .35f;
                float sx = spec != null ? Mathf.Lerp(spec.minSizeFactor, spec.maxSizeFactor, rng.NextFloat01()) : .55f + rng.NextFloat01() * .3f;
                float sz = spec != null ? sx : .55f + rng.NextFloat01() * .3f;
                var p = new FpsArenaContent { id = "arena:v2:" + floor + ":" + kind + ":" + c.x + ":" + c.z, kind = kind, cell = c, widthCells = width, depthCells = depth, size = new Vector3(width * r.cellSize * sx, height, depth * r.cellSize * sz), yaw = rng.NextInt(0, 2) * 180, contentKey = spec != null ? spec.contentKey : "", coverShape = SelectCoverShape(r.coverShapes, rng) };
                if (spec == null && kind != FpsArenaContentKind.Cover) p.size = new Vector3(kind == FpsArenaContentKind.Item ? .5f : .7f, height, kind == FpsArenaContentKind.Item ? .5f : .7f);
                l.Content.Add(p); foreach (var cell in p.Footprint()) occupied.Add(cell);
                if ((r.internalWalls || r.partitionRooms) && p.IsBlocking)
                {
                    var blocked = l.BlockedCells();
                    if (l.ReachableCount(blocked) != l.FloorCount() - blocked.Count)
                    { l.Content.Remove(p); foreach (var cell in p.Footprint()) occupied.Remove(cell); continue; }
                }
                if (++count == requested) break;
            }
        }
        private static bool ContentFits(FpsArenaLayout l, Vector3Int c, int width, int depth, HashSet<Vector3Int> occupied)
        {
            // The entire one-cell walkable ring around each rectangle remains clear.
            // Removing an interior rectangle therefore cannot disconnect the floor.
            for (int z = -1; z <= depth; z++) for (int x = -1; x <= width; x++) if (!l.HasFloor(c + new Vector3Int(x, 0, z))) return false;
            for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
                if (l.Reserved[c.x + x, c.y, c.z + z] || (l.Recipe.partitionRooms && l.RoomAt(c + new Vector3Int(x,0,z)) != l.RoomAt(c))) return false;
            for (int z = -l.Recipe.spacingCells; z < depth + l.Recipe.spacingCells; z++) for (int x = -l.Recipe.spacingCells; x < width + l.Recipe.spacingCells; x++) if (occupied.Contains(c + new Vector3Int(x, 0, z))) return false;
            return true;
        }
        private static FpsArenaCatalogSpec SelectSpec(FpsArenaCatalogSnapshot snapshot, FpsArenaContentKind kind, DungeonStableRandom rng)
        {
            double total = 0; foreach (var s in snapshot.entries) if (s.kind == kind) total += s.weight;
            if (total == 0) return null;
            double value = rng.NextFloat01() * total; FpsArenaCatalogSpec last = null;
            foreach (var s in snapshot.entries) if (s.kind == kind) { last = s; value -= s.weight; if (value < 0) return s; }
            return last;
        }
        private static FpsArenaCoverShape SelectCoverShape(FpsArenaCoverShapes mask, DungeonStableRandom rng)
        {
            var choices = new List<FpsArenaCoverShape>();
            if ((mask & FpsArenaCoverShapes.Box) != 0) choices.Add(FpsArenaCoverShape.Box);
            if ((mask & FpsArenaCoverShapes.Cylinder) != 0) choices.Add(FpsArenaCoverShape.Cylinder);
            if ((mask & FpsArenaCoverShapes.Corner) != 0) choices.Add(FpsArenaCoverShape.Corner);
            return choices[rng.NextInt(0, choices.Count)];
        }
    }
}
