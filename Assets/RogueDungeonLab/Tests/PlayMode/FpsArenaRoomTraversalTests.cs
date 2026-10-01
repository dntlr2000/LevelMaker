using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaRoomTraversalTests
    {
        [UnityTest]
        public IEnumerator RoomDoorsAndStairs_AllowActualTraversalAndWallsSealAlternateCrossings()
        {
            foreach(int seed in new[]{-7,71})
            {
                var r=FpsArenaRecipe.CreateFlexible();r.partitionRooms=true;r.roomsPerFloor=4;r.width=r.depth=32;r.floors=4;r.cellSize=2;r.floorHeight=6;
                r.roomDoorWidthCells=seed==-7?1:3;r.spacingCells=1;r.coverDensity=.1f;
                var l=FpsArenaPlanner.Generate(r,seed);var root=FpsArenaSceneBuilder.Build(l,null);root.SetActive(true);
                var player=new GameObject("Room traversal capsule");var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=Vector3.up*.9f;cc.stepOffset=.3f;cc.slopeLimit=50;cc.skinWidth=.04f;
                try
                {
                    Assert.IsNotEmpty(l.RoomDoors);Assert.IsNotEmpty(l.RoomWalls);Physics.SyncTransforms();
                    foreach(var door in l.RoomDoors)for(int i=0;i<door.widthCells;i++)
                    {
                        Vector3 start=l.Position(door.cell+door.Step*i);Teleport(cc,start+Vector3.up*.05f);Walk(cc,(Vector3)door.Normal,r.cellSize);
                        Assert.Less(Vector3.Distance(new Vector2(cc.transform.position.x,cc.transform.position.z),new Vector2((start+(Vector3)door.Normal*r.cellSize).x,(start+(Vector3)door.Normal*r.cellSize).z)),.15f,"Door failed "+door.id);
                    }
                    foreach(var wall in l.RoomWalls)
                    {
                        Vector3 start=l.Position(wall.cell);Teleport(cc,start+Vector3.up*.05f);Walk(cc,(Vector3)wall.Normal,r.cellSize);
                        Assert.Less(Vector3.Dot(cc.transform.position-start,(Vector3)wall.Normal),r.cellSize*.5f,"Room wall allowed crossing "+wall.id);
                    }
                    foreach(var stair in l.Stairs)for(int lane=0;lane<stair.width;lane++)
                    {
                        Teleport(cc,l.Position(stair.BottomLane(lane))+Vector3.up*.05f);Walk(cc,(Vector3)stair.Forward,(stair.length+1)*r.cellSize);
                        Assert.That(cc.transform.position.y,Is.EqualTo((stair.lowerFloor+1)*r.floorHeight).Within(.15f),"Room stair ascent failed");
                        Walk(cc,-(Vector3)stair.Forward,(stair.length+1)*r.cellSize);Assert.That(cc.transform.position.y,Is.EqualTo(stair.lowerFloor*r.floorHeight).Within(.15f));
                    }
                }
                finally{Object.Destroy(player);Object.Destroy(root);}
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator RoomNeighbors_LogicalOpenEdgesMatchPhysicalCapsuleClearance()
        {
            var r=FpsArenaRecipe.CreateFlexible();r.partitionRooms=true;r.roomsPerFloor=6;r.width=r.depth=32;r.floors=4;r.cellSize=2;r.floorHeight=6;r.spacingCells=1;r.coverDensity=.12f;
            var l=FpsArenaPlanner.Generate(r,73125);var root=FpsArenaSceneBuilder.Build(l,null);root.SetActive(true);
            try
            {
                Physics.SyncTransforms();var occupied=l.BlockedCells();foreach(var p in l.Content)foreach(var c in p.Footprint())occupied.Add(c);int passages=0,sealedRoutes=0;
                for(int f=0;f<r.floors;f++)for(int z=0;z<r.depth;z++)for(int x=0;x<r.width;x++)
                {
                    var c=new Vector3Int(x,f,z);if(!l.HasFloor(c)||occupied.Contains(c))continue;
                    foreach(var step in new[]{Vector3Int.right,new Vector3Int(0,0,1)})
                    {
                        var n=c+step;if(!l.HasFloor(n)||occupied.Contains(n))continue;Vector3 foot=l.Position(c)+Vector3.up*.06f;
                        bool hit=Physics.CapsuleCast(foot+Vector3.up*.35f,foot+Vector3.up*1.45f,.35f,(Vector3)step,out _,r.cellSize,~0,QueryTriggerInteraction.Ignore);
                        if(l.CanTraverse(c,n)){Assert.IsFalse(hit,"Open room route blocked at "+c);passages++;}else{Assert.IsTrue(hit,"Room boundary has a physical gap at "+c);sealedRoutes++;}
                    }
                }
                Assert.Greater(passages,1000);Assert.Greater(sealedRoutes,20);
            }
            finally{Object.Destroy(root);}yield return null;
        }
        private static void Teleport(CharacterController cc,Vector3 p){cc.enabled=false;cc.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
        private static void Walk(CharacterController cc,Vector3 direction,float distance)
        {
            float vertical=0;int steps=Mathf.CeilToInt(distance/.05f);
            for(int i=0;i<steps+20;i++){if(cc.isGrounded&&vertical<0)vertical=-2;vertical-=22f/60;cc.Move(direction*(i<steps?.05f:0)+Vector3.up*(vertical/60));}
        }
    }
}
