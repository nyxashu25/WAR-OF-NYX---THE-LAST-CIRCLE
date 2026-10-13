using System;

namespace WarOfNyx.Core.Offline
{
    /// <summary>
    /// Tunables that make a difficulty "smarter, not unfair" (master spec 38.8 / 38.9).
    ///
    /// Every value here limits decision quality and execution speed. None of them grants
    /// knowledge a human could not have: perception still requires line of sight or a
    /// sound within hearing range, and weapons still obey ammo, reload, fire-rate and the
    /// outside-circle firearm lock. Those rules live in the shared gameplay layer, not here.
    ///
    /// These are PROVISIONAL balancing values, to be tuned in the Ghost Protocol phase.
    /// </summary>
    public sealed class BotDifficultyProfile
    {
        /// <summary>No difficulty may react faster than this (humanly plausible floor, 38.9).</summary>
        public const float HumanReactionFloorSeconds = 0.20f;

        /// <summary>No difficulty may aim with zero error (38.9: "high but imperfect").</summary>
        public const float MinimumAimErrorDegrees = 1.0f;

        /// <summary>Upper bound for bot sight; bots never out-see the client's draw distance.</summary>
        public const float MaxVisionRangeMeters = 150f;

        public BotDifficulty Difficulty { get; }

        /// <summary>Delay between first legitimate sighting and the first shot, sampled per engagement.</summary>
        public float ReactionTimeMinSeconds { get; }
        public float ReactionTimeMaxSeconds { get; }

        /// <summary>Random aim cone at close range, in degrees.</summary>
        public float BaseAimErrorDegrees { get; }

        /// <summary>Extra aim error added per 10 m of target distance.</summary>
        public float AimErrorPer10Meters { get; }

        /// <summary>Fraction of recoil the bot compensates (0 = none, 1 = perfect; capped below 1).</summary>
        public float RecoilControl { get; }

        /// <summary>0..1 likelihood weights consumed by utility scoring.</summary>
        public float CoverUsage { get; }
        public float FlankingTendency { get; }

        /// <summary>Seconds between full decision re-evaluations (lower = more responsive).</summary>
        public float DecisionIntervalSeconds { get; }

        public float VisionRangeMeters { get; }
        public float FieldOfViewDegrees { get; }

        /// <summary>Multiplier on gameplay hearing radii (1 = same as the authored radius).</summary>
        public float HearingMultiplier { get; }

        /// <summary>Health below which the bot prefers disengaging to heal.</summary>
        public float HealThresholdHealth { get; }

        /// <summary>How many seconds before a shrink completes the bot starts moving to the next circle.</summary>
        public float ZoneLeadTimeSeconds { get; }

        /// <summary>Chance per decision to pick a sub-optimal option (38.11 "occasional mistakes").</summary>
        public float MistakeChance { get; }

        public LootSelectionStrategy LootSelection { get; }

        public BotDifficultyProfile(
            BotDifficulty difficulty,
            float reactionTimeMinSeconds,
            float reactionTimeMaxSeconds,
            float baseAimErrorDegrees,
            float aimErrorPer10Meters,
            float recoilControl,
            float coverUsage,
            float flankingTendency,
            float decisionIntervalSeconds,
            float visionRangeMeters,
            float fieldOfViewDegrees,
            float hearingMultiplier,
            float healThresholdHealth,
            float zoneLeadTimeSeconds,
            float mistakeChance,
            LootSelectionStrategy lootSelection)
        {
            if (reactionTimeMinSeconds < HumanReactionFloorSeconds)
                throw new ArgumentOutOfRangeException(nameof(reactionTimeMinSeconds), "Reaction time below the human floor.");
            if (reactionTimeMaxSeconds < reactionTimeMinSeconds)
                throw new ArgumentOutOfRangeException(nameof(reactionTimeMaxSeconds));
            if (baseAimErrorDegrees < MinimumAimErrorDegrees)
                throw new ArgumentOutOfRangeException(nameof(baseAimErrorDegrees), "Bots must not aim perfectly.");
            if (visionRangeMeters <= 0f || visionRangeMeters > MaxVisionRangeMeters)
                throw new ArgumentOutOfRangeException(nameof(visionRangeMeters));
            if (recoilControl < 0f || recoilControl >= 1f)
                throw new ArgumentOutOfRangeException(nameof(recoilControl));

            Difficulty = difficulty;
            ReactionTimeMinSeconds = reactionTimeMinSeconds;
            ReactionTimeMaxSeconds = reactionTimeMaxSeconds;
            BaseAimErrorDegrees = baseAimErrorDegrees;
            AimErrorPer10Meters = aimErrorPer10Meters;
            RecoilControl = recoilControl;
            CoverUsage = Clamp01(coverUsage);
            FlankingTendency = Clamp01(flankingTendency);
            DecisionIntervalSeconds = decisionIntervalSeconds;
            VisionRangeMeters = visionRangeMeters;
            FieldOfViewDegrees = fieldOfViewDegrees;
            HearingMultiplier = hearingMultiplier;
            HealThresholdHealth = healThresholdHealth;
            ZoneLeadTimeSeconds = zoneLeadTimeSeconds;
            MistakeChance = Clamp01(mistakeChance);
            LootSelection = lootSelection;
        }

