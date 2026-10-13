using System;
using System.Collections.Generic;
using WarOfNyx.Core.Items;

namespace WarOfNyx.Core.Combat
{
    /// <summary>
    /// Built-in weapon definitions with the spec 9 balancing targets. The index of each weapon in
    /// <see cref="All"/> is its NETWORK ID: append new weapons at the end, never reorder or remove
    /// (bump NetworkProtocol.Version if you must).
    /// Unity WeaponDefinition assets can override the numbers for tuning; ids stay the same.
    /// </summary>
    public static class WeaponCatalog
    {
        public const short NoWeapon = -1;

        public static readonly WeaponStats[] All = Build();

        private static readonly Dictionary<string, short> IdToIndex = BuildIndex();

        public static WeaponStats Get(short networkId) =>
            networkId >= 0 && networkId < All.Length ? All[networkId] : null;

        public static short NetworkIdOf(string id) =>
            id != null && IdToIndex.TryGetValue(id, out short index) ? index : NoWeapon;

        public static WeaponStats Get(string id) => Get(NetworkIdOf(id));

        // Ids (stable).
        public const string Nx7AssaultRifle = "nx7_ar";
        public const string ShadowSmg = "shadow_smg";
        public const string CrimsonShotgun = "crimson_shotgun";
        public const string PhantomMarksman = "phantom_marksman";
        public const string StormBurstRifle = "storm_burst";
        public const string NeonPistol = "neon_pistol";
        public const string StormLightSmg = "storm_light_smg";
        public const string Katana = "katana";
        public const string CombatKnife = "combat_knife";
        public const string EnergyBlade = "energy_blade";
        public const string Dagger = "dagger";

