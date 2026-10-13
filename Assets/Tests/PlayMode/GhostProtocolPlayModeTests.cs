using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WarOfNyx.Core.Offline;
using WarOfNyx.Gameplay.Offline;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Spec 38.17 "Additional Acceptance Test": an offline match runs with no human combat input and
    /// must reach a valid last-survivor result on its own, with bots exploring and fighting each other.
    /// Runs the real Ghost Protocol scene (physics, NavMesh, shared rules) at accelerated simulation speed.
    /// </summary>
    public class GhostProtocolPlayModeTests
    {
        private const float SimulationSpeed = 12f;
        private const float RealTimeLimit = 420f;

        [UnitySetUp]
        public IEnumerator EmptyWorld() => TestWorld.Fresh();

        [TearDown]
        public void Restore()
        {
            OfflineMatchRequest.Reset();
            Time.timeScale = 1f;
        }

        private static IEnumerator RunMatch(bool includeHuman, int seed)
        {
            if (!Application.CanStreamedLevelBeLoaded("GhostProtocol"))
                Assert.Ignore("GhostProtocol is not in Build Settings. Run War of Nyx > Setup > Run Project Setup.");

            LogAssert.ignoreFailingMessages = true;
            OfflineMatchRequest.Reset();
            OfflineMatchRequest.Bots = 11;
            OfflineMatchRequest.Difficulty = BotDifficulty.Elite;
            OfflineMatchRequest.IncludeHuman = includeHuman;
            OfflineMatchRequest.ZoneStageSeconds = 20f; // whole circle in 3 simulated minutes
            OfflineMatchRequest.Seed = seed;

            yield return SceneManager.LoadSceneAsync("GhostProtocol", LoadSceneMode.Single);
            yield return null;
            var match = Object.FindAnyObjectByType<OfflineMatch>();
            Assert.IsNotNull(match, "the scene has a local match authority");
            match.SimulationSpeed = SimulationSpeed;

            float started = Time.realtimeSinceStartup;
            while (match.Phase != OfflinePhase.Ended && Time.realtimeSinceStartup - started < RealTimeLimit)
                yield return null;

            Debug.Log($"[GhostProtocolTest] seed {seed}, human {includeHuman}: phase {match.Phase} after {match.Now:0} simulated s " +
                      $"({Time.realtimeSinceStartup - started:0} real s). Standings: " +
                      string.Join(", ", match.Resolver.Standings.Select(r => $"#{r.Placement} {r.Name} {r.Kills}k {r.Cause}")));
            Assert.AreEqual(OfflinePhase.Ended, match.Phase, "the match finished on its own (no deadlock)");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotsOnlyMatchProducesOneLegitimateWinner()
        {
            yield return RunMatch(includeHuman: false, seed: 1234);
            var match = Object.FindAnyObjectByType<OfflineMatch>();
            var resolver = match.Resolver;

            Assert.AreEqual(11, resolver.ParticipantCount, "11 bots took part");
            Assert.AreEqual(1, resolver.Records.Count(r => r.Winner), "exactly one winner");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 11), resolver.Records.Select(r => r.Placement), "every bot has a distinct placement");
            Assert.Greater(match.Combatants.Sum(c => c.Kills), 0, "bots fought and eliminated each other (spec 38.10)");
            Assert.Greater(match.Combatants.Sum(c => c.ShotsFired), 0, "bots used their weapons");
            Assert.IsTrue(match.Combatants.All(c => c.Alive == resolver.Records.Single(r => r.PlayerId == c.Id).Winner || !c.Alive),
                "only the winner may still be alive");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator IdleHumanIsEliminatedAndTheMatchStillEnds()
        {
            yield return RunMatch(includeHuman: true, seed: 777);
            var match = Object.FindAnyObjectByType<OfflineMatch>();
            var resolver = match.Resolver;

            Assert.IsNotNull(match.Human);
            Assert.AreEqual(12, resolver.ParticipantCount, "PLAYERS 12/12: one human + eleven bots");
            Assert.IsTrue(resolver.TryGet(match.Human.Id, out var human));
            Assert.Greater(human.Placement, 0, "the human got a placement");
            Assert.AreEqual(1, resolver.Records.Count(r => r.Winner));
        }
    }
}
