using System;
using System.Collections.Generic;
using System.Linq;

namespace WarOfNyx.Core.Match
{
    /// <summary>
    /// Server-authoritative start-up of one match (master spec 5 + Phase 4):
    ///
    ///   WaitingForPlayers ─(min reached)→ LobbyCountdown ─(fill timer or lobby full)→ Loading
    ///   Loading ─(everyone loaded, or load timeout: stragglers kicked)→ Countdown (players spawned, locked)
    ///   Countdown ─(server time reaches the shared start time)→ Active
    ///   any state ─(everyone left)→ back to WaitingForPlayers (reset)
    ///
    /// New players are only accepted while waiting (no late joins, spec 20). Private rooms never
    /// auto-start: the host presses START (or the room fills).
    ///
    /// Pure and time-driven: the caller passes the authoritative server time (seconds). The
    /// Fusion adapter reacts to the events (load scene, kick, spawn, unlock input).
    /// </summary>
    public sealed class MatchLifecycle
    {
        private readonly MatchRules _rules;
        private readonly HashSet<int> _players = new HashSet<int>();
        private readonly HashSet<int> _loaded = new HashSet<int>();

        public MatchLifecycle(MatchRules rules, bool privateRoom)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            IsPrivateRoom = privateRoom;
        }

        public bool IsPrivateRoom { get; }
        public MatchRules Rules => _rules;
        public MatchPhase Phase { get; private set; } = MatchPhase.WaitingForPlayers;

        /// <summary>End of the current timed phase (lobby fill, load deadline, countdown), in server seconds. 0 when untimed.</summary>
        public float PhaseEndsAt { get; private set; }

        /// <summary>Server time at which input unlocked ("FIGHT!"). Spawn protection counts from here.</summary>
        public float ActivatedAt { get; private set; }

        public int PlayerCount => _players.Count;
        public int LoadedCount => _loaded.Count;

        public bool AcceptingPlayers =>
            (Phase == MatchPhase.WaitingForPlayers || Phase == MatchPhase.LobbyCountdown) && _players.Count < _rules.maxPlayers;

        /// <summary>Players may move/act only once the match is active (synchronized start).</summary>
        public bool InputUnlocked => Phase == MatchPhase.Active;

        public int MinPlayersToContinue => IsPrivateRoom ? 1 : Math.Max(1, Math.Min(2, _rules.minPlayersToStart));

        public event Action LoadingStarted;
        public event Action<IReadOnlyList<int>> PlayersKicked;
        /// <summary>Spawn these players now (sorted ascending for deterministic spawn assignment).</summary>
        public event Action<IReadOnlyList<int>> SpawnRequested;
        public event Action<float> CountdownStarted;
        public event Action<float> Activated;
        /// <summary>Back to the lobby: reload the lobby scene, despawn avatars, reopen the session.</summary>
        public event Action ResetToLobby;

        public bool HasPlayer(int playerId) => _players.Contains(playerId);

        public bool IsLoaded(int playerId) => _loaded.Contains(playerId);

        public bool TryAddPlayer(int playerId, float now)
        {
            if (!AcceptingPlayers || _players.Contains(playerId))
                return false;
            _players.Add(playerId);
            Update(now);
            return true;
        }

        public void RemovePlayer(int playerId, float now)
        {
            if (!_players.Remove(playerId))
                return;
            _loaded.Remove(playerId);
            Update(now);
        }

        /// <summary>A player's device finished loading the arena (reported once per player).</summary>
        public void MarkLoaded(int playerId, float now)
        {
            if (Phase != MatchPhase.Loading || !_players.Contains(playerId))
                return;
            _loaded.Add(playerId);
            Update(now);
        }

        /// <summary>Private-room host pressed START.</summary>
        public bool TryHostStart(float now)
        {
            if (!IsPrivateRoom || Phase > MatchPhase.LobbyCountdown || _players.Count < _rules.privateRoomMinPlayersToStart)
                return false;
            BeginLoading(now);
            return true;
        }

        public void Update(float now)
        {
            switch (Phase)
            {
                case MatchPhase.WaitingForPlayers:
                    if (_players.Count >= _rules.maxPlayers)
                        BeginLoading(now);
                    else if (!IsPrivateRoom && _players.Count >= _rules.minPlayersToStart)
                    {
                        Phase = MatchPhase.LobbyCountdown;
                        PhaseEndsAt = now + _rules.lobbyFillSeconds;
                        if (_rules.lobbyFillSeconds <= 0f)
                            BeginLoading(now);
                    }
                    break;

                case MatchPhase.LobbyCountdown:
                    if (_players.Count < _rules.minPlayersToStart)
                    {
                        Phase = MatchPhase.WaitingForPlayers;
                        PhaseEndsAt = 0f;
                    }
                    else if (_players.Count >= _rules.maxPlayers || now >= PhaseEndsAt)
                        BeginLoading(now);
                    break;

                case MatchPhase.Loading:
                    if (_players.Count == 0)
                        Reset();
                    else if (_loaded.Count >= _players.Count)
                        StartCountdown(now);
                    else if (now >= PhaseEndsAt)
                        HandleLoadTimeout(now);
                    break;

                case MatchPhase.Countdown:
                    if (_players.Count == 0)
                        Reset();
                    else if (now >= PhaseEndsAt)
                    {
                        Phase = MatchPhase.Active;
                        ActivatedAt = PhaseEndsAt; // exactly the announced start time for everyone
                        PhaseEndsAt = 0f;
                        Activated?.Invoke(ActivatedAt);
                    }
                    break;

                case MatchPhase.Active:
                case MatchPhase.Ended:
                    if (_players.Count == 0)
                        Reset();
                    break;
            }
        }

        private void BeginLoading(float now)
        {
            Phase = MatchPhase.Loading;
            PhaseEndsAt = now + _rules.loadTimeoutSeconds;
            _loaded.Clear();
            LoadingStarted?.Invoke();
        }

        private void HandleLoadTimeout(float now)
        {
            var stragglers = _players.Where(p => !_loaded.Contains(p)).OrderBy(p => p).ToList();
            foreach (int p in stragglers)
                _players.Remove(p);
            if (stragglers.Count > 0)
                PlayersKicked?.Invoke(stragglers);

            if (_loaded.Count < MinPlayersToContinue)
            {
                // Not enough players actually made it in: abandon this start and reopen the lobby.
                Reset();
                return;
            }

            StartCountdown(now);
        }

        private void StartCountdown(float now)
        {
            var participants = _players.OrderBy(p => p).ToList();
            SpawnRequested?.Invoke(participants);
            Phase = MatchPhase.Countdown;
            PhaseEndsAt = now + _rules.countdownSeconds;
            CountdownStarted?.Invoke(PhaseEndsAt);
        }

        private void Reset()
        {
            Phase = MatchPhase.WaitingForPlayers;
            PhaseEndsAt = 0f;
            ActivatedAt = 0f;
            _loaded.Clear();
            ResetToLobby?.Invoke();
        }
    }
}
