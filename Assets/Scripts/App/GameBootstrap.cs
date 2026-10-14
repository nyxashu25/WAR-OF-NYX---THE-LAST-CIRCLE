using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.App
{
    /// <summary>
    /// Entry point. Lives alone in the Bootstrap scene (build index 0).
    ///
    /// Client: initializes <see cref="AppServices"/> and loads the main menu.
    /// Dedicated server (<c>-server</c> argument): starts the authoritative online session
    /// instead (spec 3/12). The server never shows UI.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        private IEnumerator Start()
        {
            AppServices.Initialize(config);

            // Arguments, with WON_* environment variables as container-friendly defaults (Phase 12).
            var launch = ServerLaunchOptions.Parse(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable);
            if (launch.IsServer)
            {
                yield return RunDedicatedServer(launch);
                yield break;
            }

            // Developer screenshot run (offline only, see DevCapture).
            if (Gameplay.World.DevCapture.TryStart(Environment.GetCommandLineArgs()))
                yield break;

            string menu = AppServices.Config.mainMenuScene;
            if (!Application.CanStreamedLevelBeLoaded(menu))
            {
                Debug.LogError($"[WarOfNyx] Scene '{menu}' is not in Build Settings. " +
                               "Run 'War of Nyx > Setup > Run Project Setup'.");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(menu, LoadSceneMode.Single);
        }

        private static IEnumerator RunDedicatedServer(ServerLaunchOptions launch)
        {
            if (launch.Warnings.Length > 0)
                Debug.LogWarning($"[WarOfNyx][Server] Launch argument problems: {launch.Warnings}");

            // A headless server has nothing to render; don't burn CPU on frames.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            var session = OnlineServices.Session;
            if (session == null)
            {
                Debug.LogError("[WarOfNyx][Server] " + OnlineServices.UnavailableMessage);
                Application.Quit(1);
                yield break;
            }

            Debug.Log($"[WarOfNyx][Server] Starting dedicated server: {launch}");
            ServerRuntime.Start(launch); // health endpoint, telemetry, lifetime policy
            var start = session.StartDedicatedServerAsync(launch);
            while (!start.IsCompleted)
                yield return null;

            if (start.IsFaulted || !start.Result)
            {
                Debug.LogError($"[WarOfNyx][Server] Failed to start: {session.LastError}");
                Application.Quit(2);
            }
        }
    }
}
