using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Core.Combat;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// Screen blood for the local player (presentation only):
    ///  * a critical hit (<see cref="CriticalHit.Is"/>: headshot, a big hit, or left badly hurt) splatters 1–3
    ///    procedurally painted blood marks near the screen edges (never over the crosshair); they slowly run down;
    ///  * the marks stay while you are hurt and fade away once you heal clearly above the health you were at when
    ///    they landed (<see cref="CriticalHit.HealedPast"/>);
    ///  * a red vignette grows as health drops and pulses like a heartbeat when very low ("Reduce flashing": no pulse).
    /// Off with Settings → BLOOD EFFECTS.
    /// </summary>
    public sealed class BloodOverlay : MonoBehaviour
    {
        private const int MaxSplatters = 9;
        private const int Variants = 5;

        private sealed class Splatter
        {
            public RawImage Image;
            public float HealthAtHit;
            public float BornAt;
            public float Fade = 1f;
            public bool Clearing;
            public Vector2 Start;
            public float Size;
        }

        private static Texture2D[] _textures;
        private static Texture2D _vignetteTexture;
        private readonly List<Splatter> _splatters = new List<Splatter>();
        private RectTransform _root;
        private RawImage _vignette;
        private float _vignetteLevel;
        private float _healthFraction = 1f;

        private static bool Enabled => !AppServices.IsInitialized || AppServices.UserSettings.Data.bloodEffects;
        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public static BloodOverlay Create(Transform parent)
        {
            // Below the HUD (sorting 5) so health, ammo and the crosshair stay readable on top of the blood.
            var canvas = UiFactory.CreateCanvas("BloodOverlayCanvas", sortingOrder: 4);
            canvas.transform.SetParent(parent, false);
            var overlay = canvas.gameObject.AddComponent<BloodOverlay>();
            overlay._root = (RectTransform)canvas.transform;
            overlay.BuildVignette();
            return overlay;
        }

        private void BuildVignette()
        {
            _vignetteTexture ??= VignetteTexture();
            _vignette = NewImage("LowHealthVignette", _vignetteTexture);
            var rect = _vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _vignette.color = new Color(0.55f, 0f, 0.02f, 0f);
        }

        private RawImage NewImage(string name, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_root, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A hit on the local player. Splatters only when it is critical.</summary>
        public void OnDamaged(float damageApplied, bool headshot, float healthAfter, float maxHealth)
        {
            if (!Enabled || !CriticalHit.Is(damageApplied, headshot, healthAfter, maxHealth))
                return;
            _textures ??= BuildTextures();
            int count = CriticalHit.Splatters(damageApplied, headshot);
            float fraction = maxHealth > 0f ? healthAfter / maxHealth : 0f;
            for (int i = 0; i < count; i++)
                Add(fraction);
        }

        private void Add(float healthFraction)
        {
            if (_splatters.Count >= MaxSplatters)
            {
                Destroy(_splatters[0].Image.gameObject);
                _splatters.RemoveAt(0);
            }
            var image = NewImage("BloodSplatter", _textures[Random.Range(0, _textures.Length)]);
            image.transform.SetAsFirstSibling(); // under the vignette
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            var size = _root.rect.size;
            // Near an edge or a corner, never over the centre of the screen.
            Vector2 p;
            int edge = Random.Range(0, 4);
            float along = Random.Range(0.08f, 0.92f), inset = Random.Range(0.02f, 0.16f);
            switch (edge)
            {
                case 0: p = new Vector2(inset, along); break;
                case 1: p = new Vector2(1f - inset, along); break;
                case 2: p = new Vector2(along, 1f - inset); break;
                default: p = new Vector2(along, inset + 0.05f); break;
            }
            float px = Mathf.Min(size.x, size.y) * Random.Range(0.28f, 0.46f);
            rect.sizeDelta = new Vector2(px, px);
            rect.anchoredPosition = Vector2.Scale(p, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
            image.color = new Color(1f, 1f, 1f, 0.92f);
            _splatters.Add(new Splatter { Image = image, HealthAtHit = healthFraction, BornAt = Time.time, Start = rect.anchoredPosition, Size = px });
        }

        /// <summary>Called every frame with the local player's health (heals clear the blood).</summary>
        public void SetHealth(float health, float maxHealth, bool alive)
        {
            _healthFraction = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 1f;
            if (!alive)
                _healthFraction = 0f;
        }

        /// <summary>Remove everything at once (respawn, leaving the match, blood turned off).</summary>
        public void ClearAll()
        {
            foreach (var s in _splatters)
                if (s.Image != null)
                    Destroy(s.Image.gameObject);
            _splatters.Clear();
        }

        private void Update()
        {
            if (!Enabled)
            {
                if (_splatters.Count > 0)
                    ClearAll();
                _vignette.color = new Color(_vignette.color.r, _vignette.color.g, _vignette.color.b, 0f);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            for (int i = _splatters.Count - 1; i >= 0; i--)
            {
                var s = _splatters[i];
                if (s.Image == null)
                {
                    _splatters.RemoveAt(i);
                    continue;
                }
                float age = Time.time - s.BornAt;
                if (!s.Clearing && CriticalHit.HealedPast(s.HealthAtHit, _healthFraction))
                    s.Clearing = true;
                if (s.Clearing)
                {
                    s.Fade -= dt / 1.2f;
                    if (s.Fade <= 0f)
                    {
                        Destroy(s.Image.gameObject);
                        _splatters.RemoveAt(i);
                        continue;
                    }
                }
                // Impact pop, then the blood slowly runs down and dulls a little as it dries.
                float pop = Mathf.Clamp01(age / 0.08f);
                float run = Mathf.Min(age, 12f);
                var rect = s.Image.rectTransform;
                rect.sizeDelta = new Vector2(s.Size, s.Size * (1f + run * 0.012f)) * Mathf.Lerp(0.75f, 1f, pop);
                rect.anchoredPosition = s.Start + Vector2.down * (run * s.Size * 0.008f);
                float dry = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(age / 20f));
                s.Image.color = new Color(dry, dry * 0.95f, dry * 0.95f, 0.92f * s.Fade);
            }

            // Low-health vignette with a heartbeat pulse below 25 %.
            float target = CriticalHit.Vignette(_healthFraction);
            _vignetteLevel = Mathf.MoveTowards(_vignetteLevel, target, dt * 1.5f);
            float pulse = 1f;
            if (_healthFraction < 0.25f && _healthFraction > 0f && !ReduceFlashing)
            {
                float beat = Mathf.Repeat(Time.time * 1.6f, 1f);
                pulse = 0.75f + 0.25f * (Mathf.Exp(-beat * 9f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, beat - 0.18f) * 9f) * (beat > 0.18f ? 1f : 0f));
            }
            _vignette.color = new Color(0.55f, 0f, 0.02f, 0.75f * _vignetteLevel * pulse);
        }

        // ------------------------------------------------------------------ procedural textures

        /// <summary>Radial edge darkening: transparent centre, opaque edges.</summary>
        private static Texture2D VignetteTexture()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BloodVignette" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>
        /// Blood splatter textures: an irregular main blot (overlapping noisy discs), satellite droplets, a few drips
        /// running down, a darker core, a lighter wet rim and a small highlight.
        /// </summary>
        private static Texture2D[] BuildTextures()
        {
            var textures = new Texture2D[Variants];
            for (int v = 0; v < Variants; v++)
                textures[v] = Splat(1234 + v * 77);
            return textures;
        }

        private static Texture2D Splat(int seed)
        {
            const int n = 256;
            var random = new System.Random(seed);
            float R() => (float)random.NextDouble();
            var density = new float[n * n];
            void Blob(float cx, float cy, float radius, float lumps)
            {
                float phase = R() * 10f;
                int x0 = Mathf.Max(0, (int)((cx - radius * 1.4f) * n)), x1 = Mathf.Min(n - 1, (int)((cx + radius * 1.4f) * n));
                int y0 = Mathf.Max(0, (int)((cy - radius * 1.4f) * n)), y1 = Mathf.Min(n - 1, (int)((cy + radius * 1.4f) * n));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x / (float)n - cx, dy = y / (float)n - cy;
                    float angle = Mathf.Atan2(dy, dx);
                    float r = radius * (1f + lumps * (0.5f * Mathf.Sin(angle * 5f + phase) + 0.3f * Mathf.Sin(angle * 11f + phase * 2f)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(r, 1e-4f);
                    density[y * n + x] = Mathf.Max(density[y * n + x], Mathf.Clamp01((1f - d) * 6f));
                }
            }
            float mx = 0.5f + (R() - 0.5f) * 0.15f, my = 0.55f + (R() - 0.5f) * 0.15f;
            Blob(mx, my, 0.2f + R() * 0.06f, 0.25f);
            for (int i = 0; i < 4; i++)
                Blob(mx + (R() - 0.5f) * 0.25f, my + (R() - 0.5f) * 0.25f, 0.08f + R() * 0.06f, 0.3f);
            // Satellite droplets thrown outward.
            for (int i = 0; i < 18; i++)
            {
                float a = R() * Mathf.PI * 2f, dist = 0.25f + R() * 0.2f;
                Blob(mx + Mathf.Cos(a) * dist, my + Mathf.Sin(a) * dist, 0.01f + R() * 0.025f, 0.1f);
            }
            // Drips running down from the blot.
            for (int i = 0; i < 3; i++)
            {
                float dx = mx + (R() - 0.5f) * 0.3f;
                float length = 0.15f + R() * 0.25f;
                float width = 0.012f + R() * 0.012f;
                for (float t = 0f; t < length; t += 0.006f)
                    Blob(dx + Mathf.Sin(t * 20f) * 0.004f, my - 0.1f - t, width * (1f - t / length * 0.5f), 0f);
                Blob(dx, my - 0.1f - length, width * 1.4f, 0.05f);
            }

            var px = new Color[n * n];
            var deep = new Color(0.32f, 0.01f, 0.02f);
            var fresh = new Color(0.62f, 0.03f, 0.05f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = density[y * n + x];
                if (d <= 0f)
                {
                    px[y * n + x] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }
                // Darker where it pooled, brighter wet rim at the edge, a soft highlight up-left of the blot.
                float rim = 1f - Mathf.Abs(d - 0.5f) * 2f;
                var c = Color.Lerp(fresh, deep, Mathf.SmoothStep(0f, 1f, d));
                c = Color.Lerp(c, new Color(0.75f, 0.08f, 0.1f), rim * 0.3f);
                float hx = x / (float)n - (mx - 0.07f), hy = y / (float)n - (my + 0.07f);
                float highlight = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.05f) * d;
                c = Color.Lerp(c, new Color(1f, 0.55f, 0.55f), highlight * 0.35f);
                c.a = Mathf.Clamp01(d * 1.1f) * 0.95f;
                px[y * n + x] = c;
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "BloodSplatter" };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
