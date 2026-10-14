using UnityEngine;
using WarOfNyx.App;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER vehicle explosion (visual and audio only; the damage is applied by the match): a fireball of
    /// embers and debris, an orange flash light, a ground shockwave ring and a deep synthesized boom with a rolling
    /// tail. Softened by "Reduce flashing".
    /// </summary>
    public sealed class VehicleExplosion : MonoBehaviour
    {
        private const float Duration = 1.6f;
        private static AudioClip _boom;
        private LineRenderer _ring;
        private Light _flash;
        private float _t;
        private float _intensity = 1f;

        public static void Play(Vector3 centre)
        {
            if (Application.isBatchMode)
                return;
            AudioPool.Play(Boom(), centre, 1f, Random.Range(0.92f, 1.05f), 8f, 400f, rolloff: AudioRolloffMode.Logarithmic);
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            var go = new GameObject("VehicleExplosion");
            go.transform.position = centre;
            go.AddComponent<VehicleExplosion>().Build(material);
        }

        private void Build(Material material)
        {
            _intensity = AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing ? 0.4f : 1f;
            _flash = new GameObject("Flash").AddComponent<Light>();
            _flash.transform.SetParent(transform, false);
            _flash.type = LightType.Point;
            _flash.color = new Color(1f, 0.55f, 0.2f);
            _flash.range = 22f;
            _flash.intensity = 6f * _intensity;

            if (material != null)
            {
                _ring = gameObject.AddComponent<LineRenderer>();
                _ring.sharedMaterial = material;
                _ring.useWorldSpace = false;
                _ring.loop = true;
                _ring.positionCount = 48;
                for (int i = 0; i < 48; i++)
                {
                    float a = i * Mathf.PI * 2f / 48f;
                    _ring.SetPosition(i, new Vector3(Mathf.Sin(a), -0.6f, Mathf.Cos(a)));
                }
                _ring.widthMultiplier = 0.25f;
                _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Burst("Fireball", material, 0.5f, 0.6f, 1.1f, 2f, 7f, 0.5f, 1.4f, -0.6f, new Color(1f, 0.75f, 0.25f), new Color(1f, 0.3f, 0.05f), _intensity < 1f ? 30 : 70);
                Burst("Debris", material, 0.3f, 0.8f, 1.6f, 6f, 14f, 0.05f, 0.14f, 1.2f, new Color(1f, 0.6f, 0.2f), new Color(0.4f, 0.35f, 0.3f), 40);
            }
            Destroy(gameObject, Duration + 0.2f);
        }

        private void Burst(string name, Material material, float radius, float lifeMin, float lifeMax, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float gravity, Color a, Color b, int count)
        {
            var ps = new GameObject(name).AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.3f, 0.25f, 0.22f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ps.Play();
        }

        private void Update()
        {
            _t += Time.deltaTime / Duration;
            float k = Mathf.Clamp01(_t);
            if (_flash != null)
                _flash.intensity = Mathf.Lerp(6f, 0f, Mathf.Sqrt(k * 3f)) * _intensity;
            if (_ring != null)
            {
                _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 9f, Mathf.Sqrt(k));
                _ring.startColor = _ring.endColor = new Color(1f, 0.55f, 0.2f, (1f - k) * 0.9f * _intensity);
            }
        }

        /// <summary>A deep boom: a falling low thump, a broadband blast and a long rolling tail.</summary>
        private static AudioClip Boom()
        {
            if (_boom != null)
                return _boom;
            var s = new Synth(2.6f, 22050, 71);
            s.Tone(0f, 70f, 28f, 0.9f, 1f, 0.35f, 0.004f);
            s.Noise(0f, 0.5f, 0.9f, 0.002f, 0.12f, 2400f, 40f, 300f);
            s.Noise(0.05f, 2.4f, 0.4f, 0.05f, 0.8f, 500f, 30f, 120f);
            s.Click(0f, 900f, 0.8f, 0.01f);
            s.Normalize(0.95f);
            _boom = s.ToClip("VehicleExplosion");
            return _boom;
        }
    }
}
