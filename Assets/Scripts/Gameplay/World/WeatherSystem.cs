using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.App;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.Settings;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Runtime atmosphere of a match: time-of-day lighting (sun / moon, ambient, fog, sky colours),
    /// weather (clouds, rain, wind, fog distance, lightning) and its sounds, blended over time from an
    /// <see cref="AtmospherePlan"/>. VISUAL AND AUDIO ONLY: nothing here touches gameplay state, and the
    /// fog never closes in below <see cref="Atmosphere.MinimumVisibilityMetres"/>.
    ///
    /// The plan comes from the match seed (offline: <c>OfflineMatch</c>; online: the replicated circle
    /// seed), so every player of a match sees the same weather. Scenes without a match keep their
    /// authored look (<see cref="Begin"/> is never called).
    /// "Reduce flashing" turns the lightning flash off (thunder stays).
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        public const string RainMaterialResource = "WarOfNyx/NyxRain";

        public static WeatherSystem Current { get; private set; }

        public AtmospherePlan Plan { get; private set; }
        public bool Running { get; private set; }
        public WeatherProfile Profile { get; private set; }
        /// <summary>Seconds since <see cref="Begin"/> (match time for the plan).</summary>
        public float Elapsed => Running ? Time.time - _startedAt + _startOffset : 0f;

        private Light _sun;
        private Material _sky;
        private float _startedAt;
        private float _startOffset;
        private float _windTime;
        private float _windAngle;
        private float _flash;
        private float _nextLightningAt;
        private float _nextAmbientUpdate;
        private System.Random _random;

        private ParticleSystem _rain;
        private ParticleSystem.EmissionModule _rainEmission;
        private ParticleSystem.VelocityOverLifetimeModule _rainVelocity;
        private int _rainMaxRate;
        private LineRenderer _bolt;
        private float _boltUntil;
        private AudioSource _rainAudio;
        private AudioSource _windAudio;
        private float _thunderAt = -1f;
        private float _thunderVolume;

        private static readonly int WindId = Shader.PropertyToID("_NyxWind");
        private static readonly int WindTimeId = Shader.PropertyToID("_NyxWindTime");
        private static readonly int RainId = Shader.PropertyToID("_NyxRain");
        private static readonly int CloudCoverId = Shader.PropertyToID("_NyxCloudCover");
        private static readonly int NightId = Shader.PropertyToID("_NyxNight");
        private static readonly int SunDirId = Shader.PropertyToID("_NyxSunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_NyxSunColor");
        private static readonly int FlashId = Shader.PropertyToID("_NyxFlash");

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        /// <summary>The scene's weather system, created on first use.</summary>
        public static WeatherSystem Ensure()
        {
            if (Current != null)
                return Current;
            return new GameObject("Weather (Nyx)").AddComponent<WeatherSystem>();
        }

        private void Awake()
        {
            Current = this;
            // Still air until a match starts the weather (keeps vegetation calm in menus and tests).
            Shader.SetGlobalVector(WindId, new Vector4(0.8f, 0f, 0.6f, 0.25f));
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
            GraphicsQualityService.TierChanged -= OnTierChanged;
            if (_sky != null)
                Destroy(_sky);
            // Leave globals neutral for the next scene.
            Shader.SetGlobalFloat(RainId, 0f);
            Shader.SetGlobalFloat(CloudCoverId, 0f);
            Shader.SetGlobalFloat(NightId, 0f);
            Shader.SetGlobalFloat(FlashId, 0f);
            Shader.SetGlobalVector(SunDirId, Vector4.zero);
        }

        /// <summary>Starts (or restarts) the atmosphere. <paramref name="matchSeconds"/> = how far into the match we already are.</summary>
        public void Begin(AtmospherePlan plan, float matchSeconds = 0f, int effectsSeed = 0)
        {
            Plan = plan;
            Running = true;
            _startedAt = Time.time;
            _startOffset = Mathf.Max(0f, matchSeconds);
            _random = new System.Random(effectsSeed != 0 ? effectsSeed : plan.GetHashCode());
            _windAngle = (float)(_random.NextDouble() * 360.0);
            _sun = FindSun();
            if (RenderSettings.skybox != null && _sky == null)
            {
                // Own instance: never edit the shared sky asset at runtime.
                _sky = new Material(RenderSettings.skybox) { name = "NyxSky (runtime)" };
                RenderSettings.skybox = _sky;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            if (!Application.isBatchMode)
            {
                CreateRain();
                CreateAudio();
                AmbientLife.Attach(this);
                GraphicsQualityService.TierChanged -= OnTierChanged;
                GraphicsQualityService.TierChanged += OnTierChanged;
                OnTierChanged(AppServices.IsInitialized ? AppServices.Graphics.CurrentTier : GraphicsQualityTier.Medium);
            }
            ScheduleLightning(WeatherProfile.For(plan.Start));
            _nextAmbientUpdate = 0f;
            Apply(0f, force: true);
            Debug.Log($"[WarOfNyx] Atmosphere: {plan}");
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;
            // Scene walk instead of FindObjectsByType: its overloads differ between Unity 6.3 and 6.6.
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>())
                    if (light.type == LightType.Directional)
                        return light;
            return null;
        }

        private void Update()
        {
            if (!Running)
                return;
            Apply(Time.deltaTime, force: false);
        }

        private void Apply(float dt, bool force)
        {
            var profile = Plan.ProfileAt(Elapsed);
            Profile = profile;
            var light = TimeOfDayLighting.For(Plan.Time);

            // Wind: direction wanders slowly; the shader clock speeds up with the wind.
            _windAngle += dt * (2f + profile.Wind * 6f);
            _windTime += dt * (0.7f + profile.Wind * 0.9f);
            var windDirection = Quaternion.Euler(0f, _windAngle, 0f) * Vector3.forward;
            Shader.SetGlobalVector(WindId, new Vector4(windDirection.x, 0f, windDirection.z, profile.Wind));
            Shader.SetGlobalFloat(WindTimeId, _windTime);
            Shader.SetGlobalFloat(RainId, profile.Rain);
            Shader.SetGlobalFloat(CloudCoverId, profile.CloudCover);
            Shader.SetGlobalFloat(NightId, light.Night);

            UpdateLightning(profile, dt);
            float flash = ReduceFlashing ? 0f : _flash;
            Shader.SetGlobalFloat(FlashId, flash);

            // Sun / moon.
            var sunRotation = Quaternion.Euler(light.Elevation, light.Yaw, 0f);
            var toSun = -(sunRotation * Vector3.forward);
            Shader.SetGlobalVector(SunDirId, new Vector4(toSun.x, toSun.y, toSun.z, 1f));
            Shader.SetGlobalColor(SunColorId, light.Disc);
            if (_sun != null)
            {
                _sun.transform.rotation = sunRotation;
                _sun.color = Color.Lerp(light.SunColor, new Color(0.8f, 0.85f, 1f), flash * 0.8f);
                _sun.intensity = light.SunIntensity * (1f - profile.Dim * 0.75f) + flash * 2.5f;
                _sun.shadowStrength = Mathf.Lerp(1f, 0.45f, profile.Dim);
            }

            // Sky colours (on our instance), greyed by cloud cover.
            if (_sky != null)
            {
                _sky.SetColor("_TopColor", light.SkyTop);
                _sky.SetColor("_HorizonColor", light.SkyHorizon);
                _sky.SetColor("_BottomColor", light.SkyBottom);
                _sky.SetColor("_GlowColor", light.Glow);
                _sky.SetColor("_GlowColor2", light.Glow2);
                _sky.SetFloat("_GlowHeight", light.GlowHeight);
                _sky.SetColor("_CloudLit", light.CloudLit);
                _sky.SetColor("_CloudShade", light.CloudShade);
            }

            // Fog: colour toward the cloud shade, distance from the fairness-capped visibility.
            float visibility = Atmosphere.VisibilityMetres(profile);
            RenderSettings.fogColor = Color.Lerp(light.Fog, light.CloudShade * 1.15f, profile.CloudCover * 0.55f) + Color.white * (flash * 0.15f);
            RenderSettings.fogEndDistance = visibility;
            RenderSettings.fogStartDistance = Mathf.Lerp(160f, 15f, profile.Fog);

            // Ambient: flatter and greyer under clouds.
            float grey = profile.Dim * 0.5f;
            RenderSettings.ambientSkyColor = Desaturate(light.AmbientSky, grey) + Color.white * (flash * 0.35f);
            RenderSettings.ambientEquatorColor = Desaturate(light.AmbientEquator, grey);
            RenderSettings.ambientGroundColor = light.AmbientGround;
            if (force || Time.time >= _nextAmbientUpdate || flash > 0.01f)
            {
                _nextAmbientUpdate = Time.time + 0.5f;
                DynamicGI.UpdateEnvironment();
            }

            UpdateRain(profile, windDirection);
            UpdateAudio(profile);
        }

        private static Color Desaturate(Color c, float amount)
        {
            float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return Color.Lerp(c, new Color(l, l, l), amount);
        }

        // ------------------------------------------------------------------ lightning

        private void ScheduleLightning(WeatherProfile profile)
        {
            float perMinute = Mathf.Max(0.0001f, profile.LightningPerMinute);
            float mean = 60f / perMinute;
            _nextLightningAt = Time.time + Mathf.Clamp(-Mathf.Log(1f - (float)_random.NextDouble() * 0.999f) * mean, 4f, 600f);
        }

        private void UpdateLightning(WeatherProfile profile, float dt)
        {
            if (profile.LightningPerMinute > 0.2f && Time.time >= _nextLightningAt)
            {
                Strike();
                ScheduleLightning(profile);
            }
            else if (profile.LightningPerMinute <= 0.2f)
            {
                _nextLightningAt = Mathf.Max(_nextLightningAt, Time.time + 5f);
            }

            // Double-pulse flash: bright, brief dip, second pulse, fade.
            if (_flash > 0f)
                _flash = Mathf.Max(0f, _flash - dt * 3.2f);
            if (_bolt != null && _bolt.enabled && Time.time >= _boltUntil)
                _bolt.enabled = false;
            if (_thunderAt > 0f && Time.time >= _thunderAt)
            {
                _thunderAt = -1f;
                if (!Application.isBatchMode)
                    PlayTwoD(ProceduralAudio.Thunder, _thunderVolume);
            }
        }

        private void Strike()
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            float angle = (float)(_random.NextDouble() * Mathf.PI * 2.0);
            float distance = Mathf.Lerp(250f, 700f, (float)_random.NextDouble());
            var ground = cam.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            ground.y = 0f;

            _flash = 1f;
            _thunderAt = Time.time + distance / 343f;
            _thunderVolume = Mathf.Lerp(0.9f, 0.45f, Mathf.InverseLerp(250f, 700f, distance));
            if (_bolt != null && !ReduceFlashing)
            {
                const int points = 14;
                _bolt.positionCount = points;
                var top = ground + Vector3.up * 220f;
                for (int i = 0; i < points; i++)
                {
                    float t = i / (float)(points - 1);
                    var p = Vector3.Lerp(top, ground, t);
                    if (i > 0 && i < points - 1)
                        p += new Vector3((float)_random.NextDouble() - 0.5f, 0f, (float)_random.NextDouble() - 0.5f) * 28f;
                    _bolt.SetPosition(i, p);
                }
                _bolt.enabled = true;
                _boltUntil = Time.time + 0.22f;
            }
        }

        // ------------------------------------------------------------------ rain

        private void OnTierChanged(GraphicsQualityTier tier)
        {
            _rainMaxRate = tier == GraphicsQualityTier.Low ? 350 : tier == GraphicsQualityTier.Medium ? 1100 : 2200;
            if (_rain != null)
            {
                var main = _rain.main;
                main.maxParticles = Mathf.CeilToInt(_rainMaxRate * 1.3f);
            }
        }

        private void CreateRain()
        {
            if (_rain != null)
                return;
            var material = Resources.Load<Material>(RainMaterialResource) ?? Resources.Load<Material>(SpawnEffect.MaterialResource);
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // box emits along local +Z = world down
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _rain.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 26f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.8f, 1f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            main.playOnAwake = false;

            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 46f, 1f);

            _rainEmission = _rain.emission;
            _rainEmission.rateOverTime = 0f;
            _rainVelocity = _rain.velocityOverLifetime;
            _rainVelocity.enabled = true;
            _rainVelocity.space = ParticleSystemSimulationSpace.World;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
                renderer.sharedMaterial = material;
            _rain.Play();

            var boltGo = new GameObject("Lightning");
            boltGo.transform.SetParent(transform, false);
            _bolt = boltGo.AddComponent<LineRenderer>();
            _bolt.useWorldSpace = true;
            _bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 2.2f), new Keyframe(1f, 0.6f));
            _bolt.startColor = _bolt.endColor = new Color(0.85f, 0.85f, 1f, 1f);
            _bolt.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null)
                _bolt.sharedMaterial = material;
            _bolt.enabled = false;
        }

        private void UpdateRain(WeatherProfile profile, Vector3 windDirection)
        {
            if (_rain == null)
                return;
            var cam = Camera.main;
            if (cam != null)
            {
                // Emitter above and slightly upwind of the camera, so slanted drops land around the player.
                var p = cam.transform.position + Vector3.up * 16f - windDirection * (profile.Wind * 6f);
                _rain.transform.position = p;
            }
            _rainEmission.rateOverTime = profile.Rain * _rainMaxRate;
            float slant = 2f + profile.Wind * 9f;
            _rainVelocity.x = windDirection.x * slant;
            _rainVelocity.z = windDirection.z * slant;
            _rainVelocity.y = 0f;
        }

        // ------------------------------------------------------------------ audio

        private void CreateAudio()
        {
            if (_rainAudio != null)
                return;
            _rainAudio = Loop("RainAudio", ProceduralAudio.RainLoop);
            _windAudio = Loop("WindAudio", ProceduralAudio.WindLoop);
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        private void UpdateAudio(WeatherProfile profile)
        {
            float sfx = ProceduralAudio.SfxVolume;
            if (_rainAudio != null)
                _rainAudio.volume = Mathf.MoveTowards(_rainAudio.volume, profile.Rain * 0.45f * sfx, Time.deltaTime * 0.5f);
            if (_windAudio != null)
                _windAudio.volume = Mathf.MoveTowards(_windAudio.volume, Mathf.Clamp01(profile.Wind - 0.25f) * 0.4f * sfx, Time.deltaTime * 0.5f);
        }

        private void PlayTwoD(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            var go = new GameObject("Thunder");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.volume = Mathf.Clamp01(volume * ProceduralAudio.SfxVolume);
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }
    }
}
