using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonStageOverrideFormat
    {
        public const int CurrentVersion = 1;
    }

    [Serializable]
    public sealed class DungeonSpawnBindingSnapshot
    {
        public string spawnId = string.Empty;
        public DungeonSpawnCategory category;
        public string contentKey = string.Empty;
        public Vector2Int cell;
        public string roomId = string.Empty;
        public int variantSeed;

        // 원본 spawn의 재결합에 필요한 stable ID와 의미 anchor를 새 스냅샷으로 캡처합니다.
        public static DungeonSpawnBindingSnapshot Capture(DungeonSpawnRecord spawn)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            return new DungeonSpawnBindingSnapshot
            {
                spawnId = spawn.spawnId ?? string.Empty,
                category = spawn.category,
                contentKey = spawn.contentKey ?? string.Empty,
                cell = spawn.cell,
                roomId = spawn.roomId ?? string.Empty,
                variantSeed = spawn.variantSeed
            };
        }

        // 현재 Blueprint spawn이 저장된 의미 anchor와 정확히 일치하는지 확인합니다.
        public bool Matches(DungeonSpawnRecord spawn)
        {
            return spawn != null &&
                   category == spawn.category &&
                   string.Equals(
                       contentKey ?? string.Empty,
                       spawn.contentKey ?? string.Empty,
                       StringComparison.Ordinal) &&
                   cell == spawn.cell &&
                   string.Equals(
                       roomId ?? string.Empty,
                       spawn.roomId ?? string.Empty,
                       StringComparison.Ordinal) &&
                   variantSeed == spawn.variantSeed;
        }
    }

    [Serializable]
    public sealed class DungeonSpawnDisableOverride
    {
        public string recordId = string.Empty;
        public DungeonSpawnBindingSnapshot binding =
            new DungeonSpawnBindingSnapshot();
    }

    [Serializable]
    public sealed class DungeonSpawnContentOverride
    {
        public string recordId = string.Empty;
        public DungeonSpawnBindingSnapshot binding =
            new DungeonSpawnBindingSnapshot();
        public string replacementContentKey = string.Empty;
    }

    [Serializable]
    public sealed class DungeonSpawnTransformOverride
    {
        public string recordId = string.Empty;
        public DungeonSpawnBindingSnapshot binding =
            new DungeonSpawnBindingSnapshot();
        public Vector3 localPosition;
        public float pitchDegrees;
        public float yawDegrees;
        public float rollDegrees;
        public Vector3 localScale = Vector3.one;
    }

    [CreateAssetMenu(
        menuName = "Rogue Dungeon Lab/Stage Overrides",
        fileName = "DungeonStageOverrides")]
    public sealed class DungeonStageOverrides : ScriptableObject
    {
        [Header("버전과 기준 원본")]
        [Min(1)] public int formatVersion =
            DungeonStageOverrideFormat.CurrentVersion;
        public DungeonBlueprintAsset baseBlueprint;
        public string baseBlueprintHash = string.Empty;

        [Header("비파괴 Spawn 변경")]
        public List<DungeonSpawnDisableOverride> disabledSpawns =
            new List<DungeonSpawnDisableOverride>();
        public List<DungeonSpawnRecord> addedSpawns =
            new List<DungeonSpawnRecord>();
        public List<DungeonSpawnContentOverride> contentOverrides =
            new List<DungeonSpawnContentOverride>();
        public List<DungeonSpawnTransformOverride> transformOverrides =
            new List<DungeonSpawnTransformOverride>();

        [Header("제작 메타데이터")]
        [TextArea] public string authoringNote = string.Empty;
        public string overrideHash = string.Empty;

        // 현재 비파괴 변경 집합에서 canonical SHA-256 값을 계산해 저장합니다.
        public void RefreshHash()
        {
            overrideHash = DungeonStageOverridesHasher.Compute(this);
        }
    }
}
