using System;

namespace WarOfNyx.Core.Combat
{
    public enum WeaponCategory : byte
    {
        Ranged = 0,
        Melee = 1,
    }

    public enum FireMode : byte
    {
        Semi = 0,
        Auto = 1,
        Burst = 2,
    }

    public enum AmmoType : byte
    {
        None = 0,
        Light = 1,
        Rifle = 2,
        Shell = 3,
        Marksman = 4,
    }

    /// <summary>
    /// Static definition of one weapon (master spec 7 "Loadout Data" and 9). Engine-independent so
    /// the server, client prediction, bots and tests use identical numbers. Editable in Unity
    /// through WeaponDefinition assets. All values are balancing targets [T].
    /// </summary>
    [Serializable]
    public sealed class WeaponStats
    {
        public string id = "weapon";
        public string displayName = "Weapon";
        public WeaponCategory category = WeaponCategory.Ranged;
        public FireMode fireMode = FireMode.Auto;
        public AmmoType ammoType = AmmoType.Rifle;
        public WarOfNyx.Core.Items.Rarity rarity = WarOfNyx.Core.Items.Rarity.Common;

        // ---- Damage ----
        public float damage = 20f;
        /// <summary>Projectiles per shot (shotgun pellets). Each pellet does <see cref="damage"/>.</summary>
        public int pellets = 1;
        public float headshotMultiplier = 1.5f;
        /// <summary>Full damage up to here (m)...</summary>
        public float falloffStart = 30f;
        /// <summary>...linearly down to <see cref="minDamageMultiplier"/> at this distance (m).</summary>
        public float falloffEnd = 80f;
        public float minDamageMultiplier = 0.6f;
        /// <summary>Hits beyond this distance do nothing (m).</summary>
        public float maxRange = 150f;

        // ---- Handling ----
        /// <summary>Trigger cycles per second (for Burst: bursts per second).</summary>
        public float roundsPerSecond = 9f;
        public int magazineSize = 30;
        /// <summary>Spare ammunition (of <see cref="ammoType"/>) added to the inventory when this is a starting weapon.</summary>
        public int startingReserve = 90;
        public float reloadSeconds = 2f;
        public int burstCount = 3;
        public float burstInterval = 0.07f;
        /// <summary>Character speed multiplier while this weapon is active.</summary>
        public float moveSpeedMultiplier = 1f;

        // ---- Accuracy (degrees) ----
        public float hipSpread = 2.5f;
        public float aimSpread = 0.8f;
        /// <summary>Spread added per shot (recoil bloom), capped at <see cref="maxBloom"/>, recovering over time.</summary>
        public float bloomPerShot = 0.35f;
        public float maxBloom = 4f;
        /// <summary>Must stay below bloomPerShot x roundsPerSecond, or sustained fire never loses accuracy.</summary>
        public float bloomRecoveryPerSecond = 2f;
        /// <summary>Visual camera kick per shot (client only; gameplay recoil is the bloom above).</summary>
        public float recoilKick = 0.6f;

        // ---- Melee ----
        public float meleeReach = 2.2f;
        public float meleeWindup = 0.2f;
        public float meleeCooldown = 0.6f;
        public float meleeArcDegrees = 100f;

        // ---- Loadout balancing (Phase 6) ----
        public float powerScore = 10f;

        public bool IsRanged => category == WeaponCategory.Ranged;
        public bool IsMelee => category == WeaponCategory.Melee;
        public float FireInterval => roundsPerSecond > 0f ? 1f / roundsPerSecond : float.PositiveInfinity;

        public WeaponStats Clone() => (WeaponStats)MemberwiseClone();
    }
}
