using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Combat
{
    /// <summary>
    /// Inspector-editable weapon definition (spec 7 "Loadout Data": id, name, rarity, category,
    /// damage, rate, range, magazine, ammo type, reload, movement modifier, recoil, accuracy, model,
    /// animation, sound, icon). The numbers live in <see cref="stats"/>, shared with the server and
    /// tests. Art references are placeholders until Phase 10.
    /// </summary>
    [CreateAssetMenu(menuName = "War of Nyx/Weapon Definition", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponStats stats = new WeaponStats();

        [Header("Presentation (placeholders until Phase 10)")]
        public Color placeholderColor = new Color(0.25f, 0.27f, 0.36f);
        public GameObject model;
        public AudioClip fireSound;
        public Sprite icon;
        [Tooltip("Rarity tier for loot colour (Phase 6): 0 Common, 1 Uncommon, 2 Rare, 3 Epic, 4 Legendary.")]
        [Range(0, 4)] public int rarity;
    }

    /// <summary>
    /// Resolves weapon stats by network id. Stats come from the WeaponDefinition assets listed in
    /// Resources/WarOfNyx/WeaponDatabase (tuned in the editor); ids missing there fall back to the
    /// built-in <see cref="WeaponCatalog"/>. Server and clients load the same assets, so prediction
    /// and authority use identical numbers.
    /// </summary>
    public static class WeaponRegistry
    {
        public const string DatabaseResource = "WarOfNyx/WeaponDatabase";

        private static Dictionary<short, WeaponStats> _stats;
        private static Dictionary<short, WeaponDefinition> _definitions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _stats = null;
            _definitions = null;
        }

        public static WeaponStats Get(short networkId)
        {
            EnsureLoaded();
            return _stats.TryGetValue(networkId, out var s) ? s : WeaponCatalog.Get(networkId);
        }

        public static WeaponDefinition Definition(short networkId)
        {
            EnsureLoaded();
            return _definitions.TryGetValue(networkId, out var d) ? d : null;
        }

        public static string DisplayName(short networkId) => Get(networkId)?.displayName ?? string.Empty;

        /// <summary>A freshly acquired weapon with a full magazine (spare ammo lives in the inventory).</summary>
        public static WeaponState CreateState(short networkId)
        {
            var stats = Get(networkId);
            if (stats == null)
                return WeaponState.Empty;
            return new WeaponState
            {
                WeaponId = networkId,
                Magazine = stats.IsRanged ? stats.magazineSize : 0,
            };
        }

        private static void EnsureLoaded()
        {
            if (_stats != null)
                return;

            _stats = new Dictionary<short, WeaponStats>();
            _definitions = new Dictionary<short, WeaponDefinition>();
            var database = Resources.Load<WeaponDatabase>(DatabaseResource);
            if (database == null)
                return;

            foreach (var definition in database.weapons)
            {
                if (definition == null || definition.stats == null)
                    continue;
                short id = WeaponCatalog.NetworkIdOf(definition.stats.id);
                if (id == WeaponCatalog.NoWeapon)
                {
                    Debug.LogWarning($"[WarOfNyx] WeaponDefinition '{definition.name}' has unknown id '{definition.stats.id}'. " +
                                     "Add it to WeaponCatalog to give it a network id.");
                    continue;
                }
                _stats[id] = definition.stats;
                _definitions[id] = definition;
            }
        }
    }
}
