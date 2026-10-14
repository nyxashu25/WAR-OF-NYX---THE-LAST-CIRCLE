using UnityEngine;
using WarOfNyx.App;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER sound effects synthesized at runtime (no audio assets yet). Phase 10
    /// replaces them with designed sounds; the call sites stay the same.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;
        private static AudioClip _spawnChime;

        /// <summary>Spawn portal sound: rising shimmer sweep plus a soft whoosh (~0.9 s).</summary>
        public static AudioClip SpawnChime
        {
            get
            {
                if (_spawnChime != null)
                    return _spawnChime;

                const float length = 0.9f;
                int samples = (int)(SampleRate * length);
                var data = new float[samples];
                var random = new System.Random(1234);
                float phaseA = 0f, phaseB = 0f, noise = 0f;

                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float progress = t / length;
                    float frequency = Mathf.Lerp(220f, 880f, progress * progress);
                    phaseA += 2f * Mathf.PI * frequency / SampleRate;
                    phaseB += 2f * Mathf.PI * frequency * 1.5f / SampleRate;

                    float attack = Mathf.Clamp01(t / 0.04f);
                    float release = 1f - progress;
                    float envelope = attack * release * release;

                    // One-pole low-passed noise for the "whoosh".
                    noise += ((float)random.NextDouble() * 2f - 1f - noise) * 0.08f;

                    data[i] = envelope * (0.45f * Mathf.Sin(phaseA) + 0.2f * Mathf.Sin(phaseB) + 0.35f * noise);
                }

                _spawnChime = AudioClip.Create("SpawnChime (procedural)", samples, 1, SampleRate, false);
                _spawnChime.SetData(data, 0);
                return _spawnChime;
            }
        }

        private static AudioClip _gunshot, _heavyShot, _swing, _hitTick, _reload;

        /// <summary>Short noise crack with a low thump (rifles, SMGs, pistol).</summary>
        public static AudioClip Gunshot => _gunshot != null ? _gunshot : (_gunshot = Shot("Gunshot", 0.18f, 140f, 0.65f, 11));

        /// <summary>Longer, bassier blast (shotgun, marksman).</summary>
        public static AudioClip HeavyShot => _heavyShot != null ? _heavyShot : (_heavyShot = Shot("HeavyShot", 0.35f, 80f, 0.85f, 23));

        /// <summary>Melee swing whoosh.</summary>
        public static AudioClip MeleeSwing
        {
            get
            {
                if (_swing != null)
                    return _swing;
                const float length = 0.28f;
                int n = (int)(SampleRate * length);
                var data = new float[n];
                var random = new System.Random(5);
                float lp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float p = i / (float)n;
                    float env = Mathf.Sin(p * Mathf.PI); // swell and fade
                    float cutoff = Mathf.Lerp(0.02f, 0.25f, p);
                    lp += ((float)random.NextDouble() * 2f - 1f - lp) * cutoff;
                    data[i] = lp * env * 0.9f;
                }
                return _swing = Create("MeleeSwing", data);
            }
        }

        /// <summary>Hit-marker tick for the shooter.</summary>
        public static AudioClip HitTick
        {
            get
            {
                if (_hitTick != null)
                    return _hitTick;
                int n = (int)(SampleRate * 0.06f);
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    data[i] = Mathf.Sin(2f * Mathf.PI * 1800f * t) * (1f - i / (float)n) * 0.5f;
                }
                return _hitTick = Create("HitTick", data);
            }
        }

        /// <summary>Two mechanical clicks.</summary>
        public static AudioClip Reload
        {
            get
            {
                if (_reload != null)
                    return _reload;
                int n = (int)(SampleRate * 0.4f);
                var data = new float[n];
                var random = new System.Random(9);
                foreach (int start in new[] { 0, (int)(SampleRate * 0.25f) })
                {
                    for (int i = 0; i < 900 && start + i < n; i++)
                        data[start + i] += ((float)random.NextDouble() * 2f - 1f) * (1f - i / 900f) * 0.6f;
                }
                return _reload = Create("Reload", data);
            }
        }

        private static AudioClip _hurt, _heal, _victory, _elimination;

        /// <summary>Short low "oof" thud when taking damage.</summary>
        public static AudioClip Hurt => _hurt != null ? _hurt : (_hurt = Shot("Hurt", 0.16f, 95f, 0.5f, 31));

        /// <summary>Rising shimmer when a medkit completes.</summary>
        public static AudioClip Heal => _heal != null ? _heal : (_heal = Sweep("Heal", 0.45f, 520f, 1040f, 0.35f));

        /// <summary>Three-note rising victory sting (spec 21 victory presentation).</summary>
        public static AudioClip Victory
        {
            get
            {
                if (_victory != null)
                    return _victory;
                const float note = 0.22f;
                float[] freqs = { 523.25f, 659.25f, 1046.5f };
                int per = (int)(SampleRate * note);
                var data = new float[per * freqs.Length + SampleRate / 2];
                for (int k = 0; k < freqs.Length; k++)
                {
                    for (int i = 0; i < per + (k == freqs.Length - 1 ? SampleRate / 2 : 0); i++)
                    {
                        float t = i / (float)SampleRate;
                        float env = Mathf.Exp(-t * (k == freqs.Length - 1 ? 3f : 9f));
                        int idx = k * per + i;
                        if (idx < data.Length)
                            data[idx] += (Mathf.Sin(2f * Mathf.PI * freqs[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[k] * t)) * env * 0.35f;
                    }
                }
                return _victory = Create("Victory", data);
            }
        }

        /// <summary>Falling energy burst when someone is eliminated.</summary>
        public static AudioClip Elimination => _elimination != null ? _elimination : (_elimination = Sweep("Elimination", 0.5f, 900f, 180f, 0.45f));

        private static AudioClip Sweep(string name, float length, float fromHz, float toHz, float volume)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float p = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, p) / SampleRate;
                data[i] = Mathf.Sin(phase) * Mathf.Sin(p * Mathf.PI) * volume;
            }
            return Create(name, data);
        }

        private static AudioClip _rainLoop, _windLoop, _thunder;

        /// <summary>Seamless rain hiss with scattered drop ticks (4 s loop).</summary>
        public static AudioClip RainLoop
        {
            get
            {
                if (_rainLoop != null)
                    return _rainLoop;
                const float length = 4f;
                int n = (int)(SampleRate * length);
                var data = new float[n];
                var random = new System.Random(41);
                float lp = 0f, hp = 0f, previous = 0f, drop = 0f;
                for (int i = 0; i < n; i++)
                {
                    float white = (float)random.NextDouble() * 2f - 1f;
                    lp += (white - lp) * 0.45f;            // soften the top end
                    hp = 0.97f * (hp + lp - previous);      // and remove rumble
                    previous = lp;
                    if (random.NextDouble() < 0.0009)
                        drop = 0.6f + (float)random.NextDouble() * 0.4f;
                    drop *= 0.992f;
                    data[i] = hp * 0.35f + drop * white * 0.25f;
                }
                return _rainLoop = Create("RainLoop", Crossfade(data, (int)(SampleRate * 0.25f)));
            }
        }

        /// <summary>Low wind howl with slow gusts (about 6 s loop).</summary>
        public static AudioClip WindLoop
        {
            get
            {
                if (_windLoop != null)
                    return _windLoop;
                const float length = 6f;
                int n = (int)(SampleRate * length);
                var data = new float[n];
                var random = new System.Random(57);
                float lp1 = 0f, lp2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float p = i / (float)n;
                    float gust = 0.55f + 0.3f * Mathf.Sin(p * Mathf.PI * 2f * 2f) + 0.15f * Mathf.Sin(p * Mathf.PI * 2f * 5f + 1f);
                    float cutoff = 0.02f + 0.03f * gust;
                    lp1 += ((float)random.NextDouble() * 2f - 1f - lp1) * cutoff;
                    lp2 += (lp1 - lp2) * cutoff;
                    data[i] = lp2 * gust * 3.2f;
                }
                return _windLoop = Create("WindLoop", Crossfade(data, (int)(SampleRate * 0.3f)));
            }
        }

        /// <summary>Distant thunder: a crack followed by a long rolling rumble (3.5 s).</summary>
        public static AudioClip Thunder
        {
            get
            {
                if (_thunder != null)
                    return _thunder;
                const float length = 3.5f;
                int n = (int)(SampleRate * length);
                var data = new float[n];
                var random = new System.Random(73);
                float lp1 = 0f, lp2 = 0f, crackLp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float white = (float)random.NextDouble() * 2f - 1f;
                    crackLp += (white - crackLp) * 0.5f;
                    lp1 += (white - lp1) * 0.012f;
                    lp2 += (lp1 - lp2) * 0.012f;
                    float crack = crackLp * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t / 0.01f);
                    float roll = 0.6f + 0.4f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 2.1f + 0.5f);
                    float rumble = lp2 * 14f * roll * Mathf.Clamp01(t / 0.15f) * Mathf.Exp(-t * 0.9f);
                    data[i] = Mathf.Clamp(crack * 0.5f + rumble, -1f, 1f);
                }
                return _thunder = Create("Thunder", data);
            }
        }

        /// <summary>
        /// Makes a seamless loop: the last <paramref name="overlap"/> samples are blended into the start and
        /// then cut off, so the final sample leads straight into the first.
        /// </summary>
        private static float[] Crossfade(float[] data, int overlap)
        {
            int length = data.Length - overlap;
            var loop = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (i < overlap)
                {
                    float w = i / (float)overlap;
                    loop[i] = data[i] * w + data[length + i] * (1f - w);
                }
                else
                {
                    loop[i] = data[i];
                }
            }
            return loop;
        }

        private static AudioClip Shot(string name, float length, float thumpHz, float volume, int seed)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            var random = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 18f / length);
                lp += ((float)random.NextDouble() * 2f - 1f - lp) * 0.35f;
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 30f);
                data[i] = (lp * 0.8f + thump * 0.6f) * env * volume;
            }
            return Create(name, data);
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name + " (procedural)", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Effects volume from the player's settings (master volume is applied by AudioListener).</summary>
        public static float SfxVolume => AppServices.IsInitialized ? AppServices.UserSettings.Data.sfxVolume : 1f;

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null || Application.isBatchMode)
                return;

            var go = new GameObject("Sfx_" + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 70f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = Mathf.Clamp01(volume * SfxVolume);
            source.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }
    }
}
