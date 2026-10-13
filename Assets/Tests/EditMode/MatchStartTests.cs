using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Tests
{
    public class MatchLifecycleTests
    {
        private MatchRules _rules;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _rules = new MatchRules { minPlayersToStart = 2, maxPlayers = 4, lobbyFillSeconds = 10f, loadTimeoutSeconds = 20f, countdownSeconds = 3f };
            _events = new List<string>();
        }

        private MatchLifecycle Create(bool privateRoom = false)
        {
            var m = new MatchLifecycle(_rules, privateRoom);
            m.LoadingStarted += () => _events.Add("load");
            m.PlayersKicked += ids => _events.Add("kick:" + string.Join(",", ids));
            m.SpawnRequested += ids => _events.Add("spawn:" + string.Join(",", ids));
            m.CountdownStarted += t => _events.Add($"countdown:{t}");
            m.Activated += t => _events.Add($"active:{t}");
            m.ResetToLobby += () => _events.Add("reset");
            return m;
        }

        [Test]
        public void WaitsUntilMinimumPlayers()
        {
            var m = Create();
            Assert.IsTrue(m.TryAddPlayer(1, 0f));
            m.Update(100f);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase);
            CollectionAssert.IsEmpty(_events);
        }

        [Test]
        public void FullTwoPlayerStartSequence()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 1f);
            Assert.AreEqual(MatchPhase.LobbyCountdown, m.Phase);
            Assert.AreEqual(11f, m.PhaseEndsAt);

            m.Update(11f);
            Assert.AreEqual(MatchPhase.Loading, m.Phase);
            Assert.IsFalse(m.AcceptingPlayers, "no late joins once loading starts");
            Assert.IsFalse(m.TryAddPlayer(3, 11f));

            m.MarkLoaded(2, 12f);
            Assert.AreEqual(MatchPhase.Loading, m.Phase, "waits for everyone");
            m.MarkLoaded(1, 13f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            Assert.AreEqual(16f, m.PhaseEndsAt);
            Assert.IsFalse(m.InputUnlocked, "spawned but locked during the countdown");

            m.Update(15.9f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            m.Update(16.02f);
            Assert.AreEqual(MatchPhase.Active, m.Phase);
            Assert.IsTrue(m.InputUnlocked);
            Assert.AreEqual(16f, m.ActivatedAt, "activation is the announced start time, not the late frame time");

            CollectionAssert.AreEqual(new[] { "load", "spawn:1,2", "countdown:16", "active:16" }, _events);
        }

        [Test]
        public void FullLobbyStartsImmediately()
        {
            var m = Create();
            for (int i = 1; i <= 4; i++)
                m.TryAddPlayer(i, 0f);
            Assert.AreEqual(MatchPhase.Loading, m.Phase);
            Assert.IsFalse(m.TryAddPlayer(5, 0f), "lobby full");
        }

        [Test]
        public void LobbyCountdownCancelsWhenAPlayerLeaves()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.RemovePlayer(2, 5f);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase);
            m.Update(50f);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase);
        }

        [Test]
        public void LoadTimeoutKicksSlowPlayersAndContinues()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.TryAddPlayer(3, 0f);
            m.Update(10f); // -> loading, deadline 30
            m.MarkLoaded(1, 11f);
            m.MarkLoaded(3, 12f);

            m.Update(29f);
            Assert.AreEqual(MatchPhase.Loading, m.Phase);
            m.Update(30f);

            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            CollectionAssert.Contains(_events, "kick:2");
            CollectionAssert.Contains(_events, "spawn:1,3");
            Assert.IsFalse(m.HasPlayer(2));
        }

        [Test]
        public void LoadTimeoutWithTooFewLoadedReturnsToLobby()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.Update(10f);
            m.MarkLoaded(1, 11f);
            m.Update(30f);

            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase);
            CollectionAssert.Contains(_events, "kick:2");
            CollectionAssert.Contains(_events, "reset");
            Assert.IsTrue(m.AcceptingPlayers, "lobby reopens");
        }

        [Test]
        public void LeaverDuringLoadingDoesNotBlockTheOthers()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.TryAddPlayer(3, 0f);
            m.Update(10f);
            m.MarkLoaded(1, 11f);
            m.MarkLoaded(2, 11f);
            m.RemovePlayer(3, 12f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
        }

        [Test]
        public void EveryoneLeavingResetsToLobby()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.Update(10f);
            m.MarkLoaded(1, 10f);
            m.MarkLoaded(2, 10f);
            m.Update(13f);
            Assert.AreEqual(MatchPhase.Active, m.Phase);

            m.RemovePlayer(1, 20f);
            Assert.AreEqual(MatchPhase.Active, m.Phase);
            m.RemovePlayer(2, 21f);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase);
            CollectionAssert.Contains(_events, "reset");
        }

        [Test]
        public void LoadReportsOutsideLoadingAreIgnored()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.MarkLoaded(1, 0f);
            Assert.AreEqual(0, m.LoadedCount);
            m.MarkLoaded(99, 0f);
            Assert.AreEqual(0, m.LoadedCount);
        }

        [Test]
        public void DuplicateJoinIsRejected()
        {
            var m = Create();
            Assert.IsTrue(m.TryAddPlayer(1, 0f));
            Assert.IsFalse(m.TryAddPlayer(1, 0f));
            Assert.AreEqual(1, m.PlayerCount);
        }

        [Test]
        public void PrivateRoomOnlyStartsOnHostCommand()
        {
            var m = Create(privateRoom: true);
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            m.Update(1000f);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, m.Phase, "no auto start in private rooms");

            Assert.IsTrue(m.TryHostStart(5f));
            Assert.AreEqual(MatchPhase.Loading, m.Phase);
            Assert.IsFalse(m.TryHostStart(6f), "can't start twice");
        }

        [Test]
        public void PublicMatchesIgnoreHostStart()
        {
            var m = Create();
            m.TryAddPlayer(1, 0f);
            Assert.IsFalse(m.TryHostStart(0f));
        }

        [Test]
        public void SoloPrivateRoomCanStartForTesting()
        {
            var m = Create(privateRoom: true);
            m.TryAddPlayer(1, 0f);
            Assert.IsTrue(m.TryHostStart(0f));
            m.MarkLoaded(1, 1f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
        }

        [Test]
        public void ZeroFillTimeStartsAsSoonAsMinimumIsMet()
        {
            _rules.lobbyFillSeconds = 0f;
            var m = Create();
            m.TryAddPlayer(1, 0f);
            m.TryAddPlayer(2, 0f);
            Assert.AreEqual(MatchPhase.Loading, m.Phase);
        }
    }

    public class SpawnPlannerTests
    {
        private sealed class FlatWorld : ISpawnValidator
        {
            public Func<float, float, bool> Blocked = (x, z) => false;
            public int Calls;

            public bool TryValidate(float x, float z, out float groundY)
            {
                Calls++;
                groundY = 2f;
                return !Blocked(x, z);
            }
        }

        private static float MinPairDistance(SpawnPoint[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
                for (int j = i + 1; j < points.Length; j++)
                {
                    float dx = points[i].X - points[j].X, dz = points[i].Z - points[j].Z;
                    best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
                }
            return best;
        }

        [Test]
        public void TwelvePlayersOnTheFullMapGetSixtyFiveMetres()
        {
            var rules = new MatchRules();
            var plan = SpawnPlanner.Plan(12, 0f, 0f, 500f, rules, new FlatWorld(), seed: 42);

            Assert.IsTrue(plan.Success, plan.Failure);
            Assert.AreEqual(12, plan.Points.Length);
            Assert.IsFalse(plan.Relaxed);
            Assert.GreaterOrEqual(MinPairDistance(plan.Points), 65f);
        }

        [Test]
        public void SpawnsStayInsideTheStartingZoneMinusMargin()
        {
            var rules = new MatchRules();
            var plan = SpawnPlanner.Plan(24, 100f, -50f, 500f, rules, new FlatWorld(), seed: 7);
            foreach (var p in plan.Points)
            {
                float d = (float)Math.Sqrt((p.X - 100f) * (p.X - 100f) + (p.Z + 50f) * (p.Z + 50f));
                Assert.LessOrEqual(d, 500f - rules.spawnEdgeMargin + 1e-3f);
                Assert.AreEqual(2f, p.Y, "uses the validated ground height");
            }
        }

        [Test]
        public void SmallAreaRelaxesSeparationInsteadOfFailing()
        {
            var rules = new MatchRules();
            var plan = SpawnPlanner.Plan(8, 0f, 0f, 60f, rules, new FlatWorld(), seed: 3);
            Assert.IsTrue(plan.Success, plan.Failure);
            Assert.IsTrue(plan.Relaxed);
            Assert.Less(plan.Separation, 65f);
            Assert.GreaterOrEqual(MinPairDistance(plan.Points), plan.Separation - 1e-3f);
        }

        [Test]
        public void NeverPicksInvalidGround()
        {
            // Water / buildings everywhere with x < 0.
            var world = new FlatWorld { Blocked = (x, z) => x < 0f };
            var plan = SpawnPlanner.Plan(10, 0f, 0f, 400f, new MatchRules(), world, seed: 11);
            Assert.IsTrue(plan.Success, plan.Failure);
            Assert.IsTrue(plan.Points.All(p => p.X >= 0f));
        }

        [Test]
        public void FailsCleanlyWhenNothingIsValid()
        {
            var world = new FlatWorld { Blocked = (x, z) => true };
            var plan = SpawnPlanner.Plan(2, 0f, 0f, 100f, new MatchRules(), world, seed: 1);
            Assert.IsFalse(plan.Success);
            Assert.AreEqual(0, plan.Points.Length);
            StringAssert.Contains("valid spawn candidates", plan.Failure);
        }

        [Test]
        public void FailsInsteadOfOverlappingPlayers()
        {
            // A 1 m wide strip can't hold 30 players 3 m apart... in a 10 m zone.
            var world = new FlatWorld { Blocked = (x, z) => Math.Abs(x) > 0.5f };
            var plan = SpawnPlanner.Plan(30, 0f, 0f, 15f, new MatchRules(), world, seed: 5);
            Assert.IsFalse(plan.Success);
            StringAssert.Contains("cannot separate", plan.Failure);
        }

        [Test]
        public void SameSeedSamePlan()
        {
            var a = SpawnPlanner.Plan(12, 0f, 0f, 500f, new MatchRules(), new FlatWorld(), seed: 99);
            var b = SpawnPlanner.Plan(12, 0f, 0f, 500f, new MatchRules(), new FlatWorld(), seed: 99);
            CollectionAssert.AreEqual(a.Points, b.Points);

            var c = SpawnPlanner.Plan(12, 0f, 0f, 500f, new MatchRules(), new FlatWorld(), seed: 100);
            CollectionAssert.AreNotEqual(a.Points, c.Points, "different matches get different spawns");
        }

        [Test]
        public void SpawnsFaceTheZoneCentre()
        {
            var plan = SpawnPlanner.Plan(6, 0f, 0f, 300f, new MatchRules(), new FlatWorld(), seed: 8);
            foreach (var p in plan.Points)
            {
                double rad = p.Yaw * Math.PI / 180.0;
                double fx = Math.Sin(rad), fz = Math.Cos(rad);
                double dot = (fx * -p.X + fz * -p.Z) / Math.Sqrt(p.X * p.X + p.Z * p.Z);
                Assert.Greater(dot, 0.99);
            }
        }

        [Test]
        public void SpreadsAcrossRegions()
        {
            // With 4 players on a big map, every quadrant should get someone (farthest-point spreading).
            var plan = SpawnPlanner.Plan(4, 0f, 0f, 500f, new MatchRules(), new FlatWorld(), seed: 21);
            var quadrants = plan.Points.Select(p => (p.X >= 0 ? 1 : 0) + (p.Z >= 0 ? 2 : 0)).Distinct().Count();
            Assert.GreaterOrEqual(quadrants, 3);
        }
    }

    public class SpawnProtectionTests
    {
        [Test]
        public void LastsForTheConfiguredTime()
        {
            var p = SpawnProtectionState.Grant(10f, 3f);
            Assert.IsTrue(p.IsActive(10f));
            Assert.IsTrue(p.IsActive(12.9f));
            Assert.AreEqual(1f, p.Remaining(12f), 1e-5f);
            Assert.IsFalse(p.IsActive(13f));
        }

        [TestCase(AggressiveAction.FiredWeapon)]
        [TestCase(AggressiveAction.MeleeAttack)]
        [TestCase(AggressiveAction.PickedUpCombatItem)]
        public void AggressiveActionsEndItImmediately(AggressiveAction action)
        {
            var p = SpawnProtectionState.Grant(0f, 3f);
            p.OnAggressiveAction(action);
            Assert.IsFalse(p.IsActive(0.1f));
        }

        [Test]
        public void NonActionDoesNotBreakIt()
        {
            var p = SpawnProtectionState.Grant(0f, 3f);
            p.OnAggressiveAction(AggressiveAction.None);
            Assert.IsTrue(p.IsActive(1f));
        }

        [Test]
        public void NoneIsNeverActive()
        {
            Assert.IsFalse(SpawnProtectionState.None.IsActive(0f));
        }
    }

    public class MatchmakingQueueTests
    {
        [Test]
        public void AttemptsImmediatelyThenBacksOff()
        {
            var q = new MatchmakingQueue(30f);
            q.Start(0f);
            Assert.IsTrue(q.ShouldAttempt(0f));
            q.ReportAttemptStarted();
            Assert.IsFalse(q.ShouldAttempt(0f), "one attempt at a time");

            q.ReportAttemptFailed(1f, "no open match");
            Assert.IsFalse(q.ShouldAttempt(2.9f));
            Assert.IsTrue(q.ShouldAttempt(3f), "2 s after the first failure");

            q.ReportAttemptStarted();
            q.ReportAttemptFailed(4f, "x");
            Assert.IsTrue(q.ShouldAttempt(7f), "3 s after the second");

            q.ReportAttemptStarted();
            q.ReportAttemptFailed(8f, "x");
            q.ReportAttemptStarted();
            q.ReportAttemptFailed(13f, "x");
            Assert.IsFalse(q.ShouldAttempt(17.9f));
            Assert.IsTrue(q.ShouldAttempt(18f), "capped at 5 s");
            Assert.AreEqual(4, q.Attempts);
        }

        [Test]
        public void OffersOfflineAfterThirtySecondsButKeepsSearching()
        {
            var q = new MatchmakingQueue(30f);
            q.Start(100f);
            Assert.IsFalse(q.ShouldOfferOffline(129f));
            Assert.IsTrue(q.ShouldOfferOffline(130f));
            Assert.IsTrue(q.IsActive, "never switches to bots on its own");
        }

        [Test]
        public void CancelStopsEverything()
        {
            var q = new MatchmakingQueue(30f);
            q.Start(0f);
            q.Cancel();
            Assert.IsFalse(q.ShouldAttempt(100f));
            Assert.IsFalse(q.ShouldOfferOffline(100f));
            Assert.AreEqual(MatchmakingStatus.Cancelled, q.Status);
        }

        [Test]
        public void JoinEndsTheSearch()
        {
            var q = new MatchmakingQueue(30f);
            q.Start(0f);
            q.ReportAttemptStarted();
            q.ReportJoined();
            Assert.AreEqual(MatchmakingStatus.Joined, q.Status);
            Assert.IsFalse(q.ShouldAttempt(10f));
        }
    }

    public class MatchPresentationTests
    {
        [TestCase(2.6f, "3")]
        [TestCase(2.0f, "2")]
        [TestCase(0.01f, "1")]
        [TestCase(0f, "FIGHT!")]
        [TestCase(-0.5f, "FIGHT!")]
        [TestCase(-1.5f, "")]
        public void CountdownText(float secondsLeft, string expected)
        {
            Assert.AreEqual(expected, MatchPresentation.CountdownText(secondsLeft));
        }

        [Test]
        public void PhasesMapToFlowStates()
        {
            Assert.AreEqual(GameFlowState.WaitingLobby, MatchPresentation.ToFlowState(MatchPhase.LobbyCountdown));
            Assert.AreEqual(GameFlowState.MatchPreparation, MatchPresentation.ToFlowState(MatchPhase.Loading));
            Assert.AreEqual(GameFlowState.Countdown, MatchPresentation.ToFlowState(MatchPhase.Countdown));
            Assert.AreEqual(GameFlowState.ActiveMatch, MatchPresentation.ToFlowState(MatchPhase.Active));
        }

        [Test]
        public void ClientFlowCatchesUpThroughEveryIntermediateState()
        {
            var sm = new GameStateMachine();
            var seen = new List<GameFlowState>();
            sm.StateChanged += e => seen.Add(e.Current);
            sm.TryStartSession(GameMode.OnlineBattleRoyale);

            Assert.IsTrue(sm.TryAdvanceTo(GameFlowState.Countdown));
            CollectionAssert.AreEqual(new[]
            {
                GameFlowState.Matchmaking, GameFlowState.WaitingLobby, GameFlowState.MatchPreparation,
                GameFlowState.SynchronizedSpawn, GameFlowState.Countdown,
            }, seen);

            Assert.IsTrue(sm.TryAdvanceTo(GameFlowState.MatchEnded));
            CollectionAssert.DoesNotContain(seen, GameFlowState.FinalCircle, "optional final circle is skipped");
            Assert.IsFalse(sm.TryAdvanceTo(GameFlowState.WaitingLobby), "never moves backwards");
        }

        [Test]
        public void ServerArgumentsForTwoPlayerTestMode()
        {
            var o = ServerLaunchOptions.Parse(new[] { "-server", "-minPlayers", "2", "-lobbyFill", "5" });
            Assert.AreEqual(2, o.MinPlayersToStart);
            Assert.AreEqual(5f, o.LobbyFillSeconds);

            var rules = new MatchRules { minPlayersToStart = 12, maxPlayers = 12 }.With(o.MinPlayersToStart, o.MaxPlayers, o.LobbyFillSeconds);
            Assert.AreEqual(2, rules.minPlayersToStart);
            Assert.AreEqual(5f, rules.lobbyFillSeconds);
        }

        [Test]
        public void ServerArgumentsDefaultToConfiguredRules()
        {
            var o = ServerLaunchOptions.Parse(new[] { "-server" });
            var rules = new MatchRules { minPlayersToStart = 12 }.With(o.MinPlayersToStart, -1, o.LobbyFillSeconds);
            Assert.AreEqual(12, rules.minPlayersToStart);
        }
    }
}
