using System;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Hunt;

namespace WarOfNyx.Tests
{
    /// <summary>Spec 13 "Required Test Cases" for the Nyx Hunt Protocol, plus the anti-exploit list.</summary>
    public class HuntProtocolTests
    {
        private const float Dt = 1f / 60f;
        private HuntSettings _s;

        [SetUp]
        public void SetUp()
        {
            _s = new HuntSettings { spawnGraceSeconds = 0f };
        }

        /// <summary>Runs one tracker for <paramref name="seconds"/> with positions from <paramref name="path"/> (time → x, z).</summary>
        private static (float damage, HuntEvents events) Run(HuntTracker t, ref float now, float seconds, Func<float, (float x, float z)> path)
        {
            float damage = 0f;
            var events = HuntEvents.None;
            int ticks = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < ticks; i++)
            {
                now += Dt;
                var p = path(now);
                var r = t.Update(now, p.x, p.z, Dt);
                damage += r.Damage;
                events |= r.Events;
            }
            return (damage, events);
        }

        private static Func<float, (float, float)> Still(float x = 0f, float z = 0f) => _ => (x, z);

        private HuntTracker New(float now = 0f) => new HuntTracker(_s, now, 0f, 0f, _s.spawnGraceSeconds);

        [Test]
        public void SettingsMatchTheSpec()
        {
            var s = new HuntSettings();
            Assert.IsNull(s.Validate());
            Assert.AreEqual(20f, s.warningSeconds);
            Assert.AreEqual(25f, s.finalWarningSeconds);
            Assert.AreEqual(30f, s.exposeSeconds);
            Assert.AreEqual(10f, s.exposureDurationSeconds);
            Assert.AreEqual(2f, s.damagePerSecond);
            Assert.AreEqual(8f, s.meaningfulNetDisplacement);
            Assert.AreEqual(12f, s.meaningfulPathDistance);
        }

        [Test]
        public void TenSecondsStillIsFine()
        {
            var t = New();
            float now = 0f;
            var r = Run(t, ref now, 10f, Still());
            Assert.AreEqual(0f, r.damage);
            Assert.AreEqual(HuntEvents.None, r.events);
            Assert.AreEqual(HuntState.Normal, t.Status.State);
        }

        [Test]
        public void WarningsAtTwentyAndTwentyFiveSeconds()
        {
            var t = New();
            float now = 0f;
            var r = Run(t, ref now, 20.05f, Still());
            Assert.IsTrue(r.events.HasFlag(HuntEvents.Warning), "20 s: WARNING: KEEP MOVING");
            Assert.AreEqual(HuntState.CampingWarning, t.Status.State);
            Assert.AreEqual(1, t.Status.WarningLevel);

            r = Run(t, ref now, 5f, Still());
            Assert.IsTrue(r.events.HasFlag(HuntEvents.FinalWarning), "25 s: NYX IS WATCHING YOU");
            Assert.AreEqual(2, t.Status.WarningLevel);
            Assert.AreEqual(0f, r.damage, "no penalty before 30 s");
        }

        [Test]
        public void ExposureAtThirtySecondsForTenSecondsWithPenalty()
        {
            var t = New();
            float now = 0f;
            Run(t, ref now, 29.9f, Still());
            Assert.AreNotEqual(HuntState.Exposed, t.Status.State);

            var r = Run(t, ref now, 0.2f, Still());
            Assert.IsTrue(r.events.HasFlag(HuntEvents.Exposed));
            Assert.IsTrue(t.Status.BeaconVisible);
            Assert.AreEqual(1, t.Status.ExposureCount);

            r = Run(t, ref now, 9.7f, Still());
            Assert.IsTrue(t.Status.BeaconVisible, "the beacon stays for the whole 10 s");
            Assert.AreEqual(2f * 9.7f, r.damage, 1.01f, "2 HP/s from the 30 s mark");

            r = Run(t, ref now, 0.5f, Still());
            Assert.IsTrue(r.events.HasFlag(HuntEvents.ExposureEnded));
            Assert.AreEqual(HuntState.CampingPenalty, t.Status.State, "beacon gone, penalty continues");
            Assert.Greater(r.damage, 0f);
        }

        [Test]
        public void RepeatedExposureFollowsThePolicyWithoutSpam()
        {
            var t = New();
            float now = 0f;
            int exposures = 0;
            for (int i = 0; i < (int)(90f / Dt); i++)
            {
                now += Dt;
                if (t.Update(now, 0f, 0f, Dt).Events.HasFlag(HuntEvents.Exposed))
                    exposures++;
            }
            // Exposed at 30, 50, 70, 90(-) → 3 within 90 s; one event each, never per frame.
            Assert.AreEqual(3, exposures);
            Assert.AreEqual(3, t.Status.ExposureCount);
        }

