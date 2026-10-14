namespace WarOfNyx.Core.Combat
{
    /// <summary>
    /// When a hit on the local player counts as "critical" for presentation (blood on the screen): a headshot,
    /// a single big hit, or a hit that leaves the player badly hurt. Presentation only, never gameplay.
    /// </summary>
    public static class CriticalHit
    {
        public const float BigHit = 30f;
        public const float BadlyHurtFraction = 0.35f;

        public static bool Is(float damageApplied, bool headshot, float healthAfter, float maxHealth)
        {
            if (damageApplied <= 0f)
                return false;
            if (headshot || damageApplied >= BigHit)
                return true;
            return maxHealth > 0f && healthAfter / maxHealth <= BadlyHurtFraction;
        }

        /// <summary>How many splatters to add: more for headshots and very big hits (1..3).</summary>
        public static int Splatters(float damageApplied, bool headshot) =>
            headshot || damageApplied >= 45f ? 3 : damageApplied >= BigHit ? 2 : 1;

        /// <summary>Low-health vignette strength 0..1: starts at 60 % health, full at 10 %.</summary>
        public static float Vignette(float healthFraction)
        {
            float t = (0.6f - healthFraction) / 0.5f;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>
        /// A splatter that appeared at <paramref name="healthAtHit"/> clears once the player has healed clearly above it
        /// (by 15 % of max health) or is back to at least 90 %.
        /// </summary>
        public static bool HealedPast(float healthAtHitFraction, float healthNowFraction) =>
            healthNowFraction >= 0.9f || healthNowFraction >= healthAtHitFraction + 0.15f;
    }
}
