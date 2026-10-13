using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Photon Fusion glue that must work even when Fusion is NOT installed (so it lives in the
    /// always-compiled editor assembly and uses no Fusion types):
    ///
    ///  * keeps the WON_FUSION scripting define in sync with whether the SDK is present, which
    ///    switches the WarOfNyx.Networking assembly on/off (no compile errors without Fusion);
    ///  * registers WarOfNyx.Networking in NetworkProjectConfig "AssembliesToWeave" (the asmdef also
    ///    defines FUSION_FORCE_WEAVING; this is the belt to that pair of braces);
    ///  * builds the dedicated server / test client and launches a local server.
    /// </summary>
    [InitializeOnLoad]
    public static class FusionIntegration
    {
        public const string Define = "WON_FUSION";
        public const string NetworkingAssembly = "WarOfNyx.Networking";
        public const string NetworkPlayerPrefabPath = "Assets/Resources/WarOfNyx/NetworkPlayer.prefab";

        private const string MenuRoot = "War of Nyx/Multiplayer/";
        private const string WindowsServerPath = "Builds/WindowsServer/WarOfNyxServer.exe";
        private const string LinuxServerPath = "Builds/LinuxServer/WarOfNyxServer.x86_64";
        private const string WindowsClientPath = "Builds/WindowsClient/WarOfNyx.exe";

        private static readonly NamedBuildTarget[] DefineTargets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.Server,
        };

        static FusionIntegration()
        {
            EditorApplication.delayCall += SyncDefine;
        }

        public static bool FusionInstalled =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Fusion.Runtime");

        // ------------------------------------------------------------------ Define sync

        [MenuItem(MenuRoot + "Re-check Fusion Installation", priority = 0)]
        public static void SyncDefine()
        {
            bool installed = FusionInstalled;
            foreach (var target in DefineTargets)
            {
                var symbols = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                bool has = symbols.Contains(Define);
                if (installed == has)
                    continue;

                if (installed)
                    symbols.Add(Define);
                else
                    symbols.Remove(Define);

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
                Debug.Log($"[WarOfNyx] Fusion {(installed ? "detected" : "not found")}: {(installed ? "added" : "removed")} {Define} for {target.TargetName}. Scripts will recompile.");
            }
        }

        // ------------------------------------------------------------------ Weaver registration

        [MenuItem(MenuRoot + "Register Networking Assembly With Fusion Weaver", priority = 1)]
        public static void RegisterFromMenu() => RegisterAssemblyWithWeaver(quiet: false);

        /// <summary>Adds WarOfNyx.Networking to AssembliesToWeave in NetworkProjectConfig.fusion (a JSON file).</summary>
        public static void RegisterAssemblyWithWeaver(bool quiet)
        {
            if (!FusionInstalled)
            {
                if (!quiet)
                    EditorUtility.DisplayDialog("Fusion not installed", "Import the Photon Fusion 2 SDK first (DEPENDENCIES.md).", "OK");
                return; // nothing to do yet; not an error before Phase 3 setup
            }

            string path = FindNetworkProjectConfig();
            if (path == null)
                throw new FileNotFoundException("NetworkProjectConfig.fusion not found. Open Fusion > Network Project Config once, then retry.");

            string json = File.ReadAllText(path);
            if (json.Contains($"\"{NetworkingAssembly}\""))
            {
                if (!quiet)
                    EditorUtility.DisplayDialog("Fusion weaver", $"{NetworkingAssembly} is already registered.", "OK");
                return;
            }

            int key = json.IndexOf("\"AssembliesToWeave\"", StringComparison.Ordinal);
            int open = key < 0 ? -1 : json.IndexOf('[', key);
            if (open < 0)
                throw new InvalidDataException("AssembliesToWeave not found in NetworkProjectConfig.fusion. Add WarOfNyx.Networking manually in the Fusion config inspector.");

            int next = open + 1;
            while (next < json.Length && char.IsWhiteSpace(json[next]))
                next++;
            bool empty = next < json.Length && json[next] == ']';
            string insertion = empty ? $"\"{NetworkingAssembly}\"" : $"\n    \"{NetworkingAssembly}\",";

            File.WriteAllText(path, json.Insert(open + 1, insertion));
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[WarOfNyx] Registered {NetworkingAssembly} in {path} (AssembliesToWeave).");
        }

        private static string FindNetworkProjectConfig() =>
            Directory.GetFiles("Assets", "NetworkProjectConfig.fusion", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .FirstOrDefault();

        public static void AppendValidation(StringBuilder report)
        {
            if (!FusionInstalled)
            {
                report.AppendLine("INFO  Photon Fusion 2 SDK not installed (needed for online play - DEPENDENCIES.md, Phase 3).");
                return;
            }

            report.AppendLine("OK    Photon Fusion 2 SDK detected");
            bool defined = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';').Contains(Define);
            report.AppendLine((defined ? "OK    " : "FAIL  ") + $"{Define} define set (enables WarOfNyx.Networking)");

            string config = FindNetworkProjectConfig();
            bool woven = config != null && File.ReadAllText(config).Contains($"\"{NetworkingAssembly}\"");
            report.AppendLine((woven ? "OK    " : "WARN  ") + $"{NetworkingAssembly} in AssembliesToWeave (asmdef also forces weaving)");

            bool prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefabPath) != null;
            report.AppendLine((prefab ? "OK    " : "FAIL  ") + "Network player prefab (Resources/WarOfNyx/NetworkPlayer)");
            bool controller = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/WarOfNyx/MatchController.prefab") != null;
            report.AppendLine((controller ? "OK    " : "FAIL  ") + "Match controller prefab (Resources/WarOfNyx/MatchController)");

            report.AppendLine("INFO  Fusion App ID cannot be checked from here: open Fusion > Fusion Hub and confirm it is set.");
        }

        // ------------------------------------------------------------------ Builds

        [MenuItem(MenuRoot + "Build Windows Dedicated Server", priority = 20)]
        public static void BuildWindowsServer() =>
            Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Server, WindowsServerPath);

        [MenuItem(MenuRoot + "Build Linux Dedicated Server", priority = 21)]
        public static void BuildLinuxServer() =>
            Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, LinuxServerPath);

        [MenuItem(MenuRoot + "Build Windows Test Client", priority = 22)]
        public static void BuildWindowsClient() =>
            Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, WindowsClientPath);

        [MenuItem(MenuRoot + "Launch Local Windows Server (WON-DEV)", priority = 40)]
        public static void LaunchLocalServer()
        {
            string exe = Path.GetFullPath(WindowsServerPath);
            if (!File.Exists(exe))
            {
                EditorUtility.DisplayDialog("Server not built", "Run 'Build Windows Dedicated Server' first.", "OK");
                return;
            }

            string log = Path.GetFullPath("Builds/WindowsServer/server.log");
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"-batchmode -nographics -server -session WON-DEV -maxPlayers 12 -minPlayers 2 -lobbyFill 10 -logFile \"{log}\"",
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = false,
            });
            Debug.Log($"[WarOfNyx] Local dedicated server started (session WON-DEV). Log: {log}");
        }

        private static void Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string path)
        {
            if (!FusionInstalled)
            {
                EditorUtility.DisplayDialog("Fusion not installed", "Online builds need the Photon Fusion 2 SDK (DEPENDENCIES.md).", "OK");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                subtarget = (int)subtarget,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            string message = summary.result == BuildResult.Succeeded
                ? $"Build succeeded: {path} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s)"
                : $"Build {summary.result}: {summary.totalErrors} error(s). See the Console.";
            Debug.Log("[WarOfNyx] " + message);
            EditorUtility.DisplayDialog("War of Nyx build", message, "OK");
        }
    }
}
