using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.MainMenu
{
    /// <summary>
    /// Placeholder animated menu background: slowly rotating "Nyx circle" rings built from
    /// UI images. Cheap (a handful of UI quads, no particles or post-processing).
    /// Pulsing is disabled when the player turns on "Reduce flashing" (spec 12 accessibility).
    /// Replaced by the designed 3D menu scene in Phase 10.
    /// </summary>
    public sealed class MenuBackgroundAnimator : MonoBehaviour
    {
        private sealed class Shape
        {
            public RectTransform Rect;
            public Image Image;
            public float DegreesPerSecond;
            public float BaseAlpha;
            public float PulsePhase;
        }

        private readonly List<Shape> _shapes = new List<Shape>();

        public bool ReduceFlashing { get; set; }

        public void Build(RectTransform parent)
        {
            AddRing(parent, 1500f, 6f, NyxPalette.NyxPurple, 0.10f, 4f, 0.0f);
            AddRing(parent, 1150f, 5f, NyxPalette.ElectricCyan, 0.08f, -6f, 1.3f);
            AddRing(parent, 820f, 4f, NyxPalette.Crimson, 0.07f, 9f, 2.1f);
            AddRing(parent, 520f, 3f, NyxPalette.NyxPurple, 0.12f, -12f, 0.7f);
        }

        /// <summary>A "ring" is a rotated square outline made of four thin bars.</summary>
        private void AddRing(RectTransform parent, float size, float thickness, Color color, float alpha, float speed, float phase)
        {
            var pivot = UiFactory.CreateRect("Ring", parent);
            pivot.anchorMin = pivot.anchorMax = new Vector2(0.72f, 0.5f);
            pivot.sizeDelta = new Vector2(size, size);
            pivot.localRotation = Quaternion.Euler(0f, 0f, 45f);

            for (int side = 0; side < 4; side++)
            {
                var bar = UiFactory.CreatePanel("Edge", pivot, NyxPalette.WithAlpha(color, alpha));
                var rt = bar.rectTransform;
                bool horizontal = side < 2;
                rt.anchorMin = horizontal ? new Vector2(0f, side == 0 ? 0f : 1f) : new Vector2(side == 2 ? 0f : 1f, 0f);
                rt.anchorMax = horizontal ? new Vector2(1f, side == 0 ? 0f : 1f) : new Vector2(side == 2 ? 0f : 1f, 1f);
                rt.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
                rt.anchoredPosition = Vector2.zero;

                _shapes.Add(new Shape
                {
                    Rect = side == 0 ? pivot : null,
                    Image = bar,
                    DegreesPerSecond = speed,
                    BaseAlpha = alpha,
                    PulsePhase = phase,
                });
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            foreach (var shape in _shapes)
            {
                if (shape.Rect != null)
                    shape.Rect.Rotate(0f, 0f, shape.DegreesPerSecond * dt);

                float alpha = ReduceFlashing
                    ? shape.BaseAlpha
                    : shape.BaseAlpha * (0.7f + 0.3f * Mathf.Sin(t * 0.8f + shape.PulsePhase));
                var c = shape.Image.color;
                c.a = alpha;
                shape.Image.color = c;
            }
        }
    }
}
