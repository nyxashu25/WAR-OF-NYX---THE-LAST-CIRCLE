using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.Core.Hunt;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>An exposed camper as seen by another player (replicated position and name).</summary>
    public struct HuntTarget
    {
        public Vector3 Position;
        public string Name;
    }

    /// <summary>
    /// Nyx Hunt HUD (spec 13, 19): the local player's camping status ("KEEP MOVING · exposed in 6 s",
    /// "EXPOSED · −2 HP/s · MOVE TO CLEAR") and direction indicators toward every exposed player
    /// (a marker above them when on screen, an edge arrow when not). Presentation only.
    /// </summary>
    public sealed class HuntHud : MonoBehaviour
    {
        private const int MaxIndicators = 6;

        private RectTransform _root;
        private Text _status;
        private readonly List<(RectTransform rect, UiArrow arrow, Text label)> _indicators = new List<(RectTransform, UiArrow, Text)>();

        public static HuntHud Create(Transform parent)
        {
            var go = new GameObject("HuntHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<HuntHud>();
            hud.Build();
            return hud;
        }

        /// <param name="settings">Thresholds for the countdown text (the server decides the real state).</param>
        public void Apply(in HuntStatus status, bool alive, float now, HuntSettings settings, Vector3 player, IReadOnlyList<HuntTarget> exposed)
        {
            _status.text = alive ? StatusText(status, now, settings) : string.Empty;
            UpdateIndicators(player, exposed);
        }

        public void Hide()
        {
            _status.text = string.Empty;
            foreach (var i in _indicators)
                i.rect.gameObject.SetActive(false);
        }

        public static string StatusText(in HuntStatus status, float now, HuntSettings s)
        {
            float inactive = Mathf.Max(0f, now - status.InactiveSince);
            switch (status.State)
            {
                case HuntState.CampingWarning:
                    string head = status.WarningLevel >= 2 ? HuntText.FinalWarning : HuntText.Warning;
                    return $"{head}  ·  EXPOSED IN {Mathf.CeilToInt(Mathf.Max(0f, s.exposeSeconds - inactive))} s";
                case HuntState.Exposed:
                    return $"EXPOSED FOR CAMPING  ·  −{s.damagePerSecond:0} HP/s  ·  MOVE {s.meaningfulNetDisplacement:0} m TO CLEAR";
                case HuntState.CampingPenalty:
                    return $"CAMPING PENALTY  ·  −{s.damagePerSecond:0} HP/s  ·  MOVE {s.meaningfulNetDisplacement:0} m TO CLEAR";
                default:
                    return string.Empty;
            }
        }

        private void UpdateIndicators(Vector3 player, IReadOnlyList<HuntTarget> exposed)
        {
            var cam = Camera.main;
            int count = exposed != null && cam != null ? Mathf.Min(exposed.Count, MaxIndicators) : 0;
            var size = _root.rect.size;

            for (int i = 0; i < _indicators.Count; i++)
            {
                var (rect, arrow, label) = _indicators[i];
                if (i >= count)
                {
                    rect.gameObject.SetActive(false);
                    continue;
                }
                rect.gameObject.SetActive(true);

                var target = exposed[i];
                float distance = Vector3.Distance(player, target.Position);
                label.text = $"{target.Name}  {Mathf.RoundToInt(distance)} m";

                var screen = cam.WorldToViewportPoint(target.Position + Vector3.up * 2.4f);
                bool onScreen = screen.z > 0f && screen.x > 0.04f && screen.x < 0.96f && screen.y > 0.06f && screen.y < 0.94f;
                if (onScreen)
                {
                    rect.anchoredPosition = new Vector2((screen.x - 0.5f) * size.x, (screen.y - 0.5f) * size.y);
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f); // points down at the camper
                }
                else
                {
                    // Behind the camera the projection flips; mirror it so the arrow points the right way.
                    var dir = new Vector2(screen.x - 0.5f, screen.y - 0.5f);
                    if (screen.z < 0f)
                        dir = -dir;
                    if (dir.sqrMagnitude < 1e-6f)
                        dir = Vector2.down;
                    dir.Normalize();
                    var edge = new Vector2(size.x * 0.44f, size.y * 0.4f);
                    float scale = Mathf.Min(edge.x / Mathf.Max(Mathf.Abs(dir.x), 1e-4f), edge.y / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));
                    rect.anchoredPosition = dir * scale;
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                }
            }
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("HuntHudCanvas", sortingOrder: 6);
            canvas.transform.SetParent(transform, false);
            _root = UiFactory.CreateRect("Root", canvas.transform);
            UiFactory.Stretch(_root);

            _status = UiFactory.CreateText(_root, string.Empty, 26, NyxPalette.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_status.rectTransform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.25f));
            var outline = _status.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f);

            for (int i = 0; i < MaxIndicators; i++)
            {
                var rect = UiFactory.CreateRect("HuntIndicator", _root);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(200f, 60f);

                var arrowGo = new GameObject("Arrow", typeof(RectTransform));
                arrowGo.transform.SetParent(rect, false);
                var arrow = arrowGo.AddComponent<UiArrow>();
                arrow.raycastTarget = false;
                arrow.color = NyxPalette.Crimson;
                arrow.rectTransform.sizeDelta = new Vector2(26f, 30f);

                var label = UiFactory.CreateText(rect, string.Empty, 18, NyxPalette.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(220f, 24f);
                labelRect.anchoredPosition = new Vector2(0f, 28f);
                label.gameObject.AddComponent<Outline>().effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f);

                rect.gameObject.SetActive(false);
                _indicators.Add((rect, arrow, label));
            }
        }
    }
}
