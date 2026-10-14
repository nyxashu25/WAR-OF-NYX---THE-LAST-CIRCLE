using System;

namespace WarOfNyx.Core.Vehicles
{
    /// <summary>Static definition of a vehicle type (engine-independent: the authority, tests and the Unity controller share it).</summary>
    public sealed class VehicleStats
    {
        public string Id = "vehicle";
        public string DisplayName = "Vehicle";
        public int Seats = 2;
        public float MaxHealth = 1000f;
        /// <summary>Top speed forwards and in reverse (m/s).</summary>
        public float TopSpeed = 30f;
        public float ReverseSpeed = 8f;
        /// <summary>Engine acceleration (m/s²) and braking deceleration (m/s²).</summary>
        public float Acceleration = 8f;
        public float Braking = 16f;
        /// <summary>Front-wheel steering lock at low speed (degrees).</summary>
        public float SteerDegrees = 32f;
        public float Wheelbase = 2.4f;
        public float TrackWidth = 1.6f;
        public float WheelRadius = 0.42f;
        public float Mass = 1200f;
        /// <summary>Body size (m): width, height, length.</summary>
        public float Width = 2f, Height = 1.4f, Length = 3.8f;
    }

    /// <summary>The built-in vehicles. The index is the network id: append only.</summary>
    public static class VehicleCatalog
    {
        public const string NyxBuggy = "nyx_buggy";
        public const string RaiderTruck = "raider_truck";
        public const string ScoutQuad = "scout_quad";

        public static readonly VehicleStats[] All =
        {
            // Fast, open two-seat off-roader: the all-rounder.
            new VehicleStats { Id = NyxBuggy, DisplayName = "Nyx Buggy", Seats = 2, MaxHealth = 900f, TopSpeed = 31f, ReverseSpeed = 9f, Acceleration = 9f,
                Braking = 18f, SteerDegrees = 32f, Wheelbase = 2.5f, TrackWidth = 1.7f, WheelRadius = 0.45f, Mass = 1100f, Width = 2.0f, Height = 1.5f, Length = 3.7f },
            // Armoured four-seat pickup: slower, takes much more punishment.
            new VehicleStats { Id = RaiderTruck, DisplayName = "Raider Truck", Seats = 4, MaxHealth = 1600f, TopSpeed = 26f, ReverseSpeed = 8f, Acceleration = 6.5f,
                Braking = 15f, SteerDegrees = 28f, Wheelbase = 3.1f, TrackWidth = 1.85f, WheelRadius = 0.5f, Mass = 2200f, Width = 2.2f, Height = 2.0f, Length = 5.0f },
            // Light one-seat quad: quickest off the line, fragile.
            new VehicleStats { Id = ScoutQuad, DisplayName = "Scout Quad", Seats = 1, MaxHealth = 600f, TopSpeed = 28f, ReverseSpeed = 7f, Acceleration = 11f,
                Braking = 20f, SteerDegrees = 36f, Wheelbase = 1.5f, TrackWidth = 1.2f, WheelRadius = 0.36f, Mass = 380f, Width = 1.3f, Height = 1.2f, Length = 2.1f },
        };

