using System;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;

namespace WarOfNyx.Tests
{
    public class LoadoutGeneratorTests
    {
        private static readonly Func<short, WeaponStats> Weapons = WeaponCatalog.Get;
        private readonly LoadoutBudget _budget = new LoadoutBudget();

        [Test]
        public void EveryPackageCanLandInsideThePowerBand()
        {
            foreach (var (package, min, max) in LoadoutGenerator.PowerRanges(_budget, Weapons))
                Assert.IsTrue(max >= _budget.Min && min <= _budget.Max,
                    $"{package.Name}: {min}-{max} vs band {_budget.Min}-{_budget.Max}");
        }

        [Test]
        public void ThousandLoadoutsAreValidAndBalanced()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var l = LoadoutGenerator.Generate(seed, _budget, Weapons);
                seen.Add(l.Package.Name);

                var primary = Weapons(l.Slots[0].WeaponId);
                var melee = Weapons(l.Slots[CombatLoadout.MeleeSlot].WeaponId);
                Assert.IsTrue(primary.IsRanged, "a primary ranged weapon");
                Assert.AreEqual(primary.magazineSize, l.Slots[0].Magazine, "full magazine");
                Assert.IsTrue(melee.IsMelee, "a melee weapon");
                Assert.IsTrue(l.Slots[1].IsEmptySlot, "secondary slot empty at start");
                Assert.Greater(l.Inventory.GetAmmo(primary.ammoType), 0, "spare ammo for the primary");
                Assert.That(l.Inventory.TotalMedkits, Is.InRange(1, 2), "1-2 medkits");
                Assert.AreEqual(1, l.Inventory.ArmorLevel, "basic protection");
                Assert.That(l.Power, Is.InRange(_budget.Min, _budget.Max), $"seed {seed} {l.Package.Name} power {l.Power}");
            }
            Assert.AreEqual(LoadoutGenerator.Packages.Length, seen.Count, "all six packages appear");
        }

        [Test]
        public void SameSeedSameLoadout()
        {
            var a = LoadoutGenerator.Generate(77, _budget, Weapons);
            var b = LoadoutGenerator.Generate(77, _budget, Weapons);
            Assert.AreEqual(a.Package.Name, b.Package.Name);
            CollectionAssert.AreEqual(a.Slots, b.Slots);
            Assert.AreEqual(a.Inventory, b.Inventory);
        }

        [Test]
        public void SpecPackagesAreTheSpecTable()
        {
            var byName = LoadoutGenerator.Packages.ToDictionary(p => p.Name);
            Assert.AreEqual(WeaponCatalog.ShadowSmg, byName["Shadow Runner"].PrimaryId);
            Assert.AreEqual(WeaponCatalog.Katana, byName["Shadow Runner"].MeleeId);
            Assert.AreEqual(2, byName["Shadow Runner"].MedkitCount);
            Assert.AreEqual(WeaponCatalog.PhantomMarksman, byName["Phantom Scout"].PrimaryId);
            Assert.AreEqual(1, byName["Phantom Scout"].MedkitCount);
            Assert.AreEqual(WeaponCatalog.Dagger, byName["Crimson Fighter"].MeleeId);
        }
    }

    public class InventoryTests
    {
        [Test]
        public void AmmoAndMedkitsAreCapped()
        {
            var inv = new PlayerInventory();
            Assert.AreEqual(48, inv.AddAmmo(AmmoType.Shell, 100));
            Assert.AreEqual(0, inv.AddAmmo(AmmoType.Shell, 5));
            Assert.AreEqual(Medkits.MaxPerType, inv.AddMedkits(MedkitType.Small, 50));
            Assert.AreEqual(0, inv.AddAmmo(AmmoType.None, 10));
            Assert.AreEqual(0, inv.AddAmmo(AmmoType.Rifle, -5));
        }
    }

    public class LootContainerTests
    {
        private static WeaponState[] Slots(params string[] ids) =>
            ids.Select(id => id == null ? WeaponState.Empty : new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(id), Magazine = 7 }).ToArray();

        [Test]
        public void EliminatedPlayerDropsEverything()
        {
            var inv = new PlayerInventory { RifleAmmo = 45, SmallMedkits = 2, StandardMedkits = 1, ArmorLevel = 2 };
            var c = LootContainer.FromEliminated(Slots(WeaponCatalog.Nx7AssaultRifle, null, WeaponCatalog.Katana), inv);

            Assert.AreEqual(6, c.Count, "2 weapons + rifle ammo + 2 medkit stacks + armor");
            Assert.AreEqual(LootKind.Weapon, c[0].Kind);
            Assert.AreEqual(7, c[0].Magazine, "weapon keeps its magazine");
            Assert.IsTrue(Enumerable.Range(0, LootContainer.Capacity).Any(i => c[i].Kind == LootKind.Ammo && c[i].Count == 45));
            Assert.IsTrue(Enumerable.Range(0, LootContainer.Capacity).Any(i => c[i].Kind == LootKind.Armor && c[i].ArmorLevel == 2));
        }

        [Test]
        public void AnItemCanOnlyBeClaimedOnce()
        {
            var c = new LootContainer();
            c.Add(LootItem.MedkitStack(MedkitType.Advanced, 1));
            Assert.IsTrue(c.TryTake(0, out var first));
            Assert.IsFalse(c.TryTake(0, out _), "second claim of the same slot fails (no duplication)");
            Assert.AreEqual(MedkitType.Advanced, first.Medkit);
            Assert.IsTrue(c.IsEmpty);
        }

        [Test]
        public void InvalidIndicesAreRejected()
        {
            var c = new LootContainer();
            c.Add(LootItem.AmmoStack(AmmoType.Light, 10));
            Assert.IsFalse(c.TryTake(-1, out _));
            Assert.IsFalse(c.TryTake(LootContainer.Capacity, out _));
            Assert.IsFalse(c.TryTake(5, out _), "empty slot");
        }

        [Test]
        public void HighestRarityDrivesTheCapsuleColour()
        {
            var c = new LootContainer();
            c.Add(LootItem.MedkitStack(MedkitType.Small, 1));
            Assert.AreEqual(Rarity.Common, c.HighestRarity(WeaponCatalog.Get));
            c.Add(LootItem.Weapon(new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(WeaponCatalog.PhantomMarksman) }));
            Assert.AreEqual(Rarity.Epic, c.HighestRarity(WeaponCatalog.Get));
        }

        [Test]
        public void FullContainerRefusesMore()
        {
            var c = new LootContainer();
            for (int i = 0; i < LootContainer.Capacity; i++)
                Assert.IsTrue(c.Add(LootItem.AmmoStack(AmmoType.Light, 1)));
            Assert.IsFalse(c.Add(LootItem.AmmoStack(AmmoType.Light, 1)));
        }
    }

    public class PickupTests
    {
        private static readonly Func<short, WeaponStats> Weapons = WeaponCatalog.Get;

        private static LootItem Weapon(string id, int magazine = 5) =>
            LootItem.Weapon(new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(id), Magazine = magazine });

        private static WeaponState[] Loadout(string primary, string secondary, string melee) => new[]
        {
            primary == null ? WeaponState.Empty : new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(primary), Magazine = 30 },
            secondary == null ? WeaponState.Empty : new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(secondary), Magazine = 12 },
            melee == null ? WeaponState.Empty : new WeaponState { WeaponId = WeaponCatalog.NetworkIdOf(melee) },
        };

        [Test]
        public void WeaponFillsAnEmptySlotAndKeepsItsMagazine()
        {
            var inv = new PlayerInventory();
            var slots = Loadout(WeaponCatalog.Nx7AssaultRifle, null, WeaponCatalog.Katana);
            var o = InventoryRules.ApplyPickup(ref inv, slots, 0, Weapon(WeaponCatalog.ShadowSmg, 17), Weapons);

            Assert.AreEqual(PickupResult.Taken, o.Result);
            Assert.AreEqual(1, o.Slot);
            Assert.AreEqual(17, slots[1].Magazine);
            Assert.IsTrue(o.ReturnToContainer.IsEmpty);
            Assert.IsTrue(o.IsCombatItem, "ends spawn protection");
        }

        [Test]
        public void FullSlotsSwapTheActiveWeaponIntoTheContainer()
        {
            var inv = new PlayerInventory();
            var slots = Loadout(WeaponCatalog.Nx7AssaultRifle, WeaponCatalog.NeonPistol, WeaponCatalog.Katana);
            var o = InventoryRules.ApplyPickup(ref inv, slots, 1, Weapon(WeaponCatalog.PhantomMarksman, 4), Weapons);

            Assert.AreEqual(PickupResult.Swapped, o.Result);
            Assert.AreEqual(1, o.Slot, "replaces the active ranged slot");
            Assert.AreEqual(WeaponCatalog.NetworkIdOf(WeaponCatalog.PhantomMarksman), slots[1].WeaponId);
            Assert.AreEqual(WeaponCatalog.NetworkIdOf(WeaponCatalog.NeonPistol), o.ReturnToContainer.WeaponId);
            Assert.AreEqual(12, o.ReturnToContainer.Magazine, "the dropped pistol keeps its magazine");
        }

        [Test]
        public void WithMeleeOutARangedPickupReplacesThePrimary()
        {
            var inv = new PlayerInventory();
            var slots = Loadout(WeaponCatalog.Nx7AssaultRifle, WeaponCatalog.NeonPistol, WeaponCatalog.Katana);
            var o = InventoryRules.ApplyPickup(ref inv, slots, CombatLoadout.MeleeSlot, Weapon(WeaponCatalog.ShadowSmg), Weapons);
            Assert.AreEqual(0, o.Slot);
        }

        [Test]
        public void MeleePickupReplacesTheMeleeSlot()
        {
            var inv = new PlayerInventory();
            var slots = Loadout(WeaponCatalog.Nx7AssaultRifle, null, WeaponCatalog.Dagger);
            var o = InventoryRules.ApplyPickup(ref inv, slots, 0, Weapon(WeaponCatalog.EnergyBlade, 0), Weapons);
            Assert.AreEqual(CombatLoadout.MeleeSlot, o.Slot);
            Assert.AreEqual(WeaponCatalog.NetworkIdOf(WeaponCatalog.Dagger), o.ReturnToContainer.WeaponId);
            Assert.IsTrue(slots[1].IsEmptySlot, "a blade never goes into a gun slot");
        }

        [Test]
        public void AmmoStacksPartiallyWhenNearlyFull()
        {
            var inv = new PlayerInventory { ShellAmmo = 40 };
            var o = InventoryRules.ApplyPickup(ref inv, Loadout(null, null, null), 0, LootItem.AmmoStack(AmmoType.Shell, 20), Weapons);
            Assert.AreEqual(PickupResult.TakenPartially, o.Result);
            Assert.AreEqual(48, inv.ShellAmmo);
            Assert.AreEqual(12, o.ReturnToContainer.Count, "the rest stays in the capsule");
        }

        [Test]
        public void FullAmmoIsRefusedAndNothingIsLost()
        {
            var inv = new PlayerInventory { ShellAmmo = 48 };
            var item = LootItem.AmmoStack(AmmoType.Shell, 6);
            var o = InventoryRules.ApplyPickup(ref inv, Loadout(null, null, null), 0, item, Weapons);
            Assert.AreEqual(PickupResult.Refused, o.Result);
            Assert.AreEqual(item, o.ReturnToContainer);
        }

        [Test]
        public void BetterArmorSwapsWorseIsRefused()
        {
            var inv = new PlayerInventory { ArmorLevel = 1 };
            var slots = Loadout(null, null, null);
            var o = InventoryRules.ApplyPickup(ref inv, slots, 0, LootItem.ArmorPiece(3), Weapons);
            Assert.AreEqual(PickupResult.Swapped, o.Result);
            Assert.AreEqual(3, inv.ArmorLevel);
            Assert.AreEqual(1, o.ReturnToContainer.ArmorLevel);

            var worse = InventoryRules.ApplyPickup(ref inv, slots, 0, LootItem.ArmorPiece(2), Weapons);
            Assert.AreEqual(PickupResult.Refused, worse.Result);
            Assert.AreEqual(3, inv.ArmorLevel);
        }

        [Test]
        public void TwoPlayersRacingForOneItemNeverDuplicateIt()
        {
            // Both requests reach the server; it processes them one after the other.
            var crate = new LootContainer();
            crate.Add(Weapon(WeaponCatalog.PhantomMarksman, 9));
            var invA = new PlayerInventory();
            var invB = new PlayerInventory();
            var slotsA = Loadout(WeaponCatalog.Nx7AssaultRifle, null, WeaponCatalog.Katana);
            var slotsB = Loadout(WeaponCatalog.ShadowSmg, null, WeaponCatalog.Dagger);

            int winners = 0;
            foreach (var (inv, slots) in new[] { (invA, slotsA), (invB, slotsB) })
            {
                var i = inv;
                if (crate.TryTake(0, out var item))
                {
                    InventoryRules.ApplyPickup(ref i, slots, 0, item, Weapons);
                    winners++;
                }
            }

            Assert.AreEqual(1, winners);
            int marksmen = new[] { slotsA, slotsB }.SelectMany(s => s).Count(s => s.WeaponId == WeaponCatalog.NetworkIdOf(WeaponCatalog.PhantomMarksman));
            Assert.AreEqual(1, marksmen, "exactly one copy exists");
        }
    }

    public class HealingTests
    {
        private static HealthState Hurt(float health) => new HealthState { Health = health, MaxHealth = 100f, Alive = true };

        [TestCase(20f, MedkitType.Small)]
        [TestCase(40f, MedkitType.Standard)]
        [TestCase(70f, MedkitType.Advanced)]
        [TestCase(95f, MedkitType.Advanced)]
        public void PicksTheSmallestMedkitThatCovers(float missing, MedkitType expected)
        {
            var inv = new PlayerInventory { SmallMedkits = 1, StandardMedkits = 1, AdvancedMedkits = 1 };
            Assert.AreEqual(expected, HealingRules.ChooseMedkit(inv, missing));
        }

        [Test]
        public void FallsBackToTheLargestAvailableOrNone()
        {
            Assert.AreEqual(MedkitType.Standard, HealingRules.ChooseMedkit(new PlayerInventory { SmallMedkits = 2, StandardMedkits = 1 }, 90f));
            Assert.IsNull(HealingRules.ChooseMedkit(new PlayerInventory(), 50f));
        }

        [Test]
        public void ChannelThenHealAndConsume()
        {
            var inv = new PlayerInventory { StandardMedkits = 1 };
            var hp = Hurt(30f);
            var heal = new HealState();
            Assert.AreEqual(HealBlockReason.None, HealingRules.TryStart(ref heal, inv, MedkitType.Standard, hp, 10f));
            Assert.AreEqual(0.5f, HealingRules.Progress(heal, 12f), 1e-4f);
            Assert.AreEqual(0f, HealingRules.TryComplete(ref heal, ref inv, ref hp, 13.9f), "4 s channel");
            Assert.AreEqual(50f, HealingRules.TryComplete(ref heal, ref inv, ref hp, 14f));
            Assert.AreEqual(80f, hp.Health);
            Assert.AreEqual(0, inv.StandardMedkits);
            Assert.IsFalse(heal.Active);
        }

        [Test]
        public void InterruptedHealCostsNothing()
        {
            var inv = new PlayerInventory { SmallMedkits = 1 };
            var hp = Hurt(50f);
            var heal = new HealState();
            HealingRules.TryStart(ref heal, inv, MedkitType.Small, hp, 0f);
            HealingRules.Interrupt(ref heal);
            Assert.AreEqual(0f, HealingRules.TryComplete(ref heal, ref inv, ref hp, 5f));
            Assert.AreEqual(1, inv.SmallMedkits);
            Assert.AreEqual(50f, hp.Health);
        }

        [Test]
        public void HealingNeverExceedsMax()
        {
            var inv = new PlayerInventory { AdvancedMedkits = 1 };
            var hp = Hurt(90f);
            var heal = new HealState();
            HealingRules.TryStart(ref heal, inv, MedkitType.Advanced, hp, 0f);
            Assert.AreEqual(10f, HealingRules.TryComplete(ref heal, ref inv, ref hp, 6f));
            Assert.AreEqual(100f, hp.Health);
        }

        [Test]
        public void CannotUseNonexistentOrUnneededMedkits()
        {
            var heal = new HealState();
            Assert.AreEqual(HealBlockReason.NoMedkit, HealingRules.TryStart(ref heal, new PlayerInventory(), MedkitType.Small, Hurt(10f), 0f));
            Assert.AreEqual(HealBlockReason.FullHealth, HealingRules.TryStart(ref heal, new PlayerInventory { SmallMedkits = 1 }, MedkitType.Small, Hurt(100f), 0f));
            var dead = Hurt(0f);
            dead.Alive = false;
            Assert.AreEqual(HealBlockReason.Dead, HealingRules.TryStart(ref heal, new PlayerInventory { SmallMedkits = 1 }, MedkitType.Small, dead, 0f));
        }

        [Test]
        public void MedkitLostMidChannelIsNotConsumedTwice()
        {
            // E.g. the medkit stack changed during the channel: completion re-checks the inventory.
            var inv = new PlayerInventory { SmallMedkits = 1 };
            var hp = Hurt(50f);
            var heal = new HealState();
            HealingRules.TryStart(ref heal, inv, MedkitType.Small, hp, 0f);
            inv.SmallMedkits = 0;
            Assert.AreEqual(0f, HealingRules.TryComplete(ref heal, ref inv, ref hp, 3f));
            Assert.AreEqual(50f, hp.Health);
        }

        [Test]
        public void MedkitTableMatchesSpec()
        {
            Assert.AreEqual(25f, Medkits.HealAmount(MedkitType.Small));
            Assert.AreEqual(2f, Medkits.UseSeconds(MedkitType.Small));
            Assert.AreEqual(50f, Medkits.HealAmount(MedkitType.Standard));
            Assert.AreEqual(4f, Medkits.UseSeconds(MedkitType.Standard));
            Assert.AreEqual(75f, Medkits.HealAmount(MedkitType.Advanced));
            Assert.AreEqual(6f, Medkits.UseSeconds(MedkitType.Advanced));
        }
    }
}
