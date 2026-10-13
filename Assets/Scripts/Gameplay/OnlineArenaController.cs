using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Zone;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.Hud;
using WarOfNyx.UI.Mobile;
using WarOfNyx.Settings;

namespace WarOfNyx.Gameplay
{
    /// <summary>A living player an eliminated player can watch (spec 21).</summary>
    public struct SpectateTarget
    {
        public Transform Transform;
        public PlayerMovement Movement;
        public string Name;
    }

    /// <summary>
    /// Client-side presentation for the networked BattleArena scene (Phase 3 test map):
    /// camera, touch controls, connection HUD, and the leave-match menu for the LOCAL player.
    ///
    /// It owns no gameplay. The local player's networked object (spawned by the server)
    /// calls <see cref="BindLocalPlayer"/> when it appears. Unlike practice, pausing never stops
    /// time: the match keeps running on the server, so the menu only disables local input.
    /// On the dedicated server this component does nothing.
    /// </summary>
    public sealed class OnlineArenaController : MonoBehaviour
    {
        public static OnlineArenaController Instance { get; private set; }

        /// <summary>Combat HUD for the local player (null on the dedicated server).</summary>
        public CombatHud Hud { get; private set; }

        /// <summary>Loot capsule panel for the local player (null on the dedicated server).</summary>
        public LootPanel Loot { get; private set; }

        /// <summary>Nyx Circle HUD and minimap for the local player (null on the dedicated server).</summary>
        public ZoneHud Zone { get; private set; }

        /// <summary>Living players to spectate, provided by the local networked player (Phase 9).</summary>
        public Func<IReadOnlyList<SpectateTarget>> SpectateTargets { get; set; }

        private MatchResultsPanel _results;
        private bool _historySaved;
        private Text _alive;
        private GameObject _spectateBar;
        private Text _spectateLabel;
        private Transform _spectating;
        private int _spectateIndex = -1;
        private bool _eliminatedPanelWasUp;

        /// <summary>Nyx Hunt status and exposed-player indicators (null on the dedicated server).</summary>
        public HuntHud HuntView { get; private set; }

        private readonly HuntStatusAnnouncer _huntAnnouncer = new HuntStatusAnnouncer();
        private bool _huntShown;
        private readonly ZoneAnnouncer _zoneAnnouncer = new ZoneAnnouncer();
        private string _pendingHeadline;
        private Color _pendingHeadlineColor;
        private bool _zoneShown;

        private bool _modalOpen;

        public event Action ModalCloseRequested;

        public bool MenuOpen => _menuOpen;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private PlayerMovement _localMovement;
        private PlayerInputRouter _localRouter;
        private MobileControlsUI _mobile;
        private ThirdPersonCamera _camera;
        private GameObject _menuPanel;
        private Text _status;
        private Text _stats;
        private Text _banner;
        private Text _phase;
        private Text _protection;
        private float _protectionRemaining;
        private bool _menuOpen;

        private static bool IsHeadless => Application.isBatchMode ||
                                          (OnlineServices.Session != null && OnlineServices.Session.Role == OnlineRole.DedicatedServer);

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            foreach (var s in _subscriptions)
                s.Dispose();
            _subscriptions.Clear();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Start()
        {
            AppServices.EnsureInitialized();
            if (IsHeadless)
                return;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            BuildHud();
            Hud = CombatHud.Create(transform);
            Loot = LootPanel.Create(transform);
            Zone = ZoneHud.Create(transform);
            HuntView = HuntHud.Create(transform);
            _results = MatchResultsPanel.Create(transform);
            _results.LeaveClicked += LeaveMatch;
            Hud.SpectateClicked += SpectateNext;
            Hud.LeaveClicked += LeaveMatch;
            var area = ArenaSpawnArea.Current;
            if (area != null)
                Zone.Minimap.BakeMap(area.transform.position, area.ZoneRadius * 1.15f);
            _subscriptions.Add(AppServices.Events.Subscribe<BackRequested>(_ =>
            {
                if (_modalOpen)
                    ModalCloseRequested?.Invoke(); // Esc / Android back closes the loot panel first
                else
                    ToggleMenu();
            }));

            if (OnlineServices.Session == null || OnlineServices.Session.State != OnlineSessionState.Running)
                _status.text = "NOT CONNECTED - open this scene from the main menu (ONLINE).";
        }