        private static WeaponStats[] Build() => new[]
        {
            // ---- Ranged (spec 9 table) ----
            new WeaponStats
            {
                id = Nx7AssaultRifle, displayName = "NX-7 Assault Rifle", fireMode = FireMode.Auto, ammoType = AmmoType.Rifle, rarity = Rarity.Rare,
                damage = 24f, roundsPerSecond = 9f, magazineSize = 30, startingReserve = 90, reloadSeconds = 2.1f,
                falloffStart = 30f, falloffEnd = 80f, minDamageMultiplier = 0.6f, maxRange = 150f, headshotMultiplier = 1.5f,
                hipSpread = 2.5f, aimSpread = 0.8f, bloomPerShot = 0.35f, maxBloom = 4f, bloomRecoveryPerSecond = 2f,
                recoilKick = 0.7f, moveSpeedMultiplier = 0.95f, powerScore = 30f,
            },
            new WeaponStats
            {
                id = ShadowSmg, displayName = "Shadow SMG", fireMode = FireMode.Auto, ammoType = AmmoType.Light, rarity = Rarity.Uncommon,
                damage = 16f, roundsPerSecond = 13f, magazineSize = 40, startingReserve = 120, reloadSeconds = 1.8f,
                falloffStart = 15f, falloffEnd = 40f, minDamageMultiplier = 0.5f, maxRange = 80f, headshotMultiplier = 1.4f,
                hipSpread = 3f, aimSpread = 1.4f, bloomPerShot = 0.3f, maxBloom = 4.5f, bloomRecoveryPerSecond = 2.5f,
                recoilKick = 0.45f, moveSpeedMultiplier = 1f, powerScore = 28f,
            },
            new WeaponStats
            {
                id = CrimsonShotgun, displayName = "Crimson Shotgun", fireMode = FireMode.Semi, ammoType = AmmoType.Shell, rarity = Rarity.Rare,
                damage = 12f, pellets = 8, roundsPerSecond = 1.1f, magazineSize = 6, startingReserve = 24, reloadSeconds = 3f,
                falloffStart = 6f, falloffEnd = 18f, minDamageMultiplier = 0.2f, maxRange = 30f, headshotMultiplier = 1.25f,
                hipSpread = 6f, aimSpread = 4.5f, bloomPerShot = 0f, maxBloom = 0f, bloomRecoveryPerSecond = 0f,
                recoilKick = 3f, moveSpeedMultiplier = 0.92f, powerScore = 29f,
            },
            new WeaponStats
            {
                id = PhantomMarksman, displayName = "Phantom Marksman", fireMode = FireMode.Semi, ammoType = AmmoType.Marksman, rarity = Rarity.Epic,
                damage = 55f, roundsPerSecond = 1.4f, magazineSize = 10, startingReserve = 30, reloadSeconds = 2.6f,
                falloffStart = 80f, falloffEnd = 200f, minDamageMultiplier = 0.8f, maxRange = 300f, headshotMultiplier = 2f,
                hipSpread = 4f, aimSpread = 0.15f, bloomPerShot = 2f, maxBloom = 4f, bloomRecoveryPerSecond = 2.5f,
                recoilKick = 2.5f, moveSpeedMultiplier = 0.88f, powerScore = 31f,
            },
            new WeaponStats
            {
                id = StormBurstRifle, displayName = "Storm Burst Rifle", fireMode = FireMode.Burst, ammoType = AmmoType.Rifle, rarity = Rarity.Rare,
                damage = 21f, roundsPerSecond = 3f, burstCount = 3, burstInterval = 0.07f, magazineSize = 24, startingReserve = 72,
                reloadSeconds = 2.2f, falloffStart = 35f, falloffEnd = 90f, minDamageMultiplier = 0.6f, maxRange = 160f,
                headshotMultiplier = 1.5f, hipSpread = 2.2f, aimSpread = 0.6f, bloomPerShot = 0.3f, maxBloom = 3.5f,
                bloomRecoveryPerSecond = 2f, recoilKick = 0.6f, moveSpeedMultiplier = 0.95f, powerScore = 30f,
            },
            new WeaponStats
            {
                id = NeonPistol, displayName = "Neon Pistol", fireMode = FireMode.Semi, ammoType = AmmoType.Light, rarity = Rarity.Common,
                damage = 26f, roundsPerSecond = 5f, magazineSize = 12, startingReserve = 48, reloadSeconds = 1.4f,
                falloffStart = 20f, falloffEnd = 50f, minDamageMultiplier = 0.55f, maxRange = 90f, headshotMultiplier = 1.6f,
                hipSpread = 2f, aimSpread = 0.9f, bloomPerShot = 0.5f, maxBloom = 3f, bloomRecoveryPerSecond = 2f,
                recoilKick = 1f, moveSpeedMultiplier = 1f, powerScore = 15f,
            },
            new WeaponStats
            {
                id = StormLightSmg, displayName = "Storm Light SMG", fireMode = FireMode.Auto, ammoType = AmmoType.Light, rarity = Rarity.Common,
                damage = 14f, roundsPerSecond = 15f, magazineSize = 45, startingReserve = 135, reloadSeconds = 1.6f,
                falloffStart = 12f, falloffEnd = 35f, minDamageMultiplier = 0.45f, maxRange = 70f, headshotMultiplier = 1.4f,
                hipSpread = 3.2f, aimSpread = 1.6f, bloomPerShot = 0.25f, maxBloom = 4.5f, bloomRecoveryPerSecond = 2.5f,
                recoilKick = 0.4f, moveSpeedMultiplier = 1.03f, powerScore = 27f,
            },

            // ---- Melee (spec 9 "Melee Weapons"; damage values are initial targets) ----
            Melee(Katana, "Katana", Rarity.Uncommon, damage: 45f, windup: 0.22f, cooldown: 0.65f, reach: 2.4f, arc: 100f, power: 14f),
            Melee(CombatKnife, "Combat Knife", Rarity.Common, damage: 35f, windup: 0.12f, cooldown: 0.45f, reach: 1.7f, arc: 80f, power: 11f),
            Melee(EnergyBlade, "Energy Blade", Rarity.Rare, damage: 50f, windup: 0.28f, cooldown: 0.8f, reach: 2.6f, arc: 110f, power: 15f),
            Melee(Dagger, "Dagger", Rarity.Common, damage: 30f, windup: 0.1f, cooldown: 0.4f, reach: 1.5f, arc: 70f, power: 10f),
        };

        private static WeaponStats Melee(string id, string name, Rarity rarity, float damage, float windup, float cooldown, float reach, float arc, float power) =>
            new WeaponStats
            {
                id = id, displayName = name, category = WeaponCategory.Melee, fireMode = FireMode.Semi, ammoType = AmmoType.None, rarity = rarity,
                damage = damage, magazineSize = 0, startingReserve = 0, headshotMultiplier = 1f,
                meleeWindup = windup, meleeCooldown = cooldown, meleeReach = reach, meleeArcDegrees = arc,
                maxRange = reach, falloffStart = reach, falloffEnd = reach, minDamageMultiplier = 1f,
                moveSpeedMultiplier = 1.05f, powerScore = power,
            };

        private static Dictionary<string, short> BuildIndex()
        {
            var map = new Dictionary<string, short>(StringComparer.Ordinal);
            for (short i = 0; i < All.Length; i++)
            {
                if (map.ContainsKey(All[i].id))
                    throw new InvalidOperationException($"Duplicate weapon id '{All[i].id}'.");
                map.Add(All[i].id, i);
            }
            return map;
        }
    }
}
