using UnityEngine;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.App
{
    /// <summary>
    /// Project-wide static configuration. One asset lives at
    /// Assets/Resources/WarOfNyx/GameConfig.asset (created by War of Nyx > Setup).
    /// Gameplay tunables (weapons, circle phases, camping) get their own ScriptableObjects
    /// in later phases; this asset only holds app-level settings.
    /// </summary>
    [CreateAssetMenu(menuName = "War of Nyx/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string ResourcesPath = "WarOfNyx/GameConfig";

        [Header("Identity")]
        public string gameTitle = "WAR OF NYX";
        public string subtitle = "LAST CIRCLE";
        public string tagline = "MOVE. HUNT. SURVIVE.";
        public string secondaryTagline = "THE CIRCLE CLOSES. THE WAR BEGINS.";

        [Header("Scenes (must be in Build Settings)")]
        public string bootstrapScene = "Bootstrap";
        public string mainMenuScene = "MainMenu";
        public string practiceArenaScene = "PracticeArena";
        public string matchLobbyScene = "MatchLobby";
        public string battleArenaScene = "BattleArena";
        public string matchResultsScene = "MatchResults";

        [Header("Offline Ghost Protocol defaults")]
        [Tooltip("Bots in a new offline match. Allowed: 5, 11, 15, 23. Clamped to device capacity at runtime.")]
        public int defaultOfflineBotCount = 23;
        public BotDifficulty defaultOfflineDifficulty = BotDifficulty.Nightmare;

        [Header("Connectivity (spec 38.2)")]
        [Min(1f)] public float connectivityCheckIntervalSeconds = 5f;
        [Tooltip("HTTP endpoint that returns 2xx when the internet is reachable. Empty = rely on OS reachability only.")]
        public string connectivityProbeUrl = "https://www.gstatic.com/generate_204";
        [Min(1)] public int connectivityProbeTimeoutSeconds = 4;

        [Header("Matchmaking (spec 38.2)")]
        [Tooltip("Seconds in the online queue before PLAY OFFLINE WITH BOTS is offered.")]
        [Min(5f)] public float matchmakingOfflineOfferSeconds = 30f;

        [Header("Online test sessions (Phase 3)")]
        [Tooltip("Session name a dev dedicated server is started with (-session). JOIN DEV SERVER connects to it.")]
        public string devServerSession = "WON-DEV";
        [Range(2, 24)] public int privateRoomMaxPlayers = 12;

        [Header("Match start & spawning (Phase 4, server-authoritative)")]
        [Tooltip("Lobby, loading, countdown and spawn rules. Dedicated servers can override min players / lobby fill on the command line.")]
        public MatchRules matchRules = new MatchRules();

        [Header("Starting loadouts (Phase 6, server-side)")]
        [Tooltip("Power budget every randomized starting loadout must fit (spec 7).")]
        public WarOfNyx.Core.Items.LoadoutBudget loadoutBudget = new WarOfNyx.Core.Items.LoadoutBudget();

        public OfflineMatchSettings DefaultOfflineSettings =>
            new OfflineMatchSettings(defaultOfflineBotCount, defaultOfflineDifficulty);

        /// <summary>Loads the project asset, or builds an in-memory default so the game still runs before setup.</summary>
        public static GameConfig LoadOrCreateDefault()
        {
            var config = Resources.Load<GameConfig>(ResourcesPath);
            if (config != null)
                return config;

            Debug.LogWarning($"[WarOfNyx] No GameConfig at Resources/{ResourcesPath}. Using built-in defaults. " +
                             "Run 'War of Nyx > Setup > Run Project Setup' to create it.");
            config = CreateInstance<GameConfig>();
            config.name = "GameConfig (runtime default)";
            return config;
        }

        private void OnValidate()
        {
            if (!OfflineMatchSettings.IsAllowedBotCount(defaultOfflineBotCount))
            {
                Debug.LogWarning($"[WarOfNyx] defaultOfflineBotCount {defaultOfflineBotCount} is not one of " +
                                 $"{string.Join(", ", OfflineMatchSettings.AllowedBotCounts)}; resetting to 23.");
                defaultOfflineBotCount = 23;
            }
        }
    }
}
