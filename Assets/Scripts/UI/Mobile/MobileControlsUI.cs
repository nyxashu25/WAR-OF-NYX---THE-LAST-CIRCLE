using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Landscape touch layout (spec 18). Built in code for Phase 2; Phase 11 adds the
    /// drag-to-customize editor and per-button scaling.
    ///
    /// LEFT: floating move joystick, SPRINT (toggle), CROUCH, SLIDE.
    /// RIGHT: swipe-to-look zone behind FIRE, AIM, JUMP, RELOAD, MELEE, WEAPON, MEDKIT, USE.
    ///
    /// Buttons are Input System On-Screen Buttons bound to virtual gamepad controls, i.e. the same
    /// actions as keyboard/mouse. All buttons are live from Phase 6 (combat: Phase 5;
    /// USE opens loot capsules and MEDKIT uses the best-fitting medkit: Phase 6).
    /// Layout positions are in canvas units (1080 high reference) inside the device safe area.
    /// </summary>
    public sealed class MobileControlsUI : MonoBehaviour
    {
        private const float DimmedAlpha = 0.35f;

        private Canvas _canvas;
        private FloatingJoystick _joystick;
        private TouchLookArea _lookArea;
        private Image _sprintImage;
        private PlayerInputRouter _router;
        private Image _fireImage;
        private Text _fireLabel;
        private bool _fireBlocked;
        private RectTransform _safe;
        private GameObject _editBar;
        private Text _editStatus;
        private bool _editing;

        private sealed class ControlEntry
        {
            public string Id;
            public RectTransform Rect;
            public Image Image;
            public Vector2 DefaultPosition;
            public float DefaultSize;
            public Behaviour Input; // OnScreenButton or tap handler: off while editing
        }

        private readonly List<ControlEntry> _controls = new List<ControlEntry>();

        public bool Editing => _editing;

        private static WarOfNyx.Core.Controls.TouchLayout Layout =>
            AppServices.IsInitialized ? AppServices.UserSettings.Data.touchLayout : new WarOfNyx.Core.Controls.TouchLayout();

        public bool Visible => _canvas != null && _canvas.gameObject.activeSelf;

        public static MobileControlsUI Create(PlayerInputRouter router)
        {
            var go = new GameObject("MobileControls");
            var ui = go.AddComponent<MobileControlsUI>();
            ui.Build(router);
            return ui;
        }

        /// <summary>
        /// Outside the Nyx Circle (spec 11): the FIRE button is shown disabled. Pressing it still sends
        /// the input; the SERVER refuses the shot (the UI is never the enforcement).
        /// </summary>
        public void SetFireBlocked(bool blocked)
        {
            if (_fireImage == null || _fireBlocked == blocked)
                return;
            _fireBlocked = blocked;
            _fireImage.color = ButtonColor(!blocked);
            if (_fireLabel != null)
            {
                _fireLabel.text = blocked ? "NO FIRE\nMELEE ONLY" : "FIRE";
                _fireLabel.fontSize = blocked ? 22 : 30;
                _fireLabel.color = blocked ? NyxPalette.WithAlpha(NyxPalette.Crimson, 0.9f) : NyxPalette.TextPrimary;
            }
        }

        /// <summary>
        /// Layout editor (spec 18 "adjustable touch controls"): drag buttons to move them, change size and
        /// opacity, RESET or DONE. Buttons send no input while editing. Saved with the user settings.
        /// </summary>
        public void SetEditMode(bool editing)
        {
            _editing = editing;
            foreach (var c in _controls)
            {
                if (c.Input != null)
                    c.Input.enabled = !editing;
                var drag = c.Rect.GetComponent<DraggableControl>();
                if (drag != null)
                    drag.enabled = editing;
            }
            if (_editBar != null)
                _editBar.SetActive(editing);
            if (_lookArea != null)
                _lookArea.enabled = !editing;
            if (_joystick != null)
                _joystick.enabled = !editing;
            if (!editing && AppServices.IsInitialized)
                AppServices.UserSettings.Save();
            RefreshEditStatus();
        }

        public void ApplyLayout()
        {
            var layout = Layout;
            foreach (var c in _controls)
            {
                layout.Offset(c.Id, out float x, out float y);
                c.Rect.anchoredPosition = c.DefaultPosition + new Vector2(x, y);
                c.Rect.sizeDelta = Vector2.one * (c.DefaultSize * layout.scale);
                var color = c.Image.color;
                color.a = (c.Id == "SPRINT" && _router != null && _router.SprintToggled ? 0.85f : 0.75f) * layout.opacity;
                c.Image.color = color;
            }
        }

        private void OnControlMoved(ControlEntry c)
        {
            var offset = c.Rect.anchoredPosition - c.DefaultPosition;
            Layout.SetOffset(c.Id, offset.x, offset.y);
            ApplyLayout(); // shows the clamped position
        }

        private void ChangeScale(float delta)
        {
            var layout = Layout;
            layout.scale = Mathf.Clamp(layout.scale + delta, WarOfNyx.Core.Controls.TouchLayout.MinScale, WarOfNyx.Core.Controls.TouchLayout.MaxScale);
            ApplyLayout();
            RefreshEditStatus();
        }

        private void ChangeOpacity(float delta)
        {
            var layout = Layout;
            layout.opacity = Mathf.Clamp(layout.opacity + delta, WarOfNyx.Core.Controls.TouchLayout.MinOpacity, WarOfNyx.Core.Controls.TouchLayout.MaxOpacity);
            ApplyLayout();
            RefreshEditStatus();
        }

        private void ResetLayout()
        {
            Layout.Reset();
            ApplyLayout();
            RefreshEditStatus();
        }

        private void RefreshEditStatus()
        {
            if (_editStatus != null)
                _editStatus.text = $"DRAG BUTTONS TO MOVE  ·  SIZE {Mathf.RoundToInt(Layout.scale * 100f)}%  ·  OPACITY {Mathf.RoundToInt(Layout.opacity * 100f)}%";
        }

        private void BuildEditBar(RectTransform parent)
        {
            var bar = UiFactory.CreatePanel("LayoutEditor", parent, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f), raycastTarget: true);
            UiFactory.Anchor(bar.rectTransform, new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.99f));
            var column = bar.rectTransform;
            UiFactory.AddVerticalLayout(column.gameObject, 6f, new RectOffset(12, 12, 8, 8), TextAnchor.MiddleCenter);
            _editStatus = UiFactory.CreateText(column, string.Empty, 22, NyxPalette.ElectricCyan, TextAnchor.MiddleCenter, FontStyle.Bold, 30f);
            var row = UiFactory.CreateRow("Buttons", column, 64f, 10f);
            UiFactory.CreateButton(row, "SIZE -", () => ChangeScale(-0.1f), 60f, 22);
            UiFactory.CreateButton(row, "SIZE +", () => ChangeScale(0.1f), 60f, 22);
            UiFactory.CreateButton(row, "OPACITY -", () => ChangeOpacity(-0.1f), 60f, 22);
            UiFactory.CreateButton(row, "OPACITY +", () => ChangeOpacity(0.1f), 60f, 22);
            UiFactory.CreateButton(row, "RESET", ResetLayout, 60f, 22);
            UiFactory.CreateButton(row, "DONE", () =>
            {
                SetEditMode(false);
                EditFinished?.Invoke();
            }, 60f, 22);
            _editBar = bar.gameObject;
            _editBar.SetActive(false);
        }

        /// <summary>Raised when the player presses DONE in the layout editor.</summary>
        public event System.Action EditFinished;

        public void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (_joystick != null && AppServices.IsInitialized)
                _joystick.Sensitivity = AppServices.UserSettings.Data.joystickSensitivity;
            if (_sprintImage != null && _router != null)
            {
                var c = _router.SprintToggled ? NyxPalette.VictoryGold : ButtonColor(true);
                c.a = (_router.SprintToggled ? 0.85f : 0.75f) * Layout.opacity;
                _sprintImage.color = c;
            }
        }

        private void Build(PlayerInputRouter router)
        {
            _router = router;
            UiFactory.EnsureEventSystem();

            _canvas = UiFactory.CreateCanvas("MobileControlsCanvas", sortingOrder: 10);
            _canvas.transform.SetParent(transform, false);

            var safe = UiFactory.CreateRect("SafeArea", _canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            _safe = safe;

            // Look zone first so it renders (and raycasts) behind every button.
            var look = UiFactory.CreatePanel("TouchLookArea", safe, new Color(0f, 0f, 0f, 0f), raycastTarget: true);
            UiFactory.Anchor(look.rectTransform, new Vector2(0.38f, 0f), new Vector2(1f, 1f));
            _lookArea = look.gameObject.AddComponent<TouchLookArea>();
            _lookArea.Bind(router);

            BuildJoystick(safe);

            // Left cluster above the joystick zone.
            AddButton(safe, "CROUCH", "<Gamepad>/buttonEast", BottomLeft, new Vector2(130f, 740f), 120f, active: true);
            AddButton(safe, "SLIDE", "<Gamepad>/leftShoulder", BottomLeft, new Vector2(280f, 740f), 120f, active: true);
            _sprintImage = AddToggleButton(safe, "SPRINT", BottomLeft, new Vector2(430f, 740f), 120f, router.ToggleSprint);

            // Right cluster (offsets from the bottom-right corner).
            _fireImage = AddButton(safe, "FIRE", "<Gamepad>/rightTrigger", BottomRight, new Vector2(-270f, 300f), 180f, active: true);
            _fireLabel = _fireImage.GetComponentInChildren<Text>();
            // Drag-to-aim while holding FIRE (fixes KNOWN_ISSUES 42): one finger shoots and steers.
            _fireImage.gameObject.AddComponent<TouchDragLook>().Bind(router);
            AddButton(safe, "JUMP", "<Gamepad>/buttonSouth", BottomRight, new Vector2(-110f, 140f), 140f, active: true);
            AddButton(safe, "RELOAD", "<Gamepad>/buttonWest", BottomRight, new Vector2(-110f, 330f), 110f, active: true);
            AddButton(safe, "AIM", "<Gamepad>/leftTrigger", BottomRight, new Vector2(-450f, 300f), 120f, active: true);
            AddButton(safe, "MELEE", "<Gamepad>/rightShoulder", BottomRight, new Vector2(-300f, 110f), 110f, active: true);
            AddButton(safe, "WEAPON", "<Gamepad>/rightStickPress", BottomRight, new Vector2(-450f, 120f), 110f, active: true);
            AddButton(safe, "USE", "<Gamepad>/buttonNorth", BottomRight, new Vector2(-270f, 480f), 110f, active: true);
            AddButton(safe, "MEDKIT", "<Gamepad>/dpad/down", BottomRight, new Vector2(-110f, 500f), 100f, active: true);
            // EYE (free look, PUBG-style): hold to look around without turning; drag it (or swipe) to steer the view.
            var eye = CreateRoundButton(safe, "EYE", BottomRight, new Vector2(-430f, 500f), 100f, true);
            eye.gameObject.AddComponent<TouchHoldButton>().Changed += router.SetTouchFreeLook;
            eye.gameObject.AddComponent<TouchDragLook>().Bind(router);

            // Register every button for the layout (position, size, opacity) and the editor.
            foreach (Transform child in safe)
            {
                var image = child.GetComponent<Image>();
                if (image == null || !child.name.StartsWith("Btn_"))
                    continue;
                var rect = (RectTransform)child;
                Behaviour input = child.GetComponent<OnScreenButton>();
                if (input == null) // explicit check: never ?? on Unity objects
                    input = child.GetComponent<TouchTapButton>();
                if (input == null)
                    input = child.GetComponent<TouchHoldButton>();
                var entry = new ControlEntry
                {
                    Id = child.name.Substring(4),
                    Rect = rect,
                    Image = image,
                    DefaultPosition = rect.anchoredPosition,
                    DefaultSize = rect.sizeDelta.x,
                    Input = input,
                };
                var drag = child.gameObject.AddComponent<DraggableControl>();
                drag.Moved += () => OnControlMoved(entry);
                drag.enabled = false;
                _controls.Add(entry);
            }
            BuildEditBar(safe);
            ApplyLayout();
        }

        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        private void BuildJoystick(RectTransform parent)
        {
            // Inactive while wiring so the On-Screen control registers with its path already set.
            var zone = UiFactory.CreateRect("JoystickZone", parent);
            zone.gameObject.SetActive(false);
            UiFactory.Anchor(zone, new Vector2(0f, 0f), new Vector2(0.38f, 0.6f));
            var zoneImage = zone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f); // invisible but receives touches

            var stickBase = UiFactory.CreatePanel("StickBase", zone, NyxPalette.WithAlpha(NyxPalette.MidnightNavy, 0.55f));
            stickBase.sprite = UiFactory.CircleSprite;
            var baseRect = stickBase.rectTransform;
            baseRect.anchorMin = baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.sizeDelta = new Vector2(260f, 260f);
            baseRect.anchoredPosition = new Vector2(-40f, -40f);

            var ring = UiFactory.CreatePanel("Ring", baseRect, NyxPalette.WithAlpha(NyxPalette.ElectricCyan, 0.25f));
            ring.sprite = UiFactory.CircleSprite;
            UiFactory.Stretch(ring.rectTransform, 6f, 6f, 6f, 6f);

            var knob = UiFactory.CreatePanel("Knob", baseRect, NyxPalette.WithAlpha(NyxPalette.NyxPurple, 0.9f));
            knob.sprite = UiFactory.CircleSprite;
            var knobRect = knob.rectTransform;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(110f, 110f);

            _joystick = zone.gameObject.AddComponent<FloatingJoystick>();
            _joystick.Configure(zone, baseRect, knobRect, 110f);
            zone.gameObject.SetActive(true);
        }

        private static Image AddButton(RectTransform parent, string label, string controlPath, Vector2 corner,
            Vector2 offset, float size, bool active)
        {
            var image = CreateRoundButton(parent, label, corner, offset, size, active);
            image.gameObject.SetActive(false);
            var button = image.gameObject.AddComponent<OnScreenButton>();
            button.controlPath = controlPath;
            image.gameObject.SetActive(true);
            return image;
        }

        private static Image AddToggleButton(RectTransform parent, string label, Vector2 corner, Vector2 offset, float size,
            System.Action onPress)
        {
            var image = CreateRoundButton(parent, label, corner, offset, size, true);
            image.gameObject.AddComponent<TouchTapButton>().Tapped += onPress;
            return image;
        }

        private static Image CreateRoundButton(RectTransform parent, string label, Vector2 corner, Vector2 offset, float size, bool active)
        {
            var image = UiFactory.CreatePanel("Btn_" + label, parent, ButtonColor(active), raycastTarget: true);
            image.sprite = UiFactory.CircleSprite;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = corner;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offset;

            var text = UiFactory.CreateText(rect, label, size >= 150f ? 30 : 22,
                active ? NyxPalette.TextPrimary : NyxPalette.WithAlpha(NyxPalette.TextMuted, 0.8f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(text.rectTransform);
            return image;
        }

        private static Color ButtonColor(bool active) =>
            active ? NyxPalette.WithAlpha(NyxPalette.MidnightNavy, 0.75f) : NyxPalette.WithAlpha(NyxPalette.Disabled, DimmedAlpha);
    }
}
