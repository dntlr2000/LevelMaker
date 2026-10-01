using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaTraversalTests
    {
        [UnityTest]
        public IEnumerator CharacterController_ClimbsAndDescendsBothExtremeStairProfiles()
        {
            foreach (var profile in new[] { new Vector2(2, 6), new Vector2(5, 3) })
            {
                var l = FpsArenaPlanner.Generate(new FpsArenaRecipe { width = 21, depth = 64, shape = FpsArenaShape.Ellipse, floors = 2, cellSize = profile.x, floorHeight = profile.y, coverPerFloor = 0, obstaclesPerFloor = 0, itemsPerFloor = 0 }, 73125);
                GameObject root = FpsArenaSceneBuilder.Build(l, null); root.SetActive(true);
                var player = new GameObject("Traversal capsule"); var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = .35f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f;
                try
                {
                    Physics.SyncTransforms();
                    foreach (var stair in l.Stairs)
                    {
                        // Both lanes, including the formerly vulnerable outer ellipse lane.
                        for (int lane = 0; lane < stair.width; lane++)
                        {
                        Teleport(cc, l.Position(stair.BottomLane(lane)) + Vector3.up * .05f);
                        yield return Walk(cc, (Vector3)stair.Forward, (stair.length + 1) * l.Recipe.cellSize);
                            Assert.That(player.transform.position.y, Is.EqualTo(l.Recipe.floorHeight).Within(.15f), "Ascent did not reach upper landing.");
                        yield return Walk(cc, -(Vector3)stair.Forward, (stair.length + 1) * l.Recipe.cellSize);
                            Assert.That(player.transform.position.y, Is.EqualTo(0).Within(.15f), "Descent did not reach lower landing.");
                        }
                    }
                }
                finally { Object.Destroy(player); Object.Destroy(root); }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator FlexibleSeededStairs_ClimbDescendBothLanesOnAllFloors()
        {
            foreach (int seed in new[] { -7, 71 })
            {
                var r = FpsArenaRecipe.CreateFlexible(); r.width = 20; r.depth = 20; r.shape = FpsArenaShape.Ellipse; r.floors = 4; r.cellSize = 2; r.floorHeight = 6;
                r.coverDensity = r.enemyDensity = r.gimmickDensity = r.itemDensity = 0;
                r.internalWalls = true; r.wallDensity = .35f;
                var l = FpsArenaPlanner.Generate(r, seed); var root = FpsArenaSceneBuilder.Build(l, null); root.SetActive(true);
                var player = new GameObject("V2 traversal capsule"); var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = .35f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f;
                try
                {
                    Physics.SyncTransforms();
                    foreach (var stair in l.Stairs) for (int lane = 0; lane < stair.width; lane++)
                    {
                        Teleport(cc, l.Position(stair.BottomLane(lane)) + Vector3.up * .05f);
                        yield return Walk(cc, (Vector3)stair.Forward, (stair.length + 1) * r.cellSize);
                        Assert.That(player.transform.position.y, Is.EqualTo((stair.lowerFloor + 1) * r.floorHeight).Within(.15f), "V2 ascent failed at seed/floor/lane " + seed + "/" + stair.lowerFloor + "/" + lane);
                        yield return Walk(cc, -(Vector3)stair.Forward, (stair.length + 1) * r.cellSize);
                        Assert.That(player.transform.position.y, Is.EqualTo(stair.lowerFloor * r.floorHeight).Within(.15f), "V2 descent failed at seed/floor/lane " + seed + "/" + stair.lowerFloor + "/" + lane);
                    }
                }
                finally { Object.Destroy(player); Object.Destroy(root); }
                yield return null;
            }
        }
        private static void Teleport(CharacterController cc, Vector3 p)
        { cc.enabled = false; cc.transform.position = p; cc.enabled = true; Physics.SyncTransforms(); }
        private static IEnumerator Walk(CharacterController cc, Vector3 direction, float distance)
        {
            float vertical = 0, advanced = 0;
            for (int i = 0; i < Mathf.CeilToInt(distance / .06f) + 50; i++)
            {
                if (cc.isGrounded && vertical < 0) vertical = -2;
                vertical -= 22f / 60f;
                float move = Mathf.Min(.06f, Mathf.Max(0, distance - advanced)); advanced += move;
                cc.Move(direction * move + Vector3.up * (vertical / 60f));
                yield return null;
            }
        }
    }
}
