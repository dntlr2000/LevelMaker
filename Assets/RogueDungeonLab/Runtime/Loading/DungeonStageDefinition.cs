using System;
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
        [Header("Identity")]
        public string stageId = string.Empty;

        [Header("Source")]
        public DungeonStageSourceMode sourceMode = DungeonStageSourceMode.Procedural;
        public DungeonStageBuildMode buildMode = DungeonStageBuildMode.RuntimeBuild;
        public RogueDungeonSettings recipe;
        public DungeonBlueprintAsset savedBlueprint;
        public DungeonStageOverrides stageOverrides;

        [Header("Procedural Seed")]
        public DungeonStageSeedPolicy seedPolicy = DungeonStageSeedPolicy.FixedSeed;
        public int fixedSeed = 12345;
        [Min(1)] public int generatorVersion = DungeonGeneratorVersions.Current;

        [Header("Content")]
        public DungeonContentCatalog contentCatalog;
        public DungeonMissingContentPolicy missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback;

        [Header("Bake 결과 (R6)")]
        public GameObject bakedPrefab;
        public DungeonBakeManifest bakeManifest;

        [Header("Lifecycle")]
        public bool loadOnPlay = true;

        // 새 제작 자산에 런 상태 저장용 영구 stage ID를 한 번만 부여합니다.
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stageId))
                stageId = Guid.NewGuid().ToString("N");
            else
                stageId = stageId.Trim();
        }
    }
}
