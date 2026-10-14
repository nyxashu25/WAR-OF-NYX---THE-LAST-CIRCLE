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
        // Arsenal expansion (appended: network ids 11+).
        public const string VortexLmg = "vortex_lmg";
        public const string ArcSniper = "arc_sniper";
        public const string TalonAutoShotgun = "talon_auto_shotgun";
        public const string RiftCarbine = "rift_carbine";
        public const string ViperRevolver = "viper_revolver";
        public const string PulseDmr = "pulse_dmr";
        public const string WarAxe = "war_axe";

        /// <summary>
        /// Raise when the built-in numbers change: project setup then refreshes existing weapon assets
        /// (2: accuracy pass, much tighter spread and slower bloom; melee combo styles; 3: arsenal expansion).
        /// </summary>
        public const int Version = 3;

        private static WeaponStats[] Build() => new[]
        {
            // ---- Ranged (spec 9 table) ----
            new WeaponStats
            {
                id = Nx7AssaultRifle, displayName = "NX-7 Assault Rifle", fireMode = FireMode.Auto, ammoType = AmmoType.Rifle, rarity = Rarity.Rare,
                damage = 24f, roundsPerSecond = 9f, magazineSize = 30, startingReserve = 90, reloadSeconds = 2.1f,
                falloffStart = 30f, falloffEnd = 80f, minDamageMultiplier = 0.6f, maxRange = 150f, headshotMultiplier = 1.5f,
                hipSpread = 1.5f, aimSpread = 0.3f, bloomPerShot = 0.12f, maxBloom = 1.4f, bloomRecoveryPerSecond = 0.8f,
                recoilKick = 0.7f, moveSpeedMultiplier = 0.95f, powerScore = 30f,
            },
            new WeaponStats
            {
                id = ShadowSmg, displayName = "Shadow SMG", fireMode = FireMode.Auto, ammoType = AmmoType.Light, rarity = Rarity.Uncommon,
                damage = 16f, roundsPerSecond = 13f, magazineSize = 40, startingReserve = 120, reloadSeconds = 1.8f,
                falloffStart = 15f, falloffEnd = 40f, minDamageMultiplier = 0.5f, maxRange = 80f, headshotMultiplier = 1.4f,
                hipSpread = 1.8f, aimSpread = 0.7f, bloomPerShot = 0.1f, maxBloom = 1.6f, bloomRecoveryPerSecond = 1f,
                recoilKick = 0.45f, moveSpeedMultiplier = 1f, powerScore = 28f,
            },
            new WeaponStats
            {
                id = CrimsonShotgun, displayName = "Crimson Shotgun", fireMode = FireMode.Semi, ammoType = AmmoType.Shell, rarity = Rarity.Rare,
                damage = 12f, pellets = 8, roundsPerSecond = 1.1f, magazineSize = 6, startingReserve = 24, reloadSeconds = 3f,
                falloffStart = 6f, falloffEnd = 18f, minDamageMultiplier = 0.2f, maxRange = 30f, headshotMultiplier = 1.25f,
                hipSpread = 4.5f, aimSpread = 3.2f, bloomPerShot = 0f, maxBloom = 0f, bloomRecoveryPerSecond = 0f,
                recoilKick = 3f, moveSpeedMultiplier = 0.92f, powerScore = 29f,
            },
            new WeaponStats
            {
                id = PhantomMarksman, displayName = "Phantom Marksman", fireMode = FireMode.Semi, ammoType = AmmoType.Marksman, rarity = Rarity.Epic,
                damage = 55f, roundsPerSecond = 1.4f, magazineSize = 10, startingReserve = 30, reloadSeconds = 2.6f,
                falloffStart = 80f, falloffEnd = 200f, minDamageMultiplier = 0.8f, maxRange = 300f, headshotMultiplier = 2f,
                hipSpread = 2.5f, aimSpread = 0.05f, bloomPerShot = 1.2f, maxBloom = 2.5f, bloomRecoveryPerSecond = 1.5f,
                recoilKick = 2.5f, moveSpeedMultiplier = 0.88f, powerScore = 31f,
            },
            new WeaponStats
            {
                id = StormBurstRifle, displayName = "Storm Burst Rifle", fireMode = FireMode.Burst, ammoType = AmmoType.Rifle, rarity = Rarity.Rare,
                damage = 21f, roundsPerSecond = 3f, burstCount = 3, burstInterval = 0.07f, magazineSize = 24, startingReserve = 72,
                reloadSeconds = 2.2f, falloffStart = 35f, falloffEnd = 90f, minDamageMultiplier = 0.6f, maxRange = 160f,
                headshotMultiplier = 1.5f, hipSpread = 1.3f, aimSpread = 0.25f, bloomPerShot = 0.12f, maxBloom = 1.2f,
                bloomRecoveryPerSecond = 0.8f, recoilKick = 0.6f, moveSpeedMultiplier = 0.95f, powerScore = 30f,
            },
            new WeaponStats
            {
                id = NeonPistol, displayName = "Neon Pistol", fireMode = FireMode.Semi, ammoType = AmmoType.Light, rarity = Rarity.Common,
                damage = 26f, roundsPerSecond = 5f, magazineSize = 12, startingReserve = 48, reloadSeconds = 1.4f,
                falloffStart = 20f, falloffEnd = 50f, minDamageMultiplier = 0.55f, maxRange = 90f, headshotMultiplier = 1.6f,
                hipSpread = 1.2f, aimSpread = 0.35f, bloomPerShot = 0.3f, maxBloom = 1.5f, bloomRecoveryPerSecond = 1.2f,
                recoilKick = 1f, moveSpeedMultiplier = 1f, powerScore = 15f,
            },
            new WeaponStats
            {
                id = StormLightSmg, displayName = "Storm Light SMG", fireMode = FireMode.Auto, ammoType = AmmoType.Light, rarity = Rarity.Common,
                damage = 14f, roundsPerSecond = 15f, magazineSize = 45, startingReserve = 135, reloadSeconds = 1.6f,
                falloffStart = 12f, falloffEnd = 35f, minDamageMultiplier = 0.45f, maxRange = 70f, headshotMultiplier = 1.4f,
                hipSpread = 2f, aimSpread = 0.8f, bloomPerShot = 0.08f, maxBloom = 1.8f, bloomRecoveryPerSecond = 1f,
                recoilKick = 0.4f, moveSpeedMultiplier = 1.03f, powerScore = 27f,
            },

            // ---- Melee (spec 9 "Melee Weapons"; damage values are initial targets) ----
            Melee(Katana, "Katana", Rarity.Uncommon, damage: 45f, windup: 0.22f, cooldown: 0.65f, reach: 2.4f, arc: 100f, power: 14f, MeleeStyle.Blade),
            Melee(CombatKnife, "Combat Knife", Rarity.Common, damage: 35f, windup: 0.12f, cooldown: 0.45f, reach: 1.7f, arc: 80f, power: 11f, MeleeStyle.Knife),
            Melee(EnergyBlade, "Energy Blade", Rarity.Rare, damage: 50f, windup: 0.28f, cooldown: 0.8f, reach: 2.6f, arc: 110f, power: 15f, MeleeStyle.Energy),
            Melee(Dagger, "Dagger", Rarity.Common, damage: 30f, windup: 0.1f, cooldown: 0.4f, reach: 1.5f, arc: 70f, power: 10f, MeleeStyle.Knife),

            // ---- Arsenal expansion: world loot only (starting kits are unchanged). Appended, never reorder. ----
            new WeaponStats
            {
                id = VortexLmg, displayName = "Vortex LMG", fireMode = FireMode.Auto, ammoType = AmmoType.Rifle, rarity = Rarity.Epic,
                damage = 22f, roundsPerSecond = 10f, magazineSize = 75, startingReserve = 150, reloadSeconds = 4.2f,
                falloffStart = 35f, falloffEnd = 90f, minDamageMultiplier = 0.6f, maxRange = 160f, headshotMultiplier = 1.4f,
                hipSpread = 2.2f, aimSpread = 0.6f, bloomPerShot = 0.1f, maxBloom = 1.8f, bloomRecoveryPerSecond = 0.7f,
                recoilKick = 0.8f, moveSpeedMultiplier = 0.85f, powerScore = 31f,
            },
            new WeaponStats
            {
                id = ArcSniper, displayName = "Arc Sniper", fireMode = FireMode.Semi, ammoType = AmmoType.Marksman, rarity = Rarity.Legendary,
                damage = 90f, roundsPerSecond = 0.75f, magazineSize = 5, startingReserve = 15, reloadSeconds = 3.4f,
                falloffStart = 120f, falloffEnd = 350f, minDamageMultiplier = 0.85f, maxRange = 400f, headshotMultiplier = 2.5f,
                hipSpread = 4f, aimSpread = 0.02f, bloomPerShot = 2f, maxBloom = 3f, bloomRecoveryPerSecond = 1.2f,
                recoilKick = 4f, moveSpeedMultiplier = 0.85f, powerScore = 33f,
            },
            new WeaponStats
            {
                id = TalonAutoShotgun, displayName = "Talon Auto-Shotgun", fireMode = FireMode.Auto, ammoType = AmmoType.Shell, rarity = Rarity.Epic,
                damage = 9f, pellets = 7, roundsPerSecond = 3.5f, magazineSize = 10, startingReserve = 30, reloadSeconds = 3.2f,
                falloffStart = 5f, falloffEnd = 15f, minDamageMultiplier = 0.2f, maxRange = 25f, headshotMultiplier = 1.2f,
                hipSpread = 5f, aimSpread = 4f, bloomPerShot = 0.4f, maxBloom = 2f, bloomRecoveryPerSecond = 1f,
                recoilKick = 2f, moveSpeedMultiplier = 0.92f, powerScore = 30f,
            },
            new WeaponStats
            {
                id = RiftCarbine, displayName = "Rift Carbine", fireMode = FireMode.Auto, ammoType = AmmoType.Rifle, rarity = Rarity.Uncommon,
                damage = 20f, roundsPerSecond = 11f, magazineSize = 30, startingReserve = 90, reloadSeconds = 1.9f,
                falloffStart = 25f, falloffEnd = 70f, minDamageMultiplier = 0.55f, maxRange = 120f, headshotMultiplier = 1.5f,
                hipSpread = 1.6f, aimSpread = 0.45f, bloomPerShot = 0.1f, maxBloom = 1.5f, bloomRecoveryPerSecond = 0.8f,
                recoilKick = 0.55f, moveSpeedMultiplier = 0.98f, powerScore = 28f,
            },
            new WeaponStats
            {
                id = ViperRevolver, displayName = "Viper Revolver", fireMode = FireMode.Semi, ammoType = AmmoType.Light, rarity = Rarity.Rare,
                damage = 48f, roundsPerSecond = 1.8f, magazineSize = 6, startingReserve = 30, reloadSeconds = 2.6f,
                falloffStart = 20f, falloffEnd = 60f, minDamageMultiplier = 0.6f, maxRange = 100f, headshotMultiplier = 1.8f,
                hipSpread = 1.4f, aimSpread = 0.25f, bloomPerShot = 0.9f, maxBloom = 2f, bloomRecoveryPerSecond = 1.2f,
                recoilKick = 2.2f, moveSpeedMultiplier = 1f, powerScore = 18f,
            },
            new WeaponStats
            {
                id = PulseDmr, displayName = "Pulse DMR", fireMode = FireMode.Semi, ammoType = AmmoType.Marksman, rarity = Rarity.Rare,
                damage = 40f, roundsPerSecond = 2.5f, magazineSize = 15, startingReserve = 45, reloadSeconds = 2.4f,
                falloffStart = 60f, falloffEnd = 160f, minDamageMultiplier = 0.75f, maxRange = 250f, headshotMultiplier = 1.8f,
                hipSpread = 2.2f, aimSpread = 0.12f, bloomPerShot = 0.6f, maxBloom = 2f, bloomRecoveryPerSecond = 1.2f,
                recoilKick = 1.6f, moveSpeedMultiplier = 0.92f, powerScore = 30f,
            },
            Melee(WarAxe, "Nyx War Axe", Rarity.Rare, damage: 55f, windup: 0.3f, cooldown: 0.9f, reach: 2.3f, arc: 90f, power: 15f, MeleeStyle.Blade),
        };

        private static WeaponStats Melee(string id, string name, Rarity rarity, float damage, float windup, float cooldown, float reach, float arc, float power,
            MeleeStyle style) =>
            new WeaponStats
            {
                id = id, displayName = name, category = WeaponCategory.Melee, fireMode = FireMode.Semi, ammoType = AmmoType.None, rarity = rarity,
                damage = damage, magazineSize = 0, startingReserve = 0, headshotMultiplier = 1f,
                meleeWindup = windup, meleeCooldown = cooldown, meleeReach = reach, meleeArcDegrees = arc, meleeStyle = style,
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