        [Test]
        public void MeaningfulMovementClearsEverything()
        {
            var t = New();
            float now = 0f;
            Run(t, ref now, 35f, Still());
            Assert.IsTrue(t.Status.PenaltyActive);

            // Walk 4 m/s in a straight line: 8 m net / 12 m path within 3-4 samples.
            float start = now;
            var r = Run(t, ref now, 5f, time => ((time - start) * 4f, 0f));
            Assert.IsTrue(r.events.HasFlag(HuntEvents.Cleared), "HUNT STATUS CLEARED");
            Assert.IsTrue(r.events.HasFlag(HuntEvents.ExposureEnded), "beacon removed");
            Assert.IsFalse(t.Status.PenaltyActive);

            float after = Run(t, ref now, 5f, time => ((time - start) * 4f, 0f)).damage;
            Assert.AreEqual(0f, after, "penalty stops");
            Assert.AreEqual(HuntState.Normal, t.Status.State, "RECOVERING → NORMAL");
            Assert.Less(t.InactiveSeconds(now), 2f, "tracking resumed");
        }

        [Test]
        public void RotatingCrouchingFiringDoNotCount()
        {
            // Only positions are tracked: an unchanged position is camping whatever else the player does.
            var t = New();
            float now = 0f;
            var r = Run(t, ref now, 40f, Still(3f, 3f));
            Assert.IsTrue(t.Status.PenaltyActive);
            Assert.Greater(r.damage, 0f);
        }

        [Test]
        public void MovingSlightlyAndStoppingDoesNotReset()
        {
            var t = New();
            float now = 0f;
            Run(t, ref now, 18f, Still());
            float start = now;
            Run(t, ref now, 1.5f, time => (Math.Min((time - start) * 4f, 5f), 0f)); // 5 m, then stop
            Run(t, ref now, 3f, Still(5f, 0f));
            Assert.AreEqual(HuntState.CampingWarning, t.Status.State, "5 m is not meaningful movement");
            Assert.Greater(t.InactiveSeconds(now), 22f);
        }

        [Test]
        public void CirclingInATinyAreaNeverEscapes()
        {
            var t = New();
            float now = 0f;
            // Running in a 2 m circle at 4 m/s: lots of path, almost no net displacement.
            var r = Run(t, ref now, 45f, time => (2f * MathF.Cos(time * 2f), 2f * MathF.Sin(time * 2f)));
            Assert.IsTrue(t.Status.PenaltyActive);
            Assert.Greater(r.damage, 0f);
        }

        [Test]
        public void MicroscopicJigglingNeverEscapes()
        {
            var t = New();
            float now = 0f;
            Run(t, ref now, 45f, time => ((int)(time * 7f) % 2 == 0 ? 0.3f : 0f, 0f));
            Assert.IsTrue(t.Status.PenaltyActive);
        }

        [Test]
        public void TeleportsCountForNothing()
        {
            var t = New();
            float now = 0f;
            Run(t, ref now, 32f, Still());
            // A forged/malformed jump of 200 m, then standing still again.
            Run(t, ref now, 6f, time => (200f, 0f));
            Assert.IsTrue(t.Status.PenaltyActive, "an implausible jump is not legitimate travel");
        }

        [Test]
        public void WalkingIntoAWallIsStillCamping()
        {
            // The server position doesn't change while pushing against a wall.
            var t = New();
            float now = 0f;
            Run(t, ref now, 31f, Still(10f, 0f));
            Assert.IsTrue(t.Status.BeaconVisible);
        }

        [Test]
        public void SpawnGraceDelaysTheTimer()
        {
            _s.spawnGraceSeconds = 5f;
            var t = New();
            float now = 0f;
            Run(t, ref now, 22f, Still());
            Assert.AreEqual(HuntState.Normal, t.Status.State, "warnings start 20 s after the grace");
            Run(t, ref now, 4f, Still());
            Assert.AreEqual(HuntState.CampingWarning, t.Status.State);
        }