        public static VehicleStats Get(int index) => index >= 0 && index < All.Length ? All[index] : null;

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == id)
                    return i;
            return -1;
        }
    }

    /// <summary>
    /// Vehicle rules applied by the match authority (offline: the local match; never a client's claim):
    /// entering and seats, the arcade drive model (speed, steering), crash and run-over damage, and the
    /// explosion when a vehicle is destroyed.
    /// </summary>
    public static class VehicleRules
    {
        /// <summary>Distance from a character to the vehicle's centre within which they may get in (m).</summary>
        public const float EnterRange = 3.6f;
        /// <summary>Below this relative speed a vehicle only nudges characters (m/s, 14.4 km/h).</summary>
        public const float RunOverMinSpeed = 4f;
        /// <summary>Collisions softer than this do not damage the vehicle (m/s).</summary>
        public const float CrashMinSpeed = 6f;
        /// <summary>Occupants are hurt by crashes harder than this (m/s, 50 km/h).</summary>
        public const float OccupantCrashMinSpeed = 14f;
        public const float ExplosionRadius = 6f;
        public const float ExplosionDamage = 70f;
        /// <summary>A character can be run over at most once per this many seconds by the same vehicle.</summary>
        public const float RunOverCooldown = 1f;

        /// <summary>The first free seat (0 = driver) or -1.</summary>
        public static int FreeSeat(bool[] occupied, int seats)
        {
            for (int i = 0; i < seats && i < occupied.Length; i++)
                if (!occupied[i])
                    return i;
            return -1;
        }

        /// <summary>Null when the character may get in, otherwise the reason.</summary>
        public static string CanEnter(bool alive, bool alreadyInVehicle, bool vehicleDestroyed, float distance, int freeSeat)
        {
            if (!alive)
                return "Eliminated.";
            if (alreadyInVehicle)
                return "Already in a vehicle.";
            if (vehicleDestroyed)
                return "That vehicle is wrecked.";
            if (distance > EnterRange + 0.5f)
                return "Too far away.";
            if (freeSeat < 0)
                return "No free seat.";
            return null;
        }

        /// <summary>
        /// Arcade longitudinal model: the new forward speed (m/s, negative = reversing) after <paramref name="dt"/>.
        /// Throttle forward accelerates to the top speed; throttle against the motion brakes, then reverses;
        /// no throttle coasts down (rolling drag); the handbrake stops hard.
        /// </summary>
        public static float Accelerate(float speed, float throttle, bool handbrake, VehicleStats v, float dt)
        {
            throttle = Clamp(throttle, -1f, 1f);
            if (handbrake)
                return MoveTowards(speed, 0f, v.Braking * 1.2f * dt);
            if (Math.Abs(throttle) < 0.05f)
                return MoveTowards(speed, 0f, (2.5f + Math.Abs(speed) * 0.08f) * dt);
            bool braking = speed * throttle < 0f && Math.Abs(speed) > 0.3f;
            if (braking)
                return MoveTowards(speed, 0f, v.Braking * Math.Abs(throttle) * dt);
            float target = throttle > 0f ? v.TopSpeed * throttle : -v.ReverseSpeed * -throttle;
            // Acceleration fades as the engine nears top speed.
            float headroom = 1f - Clamp(Math.Abs(speed) / Math.Max(1f, Math.Abs(target)), 0f, 1f);
            float accel = v.Acceleration * (0.35f + 0.65f * headroom);
            return MoveTowards(speed, target, accel * dt);
        }

        /// <summary>Front-wheel angle (degrees): full lock when slow, less at speed so the vehicle stays controllable.</summary>
        public static float SteerAngle(float steer, float speed, VehicleStats v)
        {
            float factor = 1f - 0.6f * Clamp(Math.Abs(speed) / Math.Max(1f, v.TopSpeed), 0f, 1f);
            return Clamp(steer, -1f, 1f) * v.SteerDegrees * factor;
        }

        /// <summary>Tyres hold about 1.1 g of cornering; turning harder than this would slide or roll the vehicle.</summary>
        public const float MaxLateralAcceleration = 11f;

        /// <summary>
        /// Yaw rate (degrees per second) of a bicycle model at this speed and wheel angle, capped so the cornering
        /// acceleration (speed x yaw rate) never exceeds <see cref="MaxLateralAcceleration"/>.
        /// </summary>
        public static float YawRate(float speed, float steerDegrees, VehicleStats v)
        {
            double rate = speed * Math.Tan(steerDegrees * Math.PI / 180.0) / Math.Max(0.5f, v.Wheelbase);
            double cap = MaxLateralAcceleration / Math.Max(1.0, Math.Abs(speed));
            rate = Math.Max(-cap, Math.Min(cap, rate));
            return (float)(rate * 180.0 / Math.PI);
        }

        /// <summary>Damage to a character hit by a vehicle at this relative speed (m/s): 0 below 4 m/s, lethal around 15 m/s.</summary>
        public static float RunOverDamage(float relativeSpeed)
        {
            if (relativeSpeed < RunOverMinSpeed)
                return 0f;
            return Clamp(15f + (relativeSpeed - RunOverMinSpeed) * 8f, 0f, 150f);
        }

        /// <summary>Damage the vehicle takes from a collision at this impact speed (m/s, along the contact normal).</summary>
        public static float CrashDamage(float impactSpeed)
        {
            if (impactSpeed < CrashMinSpeed)
                return 0f;
            float over = impactSpeed - CrashMinSpeed;
            return over * over * 2.5f;
        }

        /// <summary>Damage each occupant takes from the same crash.</summary>
        public static float OccupantCrashDamage(float impactSpeed) =>
            impactSpeed < OccupantCrashMinSpeed ? 0f : (impactSpeed - OccupantCrashMinSpeed) * 6f;

        /// <summary>Explosion damage at a distance from a destroyed vehicle (linear falloff to 0 at the radius).</summary>
        public static float ExplosionDamageAt(float distance) =>
            distance >= ExplosionRadius ? 0f : ExplosionDamage * (1f - Math.Max(0f, distance) / ExplosionRadius);

        /// <summary>How many vehicles an arena of this radius gets (offline): about one per 70 m of radius, 3 to 14.</summary>
        public static int SpawnCount(float arenaRadius) => (int)Clamp((float)Math.Round(arenaRadius / 70f), 3f, 14f);

        /// <summary>Which vehicle type a spawn slot gets: buggies most often, trucks and quads less (deterministic).</summary>
        public static int TypeFor(int seed, int slot)
        {
            var random = new Random(unchecked(seed * 73 + slot * 9973));
            double r = random.NextDouble();
            return r < 0.5 ? 0 : r < 0.78 ? 1 : 2;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

        private static float MoveTowards(float current, float target, float maxDelta) =>
            Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
    }
}
