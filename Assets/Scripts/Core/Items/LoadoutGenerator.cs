using System;
using System.Collections.Generic;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Items
{
    /// <summary>One of the spec 7 example equipment sets (random equipment, not a character class).</summary>
    public sealed class LoadoutPackage
    {
        public string Name;
        public string PrimaryId;
        public string MeleeId;
        public int MedkitCount;
    }

    [Serializable]
    public sealed class LoadoutBudget
    {
        /// <summary>Target power of every starting loadout.</summary>
        public float targetPower = 58f;
        /// <summary>Allowed deviation (±10 %): no package may be much stronger than another (spec 7).</summary>
        public float tolerance = 0.1f;
        /// <summary>"Basic protection" for everyone.</summary>
        public int armorLevel = 1;
        /// <summary>Spare ammo for the primary, in magazines.</summary>
        public float spareMagazines = 2f;

        public float Min => targetPower * (1f - tolerance);
        public float Max => targetPower * (1f + tolerance);
    }

    public sealed class StartingLoadout
    {
        public LoadoutPackage Package;
        public WeaponState[] Slots = new WeaponState[CombatLoadout.SlotCount];
        public PlayerInventory Inventory;
        public float Power;
    }

    /// <summary>
    /// Server-side randomized starting loadouts (spec 7). Picks one of the six packages at random,
    /// then chooses the medkit mix so the loadout's total power lands inside the budget band.
    /// Every loadout has a primary ranged weapon, a melee weapon, spare ammo for the primary,
    /// 1–2 medkits and Level 1 armor. Deterministic for a seed (replayable, testable).
    /// </summary>
    public static class LoadoutGenerator
    {
        public static readonly LoadoutPackage[] Packages =
        {
            new LoadoutPackage { Name = "Shadow Runner", PrimaryId = WeaponCatalog.ShadowSmg, MeleeId = WeaponCatalog.Katana, MedkitCount = 2 },
            new LoadoutPackage { Name = "Iron Hunter", PrimaryId = WeaponCatalog.Nx7AssaultRifle, MeleeId = WeaponCatalog.CombatKnife, MedkitCount = 1 },
            new LoadoutPackage { Name = "Neon Reaper", PrimaryId = WeaponCatalog.CrimsonShotgun, MeleeId = WeaponCatalog.EnergyBlade, MedkitCount = 2 },
            new LoadoutPackage { Name = "Phantom Scout", PrimaryId = WeaponCatalog.PhantomMarksman, MeleeId = WeaponCatalog.Katana, MedkitCount = 1 },
            new LoadoutPackage { Name = "Crimson Fighter", PrimaryId = WeaponCatalog.StormBurstRifle, MeleeId = WeaponCatalog.Dagger, MedkitCount = 2 },
            new LoadoutPackage { Name = "Storm Raider", PrimaryId = WeaponCatalog.StormLightSmg, MeleeId = WeaponCatalog.EnergyBlade, MedkitCount = 1 },
        };

        public static StartingLoadout Generate(int seed, LoadoutBudget budget, Func<short, WeaponStats> weapons)
        {
            if (budget == null) throw new ArgumentNullException(nameof(budget));
            if (weapons == null) throw new ArgumentNullException(nameof(weapons));

            var random = new Random(seed);
            var package = Packages[random.Next(Packages.Length)];
            short primaryId = WeaponCatalog.NetworkIdOf(package.PrimaryId);
            short meleeId = WeaponCatalog.NetworkIdOf(package.MeleeId);
            var primary = weapons(primaryId);
            var melee = weapons(meleeId);

            float basePower = primary.powerScore + melee.powerScore + ArmorInfo.Power(budget.armorLevel);

            // Enumerate medkit mixes for this package and keep the ones inside the band.
            var options = new List<(int small, int standard, float power)>();
            for (int standard = 0; standard <= package.MedkitCount; standard++)
            {
                int small = package.MedkitCount - standard;
                float power = basePower + small * Medkits.Power(MedkitType.Small) + standard * Medkits.Power(MedkitType.Standard);
                options.Add((small, standard, power));
            }

            var inBand = options.FindAll(o => o.power >= budget.Min && o.power <= budget.Max);
            (int small, int standard, float power) chosen;
            if (inBand.Count > 0)
            {
                chosen = inBand[random.Next(inBand.Count)];
            }
            else
            {
                // Nothing fits exactly: take the closest mix (and the band test will flag the tuning).
                chosen = options[0];
                foreach (var o in options)
                    if (Math.Abs(o.power - budget.targetPower) < Math.Abs(chosen.power - budget.targetPower))
                        chosen = o;
            }

            var loadout = new StartingLoadout { Package = package, Power = chosen.power };
            loadout.Slots[0] = new WeaponState { WeaponId = primaryId, Magazine = primary.magazineSize };
            loadout.Slots[1] = WeaponState.Empty;
            loadout.Slots[CombatLoadout.MeleeSlot] = new WeaponState { WeaponId = meleeId };

            var inventory = new PlayerInventory { ArmorLevel = budget.armorLevel };
            inventory.AddAmmo(primary.ammoType, (int)Math.Round(primary.magazineSize * budget.spareMagazines));
            inventory.AddMedkits(MedkitType.Small, chosen.small);
            inventory.AddMedkits(MedkitType.Standard, chosen.standard);
            loadout.Inventory = inventory;
            return loadout;
        }

        /// <summary>Power of every possible mix for every package (used by tests to verify the budget).</summary>
        public static IEnumerable<(LoadoutPackage package, float min, float max)> PowerRanges(LoadoutBudget budget, Func<short, WeaponStats> weapons)
        {
            foreach (var p in Packages)
            {
                float basePower = weapons(WeaponCatalog.NetworkIdOf(p.PrimaryId)).powerScore
                                  + weapons(WeaponCatalog.NetworkIdOf(p.MeleeId)).powerScore
                                  + ArmorInfo.Power(budget.armorLevel);
                yield return (p, basePower + p.MedkitCount * Medkits.Power(MedkitType.Small),
                    basePower + p.MedkitCount * Medkits.Power(MedkitType.Standard));
            }
        }
    }
}
