using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarOfNyx.App
{
    /// <summary>
    /// Entry point. Lives alone in the Bootstrap scene (build index 0):
    /// initializes <see cref="AppServices"/> and then loads the main menu.
    ///
    /// Dedicated-server builds (Phase 3+) will branch here to the server entry point
    /// instead of the menu; that branch does not exist yet.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        private IEnumerator Start()
        {
            AppServices.Initialize(config);

            string menu = AppServices.Config.mainMenuScene;
            if (!Application.CanStreamedLevelBeLoaded(menu))
            {
                Debug.LogError($"[WarOfNyx] Scene '{menu}' is not in Build Settings. " +
                               "Run 'War of Nyx > Setup > Run Phase 1 Project Setup'.");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(menu, LoadSceneMode.Single);
        }
    }
}
