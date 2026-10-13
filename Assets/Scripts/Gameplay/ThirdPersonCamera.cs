using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay
{
    /// <summary>
    /// Over-the-shoulder third-person camera (spec 8 / Phase 2). Client-only and purely visual:
    /// it reads the aim angles from the input router and never influences the simulation.
    ///
    /// Features: shoulder offset, crouch-aware pivot height, aim zoom (distance + FOV),
    /// sphere-cast collision so walls never block the view, smoothing that stays responsive.
    /// Online play will feed it the predicted local player (Phase 3).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Framing")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.55f, 0f, 0f);
        [SerializeField] private float pivotHeightStanding = 1.6f;
        [SerializeField] private float pivotHeightCrouched = 1.05f;
        [SerializeField] private float distance = 3.4f;
        [SerializeField] private float aimDistance = 1.8f;

        [Header("Field of view")]
        [SerializeField] private float fieldOfView = 62f;
        [SerializeField] private float aimFieldOfView = 46f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.22f;
        [SerializeField] private float minDistance = 0.4f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Smoothing (higher = snappier)")]
        [SerializeField] private float followSharpness = 25f;
        [SerializeField] private float zoomSharpness = 12f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Camera _camera;
        private Transform _target;
        private PlayerMovement _movement;
        private PlayerInputRouter _input;
        private float _pivotHeight;
        private float _currentDistance;
        private float _aimBlend;
        private Vector3 _smoothedPivot;
        private float _shake;
        private float _shakeTarget;

        public Transform Target => _target;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.1f;
        }

        /// <summary>
        /// Mild camera vibration (outside the Nyx Circle, spec 12). 0 = off, 1 = full. Visual only: it
        /// never changes the aim, and it never turns the camera on its own.
        /// </summary>
        public void SetShake(float amount) => _shakeTarget = Mathf.Clamp01(amount);

        public void Bind(Transform target, PlayerMovement movement, PlayerInputRouter input)
        {
            _target = target;
            _movement = movement;
            _input = input;
            _pivotHeight = pivotHeightStanding;
            _currentDistance = distance;
            _smoothedPivot = target.position + Vector3.up * _pivotHeight;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            float dt = Time.unscaledDeltaTime;
            bool aiming = _input != null && _input.AimHeld;
            bool lowStance = _movement != null && _movement.State.IsLowStance;

            _aimBlend = Mathf.MoveTowards(_aimBlend, aiming ? 1f : 0f, dt * zoomSharpness * 0.5f);
            _pivotHeight = Mathf.Lerp(_pivotHeight, lowStance ? pivotHeightCrouched : pivotHeightStanding, 1f - Mathf.Exp(-10f * dt));

            // Follow the pivot with light smoothing on the vertical axis only, so horizontal aim stays crisp.
            var rawPivot = _target.position + Vector3.up * _pivotHeight;
            _smoothedPivot = new Vector3(rawPivot.x, Mathf.Lerp(_smoothedPivot.y, rawPivot.y, 1f - Mathf.Exp(-followSharpness * dt)), rawPivot.z);

            Place(_smoothedPivot, dt);
            _camera.fieldOfView = Mathf.Lerp(fieldOfView, aimFieldOfView, _aimBlend);
        }

        private void SnapToTarget()
        {
            if (_target == null)
                return;
            Place(_target.position + Vector3.up * _pivotHeight, 0f);
        }

        private void Place(Vector3 pivot, float dt)
        {
            float yaw = _input != null ? _input.Yaw : _target.eulerAngles.y;
            float pitch = _input != null ? _input.Pitch : 10f;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);

            // The shoulder point sits beside the pivot; pull back from there.
            var shoulder = pivot + rotation * shoulderOffset;
            float wanted = Mathf.Lerp(distance, aimDistance, _aimBlend);
            float allowed = CollisionDistance(pivot, shoulder, rotation * Vector3.back, wanted);

            // Pull in instantly when blocked, ease back out when clear (prevents clipping and popping).
            _currentDistance = allowed < _currentDistance || dt <= 0f
                ? allowed
                : Mathf.Lerp(_currentDistance, allowed, 1f - Mathf.Exp(-zoomSharpness * dt));

            var position = shoulder + rotation * Vector3.back * _currentDistance;

            _shake = Mathf.MoveTowards(_shake, _shakeTarget, (dt > 0f ? dt : 1f) * 3f);
            if (_shake > 0.001f)
            {
                // Small, smooth tremor: a few centimetres and a fraction of a degree.
                float t = Time.unscaledTime * 22f;
                position += rotation * new Vector3(Mathf.PerlinNoise(t, 0.2f) - 0.5f, Mathf.PerlinNoise(0.6f, t) - 0.5f, 0f) * (0.06f * _shake);
                rotation *= Quaternion.Euler((Mathf.PerlinNoise(t * 0.7f, 3.1f) - 0.5f) * 0.6f * _shake, 0f,
                    (Mathf.PerlinNoise(5.3f, t * 0.7f) - 0.5f) * 0.8f * _shake);
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private float CollisionDistance(Vector3 pivot, Vector3 shoulder, Vector3 backward, float wanted)
        {
            // First make sure the shoulder itself is not inside a wall next to the player.
            var toShoulder = shoulder - pivot;
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, toShoulder.normalized, _hits,
                toShoulder.magnitude, collisionMask, QueryTriggerInteraction.Ignore);
            if (NearestOtherHit(count, out _))
                return minDistance;

            count = Physics.SphereCastNonAlloc(shoulder, collisionRadius, backward, _hits, wanted, collisionMask,
                QueryTriggerInteraction.Ignore);
            return NearestOtherHit(count, out float hitDistance) ? Mathf.Max(minDistance, hitDistance) : wanted;
        }

        private bool NearestOtherHit(int count, out float nearest)
        {
            nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(_target))
                    continue;
                // distance 0 means the sphere started overlapping; treat as fully blocked.
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    found = true;
                }
            }
            return found;
        }
    }
}
