using System.Collections.Generic;
using NUnit.Framework;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Tests
{
    /// <summary>Phase 12: container configuration, process lifetime, health and telemetry output.</summary>
    public class ServerOperationsTests
    {
        [Test]
        public void EnvironmentVariablesConfigureContainersAndArgumentsWin()
        {
            var env = new Dictionary<string, string>
            {
                ["WON_SERVER"] = "1", ["WON_SESSION"] = "EU-01", ["WON_MAX_PLAYERS"] = "24", ["WON_HEALTH_PORT"] = "8080",
                ["WON_MAX_MATCHES"] = "1", ["WON_IDLE_SHUTDOWN"] = "300",
            };
            var o = ServerLaunchOptions.Parse(new[] { "-maxPlayers", "12" }, k => env.TryGetValue(k, out var v) ? v : null);
            Assert.IsTrue(o.IsServer);
            Assert.AreEqual("EU-01", o.SessionName);
            Assert.AreEqual(12, o.MaxPlayers, "the command line overrides the environment");
            Assert.AreEqual(8080, o.HealthPort);
            Assert.AreEqual(1, o.MaxMatches);
            Assert.AreEqual(300, o.IdleShutdownSeconds);
            StringAssert.Contains("healthPort=8080", o.ToString());
        }

        [Test]
        public void InvalidOperationalValuesAreReportedNotFatal()
        {
            var o = ServerLaunchOptions.Parse(new[] { "-server", "-healthPort", "70000", "-maxMatches", "-3", "-idleShutdown", "abc" });
            Assert.AreEqual(0, o.HealthPort);
            Assert.AreEqual(0, o.MaxMatches);
            Assert.AreEqual(0, o.IdleShutdownSeconds);
            StringAssert.Contains("healthPort", o.Warnings);
        }

        [Test]
        public void OneMatchContainersExitAfterTheirMatch()
        {
            var p = new ServerLifetimePolicy(maxMatches: 1, idleShutdownSeconds: 0f);
            p.Update(8, 10f);
            Assert.IsFalse(p.ShouldExit);
            p.OnMatchFinished();
            Assert.IsTrue(p.ShouldExit);
            StringAssert.Contains("1 match", p.ExitReason);
        }

        [Test]
        public void IdleServersShutDownOnlyWhenEmptyLongEnough()
        {
            var p = new ServerLifetimePolicy(0, 120f);
            p.Update(0, 0f);
            p.Update(0, 100f);
            Assert.IsFalse(p.ShouldExit);
            p.Update(1, 110f); // someone joined: timer resets
            p.Update(0, 115f);
            p.Update(0, 230f);
            Assert.IsFalse(p.ShouldExit);
            p.Update(0, 236f);
            Assert.IsTrue(p.ShouldExit);
            var forever = new ServerLifetimePolicy(0, 0f);
            forever.Update(0, 0f);
            forever.Update(0, 1e6f);
            forever.OnMatchFinished();
            Assert.IsFalse(forever.ShouldExit, "0 = unlimited / never idle out");
        }

        [Test]
        public void HealthAndTelemetryAreValidJsonWithoutInjection()
        {
            string health = ServerJson.Health(new ServerHealth { Status = "ok", Session = "WON\"DEV", Phase = "Active", Players = 7, MaxPlayers = 12, UptimeSeconds = 61.9f, Protocol = 10 });
            Assert.AreEqual("{\"status\":\"ok\",\"session\":\"WON\\\"DEV\",\"phase\":\"Active\",\"players\":7,\"maxPlayers\":12,\"matchesFinished\":0,\"uptimeSeconds\":61,\"protocol\":10}", health);

            string line = ServerJson.Event("match_end", 312.5f, ("winner", "KAI\n"), ("kills", 4), ("tieBreak", false), ("duration", 280.25f));
            Assert.AreEqual("{\"event\":\"match_end\",\"t\":312.5,\"winner\":\"KAI\\n\",\"kills\":4,\"tieBreak\":false,\"duration\":280.25}", line);
        }
    }
}
