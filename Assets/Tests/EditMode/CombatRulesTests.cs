using System;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Tests
{
    public class WeaponCatalogTests
    {
        [TestCase(WeaponCatalog.Nx7AssaultRifle, 24f, 30, 1)]
        [TestCase(WeaponCatalog.ShadowSmg, 16f, 40, 1)]
        [TestCase(WeaponCatalog.CrimsonShotgun, 12f, 6, 8)]
        [TestCase(WeaponCatalog.PhantomMarksman, 55f, 10, 1)]
        [TestCase(WeaponCatalog.StormBurstRifle, 21f, 24, 1)]
        [TestCase(WeaponCatalog.NeonPistol, 26f, 12, 1)]
        public void SpecTableValues(string id, float damage, int magazine, int pellets)
        {
            var w = WeaponCatalog.Get(id);
            Assert.IsNotNull(w, id);
            Assert.AreEqual(damage, w.damage);
            Assert.AreEqual(magazine, w.magazineSize);
            Assert.AreEqual(pellets, w.pellets);
            Assert.IsTrue(w.IsRanged);
        }

        [Test]
        public void AtLeastSixRangedAndTwoMelee()
        {
            Assert.GreaterOrEqual(WeaponCatalog.All.Count(w => w.IsRanged), 6);
            Assert.GreaterOrEqual(WeaponCatalog.All.Count(w => w.IsMelee), 2);
            foreach (var id in new[] { WeaponCatalog.Katana, WeaponCatalog.CombatKnife, WeaponCatalog.EnergyBlade })
                Assert.IsTrue(WeaponCatalog.Get(id).IsMelee, id);
        }

        [Test]
        public void SustainedFireAlwaysBuildsBloom()
        {
            foreach (var w in WeaponCatalog.All.Where(w => w.IsRanged && w.bloomPerShot > 0f))
            {
                float shotsPerSecond = w.fireMode == FireMode.Burst ? w.roundsPerSecond * w.burstCount : w.roundsPerSecond;
                Assert.Less(w.bloomRecoveryPerSecond, w.bloomPerShot * shotsPerSecond, w.id);
            }
        }

        [Test]
        public void NetworkIdsRoundTripAndAreStable()
        {
            for (short i = 0; i < WeaponCatalog.All.Length; i++)
                Assert.AreEqual(i, WeaponCatalog.NetworkIdOf(WeaponCatalog.All[i].id));
            Assert.AreEqual(0, WeaponCatalog.NetworkIdOf(WeaponCatalog.Nx7AssaultRifle), "network ids are append-only");
            Assert.AreEqual(WeaponCatalog.NoWeapon, WeaponCatalog.NetworkIdOf("nope"));
            Assert.IsNull(WeaponCatalog.Get((short)999));
        }
    }

    public class WeaponRulesTests
    {
        private static WeaponState Fresh(string id) => WeaponState.Create(WeaponCatalog.NetworkIdOf(id));

        [Test]
        public void AutoRifleRespectsFireRate()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = Fresh(w.id);
            int shots = 0;
            for (float t = 0f; t < 1f; t += 1f / 60f)
                if (WeaponRules.TryFire(ref s, w, t, true, false, false, true, 90).Fired)
                    shots++;
            Assert.AreEqual(9, shots, "9 rounds per second at 60 Hz");
            Assert.AreEqual(30 - 9, s.Magazine);
        }

        [Test]
        public void ForgedRapidFireIsRejected()
        {
            // A client claiming to hold fire on every tick, or spamming presses, can't beat the server's rate.
            var w = WeaponCatalog.Get(WeaponCatalog.NeonPistol);
            var s = Fresh(w.id);
            int shots = 0;
            for (float t = 0f; t < 1f; t += 1f / 120f)
                if (WeaponRules.TryFire(ref s, w, t, true, true, false, true, 90).Fired)
                    shots++;
            Assert.AreEqual(5, shots);
        }

        [Test]
        public void SemiAutoNeedsAFreshPress()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.NeonPistol);
            var s = Fresh(w.id);
            Assert.IsTrue(WeaponRules.TryFire(ref s, w, 0f, true, true, false, true, 90).Fired);
            var held = WeaponRules.TryFire(ref s, w, 1f, true, false, false, true, 90);
            Assert.IsFalse(held.Fired);
            Assert.AreEqual(FireBlockReason.NoTrigger, held.Reason);
        }

        [Test]
        public void BurstFiresThreeShotsFromOnePress()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.StormBurstRifle);
            var s = Fresh(w.id);
            int shots = 0;
            for (float t = 0f; t < 0.3f; t += 1f / 60f)
                if (WeaponRules.TryFire(ref s, w, t, false, t == 0f, false, true, 90).Fired)
                    shots++;
            Assert.AreEqual(3, shots);
            Assert.AreEqual(21, s.Magazine);
        }

        [Test]
        public void ShotgunFiresEightPelletsForOneShell()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.CrimsonShotgun);
            var s = Fresh(w.id);
            var d = WeaponRules.TryFire(ref s, w, 0f, true, true, false, true, 90);
            Assert.IsTrue(d.Fired);
            Assert.AreEqual(8, d.Pellets);
            Assert.AreEqual(5, s.Magazine);
        }

        [Test]
        public void BlockedAttemptsNeverConsumeAmmo()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = Fresh(w.id);

            var outside = WeaponRules.TryFire(ref s, w, 0f, true, true, false, firearmsAllowed: false, reserveAvailable: 90);
            Assert.AreEqual(FireBlockReason.FirearmsDisabled, outside.Reason);
            Assert.AreEqual(30, s.Magazine, "spec 11: no ammo consumed outside the circle");

            WeaponRules.TryFire(ref s, w, 0f, true, true, false, true, 90);
            var cooldown = WeaponRules.TryFire(ref s, w, 0.01f, true, true, false, true, 90);
            Assert.AreEqual(FireBlockReason.Cooldown, cooldown.Reason);
            Assert.AreEqual(29, s.Magazine);
        }

        [Test]
        public void EmptyMagazineAsksForReloadAndReloadDrawsFromTheAmmoPool()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.NeonPistol);
            var s = Fresh(w.id);
            s.Magazine = 0;
            int pool = 48;
            var d = WeaponRules.TryFire(ref s, w, 0f, true, true, false, true, pool);
            Assert.AreEqual(FireBlockReason.EmptyMagazine, d.Reason);
            Assert.IsTrue(d.ShouldAutoReload);
            Assert.IsFalse(WeaponRules.TryFire(ref s, w, 0f, true, true, false, true, 0).ShouldAutoReload, "no spare ammo: no reload");

            Assert.IsTrue(WeaponRules.TryStartReload(ref s, w, 0f, pool));
            Assert.AreEqual(FireBlockReason.Reloading, WeaponRules.TryFire(ref s, w, 0.5f, true, true, false, true, pool).Reason);
            Assert.IsFalse(WeaponRules.UpdateReload(ref s, w, 1.39f, ref pool));
            Assert.IsTrue(WeaponRules.UpdateReload(ref s, w, 1.4f, ref pool));
            Assert.AreEqual(12, s.Magazine);
            Assert.AreEqual(48 - 12, pool);
        }

        [Test]
        public void PartialReserveFillsWhatItCan()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = Fresh(w.id);
            s.Magazine = 5;
            int pool = 10;
            WeaponRules.TryStartReload(ref s, w, 0f, pool);
            WeaponRules.UpdateReload(ref s, w, 10f, ref pool);
            Assert.AreEqual(15, s.Magazine);
            Assert.AreEqual(0, pool);
        }

        [Test]
        public void ReloadRefusedWhenPointless()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var full = Fresh(w.id);
            Assert.IsFalse(WeaponRules.TryStartReload(ref full, w, 0f, 90), "magazine already full");

            var dry = Fresh(w.id);
            dry.Magazine = 3;
            Assert.IsFalse(WeaponRules.TryStartReload(ref dry, w, 0f, 0), "no spare ammo");

            var knife = Fresh(WeaponCatalog.CombatKnife);
            Assert.IsFalse(WeaponRules.TryStartReload(ref knife, WeaponCatalog.Get(WeaponCatalog.CombatKnife), 0f, 90));
        }

        [Test]
        public void CancelledReloadGivesNoAmmo()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = Fresh(w.id);
            s.Magazine = 0;
            int pool = 90;
            WeaponRules.TryStartReload(ref s, w, 0f, pool);
            WeaponRules.CancelReload(ref s);
            WeaponRules.UpdateReload(ref s, w, 10f, ref pool);
            Assert.AreEqual(0, s.Magazine);
            Assert.AreEqual(90, pool);
        }

        [Test]
        public void SpreadGrowsWithSustainedFireAndRecovers()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            var s = Fresh(w.id);
            float first = WeaponRules.CurrentSpread(s, w, 0f, aiming: false);
            for (int i = 0; i < 10; i++)
                WeaponRules.TryFire(ref s, w, i * 0.12f, true, false, false, true, 90);
            float sustained = WeaponRules.CurrentSpread(s, w, 1.2f, false);
            Assert.Greater(sustained, first);
            Assert.LessOrEqual(sustained, w.hipSpread + w.maxBloom + 1e-4f);
            Assert.AreEqual(first, WeaponRules.CurrentSpread(s, w, 5f, false), 1e-4f, "bloom recovers");
            Assert.Less(WeaponRules.CurrentSpread(s, w, 5f, aiming: true), first, "aiming is more accurate");
        }

        [Test]
        public void DamageFallsOffWithDistance()
        {
            var w = WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle);
            Assert.AreEqual(24f, WeaponRules.DamageAt(w, 10f, false));
            Assert.AreEqual(24f * 0.8f, WeaponRules.DamageAt(w, 55f, false), 1e-3f, "halfway through falloff");
            Assert.AreEqual(24f * 0.6f, WeaponRules.DamageAt(w, 100f, false), 1e-3f);
            Assert.AreEqual(0f, WeaponRules.DamageAt(w, 151f, false), "beyond max range");
            Assert.AreEqual(36f, WeaponRules.DamageAt(w, 10f, true), "headshot x1.5");
        }

        /// <summary>Share of a full-auto spray that lands on a standing player (0.7 m wide, 1.5 m tall body) at a distance.</summary>
        private static float SprayHitShare(string weaponId, float distance, bool aiming, int rounds)
        {
            var w = WeaponCatalog.Get(weaponId);
            var s = Fresh(w.id);
            float halfWidth = (float)(Math.Atan(0.35 / distance) * 180.0 / Math.PI);
            float halfHeight = (float)(Math.Atan(0.75 / distance) * 180.0 / Math.PI);
            int hits = 0, pellets = 0;
            float now = 0f;
            for (int shot = 0; pellets < rounds && shot < 1000; shot++, now += w.FireInterval + 0.001f)
            {
                var d = WeaponRules.TryFire(ref s, w, now, true, true, aiming, true, 999);
                if (!d.Fired)
                    continue;
                for (int p = 0; p < d.Pellets; p++, pellets++)
                {
                    WeaponRules.SpreadOffset(7, d.ShotIndex, p, d.SpreadDegrees, out float yaw, out float pitch);
                    if (Math.Abs(yaw) <= halfWidth && Math.Abs(pitch) <= halfHeight)
                        hits++;
                }
            }
            return hits / (float)pellets;
        }

        [Test]
        public void Sprays_PutAGoodShareOfBulletsOnTarget()
        {
            // "In a spray, some bullets must land": hip fire at mid range, aimed fire at long range.
            Assert.That(SprayHitShare(WeaponCatalog.Nx7AssaultRifle, 20f, aiming: false, 30), Is.GreaterThan(0.45f), "AR hip, 20 m");
            Assert.That(SprayHitShare(WeaponCatalog.Nx7AssaultRifle, 50f, aiming: true, 30), Is.GreaterThan(0.45f), "AR aimed, 50 m");
            Assert.That(SprayHitShare(WeaponCatalog.ShadowSmg, 15f, aiming: false, 40), Is.GreaterThan(0.45f), "SMG hip, 15 m");
            Assert.That(SprayHitShare(WeaponCatalog.CrimsonShotgun, 8f, aiming: false, 8), Is.GreaterThan(0.5f), "shotgun pellets, 8 m");
            Assert.That(SprayHitShare(WeaponCatalog.PhantomMarksman, 120f, aiming: true, 1), Is.EqualTo(1f), "first aimed marksman shot, 120 m");
            // ...but not laser beams: long-range hip spray still misses a lot.
            Assert.That(SprayHitShare(WeaponCatalog.Nx7AssaultRifle, 80f, aiming: false, 30), Is.LessThan(0.6f), "AR hip, 80 m");
        }

        [Test]
        public void SpreadIsDeterministicAndBounded()
        {
            WeaponRules.SpreadOffset(3, 17, 2, 4f, out float y1, out float p1);
            WeaponRules.SpreadOffset(3, 17, 2, 4f, out float y2, out float p2);
            Assert.AreEqual(y1, y2);
            Assert.AreEqual(p1, p2);

            int distinct = 0;
            for (int pellet = 0; pellet < 8; pellet++)
            {
                WeaponRules.SpreadOffset(3, 17, pellet, 4f, out float y, out float p);
                Assert.LessOrEqual(Math.Sqrt(y * y + p * p), 4.0001);
                if (pellet > 0 && (Math.Abs(y - y1) > 1e-4f || Math.Abs(p - p1) > 1e-4f))
                    distinct++;
            }
            Assert.Greater(distinct, 5, "pellets spread out");

            WeaponRules.SpreadOffset(3, 17, 0, 0f, out float zy, out float zp);
            Assert.AreEqual(0f, zy);
            Assert.AreEqual(0f, zp);
        }
    }

    public class HealthRulesTests
    {
        [Test]
        public void DamageReducesHealthAndKillsAtZero()
        {
            var h = HealthState.Full(100f);
            var r = HealthRules.ApplyDamage(ref h, 24f, DamageKind.Weapon, false);
            Assert.AreEqual(24f, r.Applied);
            Assert.AreEqual(76f, h.Health);
            Assert.IsFalse(r.Killed);

            r = HealthRules.ApplyDamage(ref h, 500f, DamageKind.Weapon, false);
            Assert.AreEqual(76f, r.Applied, "overkill is clamped for damage statistics");
            Assert.IsTrue(r.Killed);
            Assert.IsFalse(h.Alive);
            Assert.AreEqual(0f, h.Health);
        }

        [Test]
        public void DeadPlayersTakeNoMoreDamage()
        {
            var h = HealthState.Full(100f);
            HealthRules.ApplyDamage(ref h, 100f, DamageKind.Weapon, false);
            var r = HealthRules.ApplyDamage(ref h, 10f, DamageKind.Weapon, false);
            Assert.AreEqual(DamageBlockReason.AlreadyDead, r.Blocked);
            Assert.IsFalse(r.Killed, "a single elimination per death");
        }

        [Test]
        public void SpawnProtectionBlocksPlayerDamageOnly()
        {
            var h = HealthState.Full(100f);
            Assert.AreEqual(DamageBlockReason.SpawnProtected, HealthRules.ApplyDamage(ref h, 50f, DamageKind.Weapon, true).Blocked);
            Assert.AreEqual(DamageBlockReason.SpawnProtected, HealthRules.ApplyDamage(ref h, 50f, DamageKind.Melee, true).Blocked);
            Assert.AreEqual(100f, h.Health);
            HealthRules.ApplyDamage(ref h, 5f, DamageKind.Zone, true);
            Assert.AreEqual(95f, h.Health);
        }

        [TestCase(0, 24f)]
        [TestCase(1, 20.4f)]
        [TestCase(2, 18f)]
        [TestCase(3, 15.6f)]
        public void ArmorReducesWeaponDamage(int level, float expected)
        {
            var h = HealthState.Full(100f, level);
            Assert.AreEqual(expected, HealthRules.ApplyDamage(ref h, 24f, DamageKind.Weapon, false).Applied, 1e-3f);
        }

        [Test]
        public void ArmorDoesNotReduceZoneOrCampingDamage()
        {
            var h = HealthState.Full(100f, 3);
            Assert.AreEqual(6f, HealthRules.ApplyDamage(ref h, 6f, DamageKind.Zone, false).Applied);
            Assert.AreEqual(2f, HealthRules.ApplyDamage(ref h, 2f, DamageKind.Camping, false).Applied);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-5f)]
        [TestCase(0f)]
        public void InvalidDamageIsIgnored(float amount)
        {
            var h = HealthState.Full(100f);
            Assert.AreEqual(DamageBlockReason.InvalidAmount, HealthRules.ApplyDamage(ref h, amount, DamageKind.Weapon, false).Blocked);
            Assert.AreEqual(100f, h.Health);
        }

        [Test]
        public void HealingIsCappedAtMax()
        {
            var h = HealthState.Full(100f);
            HealthRules.ApplyDamage(ref h, 30f, DamageKind.Weapon, false);
            Assert.AreEqual(30f, HealthRules.Heal(ref h, 75f));
            Assert.AreEqual(100f, h.Health);
        }
    }

    public class MeleeRulesTests
    {
        [Test]
        public void WindupThenSingleResolveThenCooldown()
        {
            var katana = WeaponCatalog.Get(WeaponCatalog.Katana);
            var m = new MeleeState();
            Assert.IsTrue(MeleeRules.TryStartSwing(ref m, katana, 1f));
            Assert.IsFalse(MeleeRules.TryResolve(ref m, katana, 1.1f), "still winding up");
            Assert.IsTrue(MeleeRules.TryResolve(ref m, katana, 1.22f));
            Assert.IsFalse(MeleeRules.TryResolve(ref m, katana, 1.3f), "resolves once");
            Assert.IsFalse(MeleeRules.TryStartSwing(ref m, katana, 1.5f), "cooldown");
            Assert.IsTrue(MeleeRules.TryStartSwing(ref m, katana, 1.87f));
            Assert.AreEqual(2, m.SwingCount);
        }

        /// <summary>Swings as fast as allowed: start, resolve, wait for recovery, start again.</summary>
        private static System.Collections.Generic.List<MeleePattern> Chain(WeaponStats w, int swings, float extraPause)
        {
            var m = new MeleeState();
            var patterns = new System.Collections.Generic.List<MeleePattern>();
            float now = 1f;
            for (int i = 0; i < swings; i++)
            {
                Assert.IsTrue(MeleeRules.TryStartSwing(ref m, w, now), $"swing {i}");
                patterns.Add(MeleeRules.Current(m, w).Pattern);
                now += 0.5f * w.meleeWindup;
                Assert.IsFalse(MeleeRules.TryResolve(ref m, w, now), "windup scales per attack");
                now = m.SwingStartedAt + w.meleeWindup * MeleeRules.Current(m, w).WindupMultiplier + 0.001f;
                Assert.IsTrue(MeleeRules.TryResolve(ref m, w, now));
                now = m.NextSwingAt + 0.01f + extraPause;
            }
            return patterns;
        }

        [Test]
        public void KatanaChainsFourDifferentAttacks_ThenStartsOver()
        {
            var katana = WeaponCatalog.Get(WeaponCatalog.Katana);
            var patterns = Chain(katana, 5, extraPause: 0f);
            CollectionAssert.AreEqual(new[]
            {
                MeleePattern.HorizontalSlash, MeleePattern.BackhandSlash, MeleePattern.OverheadStrike, MeleePattern.Thrust,
                MeleePattern.HorizontalSlash,
            }, patterns);
        }

        [Test]
        public void PausingTooLongRestartsTheCombo()
        {
            var katana = WeaponCatalog.Get(WeaponCatalog.Katana);
            var patterns = Chain(katana, 3, extraPause: MeleeRules.ComboWindowSeconds + 0.1f);
            CollectionAssert.AreEqual(new[] { MeleePattern.HorizontalSlash, MeleePattern.HorizontalSlash, MeleePattern.HorizontalSlash }, patterns);
        }

        [Test]
        public void EveryMeleeWeaponHasAComboWithDistinctPatterns()
        {
            foreach (var w in WeaponCatalog.All.Where(w => w.IsMelee))
            {
                var combo = MeleeRules.Combo(w);
                Assert.GreaterOrEqual(combo.Length, 3, w.id);
                Assert.AreEqual(combo.Length, combo.Select(a => a.Pattern).Distinct().Count(), w.id + " patterns differ");
                foreach (var a in combo)
                {
                    Assert.That(MeleeRules.Reach(w, a), Is.GreaterThan(1f), w.id);
                    Assert.That(a.ArcDegrees, Is.InRange(20f, 360f), w.id);
                    Assert.That(MeleeRules.Damage(w, a), Is.InRange(w.damage * 0.8f, w.damage * 1.5f), w.id);
                }
            }
            Assert.AreEqual(MeleeStyle.Knife, WeaponCatalog.Get(WeaponCatalog.CombatKnife).meleeStyle);
            Assert.AreEqual(MeleeStyle.Energy, WeaponCatalog.Get(WeaponCatalog.EnergyBlade).meleeStyle);
        }

        [Test]
        public void ThrustReachesFurther_SpinHitsBehind()
        {
            var katana = WeaponCatalog.Get(WeaponCatalog.Katana);
            var thrust = MeleeRules.Combo(katana)[3];
            Assert.Greater(MeleeRules.Reach(katana, thrust), katana.meleeReach);
            var energy = WeaponCatalog.Get(WeaponCatalog.EnergyBlade);
            var spin = MeleeRules.Combo(energy)[2];
            Assert.IsTrue(MeleeRules.InArc(0f, 0f, 0f, 0f, -1.5f, MeleeRules.Reach(energy, spin), spin.ArcDegrees), "spin reaches someone behind");
            Assert.IsFalse(MeleeRules.InArc(0f, 0f, 0f, 0f, -1.5f, katana.meleeReach, MeleeRules.Combo(katana)[0].ArcDegrees), "a slash does not");
        }

        [Test]
        public void RangedWeaponsCantSwing()
        {
            var m = new MeleeState();
            Assert.IsFalse(MeleeRules.TryStartSwing(ref m, WeaponCatalog.Get(WeaponCatalog.Nx7AssaultRifle), 0f));
        }

        [Test]
        public void ArcAndReach()
        {
            // Attacker at origin facing +Z with reach 2.4, arc 100°.
            Assert.IsTrue(MeleeRules.InArc(0, 0, 0f, 0f, 2f, 2.4f, 100f), "straight ahead");
            Assert.IsTrue(MeleeRules.InArc(0, 0, 0f, 1.2f, 1.6f, 2.4f, 100f), "about 37 degrees to the side");
            Assert.IsFalse(MeleeRules.InArc(0, 0, 0f, 2f, 0.5f, 2.4f, 100f), "about 76 degrees: outside the arc");
            Assert.IsFalse(MeleeRules.InArc(0, 0, 0f, 0f, -1f, 2.4f, 100f), "behind");
            Assert.IsFalse(MeleeRules.InArc(0, 0, 0f, 0f, 3f, 2.4f, 100f), "out of reach");
            Assert.IsTrue(MeleeRules.InArc(0, 0, 90f, 2f, 0f, 2.4f, 100f), "facing +X");
        }
    }

    public class CombatInputAndLoadoutTests
    {
        [Test]
        public void CombatButtonsRoundTripAlongsideMovement()
        {
            var move = new WarOfNyx.Core.Movement.MovementCommand { MoveY = 1f, JumpPressed = true, SprintHeld = true };
            var combat = new CombatCommand { FireHeld = true, FirePressed = true, ReloadPressed = false, MeleePressed = true, SelectSlot = 2, NextWeaponPressed = true };
            uint bits = InputCodec.PackButtons(move, combat);

            var decodedMove = InputCodec.Unpack(0f, 1f, 0f, 0f, bits);
            var decodedCombat = InputCodec.UnpackCombat(bits);
            Assert.IsTrue(decodedMove.JumpPressed && decodedMove.SprintHeld);
            Assert.AreEqual(combat, decodedCombat);
        }

        [Test]
        public void ForgedMultiSlotSelectionPicksOne()
        {
            var c = InputCodec.UnpackCombat((uint)(InputCodec.Buttons.Slot1 | InputCodec.Buttons.Slot3));
            Assert.AreEqual(1, c.SelectSlot);
        }

        [Test]
        public void DevelopmentLoadoutIsRiflePistolKatana()
        {
            var slots = CombatLoadout.CreateDevelopmentSlots();
            Assert.AreEqual(3, slots.Length);
            Assert.AreEqual(WeaponCatalog.Nx7AssaultRifle, WeaponCatalog.Get(slots[0].WeaponId).id);
            Assert.AreEqual(30, slots[0].Magazine);
            Assert.AreEqual(WeaponCatalog.NeonPistol, WeaponCatalog.Get(slots[1].WeaponId).id);
            Assert.AreEqual(WeaponCatalog.Katana, WeaponCatalog.Get(slots[2].WeaponId).id);
        }

        [Test]
        public void SwitchingSkipsEmptySlotsAndIgnoresSameSlot()
        {
            Func<int, bool> has = s => s != 1; // slot 2 (index 1) empty
            Assert.AreEqual(2, CombatLoadout.ResolveSwitch(0, new CombatCommand { NextWeaponPressed = true }, has));
            Assert.AreEqual(-1, CombatLoadout.ResolveSwitch(0, new CombatCommand { SelectSlot = 1 }, has), "already active");
            Assert.AreEqual(-1, CombatLoadout.ResolveSwitch(0, new CombatCommand { SelectSlot = 2 }, has), "empty");
            Assert.AreEqual(2, CombatLoadout.ResolveSwitch(0, new CombatCommand { SelectSlot = 3 }, has));
            Assert.AreEqual(0, CombatLoadout.ResolveSwitch(2, new CombatCommand { NextWeaponPressed = true }, has), "wraps around");
        }

        [Test]
        public void KillFeedText()
        {
            Assert.AreEqual("KAI eliminated AKIRA with NX-7 Assault Rifle", CombatText.Elimination("KAI", "AKIRA", "NX-7 Assault Rifle", false));
            Assert.AreEqual("REN eliminated YUNA with Katana (HEADSHOT)", CombatText.Elimination("REN", "YUNA", "Katana", true));
            Assert.AreEqual("AKIRA was eliminated", CombatText.Elimination(null, "AKIRA", null, false));
        }

        [Test]
        public void DuplicateFeedEventsAreDropped()
        {
            var d = new EventDeduplicator();
            Assert.IsTrue(d.TryAccept(1));
            Assert.IsFalse(d.TryAccept(1));
            Assert.IsTrue(d.TryAccept(2));
            for (int i = 3; i < 3 + EventDeduplicator.Capacity; i++)
                d.TryAccept(i);
            Assert.IsTrue(d.TryAccept(1), "very old ids are forgotten (bounded memory)");
        }

        [Test]
        public void ShoulderPointMatchesTheCameraShoulder()
        {
            AimGeometry.ShoulderOffset(0f, false, out float x, out float y, out float z);
            Assert.AreEqual(0.55f, x, 1e-5f);
            Assert.AreEqual(1.6f, y, 1e-5f);
            Assert.AreEqual(0f, z, 1e-5f);

            AimGeometry.ShoulderOffset(90f, true, out x, out y, out z);
            Assert.AreEqual(0f, x, 1e-5f);
            Assert.AreEqual(1.05f, y, 1e-5f);
            Assert.AreEqual(-0.55f, z, 1e-5f, "facing +X, the right shoulder is toward -Z");

            AimGeometry.Forward(0f, 90f, out float fx, out float fy, out float fz);
            Assert.AreEqual(-1f, fy, 1e-5f, "pitch +90 looks straight down");
        }
    }
}

