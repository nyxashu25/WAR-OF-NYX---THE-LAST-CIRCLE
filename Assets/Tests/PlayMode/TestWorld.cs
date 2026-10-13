using System.Collections;
using UnityEngine.SceneManagement;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Gives a test an empty world: creates a fresh scene, makes it active and unloads every other
    /// scene (e.g. an arena a previous test loaded), so raycasts only see what the test builds.
    /// </summary>
    public static class TestWorld
    {
        private static int _counter;

        public static IEnumerator Fresh()
        {
            var fresh = SceneManager.CreateScene("TestWorld_" + (++_counter));
            SceneManager.SetActiveScene(fresh);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != fresh && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
