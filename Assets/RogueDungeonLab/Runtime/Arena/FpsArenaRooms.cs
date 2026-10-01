using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    [Serializable] public sealed class FpsArenaRoom
    {
        public string id;
        public int index, floor, cellCount;
        public Vector2Int min, max; // half-open clipping rectangle inside the arena footprint
        public Vector3Int anchor;
    }
    [Serializable] public sealed class FpsArenaRoomWall
    {
        public string id;
        public Vector3Int cell; // cell on the negative side of a shared grid boundary
        public bool alongX;
        public int lengthCells;
        public float height, thickness;
        public Vector3Int Step { get { return alongX ? Vector3Int.right : new Vector3Int(0,0,1); } }
        public Vector3Int Normal { get { return alongX ? new Vector3Int(0,0,1) : Vector3Int.right; } }
        public Vector3 Position(FpsArenaLayout l)
        { return l.Position(cell) + (Vector3)Normal * (l.Recipe.cellSize * .5f) + (Vector3)Step * ((lengthCells - 1) * l.Recipe.cellSize * .5f); }
    }
    [Serializable] public sealed class FpsArenaRoomDoor
    {
        public string id;
        public Vector3Int cell;
        public bool alongX;
        public int widthCells, roomA, roomB;
        public Vector3Int Step { get { return alongX ? Vector3Int.right : new Vector3Int(0,0,1); } }
        public Vector3Int Normal { get { return alongX ? new Vector3Int(0,0,1) : Vector3Int.right; } }
        public Vector3 Position(FpsArenaLayout l)
        { return l.Position(cell) + (Vector3)Normal * (l.Recipe.cellSize * .5f) + (Vector3)Step * ((widthCells - 1) * l.Recipe.cellSize * .5f); }
    }
    [Serializable] public sealed class FpsArenaRoomConnection
    { public int roomA, roomB; public string doorId; }
    [Serializable] public sealed class FpsArenaRoomReport
    { public int floor, requested, actual; public string reason = ""; }

    internal struct FpsArenaRoomEdge : IEquatable<FpsArenaRoomEdge>
    {
        public Vector3Int cell; public bool acrossX;
        public FpsArenaRoomEdge(Vector3Int a, Vector3Int b)
        { acrossX = a.x != b.x; cell = new Vector3Int(Mathf.Min(a.x,b.x),a.y,Mathf.Min(a.z,b.z)); }
        public bool Equals(FpsArenaRoomEdge other) { return cell == other.cell && acrossX == other.acrossX; }
        public override bool Equals(object obj) { return obj is FpsArenaRoomEdge other && Equals(other); }
        public override int GetHashCode() { return cell.GetHashCode() ^ (acrossX ? 3989 : 127); }
    }

    public sealed partial class FpsArenaLayout
    {
        public readonly List<FpsArenaRoom> Rooms = new List<FpsArenaRoom>();
        public readonly List<FpsArenaRoomWall> RoomWalls = new List<FpsArenaRoomWall>();
        public readonly List<FpsArenaRoomDoor> RoomDoors = new List<FpsArenaRoomDoor>();
        public readonly List<FpsArenaRoomConnection> RoomConnections = new List<FpsArenaRoomConnection>();
        public readonly List<FpsArenaRoomReport> RoomReports = new List<FpsArenaRoomReport>();
        internal int[,,] RoomIds;
        internal readonly HashSet<FpsArenaRoomEdge> SolidRoomEdges = new HashSet<FpsArenaRoomEdge>();
        internal readonly HashSet<FpsArenaRoomEdge> ClosedRoomEdges = new HashSet<FpsArenaRoomEdge>();
        public int RoomAt(Vector3Int cell) { return RoomIds != null && HasFloor(cell) ? RoomIds[cell.x,cell.y,cell.z] : -1; }
        public bool CanTraverse(Vector3Int from, Vector3Int to, bool closeRoomDoors = false)
        {
            if (from.y != to.y)
            {
                foreach (var stair in Stairs) for (int lane = 0; lane < stair.width; lane++)
                { var a = stair.BottomLane(lane); var b = stair.TopLane(lane); if ((from == a && to == b) || (from == b && to == a)) return true; }
                return false;
            }
            if (Mathf.Abs(from.x - to.x) + Mathf.Abs(from.z - to.z) != 1) return false;
            return !(closeRoomDoors ? ClosedRoomEdges : SolidRoomEdges).Contains(new FpsArenaRoomEdge(from,to));
        }
        // Independent flood criterion: sealing all door openings must produce exactly N rooms.
        public int CountClosedRooms(int floor)
        {
            int count = 0; var seen = new HashSet<Vector3Int>(); var queue = new Queue<Vector3Int>();
            for (int z = 0; z < Recipe.depth; z++) for (int x = 0; x < Recipe.width; x++)
            {
                var start = new Vector3Int(x,floor,z); if (!HasFloor(start) || !seen.Add(start)) continue;
                count++; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var offset in new[] { Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1) })
                    { var next = c + offset; if (HasFloor(next) && CanTraverse(c,next,true) && seen.Add(next)) queue.Enqueue(next); }
                }
            }
            return count;
        }
        internal void ValidateRooms()
        {
            if (!Recipe.partitionRooms || GeneratorVersion != 2)
            { if (Rooms.Count != 0 || RoomWalls.Count != 0 || RoomDoors.Count != 0) throw new InvalidOperationException("Room geometry exists outside partition mode."); return; }
            if (RoomReports.Count != Recipe.floors || RoomIds == null || Walls.Count != 0) throw new InvalidOperationException("Room partition metadata is missing or mixed with wall-run mode.");
            foreach (var report in RoomReports)
            {
                int n = 0; foreach(var room in Rooms) if(room.floor == report.floor) n++;
                if (n != report.actual || n != CountClosedRooms(report.floor) || n > report.requested || n < 1 || (n < report.requested && string.IsNullOrEmpty(report.reason))) throw new InvalidOperationException("Arena room count/report does not match closed-door floor regions.");
            }
            foreach (var room in Rooms)
            {
                if (room.index < 0 || room.index >= Rooms.Count || RoomAt(room.anchor) != room.index) throw new InvalidOperationException("Arena room identity/anchor is invalid.");
                int n = 0; for(int z=room.min.y;z<room.max.y;z++) for(int x=room.min.x;x<room.max.x;x++) if(HasFloor(new Vector3Int(x,room.floor,z))) { n++; if(RoomAt(new Vector3Int(x,room.floor,z)) != room.index) throw new InvalidOperationException("Arena room regions overlap."); }
                if(n != room.cellCount) throw new InvalidOperationException("Arena room cell count is invalid.");
            }
            foreach (var door in RoomDoors)
            {
                if (door.widthCells != Recipe.roomDoorWidthCells || door.roomA == door.roomB || door.roomA < 0 || door.roomB < 0) throw new InvalidOperationException("Arena doorway does not connect distinct rooms.");
                for(int i=0;i<door.widthCells;i++)
                { var a=door.cell+door.Step*i; var b=a+door.Normal; if(RoomAt(a)!=door.roomA || RoomAt(b)!=door.roomB || !Reserved[a.x,a.y,a.z] || !Reserved[b.x,b.y,b.z] || !CanTraverse(a,b) || CanTraverse(a,b,true)) throw new InvalidOperationException("Arena doorway is blocked or splits its room connection."); }
            }
            foreach(var wall in RoomWalls)
            {
                if(wall.lengthCells<1 || wall.height<2 || wall.height>Recipe.floorHeight-.25f || wall.thickness<.15f || wall.thickness>1 || float.IsNaN(wall.height) || float.IsNaN(wall.thickness)) throw new InvalidOperationException("Arena room wall bounds are invalid.");
                for(int i=0;i<wall.lengthCells;i++) { var a=wall.cell+wall.Step*i; var b=a+wall.Normal; if(!HasFloor(a)||!HasFloor(b)||RoomAt(a)==RoomAt(b)||CanTraverse(a,b)) throw new InvalidOperationException("Arena partition wall is not a closed room boundary."); }
            }
            foreach(var p in Content) foreach(var c in p.Footprint()) if(RoomAt(c)!=RoomAt(p.cell)) throw new InvalidOperationException("Arena content crosses room boundaries.");
        }
    }
}
