using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Movement sounds of a character (visual/audio only): a footstep on every foot contact of the
    /// procedural gait (<see cref="CharacterAnimator.FootContact"/>), chosen by the surface underfoot and
    /// louder / heard further from walk to sprint, plus jump, landing (by fall speed) and slide sounds.
    /// The local player's own sounds are softer; everyone else's are positional, so you can hear them coming.
    /// </summary>
    public sealed class CharacterAudio : MonoBehaviour
    {
        private ICharacterPose _animator;
        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private FootSurface _surface;
        private float _nextSurfaceCheck;

        private bool Own => _router != null && _router.enabled;

        private void OnEnable()
        {
            _animator = GetComponentInChildren<ICharacterPose>(true);
            _movement = GetComponent<PlayerMovement>();
            _router = GetComponent<PlayerInputRouter>();
            if (_animator != null)
                _animator.FootContact += OnFootContact;
            if (_movement != null)
                _movement.Stepped += OnStepped;
        }

        private void OnDisable()
        {
            if (_animator != null)
                _animator.FootContact -= OnFootContact;
            if (_movement != null)
                _movement.Stepped -= OnStepped;
        }

        private FootSurface Surface()
        {
            if (Time.time >= _nextSurfaceCheck)
            {
                _nextSurfaceCheck = Time.time + 0.25f;
                _surface = FootstepAudio.SurfaceAt(transform.position);
            }
            return _surface;
        }

        private void OnFootContact(int side, float intensity)
        {
            if (Application.isBatchMode)
                return;
            var position = transform.position + transform.right * (0.12f * side);
            FootstepAudio.PlayStep(Surface(), intensity, position, Own);
        }

        private void OnStepped(MovementState state)
        {
            if (Application.isBatchMode || !isActiveAndEnabled)
                return;
            if (state.JumpedThisStep)
                FootstepAudio.PlayJump(Surface(), transform.position, Own);
            if (state.LandedThisStep)
                FootstepAudio.PlayLand(Surface(), Mathf.InverseLerp(2f, 12f, -state.VelocityY), transform.position, Own);
            if (state.SlideStartedThisStep)
                FootstepAudio.PlaySlide(transform.position, transform, Own);
        }
    }
}
