using System;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Items
{
    public struct LootClaimResult
    {
        public int Taken;
        /// <summary>A weapon or ammunition was taken (ends spawn protection, spec 6).</summary>
        public bool TookCombatItem;
        public string Message;
    }

    /// <summary>
    /// The authority's loot claim (spec 14), shared by the online server and the offline match:
    /// take one item (index 0-11) or everything that fits (index -1). Each item is removed with the
    /// atomic <see cref="LootContainer.TryTake"/>, applied with <see cref="InventoryRules.ApplyPickup"/>,
    /// and anything swapped out or left over goes back into the same slot, so nothing is duplicated
    /// or lost. TAKE ALL never swaps weapons away; it only fills empty weapon slots.
    /// </summary>
    public static class LootClaims
    {
        public static LootClaimResult Take(LootContainer container, WeaponState[] slots, ref int activeSlot, ref PlayerInventory inventory,
            int index, Func<short, WeaponStats> weapons)
        {
            var result = new LootClaimResult();
            if (container == null || slots == null)
                return result;

            int first = index >= 0 ? index : 0;
            int last = index >= 0 ? index : LootContainer.Capacity - 1;
            for (int i = first; i <= last; i++)
            {
                if (!container.TryTake(i, out var item))
                {
                    if (index >= 0)
                        result.Message = "Already taken.";
                    continue;
                }

                if (index < 0 && item.Kind == LootKind.Weapon)
                {
                    int target = InventoryRules.TargetSlot(slots, activeSlot, weapons(item.WeaponId));
                    if (target < 0 || !slots[target].IsEmptySlot)
                    {
                        container.Put(i, item);
                        continue;
                    }
                }

                var outcome = InventoryRules.ApplyPickup(ref inventory, slots, activeSlot, item, weapons);
                if (!outcome.ReturnToContainer.IsEmpty)
                    container.Put(i, outcome.ReturnToContainer);
                if (outcome.Result != PickupResult.Refused)
                {
                    result.Taken++;
                    result.TookCombatItem |= outcome.IsCombatItem;
                }
                result.Message = outcome.Message;
            }

            if (slots[activeSlot].IsEmptySlot)
                activeSlot = slots[0].IsEmptySlot ? CombatLoadout.MeleeSlot : 0;
            if (index < 0)
                result.Message = result.Taken > 0 ? $"Took {result.Taken} item{(result.Taken == 1 ? string.Empty : "s")}." : "Nothing you can carry.";
            result.Message ??= string.Empty;
            return result;
        }
    }
}
