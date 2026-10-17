using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Movement;
using WarOfNyx.Gameplay.Effects;
using P = WarOfNyx.Gameplay.Characters.CharacterAnimatorParams;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// Drives a skinned humanoid character (section 40.8): Mecanim parameters from the authoritative movement state
    /// (in-place locomotion, the character's own clips through its controller), combat presentation from the weapon rig
    /// (firearms are placed in front of the body and both hands are IK'd onto their grips, so guns never float; blades
    /// ride in the animated right hand during the authored combo clips), the aim (spine and head look along the aim
    /// pitch), seats (hands on the wheel), hit / death / victory clips, and footstep events from the animated feet.
    /// Cosmetic only: it never moves the character or changes gameplay timing (melee clips are re-timed so the blade
    /// connects exactly when the server's windup ends).
    /// </summary>
    [DefaultExecutionOrder(55)]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimationController : MonoBehaviour, ICharacterPose
    {
        /// <summary>The blade's grip in the right hand bone's frame (shared with the clip authoring).</summary>
        public static readonly Vector3 MeleeGripPosition = new Vector3(0.06f, -0.018f, 0.01f);
        public static readonly Quaternion MeleeGripRotation = Quaternion.LookRotation(Vector3.right, Vector3.forward);

        private Animator _animator;
        private CharacterAnimationProfile _profile;
        private Transform _character;
        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private WeaponVisualRig _rig;
        private Transform _rightHand, _leftHand, _head, _leftFoot, _rightFoot;
        private Vector3 _lastPosition;
        private bool _hasLast;
        private Vector3 _localVelocity;
        private float _nextIdleVariant;
        private bool _dead;
        private float _swingUntil;
        private float _hitUntil;
        private float _ikWeight;
        private readonly bool[] _footDown = { true, true };
        private float _phaseGuard;

        public float AimPitch { get; set; }
        public float LookYaw { get; set; }
        public bool Seated { get; set; }
        public Vector2 SeatFoot { get; set; } = new Vector2(0.78f, 0.55f);
        public Vector3? HandTargetL { get; set; }
        public Vector3? HandTargetR { get; set; }
        public float Health01 { get; set; } = 1f;
        public bool Dead => _dead;
        public bool Victorious { get; private set; }
        public event System.Action<int, float> FootContact;
        /// <summary>A melee strike connected (visual timing): position of the blade.</summary>
        public event System.Action<Vector3> Strike;

        public Animator Animator => _animator;
        public CharacterAnimationProfile Profile => _profile;

        /// <summary>Body size relative to KAI (weapon carry and guard positions scale with it).</summary>
        private float BodyScale => _profile != null && _profile.bodyScale > 0.1f ? _profile.bodyScale : 1f;
        public bool Moving => _localVelocity.magnitude > 0.4f;

        public void Init(CharacterAnimationProfile profile)
        {
            _profile = profile;
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            if (profile != null && profile.controller != null)
                _animator.runtimeAnimatorController = profile.controller;
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _nextIdleVariant = Time.time + Random.Range(4f, profile != null ? profile.idleVariationSeconds : 9f);
        }

        private void Awake()
        {
            if (_animator == null)
                Init(null);
        }

        private void FindCharacter()
        {
            // The character root carries the movement (players, bots) or at least the weapon rig (menu / showcase puppets).
            _movement = GetComponentInParent<PlayerMovement>();
            var rig = GetComponentInParent<WeaponVisualRig>();
            _character = _movement != null ? _movement.transform
                : rig != null ? rig.transform
                : transform.parent != null && transform.parent.parent != null ? transform.parent.parent
                : transform.parent != null ? transform.parent : transform;
            _router = _character.GetComponent<PlayerInputRouter>();
            if (rig != _rig)
            {
                if (_rig != null)
                    Unsubscribe(_rig);
                _rig = rig;
                if (_rig != null)
                {
                    _rig.AnimatedBody = true;
                    _rig.Hurt += OnHurt;
                    _rig.Defeated += OnDefeated;
                    _rig.Victorious += OnVictory;
                    _rig.Swung += OnSwing;
                }
            }
        }

        private void Unsubscribe(WeaponVisualRig rig)
        {
            rig.Hurt -= OnHurt;
            rig.Defeated -= OnDefeated;
            rig.Victorious -= OnVictory;
            rig.Swung -= OnSwing;
        }

        private void OnDestroy()
        {
            if (_rig != null)
                Unsubscribe(_rig);
        }

        private void OnHurt()
        {
            if (_dead)
                return;
            _hitUntil = Time.time + 0.35f;
            _animator.SetTrigger(P.Hit);
        }

        private void OnDefeated()
        {
            _dead = true;
            _animator.SetBool(P.Dead, true);
        }

        private void OnVictory()
        {
            Victorious = true;
            _animator.SetTrigger(P.Victory);
        }

        private void OnSwing(MeleeAttack attack, WeaponStats weapon)
        {
            float windup = weapon != null ? weapon.meleeWindup * attack.WindupMultiplier : 0.2f;
            // Re-time the clip so its strike frame lands when the server resolves the hit.
            float strikeAt = P.MeleeClipSeconds * P.MeleeStrikeFraction;
            _animator.SetFloat(P.MeleeRate, Mathf.Clamp(strikeAt / Mathf.Max(0.05f, windup), 0.6f, 2.5f));
            _animator.SetInteger(P.MeleePattern, (int)attack.Pattern);
            _animator.SetTrigger(P.Melee);
            _swingUntil = Time.time + windup + 0.45f;
            Invoke(nameof(EmitStrike), windup);
        }

        private void EmitStrike()
        {
            if (_rig != null && _rig.Hand != null)
                Strike?.Invoke(_rig.Hand.position + _rig.Hand.up * 0.6f);
        }

        // ------------------------------------------------------------------ parameters

        private void Update()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;
            if (_character == null || (_rig == null && Time.frameCount % 30 == 0))
                FindCharacter();
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            var position = _character.position;
            var world = _hasLast ? (position - _lastPosition) / dt : Vector3.zero;
            _lastPosition = position;
            _hasLast = true;
            if (world.sqrMagnitude > 15f * 15f || Seated)
                world = Vector3.zero;
            var local = _character.InverseTransformDirection(world);
            local.y = 0f;
            _localVelocity = Vector3.Lerp(_localVelocity, local, 1f - Mathf.Exp(-12f * dt));
            float speed = _localVelocity.magnitude;

            var mode = _movement != null ? _movement.State.Mode : LocomotionMode.Standing;
            bool sprinting = _movement != null && _movement.State.IsSprinting && speed > 3f;
            if (_router != null && _router.enabled)
            {
                AimPitch = _router.Pitch;
                LookYaw = _router.FreeLookYaw;
            }

            _animator.SetFloat(P.MoveX, _localVelocity.x / P.RunSpeed, 0.08f, dt);
            _animator.SetFloat(P.MoveY, _localVelocity.z / P.RunSpeed, 0.08f, dt);
            _animator.SetFloat(P.Speed, speed);
            // Feet match the ground: walk and run blend by speed; outside the authored speeds the playback scales.
            float nominal = speed < P.WalkSpeed ? P.WalkSpeed : speed < P.RunSpeed ? Mathf.Lerp(P.WalkSpeed, P.RunSpeed, Mathf.InverseLerp(P.WalkSpeed, P.RunSpeed, speed)) : sprinting ? P.SprintSpeed : P.RunSpeed;
            _animator.SetFloat(P.LocomotionRate, speed < 0.3f ? 1f : Mathf.Clamp(speed / nominal, 0.75f, 1.35f));
            _animator.SetBool(P.Sprint, sprinting);
            _animator.SetBool(P.Crouch, mode == LocomotionMode.Crouching);
            _animator.SetBool(P.Grounded, mode != LocomotionMode.Airborne || Seated);
            _animator.SetBool(P.Slide, mode == LocomotionMode.Sliding);
            _animator.SetBool(P.Seated, Seated);
            if (!_dead && speed < 0.2f && Time.time > _nextIdleVariant && (_rig == null || (!_rig.Aiming && _rig.ReloadAmount <= 0f)))
            {
                _nextIdleVariant = Time.time + (_profile != null ? _profile.idleVariationSeconds : 9f) * Random.Range(0.8f, 1.4f);
                _animator.SetTrigger(P.IdleVariant);
            }
            if (speed > 0.2f)
                _nextIdleVariant = Mathf.Max(_nextIdleVariant, Time.time + 3f);

            PlaceFirearm(sprinting, speed);
        }

        private bool HoldsFirearm => _rig != null && _rig.Weapon != null && _rig.Weapon.IsRanged && !_dead && !Seated;
        private bool HoldsBlade => _rig != null && _rig.Weapon != null && _rig.Weapon.IsMelee && !_dead && !Seated;
        private bool Swinging => Time.time < _swingUntil;

        /// <summary>Firearms: the weapon is placed in front of the body (hip, aim, sprint carry, reload, healing, recoil).</summary>
        private void PlaceFirearm(bool sprinting, float speed)
        {
            if (!HoldsFirearm || _rig.Hand == null)
                return;
            var p = _profile;
            bool pistol = _rig.Weapon.id == WeaponCatalog.NeonPistol || _rig.Weapon.id == WeaponCatalog.ViperRevolver;
            var hip = p != null ? p.rifleHip : new Vector3(0.17f, 1.29f, 0.3f);
            var ads = p != null ? p.rifleAim : new Vector3(0.12f, 1.43f, 0.34f);
            if (pistol)
            {
                hip = new Vector3(0.13f, 1.3f, 0.42f);
                ads = new Vector3(0.09f, 1.43f, 0.48f);
            }
            float aim = _rig.Aiming ? 1f : 0f;
            var pos = Vector3.Lerp(hip, ads, aim);
            float pitch = Mathf.Clamp(AimPitch, -70f, 70f);
            float crouch = _movement != null && _movement.State.Mode == LocomotionMode.Crouching ? 0.32f : 0f;
            pos.y -= crouch;
            var rotation = Quaternion.Euler(pitch - _rig.Kick * (p != null ? p.recoilBodyLanguage : 0.8f), 0f, 0f);
            // Pitch the gun around the shoulder so it stays on the aim line.
            var shoulder = new Vector3(0.1f, pos.y + 0.1f, 0f);
            pos = shoulder + Quaternion.Euler(pitch, 0f, 0f) * (pos - shoulder);
            pos.z -= _rig.Kick * 0.004f;
            if (sprinting && !_rig.Aiming)
            {
                // Sprint carry: muzzle down across the body.
                pos = Vector3.Lerp(pos, new Vector3(0.12f, 1.12f, 0.24f), 0.85f);
                rotation = Quaternion.Euler(38f, -35f, 10f);
            }
            if (_rig.ReloadAmount > 0f)
                rotation *= Quaternion.Euler(_rig.ReloadAmount * 30f, 0f, -_rig.ReloadAmount * 25f);
            if (_rig.Healing)
            {
                pos = Vector3.Lerp(pos, new Vector3(0.22f, 1.0f, 0.18f), 0.8f);
                rotation = Quaternion.Euler(55f, 0f, 0f);
            }
            pos *= BodyScale; // carry positions are authored for KAI's height
            _rig.Hand.SetPositionAndRotation(_character.TransformPoint(pos), _character.rotation * rotation);
        }

        // ------------------------------------------------------------------ IK
        // Generated humanoid clips carry no IK-goal curves, so Unity's goal IK is not used for limbs: the hands are
        // placed after the Animator with a two-bone solver on the animated arm (LateUpdate). The look-at (spine and
        // head following the aim) uses the Animator's look-at IK, which needs no goals.

        private void OnAnimatorIK(int layer)
        {
            if (_animator == null || layer != 0)
                return;
            if (!_dead && !Seated && _head != null && _character != null)
            {
                float pitch = Mathf.Clamp(AimPitch, -60f, 60f);
                var dir = _character.rotation * Quaternion.Euler(pitch, Mathf.Clamp(LookYaw, -80f, 80f), 0f) * Vector3.forward;
                _animator.SetLookAtWeight(1f, _profile != null ? _profile.spineAimShare : 0.35f, 0.7f, 0f, 0.6f);
                _animator.SetLookAtPosition(_head.position + dir * 10f);
            }
            else
            {
                _animator.SetLookAtWeight(0f);
            }
        }

        private Transform Bone(HumanBodyBones b) => _animator.GetBoneTransform(b);

        /// <summary>Moves a hand toward a goal (blended by weight) with a two-bone solve on the animated arm.</summary>
        private void ArmTo(bool right, Vector3? position, Quaternion? rotation, float weight)
        {
            if (!position.HasValue || weight <= 0.001f || _character == null)
                return;
            var upper = Bone(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var lower = Bone(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var hand = right ? _rightHand : _leftHand;
            if (upper == null || lower == null || hand == null)
                return;
            var target = Vector3.Lerp(hand.position, position.Value, weight);
            // Elbows down and a little out.
            var pole = _character.TransformDirection(new Vector3(right ? 0.55f : -0.55f, -0.8f, -0.25f));
            LimbIk.Solve(upper, lower, hand, target, pole);
            if (rotation.HasValue)
                hand.rotation = Quaternion.Slerp(hand.rotation, rotation.Value, weight * 0.9f);
        }

        private void LegTo(bool right, Vector3 ankle)
        {
            var upper = Bone(right ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg);
            var lower = Bone(right ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg);
            var foot = right ? _rightFoot : _leftFoot;
            if (upper != null && lower != null && foot != null)
                LimbIk.Solve(upper, lower, foot, ankle, _character.forward);
        }

        private void PlaceHands(float dt)
        {
            if (Seated)
            {
                ArmTo(false, HandTargetL, null, 1f);
                ArmTo(true, HandTargetR, null, 1f);
                for (int side = -1; side <= 1; side += 2)
                    LegTo(side > 0, _character.TransformPoint(new Vector3(side * 0.17f, SeatFoot.x, SeatFoot.y)));
                return;
            }
            // Hands: on the gun (firearms), on the guard pose (blades, between swings), otherwise free.
            bool firearm = HoldsFirearm && _rig.Hand != null;
            bool bladeGuard = HoldsBlade && !Swinging && _rig.Hand != null && Time.time > _hitUntil;
            _ikWeight = Mathf.MoveTowards(_ikWeight, firearm || bladeGuard ? 1f : 0f, dt * (firearm ? 8f : 5f));
            if (_ikWeight <= 0.001f)
                return;
            if (firearm)
            {
                var gun = _rig.Hand;
                var right = gun.TransformPoint(new Vector3(0.0f, -0.05f, -0.075f));
                var rightRot = gun.rotation * HandFrame(new Vector3(0f, -0.75f, 0.66f), Vector3.left, rightHand: true);
                var left = gun.TransformPoint(SupportPoint(_rig.Weapon));
                var leftRot = gun.rotation * HandFrame(Vector3.right, Vector3.up, rightHand: false);
                bool reloading = _rig.ReloadAmount > 0.05f;
                ArmTo(true, right, rightRot, _ikWeight);
                if (!_rig.Healing)
                    ArmTo(false, reloading ? gun.TransformPoint(new Vector3(-0.02f, -0.18f, 0.12f)) : left, leftRot, _ikWeight);
            }
            else if (HoldsBlade)
            {
                // Guard stance: blade forward and up, two-handed for long blades.
                var guard = _character.TransformPoint(new Vector3(0.12f, 1.08f, 0.3f) * BodyScale);
                var bladeDir = _character.TransformDirection(new Vector3(0.05f, 0.62f, 0.78f).normalized);
                var edge = _character.TransformDirection(Vector3.down);
                var handRot = Quaternion.LookRotation(edge, bladeDir) * Quaternion.Inverse(MeleeGripRotation);
                var wrist = guard - handRot * MeleeGripPosition;
                ArmTo(true, wrist, handRot, _ikWeight);
                if (_rig.Weapon.meleeStyle != MeleeStyle.Knife)
                    ArmTo(false, guard - bladeDir * 0.11f + _character.right * -0.02f, null, _ikWeight * 0.9f);
            }
        }

        /// <summary>
        /// Hand rotation from where its fingers and palm should face (in the weapon's frame). Bind pose: right fingers +X,
        /// left fingers -X, palms down, thumbs forward.
        /// </summary>
        public static Quaternion HandFrame(Vector3 fingers, Vector3 palm, bool rightHand)
        {
            fingers.Normalize();
            palm = Vector3.ProjectOnPlane(palm, fingers).normalized;
            var thumb = rightHand ? Vector3.Cross(palm, fingers) : Vector3.Cross(fingers, palm);
            return Quaternion.LookRotation(thumb, -palm);
        }

        private static Vector3 SupportPoint(WeaponStats w)
        {
            switch (w.id)
            {
                case WeaponCatalog.NeonPistol:
                case WeaponCatalog.ViperRevolver: return new Vector3(-0.03f, -0.07f, -0.01f);
                case WeaponCatalog.ShadowSmg:
                case WeaponCatalog.StormLightSmg: return new Vector3(0f, -0.06f, 0.24f);
                case WeaponCatalog.CrimsonShotgun: return new Vector3(0f, -0.07f, 0.5f);
                case WeaponCatalog.VortexLmg:
                case WeaponCatalog.PulseDmr: return new Vector3(0f, -0.05f, 0.44f);
                case WeaponCatalog.ArcSniper: return new Vector3(0f, -0.05f, 0.46f);
                case WeaponCatalog.PhantomMarksman: return new Vector3(0f, -0.05f, 0.4f);
                default: return new Vector3(0f, -0.05f, 0.38f);
            }
        }

        // ------------------------------------------------------------------ after the animation

        private void LateUpdate()
        {
            if (_animator == null || _character == null)
                return;
            PlaceHands(Time.deltaTime);
            // Blades ride in the animated right hand (the combo clips swing them).
            if (HoldsBlade && _rig.Hand != null && _rightHand != null)
                _rig.Hand.SetPositionAndRotation(_rightHand.TransformPoint(MeleeGripPosition), _rightHand.rotation * MeleeGripRotation);

            // Footsteps from the animated feet touching down.
            if (_character != null && !Seated && !_dead && _leftFoot != null && _rightFoot != null)
            {
                float speed = _localVelocity.magnitude;
                for (int i = 0; i < 2; i++)
                {
                    var foot = i == 0 ? _leftFoot : _rightFoot;
                    float height = foot.position.y - _character.position.y;
                    bool down = height < 0.11f;
                    if (down && !_footDown[i] && speed > 0.6f && Time.time > _phaseGuard)
                    {
                        _phaseGuard = Time.time + 0.12f;
                        float intensity = Mathf.Lerp(0.35f, 0.95f, Mathf.InverseLerp(1.5f, 6.5f, speed));
                        FootContact?.Invoke(i == 0 ? -1 : 1, intensity);
                    }
                    if (height > 0.14f)
                        _footDown[i] = false;
                    else if (down)
                        _footDown[i] = true;
                }
            }
        }
    }
}
