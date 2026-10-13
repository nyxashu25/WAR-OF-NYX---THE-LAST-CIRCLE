using System;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;
using WarOfNyx.Core.Zone;

namespace WarOfNyx.Tests
{
    public class ZonePlanTests
    {
        private readonly ZoneSettings _settings = new ZoneSettings();

        private sealed class HalfWater : IZoneCentreValidator
        {
            // Everything east of x = 0 is "water": only the west half is fair ground.
            public float PlayableFraction(float x, float z, float radius)
            {
                int good = 0, total = 0;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * MathF.PI / 8f;
                    total++;
                    if (x + MathF.Sin(a) * radius <= 0f)
                        good++;
                }
                if (x <= 0f) good++;
                return (good) / (float)(total + 1);
            }
        }

        [Test]
        public void SpecTableIsTheDefault()
        {
            var s = _settings.stages;
            Assert.AreEqual(9, s.Length);
            float[] radii = { 500, 400, 310, 230, 160, 105, 65, 35, 12, 0 };
            float[] damage = { 1, 1, 1, 2, 2, 2, 4, 4, 6 };
            for (int i = 0; i < s.Length; i++)
            {
                Assert.AreEqual(radii[i], s[i].startRadius, $"phase {i + 1} start");
                Assert.AreEqual(radii[i + 1], s[i].targetRadius, $"phase {i + 1} target");
                Assert.AreEqual(60f, s[i].durationSeconds, "spec 10: 60 s per phase");
                Assert.AreEqual(damage[i], s[i].damagePerSecond, $"phase {i + 1} zone damage");
            }
            Assert.IsNull(_settings.Validate());
            Assert.AreEqual(540f, _settings.TotalSeconds);
        }

        [Test]
        public void InvalidTablesAreRejected()
        {
            var growing = _settings.Clone();
            growing.stages[2].targetRadius = 999f;
            Assert.IsNotNull(growing.Validate());

            var gap = _settings.Clone();
            gap.stages[3].startRadius = 200f;
            Assert.IsNotNull(gap.Validate(), "each phase starts where the previous one ended");

            Assert.Throws<ArgumentException>(() => ZonePlanner.Plan(growing, 0f, 0f, 500f, 1));
        }

