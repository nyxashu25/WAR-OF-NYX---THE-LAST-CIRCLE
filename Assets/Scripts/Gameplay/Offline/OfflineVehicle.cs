using UnityEngine;
using WarOfNyx.Core.Vehicles;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>
    /// A drivable vehicle in an offline Ghost Protocol match. The local <see cref="OfflineMatch"/> is the authority for
    /// who sits where, damage, run-overs and destruction (<see cref="VehicleRules"/>); this component simulates the
    /// body with Unity physics: raycast wheels with spring / damper suspension and tyre grip, the shared arcade drive
    /// model (acceleration, braking, reverse, speed-sensitive steering), downforce and self-righting. Visual:
    /// <see cref="VehicleModels"/> (wheels spin, steer and follow the suspension; the steering wheel turns), and a
    /// synthesized engine whose pitch follows the speed.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class OfflineVehicle : MonoBehaviour
    {
        private const float SuspensionTravel = 0.32f;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static AudioClip _engineClip;

        private OfflineMatch _match;
        private Rigidbody _body;
        private Collider[] _colliders;
        private VehicleModel _model;
        private Transform[] _wheels;
        private Transform _steeringWheel;
        private Quaternion _steeringRest;
        private readonly float[] _wheelOffset = new float[4];
        private float _wheelSpin;
        private float _throttle, _steer, _steerSmoothed;
        private bool _handbrake;
        private float _flippedFor;
        private bool _ignoringCharacters;
        private AudioSource _engine;
        private MaterialPropertyBlock _wreckTint;

        public VehicleStats Stats { get; private set; }
        public int TypeIndex { get; private set; }
        public float Health { get; private set; }
        public bool Destroyed { get; private set; }
        public OfflineCombatant[] Occupants { get; private set; }
        public OfflineCombatant Driver => Occupants != null && Occupants.Length > 0 ? Occupants[0] : null;
        public OfflineCombatant LastAttacker { get; set; }
        /// <summary>Signed speed along the vehicle's forward axis (m/s).</summary>
        public float Speed { get; private set; }
        public Vector3 Velocity => _body != null ? _body.linearVelocity : Vector3.zero;
        public bool Grounded { get; private set; }
        public float Yaw => transform.eulerAngles.y;
        public Vector3 Centre => transform.TransformPoint(new Vector3(0f, Stats.Height * 0.45f, 0f));
        public int Kmh => Mathf.RoundToInt(Mathf.Abs(Speed) * 3.6f);
        /// <summary>An empty vehicle holds its brake (parked). Tests switch this off to drive without a rider.</summary>
        public bool HoldBrakeWhenEmpty { get; set; } = true;

        public static OfflineVehicle Spawn(OfflineMatch match, int typeIndex, int paint, Vector3 ground, float yaw)
        {
            var stats = VehicleCatalog.Get(typeIndex) ?? VehicleCatalog.All[0];
            var go = new GameObject(stats.DisplayName);
            go.transform.SetPositionAndRotation(ground + Vector3.up * 0.25f, Quaternion.Euler(0f, yaw, 0f));
            var vehicle = go.AddComponent<OfflineVehicle>();
            vehicle.Init(match, VehicleCatalog.IndexOf(stats.Id), stats, paint);
            return vehicle;
        }

        private void Init(OfflineMatch match, int typeIndex, VehicleStats stats, int paint)
        {
            _match = match;
            TypeIndex = typeIndex;
            Stats = stats;
            Health = stats.MaxHealth;
            Occupants = new OfflineCombatant[stats.Seats];
            _model = VehicleModels.Get(typeIndex, paint);

            _body = gameObject.AddComponent<Rigidbody>();
            _body.mass = stats.Mass;
            _body.linearDamping = 0.05f;
            _body.angularDamping = 1.5f;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // A low centre of mass (below the axles, as arcade vehicles do) keeps the body on its wheels.
            _body.centerOfMass = new Vector3(0f, stats.WheelRadius * 0.55f, 0f);

            _colliders = new Collider[_model.Colliders.Length];
            for (int i = 0; i < _model.Colliders.Length; i++)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = _model.Colliders[i].center;
                box.size = _model.Colliders[i].size;
                _colliders[i] = box;
            }

            if (Application.isBatchMode)
                return;
            var material = CharacterStyler.SharedMaterial;
            Part("Body", transform, _model.Body, material);
            _wheels = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = _model.Wheels[i];
                Part("Tyre", pivot, _model.Wheel, material);
                _wheels[i] = pivot;
            }
            _steeringWheel = new GameObject("Steering").transform;
            _steeringWheel.SetParent(transform, false);
            _steeringWheel.localPosition = _model.SteeringPosition;
            _steeringRest = Quaternion.Euler(_model.SteeringEuler);
            _steeringWheel.localRotation = _steeringRest;
            Part("Wheel", _steeringWheel, _model.Steering, material);

            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = EngineClip();
            _engine.loop = true;
            _engine.spatialBlend = 1f;
            _engine.minDistance = 4f;
            _engine.maxDistance = 90f;
            _engine.rolloffMode = AudioRolloffMode.Linear;
            _engine.dopplerLevel = 0.3f;
            _engine.volume = 0f;
            _engine.Play();
        }

        private static void Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ------------------------------------------------------------------ seats (the match decides; these only record)

        public int SeatOf(OfflineCombatant c)
        {
            for (int i = 0; i < Occupants.Length; i++)
                if (Occupants[i] == c)
                    return i;
            return -1;
        }

        public int FreeSeat()
        {
            var taken = new bool[Occupants.Length];
            for (int i = 0; i < taken.Length; i++)
                taken[i] = Occupants[i] != null;
            return VehicleRules.FreeSeat(taken, Stats.Seats);
        }

        internal void SetOccupant(int seat, OfflineCombatant c)
        {
            if (seat >= 0 && seat < Occupants.Length)
                Occupants[seat] = c;
            if (c == null && seat == 0)
                SetInput(0f, 0f, false);
        }

        public bool HasOccupants
        {
            get
            {
                foreach (var o in Occupants)
                    if (o != null)
                        return true;
                return false;
            }
        }

        /// <summary>Seat position in the world (hip height) for a seat index.</summary>
        public Vector3 SeatPosition(int seat) => transform.TransformPoint(_model.Seats[Mathf.Clamp(seat, 0, _model.Seats.Length - 1)]);

        /// <summary>Where an occupant steps out: beside their seat, on the ground.</summary>
        public Vector3 ExitPosition(int seat)
        {
            var local = _model.Seats[Mathf.Clamp(seat, 0, _model.Seats.Length - 1)];
            float side = local.x <= 0f ? -1f : 1f;
            var point = transform.TransformPoint(new Vector3(side * (Stats.Width * 0.5f + 0.7f), 0f, local.z));
            if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore) && !IsMine(hit.collider))
                point = hit.point;
            return point + Vector3.up * 0.1f;
        }

        public bool IsMine(Collider c)
        {
            if (c == null || _colliders == null)
                return false;
            foreach (var mine in _colliders)
                if (mine == c)
                    return true;
            return false;
        }

        /// <summary>Driver input (throttle -1..1, steer -1..1, handbrake), applied by the physics step.</summary>
        public void SetInput(float throttle, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _handbrake = handbrake;
        }

        /// <summary>Is a world point inside the body (expanded by <paramref name="margin"/>)? Used for run-overs.</summary>
        public bool Overlaps(Vector3 point, float margin)
        {
            var local = transform.InverseTransformPoint(point);
            foreach (var b in _model.Colliders)
            {
                var e = b.extents + Vector3.one * margin;
                var d = local - b.center;
                if (Mathf.Abs(d.x) <= e.x && Mathf.Abs(d.z) <= e.z && local.y <= b.center.y + e.y + 0.6f && local.y >= -0.3f)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ damage (applied by the match)

        internal void TakeDamage(float amount)
        {
            if (Destroyed || amount <= 0f)
                return;
            Health = Mathf.Max(0f, Health - amount);
        }

        internal void MarkDestroyed()
        {
            Destroyed = true;
            SetInput(0f, 0f, true);
            if (_engine != null)
                _engine.Stop();
            // Burnt-out wreck.
            _wreckTint ??= new MaterialPropertyBlock();
            _wreckTint.SetColor("_BaseColor", new Color(0.16f, 0.14f, 0.13f));
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.SetPropertyBlock(_wreckTint);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_match == null || Destroyed)
                return;
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            if (impact >= VehicleRules.CrashMinSpeed)
                _match.VehicleCrashed(this, impact);
        }

        // ------------------------------------------------------------------ physics

        private void FixedUpdate()
        {
            if (_body == null)
                return;
            float dt = Time.fixedDeltaTime;
            var up = transform.up;
            float r = Stats.WheelRadius;
            // Mounts sit 45 % of the travel above the modelled wheel centre and the springs compress 55 % under the
            // vehicle's weight, so at rest the wheels sit exactly where the model puts them.
            float k = Stats.Mass * 9.81f / 4f / (SuspensionTravel * 0.55f);
            float c = 2f * Mathf.Sqrt(k * Stats.Mass / 4f) * 0.35f;
            int grounded = 0;
            var groundNormal = Vector3.zero;
            var compression = new float[4];
            float steerAngle = VehicleRules.SteerAngle(_steerSmoothed, Speed, Stats);
            _steerSmoothed = Mathf.MoveTowards(_steerSmoothed, _steer, dt * 3f);

            for (int i = 0; i < 4; i++)
            {
                var mount = transform.TransformPoint(_model.Wheels[i] + Vector3.up * (SuspensionTravel * 0.45f));
                float length = SuspensionTravel + r;
                if (!CastWheel(mount, -up, length + 0.05f, out var hit))
                {
                    _wheelOffset[i] = Mathf.MoveTowards(_wheelOffset[i], -SuspensionTravel * 0.55f, dt * 2f);
                    continue;
                }
                grounded++;
                groundNormal += hit.normal;
                compression[i] = Mathf.Clamp(length - hit.distance, 0f, SuspensionTravel);
                _wheelOffset[i] = SuspensionTravel * 0.45f + r - hit.distance;
                var pointVelocity = _body.GetPointVelocity(hit.point);
                float spring = compression[i] * k - Vector3.Dot(pointVelocity, up) * c;
                _body.AddForceAtPosition(up * Mathf.Max(0f, spring), mount);

                // Tyre grip: cancel sliding across the wheel (front wheels turned by the steering). Applied at the
                // centre-of-mass height so cornering does not lever the body over.
                var wheelRight = i < 2 ? Quaternion.AngleAxis(steerAngle, up) * transform.right : transform.right;
                float slide = Vector3.Dot(pointVelocity, wheelRight);
                float maxGrip = Stats.Mass * 0.25f * 9.81f * 1.2f;
                float grip = Mathf.Clamp(-slide * Stats.Mass * 0.25f / dt * 0.35f, -maxGrip, maxGrip);
                var gripPoint = transform.TransformPoint(new Vector3(_model.Wheels[i].x, _body.centerOfMass.y, _model.Wheels[i].z));
                _body.AddForceAtPosition(wheelRight * grip, gripPoint);
            }
            Grounded = grounded >= 2;
            // Anti-roll bars: each axle resists one side compressing more than the other.
            for (int axle = 0; axle < 2; axle++)
            {
                int left = axle * 2, right = left + 1;
                float roll = (compression[left] - compression[right]) * k * 0.9f;
                if (Mathf.Abs(roll) < 1e-3f)
                    continue;
                // The more compressed (low) side is pushed up, the other side down.
                _body.AddForceAtPosition(up * roll, transform.TransformPoint(_model.Wheels[left]));
                _body.AddForceAtPosition(-up * roll, transform.TransformPoint(_model.Wheels[right]));
            }
            Speed = Vector3.Dot(_body.linearVelocity, transform.forward);

            bool parked = (Driver == null && HoldBrakeWhenEmpty) || Destroyed;
            if (Grounded)
            {
                // Drive: the shared arcade model decides the new forward speed; the body is pushed toward it.
                var normal = groundNormal.normalized;
                var forward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
                float target = VehicleRules.Accelerate(Speed, parked ? 0f : _throttle, parked || _handbrake, Stats, dt);
                _body.AddForce(forward * ((target - Speed) * Stats.Mass / dt) * (grounded / 4f), ForceMode.Force);

                // Steering: a bicycle-model yaw rate, blended in.
                var local = transform.InverseTransformDirection(_body.angularVelocity);
                float yawRate = VehicleRules.YawRate(Speed, steerAngle, Stats) * Mathf.Deg2Rad;
                local.y = Mathf.Lerp(local.y, yawRate, 0.35f);
                _body.angularVelocity = transform.TransformDirection(local);

                // Downforce keeps fast vehicles planted.
                _body.AddForce(-up * Mathf.Abs(Speed) * Stats.Mass * 0.06f);
            }
            else
            {
                // In the air: gently level out so jumps land on the wheels.
                var levelling = Vector3.Cross(up, Vector3.up);
                _body.AddTorque(levelling * Stats.Mass * 2.5f);
            }

            // Self-righting: stuck on the roof or side for 2.5 s at walking pace.
            if (up.y < 0.3f && _body.linearVelocity.magnitude < 2f)
            {
                _flippedFor += dt;
                if (_flippedFor > 2.5f && !Destroyed)
                {
                    _flippedFor = 0f;
                    _body.position += Vector3.up * 1.2f;
                    _body.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
            }
            else
                _flippedFor = 0f;

            // Moving vehicles pass through characters (run-overs are resolved by the match); parked ones block them.
            bool ignore = Mathf.Abs(Speed) > 3f;
            if (ignore != _ignoringCharacters && _match != null)
            {
                _ignoringCharacters = ignore;
                foreach (var combatant in _match.Combatants)
                    if (combatant != null && combatant.Movement != null && combatant.Movement.Controller != null)
                        foreach (var mine in _colliders)
                            Physics.IgnoreCollision(mine, combatant.Movement.Controller, ignore);
            }
        }

        private bool CastWheel(Vector3 origin, Vector3 direction, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, length, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (IsMine(Hits[i].collider) || Hits[i].distance >= nearest)
                    continue;
                nearest = Hits[i].distance;
                best = Hits[i];
                found = true;
            }
            return found;
        }

        // ------------------------------------------------------------------ presentation

        private void LateUpdate()
        {
            if (Stats == null)
                return;
            float dt = Time.deltaTime;
            if (_wheels != null)
            {
                _wheelSpin += Speed * dt / Mathf.Max(0.1f, Stats.WheelRadius) * Mathf.Rad2Deg;
                float steerVisual = VehicleRules.SteerAngle(_steerSmoothed, Speed, Stats);
                for (int i = 0; i < 4; i++)
                {
                    var rest = _model.Wheels[i];
                    _wheels[i].localPosition = rest + Vector3.up * Mathf.Clamp(_wheelOffset[i], -SuspensionTravel, SuspensionTravel);
                    _wheels[i].localRotation = Quaternion.Euler(0f, i < 2 ? steerVisual : 0f, 0f) * Quaternion.Euler(_wheelSpin, 0f, 0f);
                }
            }
            if (_steeringWheel != null)
                _steeringWheel.localRotation = _steeringRest * Quaternion.Euler(0f, -_steerSmoothed * (TypeIndex == 2 ? 25f : 140f), 0f);

            // Occupants ride in their seats.
            for (int i = 0; i < Occupants.Length; i++)
            {
                var o = Occupants[i];
                if (o != null && o.Alive)
                    o.PlaceInSeat(SeatPosition(i) - transform.up * CharacterSeatDrop, transform.rotation);
            }

            if (_engine != null)
            {
                bool running = Driver != null && !Destroyed;
                float load = Mathf.Abs(Speed) / Mathf.Max(1f, Stats.TopSpeed);
                _engine.volume = Mathf.MoveTowards(_engine.volume, running ? 0.35f + 0.35f * Mathf.Abs(_throttle) : 0f, dt * 1.5f);
                _engine.pitch = Mathf.Lerp(_engine.pitch, (TypeIndex == 1 ? 0.55f : TypeIndex == 2 ? 0.9f : 0.7f) + load * 1.3f + Mathf.Abs(_throttle) * 0.1f, 1f - Mathf.Exp(-4f * dt));
            }
        }

        /// <summary>
        /// The rider's pose for a seat: where the feet rest (relative to the character root), and for the driver the
        /// hands on the steering wheel / handlebars, turning with the steering (passengers rest their hands).
        /// </summary>
        public void PoseRider(OfflineCombatant rider, int seat, out Vector2 foot, out Vector3? left, out Vector3? right)
        {
            left = right = null;
            foot = TypeIndex == 2 ? new Vector2(0.5f, 0.12f) : seat >= 2 ? new Vector2(0.72f, 0.42f) : new Vector2(0.78f, 0.55f);
            if (seat != 0 || _steeringWheel == null)
                return;
            float grip = TypeIndex == 2 ? 0.36f : TypeIndex == 1 ? 0.19f : 0.16f;
            left = _steeringWheel.TransformPoint(new Vector3(-grip, 0.02f, TypeIndex == 2 ? -0.04f : 0f));
            right = _steeringWheel.TransformPoint(new Vector3(grip, 0.02f, TypeIndex == 2 ? -0.04f : 0f));
        }

        /// <summary>Smoothed steering input (-1..1), for the rider's arms and the HUD.</summary>
        public float Steer => _steerSmoothed;

        /// <summary>How far below the seat (hip height) the character's root sits.</summary>
        public const float CharacterSeatDrop = 0.95f;

        /// <summary>A looping engine note: a low sawtooth fundamental with harmonics, a firing-pulse rumble and a little noise.</summary>
        private static AudioClip EngineClip()
        {
            if (_engineClip != null)
                return _engineClip;
            const int rate = 22050;
            const float seconds = 1f;
            var data = new float[(int)(rate * seconds)];
            var random = new System.Random(5);
            float noise = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                // 60 Hz fundamental: a whole number of cycles per second, so the loop is seamless.
                float phase = t * 60f;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 30f)), 6f);
                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.2f);
                data[i] = saw * 0.28f + Mathf.Sin(t * Mathf.PI * 2f * 120f) * 0.18f + Mathf.Sin(t * Mathf.PI * 2f * 180f) * 0.08f + pulse * 0.35f + noise * 0.06f;
            }
            _engineClip = AudioClip.Create("VehicleEngine", data.Length, 1, rate, false);
            _engineClip.SetData(data, 0);
            return _engineClip;
        }
    }
}
