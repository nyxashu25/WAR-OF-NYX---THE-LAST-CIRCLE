using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarOfNyx.App;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.Offline;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Developer screenshot run of the real game (not the editor), for checking how the island looks
    /// in a built player: grass, weather, shadows, HUD. Started with
    ///   WarOfNyx.exe -devCapture &lt;folder&gt; [-screen-width 1600 -screen-height 900 -screen-fullscreen 0]
    /// It starts an OFFLINE NYX ISLAND match (fixed seed, 11 bots), captures the player's view under
    /// several times of day / weathers and a few fixed viewpoints, then quits. Offline only: it never
    /// touches online play, accounts or results (offline results are never ranked anyway).
    /// </summary>
    public sealed class DevCapture : MonoBehaviour
    {
        private string _folder;
        private int _index;

        /// <summary>Starts a capture run when <c>-devCapture</c> is on the command line.</summary>
        public static bool TryStart(string[] args)
        {
            string folder = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-devCapture")
                    folder = args[i + 1];
            if (string.IsNullOrEmpty(folder))
                return false;
            var go = new GameObject("DevCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCapture>()._folder = folder;
            return true;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            Debug.Log($"[WarOfNyx][DevCapture] Writing screenshots to {Path.GetFullPath(_folder)}");
            OfflineMatchRequest.Reset();
            OfflineMatchRequest.Bots = 11;
            OfflineMatchRequest.Seed = 20261014;
            yield return SceneManager.LoadSceneAsync(AppServices.Config.nyxIslandScene, LoadSceneMode.Single);
            yield return new WaitForSeconds(5f);

            var weather = WeatherSystem.Current;
            if (weather != null)
            {
                foreach (var (time, kind) in new[]
                         {
                             (TimeOfDay.Day, WeatherKind.Clear), (TimeOfDay.Dusk, WeatherKind.Clear), (TimeOfDay.Night, WeatherKind.Clear),
                             (TimeOfDay.Dawn, WeatherKind.Fog), (TimeOfDay.Day, WeatherKind.Rain), (TimeOfDay.Dusk, WeatherKind.Storm),
                         })
                {
                    weather.Begin(new AtmospherePlan { Time = time, Start = kind, Later = kind, ChangeAt = float.PositiveInfinity });
                    yield return new WaitForSeconds(2.5f); // let rain fill the air
                    yield return Capture($"player_{time}_{kind}");
                }
                weather.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
            }

            // Fixed viewpoints (the follow camera is switched off).
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<ThirdPersonCamera>();
                if (follow != null)
                    follow.enabled = false;
                yield return View(cam, 160f, -230f, 230f, -300f, 1.7f, "view_meadow");
                yield return View(cam, -260f, -95f, -330f, -120f, 1.7f, "view_shadow_forest");
                yield return View(cam, 255f, 150f, 280f, 240f, 2f, "view_crimson_temple");
                yield return View(cam, -60f, 230f, 20f, 330f, 2.5f, "view_eclipse_valley");
            }
            Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
            Application.Quit(0);
        }

        private IEnumerator View(Camera cam, float x, float z, float tx, float tz, float eye, string name)
        {
            cam.transform.position = new Vector3(x, Ground(x, z) + eye, z);
            cam.transform.LookAt(new Vector3(tx, Ground(tx, tz) + 1.2f, tz));
            yield return new WaitForSeconds(0.5f);
            yield return Capture(name);
        }

        /// <summary>24-bit bottom-up BMP from RGB24 rows (Unity's ReadPixels rows are bottom-up too).</summary>
        private static void WriteBmp(string path, int width, int height, byte[] rgb)
        {
            int stride = (width * 3 + 3) & ~3;
            int size = 54 + stride * height;
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            w.Write((byte)'B'); w.Write((byte)'M'); w.Write(size); w.Write(0); w.Write(54);
            w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(stride * height); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
            var row = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 3;
                    row[x * 3] = rgb[i + 2];     // B
                    row[x * 3 + 1] = rgb[i + 1]; // G
                    row[x * 3 + 2] = rgb[i];     // R
                }
                w.Write(row);
            }
        }

        private static float Ground(float x, float z) =>
            Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out var hit, 600f, ~(1 << 2 | World.EnvironmentLayers.FoliageMask), QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 6f;

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_folder, $"{++_index:00}_{name}.bmp");
            // Read the back buffer and write an uncompressed BMP: the ScreenCapture / ImageConversion
            // modules aren't part of the game, and a developer tool shouldn't add them.
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            WriteBmp(path, texture.width, texture.height, texture.GetRawTextureData());
            Destroy(texture);
            Debug.Log($"[WarOfNyx][DevCapture] {path}");
        }
    }
}