namespace WarOfNyx.Tests
{
    public class WeaponMovementModifierTests
    {
        [Test]
        public void ActiveWeaponScalesMoveSpeed()
        {
            var settings = new WarOfNyx.Core.Movement.MovementSettings();
            var state = WarOfNyx.Core.Movement.MovementState.Initial(0f);
            var ground = new WarOfNyx.Core.Movement.GroundInfo(true, true);
            var marksman = WeaponCatalog.Get(WeaponCatalog.PhantomMarksman);
            var cmd = new WarOfNyx.Core.Movement.MovementCommand { MoveY = 1f, SpeedMultiplier = marksman.moveSpeedMultiplier };
            for (int i = 0; i < 60; i++)
                state = WarOfNyx.Core.Movement.MovementModel.Step(state, cmd, ground, settings, 1f / 60f);
            Assert.AreEqual(4f * 0.88f, state.HorizontalSpeed, 0.05f);
        }

        [Test]
        public void ClientCannotSendASpeedMultiplier()
        {
            var forged = new WarOfNyx.Core.Movement.MovementCommand { MoveY = 1f, SpeedMultiplier = 5f };
            Assert.AreEqual(0f, InputCodec.Sanitize(forged).SpeedMultiplier);
        }
    }
}

namespace WarOfNyx.Tests
{
    using NUnit.Framework;
    using WarOfNyx.Core.Combat;