        /// <summary>Called by the networking layer when this device's player object spawns.</summary>
        public void BindLocalPlayer(PlayerMovement movement, PlayerInputRouter router)
        {
            if (IsHeadless)
                return;

            _localMovement = movement;
            _localRouter = router;

            var cam = Camera.main;
            if (cam != null)
            {
                _camera = cam.GetComponent<ThirdPersonCamera>();
                if (_camera == null)
                    _camera = cam.gameObject.AddComponent<ThirdPersonCamera>();
                _camera.Bind(movement.transform, movement, router);
            }

            if (_mobile == null)
                _mobile = MobileControlsUI.Create(router);
            _mobile.SetVisible(Application.isMobilePlatform || Touchscreen.current != null);
            UpdateCursor();
        }

        public void UnbindLocalPlayer(PlayerMovement movement)
        {
            if (_localMovement != movement)
                return;
            _localMovement = null;
            _localRouter = null;
            if (_camera != null)
                _camera.SetShake(0f);
            if (_mobile != null)
            {
                _mobile.SetFireBlocked(false);
                _mobile.SetVisible(false);
            }
            UpdateCursor();
        }

        private void Update()
        {
            if (IsHeadless || _status == null)
                return;

            var session = OnlineServices.Session;
            if (session != null)
            {
                string role = session.Role == OnlineRole.Host ? "HOST (private test room)" : session.Role.ToString().ToUpperInvariant();
                _status.text = $"ONLINE  |  {role}  |  ROOM {session.SessionName}  |  PLAYERS {session.PlayerCount}/{session.MaxPlayers}" +
                               (session.RoundTripMs > 0 ? $"  |  PING {session.RoundTripMs} ms" : string.Empty) +
                               (session.State != OnlineSessionState.Running ? $"  |  {session.State.ToString().ToUpperInvariant()}" : string.Empty);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame && _mobile != null)
                {
                    _mobile.SetVisible(!_mobile.Visible);
                    UpdateCursor();
                }
                if (keyboard.f3Key.wasPressedThisFrame)
                    _stats.gameObject.SetActive(!_stats.gameObject.activeSelf);
            }

            var mouse = Mouse.current;
            if (!_menuOpen && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                UpdateCursor();

            UpdateMatchOverlay(session);
            UpdateSpectating();
            bool eliminatedUp = IsLocalEliminatedPanelUp();
            if (eliminatedUp != _eliminatedPanelWasUp)
            {
                _eliminatedPanelWasUp = eliminatedUp;
                UpdateCursor(); // free the mouse for SPECTATE / RETURN TO LOBBY
            }

            if (_pendingHeadline != null && _banner.text.Length == 0 && Zone != null)
            {
                Zone.ShowHeadline(_pendingHeadline, _pendingHeadlineColor);
                _pendingHeadline = null;
            }
            if (!_zoneShown && Zone != null)
                Zone.Hide();
            _zoneShown = false;
            if (!_huntShown && HuntView != null)
            {
                HuntView.Hide();
                Zone?.Minimap.SetHuntMarkers(null);
            }
            _huntShown = false;

            if (_stats.gameObject.activeSelf && _localMovement != null)
            {
                MovementState s = _localMovement.State;
                var p = _localMovement.transform.position;
                _stats.text = $"STATE {s.Mode}{(s.IsSprinting ? " (SPRINT)" : string.Empty)}\nSPEED {s.HorizontalSpeed:0.00} m/s\n" +
                              $"POS {p.x:0.0}, {p.y:0.0}, {p.z:0.0}";
            }
        }

        /// <summary>
        /// Called every frame by the local networked player with the circle state (spec 10-12):
        /// updates the zone HUD and minimap, announcements, camera tremble, and the disabled fire button.
        /// </summary>
        public void ShowZone(in ZoneView view, Vector3 playerPosition, float yaw)
        {
            if (IsHeadless || Zone == null)
                return;

            _zoneShown = true;
            Zone.Apply(view, playerPosition, yaw);

            foreach (var a in _zoneAnnouncer.Update(view))
            {
                if (!a.Headline)
                {
                    Hud?.AddFeed(a.Text, a.Kind == ZoneAnnouncementKind.FinalWarning);
                    continue;
                }
                var color = a.Kind == ZoneAnnouncementKind.LeftSafeZone ? NyxPalette.Crimson
                    : a.Kind == ZoneAnnouncementKind.EnteredSafeZone ? NyxPalette.ElectricCyan
                    : a.Kind == ZoneAnnouncementKind.PhaseStarted ? NyxPalette.NyxPurple
                    : NyxPalette.VictoryGold;
                // Never on top of the "3, 2, 1, FIGHT!" banner: hold it until the banner clears.
                if (_banner != null && _banner.text.Length > 0)
                {
                    _pendingHeadline = a.Text;
                    _pendingHeadlineColor = color;
                }
                else
                {
                    Zone.ShowHeadline(a.Text, color);
                }
            }

            bool outside = view.Active && view.PlayerAlive && view.PlayerOutside;
            if (_camera != null)
                _camera.SetShake(outside ? (ReduceShake ? 0.2f : 1f) : 0f);
            if (_mobile != null)
                _mobile.SetFireBlocked(outside);
            Hud?.SetFirearmsBlocked(outside);
        }

