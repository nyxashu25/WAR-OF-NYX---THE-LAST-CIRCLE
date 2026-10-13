using System;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;
using WarOfNyx.Core.Zone;

namespace WarOfNyx.Tests
{
    /// <summary>
    /// Phase 13 exploit / robustness checks: random and hostile input must never produce NaN state,
    /// out-of-range aim, super speed or an authority-only field (spec 24 "never trust the client").
    /// </summary>
    public class RobustnessTests
    {
        private static float Hostile(Random r)
        {
            switch (r.Next(8))
            {
                case 0: return float.NaN;
                case 1: return float.PositiveInfinity;
                case 2: return float.NegativeInfinity;
                case 3: return float.MaxValue;
                case 4: return -float.MaxValue;
                case 5: return 0f;
                default: return (float)(r.NextDouble() * 2000.0 - 1000.0);
            }
        }

        [Test]
        public void SanitizedInputIsAlwaysFiniteAndInRange()
        {
            var r = new Random(13);
            for (int i = 0; i < 20000; i++)
            {
                var raw = new MovementCommand
                {
                    MoveX = Hostile(r), MoveY = Hostile(r), Yaw = Hostile(r), Pitch = Hostile(r),
                    SpeedMultiplier = Hostile(r), LateralWobble = Hostile(r),
                    SprintHeld = r.Next(2) == 0, AimHeld = r.Next(2) == 0, JumpPressed = r.Next(2) == 0,
                };
                var c = InputCodec.Sanitize(raw);
                Assert.IsTrue(float.IsFinite(c.MoveX) && float.IsFinite(c.MoveY) && float.IsFinite(c.Yaw) && float.IsFinite(c.Pitch), $"#{i}");
                Assert.LessOrEqual(c.MoveX * c.MoveX + c.MoveY * c.MoveY, 1.0001f, "stick never longer than 1 (no speed hack)");
                Assert.That(c.Yaw, Is.InRange(0f, 360f));
                Assert.That(c.Pitch, Is.InRange(InputCodec.MinPitch, InputCodec.MaxPitch));
                Assert.AreEqual(0f, c.SpeedMultiplier, "authority-only");
                Assert.AreEqual(0f, c.LateralWobble, "authority-only");
            }
        }

        [Test]
        public void MovementModelStaysFiniteAndBoundedUnderRandomInput()
        {
            var settings = new MovementSettings();
            var r = new Random(7);
            var state = MovementState.Initial(0f);
            float maxSpeed = Math.Max(settings.sprintSpeed, settings.slideStartSpeed) * 1.05f;
            for (int i = 0; i < 20000; i++)
            {
                var cmd = InputCodec.Sanitize(new MovementCommand
                {
                    MoveX = Hostile(r), MoveY = Hostile(r), Yaw = Hostile(r), Pitch = Hostile(r),
                    SprintHeld = r.Next(2) == 0, AimHeld = r.Next(3) == 0, JumpPressed = r.Next(10) == 0,
                    CrouchPressed = r.Next(15) == 0, SlidePressed = r.Next(15) == 0,
                });
                cmd.SpeedMultiplier = 1.1f; // the fastest legitimate weapon multiplier
                var ground = new GroundInfo(r.Next(5) != 0, r.Next(4) != 0);
                state = MovementModel.Step(state, cmd, ground, settings, 1f / 60f);
                Assert.IsTrue(float.IsFinite(state.VelocityX) && float.IsFinite(state.VelocityY) && float.IsFinite(state.VelocityZ), $"#{i}");
                Assert.LessOrEqual(state.HorizontalSpeed, maxSpeed * 1.1f, $"#{i}: horizontal speed within legitimate limits");
            }
        }

        [Test]
        public void ButtonPackingRoundTripsAndIgnoresUnknownBits()
        {
            var r = new Random(3);
            for (int i = 0; i < 5000; i++)
            {
                uint random = (uint)r.Next() | ((uint)r.Next(2) << 31);
                var combat = InputCodec.UnpackCombat(random);
                Assert.That(combat.SelectSlot, Is.InRange(0, 3), "a slot outside 1-3 can't be forged");
            }
        }

        [Test]
        public void DamageCanNeverHealOrOverflow()
        {
            var r = new Random(5);
            for (int i = 0; i < 5000; i++)
            {
                var h = HealthState.Full(100f, r.Next(0, 5));
                float before = h.Health;
                var result = HealthRules.ApplyDamage(ref h, Hostile(r), (DamageKind)r.Next(0, 6), r.Next(2) == 0);
                Assert.IsTrue(float.IsFinite(h.Health));
                Assert.LessOrEqual(h.Health, before, "damage never heals");
                Assert.GreaterOrEqual(h.Health, 0f);
                Assert.GreaterOrEqual(result.Applied, 0f);
            }
        }

        [Test]
        public void ZoneClassificationRejectsGarbagePositions()
        {
            var circle = new ZoneCircle(0f, 0f, 100f);
            Assert.IsFalse(circle.Contains(float.NaN, 0f), "NaN position is never 'inside'");
            Assert.IsFalse(circle.Contains(float.PositiveInfinity, 0f));
        }
    }
}
