using Fusion;
using UnityEditor;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Networking;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Builds Assets/Resources/WarOfNyx/NetworkPlayer.prefab from the Phase 2 placeholder player:
    /// same movement/input/animation components plus NetworkObject, NetworkTransform and
    /// NetworkPlayer. The local-only PlayerController loop is removed (Fusion's tick drives
    /// movement online). Compiles only when Fusion is installed (WON_FUSION).
    /// </summary>
    [InitializeOnLoad]
    public static class NetworkPlayerPrefabBuilder
    {
        static NetworkPlayerPrefabBuilder()
        {
            ProjectSetupWizard.RegisterStep("Network player prefab (Phase 3, Fusion)", () => Build(overwrite: false));
        }

        [MenuItem("War of Nyx/Multiplayer/Rebuild Network Player Prefab", priority = 10)]
        public static void RebuildFromMenu() => Build(overwrite: true);

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
    }
}
