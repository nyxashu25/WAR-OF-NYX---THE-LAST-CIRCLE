using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Reports finger-down / finger-up on a touch button (the EYE free-look button). Not a uGUI Selectable,
    /// for the same reason as <see cref="TouchTapButton"/>. Releases when disabled (edit mode, pause).
    /// </summary>
    public sealed class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action<bool> Changed;
        private bool _held;

        public void OnPointerDown(PointerEventData eventData) => Set(true);

        public void OnPointerUp(PointerEventData eventData) => Set(false);

        private void OnDisable() => Set(false);

        private void Set(bool held)
        {
            if (_held == held)
                return;
            _held = held;
            Changed?.Invoke(held);
        }
    }
}
