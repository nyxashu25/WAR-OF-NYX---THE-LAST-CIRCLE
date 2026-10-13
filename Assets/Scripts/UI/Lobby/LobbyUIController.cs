using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Match;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.MainMenu;

namespace WarOfNyx.UI.Lobby
{
    /// <summary>
    /// Waiting lobby (spec 20 "Lobby Requirements"), shown in the MatchLobby scene while the
    /// server gathers players: room/match id, players connected / maximum, players required,
    /// lobby countdown, loading progress, connection status, START for a private-room host, LEAVE.
    ///
    /// Client-only presentation of replicated server state. On the dedicated server it does nothing.
    /// </summary>
    public sealed class LobbyUIController : MonoBehaviour
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private Text _room;
        private Text _players;
        private Text _status;
        private Text _connection;
        private MenuButton _start;

        private void Start()
        {
            AppServices.EnsureInitialized();
            if (Application.isBatchMode)
                return;

            UiFactory.EnsureEventSystem();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
            _subscriptions.Add(AppServices.Events.Subscribe<BackRequested>(_ => Leave()));
        }

        private void OnDestroy()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            _subscriptions.Clear();
        }

        private void Update()
        {
            if (_status == null)
                return;

            var session = OnlineServices.Session;
            var match = session?.Match;

            _room.text = session != null ? $"ROOM  {session.SessionName}" : "NOT CONNECTED";
            _connection.text = session == null
                ? string.Empty
                : $"{session.Role.ToString().ToUpperInvariant()}  |  {session.State.ToString().ToUpperInvariant()}" +
                  (session.RoundTripMs > 0 ? $"  |  PING {session.RoundTripMs} ms" : string.Empty);

            if (match == null)
            {
                _players.text = session != null ? $"PLAYERS  {session.PlayerCount}" : string.Empty;
                _status.text = "WAITING FOR THE SERVER...";
                _start.Button.gameObject.SetActive(false);
                return;
            }

            _players.text = $"PLAYERS  {match.Players} / {match.MaxPlayers}" +
                            (match.IsPrivateRoom ? string.Empty : $"     NEEDED TO START  {match.RequiredPlayers}");

            _status.text = match.Phase == MatchPhase.Loading
                ? $"LOADING THE ARENA...  {match.LoadedPlayers}/{match.Players} READY"
                : MatchPresentation.LobbyStatus(match.Phase, match.Players, match.RequiredPlayers, match.MaxPlayers,
                    match.SecondsRemaining, match.IsPrivateRoom);

            _start.Button.gameObject.SetActive(match.IsPrivateRoom && session.Role == OnlineRole.Host);
            _start.Interactable = match.CanHostStart;

            // Follow the server's phase in the local flow state machine.
            AppServices.Flow.TryAdvanceTo(MatchPresentation.ToFlowState(match.Phase));
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("LobbyCanvas");
            canvas.transform.SetParent(transform, false);

            var bg = UiFactory.CreatePanel("Background", canvas.transform, NyxPalette.HudBackground);
            UiFactory.Stretch(bg.rectTransform);
            var animator = gameObject.AddComponent<MenuBackgroundAnimator>();
            animator.ReduceFlashing = AppServices.UserSettings.Data.reduceFlashing;
            animator.Build(bg.rectTransform);

            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var column = UiFactory.CreateRect("Column", safe);
            UiFactory.Anchor(column, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.92f));
            UiFactory.AddVerticalLayout(column.gameObject, 18f, null, TextAnchor.UpperLeft);

            UiFactory.CreateText(column, "WAITING LOBBY", 72, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 90f);
            _room = UiFactory.CreateText(column, string.Empty, 40, NyxPalette.ElectricCyan, TextAnchor.MiddleLeft, FontStyle.Bold, 56f);
            _players = UiFactory.CreateText(column, string.Empty, 40, NyxPalette.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold, 56f);
            _status = UiFactory.CreateText(column, string.Empty, 36, NyxPalette.VictoryGold, TextAnchor.MiddleLeft, FontStyle.Bold, 100f);
            _connection = UiFactory.CreateText(column, string.Empty, 24, NyxPalette.TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal, 40f);
            UiFactory.CreateText(column,
                "Everyone spawns at the same moment after a 3-2-1 countdown. Spawn points are picked by the server. " +
                "Bots never fill online matches.",
                24, NyxPalette.TextMuted, TextAnchor.UpperLeft, FontStyle.Italic, 70f);

            var actions = UiFactory.CreateRow("Actions", column, 100f, 20f);
            _start = UiFactory.CreateButton(actions, "START MATCH", () => OnlineServices.Session?.Match?.RequestHostStart(), 100f, 36);
            UiFactory.CreateButton(actions, "LEAVE", Leave, 100f, 36);
        }

        private async void Leave()
        {
            var session = OnlineServices.Session;
            if (session != null && session.State != OnlineSessionState.Idle)
            {
                await session.LeaveAsync(); // returns to the main menu on shutdown
                return;
            }

            AppServices.Flow.AbortToMainMenu();
            SceneManager.LoadScene(AppServices.Config.mainMenuScene);
        }
    }
}
