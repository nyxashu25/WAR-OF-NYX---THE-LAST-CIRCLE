using System.Collections.Generic;
using NUnit.Framework;
using WarOfNyx.Core.Flow;

namespace WarOfNyx.Tests
{
    public class GameStateMachineTests
    {
        private static readonly GameFlowState[] OnlineHappyPath =
        {
            GameFlowState.Matchmaking, GameFlowState.WaitingLobby, GameFlowState.MatchPreparation,
            GameFlowState.SynchronizedSpawn, GameFlowState.Countdown, GameFlowState.ActiveMatch,
            GameFlowState.FinalCircle, GameFlowState.MatchEnded, GameFlowState.ResultsScreen,
            GameFlowState.ReturnToLobby,
        };

        [Test]
        public void StartsInMainMenuWithoutMode()
        {
            var sm = new GameStateMachine();
            Assert.AreEqual(GameFlowState.MainMenu, sm.Current);
            Assert.AreEqual(GameMode.None, sm.Mode);
        }

        [Test]
        public void OnlineSessionWalksTheFullLifecycle()
        {
            var sm = new GameStateMachine();
            var seen = new List<GameFlowState>();
            sm.StateChanged += e => seen.Add(e.Current);

            Assert.IsTrue(sm.TryStartSession(GameMode.OnlineBattleRoyale));
            for (int i = 1; i < OnlineHappyPath.Length; i++)
                Assert.IsTrue(sm.TryTransition(OnlineHappyPath[i]), $"-> {OnlineHappyPath[i]}");

            CollectionAssert.AreEqual(OnlineHappyPath, seen);
            Assert.AreEqual(GameMode.OnlineBattleRoyale, sm.Mode);
        }

        [Test]
        public void OfflineSessionSkipsMatchmakingAndLobby()
        {
            var sm = new GameStateMachine();
            Assert.IsTrue(sm.TryStartSession(GameMode.OfflineBattleRoyale));
            Assert.AreEqual(GameFlowState.MatchPreparation, sm.Current);
        }

        [Test]
        public void OfflineSessionCannotEnterOnlineOnlyStates()
        {
            var sm = new GameStateMachine();
            sm.TryStartSession(GameMode.OfflineBattleRoyale);
            WalkTo(sm, GameFlowState.ReturnToLobby);

            Assert.IsFalse(sm.TryTransition(GameFlowState.Matchmaking));
            Assert.IsTrue(sm.TryTransition(GameFlowState.MatchPreparation), "local play-again goes straight to preparation");
        }

        [Test]
        public void OnlinePlayAgainMustRequeue()
        {
            var sm = new GameStateMachine();
            sm.TryStartSession(GameMode.OnlineBattleRoyale);
            sm.TryTransition(GameFlowState.WaitingLobby);
            WalkTo(sm, GameFlowState.ReturnToLobby);

            Assert.IsFalse(sm.TryTransition(GameFlowState.MatchPreparation));
            Assert.IsTrue(sm.TryTransition(GameFlowState.Matchmaking));
        }

        [Test]
        public void CannotSkipStates()
        {
            var sm = new GameStateMachine();
            sm.TryStartSession(GameMode.OnlineBattleRoyale);

            Assert.IsFalse(sm.TryTransition(GameFlowState.ActiveMatch));
            Assert.IsFalse(sm.TryTransition(GameFlowState.MatchEnded));
            Assert.AreEqual(GameFlowState.Matchmaking, sm.Current);
        }

        [Test]
        public void CannotStartTwoSessionsOrStartWithoutMode()
        {
            var sm = new GameStateMachine();
            Assert.IsFalse(sm.TryStartSession(GameMode.None));
            Assert.IsTrue(sm.TryStartSession(GameMode.PracticeArena));
            Assert.IsFalse(sm.TryStartSession(GameMode.OnlineBattleRoyale));
        }

        [Test]
        public void SessionsOnlyStartThroughTryStartSession()
        {
            var sm = new GameStateMachine();
            Assert.IsFalse(sm.TryTransition(GameFlowState.Matchmaking));
            Assert.IsFalse(sm.TryTransition(GameFlowState.MatchPreparation));
        }

        [Test]
        public void AbortFromAnyStateReturnsToMenuAndClearsMode()
        {
            var sm = new GameStateMachine();
            sm.TryStartSession(GameMode.OnlineBattleRoyale);
            sm.TryTransition(GameFlowState.WaitingLobby);
            sm.TryTransition(GameFlowState.MatchPreparation);

            GameFlowStateChanged last = default;
            sm.StateChanged += e => last = e;

            Assert.IsTrue(sm.TryTransition(GameFlowState.MainMenu));
            Assert.AreEqual(GameFlowState.MainMenu, sm.Current);
            Assert.AreEqual(GameMode.None, sm.Mode);
            Assert.AreEqual(GameFlowState.MatchPreparation, last.Previous);
            Assert.AreEqual(GameMode.OnlineBattleRoyale, last.Mode, "event reports the mode that ended");
        }

        [Test]
        public void SelfTransitionIsRejected()
        {
            var sm = new GameStateMachine();
            Assert.IsFalse(sm.TryTransition(GameFlowState.MainMenu));
        }

        private static void WalkTo(GameStateMachine sm, GameFlowState target)
        {
            var path = new[]
            {
                GameFlowState.MatchPreparation, GameFlowState.SynchronizedSpawn, GameFlowState.Countdown,
                GameFlowState.ActiveMatch, GameFlowState.MatchEnded, GameFlowState.ResultsScreen, GameFlowState.ReturnToLobby,
            };
            foreach (var state in path)
            {
                if (sm.Current == target)
                    return;
                if (sm.Current != state)
                    Assert.IsTrue(sm.TryTransition(state), $"{sm.Current} -> {state}");
            }
            Assert.AreEqual(target, sm.Current);
        }
    }
}
