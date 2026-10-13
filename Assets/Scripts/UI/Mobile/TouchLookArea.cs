using UnityEngine;
using UnityEngine.EventSystems;
using WarOfNyx.Characters;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Swipe-to-look zone (spec 18, right side). Sits behind the action buttons so a finger on a
    /// button never turns the camera, while a finger on empty screen does.
    /// Tracks one pointer id; deltas go straight to the input router as aim input.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private PlayerInputRouter _router;
        private int _pointerId = int.MinValue;

        public void Bind(PlayerInputRouter router) => _router = router;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId == int.MinValue)
                _pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId && _router != null)
                _router.AddTouchLook(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
                _pointerId = int.MinValue;
        }

        private void OnDisable() => _pointerId = int.MinValue;
    }
}
