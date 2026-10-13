using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>One row of the loot panel, already formatted by the gameplay layer.</summary>
    public struct LootEntryView
    {
        /// <summary>Null or empty = this container slot is empty (row hidden).</summary>
        public string Name;
        public string Detail;
        public Color Color;
        public string Action;
        public bool Enabled;
    }

    /// <summary>
    /// Loot capsule interface (spec 14 "Looting Interface"): item names, rarity colours, ammunition
    /// quantities and magazines, TAKE / SWAP buttons with weapon comparison, TAKE ALL, and the
    /// player's current inventory. Presentation only: every button sends a request the server
    /// validates (distance, availability, atomic claim).
    /// </summary>
    public sealed class LootPanel : MonoBehaviour
    {
        public const int Rows = 12;

        private readonly List<(GameObject root, Image swatch, Text name, Text detail, MenuButton button)> _rows =
            new List<(GameObject, Image, Text, Text, MenuButton)>();
        private GameObject _panel;
        private Text _title;
        private Text _inventory;
        private Text _empty;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public event Action<int> TakeRequested;
        public event Action TakeAllRequested;
        public event Action CloseRequested;

        public static LootPanel Create(Transform parent)
        {
            var go = new GameObject("LootPanel");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<LootPanel>();
            panel.Build();
            return panel;
        }

        public void Open(string title)
        {
            _title.text = title;
            _panel.SetActive(true);
        }

        public void Close() => _panel.SetActive(false);

        public void SetEntries(IReadOnlyList<LootEntryView> entries)
        {
            bool any = false;
            for (int i = 0; i < Rows; i++)
            {
                var row = _rows[i];
                bool visible = entries != null && i < entries.Count && !string.IsNullOrEmpty(entries[i].Name);
                row.root.SetActive(visible);
                if (!visible)
                    continue;
                any = true;
                var e = entries[i];
                row.swatch.color = e.Color;
                row.name.text = e.Name;
                row.name.color = e.Color;
                row.detail.text = e.Detail;
                row.button.SetLabel(e.Action);
                row.button.Interactable = e.Enabled;
            }
            _empty.gameObject.SetActive(!any);
        }

        public void SetInventorySummary(string text) => _inventory.text = text;

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("LootCanvas", sortingOrder: 30);
            canvas.transform.SetParent(transform, false);
            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var panel = UiFactory.CreatePanel("Panel", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.92f), raycastTarget: true);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.94f));
            _panel = panel.gameObject;

            var column = panel.rectTransform;
            UiFactory.AddVerticalLayout(column.gameObject, 6f, new RectOffset(24, 24, 18, 18), TextAnchor.UpperCenter);

            _title = UiFactory.CreateText(column, "LOOT", 40, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 52f);
            _inventory = UiFactory.CreateText(column, string.Empty, 20, NyxPalette.ElectricCyan, TextAnchor.MiddleLeft, FontStyle.Normal, 52f);
            _empty = UiFactory.CreateText(column, "EMPTY", 28, NyxPalette.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic, 40f);

            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                var row = UiFactory.CreateRect("Row" + i, column);
                UiFactory.AddLayoutElement(row.gameObject, 46f);

                var swatch = UiFactory.CreatePanel("Rarity", row, Color.white);
                var sr = swatch.rectTransform;
                sr.anchorMin = new Vector2(0f, 0.1f);
                sr.anchorMax = new Vector2(0f, 0.9f);
                sr.pivot = new Vector2(0f, 0.5f);
                sr.sizeDelta = new Vector2(8f, 0f);

                var name = UiFactory.CreateText(row, string.Empty, 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiFactory.Anchor(name.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.40f, 1f));
                var detail = UiFactory.CreateText(row, string.Empty, 19, NyxPalette.TextMuted, TextAnchor.MiddleLeft);
                UiFactory.Anchor(detail.rectTransform, new Vector2(0.40f, 0f), new Vector2(0.78f, 1f));

                var button = UiFactory.CreateButton(row, "TAKE", () => TakeRequested?.Invoke(index), 42f, 22);
                var br = (RectTransform)button.Button.transform;
                br.anchorMin = new Vector2(0.79f, 0.05f);
                br.anchorMax = new Vector2(1f, 0.95f);
                br.offsetMin = br.offsetMax = Vector2.zero;
                Destroy(button.Button.GetComponent<LayoutElement>());

                _rows.Add((row.gameObject, swatch, name, detail, button));
            }

            var actions = UiFactory.CreateRow("Actions", column, 70f, 16f);
            UiFactory.CreateButton(actions, "TAKE ALL", () => TakeAllRequested?.Invoke(), 70f, 28);
            UiFactory.CreateButton(actions, "CLOSE", () => CloseRequested?.Invoke(), 70f, 28);

            _panel.SetActive(false);
        }
    }
}
