using UnityEditor;
using UnityEngine;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Creates one WeaponDefinition asset per catalog weapon (Assets/ScriptableObjects/Weapons) and the
    /// WeaponDatabase in Resources that the server and clients load. Existing assets are kept, so
    /// designer tuning survives re-running setup; only missing weapons are added.
    /// </summary>
    public static class WeaponAssetBuilder
    {
        public const string WeaponsFolder = "Assets/ScriptableObjects/Weapons";
        public const string DatabasePath = "Assets/Resources/WarOfNyx/WeaponDatabase.asset";

        [MenuItem("War of Nyx/Setup/Create Missing Weapon Assets", priority = 22)]
        public static void BuildFromMenu() => Build();

        public static void Build()
        {
            ProjectSetupWizard.EnsureFolder(WeaponsFolder);
            ProjectSetupWizard.EnsureFolder("Assets/Resources/WarOfNyx");

            var database = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<WeaponDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            foreach (var stats in WeaponCatalog.All)
            {
                string path = $"{WeaponsFolder}/{stats.id}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<WeaponDefinition>();
                    definition.stats = stats.Clone();
                    definition.placeholderColor = stats.IsMelee ? new Color(0f, 0.9f, 1f) : new Color(0.25f, 0.27f, 0.36f);
                    AssetDatabase.CreateAsset(definition, path);
                }
                else if (definition.catalogVersion < WeaponCatalog.Version)
                {
                    // The built-in numbers changed (e.g. the accuracy pass): refresh. Designer edits made to
                    // an older asset are replaced; tune in WeaponCatalog to keep them across versions.
                    definition.stats = stats.Clone();
                    Debug.Log($"[WarOfNyx] Weapon '{stats.id}' refreshed to catalog version {WeaponCatalog.Version}.");
                }
                definition.catalogVersion = WeaponCatalog.Version;
                EditorUtility.SetDirty(definition);

                if (!database.weapons.Contains(definition))
                    database.weapons.Add(definition);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
    }
}
