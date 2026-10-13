using System;

namespace WarOfNyx.Core.Combat
{
    /// <summary>Mutable per-weapon state, owned by the authority and networked (spec 7: "keep mutable
    /// inventory and ammunition authoritative on the server").</summary>
    public struct WeaponState
    {
        public short WeaponId;
        public int Magazine;
        public int Reserve;
        public float NextFireAt;
        /// <summary>0 = not reloading.</summary>
        public float ReloadEndsAt;
        public int BurstRemaining;
        public float NextBurstShotAt;
        public float Bloom;
        public float BloomUpdatedAt;
        /// <summary>Total shots fired; seeds the deterministic spread so server and prediction agree.</summary>
        public int ShotsFired;

        public bool IsEmptySlot => WeaponId < 0;
        public bool IsReloading => ReloadEndsAt > 0f;

        public static WeaponState Empty => new WeaponState { WeaponId = WeaponCatalog.NoWeapon };

        public static WeaponState Create(short weaponId)
        {
            var stats = WeaponCatalog.Get(weaponId);
            if (stats == null)
                return Empty;
            return new WeaponState
            {
                WeaponId = weaponId,
                Magazine = stats.IsRanged ? stats.magazineSize : 0,
                Reserve = stats.IsRanged ? stats.startingReserve : 0,
            };
        }
    }

    public enum FireBlockReason : byte
    {
        None = 0,
        NoTrigger = 1,
        NotRanged = 2,
        Reloading = 3,
        Cooldown = 4,
        EmptyMagazine = 5,
        /// <summary>Spec 11: firearms are disabled outside the Nyx Circle (Phase 7 supplies the flag).</summary>
        FirearmsDisabled = 6,
    }

    public struct FireDecision
    {
        public bool Fired;
        public FireBlockReason Reason;
        /// <summary>The magazine is empty but reserve ammo exists: the caller should start a reload.</summary>
        public bool ShouldAutoReload;
        public int Pellets;
        /// <summary>Spread cone (degrees, full angle radius) used for this shot.</summary>
        public float SpreadDegrees;
        public int ShotIndex;
    }

    /// <summary>
    /// Server-authoritative firearm rules (spec 9): fire-rate enforcement, semi/auto/burst triggers,
    /// magazine + reserve ammunition, reloading, bloom-based recoil, damage falloff and headshots.
    /// Ammo is consumed ONLY when a shot is actually fired: a blocked attempt (cooldown, reload,
    /// empty, outside the circle) never costs ammunition (spec 11 rule 6).
    /// </summary>
    public static class WeaponRules
    {
        public static FireDecision TryFire(ref WeaponState s, WeaponStats w, float now,
            bool triggerHeld, bool triggerPressed, bool aiming, bool firearmsAllowed)
        {
            var decision = new FireDecision();
            if (w == null || !w.IsRanged)
            {
                decision.Reason = FireBlockReason.NotRanged;
                return decision;
            }

            UpdateReload(ref s, w, now);

            bool continuingBurst = w.fireMode == FireMode.Burst && s.BurstRemaining > 0;
            if (continuingBurst && now < s.NextBurstShotAt)
            {
                decision.Reason = FireBlockReason.Cooldown;
                return decision;
            }

            if (!continuingBurst)
            {
                bool trigger = w.fireMode == FireMode.Auto ? triggerHeld : triggerPressed;
                if (!trigger)
                {
                    decision.Reason = FireBlockReason.NoTrigger;
                    return decision;
                }
            }

            if (!firearmsAllowed)
            {
                s.BurstRemaining = 0;
                decision.Reason = FireBlockReason.FirearmsDisabled;
                return decision;
            }

            if (s.IsReloading)
            {
                decision.Reason = FireBlockReason.Reloading;
                return decision;
            }

            if (!continuingBurst && now < s.NextFireAt)
            {
                decision.Reason = FireBlockReason.Cooldown;
                return decision;
            }

            if (s.Magazine <= 0)
            {
                s.BurstRemaining = 0;
                decision.Reason = FireBlockReason.EmptyMagazine;
                decision.ShouldAutoReload = s.Reserve > 0;
                return decision;
            }

            // Fire one shot.
            decision.SpreadDegrees = CurrentSpread(s, w, now, aiming);
            s.Magazine--;
            decision.ShotIndex = s.ShotsFired;
            s.ShotsFired++;
            AddBloom(ref s, w, now);

            if (continuingBurst)
            {
                s.BurstRemaining--;
                s.NextBurstShotAt = now + w.burstInterval;
            }
            else
            {
                s.NextFireAt = now + w.FireInterval;
                if (w.fireMode == FireMode.Burst)
                {
                    s.BurstRemaining = Math.Max(0, w.burstCount - 1);
                    s.NextBurstShotAt = now + w.burstInterval;
                }
            }

            decision.Fired = true;
            decision.Pellets = Math.Max(1, w.pellets);
            decision.ShouldAutoReload = s.Magazine == 0 && s.Reserve > 0;
            return decision;
        }