        [Test]
        public void EveryCircleIsInsideThePreviousOne()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var plan = ZonePlanner.Plan(_settings, 10f, -20f, 500f, seed);
                Assert.AreEqual(10, plan.Circles.Length);
                Assert.AreEqual(new ZoneCircle(10f, -20f, 500f), plan.Start, "first circle = the map's starting safe zone");
                for (int i = 1; i < plan.Circles.Length; i++)
                {
                    Assert.IsTrue(plan.Circles[i - 1].ContainsCircle(plan.Circles[i]), $"seed {seed} circle {i}");
                    Assert.AreEqual(_settings.stages[i - 1].targetRadius, plan.Circles[i].Radius, 0.001f);
                }
            }
        }

        [Test]
        public void RadiiScaleToSmallMaps()
        {
            var plan = ZonePlanner.Plan(_settings, 0f, 0f, 55f, 7);
            Assert.AreEqual(0.11f, plan.Scale, 1e-4f);
            Assert.AreEqual(55f, plan.Circles[0].Radius, 1e-3f);
            Assert.AreEqual(400f * 0.11f, plan.Circles[1].Radius, 1e-3f);
            Assert.AreEqual(12f * 0.11f, plan.Circles[8].Radius, 1e-3f);
            Assert.AreEqual(0f, plan.Circles[9].Radius);
        }

        [Test]
        public void PlansAreDeterministicPerSeed()
        {
            var a = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 42);
            var b = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 42);
            var c = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 43);
            CollectionAssert.AreEqual(a.Circles, b.Circles);
            CollectionAssert.AreNotEqual(a.Circles, c.Circles);
        }

        [Test]
        public void CentresAvoidUnfairGround()
        {
            var validator = new HalfWater();
            int fair = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var plan = ZonePlanner.Plan(_settings, 0f, 0f, 500f, seed, validator);
                // From phase 4 on the circles are small enough to fit entirely on the west half.
                for (int i = 4; i < plan.Circles.Length; i++)
                    Assert.LessOrEqual(plan.Circles[i].X, 0.01f, $"seed {seed} circle {i} centre is over water");
                if (plan.Circles[9].X <= 0f)
                    fair++;
            }
            Assert.AreEqual(200, fair);
        }
    }

    public class ZoneTimelineTests
    {
        private readonly ZoneSettings _settings = new ZoneSettings();
        private ZonePlan _plan;
        private const float Start = 100f; // "FIGHT!" at server time 100 s

        [SetUp]
        public void SetUp() => _plan = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 3);

        private ZoneCircle At(float t) => ZoneTimeline.Evaluate(_plan, _settings, Start, t).CircleAt(t);

        [Test]
        public void PhasesLastSixtySecondsAndShrinkLinearly()
        {
            var first = ZoneTimeline.Evaluate(_plan, _settings, Start, Start);
            Assert.AreEqual(0, first.Index);
            Assert.AreEqual(Start, first.StartsAt);
            Assert.AreEqual(Start + 60f, first.EndsAt);
            Assert.AreEqual(500f, At(Start).Radius, 1e-3f);
            Assert.AreEqual(450f, At(Start + 30f).Radius, 1e-3f, "half way through phase 1");

            var second = ZoneTimeline.Evaluate(_plan, _settings, Start, Start + 60f);
            Assert.AreEqual(1, second.Index, "phase 2 starts exactly when phase 1 ends");
            Assert.AreEqual(400f, At(Start + 60f).Radius, 1e-3f);
            Assert.AreEqual(_plan.Circles[1], second.From);
            Assert.AreEqual(_plan.Circles[2], second.To, "the next circle is announced at the start of the phase");
        }

        [Test]
        public void FinalCircleClosesAndStaysClosed()
        {
            var final = ZoneTimeline.Evaluate(_plan, _settings, Start, Start + 8 * 60f + 1f);
            Assert.IsTrue(final.IsFinal);
            Assert.AreEqual(6f, final.DamagePerSecond);
            Assert.AreEqual(Start + 8 * 60f, final.FinalStartsAt);

            var closed = ZoneTimeline.Evaluate(_plan, _settings, Start, Start + 9 * 60f + 500f);
            Assert.IsTrue(closed.Finished);
            Assert.AreEqual(0f, At(Start + 9 * 60f + 500f).Radius);
            Assert.AreEqual(6f, closed.DamagePerSecond, "the closed circle keeps the final damage");
            Assert.IsFalse(At(Start + 2000f).Contains(_plan.Circles[9].X, _plan.Circles[9].Z), "nobody is inside a closed circle");
        }

        [Test]
        public void RadiusNeverGrowsAndTheCircleAlwaysContainsItsTarget()
        {
            float previous = float.MaxValue;
            for (float t = Start - 5f; t < Start + 560f; t += 0.37f)
            {
                var stage = ZoneTimeline.Evaluate(_plan, _settings, Start, t);
                var c = stage.CircleAt(t);
                Assert.LessOrEqual(c.Radius, previous + 1e-3f, $"t={t}");
                Assert.IsTrue(c.ContainsCircle(stage.To, 0.01f), $"t={t}: the shrinking circle must contain its target");
                previous = c.Radius;
            }
        }

        [Test]
        public void BeforeTheStartTheFirstCircleWaits()
        {
            var settings = _settings.Clone();
            settings.delayBeforeFirstPhase = 20f;
            var stage = ZoneTimeline.Evaluate(_plan, settings, Start, Start + 5f);
            Assert.IsFalse(stage.Shrinking(Start + 5f));
            Assert.AreEqual(500f, stage.CircleAt(Start + 5f).Radius);
            Assert.AreEqual(15f, stage.SecondsRemaining(Start + 5f), 1e-3f, "counts down to the first shrink");
        }

        [Test]
        public void ReplicatedStageGivesTheSameCircleOnEveryPeer()
        {
            // A client only knows the replicated stage values; it must derive the server's circle.
            for (float t = Start; t < Start + 540f; t += 7.3f)
            {
                var server = ZoneTimeline.Evaluate(_plan, _settings, Start, t);
                var replicated = new ZoneStage
                {
                    Index = server.Index, Count = server.Count, From = server.From, To = server.To,
                    StartsAt = server.StartsAt, EndsAt = server.EndsAt,
                };
                Assert.AreEqual(server.CircleAt(t), replicated.CircleAt(t));
            }
        }

        [Test]
        public void TestOverrideShortensEveryPhase()
        {
            var fast = _settings.WithStageSeconds(20f);
            Assert.AreEqual(180f, fast.TotalSeconds);
            Assert.AreEqual(60f, _settings.stages[0].durationSeconds, "the configured table is not modified");
            var stage = ZoneTimeline.Evaluate(_plan, fast, Start, Start + 25f);
            Assert.AreEqual(1, stage.Index);
        }
    }

    public class OutsideCircleRulesTests
    {
        private static readonly ZoneCircle Circle = new ZoneCircle(0f, 0f, 50f);

        [Test]
        public void BoundaryClassification()
        {
            Assert.IsTrue(ZoneRules.IsInside(true, Circle, 0f, 0f));
            Assert.IsTrue(ZoneRules.IsInside(true, Circle, 50f, 0f), "on the edge counts as inside");
            Assert.IsFalse(ZoneRules.IsInside(true, Circle, 50.01f, 0f));
            Assert.IsTrue(ZoneRules.IsInside(false, Circle, 900f, 900f), "no circle yet (lobby/countdown): everyone is inside");
            Assert.AreEqual(10f, Circle.DistanceToEdge(60f, 0f), 1e-4f);
            Assert.AreEqual(-50f, Circle.DistanceToEdge(0f, 0f), 1e-4f);
        }

        [Test]
        public void GunFiresInsideButNotOutsideAndAmmoIsKept()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = WeaponState.Create(WeaponCatalog.NetworkIdOf(w.id));

            bool inside = ZoneRules.FirearmsAllowed(true, Circle, 10f, 0f);
            Assert.IsTrue(WeaponRules.TryFire(ref s, w, 0f, true, true, false, inside, 90).Fired, "a gun fires inside the circle");
            int magazine = s.Magazine;

            bool outside = ZoneRules.FirearmsAllowed(true, Circle, 80f, 0f);
            for (int i = 1; i <= 30; i++)
            {
                var d = WeaponRules.TryFire(ref s, w, i * 0.2f, true, true, false, outside, 90);
                Assert.IsFalse(d.Fired, "a gun cannot fire outside");
                Assert.AreEqual(FireBlockReason.FirearmsDisabled, d.Reason);
                Assert.IsFalse(d.ShouldAutoReload);
            }
            Assert.AreEqual(magazine, s.Magazine, "invalid firing attempts consume no ammunition");

            Assert.IsTrue(WeaponRules.TryFire(ref s, w, 10f, true, true, false, ZoneRules.FirearmsAllowed(true, Circle, 0f, 0f), 90).Fired,
                "gun access returns inside");
        }

        [Test]
        public void HoldingTheTriggerWhileCrossingTheEdgeStopsAtTheEdge()
        {
            // Walk outward at 6 m/s with an automatic rifle held down, one server tick at a time.
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = WeaponState.Create(WeaponCatalog.NetworkIdOf(w.id));
            const float dt = 1f / 60f;
            float x = 45f;
            int shotsInside = 0, shotsOutside = 0;
            for (int tick = 0; tick < 120; tick++)
            {
                float now = tick * dt;
                x += 6f * dt;
                bool allowed = ZoneRules.FirearmsAllowed(true, Circle, x, 0f);
                if (WeaponRules.TryFire(ref s, w, now, true, false, false, allowed, 90).Fired)
                {
                    if (x <= Circle.Radius) shotsInside++;
                    else shotsOutside++;
                }
            }
            Assert.Greater(shotsInside, 0);
            Assert.AreEqual(0, shotsOutside, "not a single shot once the body is outside");
            Assert.AreEqual(w.magazineSize - shotsInside, s.Magazine);
        }

        [Test]
        public void LateReportsCannotBuyExtraShots()
        {
            // The server classifies the position it simulated THIS tick. A client that was inside a
            // moment ago (or whose packet arrives late) gets nothing: there is no grace window.
            var w = WeaponCatalog.Get(WeaponCatalog.PhantomMarksman);
            var s = WeaponState.Create(WeaponCatalog.NetworkIdOf(w.id));
            bool insideLastTick = ZoneRules.FirearmsAllowed(true, Circle, 49.9f, 0f);
            bool insideThisTick = ZoneRules.FirearmsAllowed(true, Circle, 50.1f, 0f);
            Assert.IsTrue(insideLastTick);
            Assert.IsFalse(WeaponRules.TryFire(ref s, w, 0f, false, true, false, insideThisTick, 30).Fired);
        }

        [Test]
        public void ClientsCannotForgeTheirZoneState()
        {
            // Input carries movement and buttons only: authority-only fields are wiped on arrival.
            var forged = new MovementCommand { MoveY = 1f, LateralWobble = 0.2f, SpeedMultiplier = 3f };
            var clean = InputCodec.Sanitize(forged);
            Assert.AreEqual(0f, clean.LateralWobble, "a client cannot add or cancel instability");
            Assert.AreEqual(0f, clean.SpeedMultiplier);
            var fields = typeof(MovementCommand).GetFields().Select(f => f.Name.ToLowerInvariant());
            Assert.IsFalse(fields.Any(f => f.Contains("inside") || f.Contains("zone")), "no inside-the-circle claim exists in the input");
        }

        [Test]
        public void MeleeWorksOutside()
        {
            Assert.IsTrue(ZoneRules.MeleeAllowedOutside);
            var katana = WeaponCatalog.Get(WeaponCatalog.Katana);
            var m = new MeleeState();
            // Melee rules take no zone flag at all: a swing outside resolves exactly as inside.
            Assert.IsTrue(MeleeRules.TryStartSwing(ref m, katana, 5f));
            Assert.IsTrue(MeleeRules.TryResolve(ref m, katana, 5f + katana.meleeWindup + 0.001f));
        }
    }

    public class ZoneDamageTests
    {
        private const float Dt = 1f / 60f;

        private static float Run(ref ZoneExposure e, bool inside, float dps, float seconds, float interval = 0.5f)
        {
            float total = 0f;
            int ticks = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < ticks; i++)
                total += ZoneDamage.Step(ref e, inside, dps, Dt, interval).Damage;
            return total;
        }

        [Test]
        public void DamagePerSecondMatchesThePhase()
        {
            foreach (float dps in new[] { 1f, 2f, 4f, 6f })
            {
                var e = new ZoneExposure();
                Assert.AreEqual(dps * 10f, Run(ref e, false, dps, 10f), 1e-3f, $"{dps} HP/s for 10 s");
            }
        }

        [Test]
        public void NoDamageInsideAndItStopsOnReEntry()
        {
            var e = new ZoneExposure();
            Assert.AreEqual(0f, Run(ref e, true, 6f, 30f));
            Run(ref e, false, 2f, 3f);
            var entered = ZoneDamage.Step(ref e, true, 2f, Dt, 0.5f);
            Assert.IsTrue(entered.Entered);
            Assert.AreEqual(0f, entered.Damage);
            Assert.AreEqual(0f, Run(ref e, true, 2f, 10f), "re-entry stops zone damage at once");
        }

        [Test]
        public void SteppingInAndOutCannotDodgeDamage()
        {
            // 0.25 s out, 0.25 s in, for 20 s: 10 s outside in total at 4 HP/s.
            var e = new ZoneExposure();
            float total = 0f;
            for (int i = 0; i < 40; i++)
            {
                total += Run(ref e, false, 4f, 0.25f);
                total += Run(ref e, true, 4f, 0.25f);
            }
            Assert.AreEqual(40f, total, 2.0001f, "exposure accumulates across short exits");
        }

        [Test]
        public void ZoneDamageIgnoresArmorAndIsAppliedOncePerTick()
        {
            var h = HealthState.Full(100f, armorLevel: 3);
            var r = HealthRules.ApplyDamage(ref h, 6f, DamageKind.Zone, spawnProtected: true);
            Assert.AreEqual(6f, r.Applied, "armor and spawn protection don't reduce zone damage");

            var e = new ZoneExposure();
            var step = ZoneDamage.Step(ref e, false, 6f, 0.5f, 0.5f);
            Assert.AreEqual(3f, step.Damage, 1e-4f);
            var huge = ZoneDamage.Step(ref e, false, 6f, 2f, 0.5f); // a long hitch is charged, not lost
            Assert.AreEqual(12f, huge.Damage, 1e-4f);
        }

        [Test]
        public void LeavingAndEnteringAreReportedOnce()
        {
            var e = new ZoneExposure();
            Assert.IsTrue(ZoneDamage.Step(ref e, false, 1f, Dt, 0.5f).Left);
            Assert.IsFalse(ZoneDamage.Step(ref e, false, 1f, Dt, 0.5f).Left);
            Assert.IsTrue(ZoneDamage.Step(ref e, true, 1f, Dt, 0.5f).Entered);
            Assert.IsFalse(ZoneDamage.Step(ref e, true, 1f, Dt, 0.5f).Entered);
        }
    }

    public class ZoneInstabilityTests
    {
        private readonly ZoneSettings _settings = new ZoneSettings();
        private const float Dt = 1f / 60f;

        [Test]
        public void WobbleStaysWithinFiveToEightPercent()
        {
            float peak = 0f;
            for (int player = 0; player < 20; player++)
            {
                for (int tick = 0; tick < 6000; tick++)
                {
                    float w = ZoneInstability.Lateral(tick, Dt, player, _settings);
                    Assert.LessOrEqual(Math.Abs(w), _settings.wobbleMax + 1e-6f);
                    peak = Math.Max(peak, Math.Abs(w));
                }
            }
            Assert.GreaterOrEqual(peak, _settings.wobbleMin, "the wobble is actually felt");
        }

        [Test]
        public void WobbleIsDeterministicPerTickAndPlayer()
        {
            Assert.AreEqual(ZoneInstability.Lateral(1234, Dt, 7, _settings), ZoneInstability.Lateral(1234, Dt, 7, _settings));
            Assert.AreNotEqual(ZoneInstability.Lateral(1234, Dt, 7, _settings), ZoneInstability.Lateral(1234, Dt, 8, _settings),
                "players wobble out of step with each other");
        }

        [Test]
        public void WobbleBendsDirectionButKeepsSpeedAndNeverMovesAStandingPlayer()
        {
            var settings = new MovementSettings();
            var straight = new MovementCommand { MoveY = 1f };
            var bent = new MovementCommand { MoveY = 1f, LateralWobble = 0.08f };
            var a = MovementState.Initial(0f);
            var b = MovementState.Initial(0f);
            var ground = new GroundInfo(true, true);
            for (int i = 0; i < 120; i++)
            {
                a = MovementModel.Step(a, straight, ground, settings, Dt);
                b = MovementModel.Step(b, bent, ground, settings, Dt);
            }
            Assert.AreEqual(a.HorizontalSpeed, b.HorizontalSpeed, 1e-3f, "no speed change");
            Assert.AreEqual(0f, a.VelocityX, 1e-4f);
            Assert.Greater(b.VelocityX, 0.3f, "bent to the right");
            float angle = MathF.Atan2(b.VelocityX, b.VelocityZ) * 180f / MathF.PI;
            Assert.That(angle, Is.InRange(4f, 5f), "8 % lateral = about 4.6 degrees, never a hard loss of control");

            var idle = MovementState.Initial(0f);
            for (int i = 0; i < 60; i++)
                idle = MovementModel.Step(idle, new MovementCommand { LateralWobble = 0.08f }, ground, settings, Dt);
            Assert.AreEqual(0f, idle.HorizontalSpeed, 1e-5f, "no drift without input (no forced movement or teleport)");
        }

        [Test]
        public void AimSwayIsSmall()
        {
            for (int tick = 0; tick < 3000; tick += 7)
            {
                ZoneInstability.AimSway(tick, Dt, 3, _settings, out float yaw, out float pitch);
                Assert.LessOrEqual(Math.Abs(yaw), _settings.aimSwayYawDegrees + 1e-5f);
                Assert.LessOrEqual(Math.Abs(pitch), _settings.aimSwayPitchDegrees + 1e-5f);
            }
        }
    }

    public class ZoneAnnouncerTests
    {
        private readonly ZoneSettings _settings = new ZoneSettings();

        private ZoneView View(ZonePlan plan, float t, bool outside = false, bool alive = true)
        {
            var stage = ZoneTimeline.Evaluate(plan, _settings, 0f, t);
            return new ZoneView { Active = true, Stage = stage, Current = stage.CircleAt(t), Time = t, PlayerOutside = outside, PlayerAlive = alive };
        }

        [Test]
        public void EachNoticeFiresOnce()
        {
            var plan = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 1);
            var announcer = new ZoneAnnouncer(_settings.finalWarningSeconds);
            var all = new System.Collections.Generic.List<ZoneAnnouncement>();
            for (float t = 0f; t < 600f; t += 0.25f)
                all.AddRange(announcer.Update(View(plan, t)));

            Assert.AreEqual(8, all.Count(a => a.Kind == ZoneAnnouncementKind.PhaseStarted && a.Headline), "phases 1-8 announce shrinking");
            Assert.AreEqual(1, all.Count(a => a.Kind == ZoneAnnouncementKind.FinalWarning));
            Assert.AreEqual("FINAL CIRCLE BEGINS IN 10 SECONDS", all.Single(a => a.Kind == ZoneAnnouncementKind.FinalWarning).Text);
            Assert.AreEqual(1, all.Count(a => a.Kind == ZoneAnnouncementKind.FinalStarted));
            Assert.AreEqual(1, all.Count(a => a.Kind == ZoneAnnouncementKind.CircleClosed));
        }

        [Test]
        public void LeavingAndReturningAreAnnounced()
        {
            var plan = ZonePlanner.Plan(_settings, 0f, 0f, 500f, 1);
            var announcer = new ZoneAnnouncer();
            announcer.Update(View(plan, 1f));
            var left = announcer.Update(View(plan, 2f, outside: true));
            Assert.AreEqual(ZoneAnnouncer.FirearmsDisabledText, left.Single().Text);
            Assert.AreEqual(0, announcer.Update(View(plan, 3f, outside: true)).Count, "no repeats while outside");
            var back = announcer.Update(View(plan, 4f));
            Assert.AreEqual(ZoneAnnouncer.SafeZoneEnteredText, back.Single().Text);
            announcer.Update(View(plan, 5f, outside: true));
            Assert.AreEqual(0, announcer.Update(View(plan, 6f, outside: true, alive: false)).Count,
                "an eliminated player gets no SAFE ZONE ENTERED");
        }
    }
}
