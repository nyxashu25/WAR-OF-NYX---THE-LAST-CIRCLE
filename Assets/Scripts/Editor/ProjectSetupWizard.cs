using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WarOfNyx.App;
using WarOfNyx.UI.MainMenu;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// One-click Phase 1 project configuration: Android player settings, URP Low/Medium/High
    /// quality presets, the GameConfig asset, the Bootstrap and MainMenu scenes, and the
    /// Input System backend. Safe to run repeatedly: existing scenes and assets are reused,
    /// not overwritten (use "Rebuild Phase 1 Scenes" to regenerate scenes on purpose).
    ///
    /// Menu: War of Nyx > Setup.
    /// </summary>
    public static class ProjectSetupWizard
    {
        private const string MenuRoot = "War of Nyx/Setup/";

        /// <summary>
        /// Extra setup steps contributed by optional editor modules (e.g. the Fusion-dependent
        /// network prefab builder, which only compiles when the Fusion SDK is installed).
        /// </summary>
        private static readonly List<(string name, Action action)> AdditionalSteps = new List<(string, Action)>();

        public static void RegisterStep(string name, Action action)
        {
            if (AdditionalSteps.Exists(s => s.name == name))
                return;
            AdditionalSteps.Add((name, action));
        }

        public const string CompanyName = "Nyx Games";
        public const string ProductName = "WAR OF NYX";
        public const string AndroidPackageId = "com.nyxgames.warofnyx";
        public const string InitialVersion = "0.1.0";
        public const int InitialBundleVersionCode = 1;

        public const string ScenesFolder = "Assets/Scenes";
        public const string RenderingFolder = "Assets/Settings/Rendering";
        public const string ConfigFolder = "Assets/Resources/WarOfNyx";
        public const string ConfigPath = ConfigFolder + "/GameConfig.asset";
        public const string BootstrapScenePath = ScenesFolder + "/Bootstrap.unity";
        public const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";

        private const string DefaultPostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        private static readonly Color MenuBackground = new Color32(0x0B, 0x0B, 0x18, 0xFF);

        // ------------------------------------------------------------------ Menu items

        [MenuItem(MenuRoot + "Run Project Setup", priority = 0)]
        public static void RunAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var report = new StringBuilder();
            GameConfig config = null;
            bool restartNeeded = false;

            Step(report, "Folders", EnsureProjectFolders);
            Step(report, "GameConfig asset", () => config = CreateOrLoadConfig());
            Step(report, "Android player settings", ConfigurePlayerSettings);
            Step(report, "URP + Low/Medium/High quality presets", ConfigureRendering);
            Step(report, "Bootstrap + MainMenu scenes, Build Settings", () => CreateScenes(config, overwrite: false));
            Step(report, "Practice Arena: player prefab, graybox scene (Phase 2)", () => PracticeArenaBuilder.Build(overwrite: false));
            Step(report, "Weapon definitions + database (Phase 5)", WeaponAssetBuilder.Build);
            foreach (var (name, action) in AdditionalSteps)
                Step(report, name, action);
            Step(report, "Register WarOfNyx.Networking with the Fusion weaver", () => FusionIntegration.RegisterAssemblyWithWeaver(quiet: true));
            Step(report, "Input System backend", () => restartNeeded = EnableInputSystemBackend());

            if (File.Exists(BootstrapScenePath))
                EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[WarOfNyx] Project setup finished:\n" + report);
            EditorUtility.DisplayDialog("War of Nyx - Project setup", report.ToString(), "OK");

            if (restartNeeded && EditorUtility.DisplayDialog("Restart required",
                    "The Input System backend was enabled. Unity must restart for it to take effect.",
                    "Restart now", "Later"))
            {
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
        }

        [MenuItem(MenuRoot + "Rebuild Phase 1 Scenes (overwrite)", priority = 20)]
        public static void RebuildScenes()
        {
            if (!EditorUtility.DisplayDialog("Rebuild scenes",
                    "This overwrites Assets/Scenes/Bootstrap.unity and MainMenu.unity. Continue?", "Overwrite", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureProjectFolders();
            CreateScenes(CreateOrLoadConfig(), overwrite: true);
        }

        [MenuItem(MenuRoot + "Validate Project", priority = 40)]
        public static void Validate()
        {
            var report = new StringBuilder();
            Check(report, "GameConfig asset exists", AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null);
            Check(report, "Bootstrap is build scene 0",
                EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == BootstrapScenePath);
            Check(report, "MainMenu in Build Settings", EditorBuildSettings.scenes.Any(s => s.path == MainMenuScenePath && s.enabled));
            Check(report, "PracticeArena in Build Settings", EditorBuildSettings.scenes.Any(s => s.path == PracticeArenaBuilder.ScenePath && s.enabled));
            Check(report, "Player placeholder prefab exists", AssetDatabase.LoadAssetAtPath<GameObject>(PracticeArenaBuilder.PrefabPath) != null);
            Check(report, "MatchLobby in Build Settings", EditorBuildSettings.scenes.Any(s => s.path == PracticeArenaBuilder.MatchLobbyScenePath && s.enabled));
            Check(report, "FX material (Resources/WarOfNyx/FX_Additive)", AssetDatabase.LoadAssetAtPath<Material>(PracticeArenaBuilder.FxMaterialPath) != null);
            Check(report, "BattleArena NavMesh baked", AssetDatabase.LoadMainAssetAtPath(PracticeArenaBuilder.BattleArenaNavMeshPath) != null);
            Check(report, "BattleArena in Build Settings", EditorBuildSettings.scenes.Any(s => s.path == PracticeArenaBuilder.BattleArenaScenePath && s.enabled));
            Check(report, "Weapon database (Resources/WarOfNyx/WeaponDatabase)", AssetDatabase.LoadAssetAtPath<WarOfNyx.Combat.WeaponDatabase>(WeaponAssetBuilder.DatabasePath) != null);
            Check(report, "MovementConfig asset exists", AssetDatabase.LoadAssetAtPath<WarOfNyx.Characters.MovementConfig>(PracticeArenaBuilder.MovementConfigPath) != null);
            Check(report, "URP is the default render pipeline", GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset);
            Check(report, $"Android package id = {AndroidPackageId}",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == AndroidPackageId);
            Check(report, "Android uses IL2CPP",
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP);
            Check(report, "Android targets ARM64", (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0);
            Check(report, "Input System backend enabled", ReadActiveInputHandler() is int handler && handler != 0);
            Check(report, "Asset serialization = Force Text", EditorSettings.serializationMode == SerializationMode.ForceText);

            FusionIntegration.AppendValidation(report);

            Debug.Log("[WarOfNyx] Validation:\n" + report);
            EditorUtility.DisplayDialog("War of Nyx - Validate", report.ToString(), "OK");
        }

        // ------------------------------------------------------------------ Steps

        public static void EnsureProjectFolders()
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(RenderingFolder);
            EnsureFolder(ConfigFolder);
        }

        public static GameConfig CreateOrLoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null)
                return config;

            EnsureFolder(ConfigFolder);
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0" || PlayerSettings.bundleVersion == "0.1")
                PlayerSettings.bundleVersion = InitialVersion;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidPackageId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, InitialBundleVersionCode);

            // Landscape only (spec 18).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Google Play requires 64-bit; IL2CPP is required for ARM64 (spec 25).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Android 8.0 minimum (spec 25). Target "Auto" = highest SDK installed; install API 36
            // via the Android SDK manager before release builds (see ANDROID_BUILD.md).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Online play and the connectivity probe need network access.
            PlayerSettings.Android.forceInternetPermission = true;

            // Photon Fusion requires text serialization; it also keeps scenes/prefabs diffable in git.
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        public static void ConfigureRendering()
        {
            EnsureFolder(RenderingFolder);

            var low = CreateOrUpdateUrpAsset("Low", a =>
            {
                a.renderScale = 0.8f;
                a.msaaSampleCount = 1;
                a.supportsHDR = false;
                a.shadowDistance = 25f;
                a.shadowCascadeCount = 1;
                a.maxAdditionalLightsCount = 0;
            });
            var medium = CreateOrUpdateUrpAsset("Medium", a =>
            {
                a.renderScale = 1.0f;
                a.msaaSampleCount = 1;
                a.supportsHDR = false;
                a.shadowDistance = 45f;
                a.shadowCascadeCount = 1;
                a.maxAdditionalLightsCount = 2;
            });
            var high = CreateOrUpdateUrpAsset("High", a =>
            {
                a.renderScale = 1.0f;
                a.msaaSampleCount = 2;
                a.supportsHDR = false;
                a.shadowDistance = 70f;
                a.shadowCascadeCount = 2;
                a.maxAdditionalLightsCount = 4;
            });

            GraphicsSettings.defaultRenderPipeline = medium;
            ConfigureQualityLevels(low, medium, high);
            AssetDatabase.SaveAssets();
        }

        public static void CreateScenes(GameConfig config, bool overwrite)
        {
            EnsureFolder(ScenesFolder);

            if (overwrite || !File.Exists(BootstrapScenePath))
                CreateBootstrapScene(config);
            if (overwrite || !File.Exists(MainMenuScenePath))
                CreateMainMenuScene();

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
            };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != BootstrapScenePath && s.path != MainMenuScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }

        /// <summary>Switches Active Input Handling to the Input System package. Returns true if a restart is needed.</summary>
        public static bool EnableInputSystemBackend()
        {
            var playerSettings = LoadPlayerSettingsObject();
            var so = new SerializedObject(playerSettings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null)
                throw new InvalidOperationException(
                    "Could not find 'activeInputHandler'. Set Project Settings > Player > Active Input Handling to 'Input System Package (New)' manually.");

            // 0 = legacy Input Manager, 1 = Input System package, 2 = both.
            if (prop.intValue != 0)
                return false;

            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return true;
        }

        // ------------------------------------------------------------------ Helpers

        private static UniversalRenderPipelineAsset CreateOrUpdateUrpAsset(string tier, Action<UniversalRenderPipelineAsset> configure)
        {
            string assetPath = $"{RenderingFolder}/URP_{tier}.asset";
            string rendererPath = $"{RenderingFolder}/URP_{tier}_Renderer.asset";

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    var postProcess = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
                    if (postProcess != null)
                        renderer.postProcessData = postProcess;
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }

                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Reduces the project's quality levels to exactly Low / Medium / High, each with its own URP asset.
        /// Edits ProjectSettings/QualitySettings.asset through SerializedObject because Unity has no
        /// public API for renaming or removing quality levels.
        /// </summary>
        private static void ConfigureQualityLevels(RenderPipelineAsset low, RenderPipelineAsset medium, RenderPipelineAsset high)
        {
            var qualityObject = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qualityObject == null)
                throw new InvalidOperationException("ProjectSettings/QualitySettings.asset not found.");

            var so = new SerializedObject(qualityObject);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
                throw new InvalidOperationException("Unexpected QualitySettings format; configure quality levels manually.");

            levels.arraySize = 3;
            ConfigureLevel(levels.GetArrayElementAtIndex(0), "Low", low, lodBias: 0.7f, textureMipLimit: 1);
            ConfigureLevel(levels.GetArrayElementAtIndex(1), "Medium", medium, lodBias: 1.0f, textureMipLimit: 0);
            ConfigureLevel(levels.GetArrayElementAtIndex(2), "High", high, lodBias: 1.5f, textureMipLimit: 0);

            var current = so.FindProperty("m_CurrentQuality");
            if (current != null)
                current.intValue = 1;

            // Every platform defaults to Medium; the game picks the real tier at startup.
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var value = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (value != null)
                        value.intValue = 1;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLevel(SerializedProperty level, string name, RenderPipelineAsset pipeline, float lodBias, int textureMipLimit)
        {
            SetString(level, "name", name);
            SetObject(level, "customRenderPipeline", pipeline);
            SetFloat(level, "lodBias", lodBias);
            SetInt(level, "vSyncCount", 0);
            SetInt(level, "antiAliasing", 0); // URP controls MSAA through the pipeline asset
            if (!SetInt(level, "globalTextureMipmapLimit", textureMipLimit))
                SetInt(level, "masterTextureLimit", textureMipLimit);
        }

        private static void CreateBootstrapScene(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Bootstrap Camera");

            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera("Main Camera");

            var go = new GameObject("MainMenu");
            go.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateCamera(string name)
        {
            var go = new GameObject(name) { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MenuBackground;
            go.AddComponent<AudioListener>();
        }

        private static UnityEngine.Object LoadPlayerSettingsObject()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .FirstOrDefault(o => o is PlayerSettings);
            if (obj == null)
                throw new InvalidOperationException("ProjectSettings/ProjectSettings.asset not found.");
            return obj;
        }

        private static int? ReadActiveInputHandler()
        {
            try
            {
                var prop = new SerializedObject(LoadPlayerSettingsObject()).FindProperty("activeInputHandler");
                return prop?.intValue;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Step(StringBuilder report, string name, Action action)
        {
            try
            {
                action();
                report.AppendLine("OK    " + name);
            }
            catch (Exception ex)
            {
                report.AppendLine($"FAIL  {name}: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        private static void Check(StringBuilder report, string name, bool ok) =>
            report.AppendLine((ok ? "OK    " : "FAIL  ") + name);

        private static bool SetString(SerializedProperty parent, string name, string value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return false;
            p.stringValue = value;
            return true;
        }

        private static bool SetInt(SerializedProperty parent, string name, int value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return false;
            p.intValue = value;
            return true;
        }

        private static bool SetFloat(SerializedProperty parent, string name, float value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return false;
            p.floatValue = value;
            return true;
        }

        private static bool SetObject(SerializedProperty parent, string name, UnityEngine.Object value)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null) return false;
            p.objectReferenceValue = value;
            return true;
        }
    }
}
