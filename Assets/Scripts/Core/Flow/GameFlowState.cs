namespace WarOfNyx.Core.Flow
{
    /// <summary>
    /// Match lifecycle states from master spec section 5.
    /// Online matches drive these from the dedicated server; offline matches from the local simulation.
    /// </summary>
    public enum GameFlowState
    {
        MainMenu = 0,
        Matchmaking = 1,
        WaitingLobby = 2,
        MatchPreparation = 3,
        SynchronizedSpawn = 4,
        Countdown = 5,
        ActiveMatch = 6,
        FinalCircle = 7,
        MatchEnded = 8,
        ResultsScreen = 9,
        ReturnToLobby = 10,
    }
}
