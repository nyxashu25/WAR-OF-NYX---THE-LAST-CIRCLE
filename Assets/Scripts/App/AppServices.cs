using UnityEngine;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Flow;
using WarOfNyx.Platform;
using WarOfNyx.Settings;

namespace WarOfNyx.App
{
    /// <summary>
    /// Process-wide services created once by <see cref="GameBootstrap"/> and kept for the
    /// lifetime of the app. Deliberately small: gameplay systems in later phases receive
    /// their dependencies from the match context, not from here.
    ///
    /// Runs on every client; nothing here is authoritative over gameplay.
    /// </summary>
    public static class AppServices
    {
        private const string RootName = "[WarOfNyx Services]";

        public static bool IsInitialized { get; private set; }
        public static GameConfig Config { get; private set; }
        public static GameEventBus Events { get; private set; }
        public static GameStateMachine Flow { get; private set; }
        public static UserSettingsService UserSettings { get; private set; }
        public static GraphicsQualityService Graphics { get; private set; }
        public static ConnectivityMonitor Connectivity { get; private set; }
        public static AppLifecycleHandler Lifecycle { get; private set; }

        /// <summary>Reset statics when domain reload is disabled (Enter Play Mode Options).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsInitialized = false;
            Config = null;
            Events = null;
            Flow = null;
            UserSettings = null;
            Graphics = null;
            Connectivity = null;
            Lifecycle = null;
        }

        /// <summary>Initializes with the default config if nothing has yet (e.g. a scene opened directly in the Editor).</summary>
        public static void EnsureInitialized()
        {
            if (!IsInitialized)
                Initialize(GameConfig.LoadOrCreateDefault());
        }

        public static void Initialize(GameConfig config)
        {
            if (IsInitialized)
                return;

            Config = config != null ? config : GameConfig.LoadOrCreateDefault();

            Events = new GameEventBus();
            Events.HandlerFailed += Debug.LogException;

            Flow = new GameStateMachine();
            Flow.StateChanged += e =>
            {
                Debug.Log($"[WarOfNyx] Flow {e.Previous} -> {e.Current} ({e.Mode})");
                Events.Publish(e);
            };

            UserSettings = new UserSettingsService();
            UserSettings.Load();

            Graphics = new GraphicsQualityService();
            int savedTier = UserSettings.Data.graphicsTier;
            Graphics.Apply(savedTier >= 0 ? (GraphicsQualityTier)savedTier : DeviceCapabilityService.RecommendedGraphicsTier);

            AudioListener.volume = UserSettings.Data.masterVolume;

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);

            Lifecycle = root.AddComponent<AppLifecycleHandler>();
            Lifecycle.Initialize(Events);

            Connectivity = root.AddComponent<ConnectivityMonitor>();
            Connectivity.Initialize(Events, Config);

            IsInitialized = true;
            UI.Hud.PerformanceOverlay.EnsureCreated(); // hidden until F4 or Settings → SHOW FPS
            Debug.Log($"[WarOfNyx] Services ready. Device: {DeviceCapabilityService.Describe()} | " +
                      $"Graphics: {Graphics.CurrentTier} | Max offline bots: {DeviceCapabilityService.MaxOfflineBots}");
        }
    }
}
