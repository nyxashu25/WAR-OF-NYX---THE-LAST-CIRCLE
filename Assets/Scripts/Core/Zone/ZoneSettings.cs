using System;

namespace WarOfNyx.Core.Zone
{
    /// <summary>One shrink phase of the Nyx Circle (spec 10). Radii are in "reference metres" (the 500 m spec map).</summary>
    [Serializable]
    public sealed class CircleStage
    {
        public float startRadius;
        public float targetRadius;
        /// <summary>Spec 10: each phase lasts 60 s; the circle shrinks continuously for the whole phase.</summary>
        public float durationSeconds = 60f;
        /// <summary>Zone damage per second to players outside during this phase (spec 12). Ignores armor.</summary>
        public float damagePerSecond;

        public CircleStage() { }

        public CircleStage(float startRadius, float targetRadius, float durationSeconds, float damagePerSecond)
        {
            this.startRadius = startRadius;
            this.targetRadius = targetRadius;
            this.durationSeconds = durationSeconds;
            this.damagePerSecond = damagePerSecond;
        }
    }

    /// <summary>
    /// Tunable Nyx Circle rules (spec 10-12). Values marked [T] are starting points for balancing.
    /// The server's copy is authoritative; clients only display the replicated circle.
    /// </summary>
    [Serializable]
    public sealed class ZoneSettings
    {
        /// <summary>The spec 10 table. Index 8 is the final circle.</summary>
        public CircleStage[] stages = SpecStages();

        /// <summary>Seconds after "FIGHT!" before phase 1 starts shrinking. Spec 10: the first phase starts immediately. [T]</summary>
        public float delayBeforeFirstPhase;

        /// <summary>Zone damage is applied in pulses of this length (amount = damage per second × interval). [T]</summary>
        public float damageIntervalSeconds = 0.5f;

        /// <summary>"FINAL CIRCLE BEGINS IN 10 SECONDS" (spec 19).</summary>
        public float finalWarningSeconds = 10f;

        // ----- centre selection (spec 10: "select valid centres that do not create impossible or unfair endgame terrain")

        /// <summary>Candidate centres tried per phase; the first fair one wins, else the fairest.</summary>
        public int centreCandidates = 24;
        /// <summary>Fraction of sampled points inside a new circle that must be playable ground (not water or cliffs). [T]</summary>
        public float minPlayableFraction = 0.75f;

        // ----- outside-circle instability (spec 12)

        /// <summary>Peak sideways movement disturbance, as a fraction of the movement direction. Spec: about 5-8 %.</summary>
        public float wobbleMin = 0.05f;
        public float wobbleMax = 0.08f;
        /// <summary>Seconds per left-right wobble cycle. [T]</summary>
        public float wobblePeriodSeconds = 1.4f;
        /// <summary>Aim sway amplitude in degrees (applied to melee aim on the authority, and to the camera). [T]</summary>
        public float aimSwayYawDegrees = 1.5f;
        public float aimSwayPitchDegrees = 1.0f;

        public int StageCount => stages?.Length ?? 0;

        /// <summary>Radius of the first circle in the spec table (500 m). Maps are scaled against it.</summary>
        public float ReferenceStartRadius => StageCount > 0 ? stages[0].startRadius : 0f;

        /// <summary>Converts spec radii to a map whose starting safe zone is <paramref name="mapStartRadius"/>.</summary>
        public float ScaleFor(float mapStartRadius) =>
            ReferenceStartRadius > 0f && mapStartRadius > 0f ? mapStartRadius / ReferenceStartRadius : 1f;

        public float TotalSeconds
        {
            get
            {
                float total = Math.Max(0f, delayBeforeFirstPhase);
                for (int i = 0; i < StageCount; i++)
                    total += Math.Max(0f, stages[i].durationSeconds);
                return total;
            }
        }

        /// <summary>The spec 10 / spec 12 table: 9 phases of 60 s; 1 HP/s (1-3), 2 (4-6), 4 (7-8), 6 (final).</summary>
        public static CircleStage[] SpecStages() => new[]
        {
            new CircleStage(500f, 400f, 60f, 1f),
            new CircleStage(400f, 310f, 60f, 1f),
            new CircleStage(310f, 230f, 60f, 1f),
            new CircleStage(230f, 160f, 60f, 2f),
            new CircleStage(160f, 105f, 60f, 2f),
            new CircleStage(105f, 65f, 60f, 2f),
            new CircleStage(65f, 35f, 60f, 4f),
            new CircleStage(35f, 12f, 60f, 4f),
            new CircleStage(12f, 0f, 60f, 6f),
        };

        public ZoneSettings Clone()
        {
            var copy = (ZoneSettings)MemberwiseClone();
            copy.stages = new CircleStage[StageCount];
            for (int i = 0; i < StageCount; i++)
            {
                var s = stages[i];
                copy.stages[i] = new CircleStage(s.startRadius, s.targetRadius, s.durationSeconds, s.damagePerSecond);
            }
            return copy;
        }

        /// <summary>Dedicated-server override (-zoneSeconds) for quick tests. 0 or less keeps the configured durations.</summary>
        public ZoneSettings WithStageSeconds(float seconds)
        {
            var copy = Clone();
            if (seconds > 0f)
                foreach (var s in copy.stages)
                    s.durationSeconds = seconds;
            return copy;
        }

        /// <summary>Null when valid, otherwise a description of the first problem.</summary>
        public string Validate()
        {
            if (StageCount == 0)
                return "No circle stages.";
            for (int i = 0; i < StageCount; i++)
            {
                var s = stages[i];
                if (s == null)
                    return $"Stage {i + 1} is missing.";
                if (s.durationSeconds <= 0f)
                    return $"Stage {i + 1} must last longer than 0 s.";
                if (s.targetRadius < 0f || s.targetRadius > s.startRadius)
                    return $"Stage {i + 1} must shrink (target {s.targetRadius} > start {s.startRadius}).";
                if (s.damagePerSecond < 0f)
                    return $"Stage {i + 1} has negative damage.";
                if (i > 0 && Math.Abs(stages[i - 1].targetRadius - s.startRadius) > 0.001f)
                    return $"Stage {i + 1} must start where stage {i} ends ({stages[i - 1].targetRadius} m).";
            }
            if (wobbleMin < 0f || wobbleMax < wobbleMin || wobbleMax > 0.25f)
                return "Wobble must satisfy 0 <= min <= max <= 0.25.";
            if (damageIntervalSeconds <= 0f)
                return "Damage interval must be positive.";
            return null;
        }
    }
}
