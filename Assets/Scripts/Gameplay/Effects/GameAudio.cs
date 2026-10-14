using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Tiny offline synthesizer for PLACEHOLDER sound effects (no audio files): noise and sine layers,
    /// one-pole filters, envelopes, clicks, metallic rings, echoes and soft clipping. Deterministic per seed.
    /// </summary>
    internal sealed class Synth
    {
        public readonly float[] Data;
        public readonly int SampleRate;
        private readonly System.Random _random;

        public Synth(float seconds, int sampleRate, int seed)
        {
            SampleRate = sampleRate;
            Data = new float[Mathf.Max(1, (int)(seconds * sampleRate))];
            _random = new System.Random(seed);
        }

        public float Rand() => (float)_random.NextDouble();

        private float White() => (float)_random.NextDouble() * 2f - 1f;

        private float Coef(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / SampleRate);

        /// <summary>Filtered noise burst: attack, exponential decay (tau), band between highPass and lowPass (Hz, 0 = off).</summary>
        public void Noise(float start, float length, float gain, float attack, float tau, float lowPass, float highPass, float lowPassEnd = 0f)
        {
            int s0 = (int)(start * SampleRate), n = (int)(length * SampleRate);
            float lp1 = 0f, lp2 = 0f, hpState = 0f;
            float aHp = highPass > 0f ? Coef(highPass) : 0f;
            for (int i = 0; i < n && s0 + i < Data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float cutoff = lowPassEnd > 0f ? Mathf.Lerp(lowPass, lowPassEnd, t / Mathf.Max(length, 1e-4f)) : lowPass;
                float aLp = cutoff > 0f ? Coef(cutoff) : 1f;
                float x = White();
                lp1 += aLp * (x - lp1);
                lp2 += aLp * (lp1 - lp2);
                float y = cutoff > 0f ? lp2 : x;
                if (aHp > 0f)
                {
                    hpState += aHp * (y - hpState);
                    y -= hpState;
                }
                float env = (attack > 0f ? Mathf.Clamp01(t / attack) : 1f) * Mathf.Exp(-t / Mathf.Max(tau, 1e-4f));
                if (s0 + i >= 0)
                    Data[s0 + i] += y * gain * env;
            }
        }

        /// <summary>Sine sweeping from f0 to f1 over the length, exponential decay.</summary>
        public void Tone(float start, float f0, float f1, float length, float gain, float tau, float attack = 0.001f)
        {
            int s0 = (int)(start * SampleRate), n = (int)(length * SampleRate);
            float phase = 0f;
            for (int i = 0; i < n && s0 + i < Data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(f0, f1, Mathf.Clamp01(t / Mathf.Max(length * 0.5f, 1e-4f)));
                phase += 2f * Mathf.PI * f / SampleRate;
                float env = Mathf.Clamp01(t / attack) * Mathf.Exp(-t / Mathf.Max(tau, 1e-4f));
                if (s0 + i >= 0)
                    Data[s0 + i] += Mathf.Sin(phase) * gain * env;
            }
        }

        /// <summary>Short mechanical click: a damped high sine plus a tick of noise.</summary>
        public void Click(float start, float freq, float gain, float tau = 0.004f)
        {
            Tone(start, freq, freq * 0.85f, tau * 8f, gain, tau);
            Noise(start, tau * 4f, gain * 0.6f, 0f, tau * 0.6f, 9000f, 1500f);
        }

        /// <summary>Metallic ring: inharmonic partials decaying together.</summary>
        public void Ring(float start, float[] freqs, float gain, float tau)
        {
            for (int k = 0; k < freqs.Length; k++)
                Tone(start, freqs[k], freqs[k], tau * 6f, gain / (1f + k * 0.6f), tau * (1f - k * 0.12f));
        }

        /// <summary>Granular crunch: many tiny noise grains (gravel, dirt, leaves).</summary>
        public void Grains(float start, float length, int count, float gain, float lowPass)
        {
            for (int k = 0; k < count; k++)
                Noise(start + Rand() * length, 0.006f, gain * (0.4f + Rand() * 0.6f), 0f, 0.0015f, lowPass, 400f);
        }

        /// <summary>Adds a delayed, quieter, duller copy (slapback / outdoor echo).</summary>
        public void Echo(float delay, float gain, float lowPass)
        {
            int d = (int)(delay * SampleRate);
            float a = Coef(lowPass), lp = 0f;
            for (int i = Data.Length - 1; i >= d; i--)
            {
                lp += a * (Data[i - d] - lp);
                Data[i] += lp * gain;
            }
        }

        public void SoftClip(float drive)
        {
            for (int i = 0; i < Data.Length; i++)
                Data[i] = (float)System.Math.Tanh(Data[i] * drive);
        }

        public void Normalize(float peak)
        {
            float max = 1e-6f;
            foreach (float v in Data)
                max = Mathf.Max(max, Mathf.Abs(v));
            float g = peak / max;
            for (int i = 0; i < Data.Length; i++)
                Data[i] *= g;
            // 3 ms fade-out so a clip never ends on a click.
            int fade = Mathf.Min(Data.Length, SampleRate * 3 / 1000);
            for (int i = 0; i < fade; i++)
                Data[Data.Length - 1 - i] *= i / (float)fade;
        }

        public AudioClip ToClip(string name)
        {
            var clip = AudioClip.Create(name + " (procedural)", Data.Length, 1, SampleRate, false);
            clip.SetData(Data, 0);
            return clip;
        }
    }

    /// <summary>
    /// Pooled 3-D audio sources, so automatic fire from many characters doesn't create and destroy a
    /// GameObject per shot. Sources can follow a transform (reloads, footsteps on a moving character).
    /// </summary>
    public sealed class AudioPool : MonoBehaviour
    {
        private const int Size = 48;
        private static AudioPool _instance;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly Dictionary<AudioSource, Transform> _follow = new Dictionary<AudioSource, Transform>();
        private int _next;

        private static AudioPool Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                var go = new GameObject("AudioPool (procedural sfx)");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AudioPool>();
                for (int i = 0; i < Size; i++)
                {
                    var child = new GameObject("Voice");
                    child.transform.SetParent(go.transform, false);
                    var s = child.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 1f;
                    s.dopplerLevel = 0f;
                    _instance._sources.Add(s);
                }
                return _instance;
            }
        }

        /// <summary>Plays a clip at a position (or following <paramref name="follow"/>). Returns the source (to stop it early).</summary>
        public static AudioSource Play(AudioClip clip, Vector3 position, float volume, float pitch, float minDistance, float maxDistance,
            Transform follow = null, bool spatial = true, AudioRolloffMode rolloff = AudioRolloffMode.Linear)
        {
            if (clip == null || Application.isBatchMode)
                return null;
            var pool = Instance;
            // Prefer a free voice; otherwise steal the oldest-started one.
            AudioSource source = null;
            for (int k = 0; k < pool._sources.Count; k++)
            {
                var s = pool._sources[(pool._next + k) % pool._sources.Count];
                if (!s.isPlaying)
                {
                    source = s;
                    break;
                }
            }
            if (source == null)
                source = pool._sources[pool._next];
            pool._next = (pool._sources.IndexOf(source) + 1) % pool._sources.Count;

            source.Stop();
            source.clip = clip;
            source.transform.position = follow != null ? follow.position : position;
            source.volume = Mathf.Clamp01(volume * ProceduralAudio.SfxVolume);
            source.pitch = pitch;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = rolloff;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            if (follow != null)
                pool._follow[source] = follow;
            else
                pool._follow.Remove(source);
            source.Play();
            return source;
        }

        private void LateUpdate()
        {
            if (_follow.Count == 0)
                return;
            List<AudioSource> done = null;
            foreach (var pair in _follow)
            {
                if (!pair.Key.isPlaying || pair.Value == null)
                {
                    (done ??= new List<AudioSource>()).Add(pair.Key);
                    continue;
                }
                pair.Key.transform.position = pair.Value.position;
            }
            if (done != null)
                foreach (var s in done)
                    _follow.Remove(s);
        }

        /// <summary>Distance from the listener (camera) to a point.</summary>
        public static float ListenerDistance(Vector3 position)
        {
            var cam = Camera.main;
            return cam != null ? Vector3.Distance(cam.transform.position, position) : 0f;
        }
    }

    /// <summary>
    /// Gunfire and reload sounds per weapon (PLACEHOLDER synthesis; swap for recorded audio later):
    ///  * a shot is layered from a supersonic crack, the muzzle blast body, a low thump, the weapon's action
    ///    (pistol slide, shotgun pump, marksman bolt) and an outdoor echo tail; three variants per weapon and a
    ///    small pitch jitter keep automatic fire from sounding looped;
    ///  * beyond ~45 m a muffled distant version is used (rolling boom, longer echo, no crack), audible to 450 m;
    ///  * reloads are timed to the weapon's reload length: magazine release, magazine out, magazine in, bolt
    ///    rack (rifles / SMGs), slide release (pistol), shell-by-shell and pump (shotgun), bolt (marksman);
    ///  * an empty-magazine dry click.
    /// </summary>
    public static class GunAudio
    {
        private struct Profile
        {
            public float Crack, BodyCutoff, BodyTau, ThumpHz, Thump, Tail, TailTau, Length;
            public float MechAt; // seconds after the shot for the action sound (0 = none)
            public int Mech;     // 0 none, 1 pistol slide, 2 shotgun pump, 3 bolt
        }

        private static Profile For(WeaponStats w)
        {
            switch (w != null ? w.id : string.Empty)
            {
                case WeaponCatalog.ShadowSmg:
                case WeaponCatalog.StormLightSmg:
                    return new Profile { Crack = 0.8f, BodyCutoff = 4200f, BodyTau = 0.04f, ThumpHz = 120f, Thump = 0.35f, Tail = 0.18f, TailTau = 0.28f, Length = 0.45f };
                case WeaponCatalog.CrimsonShotgun:
                    return new Profile { Crack = 0.6f, BodyCutoff = 1700f, BodyTau = 0.13f, ThumpHz = 58f, Thump = 1f, Tail = 0.4f, TailTau = 0.65f, Length = 1.25f, MechAt = 0.5f, Mech = 2 };
                case WeaponCatalog.PhantomMarksman:
                    return new Profile { Crack = 1f, BodyCutoff = 2600f, BodyTau = 0.11f, ThumpHz = 52f, Thump = 0.95f, Tail = 0.5f, TailTau = 0.9f, Length = 1.4f, MechAt = 0.55f, Mech = 3 };
                case WeaponCatalog.NeonPistol:
                    return new Profile { Crack = 0.85f, BodyCutoff = 3700f, BodyTau = 0.05f, ThumpHz = 140f, Thump = 0.45f, Tail = 0.18f, TailTau = 0.3f, Length = 0.5f, MechAt = 0.025f, Mech = 1 };
                case WeaponCatalog.VortexLmg:
                    return new Profile { Crack = 0.95f, BodyCutoff = 2800f, BodyTau = 0.08f, ThumpHz = 75f, Thump = 0.8f, Tail = 0.35f, TailTau = 0.55f, Length = 0.7f };
                case WeaponCatalog.ArcSniper:
                    return new Profile { Crack = 1f, BodyCutoff = 2200f, BodyTau = 0.14f, ThumpHz = 45f, Thump = 1f, Tail = 0.6f, TailTau = 1.1f, Length = 1.7f, MechAt = 0.6f, Mech = 3 };
                case WeaponCatalog.TalonAutoShotgun:
                    return new Profile { Crack = 0.6f, BodyCutoff = 1900f, BodyTau = 0.1f, ThumpHz = 62f, Thump = 0.9f, Tail = 0.35f, TailTau = 0.5f, Length = 0.9f };
                case WeaponCatalog.RiftCarbine:
                    return new Profile { Crack = 0.85f, BodyCutoff = 3400f, BodyTau = 0.055f, ThumpHz = 100f, Thump = 0.55f, Tail = 0.24f, TailTau = 0.4f, Length = 0.55f };
                case WeaponCatalog.ViperRevolver:
                    return new Profile { Crack = 0.9f, BodyCutoff = 2400f, BodyTau = 0.09f, ThumpHz = 70f, Thump = 0.85f, Tail = 0.4f, TailTau = 0.7f, Length = 1f };
                case WeaponCatalog.PulseDmr:
                    return new Profile { Crack = 1f, BodyCutoff = 3000f, BodyTau = 0.09f, ThumpHz = 65f, Thump = 0.8f, Tail = 0.4f, TailTau = 0.7f, Length = 1f };
                case WeaponCatalog.StormBurstRifle:
                    return new Profile { Crack = 0.95f, BodyCutoff = 3600f, BodyTau = 0.06f, ThumpHz = 95f, Thump = 0.6f, Tail = 0.28f, TailTau = 0.45f, Length = 0.6f };
                default: // NX-7 assault rifle and unknown rifles
                    return new Profile { Crack = 0.9f, BodyCutoff = 3200f, BodyTau = 0.07f, ThumpHz = 88f, Thump = 0.65f, Tail = 0.3f, TailTau = 0.5f, Length = 0.65f };
            }
        }

        private const int Variants = 3;
        private static readonly Dictionary<string, AudioClip[]> Near = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, AudioClip[]> Far = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, AudioClip> Reloads = new Dictionary<string, AudioClip>();
        private static AudioClip _dryClick;

        public const float FarDistance = 45f;

        public static void PlayShot(WeaponStats weapon, Vector3 position)
        {
            if (Application.isBatchMode)
                return;
            string key = weapon != null ? weapon.id : "default";
            float distance = AudioPool.ListenerDistance(position);
            bool far = distance > FarDistance;
            var clips = far ? Get(Far, key, weapon, true) : Get(Near, key, weapon, false);
            var clip = clips[Random.Range(0, clips.Length)];
            float pitch = Random.Range(0.96f, 1.04f);
            if (far)
                AudioPool.Play(clip, position, 0.95f, pitch, 30f, 450f, rolloff: AudioRolloffMode.Logarithmic);
            else
                AudioPool.Play(clip, position, 1f, pitch, 4f, 450f, rolloff: AudioRolloffMode.Logarithmic);
        }

        /// <summary>The (cached) shot clips of a weapon: near or distant versions. Also used by tests.</summary>
        public static AudioClip[] ShotClips(WeaponStats weapon, bool far)
        {
            string key = weapon != null ? weapon.id : "default";
            return far ? Get(Far, key, weapon, true) : Get(Near, key, weapon, false);
        }

        /// <summary>The (cached) reload clip of a ranged weapon. Also used by tests.</summary>
        public static AudioClip ReloadClip(WeaponStats weapon)
        {
            string key = weapon.id + "_" + weapon.reloadSeconds.ToString("0.00");
            if (!Reloads.TryGetValue(key, out var clip))
                Reloads[key] = clip = Reload(weapon);
            return clip;
        }

        private static AudioClip[] Get(Dictionary<string, AudioClip[]> cache, string key, WeaponStats weapon, bool far)
        {
            if (cache.TryGetValue(key, out var clips))
                return clips;
            var p = For(weapon);
            clips = new AudioClip[Variants];
            for (int v = 0; v < Variants; v++)
                clips[v] = far ? Distant(p, key, v) : Shot(p, key, v);
            cache[key] = clips;
            return clips;
        }

        private static AudioClip Shot(Profile p, string key, int variant)
        {
            var s = new Synth(p.Length, 44100, key.GetHashCode() * 31 + variant);
            float j = 1f + (s.Rand() - 0.5f) * 0.08f;
            s.Noise(0f, 0.014f, 1.4f * p.Crack, 0f, 0.0028f, 0f, 2200f);                         // supersonic crack
            s.Noise(0f, 0.25f, 1.2f, 0.0008f, p.BodyTau * j, p.BodyCutoff * j, 60f);               // muzzle blast
            s.Noise(0.003f, 0.12f, 0.45f, 0.001f, p.BodyTau * 0.5f, p.BodyCutoff * 2f, 1500f);     // bright edge
            s.Tone(0f, p.ThumpHz * 2.2f, p.ThumpHz, 0.18f, p.Thump * 1.2f, 0.055f);                 // chest thump
            s.Noise(0.015f, p.Length, p.Tail, 0.01f, p.TailTau, 900f, 80f);                         // air / room tail
            Mechanism(s, p);
            s.Echo(0.11f + s.Rand() * 0.03f, 0.22f, 1500f);                                         // slapback
            s.Echo(0.27f + s.Rand() * 0.05f, 0.12f, 900f);
            s.SoftClip(1.7f);
            s.Normalize(0.95f);
            return s.ToClip($"Shot_{key}_{variant}");
        }

        private static AudioClip Distant(Profile p, string key, int variant)
        {
            var s = new Synth(p.Length + 0.9f, 22050, key.GetHashCode() * 17 + variant);
            s.Noise(0f, 0.6f, 1f, 0.004f, p.BodyTau * 1.8f, 650f, 40f);                              // dull boom
            s.Tone(0f, p.ThumpHz * 1.5f, p.ThumpHz * 0.8f, 0.3f, p.Thump * 1.1f, 0.09f, 0.004f);
            s.Noise(0.03f, p.Length + 0.8f, p.Tail * 1.4f, 0.03f, p.TailTau * 1.8f, 420f, 40f);      // rolling tail
            s.Echo(0.32f + s.Rand() * 0.1f, 0.35f, 500f);
            s.Echo(0.7f + s.Rand() * 0.15f, 0.2f, 350f);
            s.SoftClip(1.4f);
            s.Normalize(0.8f);
            return s.ToClip($"ShotFar_{key}_{variant}");
        }

        private static void Mechanism(Synth s, Profile p)
        {
            switch (p.Mech)
            {
                case 1: // pistol slide cycling
                    s.Click(p.MechAt, 3100f, 0.35f, 0.003f);
                    s.Click(p.MechAt + 0.035f, 2600f, 0.3f, 0.003f);
                    break;
                case 2: // shotgun pump: back, then forward
                    s.Noise(p.MechAt, 0.09f, 0.35f, 0.02f, 0.05f, 2500f, 600f);
                    s.Click(p.MechAt + 0.08f, 1700f, 0.6f, 0.006f);
                    s.Noise(p.MechAt + 0.16f, 0.07f, 0.3f, 0.015f, 0.04f, 2500f, 600f);
                    s.Click(p.MechAt + 0.23f, 1500f, 0.7f, 0.007f);
                    s.Ring(p.MechAt + 0.23f, new[] { 980f, 1730f, 2840f }, 0.12f, 0.05f);
                    break;
                case 3: // bolt: up, back, forward, down
                    s.Click(p.MechAt, 2300f, 0.4f, 0.004f);
                    s.Noise(p.MechAt + 0.05f, 0.1f, 0.25f, 0.02f, 0.05f, 3000f, 700f);
                    s.Click(p.MechAt + 0.16f, 2000f, 0.5f, 0.005f);
                    s.Noise(p.MechAt + 0.24f, 0.08f, 0.22f, 0.02f, 0.04f, 3000f, 700f);
                    s.Click(p.MechAt + 0.33f, 2600f, 0.6f, 0.005f);
                    s.Ring(p.MechAt + 0.33f, new[] { 1450f, 2380f, 3720f }, 0.1f, 0.04f);
                    break;
            }
        }

        /// <summary>Reload sound for a weapon, following the character. Returns the source so a cancelled reload can stop it.</summary>
        public static AudioSource PlayReload(WeaponStats weapon, Transform character)
        {
            if (weapon == null || !weapon.IsRanged || Application.isBatchMode || character == null)
                return null;
            return AudioPool.Play(ReloadClip(weapon), character.position + Vector3.up * 1.2f, 0.8f, 1f, 2f, 28f, character);
        }

        private static AudioClip Reload(WeaponStats w)
        {
            float r = Mathf.Max(0.6f, w.reloadSeconds);
            var s = new Synth(r + 0.25f, 22050, w.id.GetHashCode());
            void MagOut(float t)
            {
                s.Click(t, 2600f, 0.35f, 0.003f);                                // release button
                s.Noise(t + 0.05f, 0.16f, 0.3f, 0.03f, 0.06f, 3200f, 700f);      // slides out
                s.Noise(t + 0.25f, 0.2f, 0.12f, 0.04f, 0.08f, 1800f, 300f);      // pouch rustle
            }
            void MagIn(float t)
            {
                s.Noise(t - 0.07f, 0.08f, 0.25f, 0.02f, 0.03f, 3200f, 700f);
                s.Tone(t, 170f, 95f, 0.08f, 0.55f, 0.025f);                      // seats with a thud
                s.Click(t, 2200f, 0.55f, 0.004f);
            }
            void Rack(float t)
            {
                s.Click(t, 3000f, 0.45f, 0.003f);
                s.Ring(t, new[] { 1450f, 2300f, 3700f }, 0.1f, 0.04f);
                s.Click(t + 0.1f, 2600f, 0.55f, 0.004f);
                s.Ring(t + 0.1f, new[] { 1300f, 2150f, 3400f }, 0.12f, 0.05f);
            }

            switch (w.id)
            {
                case WeaponCatalog.CrimsonShotgun:
                {
                    int shells = 4;
                    for (int k = 0; k < shells; k++)
                    {
                        float t = 0.2f + k * (r * 0.62f / shells);
                        s.Noise(t - 0.05f, 0.05f, 0.18f, 0.01f, 0.02f, 2500f, 500f);
                        s.Click(t, 1500f, 0.45f, 0.006f);
                        s.Tone(t, 260f, 200f, 0.05f, 0.25f, 0.015f);
                    }
                    s.Noise(r * 0.82f, 0.09f, 0.35f, 0.02f, 0.05f, 2500f, 600f);
                    s.Click(r * 0.82f + 0.08f, 1700f, 0.6f, 0.006f);
                    s.Noise(r * 0.82f + 0.15f, 0.07f, 0.3f, 0.015f, 0.04f, 2500f, 600f);
                    s.Click(r * 0.82f + 0.22f, 1500f, 0.7f, 0.007f);
                    break;
                }
                case WeaponCatalog.NeonPistol:
                    MagOut(0.05f);
                    MagIn(r * 0.6f);
                    s.Click(r * 0.86f, 3200f, 0.6f, 0.003f);                    // slide release
                    s.Ring(r * 0.86f, new[] { 1700f, 2900f, 4100f }, 0.1f, 0.03f);
                    break;
                case WeaponCatalog.ViperRevolver:
                    s.Click(0.05f, 2100f, 0.5f, 0.004f);                        // cylinder swings out
                    for (int k = 0; k < 6; k++)
                        s.Click(0.2f + k * 0.025f, 4200f, 0.25f, 0.002f);       // empty cases fall
                    s.Noise(r * 0.55f, 0.06f, 0.3f, 0.01f, 0.03f, 3000f, 800f); // speed loader
                    s.Click(r * 0.85f, 2400f, 0.65f, 0.005f);                   // cylinder closes
                    s.Ring(r * 0.85f, new[] { 1500f, 2600f, 3900f }, 0.1f, 0.04f);
                    break;
                case WeaponCatalog.PhantomMarksman:
                case WeaponCatalog.ArcSniper:
                    s.Click(0.05f, 2300f, 0.4f, 0.004f);                        // bolt up and back
                    s.Noise(0.1f, 0.1f, 0.25f, 0.02f, 0.05f, 3000f, 700f);
                    MagOut(0.3f);
                    MagIn(r * 0.6f);
                    s.Noise(r * 0.8f, 0.08f, 0.22f, 0.02f, 0.04f, 3000f, 700f);  // bolt forward and down
                    s.Click(r * 0.86f, 2600f, 0.6f, 0.005f);
                    break;
                default:
                    MagOut(0.06f);
                    MagIn(r * 0.58f);
                    Rack(r * 0.8f);
                    break;
            }
            s.Normalize(0.85f);
            return s.ToClip($"Reload_{w.id}");
        }

        /// <summary>Trigger pulled on an empty magazine.</summary>
        public static void PlayDryFire(Vector3 position)
        {
            if (_dryClick == null)
            {
                var s = new Synth(0.08f, 22050, 99);
                s.Click(0f, 2800f, 0.7f, 0.003f);
                s.Click(0.012f, 1900f, 0.3f, 0.003f);
                s.Normalize(0.7f);
                _dryClick = s.ToClip("DryFire");
            }
            AudioPool.Play(_dryClick, position, 0.6f, Random.Range(0.97f, 1.03f), 1f, 12f);
        }
    }

    /// <summary>What a foot lands on.</summary>
    public enum FootSurface : byte
    {
        Grass = 0,
        Dirt = 1,
        Stone = 2,
        Metal = 3,
        Wood = 4,
    }

    /// <summary>
    /// Footsteps, jumps, landings and slides (PLACEHOLDER synthesis): four variants per surface, louder and
    /// heard further the faster the gait (walk ≈ 15 m, run ≈ 28 m, sprint ≈ 40 m, crouch ≈ 8 m). Positional,
    /// so enemies can be heard approaching; your own steps are played softer.
    /// </summary>
    public static class FootstepAudio
    {
        private const int Variants = 4;
        private static readonly Dictionary<FootSurface, AudioClip[]> Steps = new Dictionary<FootSurface, AudioClip[]>();
        private static AudioClip _jump, _land, _slide;

        public static void PlayStep(FootSurface surface, float intensity, Vector3 position, bool own)
        {
            if (Application.isBatchMode)
                return;
            var clips = StepClips(surface);
            float volume = Mathf.Lerp(0.25f, 0.9f, intensity) * (own ? 0.55f : 1f);
            float range = Mathf.Lerp(8f, 40f, intensity);
            AudioPool.Play(clips[Random.Range(0, Variants)], position, volume, Random.Range(0.93f, 1.07f), 1.5f, range);
        }

        /// <summary>The (cached) footstep variants for a surface. Also used by tests.</summary>
        public static AudioClip[] StepClips(FootSurface surface)
        {
            if (!Steps.TryGetValue(surface, out var clips))
            {
                clips = new AudioClip[Variants];
                for (int v = 0; v < Variants; v++)
                    clips[v] = Step(surface, v);
                Steps[surface] = clips;
            }
            return clips;
        }

        public static void PlayJump(FootSurface surface, Vector3 position, bool own)
        {
            if (Application.isBatchMode)
                return;
            if (_jump == null)
            {
                var s = new Synth(0.3f, 22050, 7);
                s.Noise(0f, 0.25f, 0.5f, 0.02f, 0.08f, 900f, 200f, 3200f); // cloth whoosh
                s.Normalize(0.6f);
                _jump = s.ToClip("Jump");
            }
            AudioPool.Play(_jump, position, own ? 0.45f : 0.7f, Random.Range(0.95f, 1.05f), 1.5f, 25f);
            PlayStep(surface, 0.6f, position, own);
        }

        public static void PlayLand(FootSurface surface, float impact, Vector3 position, bool own)
        {
            if (Application.isBatchMode)
                return;
            if (_land == null)
            {
                var s = new Synth(0.35f, 22050, 8);
                s.Tone(0f, 110f, 55f, 0.2f, 0.9f, 0.07f);
                s.Noise(0f, 0.2f, 0.5f, 0.002f, 0.05f, 900f, 60f);
                s.Normalize(0.8f);
                _land = s.ToClip("Land");
            }
            float v = Mathf.Clamp01(impact);
            AudioPool.Play(_land, position, (0.4f + 0.6f * v) * (own ? 0.6f : 1f), Random.Range(0.95f, 1.05f), 2f, 35f);
            PlayStep(surface, 0.7f + 0.3f * v, position, own);
        }

        public static void PlaySlide(Vector3 position, Transform follow, bool own)
        {
            if (Application.isBatchMode)
                return;
            if (_slide == null)
            {
                var s = new Synth(0.7f, 22050, 9);
                s.Noise(0f, 0.7f, 0.5f, 0.03f, 0.3f, 2200f, 250f, 900f); // scrape, darkening as it slows
                s.Grains(0.02f, 0.55f, 60, 0.25f, 5000f);
                s.Normalize(0.7f);
                _slide = s.ToClip("Slide");
            }
            AudioPool.Play(_slide, position, own ? 0.55f : 0.85f, 1f, 2f, 30f, follow);
        }

        private static AudioClip Step(FootSurface surface, int variant)
        {
            var s = new Synth(0.32f, 22050, (int)surface * 101 + variant * 7);
            float j = 1f + (s.Rand() - 0.5f) * 0.15f;
            switch (surface)
            {
                case FootSurface.Grass:
                    s.Noise(0f, 0.16f, 0.6f, 0.012f, 0.045f, 6500f * j, 1600f);   // swish of blades
                    s.Noise(0.035f, 0.12f, 0.35f, 0.01f, 0.035f, 5000f * j, 1200f);
                    s.Tone(0f, 95f, 70f, 0.06f, 0.2f, 0.02f);
                    break;
                case FootSurface.Dirt:
                    s.Grains(0f, 0.09f, 28, 0.55f, 5500f * j);                    // crunch
                    s.Tone(0f, 90f * j, 60f, 0.07f, 0.45f, 0.025f);
                    s.Noise(0f, 0.08f, 0.25f, 0.003f, 0.03f, 1200f, 100f);
                    break;
                case FootSurface.Stone:
                    s.Click(0f, 1800f * j, 0.5f, 0.005f);                          // heel
                    s.Noise(0f, 0.05f, 0.4f, 0.001f, 0.012f, 7000f, 900f);
                    s.Tone(0f, 130f, 90f, 0.06f, 0.35f, 0.018f);
                    s.Click(0.045f, 1500f * j, 0.3f, 0.004f);                      // toe
                    s.Echo(0.06f, 0.15f, 3000f);
                    break;
                case FootSurface.Metal:
                    s.Click(0f, 1400f * j, 0.5f, 0.004f);
                    s.Ring(0f, new[] { 520f * j, 1130f * j, 1870f * j, 2650f * j }, 0.28f, 0.07f);
                    s.Tone(0f, 150f, 100f, 0.06f, 0.35f, 0.02f);
                    s.Click(0.05f, 1200f * j, 0.25f, 0.004f);
                    break;
                case FootSurface.Wood:
                    s.Tone(0f, 200f * j, 180f * j, 0.15f, 0.6f, 0.04f);             // hollow knock
                    s.Tone(0f, 360f * j, 330f * j, 0.1f, 0.25f, 0.025f);
                    s.Click(0f, 2200f, 0.25f, 0.003f);
                    s.Noise(0.04f, 0.05f, 0.12f, 0.005f, 0.015f, 3000f, 500f);       // creak
                    break;
            }
            s.Normalize(0.75f);
            return s.ToClip($"Step_{surface}_{variant}");
        }

        /// <summary>
        /// Surface under a point: terrain splat (grass / dirt / rock / plaza stone) or the material of the
        /// object stood on (metal panels and containers, wood, otherwise stone / concrete).
        /// </summary>
        public static FootSurface SurfaceAt(Vector3 feet)
        {
            const int mask = ~((1 << 2) | World.EnvironmentLayers.FoliageMask);
            if (!Physics.Raycast(feet + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, mask, QueryTriggerInteraction.Ignore))
                return FootSurface.Grass;
            if (hit.collider is TerrainCollider terrainCollider)
            {
                var terrain = terrainCollider.GetComponent<Terrain>();
                var data = terrain != null ? terrain.terrainData : null;
                if (data == null || data.alphamapLayers == 0)
                    return FootSurface.Grass;
                var local = hit.point - terrain.transform.position;
                int x = Mathf.Clamp((int)(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
                int z = Mathf.Clamp((int)(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
                var weights = data.GetAlphamaps(x, z, 1, 1);
                int best = 0;
                for (int l = 1; l < data.alphamapLayers; l++)
                    if (weights[0, 0, l] > weights[0, 0, best])
                        best = l;
                // Island layers: 0 grass, 1 dirt / sand, 2 rock, 3 plaza stone.
                return best == 0 ? FootSurface.Grass : best == 1 ? FootSurface.Dirt : FootSurface.Stone;
            }
            var renderer = hit.collider.GetComponent<Renderer>();
            string name = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            if (name.Contains("Khaki"))
                return FootSurface.Dirt; // sandbags
            if (name.Contains("Metal") || name.Contains("Container") || name.Contains("Prop"))
                return FootSurface.Metal;
            if (name.Contains("Wood"))
                return FootSurface.Wood;
            return FootSurface.Stone;
        }
    }
}
