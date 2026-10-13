using System;

namespace WarOfNyx.Core.Offline
{
    /// <summary>
    /// Configuration chosen on the offline setup screen (master spec 38.3).
    /// Immutable value; use the With* helpers to change it.
    /// </summary>
    public readonly struct OfflineMatchSettings : IEquatable<OfflineMatchSettings>
    {
        /// <summary>The bot counts offered by the setup screen: 1 human + N bots.</summary>
        public static readonly int[] AllowedBotCounts = { 5, 11, 15, 23 };

        /// <summary>Spec default (38.3). Projects can override it in GameConfig.</summary>
        public const int SpecDefaultBotCount = 11;

        /// <summary>Humans in an offline match. Always exactly one.</summary>
        public const int HumanPlayers = 1;

        public readonly int BotCount;
        public readonly BotDifficulty Difficulty;

        public OfflineMatchSettings(int botCount, BotDifficulty difficulty)
        {
            BotCount = botCount;
            Difficulty = difficulty;
        }

        public static OfflineMatchSettings SpecDefault => new OfflineMatchSettings(SpecDefaultBotCount, BotDifficulty.Warrior);

        /// <summary>Every bot is a normal competitor: "PLAYERS ALIVE: 24/24" for 23 bots.</summary>
        public int TotalCompetitors => BotCount + HumanPlayers;

        public bool IsValid => IsAllowedBotCount(BotCount) && Enum.IsDefined(typeof(BotDifficulty), Difficulty);

        public OfflineMatchSettings WithBotCount(int botCount) => new OfflineMatchSettings(botCount, Difficulty);

        public OfflineMatchSettings WithDifficulty(BotDifficulty difficulty) => new OfflineMatchSettings(BotCount, difficulty);

        /// <summary>
        /// Returns settings that this device can run: the requested bot count if allowed,
        /// otherwise the largest allowed count that does not exceed <paramref name="maxBotsSupported"/>.
        /// Difficulty is never changed — it costs decision quality, not device performance.
        /// </summary>
        public OfflineMatchSettings ClampToCapacity(int maxBotsSupported)
        {
            int requested = IsAllowedBotCount(BotCount) ? BotCount : SpecDefaultBotCount;
            int best = AllowedBotCounts[0];
            foreach (int count in AllowedBotCounts)
            {
                if (count <= maxBotsSupported && count <= requested)
                    best = count;
            }

            var difficulty = Enum.IsDefined(typeof(BotDifficulty), Difficulty) ? Difficulty : BotDifficulty.Warrior;
            return new OfflineMatchSettings(best, difficulty);
        }

        public static bool IsAllowedBotCount(int botCount) => Array.IndexOf(AllowedBotCounts, botCount) >= 0;

        public bool Equals(OfflineMatchSettings other) => BotCount == other.BotCount && Difficulty == other.Difficulty;

        public override bool Equals(object obj) => obj is OfflineMatchSettings other && Equals(other);

        public override int GetHashCode() => (BotCount * 397) ^ (int)Difficulty;

        public override string ToString() => $"{Difficulty.DisplayName()} - {BotCount} bots ({TotalCompetitors} players)";
    }
}
