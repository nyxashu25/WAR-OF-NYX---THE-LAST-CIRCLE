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
