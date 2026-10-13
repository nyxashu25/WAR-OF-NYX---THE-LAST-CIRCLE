using System;
using WarOfNyx.Core.Flow;

namespace WarOfNyx.Core.Match
{
    /// <summary>Display helpers shared by every client, so all players see the same thing.</summary>
    public static class MatchPresentation
    {
        /// <summary>
        /// The big centre-screen countdown (spec 5: "3... 2... 1... FIGHT!").
        /// <paramref name="secondsUntilStart"/> comes from the server's shared start time; "FIGHT!"
        /// stays visible for <paramref name="fightBannerSeconds"/> after the start, then nothing.
        /// </summary>
        public static string CountdownText(float secondsUntilStart, float fightBannerSeconds = 1f)
        {
            if (float.IsNaN(secondsUntilStart) || float.IsInfinity(secondsUntilStart))
                return string.Empty;
            if (secondsUntilStart > 0f)
                return ((int)Math.Ceiling(secondsUntilStart)).ToString();
            return -secondsUntilStart < fightBannerSeconds ? "FIGHT!" : string.Empty;
        }

        /// <summary>Client-side flow state for a replicated server phase.</summary>
        public static GameFlowState ToFlowState(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.WaitingForPlayers:
                case MatchPhase.LobbyCountdown:
                    return GameFlowState.WaitingLobby;
                case MatchPhase.Loading:
                    return GameFlowState.MatchPreparation;
                case MatchPhase.Countdown:
                    return GameFlowState.Countdown;
                case MatchPhase.Active:
                    return GameFlowState.ActiveMatch;
                case MatchPhase.Ended:
                    return GameFlowState.MatchEnded;
                default:
                    return GameFlowState.WaitingLobby;
            }
        }

        public static string LobbyStatus(MatchPhase phase, int players, int required, int max, float secondsLeft, bool privateRoom)
        {
            switch (phase)
            {
                case MatchPhase.WaitingForPlayers:
                    return privateRoom
                        ? $"PRIVATE ROOM - {players}/{max} PLAYERS - WAITING FOR THE HOST TO START"
                        : $"WAITING FOR PLAYERS - {players}/{required} NEEDED TO START";
                case MatchPhase.LobbyCountdown:
                    return $"MATCH STARTS IN {Math.Max(0, (int)Math.Ceiling(secondsLeft))}s - {players}/{max} PLAYERS";
                case MatchPhase.Loading:
                    return "LOADING THE ARENA...";
                default:
                    return string.Empty;
            }
        }
    }
}
