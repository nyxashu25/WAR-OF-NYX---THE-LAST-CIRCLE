using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// The character's supernatural accent (section 40.12), only at moments that matter: an energy burst rising
    /// around them at spawn, sparks where their blade connects, and a brief aura with glowing eyes at victory.
    /// Normal movement stays clean. Small bursts, nothing permanent; reduced with "Reduce flashing" and skipped far away.
    /// KAI's accent: violet shadow wisps rising from the ground.
    /// </summary>
    public sealed class CharacterVisualEffects : MonoBehaviour
    {
        private Color _color = new Color(0.62f, 0.25f, 1f);
        private Material _fx;
        private CharacterAnimationController _controller;
        private CharacterFacialAnimator _face;
        private bool _victoryShown;

        public void Init(CharacterVisualProfile profile)
        {
            if (profile != null)
                _color = profile.energyColor;
        }

        private void Start()
        {
            _fx = Resources.Load<Material>(SpawnEffect.MaterialResource);
            _controller = GetComponent<CharacterAnimationController>();
            _face = GetComponent<CharacterFacialAnimator>();
            if (_controller != null)
                _controller.Strike += OnStrike;
            Burst(transform.position, 0.35f, 26, 1.3f, 2.4f, -0.5f, 0.9f);
            _face?.Glow(1.2f);
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.Strike -= OnStrike;
        }

        private static float Intensity => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing ? 0.45f : 1f;

        private void OnStrike(Vector3 at)
        {
            Burst(at, 0.05f, 14, 1.5f, 4f, 0f, 0.35f);
            _face?.Flare(0.8f);
        }

        private void Update()
        {
            if (_controller != null && _controller.Victorious && !_victoryShown)
            {
                _victoryShown = true;
                Burst(transform.position, 0.6f, 40, 0.5f, 1.6f, -0.25f, 2.2f);
                _face?.Glow(1.5f);
            }
        }

        /// <summary>A short particle burst in the character's energy colour.</summary>
        private void Burst(Vector3 at, float radius, int count, float speedMin, float speedMax, float gravity, float life)
        {
            if (_fx == null || Application.isBatchMode)
                return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - at).sqrMagnitude > 60f * 60f)
                return;
            var go = new GameObject("CharacterFx");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(_color, Color.Lerp(_color, Color.white, 0.4f));
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(4, Mathf.RoundToInt(count * Intensity))) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(_color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = _fx;
            ps.Play();
            Destroy(go, life + 0.4f);
        }
    }
}
