namespace WarOfNyx.Core.Offline
{
    /// <summary>Offline bot difficulty (master spec 38.3 / 38.9).</summary>
    public enum BotDifficulty
    {
        Rookie = 0,
        Warrior = 1,
        Elite = 2,
        Nightmare = 3,
    }

    /// <summary>How a bot judges loot (38.9 "Loot selection" row).</summary>
    public enum LootSelectionStrategy
    {
        Randomized = 0,
        Basic = 1,
        Tactical = 2,
        Optimized = 3,
    }

    public static class BotDifficultyExtensions
    {
        public static string DisplayName(this BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Rookie: return "ROOKIE";
                case BotDifficulty.Warrior: return "WARRIOR";
                case BotDifficulty.Elite: return "ELITE";
                case BotDifficulty.Nightmare: return "NIGHTMARE";
                default: return difficulty.ToString().ToUpperInvariant();
            }
        }
    }
}
