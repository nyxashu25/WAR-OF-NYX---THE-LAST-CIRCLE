using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// Foot placement on uneven ground, stairs and slopes (section 40.8): after the Animator has posed the body, each
    /// foot is moved to the ground under it (within a small range) with a two-bone solve on the animated leg and tilted
    /// to the slope, and the hips lower so the lower foot can reach. Planted feet take the full correction, swinging feet
    /// less, so steps still clear the ground. Purely visual: the authoritative capsule is untouched. Skipped in the air,
    /// sliding, seated, dead, and beyond 30 m from the camera.
    /// </summary>
    [DefaultExecutionOrder(56)]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterFootIK : MonoBehaviour
    {
        private static readonly int GroundMask = ~((1 << 2) | World.EnvironmentLayers.FoliageMask);
        private const float MaxCameraDistance = 30f;

        private Animator _animator;
        private PlayerMovement _movement;
        private CharacterAnimationController _controller;
        private Transform _hips;
        private readonly Transform[] _upper = new Transform[2], _lower = new Transform[2], _foot = new Transform[2];
        private float _range = 0.35f;
        private float _hipOffset;
        private readonly float[] _footOffset = new float[2];
        private float _weight;

        public void Init(CharacterAnimationProfile profile)
        {
            if (profile != null)
                _range = profile.footIkRange;
        }

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<CharacterAnimationController>();
            if (_animator == null || !_animator.isHuman)
                return;
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _upper[0] = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _lower[0] = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _foot[0] = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _upper[1] = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _lower[1] = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _foot[1] = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void LateUpdate()
        {
            if (_hips == null || _foot[0] == null)
                return;
            if (_movement == null)
                _movement = GetComponentInParent<PlayerMovement>();
            float dt = Time.deltaTime;
            var cam = Camera.main;
            bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < MaxCameraDistance * MaxCameraDistance;
            bool active = near && _movement != null && _movement.State.Mode != LocomotionMode.Airborne && _movement.State.Mode != LocomotionMode.Sliding &&
                          (_controller == null || (!_controller.Seated && !_controller.Dead));
            _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, dt * 6f);
            if (_weight <= 0.001f)
                return;

            float rootY = transform.position.y;
            var animated = new Vector3[2];
            var normals = new Vector3[2];
            float lowest = 0f;
            for (int i = 0; i < 2; i++)
            {
                animated[i] = _foot[i].position;
                float target = 0f;
                normals[i] = Vector3.up;
                if (Physics.Raycast(new Vector3(animated[i].x, rootY + _range + 0.3f, animated[i].z), Vector3.down, out var hit, _range * 2f + 0.3f, GroundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    target = Mathf.Clamp(hit.point.y - rootY, -_range, _range);
                    normals[i] = hit.normal;
                }
                _footOffset[i] = Mathf.Lerp(_footOffset[i], target, 1f - Mathf.Exp(-14f * dt));
                lowest = Mathf.Min(lowest, _footOffset[i]);
            }
            _hipOffset = Mathf.Lerp(_hipOffset, lowest, 1f - Mathf.Exp(-10f * dt));
            _hips.position += Vector3.up * (_hipOffset * _weight);
            for (int i = 0; i < 2; i++)
            {
                float lifted = animated[i].y - rootY;
                float plant = Mathf.InverseLerp(0.16f, 0.09f, lifted);
                float offset = _footOffset[i] * _weight * Mathf.Lerp(0.5f, 1f, plant);
                var knee = (_lower[i].position - _upper[i].position) + (_lower[i].position - _foot[i].position);
                LimbIk.Solve(_upper[i], _lower[i], _foot[i], animated[i] + Vector3.up * offset, knee.sqrMagnitude > 1e-6f ? knee : transform.forward);
                if (plant > 0f)
                    _foot[i].rotation = Quaternion.Slerp(_foot[i].rotation, Quaternion.FromToRotation(Vector3.up, normals[i]) * _foot[i].rotation, plant * _weight * 0.8f);
            }
        }
    }
}
