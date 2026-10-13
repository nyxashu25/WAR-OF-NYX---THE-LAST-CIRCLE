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
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Zone;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.Hud;
using WarOfNyx.UI.Mobile;

namespace WarOfNyx.Gameplay
{
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

            if (_pendingHeadline != null && _banner.text.Length == 0 && Zone != null)
            {
                Zone.ShowHeadline(_pendingHeadline, _pendingHeadlineColor);
                _pendingHeadline = null;
            }
            if (!_zoneShown && Zone != null)
                Zone.Hide();
            _zoneShown = false;

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
            bool capture = !_menuOpen && !_modalOpen && _localMovement != null && !(_mobile != null && _mobile.Visible) && !Application.isMobilePlatform;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

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
            UiFactory.CreateButton(column, "LEAVE MATCH", LeaveMatch, 100f, 36);
            _menuPanel.SetActive(false);
        }
    }
}
