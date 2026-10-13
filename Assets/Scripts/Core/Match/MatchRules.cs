using System;

namespace WarOfNyx.Core.Match
{
    /// <summary>
    /// Tunable match start-up and spawn rules (master spec sections 4, 5, 6). Values are [T].
    /// The server's copy is authoritative; clients only display what the server replicates.
    /// </summary>
    [Serializable]
    public sealed class MatchRules
    {
        /// <summary>Players needed before the lobby starts counting down: 12 for the public MVP (spec 4).
        /// The dev two-player test mode is explicit: launch the server with -minPlayers 2.</summary>
        public int minPlayersToStart = 12;
        public int maxPlayers = 12;

        /// <summary>Once the minimum is reached, wait this long for more players (resets nothing; a full lobby starts at once).</summary>
        public float lobbyFillSeconds = 15f;

        /// <summary>Players that have not loaded the arena by then are removed (spec 5: slow devices must not delay everyone).</summary>
        public float loadTimeoutSeconds = 20f;

        /// <summary>The "3... 2... 1... FIGHT!" countdown. Everyone gains control at the same server tick.</summary>
        public float countdownSeconds = 3f;

        public float spawnProtectionSeconds = 3f;

        /// <summary>Spec 6: 65 m initial separation, relaxed step by step, never below the hard floor.</summary>
        public float minSpawnSeparation = 65f;
        public float[] separationRelaxSteps = { 50f, 40f, 25f };
        /// <summary>Absolute minimum: two capsules can never overlap or spawn touching.</summary>
        public float separationHardFloor = 3f;
        /// <summary>Spawns stay this far inside the starting safe zone.</summary>
        public float spawnEdgeMargin = 5f;
        /// <summary>Height above the validated ground the player appears at, so the landing is visible.</summary>
        public float spawnDropHeight = 1.2f;

        /// <summary>Private (host) rooms only start when the host presses START.</summary>
        public int privateRoomMinPlayersToStart = 1;

        public MatchRules Clone()
        {
            var copy = (MatchRules)MemberwiseClone();
            copy.separationRelaxSteps = (float[])separationRelaxSteps?.Clone() ?? Array.Empty<float>();
            return copy;
        }

        /// <summary>Applies dedicated-server command-line overrides (negative values = keep).</summary>
        public MatchRules With(int minPlayers, int maxPlayersOverride, float lobbyFill)
        {
            var copy = Clone();
            if (maxPlayersOverride > 0) copy.maxPlayers = maxPlayersOverride;
            if (minPlayers > 0) copy.minPlayersToStart = Math.Min(minPlayers, copy.maxPlayers);
            if (lobbyFill >= 0f) copy.lobbyFillSeconds = lobbyFill;
            return copy;
        }
    }

    /// <summary>Server-side match phase, replicated to clients for UI and input locking.</summary>
    public enum MatchPhase : byte
    {
        WaitingForPlayers = 0,
        LobbyCountdown = 1,
        Loading = 2,
        Countdown = 3,
        Active = 4,
        /// <summary>Phase 9 (winner / results). Not reached yet.</summary>
        Ended = 5,
    }

    /// <summary>Session properties used by matchmaking (servers publish, clients filter).</summary>
    public static class NetworkProtocol
    {
        /// <summary>Bump whenever networked state or inputs change incompatibly; clients only join equal versions.</summary>
        public const int Version = 8;

        public const string VersionKey = "pv";
        public const string ModeKey = "mode";
        public const int ModeBattleRoyale = 1;
    }
}
