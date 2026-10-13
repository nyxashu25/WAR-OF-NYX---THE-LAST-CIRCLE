using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.Core.Match;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>One line of the results table (server-reported values, replicated).</summary>
    public struct ResultRow
    {
        public int Placement;
        public string Name;
        public int Kills;
        public float Damage;
        public float SurvivalSeconds;
        public bool IsLocal;
        public bool Winner;
    }

    /// <summary>
    /// End-of-match screen (spec 21): the victory screen for the last survivor ("WAR OF NYX /
    /// LAST SURVIVOR / VICTORY ACHIEVED" with name, kills, damage, survival time and placement),
    /// or "MATCH OVER" with the winner and your placement for everyone else, plus the standings
    /// and the automatic return to the lobby. PLACEHOLDER look.
    /// </summary>
    public sealed class MatchResultsPanel : MonoBehaviour
    {
        private const int MaxRows = 12;

        private GameObject _panel;
        private Text _title;
        private Text _subtitle;
        private Text _headline;
        private Text _stats;
        private Text _extra;
        private Text _table;
        private Text _footer;

        public event Action LeaveClicked;

        public bool Visible => _panel != null && _panel.activeSelf;

        public static MatchResultsPanel Create(Transform parent)
        {
            var go = new GameObject("MatchResults");
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<MatchResultsPanel>();
            panel.Build();
            return panel;
        }

        public void Show(IReadOnlyList<ResultRow> rows, int participants, float secondsToLobby, bool wonByTieBreak)
        {
            _panel.SetActive(true);

            ResultRow? local = null, winner = null;
            foreach (var r in rows)
            {
                if (r.IsLocal) local = r;
                if (r.Winner) winner = r;
            }

            bool victory = local.HasValue && local.Value.Winner;
            if (victory)
            {
                var me = local.Value;
                _title.text = MatchResultText.VictoryTitle;
                _title.color = NyxPalette.VictoryGold;
                _subtitle.text = MatchResultText.VictorySubtitle;
                _headline.text = MatchResultText.VictoryLine + (wonByTieBreak ? "  (TIE-BREAK)" : string.Empty);
                _headline.color = NyxPalette.VictoryGold;
                _stats.text = $"{me.Name}   ·   {me.Kills} ELIMINATIONS   ·   {me.Damage:0} DAMAGE   ·   SURVIVED {MatchResultText.Clock(me.SurvivalSeconds)}   ·   " +
                              MatchResultText.Placement(1, participants);
            }
            else
            {
                _title.text = "MATCH OVER";
                _title.color = NyxPalette.TextPrimary;
                _subtitle.text = winner.HasValue ? $"WINNER: {winner.Value.Name}" : "NO SURVIVOR";
                _headline.text = local.HasValue ? $"YOU PLACED {MatchResultText.Placement(local.Value.Placement, participants)}" : string.Empty;
                _headline.color = NyxPalette.ElectricCyan;
                _stats.text = local.HasValue
                    ? $"{local.Value.Kills} ELIMINATIONS   ·   {local.Value.Damage:0} DAMAGE   ·   SURVIVED {MatchResultText.Clock(local.Value.SurvivalSeconds)}"
                    : string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<b>#     PLAYER                 KILLS     DAMAGE     TIME</b>");
            int shown = 0;
            foreach (var r in rows)
            {
                if (shown++ >= MaxRows)
                    break;
                string name = (r.Name ?? string.Empty).Replace("<", "‹").Replace(">", "›");
                if (name.Length > 16)
                    name = name.Substring(0, 16);
                string line = $"{(r.Placement > 0 ? r.Placement.ToString() : "-"),-5} {name,-22} {r.Kills,5}     {r.Damage,6:0}     {MatchResultText.Clock(r.SurvivalSeconds),5}";
                sb.AppendLine(r.Winner ? $"<color=#FFC857>{line}</color>" : r.IsLocal ? $"<color=#00E5FF>{line}</color>" : line);
            }
            _table.text = sb.ToString();
            _footer.text = $"RETURNING TO THE LOBBY IN {Mathf.CeilToInt(Mathf.Max(0f, secondsToLobby))} s";
        }

        /// <summary>An extra line under the stats (offline: bots eliminated, accuracy, difficulty).</summary>
        public void SetExtraLine(string text) => _extra.text = text ?? string.Empty;

        /// <summary>Replaces the "returning to the lobby" footer (offline matches have no lobby).</summary>
        public void SetFooter(string text) => _footer.text = text ?? string.Empty;

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("MatchResultsCanvas", sortingOrder: 30);
            canvas.transform.SetParent(transform, false);

            var panel = UiFactory.CreatePanel("Results", canvas.transform, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.88f), raycastTarget: true);
            UiFactory.Stretch(panel.rectTransform);
            _panel = panel.gameObject;

            var safe = UiFactory.CreateRect("SafeArea", panel.rectTransform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var column = UiFactory.CreateRect("Column", safe);
            UiFactory.Anchor(column, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.96f));
            UiFactory.AddVerticalLayout(column.gameObject, 8f, null, TextAnchor.UpperCenter);

            _title = UiFactory.CreateText(column, string.Empty, 84, NyxPalette.VictoryGold, TextAnchor.MiddleCenter, FontStyle.Bold, 100f);
            var outline = _title.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.NyxPurple;
            outline.effectDistance = new Vector2(4f, -4f);
            _subtitle = UiFactory.CreateText(column, string.Empty, 44, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold, 56f);
            _headline = UiFactory.CreateText(column, string.Empty, 36, NyxPalette.VictoryGold, TextAnchor.MiddleCenter, FontStyle.Bold, 48f);
            _stats = UiFactory.CreateText(column, string.Empty, 24, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Normal, 40f);
            _extra = UiFactory.CreateText(column, string.Empty, 22, NyxPalette.ElectricCyan, TextAnchor.MiddleCenter, FontStyle.Normal, 32f);

            _table = UiFactory.CreateText(column, string.Empty, 22, NyxPalette.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, 420f);
            _table.supportRichText = true;
            _table.font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Roboto Mono", "monospace" }, 22) ?? _table.font;

            _footer = UiFactory.CreateText(column, string.Empty, 24, NyxPalette.ElectricCyan, TextAnchor.MiddleCenter, FontStyle.Bold, 40f);
            UiFactory.CreateButton(column, "LEAVE MATCH", () => LeaveClicked?.Invoke(), 84f, 32);

            _panel.SetActive(false);
        }
    }
}
