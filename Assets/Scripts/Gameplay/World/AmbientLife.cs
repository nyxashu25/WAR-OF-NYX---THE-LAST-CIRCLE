using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.App;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.Settings;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Cosmetic life around the player (PLACEHOLDER art, visual only, never affects play):
    ///  * bird flocks circling high above the island in calm daylight (none at night or in storms),
    ///  * fireflies around the camera on calm nights,
    ///  * cherry petals drifting near blossom trees (any <see cref="PetalSource"/> in the scene).
    /// Everything is sized for phones: about 30 birds, under 100 fireflies, under 150 petals.
    /// Created by <see cref="WeatherSystem"/>; reads its current plan and profile every frame.
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        private const int Flocks = 4;
        private const int BirdsPerFlock = 7;

        private WeatherSystem _weather;
        private readonly List<Bird> _birds = new List<Bird>();
        private ParticleSystem _fireflies;
        private ParticleSystem _petals;
        private Material _additive;
        private float _birdsVisible;

        private struct Bird
        {
            public Transform Body;
            public Transform WingL;
            public Transform WingR;
            public int Flock;
            public Vector3 Offset;
            public float Phase;
        }

        public static AmbientLife Attach(WeatherSystem weather)
        {
            var life = weather.GetComponent<AmbientLife>();
            if (life == null) // not '??': Unity's destroyed-object null must go through the overloaded ==
                life = weather.gameObject.AddComponent<AmbientLife>();
            life._weather = weather;
            return life;
        }

        private void Start()
        {
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }
            _additive = Resources.Load<Material>(WeatherSystem.RainMaterialResource) ?? Resources.Load<Material>(SpawnEffect.MaterialResource);
            CreateBirds();
            _fireflies = CreateParticles("Fireflies", new Color(0.75f, 1f, 0.45f, 1f), 0.16f, 90);
            _petals = CreateParticles("Petals", new Color(1f, 0.62f, 0.82f, 0.9f), 0.12f, 150);
        }

        private void Update()
        {
            if (_weather == null || !_weather.Running)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var plan = _weather.Plan;
            var profile = _weather.Profile;
            bool low = AppServices.IsInitialized && AppServices.Graphics.CurrentTier == GraphicsQualityTier.Low;

            // Birds: calm daylight only.
            bool birdWeather = plan.Time != TimeOfDay.Night && profile.Rain < 0.2f && profile.Fog < 0.7f;
            _birdsVisible = Mathf.MoveTowards(_birdsVisible, birdWeather ? 1f : 0f, Time.deltaTime * 0.2f);
            UpdateBirds(cam.transform.position);

            // Fireflies: calm, dry nights (Low tier: fewer).
            if (_fireflies != null)
            {
                bool on = plan.Time == TimeOfDay.Night && profile.Rain < 0.1f && profile.Wind < 0.6f;
                var emission = _fireflies.emission;
                emission.rateOverTime = on ? (low ? 6f : 14f) : 0f;
                _fireflies.transform.position = cam.transform.position + cam.transform.forward * 8f;
            }

            // Petals: near the nearest petal source, drifting with the wind.
            if (_petals != null)
            {
                var source = PetalSource.Nearest(cam.transform.position, out float distance);
                var emission = _petals.emission;
                bool on = source != null && distance < source.Radius + 40f && profile.Rain < 0.5f;
                emission.rateOverTime = on ? (low ? 6f : 16f) : 0f;
                if (source != null)
                    _petals.transform.position = Vector3.Lerp(source.transform.position, cam.transform.position, 0.6f) + Vector3.up * 6f;
                var velocity = _petals.velocityOverLifetime;
                var wind = Shader.GetGlobalVector("_NyxWind");
                velocity.x = wind.x * (0.6f + wind.w * 2f);
                velocity.z = wind.z * (0.6f + wind.w * 2f);
            }
        }

        // ------------------------------------------------------------------ birds

        private void CreateBirds()
        {
            var mesh = WingMesh();
            var material = Resources.Load<Material>("WarOfNyx/NyxToon");
            var random = new System.Random(19);
            for (int f = 0; f < Flocks; f++)
            for (int i = 0; i < BirdsPerFlock; i++)
            {
                var body = new GameObject("Bird").transform;
                body.SetParent(transform, false);
                var wingL = Wing(body, mesh, material, -1f);
                var wingR = Wing(body, mesh, material, 1f);
                // V formation behind the leader.
                int row = (i + 1) / 2;
                float side = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f);
                _birds.Add(new Bird
                {
                    Body = body, WingL = wingL, WingR = wingR, Flock = f,
                    Offset = new Vector3(side * row * 1.6f, (float)random.NextDouble() * 0.6f, -row * 1.8f),
                    Phase = (float)random.NextDouble() * 6f,
                });
            }
        }

        private static Transform Wing(Transform body, Mesh mesh, Material material, float side)
        {
            var go = new GameObject(side < 0 ? "WingL" : "WingR");
            go.transform.SetParent(body, false);
            go.transform.localScale = new Vector3(side, 1f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.16f)); // dark silhouette against the sky
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        /// <summary>One wing: a thin swept triangle, 0.9 m span, dark silhouette.</summary>
        private static Mesh WingMesh()
        {
            var mesh = new Mesh { name = "BirdWing (procedural)" };
            mesh.vertices = new[] { new Vector3(0f, 0f, 0.25f), new Vector3(0.9f, 0f, -0.15f), new Vector3(0f, 0f, -0.25f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateBirds(Vector3 camera)
        {
            float t = Time.time;
            bool show = _birdsVisible > 0.01f;
            for (int i = 0; i < _birds.Count; i++)
            {
                var bird = _birds[i];
                if (bird.Body.gameObject.activeSelf != show)
                    bird.Body.gameObject.SetActive(show);
                if (!show)
                    continue;
                // Each flock circles a slowly wandering point near (but not right above) the camera.
                float f = bird.Flock;
                float radius = 70f + f * 25f;
                float speed = 0.05f + f * 0.008f;
                float angle = t * speed + f * 1.7f;
                var centre = new Vector3(Mathf.Round(camera.x / 200f) * 200f + Mathf.Sin(f * 2.3f) * 120f, 0f,
                                         Mathf.Round(camera.z / 200f) * 200f + Mathf.Cos(f * 2.3f) * 120f);
                var leader = centre + new Vector3(Mathf.Cos(angle) * radius, 55f + f * 9f + Mathf.Sin(t * 0.3f + f) * 4f, Mathf.Sin(angle) * radius);
                var heading = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, 0f, -12f);
                bird.Body.SetPositionAndRotation(leader + rotation * bird.Offset, rotation);
                float flap = Mathf.Sin(t * 7f + bird.Phase) * 35f * _birdsVisible;
                bird.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                bird.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }

        // ------------------------------------------------------------------ particles

        private ParticleSystem CreateParticles(string name, Color color, float size, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = name == "Petals" ? 0.02f : 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = name == "Petals" ? 30f : 22f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = name == "Petals" ? 0.6f : 0.4f;
            noise.frequency = 0.3f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = name == "Petals" ? -0.35f : 0.05f;
            velocity.z = 0f;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (_additive != null)
                renderer.sharedMaterial = _additive;
            ps.Play();
            return ps;
        }
    }
}
