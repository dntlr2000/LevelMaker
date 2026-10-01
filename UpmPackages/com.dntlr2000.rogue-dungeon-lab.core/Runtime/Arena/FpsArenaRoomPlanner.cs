using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static partial class FpsArenaPlanner
    {
        private const ulong RoomStreamV2 = 0x4152454E41524D32UL;
        private sealed class RoomRegion { public int x0,x1,z0,z1,area; }
        private sealed class RoomCut
        { public RoomRegion source,a,b; public bool alongX; public List<Vector3Int> frontier; public List<int> doors; }
        private static void PartitionArenaRooms(FpsArenaLayout l)
        {
            var r=l.Recipe;
            // Room mode replaces the open-arena cross spine with protected routes inside rooms.
            l.Reserved=new bool[r.width,r.floors,r.depth]; l.RoomIds=new int[r.width,r.floors,r.depth];
            for(int f=0;f<r.floors;f++) for(int z=0;z<r.depth;z++) for(int x=0;x<r.width;x++) l.RoomIds[x,f,z]=-1;
            foreach(var spawn in l.Spawns) Reserve(l,spawn.y,spawn.x-1,spawn.z-1,3,3);
            foreach(var s in l.Stairs) for(int f=s.lowerFloor;f<=s.lowerFloor+1;f++) { var b=s.ProtectedBounds;Reserve(l,f,b.x,b.y,b.width,b.height); }
            var spawnProtected=new bool[r.width,r.floors,r.depth];
            foreach(var spawn in l.Spawns) for(int z=spawn.z-1;z<=spawn.z+1;z++) for(int x=spawn.x-1;x<=spawn.x+1;x++) spawnProtected[x,spawn.y,z]=true;
            var doorProtected=new HashSet<Vector3Int>();
            for(int floor=0;floor<r.floors;floor++)
            {
                var initial=new RoomRegion { x0=0,x1=r.width,z0=0,z1=r.depth,area=RegionArea(l,floor,0,r.width,0,r.depth) };
                var regions=new List<RoomRegion> { initial }; var rng=DungeonStableRandomStreams.Create(l.Seed,RoomStreamV2+(ulong)floor);
                int cuts=0;
                while(regions.Count<r.roomsPerFloor)
                {
                    var best=new List<RoomCut>(); int largest=-1;
                    foreach(var region in regions)
                    {
                        if(region.area<largest) continue;
                        var options=FindRoomCuts(l,floor,region,spawnProtected,doorProtected);
                        if(options.Count==0) continue;
                        if(region.area>largest) { best.Clear(); largest=region.area; }
                        best.AddRange(options);
                    }
                    if(best.Count==0) break;
                    var cut=best[rng.NextInt(0,best.Count)]; int doorStart=cut.doors[rng.NextInt(0,cut.doors.Count)];
                    string id="arena:v2:partition:"+floor+":"+cuts++;
                    var door=new FpsArenaRoomDoor { id=id+":door",cell=cut.frontier[doorStart],alongX=cut.alongX,widthCells=r.roomDoorWidthCells };
                    l.RoomDoors.Add(door);
                    AddPartitionSpan(l,id+":a",cut.frontier[0],cut.alongX,doorStart);
                    int after=doorStart+r.roomDoorWidthCells;
                    AddPartitionSpan(l,id+":b",cut.frontier[after],cut.alongX,cut.frontier.Count-after);
                    for(int i=0;i<cut.frontier.Count;i++)
                    {
                        var a=cut.frontier[i]; var b=a+door.Normal; var edge=new FpsArenaRoomEdge(a,b);
                        l.ClosedRoomEdges.Add(edge); if(i<doorStart||i>=after) l.SolidRoomEdges.Add(edge);
                        // Keep content bounds away from both sides of every shared wall.
                        l.Reserved[a.x,a.y,a.z]=true; l.Reserved[b.x,b.y,b.z]=true;
                    }
                    for(int i=0;i<door.widthCells;i++) for(int side=-1;side<=2;side++)
                    {
                        var c=door.cell+door.Step*i+door.Normal*side;
                        if(l.HasFloor(c)) { l.Reserved[c.x,c.y,c.z]=true; doorProtected.Add(c); }
                    }
                    regions.Remove(cut.source); regions.Add(cut.a); regions.Add(cut.b);
                }
                regions.Sort((a,b)=>a.z0!=b.z0?a.z0.CompareTo(b.z0):a.x0.CompareTo(b.x0));
                foreach(var region in regions)
                {
                    int index=l.Rooms.Count; var anchor=FindRoomAnchor(l,floor,region);
                    var room=new FpsArenaRoom { id="arena:v2:room:"+floor+":"+index,index=index,floor=floor,min=new Vector2Int(region.x0,region.z0),max=new Vector2Int(region.x1,region.z1),cellCount=region.area,anchor=anchor };
                    l.Rooms.Add(room);
                    for(int z=region.z0;z<region.z1;z++) for(int x=region.x0;x<region.x1;x++) if(l.HasFloor(new Vector3Int(x,floor,z))) l.RoomIds[x,floor,z]=index;
                }
                l.RoomReports.Add(new FpsArenaRoomReport { floor=floor,requested=r.roomsPerFloor,actual=regions.Count,reason=regions.Count<r.roomsPerFloor?"최소 방 폭·면적과 계단·스폰·기존 출입구 보호 조건을 만족하는 추가 구획선을 찾지 못했습니다.":"" });
            }
            foreach(var door in l.RoomDoors)
            {
                door.roomA=l.RoomAt(door.cell); door.roomB=l.RoomAt(door.cell+door.Normal);
                l.RoomConnections.Add(new FpsArenaRoomConnection { roomA=door.roomA,roomB=door.roomB,doorId=door.id });
                for(int i=0;i<door.widthCells;i++) { ReserveRoomRoute(l,door.cell+door.Step*i); ReserveRoomRoute(l,door.cell+door.Step*i+door.Normal); }
            }
            foreach(var spawn in l.Spawns) ReserveRoomRoute(l,spawn);
            foreach(var s in l.Stairs) for(int lane=0;lane<s.width;lane++) { ReserveRoomRoute(l,s.BottomLane(lane)); ReserveRoomRoute(l,s.TopLane(lane)); }
            foreach(var room in l.Rooms) ReserveRoomRoute(l,room.anchor);
        }
        private static void AddPartitionSpan(FpsArenaLayout l,string id,Vector3Int start,bool alongX,int length)
        { l.RoomWalls.Add(new FpsArenaRoomWall { id=id,cell=start,alongX=alongX,lengthCells=length,height=l.Recipe.roomWallHeight,thickness=l.Recipe.roomWallThickness }); }
        private static List<RoomCut> FindRoomCuts(FpsArenaLayout l,int floor,RoomRegion region,bool[,,] spawnProtected,HashSet<Vector3Int> doorProtected)
        {
            var options=new List<RoomCut>(); int min=l.Recipe.roomMinWidthCells;
            for(int axis=0;axis<2;axis++)
            {
                int low=axis==0?region.x0:region.z0,high=axis==0?region.x1:region.z1;
                for(int at=low+min;at<=high-min;at++)
                {
                    var a=new RoomRegion { x0=region.x0,x1=axis==0?at:region.x1,z0=region.z0,z1=axis==1?at:region.z1 };
                    var b=new RoomRegion { x0=axis==0?at:region.x0,x1=region.x1,z0=axis==1?at:region.z0,z1=region.z1 };
                    a.area=RegionArea(l,floor,a.x0,a.x1,a.z0,a.z1); b.area=region.area-a.area;
                    if(a.area<l.Recipe.roomMinAreaCells||b.area<l.Recipe.roomMinAreaCells||!RoomHasMinimumSpan(l,floor,a)||!RoomHasMinimumSpan(l,floor,b)) continue;
                    var frontier=new List<Vector3Int>(); var normal=axis==0?Vector3Int.right:new Vector3Int(0,0,1); var step=axis==0?new Vector3Int(0,0,1):Vector3Int.right;
                    for(int i=axis==0?region.z0:region.x0;i<(axis==0?region.z1:region.x1);i++)
                    {
                        var c=axis==0?new Vector3Int(at-1,floor,i):new Vector3Int(i,floor,at-1);
                        if(l.HasFloor(c)&&l.HasFloor(c+normal)) frontier.Add(c);
                    }
                    if(frontier.Count<l.Recipe.roomDoorWidthCells+2) continue;
                    bool safe=true;
                    for(int i=0;i<frontier.Count;i++)
                    {
                        var c=frontier[i];
                        if((i>0&&c!=frontier[i-1]+step)||doorProtected.Contains(c)||doorProtected.Contains(c+normal)||NearRoomStair(l,c)||NearRoomStair(l,c+normal)) { safe=false; break; }
                    }
                    if(!safe) continue;
                    var doors=new List<int>();
                    for(int start=1;start<=frontier.Count-l.Recipe.roomDoorWidthCells-1;start++)
                    {
                        bool protects=true;
                        for(int i=0;i<frontier.Count;i++)
                        {
                            if(i>=start&&i<start+l.Recipe.roomDoorWidthCells) continue;
                            var c=frontier[i]; var d=c+normal;
                            if(spawnProtected[c.x,c.y,c.z]||spawnProtected[d.x,d.y,d.z]) { protects=false; break; }
                        }
                        if(protects) doors.Add(start);
                    }
                    if(doors.Count==0||!ConnectedRoomRegion(l,floor,a)||!ConnectedRoomRegion(l,floor,b)) continue;
                    options.Add(new RoomCut { source=region,a=a,b=b,alongX=axis==1,frontier=frontier,doors=doors });
                }
            }
            return options;
        }
        private static bool NearRoomStair(FpsArenaLayout l,Vector3Int c)
        {
            foreach(var s in l.Stairs) if((c.y==s.lowerFloor||c.y==s.lowerFloor+1)&&s.ProtectedBounds.Contains(new Vector2Int(c.x,c.z))) return true;
            return false;
        }
        private static int RegionArea(FpsArenaLayout l,int floor,int x0,int x1,int z0,int z1)
        { int n=0; for(int z=z0;z<z1;z++) for(int x=x0;x<x1;x++) if(l.Floors[x,floor,z]) n++; return n; }
        private static bool RoomHasMinimumSpan(FpsArenaLayout l,int floor,RoomRegion region)
        {
            int minX=region.x1,maxX=region.x0-1,minZ=region.z1,maxZ=region.z0-1;
            for(int z=region.z0;z<region.z1;z++)for(int x=region.x0;x<region.x1;x++)if(l.Floors[x,floor,z])
            { minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z); }
            return maxX-minX+1>=l.Recipe.roomMinWidthCells&&maxZ-minZ+1>=l.Recipe.roomMinWidthCells;
        }
        private static bool ConnectedRoomRegion(FpsArenaLayout l,int floor,RoomRegion region)
        {
            var seen=new bool[l.Recipe.width,l.Recipe.depth]; var queue=new Queue<Vector3Int>(); int count=0;
            for(int z=region.z0;z<region.z1&&queue.Count==0;z++) for(int x=region.x0;x<region.x1;x++) if(l.Floors[x,floor,z]) { queue.Enqueue(new Vector3Int(x,floor,z));seen[x,z]=true;break; }
            while(queue.Count>0)
            {
                var c=queue.Dequeue();count++;
                foreach(var offset in new[] { Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1) })
                { var n=c+offset;if(n.x>=region.x0&&n.x<region.x1&&n.z>=region.z0&&n.z<region.z1&&!seen[n.x,n.z]&&l.Floors[n.x,floor,n.z]) { seen[n.x,n.z]=true;queue.Enqueue(n); } }
            }
            return count==region.area;
        }
        private static Vector3Int FindRoomAnchor(FpsArenaLayout l,int floor,RoomRegion region)
        {
            Vector3Int best=default; int distance=int.MaxValue;
            for(int z=region.z0;z<region.z1;z++) for(int x=region.x0;x<region.x1;x++) if(l.Floors[x,floor,z])
            { int d=Mathf.Abs(2*x-region.x0-region.x1+1)+Mathf.Abs(2*z-region.z0-region.z1+1);if(d<distance) { distance=d;best=new Vector3Int(x,floor,z); } }
            return best;
        }
        private static void ReserveRoomRoute(FpsArenaLayout l,Vector3Int start)
        {
            int room=l.RoomAt(start); if(room<0) throw new InvalidOperationException("Room route starts outside a floor region.");
            var target=l.Rooms[room].anchor; var previous=new Dictionary<Vector3Int,Vector3Int>();var queue=new Queue<Vector3Int>();previous.Add(start,start);queue.Enqueue(start);
            while(queue.Count>0&&!previous.ContainsKey(target))
            {
                var c=queue.Dequeue();foreach(var offset in new[] { Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1) })
                { var next=c+offset;if(l.RoomAt(next)==room&&l.CanTraverse(c,next)&&!previous.ContainsKey(next)) { previous.Add(next,c);queue.Enqueue(next); } }
            }
            if(!previous.ContainsKey(target)) throw new InvalidOperationException("Room doorway/stair has no route to its room anchor.");
            for(var c=target;;c=previous[c])
            {
                for(int z=-1;z<=1;z++) for(int x=-1;x<=1;x++) { var n=c+new Vector3Int(x,0,z);if(l.RoomAt(n)==room) l.Reserved[n.x,n.y,n.z]=true; }
                if(c==start) break;
            }
        }
    }
}
