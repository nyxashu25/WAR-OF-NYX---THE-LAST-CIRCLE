namespace WarOfNyx.Core.Flow
{
    /// <summary>
    /// The three top-level ways to play (master spec section 38.1).
    /// The mode decides which simulation authority runs the match:
    /// Online = dedicated Fusion server, Offline/Practice = local authoritative simulation.
    /// </summary>
    public enum GameMode
    {
        None = 0,
        OnlineBattleRoyale = 1,
        OfflineBattleRoyale = 2,
        PracticeArena = 3,
    }

    public static class GameModeExtensions
    {
        /// <summary>True when the match needs a remote authoritative server.</summary>
        public static bool RequiresServer(this GameMode mode) => mode == GameMode.OnlineBattleRoyale;

        /// <summary>True when the match is simulated entirely on this device.</summary>
        public static bool IsLocalSimulation(this GameMode mode) =>
            mode == GameMode.OfflineBattleRoyale || mode == GameMode.PracticeArena;
    }
}
