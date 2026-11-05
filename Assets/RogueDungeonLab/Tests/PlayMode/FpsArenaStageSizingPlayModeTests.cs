using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaStageSizingPlayModeTests
    {
        // 저장된 미터 규격과 벽 치수는 제작 기본값과 무관하게 반복 Play 로드의 Collider에 반영된다.
        [UnityTest] public IEnumerator AuthoredDimensions_LoadExactColliders_AndMetadataCannotOverrideThem()
        {
            var stage = ScriptableObject.CreateInstance<FpsArenaStageAsset>(); stage.data = FpsArenaStageData.Create(18, 16, 2, 4, 5);
            stage.data.partitions.Add(new FpsArenaRoomWall { id = "metric-boundary", cell = new Vector3Int(-1, 0, 3), alongX = false, lengthCells = 1, height = 2.2f, thickness = .6f });
            stage.wallDefaults = new FpsArenaWallAuthoringDefaults { boundaryHeight = 1, boundaryThickness = .1f };
            var host = new GameObject("Authored metric Play test"); host.SetActive(false); var generator = host.AddComponent<FpsArenaGenerator>(); generator.manualStage = stage;
            try
            {
                host.SetActive(true); yield return null; Physics.SyncTransforms();
                var box = generator.GeneratedRoot.transform.Find("metric-boundary").GetComponent<BoxCollider>();
                Assert.That(Vector3.Distance(new Vector3(.6f, 2.2f, 4), box.bounds.size), Is.LessThan(.0001f));
                string hash = generator.CurrentLayout.Hash; stage.wallDefaults.boundaryHeight = 3; generator.Generate(); yield return null; Physics.SyncTransforms();
                Assert.AreEqual(hash, generator.CurrentLayout.Hash); Assert.AreEqual(1, host.transform.childCount);
                Assert.That(Vector3.Distance(new Vector3(.6f, 2.2f, 4), generator.GeneratedRoot.transform.Find("metric-boundary").GetComponent<BoxCollider>().bounds.size), Is.LessThan(.0001f));
                Assert.AreEqual(4, generator.CurrentLayout.Recipe.cellSize); Assert.AreEqual(5, generator.CurrentLayout.Recipe.floorHeight);
                Assert.AreEqual(1, generator.CurrentLayout.BoundaryWalls.Count); LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); Object.Destroy(stage); }
            yield return null;
        }
    }
}
