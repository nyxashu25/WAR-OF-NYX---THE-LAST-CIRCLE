using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WarOfNyx.UI.Common
{
    /// <summary>
    /// Builds uGUI hierarchies from code. Phase 1 uses code-built placeholder UI so the
    /// menu works without hand-authored prefabs; Phase 10 replaces it with designed prefabs.
    /// Uses the legacy uGUI Text with Unity's built-in runtime font so no TextMesh Pro
    /// resources need importing yet.
    /// </summary>
    public static class UiFactory
    {
        public const int UiLayer = 5;
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static Font _font;
        private static Sprite _circle;

        /// <summary>Anti-aliased white circle generated at runtime (no texture asset needed). Tint via Image.color.</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null)
                    return _circle;

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "NyxCircle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[size * size];
                float radius = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - size * 0.5f;
                        float dy = y + 0.5f - size * 0.5f;
                        float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                _circle.name = "NyxCircle";
                return _circle;
            }
        }

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UiLayer;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // landscape: scale by height so phones of any width stay readable

            return canvas;
        }

        /// <summary>Creates an EventSystem driven by the Input System, unless one already exists.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Places a rect between normalized anchors with zero offsets.</summary>
        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image CreatePanel(string name, Transform parent, Color color, bool raycastTarget = false)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, float preferredHeight = -1f)
        {
            var rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (preferredHeight > 0f)
                AddLayoutElement(rect.gameObject, preferredHeight);
            return text;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing, RectOffset padding = null)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        /// <summary>A horizontal row that sits inside a vertical layout with a fixed height.</summary>
        public static RectTransform CreateRow(string name, Transform parent, float height, float spacing)
        {
            var row = CreateRect(name, parent);
            AddHorizontalLayout(row.gameObject, spacing);
            AddLayoutElement(row.gameObject, height);
            return row;
        }

        public static LayoutElement AddLayoutElement(GameObject go, float preferredHeight, float flexibleWidth = -1f)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            if (flexibleWidth >= 0f)
                element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static MenuButton CreateButton(Transform parent, string label, Action onClick, float height = 96f, int fontSize = 34)
        {
            var rect = CreateRect("Button_" + label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = NyxPalette.MidnightNavy;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            // Thin accent bar on the left edge, recoloured to show selection.
            var accent = CreatePanel("Accent", rect, NyxPalette.ElectricCyan);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(8f, 0f);
            accent.rectTransform.anchoredPosition = Vector2.zero;

            var text = CreateText(rect, label, fontSize, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 16f, 4f, 16f, 4f);

            AddLayoutElement(rect.gameObject, height);
            return new MenuButton(button, image, accent, text);
        }
    }

    /// <summary>Handle to a code-built menu button with selection / disabled styling.</summary>
    public sealed class MenuButton
    {
        public Button Button { get; }
        public Image Background { get; }
        public Image Accent { get; }
        public Text Label { get; }

        public MenuButton(Button button, Image background, Image accent, Text label)
        {
            Button = button;
            Background = background;
            Accent = accent;
            Label = label;
        }

        public bool Interactable
        {
            get => Button.interactable;
            set
            {
                Button.interactable = value;
                Label.color = value ? NyxPalette.TextPrimary : NyxPalette.TextMuted;
                Accent.color = value ? NyxPalette.ElectricCyan : NyxPalette.Disabled;
            }
        }

        public void SetSelected(bool selected)
        {
            Background.color = selected ? NyxPalette.NyxPurple : NyxPalette.MidnightNavy;
            if (Button.interactable)
                Accent.color = selected ? NyxPalette.VictoryGold : NyxPalette.ElectricCyan;
        }

        public void SetLabel(string label) => Label.text = label;
    }
}
