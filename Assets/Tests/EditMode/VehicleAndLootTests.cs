using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Vehicles;

namespace WarOfNyx.Tests
{
    /// <summary>Vehicles (drive model, seats, crash / run-over / explosion damage) and world loot (arsenal expansion).</summary>
    public class VehicleAndLootTests
    {
        private sealed class OpenGround : ISpawnValidator
        {
            public bool TryValidate(float x, float z, out float groundY)
            {
                groundY = 1f;
                return x < 50f; // half the map is "water"
            }
        }

        [Test]
        public void Catalog_HasThreeDistinctVehicles()
        {
            Assert.That(VehicleCatalog.All.Length, Is.EqualTo(3));
            Assert.That(VehicleCatalog.All.Select(v => v.Id).Distinct().Count(), Is.EqualTo(3));
            var buggy = VehicleCatalog.Get(VehicleCatalog.IndexOf(VehicleCatalog.NyxBuggy));
            var truck = VehicleCatalog.Get(VehicleCatalog.IndexOf(VehicleCatalog.RaiderTruck));
            var quad = VehicleCatalog.Get(VehicleCatalog.IndexOf(VehicleCatalog.ScoutQuad));
            Assert.That(truck.MaxHealth, Is.GreaterThan(buggy.MaxHealth), "the truck is armoured");
            Assert.That(truck.Seats, Is.GreaterThan(buggy.Seats));
            Assert.That(quad.Acceleration, Is.GreaterThan(buggy.Acceleration), "the quad is quickest off the line");
            Assert.That(quad.MaxHealth, Is.LessThan(buggy.MaxHealth));
            Assert.That(VehicleCatalog.Get(-1), Is.Null);
        }

        [Test]
        public void Throttle_ReachesTopSpeed_AndNeverExceedsIt()
        {
            var v = VehicleCatalog.All[0];
            float speed = 0f, t = 0f;
            while (speed < v.TopSpeed * 0.95f && t < 30f)
            {
                speed = VehicleRules.Accelerate(speed, 1f, false, v, 0.02f);
                t += 0.02f;
            }
            Assert.That(t, Is.InRange(2f, 12f), "0 to 95 % of top speed takes a few seconds");
            for (int i = 0; i < 1000; i++)
                speed = VehicleRules.Accelerate(speed, 1f, false, v, 0.02f);
            Assert.That(speed, Is.EqualTo(v.TopSpeed).Within(1e-3f));
        }

        [Test]
        public void ReverseThrottle_BrakesFirst_ThenReverses_AndCoastingSlowsDown()
        {
            var v = VehicleCatalog.All[0];
            float speed = 20f;
            speed = VehicleRules.Accelerate(speed, -1f, false, v, 0.1f);
            Assert.That(speed, Is.EqualTo(20f - v.Braking * 0.1f).Within(1e-3f), "braking, not reversing");
            for (int i = 0; i < 400; i++)
                speed = VehicleRules.Accelerate(speed, -1f, false, v, 0.02f);
            Assert.That(speed, Is.EqualTo(-v.ReverseSpeed).Within(1e-3f));
            float coast = VehicleRules.Accelerate(15f, 0f, false, v, 1f);
            Assert.That(coast, Is.InRange(10f, 14.9f));
            Assert.That(VehicleRules.Accelerate(15f, 1f, true, v, 1f), Is.EqualTo(0f), "handbrake stops hard");
        }

        [Test]
        public void Steering_IsReducedAtSpeed_AndTurnsTheRightWay()
        {
            var v = VehicleCatalog.All[0];
            Assert.That(VehicleRules.SteerAngle(1f, 0f, v), Is.EqualTo(v.SteerDegrees));
            Assert.That(VehicleRules.SteerAngle(1f, v.TopSpeed, v), Is.LessThan(v.SteerDegrees * 0.5f));
            Assert.That(VehicleRules.YawRate(10f, 20f, v), Is.GreaterThan(0f), "steer right while driving forward turns right");
            Assert.That(VehicleRules.YawRate(-5f, 20f, v), Is.LessThan(0f), "reversing turns the other way");
            foreach (float speed in new[] { 5f, 15f, 30f })
            {
                float rate = VehicleRules.YawRate(speed, v.SteerDegrees, v) * (float)System.Math.PI / 180f;
                Assert.That(speed * rate, Is.LessThanOrEqualTo(VehicleRules.MaxLateralAcceleration + 1e-3f), $"cornering capped at {speed} m/s (no rollovers)");
            }
        }

