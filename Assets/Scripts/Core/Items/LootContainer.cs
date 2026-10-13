using System;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Items
{
    public enum LootKind : byte
    {
        Empty = 0,
        Weapon = 1,
        Ammo = 2,
        Medkit = 3,
        Armor = 4,
    }

    /// <summary>One entry in a loot container. Weapons keep their magazine (spec 14: "preserve the actual ammunition quantities and weapon state").</summary>
    public struct LootItem : IEquatable<LootItem>
    {
        public LootKind Kind;
        public short WeaponId;
        public int Magazine;
        public AmmoType Ammo;
        public MedkitType Medkit;
        public int Count;
        public int ArmorLevel;

        public bool IsEmpty => Kind == LootKind.Empty;

        public static LootItem Weapon(in WeaponState state) =>
            new LootItem { Kind = LootKind.Weapon, WeaponId = state.WeaponId, Magazine = state.Magazine };

        public static LootItem AmmoStack(AmmoType type, int count) =>
            new LootItem { Kind = LootKind.Ammo, Ammo = type, Count = count };

        public static LootItem MedkitStack(MedkitType type, int count) =>
            new LootItem { Kind = LootKind.Medkit, Medkit = type, Count = count };

        public static LootItem ArmorPiece(int level) =>
            new LootItem { Kind = LootKind.Armor, ArmorLevel = level };

        public Rarity RarityOf(Func<short, WeaponStats> weapons)
        {
            switch (Kind)
            {
                case LootKind.Weapon: return weapons?.Invoke(WeaponId)?.rarity ?? Rarity.Common;
                case LootKind.Medkit: return Medkits.RarityOf(Medkit);
                case LootKind.Armor: return ArmorInfo.RarityOf(ArmorLevel);
                default: return Rarity.Common;
            }
        }

        public bool Equals(LootItem o) =>
            Kind == o.Kind && WeaponId == o.WeaponId && Magazine == o.Magazine && Ammo == o.Ammo &&
            Medkit == o.Medkit && Count == o.Count && ArmorLevel == o.ArmorLevel;

        public override bool Equals(object obj) => obj is LootItem o && Equals(o);

        public override int GetHashCode() => ((int)Kind * 397) ^ (WeaponId * 31) ^ Count ^ (ArmorLevel << 8);
    }

    /// <summary>
    /// The contents of an elimination loot capsule (spec 14). Fixed capacity so it maps 1:1 to
    /// networked state. All mutation happens on the server; <see cref="TryTake"/> is the atomic
    /// "check and remove" that guarantees one item can only ever be claimed once.
    /// </summary>
    public sealed class LootContainer
    {
        public const int Capacity = 12;

        private readonly LootItem[] _items = new LootItem[Capacity];

        public LootItem this[int index] => index >= 0 && index < Capacity ? _items[index] : default;

        public bool IsEmpty
        {
            get
            {
                foreach (var item in _items)
                    if (!item.IsEmpty)
                        return false;
                return true;
            }
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var item in _items)
                    if (!item.IsEmpty)
                        n++;
                return n;
            }
        }

        /// <summary>Everything an eliminated player carried: weapons (with magazines), ammo, medkits, armor.</summary>
        public static LootContainer FromEliminated(WeaponState[] slots, in PlayerInventory inventory)
        {
            var c = new LootContainer();
            if (slots != null)
                foreach (var s in slots)
                    if (!s.IsEmptySlot)
                        c.Add(LootItem.Weapon(s));

            foreach (var type in AmmoInfo.All)
            {
                int n = inventory.GetAmmo(type);
                if (n > 0)
                    c.Add(LootItem.AmmoStack(type, n));
            }

            foreach (var type in Medkits.All)
            {
                int n = inventory.GetMedkits(type);
                if (n > 0)
                    c.Add(LootItem.MedkitStack(type, n));
            }

            if (inventory.ArmorLevel > 0)
                c.Add(LootItem.ArmorPiece(inventory.ArmorLevel));
            return c;
        }

        /// <summary>Adds to the first free slot. Returns false when full.</summary>
        public bool Add(in LootItem item)
        {
            if (item.IsEmpty)
                return true;
            for (int i = 0; i < Capacity; i++)
            {
                if (_items[i].IsEmpty)
                {
                    _items[i] = item;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Atomic claim: returns the item and empties the slot; a second claim of the same slot fails.</summary>
        public bool TryTake(int index, out LootItem item)
        {
            item = default;
            if (index < 0 || index >= Capacity || _items[index].IsEmpty)
                return false;
            item = _items[index];
            _items[index] = default;
            return true;
        }

        /// <summary>Puts an item back into a specific slot (remainders and swapped-out weapons).</summary>
        public void Put(int index, in LootItem item)
        {
            if (index >= 0 && index < Capacity)
                _items[index] = item;
        }

        public Rarity HighestRarity(Func<short, WeaponStats> weapons)
        {
            var best = Rarity.Common;
            foreach (var item in _items)
            {
                if (item.IsEmpty)
                    continue;
                var r = item.RarityOf(weapons);
                if (r > best)
                    best = r;
            }
            return best;
        }
    }
}
