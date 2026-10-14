using System;
using System.Collections.Generic;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Match;

namespace WarOfNyx.Core.Items
{
    /// <summary>A planned point on the map (loot cache, vehicle parking spot): ground position and facing.</summary>
    public struct MapSpot
    {
        public float X, Y, Z, Yaw;
    }

    /// <summary>
    /// Scatters points over the arena (deterministic per seed): every point passes the map's spawn validator
    /// (solid, dry, walkable, reachable, uncovered) plus an optional extra check, and keeps a minimum separation.
    /// </summary>
    public static class ScatterPlanner
    {
        public static List<MapSpot> Plan(int count, float centreX, float centreZ, float radius, float minSeparation, int seed,
            ISpawnValidator validator, Func<float, float, float, bool> extraCheck = null, int attemptsPerPoint = 40)
        {
            var result = new List<MapSpot>();
            if (count <= 0 || validator == null)
                return result;
            var random = new Random(seed);
            float separation = minSeparation;
            int attempts = count * attemptsPerPoint;
            // Relax the separation if the map is crowded, so the count is met where possible.
            for (int pass = 0; pass < 3 && result.Count < count; pass++, separation *= 0.6f)
            {
                for (int i = 0; i < attempts && result.Count < count; i++)
                {
                    double a = random.NextDouble() * Math.PI * 2.0;
                    double d = radius * Math.Sqrt(random.NextDouble());
                    float x = centreX + (float)(Math.Sin(a) * d), z = centreZ + (float)(Math.Cos(a) * d);
                    float yaw = (float)(random.NextDouble() * 360.0);
                    if (!FarFromAll(result, x, z, separation))
                        continue;
                    if (!validator.TryValidate(x, z, out float y))
                        continue;
                    if (extraCheck != null && !extraCheck(x, y, z))
                        continue;
                    result.Add(new MapSpot { X = x, Y = y, Z = z, Yaw = yaw });
                }
            }
            return result;
        }

        private static bool FarFromAll(List<MapSpot> spots, float x, float z, float separation)
        {
            float s2 = separation * separation;
            foreach (var s in spots)
            {
                float dx = s.X - x, dz = s.Z - z;
                if (dx * dx + dz * dz < s2)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// World loot (supply caches placed on the map at match start, in addition to starting kits and elimination
    /// capsules): one weapon by rarity (common most often, legendary rarely), ammunition for it, and a chance of
    /// medkits and armour. Deterministic per seed and cache index, so every device builds the same caches.
    /// </summary>
    public static class WorldLoot
    {
        /// <summary>Rarity weights: Common, Uncommon, Rare, Epic, Legendary.</summary>
        public static readonly float[] RarityWeights = { 30f, 27f, 22f, 14f, 7f };

        /// <summary>How many caches an offline match places for this many players: two per player, 24 to 80.</summary>
        public static int CacheCount(int players) => Math.Max(24, Math.Min(80, players * 2));

        public static Rarity RollRarity(Random random)
        {
            float total = 0f;
            foreach (var w in RarityWeights)
                total += w;
            double r = random.NextDouble() * total;
            for (int i = 0; i < RarityWeights.Length; i++)
            {
                r -= RarityWeights[i];
                if (r < 0)
                    return (Rarity)i;
            }
            return Rarity.Common;
        }

        /// <summary>A weapon of the rolled rarity (or the nearest lower rarity that has one).</summary>
        public static short PickWeapon(Random random, Func<short, WeaponStats> weapons, int weaponCount)
        {
            var rarity = RollRarity(random);
            var pool = new List<short>();
            for (int r = (int)rarity; r >= 0 && pool.Count == 0; r--)
                for (short id = 0; id < weaponCount; id++)
                {
                    var w = weapons(id);
                    if (w != null && (int)w.rarity == r)
                        pool.Add(id);
                }
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : (short)0;
        }

        public static LootContainer Contents(int seed, int index, Func<short, WeaponStats> weapons, int weaponCount)
        {
            var random = new Random(unchecked(seed * 486187739 + index * 7919 + 17));
            var container = new LootContainer();
            short id = PickWeapon(random, weapons, weaponCount);
            var weapon = weapons(id);
            if (weapon != null)
            {
                container.Add(LootItem.Weapon(new WeaponState { WeaponId = id, Magazine = weapon.IsRanged ? weapon.magazineSize : 0 }));
                if (weapon.IsRanged && weapon.ammoType != AmmoType.None)
                    container.Add(LootItem.AmmoStack(weapon.ammoType, Math.Max(weapon.magazineSize, weapon.startingReserve / 2)));
            }
            double extra = random.NextDouble();
            if (extra < 0.35)
                container.Add(LootItem.MedkitStack(MedkitType.Small, 2));
            else if (extra < 0.5)
                container.Add(LootItem.MedkitStack(MedkitType.Standard, 1));
            if (random.NextDouble() < 0.18)
                container.Add(LootItem.ArmorPiece(random.NextDouble() < 0.25 ? 2 : 1));
            return container;
        }
    }
}
