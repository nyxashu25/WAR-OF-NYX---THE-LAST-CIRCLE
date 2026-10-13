using System;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Core.Bots
{
    /// <summary>The 19 bot states of spec 38.4.</summary>
    public enum BotState : byte
    {
        Spawning = 0,
        Exploring = 1,
        Patrolling = 2,
        SearchingForEnemies = 3,
        InvestigatingSound = 4,
        SeekingCover = 5,
        Hiding = 6,
        StalkingTarget = 7,
        Ambushing = 8,
        EngagingEnemy = 9,
        Repositioning = 10,
        Retreating = 11,
        Healing = 12,
        Looting = 13,
        SeekingSafeZone = 14,
        EscapingCircle = 15,
        AvoidingCampingPenalty = 16,
        FinalCircleCombat = 17,
        Eliminated = 18,
    }

    /// <summary>
    /// Everything a bot's brain may use for one decision. Filled by the simulation from LEGITIMATE
    /// information only: its own state, what its senses produced (memory), and the circle data every
    /// player sees on the HUD. Never other players' true positions.
    /// </summary>
    public struct BotSituation
    {
        public bool Alive;
        public float SecondsSinceSpawn;

        public float Health;
        public float MaxHealth;
        public int Medkits;
        public bool Healing;
        /// <summary>Took damage in the last couple of seconds.</summary>
        public bool UnderFire;

        public bool HasRangedWithAmmo;
        public float RangedEngagementRange;
        public bool HasMelee;
        public float MeleeReach;

        public bool ZoneActive;
        public bool Outside;
        /// <summary>Metres to the current edge: negative inside.</summary>
        public float DistanceToEdge;
        /// <summary>Seconds until the current shrink ends (what the HUD shows).</summary>
        public float SecondsToShrinkEnd;
        /// <summary>Inside the announced NEXT circle.</summary>
        public bool InsideNextCircle;
        public bool FinalStage;

        public float InactiveSeconds;
        public bool Exposed;

        public bool EnemyVisible;
        public float EnemyDistance;
        public bool EnemyRemembered;
        public float RememberedConfidence;
        public bool RememberedFromSound;

        public bool LootNearby;
        public float LootValue;
        public bool CoverNearby;

        public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;
    }

    /// <summary>
    /// Hierarchical decision making (spec 38.4): a hard top layer (eliminated, spawning, escaping the
    /// circle) and utility scores for everything else, biased by personality and limited by the
    /// difficulty profile. Hysteresis keeps a state unless another is clearly better, and the
    /// profile's mistake chance sometimes picks the second-best option (38.11 "occasional mistakes").
    /// Decisions run on the profile's decision interval, scheduled by the simulation.
    /// </summary>
    public sealed class BotBrain
    {
        public const float SwitchMargin = 0.15f;
        private static readonly BotState[] Candidates =
        {
            BotState.Exploring, BotState.Patrolling, BotState.SearchingForEnemies, BotState.InvestigatingSound,
            BotState.SeekingCover, BotState.Hiding, BotState.StalkingTarget, BotState.Ambushing, BotState.EngagingEnemy,
            BotState.Repositioning, BotState.Retreating, BotState.Healing, BotState.Looting, BotState.SeekingSafeZone,
            BotState.EscapingCircle, BotState.AvoidingCampingPenalty, BotState.FinalCircleCombat,
        };

        private readonly Random _random;

        public BotBrain(BotPersonality personality, BotDifficultyProfile difficulty, int seed)
        {
            Personality = personality ?? throw new ArgumentNullException(nameof(personality));
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            _random = new Random(seed);
        }

        public BotPersonality Personality { get; }
        public BotDifficultyProfile Difficulty { get; }
        public BotState State { get; private set; } = BotState.Spawning;
        public float LastScore { get; private set; }

        public BotState Decide(in BotSituation s)
        {
            if (!s.Alive)
                return State = BotState.Eliminated;
            if (s.SecondsSinceSpawn < 1f)
                return State = BotState.Spawning;

            float current = State == BotState.Spawning ? float.NegativeInfinity : Score(State, s, Personality, Difficulty);
            BotState best = State, second = State;
            float bestScore = float.NegativeInfinity, secondScore = float.NegativeInfinity;
            foreach (var candidate in Candidates)
            {
                float score = Score(candidate, s, Personality, Difficulty);
                if (score > bestScore)
                {
                    second = best;
                    secondScore = bestScore;
                    best = candidate;
                    bestScore = score;
                }
                else if (score > secondScore)
                {
                    second = candidate;
                    secondScore = score;
                }
            }

            // Occasional mistake: take the runner-up if it is still a sensible option (never while outside the circle).
            if (!s.Outside && secondScore > 0.3f && _random.NextDouble() < Difficulty.MistakeChance)
            {
                best = second;
                bestScore = secondScore;
            }

            if (best != State && (bestScore > current + SwitchMargin || current <= 0f))
                State = best;
            LastScore = Math.Max(bestScore, current);
            return State;
        }

        /// <summary>Utility of a state in a situation (≤ 0 = not applicable). Public for tests and tuning.</summary>
        public static float Score(BotState state, in BotSituation s, BotPersonality p, BotDifficultyProfile d)
        {
            float hp = s.HealthFraction;
            float healThreshold = Math.Min(0.9f, d.HealThresholdHealth / Math.Max(1f, s.MaxHealth) * p.Caution);
            bool canFightAtRange = s.HasRangedWithAmmo && !s.Outside && s.EnemyDistance <= s.RangedEngagementRange * 1.15f;
            bool canFightInMelee = s.HasMelee && s.EnemyDistance <= Math.Max(3f, s.MeleeReach + 1.5f);

            switch (state)
            {
                case BotState.EscapingCircle:
                    return s.ZoneActive && s.Outside ? 2.6f + Math.Min(1f, s.DistanceToEdge / 30f) : 0f;

                case BotState.SeekingSafeZone:
                {
                    if (!s.ZoneActive || s.Outside || s.InsideNextCircle)
                        return 0f;
                    float lead = d.ZoneLeadTimeSeconds * p.ZonePlanning;
                    if (s.SecondsToShrinkEnd > lead + 30f)
                        return 0.2f;
                    return 1.3f + 0.8f * Clamp01(1f - s.SecondsToShrinkEnd / Math.Max(1f, lead + 30f));
                }

                case BotState.AvoidingCampingPenalty:
                    if (s.Exposed) return 2.7f;
                    return s.InactiveSeconds > 12f ? 0.9f + (s.InactiveSeconds - 12f) * 0.12f : 0f;

                case BotState.Healing:
                {
                    if (s.Medkits <= 0 || hp >= 0.95f) return 0f;
                    if (s.Healing) return 2.1f;
                    float danger = s.EnemyVisible && s.EnemyDistance < 25f ? 0.5f : 1f;
                    return hp < healThreshold ? (1.4f + (healThreshold - hp) * 2f) * danger * p.Caution : 0.3f * (1f - hp);
                }

                case BotState.Retreating:
                    if (!s.EnemyVisible && !s.UnderFire) return 0f;
                    if (hp < healThreshold) return 1.6f * p.Caution;
                    return !s.HasRangedWithAmmo && !canFightInMelee && s.EnemyVisible ? 1.2f * p.Caution : 0f;

                case BotState.EngagingEnemy:
                {
                    if (!s.EnemyVisible || (!canFightAtRange && !canFightInMelee)) return 0f;
                    float score = 1.2f + 0.5f * p.Aggression + (s.UnderFire ? 0.3f : 0f) + (s.FinalStage ? 0.4f : 0f);
                    if (hp < healThreshold) score *= 0.6f;
                    else if (s.UnderFire) score *= 0.6f + 0.4f * hp; // trading hits while hurt: consider moving
                    if (canFightInMelee && !canFightAtRange) score *= 0.8f + 0.2f * p.MeleePreference;
                    return score;
                }

                case BotState.StalkingTarget:
                    if (s.EnemyVisible && !canFightAtRange && !canFightInMelee) return 0.9f * p.Aggression;
                    // A fresh trail (seen moments ago) is followed; a cold one is searched for instead.
                    return !s.EnemyVisible && s.EnemyRemembered && !s.RememberedFromSound && s.RememberedConfidence >= 0.6f
                        ? 0.8f * p.Aggression * s.RememberedConfidence
                        : 0f;

                case BotState.Repositioning:
                {
                    // Strafe / flank: when taking hits at mid health, or when the enemy is too close for this weapon.
                    if (!s.EnemyVisible || !canFightAtRange) return 0f;
                    float score = 0.9f * d.FlankingTendency * p.Flanking;
                    if (s.UnderFire && hp < 0.7f) score += 1.2f;
                    if (s.EnemyDistance < s.RangedEngagementRange * 0.2f && !canFightInMelee) score += 0.6f;
                    return score;
                }

                case BotState.SeekingCover:
                    return s.UnderFire && s.CoverNearby ? (0.9f + d.CoverUsage) * p.Stealth * (1.2f - hp * 0.4f) : 0f;

                case BotState.Hiding:
                    if (s.EnemyVisible || !s.CoverNearby || s.InactiveSeconds > 10f) return 0f;
                    return (hp < 0.5f ? 0.9f : 0.45f) * p.Stealth * d.CoverUsage;

                case BotState.Ambushing:
                    if (s.EnemyVisible || !s.EnemyRemembered || !s.CoverNearby || s.InactiveSeconds > 10f) return 0f;
                    return 0.85f * p.Stealth * d.CoverUsage * s.RememberedConfidence;

                case BotState.InvestigatingSound:
                    return !s.EnemyVisible && s.EnemyRemembered && s.RememberedFromSound
                        ? (0.7f + 0.3f * p.Aggression) * s.RememberedConfidence * (hp < healThreshold ? 0.4f : 1f) * 1.6f
                        : 0f;

                case BotState.SearchingForEnemies:
                    return !s.EnemyVisible && s.EnemyRemembered && !s.RememberedFromSound && s.RememberedConfidence < 0.6f
                        ? 0.55f + 0.4f * p.Aggression * s.RememberedConfidence
                        : 0f;

                case BotState.Looting:
                    if (!s.LootNearby || s.EnemyVisible) return 0f;
                    return Math.Min(1.8f, 0.5f + s.LootValue / 40f) * p.Greed;

                case BotState.FinalCircleCombat:
                    return s.FinalStage && !s.Outside ? 1.0f : 0f;

                case BotState.Patrolling:
                    return 0.45f + (p.Type == BotPersonalityType.Strategist ? 0.1f : 0f);

                case BotState.Exploring:
                    return 0.5f + (p.Type == BotPersonalityType.Scavenger || p.Type == BotPersonalityType.Hunter ? 0.1f : 0f);

                default:
                    return 0f;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Bot aiming (spec 38.8 / 38.9): turns toward the target at a limited speed, waits a sampled
    /// human-plausible reaction time after first sighting, and carries an aim error that grows with
    /// distance and never reaches zero. It produces the SAME yaw/pitch input a human would send.
    /// </summary>
    public sealed class BotAim
    {
        private readonly BotDifficultyProfile _profile;
        private readonly Random _random;
        private float _errorYaw, _errorPitch, _nextErrorAt;
        private int _targetId = -1;
        private float _readyAt;

        public BotAim(BotDifficultyProfile profile, int seed)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = new Random(seed);
        }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        /// <summary>Degrees per second the bot can turn (faster on higher difficulty, still finite).</summary>
        public float TurnSpeed => 160f + (1f - _profile.BaseAimErrorDegrees / 6f) * 260f;

        public void Reset(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
            _targetId = -1;
        }

        /// <summary>Look along a direction (walking around): no error, no reaction time.</summary>
        public void LookTowards(float yaw, float pitch, float dt)
        {
            Yaw = Step(Yaw, yaw, TurnSpeed * 0.6f * dt);
            Pitch = Step(Pitch, pitch, TurnSpeed * 0.6f * dt);
            _targetId = -1;
        }

        /// <summary>Track a target at the given exact angles (from the bot's own observation).</summary>
        public void Track(int targetId, float targetYaw, float targetPitch, float distance, float now, float dt)
        {
            if (targetId != _targetId)
            {
                _targetId = targetId;
                _readyAt = now + Lerp(_profile.ReactionTimeMinSeconds, _profile.ReactionTimeMaxSeconds, (float)_random.NextDouble());
                _nextErrorAt = 0f;
            }

            if (now >= _nextErrorAt)
            {
                // A new imperfect aim point every so often (no machine-like tracking).
                float cone = _profile.AimErrorAtDistance(distance);
                _errorYaw = (float)(_random.NextDouble() * 2.0 - 1.0) * cone;
                _errorPitch = (float)(_random.NextDouble() * 2.0 - 1.0) * cone * 0.6f;
                _nextErrorAt = now + 0.35f + (float)_random.NextDouble() * 0.4f;
            }

            Yaw = Step(Yaw, targetYaw + _errorYaw, TurnSpeed * dt);
            Pitch = Step(Pitch, targetPitch + _errorPitch, TurnSpeed * dt);
        }

        public float ReadyAt => _readyAt;

        /// <summary>Reaction time elapsed and the aim is close enough to the target to be worth a shot.</summary>
        public bool CanShoot(float targetYaw, float targetPitch, float distance, float now)
        {
            if (_targetId < 0 || now < _readyAt)
                return false;
            float tolerance = _profile.AimErrorAtDistance(distance) * 1.6f + 1.5f;
            return Math.Abs(BotPerception.DeltaAngle(Yaw, targetYaw)) <= tolerance
                   && Math.Abs(targetPitch - Pitch) <= tolerance;
        }

        private static float Step(float from, float to, float maxDelta)
        {
            float delta = BotPerception.DeltaAngle(from, to);
            if (Math.Abs(delta) <= maxDelta)
                return to;
            return from + Math.Sign(delta) * maxDelta;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