        [Test]
        public void Seats_AndEnterRules()
        {
            Assert.That(VehicleRules.FreeSeat(new[] { false, false }, 2), Is.EqualTo(0), "driver seat first");
            Assert.That(VehicleRules.FreeSeat(new[] { true, false }, 2), Is.EqualTo(1));
            Assert.That(VehicleRules.FreeSeat(new[] { true, true }, 2), Is.EqualTo(-1));
            Assert.That(VehicleRules.CanEnter(true, false, false, 2f, 0), Is.Null);
            Assert.That(VehicleRules.CanEnter(false, false, false, 2f, 0), Is.Not.Null);
            Assert.That(VehicleRules.CanEnter(true, true, false, 2f, 0), Is.Not.Null);
            Assert.That(VehicleRules.CanEnter(true, false, true, 2f, 0), Is.Not.Null, "wrecks can't be driven");
            Assert.That(VehicleRules.CanEnter(true, false, false, 10f, 0), Is.Not.Null, "the authority re-checks distance");
            Assert.That(VehicleRules.CanEnter(true, false, false, 2f, -1), Is.Not.Null);
        }

        [Test]
        public void Damage_Curves()
        {
            Assert.That(VehicleRules.RunOverDamage(2f), Is.EqualTo(0f), "a slow nudge does nothing");
            Assert.That(VehicleRules.RunOverDamage(8f), Is.InRange(30f, 60f));
            Assert.That(VehicleRules.RunOverDamage(16f), Is.GreaterThanOrEqualTo(100f), "a fast hit is lethal");
            Assert.That(VehicleRules.CrashDamage(5f), Is.EqualTo(0f));
            Assert.That(VehicleRules.CrashDamage(20f), Is.GreaterThan(VehicleRules.CrashDamage(12f)));
            Assert.That(VehicleRules.CrashDamage(30f), Is.GreaterThan(VehicleCatalog.All[0].MaxHealth * 0.5f), "a wall at full speed wrecks a buggy quickly");
            Assert.That(VehicleRules.OccupantCrashDamage(10f), Is.EqualTo(0f));
            Assert.That(VehicleRules.OccupantCrashDamage(25f), Is.InRange(40f, 90f));
            Assert.That(VehicleRules.ExplosionDamageAt(0f), Is.EqualTo(VehicleRules.ExplosionDamage));
            Assert.That(VehicleRules.ExplosionDamageAt(VehicleRules.ExplosionRadius), Is.EqualTo(0f));
        }

        [Test]
        public void SpawnCounts_AndTypes_AreDeterministic()
        {
            Assert.That(VehicleRules.SpawnCount(50f), Is.EqualTo(3));
            Assert.That(VehicleRules.SpawnCount(600f), Is.InRange(8, 9));
            Assert.That(VehicleRules.SpawnCount(5000f), Is.EqualTo(14));
            var types = Enumerable.Range(0, 200).Select(i => VehicleRules.TypeFor(42, i)).ToList();
            Assert.That(types.Distinct().Count(), Is.EqualTo(3));
            Assert.That(types.Count(t => t == 0), Is.GreaterThan(types.Count(t => t == 2)), "buggies are the most common");
            Assert.That(VehicleRules.TypeFor(42, 7), Is.EqualTo(VehicleRules.TypeFor(42, 7)));
        }

