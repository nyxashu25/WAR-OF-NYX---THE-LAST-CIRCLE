using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Core.Zone;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// Nyx Circle HUD for the local player (spec 10-12, 19): minimap with current and next circle,
    /// phase status and countdown, the crimson "RETURN TO SAFE ZONE" warning with distance while
    /// outside, big announcements, and a violet screen-edge distortion while outside. Purely
    /// presentation of replicated/predicted state. "Reduce flashing" steadies the edge effect;
    /// the gameplay penalty is unaffected.
    /// </summary>
    public sealed class ZoneHud : MonoBehaviour
    {
        private Minimap _minimap;
        private Text _status;
        private Text _warning;
        private Text _headline;
        private Image _edge;
        private float _headlineUntil;
        private float _edgeAlpha;
        private bool _outside;

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public Minimap Minimap => _minimap;

        public static ZoneHud Create(Transform parent)
        {
            var go = new GameObject("ZoneHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<ZoneHud>();
            hud.Build();
            return hud;
        }

        public void Apply(in ZoneView view, Vector3 playerPosition, float yaw)
        {
            _outside = view.Active && view.PlayerAlive && view.PlayerOutside;
            var stage = view.Stage;
            _minimap.SetView(playerPosition, yaw, view.Active, view.Current, stage.To, view.Active && !stage.Finished, _outside);

            if (!view.Active)
                _status.text = string.Empty;
            else if (stage.Finished)
                _status.text = "NYX CIRCLE CLOSED";
            else
            {
                string name = stage.IsFinal ? "FINAL CIRCLE" : $"NYX CIRCLE {stage.Index + 1}/{stage.Count}";
                string verb = view.Time < stage.StartsAt ? "SHRINKS IN" : "SHRINKING";
                _status.text = $"{name}\n{verb}  {Clock(stage.SecondsRemaining(view.Time))}  ·  {stage.DamagePerSecond:0} HP/s outside";
            }

            if (_outside)
            {
                _warning.text = $"RETURN TO SAFE ZONE  ·  {Mathf.CeilToInt(Mathf.Max(0f, view.PlayerDistanceToEdge))} m\n" +
                                ZoneAnnouncer.FirearmsDisabledText;
            }
            else
            {
                _warning.text = string.Empty;
            }
        }

        /// <summary>Big centre-screen notice (e.g. "NYX CIRCLE SHRINKING", "SAFE ZONE ENTERED").</summary>
        public void ShowHeadline(string text, Color color, float seconds = 2.5f)
        {
            _headline.text = text;
            _headline.color = color;
            _headlineUntil = Time.unscaledTime + seconds;
        }

        public void Hide()
        {
            _outside = false;
            _status.text = string.Empty;
            _warning.text = string.Empty;
        }

        private void Update()
        {
            if (_headline.text.Length > 0 && Time.unscaledTime > _headlineUntil)
                _headline.text = string.Empty;

            // Screen-edge distortion: a violet vignette that breathes (steady when flashing is reduced).
            float t = Time.unscaledTime;
            float target = 0f;
            if (_outside)
                target = ReduceFlashing ? 0.35f : 0.42f + 0.18f * Mathf.Sin(t * 5f) + 0.08f * Mathf.Sin(t * 23f);
            _edgeAlpha = Mathf.MoveTowards(_edgeAlpha, target, Time.unscaledDeltaTime * 2.5f);
            _edge.color = new Color(0.54f, 0.17f, 0.89f, _edgeAlpha);
            _edge.enabled = _edgeAlpha > 0.001f;

            if (_outside)
                _warning.color = ReduceFlashing || Mathf.Sin(t * 6f) > -0.3f ? NyxPalette.Crimson : NyxPalette.WithAlpha(NyxPalette.Crimson, 0.6f);
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("ZoneHudCanvas", sortingOrder: 4); // under the combat HUD and controls
            canvas.transform.SetParent(transform, false);

            _edge = UiFactory.CreatePanel("EdgeDistortion", canvas.transform, new Color(0.54f, 0.17f, 0.89f, 0f));
            _edge.sprite = Sprite.Create(EdgeTexture(), new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f));
            UiFactory.Stretch(_edge.rectTransform);
            _edge.enabled = false;

            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _minimap = Minimap.Create(safe);

            _status = UiFactory.CreateText(safe, string.Empty, 20, NyxPalette.TextPrimary, TextAnchor.UpperRight, FontStyle.Bold);
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(1f, 1f);
            statusRect.sizeDelta = new Vector2(420f, 56f);
            statusRect.anchoredPosition = new Vector2(-20f, -125f - Minimap.PanelSize - 6f);

            _warning = UiFactory.CreateText(safe, string.Empty, 30, NyxPalette.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_warning.rectTransform, new Vector2(0.2f, 0.755f), new Vector2(0.8f, 0.83f));
            var warningOutline = _warning.gameObject.AddComponent<Outline>();
            warningOutline.effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.8f);

            _headline = UiFactory.CreateText(safe, string.Empty, 54, NyxPalette.NyxPurple, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_headline.rectTransform, new Vector2(0.15f, 0.68f), new Vector2(0.85f, 0.75f));
            var headlineOutline = _headline.gameObject.AddComponent<Outline>();
            headlineOutline.effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.9f);
            headlineOutline.effectDistance = new Vector2(3f, -3f);
        }

        /// <summary>Transparent centre, opaque edges (alpha only; tinted by the image colour).</summary>
        private static Texture2D EdgeTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ZoneEdge (runtime)", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f;
                    float dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                    float edge = Mathf.Clamp01((Mathf.Max(dx, dy) - 0.55f) / 0.45f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * edge * edge));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
