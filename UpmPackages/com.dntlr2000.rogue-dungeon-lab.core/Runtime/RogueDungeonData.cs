using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    [Serializable]
    public sealed class DensityProfile
    {
        [Range(0f, 0.5f)] public float baseDensity = 0.04f;
        public AnimationCurve overProgression = AnimationCurve.Linear(0f, 0.75f, 1f, 1.25f);
        [Range(0f, 1f)] public float roomBias = 0.65f;
        [Range(0f, 1f)] public float clustering = 0.25f;
        [Min(0)] public int maxCount = 100;

        public float EvaluateProbability(float progression, bool isInsideRoom, float clusterNoise)
        {
            EnsureValid();
            float curveMultiplier = Mathf.Max(0f, overProgression.Evaluate(Mathf.Clamp01(progression)));
            float roomMultiplier = isInsideRoom
                ? Mathf.Lerp(1f, 1.45f, roomBias)
                : Mathf.Lerp(1f, 0.55f, roomBias);
            float clumpValue = Mathf.Lerp(0.2f, 1.8f, Mathf.Clamp01(clusterNoise));
            float clusterMultiplier = Mathf.Lerp(1f, clumpValue, clustering);
            return Mathf.Clamp01(baseDensity * curveMultiplier * roomMultiplier * clusterMultiplier);
        }

        public void EnsureValid()
        {
            baseDensity = Mathf.Clamp(baseDensity, 0f, 0.5f);
            roomBias = Mathf.Clamp01(roomBias);
            clustering = Mathf.Clamp01(clustering);
            maxCount = Mathf.Max(0, maxCount);
            if (overProgression == null || overProgression.length == 0)
            {
                overProgression = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }
        }

        public static DensityProfile EnemyDefault()
        {
            return new DensityProfile
            {
                baseDensity = 0.035f,
                overProgression = new AnimationCurve(
                    new Keyframe(0f, 0.45f),
                    new Keyframe(0.35f, 0.9f),
                    new Keyframe(1f, 1.55f)),
                roomBias = 0.78f,
                clustering = 0.2f,
                maxCount = 80
            };
        }

        public static DensityProfile DestructibleDefault()
        {
            return new DensityProfile
            {
                baseDensity = 0.055f,
                overProgression = new AnimationCurve(
                    new Keyframe(0f, 1.15f),
                    new Keyframe(0.55f, 1f),
                    new Keyframe(1f, 0.75f)),
                roomBias = 0.82f,
                clustering = 0.42f,
                maxCount = 120
            };
        }

        public static DensityProfile PropDefault()
        {
            return new DensityProfile
            {
                baseDensity = 0.075f,
                overProgression = new AnimationCurve(
                    new Keyframe(0f, 0.8f),
                    new Keyframe(0.5f, 1.25f),
                    new Keyframe(1f, 0.95f)),
                roomBias = 0.35f,
                clustering = 0.7f,
                maxCount = 180
            };
        }
    }

    [Serializable]
    public sealed class DropEntry
    {
        public string itemId = "Gold";
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int minQuantity = 1;
        [Min(0)] public int maxQuantity = 1;
        public bool representsNoDrop;
        public Color markerColor = new Color(1f, 0.85f, 0.2f, 1f);

        public void ClampValues()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = representsNoDrop ? "Nothing" : "UnnamedItem";
            }
            weight = Mathf.Max(0f, weight);
            minQuantity = Mathf.Max(0, minQuantity);
            maxQuantity = Mathf.Max(minQuantity, maxQuantity);
            if (representsNoDrop)
            {
                minQuantity = 0;
                maxQuantity = 0;
            }
        }
    }

    public struct DropRoll
    {
        public readonly string ItemId;
        public readonly int Quantity;
        public readonly bool IsNoDrop;
        public readonly Color MarkerColor;

        public DropRoll(string itemId, int quantity, bool isNoDrop, Color markerColor)
        {
            ItemId = itemId;
            Quantity = quantity;
            IsNoDrop = isNoDrop;
            MarkerColor = markerColor;
        }

        public static DropRoll Invalid()
        {
            return new DropRoll("Nothing", 0, true, Color.clear);
        }
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Weighted Drop Table", fileName = "DropTable")]
    public sealed partial class WeightedDropTable : ScriptableObject
    {
        public List<DropEntry> entries = new List<DropEntry>();

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < entries.Count; i++)
                {
                    DropEntry entry = entries[i];
                    if (entry != null) total += Mathf.Max(0f, entry.weight);
                }
                return total;
            }
        }

        public DropRoll Roll(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            float total = TotalWeight;
            if (entries.Count == 0 || total <= 0f) return DropRoll.Invalid();

            double pick = random.NextDouble() * total;
            float cursor = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                DropEntry entry = entries[i];
                if (entry == null || entry.weight <= 0f) continue;
                cursor += entry.weight;
                if (pick <= cursor)
                {
                    entry.ClampValues();
                    int quantity = entry.representsNoDrop ? 0 : random.Next(entry.minQuantity, entry.maxQuantity + 1);
                    return new DropRoll(entry.itemId, quantity, entry.representsNoDrop, entry.markerColor);
                }
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                DropEntry entry = entries[i];
                if (entry == null || entry.weight <= 0f) continue;
                entry.ClampValues();
                int quantity = entry.representsNoDrop ? 0 : random.Next(entry.minQuantity, entry.maxQuantity + 1);
                return new DropRoll(entry.itemId, quantity, entry.representsNoDrop, entry.markerColor);
            }
            return DropRoll.Invalid();
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null) entries[i].ClampValues();
            }
        }
    }

    internal static class RuntimeDropTables
    {
        private static WeightedDropTable s_enemy;
        private static WeightedDropTable s_destructible;

        public static WeightedDropTable Enemy
        {
            get
            {
                if (s_enemy == null)
                {
                    s_enemy = ScriptableObject.CreateInstance<WeightedDropTable>();
                    s_enemy.name = "Runtime Enemy Drop Table";
                    s_enemy.hideFlags = HideFlags.HideAndDontSave;
                    s_enemy.entries.Add(new DropEntry { itemId = "Gold", weight = 55f, minQuantity = 1, maxQuantity = 3, markerColor = new Color(1f, 0.82f, 0.15f) });
                    s_enemy.entries.Add(new DropEntry { itemId = "Potion", weight = 15f, markerColor = new Color(0.9f, 0.2f, 0.35f) });
                    s_enemy.entries.Add(new DropEntry { itemId = "RareShard", weight = 5f, markerColor = new Color(0.5f, 0.75f, 1f) });
                    s_enemy.entries.Add(new DropEntry { itemId = "Nothing", weight = 25f, representsNoDrop = true, markerColor = Color.clear });
                }
                return s_enemy;
            }
        }

        public static WeightedDropTable Destructible
        {
            get
            {
                if (s_destructible == null)
                {
                    s_destructible = ScriptableObject.CreateInstance<WeightedDropTable>();
                    s_destructible.name = "Runtime Destructible Drop Table";
                    s_destructible.hideFlags = HideFlags.HideAndDontSave;
                    s_destructible.entries.Add(new DropEntry { itemId = "Gold", weight = 45f, minQuantity = 1, maxQuantity = 2, markerColor = new Color(1f, 0.82f, 0.15f) });
                    s_destructible.entries.Add(new DropEntry { itemId = "CraftMaterial", weight = 30f, minQuantity = 1, maxQuantity = 4, markerColor = new Color(0.45f, 0.9f, 0.55f) });
                    s_destructible.entries.Add(new DropEntry { itemId = "Potion", weight = 5f, markerColor = new Color(0.9f, 0.2f, 0.35f) });
                    s_destructible.entries.Add(new DropEntry { itemId = "Nothing", weight = 20f, representsNoDrop = true, markerColor = Color.clear });
                }
                return s_destructible;
            }
        }
    }

    public enum DungeonPreset { Compact, Balanced, Chaos }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Dungeon Settings", fileName = "RogueDungeonSettings")]
    public sealed partial class RogueDungeonSettings : ScriptableObject
    {
        [Header("Seed")]
        public int seed = 12345;

        [Header("Stage Geometry")]
        [Range(12, 96)] public int stageWidthCells = 42;
        [Range(12, 96)] public int stageDepthCells = 42;
        [Range(1.5f, 6f)] public float cellSize = 3f;
        [Range(1.5f, 8f)] public float wallHeight = 3.2f;

        [Header("Rooms and Corridors")]
        [Range(2, 40)] public int desiredRoomCount = 14;
        public Vector2Int roomSizeMin = new Vector2Int(4, 4);
        public Vector2Int roomSizeMax = new Vector2Int(9, 9);
        [Range(5, 100)] public int roomPlacementAttempts = 35;
        [Range(1, 4)] public int corridorWidthCells = 1;
        [Range(0f, 1f)] public float extraConnectionChance = 0.2f;

        [Header("Stage Contents")]
        [Range(0, 30)] public int specialGimmickCount = 4;
        [Range(0, 4)] public int contentSpacingCells = 1;
        [Range(0, 8)] public int reservedEntranceRadiusCells = 3;
        public DensityProfile enemyProfile = DensityProfile.EnemyDefault();
        public DensityProfile destructibleProfile = DensityProfile.DestructibleDefault();
        public DensityProfile propProfile = DensityProfile.PropDefault();

        [Header("Drop Validation")]
        public WeightedDropTable enemyDropTable;
        public WeightedDropTable destructibleDropTable;
        public bool spawnDropMarkers = true;
        public bool resetDropStatsOnGenerate;

        [Header("Runtime")]
        public bool generateOnPlay = true;

        public WeightedDropTable EffectiveEnemyDropTable { get { return enemyDropTable != null ? enemyDropTable : RuntimeDropTables.Enemy; } }
        public WeightedDropTable EffectiveDestructibleDropTable { get { return destructibleDropTable != null ? destructibleDropTable : RuntimeDropTables.Destructible; } }

        public void ClampValues()
        {
            stageWidthCells = Mathf.Clamp(stageWidthCells, 12, 96);
            stageDepthCells = Mathf.Clamp(stageDepthCells, 12, 96);
            cellSize = Mathf.Clamp(cellSize, 1.5f, 6f);
            wallHeight = Mathf.Clamp(wallHeight, 1.5f, 8f);
            desiredRoomCount = Mathf.Clamp(desiredRoomCount, 2, 40);
            roomPlacementAttempts = Mathf.Clamp(roomPlacementAttempts, 5, 100);
            corridorWidthCells = Mathf.Clamp(corridorWidthCells, 1, 4);
            extraConnectionChance = Mathf.Clamp01(extraConnectionChance);
            specialGimmickCount = Mathf.Clamp(specialGimmickCount, 0, 30);
            contentSpacingCells = Mathf.Clamp(contentSpacingCells, 0, 4);
            reservedEntranceRadiusCells = Mathf.Clamp(reservedEntranceRadiusCells, 0, 8);

            roomSizeMin.x = Mathf.Clamp(roomSizeMin.x, 3, Mathf.Max(3, stageWidthCells - 4));
            roomSizeMin.y = Mathf.Clamp(roomSizeMin.y, 3, Mathf.Max(3, stageDepthCells - 4));
            roomSizeMax.x = Mathf.Clamp(roomSizeMax.x, roomSizeMin.x, Mathf.Max(roomSizeMin.x, stageWidthCells - 4));
            roomSizeMax.y = Mathf.Clamp(roomSizeMax.y, roomSizeMin.y, Mathf.Max(roomSizeMin.y, stageDepthCells - 4));

            if (enemyProfile == null) enemyProfile = DensityProfile.EnemyDefault();
            if (destructibleProfile == null) destructibleProfile = DensityProfile.DestructibleDefault();
            if (propProfile == null) propProfile = DensityProfile.PropDefault();
            enemyProfile.EnsureValid();
            destructibleProfile.EnsureValid();
            propProfile.EnsureValid();
        }

        public void ApplyPreset(DungeonPreset preset)
        {
            if (preset == DungeonPreset.Compact)
            {
                stageWidthCells = 26; stageDepthCells = 26; desiredRoomCount = 8;
                roomSizeMin = new Vector2Int(4, 4); roomSizeMax = new Vector2Int(7, 7);
                corridorWidthCells = 1; extraConnectionChance = 0.12f; specialGimmickCount = 2;
                enemyProfile = DensityProfile.EnemyDefault(); enemyProfile.baseDensity = 0.025f; enemyProfile.maxCount = 30;
                destructibleProfile = DensityProfile.DestructibleDefault(); destructibleProfile.baseDensity = 0.04f; destructibleProfile.maxCount = 45;
                propProfile = DensityProfile.PropDefault(); propProfile.baseDensity = 0.055f; propProfile.maxCount = 70;
            }
            else if (preset == DungeonPreset.Chaos)
            {
                stageWidthCells = 64; stageDepthCells = 56; desiredRoomCount = 24;
                roomSizeMin = new Vector2Int(4, 4); roomSizeMax = new Vector2Int(11, 10);
                corridorWidthCells = 2; extraConnectionChance = 0.42f; specialGimmickCount = 10;
                enemyProfile = DensityProfile.EnemyDefault(); enemyProfile.baseDensity = 0.055f; enemyProfile.clustering = 0.58f; enemyProfile.maxCount = 170;
                destructibleProfile = DensityProfile.DestructibleDefault(); destructibleProfile.baseDensity = 0.08f; destructibleProfile.clustering = 0.7f; destructibleProfile.maxCount = 220;
                propProfile = DensityProfile.PropDefault(); propProfile.baseDensity = 0.105f; propProfile.maxCount = 320;
            }
            else
            {
                stageWidthCells = 42; stageDepthCells = 42; desiredRoomCount = 14;
                roomSizeMin = new Vector2Int(4, 4); roomSizeMax = new Vector2Int(9, 9);
                corridorWidthCells = 1; extraConnectionChance = 0.2f; specialGimmickCount = 4;
                enemyProfile = DensityProfile.EnemyDefault();
                destructibleProfile = DensityProfile.DestructibleDefault();
                propProfile = DensityProfile.PropDefault();
            }
            ClampValues();
        }

        private void OnValidate() { ClampValues(); }
    }

    [Serializable]
    public sealed class GenerationReport
    {
        public int activeSeed;
        public int roomCount;
        public int floorCellCount;
        public int enemyCount;
        public int destructibleCount;
        public int propCount;
        public int gimmickCount;
        public int meshTriangleCount;
        public double generationMilliseconds;
        public Bounds worldBounds;
        public List<string> warnings = new List<string>();
    }

    public sealed class DungeonLayout
    {
        private readonly bool[,] _floor;
        private readonly int[,] _roomIds;
        private readonly int[,] _distance;
        private readonly List<RectInt> _rooms = new List<RectInt>();

        public int Width { get; private set; }
        public int Depth { get; private set; }
        public Vector2Int Entrance { get; internal set; }
        public Vector2Int Exit { get; internal set; }
        public int MaxDistance { get; internal set; }
        public int WalkableCellCount { get; internal set; }
        public IReadOnlyList<RectInt> Rooms { get { return _rooms; } }

        public DungeonLayout(int width, int depth)
        {
            Width = width; Depth = depth;
            _floor = new bool[width, depth];
            _roomIds = new int[width, depth];
            _distance = new int[width, depth];
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                _roomIds[x, z] = -1;
                _distance[x, z] = -1;
            }
        }

        internal void AddRoom(RectInt room) { _rooms.Add(room); }
        public bool InBounds(Vector2Int c) { return c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Depth; }
        public bool IsFloor(Vector2Int c) { return InBounds(c) && _floor[c.x, c.y]; }
        public int GetRoomId(Vector2Int c) { return InBounds(c) ? _roomIds[c.x, c.y] : -1; }
        public int GetDistance(Vector2Int c) { return InBounds(c) ? _distance[c.x, c.y] : -1; }
        public float GetProgression(Vector2Int c)
        {
            int d = GetDistance(c);
            return d < 0 || MaxDistance <= 0 ? 0f : Mathf.Clamp01(d / (float)MaxDistance);
        }
        public Vector3 CellToLocalPosition(Vector2Int c, float cellSize)
        {
            return new Vector3((c.x - Width * 0.5f + 0.5f) * cellSize, 0f, (c.y - Depth * 0.5f + 0.5f) * cellSize);
        }
        public IEnumerable<Vector2Int> EnumerateFloorCells()
        {
            for (int x = 0; x < Width; x++)
            for (int z = 0; z < Depth; z++)
                if (_floor[x, z]) yield return new Vector2Int(x, z);
        }
        internal void SetFloor(Vector2Int c, int roomId, bool preserveExistingRoomId)
        {
            if (!InBounds(c)) return;
            if (!_floor[c.x, c.y]) WalkableCellCount++;
            _floor[c.x, c.y] = true;
            if (!preserveExistingRoomId || _roomIds[c.x, c.y] < 0) _roomIds[c.x, c.y] = roomId;
        }
        internal void SetDistance(Vector2Int c, int d) { if (InBounds(c)) _distance[c.x, c.y] = d; }
    }
}
