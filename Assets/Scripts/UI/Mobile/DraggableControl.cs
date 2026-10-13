using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WarOfNyx.UI.Mobile
{
    /// <summary>Layout editor handle: while enabled, dragging moves the control (canvas units).</summary>
    public sealed class DraggableControl : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        public event Action Moved;

        public void OnDrag(PointerEventData eventData)
        {
            if (!enabled)
                return;
            var rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (enabled)
                Moved?.Invoke();
        }
    }
}
