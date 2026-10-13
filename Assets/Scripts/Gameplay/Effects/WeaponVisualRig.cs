using UnityEngine;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER weapon presentation on a character (spec 9 feedback): a box "gun" or "blade" in
    /// the right hand, muzzle flash, tracer, impact sparks, melee slash arc, firing/reload/swing
    /// motion and the defeat fall. Visual only, client-side; it never decides hits. Phase 10
    /// replaces the meshes and VFX but keeps these entry points.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerAnimationController, so the defeat pose wins
    public sealed class WeaponVisualRig : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(0f, 0.9f, 1f);
        private static readonly Color Purple = new Color(0.54f, 0.17f, 0.89f);

        private Transform _hand;
        private Transform _gun;
        private Transform _blade;
        private Transform _muzzle;
        private Transform _visualRoot;
        private Light _flashLight;
        private Material _fx;
        private float _kick;
        private float _swing = -1f;
        private float _reloadDip;
        private float _flashUntil;
        private float _deathT = -1f;

        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position + Vector3.up * 1.4f;

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

            _gun = Box("Gun", _hand, new Vector3(0f, 0f, 0.25f), new Vector3(0.1f, 0.14f, 0.6f), new Color(0.2f, 0.22f, 0.3f));
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_gun, false);
            _muzzle.localPosition = new Vector3(0f, 0.02f, 0.55f);

            _blade = Box("Blade", _hand, new Vector3(0f, 0.45f, 0.1f), new Vector3(0.05f, 0.9f, 0.08f), Cyan);
            _blade.gameObject.SetActive(false);

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
            if (_gun == null)
                return;
            bool melee = weapon != null && weapon.IsMelee;
            _gun.gameObject.SetActive(weapon != null && !melee);
            _blade.gameObject.SetActive(melee);
            if (weapon != null && !melee)
                _gun.localScale = new Vector3(0.1f, 0.14f, weapon.pellets > 1 ? 0.75f : weapon.maxRange > 200f ? 0.95f : 0.6f);
        }

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

        public void PlaySwing()
        {
            _swing = 0f;
            ProceduralAudio.PlayAt(ProceduralAudio.MeleeSwing, transform.position + Vector3.up * 1.2f, 0.9f);
            if (_fx != null)
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

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 60f);
            _reloadDip = Mathf.MoveTowards(_reloadDip, 0f, dt * 0.7f);
            float swingAngle = 0f;
            if (_swing >= 0f)
            {
                _swing += dt * 4f;
                swingAngle = Mathf.Sin(Mathf.Clamp01(_swing) * Mathf.PI) * 110f;
                if (_swing >= 1f)
                    _swing = -1f;
            }
            _hand.localRotation = Quaternion.Euler(-_kick + _reloadDip * 35f, 0f, -swingAngle);
            _flashLight.intensity = Time.time < _flashUntil ? 4f : 0f;

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

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>()); // visual only: never blocks shots or movement
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = color;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
