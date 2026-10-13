using UnityEngine;
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

        public bool Visible => _canvas != null && _canvas.gameObject.activeSelf;

        public static MobileControlsUI Create(PlayerInputRouter router)
        {
            var go = new GameObject("MobileControls");
            var ui = go.AddComponent<MobileControlsUI>();
            ui.Build(router);
            return ui;
        }

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
                _sprintImage.color = _router.SprintToggled ? NyxPalette.WithAlpha(NyxPalette.VictoryGold, 0.85f) : ButtonColor(true);
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
            AddButton(safe, "FIRE", "<Gamepad>/rightTrigger", BottomRight, new Vector2(-270f, 300f), 180f, active: true);
            AddButton(safe, "JUMP", "<Gamepad>/buttonSouth", BottomRight, new Vector2(-110f, 140f), 140f, active: true);
            AddButton(safe, "RELOAD", "<Gamepad>/buttonWest", BottomRight, new Vector2(-110f, 330f), 110f, active: true);
            AddButton(safe, "AIM", "<Gamepad>/leftTrigger", BottomRight, new Vector2(-450f, 300f), 120f, active: true);
            AddButton(safe, "MELEE", "<Gamepad>/rightShoulder", BottomRight, new Vector2(-300f, 110f), 110f, active: true);
            AddButton(safe, "WEAPON", "<Gamepad>/rightStickPress", BottomRight, new Vector2(-450f, 120f), 110f, active: true);
            AddButton(safe, "USE", "<Gamepad>/buttonNorth", BottomRight, new Vector2(-270f, 480f), 110f, active: true);
            AddButton(safe, "MEDKIT", "<Gamepad>/dpad/down", BottomRight, new Vector2(-110f, 500f), 100f, active: true);
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
