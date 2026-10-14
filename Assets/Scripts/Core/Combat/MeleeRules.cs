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
        /// <summary>Position in the weapon's combo chain of the current / last swing (0 = opener).</summary>
        public byte ComboStep;

        public bool InProgress => SwingStartedAt > 0f;
    }

    /// <summary>How a melee weapon fights: which combo chain it uses.</summary>
    public enum MeleeStyle : byte
    {
        /// <summary>Long two-handed blade (katana): slash, backhand, overhead, thrust.</summary>
        Blade = 0,
        /// <summary>Short one-handed blade (knife, dagger): slash, backhand, stab.</summary>
        Knife = 1,
        /// <summary>Energy blade: slash, rising slash, spin.</summary>
        Energy = 2,
    }

    /// <summary>The shape of one attack. Animation, slash trail and the hit check all follow it.</summary>
    public enum MeleePattern : byte
    {
        HorizontalSlash = 0,
        BackhandSlash = 1,
        OverheadStrike = 2,
        Thrust = 3,
        RisingSlash = 4,
        SpinSlash = 5,
        QuickStab = 6,
    }

    /// <summary>One attack of a combo: its pattern and how it changes the weapon's base numbers.</summary>
    public readonly struct MeleeAttack
    {
        public readonly MeleePattern Pattern;
        public readonly float ArcDegrees;
        /// <summary>Metres added to the weapon's reach (thrusts reach further, spins less).</summary>
        public readonly float ReachBonus;
        public readonly float DamageMultiplier;
        public readonly float WindupMultiplier;
        public readonly float CooldownMultiplier;

        public MeleeAttack(MeleePattern pattern, float arc, float reachBonus, float damage, float windup, float cooldown)
        {
            Pattern = pattern;
            ArcDegrees = arc;
            ReachBonus = reachBonus;
            DamageMultiplier = damage;
            WindupMultiplier = windup;
            CooldownMultiplier = cooldown;
        }
    }

    /// <summary>
    /// Melee (spec 9): windup → hit check → cooldown, in combo chains (<see cref="Combo"/>): pressing again
    /// soon after a swing continues with the next attack of the weapon's chain. Melee stays usable outside the Nyx Circle,
    /// so these rules deliberately never consult the firearm restriction.
    /// The hit check itself (lag-compensated overlap + line of sight) is done by the physics layer;
    /// the pure reach/arc geometry lives here.
    /// </summary>
    public static class MeleeRules
    {
        /// <summary>
        /// Seconds after a swing's recovery ends in which the next press continues the combo. Later presses
        /// (or finishing the chain) start again from the opener.
        /// </summary>
        public const float ComboWindowSeconds = 0.6f;

        private static readonly MeleeAttack[] BladeCombo =
        {
            new MeleeAttack(MeleePattern.HorizontalSlash, arc: 110f, reachBonus: 0f, damage: 1f, windup: 1f, cooldown: 0.7f),
            new MeleeAttack(MeleePattern.BackhandSlash, arc: 100f, reachBonus: 0f, damage: 1f, windup: 0.85f, cooldown: 0.7f),
            new MeleeAttack(MeleePattern.OverheadStrike, arc: 55f, reachBonus: 0.3f, damage: 1.4f, windup: 1.35f, cooldown: 0.9f),
            new MeleeAttack(MeleePattern.Thrust, arc: 30f, reachBonus: 0.9f, damage: 1.25f, windup: 1.1f, cooldown: 1.5f),
        };

        private static readonly MeleeAttack[] KnifeCombo =
        {
            new MeleeAttack(MeleePattern.HorizontalSlash, arc: 90f, reachBonus: 0f, damage: 1f, windup: 1f, cooldown: 0.7f),
            new MeleeAttack(MeleePattern.BackhandSlash, arc: 90f, reachBonus: 0f, damage: 1f, windup: 0.9f, cooldown: 0.7f),
            new MeleeAttack(MeleePattern.QuickStab, arc: 35f, reachBonus: 0.3f, damage: 1.3f, windup: 1f, cooldown: 1.3f),
        };

        private static readonly MeleeAttack[] EnergyCombo =
        {
            new MeleeAttack(MeleePattern.HorizontalSlash, arc: 115f, reachBonus: 0f, damage: 1f, windup: 1f, cooldown: 0.7f),
            new MeleeAttack(MeleePattern.RisingSlash, arc: 90f, reachBonus: 0.1f, damage: 1.05f, windup: 0.9f, cooldown: 0.75f),
            new MeleeAttack(MeleePattern.SpinSlash, arc: 360f, reachBonus: -0.2f, damage: 0.9f, windup: 1.3f, cooldown: 1.4f),
        };

        /// <summary>The combo chain of a melee weapon (every attack in order).</summary>
        public static MeleeAttack[] Combo(WeaponStats w)
        {
            switch (w != null ? w.meleeStyle : MeleeStyle.Blade)
            {
                case MeleeStyle.Knife: return KnifeCombo;
                case MeleeStyle.Energy: return EnergyCombo;
                default: return BladeCombo;
            }
        }

        /// <summary>The attack of the current (or last) swing.</summary>
        public static MeleeAttack Current(in MeleeState m, WeaponStats w)
        {
            var combo = Combo(w);
            return combo[m.ComboStep % combo.Length];
        }

        public static float Reach(WeaponStats w, in MeleeAttack a) => Math.Max(0.5f, w.meleeReach + a.ReachBonus);

        public static float Damage(WeaponStats w, in MeleeAttack a) => w.damage * a.DamageMultiplier;

        public static bool TryStartSwing(ref MeleeState m, WeaponStats w, float now)
        {
            if (w == null || !w.IsMelee || m.InProgress || now < m.NextSwingAt)
                return false;
            var combo = Combo(w);
            bool chained = m.SwingCount > 0 && now <= m.NextSwingAt + ComboWindowSeconds && m.ComboStep + 1 < combo.Length;
            m.ComboStep = (byte)(chained ? m.ComboStep + 1 : 0);
            var attack = combo[m.ComboStep];
            m.SwingStartedAt = now;
            m.NextSwingAt = now + w.meleeWindup * attack.WindupMultiplier + w.meleeCooldown * attack.CooldownMultiplier;
            m.SwingCount++;
            return true;
        }

        /// <summary>True exactly once per swing, when the windup has elapsed: resolve hits now.</summary>
        public static bool TryResolve(ref MeleeState m, WeaponStats w, float now)
        {
            if (!m.InProgress || w == null || now < m.SwingStartedAt + w.meleeWindup * Current(m, w).WindupMultiplier)
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
