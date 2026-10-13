using System;

namespace WarOfNyx.Core.Bots
{
    /// <summary>Spec 38.7: six personalities, assigned at random per bot for the whole match.</summary>
    public enum BotPersonalityType : byte
    {
        Hunter = 0,
        Phantom = 1,
        Strategist = 2,
        Berserker = 3,
        Survivor = 4,
        Scavenger = 5,
    }

    /// <summary>
    /// Weights that bias the utility scores (1 = neutral). Personalities change WHAT a bot prefers,
    /// never what it can perceive or how well it aims (that is the difficulty profile, and both are
    /// bounded by the shared game rules).
    /// </summary>
    public sealed class BotPersonality
    {
        public BotPersonalityType Type { get; }
        /// <summary>Willingness to start and keep fights.</summary>
        public float Aggression { get; }
        /// <summary>Preference for cover, hiding and ambushes.</summary>
        public float Stealth { get; }
        /// <summary>How early it moves toward the next circle.</summary>
        public float ZonePlanning { get; }
        /// <summary>Preference for melee and closing distance.</summary>
        public float MeleePreference { get; }
        /// <summary>Health fraction under which it retreats (multiplies the difficulty's heal threshold).</summary>
        public float Caution { get; }
        /// <summary>Interest in loot capsules and equipment upgrades.</summary>
        public float Greed { get; }
        /// <summary>Chance to flank instead of approaching head-on (multiplies the difficulty's tendency).</summary>
        public float Flanking { get; }
        /// <summary>Tendency to fire long bursts (low = conserves ammunition).</summary>
        public float TriggerHappiness { get; }

        private BotPersonality(BotPersonalityType type, float aggression, float stealth, float zonePlanning, float melee,
            float caution, float greed, float flanking, float trigger)
        {
            Type = type;
            Aggression = aggression;
            Stealth = stealth;
            ZonePlanning = zonePlanning;
            MeleePreference = melee;
            Caution = caution;
            Greed = greed;
            Flanking = flanking;
            TriggerHappiness = trigger;
        }

        public static readonly BotPersonality Hunter = new BotPersonality(BotPersonalityType.Hunter, 1.5f, 0.7f, 1.0f, 0.8f, 0.8f, 1.0f, 1.2f, 1.2f);
        public static readonly BotPersonality Phantom = new BotPersonality(BotPersonalityType.Phantom, 0.7f, 1.7f, 1.1f, 0.6f, 1.1f, 0.9f, 1.3f, 0.7f);
        public static readonly BotPersonality Strategist = new BotPersonality(BotPersonalityType.Strategist, 0.9f, 1.1f, 1.7f, 0.6f, 1.1f, 1.0f, 1.0f, 0.6f);
        public static readonly BotPersonality Berserker = new BotPersonality(BotPersonalityType.Berserker, 1.7f, 0.5f, 0.9f, 1.8f, 0.6f, 0.8f, 1.5f, 1.4f);
        public static readonly BotPersonality Survivor = new BotPersonality(BotPersonalityType.Survivor, 0.6f, 1.3f, 1.3f, 0.7f, 1.6f, 1.1f, 0.8f, 0.8f);
        public static readonly BotPersonality Scavenger = new BotPersonality(BotPersonalityType.Scavenger, 1.0f, 1.0f, 1.0f, 0.8f, 1.0f, 1.8f, 1.0f, 1.0f);

        public static readonly BotPersonality[] All = { Hunter, Phantom, Strategist, Berserker, Survivor, Scavenger };

        public static BotPersonality Of(BotPersonalityType type) => All[(int)type];

        public static BotPersonality Random(Random random) => All[random.Next(All.Length)];
    }

    /// <summary>Bot display names (spec 38.10 examples). Clearly bot names; offline only, never shown as humans online.</summary>
    public static class BotNames
    {
        private static readonly string[] Prefixes =
        {
            "GHOST", "SHADOW", "REAPER", "PHANTOM", "SPECTER", "WRAITH", "RAVEN", "VIPER", "HOLLOW", "ECHO", "NOVA", "KAGE",
        };

        /// <summary>Distinct names for <paramref name="count"/> bots, e.g. GHOST_07, SHADOW_12.</summary>
        public static string[] Generate(int count, Random random)
        {
            var names = new string[Math.Max(0, count)];
            var used = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < names.Length; i++)
            {
                string name;
                do
                {
                    name = $"{Prefixes[random.Next(Prefixes.Length)]}_{random.Next(1, 100):00}";
                } while (!used.Add(name));
                names[i] = name;
            }
            return names;
        }
    }
}
