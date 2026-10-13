using UnityEngine;
using UnityEngine.EventSystems;
using WarOfNyx.Characters;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Forwards drags on a button (FIRE) to the aim input, so the same finger can fire and steer.
    /// The button's own press handling (On-Screen Button) is unaffected.
    /// </summary>
    public sealed class TouchDragLook : MonoBehaviour, IDragHandler
    {
        private PlayerInputRouter _router;

        public void Bind(PlayerInputRouter router) => _router = router;

        public void OnDrag(PointerEventData eventData)
        {
            if (isActiveAndEnabled && _router != null)
                _router.AddTouchLook(eventData.delta);
        }
    }
}
