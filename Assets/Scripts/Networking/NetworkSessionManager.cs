using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarOfNyx.App;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Networking;
using WarOfNyx.Gameplay;
using FlowMode = WarOfNyx.Core.Flow.GameMode;
using GameMode = Fusion.GameMode;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// Owns the Photon Fusion <see cref="NetworkRunner"/> and implements <see cref="IOnlineSessionService"/>.
    ///
    /// Roles:
    ///   Dedicated server  GameMode.Server  headless authority; public matches (spec 3/20).
    ///   Host              GameMode.Host    device is client + authority; PRIVATE TEST ROOMS ONLY.
    ///   Client            GameMode.Client  Quick Match (random open public session) or join by code.
    ///
    /// Match flow (Phase 4), all decided on the authority by <see cref="MatchLifecycle"/>:
    ///   session starts in the MatchLobby scene → players gather → lifecycle starts loading →
    ///   session closed to new joins, server loads BattleArena for everyone → each client reports
    ///   "loaded" → server plans spawns (SpawnPlanner) and spawns everyone, input locked →
    ///   shared start time → "FIGHT!" → everyone gains control on the same tick.
    ///   If everyone leaves, the server returns to the lobby and reopens the session.
    /// </summary>
    public sealed class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks, IOnlineSessionService
    {
        public const string PlayerPrefabResource = "WarOfNyx/NetworkPlayer";
        public const string MatchLobbyScenePath = "Assets/Scenes/MatchLobby.unity";
        public const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity";

        private readonly Dictionary<PlayerRef, NetworkObject> _avatars = new Dictionary<PlayerRef, NetworkObject>();
        private readonly Dictionary<int, PlayerRef> _playerRefs = new Dictionary<int, PlayerRef>();
        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private NetworkObject _matchControllerPrefab;
        private NetworkMatchController _matchController;
        private MatchLifecycle _lifecycle;
        private MatchRules _rules;
        private ServerLaunchOptions _serverOptions;
        private bool _pendingLoadReport;
        private int _lobbySceneIndex = -1;
        private int _arenaSceneIndex = -1;

        public OnlineSessionState State { get; private set; } = OnlineSessionState.Idle;
        public OnlineRole Role { get; private set; } = OnlineRole.None;
        public string SessionName { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public int MaxPlayers { get; private set; }

        public int PlayerCount => _runner != null && _runner.IsRunning ? _runner.ActivePlayers.Count() : 0;

        public int RoundTripMs => _runner != null && _runner.IsRunning && _runner.IsClient
            ? Mathf.RoundToInt((float)(_runner.GetPlayerRtt(_runner.LocalPlayer) * 1000.0))
            : 0;

        public IMatchStateView Match => NetworkMatchController.Instance;

        public event Action<OnlineSessionState> StateChanged;

        private bool IsAuthority => _runner != null && _runner.IsServer;
        private float Now => _runner != null ? _runner.SimulationTime : 0f;

        /// <summary>Registers the Fusion implementation before the first scene loads (the dedicated-server bootstrap needs it).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            var go = new GameObject("[WarOfNyx Online]");
            DontDestroyOnLoad(go);
            OnlineServices.Session = go.AddComponent<NetworkSessionManager>();
        }

        // ------------------------------------------------------------------ IOnlineSessionService

        public Task<bool> QuickMatchAsync() =>
            StartAsync(GameMode.Client, OnlineRole.Client, null, 0, 0, quickMatch: true);

        public Task<bool> HostPrivateRoomAsync(string roomCode, int maxPlayers) =>
            StartAsync(GameMode.Host, OnlineRole.Host, roomCode, maxPlayers, 0, quickMatch: false);

        public Task<bool> JoinAsync(string sessionName) =>
            StartAsync(GameMode.Client, OnlineRole.Client, sessionName, 0, 0, quickMatch: false);

        public Task<bool> StartDedicatedServerAsync(ServerLaunchOptions options)
        {
            _serverOptions = options;
            if (!string.IsNullOrEmpty(options.Region))
                Debug.LogWarning("[WarOfNyx][Server] -region is parsed but not applied yet; set Fixed Region in PhotonAppSettings (Phase 12).");
            return StartAsync(GameMode.Server, OnlineRole.DedicatedServer, options.SessionName, options.MaxPlayers, options.Port, quickMatch: false);
        }

        public async Task LeaveAsync()
        {
            if (_runner == null)
            {
                SetState(OnlineSessionState.Idle);
                return;
            }

            SetState(OnlineSessionState.ShuttingDown);
            await _runner.Shutdown(); // OnShutdown cleans up and returns to the menu
        }

        // ------------------------------------------------------------------ Start-up

        private async Task<bool> StartAsync(GameMode mode, OnlineRole role, string sessionName, int maxPlayers, int port, bool quickMatch)
        {
            if (State == OnlineSessionState.Starting || State == OnlineSessionState.Running)
                return Fail("Already in an online session.");

            string code = null;
            if (!quickMatch && !SessionCodes.TrySanitize(sessionName, out code))
                return Fail($"Invalid room code '{sessionName}'. Use 4-16 letters, digits or '-'.");

            bool authority = mode != GameMode.Client;
            _lobbySceneIndex = SceneUtility.GetBuildIndexByScenePath(MatchLobbyScenePath);
            _arenaSceneIndex = SceneUtility.GetBuildIndexByScenePath(BattleArenaScenePath);

            AppServices.EnsureInitialized();
            if (authority)
            {
                _playerPrefab = Resources.Load<NetworkObject>(PlayerPrefabResource);
                _matchControllerPrefab = Resources.Load<NetworkObject>(NetworkMatchController.PrefabResource);
                if (_playerPrefab == null || _matchControllerPrefab == null)
                    return Fail("Network prefabs missing. Run War of Nyx > Setup > Run Project Setup with Fusion installed.");
                if (_lobbySceneIndex < 0 || _arenaSceneIndex < 0)
                    return Fail("MatchLobby/BattleArena scenes are not in Build Settings. Run War of Nyx > Setup > Run Project Setup.");

                _rules = BuildRules(role, maxPlayers);
                _lifecycle = new MatchLifecycle(_rules, privateRoom: role == OnlineRole.Host);
                HookLifecycle(_lifecycle);
            }
            else
            {
                _lifecycle = null;
            }

            if (AppServices.Flow.Current == GameFlowState.MainMenu)
                AppServices.Flow.TryStartSession(FlowMode.OnlineBattleRoyale);

            Role = role;
            SessionName = code ?? "(searching)";
            MaxPlayers = authority ? _rules.maxPlayers : 0;
            LastError = string.Empty;
            _avatars.Clear();
            _playerRefs.Clear();
            _pendingLoadReport = false;
            SetState(OnlineSessionState.Starting);

            var runnerObject = new GameObject($"NetworkRunner ({role})");
            DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = mode != GameMode.Server;
            _runner.AddCallbacks(this);
            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            var matchProperties = new Dictionary<string, SessionProperty>
            {
                { NetworkProtocol.VersionKey, NetworkProtocol.Version },
                { NetworkProtocol.ModeKey, NetworkProtocol.ModeBattleRoyale },
            };

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = code, // null for Quick Match = join a random open session
                SceneManager = sceneManager,
            };

            if (authority)
            {
                args.PlayerCount = _rules.maxPlayers;
                args.IsOpen = true;
                // Public servers are listed for Quick Match; private rooms are only reachable by code.
                args.IsVisible = role == OnlineRole.DedicatedServer;
                args.SessionProperties = matchProperties;
                var sceneInfo = new NetworkSceneInfo();
                sceneInfo.AddSceneRef(SceneRef.FromIndex(_lobbySceneIndex), LoadSceneMode.Single, LocalPhysicsMode.None, true);
                args.Scene = sceneInfo;
            }
            else if (quickMatch)
            {
                args.SessionProperties = matchProperties; // only same-version battle royale sessions
            }

            if (mode == GameMode.Server && port > 0)
                args.Address = NetAddress.Any((ushort)port);

            Debug.Log($"[WarOfNyx][Online] Starting {role} {(quickMatch ? "quick match search" : $"session '{code}'")}...");

            StartGameResult result;
            try
            {
                result = await _runner.StartGame(args);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                CleanupRunner();
                return Fail("Could not start the session: " + ex.Message);
            }

            if (!result.Ok)
            {
                CleanupRunner();
                string reason;
                switch (result.ShutdownReason)
                {
                    case ShutdownReason.GameNotFound:
                        reason = quickMatch ? "No open match found yet." : $"Room '{code}' does not exist.";
                        break;
                    case ShutdownReason.GameClosed:
                        reason = "That match has already started (late joining is not allowed).";
                        break;
                    case ShutdownReason.GameIsFull:
                        reason = "That match is full.";
                        break;
                    case ShutdownReason.GameIdAlreadyExists:
                        reason = $"Room '{code}' already exists. Pick another code.";
                        break;
                    default:
                        reason = $"Could not {(authority ? "create" : "join")} the session: {result.ShutdownReason} {result.ErrorMessage}".Trim();
                        break;
                }
                return Fail(reason, quiet: quickMatch);
            }

            if (!authority)
            {
                MaxPlayers = _runner.SessionInfo != null ? _runner.SessionInfo.MaxPlayers : 0;
                SessionName = _runner.SessionInfo != null ? _runner.SessionInfo.Name : SessionName;
            }

            SetState(OnlineSessionState.Running);
            Debug.Log($"[WarOfNyx][Online] {role} running in session '{SessionName}' (max {MaxPlayers}).");
            return true;
        }

        private MatchRules BuildRules(OnlineRole role, int maxPlayers)
        {
            var configured = AppServices.Config.matchRules ?? new MatchRules();
            if (role == OnlineRole.Host)
                return configured.With(-1, Mathf.Clamp(maxPlayers, 1, ServerLaunchOptions.MaxPlayersLimit), -1f);

            var o = _serverOptions;
            return o == null
                ? configured.Clone()
                : configured.With(o.MinPlayersToStart, o.MaxPlayers, o.LobbyFillSeconds);
        }

        private bool Fail(string message, bool quiet = false)
        {
            LastError = message;
            if (quiet)
                Debug.Log("[WarOfNyx][Online] " + message);
            else
                Debug.LogError("[WarOfNyx][Online] " + message);
            SetState(OnlineSessionState.Failed);
            if (AppServices.IsInitialized)
                AppServices.Flow.AbortToMainMenu();
            return false;
        }

        private void SetState(OnlineSessionState next)
        {
            if (State == next)
                return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private void CleanupRunner()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                Destroy(_runner.gameObject);
            }
            _runner = null;
            _lifecycle = null;
            _matchController = null;
            Role = OnlineRole.None;
        }

        // ------------------------------------------------------------------ Server: lifecycle reactions

        private void HookLifecycle(MatchLifecycle lifecycle)
        {
            lifecycle.LoadingStarted += OnLoadingStarted;
            lifecycle.PlayersKicked += OnPlayersKicked;
            lifecycle.SpawnRequested += OnSpawnRequested;
            lifecycle.Activated += at => Debug.Log($"[WarOfNyx][Match] FIGHT! Match active at t={at:0.00}s with {lifecycle.PlayerCount} players.");
            lifecycle.ResetToLobby += OnResetToLobby;
        }

        private void OnLoadingStarted()
        {
            Debug.Log($"[WarOfNyx][Match] Starting match with {_lifecycle.PlayerCount} players: closing session, loading arena.");
            SetSessionOpen(false);
            _runner.LoadScene(SceneRef.FromIndex(_arenaSceneIndex), LoadSceneMode.Single);
        }

        private void OnPlayersKicked(IReadOnlyList<int> playerIds)
        {
            foreach (int id in playerIds)
            {
                Debug.LogWarning($"[WarOfNyx][Match] Player {id} did not load in time and was removed.");
                if (_playerRefs.TryGetValue(id, out var player))
                    _runner.Disconnect(player);
            }
        }

        private void OnSpawnRequested(IReadOnlyList<int> playerIds)
        {
            var area = ArenaSpawnArea.Current;
            if (area == null)
            {
                Debug.LogError("[WarOfNyx][Spawn] No ArenaSpawnArea in the arena scene; cannot spawn safely.");
                LastError = "The arena has no spawn area.";
                _ = LeaveAsync();
                return;
            }

            int seed = Environment.TickCount ^ (playerIds.Count * 7919);
            var plan = area.PlanSpawns(playerIds.Count, _rules, seed);
            if (!plan.Success)
            {
                // Never place players at invalid positions (spec 6).
                Debug.LogError($"[WarOfNyx][Spawn] Spawn planning failed: {plan.Failure}");
                LastError = "No valid spawn layout: " + plan.Failure;
                _ = LeaveAsync();
                return;
            }

            float protectionEndsAt = Now + _rules.countdownSeconds + _rules.spawnProtectionSeconds;
            for (int i = 0; i < playerIds.Count; i++)
            {
                if (!_playerRefs.TryGetValue(playerIds[i], out var player))
                    continue;
                var p = plan.Points[i];
                SpawnAvatar(player, new Vector3(p.X, p.Y + _rules.spawnDropHeight, p.Z), p.Yaw, protectionEndsAt,
                    loadoutSeed: unchecked(seed * 31 + playerIds[i] * 7919 + i)); // independent random loadout per player
            }

            Debug.Log($"[WarOfNyx][Spawn] Spawned {playerIds.Count} players, min separation {plan.Separation} m" +
                      (plan.Relaxed ? " (relaxed)." : "."));
        }

        private void OnResetToLobby()
        {
            Debug.Log("[WarOfNyx][Match] Returning to the lobby and reopening the session.");
            foreach (var avatar in _avatars.Values)
            {
                if (avatar != null)
                    _runner.Despawn(avatar);
            }
            _avatars.Clear();
            SetSessionOpen(true);
            if (SceneManager.GetActiveScene().buildIndex != _lobbySceneIndex)
                _runner.LoadScene(SceneRef.FromIndex(_lobbySceneIndex), LoadSceneMode.Single);
        }

        private void SetSessionOpen(bool open)
        {
            if (_runner.SessionInfo != null && _runner.SessionInfo.IsValid)
                _runner.SessionInfo.IsOpen = open;
        }

        private void SpawnAvatar(PlayerRef player, Vector3 position, float yaw, float protectionEndsAt, int loadoutSeed)
        {
            if (_avatars.ContainsKey(player))
                return;

            var spawned = _runner.Spawn(_playerPrefab, position, Quaternion.Euler(0f, yaw, 0f), player,
                (r, obj) => obj.GetComponent<NetworkPlayer>().InitializeSpawn(position, yaw, player.PlayerId, protectionEndsAt, loadoutSeed));
            if (spawned != null)
            {
                _avatars[player] = spawned;
                _runner.SetPlayerObject(player, spawned);
            }
        }

        /// <summary>Server: a player's device reported that the arena finished loading.</summary>
        public void OnPlayerLoaded(PlayerRef player)
        {
            if (_lifecycle != null && player != PlayerRef.None)
                _lifecycle.MarkLoaded(player.PlayerId, Now);
        }

        private void SpawnMatchControllerIfNeeded()
        {
            if (!IsAuthority || _matchController != null || _lifecycle == null)
                return;
            var obj = _runner.Spawn(_matchControllerPrefab);
            _matchController = obj.GetComponent<NetworkMatchController>();
            _matchController.InitializeServer(_lifecycle, this);
        }

        // ------------------------------------------------------------------ Client: load report

        private void Update()
        {
            if (_pendingLoadReport && NetworkMatchController.Instance != null)
            {
                _pendingLoadReport = false;
                NetworkMatchController.Instance.ReportLocalLoaded();
            }
        }

        // ------------------------------------------------------------------ INetworkRunnerCallbacks

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // Late joining is not allowed (spec 20): only accept while the lobby is gathering.
            if (_lifecycle != null && !_lifecycle.AcceptingPlayers)
                request.Refuse();
            else
                request.Accept();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || _lifecycle == null)
                return;

            if (!_lifecycle.TryAddPlayer(player.PlayerId, Now))
            {
                Debug.Log($"[WarOfNyx][Match] Refusing player {player.PlayerId}: match already starting or full.");
                runner.Disconnect(player);
                return;
            }

            _playerRefs[player.PlayerId] = player;
            Debug.Log($"[WarOfNyx][Match] Player {player.PlayerId} joined the lobby ({_lifecycle.PlayerCount}/{_rules.maxPlayers}).");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || _lifecycle == null)
                return;

            if (_avatars.TryGetValue(player, out var avatar))
            {
                if (avatar != null)
                    runner.Despawn(avatar);
                _avatars.Remove(player);
            }
            _playerRefs.Remove(player.PlayerId);
            _lifecycle.RemovePlayer(player.PlayerId, Now);
            Debug.Log($"[WarOfNyx][Match] Player {player.PlayerId} left ({_lifecycle.PlayerCount} remaining).");
        }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            int index = SceneManager.GetActiveScene().buildIndex;
            if (index == _lobbySceneIndex)
            {
                SpawnMatchControllerIfNeeded();
                return;
            }

            if (index == _arenaSceneIndex)
            {
                // Every peer (host included) reports once; the dedicated server has no player of its own.
                if (NetworkMatchController.Instance != null)
                    NetworkMatchController.Instance.ReportLocalLoaded();
                else
                    _pendingLoadReport = true;
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var router = NetworkPlayer.LocalRouter;
            if (router == null)
                return;
            router.ConsumeInput(out var movement, out var combat);
            input.Set(NetworkInputData.From(movement, combat));
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[WarOfNyx][Online] Runner shut down: {shutdownReason}");
            bool wasServer = Role == OnlineRole.DedicatedServer;

            _avatars.Clear();
            _playerRefs.Clear();
            _runner = null;
            _lifecycle = null;
            _matchController = null;
            _pendingLoadReport = false;
            Role = OnlineRole.None;

            if (shutdownReason != ShutdownReason.Ok && string.IsNullOrEmpty(LastError))
                LastError = "Session ended: " + shutdownReason;
            SetState(shutdownReason == ShutdownReason.Ok || State == OnlineSessionState.ShuttingDown
                ? OnlineSessionState.Idle
                : OnlineSessionState.Failed);

            if (wasServer)
            {
                Application.Quit(shutdownReason == ShutdownReason.Ok ? 0 : 3);
                return;
            }

            if (AppServices.IsInitialized)
            {
                AppServices.Flow.AbortToMainMenu();
                string menu = AppServices.Config.mainMenuScene;
                if (SceneManager.GetActiveScene().name != menu)
                    SceneManager.LoadScene(menu);
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            LastError = "Disconnected from server: " + reason;
            Debug.LogWarning("[WarOfNyx][Online] " + LastError);
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            LastError = reason == NetConnectFailedReason.ServerRefused
                ? "The match already started or is full."
                : "Connection failed: " + reason;
            Debug.LogWarning("[WarOfNyx][Online] " + LastError);
        }

        public void OnConnectedToServer(NetworkRunner runner) =>
            Debug.Log("[WarOfNyx][Online] Connected to server.");

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
