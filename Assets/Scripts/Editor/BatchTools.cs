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
