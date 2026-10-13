using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.Characters;
using WarOfNyx.Gameplay;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Generates the Phase 2 content: graybox materials, the MovementConfig asset, the
    /// PLACEHOLDER player prefab, and the PracticeArena test scene. Geometry is primitive
    /// graybox on purpose (spec 16 stage 1); it exists to test movement, not to look good.
    ///
    /// Arena layout (spawn at z = -40 facing +Z):
    ///   x = -48  sprint lane with a marker every 10 m (speed checks)
    ///   x = -30  ramps 20° / 35° (climbable) and 50° (steeper than the 45° slope limit)
    ///   x = -15  stairs, 0.25 m rise per step
    ///   x =   0  jump boxes 0.5 / 1.0 / 1.4 m (reachable) and 2.0 m (not reachable)
    ///   x =  15  1.4 m crouch tunnel and a slide-under bar
    ///   x =  30  cover blocks
    /// </summary>
    public static class PracticeArenaBuilder
    {
        public const string ScenePath = ProjectSetupWizard.ScenesFolder + "/PracticeArena.unity";
        public const string PrefabFolder = "Assets/Prefabs/Characters";
        public const string PrefabPath = PrefabFolder + "/PlayerPlaceholder.prefab";
        public const string ConfigFolder = "Assets/ScriptableObjects/Gameplay";
        public const string MovementConfigPath = ConfigFolder + "/MovementConfig.asset";
        public const string MaterialsFolder = "Assets/Art/Materials/Graybox";

        private sealed class Palette
        {
            public Material Ground, Wall, Ramp, Platform, Hazard, Marker, Spawn, PlayerBody, PlayerVisor;
        }

        [MenuItem("War of Nyx/Setup/Rebuild Practice Arena (overwrite)", priority = 21)]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Practice Arena",
                    "This overwrites PracticeArena.unity and PlayerPlaceholder.prefab. Continue?", "Overwrite", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build(overwrite: true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Creates whatever is missing (or everything when <paramref name="overwrite"/>).</summary>
        public static void Build(bool overwrite)
        {
            ProjectSetupWizard.EnsureFolder(PrefabFolder);
            ProjectSetupWizard.EnsureFolder(ConfigFolder);
            ProjectSetupWizard.EnsureFolder(MaterialsFolder);
            ProjectSetupWizard.EnsureFolder(ProjectSetupWizard.ScenesFolder);

            var config = CreateOrLoadMovementConfig();
            var palette = CreateMaterials();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (overwrite || prefab == null)
                prefab = CreatePlayerPrefab(config, palette);

            if (overwrite || !File.Exists(ScenePath))
                CreateScene(prefab.GetComponent<PlayerController>(), palette);

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Assets

        private static MovementConfig CreateOrLoadMovementConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>(MovementConfigPath);
            if (config != null)
                return config;
            config = ScriptableObject.CreateInstance<MovementConfig>();
            AssetDatabase.CreateAsset(config, MovementConfigPath);
            return config;
        }

        private static Palette CreateMaterials() => new Palette
        {
            Ground = Mat("Graybox_Ground", new Color(0.17f, 0.18f, 0.24f)),
            Wall = Mat("Graybox_Wall", new Color(0.32f, 0.33f, 0.42f)),
            Ramp = Mat("Graybox_Ramp", new Color(0.42f, 0.25f, 0.62f)),
            Platform = Mat("Graybox_Platform", new Color(0.10f, 0.52f, 0.62f)),
            Hazard = Mat("Graybox_Tunnel", new Color(0.75f, 0.19f, 0.31f)),
            Marker = Mat("Graybox_Marker", new Color(0.90f, 0.90f, 1.00f)),
            Spawn = Mat("Graybox_Spawn", new Color(1.00f, 0.78f, 0.34f)),
            PlayerBody = Mat("Placeholder_PlayerBody", new Color(0.54f, 0.17f, 0.89f)),
            PlayerVisor = Mat("Placeholder_PlayerVisor", new Color(0.00f, 0.90f, 1.00f)),
        };

        private static Material Mat(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePlayerPrefab(MovementConfig config, Palette palette)
        {
            var root = new GameObject("PlayerPlaceholder");
            var controller = root.AddComponent<CharacterController>();
            controller.height = config.settings.standingHeight;
            controller.radius = config.settings.capsuleRadius;
            controller.center = new Vector3(0f, config.settings.standingHeight * 0.5f, 0f);

            var movement = root.AddComponent<PlayerMovement>();
            SetReference(movement, "config", config);
            root.AddComponent<PlayerInputRouter>();
            root.AddComponent<PlayerController>();
            var animation = root.AddComponent<PlayerAnimationController>();

            // PLACEHOLDER visual: pivot at the feet so crouch squash and slide lean look right.
            var visual = new GameObject("Visual (PLACEHOLDER)").transform;
            visual.SetParent(root.transform, false);

            var body = Primitive(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), palette.PlayerBody);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var visor = Primitive(PrimitiveType.Cube, "Visor", visual, new Vector3(0f, 1.5f, 0.27f), new Vector3(0.46f, 0.14f, 0.12f), palette.PlayerVisor);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            var pack = Primitive(PrimitiveType.Cube, "Pack", visual, new Vector3(0f, 1.1f, -0.3f), new Vector3(0.42f, 0.5f, 0.16f), palette.Wall);
            Object.DestroyImmediate(pack.GetComponent<Collider>());

            SetReference(animation, "placeholderVisual", visual);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ Scene

        private static void CreateScene(PlayerController playerPrefab, Palette p)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.46f);
            RenderSettings.skybox = null;

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.08f, 0.16f);
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ThirdPersonCamera>();
            camGo.transform.position = new Vector3(0f, 3f, -45f);

            var geometry = new GameObject("Graybox").transform;

            // Floor and perimeter
            Block(geometry, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), p.Ground);
            Block(geometry, "Wall_N", new Vector3(0f, 2f, 60.5f), new Vector3(122f, 4f, 1f), p.Wall);
            Block(geometry, "Wall_S", new Vector3(0f, 2f, -60.5f), new Vector3(122f, 4f, 1f), p.Wall);
            Block(geometry, "Wall_E", new Vector3(60.5f, 2f, 0f), new Vector3(1f, 4f, 120f), p.Wall);
            Block(geometry, "Wall_W", new Vector3(-60.5f, 2f, 0f), new Vector3(1f, 4f, 120f), p.Wall);

            // Spawn pad
            Block(geometry, "SpawnPad", new Vector3(0f, 0.01f, -40f), new Vector3(4f, 0.02f, 4f), p.Spawn);
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 0.1f, -40f);

            // Sprint lane with markers every 10 m
            for (int i = 0; i <= 8; i++)
                Block(geometry, $"LaneMarker_{i * 10}m", new Vector3(-48f, 0.01f, -40f + i * 10f), new Vector3(6f, 0.02f, 0.3f), p.Marker);

            // Ramps
            Ramp(geometry, "Ramp_20deg", -34f, 20f, p.Ramp, p.Platform, withPlatform: true);
            Ramp(geometry, "Ramp_35deg", -29f, 35f, p.Ramp, p.Platform, withPlatform: true);
            Ramp(geometry, "Ramp_50deg_TooSteep", -24f, 50f, p.Hazard, p.Platform, withPlatform: false);

            // Stairs: 10 steps x 0.25 m
            for (int i = 0; i < 10; i++)
            {
                float h = 0.25f * (i + 1);
                Block(geometry, $"Stair_{i + 1}", new Vector3(-15f, h * 0.5f, -10f + i * 0.45f), new Vector3(4f, h, 0.45f), p.Wall);
            }
            // Last step ends at z = -10 + 9 * 0.45 + 0.225 = -5.725; the 4 m landing starts there.
            Block(geometry, "Stair_Top", new Vector3(-15f, 1.25f, -5.725f + 2f), new Vector3(4f, 2.5f, 4f), p.Platform);

            // Jump boxes
            float[] heights = { 0.5f, 1.0f, 1.4f, 2.0f };
            for (int i = 0; i < heights.Length; i++)
            {
                var mat = heights[i] > 1.5f ? p.Hazard : p.Platform;
                Block(geometry, $"JumpBox_{heights[i]:0.0}m", new Vector3(0f, heights[i] * 0.5f, -20f + i * 6f), new Vector3(2.5f, heights[i], 2.5f), mat);
            }

            // Crouch tunnel: inner clearance 1.4 m (standing 1.8 m does not fit, crouched 1.15 m does)
            Block(geometry, "Tunnel_WallL", new Vector3(13.6f, 0.7f, -15f), new Vector3(0.4f, 1.4f, 10f), p.Wall);
            Block(geometry, "Tunnel_WallR", new Vector3(16.4f, 0.7f, -15f), new Vector3(0.4f, 1.4f, 10f), p.Wall);
            Block(geometry, "Tunnel_Roof", new Vector3(15f, 1.55f, -15f), new Vector3(3.2f, 0.3f, 10f), p.Hazard);

            // Slide-under bar: bottom edge 1.3 m
            Block(geometry, "SlideBar_PostL", new Vector3(12.8f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 0.4f), p.Wall);
            Block(geometry, "SlideBar_PostR", new Vector3(17.2f, 1.25f, 0f), new Vector3(0.4f, 2.5f, 0.4f), p.Wall);
            Block(geometry, "SlideBar", new Vector3(15f, 1.9f, 0f), new Vector3(4.8f, 1.2f, 0.4f), p.Hazard);

            // Cover field
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 4; z++)
                {
                    bool tall = (x + z) % 2 == 0;
                    float h = tall ? 2.2f : 1.1f;
                    Block(geometry, $"Cover_{x}_{z}", new Vector3(26f + x * 6f, h * 0.5f, -20f + z * 8f), new Vector3(2f, h, tall ? 1f : 3f), p.Wall);
                }
            }

            foreach (Transform child in geometry)
                child.gameObject.isStatic = true;

            var arena = new GameObject("PracticeArena").AddComponent<PracticeArenaController>();
            var so = new SerializedObject(arena);
            so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void Ramp(Transform parent, string name, float x, float angle, Material rampMat, Material platformMat, bool withPlatform)
        {
            const float length = 8f;
            const float thickness = 0.4f;
            const float startZ = 10f;
            float rad = angle * Mathf.Deg2Rad;
            float rise = length * Mathf.Sin(rad);
            float run = length * Mathf.Cos(rad);

            // Slab centre sits halfway up the slope, sunk by half its thickness so the low edge meets the floor.
            var centre = new Vector3(x, rise * 0.5f - thickness * 0.5f * Mathf.Cos(rad), startZ + run * 0.5f + thickness * 0.5f * Mathf.Sin(rad));
            var ramp = Block(parent, name, centre, new Vector3(4f, thickness, length), rampMat);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);

            if (withPlatform)
                Block(parent, name + "_Top", new Vector3(x, rise * 0.5f, startZ + run + 2f), new Vector3(4f, rise, 4f), platformMat);
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, position, size, material);
            return go;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
