using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Command-line / CI entry points (no dialogs). Examples:
    ///
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod WarOfNyx.EditorTools.BatchTools.Setup -quit
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod WarOfNyx.EditorTools.BatchTools.BuildWindowsPlayer -quit
    ///
    /// The Windows player works WITHOUT Fusion: menu, settings and the offline Practice Arena run;
    /// online buttons explain that the Fusion SDK is missing. With Fusion installed it is a full
    /// online client too.
    /// </summary>
    public static class BatchTools
    {
        public const string WindowsPlayerPath = "Builds/WindowsPlayer/WarOfNyx.exe";

        /// <summary>Same steps as War of Nyx > Setup > Run Project Setup, without any dialog. Exit code 1 on failure.</summary>
        public static void Setup()
        {
            try
            {
                var report = ProjectSetupWizard.RunSteps(out bool restartNeeded);
                Debug.Log("[WarOfNyx][Batch] Project setup finished:\n" + report +
                          (restartNeeded ? "\nThe Input System backend changed: the next editor start picks it up." : string.Empty));
                if (report.Contains("FAIL "))
                    EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Rebuilds NYX ISLAND (overwrite) and re-applies the visual style. Batch / CI friendly.</summary>
        [MenuItem("War of Nyx/Setup/Rebuild NYX ISLAND (overwrite)", priority = 22)]
        public static void RebuildIsland()
        {
            NyxIslandBuilder.Build(overwrite: true);
            VisualStyleBuilder.Apply();
        }

        /// <summary>Samples spawn points on NYX ISLAND and logs why candidates fail (map debugging).</summary>
        public static void DiagnoseIslandSpawns()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(NyxIslandBuilder.ScenePath);
            var area = UnityEngine.Object.FindAnyObjectByType<WarOfNyx.Gameplay.ArenaSpawnArea>();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            var random = new System.Random(1);
            for (int i = 0; i < 400; i++)
            {
                double a = random.NextDouble() * System.Math.PI * 2, r = area.ZoneRadius * System.Math.Sqrt(random.NextDouble());
                string reason = area.Check((float)(System.Math.Cos(a) * r), (float)(System.Math.Sin(a) * r), out _) ?? "VALID";
                counts[reason] = counts.TryGetValue(reason, out int n) ? n + 1 : 1;
            }
            Debug.Log("[WarOfNyx][Diagnose] NYX ISLAND spawn checks over 400 samples: " +
                      string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}")) +
                      $" | NavMesh triangles: {UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length / 3}");
        }

        public const string AndroidApkPath = "Builds/Android/WarOfNyx-dev.apk";
        public const string AndroidAabPath = "Builds/Android/WarOfNyx.aab";

        /// <summary>Development APK (ARM64, IL2CPP) for side-loading on test phones (Phase 11).</summary>
        [MenuItem("War of Nyx/Build/Android APK (development)", priority = 31)]
        public static void BuildAndroidApk()
        {
            if (!AndroidReady(out string why))
            {
                Fail(why);
                return;
            }
            EditorUserBuildSettings.buildAppBundle = false;
            BuildAndroid(AndroidApkPath, BuildOptions.Development);
        }

        /// <summary>
        /// Release App Bundle for the Play internal testing track (Phase 14). The upload key is read ONLY from
        /// environment variables (WON_KEYSTORE_PATH, WON_KEYSTORE_PASS, WON_KEY_ALIAS, WON_KEY_PASS); it is
        /// never stored in the project or committed (spec 25).
        /// </summary>
        [MenuItem("War of Nyx/Build/Android App Bundle (release, signed)", priority = 32)]
        public static void BuildAndroidAab()
        {
            if (!AndroidReady(out string why))
            {
                Fail(why);
                return;
            }
            string keystore = Environment.GetEnvironmentVariable("WON_KEYSTORE_PATH");
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            {
                Fail("Release signing needs WON_KEYSTORE_PATH (+ WON_KEYSTORE_PASS, WON_KEY_ALIAS, WON_KEY_PASS) set in the environment. See ANDROID_BUILD.md.");
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("WON_KEYSTORE_PASS") ?? string.Empty;
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("WON_KEY_ALIAS") ?? string.Empty;
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("WON_KEY_PASS") ?? string.Empty;
            EditorUserBuildSettings.buildAppBundle = true;
            try
            {
                BuildAndroid(AndroidAabPath, BuildOptions.None);
            }
            finally
            {
                // Don't leave secrets in ProjectSettings (they would be saved to disk and could be committed).
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
            }
        }

        private static bool AndroidReady(out string why)
        {
            why = null;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                why = "Android Build Support is not installed for this Unity version. Unity Hub > Installs > (version) > Add modules > " +
                      "Android Build Support (+ OpenJDK, Android SDK & NDK Tools).";
            else if (EditorBuildSettings.scenes.Length == 0)
                why = "No scenes in Build Settings. Run War of Nyx > Setup > Run Project Setup first.";
            return why == null;
        }

        private static void BuildAndroid(string path, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                options = options,
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log(ok
                ? $"[WarOfNyx][Batch] Android build succeeded: {Path.GetFullPath(path)} ({report.summary.totalSize / (1024 * 1024)} MB)"
                : $"[WarOfNyx][Batch] Android build {report.summary.result}: {report.summary.totalErrors} error(s).");
            if (!ok && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void Fail(string message)
        {
            Debug.LogError("[WarOfNyx][Batch] " + message);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                EditorUtility.DisplayDialog("War of Nyx build", message, "OK");
        }

        [MenuItem("War of Nyx/Build/Windows Player (offline capable)", priority = 30)]
        public static void BuildWindowsPlayerMenu()
        {
            string message = BuildWindows(out _);
            EditorUtility.DisplayDialog("War of Nyx build", message, "OK");
        }

        /// <summary>Batch build of the Windows player into Builds/WindowsPlayer. Exit code 1 on failure.</summary>
        public static void BuildWindowsPlayer()
        {
            BuildWindows(out bool ok);
            if (!ok)
                EditorApplication.Exit(1);
        }

        private static string BuildWindows(out bool ok)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                ok = false;
                const string none = "No scenes in Build Settings. Run War of Nyx > Setup > Run Project Setup first.";
                Debug.LogError("[WarOfNyx][Batch] " + none);
                return none;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(WindowsPlayerPath) ?? "Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsPlayerPath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            ok = summary.result == BuildResult.Succeeded;
            string message = ok
                ? $"Build succeeded: {Path.GetFullPath(WindowsPlayerPath)} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s)"
                : $"Build {summary.result}: {summary.totalErrors} error(s). See the log.";
            Debug.Log("[WarOfNyx][Batch] " + message);
            return message;
        }
    }
}
