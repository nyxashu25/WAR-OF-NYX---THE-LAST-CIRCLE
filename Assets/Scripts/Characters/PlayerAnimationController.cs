using UnityEngine;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Characters
{
    /// <summary>
    /// Presents the movement state. Purely visual, never affects gameplay.
    ///
    /// When an Animator with a controller is assigned (Phase 10 anime rigs), it drives these
    /// parameters: Speed (float), Grounded (bool), Crouching (bool), Sliding (bool),
    /// Sprinting (bool), VerticalSpeed (float), Jump (trigger), Land (trigger).
    ///
    /// Without one it animates the Phase 2 PLACEHOLDER capsule procedurally (squash on crouch,
    /// lean on sprint/slide, bob while moving, squash on landing), so movement can be read
    /// before real animations exist.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int CrouchingId = Animator.StringToHash("Crouching");
        private static readonly int SlidingId = Animator.StringToHash("Sliding");
        private static readonly int SprintingId = Animator.StringToHash("Sprinting");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int LandId = Animator.StringToHash("Land");

        [SerializeField] private Animator animator;
        [Tooltip("Placeholder body root, scaled/tilted procedurally when no Animator controller is set.")]
        [SerializeField] private Transform placeholderVisual;

        private PlayerMovement _movement;
        private Vector3 _visualBaseScale = Vector3.one;
        private Vector3 _visualBasePosition;
        private float _bobPhase;
        private float _landSquash;
        private float _lean;
        private float _heightRatio = 1f;

        private bool UsesAnimator => animator != null && animator.runtimeAnimatorController != null;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            if (placeholderVisual != null)
            {
                _visualBaseScale = placeholderVisual.localScale;
                _visualBasePosition = placeholderVisual.localPosition;
            }
        }

        private void OnEnable() => _movement.Stepped += OnStepped;

        private void OnDisable() => _movement.Stepped -= OnStepped;

        private void OnStepped(MovementState state)
        {
            if (UsesAnimator)
            {
                animator.SetFloat(SpeedId, state.HorizontalSpeed);
                animator.SetBool(GroundedId, state.Mode != LocomotionMode.Airborne);
                animator.SetBool(CrouchingId, state.Mode == LocomotionMode.Crouching);
                animator.SetBool(SlidingId, state.Mode == LocomotionMode.Sliding);
                animator.SetBool(SprintingId, state.IsSprinting);
                animator.SetFloat(VerticalSpeedId, state.VelocityY);
                if (state.JumpedThisStep) animator.SetTrigger(JumpId);
                if (state.LandedThisStep) animator.SetTrigger(LandId);
                return;
            }

            if (state.LandedThisStep)
                _landSquash = 0.18f;
        }

        private void LateUpdate()
        {
            if (UsesAnimator || placeholderVisual == null)
                return;

            var state = _movement.State;
            var settings = _movement.Settings;
            float dt = Time.deltaTime;

            // Height follows the capsule smoothly (the collider itself changes instantly).
            float targetRatio = MovementModel.CapsuleHeight(state, settings) / settings.standingHeight;
            _heightRatio = Mathf.MoveTowards(_heightRatio, targetRatio, dt * 6f);

            float targetLean = state.Mode == LocomotionMode.Sliding ? -25f
                : state.IsSprinting ? 12f
                : state.Mode == LocomotionMode.Airborne ? 4f
                : 0f;
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-12f * dt));

            float speed = state.HorizontalSpeed;
            float bob = 0f;
            if (state.Mode == LocomotionMode.Standing || state.Mode == LocomotionMode.Crouching)
            {
                _bobPhase += dt * speed * 2.2f;
                bob = Mathf.Abs(Mathf.Sin(_bobPhase)) * Mathf.Min(speed / 6.5f, 1f) * 0.06f;
            }

            _landSquash = Mathf.MoveTowards(_landSquash, 0f, dt * 1.2f);

            float squashY = _heightRatio * (1f - _landSquash);
            float stretchXZ = 1f + _landSquash * 0.5f;
            placeholderVisual.localScale = new Vector3(_visualBaseScale.x * stretchXZ, _visualBaseScale.y * squashY, _visualBaseScale.z * stretchXZ);
            placeholderVisual.localPosition = new Vector3(_visualBasePosition.x, _visualBasePosition.y * squashY + bob, _visualBasePosition.z);
            placeholderVisual.localRotation = Quaternion.Euler(_lean, 0f, 0f);
        }
    }
}
