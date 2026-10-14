using System;
using UnityEngine;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Characters
{
    /// <summary>
    /// Unity physics adapter for the shared <see cref="MovementModel"/>.
    ///
    /// Authority: in Phase 2 the local player simulates itself. From Phase 3 the same
    /// <see cref="Simulate"/> call runs on the server (authoritative) and on the owning client
    /// (prediction, re-simulated on correction). Bots call it with AI-generated commands.
    ///
    /// Per step: read ground and headroom → MovementModel.Step → resize capsule →
    /// CharacterController.Move → feed the collision result back into the state.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;
        [Tooltip("Layers that block standing up (headroom check).")]
        [SerializeField] private LayerMask headroomMask = ~0;
        [SerializeField, Range(0f, 89f)] private float slopeLimit = 45f;
        [SerializeField] private float stepOffset = 0.35f;

        private readonly Collider[] _overlapBuffer = new Collider[8];
        private CharacterController _controller;
        private MovementSettings _settings;
        private MovementState _state;
        private bool _grounded;

        public MovementState State => _state;

        /// <summary>
        /// Grounded result of the last simulated move. Stored (not read live from the
        /// CharacterController) so it can be networked and restored for re-simulation.
        /// </summary>
        public bool IsGrounded => _grounded;
        public MovementSettings Settings => _settings;
        public CharacterController Controller => _controller;
        public float CurrentHeight => _controller != null ? _controller.height : 0f;

        /// <summary>Raised after every simulated step (animation, audio, VFX listen here).</summary>
        public event Action<MovementState> Stepped;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _settings = config != null && config.settings != null ? config.settings : new MovementSettings();

            _controller.radius = _settings.capsuleRadius;
            _controller.slopeLimit = slopeLimit;
            _controller.stepOffset = Mathf.Min(stepOffset, _settings.crouchHeight * 0.5f);
            _controller.skinWidth = 0.04f;
            _controller.minMoveDistance = 0f;
            SetCapsuleHeight(_settings.standingHeight);

            _state = MovementState.Initial(transform.eulerAngles.y);
        }

        /// <summary>Overrides the settings at runtime (tests, debug tuning).</summary>
        public void UseSettings(MovementSettings settings)
        {
            _settings = settings ?? new MovementSettings();
            _controller.radius = _settings.capsuleRadius;
            SetCapsuleHeight(MovementModel.CapsuleHeight(_state, _settings));
        }

        /// <summary>Places the character and resets its movement state (spawn, respawn, reconnect).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            // CharacterController overrides transform changes unless it is disabled during the move.
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = true;
            _state = MovementState.Initial(yaw);
            _grounded = false;
            SetCapsuleHeight(_settings.standingHeight);
        }

        /// <summary>
        /// Rides a vehicle seat: the controller is off (the vehicle carries the character) and the state keeps the
        /// vehicle's heading. Called every frame while seated; <see cref="Teleport"/> steps out.
        /// </summary>
        public void Seat(Vector3 position, Quaternion rotation)
        {
            if (_controller.enabled)
                _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _state = MovementState.Initial(rotation.eulerAngles.y);
            _grounded = true;
        }

        /// <summary>
        /// Rolls the character back to a stored simulation state (networked prediction:
        /// called at the start of every tick, including re-simulated ticks, before <see cref="Simulate"/>).
        /// </summary>
        public void RestoreState(in MovementState state, bool grounded, Vector3 position)
        {
            _state = state;
            _grounded = grounded;
            SetCapsuleHeight(MovementModel.CapsuleHeight(_state, _settings));
            if ((transform.position - position).sqrMagnitude > 1e-10f)
            {
                transform.position = position;
                Physics.SyncTransforms(); // CharacterController must see the restored position
            }
            transform.rotation = Quaternion.Euler(0f, state.Yaw, 0f);
        }

        /// <summary>
        /// Shows a replicated state without simulating it (remote players on a client).
        /// Updates the capsule height and notifies animation.
        /// </summary>
        public void ApplyVisualState(in MovementState state)
        {
            _state = state;
            SetCapsuleHeight(MovementModel.CapsuleHeight(_state, _settings));
            Stepped?.Invoke(_state);
        }

        public void Simulate(in MovementCommand command, float deltaTime)
        {
            if (deltaTime <= 0f || !_controller.enabled)
                return;

            var ground = new GroundInfo(_grounded, !_state.IsLowStance || HasStandingHeadroom());
            _state = MovementModel.Step(_state, command, ground, _settings, deltaTime);

            SetCapsuleHeight(MovementModel.CapsuleHeight(_state, _settings));

            var start = transform.position;
            var velocity = new Vector3(_state.VelocityX, _state.VelocityY, _state.VelocityZ);
            var flags = _controller.Move(velocity * deltaTime);
            _grounded = _controller.isGrounded;
            var actual = (transform.position - start) / deltaTime;
            MovementModel.ApplyCollisionResult(ref _state, actual.x, actual.z, (flags & CollisionFlags.Above) != 0);

            transform.rotation = Quaternion.Euler(0f, _state.Yaw, 0f);
            Stepped?.Invoke(_state);
        }

        /// <summary>True when a standing capsule would fit at the current position.</summary>
        public bool HasStandingHeadroom()
        {
            float radius = _settings.capsuleRadius * 0.95f;
            var origin = transform.position;
            // Only test the part the standing capsule adds above the crouched one.
            var bottom = origin + Vector3.up * Mathf.Max(radius, _settings.crouchHeight - radius);
            var top = origin + Vector3.up * (_settings.standingHeight - radius);

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlapBuffer, headroomMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit != null && hit != _controller && !hit.transform.IsChildOf(transform))
                    return false;
            }
            return true;
        }

        private void SetCapsuleHeight(float height)
        {
            if (Mathf.Approximately(_controller.height, height))
                return;
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }
    }
}
