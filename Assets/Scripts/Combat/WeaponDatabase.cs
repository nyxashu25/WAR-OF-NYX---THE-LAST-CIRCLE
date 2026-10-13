using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Combat
{
    /// <summary>List of all weapon definitions. One asset at Resources/WarOfNyx/WeaponDatabase (created by War of Nyx > Setup).</summary>
    [CreateAssetMenu(menuName = "War of Nyx/Weapon Database", fileName = "WeaponDatabase")]
    public sealed class WeaponDatabase : ScriptableObject
    {
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();
    }
}