        /// <summary>
        /// Called every frame by the local networked player (spec 13): own camping status and notices,
        /// plus every exposed player for the minimap and direction indicators.
        /// </summary>
        public void ShowHunt(in HuntStatus status, bool alive, float now, Vector3 playerPosition, IReadOnlyList<HuntTarget> exposed)
        {
            if (IsHeadless || HuntView == null)
                return;
            _huntShown = true;
            var settings = AppServices.IsInitialized && AppServices.Config.hunt != null ? AppServices.Config.hunt : DefaultHunt;
            HuntView.Apply(status, alive, now, settings, playerPosition, exposed);
            Zone?.Minimap.SetHuntMarkers(exposed);

            string headline = _huntAnnouncer.Update(status, alive);
            if (headline != null && Zone != null)
            {
                var color = headline == HuntText.Cleared ? NyxPalette.ElectricCyan : NyxPalette.Crimson;
                Zone.ShowHeadline(headline, color);
            }
        }

        private static readonly HuntSettings DefaultHunt = new HuntSettings();

        private static bool ReduceShake => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceCameraShake;

        /// <summary>Called every frame by the local networked player with its replicated protection time.</summary>
        public void ShowSpawnProtection(float secondsRemaining) => _protectionRemaining = secondsRemaining;

        private void UpdateMatchOverlay(IOnlineSessionService session)
        {
            var match = session?.Match;
            if (match == null)
            {
                _banner.text = string.Empty;
                _phase.text = string.Empty;
                _protection.text = string.Empty;
                return;
            }

            AppServices.Flow.TryAdvanceTo(MatchPresentation.ToFlowState(match.Phase));

            switch (match.Phase)
            {
                case MatchPhase.Loading:
                    _banner.text = string.Empty;
                    _phase.text = $"WAITING FOR ALL PLAYERS TO LOAD  {match.LoadedPlayers}/{match.Players}";
                    break;
                case MatchPhase.Ended:
                    _banner.text = string.Empty;
                    _phase.text = string.Empty;
                    break;
                case MatchPhase.Countdown:
                case MatchPhase.Active:
                    // Same server start time on every device: "3", "2", "1", "FIGHT!".
                    _banner.text = MatchPresentation.CountdownText(-match.SecondsSinceStart);
                    _banner.color = _banner.text == "FIGHT!" ? NyxPalette.Crimson : NyxPalette.TextPrimary;
                    _phase.text = match.Phase == MatchPhase.Countdown ? "GET READY" : string.Empty;
                    break;
                default:
                    _banner.text = string.Empty;
                    _phase.text = string.Empty;
                    break;
            }

            _protection.text = _protectionRemaining > 0f ? $"SPAWN PROTECTION  {_protectionRemaining:0.0}s" : string.Empty;
            _alive.text = match.Phase == MatchPhase.Active && match.Participants > 0
                ? $"ALIVE  {match.AlivePlayers} / {match.Participants}"
                : string.Empty;
        }

        // ------------------------------------------------------------------ Phase 9: headlines, spectating, results

        /// <summary>Big centre notice (FIRST BLOOD, DOUBLE ELIMINATION...).</summary>
        public void ShowHeadline(string text, Color color)
        {
            if (Zone != null)
                Zone.ShowHeadline(text, color);
        }

        /// <summary>Watch the next living player (spec 21). The spectator sees only what that player's camera shows.</summary>
        public void SpectateNext()
        {
            var targets = SpectateTargets != null ? SpectateTargets() : null;
            if (targets == null || targets.Count == 0)
            {
                Hud?.ShowMessage("Nobody left to spectate.");
                StopSpectating();
                return;
            }
            _spectateIndex = (_spectateIndex + 1) % targets.Count;
            var target = targets[_spectateIndex];
            if (_camera == null && Camera.main != null)
                _camera = Camera.main.GetComponent<ThirdPersonCamera>();
            if (_camera == null)
                return;
            _camera.Bind(target.Transform, target.Movement, null);
            _spectating = target.Transform;
            _spectateLabel.text = $"SPECTATING  {target.Name}";
            _spectateBar.SetActive(true);
            Hud?.SuppressEliminated(true);
        }

