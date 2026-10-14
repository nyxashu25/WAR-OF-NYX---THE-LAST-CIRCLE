using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.Gameplay.World;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Renders showcase screenshots of NYX ISLAND and the character roster to Builds/Screenshots
    /// (store listing drafts, Phase 14, and a visual check of the Phase 10 look). Needs a GPU: run in
    /// batch mode WITHOUT -nographics:
    ///   Unity.exe -batchmode -projectPath . -executeMethod WarOfNyx.EditorTools.ShowcaseCapture.Capture -quit
    /// Nothing is saved into the scenes.
    /// </summary>
    public static class ShowcaseCapture
    {
        public const string OutputFolder = "Builds/Screenshots";

        [MenuItem("War of Nyx/Build/Capture Showcase Screenshots", priority = 40)]
        public static void Capture()
        {
            Directory.CreateDirectory(OutputFolder);
            if (!File.Exists(NyxIslandBuilder.ScenePath))
            {
                Debug.LogError("[WarOfNyx] NYX ISLAND is missing. Run War of Nyx > Setup > Run Project Setup first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(NyxIslandBuilder.ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[WarOfNyx] NYX ISLAND has no Main Camera.");
                return;
            }
            cam.farClipPlane = 1500f;
            Shader.SetGlobalFloat("_NyxOutlineDisable", 0f);

            // Each shot shows a different time of day / weather (the same system a match uses).
            _weather = new GameObject("Weather (capture)").AddComponent<WeatherSystem>();
            Warmup(cam);
            float y = NyxIslandBuilder.PlateauHeight;
            Atmos(TimeOfDay.Day, WeatherKind.Clear);
            Shot(cam, new Vector3(-420f, 260f, -520f), new Vector3(0f, 0f, 0f), "01_nyx_island_overview.png");
            Atmos(TimeOfDay.Night, WeatherKind.Clear);
            Shot(cam, new Vector3(-230f, y + 6f, 140f), new Vector3(-280f, y + 8f, 230f), "02_neon_district.png");
            Atmos(TimeOfDay.Dawn, WeatherKind.Clear);
            Shot(cam, new Vector3(280f, y + 8f, 150f), new Vector3(280f, y + 9f, 240f), "03_crimson_temple.png");
            Atmos(TimeOfDay.Dusk, WeatherKind.Storm);
            Shot(cam, new Vector3(60f, y + 22f, -360f), new Vector3(0f, y + 2f, -440f), "04_phantom_docks.png");
            Atmos(TimeOfDay.Dusk, WeatherKind.Clear);
            Shot(cam, new Vector3(55f, y + 14f, -70f), new Vector3(0f, y + 16f, 0f), "05_nyx_core.png");
            Atmos(TimeOfDay.Day, WeatherKind.Clear);
            Shot(cam, new Vector3(-250f, y + 5f, -90f), new Vector3(-330f, y + 3f, -120f), "06_shadow_forest.png");
            Atmos(TimeOfDay.Dawn, WeatherKind.Fog);
            Shot(cam, new Vector3(390f, y + 18f, -40f), new Vector3(330f, y + 4f, -110f), "07_iron_fortress.png");

            // Ground-level views: grass and groves, the coast, rain over the valley.
            Atmos(TimeOfDay.Day, WeatherKind.Clear);
            Ground(cam, 160f, -230f, 230f, -300f, 1.7f, "09_meadow.png");
            var coast = new Vector2(-0.74f, 0.67f);
            Atmos(TimeOfDay.Dusk, WeatherKind.Clear);
            Ground(cam, coast.x * 520f + 30f, coast.y * 520f, coast.x * 590f, coast.y * 590f, 2.5f, "10_coast.png");
            Atmos(TimeOfDay.Day, WeatherKind.Rain);
            Ground(cam, -60f, 230f, 20f, 330f, 3f, "11_eclipse_valley_rain.png");
            Atmos(TimeOfDay.Dusk, WeatherKind.Clear);

            // Character roster lineup on the core plaza.
            var lineup = new GameObject("Lineup (temporary)");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PracticeArenaBuilder.PrefabPath);
            string[] weapons = { WeaponCatalog.Katana, WeaponCatalog.Nx7AssaultRifle, WeaponCatalog.ShadowSmg, WeaponCatalog.CrimsonShotgun };
            var centre = new Vector3(0f, y, -40f);
            for (int i = 0; i < CharacterRoster.Count && prefab != null; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(lineup.transform, false);
                go.transform.position = centre + new Vector3((i - 1.5f) * 1.6f, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var visual = go.transform.Find("Visual (PLACEHOLDER)");
                CharacterStyler.Apply(visual, CharacterRoster.Get(i));
                var hand = new GameObject("Hand").transform;
                hand.SetParent(visual, false);
                hand.localPosition = new Vector3(0.38f, 1.0f, 0.25f);
                var stats = WeaponCatalog.Get(weapons[i]);
                WeaponModelFactory.Build(stats, hand, out _);
                if (stats.IsMelee)
                    hand.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
            Shot(cam, centre + new Vector3(0f, 1.4f, -5.2f), centre + new Vector3(0f, 1.05f, 0f), "08_character_roster.png");
            Object.DestroyImmediate(lineup);
            Object.DestroyImmediate(_weather.gameObject);

            VegetationLineup(cam, new Vector3(0f, y, -40f));

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[WarOfNyx] Showcase screenshots written to {Path.GetFullPath(OutputFolder)}");
        }

        /// <summary>Every vegetation prefab, full detail (front row) and forced low detail (back row): art review.</summary>
        private static void VegetationLineup(Camera cam, Vector3 centre)
        {
            var root = new GameObject("Vegetation lineup (temporary)");
            var prefabs = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentArtBuilder.Folder + "/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tree_") && path.EndsWith("_0.prefab") || path.Contains("/Bush_0") || path.Contains("/Rock_0"))
                    prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            for (int i = 0; i < prefabs.Count; i++)
            for (int lod = 0; lod < 2; lod++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                go.transform.SetParent(root.transform, false);
                go.transform.position = centre + new Vector3((i - (prefabs.Count - 1) * 0.5f) * 7f, 0f, 10f + lod * 14f);
                var group = go.GetComponent<LODGroup>();
                if (group != null)
                    group.ForceLOD(lod);
            }
            // A patch of grass and flower clumps as plain objects (checks the mesh + material without the terrain detail system).
            var clumps = new System.Collections.Generic.List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnvironmentArtBuilder.Folder + "/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Grass_") || path.Contains("/Flowers_"))
                    clumps.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            var random = new System.Random(3);
            for (int i = 0; i < 400 && clumps.Count > 0; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(clumps[random.Next(clumps.Count)]);
                go.transform.SetParent(root.transform, false);
                go.transform.position = centre + new Vector3((float)random.NextDouble() * 16f - 8f, 0f, (float)random.NextDouble() * 8f - 6f);
                go.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
            Shot(cam, centre + new Vector3(0f, 6f, -14f), centre + new Vector3(0f, 4f, 16f), "12_vegetation_lineup.png");
            Object.DestroyImmediate(root);
        }

        /// <summary>Debug: one tree seen from several distances (checks LOD and shading far away).</summary>
        public static void CaptureDistanceDebug()
        {
            Directory.CreateDirectory(OutputFolder);
            EditorSceneManager.OpenScene(NyxIslandBuilder.ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            cam.farClipPlane = 1500f;
            _weather = new GameObject("Weather (capture)").AddComponent<WeatherSystem>();
            Atmos(TimeOfDay.Day, WeatherKind.Clear);
            Warmup(cam);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentArtBuilder.Folder + "/Prefabs/Tree_Broadleaf_0.prefab");
            var target = new Vector3(0f, NyxIslandBuilder.PlateauHeight, -40f);
            foreach (float d in new[] { 30f, 120f, 250f, 450f })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = target;
                go.transform.localScale = Vector3.one * (d / 30f); // keep it the same size on screen
                var from = target + new Vector3(0f, d * 0.35f, -d);
                cam.fieldOfView = 60f;
                Shot(cam, from, target + Vector3.up * 5f * (d / 30f), $"debug_tree_{d:0}m.png");
                Object.DestroyImmediate(go);
            }
            Debug.Log("[WarOfNyx] distance debug shots written; LOD0 bias: " + QualitySettings.lodBias + ", quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        private static WeatherSystem _weather;

        /// <summary>The first render after opening a scene in batch mode draws vegetation black; render twice and discard.</summary>
        private static void Warmup(Camera cam)
        {
            var rt = new RenderTexture(640, 360, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        private static void Atmos(TimeOfDay time, WeatherKind weather) =>
            _weather.Begin(new AtmospherePlan { Time = time, Start = weather, Later = weather, ChangeAt = float.PositiveInfinity });

        /// <summary>Camera <paramref name="eye"/> metres above the terrain, looking at a point on the ground.</summary>
        private static void Ground(Camera cam, float x, float z, float tx, float tz, float eye, string file) =>
            Shot(cam, new Vector3(x, NyxIslandBuilder.Height(x, z) + eye, z), new Vector3(tx, NyxIslandBuilder.Height(tx, tz) + 1.2f, tz), file);

        private static void Shot(Camera cam, Vector3 position, Vector3 target, string file)
        {
            const int width = 1920, height = 1080;
            cam.transform.position = position;
            cam.transform.LookAt(target);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;
            cam.targetTexture = previous;
            File.WriteAllBytes(Path.Combine(OutputFolder, file), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
