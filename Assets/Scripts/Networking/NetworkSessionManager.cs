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
using FlowMode = WarOfNyx.Core.Flow.GameMode;
using GameMode = Fusion.GameMode;
using WarOfNyx.Core.Networking;
using WarOfNyx.Gameplay;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// Owns the Photon Fusion <see cref="NetworkRunner"/> and implements <see cref="IOnlineSessionService"/>
    /// for the rest of the game (Phase 3).
    ///
    /// Roles:
    ///   Dedicated server  GameMode.Server  headless authority; public matches (spec 3/20).
    ///   Host              GameMode.Host    a device is client + authority; PRIVATE TEST ROOMS ONLY.
    ///   Client            GameMode.Client  joins a server or host by session name / room code.
    ///
    /// Server-only work here: spawning/despawning player objects. Client-only work: polling the
    /// local input router once per tick (OnInput). Matchmaking, waiting lobby, late-join
    /// rejection and real spawn selection arrive in Phase 4.
    /// </summary>
    public sealed class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks, IOnlineSessionService
    {
        public const string PlayerPrefabResource = "WarOfNyx/NetworkPlayer";
        public const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity";

        private readonly Dictionary<PlayerRef, NetworkObject> _players = new Dictionary<PlayerRef, NetworkObject>();
        private readonly List<PlayerRef> _pendingSpawns = new List<PlayerRef>();
        private NetworkRunner _runner;
        private NetworkObject _playerPrefab;
        private bool _sceneReady;
        private int _nextSpawnSlot;

        public OnlineSessionState State { get; private set; } = OnlineSessionState.Idle;
        public OnlineRole Role { get; private set; } = OnlineRole.None;
        public string SessionName { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public int MaxPlayers { get; private set; }

        public int PlayerCount => _runner != null && _runner.IsRunning ? _runner.ActivePlayers.Count() : 0;

        public int RoundTripMs => _runner != null && _runner.IsRunning && _runner.IsClient
            ? Mathf.RoundToInt((float)(_runner.GetPlayerRtt(_runner.LocalPlayer) * 1000.0))
            : 0;

        public event Action<OnlineSessionState> StateChanged;

        /// <summary>Registers the Fusion implementation before the first scene loads (so the dedicated server bootstrap finds it).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            var go = new GameObject("[WarOfNyx Online]");
            DontDestroyOnLoad(go);
            OnlineServices.Session = go.AddComponent<NetworkSessionManager>();
        }

        // ------------------------------------------------------------------ IOnlineSessionService

        public Task<bool> HostPrivateRoomAsync(string roomCode, int maxPlayers) =>
            StartAsync(GameMode.Host, OnlineRole.Host, roomCode, maxPlayers, 0);

        public Task<bool> JoinAsync(string sessionName) =>
            StartAsync(GameMode.Client, OnlineRole.Client, sessionName, 0, 0);

        public Task<bool> StartDedicatedServerAsync(ServerLaunchOptions options)
        {
            if (!string.IsNullOrEmpty(options.Region))
                Debug.LogWarning("[WarOfNyx][Server] -region is parsed but not applied yet; set Fixed Region in PhotonAppSettings (Phase 12).");
            return StartAsync(GameMode.Server, OnlineRole.DedicatedServer, options.SessionName, options.MaxPlayers, options.Port);
        }

        public async Task LeaveAsync()
        {
            if (_runner == null)
            {
                SetState(OnlineSessionState.Idle);
                return;
            }

            SetState(OnlineSessionState.ShuttingDown);
            await _runner.Shutdown(); // OnShutdown does the cleanup and the scene change
        }

        // ------------------------------------------------------------------ Start-up

        private async Task<bool> StartAsync(GameMode mode, OnlineRole role, string sessionName, int maxPlayers, int port)
        {
            if (State == OnlineSessionState.Starting || State == OnlineSessionState.Running)
                return Fail("Already in an online session.");

            if (!SessionCodes.TrySanitize(sessionName, out string code))
                return Fail($"Invalid room code '{sessionName}'. Use 4-16 letters, digits or '-'.");

            bool authority = mode != GameMode.Client;
            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(BattleArenaScenePath);
            if (authority)
            {
                _playerPrefab = Resources.Load<NetworkObject>(PlayerPrefabResource);
                if (_playerPrefab == null)
                    return Fail("Network player prefab missing. Run War of Nyx > Setup > Run Project Setup with Fusion installed.");
                if (sceneIndex < 0)
                    return Fail("BattleArena scene is not in Build Settings. Run War of Nyx > Setup > Run Project Setup.");
            }

            AppServices.EnsureInitialized();
            if (AppServices.Flow.Current == GameFlowState.MainMenu)
                AppServices.Flow.TryStartSession(FlowMode.OnlineBattleRoyale);

            Role = role;
            SessionName = code;
            MaxPlayers = authority ? Mathf.Clamp(maxPlayers, ServerLaunchOptions.MinPlayers, ServerLaunchOptions.MaxPlayersLimit) : 0;
            LastError = string.Empty;
            _players.Clear();
            _pendingSpawns.Clear();
            _nextSpawnSlot = 0;
            _sceneReady = false;
            SetState(OnlineSessionState.Starting);

            var runnerObject = new GameObject($"NetworkRunner ({role})");
            DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = mode != GameMode.Server;
            _runner.AddCallbacks(this);
            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = code,
                SceneManager = sceneManager,
            };

            if (authority)
            {
                args.PlayerCount = MaxPlayers;
                var sceneInfo = new NetworkSceneInfo();
                sceneInfo.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single, LocalPhysicsMode.None, true);
                args.Scene = sceneInfo;
            }

            if (mode == GameMode.Server && port > 0)
                args.Address = NetAddress.Any((ushort)port);

            Debug.Log($"[WarOfNyx][Online] Starting {role} session '{code}'...");

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
                return Fail($"Could not {(authority ? "create" : "join")} '{code}': {result.ShutdownReason} {result.ErrorMessage}".Trim());
            }

            if (!authority)
                MaxPlayers = _runner.SessionInfo != null ? _runner.SessionInfo.MaxPlayers : 0;

            SetState(OnlineSessionState.Running);
            AdvanceFlowToActive();
            Debug.Log($"[WarOfNyx][Online] {role} running in session '{code}' (max {MaxPlayers}).");
            return true;
        }

        private static void AdvanceFlowToActive()
        {
            // Phase 3 has no lobby or countdown yet: walk straight to the active state.
            var flow = AppServices.Flow;
            flow.TryTransition(GameFlowState.WaitingLobby);
            flow.TryTransition(GameFlowState.MatchPreparation);
            flow.TryTransition(GameFlowState.SynchronizedSpawn);
            flow.TryTransition(GameFlowState.Countdown);
            flow.TryTransition(GameFlowState.ActiveMatch);
        }

        private bool Fail(string message)
        {
            LastError = message;
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
                if (_runner.gameObject != null)
                    Destroy(_runner.gameObject);
            }
            _runner = null;
            Role = OnlineRole.None;
        }

        // ------------------------------------------------------------------ Server: players

        private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
        {
            if (_players.ContainsKey(player))
                return;

            Vector3 position = Vector3.up;
            float yaw = 0f;
            var area = ArenaSpawnArea.Current;
            if (area != null)
                area.GetSpawn(_nextSpawnSlot++, out position, out yaw);
            else
                Debug.LogWarning("[WarOfNyx][Online] No ArenaSpawnArea in the scene; spawning at the origin.");

            var spawned = runner.Spawn(_playerPrefab, position, Quaternion.Euler(0f, yaw, 0f), player,
                (r, obj) => obj.GetComponent<NetworkPlayer>().InitializeSpawn(position, yaw, player.PlayerId));

            if (spawned != null)
            {
                _players[player] = spawned;
                runner.SetPlayerObject(player, spawned);
                Debug.Log($"[WarOfNyx][Online] Spawned player {player.PlayerId} at {position}.");
            }
        }

        // ------------------------------------------------------------------ INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            Debug.Log($"[WarOfNyx][Online] Player {player.PlayerId} joined ({PlayerCount}/{MaxPlayers}).");
            if (_sceneReady)
                SpawnPlayer(runner, player);
            else
                _pendingSpawns.Add(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            _pendingSpawns.Remove(player);
            if (_players.TryGetValue(player, out var obj))
            {
                if (obj != null)
                    runner.Despawn(obj);
                _players.Remove(player);
            }
            Debug.Log($"[WarOfNyx][Online] Player {player.PlayerId} left.");
        }

        public void OnSceneLoadStart(NetworkRunner runner) => _sceneReady = false;

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            _sceneReady = true;
            if (!runner.IsServer)
                return;

            foreach (var player in _pendingSpawns.ToArray())
                SpawnPlayer(runner, player);
            _pendingSpawns.Clear();
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var router = NetworkPlayer.LocalRouter;
            if (router == null)
                return;
            input.Set(NetworkInputData.From(router.ConsumeCommand()));
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[WarOfNyx][Online] Runner shut down: {shutdownReason}");
            bool wasServer = Role == OnlineRole.DedicatedServer;

            _players.Clear();
            _pendingSpawns.Clear();
            _runner = null;
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
            LastError = "Connection failed: " + reason;
            Debug.LogWarning("[WarOfNyx][Online] " + LastError);
        }

        public void OnConnectedToServer(NetworkRunner runner) =>
            Debug.Log("[WarOfNyx][Online] Connected to server.");

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
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
