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
        public const string LootCratePrefabPath = "Assets/Resources/WarOfNyx/LootCrate.prefab";

        static NetworkPlayerPrefabBuilder()
        {
            ProjectSetupWizard.RegisterStep("Network player prefab (Phase 3, Fusion)", () => Build(overwrite: false));
            ProjectSetupWizard.RegisterStep("Match controller prefab (Phase 4, Fusion)", () => BuildMatchController(overwrite: false));
            ProjectSetupWizard.RegisterStep("Loot capsule prefab (Phase 6, Fusion)", () => BuildLootCrate(overwrite: false));
        }

        [MenuItem("War of Nyx/Multiplayer/Rebuild Network Prefabs", priority = 10)]
        public static void RebuildFromMenu()
        {
            Build(overwrite: true);
            BuildMatchController(overwrite: true);
            BuildLootCrate(overwrite: true);
        }

        public static void Build(bool overwrite)
        {
            const string path = FusionIntegration.NetworkPlayerPrefabPath;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Prefabs from before Phase 5 lack combat/hitboxes: rebuild them automatically.
            if (!overwrite && existing != null && existing.GetComponent<NetworkCombat>() != null)
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
            instance.AddComponent<NetworkCombat>();

            // Player capsules live on "Ignore Raycast" (layer 2): shots then hit the LAG-COMPENSATED
            // hitboxes below instead of present-time CharacterControllers.
            instance.layer = 2;

            // Lag-compensated hitboxes (spec 9: server-side hit validation, head/body multipliers).
            var root = instance.AddComponent<HitboxRoot>();
            root.Offset = new Vector3(0f, 0.9f, 0f);
            root.BroadRadius = 1.4f;

            var body = new GameObject(NetworkCombat.BodyHitboxName).AddComponent<Hitbox>();
            body.transform.SetParent(instance.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.Type = HitboxTypes.Capsule;
            body.CapsuleRadius = 0.35f;
            body.CapsuleExtents = 0.35f;

            var head = new GameObject(NetworkCombat.HeadHitboxName).AddComponent<Hitbox>();
            head.transform.SetParent(instance.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.Type = HitboxTypes.Sphere;
            head.SphereRadius = 0.2f;

            root.InitHitboxes();

            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            Debug.Log($"[WarOfNyx] Built {path}.");
        }

        public static void BuildLootCrate(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(LootCratePrefabPath) != null)
                return;

            ProjectSetupWizard.EnsureFolder("Assets/Resources/WarOfNyx");
            var go = new GameObject("LootCrate");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkLootCrate>();
            PrefabUtility.SaveAsPrefabAsset(go, LootCratePrefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[WarOfNyx] Built {LootCratePrefabPath}.");
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