        [Test]
        public void ArsenalExpansion_IsAppended_AndBalanced()
        {
            Assert.That(WeaponCatalog.NetworkIdOf(WeaponCatalog.Nx7AssaultRifle), Is.EqualTo(0), "old ids unchanged");
            Assert.That(WeaponCatalog.NetworkIdOf(WeaponCatalog.Dagger), Is.EqualTo(10));
            foreach (var id in new[] { WeaponCatalog.VortexLmg, WeaponCatalog.ArcSniper, WeaponCatalog.TalonAutoShotgun, WeaponCatalog.RiftCarbine,
                         WeaponCatalog.ViperRevolver, WeaponCatalog.PulseDmr, WeaponCatalog.WarAxe })
            {
                Assert.That(WeaponCatalog.NetworkIdOf(id), Is.GreaterThan(10), id + " is appended");
                var w = WeaponCatalog.Get(id);
                if (w.IsRanged)
                    Assert.That(w.bloomRecoveryPerSecond, Is.LessThan(w.bloomPerShot * w.roundsPerSecond), id + ": sustained fire builds bloom");
            }
            var sniper = WeaponCatalog.Get(WeaponCatalog.ArcSniper);
            Assert.That(sniper.rarity, Is.EqualTo(Rarity.Legendary));
            Assert.That(sniper.damage * sniper.headshotMultiplier, Is.LessThan(250f), "sniper headshot damage stays bounded");
            Assert.That(NetworkProtocol.Version, Is.GreaterThanOrEqualTo(13), "new weapon ids need a new protocol version");
        }

        [Test]
        public void WorldLoot_IsDeterministic_HasAmmoForItsGun_AndFavoursCommonItems()
        {
            int count = WeaponCatalog.All.Length;
            var a = WorldLoot.Contents(7, 3, WeaponCatalog.Get, count);
            var b = WorldLoot.Contents(7, 3, WeaponCatalog.Get, count);
            for (int i = 0; i < LootContainer.Capacity; i++)
                Assert.That(a[i], Is.EqualTo(b[i]));

            var rarities = new Dictionary<Rarity, int>();
            var seen = new HashSet<short>();
            for (int i = 0; i < 2000; i++)
            {
                var c = WorldLoot.Contents(11, i, WeaponCatalog.Get, count);
                var weapon = c[0];
                Assert.That(weapon.Kind, Is.EqualTo(LootKind.Weapon));
                seen.Add(weapon.WeaponId);
                var stats = WeaponCatalog.Get(weapon.WeaponId);
                rarities[stats.rarity] = rarities.TryGetValue(stats.rarity, out int n) ? n + 1 : 1;
                if (stats.IsRanged)
                {
                    Assert.That(c[1].Kind, Is.EqualTo(LootKind.Ammo), stats.id);
                    Assert.That(c[1].Ammo, Is.EqualTo(stats.ammoType), stats.id + " comes with its own ammo");
                    Assert.That(weapon.Magazine, Is.EqualTo(stats.magazineSize), "loaded");
                }
            }
            Assert.That(seen.Count, Is.EqualTo(count), "every weapon, old and new, can be found on the map");
            Assert.That(rarities[Rarity.Common], Is.GreaterThan(rarities[Rarity.Legendary] * 2));
            Assert.That(rarities.ContainsKey(Rarity.Legendary), "legendary weapons exist, rarely");
            Assert.That(WorldLoot.CacheCount(25), Is.EqualTo(50));
            Assert.That(WorldLoot.CacheCount(2), Is.EqualTo(24));
        }

        [Test]
        public void ScatterPlanner_RespectsTheValidator_AndSeparation()
        {
            var spots = ScatterPlanner.Plan(30, 0f, 0f, 200f, 25f, 5, new OpenGround());
            Assert.That(spots.Count, Is.EqualTo(30));
            foreach (var s in spots)
            {
                Assert.That(s.X, Is.LessThan(50f), "only valid ground");
                Assert.That(s.X * s.X + s.Z * s.Z, Is.LessThanOrEqualTo(200f * 200f + 1f));
            }
            var again = ScatterPlanner.Plan(30, 0f, 0f, 200f, 25f, 5, new OpenGround());
            Assert.That(again.Select(s => s.X), Is.EqualTo(spots.Select(s => s.X)), "deterministic");
            var extra = ScatterPlanner.Plan(10, 0f, 0f, 200f, 10f, 5, new OpenGround(), (x, y, z) => z > 0f);
            Assert.That(extra.All(s => s.Z > 0f), "the extra check (vehicle clearance) applies");
        }
    }
}
