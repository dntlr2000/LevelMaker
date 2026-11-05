using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaBoundaryWallTests
    {
        private string folder;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        // 실제 프로젝트와 구분되는 테스트 전용 자산 폴더만 만든다.
        [SetUp] public void SetUp()
        { folder = "Assets/__BoundaryWallTest_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7)); }

        // 테스트가 만든 오브젝트와 자산만 정리한다.
        [TearDown] public void TearDown()
        {
            Undo.ClearAll();
            foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            owned.Clear(); AssetDatabase.DeleteAsset(folder);
        }

        // 수명 관리가 필요한 Unity 오브젝트를 등록한다.
        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }

        // 저장된 v2 수동 스테이지를 준비한다.
        private FpsArenaStageAsset Stage(int floors = 1)
        { return FpsArenaStageAuthoring.CreateAsset(folder + "/Stage.asset", FpsArenaStageData.Create(16, 16, floors)); }

        // 테스트 장면에만 적용할 생성기를 만든다.
        private FpsArenaGenerator Generator()
        { return Own(new GameObject("Boundary wall test")).AddComponent<FpsArenaGenerator>(); }

        // 새 수동 자산은 바닥만 생성하며 Builder가 외곽벽을 보충하지 않는다.
        [Test] public void NewV2Stage_HasNoImplicitBoundaryWalls()
        {
            var data = FpsArenaStageData.Create(); var layout = data.ToLayout();
            Assert.AreEqual(2, data.version); Assert.IsEmpty(data.partitions); Assert.IsEmpty(layout.BoundaryWalls);
            var root = Own(FpsArenaSceneBuilder.Build(layout, null));
            foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            { Assert.AreNotEqual("외곽가드", box.name); Assert.IsFalse(box.name.StartsWith("arena:boundary:", StringComparison.Ordinal)); }
            Assert.AreEqual(data.ComputeHash(), layout.ComputeHash());
        }

        // 모든 층과 내부 구멍을 채우되 기존 내부벽의 ID/치수와 중복 없는 결과를 보존한다.
        [Test] public void AutoPlacement_CoversAllFloorsAndHoles_AndPreservesExistingWalls()
        {
            var data = FpsArenaStageData.Create(16, 16, 2);
            data.floorCells.Remove(new Vector3Int(5, 0, 5)); data.floorCells.Remove(new Vector3Int(6, 1, 6));
            var existing = new FpsArenaRoomWall { id = "custom", cell = new Vector3Int(8, 0, 8), lengthCells = 1, height = 2.8f, thickness = .4f };
            data.partitions.Add(existing); string original = JsonUtility.ToJson(existing);
            Assert.AreEqual(136, data.AddMissingBoundaryWalls());
            Assert.AreEqual(original, JsonUtility.ToJson(data.partitions.Find(w => w.id == "custom")));
            string hash = data.ComputeHash(); Assert.AreEqual(0, data.AddMissingBoundaryWalls()); Assert.AreEqual(hash, data.ComputeHash());
            var layout = data.ToLayout(); Assert.AreEqual(136, layout.BoundaryWalls.Count); Assert.AreEqual(1, layout.RoomWalls.Count);
            Assert.IsTrue(data.partitions.Exists(w => w.cell.x == -1)); Assert.IsTrue(data.partitions.Exists(w => w.cell.z == -1));
            Assert.IsTrue(data.partitions.Exists(w => w.cell == new Vector3Int(5, 0, 5) && !w.alongX));
            foreach (var wall in layout.BoundaryWalls) { Assert.AreEqual(1.5f, wall.height); Assert.AreEqual(.15f, wall.thickness); }
            Assert.AreEqual(layout.Hash, layout.ComputeHash());
        }

        // 자동 배치는 한 번의 Undo로 돌아가며 무변경 재실행은 자산을 dirty 처리하지 않는다.
        [Test] public void AutoPlacement_IsOneUndo_AndNoOpDoesNotDirtyAsset()
        {
            var stage = Stage(); string before = stage.data.ComputeHash(); Undo.IncrementCurrentGroup();
            FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage); string after = stage.data.ComputeHash();
            Assert.AreEqual(64, stage.data.partitions.Count); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.AreEqual(before, stage.data.ComputeHash()); Undo.PerformRedo(); Assert.AreEqual(after, stage.data.ComputeHash());
            AssetDatabase.SaveAssetIfDirty(stage); Assert.IsFalse(EditorUtility.IsDirty(stage)); int group = Undo.GetCurrentGroup();
            FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage);
            Assert.AreEqual(group, Undo.GetCurrentGroup()); Assert.IsFalse(EditorUtility.IsDirty(stage)); Assert.AreEqual(after, stage.data.ComputeHash());
        }

        // v1 읽기/해시는 원본을 수정하지 않고 명시적 변환만 Undo 가능한 v2를 만든다.
        [Test] public void LegacyRead_IsNonMutating_AndExplicitUpgradeSupportsUndoAndReload()
        {
            var stage = Stage(); stage.data.version = 1; EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage);
            string source = JsonUtility.ToJson(stage.data), disk = File.ReadAllText(folder + "/Stage.asset");
            var layout = stage.CreateLayout(); Assert.AreEqual(64, layout.BoundaryWalls.Count);
            Assert.AreEqual(source, JsonUtility.ToJson(stage.data)); Assert.AreEqual(disk, File.ReadAllText(folder + "/Stage.asset"));
            Assert.IsFalse(EditorUtility.IsDirty(stage)); Assert.AreEqual(layout.Hash, stage.data.ComputeHash()); Assert.AreEqual(layout.Hash, layout.ComputeHash());
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.Edit(stage, "차단", d => d.partitions.Clear()));
            Undo.IncrementCurrentGroup(); FpsArenaStageAuthoring.UpgradeWalls(stage);
            Assert.AreEqual(2, stage.data.version); Assert.AreEqual(64, stage.data.partitions.Count); Assert.AreEqual(layout.Hash, stage.CreateLayout().Hash);
            Assert.AreEqual(disk, File.ReadAllText(folder + "/Stage.asset")); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.AreEqual(1, stage.data.version); Assert.IsEmpty(stage.data.partitions);
            Undo.PerformRedo(); AssetDatabase.SaveAssetIfDirty(stage); AssetDatabase.ImportAsset(folder + "/Stage.asset", ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath<FpsArenaStageAsset>(folder + "/Stage.asset");
            Assert.AreEqual(2, reopened.data.version); Assert.AreEqual(64, reopened.CreateLayout().BoundaryWalls.Count);
        }

        // 절차 결과 복사에는 명시적 외곽벽과 기존 내부벽이 함께 포함된다.
        [Test] public void ProceduralCapture_IncludesBoundaryWalls_WithoutChangingOriginalHash()
        {
            foreach (bool rooms in new[] { false, true })
            {
                var recipe = FpsArenaRecipe.CreateFlexible(); recipe.partitionRooms = rooms;
                var original = FpsArenaPlanner.Generate(recipe, 73125); string hash = original.Hash;
                var data = FpsArenaStageData.Capture(original); var restored = data.ToLayout();
                Assert.Greater(original.BoundaryWalls.Count, 0); Assert.AreEqual(original.BoundaryWalls.Count, restored.BoundaryWalls.Count);
                Assert.AreEqual(hash, original.ComputeHash()); Assert.AreEqual(data.ComputeHash(), restored.Hash); Assert.AreEqual(restored.Hash, restored.ComputeHash());
                for (int i = 0; i < original.BoundaryWalls.Count; i++)
                {
                    var wall = original.BoundaryWalls[i]; var copy = data.partitions.Find(w => w.id == wall.id);
                    Assert.IsNotNull(copy); Assert.AreEqual(wall.Position(original), copy.Position(restored)); Assert.AreEqual(wall.height, copy.height);
                }
            }
        }

        // 일부/전체 외곽 삭제가 자산 재임포트와 마지막 적용 Prefab 스냅샷 복원 후에도 유지된다.
        [TestCase(false)] [TestCase(true)]
        public void DeletedBoundaries_StayDeletedAcrossAssetAndBuiltSnapshotReload(bool removeAll)
        {
            var stage = Stage(); FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage);
            FpsArenaStageAuthoring.Edit(stage, "외곽 열기", d => {
                if (removeAll) d.partitions.Clear();
                else { FpsArenaStageAuthoring.SetWall(d, new Vector3Int(-1, 0, 5), false, false); FpsArenaStageAuthoring.SetWall(d, new Vector3Int(5, 0, -1), true, false); }
            });
            int count = removeAll ? 0 : 62; string hash = stage.data.ComputeHash(); AssetDatabase.SaveAssetIfDirty(stage);
            AssetDatabase.ImportAsset(folder + "/Stage.asset", ImportAssetOptions.ForceUpdate);
            stage = AssetDatabase.LoadAssetAtPath<FpsArenaStageAsset>(folder + "/Stage.asset"); Assert.AreEqual(count, stage.CreateLayout().BoundaryWalls.Count);
            var generator = Generator(); FpsArenaStageAuthoring.Apply(stage, generator); FpsArenaStageAuthoring.Apply(stage, generator);
            Assert.AreEqual(count, generator.CurrentLayout.BoundaryWalls.Count); Assert.AreEqual(1, generator.transform.childCount);
            var path = folder + "/Built.prefab"; PrefabUtility.SaveAsPrefabAsset(generator.gameObject, path);
            stage.data.AddMissingBoundaryWalls();
            var instance = Own((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            var restored = instance.GetComponent<FpsArenaGenerator>();
            Assert.AreEqual(hash, restored.CurrentLayout.Hash); Assert.AreEqual(count, restored.CurrentLayout.BoundaryWalls.Count);
        }

        // 내부/음수 외곽 경계는 허용하고 중복과 바닥 없는 경계는 계속 거부한다.
        [Test] public void Validation_AllowsNegativeBoundaryAnchors_RejectsDuplicateAndOrphanEdges()
        {
            var data = FpsArenaStageData.Create();
            FpsArenaStageAuthoring.SetWall(data, new Vector3Int(-1, 0, 5), false, true);
            FpsArenaStageAuthoring.SetWall(data, new Vector3Int(5, 0, -1), true, true);
            FpsArenaStageAuthoring.SetWall(data, new Vector3Int(15, 0, 7), false, true);
            FpsArenaStageAuthoring.SetWall(data, new Vector3Int(7, 0, 15), true, true);
            Assert.DoesNotThrow(() => data.ToLayout());
            data.partitions.Add(new FpsArenaRoomWall { id = "duplicate", cell = new Vector3Int(-1, 0, 5), lengthCells = 1, height = 1.5f, thickness = .15f });
            Assert.Throws<InvalidOperationException>(() => data.ToLayout()); data.partitions.RemoveAt(data.partitions.Count - 1);
            data.partitions.Add(new FpsArenaRoomWall { id = "orphan", cell = new Vector3Int(-2, 0, 5), lengthCells = 1, height = 1.5f, thickness = .15f });
            StringAssert.Contains("경계", Assert.Throws<InvalidOperationException>(() => data.ToLayout()).Message);
        }

        // 네 면과 동률 선택을 동일한 정규 경계 좌표로 해석한다.
        [TestCase(.5f, .01f, 0, 0, true)] [TestCase(.99f, .5f, 0, 0, false)]
        [TestCase(.5f, .99f, 0, -1, true)] [TestCase(.01f, .5f, -1, 0, false)] [TestCase(.5f, .5f, 0, 0, true)]
        public void EdgePicker_ResolvesAllFourSides(float x, float y, int dx, int dz, bool expectedAlongX)
        {
            var cell = new Vector3Int(0, 0, 0);
            FpsArenaStageAuthoring.ResolveWallEdge(cell, new Vector2(x, y), out var anchor, out bool alongX);
            Assert.AreEqual(new Vector3Int(dx, 0, dz), anchor); Assert.AreEqual(expectedAlongX, alongX);
        }

        // 실제 창의 칠하기/선택/문 도구가 같은 셀의 네 면과 음수 외곽 좌표를 편집하는지 확인한다.
        [Test] public void WindowTools_PaintSelectAndDeleteAllFourCellEdges()
        {
            var stage = Stage(); var window = Own(ScriptableObject.CreateInstance<FpsArenaStageWindow>());
            var type = typeof(FpsArenaStageWindow); var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("stage", flags).SetValue(window, stage); type.GetField("tool", flags).SetValue(window, 3);
            var area = new Rect(10, 20, 448, 448); var cell = Vector3Int.zero;
            var cellRect = (Rect)type.GetMethod("CellRect", flags).Invoke(window, new object[] { area, cell });
            var paint = type.GetMethod("Paint", flags);
            foreach (var uv in new[] { new Vector2(.5f, .01f), new Vector2(.99f, .5f), new Vector2(.5f, .99f), new Vector2(.01f, .5f) })
                paint.Invoke(window, new object[] { cell, area, new Vector2(cellRect.x + uv.x * 28, cellRect.y + uv.y * 28) });
            Assert.AreEqual(4, stage.data.partitions.Count);
            type.GetMethod("Select", flags).Invoke(window, new object[] { cell, area, new Vector2(cellRect.x + 1, cellRect.center.y) });
            string selected = (string)type.GetField("selectedId", flags).GetValue(window);
            Assert.IsTrue(stage.data.partitions.Exists(w => w.id == selected && w.cell == new Vector3Int(-1, 0, 0)));
            type.GetMethod("DeleteSelection", flags).Invoke(window, null); Assert.AreEqual(3, stage.data.partitions.Count);
            type.GetField("tool", flags).SetValue(window, 4); type.GetField("lastPaint", flags).SetValue(window, new Vector3Int(-1, -1, -1));
            paint.Invoke(window, new object[] { cell, area, new Vector2(cellRect.center.x, cellRect.yMax - 1) });
            Assert.AreEqual(2, stage.data.partitions.Count); Assert.IsFalse(stage.data.partitions.Exists(w => w.cell.z == -1));
            Assert.DoesNotThrow(() => stage.CreateLayout());
        }

        // 긴 벽의 한 경계를 열어도 나머지 구간의 치수와 데이터 해시 일관성을 보존한다.
        [Test] public void DoorTool_SplitsOnlyRequestedEdgeOfLongWall()
        {
            var data = FpsArenaStageData.Create();
            data.partitions.Add(new FpsArenaRoomWall { id = "long", cell = new Vector3Int(4, 0, 7), alongX = true, lengthCells = 4, height = 3, thickness = .3f });
            FpsArenaStageAuthoring.SetWall(data, new Vector3Int(5, 0, 7), true, false);
            Assert.AreEqual(2, data.partitions.Count); Assert.IsFalse(data.partitions.Exists(w => FpsArenaStageAuthoring.IsWallOnEdge(w, new Vector3Int(5, 0, 7), true)));
            foreach (int x in new[] { 4, 6, 7 }) Assert.IsTrue(data.partitions.Exists(w => FpsArenaStageAuthoring.IsWallOnEdge(w, new Vector3Int(x, 0, 7), true)));
            var layout = data.ToLayout(); Assert.AreEqual(layout.Hash, layout.ComputeHash());
        }

        // 자동 배치가 계단 개구부/착지 경계에 벽을 만들지 않고 기존 계단 검사는 유지한다.
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AutoPlacement_ExcludesStairOpeningsAndLandings(int direction)
        {
            var data = FpsArenaStageData.Create(16, 16, 2);
            FpsArenaStageAuthoring.AddStair(data, new Vector3Int(5, 0, 5), (FpsArenaStairDirection)direction);
            Assert.AreEqual(128, data.AddMissingBoundaryWalls());
            var layout = data.ToLayout(); Assert.AreEqual(128, layout.BoundaryWalls.Count); Assert.DoesNotThrow(layout.Validate);
            var stair = data.stairs[0]; data.partitions.Add(new FpsArenaRoomWall { id = "bad-stair-wall", cell = Vector3Int.Min(stair.BottomLane(0), stair.Cell(0, 0)), alongX = direction % 2 == 0, lengthCells = 1, height = 1.5f, thickness = .15f });
            Assert.Throws<InvalidOperationException>(() => data.ToLayout());
        }

        // 기존 콘텐츠 ID와 충돌하는 자동 벽 후보는 부분 배치나 원본 수정을 남기지 않는다.
        [Test] public void AutoPlacement_IdCollisionDoesNotPartlyMutateAsset()
        {
            var stage = Stage();
            var content = FpsArenaStageAuthoring.MakeContent(stage.data, FpsArenaContentKind.Gimmick, new Vector3Int(4, 0, 4));
            content.id = FpsArenaBoundaryWalls.CreateForStage(stage.data)[0].id; stage.data.content.Add(content);
            string before = JsonUtility.ToJson(stage.data);
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage));
            Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Assert.IsEmpty(stage.data.partitions);
        }
    }
}
