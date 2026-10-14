using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// In-match combat HUD (spec 19): health, weapon and ammunition, slots, dynamic crosshair,
    /// hit markers, damage flash and direction, kill feed, elimination screen.
    /// Purely presentation of replicated/predicted state, pushed by the local player's combat
    /// component. Placeholder layout (code-built); Phase 10/11 replace the look, not the API.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        private const int FeedLines = 5;
        private const float FeedLifetime = 7f;

        private Image _healthFill;
        private Text _healthText;
        private Text _weaponText;
        private Text _ammoText;
        private Text _slotsText;
        private Text _inventoryText;
        private Text _healText;
        private Text _promptText;
        private Text _messageText;
        private float _messageUntil;
        private RectTransform[] _crosshair;
        private RectTransform _crosshairRoot;
        private CanvasGroup _hitMarker;
        private Image[] _hitMarkerBars;
        private Image _damageFlash;
        private RectTransform _damageArrow;
        private Image _damageArrowImage;
        private GameObject _eliminated;
        private Text _eliminatedBy;
        private Text _eliminatedDetails;
        private bool _eliminatedSuppressed;

        /// <summary>Elimination screen buttons (spec 21): spectate survivors, or leave to the main menu.</summary>
        public event Action SpectateClicked;
        public event Action LeaveClicked;
        private Text _feed;
        private readonly List<(string text, float until, bool highlight)> _feedEntries = new List<(string, float, bool)>();

        private bool _firearmsBlocked;
        private float _hitMarkerUntil;
        private float _flashAlpha;
        private float _arrowAlpha;

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public static CombatHud Create(Transform parent)
        {
            var go = new GameObject("CombatHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<CombatHud>();
            hud.Build();
            return hud;
        }

        // ------------------------------------------------------------------ API

        public void SetVitals(float health, float maxHealth)
        {
            float ratio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
            _healthFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            _healthFill.color = ratio > 0.5f ? NyxPalette.ElectricCyan : ratio > 0.25f ? NyxPalette.VictoryGold : NyxPalette.Crimson;
            _healthText.text = Mathf.CeilToInt(health).ToString();
        }

        public void SetWeapon(string weaponName, bool ranged, int magazine, int reserve, float reloadProgress)
        {
            _weaponText.text = weaponName.ToUpperInvariant();
            if (!ranged)
                _ammoText.text = "MELEE";
            else if (reloadProgress > 0f)
                _ammoText.text = $"RELOADING {Mathf.RoundToInt(reloadProgress * 100f)}%";
            else
                _ammoText.text = $"{magazine} / {reserve}";
            _ammoText.color = ranged && magazine == 0 && reloadProgress <= 0f ? NyxPalette.Crimson : NyxPalette.TextPrimary;
        }

        /// <summary>Driving: the vehicle name and speed replace the weapon line; the second line shows its health.</summary>
        public void SetVehicle(string vehicleName, int kmh, float health, float maxHealth)
        {
            _weaponText.text = $"{vehicleName.ToUpperInvariant()}   {kmh} KM/H";
            _ammoText.text = $"VEHICLE {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}";
            _ammoText.color = health < maxHealth * 0.3f ? NyxPalette.Crimson : NyxPalette.TextPrimary;
        }

        public void SetSlots(string slot1, string slot2, string slot3, int activeSlot)
        {
            string Mark(int i, string n) => string.IsNullOrEmpty(n) ? $"[{i + 1}] -" : (i == activeSlot ? $"<b>[{i + 1}] {n}</b>" : $"[{i + 1}] {n}");
            _slotsText.text = $"{Mark(0, slot1)}    {Mark(1, slot2)}    {Mark(2, slot3)}";
        }

        /// <summary>Crosshair gap matches the real spread cone at the camera's field of view.</summary>
        public void SetCrosshair(float spreadDegrees, float verticalFov, bool visible)
        {
            _crosshairRoot.gameObject.SetActive(visible);
            if (!visible)
                return;
            float halfHeight = UiFactory.ReferenceResolution.y * 0.5f;
            float gap = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad) / Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * halfHeight;
            gap = Mathf.Clamp(gap, 6f, 160f);
            _crosshair[0].anchoredPosition = new Vector2(0f, gap);
            _crosshair[1].anchoredPosition = new Vector2(0f, -gap);
            _crosshair[2].anchoredPosition = new Vector2(gap, 0f);
            _crosshair[3].anchoredPosition = new Vector2(-gap, 0f);
        }

        /// <summary>Outside the circle (spec 11): the crosshair turns crimson and dims to show guns can't fire.</summary>
        public void SetFirearmsBlocked(bool blocked)
        {
            if (_firearmsBlocked == blocked)
                return;
            _firearmsBlocked = blocked;
            var color = blocked ? NyxPalette.WithAlpha(NyxPalette.Crimson, 0.55f) : NyxPalette.WithAlpha(Color.white, 0.9f);
            foreach (var bar in _crosshair)
                bar.GetComponent<Image>().color = color;
        }

        /// <summary>Server-confirmed hit (spec 9 "hit markers"): white, gold for headshots, crimson for eliminations.</summary>
        public void HitMarker(bool headshot, bool kill)
        {
            var color = kill ? NyxPalette.Crimson : headshot ? NyxPalette.VictoryGold : Color.white;
            foreach (var bar in _hitMarkerBars)
                bar.color = color;
            _hitMarkerUntil = Time.unscaledTime + (kill ? 0.45f : 0.2f);
        }

        /// <param name="relativeYawDegrees">Direction of the attacker relative to the camera (0 = in front).</param>
        public void DamageTaken(float amount, float relativeYawDegrees)
        {
            _flashAlpha = Mathf.Clamp(amount / 60f, 0.15f, 0.45f) * (ReduceFlashing ? 0.4f : 1f);
            _damageArrow.localRotation = Quaternion.Euler(0f, 0f, -relativeYawDegrees);
            _arrowAlpha = 1f;
        }

        public void AddFeed(string text, bool highlight)
        {
            _feedEntries.Add((text, Time.unscaledTime + FeedLifetime, highlight));
            while (_feedEntries.Count > FeedLines)
                _feedEntries.RemoveAt(0);
            RefreshFeed();
        }

        /// <summary>Armor, medkits and spare ammo line (spec 19: armor, medkit quantity).</summary>
        public void SetInventory(string summary) => _inventoryText.text = summary;

        /// <summary>Medkit channel progress; 0 hides it.</summary>
        public void SetHealing(float progress, string medkitName)
        {
            _healText.text = progress > 0f ? $"HEALING {Mathf.RoundToInt(progress * 100f)}%  ({medkitName})" : string.Empty;
        }

        /// <summary>Contextual prompt above the HUD, e.g. "[E] OPEN LOOT". Empty hides it.</summary>
        public void SetPrompt(string prompt) => _promptText.text = prompt;

        /// <summary>Short notification (loot results, heal refusals).</summary>
        public void ShowMessage(string message, float seconds = 2.5f)
        {
            _messageText.text = message;
            _messageUntil = Time.unscaledTime + seconds;
        }

        /// <param name="causeText">Overrides the line, e.g. "Consumed by the Nyx Circle.".</param>
        public void ShowEliminated(string killerName, string weaponName, string causeText = null, string details = null)
        {
            _eliminated.SetActive(!_eliminatedSuppressed);
            _eliminatedDetails.text = details ?? string.Empty;
            _eliminatedBy.text = !string.IsNullOrEmpty(causeText) ? causeText
                : string.IsNullOrEmpty(killerName)
                ? "You were eliminated."
                : $"Eliminated by {killerName}{(string.IsNullOrEmpty(weaponName) ? string.Empty : $" with {weaponName}")}";
            _crosshairRoot.gameObject.SetActive(false);
        }

        public void HideEliminated() => _eliminated.SetActive(false);

        public bool EliminatedVisible => _eliminated != null && _eliminated.activeSelf;

        /// <summary>While spectating or on the results screen the elimination panel stays hidden.</summary>
        public void SuppressEliminated(bool suppress)
        {
            _eliminatedSuppressed = suppress;
            if (suppress)
                _eliminated.SetActive(false);
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _hitMarker.alpha = Time.unscaledTime < _hitMarkerUntil ? 1f : 0f;
            if (_messageText.text.Length > 0 && Time.unscaledTime > _messageUntil)
                _messageText.text = string.Empty;

            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 0.9f);
            _damageFlash.color = NyxPalette.WithAlpha(NyxPalette.Crimson, _flashAlpha);

            _arrowAlpha = Mathf.MoveTowards(_arrowAlpha, 0f, dt * 0.8f);
            _damageArrowImage.color = NyxPalette.WithAlpha(NyxPalette.Crimson, _arrowAlpha);

            if (_feedEntries.Count > 0 && _feedEntries[0].until < Time.unscaledTime)
            {
                _feedEntries.RemoveAll(e => e.until < Time.unscaledTime);
                RefreshFeed();
            }
        }

        private void RefreshFeed()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (text, _, highlight) in _feedEntries)
            {
                string safe = text.Replace("<", "‹").Replace(">", "›"); // player names never inject markup
                sb.AppendLine(highlight ? $"<color=#FF304F>{safe}</color>" : safe);
            }
            _feed.text = sb.ToString();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("CombatHudCanvas", sortingOrder: 5);
            canvas.transform.SetParent(transform, false);

            _damageFlash = UiFactory.CreatePanel("DamageFlash", canvas.transform, NyxPalette.WithAlpha(NyxPalette.Crimson, 0f));
            UiFactory.Stretch(_damageFlash.rectTransform);

            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Health (bottom centre).
            var healthBack = UiFactory.CreatePanel("HealthBar", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.7f));
            var hb = healthBack.rectTransform;
            hb.anchorMin = hb.anchorMax = new Vector2(0.5f, 0f);
            hb.pivot = new Vector2(0.5f, 0f);
            hb.sizeDelta = new Vector2(520f, 26f);
            hb.anchoredPosition = new Vector2(0f, 40f);
            _healthFill = UiFactory.CreatePanel("Fill", hb, NyxPalette.ElectricCyan);
            UiFactory.Stretch(_healthFill.rectTransform, 3f, 3f, 3f, 3f);
            _healthText = UiFactory.CreateText(hb, "100", 22, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_healthText.rectTransform);

            _weaponText = Label(safe, new Vector2(0.5f, 0f), new Vector2(0f, 104f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            _ammoText = Label(safe, new Vector2(0.5f, 0f), new Vector2(0f, 76f), 26, TextAnchor.MiddleCenter, FontStyle.Bold);
            _slotsText = Label(safe, new Vector2(0.5f, 0f), new Vector2(0f, 134f), 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            _slotsText.supportRichText = true;
            _slotsText.color = NyxPalette.TextMuted;
            _inventoryText = Label(safe, new Vector2(0.5f, 0f), new Vector2(0f, 160f), 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            _inventoryText.color = NyxPalette.ElectricCyan;
            _healText = Label(safe, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            _healText.color = new Color(0.3f, 0.95f, 0.5f);
            _promptText = Label(safe, new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            _promptText.color = NyxPalette.VictoryGold;
            _messageText = Label(safe, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Crosshair (centre of the full screen, matching the camera centre).
            _crosshairRoot = UiFactory.CreateRect("Crosshair", canvas.transform);
            _crosshairRoot.anchorMin = _crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshair = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = UiFactory.CreatePanel("Bar", _crosshairRoot, NyxPalette.WithAlpha(Color.white, 0.9f)).rectTransform;
                bar.sizeDelta = i < 2 ? new Vector2(3f, 14f) : new Vector2(14f, 3f);
                _crosshair[i] = bar;
            }
            UiFactory.CreatePanel("Dot", _crosshairRoot, NyxPalette.ElectricCyan).rectTransform.sizeDelta = new Vector2(4f, 4f);

            // Hit marker: four short diagonal bars.
            var marker = UiFactory.CreateRect("HitMarker", canvas.transform);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            _hitMarker = marker.gameObject.AddComponent<CanvasGroup>();
            _hitMarker.alpha = 0f;
            _hitMarker.blocksRaycasts = false;
            _hitMarkerBars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = UiFactory.CreatePanel("X", marker, Color.white);
                float angle = 45f + 90f * i;
                var dir = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                bar.rectTransform.sizeDelta = new Vector2(4f, 16f);
                bar.rectTransform.anchoredPosition = (Vector2)dir * 18f;
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                _hitMarkerBars[i] = bar;
            }

            // Damage direction arrow orbiting the centre.
            _damageArrow = UiFactory.CreateRect("DamageDirection", canvas.transform);
            _damageArrow.anchorMin = _damageArrow.anchorMax = new Vector2(0.5f, 0.5f);
            _damageArrowImage = UiFactory.CreatePanel("Arc", _damageArrow, NyxPalette.WithAlpha(NyxPalette.Crimson, 0f));
            _damageArrowImage.rectTransform.sizeDelta = new Vector2(120f, 10f);
            _damageArrowImage.rectTransform.anchoredPosition = new Vector2(0f, 190f);

            // Kill feed (top right, left of the minimap).
            _feed = UiFactory.CreateText(safe, string.Empty, 22, NyxPalette.TextPrimary, TextAnchor.UpperRight);
            _feed.supportRichText = true;
            UiFactory.Anchor(_feed.rectTransform, new Vector2(0.42f, 0.62f), new Vector2(0.84f, 0.9f));

            // Elimination screen (spec 21): who, kills, time survived, placement, SPECTATE / RETURN TO LOBBY.
            var panel = UiFactory.CreatePanel("Eliminated", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.6f), raycastTarget: true);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.25f, 0.36f), new Vector2(0.75f, 0.74f));
            var column = panel.rectTransform;
            UiFactory.AddVerticalLayout(column.gameObject, 8f, new RectOffset(20, 20, 20, 20), TextAnchor.MiddleCenter);
            UiFactory.CreateText(column, "ELIMINATED", 72, NyxPalette.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold, 90f);
            _eliminatedBy = UiFactory.CreateText(column, string.Empty, 30, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold, 44f);
            _eliminatedDetails = UiFactory.CreateText(column, string.Empty, 24, NyxPalette.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, 36f);
            var buttons = UiFactory.CreateRow("Buttons", column, 80f, 20f);
            UiFactory.CreateButton(buttons, "SPECTATE", () => SpectateClicked?.Invoke(), 76f, 30);
            UiFactory.CreateButton(buttons, "RETURN TO LOBBY", () => LeaveClicked?.Invoke(), 76f, 30);
            _eliminated = panel.gameObject;
            _eliminated.SetActive(false);
        }

        private static Text Label(RectTransform parent, Vector2 anchor, Vector2 position, int size, TextAnchor alignment, FontStyle style)
        {
            var text = UiFactory.CreateText(parent, string.Empty, size, NyxPalette.TextPrimary, alignment, style);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(900f, 34f);
            rect.anchoredPosition = position;
            return text;
        }
    }
}
