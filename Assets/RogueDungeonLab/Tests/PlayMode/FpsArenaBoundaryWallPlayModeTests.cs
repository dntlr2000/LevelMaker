using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaBoundaryWallPlayModeTests
    {
        // 서쪽/남쪽 삭제 구간은 실제로 통과하고 남아 있는 외곽벽은 캐릭터를 차단한다.
        [UnityTest] public IEnumerator DeletedWestAndSouthWalls_StayOpenAfterRepeatedPlayLoad()
        {
            var stage = ScriptableObject.CreateInstance<FpsArenaStageAsset>(); stage.data.AddMissingBoundaryWalls();
            stage.data.partitions.RemoveAll(w => w.cell == new Vector3Int(-1, 0, 5) && !w.alongX || w.cell == new Vector3Int(5, 0, -1) && w.alongX);
            var host = new GameObject("Open boundary Play test"); host.SetActive(false);
            var generator = host.AddComponent<FpsArenaGenerator>(); generator.manualStage = stage;
            var player = new GameObject("Boundary collision capsule"); var cc = Configure(player);
            try
            {
                host.SetActive(true); yield return null; string hash = generator.CurrentLayout.Hash;
                generator.Generate(); yield return null; Physics.SyncTransforms();
                Assert.AreEqual(hash, generator.CurrentLayout.Hash); Assert.AreEqual(62, generator.CurrentLayout.BoundaryWalls.Count); Assert.AreEqual(1, host.transform.childCount);
                var layout = generator.CurrentLayout;
                Vector3 westOpen = layout.Position(new Vector3Int(0, 0, 5)); Teleport(cc, westOpen); Walk(cc, Vector3.left, 3);
                Assert.Less(cc.transform.position.x, westOpen.x - 2.8f);
                Vector3 westClosed = layout.Position(new Vector3Int(0, 0, 6)); Teleport(cc, westClosed); Walk(cc, Vector3.left, 3);
                Assert.Greater(cc.transform.position.x, westClosed.x - 1.3f);
                Vector3 southOpen = layout.Position(new Vector3Int(5, 0, 0)); Teleport(cc, southOpen); Walk(cc, Vector3.back, 3);
                Assert.Less(cc.transform.position.z, southOpen.z - 2.8f);
                Vector3 southClosed = layout.Position(new Vector3Int(6, 0, 0)); Teleport(cc, southClosed); Walk(cc, Vector3.back, 3);
                Assert.Greater(cc.transform.position.z, southClosed.z - 1.3f);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); Object.Destroy(player); Object.Destroy(stage); }
            yield return null;
        }

        // v1 입력을 바꾸지 않고 기존 외곽을 복원해도 계단 두 lane의 왕복 이동이 유지된다.
        [UnityTest] public IEnumerator LegacyManualLoad_PreservesBoundaryAndStairTraversalWithoutMutatingInput()
        {
            var stage = ScriptableObject.CreateInstance<FpsArenaStageAsset>(); stage.data = FpsArenaStageData.Create(16, 16, 2);
            var stair = new FpsArenaStair { x = 5, z = 5, lowerFloor = 0, width = 2, length = 4, direction = FpsArenaStairDirection.Right };
            stage.data.stairs.Add(stair);
            for (int f = 0; f < 2; f++) for (int lane = 0; lane < 2; lane++) for (int step = 0; step < 4; step++) stage.data.floorCells.Remove(stair.Cell(lane, step, f));
            stage.data.version = 1; string before = JsonUtility.ToJson(stage.data);
            var host = new GameObject("Legacy boundary Play test"); host.SetActive(false);
            var generator = host.AddComponent<FpsArenaGenerator>(); generator.manualStage = stage;
            var player = new GameObject("Legacy boundary capsule"); var cc = Configure(player);
            try
            {
                host.SetActive(true); yield return null; Physics.SyncTransforms();
                Assert.AreEqual(before, JsonUtility.ToJson(stage.data)); Assert.AreEqual(128, generator.CurrentLayout.BoundaryWalls.Count);
                var layout = generator.CurrentLayout;
                for (int lane = 0; lane < 2; lane++)
                {
                    Vector3 start = layout.Position(stair.BottomLane(lane)), end = layout.Position(stair.TopLane(lane));
                    Teleport(cc, start); Walk(cc, (Vector3)stair.Forward, 15);
                    Assert.That(Vector3.Distance(cc.transform.position, end), Is.LessThan(.25f));
                    Walk(cc, -(Vector3)stair.Forward, 15); Assert.That(Vector3.Distance(cc.transform.position, start), Is.LessThan(.25f));
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); Object.Destroy(player); Object.Destroy(stage); }
            yield return null;
        }

        // 검증 캡슐은 기존 FPS 이동 검사와 같은 충돌 치수를 사용한다.
        private static CharacterController Configure(GameObject player)
        {
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f;
            cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f; return cc;
        }

        // 순간 이동 직후 실제 PhysX 위치를 동기화한다.
        private static void Teleport(CharacterController cc, Vector3 p)
        { cc.enabled = false; cc.transform.position = p + Vector3.up * .05f; cc.enabled = true; Physics.SyncTransforms(); }

        // 일정 스텝의 CharacterController.Move로 벽 통과/차단과 계단 이동을 확인한다.
        private static void Walk(CharacterController cc, Vector3 direction, float distance)
        {
            float vertical = 0; int steps = Mathf.CeilToInt(distance / .05f);
            for (int i = 0; i < steps + 20; i++)
            { if (cc.isGrounded && vertical < 0) vertical = -2; vertical -= 22f / 60; cc.Move(direction * (i < steps ? .05f : 0) + Vector3.up * (vertical / 60)); }
        }
    }
}