        public static bool TryStartReload(ref WeaponState s, WeaponStats w, float now)
        {
            if (w == null || !w.IsRanged || s.IsReloading || s.Magazine >= w.magazineSize || s.Reserve <= 0)
                return false;
            s.ReloadEndsAt = now + w.reloadSeconds;
            s.BurstRemaining = 0;
            return true;
        }

        /// <summary>Completes a reload whose time is up: moves ammo from reserve into the magazine.</summary>
        public static bool UpdateReload(ref WeaponState s, WeaponStats w, float now)
        {
            if (!s.IsReloading || now < s.ReloadEndsAt)
                return false;
            int take = Math.Min(w.magazineSize - s.Magazine, s.Reserve);
            s.Magazine += Math.Max(0, take);
            s.Reserve -= Math.Max(0, take);
            s.ReloadEndsAt = 0f;
            return true;
        }

        public static void CancelReload(ref WeaponState s)
        {
            s.ReloadEndsAt = 0f;
            s.BurstRemaining = 0;
        }

        /// <summary>Remaining reload time in 0..1 for the HUD (1 = just started).</summary>
        public static float ReloadProgress(in WeaponState s, WeaponStats w, float now) =>
            s.IsReloading && w.reloadSeconds > 0f ? Clamp01(1f - (s.ReloadEndsAt - now) / w.reloadSeconds) : 0f;

        public static float CurrentSpread(in WeaponState s, WeaponStats w, float now, bool aiming)
        {
            float bloom = Math.Max(0f, s.Bloom - w.bloomRecoveryPerSecond * Math.Max(0f, now - s.BloomUpdatedAt));
            return (aiming ? w.aimSpread : w.hipSpread) + bloom;
        }

        private static void AddBloom(ref WeaponState s, WeaponStats w, float now)
        {
            float decayed = Math.Max(0f, s.Bloom - w.bloomRecoveryPerSecond * Math.Max(0f, now - s.BloomUpdatedAt));
            s.Bloom = Math.Min(w.maxBloom, decayed + w.bloomPerShot);
            s.BloomUpdatedAt = now;
        }

        /// <summary>Damage of one pellet/bullet at a distance (falloff, max range, headshot).</summary>
        public static float DamageAt(WeaponStats w, float distance, bool headshot)
        {
            if (w == null || distance < 0f || distance > w.maxRange)
                return 0f;

            float multiplier = 1f;
            if (distance > w.falloffStart && w.falloffEnd > w.falloffStart)
            {
                float t = Clamp01((distance - w.falloffStart) / (w.falloffEnd - w.falloffStart));
                multiplier = 1f + (w.minDamageMultiplier - 1f) * t;
            }

            return w.damage * multiplier * (headshot ? w.headshotMultiplier : 1f);
        }

        /// <summary>
        /// Deterministic spread offset for one pellet: uniform inside a cone of <paramref name="spreadDegrees"/>.
        /// The same seed gives the same direction on the server and in client prediction.
        /// </summary>
        public static void SpreadOffset(int playerId, int shotIndex, int pellet, float spreadDegrees,
            out float yawOffset, out float pitchOffset)
        {
            uint h = Hash((uint)playerId * 73856093u ^ (uint)shotIndex * 19349663u ^ (uint)pellet * 83492791u);
            float u = (h & 0xFFFF) / 65535f;
            float v = ((h >> 16) & 0xFFFF) / 65535f;
            float radius = spreadDegrees * (float)Math.Sqrt(u);
            float angle = v * 2f * (float)Math.PI;
            yawOffset = radius * (float)Math.Cos(angle);
            pitchOffset = radius * (float)Math.Sin(angle);
        }

        private static uint Hash(uint x)
        {
            // Murmur3 finalizer: cheap, well distributed, identical on every platform.
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return x;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
