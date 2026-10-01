using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public enum FpsArenaStairDirection { Forward = 0, Right = 1, Backward = 2, Left = 3 }

    public sealed partial class FpsArenaStair
    {
        public FpsArenaStairDirection direction;
        public Vector3Int Forward { get { return direction == FpsArenaStairDirection.Right ? Vector3Int.right : direction == FpsArenaStairDirection.Backward ? new Vector3Int(0,0,-1) : direction == FpsArenaStairDirection.Left ? Vector3Int.left : new Vector3Int(0,0,1); } }
        public Vector3Int LaneStep { get { var f=Forward;return new Vector3Int(f.z,0,-f.x); } }
        public bool AcrossX { get { return ((int)direction & 1) != 0; } }
        // x/z retain their meaning as minimum footprint coordinates for every rotation.
        public int FootprintWidth { get { return AcrossX ? length : width; } }
        public int FootprintDepth { get { return AcrossX ? width : length; } }
        public int MarginX { get { return AcrossX ? 2 : 1; } }
        public int MarginZ { get { return AcrossX ? 1 : 2; } }
        public RectInt ProtectedBounds { get { return new RectInt(x-MarginX,z-MarginZ,FootprintWidth+2*MarginX,FootprintDepth+2*MarginZ); } }
        public Vector3Int Cell(int lane, int step, int floorOffset = 0)
        {
            var f=Forward;var side=LaneStep;
            int originX=x-(Mathf.Min(0,f.x)*(length-1)+Mathf.Min(0,side.x)*(width-1));
            int originZ=z-(Mathf.Min(0,f.z)*(length-1)+Mathf.Min(0,side.z)*(width-1));
            return new Vector3Int(originX,lowerFloor+floorOffset,originZ)+side*lane+f*step;
        }
        public Vector3Int BottomLane(int lane) { return Cell(lane,-1); }
        public Vector3Int TopLane(int lane) { return Cell(lane,length,1); }
    }

    [Serializable] public sealed class FpsArenaStairDirectionReport
    {
        public int lowerFloor, validDirectionsMask, candidates;
        public string reason = "";
    }
    public sealed partial class FpsArenaLayout
    {
        public readonly List<FpsArenaStairDirectionReport> StairDirectionReports = new List<FpsArenaStairDirectionReport>();
    }

    // Exact pre-direction field order for previously saved room-mode recipe hashes.
    [Serializable] internal sealed class FpsArenaRoomRecipeSnapshot
    {
        public int width, depth; public FpsArenaShape shape; public int floors;
        public float cellSize, floorHeight; public int stairsPerFloor, coverPerFloor, obstaclesPerFloor, itemsPerFloor;
        public float coverHeight, obstacleHeight; public int spacingCells;
        public FpsArenaGeneratorVersion generatorVersion;
        public float coverDensity, enemyDensity, gimmickDensity, itemDensity;
        public int maxContentPerCategoryPerFloor;
        public int coverMinWidthCells, coverMaxWidthCells, coverMinDepthCells, coverMaxDepthCells;
        public float coverMinHeight, coverMaxHeight; public FpsArenaCoverShapes coverShapes;
        public bool internalWalls; public float wallDensity;
        public int wallMinLengthCells, wallMaxLengthCells; public float wallHeight, wallThickness;
        public int wallDoorWidthCells, maxWallRunsPerFloor;
        public bool partitionRooms; public int roomsPerFloor, roomMinWidthCells, roomMinAreaCells, roomDoorWidthCells;
        public float roomWallHeight, roomWallThickness;
    }
}
