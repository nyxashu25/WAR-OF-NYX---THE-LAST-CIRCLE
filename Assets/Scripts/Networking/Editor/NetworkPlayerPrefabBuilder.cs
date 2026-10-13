using Fusion;
using UnityEditor;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Networking;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Builds the Fusion prefabs in Assets/Resources/WarOfNyx (compiles only with WON_FUSION):
    ///  * NetworkPlayer.prefab: the Phase 2 placeholder player plus NetworkObject, NetworkTransform and
    ///    NetworkPlayer. The local-only PlayerController loop is removed (Fusion's tick drives movement).
    ///  * MatchController.prefab: NetworkObject plus NetworkMatchController (lobby → match lifecycle).
    /// </summary>
    [InitializeOnLoad]
    public static class NetworkPlayerPrefabBuilder
    {
        public const string MatchControllerPrefabPath = "Assets/Resources/WarOfNyx/MatchController.prefab";

        static NetworkPlayerPrefabBuilder()
        {
            ProjectSetupWizard.RegisterStep("Network player prefab (Phase 3, Fusion)", () => Build(overwrite: false));
            ProjectSetupWizard.RegisterStep("Match controller prefab (Phase 4, Fusion)", () => BuildMatchController(overwrite: false));
        }

        [MenuItem("War of Nyx/Multiplayer/Rebuild Network Prefabs", priority = 10)]
        public static void RebuildFromMenu()
        {
            Build(overwrite: true);
            BuildMatchController(overwrite: true);
        }

        public static void Build(bool overwrite)
        {
            const string path = FusionIntegration.NetworkPlayerPrefabPath;
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PracticeArenaBuilder.PrefabPath);
            if (source == null)
            {
                PracticeArenaBuilder.Build(overwrite: false);
                source = AssetDatabase.LoadAssetAtPath<GameObject>(PracticeArenaBuilder.PrefabPath);
            }

            ProjectSetupWizard.EnsureFolder("Assets/Resources/WarOfNyx");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "NetworkPlayer";

            var localLoop = instance.GetComponent<PlayerController>();
            if (localLoop != null)
                Object.DestroyImmediate(localLoop);

            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkTransform>();
            instance.AddComponent<NetworkPlayer>();

            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            Debug.Log($"[WarOfNyx] Built {path}.");
        }

        public static void BuildMatchController(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(MatchControllerPrefabPath) != null)
                return;

            ProjectSetupWizard.EnsureFolder("Assets/Resources/WarOfNyx");
            var go = new GameObject("MatchController");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkMatchController>();
            PrefabUtility.SaveAsPrefabAsset(go, MatchControllerPrefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[WarOfNyx] Built {MatchControllerPrefabPath}.");
        }
    }
}
