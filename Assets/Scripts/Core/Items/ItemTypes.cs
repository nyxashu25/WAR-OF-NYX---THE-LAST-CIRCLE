using System;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Items
{
    /// <summary>Spec 14 loot colours: Common white, Uncommon green, Rare blue, Epic purple, Legendary gold.</summary>
    public enum Rarity : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    public enum MedkitType : byte
    {
        Small = 0,
        Standard = 1,
        Advanced = 2,
    }

    /// <summary>Medkits (spec 15 table). Values [T].</summary>
    public static class Medkits
    {
        public static readonly MedkitType[] All = { MedkitType.Small, MedkitType.Standard, MedkitType.Advanced };
        public const int MaxPerType = 6;

        public static float HealAmount(MedkitType type) =>
            type == MedkitType.Small ? 25f : type == MedkitType.Standard ? 50f : 75f;

        public static float UseSeconds(MedkitType type) =>
            type == MedkitType.Small ? 2f : type == MedkitType.Standard ? 4f : 6f;

        /// <summary>Loadout power-budget value.</summary>
        public static float Power(MedkitType type) =>
            type == MedkitType.Small ? 4f : type == MedkitType.Standard ? 7f : 10f;

        public static Rarity RarityOf(MedkitType type) =>
            type == MedkitType.Small ? Rarity.Common : type == MedkitType.Standard ? Rarity.Uncommon : Rarity.Rare;

        public static string DisplayName(MedkitType type) =>
            type == MedkitType.Small ? "Small Medkit" : type == MedkitType.Standard ? "Standard Medkit" : "Advanced Medkit";
    }

    /// <summary>Armor levels 1-3 (spec 15). Damage reduction lives in <see cref="HealthRules"/>.</summary>
    public static class ArmorInfo
    {
        public const int MaxLevel = 3;

        public static float Power(int level) => level <= 0 ? 0f : level == 1 ? 6f : level == 2 ? 10f : 14f;

        public static Rarity RarityOf(int level) => level <= 1 ? Rarity.Common : level == 2 ? Rarity.Rare : Rarity.Epic;

        public static string DisplayName(int level) => $"Level {level} Armor";
    }

    public static class AmmoInfo
    {
        public static readonly AmmoType[] All = { AmmoType.Light, AmmoType.Rifle, AmmoType.Shell, AmmoType.Marksman };

        /// <summary>Maximum carried per type.</summary>
        public static int Cap(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Light: return 240;
                case AmmoType.Rifle: return 240;
                case AmmoType.Shell: return 48;
                case AmmoType.Marksman: return 60;
                default: return 0;
            }
        }

        public static string DisplayName(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Light: return "Light Ammo";
                case AmmoType.Rifle: return "Rifle Ammo";
                case AmmoType.Shell: return "Shells";
                case AmmoType.Marksman: return "Marksman Rounds";
                default: return "Ammo";
            }
        }
    }

    /// <summary>
    /// Everything a player carries besides the three weapon slots: ammunition by type, medkits by
    /// type and armor. A fixed-layout value type so it can be networked and rolled back.
    /// Owned by the authority (spec 7: inventory and ammunition are authoritative on the server).
    /// </summary>
    public struct PlayerInventory
    {
        public int LightAmmo;
        public int RifleAmmo;
        public int ShellAmmo;
        public int MarksmanAmmo;
        public int SmallMedkits;
        public int StandardMedkits;
        public int AdvancedMedkits;
        public int ArmorLevel;

        public int GetAmmo(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Light: return LightAmmo;
                case AmmoType.Rifle: return RifleAmmo;
                case AmmoType.Shell: return ShellAmmo;
                case AmmoType.Marksman: return MarksmanAmmo;
                default: return 0;
            }
        }

        public void SetAmmo(AmmoType type, int value)
        {
            value = Math.Max(0, Math.Min(AmmoInfo.Cap(type), value));
            switch (type)
            {
                case AmmoType.Light: LightAmmo = value; break;
                case AmmoType.Rifle: RifleAmmo = value; break;
                case AmmoType.Shell: ShellAmmo = value; break;
                case AmmoType.Marksman: MarksmanAmmo = value; break;
            }
        }

        /// <summary>Adds up to the cap; returns how many were actually taken.</summary>
        public int AddAmmo(AmmoType type, int count)
        {
            if (count <= 0 || type == AmmoType.None)
                return 0;
            int before = GetAmmo(type);
            SetAmmo(type, before + count);
            return GetAmmo(type) - before;
        }

        public int GetMedkits(MedkitType type) =>
            type == MedkitType.Small ? SmallMedkits : type == MedkitType.Standard ? StandardMedkits : AdvancedMedkits;

        public void SetMedkits(MedkitType type, int value)
        {
            value = Math.Max(0, Math.Min(Medkits.MaxPerType, value));
            if (type == MedkitType.Small) SmallMedkits = value;
            else if (type == MedkitType.Standard) StandardMedkits = value;
            else AdvancedMedkits = value;
        }

        public int AddMedkits(MedkitType type, int count)
        {
            if (count <= 0)
                return 0;
            int before = GetMedkits(type);
            SetMedkits(type, before + count);
            return GetMedkits(type) - before;
        }

        public int TotalMedkits => SmallMedkits + StandardMedkits + AdvancedMedkits;
    }
}
