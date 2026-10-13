using System;
using System.Collections.Generic;

namespace WarOfNyx.Core.Match
{
    /// <summary>World-side spawn checks, implemented by the physics/navmesh layer.</summary>
    public interface ISpawnValidator
    {
        /// <summary>
        /// True when a character can stand at (x, z): walkable ground found (returned as
        /// <paramref name="groundY"/>), on the navmesh, no geometry overlapping the capsule,
        /// not in water, not inside closed geometry.
        /// </summary>
        bool TryValidate(float x, float z, out float groundY);
    }

    public readonly struct SpawnPoint
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        /// <summary>Facing, degrees (0 = +Z). Spawns face the zone centre so players start oriented.</summary>
        public readonly float Yaw;

        public SpawnPoint(float x, float y, float z, float yaw)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }
    }

    public sealed class SpawnPlan
    {
        public SpawnPoint[] Points { get; internal set; } = Array.Empty<SpawnPoint>();
        public bool Success { get; internal set; }
        /// <summary>The separation actually achieved (may be relaxed below the configured minimum).</summary>
        public float Separation { get; internal set; }
        public bool Relaxed { get; internal set; }
        public int ValidCandidates { get; internal set; }
        public string Failure { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Server-side random spawn selection (master spec 6).
    ///
    /// 1. Sample candidate points uniformly inside the starting safe zone (minus an edge margin).
    /// 2. Keep only candidates the world validator approves (ground, navmesh, no overlap, no water).
    /// 3. Pick spawns by randomized farthest-point sampling: each new spawn is the valid candidate
    ///    farthest from all spawns so far, which spreads players across different regions.
    /// 4. Require the configured separation (65 m); if impossible, relax step by step
    ///    (50 → 40 → 25 m) and finally to the hard floor. Never place anyone at an invalid point.
    ///
    /// Deterministic for a given seed and validator, which makes it testable and replayable.
    /// </summary>
    public static class SpawnPlanner
    {
        public const int MinCandidates = 96;
        public const int CandidatesPerPlayer = 32;

        public static SpawnPlan Plan(int playerCount, float centreX, float centreZ, float zoneRadius,
            MatchRules rules, ISpawnValidator validator, int seed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (validator == null) throw new ArgumentNullException(nameof(validator));

            var plan = new SpawnPlan();
            if (playerCount <= 0)
            {
                plan.Success = true;
                return plan;
            }

            float radius = Math.Max(0f, zoneRadius - rules.spawnEdgeMargin);
            var random = new Random(seed);
            var candidates = SampleCandidates(playerCount, centreX, centreZ, radius, validator, random);
            plan.ValidCandidates = candidates.Count;

            if (candidates.Count < playerCount)
            {
                plan.Failure = $"only {candidates.Count} valid spawn candidates for {playerCount} players";
                return plan;
            }

            foreach (float separation in SeparationSteps(rules))
            {
                var chosen = FarthestPointPick(candidates, playerCount, separation, random);
                if (chosen == null)
                    continue;

                plan.Points = chosen.ConvertAll(c => new SpawnPoint(c.X, c.Y, c.Z, YawTowards(c.X, c.Z, centreX, centreZ))).ToArray();
                plan.Success = true;
                plan.Separation = separation;
                plan.Relaxed = separation < rules.minSpawnSeparation;
                return plan;
            }

            plan.Failure = $"cannot separate {playerCount} players by even {rules.separationHardFloor} m";
            return plan;
        }

        private struct Candidate
        {
            public float X, Y, Z;
        }

        private static List<Candidate> SampleCandidates(int players, float cx, float cz, float radius,
            ISpawnValidator validator, Random random)
        {
            int samples = Math.Max(MinCandidates, players * CandidatesPerPlayer);
            var list = new List<Candidate>(samples);
            for (int i = 0; i < samples; i++)
            {
                // Uniform over the disc area: r = R * sqrt(u).
                double angle = random.NextDouble() * Math.PI * 2.0;
                double r = radius * Math.Sqrt(random.NextDouble());
                float x = cx + (float)(Math.Sin(angle) * r);
                float z = cz + (float)(Math.Cos(angle) * r);
                if (validator.TryValidate(x, z, out float y))
                    list.Add(new Candidate { X = x, Y = y, Z = z });
            }
            return list;
        }

        private static IEnumerable<float> SeparationSteps(MatchRules rules)
        {
            yield return rules.minSpawnSeparation;
            if (rules.separationRelaxSteps != null)
            {
                foreach (float step in rules.separationRelaxSteps)
                {
                    if (step < rules.minSpawnSeparation && step > rules.separationHardFloor)
                        yield return step;
                }
            }
            yield return rules.separationHardFloor;
        }

        /// <summary>Returns null when <paramref name="count"/> points at least <paramref name="separation"/> apart can't be found.</summary>
        private static List<Candidate> FarthestPointPick(List<Candidate> candidates, int count, float separation, Random random)
        {
            var chosen = new List<Candidate>(count) { candidates[random.Next(candidates.Count)] };
            float minSq = separation * separation;

            // Distance from every candidate to its nearest chosen point.
            var nearestSq = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                nearestSq[i] = DistSq(candidates[i], chosen[0]);

            while (chosen.Count < count)
            {
                int best = -1;
                float bestSq = -1f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    // Small random jitter breaks ties so equal layouts don't always produce identical picks.
                    float score = nearestSq[i] * (1f + (float)random.NextDouble() * 0.05f);
                    if (nearestSq[i] >= minSq && score > bestSq)
                    {
                        bestSq = score;
                        best = i;
                    }
                }

                if (best < 0)
                    return null;

                var pick = candidates[best];
                chosen.Add(pick);
                for (int i = 0; i < candidates.Count; i++)
                    nearestSq[i] = Math.Min(nearestSq[i], DistSq(candidates[i], pick));
            }

            return chosen;
        }

        private static float DistSq(Candidate a, Candidate b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static float YawTowards(float x, float z, float cx, float cz)
        {
            float dx = cx - x;
            float dz = cz - z;
            if (dx * dx + dz * dz < 1e-6f)
                return 0f;
            float yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            return yaw < 0f ? yaw + 360f : yaw;
        }
    }
}