        [Test]
        public void ReconnectingNeverResetsTheTimer()
        {
            var protocol = new NyxHuntProtocol(_s);
            float now = 0f;
            protocol.Track(7, now, 0f, 0f);
            for (; now < 32f; now += Dt)
                protocol.Update(7, now, 0f, 0f, Dt);
            Assert.IsTrue(protocol.TryGet(7, out var before) && before.Status.PenaltyActive);

            protocol.MarkDisconnected(7);
            Assert.AreEqual(0f, protocol.Update(7, now + 1f, 0f, 0f, Dt).Damage, "nothing happens while away");

            var after = protocol.Track(7, now + 2f, 0f, 0f);
            Assert.AreSame(before, after, "the same tracker continues");
            Assert.Greater(after.InactiveSeconds(now + 2f), 30f, "the camping timer was not reset");

            float graceDamage = 0f;
            float t = now + 2f;
            for (int i = 0; i < 60 * 4; i++, t += Dt)
                graceDamage += protocol.Update(7, t, 0f, 0f, Dt).Damage;
            Assert.AreEqual(0f, graceDamage, "brief reconnect grace pauses the penalty");
            float later = 0f;
            for (int i = 0; i < 60 * 3; i++, t += Dt)
                later += protocol.Update(7, t, 0f, 0f, Dt).Damage;
            Assert.Greater(later, 0f, "then the penalty resumes");
        }

        [Test]
        public void DeathRemovesTheBeacon()
        {
            var protocol = new NyxHuntProtocol(_s);
            protocol.Track(1, 0f, 0f, 0f);
            for (float now = 0f; now < 31f; now += Dt)
                protocol.Update(1, now, 0f, 0f, Dt);
            Assert.AreEqual(HuntEvents.Removed, protocol.Remove(1));
            Assert.IsFalse(protocol.TryGet(1, out _));
            Assert.AreEqual(0f, protocol.Update(1, 40f, 0f, 0f, Dt).Damage);
        }

        [Test]
        public void SeveralCampersAreTrackedSeparately()
        {
            var protocol = new NyxHuntProtocol(_s);
            protocol.Track(1, 0f, 0f, 0f);
            protocol.Track(2, 0f, 50f, 50f);
            protocol.Track(3, 0f, -50f, 0f);
            for (float now = 0f; now < 35f; now += Dt)
            {
                protocol.Update(1, now, 0f, 0f, Dt);                 // camps
                protocol.Update(2, now, 50f + now * 4f % 60f, 50f, Dt); // keeps running
                protocol.Update(3, now, -50f, 0f, Dt);               // camps
            }
            protocol.TryGet(1, out var a);
            protocol.TryGet(2, out var b);
            protocol.TryGet(3, out var c);
            Assert.IsTrue(a.Status.BeaconVisible);
            Assert.AreEqual(HuntState.Normal, b.Status.State);
            Assert.IsTrue(c.Status.BeaconVisible);
        }

        [Test]
        public void ZoneAndCampingStackOnceWithTheRightCause()
        {
            Assert.AreEqual(5f, EnvironmentDamage.Total(3f, 2f));
            Assert.AreEqual(2f, EnvironmentDamage.Total(0f, 2f));
            Assert.AreEqual(0f, EnvironmentDamage.Total(-1f, float.NaN), "garbage never heals or hurts");
            Assert.AreEqual(DamageKind.Camping, EnvironmentDamage.Cause(1f, 2f));
            Assert.AreEqual(DamageKind.Zone, EnvironmentDamage.Cause(3f, 2f));

            // Camping damage ignores armor, like zone damage.
            var h = HealthState.Full(100f, armorLevel: 3);
            Assert.AreEqual(5f, HealthRules.ApplyDamage(ref h, 5f, DamageKind.Camping, false).Applied);
        }

        [Test]
        public void FeedTextMatchesTheSpec()
        {
            Assert.AreEqual("HUNT ALERT: REN EXPOSED FOR CAMPING", HuntText.Alert("REN"));
            Assert.AreEqual("REN was taken by the Nyx Hunt", CombatText.Environmental("REN", DamageKind.Camping));
        }

        [Test]
        public void LocalNoticesFireOnceEach()
        {
            var a = new HuntStatusAnnouncer();
            var t = New();
            float now = 0f;
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < (int)(45f / Dt); i++)
            {
                now += Dt;
                t.Update(now, 0f, 0f, Dt);
                var h = a.Update(t.Status, alive: true);
                if (h != null) seen.Add(h);
            }
            CollectionAssert.AreEqual(new[] { HuntText.Warning, HuntText.FinalWarning, HuntText.YouAreExposed }, seen);

            float start = now;
            for (int i = 0; i < (int)(6f / Dt); i++)
            {
                now += Dt;
                t.Update(now, (now - start) * 4f, 0f, Dt);
                var h = a.Update(t.Status, alive: true);
                if (h != null) seen.Add(h);
            }
            Assert.AreEqual(HuntText.Cleared, seen[seen.Count - 1]);
            Assert.AreEqual(4, seen.Count);
        }
    }
}
