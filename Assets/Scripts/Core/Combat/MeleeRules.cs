using System;

namespace WarOfNyx.Core.Combat
{
    public struct MeleeState
    {
        /// <summary>Server time the current swing started; 0 = no swing in progress.</summary>
        public float SwingStartedAt;
        public float NextSwingAt;
        /// <summary>Counts swings; drives swing animation/FX on every client.</summary>
        public int SwingCount;

        public bool InProgress => SwingStartedAt > 0f;
    }

    /// <summary>
    /// Melee (spec 9): windup → hit check → cooldown. Melee stays usable outside the Nyx Circle,
    /// so these rules deliberately never consult the firearm restriction.
    /// The hit check itself (lag-compensated overlap + line of sight) is done by the physics layer;
    /// the pure reach/arc geometry lives here.
    /// </summary>
    public static class MeleeRules
    {
        public static bool TryStartSwing(ref MeleeState m, WeaponStats w, float now)
        {
            if (w == null || !w.IsMelee || m.InProgress || now < m.NextSwingAt)
                return false;
            m.SwingStartedAt = now;
            m.NextSwingAt = now + w.meleeWindup + w.meleeCooldown;
            m.SwingCount++;
            return true;
        }

        /// <summary>True exactly once per swing, when the windup has elapsed: resolve hits now.</summary>
        public static bool TryResolve(ref MeleeState m, WeaponStats w, float now)
        {
            if (!m.InProgress || w == null || now < m.SwingStartedAt + w.meleeWindup)
                return false;
            m.SwingStartedAt = 0f;
            return true;
        }

        public static void Cancel(ref MeleeState m) => m.SwingStartedAt = 0f;

        /// <summary>
        /// Whether a target is within reach and inside the swing arc (horizontal plane).
        /// Yaw convention: 0° = +Z, 90° = +X.
        /// </summary>
        public static bool InArc(float attackerX, float attackerZ, float yawDegrees, float targetX, float targetZ,
            float reach, float arcDegrees)
        {
            float dx = targetX - attackerX;
            float dz = targetZ - attackerZ;
            float distSq = dx * dx + dz * dz;
            if (distSq > reach * reach)
                return false;
            if (distSq < 1e-6f)
                return true;

            float rad = yawDegrees * (float)Math.PI / 180f;
            float fx = (float)Math.Sin(rad);
            float fz = (float)Math.Cos(rad);
            float cos = (fx * dx + fz * dz) / (float)Math.Sqrt(distSq);
            float halfArc = arcDegrees * 0.5f * (float)Math.PI / 180f;
            return cos >= (float)Math.Cos(halfArc);
        }
    }
}
