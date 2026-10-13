using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarOfNyx.Core.Match;
using WarOfNyx.Gameplay;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Phase 4: server spawn validation against real Unity colliders (no navmesh in this test world,
    /// so the validator falls back to physics-only checks and logs a warning).
    /// </summary>
    public class SpawnValidationPlayModeTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null)
                    Object.Destroy(go);
            _objects.Clear();
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            _objects.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator SpawnsAvoidWallsRoofsAndTheVoid()
        {
            LogAssert.ignoreFailingMessages = true; // "no baked NavMesh" warning is expected here

            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f));          // map is 80x80
            Box("Wall", new Vector3(20f, 1.5f, 0f), new Vector3(12f, 3f, 12f));          // solid block
            Box("Roof", new Vector3(-20f, 3.2f, 0f), new Vector3(12f, 0.4f, 12f));       // shelter with open sides

            var areaGo = new GameObject("SpawnArea");
            _objects.Add(areaGo);
            var area = areaGo.AddComponent<ArenaSpawnArea>();
            Physics.SyncTransforms();
            yield return null;

            var rules = new MatchRules { minSpawnSeparation = 20f, separationRelaxSteps = new[] { 12f }, spawnEdgeMargin = 2f };
            var plan = area.PlanSpawns(6, rules, seed: 1234);

            Assert.IsTrue(plan.Success, plan.Failure);
            // Without a navmesh, standing ON TOP of the block or roof is physically valid
            // (reachability is only checked when a NavMesh is baked). Never inside or under them.
            foreach (var p in plan.Points)
            {
                bool overWall = Mathf.Abs(p.X - 20f) < 6.5f && Mathf.Abs(p.Z) < 6.5f;
                bool overRoof = Mathf.Abs(p.X + 20f) < 6.5f && Mathf.Abs(p.Z) < 6.5f;
                if (overWall)
                    Assert.AreEqual(3f, p.Y, 0.05f, $"spawn {p.X},{p.Z} would be inside the wall");
                else if (overRoof)
                    Assert.AreEqual(3.4f, p.Y, 0.05f, $"spawn {p.X},{p.Z} would be under the roof");
                else
                    Assert.AreEqual(0f, p.Y, 0.05f, "open ground is the floor");
                Assert.LessOrEqual(Mathf.Abs(p.X), 40f, "on the map");
                Assert.LessOrEqual(Mathf.Abs(p.Z), 40f, "on the map");
            }
        }

        [UnityTest]
        public IEnumerator ValidatorRejectsPointsWithNoGround()
        {
            LogAssert.ignoreFailingMessages = true;
            Box("SmallFloor", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
            var areaGo = new GameObject("SpawnArea");
            _objects.Add(areaGo);
            var area = areaGo.AddComponent<ArenaSpawnArea>();
            Physics.SyncTransforms();
            yield return null;

            Assert.IsTrue(area.TryValidate(0f, 0f, out float y));
            Assert.AreEqual(0f, y, 0.05f);
            Assert.IsFalse(area.TryValidate(30f, 30f, out _), "outside the floor there is nothing to stand on");
        }
    }
}
