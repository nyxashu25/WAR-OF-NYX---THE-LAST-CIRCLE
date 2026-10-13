using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.Core.Zone;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// North-up minimap (spec 19 "Minimap Requirements"), centred on the local player:
    /// a top-down map of the arena (baked once from the level's colliders), the player's position
    /// and facing, the current Nyx Circle, the next circle, and an edge arrow toward the safe zone.
    /// Shows no other players (spec: never reveal enemy locations; exposed campers arrive in Phase 8).
    /// PLACEHOLDER look; Phase 10 restyles it.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        public const float PanelSize = 240f;
        private const int MapResolution = 128;
        private const float MaxViewRadius = 150f;

        private RectTransform _content;
        private RawImage _map;
        private UiRing _current;
        private UiRing _next;
        private RectTransform _player;
        private RectTransform _toSafe;
        private Texture2D _texture;

        private Vector2 _mapCentre;
        private float _mapHalf = 60f;
        private float _viewRadius = 60f;

        public static Minimap Create(RectTransform parent)
        {
            var panel = UiFactory.CreatePanel("Minimap", parent, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.75f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(PanelSize, PanelSize);
            rect.anchoredPosition = new Vector2(-20f, -125f);
            panel.gameObject.AddComponent<RectMask2D>();
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.WithAlpha(NyxPalette.NyxPurple, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var minimap = panel.gameObject.AddComponent<Minimap>();
            minimap.Build(rect);
            return minimap;
        }

        /// <summary>
        /// Renders the arena top-down by raycasting a grid (once per map load, client only):
        /// brighter = higher, darker = steep, blue = water.
        /// </summary>
        public void BakeMap(Vector3 centre, float halfSize)
        {
            _mapCentre = new Vector2(centre.x, centre.z);
            _mapHalf = Mathf.Max(10f, halfSize);
            _viewRadius = Mathf.Min(_mapHalf, MaxViewRadius);

            int water = LayerMask.NameToLayer("Water");
            var heights = new float[MapResolution * MapResolution];
            var kinds = new byte[MapResolution * MapResolution]; // 0 none, 1 ground, 2 steep, 3 water
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < MapResolution; y++)
            {
                for (int x = 0; x < MapResolution; x++)
                {
                    float wx = _mapCentre.x - _mapHalf + (x + 0.5f) / MapResolution * _mapHalf * 2f;
                    float wz = _mapCentre.y - _mapHalf + (y + 0.5f) / MapResolution * _mapHalf * 2f;
                    int i = y * MapResolution + x;
                    if (!Physics.Raycast(new Vector3(wx, centre.y + 300f, wz), Vector3.down, out var hit, 700f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        continue;
                    heights[i] = hit.point.y;
                    kinds[i] = water >= 0 && hit.collider.gameObject.layer == water ? (byte)3
                        : Vector3.Angle(hit.normal, Vector3.up) > 40f ? (byte)2 : (byte)1;
                    min = Mathf.Min(min, hit.point.y);
                    max = Mathf.Max(max, hit.point.y);
                }
            }

            float range = Mathf.Max(1f, max - min);
            var pixels = new Color32[heights.Length];
            var low = new Color(0.10f, 0.11f, 0.20f);
            var high = new Color(0.42f, 0.44f, 0.62f);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c;
                switch (kinds[i])
                {
                    case 0: c = new Color(0.02f, 0.02f, 0.05f); break;
                    case 3: c = new Color(0.08f, 0.22f, 0.45f); break;
                    default:
                        c = Color.Lerp(low, high, (heights[i] - min) / range);
                        if (kinds[i] == 2)
                            c *= 0.6f;
                        break;
                }
                c.a = 1f;
                pixels[i] = c;
            }

            if (_texture == null)
            {
                _texture = new Texture2D(MapResolution, MapResolution, TextureFormat.RGBA32, false)
                {
                    name = "Minimap (runtime)",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false);
            _map.texture = _texture;
            _map.color = Color.white;
        }

        /// <param name="yaw">Facing in degrees (0 = north/+Z, clockwise).</param>
        public void SetView(Vector3 player, float yaw, bool zoneActive, in ZoneCircle current, in ZoneCircle next, bool showNext, bool outside)
        {
            float scale = PanelSize * 0.5f / _viewRadius; // canvas units per metre
            var p = new Vector2(player.x, player.z);

            _map.rectTransform.sizeDelta = Vector2.one * (_mapHalf * 2f * scale);
            _map.rectTransform.anchoredPosition = (_mapCentre - p) * scale;
            _player.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            _current.gameObject.SetActive(zoneActive && current.Radius > 0.01f);
            if (_current.gameObject.activeSelf)
            {
                _current.rectTransform.anchoredPosition = (new Vector2(current.X, current.Z) - p) * scale;
                _current.Radius = current.Radius * scale;
                _current.color = outside ? NyxPalette.Crimson : NyxPalette.ElectricCyan;
            }

            _next.gameObject.SetActive(zoneActive && showNext && next.Radius > 0.01f);
            if (_next.gameObject.activeSelf)
            {
                _next.rectTransform.anchoredPosition = (new Vector2(next.X, next.Z) - p) * scale;
                _next.Radius = next.Radius * scale;
            }

            // Edge arrow toward the safe zone when it is off the minimap or the player is outside.
            var toCentre = new Vector2(current.X, current.Z) - p;
            bool pointToSafety = zoneActive && current.Radius > 0f && (outside || toCentre.magnitude * scale > PanelSize * 0.5f);
            _toSafe.gameObject.SetActive(pointToSafety);
            if (pointToSafety && toCentre.sqrMagnitude > 0.0001f)
            {
                var dir = toCentre.normalized;
                _toSafe.anchoredPosition = dir * (PanelSize * 0.5f - 16f);
                _toSafe.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }
        }

        private void Build(RectTransform panel)
        {
            _content = UiFactory.CreateRect("Content", panel);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(_content, false);
            _map = mapGo.AddComponent<RawImage>();
            _map.raycastTarget = false;
            _map.color = new Color(0.05f, 0.05f, 0.1f); // until a map is baked
            var mapRect = _map.rectTransform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0.5f, 0.5f);

            _next = Ring("NextCircle", Color.white, 2f);
            _current = Ring("Circle", NyxPalette.ElectricCyan, 3f);

            _player = Arrow("Player", NyxPalette.VictoryGold, new Vector2(16f, 20f));
            _toSafe = Arrow("ToSafeZone", NyxPalette.ElectricCyan, new Vector2(18f, 16f));
            _toSafe.gameObject.SetActive(false);
        }

        private UiRing Ring(string name, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var ring = go.AddComponent<UiRing>();
            ring.raycastTarget = false;
            ring.color = color;
            ring.Thickness = thickness;
            var rect = ring.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return ring;
        }

        private RectTransform Arrow(string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var arrow = go.AddComponent<UiArrow>();
            arrow.raycastTarget = false;
            arrow.color = color;
            var rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }
    }
}
