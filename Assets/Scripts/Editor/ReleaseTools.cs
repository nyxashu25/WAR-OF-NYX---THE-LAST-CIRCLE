using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Phase 14 release preparation:
    ///  * <see cref="GenerateBranding"/>: a PLACEHOLDER app icon (Nyx Circle eclipse emblem, 1024 px) and a
    ///    Play feature graphic (1024×500), set as the app icon and splash logo.
    ///  * <see cref="ValidateRelease"/>: a release-readiness report (PASS / WARN / BLOCKED) written to
    ///    Builds/release-check.txt. It reports; it never claims store approval.
    /// </summary>
    public static class ReleaseTools
    {
        public const string BrandingFolder = "Assets/Art/Branding";
        public const string IconPath = BrandingFolder + "/AppIcon.png";
        public const string FeatureGraphicPath = BrandingFolder + "/FeatureGraphic.png";
        public const string ReportPath = "Builds/release-check.txt";

        private static readonly Color Night = new Color(0.03f, 0.02f, 0.08f);
        private static readonly Color Indigo = new Color(0.12f, 0.06f, 0.3f);
        private static readonly Color Violet = new Color(0.54f, 0.17f, 0.89f);
        private static readonly Color Cyan = new Color(0f, 0.9f, 1f);
        private static readonly Color Crimson = new Color(1f, 0.19f, 0.31f);

        [MenuItem("War of Nyx/Release/Generate App Icon and Splash", priority = 1)]
        public static void GenerateBranding()
        {
            ProjectSetupWizard.EnsureFolder(BrandingFolder);
            WritePng(IconPath, DrawEmblem(1024, 1024, 0.5f));
            WritePng(FeatureGraphicPath, DrawEmblem(1024, 500, 0.24f));
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FeatureGraphicPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite; // usable as icon and as splash logo
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            PlayerSettings.SplashScreen.backgroundColor = Night;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            if (sprite != null)
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, sprite) };
            AssetDatabase.SaveAssets();
            Debug.Log("[WarOfNyx][Release] App icon and splash logo generated (PLACEHOLDER art).");
        }

        /// <summary>Signed-distance style drawing: dark sky, glowing Nyx Circle ring, crimson eclipse, cyan blade slash.</summary>
        private static Texture2D DrawEmblem(int width, int height, float emblemCentreX)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            float size = Mathf.Min(width, height);
            var centre = new Vector2(width * emblemCentreX, height * 0.5f);
            float ringR = size * 0.37f, ringW = size * 0.05f;
            float sunR = size * 0.22f;
            var shadowOffset = new Vector2(size * 0.09f, size * 0.06f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, centre);
                // Background: indigo glow fading to night, wider for the banner.
                float bg = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(width * 0.5f, height * 0.5f)) / (Mathf.Max(width, height) * 0.7f));
                var c = Color.Lerp(Night, Indigo, bg * bg);

                // Nyx Circle ring with a soft glow, violet → cyan around the circumference.
                float angle = Mathf.Atan2(p.y - centre.y, p.x - centre.x);
                var ringColor = Color.Lerp(Violet, Cyan, 0.5f + 0.5f * Mathf.Sin(angle + 0.6f)); // integer frequency: no seam at ±π
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - ringR) / ringW);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(d - ringR) / (ringW * 4f)) * 0.35f;
                c = Color.Lerp(c, ringColor, Mathf.Clamp01(ring * ring * 1.2f + glow));

                // Crimson sun eclipsed by a dark disc → crescent.
                float sun = Mathf.Clamp01((sunR - d) / 2f);
                float shadow = Mathf.Clamp01((sunR * 0.92f - Vector2.Distance(p, centre + shadowOffset)) / 2f);
                c = Color.Lerp(c, Color.Lerp(Crimson, Violet, d / sunR * 0.5f), sun);
                c = Color.Lerp(c, Night, shadow);

                // Blade slash: a thin bright diagonal through the emblem.
                var a = centre + new Vector2(-size * 0.42f, -size * 0.3f);
                var b = centre + new Vector2(size * 0.42f, size * 0.3f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                float lineD = Vector2.Distance(p, a + (b - a) * t);
                float taper = Mathf.Sin(t * Mathf.PI);
                float slash = Mathf.Clamp01((size * 0.008f * taper + 1f - lineD) / 1.5f);
                float slashGlow = Mathf.Clamp01(1f - lineD / (size * 0.03f)) * 0.4f * taper;
                c = Color.Lerp(c, Color.white, slash);
                c = Color.Lerp(c, Cyan, slashGlow);

                c.a = 1f;
                pixels[y * width + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        private static void WritePng(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------ release readiness

        [MenuItem("War of Nyx/Release/Validate Release Readiness", priority = 2)]
        public static void ValidateReleaseMenu()
        {
            string report = ValidateRelease();
            EditorUtility.DisplayDialog("War of Nyx - release readiness", report.Length > 1800 ? report.Substring(0, 1800) + "\n…" : report, "OK");
        }

        public static string ValidateRelease()
        {
            var sb = new StringBuilder();
            int blocked = 0, warnings = 0;
            void Check(bool ok, string pass, string fail, bool blocking = true)
            {
                if (ok)
                    sb.AppendLine("PASS     " + pass);
                else if (blocking)
                {
                    blocked++;
                    sb.AppendLine("BLOCKED  " + fail);
                }
                else
                {
                    warnings++;
                    sb.AppendLine("WARN     " + fail);
                }
            }

            var android = NamedBuildTarget.Android;
            Check(PlayerSettings.GetApplicationIdentifier(android) == ProjectSetupWizard.AndroidPackageId,
                $"Package id {ProjectSetupWizard.AndroidPackageId}", $"Package id is {PlayerSettings.GetApplicationIdentifier(android)}");
            Check(System.Text.RegularExpressions.Regex.IsMatch(PlayerSettings.bundleVersion, @"^\d+\.\d+\.\d+$"),
                $"Version {PlayerSettings.bundleVersion} (semantic)", $"Version '{PlayerSettings.bundleVersion}' is not MAJOR.MINOR.PATCH");
            Check(PlayerSettings.Android.bundleVersionCode >= 1, $"Version code {PlayerSettings.Android.bundleVersionCode} (raise for every upload)", "Version code < 1");
            Check(PlayerSettings.GetScriptingBackend(android) == ScriptingImplementation.IL2CPP, "IL2CPP", "Scripting backend is not IL2CPP");
            Check(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "ARM64 only", $"Architectures: {PlayerSettings.Android.targetArchitectures}");
            Check((int)PlayerSettings.Android.minSdkVersion >= 26, $"Minimum API {(int)PlayerSettings.Android.minSdkVersion}", "Minimum API below 26");
            var target = PlayerSettings.Android.targetSdkVersion;
            Check(target != AndroidSdkVersions.AndroidApiLevelAuto && (int)target >= 36,
                $"Target API {(int)target}",
                target == AndroidSdkVersions.AndroidApiLevelAuto
                    ? "Target API is 'Auto' (= highest SDK installed). Install Android SDK Platform 36 and confirm the built AAB targets 36 (Google Play 2026 rule; re-check current policy)"
                    : $"Target API {(int)target} is below 36", blocking: false);
            Check(!PlayerSettings.allowedAutorotateToPortrait && !PlayerSettings.allowedAutorotateToPortraitUpsideDown, "Landscape only", "Portrait allowed");
            Check(PlayerSettings.Android.forceInternetPermission, "INTERNET permission (online play)", "INTERNET permission not forced");

            string manifest = "Assets/Plugins/Android/AndroidManifest.xml";
            string[] dangerous = { "ACCESS_FINE_LOCATION", "ACCESS_COARSE_LOCATION", "READ_CONTACTS", "RECORD_AUDIO", "CAMERA", "READ_EXTERNAL_STORAGE", "READ_MEDIA" };
            bool manifestClean = !File.Exists(manifest) || !dangerous.Any(perm => File.ReadAllText(manifest).Contains(perm));
            Check(manifestClean, "No sensitive permissions requested", $"{manifest} requests a sensitive permission");

            var keyFiles = Directory.GetFiles("Assets", "*.keystore", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets", "*.jks", SearchOption.AllDirectories)).ToArray();
            Check(keyFiles.Length == 0, "No keystore inside the project", "Keystore file inside Assets: " + string.Join(", ", keyFiles));
            Check(string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) && string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass),
                "No signing passwords stored in ProjectSettings", "Signing passwords are stored in ProjectSettings - clear them");

            Check(!EditorUserBuildSettings.development, "Development build off", "Development build is on (fine for testing, not for release)", blocking: false);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Check(scenes.Length > 0 && scenes[0].EndsWith("Bootstrap.unity"), $"{scenes.Length} scenes, Bootstrap first", "Bootstrap is not the first scene");
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            Check(icons != null && icons.Length > 0 && icons[0] != null, "App icon set (placeholder art)", "No app icon (War of Nyx > Release > Generate App Icon and Splash)");
            Check(File.Exists("PRIVACY.md"), "Privacy policy text (PRIVACY.md) - host it at a public URL for the store", "PRIVACY.md missing");
            Check(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), "Android Build Support installed",
                "Android Build Support module not installed (Unity Hub)");
            Check(FusionIntegration.FusionInstalled, "Photon Fusion installed", "Photon Fusion SDK not installed: online play cannot ship");

            sb.Insert(0, $"WAR OF NYX release readiness - {DateTime.Now:yyyy-MM-dd HH:mm} - {blocked} blocked, {warnings} warning(s)\n" +
                         "This is a technical checklist; it does not mean the store will approve the app.\n\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Builds");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[WarOfNyx][Release] " + sb);
            return sb.ToString();
        }
    }
}
