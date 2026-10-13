using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WarOfNyx.Core.Offline;
using WarOfNyx.Gameplay.Offline;
using Debug = UnityEngine.Debug;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Phase 13 benchmark: cost of one authoritative offline simulation tick (all bots thinking,
    /// perceiving, navigating and fighting + physics queries) on NYX ISLAND for 5 / 11 / 23 NIGHTMARE
    /// bots. Rendering is excluded (batch mode); this measures the CPU budget the AI and rules need.
    /// Results are logged as "[Perf]" lines and recorded in PERFORMANCE.md. The assertion is a loose
    /// regression guard for desktop CPUs, not the Android target.
    /// </summary>
    public class OfflinePerformanceTests
    {
        private const int WarmupTicks = 240;   // countdown + first second of fighting
        private const int MeasuredTicks = 1800; // 30 simulated seconds at 60 Hz

        [UnitySetUp]
        public IEnumerator EmptyWorld() => TestWorld.Fresh();

        [TearDown]
        public void Restore() => OfflineMatchRequest.Reset();

        [UnityTest, Timeout(600000)]
        public IEnumerator TickCostWith5Bots() => Measure(5);

        [UnityTest, Timeout(600000)]
        public IEnumerator TickCostWith11Bots() => Measure(11);

        [UnityTest, Timeout(600000)]
        public IEnumerator TickCostWith23Bots() => Measure(23);

        private static IEnumerator Measure(int bots)
        {
            if (!Application.CanStreamedLevelBeLoaded("NyxIsland"))
                Assert.Ignore("NyxIsland is not in Build Settings. Run War of Nyx > Setup > Run Project Setup.");

            LogAssert.ignoreFailingMessages = true;
            OfflineMatchRequest.Reset();
            OfflineMatchRequest.Bots = bots;
            OfflineMatchRequest.Difficulty = BotDifficulty.Nightmare;
            OfflineMatchRequest.IncludeHuman = false;
            OfflineMatchRequest.Seed = 99;
            yield return SceneManager.LoadSceneAsync("NyxIsland", LoadSceneMode.Single);
            yield return null;

            var match = Object.FindAnyObjectByType<OfflineMatch>();
            Assert.IsNotNull(match);
            Assert.AreEqual(bots, match.BotCount, "this PC must allow the requested bot count");
            match.SimulationSpeed = 0f; // the test drives the ticks itself

            for (int i = 0; i < WarmupTicks; i++)
                match.Step();

            var samples = new double[MeasuredTicks];
            var watch = new Stopwatch();
            for (int i = 0; i < MeasuredTicks; i++)
            {
                watch.Restart();
                match.Step();
                watch.Stop();
                samples[i] = watch.Elapsed.TotalMilliseconds;
                if (i % 120 == 0)
                    yield return null; // let Unity breathe (physics sync, GC)
            }

            var sorted = samples.OrderBy(s => s).ToArray();
            double average = samples.Average();
            double p95 = sorted[(int)(sorted.Length * 0.95)];
            double max = sorted[sorted.Length - 1];
            int alive = match.Combatants.Count(c => c.Alive);
            Debug.Log($"[Perf] NYX ISLAND, {bots} NIGHTMARE bots: tick avg {average:0.000} ms, p95 {p95:0.000} ms, max {max:0.000} ms " +
                      $"over {MeasuredTicks} ticks (60 Hz budget 16.7 ms); alive after 34 s: {alive}/{bots}; " +
                      $"{SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB");
            Assert.Less(average, 8.0, "regression guard (desktop): AI + rules must stay far below one 60 Hz frame");
        }
    }
}
