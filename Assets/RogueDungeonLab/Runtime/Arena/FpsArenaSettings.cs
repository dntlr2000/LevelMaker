using System;
using UnityEngine;

namespace RogueDungeonLab
{
    public enum FpsArenaShape { Rectangle, Ellipse, Octagon }
    public enum FpsArenaPreset { Duel, Team, Vertical }
    public enum FpsArenaGeneratorVersion { LegacyV1 = 1, FlexibleV2 = 2 }
    public enum FpsArenaCoverShape { Box, Cylinder, Corner }
    [Flags] public enum FpsArenaCoverShapes { Box = 1, Cylinder = 2, Corner = 4, All = 7 }

    [Serializable]
    public sealed class FpsArenaRecipe
    {
        [Range(20, 64)] public int width = 28;
        [Range(20, 64)] public int depth = 28;
        public FpsArenaShape shape = FpsArenaShape.Octagon;
        [Range(1, 4)] public int floors = 2;
        [Range(2, 5)] public float cellSize = 3;
        [Range(3, 6)] public float floorHeight = 4;
        [Range(1, 2)] public int stairsPerFloor = 2;
        [Range(0, 100)] public int coverPerFloor = 16;
        [Range(0, 100)] public int obstaclesPerFloor = 8;
        [Range(0, 100)] public int itemsPerFloor = 6;
        [Range(.5f, 1.5f)] public float coverHeight = 1.1f;
        [Range(1.6f, 2.5f)] public float obstacleHeight = 2.2f;
        [Range(1, 3)] public int spacingCells = 2;
        // Missing fields in V1 serialized recipes retain the legacy contract.
        public FpsArenaGeneratorVersion generatorVersion = FpsArenaGeneratorVersion.LegacyV1;
        [Range(0, 1)] public float coverDensity = .035f;
        [Range(0, 1)] public float enemyDensity = .02f;
        [Range(0, 1)] public float gimmickDensity = .01f;
        [Range(0, 1)] public float itemDensity = .015f;
        [Range(1, 1000)] public int maxContentPerCategoryPerFloor = 100;
        [Range(1, 4)] public int coverMinWidthCells = 1, coverMaxWidthCells = 3;
        [Range(1, 4)] public int coverMinDepthCells = 1, coverMaxDepthCells = 2;
        [Range(.5f, 2.5f)] public float coverMinHeight = .7f, coverMaxHeight = 1.4f;
        public FpsArenaCoverShapes coverShapes = FpsArenaCoverShapes.All;

        // Opt-in: older assets and V2 recipes keep their original layout and hash.
        public bool internalWalls = false;
        [Range(0, .35f)] public float wallDensity = .12f;
        [Range(3, 16)] public int wallMinLengthCells = 5, wallMaxLengthCells = 10;
        [Range(2, 6)] public float wallHeight = 3;
        [Range(.15f, 1)] public float wallThickness = .35f;
        [Range(1, 3)] public int wallDoorWidthCells = 2;
        [Range(1, 40)] public int maxWallRunsPerFloor = 20;

        // Separate opt-in preserves both the original arena and saved wall-run mode.
        public bool partitionRooms = false;
        [Range(1, 12)] public int roomsPerFloor = 4;
        [Range(4, 12)] public int roomMinWidthCells = 4;
        [Range(16, 256)] public int roomMinAreaCells = 32;
        [Range(1, 3)] public int roomDoorWidthCells = 2;
        [Range(2, 6)] public float roomWallHeight = 3;
        [Range(.15f, 1)] public float roomWallThickness = .35f;

        // Missing in saved recipes: preserve their original +Z stairs and exact hash.
        public bool randomizeStairDirections = false;

