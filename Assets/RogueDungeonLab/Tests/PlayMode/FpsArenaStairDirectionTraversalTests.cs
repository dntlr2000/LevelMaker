using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaStairDirectionTraversalTests
    {
        [UnityTest]
        public IEnumerator ActualCardinalStairs_AllFloorsBothLanesClimbDescendAndClearCeilings()
        {
            var seen=new HashSet<FpsArenaStairDirection>();int flights=0;
            foreach(FpsArenaShape shape in new[]{FpsArenaShape.Rectangle,FpsArenaShape.Ellipse,FpsArenaShape.Octagon})
            foreach(int seed in new[]{0,1,71,-173})foreach(bool steep in new[]{false,true})
            {
                var r=FpsArenaRecipe.CreateFlexible();r.width=r.depth=32;r.floors=4;r.partitionRooms=true;r.roomsPerFloor=4;r.shape=shape;r.cellSize=steep?2:5;r.floorHeight=steep?6:3;
                r.coverDensity=r.enemyDensity=r.itemDensity=r.gimmickDensity=.3f;r.maxContentPerCategoryPerFloor=5;r.spacingCells=1;
                var l=FpsArenaPlanner.Generate(r,seed);var root=FpsArenaSceneBuilder.Build(l,null);root.SetActive(true);
                var player=new GameObject("Cardinal stair traversal capsule");var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=Vector3.up*.9f;cc.stepOffset=.3f;cc.slopeLimit=50;cc.skinWidth=.04f;
                try
                {
                    Physics.SyncTransforms();foreach(var s in l.Stairs)
                    {
                        flights++;seen.Add(s.direction);
                        for(int lane=0;lane<s.width;lane++)
                        {
                            var bottom=l.Position(s.BottomLane(lane));var top=l.Position(s.TopLane(lane));Teleport(cc,bottom+Vector3.up*.05f);
                            Walk(cc,(Vector3)s.Forward,(s.length+1)*r.cellSize);AssertPose(cc.transform.position,top,"Ascent "+shape+"/"+seed+"/"+s.lowerFloor+"/"+s.direction+"/"+lane);
                            Walk(cc,-(Vector3)s.Forward,(s.length+1)*r.cellSize);AssertPose(cc.transform.position,bottom,"Descent "+s.direction);
                        }
                    }
                }
                finally{Object.Destroy(player);Object.Destroy(root);}yield return null;
            }
            Assert.AreEqual(144,flights);Assert.AreEqual(4,seen.Count);
        }
        private static void AssertPose(Vector3 actual,Vector3 expected,string message)
        { Assert.That(actual.y,Is.EqualTo(expected.y).Within(.15f),message);Assert.Less(Vector2.Distance(new Vector2(actual.x,actual.z),new Vector2(expected.x,expected.z)),.2f,message); }
        private static void Teleport(CharacterController cc,Vector3 p){cc.enabled=false;cc.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
        private static void Walk(CharacterController cc,Vector3 direction,float distance)
        { float vertical=0;int steps=Mathf.CeilToInt(distance/.05f);for(int i=0;i<steps+20;i++){if(cc.isGrounded&&vertical<0)vertical=-2;vertical-=22f/60;cc.Move(direction*(i<steps?.05f:0)+Vector3.up*(vertical/60));} }
    }
}