        public void StopSpectating()
        {
            if (_spectating == null && (_spectateBar == null || !_spectateBar.activeSelf))
                return;
            _spectating = null;
            _spectateIndex = -1;
            if (_spectateBar != null)
                _spectateBar.SetActive(false);
            if (_camera != null && _localMovement != null)
                _camera.Bind(_localMovement.transform, _localMovement, _localRouter);
            Hud?.SuppressEliminated(_results != null && _results.Visible);
        }

        private void UpdateSpectating()
        {
            if (_spectating == null)
                return;
            var targets = SpectateTargets != null ? SpectateTargets() : null;
            bool stillAlive = false;
            if (targets != null)
                foreach (var t in targets)
                    stillAlive |= t.Transform == _spectating;
            if (!stillAlive)
            {
                // The watched player fell: move on to someone else (or back to your own view).
                _spectateIndex = -1;
                if (targets != null && targets.Count > 0)
                    SpectateNext();
                else
                    StopSpectating();
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.nKey.wasPressedThisFrame)
                SpectateNext();
        }

        /// <summary>Called every frame by the match controller once the server has declared the result.</summary>
        public void ShowResults(IReadOnlyList<ResultRow> rows, int participants, float secondsToLobby, bool wonByTieBreak,
            Transform winner = null)
        {
            if (IsHeadless || _results == null)
                return;

            // Victory presentation (Phase 10): orbit the winner for a few seconds before the table.
            if (_resultsRequestedAt < 0f)
                _resultsRequestedAt = Time.unscaledTime;
            float t = Time.unscaledTime - _resultsRequestedAt;
            if (winner != null && t < VictoryCameraSeconds && Camera.main != null)
            {
                StopSpectating();
                Hud?.SuppressEliminated(true);
                if (_camera != null)
                    _camera.enabled = false;
                var cam = Camera.main.transform;
                var focus = winner.position + Vector3.up * 1.3f;
                cam.position = focus + Quaternion.Euler(0f, t * 40f, 0f) * new Vector3(0f, 1.2f, -5.5f);
                cam.LookAt(focus);
                return;
            }
            if (!_results.Visible)
            {
                StopSpectating();
                Hud?.SuppressEliminated(true);
                AppServices.Flow.TryAdvanceTo(GameFlowState.ResultsScreen);
            }
            _results.Show(rows, participants, secondsToLobby, wonByTieBreak);
            UpdateCursor();

            if (!_historySaved)
            {
                foreach (var r in rows)
                {
                    if (!r.IsLocal || r.Placement <= 0)
                        continue;
                    _historySaved = true;
                    MatchHistoryStore.Add(new WarOfNyx.Core.Match.MatchHistoryEntry
                    {
                        mode = "online",
                        unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        placement = r.Placement,
                        participants = participants,
                        kills = r.Kills,
                        damage = r.Damage,
                        survivalSeconds = r.SurvivalSeconds,
                        won = r.Winner,
                    });
                    Debug.Log($"[WarOfNyx] Match saved to local history: #{r.Placement} of {participants}, {r.Kills} kills.");
                }
            }
        }

        private const float VictoryCameraSeconds = 3.5f;
        private float _resultsRequestedAt = -1f;

        public void HideResults()
        {
            _resultsRequestedAt = -1f;
            if (_camera != null && !_camera.enabled)
                _camera.enabled = true;
            if (_results == null || !_results.Visible)
                return;
            _results.Hide();
            _historySaved = false;
            Hud?.SuppressEliminated(false);
            UpdateCursor();
        }

        /// <summary>
        /// A modal panel (loot) is open: free the cursor and pause local input (the match keeps running).
        /// </summary>
        public void SetModalOpen(bool open)
        {
            if (_modalOpen == open)
                return;
            _modalOpen = open;
            if (_localRouter != null)
                _localRouter.InputEnabled = !open && !_menuOpen;
            UpdateCursor();
        }

        private void ToggleMenu()
        {
            _menuOpen = !_menuOpen;
            _menuPanel.SetActive(_menuOpen);
            if (_localRouter != null)
                _localRouter.InputEnabled = !_menuOpen && !_modalOpen; // neutral input; the match keeps running
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            UpdateCursor();
        }

