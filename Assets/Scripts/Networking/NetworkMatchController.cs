using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Match;
using WarOfNyx.Gameplay;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// The match's networked brain (Phase 4). One per session, spawned by the server in the lobby
    /// and kept across the lobby → arena scene change (DontDestroyOnLoad).
    ///
    ///   SERVER: owns the <see cref="MatchLifecycle"/>, advances it with the tick clock and
    ///           publishes phase, counts, deadlines and the shared start time.
    ///   CLIENTS: read the replicated values for UI and for the synchronized input unlock.
    ///
    /// Synchronized start: the start time is a server simulation time. Every peer unlocks input on
    /// the first tick whose simulation time reaches it, so the server and every predicting client
    /// agree on the exact tick, however slowly a device loaded.
    /// </summary>
    public sealed class NetworkMatchController : NetworkBehaviour, IMatchStateView
    {
        public const string PrefabResource = "WarOfNyx/MatchController";

        public static NetworkMatchController Instance { get; private set; }

        [Networked] private byte NetPhase { get; set; }
        [Networked] private int NetPlayers { get; set; }
        [Networked] private int NetRequired { get; set; }
        [Networked] private int NetMax { get; set; }
        [Networked] private int NetLoaded { get; set; }
        [Networked] private NetworkBool NetPrivate { get; set; }
        [Networked] private float NetPhaseEndsAt { get; set; }
        /// <summary>"FIGHT!" time in server simulation seconds; 0 until a countdown starts.</summary>
        [Networked] private float NetStartTime { get; set; }

        public float StartTime => NetStartTime;

        private MatchLifecycle _lifecycle;
        private NetworkSessionManager _session;

        // ------------------------------------------------------------------ server

        /// <summary>Server only: attach the lifecycle right after spawning.</summary>
        public void InitializeServer(MatchLifecycle lifecycle, NetworkSessionManager session)
        {
            _lifecycle = lifecycle;
            _session = session;
            _lifecycle.CountdownStarted += endsAt => NetStartTime = endsAt;
            _lifecycle.ResetToLobby += () => NetStartTime = 0f;
            Publish();
        }

        public override void Spawned()
        {
            Instance = this;
            Runner.MakeDontDestroyOnLoad(gameObject);
            gameObject.name = "MatchController (networked)";
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _lifecycle == null)
                return;
            _lifecycle.Update(Runner.SimulationTime);
            Publish();
        }

        private void Publish()
        {
            NetPhase = (byte)_lifecycle.Phase;
            NetPlayers = _lifecycle.PlayerCount;
            NetLoaded = _lifecycle.LoadedCount;
            NetRequired = _lifecycle.IsPrivateRoom ? _lifecycle.Rules.privateRoomMinPlayersToStart : _lifecycle.Rules.minPlayersToStart;
            NetMax = _lifecycle.Rules.maxPlayers;
            NetPrivate = _lifecycle.IsPrivateRoom;
            NetPhaseEndsAt = _lifecycle.PhaseEndsAt;
        }

        /// <summary>A client finished loading the arena. Sources: any client; executed on the server only.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReportLoaded(RpcInfo info = default)
        {
            if (_session != null)
                _session.OnPlayerLoaded(info.Source);
        }

        /// <summary>Called on every peer when its arena scene finished loading.</summary>
        public void ReportLocalLoaded()
        {
            if (Runner.IsServer)
            {
                // Host: its own player counts as loaded directly. A dedicated server has no player.
                if (Runner.LocalPlayer != PlayerRef.None && _session != null)
                    _session.OnPlayerLoaded(Runner.LocalPlayer);
            }
            else
            {
                RPC_ReportLoaded();
            }
        }

        // ------------------------------------------------------------------ eliminations (Phase 5)

        [Networked] private int NetEliminationEventId { get; set; }
        [Networked] public int Eliminations { get; set; }

        private readonly EventDeduplicator _feedDedup = new EventDeduplicator();

        /// <summary>
        /// Server only: records an elimination decided by the damage system and announces it.
        /// Alive counting and victory are completed in Phase 9.
        /// </summary>
        public void RegisterElimination(PlayerRef victim, PlayerRef killer, short weaponId, bool headshot)
        {
            if (!HasStateAuthority)
                return;
            NetEliminationEventId++;
            Eliminations++;
            Debug.Log($"[WarOfNyx][Match] Elimination #{NetEliminationEventId}: player {killer.PlayerId} -> player {victim.PlayerId} " +
                      $"({WeaponRegistry.DisplayName(weaponId)}{(headshot ? ", headshot" : string.Empty)})");
            RPC_EliminationFeed(NetEliminationEventId, victim, killer, weaponId, headshot);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_EliminationFeed(int eventId, PlayerRef victim, PlayerRef killer, short weaponId, NetworkBool headshot)
        {
            if (!_feedDedup.TryAccept(eventId) || Application.isBatchMode)
                return;

            string text = CombatText.Elimination(NameOf(killer), NameOf(victim), WeaponRegistry.DisplayName(weaponId), headshot);
            bool involvesMe = Runner.LocalPlayer != PlayerRef.None && (victim == Runner.LocalPlayer || killer == Runner.LocalPlayer);
            var arena = OnlineArenaController.Instance;
            if (arena != null && arena.Hud != null)
                arena.Hud.AddFeed(text, involvesMe);
        }

        private string NameOf(PlayerRef player)
        {
            if (player == PlayerRef.None)
                return null;
            return Runner.TryGetPlayerObject(player, out var obj) && obj.TryGetComponent<NetworkPlayer>(out var p)
                ? p.PlayerName
                : $"Player {player.PlayerId}";
        }

        // ------------------------------------------------------------------ rules shared by all peers

        /// <summary>
        /// True when player input may move characters at simulation time <paramref name="simulationTime"/>.
        /// Evaluated with the tick's own time on the server and in client prediction, so both agree.
        /// </summary>
        public bool MovementAllowedAt(float simulationTime)
        {
            var phase = (MatchPhase)NetPhase;
            if (phase == MatchPhase.Active)
                return true;
            return phase == MatchPhase.Countdown && StartTime > 0f && simulationTime >= StartTime;
        }

        // ------------------------------------------------------------------ IMatchStateView

        public MatchPhase Phase => (MatchPhase)NetPhase;
        public int Players => NetPlayers;
        public int RequiredPlayers => NetRequired;
        public int MaxPlayers => NetMax;
        public int LoadedPlayers => NetLoaded;
        public bool IsPrivateRoom => NetPrivate;

        private float Now => Runner != null ? Runner.SimulationTime : 0f;

        public float SecondsRemaining => NetPhaseEndsAt > 0f ? Mathf.Max(0f, NetPhaseEndsAt - Now) : 0f;

        public float SecondsSinceStart => StartTime > 0f ? Now - StartTime : float.NegativeInfinity;

        public bool CanHostStart =>
            NetPrivate && Runner != null && Runner.IsServer && Runner.LocalPlayer != PlayerRef.None
            && (Phase == MatchPhase.WaitingForPlayers || Phase == MatchPhase.LobbyCountdown) && NetPlayers >= NetRequired;

        public void RequestHostStart()
        {
            if (HasStateAuthority && _lifecycle != null && CanHostStart)
                _lifecycle.TryHostStart(Runner.SimulationTime);
        }
    }
}
