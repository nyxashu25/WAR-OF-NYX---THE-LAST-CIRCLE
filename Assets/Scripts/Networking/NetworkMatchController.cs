using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Match;
using WarOfNyx.Gameplay;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.UI.Hud;

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
            _lifecycle.ResetToLobby += () =>
            {
                NetStartTime = 0f;
                Hunt = null;
                ServerResetResults();
            };
            // A fresh Nyx Hunt Protocol per match; players are tracked from their first active tick.
            _lifecycle.Activated += _ =>
            {
                var settings = App.AppServices.IsInitialized && App.AppServices.Config.hunt != null
                    ? App.AppServices.Config.hunt
                    : new HuntSettings();
                string error = settings.Validate();
                if (error != null)
                {
                    Debug.LogError($"[WarOfNyx][Hunt] Invalid hunt settings ({error}); using the spec defaults.");
                    settings = new HuntSettings();
                }
                Hunt = new NyxHuntProtocol(settings);
                ServerBeginResults(_lifecycle.ActivatedAt);
            };
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
            ServerUpdateResults();
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
        /// Server only: records an elimination decided by the damage system (a player or the Nyx Circle) and announces it.
        /// Alive counting and victory are completed in Phase 9.
        /// </summary>
        public void RegisterElimination(PlayerRef victim, PlayerRef killer, short weaponId, bool headshot, DamageKind cause)
        {
            if (!HasStateAuthority)
                return;
            NetEliminationEventId++;
            Eliminations++;
            float now = Runner.SimulationTime;
            int killerId = killer == PlayerRef.None || killer == victim ? -1 : killer.PlayerId;
            _resolver?.RecordElimination(victim.PlayerId, Runner.Tick.Raw, now, killerId, cause);
            string notice = killerId >= 0 && _notices != null ? _notices.OnElimination(killerId, victim.PlayerId, now) : null;
            Debug.Log($"[WarOfNyx][Match] Elimination #{NetEliminationEventId} ({cause}): player {killer.PlayerId} -> player {victim.PlayerId} " +
                      $"({WeaponRegistry.DisplayName(weaponId)}{(headshot ? ", headshot" : string.Empty)})");
            RPC_EliminationFeed(NetEliminationEventId, victim, killer, weaponId, headshot, (byte)cause, notice ?? string.Empty);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_EliminationFeed(int eventId, PlayerRef victim, PlayerRef killer, short weaponId, NetworkBool headshot, byte cause, string notice)
        {
            if (!_feedDedup.TryAccept(eventId) || Application.isBatchMode)
                return;

            var kind = (DamageKind)cause;
            string text = kind == DamageKind.Weapon || kind == DamageKind.Melee
                ? CombatText.Elimination(NameOf(killer), NameOf(victim), WeaponRegistry.DisplayName(weaponId), headshot)
                : CombatText.Environmental(NameOf(victim), kind);
            bool involvesMe = Runner.LocalPlayer != PlayerRef.None && (victim == Runner.LocalPlayer || killer == Runner.LocalPlayer);
            var arena = OnlineArenaController.Instance;
            if (arena != null && arena.Hud != null)
            {
                arena.Hud.AddFeed(text, involvesMe);
                if (!string.IsNullOrEmpty(notice))
                {
                    arena.Hud.AddFeed($"{notice}: {NameOf(killer)}", true);
                    arena.ShowHeadline($"{notice}  -  {NameOf(killer)}", UI.Common.NyxPalette.VictoryGold);
                }
            }
        }

        private string NameOf(PlayerRef player)
        {
            if (player == PlayerRef.None)
                return null;
            return Runner.TryGetPlayerObject(player, out var obj) && obj.TryGetComponent<NetworkPlayer>(out var p)
                ? p.PlayerName
                : $"Player {player.PlayerId}";
        }

        // ------------------------------------------------------------------ Nyx Hunt Protocol (Phase 8)

        /// <summary>Server only: the camping trackers of the running match (null outside a match).</summary>
        public NyxHuntProtocol Hunt { get; private set; }

        [Networked] private int NetHuntEventId { get; set; }
        private readonly EventDeduplicator _huntDedup = new EventDeduplicator();

        /// <summary>Server only: a player has just been exposed (first or repeated exposure). One feed line per exposure.</summary>
        public void AnnounceHuntAlert(PlayerRef player, int exposureCount)
        {
            if (!HasStateAuthority)
                return;
            NetHuntEventId++;
            Debug.Log($"[WarOfNyx][Hunt] Player {player.PlayerId} exposed for camping (#{exposureCount}).");
            RPC_HuntAlert(NetHuntEventId, player);
        }

        /// <summary>
        /// Server: a player left mid-match. Their camping timer is kept (reconnecting never resets it),
        /// and leaving counts as an elimination for placement.
        /// </summary>
        public void OnPlayerLeftMatch(PlayerRef player)
        {
            if (!HasStateAuthority)
                return;
            if (Hunt != null)
                Hunt.MarkDisconnected(player.PlayerId);
            if (_resolver != null && _lifecycle != null && _lifecycle.Phase == MatchPhase.Active)
                _resolver.RecordLeft(player.PlayerId, Runner.Tick.Raw, Runner.SimulationTime);
        }

        // ------------------------------------------------------------------ match end (Phase 9)

        [Networked] private int NetAlive { get; set; }
        [Networked] private int NetParticipants { get; set; }
        /// <summary>Player id of the winner, -1 while undecided or when nobody survived a solo test.</summary>
        [Networked] private int NetWinnerId { get; set; }
        [Networked] private NetworkBool NetWinnerTieBreak { get; set; }

        private MatchResolver _resolver;
        private KillNotices _notices;
        private bool _victoryFxPlayed;
        private readonly List<ResultRow> _rows = new List<ResultRow>();

        public int WinnerId => NetWinnerId;

        private void ServerBeginResults(float activatedAt)
        {
            _resolver = new MatchResolver(activatedAt);
            _notices = new KillNotices();
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null && p.Object != null)
                    _resolver.AddParticipant(p.Object.InputAuthority.PlayerId, p.PlayerName);
            }
            NetParticipants = _resolver.ParticipantCount;
            NetAlive = _resolver.AliveCount;
            NetWinnerId = -1;
            NetWinnerTieBreak = false;
        }

        private void ServerResetResults()
        {
            _resolver = null;
            _notices = null;
            NetAlive = 0;
            NetParticipants = 0;
            NetWinnerId = -1;
            NetWinnerTieBreak = false;
            Eliminations = 0;
        }

        /// <summary>Server, every tick of an active match: trusted stats, placements, last-survivor check.</summary>
        private void ServerUpdateResults()
        {
            if (_resolver == null || _lifecycle.Phase != MatchPhase.Active)
                return;

            float now = Runner.SimulationTime;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || p.Object == null)
                    continue;
                int id = p.Object.InputAuthority.PlayerId;
                if (_resolver.TryGet(id, out var record))
                    record.Name = p.PlayerName;
                var combat = p.GetComponent<NetworkCombat>();
                if (combat != null)
                    _resolver.UpdateStats(id, combat.Kills, combat.DamageDealt);
            }

            // Everything from earlier ticks is complete; same-tick eliminations are tie-broken together.
            foreach (var placed in _resolver.ResolveUpTo(Runner.Tick.Raw - 1, now))
            {
                var player = FindPlayer(placed.PlayerId);
                if (player != null)
                    player.ServerSetResult(placed.Placement, placed.SurvivalSeconds, placed.Winner);
            }
            NetAlive = _resolver.AliveCount;

            if (_resolver.Finished)
                ServerFinishMatch(now);
        }

        private void ServerFinishMatch(float now)
        {
            if (!_lifecycle.EndMatch(now))
                return;

            var winner = _resolver.Winner;
            NetWinnerId = winner != null ? winner.PlayerId : -1;
            NetWinnerTieBreak = winner != null && winner.WonByTieBreak;
            if (TryGetComponent<NetworkNyxCircle>(out var zone))
                zone.ServerFreeze(now);

            Debug.Log(winner != null
                ? $"[WarOfNyx][Match] VICTORY: {winner.Name} (player {winner.PlayerId}) is the last survivor" +
                  $"{(winner.WonByTieBreak ? " by tie-break" : string.Empty)}. {winner.Kills} kills, {winner.Damage:0} damage."
                : "[WarOfNyx][Match] The match ended with no survivor.");
            WriteResultsFile(winner);
        }

        private static NetworkPlayer FindPlayer(int playerId)
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null && p.Object != null && p.Object.InputAuthority.PlayerId == playerId)
                    return p;
            }
            return null;
        }

        [Serializable]
        private sealed class ResultsFileRow
        {
            public int placement;
            public int playerId;
            public string name;
            public int kills;
            public float damage;
            public float survivalSeconds;
            public bool left;
            public string cause;
        }

        [Serializable]
        private sealed class ResultsFile
        {
            public string session;
            public string endedUtc;
            public int protocol;
            public string winner;
            public bool tieBreak;
            public List<ResultsFileRow> standings = new List<ResultsFileRow>();
        }

        /// <summary>
        /// Server: the trusted match record ("save match results where supported"). Written next to the
        /// server's data as JSON; a persistent backend can import these later. Never written by clients.
        /// </summary>
        private void WriteResultsFile(ParticipantRecord winner)
        {
            try
            {
                var file = new ResultsFile
                {
                    session = Runner.SessionInfo != null ? Runner.SessionInfo.Name : "unknown",
                    endedUtc = DateTime.UtcNow.ToString("o"),
                    protocol = NetworkProtocol.Version,
                    winner = winner != null ? winner.Name : string.Empty,
                    tieBreak = winner != null && winner.WonByTieBreak,
                };
                foreach (var r in _resolver.Standings)
                {
                    file.standings.Add(new ResultsFileRow
                    {
                        placement = r.Placement, playerId = r.PlayerId, name = r.Name, kills = r.Kills, damage = r.Damage,
                        survivalSeconds = r.SurvivalSeconds, left = r.Left, cause = r.Winner ? "winner" : r.Cause.ToString(),
                    });
                }

                string dir = Path.Combine(Application.persistentDataPath, "match-results");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{file.session}.json");
                File.WriteAllText(path, JsonUtility.ToJson(file, true));
                Debug.Log($"[WarOfNyx][Match] Results saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WarOfNyx][Match] Could not save the results file: {e.Message}");
            }
        }

        public override void Render()
        {
            if (Application.isBatchMode)
                return;
            var arena = OnlineArenaController.Instance;
            if (arena == null)
                return;

            if (Phase != MatchPhase.Ended)
            {
                _victoryFxPlayed = false;
                arena.HideResults();
                return;
            }

            _rows.Clear();
            NetworkPlayer winnerPlayer = null;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || p.Object == null)
                    continue;
                var combat = p.GetComponent<NetworkCombat>();
                _rows.Add(new ResultRow
                {
                    Placement = p.Placement,
                    Name = p.PlayerName,
                    Kills = combat != null ? combat.Kills : 0,
                    Damage = combat != null ? combat.DamageDealt : 0f,
                    SurvivalSeconds = p.SurvivalSeconds,
                    IsLocal = p.HasInputAuthority,
                    Winner = p.IsWinner,
                });
                if (p.IsWinner)
                    winnerPlayer = p;
            }
            _rows.Sort((a, b) => (a.Placement == 0 ? int.MaxValue : a.Placement).CompareTo(b.Placement == 0 ? int.MaxValue : b.Placement));
            arena.ShowResults(_rows, NetParticipants, SecondsRemaining, NetWinnerTieBreak, winnerPlayer != null ? winnerPlayer.transform : null);

            // PLACEHOLDER victory celebration at the winner (Phase 10 adds the real animation).
            if (!_victoryFxPlayed && winnerPlayer != null)
            {
                _victoryFxPlayed = true;
                SpawnEffect.Play(winnerPlayer.transform.position);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_HuntAlert(int eventId, PlayerRef player)
        {
            if (!_huntDedup.TryAccept(eventId) || Application.isBatchMode)
                return;
            var arena = OnlineArenaController.Instance;
            if (arena != null && arena.Hud != null)
                arena.Hud.AddFeed(HuntText.Alert(NameOf(player)), highlight: true);
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

        public int AlivePlayers => NetAlive;
        public int Participants => NetParticipants;

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
