using System;

namespace WarOfNyx.Core.Match
{
    /// <summary>Actions that immediately end spawn protection (spec 6).</summary>
    [Flags]
    public enum AggressiveAction
    {
        None = 0,
        FiredWeapon = 1 << 0,
        MeleeAttack = 1 << 1,
        PickedUpCombatItem = 1 << 2,
        UsedAbility = 1 << 3,
    }

    /// <summary>
    /// Short post-spawn invulnerability (spec 6). Server-authoritative value type, stored in
    /// networked state. Abuse prevention: it can only be granted at spawn, never refreshed,
    /// and ANY aggressive action ends it immediately — so a protected player can never deal
    /// damage while still protected.
    /// </summary>
    public struct SpawnProtectionState
    {
        public float EndsAt;
        public bool Broken;

        public static SpawnProtectionState Grant(float startTime, float durationSeconds) =>
            new SpawnProtectionState { EndsAt = startTime + Math.Max(0f, durationSeconds), Broken = false };

        public static SpawnProtectionState None => new SpawnProtectionState { EndsAt = 0f, Broken = true };

        public bool IsActive(float now) => !Broken && now < EndsAt;

        public float Remaining(float now) => IsActive(now) ? EndsAt - now : 0f;

        /// <summary>Call BEFORE resolving the action, so the action itself is never protected.</summary>
        public void OnAggressiveAction(AggressiveAction action)
        {
            if (action != AggressiveAction.None)
                Broken = true;
        }
    }
}
