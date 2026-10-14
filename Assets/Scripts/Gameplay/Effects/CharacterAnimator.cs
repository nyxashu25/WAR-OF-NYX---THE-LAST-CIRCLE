using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Procedural animation of a styled character's <see cref="CharacterBody"/> (PLACEHOLDER for authored
    /// animation clips; visual only, never affects gameplay):
    ///  * gait from the character's real motion: idle → walk → run → sprint, with stride length, foot lift,
    ///    body bob, hip sway / twist, forward lean, and strafing / backpedalling legs; feet are placed with
    ///    two-bone IK, so crouching and landing bend the knees instead of squashing the body;
    ///  * crouch, jump (tucked legs), slide (one leg forward, leaning back), landing dip;
    ///  * arms: guns held with both hands (hip, aim-down-sights, sprint "port arms", reload, recoil, healing),
    ///    blades held in guard and swung along each combo attack's own path (<see cref="MeleePattern"/>),
    ///    free arms swinging against the legs;
    ///  * spine and head follow the aim pitch.
    /// Speed comes from position changes, so it works the same for the local player, offline bots and
    /// interpolated online players.
    /// </summary>
    [DefaultExecutionOrder(50)] // after PlayerAnimationController, before WeaponVisualRig (hurt / defeat on the root)
    public sealed class CharacterAnimator : MonoBehaviour
    {
        /// <summary>Aim pitch in degrees (+ = looking down). Set by the owner; the local input router is read automatically.</summary>
        public float AimPitch { get; set; }

        /// <summary>Head turn while free-looking (degrees, + = right); the head follows the camera within its range.</summary>
        public float LookYaw { get; set; }

        /// <summary>Riding a vehicle: the seated pose replaces the gait (set by the owner).</summary>
        public bool Seated { get; set; }

        /// <summary>Seated: where the feet rest, relative to the character root (height, forward) in metres.</summary>
        public Vector2 SeatFoot { get; set; } = new Vector2(0.8f, 0.55f);

        /// <summary>Seated driver: world positions of the hands on the steering wheel / handlebars (null = hands on the knees).</summary>
        public Vector3? HandTargetL { get; set; }
        public Vector3? HandTargetR { get; set; }

        /// <summary>A foot touched the ground: side (-1 left, +1 right) and intensity (0.2 crouch-walk … 0.95 sprint).</summary>
        public event System.Action<int, float> FootContact;

        private readonly float[] _lastFootCos = { 1f, 1f };

        private CharacterBody _body;
        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private WeaponVisualRig _rig;

        private Vector3 _lastPosition;
        private bool _hasLast;
        private Vector3 _localVelocity;
        private float _phase;
        private float _moveBlend, _runBlend, _sprintBlend, _crouchBlend, _airBlend, _slideBlend, _aimBlend;
        private float _landDip;
        private float _breath;
        /// <summary>Torso twist and forward bend driven by the current melee swing (degrees).</summary>
        private float _swingTwist, _swingBend;

        private void OnEnable()
        {
            _movement = GetComponent<PlayerMovement>();
            _router = GetComponent<PlayerInputRouter>();
            if (_movement != null)
                _movement.Stepped += OnStepped;
        }

        private void OnDisable()
        {
            if (_movement != null)
                _movement.Stepped -= OnStepped;
        }

        private void OnStepped(MovementState state)
        {
            if (state.LandedThisStep)
                _landDip = Mathf.Clamp(-state.VelocityY * 0.012f + 0.06f, 0.06f, 0.16f);
        }

        private void LateUpdate()
        {
            if (_body == null)
            {
                _body = GetComponentInChildren<CharacterBody>();
                if (_body == null)
                    return;
            }
            if (_rig == null)
                _rig = GetComponent<WeaponVisualRig>();
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            var frame = _body.transform;

            if (Seated)
            {
                PoseSeated(frame);
                _hasLast = false; // no velocity spike when stepping out
                return;
            }

            // ---- Motion (from real position changes: works for every kind of character)
            var position = transform.position;
            var worldVelocity = _hasLast ? (position - _lastPosition) / dt : Vector3.zero;
            _lastPosition = position;
            _hasLast = true;
            if (worldVelocity.sqrMagnitude > 15f * 15f)
                worldVelocity = Vector3.zero; // teleport / respawn
            var local = frame.InverseTransformDirection(worldVelocity);
            local.y = 0f;
            _localVelocity = Vector3.Lerp(_localVelocity, local, 1f - Mathf.Exp(-12f * dt));
            float speed = _localVelocity.magnitude;
            var moveDir = speed > 0.05f ? _localVelocity / speed : Vector3.forward;

            var mode = LocomotionMode.Standing;
            bool sprinting = speed > 5.6f;
            if (_movement != null)
            {
                mode = _movement.State.Mode;
                sprinting = _movement.State.IsSprinting;
            }
            if (_router != null && _router.enabled)
            {
                AimPitch = _router.Pitch;
                LookYaw = _router.FreeLookYaw;
            }

            float k = 1f - Mathf.Exp(-10f * dt);
            _moveBlend = Mathf.Lerp(_moveBlend, Mathf.Clamp01(speed / 0.8f), k);
            _runBlend = Mathf.Lerp(_runBlend, Mathf.InverseLerp(2.2f, 4f, speed), k);
            _sprintBlend = Mathf.Lerp(_sprintBlend, sprinting ? 1f : 0f, k);
            _crouchBlend = Mathf.Lerp(_crouchBlend, mode == LocomotionMode.Crouching ? 1f : 0f, k);
            _airBlend = Mathf.Lerp(_airBlend, mode == LocomotionMode.Airborne ? 1f : 0f, 1f - Mathf.Exp(-14f * dt));
            _slideBlend = Mathf.Lerp(_slideBlend, mode == LocomotionMode.Sliding ? 1f : 0f, 1f - Mathf.Exp(-14f * dt));
            _aimBlend = Mathf.Lerp(_aimBlend, _rig != null && _rig.Aiming ? 1f : 0f, 1f - Mathf.Exp(-14f * dt));
            _landDip = Mathf.MoveTowards(_landDip, 0f, dt * 0.6f);
            _breath += dt * 1.6f;

            // ---- Gait parameters: walk → run → sprint
            float stride = Mathf.Lerp(Mathf.Lerp(0.55f, 0.95f, _runBlend), 1.3f, _sprintBlend) * Mathf.Lerp(1f, 0.7f, _crouchBlend);
            float lift = Mathf.Lerp(Mathf.Lerp(0.09f, 0.2f, _runBlend), 0.3f, _sprintBlend) * Mathf.Lerp(1f, 0.6f, _crouchBlend);
            float bobAmp = Mathf.Lerp(Mathf.Lerp(0.025f, 0.05f, _runBlend), 0.07f, _sprintBlend);
            if (_airBlend < 0.5f && _slideBlend < 0.5f)
                _phase += dt * Mathf.PI * speed / Mathf.Max(stride, 0.3f);
            _phase %= Mathf.PI * 4f;

            var swing = SwingPose(out bool swinging, out float spin, out float lunge);
            PosePelvis(stride, bobAmp, spin, lunge, moveDir);
            PoseLegs(frame, moveDir, stride, lift);
            PoseArms(frame, swing, swinging, spin);
        }

        // ------------------------------------------------------------------ body

        /// <summary>Seated in a vehicle: hips on the seat, legs bent to the floor or foot pegs, hands on the wheel.</summary>
        private void PoseSeated(Transform frame)
        {
            if (_router != null && _router.enabled)
            {
                AimPitch = _router.Pitch;
                LookYaw = _router.FreeLookYaw;
            }
            _body.Pelvis.localPosition = new Vector3(0f, _body.PelvisRestHeight, -0.04f);
            _body.Pelvis.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            _body.Spine.localRotation = Quaternion.Euler(4f + Mathf.Clamp(AimPitch, -40f, 40f) * 0.15f, 0f, 0f);
            float look = Mathf.Clamp(LookYaw, -80f, 80f);
            _body.Head.localRotation = Quaternion.Euler(Mathf.Clamp(AimPitch, -50f, 50f) * 0.4f, look * 0.8f, 0f);

            for (int side = -1; side <= 1; side += 2)
            {
                var foot = frame.TransformPoint(new Vector3(side * 0.17f, SeatFoot.x, SeatFoot.y));
                var kneeUp = frame.TransformDirection(new Vector3(side * 0.15f, 1f, 1f));
                Ik.SolveTwoBone(_body.Thigh(side), _body.Shin(side), CharacterBody.ThighLength, CharacterBody.ShinLength, foot, kneeUp);
                _body.Foot(side).rotation = frame.rotation;

                var target = side < 0 ? HandTargetL : HandTargetR;
                var hand = target ?? frame.TransformPoint(new Vector3(side * 0.2f, _body.PelvisRestHeight + 0.05f, 0.32f));
                var elbow = frame.TransformDirection(new Vector3(side * 1f, -0.8f, -0.3f));
                Ik.SolveTwoBone(_body.UpperArm(side), _body.Forearm(side), CharacterBody.UpperArmLength, CharacterBody.ForearmLength, hand, elbow);
            }
        }

        private void PosePelvis(float stride, float bobAmp, float spin, float lunge, Vector3 moveDir)
        {
            float phase = _phase;
            float walking = _moveBlend * (1f - _airBlend) * (1f - _slideBlend);
            float bob = -bobAmp * 0.5f * (1f - Mathf.Cos(2f * phase)) * walking;
            float height = _body.PelvisRestHeight
                           - Mathf.Lerp(0.015f, 0.06f, Mathf.Max(_runBlend * 0.6f, _sprintBlend)) * walking
                           - 0.32f * _crouchBlend - 0.5f * _slideBlend - _landDip + bob
                           + Mathf.Sin(_breath) * 0.004f;
            float sway = Mathf.Sin(phase) * 0.025f * walking;
            _body.Pelvis.localPosition = new Vector3(sway, height, lunge * 0.18f);

            // Lean into the run (forward component), bank into strafes, lean back in a slide.
            float forward = Vector3.Dot(moveDir, Vector3.forward) * _moveBlend;
            float side = Vector3.Dot(moveDir, Vector3.right) * _moveBlend;
            float lean = Mathf.Lerp(Mathf.Lerp(3f, 9f, _runBlend), 17f, _sprintBlend) * forward * (1f - _aimBlend * 0.6f)
                         + 10f * _crouchBlend - 28f * _slideBlend + 6f * _airBlend + lunge * 10f;
            float twist = Mathf.Sin(phase) * Mathf.Lerp(5f, 10f, _runBlend) * walking;
            _body.Pelvis.localRotation = Quaternion.Euler(lean, twist + spin, -side * 6f * (1f - _aimBlend));

            // Spine counter-twists and follows the aim; head looks where the player aims.
            float pitch = Mathf.Clamp(AimPitch, -70f, 70f);
            _body.Spine.localRotation = Quaternion.Euler(pitch * 0.35f - lean * 0.25f + Mathf.Sin(_breath) * 0.8f + _swingBend,
                -twist * 1.3f + _swingTwist, 0f);
            float look = Mathf.Clamp(LookYaw, -80f, 80f);
            _body.Head.localRotation = Quaternion.Euler(pitch * 0.45f - lean * 0.45f, twist * 0.3f + look * 0.8f, 0f);
        }

        private void PoseLegs(Transform frame, Vector3 moveDir, float stride, float lift)
        {
            float walking = _moveBlend * (1f - _slideBlend);
            for (int side = -1; side <= 1; side += 2)
            {
                float phase = _phase + (side > 0 ? Mathf.PI : 0f);
                float along = Mathf.Sin(phase) * stride * 0.5f * walking;
                float up = Mathf.Max(0f, Mathf.Cos(phase)) * lift * walking;

                // Foot contact: the swing (cos > 0) ends and the stance begins.
                float footCos = Mathf.Cos(phase);
                int index = side < 0 ? 0 : 1;
                if (_lastFootCos[index] > 0f && footCos <= 0f && walking > 0.3f && _airBlend < 0.3f)
                {
                    float intensity = Mathf.Lerp(Mathf.Lerp(0.35f, 0.65f, _runBlend), 0.95f, _sprintBlend) * Mathf.Lerp(1f, 0.45f, _crouchBlend);
                    FootContact?.Invoke(side, intensity);
                }
                _lastFootCos[index] = footCos;

                var foot = new Vector3(side * (CharacterBody.HipSpacing + 0.015f + 0.04f * _crouchBlend), CharacterBody.AnkleHeight + up, 0.02f)
                           + moveDir * along;
                // Idle / crouch stance: one foot a little ahead of the other.
                foot.z += (side < 0 ? 0.06f : -0.04f) * (1f - walking) + (side < 0 ? 0.1f : -0.08f) * _crouchBlend * (1f - walking);
                // Airborne: legs tucked under the body.
                foot = Vector3.Lerp(foot, new Vector3(side * 0.13f, 0.38f + (side < 0 ? 0.05f : 0f), side < 0 ? 0.18f : -0.12f), _airBlend * 0.85f);
                // Slide: lead leg out in front, the other folded under.
                foot = Vector3.Lerp(foot, side < 0 ? new Vector3(-0.12f, 0.08f, 0.62f) : new Vector3(0.14f, 0.1f, -0.02f), _slideBlend);

                var thigh = _body.Thigh(side);
                var target = frame.TransformPoint(foot);
                var pole = frame.TransformDirection(new Vector3(side * 0.12f, 0f, 1f));
                Ik.SolveTwoBone(thigh, _body.Shin(side), CharacterBody.ThighLength, CharacterBody.ShinLength, target, pole);

                // Feet stay flat on the ground, toes dip while the foot swings and point down in the air.
                float toe = lift > 0.01f ? 25f * Mathf.Clamp01(up / lift) : 0f;
                _body.Foot(side).rotation = frame.rotation * Quaternion.Euler(toe + 35f * _airBlend, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ weapon and arms

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Valid;
        }

        /// <summary>Weapon pose in the body frame, or invalid when unarmed.</summary>
        private Pose WeaponPose(Pose swing, bool swinging)
        {
            var weapon = _rig != null ? _rig.Weapon : null;
            if (weapon == null || _rig.Hand == null)
                return default;

            float bounce = -0.5f * (1f - Mathf.Cos(2f * _phase)) * 0.03f * _moveBlend;
            if (weapon.IsMelee)
            {
                if (swinging)
                    return swing;
                bool twoHanded = weapon.meleeStyle != MeleeStyle.Knife;
                // Guard: blade forward and up (two hands) / forward (knife); sprinting: blade back, ninja-run style.
                var guardPos = twoHanded ? new Vector3(0.1f, 1.08f, 0.32f) : new Vector3(0.24f, 1.02f, 0.26f);
                var guardDir = twoHanded ? new Vector3(0.05f, 0.62f, 0.78f) : new Vector3(0.05f, 0.25f, 1f);
                var runPos = new Vector3(0.3f, 1.0f, -0.08f);
                var runDir = new Vector3(0.05f, -0.45f, -0.89f);
                float run = Mathf.Max(_sprintBlend, _runBlend * 0.5f) * (1f - _crouchBlend);
                var pos = Vector3.Lerp(guardPos, runPos, run) + Vector3.up * (bounce + _crouchBlend * -0.25f);
                var dir = Vector3.Slerp(guardDir.normalized, runDir.normalized, run);
                return new Pose { Position = pos, Rotation = BladeRotation(dir, Vector3.down), Valid = true };
            }

            bool pistol = weapon.id == WeaponCatalog.NeonPistol || weapon.id == WeaponCatalog.ViperRevolver;
            var hip = pistol ? new Vector3(0.13f, 1.3f, 0.42f) : new Vector3(0.17f, 1.29f, 0.3f);
            var ads = pistol ? new Vector3(0.09f, 1.43f, 0.48f) : new Vector3(0.12f, 1.43f, 0.34f);
            var p = Vector3.Lerp(hip, ads, _aimBlend);
            p.y -= 0.3f * _crouchBlend + _landDip * 0.5f;
            var aim = Quaternion.Euler(Mathf.Clamp(AimPitch, -70f, 70f), 0f, 0f);
            var pivot = new Vector3(0.1f, p.y + 0.1f, 0f);
            p = pivot + aim * (p - pivot) + Vector3.up * bounce * (1f - _aimBlend);
            var r = aim;

            // Sprint: weapon lowered across the chest ("port arms").
            float port = _sprintBlend * (1f - _aimBlend);
            p = Vector3.Lerp(p, new Vector3(0.02f, 1.13f + bounce, 0.27f), port);
            r = Quaternion.Slerp(r, Quaternion.Euler(28f, -55f, -25f), port);

            // Recoil, reload dip and roll, healing (weapon lowered), quick melee bash.
            float kick = _rig.Kick;
            p.z -= kick * 0.004f;
            r *= Quaternion.Euler(-kick * 0.6f, 0f, 0f);
            float reload = Mathf.Sin(Mathf.Clamp01(_rig.ReloadAmount) * Mathf.PI);
            p.y -= reload * 0.08f;
            r *= Quaternion.Euler(reload * 22f, 0f, reload * 35f);
            if (_rig.Healing)
            {
                p = Vector3.Lerp(p, new Vector3(0.2f, 1.02f, 0.25f), 0.85f);
                r = Quaternion.Slerp(r, Quaternion.Euler(55f, 0f, 0f), 0.85f);
            }
            if (swinging)
            {
                // Melee button with a gun out: a quick bash with the weapon.
                float bash = Mathf.Sin(Mathf.Clamp01(_rig.SwingProgress) * Mathf.PI);
                p += new Vector3(-0.05f, 0.05f, 0.28f) * bash;
                r *= Quaternion.Euler(-25f * bash, -20f * bash, 0f);
            }
            return new Pose { Position = p, Rotation = r, Valid = true };
        }

        /// <summary>Support-hand grip on the weapon (weapon-local), or null for one-handed weapons.</summary>
        private Vector3? SupportGrip(WeaponStats weapon, bool swinging)
        {
            if (weapon == null)
                return null;
            if (weapon.IsMelee)
            {
                if (weapon.meleeStyle == MeleeStyle.Knife)
                    return null;
                var pattern = _rig.SwingPattern;
                if (swinging && (pattern == MeleePattern.SpinSlash || pattern == MeleePattern.QuickStab))
                    return null;
                if (!swinging && _sprintBlend > 0.5f)
                    return null; // one-handed while sprinting
                return new Vector3(0f, -0.11f, 0f); // lower on the handle
            }
            if (_rig.Healing)
                return null;
            if (_rig.ReloadAmount > 0.05f)
                return new Vector3(0f, -0.15f, 0.15f); // at the magazine
            switch (weapon.id)
            {
                case WeaponCatalog.NeonPistol:
                case WeaponCatalog.ViperRevolver: return new Vector3(-0.03f, -0.07f, -0.01f);
                case WeaponCatalog.VortexLmg: return new Vector3(0f, -0.05f, 0.42f);
                case WeaponCatalog.ArcSniper: return new Vector3(0f, -0.05f, 0.44f);
                case WeaponCatalog.PulseDmr: return new Vector3(0f, -0.05f, 0.42f);
                case WeaponCatalog.TalonAutoShotgun: return new Vector3(0f, -0.04f, 0.38f);
                case WeaponCatalog.RiftCarbine: return new Vector3(0f, -0.05f, 0.34f);
                case WeaponCatalog.ShadowSmg:
                case WeaponCatalog.StormLightSmg: return new Vector3(0f, -0.06f, 0.2f);
                case WeaponCatalog.CrimsonShotgun: return new Vector3(0f, -0.07f, 0.42f);
                case WeaponCatalog.PhantomMarksman: return new Vector3(0f, -0.05f, 0.36f);
                default: return new Vector3(0f, -0.05f, 0.3f);
            }
        }

        private void PoseArms(Transform frame, Pose swing, bool swinging, float spin)
        {
            var weapon = _rig != null ? _rig.Weapon : null;
            var pose = WeaponPose(swing, swinging);
            var spinRotation = Quaternion.Euler(0f, spin, 0f);
            Vector3? support = null;

            if (pose.Valid)
            {
                var hand = _rig.Hand;
                hand.position = frame.TransformPoint(spinRotation * pose.Position);
                hand.rotation = frame.rotation * spinRotation * pose.Rotation;
                Ik.SolveTwoBone(_body.UpperArmR, _body.ForearmR, CharacterBody.UpperArmLength, CharacterBody.ForearmLength, hand.position,
                    frame.TransformDirection(spinRotation * new Vector3(0.6f, -0.5f, -0.4f)));
                _body.HandR.rotation = hand.rotation;
                support = SupportGrip(weapon, swinging);
                if (support.HasValue)
                {
                    var grip = hand.TransformPoint(support.Value);
                    Ik.SolveTwoBone(_body.UpperArmL, _body.ForearmL, CharacterBody.UpperArmLength, CharacterBody.ForearmLength, grip,
                        frame.TransformDirection(spinRotation * new Vector3(-0.6f, -0.5f, -0.3f)));
                    _body.HandL.rotation = hand.rotation;
                }
            }
            else
            {
                FreeArm(+1);
            }

            if (!support.HasValue)
            {
                if (_rig != null && _rig.Healing && pose.Valid)
                {
                    // Pressing the medkit to the chest.
                    Ik.SolveTwoBone(_body.UpperArmL, _body.ForearmL, CharacterBody.UpperArmLength, CharacterBody.ForearmLength,
                        frame.TransformPoint(new Vector3(-0.02f, 1.22f, 0.22f)), frame.TransformDirection(new Vector3(-0.6f, -0.6f, -0.2f)));
                }
                else
                {
                    FreeArm(-1);
                }
            }
        }

        /// <summary>Arm swinging against the legs (bigger and with more elbow bend the faster the gait).</summary>
        private void FreeArm(int side)
        {
            float phase = _phase + (side < 0 ? Mathf.PI : 0f); // opposite to the same-side leg
            float walking = _moveBlend * (1f - _airBlend) * (1f - _slideBlend);
            float amp = Mathf.Lerp(Mathf.Lerp(18f, 38f, _runBlend), 58f, _sprintBlend) * walking;
            float swingAngle = Mathf.Sin(phase) * amp;
            float elbow = Mathf.Lerp(Mathf.Lerp(12f, 55f, _runBlend), 88f, _sprintBlend) * walking + 8f + Mathf.Max(0f, swingAngle) * 0.4f;
            float spread = side * (7f + 30f * _airBlend + 12f * _slideBlend);
            float forward = swingAngle + 35f * _airBlend + 20f * _crouchBlend;
            _body.UpperArm(side).localRotation = Quaternion.Euler(-forward, 0f, spread);
            _body.Forearm(side).localRotation = Quaternion.Euler(-elbow, 0f, 0f);
            _body.Hand(side).localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------ melee swing paths

        private static Quaternion BladeRotation(Vector3 bladeDir, Vector3 edgeHint)
        {
            bladeDir.Normalize();
            var edge = Vector3.ProjectOnPlane(edgeHint, bladeDir);
            if (edge.sqrMagnitude < 1e-6f)
                edge = Vector3.ProjectOnPlane(Vector3.forward, bladeDir);
            return Quaternion.LookRotation(edge.normalized, bladeDir); // model: +Y = blade, +Z = edge
        }

        /// <summary>
        /// The blade / weapon pose of the current melee swing: wind up into the attack's start pose, strike along
        /// its path, recover to guard. Also returns the body spin (spin attack) and lunge (thrusts).
        /// </summary>
        private Pose SwingPose(out bool swinging, out float spin, out float lunge)
        {
            spin = 0f;
            lunge = 0f;
            _swingTwist = 0f;
            _swingBend = 0f;
            swinging = _rig != null && _rig.Swinging;
            if (!swinging || _rig.Weapon == null || !_rig.Weapon.IsMelee)
                return default;

            float t = _rig.SwingElapsed;
            float windup = Mathf.Max(0.05f, _rig.SwingWindup);
            var pattern = _rig.SwingPattern;
            float strike = pattern == MeleePattern.SpinSlash ? 0.34f : pattern == MeleePattern.OverheadStrike ? 0.2f
                : pattern == MeleePattern.QuickStab || pattern == MeleePattern.Thrust ? 0.12f : 0.16f;
            const float recover = 0.3f;

            Pose start = Path(pattern, 0f), end = Path(pattern, 1f);
            var guard = new Pose { Position = new Vector3(0.1f, 1.08f, 0.32f), Rotation = BladeRotation(new Vector3(0.05f, 0.62f, 0.78f), Vector3.down) };
            // The torso winds up against the cut and unwinds through it (shoulders lead the blade).
            float twistFrom = TwistOf(pattern, 0f), twistTo = TwistOf(pattern, 1f);
            if (t < windup)
            {
                float u = Ease(t / windup);
                if (pattern == MeleePattern.Thrust || pattern == MeleePattern.QuickStab)
                    lunge = -0.3f * u;
                _swingTwist = twistFrom * u;
                _swingBend = pattern == MeleePattern.OverheadStrike ? -8f * u : 0f;
                return Blend(guard, start, u);
            }
            if (t < windup + strike)
            {
                float u = EaseInOut((t - windup) / strike);
                if (pattern == MeleePattern.SpinSlash)
                    spin = -360f * u;
                if (pattern == MeleePattern.Thrust || pattern == MeleePattern.QuickStab)
                    lunge = Mathf.Lerp(-0.3f, 1f, u);
                _swingTwist = Mathf.Lerp(twistFrom, twistTo, u);
                _swingBend = pattern == MeleePattern.OverheadStrike ? Mathf.Lerp(-8f, 18f, u) : 0f;
                return Path(pattern, u);
            }
            float back = Mathf.Clamp01((t - windup - strike) / recover);
            if (pattern == MeleePattern.Thrust || pattern == MeleePattern.QuickStab)
                lunge = 1f - back;
            _swingTwist = twistTo * (1f - Ease(back));
            _swingBend = pattern == MeleePattern.OverheadStrike ? 18f * (1f - Ease(back)) : 0f;
            return Blend(end, guard, Ease(back));
        }

        /// <summary>Spine yaw at the start (u = 0) and end (u = 1) of an attack's strike.</summary>
        private static float TwistOf(MeleePattern pattern, float u)
        {
            switch (pattern)
            {
                case MeleePattern.HorizontalSlash: return Mathf.Lerp(35f, -35f, u);
                case MeleePattern.BackhandSlash: return Mathf.Lerp(-35f, 30f, u);
                case MeleePattern.RisingSlash: return Mathf.Lerp(-25f, 25f, u);
                case MeleePattern.Thrust:
                case MeleePattern.QuickStab: return Mathf.Lerp(25f, -10f, u);
                default: return 0f;
            }
        }

        private static Pose Path(MeleePattern pattern, float u)
        {
            var chest = new Vector3(0f, 1.25f, 0.1f);
            switch (pattern)
            {
                case MeleePattern.HorizontalSlash:
                    return Arc(chest, Mathf.Lerp(110f, -95f, u), Mathf.Lerp(8f, -6f, u), Mathf.Lerp(0.05f, -0.05f, u), leftward: true);
                case MeleePattern.BackhandSlash:
                    return Arc(chest, Mathf.Lerp(-105f, 100f, u), Mathf.Lerp(40f, -30f, u), Mathf.Lerp(0.28f, -0.22f, u), leftward: false);
                case MeleePattern.RisingSlash:
                    return Arc(chest, Mathf.Lerp(-65f, 75f, u), Mathf.Lerp(-40f, 60f, u), Mathf.Lerp(-0.3f, 0.35f, u), leftward: false);
                case MeleePattern.SpinSlash:
                    return Arc(chest, 95f, 0f, 0f, leftward: true);
                case MeleePattern.OverheadStrike:
                {
                    float b = Mathf.Lerp(-35f, 125f, u) * Mathf.Deg2Rad;
                    var dir = new Vector3(0.04f, Mathf.Cos(b), Mathf.Sin(b));
                    var hand = chest + new Vector3(0.05f, 0.34f * Mathf.Cos(b) + 0.06f, 0.36f * Mathf.Sin(b) + 0.05f);
                    return new Pose { Position = hand, Rotation = BladeRotation(dir, new Vector3(0f, -Mathf.Sin(b), Mathf.Cos(b))), Valid = true };
                }
                case MeleePattern.QuickStab:
                {
                    var hand = Vector3.Lerp(new Vector3(0.26f, 1.12f, 0.02f), new Vector3(0.12f, 1.24f, 0.62f), u);
                    return new Pose { Position = hand, Rotation = BladeRotation(new Vector3(-0.05f, 0.05f, 1f), Vector3.down), Valid = true };
                }
                default: // Thrust
                {
                    var hand = Vector3.Lerp(new Vector3(0.22f, 1.2f, -0.12f), new Vector3(0.08f, 1.28f, 0.74f), u);
                    return new Pose { Position = hand, Rotation = BladeRotation(new Vector3(-0.04f, 0.04f, 1f), Vector3.down), Valid = true };
                }
            }
        }

        /// <summary>Blade sweeping around the body: azimuth (0 = forward, + = right), elevation, hand height offset.</summary>
        private static Pose Arc(Vector3 chest, float azimuth, float elevation, float height, bool leftward)
        {
            float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
            var hand = chest + new Vector3(Mathf.Sin(a) * 0.4f, height, Mathf.Cos(a) * 0.38f + 0.05f);
            // The edge leads the cut: tangent of the sweep.
            var edge = leftward ? new Vector3(-Mathf.Cos(a), 0f, Mathf.Sin(a)) : new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            return new Pose { Position = hand, Rotation = BladeRotation(dir, edge), Valid = true };
        }

        private static Pose Blend(Pose a, Pose b, float t) => new Pose
        {
            Position = Vector3.Lerp(a.Position, b.Position, t),
            Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
            Valid = true,
        };

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>Analytic two-bone IK for limbs whose segments hang along the joint's local -Y.</summary>
    public static class Ik
    {
        /// <summary>
        /// Rotates <paramref name="upper"/> and <paramref name="lower"/> so the chain's end reaches toward
        /// <paramref name="target"/>, bending toward <paramref name="pole"/> (knees forward, elbows out and back).
        /// Unreachable targets get a straight limb pointing at them.
        /// </summary>
        public static void SolveTwoBone(Transform upper, Transform lower, float upperLength, float lowerLength, Vector3 target, Vector3 pole)
        {
            var root = upper.position;
            var toTarget = target - root;
            float distance = toTarget.magnitude;
            if (distance < 1e-4f)
                return;
            var dir = toTarget / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.01f, (upperLength + lowerLength) * 0.999f);
            float cos = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance), -1f, 1f);
            float sin = Mathf.Sqrt(1f - cos * cos);
            var bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-6f)
                bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            bend.Normalize();
            var joint = root + dir * (upperLength * cos) + bend * (upperLength * sin);
            var end = root + dir * distance;
            upper.rotation = Limb(joint - root, bend);
            lower.rotation = Limb(end - joint, bend);
        }

        /// <summary>Rotation whose local -Y points along <paramref name="along"/>, local +Z toward <paramref name="hint"/>.</summary>
        public static Quaternion Limb(Vector3 along, Vector3 hint)
        {
            var forward = Vector3.ProjectOnPlane(hint, along);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, along);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.right;
            return Quaternion.LookRotation(forward.normalized, -along.normalized);
        }
    }
}
