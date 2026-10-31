using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaManualStagePlayModeTests
    {
        [UnityTest]
        // 의도적으로 격리된 층과 닫힌 구획도 Play에서 로드·재생성할 수 있는지 검증한다.
        public IEnumerator DisconnectedManualStage_LoadsInPlay_AndRepeatedLoadKeepsOneRoot()
        {
            var stage = ScriptableObject.CreateInstance<FpsArenaStageAsset>();
            stage.data = FpsArenaStageData.Create(16, 16, 2);
            for (int z = 0; z < stage.data.depth; z++) stage.data.partitions.Add(new FpsArenaRoomWall { id = "closed:" + z, cell = new Vector3Int(7, 0, z), lengthCells = 1, height = 3, thickness = .25f });
            var host = new GameObject("Disconnected manual stage Play test"); host.SetActive(false);
            var generator = host.AddComponent<FpsArenaGenerator>(); generator.manualStage = stage; generator.generateOnPlay = true;
            try
            {
                host.SetActive(true); yield return null;
                Assert.IsNotNull(generator.CurrentLayout); Assert.IsTrue(generator.CurrentLayout.IsAuthored);
                StringAssert.Contains("384셀", FpsArenaManualValidation.GetConnectivityWarning(generator.CurrentLayout));
                string hash = generator.CurrentLayout.Hash;
                generator.Generate(); yield return null;
                Assert.AreEqual(hash, generator.CurrentLayout.Hash); Assert.AreEqual(1, host.transform.childCount);
                Assert.DoesNotThrow(generator.CurrentLayout.Validate); LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); Object.Destroy(stage); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedManualStage_RepeatedLoadKeepsOneRoot_AndPlayerTraversesDoorAndStairs()
        {
            var stage = ScriptableObject.CreateInstance<FpsArenaStageAsset>();
            stage.data = FpsArenaStageData.Create(16, 16, 2);
            var stair = new FpsArenaStair { x = 5, z = 5, lowerFloor = 0, length = 4, width = 2, direction = FpsArenaStairDirection.Right };
            stage.data.stairs.Add(stair);
            for (int f = 0; f < 2; f++) for (int lane = 0; lane < 2; lane++) for (int step = 0; step < 4; step++) stage.data.floorCells.Remove(stair.Cell(lane, step, f));
            for (int z = 0; z < 16; z++) if (z != 2) stage.data.partitions.Add(new FpsArenaRoomWall { id = "partition:" + z, cell = new Vector3Int(11, 0, z), lengthCells = 1, height = 3, thickness = .25f });
            var host = new GameObject("Manual stage Play test"); host.SetActive(false);
            var generator = host.AddComponent<FpsArenaGenerator>(); generator.manualStage = stage; generator.generateOnPlay = true;
            var player = new GameObject("Manual traversal capsule"); var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f; cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f;
            try
            {
                host.SetActive(true); yield return null;
                Assert.IsTrue(generator.CurrentLayout.IsAuthored); string hash = generator.CurrentLayout.Hash;
                generator.seed += 100; generator.Generate(); yield return null;
                Assert.AreEqual(hash, generator.CurrentLayout.Hash); Assert.AreEqual(1, host.transform.childCount);
                Physics.SyncTransforms(); var layout = generator.CurrentLayout;
                for (int lane = 0; lane < stair.width; lane++)
                {
                    Vector3 start = layout.Position(stair.BottomLane(lane)), end = layout.Position(stair.TopLane(lane));
                    Teleport(cc, start + Vector3.up * .05f); Walk(cc, (Vector3)stair.Forward, 15);
                    Assert.That(player.transform.position.y, Is.EqualTo(end.y).Within(.15f)); Assert.That(Vector3.Distance(player.transform.position, end), Is.LessThan(.25f));
                    Walk(cc, -(Vector3)stair.Forward, 15); Assert.That(Vector3.Distance(player.transform.position, start), Is.LessThan(.25f));
                }
                var beforeDoor = layout.Position(new Vector3Int(11, 0, 2));
                Teleport(cc, beforeDoor + Vector3.up * .05f); Walk(cc, Vector3.right, 3); Assert.Greater(player.transform.position.x, beforeDoor.x + 2.8f);
                var beforeWall = layout.Position(new Vector3Int(11, 0, 3));
                Teleport(cc, beforeWall + Vector3.up * .05f); Walk(cc, Vector3.right, 3); Assert.Less(player.transform.position.x, beforeWall.x + 1.3f);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); Object.Destroy(player); Object.Destroy(stage); }
            yield return null;
        }
        private static void Teleport(CharacterController cc, Vector3 p) { cc.enabled = false; cc.transform.position = p; cc.enabled = true; Physics.SyncTransforms(); }
        private static void Walk(CharacterController cc, Vector3 direction, float distance)
        {
            float vertical = 0; int steps = Mathf.CeilToInt(distance / .05f);
            for (int i = 0; i < steps + 20; i++) { if (cc.isGrounded && vertical < 0) vertical = -2; vertical -= 22f / 60; cc.Move(direction * (i < steps ? .05f : 0) + Vector3.up * (vertical / 60)); }
        }
    }
}