        /// <summary>Opens the touch layout editor (Phase 11). Local input stays off until DONE; the match keeps running.</summary>
        private void EditControls()
        {
            if (_mobile == null || _localRouter == null)
                return;
            if (_menuOpen)
                ToggleMenu();
            _localRouter.InputEnabled = false;
            _mobile.SetVisible(true);
            _mobile.EditFinished -= OnEditFinished;
            _mobile.EditFinished += OnEditFinished;
            _mobile.SetEditMode(true);
            UpdateCursor();
        }

        private void OnEditFinished()
        {
            if (_localRouter != null)
                _localRouter.InputEnabled = !_menuOpen && !_modalOpen;
            _mobile.SetVisible(Application.isMobilePlatform || Touchscreen.current != null);
            UpdateCursor();
        }

        private async void LeaveMatch()
        {
            _menuPanel.SetActive(false);
            var session = OnlineServices.Session;
            if (session != null && session.State != OnlineSessionState.Idle)
            {
                await session.LeaveAsync(); // the session layer returns to the menu on shutdown
                return;
            }

            AppServices.Flow.AbortToMainMenu();
            SceneManager.LoadScene(AppServices.Config.mainMenuScene);
        }

        private void UpdateCursor()
        {
            bool capture = !_menuOpen && !_modalOpen && !(_mobile != null && _mobile.Editing) && !(_results != null && _results.Visible) && !IsLocalEliminatedPanelUp()
                           && _localMovement != null && !(_mobile != null && _mobile.Visible) && !Application.isMobilePlatform;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        /// <summary>The elimination panel has buttons: free the cursor while it shows (desktop).</summary>
        private bool IsLocalEliminatedPanelUp() => Hud != null && Hud.EliminatedVisible;

        private void BuildHud()
        {
            var canvas = UiFactory.CreateCanvas("OnlineHud", sortingOrder: 20);
            canvas.transform.SetParent(transform, false);
            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _status = UiFactory.CreateText(safe, "CONNECTING...", 24, NyxPalette.ElectricCyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UiFactory.Anchor(_status.rectTransform, new Vector2(0.02f, 0.92f), new Vector2(0.8f, 0.99f));

            _stats = UiFactory.CreateText(safe, string.Empty, 22, NyxPalette.TextPrimary, TextAnchor.UpperLeft);
            UiFactory.Anchor(_stats.rectTransform, new Vector2(0.02f, 0.74f), new Vector2(0.4f, 0.91f));
            _stats.gameObject.SetActive(false);

            _banner = UiFactory.CreateText(safe, string.Empty, 200, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_banner.rectTransform, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.8f));
            var outline = _banner.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.NyxPurple;
            outline.effectDistance = new Vector2(5f, -5f);

            _phase = UiFactory.CreateText(safe, string.Empty, 32, NyxPalette.VictoryGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_phase.rectTransform, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.4f));

            _protection = UiFactory.CreateText(safe, string.Empty, 26, NyxPalette.ElectricCyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_protection.rectTransform, new Vector2(0.3f, 0.82f), new Vector2(0.7f, 0.88f));

            _alive = UiFactory.CreateText(safe, string.Empty, 28, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Anchor(_alive.rectTransform, new Vector2(0.4f, 0.93f), new Vector2(0.6f, 0.99f));
            _alive.gameObject.AddComponent<Outline>().effectColor = NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f);

            // Spectator bar (spec 21).
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

            var menuButton = UiFactory.CreateButton(safe, "II", ToggleMenu, 90f, 34);
            var rect = (RectTransform)menuButton.Button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(100f, 90f);
            rect.anchoredPosition = new Vector2(-20f, -20f);

            var panel = UiFactory.CreatePanel("MatchMenu", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f), raycastTarget: true);
            UiFactory.Stretch(panel.rectTransform);
            _menuPanel = panel.gameObject;

            var column = UiFactory.CreateRect("Column", panel.rectTransform);
            UiFactory.Anchor(column, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.75f));
            UiFactory.AddVerticalLayout(column.gameObject, 18f, null, TextAnchor.MiddleCenter);
            UiFactory.CreateText(column, "MATCH MENU", 56, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold, 80f);
            UiFactory.CreateText(column, "The match keeps running while this menu is open.", 22, NyxPalette.TextMuted,
                TextAnchor.MiddleCenter, FontStyle.Normal, 40f);
            UiFactory.CreateButton(column, "RESUME", ToggleMenu, 100f, 36);
            UiFactory.CreateButton(column, "EDIT TOUCH CONTROLS", EditControls, 100f, 30);
            UiFactory.CreateButton(column, "LEAVE MATCH", LeaveMatch, 100f, 36);
            _menuPanel.SetActive(false);
        }
    }
}
