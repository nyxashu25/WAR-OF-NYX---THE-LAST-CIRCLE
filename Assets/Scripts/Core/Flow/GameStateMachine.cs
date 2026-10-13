using System;
using System.Collections.Generic;

namespace WarOfNyx.Core.Flow
{
    /// <summary>Raised after every successful state change.</summary>
    public readonly struct GameFlowStateChanged
    {
        public readonly GameFlowState Previous;
        public readonly GameFlowState Current;
        public readonly GameMode Mode;

        public GameFlowStateChanged(GameFlowState previous, GameFlowState current, GameMode mode)
        {
            Previous = previous;
            Current = current;
            Mode = mode;
        }
    }

    /// <summary>
    /// Pure, engine-independent match flow state machine (master spec section 5).
    ///
    /// Authority: on an online match only the dedicated server may advance this machine;
    /// clients mirror the replicated state. Offline/practice matches advance it locally.
    /// The machine itself does not know who is calling it — callers enforce authority.
    ///
    /// Rules:
    ///  * A session starts from MainMenu with an explicit <see cref="GameMode"/>.
    ///  * Online sessions go through Matchmaking and WaitingLobby; local sessions skip them.
    ///  * Any state may abort back to MainMenu (disconnect, load failure, player quit).
    ///  * Returning to MainMenu ends the session and clears the mode.
    /// </summary>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameFlowState, GameFlowState[]> Transitions =
            new Dictionary<GameFlowState, GameFlowState[]>
            {
                { GameFlowState.MainMenu, new[] { GameFlowState.Matchmaking, GameFlowState.MatchPreparation } },
                { GameFlowState.Matchmaking, new[] { GameFlowState.WaitingLobby } },
                { GameFlowState.WaitingLobby, new[] { GameFlowState.MatchPreparation } },
                { GameFlowState.MatchPreparation, new[] { GameFlowState.SynchronizedSpawn } },
                { GameFlowState.SynchronizedSpawn, new[] { GameFlowState.Countdown } },
                { GameFlowState.Countdown, new[] { GameFlowState.ActiveMatch } },
                { GameFlowState.ActiveMatch, new[] { GameFlowState.FinalCircle, GameFlowState.MatchEnded } },
                { GameFlowState.FinalCircle, new[] { GameFlowState.MatchEnded } },
                { GameFlowState.MatchEnded, new[] { GameFlowState.ResultsScreen } },
                { GameFlowState.ResultsScreen, new[] { GameFlowState.ReturnToLobby } },
                { GameFlowState.ReturnToLobby, new[] { GameFlowState.Matchmaking, GameFlowState.MatchPreparation } },
            };

        public GameFlowState Current { get; private set; } = GameFlowState.MainMenu;
        public GameMode Mode { get; private set; } = GameMode.None;

        public event Action<GameFlowStateChanged> StateChanged;

        /// <summary>Leaves MainMenu and starts a session in the given mode.</summary>
        public bool TryStartSession(GameMode mode)
        {
            if (Current != GameFlowState.MainMenu || mode == GameMode.None)
                return false;

            var entry = mode.RequiresServer() ? GameFlowState.Matchmaking : GameFlowState.MatchPreparation;
            Mode = mode;
            Apply(entry);
            return true;
        }

        /// <summary>Requests a transition. Returns false (and changes nothing) when it is not allowed.</summary>
        public bool TryTransition(GameFlowState next)
        {
            if (!CanTransition(next))
                return false;

            if (next == GameFlowState.MainMenu)
            {
                AbortToMainMenu();
                return true;
            }

            Apply(next);
            return true;
        }

        public bool CanTransition(GameFlowState next)
        {
            if (next == Current)
                return false;

            // Aborting to the main menu is always possible from inside a session.
            if (next == GameFlowState.MainMenu)
                return Current != GameFlowState.MainMenu;

            // Sessions can only begin through TryStartSession so the mode is always known.
            if (Current == GameFlowState.MainMenu)
                return false;

            if (!Transitions.TryGetValue(Current, out var allowed) || Array.IndexOf(allowed, next) < 0)
                return false;

            // Online-only states are unreachable for local simulations and vice versa.
            bool onlineOnly = next == GameFlowState.Matchmaking || next == GameFlowState.WaitingLobby;
            if (onlineOnly && !Mode.RequiresServer())
                return false;

            // A local "play again" goes straight to preparation; online must re-queue.
            if (Current == GameFlowState.ReturnToLobby && next == GameFlowState.MatchPreparation && Mode.RequiresServer())
                return false;

            return true;
        }

        private static readonly GameFlowState[] CanonicalOrder =
        {
            GameFlowState.Matchmaking, GameFlowState.WaitingLobby, GameFlowState.MatchPreparation,
            GameFlowState.SynchronizedSpawn, GameFlowState.Countdown, GameFlowState.ActiveMatch,
            GameFlowState.FinalCircle, GameFlowState.MatchEnded, GameFlowState.ResultsScreen, GameFlowState.ReturnToLobby,
        };

        /// <summary>
        /// Moves forward through every intermediate state until <paramref name="target"/> is reached
        /// (clients follow the server's phase, which can skip states between two updates).
        /// States that don't apply to the mode (e.g. Matchmaking offline) are skipped.
        /// Never moves backwards; returns true when <see cref="Current"/> equals the target.
        /// </summary>
        public bool TryAdvanceTo(GameFlowState target)
        {
            if (Current == target)
                return true;
            if (Current == GameFlowState.MainMenu || target == GameFlowState.MainMenu)
                return false;

            int from = Array.IndexOf(CanonicalOrder, Current);
            int to = Array.IndexOf(CanonicalOrder, target);
            if (from < 0 || to < 0 || to <= from)
                return false;

            for (int i = from + 1; i <= to; i++)
            {
                var next = CanonicalOrder[i];
                // FinalCircle is optional: only enter it when it is the actual target.
                if (next == GameFlowState.FinalCircle && target != GameFlowState.FinalCircle)
                    continue;
                if (CanTransition(next))
                    Apply(next);
            }

            return Current == target;
        }

        /// <summary>Ends the session (disconnect, quit, load failure) and returns to the main menu.</summary>
        public void AbortToMainMenu()
        {
            if (Current == GameFlowState.MainMenu)
                return;

            Apply(GameFlowState.MainMenu);
            Mode = GameMode.None;
        }

        private void Apply(GameFlowState next)
        {
            var previous = Current;
            Current = next;
            StateChanged?.Invoke(new GameFlowStateChanged(previous, next, Mode));
        }
    }
}
