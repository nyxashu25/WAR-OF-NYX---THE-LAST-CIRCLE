using UnityEngine;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER weapon presentation on a character (spec 9 feedback): a per-weapon cel-shaded model
    /// (<see cref="WeaponModelFactory"/>) in the right hand, muzzle flash, tracer, impact sparks, melee slash arc, firing/reload/swing
    /// motion and the defeat fall. Visual only, client-side; it never decides hits. Phase 10
    /// replaces the meshes and VFX but keeps these entry points.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerAnimationController, so the defeat pose wins
    public sealed class WeaponVisualRig : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0f, 0.9f, 1f);
        private static readonly Color Purple = new Color(0.54f, 0.17f, 0.89f);

        private Transform _hand;
        private Transform _model;
        private Transform _muzzle;
        private LineRenderer _healRing;
        private float _hurt;
        private float _victoryT = -1f;
        private bool _healing;
        private bool _aiming;
        private Transform _visualRoot;
        private Light _flashLight;
        private Material _fx;
        private float _kick;
        private float _swing = -1f;
        private float _reloadDip;
        private float _flashUntil;
        private float _deathT = -1f;
        private float _swingStartedAt = -1f;
        private float _swingWindup;
        private float _swingTotal;
        private MeleePattern _swingPattern;
        private TrailRenderer _trail;
        private CharacterBody _body;

        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f;

        // ---- State read by CharacterAnimator (which poses the hands when the character has a jointed body)
        /// <summary>The held weapon's grip transform (the model is its child).</summary>
        public Transform Hand => _hand;
        public WeaponStats Weapon { get; private set; }
        public bool Aiming => _aiming;
        public bool Healing => _healing;
        /// <summary>Recoil kick in degrees (decays quickly).</summary>
        public float Kick => _kick;
        /// <summary>1 at the start of a reload, easing to 0.</summary>
        public float ReloadAmount => _reloadDip;
        public bool Swinging => _swingStartedAt >= 0f && Time.time - _swingStartedAt < _swingTotal;
        public float SwingElapsed => Swinging ? Time.time - _swingStartedAt : 0f;
        public float SwingWindup => _swingWindup;
        public MeleePattern SwingPattern => _swingPattern;
        /// <summary>0..1 over the whole swing.</summary>
        public float SwingProgress => Swinging ? Mathf.Clamp01(SwingElapsed / _swingTotal) : 0f;

        public static WeaponVisualRig Attach(Transform character, Transform visualRoot)
        {
            if (Application.isBatchMode)
                return null;
            var rig = character.gameObject.AddComponent<WeaponVisualRig>();
            rig.Build(visualRoot != null ? visualRoot : character);
            return rig;
        }

        private void Build(Transform visualRoot)
        {
            _visualRoot = visualRoot;
            _fx = Resources.Load<Material>(SpawnEffect.MaterialResource);

            _hand = new GameObject("Hand (PLACEHOLDER)").transform;
            _hand.SetParent(visualRoot, false);
            _hand.localPosition = new Vector3(0.38f, 1.25f, 0.25f);

            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_hand, false);
            _muzzle.localPosition = new Vector3(0f, 0.02f, 0.55f);

            if (_fx != null)
            {
                // Medkit channel ring at the feet (Phase 10 healing presentation).
                var ringGo = new GameObject("HealRing");
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = Vector3.up * 0.08f;
                _healRing = ringGo.AddComponent<LineRenderer>();
                _healRing.sharedMaterial = _fx;
                _healRing.useWorldSpace = false;
                _healRing.loop = true;
                _healRing.positionCount = 32;
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2f / 32f;
                    _healRing.SetPosition(i, new Vector3(Mathf.Sin(a) * 0.7f, 0f, Mathf.Cos(a) * 0.7f));
                }
                _healRing.widthMultiplier = 0.06f;
                _healRing.startColor = _healRing.endColor = new Color(0.3f, 1f, 0.55f, 0.8f);
                _healRing.enabled = false;
            }

            var lightGo = new GameObject("MuzzleLight");
            lightGo.transform.SetParent(_muzzle, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 6f;
            _flashLight.intensity = 0f;
            _flashLight.color = new Color(1f, 0.85f, 0.6f);
        }

        public void ShowWeapon(WeaponStats weapon)
        {
            if (_hand == null)
                return;
            if (_model != null)
                Destroy(_model.gameObject);
            Weapon = weapon;
            _model = WeaponModelFactory.Build(weapon, _hand, out var muzzle);
            _muzzle.localPosition = muzzle;
            _trail = null;
            var tip = _model.Find("TrailTip");
            if (tip != null && _fx != null)
            {
                // Slash trail along the real blade path (enabled during the strike).
                _trail = tip.gameObject.AddComponent<TrailRenderer>();
                _trail.sharedMaterial = _fx;
                _trail.time = 0.12f;
                _trail.minVertexDistance = 0.04f;
                _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(1f, 0f));
                bool energy = weapon != null && weapon.meleeStyle == MeleeStyle.Energy;
                _trail.startColor = energy ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f) : new Color(0.9f, 0.95f, 1f, 0.7f);
                _trail.endColor = new Color(Purple.r, Purple.g, Purple.b, 0f);
                _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trail.emitting = false;
            }
        }

        /// <summary>Short recoil of the whole body when hit (visual only).</summary>
        public void PlayHurt() => _hurt = 1f;

        /// <summary>Winner celebration: spin and hop until the scene changes.</summary>
        public void PlayVictory()
        {
            if (_victoryT < 0f)
                _victoryT = 0f;
        }

        public void SetHealing(bool healing) => _healing = healing;

        public void SetAiming(bool aiming) => _aiming = aiming;

        /// <summary>Muzzle flash, tracer, impact and sound for one shot (pellet hit points are visual estimates).</summary>
        public void PlayShot(WeaponStats weapon, Vector3 origin, Vector3[] endPoints, int count, bool hitSomething)
        {
            _kick = Mathf.Min(_kick + (weapon != null ? weapon.recoilKick * 4f : 3f), 20f);
            _flashUntil = Time.time + 0.05f;
            for (int i = 0; i < count; i++)
                SpawnTracer(MuzzlePosition, endPoints[i]);
            if (hitSomething)
                for (int i = 0; i < count; i++)
                    SpawnImpact(endPoints[i]);
            bool heavy = weapon != null && (weapon.pellets > 1 || weapon.damage >= 50f);
            ProceduralAudio.PlayAt(heavy ? ProceduralAudio.HeavyShot : ProceduralAudio.Gunshot, MuzzlePosition, heavy ? 1f : 0.8f);
        }

        /// <summary>One melee attack of a combo (the pattern decides the swing path, see CharacterAnimator).</summary>
        public void PlaySwing(MeleeAttack attack, WeaponStats meleeWeapon)
        {
            _swing = 0f;
            _swingStartedAt = Time.time;
            _swingPattern = attack.Pattern;
            _swingWindup = meleeWeapon != null ? meleeWeapon.meleeWindup * attack.WindupMultiplier : 0.2f;
            _swingTotal = _swingWindup + (attack.Pattern == MeleePattern.SpinSlash ? 0.34f : 0.2f) + 0.3f;
            ProceduralAudio.PlayAt(ProceduralAudio.MeleeSwing, transform.position + Vector3.up * 1.2f, 0.9f);
            // Characters without a jointed body (or a gun bash) keep the simple arc effect.
            if (_fx != null && (_body == null || _trail == null))
                SpawnSlashArc();
        }

        public void PlayReload()
        {
            _reloadDip = 1f;
            ProceduralAudio.PlayAt(ProceduralAudio.Reload, transform.position + Vector3.up * 1.2f, 0.6f);
        }

        public void PlayDefeat()
        {
            if (_deathT < 0f)
                _deathT = 0f;
        }

        public void ResetDefeat()
        {
            _deathT = -1f;
            if (_visualRoot != null)
                _visualRoot.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (_hand == null)
                return;
            float dt = Time.deltaTime;
            if (_body == null)
                _body = GetComponentInChildren<CharacterBody>();

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 60f);
            _reloadDip = Mathf.MoveTowards(_reloadDip, 0f, dt * 0.7f);
            if (_trail != null)
            {
                float t = SwingElapsed;
                _trail.emitting = Swinging && t >= _swingWindup - 0.03f && t <= _swingWindup + (_swingPattern == MeleePattern.SpinSlash ? 0.36f : 0.2f);
            }
            float swingAngle = 0f;
            if (_swing >= 0f)
            {
                _swing += dt * 4f;
                swingAngle = Mathf.Sin(Mathf.Clamp01(_swing) * Mathf.PI) * 110f;
                if (_swing >= 1f)
                    _swing = -1f;
            }
            // With a jointed body, CharacterAnimator places the hand (grip, aim, recoil, swings) before this runs.
            if (_body == null)
                _hand.localRotation = Quaternion.Euler(-_kick + _reloadDip * 35f + (_healing ? 50f : 0f) - (_aiming ? 8f : 0f), 0f, -swingAngle);
            _flashLight.intensity = Time.time < _flashUntil ? 4f : 0f;

            if (_healRing != null)
            {
                _healRing.enabled = _healing && _deathT < 0f;
                if (_healing)
                    _healRing.transform.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }

            // Hurt flinch and victory are added on top of the animation controller's pose (this runs after it).
            _hurt = Mathf.MoveTowards(_hurt, 0f, dt * 5f);
            if (_hurt > 0f && _visualRoot != null && _deathT < 0f)
                _visualRoot.localRotation *= Quaternion.Euler(-12f * Mathf.Sin(_hurt * Mathf.PI), 0f, 0f);
            if (_victoryT >= 0f && _visualRoot != null && _deathT < 0f)
            {
                _victoryT += dt;
                _visualRoot.localRotation *= Quaternion.Euler(0f, _victoryT * 220f, 0f);
                _visualRoot.localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(_victoryT * 5f)) * 0.35f;
            }

            if (_deathT >= 0f && _visualRoot != null)
            {
                // Defeat: topple backwards over ~0.6 s (placeholder for the anime defeat animation).
                _deathT = Mathf.Min(1f, _deathT + dt / 0.6f);
                float eased = 1f - (1f - _deathT) * (1f - _deathT);
                _visualRoot.localRotation = Quaternion.Euler(-85f * eased, 0f, 0f);
            }
        }

        private void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (_fx == null)
                return;
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _fx;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.035f;
            line.startColor = new Color(1f, 0.9f, 0.6f, 0.9f);
            line.endColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go, 0.06f);
        }

        private void SpawnImpact(Vector3 point)
        {
            if (_fx == null)
                return;
            var go = new GameObject("Impact");
            go.transform.position = point;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = 0.06f;
            main.startColor = new Color(1f, 0.8f, 0.4f);
            main.maxParticles = 10;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _fx;
            ps.Play();
            Destroy(go, 0.5f);
        }

        private void SpawnSlashArc()
        {
            var go = new GameObject("Slash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _fx;
            line.useWorldSpace = false;
            const int segments = 16;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(-55f, 55f, i / (segments - 1f)) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(Mathf.Sin(a) * 1.6f, Mathf.Lerp(0.3f, -0.2f, i / (segments - 1f)), Mathf.Cos(a) * 1.6f));
            }
            line.widthMultiplier = 0.25f;
            line.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
            line.endColor = new Color(Purple.r, Purple.g, Purple.b, 0f);
            Destroy(go, 0.15f);
        }
    }
}
