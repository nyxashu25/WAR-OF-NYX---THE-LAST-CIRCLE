using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WarOfNyx.App;
using WarOfNyx.Core.Flow;
using WarOfNyx.UI.MainMenu;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Phase 1 acceptance smoke test: the game boots from the Bootstrap scene into the
    /// main menu without errors. Requires the scenes created by War of Nyx > Setup.
    /// </summary>
    public class BootstrapSmokeTests
    {
        private const float TimeoutSeconds = 10f;

        [UnityTest]
        public IEnumerator BootstrapLoadsMainMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded("Bootstrap"))
                Assert.Ignore("Bootstrap scene is not in Build Settings. Run War of Nyx > Setup > Run Phase 1 Project Setup.");

            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "Bootstrap did not reach the main menu");

            // Let the menu run its Start() and build its UI.
            yield return null;
            yield return null;

            Assert.IsTrue(AppServices.IsInitialized);
            Assert.AreEqual(GameFlowState.MainMenu, AppServices.Flow.Current);
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuController>(), "MainMenuController missing");
            Assert.IsNotNull(GameObject.Find("MainMenuCanvas"), "menu UI was not built");
        }
    }
}
