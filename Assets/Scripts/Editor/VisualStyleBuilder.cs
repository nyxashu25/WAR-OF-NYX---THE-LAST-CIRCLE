using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Phase 10 "anime visual identity" setup step (idempotent):
    ///  * the shared cel-shaded material (Resources/WarOfNyx/NyxToon.mat, used at runtime),
    ///  * graybox materials switched to the toon shader (colours kept, neon parts glow),
    ///  * a post-processing profile (bloom, neutral tonemapping, saturated grading, light vignette),
    ///  * a dusk-purple procedural sky,
    ///  * every gameplay scene: global volume (off on the Low tier), post-processing on the camera,
    ///    gradient ambient light, distance fog, warm key light.
    /// </summary>
    public static class VisualStyleBuilder
    {
        public const string ArtRenderingFolder = "Assets/Art/Rendering";
        public const string ToonMaterialPath = "Assets/Resources/WarOfNyx/NyxToon.mat";
        public const string ProfilePath = ArtRenderingFolder + "/NyxPostProcess.asset";
        public const string SkyPath = ArtRenderingFolder + "/NyxSky.mat";
        private const string VolumeName = "Post Processing (Nyx)";

        private static readonly string[] Scenes =
        {
            ProjectSetupWizard.ScenesFolder + "/MainMenu.unity",
            PracticeArenaBuilder.ScenePath,
            PracticeArenaBuilder.BattleArenaScenePath,
            PracticeArenaBuilder.GhostProtocolScenePath,
            NyxIslandBuilder.ScenePath,
        };

        public static void Apply()
        {
            ProjectSetupWizard.EnsureFolder(ArtRenderingFolder);
            ProjectSetupWizard.EnsureFolder("Assets/Resources/WarOfNyx");

            var toonShader = Shader.Find("WarOfNyx/NyxToon");
            if (toonShader == null)
                throw new System.InvalidOperationException("Shader WarOfNyx/NyxToon not found (Assets/Art/Shaders/NyxToon.shader failed to import?).");

            var toon = AssetDatabase.LoadAssetAtPath<Material>(ToonMaterialPath);
            if (toon == null)
            {
                toon = new Material(toonShader) { name = "NyxToon" };
                AssetDatabase.CreateAsset(toon, ToonMaterialPath);
            }
            toon.shader = toonShader;
            EditorUtility.SetDirty(toon);

            ConvertGrayboxMaterials(toonShader);
            var profile = CreateProfile();
            var sky = CreateSky();

            foreach (var path in Scenes)
            {
                if (File.Exists(path))
                    StyleScene(path, profile, sky);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConvertGrayboxMaterials(Shader toonShader)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PracticeArenaBuilder.MaterialsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;
                var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                if (material.shader != toonShader)
                    material.shader = toonShader;
                material.SetColor("_BaseColor", color);
                // Neon accents on interactive / navigational surfaces.
                bool neon = path.Contains("Platform") || path.Contains("Marker") || path.Contains("Spawn") || path.Contains("Visor") || path.Contains("Neon");
                material.SetColor("_EmissionColor", neon ? color * 0.6f : Color.black);
                material.SetColor("_ShadeColor", Color.Lerp(new Color(0.3f, 0.26f, 0.5f), color, 0.2f));
                EditorUtility.SetDirty(material);
            }
        }

        private static VolumeProfile CreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var grading = GetOrAdd<ColorAdjustments>(profile);
            grading.saturation.Override(15f);
            grading.contrast.Override(10f);
            grading.colorFilter.Override(new Color(1f, 0.97f, 1.03f));

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.color.Override(new Color(0.08f, 0.02f, 0.15f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing))
                return existing;
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile); // persist inside the profile asset
            return component;
        }

        private static Material CreateSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            var shader = Shader.Find("WarOfNyx/NyxSky") ?? Shader.Find("Skybox/Procedural");
            if (shader == null)
                return null;
            if (sky == null)
            {
                sky = new Material(shader) { name = "NyxSky" };
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            sky.shader = shader; // gradient dusk sky (the scattering skybox gave a yellow horizon band)
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>All components of a type in a scene (works the same on Unity 6.3 and newer).</summary>
        private static System.Collections.Generic.IEnumerable<T> InScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<T>(true))
                    yield return c;
        }

        private static void StyleScene(string path, VolumeProfile profile, Material sky)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool menu = path.EndsWith("MainMenu.unity");

            var volumeGo = GameObject.Find(VolumeName);
            if (volumeGo == null)
            {
                volumeGo = new GameObject(VolumeName);
                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0f;
                volume.sharedProfile = profile;
                volumeGo.AddComponent<QualityAwareVolume>();
            }

            foreach (var cam in InScene<Camera>(scene))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                if (!menu)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.farClipPlane = Mathf.Max(cam.farClipPlane, 900f);
                }
                EditorUtility.SetDirty(cam);
            }

            if (!menu)
            {
                if (sky != null)
                    RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.5f, 0.45f, 0.72f);
                RenderSettings.ambientEquatorColor = new Color(0.32f, 0.36f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.22f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.3f, 0.24f, 0.45f);
                RenderSettings.fogColor = new Color(0.36f, 0.27f, 0.52f);
                RenderSettings.fogStartDistance = 160f;
                RenderSettings.fogEndDistance = 1000f;

                foreach (var light in InScene<Light>(scene))
                {
                    if (light.type != LightType.Directional)
                        continue;
                    light.color = new Color(1f, 0.9f, 0.82f);
                    light.intensity = 1.25f;
                    EditorUtility.SetDirty(light);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