    /// <summary>Screen blood: when a hit is "critical", and when healing clears it.</summary>
    public class CriticalHitTests
    {
        [Test]
        public void HeadshotsBigHitsAndBadlyHurt_AreCritical()
        {
            Assert.IsTrue(CriticalHit.Is(12f, headshot: true, 88f, 100f), "headshot");
            Assert.IsTrue(CriticalHit.Is(30f, false, 70f, 100f), "big hit");
            Assert.IsTrue(CriticalHit.Is(10f, false, 30f, 100f), "left badly hurt");
            Assert.IsFalse(CriticalHit.Is(14f, false, 86f, 100f), "a graze is not");
            Assert.IsFalse(CriticalHit.Is(0f, true, 100f, 100f), "blocked damage is not");
            Assert.AreEqual(3, CriticalHit.Splatters(20f, true));
            Assert.AreEqual(2, CriticalHit.Splatters(32f, false));
        }

        [Test]
        public void HealingClearsTheBlood_VignetteFollowsHealth()
        {
            Assert.IsFalse(CriticalHit.HealedPast(0.3f, 0.35f), "not healed enough yet");
            Assert.IsTrue(CriticalHit.HealedPast(0.3f, 0.5f), "healed clearly above the hit");
            Assert.IsTrue(CriticalHit.HealedPast(0.85f, 0.9f), "back to (almost) full");
            Assert.AreEqual(0f, CriticalHit.Vignette(0.8f));
            Assert.AreEqual(1f, CriticalHit.Vignette(0.05f));
            Assert.That(CriticalHit.Vignette(0.3f), Is.InRange(0.5f, 0.7f));
        }
    }
}
