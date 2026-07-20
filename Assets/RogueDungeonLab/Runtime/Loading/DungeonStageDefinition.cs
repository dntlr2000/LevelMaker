using UnityEngine;

namespace RogueDungeonLab
{
    public enum DungeonStageSourceMode
    {
        Procedural = 0,
        SavedBlueprint = 1
    }

    public enum DungeonStageBuildMode
    {
        RuntimeBuild = 0,
        BakedPrefab = 1
    }

    public enum DungeonStageSeedPolicy
    {
        RandomPerLoad = 0,
        RunSeed = 1,
        FixedSeed = 2
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Stage Definition", fileName = "DungeonStageDefinition")]
    public sealed class DungeonStageDefinition : ScriptableObject
    {
        [Header("Source")]
        public DungeonStageSourceMode sourceMode = DungeonStageSourceMode.Procedural;
        public DungeonStageBuildMode buildMode = DungeonStageBuildMode.RuntimeBuild;
        public RogueDungeonSettings recipe;
        public DungeonBlueprintAsset savedBlueprint;

        [Header("Procedural Seed")]
        public DungeonStageSeedPolicy seedPolicy = DungeonStageSeedPolicy.FixedSeed;
        public int fixedSeed = 12345;
        [Min(1)] public int generatorVersion = DungeonGeneratorVersions.Current;

        [Header("Content")]
        public DungeonContentCatalog contentCatalog;
        public DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback;

        [Header("Lifecycle")]
        public bool loadOnPlay = true;
    }
}
