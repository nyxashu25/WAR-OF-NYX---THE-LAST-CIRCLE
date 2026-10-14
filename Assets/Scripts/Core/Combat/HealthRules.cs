using System;

namespace WarOfNyx.Core.Combat
{
    public enum DamageKind : byte
    {
        Weapon = 0,
        Melee = 1,
        Zone = 2,
        Camping = 3,
        Fall = 4,
        Environment = 5,
        /// <summary>Run over, crashed, or caught in a vehicle explosion (the authority applies it; armour does not reduce it).</summary>
        Vehicle = 6,
    }

    public enum DamageBlockReason : byte
    {
        None = 0,
        AlreadyDead = 1,
        SpawnProtected = 2,
        InvalidAmount = 3,
    }

    public struct HealthState
    {
        public float Health;
        public float MaxHealth;
        /// <summary>0 = none, 1-3 = armor levels (spec 15). Loadouts grant it in Phase 6.</summary>
        public int ArmorLevel;
        public bool Alive;

        public static HealthState Full(float max, int armorLevel = 0) =>
            new HealthState { Health = max, MaxHealth = max, ArmorLevel = armorLevel, Alive = true };
    }

    public struct DamageResult
    {
        public float Applied;
        public bool Killed;
        public DamageBlockReason Blocked;
    }

    /// <summary>
    /// Authoritative health and damage rules (spec 15). Only the server calls these; clients
    /// receive the resulting health. Clients can never report damage.
    /// </summary>
    public static class HealthRules
    {
        private static readonly float[] ArmorReduction = { 0f, 0.15f, 0.25f, 0.35f };

        /// <summary>Armor only reduces weapon and melee damage; zone and camping damage ignore it (spec 12, 13, 15).</summary>
        public static float ArmorMultiplier(int armorLevel, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Melee)
                return 1f;
            int level = Math.Max(0, Math.Min(ArmorReduction.Length - 1, armorLevel));
            return 1f - ArmorReduction[level];
        }

        /// <param name="spawnProtected">Spawn protection blocks player-dealt damage (weapon, melee) only.</param>
        public static DamageResult ApplyDamage(ref HealthState h, float amount, DamageKind kind, bool spawnProtected)
        {
            var result = new DamageResult();
            if (!h.Alive)
            {
                result.Blocked = DamageBlockReason.AlreadyDead;
                return result;
            }
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            {
                result.Blocked = DamageBlockReason.InvalidAmount;
                return result;
            }
            if (spawnProtected && (kind == DamageKind.Weapon || kind == DamageKind.Melee))
            {
                result.Blocked = DamageBlockReason.SpawnProtected;
                return result;
            }

            float applied = Math.Min(h.Health, amount * ArmorMultiplier(h.ArmorLevel, kind));
            h.Health -= applied;
            result.Applied = applied;

            if (h.Health <= 0.0001f)
            {
                h.Health = 0f;
                h.Alive = false;
                result.Killed = true;
            }
            return result;
        }

        /// <summary>Heals up to the maximum (spec 15: healing cannot exceed max health). Returns the amount healed.</summary>
        public static float Heal(ref HealthState h, float amount)
        {
            if (!h.Alive || float.IsNaN(amount) || amount <= 0f)
                return 0f;
            float healed = Math.Min(amount, h.MaxHealth - h.Health);
            h.Health += healed;
            return healed;
        }
    }
}
