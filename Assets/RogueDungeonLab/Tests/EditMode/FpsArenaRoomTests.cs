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
    public sealed class FpsArenaRoomTests
    {
        [Serializable] private sealed class BaselineCase { public string recipeJson;public int seed;public string hash; }
        [Serializable] private sealed class Baselines { public List<BaselineCase> cases; }
        private static FpsArenaRecipe Recipe(int count=4,int floors=2)
        { var r=FpsArenaRecipe.CreateFlexible();r.partitionRooms=true;r.roomsPerFloor=count;r.width=r.depth=32;r.floors=floors;r.spacingCells=1;return r; }
        [Test]
        public void PartitionOff_Preserves24PreChangeUnityHashesIncludingSavedWallRunMode()
        {
            var cases=JsonUtility.FromJson<Baselines>(File.ReadAllText(Application.dataPath+"/RogueDungeonLab/Tests/EditMode/FpsArenaRoomsOffBaseline.json")).cases;
            Assert.AreEqual(24,cases.Count);
            foreach(var c in cases)
            {
                var r=JsonUtility.FromJson<FpsArenaRecipe>(c.recipeJson);Assert.IsFalse(r.partitionRooms);
                Assert.AreEqual(c.hash,FpsArenaPlanner.Generate(r,c.seed).Hash);
                r.roomsPerFloor=12;r.roomMinWidthCells=12;r.roomWallThickness=1;
                var l=FpsArenaPlanner.Generate(r,c.seed);Assert.AreEqual(c.hash,l.Hash);Assert.IsEmpty(l.Rooms);
            }
        }
        [Test]
        public void RequestedRoomCounts_ProduceExactlyTheSameNumberOfClosedDoorRegions()
        {
            foreach(int count in new[]{1,2,3,4,6,8,12}) foreach(int seed in new[]{0,71,-7})
            {
                var r=Recipe(count,1);r.shape=FpsArenaShape.Rectangle;r.coverDensity=r.enemyDensity=r.gimmickDensity=r.itemDensity=0;
                var l=FpsArenaPlanner.Generate(r,seed);l.Validate();
                Assert.AreEqual(count,l.RoomReports[0].actual,"Room count "+count+", seed "+seed+": "+l.RoomReports[0].reason);
                Assert.AreEqual(count,l.CountClosedRooms(0));Assert.AreEqual(count-1,l.RoomDoors.Count);CheckGraph(l);
                Assert.AreEqual(l.FloorCount(),l.Rooms.Sum(room=>room.cellCount));
                foreach(var room in l.Rooms) Assert.GreaterOrEqual(room.cellCount,r.roomMinAreaCells);
            }
        }
        [Test]
        public void RepeatedSeedAndContentDensity_KeepRoomGeometryAndGraphStable()
        {
            var r=Recipe();string source=JsonUtility.ToJson(r);var a=FpsArenaPlanner.Generate(r,71);var b=FpsArenaPlanner.Generate(r,71);
            Assert.AreEqual(a.Hash,b.Hash);Assert.AreEqual(source,JsonUtility.ToJson(r));Assert.IsEmpty(a.Walls);
            r.coverDensity=r.enemyDensity=r.gimmickDensity=r.itemDensity=.3f;var dense=FpsArenaPlanner.Generate(r,71);
            CollectionAssert.AreEqual(a.Rooms.Select(JsonUtility.ToJson),dense.Rooms.Select(JsonUtility.ToJson));
            CollectionAssert.AreEqual(a.RoomDoors.Select(JsonUtility.ToJson),dense.RoomDoors.Select(JsonUtility.ToJson));
            Assert.AreNotEqual(a.Hash,FpsArenaPlanner.Generate(Recipe(),-7).Hash);
        }
        [Test]
        public void ShapesFloorsCountsAndSeeds_216ProfilesRespectActualRoomsAndAllContentConnectivity()
        {
            int profiles=0;
            foreach(FpsArenaShape shape in Enum.GetValues(typeof(FpsArenaShape)))
            foreach(var size in new[]{new Vector2Int(20,20),new Vector2Int(32,24),new Vector2Int(64,21)})
            foreach(int floors in new[]{1,2,4}) foreach(int count in new[]{2,4,8,12}) foreach(int seed in new[]{0,71})
            {
                var r=Recipe(count,floors);r.width=size.x;r.depth=size.y;r.shape=shape;r.cellSize=2;r.floorHeight=6;
                r.roomDoorWidthCells=seed==0?1:3;r.coverDensity=r.enemyDensity=r.gimmickDensity=r.itemDensity=1;r.maxContentPerCategoryPerFloor=10;
                var l=FpsArenaPlanner.Generate(r,seed);l.Validate();CheckGraph(l);profiles++;
                foreach(var room in l.Rooms)
                {
                    var cells=new List<Vector3Int>();for(int z=room.min.y;z<room.max.y;z++)for(int x=room.min.x;x<room.max.x;x++){var c=new Vector3Int(x,room.floor,z);if(l.HasFloor(c))cells.Add(c);}
                    Assert.GreaterOrEqual(cells.Max(c=>c.x)-cells.Min(c=>c.x)+1,r.roomMinWidthCells);Assert.GreaterOrEqual(cells.Max(c=>c.z)-cells.Min(c=>c.z)+1,r.roomMinWidthCells);
                }
                var blocked=l.BlockedCells();foreach(var p in l.Content) foreach(var c in p.Footprint()) { blocked.Add(c);Assert.AreEqual(l.RoomAt(p.cell),l.RoomAt(c));Assert.IsFalse(l.Reserved[c.x,c.y,c.z]); }
                Assert.AreEqual(l.FloorCount()-blocked.Count,l.ReachableCount(blocked),shape+"/"+size+"/"+floors+"/"+count+"/"+seed);
                for(int f=0;f<floors;f++)
                {
                    Assert.AreEqual(l.RoomReports[f].actual,l.CountClosedRooms(f));
                    Assert.LessOrEqual(l.RoomReports[f].actual,count);
                    if(l.RoomReports[f].actual<count)Assert.IsNotEmpty(l.RoomReports[f].reason);
                    for(int z=0;z<r.depth;z++)for(int x=0;x<r.width;x++)
                    {
                        var c=new Vector3Int(x,f,z);if(!l.HasFloor(c))continue;
                        foreach(var step in new[]{Vector3Int.right,new Vector3Int(0,0,1)})
                        { var n=c+step;if(l.HasFloor(n)&&l.RoomAt(c)!=l.RoomAt(n))Assert.IsFalse(l.CanTraverse(c,n,true),"Room boundary has an end bypass."); }
                    }
                }
            }
            Assert.AreEqual(216,profiles);
        }
        [Test]
        public void ImpossibleCount_ReportsActualCountAndReasonInsteadOfWallFragments()
        {
            var r=Recipe(12,4);r.width=r.depth=20;r.roomMinWidthCells=12;r.roomMinAreaCells=256;
            var l=FpsArenaPlanner.Generate(r,71);Assert.AreEqual(4,l.Rooms.Count);Assert.IsEmpty(l.RoomWalls);Assert.IsEmpty(l.RoomDoors);
            foreach(var report in l.RoomReports){Assert.AreEqual(1,report.actual);Assert.AreEqual(12,report.requested);Assert.IsNotEmpty(report.reason);}
            l.Validate();
        }
        [Test]
        public void ActualRoomColliders_HaveContinuousBoundariesAndExactDoorWidths()
        {
            var l=FpsArenaPlanner.Generate(Recipe(),73125);var root=FpsArenaSceneBuilder.Build(l,null);root.SetActive(true);
            try
            {
                Assert.IsNotEmpty(l.RoomWalls);Physics.SyncTransforms();
                foreach(var wall in l.RoomWalls)
                {
                    var collider=root.transform.Find(wall.id).GetComponent<BoxCollider>();Assert.IsFalse(collider.isTrigger);
                    Vector3 size=wall.alongX?new Vector3(wall.lengthCells*l.Recipe.cellSize,wall.height,wall.thickness):new Vector3(wall.thickness,wall.height,wall.lengthCells*l.Recipe.cellSize);
                    Assert.Less(Vector3.Distance(size,collider.bounds.size),.001f);
                    foreach(var identity in root.GetComponentsInChildren<FpsArenaContentIdentity>()) foreach(var content in identity.GetComponentsInChildren<Collider>())Assert.IsFalse(collider.bounds.Intersects(content.bounds));
                }
                foreach(var door in l.RoomDoors)
                {
                    Assert.AreEqual(door.widthCells*l.Recipe.cellSize,l.Recipe.roomDoorWidthCells*l.Recipe.cellSize);
                    Assert.IsEmpty(root.transform.Find(door.id).GetComponentsInChildren<Collider>());
                    for(int i=0;i<door.widthCells;i++)
                    {
                        var foot=l.Position(door.cell+door.Step*i)+Vector3.up*.06f;
                        Assert.IsFalse(Physics.CapsuleCast(foot+Vector3.up*.35f,foot+Vector3.up*1.45f,.35f,(Vector3)door.Normal,out var hit,l.Recipe.cellSize,~0,QueryTriggerInteraction.Ignore),door.id+" "+(hit.collider!=null?hit.collider.name:""));
                    }
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void PartitionSettings_UndoSaveReopenAndBuiltSnapshotRestore()
        {
            string folder="Assets/__RoomSave_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                var settings=ScriptableObject.CreateInstance<FpsArenaSettings>();settings.recipe=Recipe();AssetDatabase.CreateAsset(settings,folder+"/Settings.asset");
                var so=new SerializedObject(settings);so.Update();so.FindProperty("recipe.roomsPerFloor").intValue=6;so.ApplyModifiedProperties();EditorUtility.SetDirty(settings);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.AreEqual(4,settings.recipe.roomsPerFloor);
                so.Update();so.FindProperty("recipe.roomsPerFloor").intValue=6;so.ApplyModifiedProperties();EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(folder+"/Settings.asset",ImportAssetOptions.ForceUpdate);
                settings=AssetDatabase.LoadAssetAtPath<FpsArenaSettings>(folder+"/Settings.asset");Assert.AreEqual(6,settings.recipe.roomsPerFloor);
                var g=new GameObject("Saved room arena").AddComponent<FpsArenaGenerator>();g.settings=settings;g.generateOnPlay=false;
                FpsArenaWindow.GenerateWithUndo(g);FpsArenaWindow.GenerateWithUndo(g);Assert.AreEqual(1,g.transform.childCount);string hash=g.CurrentLayout.Hash;int rooms=g.CurrentLayout.Rooms.Count;
                Assert.IsTrue(EditorSceneManager.SaveScene(g.gameObject.scene,folder+"/Arena.unity"));settings.recipe.partitionRooms=false;settings.recipe.internalWalls=true;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(folder+"/Arena.unity",OpenSceneMode.Single);g=Object.FindFirstObjectByType<FpsArenaGenerator>();
                Assert.AreEqual(hash,g.CurrentLayout.Hash);Assert.AreEqual(rooms,g.CurrentLayout.Rooms.Count);Assert.AreEqual(1,g.transform.childCount);Assert.IsEmpty(g.Plan().Rooms);
                g.enabled=false;g.enabled=true;Assert.AreEqual(hash,g.CurrentLayout.Hash);
            }
            finally{Undo.ClearAll();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);AssetDatabase.DeleteAsset(folder);}
        }
        private static void CheckGraph(FpsArenaLayout l)
        {
            foreach(var report in l.RoomReports)
            {
                var rooms=l.Rooms.Where(r=>r.floor==report.floor).Select(r=>r.index).ToArray();var seen=new HashSet<int>{rooms[0]};var queue=new Queue<int>();queue.Enqueue(rooms[0]);
                while(queue.Count>0){int room=queue.Dequeue();foreach(var c in l.RoomConnections){int next=c.roomA==room?c.roomB:c.roomB==room?c.roomA:-1;if(next>=0&&seen.Add(next))queue.Enqueue(next);}}
                Assert.AreEqual(report.actual,seen.Count);Assert.AreEqual(report.actual-1,l.RoomConnections.Count(c=>l.Rooms[c.roomA].floor==report.floor));
            }
        }
    }
}
