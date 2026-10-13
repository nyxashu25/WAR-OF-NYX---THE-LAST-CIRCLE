using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Networking;
using WarOfNyx.Core.Offline;
using WarOfNyx.Platform;
using WarOfNyx.Settings;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.MainMenu
{
    /// <summary>
    /// Main menu (spec 19 + 38.1/38.2/38.3). Builds its UI from code at startup.
    ///
    /// Purely client-side presentation. Features that do not exist yet are shown disabled
    /// or answer with an explicit "arrives in Phase N" notice — nothing pretends to work.
    ///
    /// Screens: Home, Offline setup (Ghost Protocol), Settings.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuScreen { Home, OfflineSetup, Settings, Online }

        private static readonly string[] DifficultyBlurbs =
        {
            "Basic tactics, slow reactions, rare cover use. Good for learning the circle.",
            "Moderate tactics, some cover and limited flanking.",
            "Fast, proactive bots that flank, use cover and plan the circle early.",
            "Very high tactical intelligence. Strategic flanking, excellent healing and circle planning. " +
            "Fast but humanly plausible reactions, high but imperfect aim. No wallhacks, no infinite ammo.",
        };

        private readonly Dictionary<MenuScreen, GameObject> _screens = new Dictionary<MenuScreen, GameObject>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        private MenuScreen _current;
        private Text _connectionText;
        private Text _noticeText;
        private float _noticeHideAt;
        private MenuButton _onlineButton;
        private MenuBackgroundAnimator _background;

        // Offline setup state
        private OfflineMatchSettings _offline;
        private int _maxBots;
        private readonly List<(int count, MenuButton button)> _botButtons = new List<(int, MenuButton)>();
        private readonly List<(BotDifficulty difficulty, MenuButton button)> _difficultyButtons = new List<(BotDifficulty, MenuButton)>();
        private Text _offlineSummary;
        private Text _difficultyBlurb;
        private Text _capacityNote;

        // Online state
        private InputField _nameInput;
        private InputField _roomInput;
        private Text _onlineStatus;
        private readonly List<MenuButton> _onlineActions = new List<MenuButton>();

        // Settings state
        private readonly List<(GraphicsQualityTier tier, MenuButton button)> _qualityButtons = new List<(GraphicsQualityTier, MenuButton)>();
        private readonly List<Action> _settingsRefreshers = new List<Action>();

        private void Start()
        {
            AppServices.EnsureInitialized();
            UiFactory.EnsureEventSystem();

            _maxBots = DeviceCapabilityService.MaxOfflineBots;
            _offline = AppServices.UserSettings
                .GetOfflineSettings(AppServices.Config.DefaultOfflineSettings)
                .ClampToCapacity(_maxBots);

            BuildUi();
            Show(MenuScreen.Home);

            _subscriptions.Add(AppServices.Events.Subscribe<ConnectivityChanged>(_ => RefreshConnectivity()));
            _subscriptions.Add(AppServices.Events.Subscribe<BackRequested>(_ => OnBack()));
            RefreshConnectivity();

            var session = OnlineServices.Session;
            if (session != null)
            {
                session.StateChanged += OnSessionStateChanged;
                // Back from a session that ended badly (kicked, server gone, failed join): say why.
                if (session.State == OnlineSessionState.Failed && !string.IsNullOrEmpty(session.LastError))
                    Notify(session.LastError, 7f);
            }
        }

        private void OnDestroy()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            _subscriptions.Clear();
            if (OnlineServices.Session != null)
                OnlineServices.Session.StateChanged -= OnSessionStateChanged;
        }

        private void OnSessionStateChanged(OnlineSessionState state)
        {
            if (_current == MenuScreen.Online)
                RefreshOnline();
        }

        private void Update()
        {
            if (_noticeText != null && _noticeText.enabled && Time.unscaledTime >= _noticeHideAt)
                _noticeText.enabled = false;
        }

        // ------------------------------------------------------------------ UI construction

        private void BuildUi()
        {
            var canvas = UiFactory.CreateCanvas("MainMenuCanvas");
            canvas.transform.SetParent(transform, false);

            var bg = UiFactory.CreatePanel("Background", canvas.transform, NyxPalette.HudBackground);
            UiFactory.Stretch(bg.rectTransform);

            _background = gameObject.AddComponent<MenuBackgroundAnimator>();
            _background.ReduceFlashing = AppServices.UserSettings.Data.reduceFlashing;
            _background.Build(bg.rectTransform);

            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            _screens[MenuScreen.Home] = BuildHome(safe);
            _screens[MenuScreen.OfflineSetup] = BuildOfflineSetup(safe);
            _screens[MenuScreen.Settings] = BuildSettings(safe);
            _screens[MenuScreen.Online] = BuildOnline(safe);

            _noticeText = UiFactory.CreateText(safe, string.Empty, 30, NyxPalette.VictoryGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            var noticeRect = _noticeText.rectTransform;
            noticeRect.anchorMin = new Vector2(0.05f, 0.01f);
            noticeRect.anchorMax = new Vector2(0.95f, 0.09f);
            noticeRect.offsetMin = noticeRect.offsetMax = Vector2.zero;
            _noticeText.enabled = false;
        }

        private GameObject BuildHome(RectTransform parent)
        {
            var root = UiFactory.CreateRect("HomeScreen", parent);
            UiFactory.Stretch(root);

            // Left: logo, taglines, connection status.
            var brand = UiFactory.CreateRect("Brand", root);
            UiFactory.Anchor(brand, new Vector2(0.05f, 0.12f), new Vector2(0.50f, 0.92f));
            UiFactory.AddVerticalLayout(brand.gameObject, 8f, null, TextAnchor.MiddleLeft);

            var cfg = AppServices.Config;
            var logo = UiFactory.CreateText(brand, cfg.gameTitle, 120, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 140f);
            var outline = logo.gameObject.AddComponent<Outline>();
            outline.effectColor = NyxPalette.NyxPurple;
            outline.effectDistance = new Vector2(4f, -4f);

            UiFactory.CreateText(brand, cfg.subtitle, 64, NyxPalette.Crimson, TextAnchor.MiddleLeft, FontStyle.Bold, 80f);
            UiFactory.CreateText(brand, cfg.tagline, 40, NyxPalette.ElectricCyan, TextAnchor.MiddleLeft, FontStyle.Bold, 56f);
            UiFactory.CreateText(brand, cfg.secondaryTagline, 28, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic, 44f);
            UiFactory.CreateRect("Spacer", brand).gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _connectionText = UiFactory.CreateText(brand, "Checking connection...", 28, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, 90f);
            UiFactory.CreateText(brand, $"v{Application.version}  |  Phase 2 build - Practice Arena movement only",
                22, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, 34f);

            // Right: mode buttons and secondary menu.
            var menu = UiFactory.CreateRect("Menu", root);
            UiFactory.Anchor(menu, new Vector2(0.56f, 0.10f), new Vector2(0.95f, 0.92f));
            UiFactory.AddVerticalLayout(menu.gameObject, 14f, null, TextAnchor.MiddleCenter);

            _onlineButton = UiFactory.CreateButton(menu, "ONLINE BATTLE ROYALE", OnOnlinePressed, 110f, 38);
            UiFactory.CreateButton(menu, "OFFLINE BATTLE ROYALE", () => Show(MenuScreen.OfflineSetup), 110f, 38);
            UiFactory.CreateButton(menu, "PRACTICE ARENA", OnPracticePressed, 96f, 34);

            var row = UiFactory.CreateRow("SecondaryRow", menu, 80f, 14f);
            UiFactory.CreateButton(row, "CHARACTERS", null, 80f, 26).Interactable = false;
            UiFactory.CreateButton(row, "LOADOUTS", null, 80f, 26).Interactable = false;
            UiFactory.CreateButton(row, "PROFILE", null, 80f, 26).Interactable = false;

            var row2 = UiFactory.CreateRow("SystemRow", menu, 80f, 14f);
            UiFactory.CreateButton(row2, "SETTINGS", () => Show(MenuScreen.Settings), 80f, 28);
            UiFactory.CreateButton(row2, "EXIT", Quit, 80f, 28);

            return root.gameObject;
        }

        private GameObject BuildOfflineSetup(RectTransform parent)
        {
            var root = UiFactory.CreateRect("OfflineSetupScreen", parent);
            UiFactory.Anchor(root, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.95f));
            UiFactory.AddVerticalLayout(root.gameObject, 14f, null, TextAnchor.UpperCenter);

            UiFactory.CreateText(root, "OFFLINE BATTLE ROYALE", 64, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 80f);
            UiFactory.CreateText(root, "GHOST PROTOCOL  -  1 human vs AI bots. No internet or server required.",
                28, NyxPalette.ElectricCyan, TextAnchor.MiddleLeft, FontStyle.Bold, 44f);

            UiFactory.CreateText(root, "BOTS", 30, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold, 40f);
            var botRow = UiFactory.CreateRow("BotRow", root, 90f, 14f);
            foreach (int count in OfflineMatchSettings.AllowedBotCounts)
            {
                int captured = count;
                var button = UiFactory.CreateButton(botRow, $"{count} BOTS", () => SelectBots(captured), 90f, 32);
                button.Interactable = count <= _maxBots;
                _botButtons.Add((count, button));
            }

            _capacityNote = UiFactory.CreateText(root, string.Empty, 24, NyxPalette.Crimson, TextAnchor.MiddleLeft, FontStyle.Normal, 36f);

            UiFactory.CreateText(root, "DIFFICULTY", 30, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold, 40f);
            var diffRow = UiFactory.CreateRow("DifficultyRow", root, 90f, 14f);
            foreach (var difficulty in BotDifficultyCatalog.All)
            {
                var captured = difficulty;
                var button = UiFactory.CreateButton(diffRow, difficulty.DisplayName(), () => SelectDifficulty(captured), 90f, 32);
                _difficultyButtons.Add((difficulty, button));
            }

            _difficultyBlurb = UiFactory.CreateText(root, string.Empty, 26, NyxPalette.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal, 76f);
            _offlineSummary = UiFactory.CreateText(root, string.Empty, 40, NyxPalette.VictoryGold, TextAnchor.MiddleLeft, FontStyle.Bold, 60f);

            var actions = UiFactory.CreateRow("Actions", root, 100f, 20f);
            UiFactory.CreateButton(actions, "BACK", () => Show(MenuScreen.Home), 100f, 34);
            UiFactory.CreateButton(actions, "START MATCH", OnStartOffline, 100f, 38);

            RefreshOffline();
            return root.gameObject;
        }

        private GameObject BuildSettings(RectTransform parent)
        {
            var root = UiFactory.CreateRect("SettingsScreen", parent);
            UiFactory.Anchor(root, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.95f));
            UiFactory.AddVerticalLayout(root.gameObject, 12f, null, TextAnchor.UpperCenter);

            var data = AppServices.UserSettings.Data;

            UiFactory.CreateText(root, "SETTINGS", 64, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 80f);

            UiFactory.CreateText(root, "GRAPHICS QUALITY", 28, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold, 38f);
            var qualityRow = UiFactory.CreateRow("QualityRow", root, 80f, 14f);
            foreach (GraphicsQualityTier tier in Enum.GetValues(typeof(GraphicsQualityTier)))
            {
                var captured = tier;
                int fps = GraphicsQualityService.TargetFrameRate(tier);
                var button = UiFactory.CreateButton(qualityRow, $"{tier.ToString().ToUpperInvariant()} ({fps} FPS)", () =>
                {
                    AppServices.Graphics.Apply(captured);
                    AppServices.UserSettings.Data.graphicsTier = (int)captured;
                    AppServices.UserSettings.Save();
                    RefreshSettings();
                }, 80f, 28);
                _qualityButtons.Add((tier, button));
            }

            var audioRow = UiFactory.CreateRow("AudioRow", root, 80f, 14f);
            AddStepper(audioRow, "MASTER", () => data.masterVolume, v =>
            {
                data.masterVolume = v;
                AudioListener.volume = v;
            }, 0f, 1f, 0.1f, true);
            AddStepper(audioRow, "MUSIC", () => data.musicVolume, v => data.musicVolume = v, 0f, 1f, 0.1f, true);
            AddStepper(audioRow, "SFX", () => data.sfxVolume, v => data.sfxVolume = v, 0f, 1f, 0.1f, true);
            AddStepper(audioRow, "UI", () => data.uiVolume, v => data.uiVolume = v, 0f, 1f, 0.1f, true);

            var sensRow = UiFactory.CreateRow("SensitivityRow", root, 80f, 14f);
            AddStepper(sensRow, "AIM SENS", () => data.aimSensitivity, v => data.aimSensitivity = v,
                UserSettingsService.MinSensitivity, UserSettingsService.MaxSensitivity, 0.1f, false);
            AddStepper(sensRow, "STICK SENS", () => data.joystickSensitivity, v => data.joystickSensitivity = v,
                UserSettingsService.MinSensitivity, UserSettingsService.MaxSensitivity, 0.1f, false);

            var toggleRow = UiFactory.CreateRow("ComfortRow", root, 80f, 14f);
            AddToggle(toggleRow, "REDUCE SHAKE", () => data.reduceCameraShake, v => data.reduceCameraShake = v);
            AddToggle(toggleRow, "REDUCE FLASHING", () => data.reduceFlashing, v =>
            {
                data.reduceFlashing = v;
                _background.ReduceFlashing = v;
            });
            AddToggle(toggleRow, "GYRO AIM", () => data.gyroAim, v => data.gyroAim = v);

            UiFactory.CreateText(root,
                "Comfort options only change visuals; outside-circle and camping penalties still apply. " +
                "Music/SFX/UI volume, sensitivity and gyro take effect once those systems exist (Phases 2, 10, 11).",
                22, NyxPalette.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, 64f);

            var actions = UiFactory.CreateRow("Actions", root, 90f, 20f);
            UiFactory.CreateButton(actions, "BACK", () => Show(MenuScreen.Home), 90f, 32);

            RefreshSettings();
            return root.gameObject;
        }

        /// <summary>"- LABEL 80% +" control. Touch-friendly and avoids building uGUI sliders by hand.</summary>
        private void AddStepper(RectTransform row, string label, Func<float> get, Action<float> set,
            float min, float max, float step, bool asPercent)
        {
            var group = UiFactory.CreateRect("Stepper_" + label, row);
            UiFactory.AddHorizontalLayout(group.gameObject, 4f);

            var minus = UiFactory.CreateButton(group, "-", () => Change(-step), 80f, 36);
            var valueButton = UiFactory.CreateButton(group, label, null, 80f, 24);
            valueButton.Button.interactable = false;
            valueButton.Label.color = NyxPalette.TextPrimary;
            var plus = UiFactory.CreateButton(group, "+", () => Change(step), 80f, 36);

            // Give the value label most of the width; the +/- buttons stay compact.
            UiFactory.AddLayoutElement(minus.Button.gameObject, 80f, 1f);
            UiFactory.AddLayoutElement(valueButton.Button.gameObject, 80f, 3f);
            UiFactory.AddLayoutElement(plus.Button.gameObject, 80f, 1f);

            void Refresh()
            {
                float v = get();
                valueButton.SetLabel(asPercent ? $"{label} {Mathf.RoundToInt(v * 100f)}%" : $"{label} {v:0.0}x");
            }

            void Change(float delta)
            {
                set(Mathf.Clamp(Mathf.Round((get() + delta) * 100f) / 100f, min, max));
                AppServices.UserSettings.Save();
                Refresh();
            }

            _settingsRefreshers.Add(Refresh);
        }

        private void AddToggle(RectTransform row, string label, Func<bool> get, Action<bool> set)
        {
            MenuButton button = null;
            button = UiFactory.CreateButton(row, label, () =>
            {
                set(!get());
                AppServices.UserSettings.Save();
                Refresh();
            }, 80f, 26);

            void Refresh()
            {
                bool on = get();
                button.SetLabel($"{label}: {(on ? "ON" : "OFF")}");
                button.SetSelected(on);
            }

            _settingsRefreshers.Add(Refresh);
        }

        // ------------------------------------------------------------------ Behaviour

        private void Show(MenuScreen screen)
        {
            _current = screen;
            foreach (var pair in _screens)
                pair.Value.SetActive(pair.Key == screen);

            if (screen == MenuScreen.OfflineSetup)
                RefreshOffline();
            else if (screen == MenuScreen.Online)
                RefreshOnline();
            else if (screen == MenuScreen.Settings)
                RefreshSettings();
        }

        private void OnBack()
        {
            if (_current != MenuScreen.Home)
                Show(MenuScreen.Home);
        }

        private void RefreshConnectivity()
        {
            var status = AppServices.Connectivity != null ? AppServices.Connectivity.Status : ConnectivityStatus.Unknown;
            switch (status)
            {
                case ConnectivityStatus.Online:
                    _connectionText.text = OnlineServices.IsAvailable
                        ? "INTERNET: CONNECTED\nOnline test sessions available (private rooms / dev server). Matchmaking: Phase 4."
                        : "INTERNET: CONNECTED\nOnline module not installed (needs Photon Fusion 2 SDK + App ID).";
                    _connectionText.color = NyxPalette.ElectricCyan;
                    _onlineButton.Interactable = true;
                    break;
                case ConnectivityStatus.Offline:
                    _connectionText.text = "INTERNET: NOT AVAILABLE\nOnline matchmaking disabled. Offline Battle Royale is always available.";
                    _connectionText.color = NyxPalette.Crimson;
                    _onlineButton.Interactable = false;
                    break;
                default:
                    _connectionText.text = "INTERNET: CHECKING...";
                    _connectionText.color = NyxPalette.TextMuted;
                    _onlineButton.Interactable = false;
                    break;
            }
        }

        private void OnOnlinePressed()
        {
            if (!OnlineServices.IsAvailable)
            {
                Notify(OnlineServices.UnavailableMessage, 7f);
                return;
            }
            Show(MenuScreen.Online);
        }

        // ------------------------------------------------------------------ Online (Phase 3 test sessions)

        private GameObject BuildOnline(RectTransform parent)
        {
            var root = UiFactory.CreateRect("OnlineScreen", parent);
            UiFactory.Anchor(root, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.95f));
            UiFactory.AddVerticalLayout(root.gameObject, 14f, null, TextAnchor.UpperCenter);

            UiFactory.CreateText(root, "ONLINE - TEST SESSIONS", 60, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 76f);
            UiFactory.CreateText(root,
                "Phase 3: real players on a Photon Fusion session. Public matches run on a dedicated server (JOIN DEV SERVER). " +
                "HOST PRIVATE ROOM makes this device the host - for testing with friends only. Matchmaking and lobby arrive in Phase 4.",
                24, NyxPalette.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, 70f);

            _nameInput = UiFactory.CreateInputField(root, "YOUR NAME", PlayerNames.MaxLength);
            _nameInput.text = AppServices.UserSettings.Data.playerName ?? string.Empty;
            _roomInput = UiFactory.CreateInputField(root, "ROOM CODE (empty = new code when hosting)", SessionCodes.MaxLength);

            var row = UiFactory.CreateRow("OnlineActions", root, 96f, 16f);
            _onlineActions.Add(UiFactory.CreateButton(row, "HOST PRIVATE ROOM", OnHostPressed, 96f, 30));
            _onlineActions.Add(UiFactory.CreateButton(row, "JOIN ROOM", OnJoinPressed, 96f, 30));
            _onlineActions.Add(UiFactory.CreateButton(row, $"JOIN DEV SERVER ({AppServices.Config.devServerSession})", OnJoinDevPressed, 96f, 26));

            _onlineStatus = UiFactory.CreateText(root, string.Empty, 28, NyxPalette.VictoryGold, TextAnchor.MiddleLeft, FontStyle.Bold, 80f);

            var back = UiFactory.CreateRow("Back", root, 90f, 16f);
            UiFactory.CreateButton(back, "BACK", () => Show(MenuScreen.Home), 90f, 32);

            return root.gameObject;
        }

        private void RefreshOnline()
        {
            var session = OnlineServices.Session;
            bool busy = session != null && (session.State == OnlineSessionState.Starting || session.State == OnlineSessionState.Running);
            foreach (var button in _onlineActions)
                button.Interactable = session != null && !busy;

            if (session == null)
                _onlineStatus.text = OnlineServices.UnavailableMessage;
            else if (session.State == OnlineSessionState.Starting)
                _onlineStatus.text = $"CONNECTING TO {session.SessionName}...";
            else if (!string.IsNullOrEmpty(session.LastError))
                _onlineStatus.text = session.LastError;
            else
                _onlineStatus.text = string.Empty;
        }

        private void SaveName()
        {
            // Same rule the server applies; an unusable name is stored empty so the server assigns NYX-<id>.
            string clean = PlayerNames.Sanitize(_nameInput.text, 0);
            bool unusable = clean == PlayerNames.Fallback(0);
            AppServices.UserSettings.Data.playerName = unusable ? string.Empty : clean;
            _nameInput.text = AppServices.UserSettings.Data.playerName;
            AppServices.UserSettings.Save();
        }

        private async void OnHostPressed()
        {
            SaveName();
            string code = _roomInput.text;
            if (string.IsNullOrWhiteSpace(code))
            {
                code = SessionCodes.Generate(new System.Random());
                _roomInput.text = code;
            }
            else if (!SessionCodes.TrySanitize(code, out code))
            {
                _onlineStatus.text = "Room codes use 4-16 letters, digits or '-'.";
                return;
            }

            await RunOnline(() => OnlineServices.Session.HostPrivateRoomAsync(code, AppServices.Config.privateRoomMaxPlayers));
        }

        private async void OnJoinPressed()
        {
            SaveName();
            if (!SessionCodes.TrySanitize(_roomInput.text, out string code))
            {
                _onlineStatus.text = "Enter the room code you were given (4-16 letters, digits or '-').";
                return;
            }

            await RunOnline(() => OnlineServices.Session.JoinAsync(code));
        }

        private async void OnJoinDevPressed()
        {
            SaveName();
            await RunOnline(() => OnlineServices.Session.JoinAsync(AppServices.Config.devServerSession));
        }

        private async System.Threading.Tasks.Task RunOnline(Func<System.Threading.Tasks.Task<bool>> start)
        {
            var task = start();
            RefreshOnline();
            await task;
            // On success Fusion loads the BattleArena scene and this menu is destroyed.
            if (this != null)
                RefreshOnline();
        }

        private void OnPracticePressed()
        {
            string scene = AppServices.Config.practiceArenaScene;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Notify("Practice Arena scene missing. Run War of Nyx > Setup > Run Project Setup in the Unity Editor.");
                return;
            }

            if (AppServices.Flow.TryStartSession(GameMode.PracticeArena))
                SceneManager.LoadScene(scene);
        }

        private void SelectBots(int count)
        {
            if (count > _maxBots)
                return;
            _offline = _offline.WithBotCount(count);
            AppServices.UserSettings.SetOfflineSettings(_offline);
            RefreshOffline();
        }

        private void SelectDifficulty(BotDifficulty difficulty)
        {
            _offline = _offline.WithDifficulty(difficulty);
            AppServices.UserSettings.SetOfflineSettings(_offline);
            RefreshOffline();
        }

        private void RefreshOffline()
        {
            if (_offlineSummary == null)
                return;

            foreach (var (count, button) in _botButtons)
                button.SetSelected(count == _offline.BotCount);
            foreach (var (difficulty, button) in _difficultyButtons)
                button.SetSelected(difficulty == _offline.Difficulty);

            _offlineSummary.text =
                $"PLAYERS ALIVE AT START: {_offline.TotalCompetitors}/{_offline.TotalCompetitors}  (YOU + {_offline.BotCount} BOTS)  -  {_offline.Difficulty.DisplayName()}";
            _difficultyBlurb.text = DifficultyBlurbs[(int)_offline.Difficulty];

            var requested = AppServices.UserSettings.GetOfflineSettings(AppServices.Config.DefaultOfflineSettings);
            _capacityNote.text = requested.BotCount > _maxBots
                ? $"{requested.BotCount} bots needs a higher-tier device; this device is limited to {_maxBots} bots."
                : _maxBots < OfflineMatchSettings.AllowedBotCounts[OfflineMatchSettings.AllowedBotCounts.Length - 1]
                    ? $"Larger bot counts are disabled on this device (limit {_maxBots})."
                    : string.Empty;
        }

        private void OnStartOffline()
        {
            AppServices.UserSettings.SetOfflineSettings(_offline);
            Notify($"Saved: {_offline}. Offline matches are built in the Ghost Protocol phase, after core combat - not playable yet.");
        }

        private void RefreshSettings()
        {
            var tier = AppServices.Graphics.CurrentTier;
            foreach (var (t, button) in _qualityButtons)
                button.SetSelected(t == tier);
            foreach (var refresh in _settingsRefreshers)
                refresh();
        }

        private void Notify(string message, float seconds = 5f)
        {
            _noticeText.text = message;
            _noticeText.enabled = true;
            _noticeHideAt = Time.unscaledTime + seconds;
            Debug.Log($"[WarOfNyx] {message}");
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
