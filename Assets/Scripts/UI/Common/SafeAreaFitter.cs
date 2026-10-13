using UnityEngine;

namespace WarOfNyx.UI.Common
{
    /// <summary>
    /// Keeps a RectTransform inside the device safe area (notches, punch-hole cameras,
    /// rounded corners — spec 18 "Respect device safe areas and display cutouts").
    /// Put it on a full-screen child of the canvas; place interactive UI under it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedArea;
        private Vector2Int _appliedScreen;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedArea || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
                Apply();
        }

        private void Apply()
        {
            var area = Screen.safeArea;
            _appliedArea = area;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var min = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            var max = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
