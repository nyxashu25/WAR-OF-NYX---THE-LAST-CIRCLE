using UnityEngine;
using WarOfNyx.App;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER anime elimination burst (Phase 10): a ring shockwave and violet/cyan shards rising
    /// from the fallen character, with a falling energy sound. ~1 s, cheap, softened by "Reduce flashing".
    /// </summary>
    public sealed class EliminationEffect : MonoBehaviour
    {
        private const float Duration = 1.1f;
        private LineRenderer _ring;
        private float _t;
        private float _intensity = 1f;

        public static void Play(Vector3 feet)
        {
            if (Application.isBatchMode)
                return;
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            ProceduralAudio.PlayAt(ProceduralAudio.Elimination, feet + Vector3.up, 0.8f);
            if (material == null)
                return;
            var go = new GameObject("EliminationEffect");
            go.transform.position = feet + Vector3.up * 0.1f;
            go.AddComponent<EliminationEffect>().Build(material);
        }

        private void Build(Material material)
        {
            _intensity = AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing ? 0.45f : 1f;

            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.sharedMaterial = material;
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = 40;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                _ring.SetPosition(i, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            _ring.widthMultiplier = 0.12f;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var shards = new GameObject("Shards").AddComponent<ParticleSystem>();
            shards.transform.SetParent(transform, false);
            shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = shards.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.gravityModifier = -0.2f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.25f, 1f), new Color(0f, 0.9f, 1f));
            main.maxParticles = 40;
            var emission = shards.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(_intensity < 1f ? 14 : 32)) });
            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shards.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            shards.Play();
            Destroy(gameObject, Duration + 0.1f);
        }

        private void Update()
        {
            _t += Time.deltaTime / Duration;
            float s = Mathf.Lerp(0.4f, 4f, Mathf.Sqrt(Mathf.Clamp01(_t)));
            _ring.transform.localScale = Vector3.one * s;
            float a = (1f - Mathf.Clamp01(_t)) * 0.9f * _intensity;
            _ring.startColor = _ring.endColor = new Color(0.62f, 0.25f, 1f, a);
        }
    }
}
