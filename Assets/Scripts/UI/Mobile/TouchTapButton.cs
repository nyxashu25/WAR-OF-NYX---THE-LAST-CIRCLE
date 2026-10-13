using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>
    /// Fires on finger-down. Deliberately NOT a uGUI Button/Selectable: a selected Button would
    /// receive the UI "Submit" action, which shares the virtual gamepad's south button with JUMP,
    /// so tapping SPRINT and then JUMP would toggle sprint again.
    /// </summary>
    public sealed class TouchTapButton : MonoBehaviour, IPointerDownHandler
    {
        public event Action Tapped;

        public void OnPointerDown(PointerEventData eventData) => Tapped?.Invoke();
    }
}
