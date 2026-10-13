using System;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Core.Bots
{
    /// <summary>
    /// Context-aware loot choice for bots (spec 38.13). Scores each item in a capsule against what the
    /// bot carries; the actual pickup then goes through the SAME <see cref="InventoryRules.ApplyPickup"/>
    /// as a human's TAKE button. Examples from the spec: a wounded bot values medkits over another
    /// weapon; a bot with a shotgun and little ammo swaps it for an assault rifle.
    /// </summary>
    public static class BotLootPlanner
    {
        /// <summary>Usefulness of one item to this bot (≤ 0 = not worth taking).</summary>
        public static float Value(in LootItem item, WeaponState[] slots, in PlayerInventory inventory, float healthFraction,
            Func<short, WeaponStats> weapons)
        {
            switch (item.Kind)
            {
                case LootKind.Medkit:
                    if (inventory.GetMedkits(item.Medkit) >= Medkits.MaxPerType) return 0f;
                    return 10f + 40f * (1f - healthFraction) + Medkits.HealAmount(item.Medkit) * 0.1f;

                case LootKind.Armor:
                    return item.ArmorLevel > inventory.ArmorLevel ? 20f + 8f * (item.ArmorLevel - inventory.ArmorLevel) : 0f;

                case LootKind.Ammo:
                {
                    if (inventory.GetAmmo(item.Ammo) >= AmmoInfo.Cap(item.Ammo)) return 0f;
                    bool usable = false;
                    int magazine = 30;
                    foreach (var s in slots)
                    {
                        var w = weapons(s.WeaponId);
                        if (w != null && w.IsRanged && w.ammoType == item.Ammo)
                        {
                            usable = true;
                            magazine = w.magazineSize;
                        }
                    }
                    if (!usable) return 1f;
                    int carried = inventory.GetAmmo(item.Ammo);
                    return carried < magazine * 2 ? 30f : 8f;
                }

                case LootKind.Weapon:
                {
                    var candidate = weapons(item.WeaponId);
                    if (candidate == null) return 0f;
                    if (candidate.IsMelee)
                    {
                        var melee = weapons(slots[CombatLoadout.MeleeSlot].WeaponId);
                        return melee == null ? 18f : candidate.powerScore - melee.powerScore;
                    }

                    // Ranged: an empty secondary slot is always worth filling; otherwise compare with the
                    // weaker ranged weapon carried, counting a weapon without ammunition as worthless.
                    float weakest = float.MaxValue;
                    bool emptySlot = false;
                    for (int i = 0; i < CombatLoadout.MeleeSlot; i++)
                    {
                        if (slots[i].IsEmptySlot)
                        {
                            emptySlot = true;
                            continue;
                        }
                        var w = weapons(slots[i].WeaponId);
                        if (w == null || !w.IsRanged) continue;
                        bool starved = slots[i].Magazine + inventory.GetAmmo(w.ammoType) == 0;
                        weakest = Math.Min(weakest, starved ? 0f : w.powerScore);
                    }
                    bool hasAmmoFor = item.Magazine > 0 || inventory.GetAmmo(candidate.ammoType) > 0;
                    if (!hasAmmoFor) return emptySlot ? 4f : 0f;
                    if (emptySlot) return 18f + candidate.powerScore * 0.5f;
                    return weakest == float.MaxValue ? 18f : candidate.powerScore - weakest - 2f;
                }

                default:
                    return 0f;
            }
        }

        /// <summary>Index of the item to take next, or -1 when nothing is worth it.</summary>
        public static int Choose(LootContainer container, WeaponState[] slots, in PlayerInventory inventory, float healthFraction,
            LootSelectionStrategy strategy, Random random, Func<short, WeaponStats> weapons)
        {
            if (container == null)
                return -1;
            int best = -1, positives = 0;
            float bestValue = 0f;
            for (int i = 0; i < LootContainer.Capacity; i++)
            {
                var item = container[i];
                if (item.IsEmpty)
                    continue;
                float value = Value(item, slots, inventory, healthFraction, weapons);
                if (value <= 0f)
                    continue;
                positives++;
                switch (strategy)
                {
                    case LootSelectionStrategy.Randomized:
                        // Reservoir sampling: any useful item, uniformly.
                        if (random.Next(positives) == 0)
                            best = i;
                        break;
                    case LootSelectionStrategy.Basic:
                        if (best < 0)
                            best = i; // first useful item
                        break;
                    default: // Tactical / Optimized: the most valuable
                        float v = strategy == LootSelectionStrategy.Optimized ? value : value * (0.85f + 0.3f * (float)random.NextDouble());
                        if (v > bestValue)
                        {
                            bestValue = v;
                            best = i;
                        }
                        break;
                }
            }
            return best;
        }
    }
}
