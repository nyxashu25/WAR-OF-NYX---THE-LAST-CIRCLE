using System;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Items
{
    public enum PickupResult : byte
    {
        Taken = 0,
        /// <summary>Only part of a stack fitted; the rest stays in the container.</summary>
        TakenPartially = 1,
        /// <summary>The item replaced something the player had; the old item goes back into the container.</summary>
        Swapped = 2,
        /// <summary>Nothing could be taken (full stack, equal or better armor already worn).</summary>
        Refused = 3,
    }

    public struct PickupOutcome
    {
        public PickupResult Result;
        /// <summary>Put this back into the container slot the item came from (remainder or swapped-out item).</summary>
        public LootItem ReturnToContainer;
        /// <summary>Weapons and ammunition count as "combat items" and end spawn protection (spec 6).</summary>
        public bool IsCombatItem;
        /// <summary>Weapon slot that received the item, or -1.</summary>
        public int Slot;
        public string Message;
    }

    /// <summary>
    /// Server-authoritative pickup rules (spec 14 "Looting Interface": quick pickup and weapon
    /// replacement). Weapons go to an empty matching slot, otherwise they replace the active ranged
    /// weapon (or the melee weapon) and the replaced weapon goes back into the container, so items are
    /// moved, never created or destroyed.
    /// </summary>
    public static class InventoryRules
    {
        public static PickupOutcome ApplyPickup(ref PlayerInventory inventory, WeaponState[] slots, int activeSlot,
            in LootItem item, Func<short, WeaponStats> weapons)
        {
            switch (item.Kind)
            {
                case LootKind.Weapon:
                    return PickupWeapon(slots, activeSlot, item, weapons);
                case LootKind.Ammo:
                {
                    int taken = inventory.AddAmmo(item.Ammo, item.Count);
                    return Stack(taken, item, AmmoInfo.DisplayName(item.Ammo), combat: true);
                }
                case LootKind.Medkit:
                {
                    int taken = inventory.AddMedkits(item.Medkit, item.Count);
                    return Stack(taken, item, Medkits.DisplayName(item.Medkit), combat: false);
                }
                case LootKind.Armor:
                {
                    if (item.ArmorLevel <= inventory.ArmorLevel)
                        return new PickupOutcome
                        {
                            Result = PickupResult.Refused, ReturnToContainer = item, Slot = -1,
                            Message = $"You already wear Level {inventory.ArmorLevel} armor.",
                        };
                    int old = inventory.ArmorLevel;
                    inventory.ArmorLevel = Math.Min(ArmorInfo.MaxLevel, item.ArmorLevel);
                    return new PickupOutcome
                    {
                        Result = old > 0 ? PickupResult.Swapped : PickupResult.Taken,
                        ReturnToContainer = old > 0 ? LootItem.ArmorPiece(old) : default,
                        Slot = -1,
                        Message = $"Equipped {ArmorInfo.DisplayName(inventory.ArmorLevel)}",
                    };
                }
                default:
                    return new PickupOutcome { Result = PickupResult.Refused, Slot = -1, Message = "Nothing there." };
            }
        }

        /// <summary>Which slot a weapon pickup would go to (for the loot UI's TAKE / SWAP label).</summary>
        public static int TargetSlot(WeaponState[] slots, int activeSlot, WeaponStats weapon)
        {
            if (weapon == null)
                return -1;
            if (weapon.IsMelee)
                return CombatLoadout.MeleeSlot;
            if (slots[0].IsEmptySlot) return 0;
            if (slots[1].IsEmptySlot) return 1;
            return activeSlot == 0 || activeSlot == 1 ? activeSlot : 0;
        }

        private static PickupOutcome PickupWeapon(WeaponState[] slots, int activeSlot, in LootItem item, Func<short, WeaponStats> weapons)
        {
            var stats = weapons(item.WeaponId);
            if (stats == null)
                return new PickupOutcome { Result = PickupResult.Refused, ReturnToContainer = item, Slot = -1, Message = "Unknown weapon." };

            int slot = TargetSlot(slots, activeSlot, stats);
            var previous = slots[slot];
            slots[slot] = new WeaponState
            {
                WeaponId = item.WeaponId,
                Magazine = stats.IsRanged ? Math.Max(0, Math.Min(stats.magazineSize, item.Magazine)) : 0,
            };

            bool swapped = !previous.IsEmptySlot;
            return new PickupOutcome
            {
                Result = swapped ? PickupResult.Swapped : PickupResult.Taken,
                ReturnToContainer = swapped ? LootItem.Weapon(previous) : default,
                IsCombatItem = true,
                Slot = slot,
                Message = swapped ? $"Swapped for {stats.displayName}" : $"Picked up {stats.displayName}",
            };
        }

        private static PickupOutcome Stack(int taken, in LootItem item, string name, bool combat)
        {
            if (taken <= 0)
                return new PickupOutcome { Result = PickupResult.Refused, ReturnToContainer = item, Slot = -1, Message = $"{name}: already full." };

            var remainder = item;
            remainder.Count = item.Count - taken;
            return new PickupOutcome
            {
                Result = remainder.Count > 0 ? PickupResult.TakenPartially : PickupResult.Taken,
                ReturnToContainer = remainder.Count > 0 ? remainder : default,
                IsCombatItem = combat,
                Slot = -1,
                Message = $"+{taken} {name}",
            };
        }
    }

    public struct HealState
    {
        public MedkitType Type;
        /// <summary>0 = not healing.</summary>
        public float EndsAt;
        public float StartedAt;

        public bool Active => EndsAt > 0f;
    }

    public enum HealBlockReason : byte
    {
        None = 0,
        Dead = 1,
        FullHealth = 2,
        NoMedkit = 3,
        AlreadyHealing = 4,
    }

    /// <summary>
    /// Medkit channelling (spec 15): using a medkit takes 2/4/6 s. Sprinting or attacking interrupts it
    /// (MVP rule). The medkit is only consumed when the channel completes, so an interrupted heal costs
    /// nothing, and a player can never use a medkit they don't have.
    /// </summary>
    public static class HealingRules
    {
        /// <summary>Smallest medkit that fully covers the missing health, otherwise the largest available; null if none.</summary>
        public static MedkitType? ChooseMedkit(in PlayerInventory inventory, float missingHealth)
        {
            MedkitType? largest = null;
            foreach (var type in Medkits.All)
            {
                if (inventory.GetMedkits(type) <= 0)
                    continue;
                if (Medkits.HealAmount(type) >= missingHealth)
                    return type;
                largest = type;
            }
            return largest;
        }

        public static HealBlockReason TryStart(ref HealState heal, in PlayerInventory inventory, MedkitType type, in HealthState health, float now)
        {
            if (!health.Alive) return HealBlockReason.Dead;
            if (heal.Active) return HealBlockReason.AlreadyHealing;
            if (health.Health >= health.MaxHealth) return HealBlockReason.FullHealth;
            if (inventory.GetMedkits(type) <= 0) return HealBlockReason.NoMedkit;

            heal.Type = type;
            heal.StartedAt = now;
            heal.EndsAt = now + Medkits.UseSeconds(type);
            return HealBlockReason.None;
        }

        /// <summary>Completes a finished channel: consumes the medkit and heals (capped at max). Returns the amount healed.</summary>
        public static float TryComplete(ref HealState heal, ref PlayerInventory inventory, ref HealthState health, float now)
        {
            if (!heal.Active || now < heal.EndsAt)
                return 0f;

            var type = heal.Type;
            heal = default;
            if (!health.Alive || inventory.GetMedkits(type) <= 0)
                return 0f;

            inventory.SetMedkits(type, inventory.GetMedkits(type) - 1);
            return HealthRules.Heal(ref health, Medkits.HealAmount(type));
        }

        public static void Interrupt(ref HealState heal) => heal = default;

        public static float Progress(in HealState heal, float now)
        {
            if (!heal.Active)
                return 0f;
            float total = heal.EndsAt - heal.StartedAt;
            return total <= 0f ? 1f : Math.Max(0f, Math.Min(1f, (now - heal.StartedAt) / total));
        }
    }
}
