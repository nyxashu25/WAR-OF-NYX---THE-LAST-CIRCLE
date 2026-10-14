using System;
using System.Collections.Generic;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// Builds the loot panel rows (name, rarity colour, details, TAKE / SWAP / FULL) from a capsule's
    /// items and the viewer's inventory. Shared by online capsules and offline ones, so both look and
    /// behave the same. The button only REQUESTS a pickup; the authority applies the inventory rules.
    /// </summary>
    public static class LootViews
    {
        public static void Build(Func<int, LootItem> item, WeaponState[] slots, int activeSlot, in PlayerInventory inventory,
            List<LootEntryView> output)
        {
            output.Clear();
            for (int i = 0; i < LootContainer.Capacity; i++)
            {
                var entry = item(i);
                var view = new LootEntryView { Enabled = true, Action = "TAKE" };
                switch (entry.Kind)
                {
                    case LootKind.Weapon:
                    {
                        var stats = WeaponRegistry.Get(entry.WeaponId);
                        if (stats == null)
                            break;
                        view.Name = stats.displayName;
                        view.Color = RarityColors.For(stats.rarity);
                        int target = InventoryRules.TargetSlot(slots, activeSlot, stats);
                        var current = target >= 0 ? WeaponRegistry.Get(slots[target].WeaponId) : null;
                        string ammo = stats.IsRanged
                            ? $"{entry.Magazine}/{stats.magazineSize} loaded, {AmmoInfo.DisplayName(stats.ammoType)}"
                            : "Melee";
                        view.Detail = current != null
                            ? $"{ammo}  |  replaces {current.displayName} ({current.damage:0} -> {stats.damage:0} dmg)"
                            : $"{ammo}  |  {stats.damage:0} dmg";
                        view.Action = current != null ? $"SWAP [{target + 1}]" : "TAKE";
                        break;
                    }
                    case LootKind.Ammo:
                        view.Name = AmmoInfo.DisplayName(entry.Ammo);
                        view.Color = RarityColors.For(Rarity.Common);
                        view.Detail = $"x{entry.Count}  (you carry {inventory.GetAmmo(entry.Ammo)}/{AmmoInfo.Cap(entry.Ammo)})";
                        view.Enabled = inventory.GetAmmo(entry.Ammo) < AmmoInfo.Cap(entry.Ammo);
                        view.Action = view.Enabled ? "TAKE" : "FULL";
                        break;
                    case LootKind.Medkit:
                        view.Name = Medkits.DisplayName(entry.Medkit);
                        view.Color = RarityColors.For(Medkits.RarityOf(entry.Medkit));
                        view.Detail = $"x{entry.Count}  (+{Medkits.HealAmount(entry.Medkit):0} HP, {Medkits.UseSeconds(entry.Medkit):0}s)";
                        view.Enabled = inventory.GetMedkits(entry.Medkit) < Medkits.MaxPerType;
                        view.Action = view.Enabled ? "TAKE" : "FULL";
                        break;
                    case LootKind.Armor:
                        view.Name = ArmorInfo.DisplayName(entry.ArmorLevel);
                        view.Color = RarityColors.For(ArmorInfo.RarityOf(entry.ArmorLevel));
                        view.Detail = $"you wear Level {inventory.ArmorLevel}";
                        view.Enabled = entry.ArmorLevel > inventory.ArmorLevel;
                        view.Action = view.Enabled ? (inventory.ArmorLevel > 0 ? "SWAP" : "EQUIP") : "OWNED";
                        break;
                }
                output.Add(view);
            }
        }

        public static string Summary(WeaponState[] slots, in PlayerInventory inventory, float secondsLeft) =>
            $"YOU: [1] {Display(slots[0])}  [2] {Display(slots[1])}  [3] {Display(slots[2])}\n" +
            $"Armor L{inventory.ArmorLevel}   Medkits S{inventory.SmallMedkits} M{inventory.StandardMedkits} L{inventory.AdvancedMedkits}   " +
            $"Ammo L{inventory.LightAmmo} R{inventory.RifleAmmo} S{inventory.ShellAmmo} M{inventory.MarksmanAmmo}   " +
            (float.IsInfinity(secondsLeft) ? "Supply cache" : $"Capsule closes in {(int)Math.Ceiling(Math.Max(0f, secondsLeft))}s");

        private static string Display(in WeaponState s) => s.IsEmptySlot ? "-" : WeaponRegistry.DisplayName(s.WeaponId);
    }
}
