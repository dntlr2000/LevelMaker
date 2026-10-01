using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaStairDirectionTests
    {
        [Serializable] private sealed class BaselineCase { public string recipeJson;public int seed;public string hash; }
        [Serializable] private sealed class Baselines { public List<BaselineCase> cases; }
        private static FpsArenaRecipe Recipe()
        { var r=FpsArenaRecipe.CreateFlexible();r.width=r.depth=32;r.floors=4;r.partitionRooms=true;r.spacingCells=1;return r; }
        [Test]
        public void DirectionOff_Retains78PreChangeLegacyWallAndRoomHashes()
        {
            int n=0;
            foreach(var fixture in new[]{"FpsArenaStairDirectionsLegacyBaseline.json","FpsArenaRoomsOffBaseline.json","FpsArenaWallsOffBaseline.json"})
            foreach(var c in JsonUtility.FromJson<Baselines>(File.ReadAllText(Application.dataPath+"/RogueDungeonLab/Tests/EditMode/"+fixture)).cases)
            {
                var r=JsonUtility.FromJson<FpsArenaRecipe>(c.recipeJson);Assert.IsFalse(r.randomizeStairDirections);
                var l=FpsArenaPlanner.Generate(r,c.seed);Assert.AreEqual(c.hash,l.Hash,fixture+" seed "+c.seed);
                Assert.IsTrue(l.Stairs.All(s=>s.direction==FpsArenaStairDirection.Forward));n++;
            }
            Assert.AreEqual(78,n);Assert.IsTrue(FpsArenaRecipe.CreateFlexible().randomizeStairDirections);
        }
        [Test]
        public void SeededDirections_RepeatAndVaryOnEveryFloorWithoutMutatingRoomSettings()
        {
            var r=Recipe();r.coverDensity=r.enemyDensity=r.itemDensity=r.gimmickDensity=0;string input=JsonUtility.ToJson(r);
            var seen=new[]{new HashSet<FpsArenaStairDirection>(),new HashSet<FpsArenaStairDirection>(),new HashSet<FpsArenaStairDirection>()};
            var signatures=new HashSet<string>();
            for(int seed=0;seed<24;seed++)
            {
                var a=FpsArenaPlanner.Generate(r,seed);var b=FpsArenaPlanner.Generate(r,seed);Assert.AreEqual(a.Hash,b.Hash);
                signatures.Add(string.Join(";",a.Stairs.Select(JsonUtility.ToJson)));
                foreach(var s in a.Stairs)seen[s.lowerFloor].Add(s.direction);
                foreach(var report in a.StairDirectionReports){Assert.Greater(report.candidates,0);Assert.Greater(report.validDirectionsMask,0);if(report.validDirectionsMask!=15)Assert.IsNotEmpty(report.reason);}
            }
            foreach(var directions in seen)Assert.AreEqual(4,directions.Count);
            Assert.Greater(signatures.Count,20);Assert.AreEqual(input,JsonUtility.ToJson(r));
            var before=FpsArenaPlanner.Generate(r,71);r.coverDensity=r.enemyDensity=r.itemDensity=r.gimmickDensity=.5f;
            CollectionAssert.AreEqual(before.Stairs.Select(JsonUtility.ToJson),FpsArenaPlanner.Generate(r,71).Stairs.Select(JsonUtility.ToJson));
        }
        [Test]
        public void ShapesDimensionsFloorsCountsAndModes_288ProfilesHaveProtectedRotatedLandings()
        {
            int profiles=0;
            foreach(FpsArenaShape shape in Enum.GetValues(typeof(FpsArenaShape)))
            foreach(var size in new[]{new Vector2Int(20,20),new Vector2Int(21,64),new Vector2Int(64,20),new Vector2Int(32,32)})
            foreach(int floors in new[]{2,4})foreach(int stairs in new[]{1,2})foreach(int mode in new[]{0,1,2})foreach(int seed in new[]{-173,71})
            {
                var r=Recipe();r.shape=shape;r.width=size.x;r.depth=size.y;r.floors=floors;r.stairsPerFloor=stairs;r.cellSize=2;r.floorHeight=6;
                r.partitionRooms=mode==2;r.internalWalls=mode==1;r.roomsPerFloor=4;r.coverDensity=r.enemyDensity=r.gimmickDensity=r.itemDensity=1;r.maxContentPerCategoryPerFloor=5;
                var l=FpsArenaPlanner.Generate(r,seed);l.Validate();Assert.AreEqual((floors-1)*stairs,l.Stairs.Count);profiles++;
                foreach(var s in l.Stairs)for(int lane=0;lane<s.width;lane++)
                {
                    foreach(var c in new[]{s.BottomLane(lane),s.TopLane(lane)})
                    {Assert.IsTrue(l.HasFloor(c));Assert.IsTrue(l.Reserved[c.x,c.y,c.z]);}
                    Assert.AreEqual(s.LaneStep,s.BottomLane(lane+1)-s.BottomLane(lane));
                    for(int step=0;step<s.length;step++)for(int f=0;f<=1;f++)Assert.IsFalse(l.HasFloor(s.Cell(lane,step,f)));
                }
                var occupied=l.BlockedCells();foreach(var p in l.Content)foreach(var c in p.Footprint())occupied.Add(c);
                Assert.AreEqual(l.FloorCount()-occupied.Count,l.ReachableCount(occupied),shape+"/"+size+"/"+floors+"/"+mode+"/"+seed);
            }
            Assert.AreEqual(288,profiles);
        }
        [Test]
        public void RealGeometry_RotatesEveryTreadAndGuardAndLeavesBothLandingsClear()
        {
            var seen=new HashSet<FpsArenaStairDirection>();
            for(int seed=0;seed<4;seed++)
            {
                var r=Recipe();r.cellSize=2;r.floorHeight=6;var l=FpsArenaPlanner.Generate(r,seed);var root=FpsArenaSceneBuilder.Build(l,null);root.SetActive(true);
                try
                {
                    Physics.SyncTransforms();foreach(var s in l.Stairs)
                    {
                        seen.Add(s.direction);var group=root.transform.Find("계단_"+s.lowerFloor+"_"+s.x+"_"+s.z);
                        Assert.Less(Vector3.Distance(group.forward,(Vector3)s.Forward),.001f);
                        Assert.Less(Vector3.Distance(group.right,(Vector3)s.LaneStep),.001f);
                        Assert.Less(Vector3.Distance(group.localPosition,l.Position(s.Cell(0,0))),.001f);
                        Assert.AreEqual(3,group.GetComponentsInChildren<BoxCollider>().Count(c=>c.name.Contains("가드")));
                        for(int lane=0;lane<s.width;lane++)foreach(var c in new[]{s.BottomLane(lane),s.TopLane(lane)})
                        {
                            var foot=l.Position(c)+Vector3.up*.06f;
                            Assert.IsFalse(Physics.CheckCapsule(foot+Vector3.up*.35f,foot+Vector3.up*1.45f,.35f,~0,QueryTriggerInteraction.Ignore),"Landing capsule blocked "+s.direction+" "+c);
                        }
                        var center=l.Position(new Vector3Int(s.x,s.lowerFloor,s.z))+new Vector3((s.FootprintWidth-1)*r.cellSize*.5f,r.floorHeight*.5f,(s.FootprintDepth-1)*r.cellSize*.5f);
                        var flight=new Bounds(center,new Vector3(s.FootprintWidth*r.cellSize-.02f,r.floorHeight-.02f,s.FootprintDepth*r.cellSize-.02f));
                        foreach(var collider in root.GetComponentsInChildren<Collider>())if(!collider.transform.IsChildOf(group))Assert.IsFalse(flight.Intersects(collider.bounds),"Foreign collider enters flight: "+collider.name);
                    }
                }
                finally{Object.DestroyImmediate(root);}
            }
            Assert.AreEqual(4,seen.Count);
        }
        [Test]
        public void SavedRoomExampleAndDirectionUndo_SaveReopenWithoutChangingAcceptedGeometry()
        {
            string folder="Assets/__StairSave_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            try
            {
                EditorSceneManager.OpenScene("Assets/FpsArenaRoomsExample/RoomPartitions.unity",OpenSceneMode.Single);
                var g=Object.FindFirstObjectByType<FpsArenaGenerator>();
                Assert.AreEqual("a57ffb3b9d6ba01e4191ed3cf5a72e75b4e34f6adde2742d9e1fead5b4e0f3f8",g.CurrentLayout.Hash);
                Assert.AreEqual(8,g.CurrentLayout.Rooms.Count);Assert.IsFalse(g.CurrentLayout.Recipe.randomizeStairDirections);
                Assert.AreEqual(4,g.settings.recipe.roomsPerFloor);Assert.AreEqual(32,g.settings.recipe.width);Assert.AreEqual(2,g.settings.recipe.floors);
                var settings=Object.Instantiate(g.settings);AssetDatabase.CreateAsset(settings,folder+"/Settings.asset");g.settings=settings;
                bool previous=settings.recipe.randomizeStairDirections;
                var so=new SerializedObject(settings);so.Update();so.FindProperty("recipe.randomizeStairDirections").boolValue=!previous;so.ApplyModifiedProperties();EditorUtility.SetDirty(settings);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.AreEqual(previous,settings.recipe.randomizeStairDirections);
                so.Update();so.FindProperty("recipe.randomizeStairDirections").boolValue=true;so.ApplyModifiedProperties();EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(folder+"/Settings.asset",ImportAssetOptions.ForceUpdate);
                Assert.IsTrue(settings.recipe.randomizeStairDirections);FpsArenaWindow.GenerateWithUndo(g);string hash=g.CurrentLayout.Hash;FpsArenaWindow.GenerateWithUndo(g);Assert.AreEqual(hash,g.CurrentLayout.Hash);Assert.AreEqual(1,g.transform.childCount);
                Assert.IsTrue(EditorSceneManager.SaveScene(g.gameObject.scene,folder+"/Arena.unity"));settings.recipe.randomizeStairDirections=false;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(folder+"/Arena.unity",OpenSceneMode.Single);g=Object.FindFirstObjectByType<FpsArenaGenerator>();Assert.AreEqual(hash,g.CurrentLayout.Hash);Assert.IsTrue(g.CurrentLayout.Recipe.randomizeStairDirections);Assert.IsFalse(g.Plan().Recipe.randomizeStairDirections);
                g.enabled=false;g.enabled=true;Assert.AreEqual(hash,g.CurrentLayout.Hash);
            }
            finally{Undo.ClearAll();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(folder);}
        }
    }
}
