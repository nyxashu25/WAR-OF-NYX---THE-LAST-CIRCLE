using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WarOfNyx.Characters;
using WarOfNyx.Core.Movement;
using WarOfNyx.Gameplay;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Phase 2 acceptance tests with real Unity physics (CharacterController + colliders):
    /// the shared movement model drives an actual capsule through test geometry.
    /// Each test builds its own tiny world and steps the simulation directly at 60 Hz.
    /// </summary>
    public class PlayerMovementPlayModeTests
    {
        private const float Dt = 1f / 60f;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator EmptyWorld() => TestWorld.Fresh();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
        }

        private GameObject Box(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = size;
            _spawned.Add(go);
            return go;
        }

        private PlayerMovement Player(Vector3 position)
        {
            var go = new GameObject("TestPlayer");
            go.SetActive(false);
            go.AddComponent<CharacterController>();
            var movement = go.AddComponent<PlayerMovement>();
            go.SetActive(true);
            movement.Teleport(position, 0f);
            _spawned.Add(go);
            Physics.SyncTransforms();
            return movement;
        }

        private static void Run(PlayerMovement movement, MovementCommand command, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
                movement.Simulate(command, Dt);
        }

        private void Floor() => Box(new Vector3(0f, -0.5f, 0f), new Vector3(100f, 1f, 100f));

        [UnityTest]
        public IEnumerator WalksAtWalkSpeedOnRealGround()
        {
            Floor();
            var player = Player(new Vector3(0f, 0.05f, 0f));
            yield return null;

            Run(player, default, 0.3f); // settle onto the floor
            Assert.IsTrue(player.Controller.isGrounded, "should be grounded on the floor");

            float startZ = player.transform.position.z;
            Run(player, new MovementCommand { MoveY = 1f }, 2f);
            float travelled = player.transform.position.z - startZ;

            // 2 s at 4 m/s minus the ~0.1 s acceleration ramp.
            Assert.AreEqual(7.8f, travelled, 0.3f);
            Assert.AreEqual(0f, player.transform.position.y, 0.1f, "stays on the floor");
        }

        [UnityTest]
        public IEnumerator WallsBlockMovementAndKillSpeed()
        {
            Floor();
            Box(new Vector3(0f, 1.5f, 3f), new Vector3(10f, 3f, 1f));
            var player = Player(new Vector3(0f, 0.05f, 0f));
            yield return null;

            Run(player, new MovementCommand { MoveY = 1f, SprintHeld = true }, 2f);

            Assert.Less(player.transform.position.z, 2.5f - 0.3f, "did not pass through the wall");
            Assert.Less(player.State.HorizontalSpeed, 1f, "speed does not build up against the wall");
        }

        [UnityTest]
        public IEnumerator JumpClearsTheConfiguredHeight()
        {
            Floor();
            var player = Player(new Vector3(0f, 0.05f, 0f));
            yield return null;
            Run(player, default, 0.3f);

            player.Simulate(new MovementCommand { JumpPressed = true }, Dt);
            float maxY = 0f;
            for (int i = 0; i < 120; i++)
            {
                player.Simulate(default, Dt);
                maxY = Mathf.Max(maxY, player.transform.position.y);
            }

            Assert.AreEqual(1.5f, maxY, 0.15f);
            Assert.IsTrue(player.Controller.isGrounded, "landed again");
        }

        [UnityTest]
        public IEnumerator CrouchFitsThroughLowTunnelAndStandingDoesNot()
        {
            Floor();
            // 1.4 m clearance tunnel starting at z = 3
            Box(new Vector3(0f, 1.55f, 8f), new Vector3(4f, 0.3f, 10f));
            Box(new Vector3(-1.4f, 0.7f, 8f), new Vector3(0.4f, 1.4f, 10f));
            Box(new Vector3(1.4f, 0.7f, 8f), new Vector3(0.4f, 1.4f, 10f));

            var standing = Player(new Vector3(0f, 0.05f, 0f));
            yield return null;
            Run(standing, new MovementCommand { MoveY = 1f }, 3f);
            Assert.Less(standing.transform.position.z, 3f, "a standing capsule cannot enter");

            standing.Teleport(new Vector3(0f, 0.05f, 0f), 0f);
            Physics.SyncTransforms();
            Run(standing, default, 0.2f);
            standing.Simulate(new MovementCommand { CrouchPressed = true }, Dt);
            Run(standing, new MovementCommand { MoveY = 1f }, 4f);
            Assert.Greater(standing.transform.position.z, 9f, "a crouched capsule passes through");

            // Trying to stand up inside the tunnel is refused.
            standing.Simulate(new MovementCommand { CrouchPressed = true }, Dt);
            Run(standing, default, 0.3f);
            Assert.AreEqual(LocomotionMode.Crouching, standing.State.Mode);
        }

        [UnityTest]
        public IEnumerator SlideCoversMoreGroundThanWalking()
        {
            Floor();
            var player = Player(new Vector3(0f, 0.05f, -40f));
            yield return null;
            Run(player, new MovementCommand { MoveY = 1f, SprintHeld = true }, 1f);

            float start = player.transform.position.z;
            player.Simulate(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true }, Dt);
            Assert.AreEqual(LocomotionMode.Sliding, player.State.Mode);
            Run(player, new MovementCommand { MoveY = 1f, SprintHeld = true }, 0.65f);

            float slideDistance = player.transform.position.z - start;
            Assert.Greater(slideDistance, 4f * 0.7f, "slide is faster than walking over the same time");
            Assert.AreEqual(1.15f, player.CurrentHeight, 0.01f, "capsule is low while sliding");
        }

        [UnityTest]
        public IEnumerator PracticeArenaSpawnsAControllablePlayer()
        {
            if (!Application.CanStreamedLevelBeLoaded("PracticeArena"))
                Assert.Ignore("PracticeArena is not in Build Settings. Run War of Nyx > Setup > Run Project Setup.");

            yield return SceneManager.LoadSceneAsync("PracticeArena", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var arena = Object.FindAnyObjectByType<PracticeArenaController>();
            Assert.IsNotNull(arena, "arena controller missing");
            Assert.IsNotNull(arena.Player, "player was not spawned");
            Assert.IsNotNull(Object.FindAnyObjectByType<ThirdPersonCamera>(), "camera missing");

            // Let it fall onto the spawn pad and make sure it is standing on geometry.
            for (int i = 0; i < 60; i++)
                yield return null;
            Assert.IsTrue(arena.Player.Movement.Controller.isGrounded, "player is grounded in the arena");
            Assert.Greater(arena.Player.transform.position.y, -1f, "did not fall through the floor");
        }
    }
}
