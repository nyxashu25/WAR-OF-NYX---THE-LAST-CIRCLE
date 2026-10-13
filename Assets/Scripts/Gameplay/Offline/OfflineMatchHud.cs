using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Zone;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.Hud;
using WarOfNyx.UI.Mobile;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>
    /// Presentation of an offline match for the local human (spec 38): the same HUD pieces as online
    /// (combat HUD, circle HUD + minimap, Nyx Hunt status, loot panel, results) plus pause (offline
    /// only), spectating bots after elimination, and "PLAYERS ALIVE x/y". Shows state; never changes rules.
    /// </summary>
    [RequireComponent(typeof(OfflineMatch))]
    public sealed class OfflineMatchHud : MonoBehaviour
    {
        private OfflineMatch _match;
        private CombatHud _hud;
        private ZoneHud _zone;
        private HuntHud _hunt;
        private LootPanel _loot;
        private MatchResultsPanel _results;
        private MobileControlsUI _mobile;
        private ThirdPersonCamera _camera;
        private readonly ZoneAnnouncer _zoneAnnouncer = new ZoneAnnouncer();
        private readonly HuntStatusAnnouncer _huntAnnouncer = new HuntStatusAnnouncer();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<HuntTarget> _exposed = new List<HuntTarget>();
        private readonly List<LootEntryView> _entries = new List<LootEntryView>();
        private readonly List<ResultRow> _rows = new List<ResultRow>();

        private Text _banner;
        private Text _alive;
        private Text _spectateLabel;
        private GameObject _spectateBar;
        private GameObject _pausePanel;
        private bool _bound;
        private bool _paused;
        private OfflineLootCrate _nearby;
        private OfflineLootCrate _open;
        private OfflineCombatant _spectating;
        private int _spectateIndex = -1;

        private static bool ReduceShake => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceCameraShake;

        private void Awake() => _match = GetComponent<OfflineMatch>();

        private void OnDestroy()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Bind()
        {
            _bound = true;
            if (Application.isBatchMode)
                return;

            _hud = CombatHud.Create(transform);
            _zone = ZoneHud.Create(transform);
            _hunt = HuntHud.Create(transform);
            _loot = LootPanel.Create(transform);
            _results = MatchResultsPanel.Create(transform);
            BuildOverlay();

            var area = ArenaSpawnArea.Current;
            if (area != null)
                _zone.Minimap.BakeMap(area.transform.position, area.ZoneRadius * 1.15f);

            _match.Feed += (text, highlight) => _hud.AddFeed(text, highlight);
            _match.Headline += (text, color) => _zone.ShowHeadline(text, color);
            _match.Message += text => _hud.ShowMessage(text, 1.5f);
            _match.HumanHit += (victim, head, kill) =>
            {
                _hud.HitMarker(head, kill);
                Effects.ProceduralAudio.PlayAt(Effects.ProceduralAudio.HitTick, _match.Human.Feet, 0.7f);
            };
            _match.HumanDamaged += (amount, source) =>
            {
                var cam = Camera.main;
                if (cam == null) return;
                var to = source - cam.transform.position;
                float relative = Vector3.SignedAngle(Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up), Vector3.ProjectOnPlane(to, Vector3.up), Vector3.up);
                _hud.DamageTaken(amount, relative);
            };
            _hud.SpectateClicked += SpectateNext;
            _hud.LeaveClicked += Leave;
            _results.LeaveClicked += Leave;
            _loot.TakeRequested += i => TakeLoot(i);
            _loot.TakeAllRequested += () => TakeLoot(-1);
            _loot.CloseRequested += CloseLoot;
            _subscriptions.Add(AppServices.Events.Subscribe<BackRequested>(_ =>
            {
                if (_loot.IsOpen) CloseLoot();
                else TogglePause();
            }));
            _subscriptions.Add(AppServices.Events.Subscribe<AppPauseChanged>(e =>
            {
                if (e.IsPaused && !_paused && _match.Phase != OfflinePhase.Ended) TogglePause(); // background = pause (offline only)
            }));

            var cam = Camera.main;
            if (cam != null)
            {
                _camera = cam.GetComponent<ThirdPersonCamera>();
                if (_camera == null) // Unity "fake null": never use ?? with components
                    _camera = cam.gameObject.AddComponent<ThirdPersonCamera>();
                var human = _match.Human;
                if (human != null)
                    _camera.Bind(human.transform, human.Movement, human.Router);
                else
                    SpectateNext();
            }

            if (_match.Human != null)
            {
                _match.Human.Router.InteractPressed += OnInteract;
                _mobile = MobileControlsUI.Create(_match.Human.Router);
                _mobile.SetVisible(Application.isMobilePlatform || Touchscreen.current != null);
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UpdateCursor();
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (!_bound)
            {
                if (_match.Phase != OfflinePhase.Starting)
                    Bind();
                return;
            }
            if (Application.isBatchMode)
                return;

            float now = _match.Now;
            _banner.text = _match.Phase == OfflinePhase.Countdown || _match.Phase == OfflinePhase.Active
                ? MatchPresentation.CountdownText(_match.FightStartedAt - now)
                : string.Empty;
            _banner.color = _banner.text == "FIGHT!" ? NyxPalette.Crimson : NyxPalette.TextPrimary;

            var resolver = _match.Resolver;
            int total = _match.Combatants.Count;
            int alive = 0;
            foreach (var c in _match.Combatants)
                if (c.Alive) alive++;
            _alive.text = _match.Phase == OfflinePhase.Ended ? string.Empty : $"PLAYERS ALIVE: {alive}/{total}";

            var human = _match.Human;
            var viewed = human != null ? human : _spectating;
            if (human != null)
                UpdateHumanHud(human, now);
            UpdateZone(viewed, now);
            UpdateHunt(human, viewed, now);
            UpdateLoot(human, now);
            UpdateSpectating();

            if (_match.Phase == OfflinePhase.Ended && resolver != null)
                ShowResults(resolver);

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame && _mobile != null)
            {
                _mobile.SetVisible(!_mobile.Visible);
                UpdateCursor();
            }
        }

        private void UpdateHumanHud(OfflineCombatant me, float now)
        {
            var weapon = me.ActiveWeapon;
            var state = me.Slots[me.ActiveSlot];
            bool ranged = weapon != null && weapon.IsRanged;
            int spare = ranged ? me.Inventory.GetAmmo(weapon.ammoType) : 0;
            _hud.SetVitals(me.Health.Health, me.Health.MaxHealth);
            _hud.SetWeapon(weapon != null ? weapon.displayName : "Unarmed", ranged, state.Magazine, spare,
                weapon != null ? WeaponRules.ReloadProgress(state, weapon, now) : 0f);
            var inv = me.Inventory;
            _hud.SetInventory($"ARMOR L{inv.ArmorLevel}     MEDKITS  S{inv.SmallMedkits}  M{inv.StandardMedkits}  L{inv.AdvancedMedkits}     " +
                              (ranged ? AmmoInfo.DisplayName(weapon.ammoType).ToUpperInvariant() + " " + spare : string.Empty));
            _hud.SetHealing(HealingRules.Progress(me.Heal, now), Medkits.DisplayName(me.Heal.Type));
            _hud.SetSlots(WeaponRegistry.DisplayName(me.Slots[0].WeaponId), WeaponRegistry.DisplayName(me.Slots[1].WeaponId),
                WeaponRegistry.DisplayName(me.Slots[2].WeaponId), me.ActiveSlot);
            var cam = Camera.main;
            float spread = ranged ? WeaponRules.CurrentSpread(state, weapon, now, me.Router.AimHeld) : 0.5f;
            _hud.SetCrosshair(spread, cam != null ? cam.fieldOfView : 60f, me.Alive && _spectating == null);

            if (!me.Alive)
            {
                string details = $"{me.Kills} ELIMINATIONS  ·  SURVIVED {MatchResultText.Clock(me.EliminatedAt - _match.FightStartedAt)}";
                if (_match.Resolver != null && _match.Resolver.TryGet(me.Id, out var rec) && rec.Placement > 0)
                    details += $"  ·  {MatchResultText.Placement(rec.Placement, _match.Combatants.Count)}";
                if (me.EliminationCause == DamageKind.Zone)
                    _hud.ShowEliminated(null, null, "Consumed by the Nyx Circle.", details);
                else if (me.EliminationCause == DamageKind.Camping)
                    _hud.ShowEliminated(null, null, "Taken by the Nyx Hunt (camping).", details);
                else
                    _hud.ShowEliminated(_match.Find(me.EliminatedBy)?.DisplayName, WeaponRegistry.DisplayName(me.EliminatedByWeapon), null, details);
            }
            else
            {
                _hud.HideEliminated();
            }

            bool blocked = me.Alive && me.OutsideZone;
            _hud.SetFirearmsBlocked(blocked);
            _mobile?.SetFireBlocked(blocked);
            if (_camera != null && _spectating == null)
                _camera.SetShake(blocked ? (ReduceShake ? 0.2f : 1f) : 0f);
            UpdateCursor();
        }

        private void UpdateZone(OfflineCombatant viewed, float now)
        {
            if (!_match.ZoneActive || viewed == null)
            {
                _zone.Hide();
                return;
            }
            var circle = _match.CurrentCircle;
            var p = viewed.Feet;
            var view = new ZoneView
            {
                Active = true,
                Stage = _match.Stage,
                Current = circle,
                Time = now,
                PlayerOutside = viewed.OutsideZone,
                PlayerAlive = viewed.Alive,
                PlayerDistanceToEdge = circle.DistanceToEdge(p.x, p.z),
            };
            _zone.Apply(view, p, viewed.Yaw);
            foreach (var a in _zoneAnnouncer.Update(view))
            {
                if (!a.Headline)
                {
                    _hud.AddFeed(a.Text, a.Kind == ZoneAnnouncementKind.FinalWarning);
                    continue;
                }
                if (_banner.text.Length > 0 && a.Kind == ZoneAnnouncementKind.PhaseStarted)
                    continue; // don't cover "FIGHT!"; the feed line says it too
                var color = a.Kind == ZoneAnnouncementKind.LeftSafeZone ? NyxPalette.Crimson
                    : a.Kind == ZoneAnnouncementKind.EnteredSafeZone ? NyxPalette.ElectricCyan
                    : a.Kind == ZoneAnnouncementKind.PhaseStarted ? NyxPalette.NyxPurple
                    : NyxPalette.VictoryGold;
                _zone.ShowHeadline(a.Text, color);
            }
        }

        private void UpdateHunt(OfflineCombatant human, OfflineCombatant viewed, float now)
        {
            _exposed.Clear();
            foreach (var c in _match.Combatants)
            {
                if (c != human && c.Alive && _match.IsExposed(c.Id))
                    _exposed.Add(new HuntTarget { Position = c.Feet, Name = c.DisplayName });
            }
            _zone.Minimap.SetHuntMarkers(_exposed);
            if (human == null || viewed == null)
            {
                _hunt.Hide();
                return;
            }
            _match.TryGetHunt(human.Id, out var status);
            var settings = AppServices.Config.hunt ?? new HuntSettings();
            _hunt.Apply(status, human.Alive, now, settings, viewed.Feet, _exposed);
            string headline = _huntAnnouncer.Update(status, human.Alive);
            if (headline != null)
                _zone.ShowHeadline(headline, headline == HuntText.Cleared ? NyxPalette.ElectricCyan : NyxPalette.Crimson);
        }

        // ------------------------------------------------------------------ loot (same panel and rules as online)

        private void UpdateLoot(OfflineCombatant me, float now)
        {
            if (me == null)
                return;
            _nearby = me.Alive ? _match.NearestCrate(me.Feet, OfflineLootCrate.InteractRange) : null;
            if (_open != null && (_open != _nearby || !me.Alive))
                CloseLoot();
            string key = Application.isMobilePlatform ? "USE" : "E";
            _hud.SetPrompt(_nearby != null && _open == null
                ? $"[{key}] OPEN LOOT - {_nearby.OwnerName}  ({Mathf.CeilToInt(_nearby.SecondsLeft(now))}s)"
                : string.Empty);
            if (_open != null)
            {
                LootViews.Build(i => _open.Contents[i], me.Slots, me.ActiveSlot, me.Inventory, _entries);
                _loot.SetEntries(_entries);
                _loot.SetInventorySummary(LootViews.Summary(me.Slots, me.Inventory, _open.SecondsLeft(now)));
            }
        }

        private void OnInteract()
        {
            if (_paused || _match.Human == null)
                return;
            if (_open != null)
                CloseLoot();
            else if (_nearby != null && _match.Human.Alive)
            {
                _open = _nearby;
                _loot.Open(_open.OwnerName.ToUpperInvariant() + " - LOOT CAPSULE");
                _match.Human.Router.InputEnabled = false;
                UpdateCursor();
            }
        }

        private void TakeLoot(int index)
        {
            if (_open == null)
                return;
            string message = _match.TryTakeLoot(_match.Human, _open, index);
            if (!string.IsNullOrEmpty(message))
                _hud.ShowMessage(message);
        }

        private void CloseLoot()
        {
            _open = null;
            if (_loot != null)
                _loot.Close();
            if (_match.Human != null)
                _match.Human.Router.InputEnabled = !_paused;
            UpdateCursor();
        }

        // ------------------------------------------------------------------ spectating, results, pause

        public void SpectateNext()
        {
            var alive = new List<OfflineCombatant>();
            foreach (var c in _match.Combatants)
                if (c.Alive && c != _match.Human)
                    alive.Add(c);
            if (alive.Count == 0 || _camera == null)
            {
                StopSpectating();
                return;
            }
            _spectateIndex = (_spectateIndex + 1) % alive.Count;
            _spectating = alive[_spectateIndex];
            _camera.Bind(_spectating.transform, _spectating.Movement, null);
            _camera.SetShake(0f);
            _spectateLabel.text = $"SPECTATING  {_spectating.DisplayName}  [BOT]";
            _spectateBar.SetActive(true);
            _hud?.SuppressEliminated(true);
        }

        private void StopSpectating()
        {
            _spectating = null;
            _spectateIndex = -1;
            if (_spectateBar != null)
                _spectateBar.SetActive(false);
            var human = _match.Human;
            if (_camera != null && human != null)
                _camera.Bind(human.transform, human.Movement, human.Router);
            _hud?.SuppressEliminated(_results != null && _results.Visible);
        }

        private void UpdateSpectating()
        {
            if (_spectating != null && !_spectating.Alive)
            {
                _spectateIndex = -1;
                SpectateNext();
            }
            var keyboard = Keyboard.current;
            if (_spectating != null && keyboard != null && keyboard.nKey.wasPressedThisFrame)
                SpectateNext();
        }

        private void ShowResults(MatchResolver resolver)
        {
            _rows.Clear();
            foreach (var r in resolver.Standings)
            {
                _rows.Add(new ResultRow
                {
                    Placement = r.Placement,
                    Name = r.Name + (_match.Human != null && r.PlayerId == _match.Human.Id ? string.Empty : "  [BOT]"),
                    Kills = r.Kills,
                    Damage = r.Damage,
                    SurvivalSeconds = r.SurvivalSeconds,
                    IsLocal = _match.Human != null && r.PlayerId == _match.Human.Id,
                    Winner = r.Winner,
                });
            }
            if (!_results.Visible)
            {
                _hud.SuppressEliminated(true);
                CloseLoot();
                _results.SetExtraLine(_match.HumanResultDetails());
                if (AppServices.IsInitialized)
                    AppServices.Flow.TryAdvanceTo(Core.Flow.GameFlowState.ResultsScreen);
            }
            _results.Show(_rows, resolver.ParticipantCount, 0f, resolver.Winner != null && resolver.Winner.WonByTieBreak);
            _results.SetFooter("OFFLINE MATCH COMPLETE - results saved on this device only");
            UpdateCursor();
        }

        private void TogglePause()
        {
            if (_match.Phase == OfflinePhase.Ended)
                return;
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f; // offline only: the whole local simulation stops
            _pausePanel.SetActive(_paused);
            if (_match.Human != null)
                _match.Human.Router.InputEnabled = !_paused && _open == null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            UpdateCursor();
        }

        private void Leave()
        {
            Time.timeScale = 1f;
            AppServices.Flow.AbortToMainMenu();
            SceneManager.LoadScene(AppServices.Config.mainMenuScene);
        }

        private void UpdateCursor()
        {
            var human = _match.Human;
            bool capture = human != null && human.Alive && !_paused && _open == null && !(_results != null && _results.Visible)
                           && _spectating == null && !(_mobile != null && _mobile.Visible) && !Application.isMobilePlatform;
            var wanted = capture ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != wanted)
            {
                Cursor.lockState = wanted;
                Cursor.visible = !capture;
            }
        }

        private void BuildOverlay()
        {
            var canvas = UiFactory.CreateCanvas("OfflineOverlay", sortingOrder: 20);
            canvas.transform.SetParent(transform, false);
            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var mode = UiFactory.CreateText(safe, "OFFLINE  |  GHOST PROTOCOL  |  " + _match.Difficulty.ToString().ToUpperInvariant(),
                24, NyxPalette.ElectricCyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UiFactory.Anchor(mode.rectTransform, new Vector2(0.02f, 0.92f), new Vector2(0.6f, 0.99f));

            _alive = UiFactory.CreateText(safe, string.Empty, 28, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_alive.rectTransform, new Vector2(0.38f, 0.93f), new Vector2(0.62f, 0.99f));
            _alive.gameObject.AddComponent<Outline>().effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f);

            _banner = UiFactory.CreateText(safe, string.Empty, 200, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_banner.rectTransform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.8f));
            var outline = _banner.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.NyxPurple;
            outline.effectDistance = new Vector2(5f, -5f);

            var bar = UiFactory.CreatePanel("SpectateBar", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.7f), raycastTarget: true);
            UiFactory.Anchor(bar.rectTransform, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.12f));
            UiFactory.AddHorizontalLayout(bar.gameObject, 16f, new RectOffset(16, 16, 8, 8));
            _spectateLabel = UiFactory.CreateText(bar.rectTransform, string.Empty, 28, NyxPalette.ElectricCyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.AddLayoutElement(_spectateLabel.gameObject, 70f, 3f);
            var next = UiFactory.CreateButton(bar.rectTransform, "NEXT [N]", SpectateNext, 70f, 26);
            UiFactory.AddLayoutElement(next.Button.gameObject, 70f, 1f);
            var stop = UiFactory.CreateButton(bar.rectTransform, "STOP", StopSpectating, 70f, 26);
            UiFactory.AddLayoutElement(stop.Button.gameObject, 70f, 1f);
            _spectateBar = bar.gameObject;
            _spectateBar.SetActive(false);

            var menuButton = UiFactory.CreateButton(safe, "II", TogglePause, 90f, 34);
            var rect = (RectTransform)menuButton.Button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(100f, 90f);
            rect.anchoredPosition = new Vector2(-20f, -20f);

            var panel = UiFactory.CreatePanel("PauseMenu", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f), raycastTarget: true);
            UiFactory.Stretch(panel.rectTransform);
            var column = UiFactory.CreateRect("Column", panel.rectTransform);
            UiFactory.Anchor(column, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.75f));
            UiFactory.AddVerticalLayout(column.gameObject, 18f, null, TextAnchor.MiddleCenter);
            UiFactory.CreateText(column, "PAUSED", 56, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold, 80f);
            UiFactory.CreateText(column, "Offline match: time is stopped.", 22, NyxPalette.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, 40f);
            UiFactory.CreateButton(column, "RESUME", TogglePause, 100f, 36);
            UiFactory.CreateButton(column, "LEAVE MATCH", Leave, 100f, 36);
            _pausePanel = panel.gameObject;
            _pausePanel.SetActive(false);
        }
    }
}