        /// <summary>Aim cone (degrees) a bot of this difficulty has at the given distance.</summary>
        public float AimErrorAtDistance(float distanceMeters)
        {
            if (distanceMeters < 0f)
                distanceMeters = 0f;
            return BaseAimErrorDegrees + AimErrorPer10Meters * (distanceMeters / 10f);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>The four built-in profiles from the 38.9 table.</summary>
    public static class BotDifficultyCatalog
    {
        public static readonly BotDifficultyProfile Rookie = new BotDifficultyProfile(
            BotDifficulty.Rookie,
            reactionTimeMinSeconds: 0.65f, reactionTimeMaxSeconds: 1.00f,
            baseAimErrorDegrees: 6.0f, aimErrorPer10Meters: 1.2f, recoilControl: 0.20f,
            coverUsage: 0.15f, flankingTendency: 0.05f, decisionIntervalSeconds: 1.00f,
            visionRangeMeters: 70f, fieldOfViewDegrees: 100f, hearingMultiplier: 0.8f,
            healThresholdHealth: 30f, zoneLeadTimeSeconds: 5f, mistakeChance: 0.25f,
            lootSelection: LootSelectionStrategy.Randomized);

        public static readonly BotDifficultyProfile Warrior = new BotDifficultyProfile(
            BotDifficulty.Warrior,
            reactionTimeMinSeconds: 0.45f, reactionTimeMaxSeconds: 0.70f,
            baseAimErrorDegrees: 4.0f, aimErrorPer10Meters: 0.9f, recoilControl: 0.45f,
            coverUsage: 0.40f, flankingTendency: 0.20f, decisionIntervalSeconds: 0.75f,
            visionRangeMeters: 90f, fieldOfViewDegrees: 110f, hearingMultiplier: 0.9f,
            healThresholdHealth: 45f, zoneLeadTimeSeconds: 15f, mistakeChance: 0.15f,
            lootSelection: LootSelectionStrategy.Basic);

        public static readonly BotDifficultyProfile Elite = new BotDifficultyProfile(
            BotDifficulty.Elite,
            reactionTimeMinSeconds: 0.30f, reactionTimeMaxSeconds: 0.48f,
            baseAimErrorDegrees: 2.6f, aimErrorPer10Meters: 0.6f, recoilControl: 0.65f,
            coverUsage: 0.70f, flankingTendency: 0.45f, decisionIntervalSeconds: 0.50f,
            visionRangeMeters: 110f, fieldOfViewDegrees: 120f, hearingMultiplier: 1.0f,
            healThresholdHealth: 55f, zoneLeadTimeSeconds: 30f, mistakeChance: 0.08f,
            lootSelection: LootSelectionStrategy.Tactical);

        public static readonly BotDifficultyProfile Nightmare = new BotDifficultyProfile(
            BotDifficulty.Nightmare,
            reactionTimeMinSeconds: 0.22f, reactionTimeMaxSeconds: 0.35f,
            baseAimErrorDegrees: 1.8f, aimErrorPer10Meters: 0.45f, recoilControl: 0.80f,
            coverUsage: 0.90f, flankingTendency: 0.65f, decisionIntervalSeconds: 0.35f,
            visionRangeMeters: 120f, fieldOfViewDegrees: 120f, hearingMultiplier: 1.0f,
            healThresholdHealth: 60f, zoneLeadTimeSeconds: 45f, mistakeChance: 0.04f,
            lootSelection: LootSelectionStrategy.Optimized);

        public static BotDifficultyProfile Get(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Rookie: return Rookie;
                case BotDifficulty.Warrior: return Warrior;
                case BotDifficulty.Elite: return Elite;
                case BotDifficulty.Nightmare: return Nightmare;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }

        public static readonly BotDifficulty[] All =
        {
            BotDifficulty.Rookie, BotDifficulty.Warrior, BotDifficulty.Elite, BotDifficulty.Nightmare,
        };
    }
}
