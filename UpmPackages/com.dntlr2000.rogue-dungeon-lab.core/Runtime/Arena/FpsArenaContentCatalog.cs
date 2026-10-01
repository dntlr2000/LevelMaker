using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    [Serializable]
    public sealed class FpsArenaCatalogEntry
    {
        public string contentKey;
        public FpsArenaContentKind kind = FpsArenaContentKind.Enemy;
        public GameObject prefab;
        [Min(.001f)] public float weight = 1;
        [Range(1, 4)] public int widthCells = 1, depthCells = 1;
        [Range(.1f, 6)] public float height = 1.8f;
        [Range(.25f, 1)] public float minSizeFactor = .8f, maxSizeFactor = 1;
        // Actual authored local bounds; fitting keeps the instance in its planned footprint.
        public Vector3 authoredBounds = new Vector3(1, 2, 1);
        public Vector3 authoredBoundsCenter = new Vector3(0, 1, 0);
    }
    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/FPS Arena 콘텐츠 카탈로그", fileName = "FpsArenaContentCatalog")]
    public sealed class FpsArenaContentCatalog : ScriptableObject
    {
        public List<FpsArenaCatalogEntry> entries = new List<FpsArenaCatalogEntry>();
        public FpsArenaCatalogSnapshot Snapshot()
        {
            var result = new FpsArenaCatalogSnapshot(); var keys = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null) return result;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.contentKey) || e.contentKey != e.contentKey.Trim() || !keys.Add(e.contentKey)) throw new InvalidOperationException("Arena catalog requires unique non-empty content keys.");
                if (e.kind == FpsArenaContentKind.Obstacle || !Enum.IsDefined(typeof(FpsArenaContentKind), e.kind)) throw new InvalidOperationException("V2 catalog supports cover, enemy, gimmick and item categories.");
                if (e.prefab == null) throw new InvalidOperationException("Arena catalog prefab is missing: " + e.contentKey);
                if (!FinitePositive(e.weight) || e.widthCells < 1 || e.widthCells > 4 || e.depthCells < 1 || e.depthCells > 4 || !FinitePositive(e.height) || e.height > 6 || !FinitePositive(e.authoredBounds.x) || !FinitePositive(e.authoredBounds.y) || !FinitePositive(e.authoredBounds.z) || !Finite(e.authoredBoundsCenter.x) || !Finite(e.authoredBoundsCenter.y) || !Finite(e.authoredBoundsCenter.z) || e.minSizeFactor < .25f || e.maxSizeFactor > 1 || e.minSizeFactor > e.maxSizeFactor || !FinitePositive(e.minSizeFactor) || !FinitePositive(e.maxSizeFactor)) throw new InvalidOperationException("Arena catalog dimensions/weight are invalid: " + e.contentKey);
                result.entries.Add(new FpsArenaCatalogSpec { contentKey = e.contentKey, kind = e.kind, weight = e.weight, widthCells = e.widthCells, depthCells = e.depthCells, height = e.height, minSizeFactor = e.minSizeFactor, maxSizeFactor = e.maxSizeFactor });
            }
            result.entries.Sort((a,b) => StringComparer.Ordinal.Compare(a.contentKey, b.contentKey));
            return result;
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static bool FinitePositive(float v) { return v > 0 && !float.IsNaN(v) && !float.IsInfinity(v); }
        public FpsArenaCatalogEntry Find(string key)
        { if (entries != null) foreach (var e in entries) if (e != null && string.Equals(e.contentKey, key, StringComparison.Ordinal)) return e; return null; }
    }
    [Serializable] public sealed class FpsArenaCatalogSpec
    {
        public string contentKey; public FpsArenaContentKind kind; public float weight;
        public int widthCells, depthCells; public float height, minSizeFactor, maxSizeFactor;
    }
    [Serializable] public sealed class FpsArenaCatalogSnapshot
    {
        public List<FpsArenaCatalogSpec> entries = new List<FpsArenaCatalogSpec>();
        public FpsArenaCatalogSnapshot Clone() { return JsonUtility.FromJson<FpsArenaCatalogSnapshot>(JsonUtility.ToJson(this)); }
    }
    public sealed class FpsArenaSpawnContext
    {
        public FpsArenaLayout Layout { get; private set; }
        public FpsArenaContent Content { get; private set; }
        public FpsArenaContentIdentity Identity { get; private set; }
        public FpsArenaSpawnContext(FpsArenaLayout layout, FpsArenaContent content, FpsArenaContentIdentity identity) { Layout = layout; Content = content; Identity = identity; }
    }
    // Called while staging is inactive. Product components can attach AI/pickup/gimmick behavior.
    public interface IFpsArenaContentInitializer { void InitializeArenaContent(FpsArenaSpawnContext context); }
}
