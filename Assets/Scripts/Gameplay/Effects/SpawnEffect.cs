using UnityEngine;
using WarOfNyx.App;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Spawn presentation (spec 6): blue-purple portal ring, short energy beam, spark burst,
    /// ground shockwave and a spawn sound. Purely visual and client-only; it never blocks the
    /// player's view for long (1.4 s) and honours "Reduce flashing".
    /// The character's landing comes from spawning slightly above the ground (MatchRules.spawnDropHeight).
    /// </summary>
    public sealed class SpawnEffect : MonoBehaviour
    {
        public const string MaterialResource = "WarOfNyx/FX_Additive";
        private const float Duration = 1.4f;
        private const int RingSegments = 48;

        private static readonly Color Purple = new Color(0.54f, 0.17f, 0.89f);
        private static readonly Color Cyan = new Color(0f, 0.9f, 1f);
        private static bool _warnedMissingMaterial;

        private LineRenderer _portal;
        private LineRenderer _shockwave;
        private LineRenderer _beam;
        private float _time;
        private float _intensity = 1f;

        public static void Play(Vector3 groundPosition)
        {
            if (Application.isBatchMode)
                return;

            var material = Resources.Load<Material>(MaterialResource);
            if (material == null)
            {
                if (!_warnedMissingMaterial)
                    Debug.LogWarning($"[WarOfNyx] FX material Resources/{MaterialResource} missing - run War of Nyx > Setup. Spawn VFX skipped.");
                _warnedMissingMaterial = true;
                ProceduralAudio.PlayAt(ProceduralAudio.SpawnChime, groundPosition, 0.8f);
                return;
            }

            var go = new GameObject("SpawnEffect");
            go.transform.position = groundPosition;
            go.AddComponent<SpawnEffect>().Build(material);
        }

        private void Build(Material material)
        {
            bool reduceFlashing = AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;
            _intensity = reduceFlashing ? 0.45f : 1f;

            _portal = CreateRing("Portal", material, 1.2f, 0.14f);
            _shockwave = CreateRing("Shockwave", material, 0.5f, 0.2f);

            _beam = CreateLine("Beam", material);
            _beam.positionCount = 2;
            _beam.SetPosition(0, Vector3.zero);
            _beam.SetPosition(1, Vector3.up * 6f);

            CreateBurst(material, reduceFlashing ? 16 : 40);
            ProceduralAudio.PlayAt(ProceduralAudio.SpawnChime, transform.position, 0.8f);
            Apply(0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            if (_time >= Duration)
            {
                Destroy(gameObject);
                return;
            }
            Apply(_time / Duration);
        }

        private void Apply(float t)
        {
            float fadeOut = 1f - t;

            // Portal: spins and shrinks away.
            _portal.transform.localRotation = Quaternion.Euler(0f, t * 360f, 0f);
            _portal.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t * t);
            SetColor(_portal, Color.Lerp(Purple, Cyan, t), fadeOut * 0.9f);

            // Shockwave: fast expanding ground ring in the first third.
            float s = Mathf.Clamp01(t * 3f);
            _shockwave.transform.localScale = Vector3.one * Mathf.Lerp(1f, 8f, s);
            SetColor(_shockwave, Cyan, (1f - s) * 0.8f);

            // Beam: bright at once, thins out quickly.
            float b = Mathf.Clamp01(t * 2.5f);
            _beam.widthMultiplier = Mathf.Lerp(0.7f, 0f, b);
            SetColor(_beam, Color.Lerp(Cyan, Purple, b), (1f - b) * 0.85f);
        }

        private void SetColor(LineRenderer line, Color color, float alpha)
        {
            var c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha * _intensity));
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, line == _beam ? 0f : c.a);
        }

        private LineRenderer CreateLine(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private LineRenderer CreateRing(string name, Material material, float radius, float width)
        {
            var line = CreateLine(name, material);
            line.transform.localPosition = Vector3.up * 0.06f;
            line.loop = true;
            line.widthMultiplier = width;
            line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius));
            }
            return line;
        }

        private void CreateBurst(Material material, int count)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.3f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone points up

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // must be stopped before changing duration

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(Purple, Cyan);
            main.gravityModifier = 0.4f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.6f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(_intensity, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
        }
    }
}
