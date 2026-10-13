using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Floating virtual joystick (spec 18, left side). The stick appears wherever the thumb lands
    /// inside its touch zone and returns to its rest position when released.
    ///
    /// It is an Input System <see cref="OnScreenControl"/>: it writes to a virtual
    /// <c>&lt;Gamepad&gt;/leftStick</c>, so gameplay reads the same "Move" action as the keyboard.
    /// Tracks a single pointer id, so other fingers (look, buttons) never interfere.
    /// </summary>
    public sealed class FloatingJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [InputControl(layout = "Vector2")]
        [SerializeField] private string controlPathValue = "<Gamepad>/leftStick";

        [SerializeField] private RectTransform stickBase;
        [SerializeField] private RectTransform knob;
        [Tooltip("Thumb travel (canvas units) for full deflection at sensitivity 1.")]
        [SerializeField] private float movementRange = 110f;

        private RectTransform _zone;
        private Vector2 _restPosition;
        private int _pointerId = int.MinValue;

        /// <summary>Higher sensitivity = less thumb travel for full speed (player setting).</summary>
        public float Sensitivity { get; set; } = 1f;

        public Vector2 Value { get; private set; }

        protected override string controlPathInternal
        {
            get => controlPathValue;
            set => controlPathValue = value;
        }

        public void Configure(RectTransform zone, RectTransform baseRect, RectTransform knobRect, float range)
        {
            _zone = zone;
            stickBase = baseRect;
            knob = knobRect;
            movementRange = range;
            _restPosition = baseRect.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue || _zone == null)
                return;

            _pointerId = eventData.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)stickBase.parent, eventData.position,
                    eventData.pressEventCamera, out var local))
                stickBase.anchoredPosition = local;

            knob.anchoredPosition = Vector2.zero;
            Send(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, eventData.position,
                    eventData.pressEventCamera, out var offset))
                return;

            float range = movementRange / Mathf.Clamp(Sensitivity, 0.2f, 3f);
            offset = Vector2.ClampMagnitude(offset, range);
            knob.anchoredPosition = offset * (movementRange / range); // knob visual always uses the full ring
            Send(offset / range);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
                return;
            Release();
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }

        private void Release()
        {
            _pointerId = int.MinValue;
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
            if (stickBase != null)
                stickBase.anchoredPosition = _restPosition;
            Send(Vector2.zero);
        }

        private void Send(Vector2 value)
        {
            Value = value;
            if (isActiveAndEnabled)
                SendValueToControl(value);
        }
    }
}
