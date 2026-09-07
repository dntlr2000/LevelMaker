using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonBlueprintFormat
    {
        public const int CurrentVersion = 1;
    }

    [Flags]
    public enum DungeonCellFlags
    {
        None = 0,
        Floor = 1
    }

    public enum DungeonSpawnCategory
    {
        Marker = 0,
        Gimmick = 1,
        Enemy = 2,
        Destructible = 3,
        Prop = 4
    }

    [Serializable]
    public sealed class DungeonGridRecord
    {
        public int width;
        public int depth;
        public float cellSize = 3f;
        public float wallHeight = 3.2f;
    }

    [Serializable]
    public sealed class DungeonCellRecord
    {
        public Vector2Int coordinate;
        public DungeonCellFlags flags = DungeonCellFlags.Floor;
        public string roomId = string.Empty;
        public int distanceFromEntrance;
    }

    [Serializable]
    public sealed class DungeonRoomRecord
    {
        public string roomId = string.Empty;
        public RectInt bounds;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public sealed class DungeonSpawnRecord
    {
        public string spawnId = string.Empty;
        public DungeonSpawnCategory category;
        public string contentKey = string.Empty;
        public string instanceName = string.Empty;
        public Vector2Int cell;
        public Vector3 localPosition;
        public float pitchDegrees;
        public float yawDegrees;
        public float rollDegrees;
        public Vector3 localScale = Vector3.one;
        public string roomId = string.Empty;
        public float progression;
        public List<string> tags = new List<string>();
        public int variantSeed;
    }

    [Serializable]
    public sealed class DungeonBlueprint
    {
        public int formatVersion = DungeonBlueprintFormat.CurrentVersion;
        public int generatorVersion = DungeonGeneratorVersions.Current;
        public int seed;
        public string recipeHash = string.Empty;
        public string catalogPlanningHash = string.Empty;
        public string blueprintHash = string.Empty;
        public long createdUtcTicks;
        public string authoringNote = string.Empty;
        public DungeonGridRecord grid = new DungeonGridRecord();
        public List<DungeonCellRecord> cells = new List<DungeonCellRecord>();
        public List<DungeonRoomRecord> rooms = new List<DungeonRoomRecord>();
        public Vector2Int entrance;
        public Vector2Int exit;
        public List<DungeonSpawnRecord> spawns = new List<DungeonSpawnRecord>();

        // 현재 논리 데이터에서 파생한 canonical SHA-256 값을 blueprintHash에 기록합니다.
        public void RefreshHash()
        {
            blueprintHash = DungeonBlueprintHasher.Compute(this);
        }

        // Unity JSON 직렬화를 이용해 컬렉션까지 분리된 깊은 복사본을 만듭니다.
        public DungeonBlueprint DeepClone()
        {
            return DungeonBlueprintSerialization.DeepClone(this);
        }
    }
}
