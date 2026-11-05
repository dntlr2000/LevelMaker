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
    public sealed class FpsArenaStageSizingTests
    {
        private string folder;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        // 각 검사는 검증 사본 안의 독립 자산 폴더만 사용한다.
        [SetUp] public void SetUp()
        { folder = "Assets/__ManualUi_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7)); }

        // 생성한 객체/자산만 정리하고 다른 테스트와 Undo 기록을 공유하지 않는다.
        [TearDown] public void TearDown()
        {
            Undo.ClearAll(); foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            owned.Clear(); AssetDatabase.DeleteAsset(folder);
        }

        // 기본 카탈로그 없는 수동 자산을 만든다.
        private FpsArenaStageAsset Stage(int floors = 1)
        { return FpsArenaStageAuthoring.CreateAsset(folder + "/Stage.asset", FpsArenaStageData.Create(16, 16, floors)); }

        // 경계형 두 벽과 호환 벽 조각을 서로 겹치지 않게 준비한다.
        private void Walls(FpsArenaStageAsset stage)
        {
            for (int f = 0; f < stage.data.floors; f++)
            {
                FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(-1, f, 3), false, true);
                FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(4, f, 4), true, true);
            }
            stage.data.walls.Add(new FpsArenaWall { id = "legacy-wall", cell = new Vector3Int(9, 0, 9), alongX = true, lengthCells = 4, height = 2.5f, thickness = .35f, doorOffsetCells = 1, doorWidthCells = 1 });
            Assert.DoesNotThrow(() => stage.data.ToLayout());
        }

        // 규격의 최솟값/최댓값을 생성할 수 있고 잘못된 미터 입력은 거부한다.
        [TestCase(8, 8, 1, 2, 3)] [TestCase(64, 64, 4, 5, 6)]
        public void Create_UsesAllExplicitDimensions(int width, int depth, int floors, float cell, float height)
        {
            var data = FpsArenaStageData.Create(width, depth, floors, cell, height);
            Assert.AreEqual(width * depth * floors, data.floorCells.Count); Assert.AreEqual(cell, data.cellSize); Assert.AreEqual(height, data.floorHeight);
            Assert.DoesNotThrow(() => data.ToLayout()); Assert.AreEqual(2, data.version); Assert.IsEmpty(data.partitions);
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageData.Create(16, 16, 1, float.NaN, 4));
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageData.Create(7, 16, 1, 3, 4));
        }

        // 구형 자산의 기본값을 읽기만 하면 메타데이터/해시/파일/dirty가 모두 그대로다.
        [Test] public void DefaultRead_IsNonMutating_AndReturnsIndependentCopy()
        {
            var stage = Stage(); string path = AssetDatabase.GetAssetPath(stage), disk = File.ReadAllText(path), hash = stage.data.ComputeHash();
            var defaults = stage.GetWallDefaults(); defaults.boundaryHeight = 2;
            Assert.IsNull(stage.wallDefaults); Assert.AreEqual(1.5f, stage.GetWallDefaults().boundaryHeight); Assert.AreEqual(3.75f, stage.GetWallDefaults().InteriorHeight(4));
            Assert.AreEqual(hash, stage.data.ComputeHash()); Assert.AreEqual(disk, File.ReadAllText(path)); Assert.IsFalse(EditorUtility.IsDirty(stage));
            FpsArenaStageAuthoring.SetWallDefaults(stage, new FpsArenaWallAuthoringDefaults()); Assert.IsNull(stage.wallDefaults); Assert.IsFalse(EditorUtility.IsDirty(stage));
        }

        // 제작 기본값 저장은 배치 해시와 기존 벽을 바꾸지 않고 Undo/Redo와 재임포트를 지원한다.
        [Test] public void Defaults_SaveCloneUndoRedoAndCopy_WithoutChangingLayoutHash()
        {
            var stage = Stage(); Walls(stage); string before = JsonUtility.ToJson(stage.data), hash = stage.data.ComputeHash();
            var defaults = new FpsArenaWallAuthoringDefaults { automaticInteriorHeight = false, interiorHeight = 2.2f, interiorThickness = .4f, boundaryHeight = 1.7f, boundaryThickness = .3f };
            Undo.IncrementCurrentGroup(); FpsArenaStageAuthoring.SetWallDefaults(stage, defaults); defaults.boundaryHeight = 1;
            Assert.AreEqual(1.7f, stage.wallDefaults.boundaryHeight); Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Assert.AreEqual(hash, stage.data.ComputeHash());
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.IsNull(stage.wallDefaults);
            Undo.PerformRedo(); Assert.AreEqual(1.7f, stage.wallDefaults.boundaryHeight);
            var copy = FpsArenaStageAuthoring.CreateAsset(folder + "/Copy.asset", stage.data, stage.contentCatalog, stage.wallDefaults);
            Assert.AreNotSame(stage.wallDefaults, copy.wallDefaults); Assert.AreEqual(JsonUtility.ToJson(stage.wallDefaults), JsonUtility.ToJson(copy.wallDefaults));
            AssetDatabase.SaveAssetIfDirty(stage); string path = AssetDatabase.GetAssetPath(stage); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<FpsArenaStageAsset>(path); Assert.AreEqual(1.7f, loaded.GetWallDefaults().boundaryHeight); Assert.AreEqual(hash, loaded.data.ComputeHash());
        }

        // 새 벽만 내부/외곽 기본값을 적용하고 자동 배치가 기존 벽 규격을 덮지 않는다.
        [Test] public void DrawingAndAutofill_UseDefaultsOnlyForNewWalls()
        {
            var stage = Stage(); FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(-1, 0, 3), false, true);
            var existing = stage.data.partitions[0]; existing.height = 2;
            var defaults = new FpsArenaWallAuthoringDefaults { automaticInteriorHeight = false, interiorHeight = 2.8f, interiorThickness = .5f, boundaryHeight = 1.2f, boundaryThickness = .2f };
            FpsArenaStageAuthoring.SetWallDefaults(stage, defaults);
            FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(4, 0, 4), true, true, stage.GetWallDefaults());
            string innerId = stage.data.partitions[1].id, existingId = existing.id;
            FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage);
            Assert.AreEqual(2, stage.data.partitions.Find(w => w.id == existingId).height);
            Assert.AreEqual(2.8f, stage.data.partitions.Find(w => w.id == innerId).height);
            foreach (var wall in stage.data.partitions) if (wall.id != innerId && wall.id != existingId) { Assert.AreEqual(1.2f, wall.height); Assert.AreEqual(.2f, wall.thickness); }
            string json = JsonUtility.ToJson(stage.data); AssetDatabase.SaveAssetIfDirty(stage); FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage);
            Assert.AreEqual(json, JsonUtility.ToJson(stage.data)); Assert.IsFalse(EditorUtility.IsDirty(stage));
        }

        // 확대는 기존 구멍/계단/배치를 남기고 새 영역에만 바닥을 추가한다.
        [Test] public void Expansion_AddsOnlyNewFloorCells_AndPreservesAllAuthoredRecords()
        {
            var stage = Stage(2); FpsArenaStageAuthoring.AddStair(stage.data, new Vector3Int(5, 0, 5), FpsArenaStairDirection.Right);
            var hole = new Vector3Int(3, 0, 12); stage.data.floorCells.Remove(hole);
            FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(-1, 0, 3), false, true);
            stage.data.content.Add(new FpsArenaContent { id = "metric-content", cell = new Vector3Int(12, 0, 12), kind = FpsArenaContentKind.Cover, widthCells = 1, depthCells = 1, size = new Vector3(2.6f, 2.2f, 2.4f) });
            string walls = JsonUtility.ToJson(stage.data.partitions[0]), content = JsonUtility.ToJson(stage.data.content[0]), stair = JsonUtility.ToJson(stage.data.stairs[0]);
            int count = stage.data.floorCells.Count;
            var preview = FpsArenaStageAuthoring.PreviewResize(stage, 18, 20, 3, 4, 5);
            Assert.IsTrue(preview.CanApply, string.Join("\n", preview.Errors)); Assert.AreEqual(568, preview.AddedFloorCells);
            FpsArenaStageAuthoring.ResizeStage(stage, 18, 20, 3, 4, 5);
            Assert.AreEqual(count + 568, stage.data.floorCells.Count); Assert.IsFalse(stage.data.floorCells.Contains(hole)); Assert.AreEqual(1, stage.data.partitions.Count);
            Assert.AreEqual(walls, JsonUtility.ToJson(stage.data.partitions[0])); Assert.AreEqual(content, JsonUtility.ToJson(stage.data.content[0])); Assert.AreEqual(stair, JsonUtility.ToJson(stage.data.stairs[0]));
            Assert.IsFalse(stage.data.floorCells.Contains(stage.data.stairs[0].Cell(0, 0, 1)));
        }

        // 축소 문제 목록은 바닥/벽/스폰/콘텐츠/계단을 포함하고 실패 시 원본을 보존한다.
        [Test] public void UnsafeShrink_ListsEveryRecordCategory_AndDoesNotMutate()
        {
            var stage = Stage(2); FpsArenaStageAuthoring.AddStair(stage.data, new Vector3Int(10, 0, 10), FpsArenaStairDirection.Forward);
            stage.data.spawns.Add(new Vector3Int(15, 0, 15));
            FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(15, 0, 5), false, true);
            stage.data.content.Add(new FpsArenaContent { id = "outside-content", cell = new Vector3Int(14, 0, 2), kind = FpsArenaContentKind.Item, size = Vector3.one });
            EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage); string before = JsonUtility.ToJson(stage.data);
            var preview = FpsArenaStageAuthoring.PreviewResize(stage, 8, 8, 1, 3, 4); string errors = string.Join("\n", preview.Errors);
            Assert.IsFalse(preview.CanApply);
            foreach (string kind in new[] { "바닥", "벽 [", "스폰", "콘텐츠 [outside-content]", "계단" }) StringAssert.Contains(kind, errors);
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.ResizeStage(stage, 8, 8, 1, 3, 4));
            Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Assert.IsFalse(EditorUtility.IsDirty(stage));
        }

        // 범위 밖 바닥을 사용자가 정리한 후에는 음수 외곽 anchor를 보존한 채 축소한다.
        [Test] public void SafeShrink_PreservesNegativeBoundaryAnchor_AndSupportsUndoRedo()
        {
            var stage = Stage(); stage.data.floorCells.RemoveAll(c => c.x >= 8 || c.z >= 8);
            FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(-1, 0, 3), false, true);
            EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage); string before = JsonUtility.ToJson(stage.data), wall = JsonUtility.ToJson(stage.data.partitions[0]);
            Undo.IncrementCurrentGroup(); FpsArenaStageAuthoring.ResizeStage(stage, 8, 8, 1, 3, 4);
            Assert.AreEqual(64, stage.data.floorCells.Count); Assert.AreEqual(wall, JsonUtility.ToJson(stage.data.partitions[0]));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Undo.PerformRedo(); Assert.AreEqual(8, stage.data.width);
        }

        // 셀/층간 높이 축소는 기존 미터 치수를 자동 줄이지 않고 문제 ID를 안내한다.
        [Test] public void MetricShrink_ReportsWallAndRotatedContentDimensions()
        {
            var stage = Stage(); FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(4, 0, 4), true, true);
            stage.data.content.Add(new FpsArenaContent { id = "large-rotated", kind = FpsArenaContentKind.Cover, cell = new Vector3Int(8, 0, 8), size = new Vector3(2, 3.2f, 2), yaw = 45 });
            var preview = FpsArenaStageAuthoring.PreviewResize(stage, 16, 16, 1, 2, 3);
            Assert.IsFalse(preview.CanApply); string errors = string.Join("\n", preview.Errors);
            StringAssert.Contains(stage.data.partitions[0].id, errors); StringAssert.Contains("large-rotated", errors); Assert.AreEqual(3.75f, stage.data.partitions[0].height);
        }

        // 카탈로그 오류는 기존 장면 적용 검사에 남겨두고 규격 서비스와 분리한다.
        [Test] public void Resize_DoesNotRequireCatalog_AndSameDimensionsAreNoOp()
        {
            var stage = Stage(); stage.data.content.Add(new FpsArenaContent { id = "missing-prefab", contentKey = "enemy.missing", kind = FpsArenaContentKind.Enemy, cell = new Vector3Int(8, 0, 8), size = Vector3.one });
            EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage);
            FpsArenaStageAuthoring.ResizeStage(stage, 16, 16, 1, 3, 4); Assert.IsFalse(EditorUtility.IsDirty(stage));
            FpsArenaStageAuthoring.ResizeStage(stage, 18, 16, 1, 3, 4); Assert.AreEqual(18, stage.data.width);
            Assert.Throws<InvalidOperationException>(() => stage.CreateLayout());
        }

        // 층간 높이가 바뀌어도 기존 벽은 그대로이며 자동 기본 높이는 다음 새 벽에만 사용된다.
        [Test] public void AutomaticHeight_AffectsOnlyFutureInteriorWalls()
        {
            var stage = Stage(); FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(4, 0, 4), true, true);
            FpsArenaStageAuthoring.ResizeStage(stage, 16, 16, 1, 3, 5);
            FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(6, 0, 6), true, true, stage.GetWallDefaults());
            Assert.AreEqual(3.75f, stage.data.partitions[0].height); Assert.AreEqual(4.75f, stage.data.partitions[1].height);
        }

        // 개별 벽 치수 변경은 기존 길이/문과 선택하지 않은 벽을 보존한다.
        [TestCase(false)] [TestCase(true)] public void SelectedDimensions_PreserveIdentityLengthAndDoors(bool legacy)
        {
            var stage = Stage(); Walls(stage); string id = legacy ? "legacy-wall" : stage.data.partitions[0].id;
            string untouched = JsonUtility.ToJson(stage.data.partitions[1]);
            FpsArenaStageAuthoring.SetWallDimensions(stage, id, 2.1f, .6f);
            if (legacy) { Assert.AreEqual(4, stage.data.walls[0].lengthCells); Assert.AreEqual(1, stage.data.walls[0].doorWidthCells); Assert.AreEqual(1, stage.data.walls[0].doorOffsetCells); Assert.AreEqual(2.1f, stage.data.walls[0].height); }
            else { Assert.AreEqual(id, stage.data.partitions[0].id); Assert.AreEqual(2.1f, stage.data.partitions[0].height); }
            Assert.AreEqual(untouched, JsonUtility.ToJson(stage.data.partitions[1]));
        }

        // 일괄 필터와 층 범위를 지키고 한 번의 Undo로 모든 대상 치수를 복원한다.
        [TestCase(FpsArenaWallSelection.All, false, 5)] [TestCase(FpsArenaWallSelection.Interior, false, 3)] [TestCase(FpsArenaWallSelection.Boundary, false, 2)]
        [TestCase(FpsArenaWallSelection.All, true, 3)] [TestCase(FpsArenaWallSelection.Interior, true, 2)] [TestCase(FpsArenaWallSelection.Boundary, true, 1)]
        public void BatchDimensions_RespectFilterFloorAndSingleUndo(FpsArenaWallSelection filter, bool currentOnly, int expected)
        {
            var stage = Stage(2); Walls(stage); string before = JsonUtility.ToJson(stage.data); int? floor = currentOnly ? 0 : (int?)null;
            Assert.AreEqual(expected, FpsArenaStageAuthoring.CountWalls(stage.data, floor, filter));
            Undo.IncrementCurrentGroup(); FpsArenaStageAuthoring.SetWallDimensionsBatch(stage, floor, filter, 2, .6f);
            int changed = 0; foreach (var wall in stage.data.partitions) if (wall.height == 2 && wall.thickness == .6f) changed++;
            foreach (var wall in stage.data.walls) if (wall.height == 2 && wall.thickness == .6f) changed++;
            Assert.AreEqual(expected, changed); Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Undo.PerformRedo();
            EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage); FpsArenaStageAuthoring.SetWallDimensionsBatch(stage, floor, filter, 2, .6f); Assert.IsFalse(EditorUtility.IsDirty(stage));
        }

        // 잘못된 치수와 대상 0개는 자산/dirty를 변경하지 않는다.
        [TestCase(float.NaN, .2f)] [TestCase(.05f, .2f)] [TestCase(3.91f, .2f)] [TestCase(2, 1.1f)]
        public void InvalidDimensions_LeaveAllSourcesUntouched(float height, float thickness)
        {
            var stage = Stage(); Walls(stage); EditorUtility.SetDirty(stage); AssetDatabase.SaveAssetIfDirty(stage); string before = JsonUtility.ToJson(stage.data);
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.SetWallDimensions(stage, stage.data.partitions[0].id, height, thickness));
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.SetWallDimensionsBatch(stage, null, FpsArenaWallSelection.All, height, thickness));
            Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Assert.IsFalse(EditorUtility.IsDirty(stage));
        }

        // 빈 대상의 일괄 변경과 구형 v1 쓰기 차단도 Undo/원본을 보존한다.
        [Test] public void EmptyBatchAndLegacyGuard_DoNotWrite()
        {
            var stage = Stage(); FpsArenaStageAuthoring.SetWallDimensionsBatch(stage, null, FpsArenaWallSelection.All, 2, .3f); Assert.IsFalse(EditorUtility.IsDirty(stage));
            stage.data.version = 1; string before = JsonUtility.ToJson(stage.data);
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.ResizeStage(stage, 20, 20, 1, 3, 4));
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.SetWallDefaults(stage, new FpsArenaWallAuthoringDefaults())); Assert.AreEqual(before, JsonUtility.ToJson(stage.data));
        }

        // 자산 재임포트와 마지막 적용 스냅샷 복원은 편집 중인 자산과 분리된 치수를 유지한다.
        [Test] public void SavedSizingAndSnapshot_KeepAppliedColliderDimensions()
        {
            var stage = Stage(); FpsArenaStageAuthoring.SetWall(stage.data, new Vector3Int(-1, 0, 3), false, true); string id = stage.data.partitions[0].id;
            FpsArenaStageAuthoring.ResizeStage(stage, 18, 16, 2, 4, 5); FpsArenaStageAuthoring.SetWallDimensions(stage, id, 2.2f, .6f);
            AssetDatabase.SaveAssetIfDirty(stage); string path = AssetDatabase.GetAssetPath(stage); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); stage = AssetDatabase.LoadAssetAtPath<FpsArenaStageAsset>(path);
            var host = new GameObject("Manual sizing snapshot"); owned.Add(host); var generator = host.AddComponent<FpsArenaGenerator>(); FpsArenaStageAuthoring.Apply(stage, generator);
            var box = generator.transform.GetChild(0).Find(id).GetComponent<BoxCollider>(); Assert.AreEqual(new Vector3(.6f, 2.2f, 4), Vector3.Scale(box.size, box.transform.lossyScale));
            string hash = generator.CurrentLayout.Hash; var root = generator.GeneratedRoot;
            Assert.Throws<InvalidOperationException>(() => FpsArenaStageAuthoring.ResizeStage(stage, 8, 8, 1, 3, 4)); Assert.AreSame(root, generator.GeneratedRoot); Assert.AreEqual(hash, generator.CurrentLayout.Hash);
            string prefab = folder + "/Applied.prefab"; PrefabUtility.SaveAsPrefabAsset(host, prefab);
            stage.data.partitions.Clear(); stage.data.cellSize = 2;
            var restored = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)); owned.Add(restored);
            Assert.AreEqual(hash, restored.GetComponent<FpsArenaGenerator>().CurrentLayout.Hash);
            var restoredBox = restored.transform.GetChild(0).Find(id).GetComponent<BoxCollider>(); Assert.AreEqual(new Vector3(.6f, 2.2f, 4), Vector3.Scale(restoredBox.size, restoredBox.transform.lossyScale));
        }

        // 창의 임시 규격 입력은 자산을 바꾸지 않고 취소로 현재 규격을 다시 읽는다.
        [Test] public void WindowSizingDraft_CancelAndUndoRefreshAreNonMutating()
        {
            var stage = Stage(); var window = ScriptableObject.CreateInstance<FpsArenaStageWindow>(); owned.Add(window);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FpsArenaStageWindow).GetField("stage", flags).SetValue(window, stage);
            typeof(FpsArenaStageWindow).GetMethod("SyncSizingState", flags).Invoke(window, null);
            typeof(FpsArenaStageWindow).GetField("editWidth", flags).SetValue(window, 30);
            Assert.AreEqual(16, stage.data.width); Assert.IsFalse(EditorUtility.IsDirty(stage));
            typeof(FpsArenaStageWindow).GetMethod("LoadSizeDraft", flags).Invoke(window, null);
            Assert.AreEqual(16, typeof(FpsArenaStageWindow).GetField("editWidth", flags).GetValue(window)); Assert.IsNull(stage.wallDefaults);
        }
    }
}