        public FpsArenaRecipe Normalized()
        {
            var r = (FpsArenaRecipe)MemberwiseClone();
            r.width = Mathf.Clamp(width, 20, 64); r.depth = Mathf.Clamp(depth, 20, 64);
            r.floors = Mathf.Clamp(floors, 1, 4);
            r.cellSize = FiniteClamp(cellSize, 2, 5, 3);
            r.floorHeight = FiniteClamp(floorHeight, 3, 6, 4);
            r.coverHeight = FiniteClamp(coverHeight, .5f, 1.5f, 1.1f);
            r.obstacleHeight = FiniteClamp(obstacleHeight, 1.6f, 2.5f, 2.2f);
            r.stairsPerFloor = Mathf.Clamp(stairsPerFloor, 1, 2);
            r.coverPerFloor = Mathf.Clamp(coverPerFloor, 0, 100);
            r.obstaclesPerFloor = Mathf.Clamp(obstaclesPerFloor, 0, 100);
            r.itemsPerFloor = Mathf.Clamp(itemsPerFloor, 0, 100);
            r.spacingCells = Mathf.Clamp(spacingCells, 1, 3);
            if (!Enum.IsDefined(typeof(FpsArenaShape), shape)) r.shape = FpsArenaShape.Rectangle;
            if (generatorVersion != FpsArenaGeneratorVersion.FlexibleV2) r.generatorVersion = FpsArenaGeneratorVersion.LegacyV1;
            r.coverDensity = FiniteClamp(coverDensity, 0, 1, .035f);
            r.enemyDensity = FiniteClamp(enemyDensity, 0, 1, .02f);
            r.gimmickDensity = FiniteClamp(gimmickDensity, 0, 1, .01f);
            r.itemDensity = FiniteClamp(itemDensity, 0, 1, .015f);
            r.maxContentPerCategoryPerFloor = Mathf.Clamp(maxContentPerCategoryPerFloor, 1, 1000);
            r.coverMinWidthCells = Mathf.Clamp(coverMinWidthCells, 1, 4);
            r.coverMaxWidthCells = Mathf.Clamp(coverMaxWidthCells, r.coverMinWidthCells, 4);
            r.coverMinDepthCells = Mathf.Clamp(coverMinDepthCells, 1, 4);
            r.coverMaxDepthCells = Mathf.Clamp(coverMaxDepthCells, r.coverMinDepthCells, 4);
            r.coverMinHeight = FiniteClamp(coverMinHeight, .5f, 2.5f, .7f);
            r.coverMaxHeight = FiniteClamp(coverMaxHeight, r.coverMinHeight, 2.5f, Mathf.Max(r.coverMinHeight, 1.4f));
            r.coverShapes &= FpsArenaCoverShapes.All;
            if (r.coverShapes == 0) r.coverShapes = FpsArenaCoverShapes.Box;
            r.wallDensity = FiniteClamp(wallDensity, 0, .35f, .12f);
            r.wallMinLengthCells = Mathf.Clamp(wallMinLengthCells, 3, 16);
            r.wallMaxLengthCells = Mathf.Clamp(wallMaxLengthCells, r.wallMinLengthCells, 16);
            r.wallHeight = FiniteClamp(wallHeight, 2, r.floorHeight - .25f, Mathf.Min(3, r.floorHeight - .25f));
            r.wallThickness = FiniteClamp(wallThickness, .15f, 1, .35f);
            r.wallDoorWidthCells = Mathf.Clamp(wallDoorWidthCells, 1, 3);
            r.maxWallRunsPerFloor = Mathf.Clamp(maxWallRunsPerFloor, 1, 40);
            r.roomsPerFloor = Mathf.Clamp(roomsPerFloor, 1, 12);
            r.roomMinWidthCells = Mathf.Clamp(roomMinWidthCells, 4, 12);
            r.roomMinAreaCells = Mathf.Clamp(roomMinAreaCells, 16, 256);
            r.roomDoorWidthCells = Mathf.Clamp(roomDoorWidthCells, 1, 3);
            r.roomWallHeight = FiniteClamp(roomWallHeight, 2, r.floorHeight - .25f, Mathf.Min(3, r.floorHeight - .25f));
            r.roomWallThickness = FiniteClamp(roomWallThickness, .15f, 1, .35f);
            return r;
        }
        private static float FiniteClamp(float v, float min, float max, float fallback)
        { return float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max); }
        public static FpsArenaRecipe CreateFlexible() { return new FpsArenaRecipe { generatorVersion = FpsArenaGeneratorVersion.FlexibleV2, randomizeStairDirections = true }; }
        // Upgrade is explicit and does not modify the supplied serialized recipe.
        public FpsArenaRecipe Upgraded()
        {
            var r = Normalized(); r.generatorVersion = FpsArenaGeneratorVersion.FlexibleV2;
            if (generatorVersion != FpsArenaGeneratorVersion.FlexibleV2)
            {
                r.randomizeStairDirections = true;
                float cells = Mathf.Max(1, r.width * r.depth * .7f);
                r.coverDensity = coverPerFloor / cells; r.enemyDensity = obstaclesPerFloor / cells;
                r.itemDensity = itemsPerFloor / cells;
                r.gimmickDensity = .01f; r.maxContentPerCategoryPerFloor = 100;
                r.coverMinWidthCells = r.coverMinDepthCells = 1; r.coverMaxWidthCells = 3; r.coverMaxDepthCells = 2;
                r.coverMinHeight = .7f; r.coverMaxHeight = 1.4f; r.coverShapes = FpsArenaCoverShapes.All;
            }
            return r.Normalized();
        }
        public static FpsArenaRecipe Preset(FpsArenaPreset preset)
        {
            var r = CreateFlexible();
            if (preset == FpsArenaPreset.Duel) { r.width = 20; r.depth = 24; r.floors = 1; r.shape = FpsArenaShape.Rectangle; r.coverPerFloor = 10; r.obstaclesPerFloor = 4; r.itemsPerFloor = 4; r.coverDensity = .035f; r.enemyDensity = .018f; }
            if (preset == FpsArenaPreset.Vertical) { r.width = 32; r.depth = 32; r.floors = 4; r.shape = FpsArenaShape.Ellipse; r.coverPerFloor = 18; r.obstaclesPerFloor = 10; r.itemsPerFloor = 8; }
            return r;
        }
    }

    // Field order and values exactly match V1's JsonUtility recipe hash input.
    [Serializable] internal sealed class FpsArenaLegacyRecipeSnapshot
    {
        public int width, depth; public FpsArenaShape shape; public int floors;
        public float cellSize, floorHeight; public int stairsPerFloor, coverPerFloor, obstaclesPerFloor, itemsPerFloor;
        public float coverHeight, obstacleHeight; public int spacingCells;
        public FpsArenaLegacyRecipeSnapshot(FpsArenaRecipe r)
        { width = r.width; depth = r.depth; shape = r.shape; floors = r.floors; cellSize = r.cellSize; floorHeight = r.floorHeight; stairsPerFloor = r.stairsPerFloor; coverPerFloor = r.coverPerFloor; obstaclesPerFloor = r.obstaclesPerFloor; itemsPerFloor = r.itemsPerFloor; coverHeight = r.coverHeight; obstacleHeight = r.obstacleHeight; spacingCells = r.spacingCells; }
    }

    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/FPS Arena 설정", fileName = "FpsArenaSettings")]
    public sealed class FpsArenaSettings : ScriptableObject
    {
        public int seed = 73125;
        public FpsArenaRecipe recipe = FpsArenaRecipe.CreateFlexible();
        public FpsArenaContentCatalog contentCatalog;
    }
}
