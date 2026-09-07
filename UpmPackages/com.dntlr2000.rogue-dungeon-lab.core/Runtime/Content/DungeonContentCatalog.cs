using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public enum DungeonContentPlacement
    {
        Any = 0,
        RoomOnly = 1,
        CorridorOnly = 2
    }

    // 0은 기존에 직렬화된 StageDefinition이 primitive 표현을 계속 사용하도록 고정합니다.
    public enum DungeonMissingContentPolicy
    {
        BuiltInFallback = 0,
        Error = 1,
        Skip = 2
    }

    [Serializable]
    public sealed class DungeonContentCatalogEntry
    {
        public string contentKey = string.Empty;
        public DungeonSpawnCategory category;
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Range(0f, 1f)] public float minProgression;
        [Range(0f, 1f)] public float maxProgression = 1f;
        public DungeonContentPlacement placement = DungeonContentPlacement.Any;
        public List<string> requiredRoomTags = new List<string>();
        public Vector2Int footprintCells = Vector2Int.one;
        [Min(0)] public int minimumSpacingCells;
        public bool randomizeYaw;
        public Vector2 yawDegreesRange = new Vector2(0f, 360f);
        public Vector2 uniformScaleRange = Vector2.one;
        public WeightedDropTable dropTable;
        public string gameplayId = string.Empty;
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Content Catalog", fileName = "DungeonContentCatalog")]
    public sealed class DungeonContentCatalog : ScriptableObject
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public List<DungeonContentCatalogEntry> entries = new List<DungeonContentCatalogEntry>();

        // Prefab 같은 표현 참조를 제외한 불변 생성 입력을 캡처합니다.
        public DungeonContentCatalogPlanningSnapshot CapturePlanningSnapshot()
        {
            return DungeonContentCatalogPlanningSnapshot.Capture(this);
        }

        // Inspector 순서와 표현 전용 참조에 독립적인 planning SHA-256을 계산합니다.
        public string ComputePlanningHash()
        {
            return CapturePlanningSnapshot().ComputeHash();
        }
    }

    [Serializable]
    public sealed class DungeonContentPlanningEntry
    {
        public string contentKey = string.Empty;
        public DungeonSpawnCategory category;
        public float weight = 1f;
        public float minProgression;
        public float maxProgression = 1f;
        public DungeonContentPlacement placement = DungeonContentPlacement.Any;
        public List<string> requiredRoomTags = new List<string>();
        public Vector2Int footprintCells = Vector2Int.one;
        public int minimumSpacingCells;
        public bool randomizeYaw;
        public Vector2 yawDegreesRange = new Vector2(0f, 360f);
        public Vector2 uniformScaleRange = Vector2.one;

        public DungeonContentPlanningEntry DeepClone()
        {
            return new DungeonContentPlanningEntry
            {
                contentKey = contentKey ?? string.Empty,
                category = category,
                weight = weight,
                minProgression = minProgression,
                maxProgression = maxProgression,
                placement = placement,
                requiredRoomTags = requiredRoomTags != null
                    ? new List<string>(requiredRoomTags)
                    : new List<string>(),
                footprintCells = footprintCells,
                minimumSpacingCells = minimumSpacingCells,
                randomizeYaw = randomizeYaw,
                yawDegreesRange = yawDegreesRange,
                uniformScaleRange = uniformScaleRange
            };
        }

        internal static DungeonContentPlanningEntry Capture(DungeonContentCatalogEntry source)
        {
            if (source == null) return null;
            return new DungeonContentPlanningEntry
            {
                contentKey = source.contentKey ?? string.Empty,
                category = source.category,
                weight = source.weight,
                minProgression = source.minProgression,
                maxProgression = source.maxProgression,
                placement = source.placement,
                requiredRoomTags = source.requiredRoomTags != null
                    ? new List<string>(source.requiredRoomTags)
                    : new List<string>(),
                footprintCells = source.footprintCells,
                minimumSpacingCells = source.minimumSpacingCells,
                randomizeYaw = source.randomizeYaw,
                yawDegreesRange = source.yawDegreesRange,
                uniformScaleRange = source.uniformScaleRange
            };
        }
    }

    [Serializable]
    public sealed class DungeonContentCatalogPlanningSnapshot
    {
        public int formatVersion = DungeonContentCatalog.CurrentFormatVersion;
        public List<DungeonContentPlanningEntry> entries = new List<DungeonContentPlanningEntry>();

        public string ComputeHash()
        {
            return DungeonContentCatalogHasher.Compute(this);
        }

        // 요청 생성 뒤 원본 catalog가 바뀌어도 결과가 변하지 않도록 컬렉션까지 복사합니다.
        public DungeonContentCatalogPlanningSnapshot DeepClone()
        {
            DungeonContentCatalogPlanningSnapshot clone = new DungeonContentCatalogPlanningSnapshot
            {
                formatVersion = formatVersion,
                entries = new List<DungeonContentPlanningEntry>()
            };
            if (entries == null)
            {
                clone.entries = null;
                return clone;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                clone.entries.Add(entries[i] != null ? entries[i].DeepClone() : null);
            }
            return clone;
        }

        internal static DungeonContentCatalogPlanningSnapshot Capture(DungeonContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            DungeonContentCatalogPlanningSnapshot snapshot = new DungeonContentCatalogPlanningSnapshot
            {
                formatVersion = catalog.formatVersion,
                entries = new List<DungeonContentPlanningEntry>()
            };
            if (catalog.entries == null)
            {
                snapshot.entries = null;
                return snapshot;
            }
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                snapshot.entries.Add(DungeonContentPlanningEntry.Capture(catalog.entries[i]));
            }
            return snapshot;
        }
    }
}
