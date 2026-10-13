using System;

namespace WarOfNyx.Core.Zone
{
    /// <summary>A circle on the ground plane (X/Z, metres).</summary>
    public readonly struct ZoneCircle : IEquatable<ZoneCircle>
    {
        public readonly float X;
        public readonly float Z;
        public readonly float Radius;

        public ZoneCircle(float x, float z, float radius)
        {
            X = x;
            Z = z;
            Radius = radius < 0f ? 0f : radius;
        }

        /// <summary>Inside means on or within the edge. A closed circle (radius 0) contains nobody.</summary>
        public bool Contains(float x, float z)
        {
            if (Radius <= 0f)
                return false;
            float dx = x - X;
            float dz = z - Z;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        /// <summary>Metres to the edge: negative inside, positive outside.</summary>
        public float DistanceToEdge(float x, float z)
        {
            float dx = x - X;
            float dz = z - Z;
            return MathF.Sqrt(dx * dx + dz * dz) - Radius;
        }

        /// <summary>True when <paramref name="inner"/> lies entirely inside this circle (spec 10).</summary>
        public bool ContainsCircle(in ZoneCircle inner, float tolerance = 0.001f)
        {
            float dx = inner.X - X;
            float dz = inner.Z - Z;
            return MathF.Sqrt(dx * dx + dz * dz) + inner.Radius <= Radius + tolerance;
        }

        public static ZoneCircle Lerp(in ZoneCircle a, in ZoneCircle b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new ZoneCircle(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t, a.Radius + (b.Radius - a.Radius) * t);
        }

        public bool Equals(ZoneCircle other) => X == other.X && Z == other.Z && Radius == other.Radius;
        public override bool Equals(object obj) => obj is ZoneCircle c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(X, Z, Radius);
        public override string ToString() => $"({X:0.0}, {Z:0.0}) r={Radius:0.0}";
    }

    /// <summary>World check used to keep circles on fair ground (implemented with physics/navmesh in Unity).</summary>
    public interface IZoneCentreValidator
    {
        /// <summary>Fraction (0-1) of sample points inside the circle that are playable ground (not water, cliffs or off-map).</summary>
        float PlayableFraction(float x, float z, float radius);
    }

    /// <summary>
    /// The whole match's circles, decided by the server when the match starts:
    /// <c>Circles[0]</c> is the starting safe zone and <c>Circles[i + 1]</c> is where phase i ends.
    /// </summary>
    public sealed class ZonePlan
    {
        public ZonePlan(ZoneCircle[] circles, float scale)
        {
            Circles = circles ?? throw new ArgumentNullException(nameof(circles));
            Scale = scale;
        }

        public ZoneCircle[] Circles { get; }
        /// <summary>Map radius ÷ spec radius (1 on the 1.2 km NYX ISLAND, about 0.11 on the 120 m test arena).</summary>
        public float Scale { get; }
        public int StageCount => Circles.Length - 1;
        public ZoneCircle Start => Circles[0];
    }

    /// <summary>
    /// Server-side choice of every circle centre (spec 10).
    ///   * Radii follow the configured phase table, scaled to the map's starting safe zone.
    ///   * Each next circle is entirely inside the previous one: its centre is sampled uniformly
    ///     within (previous radius − new radius) of the previous centre.
    ///   * Fairness: each candidate is scored by the world validator (share of playable ground);
    ///     the first candidate above the threshold wins, otherwise the best one found.
    /// Deterministic for a given seed and validator.
    /// </summary>
    public static class ZonePlanner
    {
        public static ZonePlan Plan(ZoneSettings settings, float centreX, float centreZ, float mapStartRadius,
            int seed, IZoneCentreValidator validator = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));

            float scale = settings.ScaleFor(mapStartRadius);
            var circles = new ZoneCircle[settings.StageCount + 1];
            circles[0] = new ZoneCircle(centreX, centreZ, settings.stages[0].startRadius * scale);
            var random = new Random(seed);

            for (int i = 0; i < settings.StageCount; i++)
            {
                var previous = circles[i];
                float radius = settings.stages[i].targetRadius * scale;
                circles[i + 1] = PickNext(previous, radius, settings, validator, random);
            }

            return new ZonePlan(circles, scale);
        }

        private static ZoneCircle PickNext(in ZoneCircle previous, float radius, ZoneSettings settings,
            IZoneCentreValidator validator, Random random)
        {
            // Small safety margin so float error never puts the new edge outside the old one.
            float maxShift = Math.Max(0f, previous.Radius - radius) * 0.999f;
            int tries = Math.Max(1, settings.centreCandidates);

            ZoneCircle best = new ZoneCircle(previous.X, previous.Z, radius);
            float bestScore = float.NegativeInfinity;

            for (int k = 0; k < tries; k++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                double distance = maxShift * Math.Sqrt(random.NextDouble()); // uniform over the disc
                var candidate = new ZoneCircle(
                    previous.X + (float)(Math.Sin(angle) * distance),
                    previous.Z + (float)(Math.Cos(angle) * distance),
                    radius);

                float score = validator == null ? 1f : Clamp01(validator.PlayableFraction(candidate.X, candidate.Z, Math.Max(radius, 0.5f)));
                if (score >= settings.minPlayableFraction)
                    return candidate;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
