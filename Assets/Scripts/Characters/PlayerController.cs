using UnityEngine;

namespace WarOfNyx.Characters
{
    /// <summary>
    /// Local human control loop for Phase 2: each frame, take the input router's command
    /// and simulate it on the movement component.
    ///
    /// In Phase 3 this loop moves into Fusion: the router's command becomes the networked
    /// input struct, and <see cref="PlayerMovement.Simulate"/> runs inside FixedUpdateNetwork on
    /// the server (authoritative) and the owning client (prediction).
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInputRouter))]
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>Clamp very long frames (app resume, hitches) so a single step can't tunnel through walls.</summary>
        private const float MaxStepSeconds = 0.05f;

        private PlayerMovement _movement;
        private PlayerInputRouter _input;

        public PlayerMovement Movement => _movement;
        public PlayerInputRouter InputRouter => _input;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _input = GetComponent<PlayerInputRouter>();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxStepSeconds);
            if (dt <= 0f)
                return; // paused (timeScale 0)

            var command = _input.ConsumeCommand();
            _movement.Simulate(command, dt);
        }

        /// <summary>Places the player and points camera/aim in the spawn direction.</summary>
        public void Spawn(Vector3 position, float yaw)
        {
            _movement.Teleport(position, yaw);
            _input.SetAim(yaw, 10f);
        }
    }
}
