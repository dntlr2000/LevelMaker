using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class FpsArenaWallTraversalTests
    {
        [UnityTest]
        public IEnumerator CharacterController_CrossesDoorwaysAndEndBypassesAndStopsAtSolidWalls()
        {
            foreach (int seed in new[] { -7, 73125 })
            {
                var r = FpsArenaRecipe.CreateFlexible(); r.width = r.depth = 32; r.floors = 4; r.spacingCells = 1;
                r.internalWalls = true; r.wallDensity = .25f; r.wallDoorWidthCells = seed == -7 ? 1 : 3;
                var l = FpsArenaPlanner.Generate(r, seed); var root = FpsArenaSceneBuilder.Build(l,null); root.SetActive(true);
                var player = new GameObject("Wall passage capsule"); var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f; cc.stepOffset = .3f; cc.skinWidth = .04f;
                try
                {
                    Assert.IsNotEmpty(l.Walls); Physics.SyncTransforms();
                    foreach (var wall in l.Walls)
                    {
                        Vector3 direction = wall.alongX ? Vector3.forward : Vector3.right;
                        Vector3 door = l.Position(wall.cell + wall.Step * wall.doorOffsetCells);
                        if (wall.doorWidthCells > 0)
                        {
                            for (int i = 0; i < wall.doorWidthCells; i++)
                            {
                                Vector3 target = door + (Vector3)wall.Step * (i * r.cellSize);
                                Teleport(cc, target - direction * r.cellSize + Vector3.up * .05f);
                                Walk(cc, direction, 2 * r.cellSize);
                                Assert.That(Vector3.Dot(cc.transform.position - target,direction), Is.GreaterThan(r.cellSize - .15f), "Door traversal failed: " + wall.id);
                            }
                        }
                        // The ring outside the end must remain traversable, even with other content.
                        Vector3 end = l.Position(wall.cell - wall.Step);
                        Teleport(cc,end - direction * r.cellSize + Vector3.up * .05f); Walk(cc,direction,2 * r.cellSize);
                        Assert.That(Vector3.Dot(cc.transform.position - end,direction), Is.GreaterThan(r.cellSize - .15f), "Wall end bypass blocked: " + wall.id);
                        Vector3 solid = l.Position(wall.cell);
                        Teleport(cc, solid - direction * r.cellSize + Vector3.up * .05f); Walk(cc,direction,2 * r.cellSize);
                        Assert.That(Vector3.Dot(cc.transform.position - solid,direction), Is.LessThan(-wall.thickness * .5f), "Solid wall did not block capsule: " + wall.id);
                        yield return null;
                    }
                }
                finally { Object.Destroy(player); Object.Destroy(root); }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator PhysicsCapsuleCast_AllLogicalNeighborPassagesArePhysicallyClear()
        {
            var r = FpsArenaRecipe.CreateFlexible(); r.width = r.depth = 24; r.floors = 4; r.cellSize = 2; r.floorHeight = 6;
            r.internalWalls = true; r.wallDensity = .35f; r.spacingCells = 1; r.coverDensity = .2f;
            var l = FpsArenaPlanner.Generate(r, 71); var root = FpsArenaSceneBuilder.Build(l, null); root.SetActive(true);
            try
            {
                Physics.SyncTransforms(); var occupied = l.BlockedCells();
                foreach (var p in l.Content) foreach (var cell in p.Footprint()) occupied.Add(cell);
                int passages = 0;
                for (int f = 0; f < r.floors; f++) for (int x = 0; x < r.width; x++) for (int z = 0; z < r.depth; z++)
                {
                    var c = new Vector3Int(x,f,z); if (!l.HasFloor(c) || occupied.Contains(c)) continue;
                    foreach (var step in new[] { Vector3Int.right, new Vector3Int(0,0,1) })
                    {
                        var next = c + step; if (!l.HasFloor(next) || occupied.Contains(next)) continue;
                        Vector3 foot = l.Position(c) + Vector3.up * .06f;
                        Assert.IsFalse(Physics.CapsuleCast(foot + Vector3.up * .35f,foot + Vector3.up * 1.45f,.35f,(Vector3)step,out var hit,r.cellSize,~0,QueryTriggerInteraction.Ignore), "Physical corridor blocked at " + c + " -> " + next + " by " + (hit.collider != null ? hit.collider.name : ""));
                        passages++;
                    }
                }
                Assert.Greater(passages, 1000);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        private static void Teleport(CharacterController cc, Vector3 p)
        { cc.enabled = false; cc.transform.position = p; cc.enabled = true; Physics.SyncTransforms(); }
        private static void Walk(CharacterController cc, Vector3 direction, float distance)
        {
            float vertical = 0;
            for (int i = 0; i < Mathf.CeilToInt(distance / .05f) + 20; i++)
            {
                if (cc.isGrounded && vertical < 0) vertical = -2;
                vertical -= 22f / 60;
                cc.Move(direction * (i < Mathf.CeilToInt(distance / .05f) ? .05f : 0) + Vector3.up * (vertical / 60));
            }
        }
    }
}
