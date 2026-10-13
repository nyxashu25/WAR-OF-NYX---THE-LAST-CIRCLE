using System;

namespace WarOfNyx.Core.Zone
{
    /// <summary>
    /// Outside-circle rules (spec 11-12), evaluated by the server on the player's AUTHORITATIVE body
    /// position. Nothing here takes a client claim: the input stream has no "inside" flag, and the
    /// server classifies its own simulated position each tick (client prediction runs the same
    /// check on its predicted position only to show the result early).
    /// </summary>
    public static class ZoneRules
    {
        /// <summary>Spec 11: melee is never restricted by the circle.</summary>
        public const bool MeleeAllowedOutside = true;

        /// <summary>Inside the current circle? Before the circle exists (lobby, countdown) everyone counts as inside.</summary>
        public static bool IsInside(bool zoneActive, in ZoneCircle current, float x, float z) =>
            !zoneActive || current.Contains(x, z);

        /// <summary>Spec 11: ranged firing needs the shooter's body inside the circle.</summary>
        public static bool FirearmsAllowed(bool zoneActive, in ZoneCircle current, float x, float z) =>
            IsInside(zoneActive, current, x, z);
    }

    /// <summary>Per-player zone damage accumulator (networked on the server).</summary>
    public struct ZoneExposure
    {
        /// <summary>Time spent outside not yet charged as damage (always below one interval).</summary>
        public float Pending;
        public bool Outside;
    }

    public struct ZoneExposureResult
    {
        /// <summary>Damage to apply this tick (0 most ticks; a pulse every damage interval while outside).</summary>
        public float Damage;
        public bool Left;
        public bool Entered;
    }

    /// <summary>
    /// Zone damage (spec 12): while outside, every <c>damageIntervalSeconds</c> of exposure deals
    /// damage-per-second × interval. Re-entering stops it at once. The partial interval is kept
    /// (never refunded, never charged while inside), so stepping in and out of the edge can't
    /// dodge damage. Called once per server tick, so damage can never be applied twice in a tick.
    /// </summary>
    public static class ZoneDamage
    {
        private const float Epsilon = 1e-4f;

        public static ZoneExposureResult Step(ref ZoneExposure e, bool inside, float damagePerSecond, float dt, float interval)
        {
            var result = new ZoneExposureResult();
            if (inside)
            {
                result.Entered = e.Outside;
                e.Outside = false;
                return result;
            }

            result.Left = !e.Outside;
            e.Outside = true;
            if (dt <= 0f || interval <= 0f || damagePerSecond <= 0f || float.IsNaN(dt))
                return result;

            e.Pending += dt;
            while (e.Pending >= interval - Epsilon)
            {
                e.Pending = Math.Max(0f, e.Pending - interval);
                result.Damage += damagePerSecond * interval;
            }
            return result;
        }
    }

    /// <summary>
    /// Outside-circle instability (spec 12): a gentle left-right wobble of the movement direction
    /// plus a small aim sway. Deterministic from the server tick and the player id, so the server
    /// and the predicting client compute the same value and the client cannot remove it. It never
    /// moves a player who isn't moving, never teleports and never turns the camera on its own.
    /// </summary>
    public static class ZoneInstability
    {
        private const double TwoPi = Math.PI * 2.0;

        /// <summary>Sideways disturbance as a fraction of the movement direction, in [-wobbleMax, wobbleMax].</summary>
        public static float Lateral(int tick, float tickSeconds, int playerId, ZoneSettings settings)
        {
            if (settings == null)
                return 0f;
            double t = tick * (double)tickSeconds;
            uint h = Hash(playerId);
            double phase = (h & 0xFFFF) / 65536.0 * TwoPi;
            double ampPhase = ((h >> 16) & 0xFFFF) / 65536.0 * TwoPi;

            // The peak itself drifts slowly between the configured 5 % and 8 %.
            double amp = settings.wobbleMin + (settings.wobbleMax - settings.wobbleMin) * (0.5 + 0.5 * Math.Sin(t * 0.73 + ampPhase));
            double period = Math.Max(0.2, settings.wobblePeriodSeconds);
            return (float)(amp * Math.Sin(TwoPi * t / period + phase));
        }

        /// <summary>Small deterministic aim offset (degrees) while outside.</summary>
        public static void AimSway(int tick, float tickSeconds, int playerId, ZoneSettings settings, out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            if (settings == null)
                return;
            double t = tick * (double)tickSeconds;
            uint h = Hash(playerId ^ 0x5bd1e995);
            double phase = (h & 0xFFFF) / 65536.0 * TwoPi;
            yaw = (float)(settings.aimSwayYawDegrees * Math.Sin(t * 2.1 + phase));
            pitch = (float)(settings.aimSwayPitchDegrees * Math.Sin(t * 3.3 + phase * 0.5));
        }

        private static uint Hash(int value)
        {
            unchecked
            {
                uint h = (uint)value * 0x9E3779B1u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
