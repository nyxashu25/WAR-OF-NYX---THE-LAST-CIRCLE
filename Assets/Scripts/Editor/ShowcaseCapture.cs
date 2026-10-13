using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Gameplay.Effects;

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

            float y = NyxIslandBuilder.PlateauHeight;
            Shot(cam, new Vector3(-420f, 260f, -520f), new Vector3(0f, 0f, 0f), "01_nyx_island_overview.png");
            Shot(cam, new Vector3(-230f, y + 6f, 140f), new Vector3(-280f, y + 8f, 230f), "02_neon_district.png");
            Shot(cam, new Vector3(280f, y + 8f, 150f), new Vector3(280f, y + 9f, 240f), "03_crimson_temple.png");
            Shot(cam, new Vector3(60f, y + 22f, -360f), new Vector3(0f, y + 2f, -440f), "04_phantom_docks.png");
            Shot(cam, new Vector3(55f, y + 14f, -70f), new Vector3(0f, y + 16f, 0f), "05_nyx_core.png");
            Shot(cam, new Vector3(-250f, y + 5f, -90f), new Vector3(-330f, y + 3f, -120f), "06_shadow_forest.png");
            Shot(cam, new Vector3(390f, y + 18f, -40f), new Vector3(330f, y + 4f, -110f), "07_iron_fortress.png");

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

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[WarOfNyx] Showcase screenshots written to {Path.GetFullPath(OutputFolder)}");
        }

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
