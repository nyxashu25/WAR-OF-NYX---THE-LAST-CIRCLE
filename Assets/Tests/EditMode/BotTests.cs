using System;
using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Bots;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Tests
{
    /// <summary>Ghost Protocol (spec 38): bot senses, memory, decisions, aim and looting.</summary>
    public class BotPerceptionTests
    {
        private static readonly BotDifficultyProfile Nightmare = BotDifficultyCatalog.Get(BotDifficulty.Nightmare);

        private static VisibilityQuery Q(float tx, float tz, bool los = true, float yaw = 0f, bool crouch = false, bool moving = true) =>
            new VisibilityQuery { ObserverYaw = yaw, TargetX = tx, TargetZ = tz, LineOfSightClear = los, TargetCrouched = crouch, TargetMoving = moving };

        [Test]
        public void NeverSeesThroughWalls()
        {
            Assert.IsTrue(BotPerception.CanSee(Nightmare, Q(0f, 20f)));
            Assert.IsFalse(BotPerception.CanSee(Nightmare, Q(0f, 20f, los: false)), "solid geometry blocks vision");
            Assert.IsFalse(BotPerception.CanSee(Nightmare, Q(0f, 2f, los: false)), "even point blank");
        }

        [Test]
        public void FieldOfViewAndRangeLimitVision()
        {
            Assert.IsFalse(BotPerception.CanSee(Nightmare, Q(0f, -20f)), "behind the bot");
            Assert.IsTrue(BotPerception.CanSee(Nightmare, Q(0f, -3f)), "someone right behind is felt (proximity)");
            float range = Nightmare.VisionRangeMeters;
            Assert.IsFalse(BotPerception.CanSee(Nightmare, Q(0f, range + 1f)));
            Assert.IsFalse(BotPerception.CanSee(Nightmare, Q(0f, range * 0.8f, crouch: true, moving: false)), "a still, crouching target is harder to spot");
        }

        [Test]
        public void HearingGivesAnApproximateSourceOnly()
        {
            var shot = new SoundEvent { Kind = SoundKind.Gunshot, X = 50f, Z = 0f };
            Assert.IsTrue(BotPerception.CanHear(Nightmare, shot, 0f, 0f));
            Assert.IsFalse(BotPerception.CanHear(Nightmare, new SoundEvent { Kind = SoundKind.Footsteps, X = 50f }, 0f, 0f));

            var random = new Random(4);
            bool anyOff = false;
            for (int i = 0; i < 20; i++)
            {
                BotPerception.EstimateSource(shot, 0f, 0f, random, out float x, out float z);
                float error = MathF.Sqrt((x - 50f) * (x - 50f) + z * z);
                Assert.LessOrEqual(error, 50f * 0.25f + 1e-3f);
                anyOff |= error > 0.5f;
            }
            Assert.IsTrue(anyOff, "a gunshot is investigated, not pinpointed");
        }

        [Test]
        public void MemoryDecaysAndPrefersVisibleTargets()
        {
            var m = new BotMemory();
            m.See(1, 10f, 0f, 0f);
            m.Hear(2, 5f, 5f, 0f);
            Assert.AreEqual(1, m.Best(0f, 0f).Id, "seen beats heard");

            m.LostSight(1);
            m.Decay(5f);
            Assert.IsTrue(m.TryGet(1, out var c1));
            Assert.AreEqual(1f - 0.4f, c1.Confidence, 1e-4f, "confidence decays once out of sight");
            Assert.AreEqual(10f, c1.X, "the last KNOWN position, not a live one");

            m.Decay(60f);
            Assert.AreEqual(0, m.Count, "stale contacts are forgotten");
        }

        [Test]
        public void NewSightingRestartsTheReactionClock()
        {
            var m = new BotMemory();
            m.See(7, 0f, 10f, 1f);
            m.See(7, 0f, 11f, 1.5f);
            Assert.IsTrue(m.TryGet(7, out var c));
            Assert.AreEqual(1f, c.SpottedAt);
            Assert.AreEqual(2f, c.VelocityZ, 1e-4f);
            m.LostSight(7);
            m.See(7, 0f, 12f, 4f);
            Assert.AreEqual(4f, c.SpottedAt);
        }
    }

    public class BotBrainTests
    {
        private static BotSituation Calm() => new BotSituation
        {
            Alive = true, SecondsSinceSpawn = 30f, Health = 100f, MaxHealth = 100f, Medkits = 2,
            HasRangedWithAmmo = true, RangedEngagementRange = 45f, HasMelee = true, MeleeReach = 2f,
            ZoneActive = true, DistanceToEdge = -40f, SecondsToShrinkEnd = 50f, InsideNextCircle = true,
        };

        private static BotState Decide(in BotSituation s, BotPersonality p = null, BotDifficulty d = BotDifficulty.Nightmare, int seed = 1)
        {
            var brain = new BotBrain(p ?? BotPersonality.Hunter, WithoutMistakes(d), seed);
            return brain.Decide(s);
        }

        /// <summary>Same profile as the catalog but with no random mistakes, so tests are exact.</summary>
        private static BotDifficultyProfile WithoutMistakes(BotDifficulty d)
        {
            var p = BotDifficultyCatalog.Get(d);
            return new BotDifficultyProfile(p.Difficulty, p.ReactionTimeMinSeconds, p.ReactionTimeMaxSeconds, p.BaseAimErrorDegrees,
                p.AimErrorPer10Meters, p.RecoilControl, p.CoverUsage, p.FlankingTendency, p.DecisionIntervalSeconds,
                p.VisionRangeMeters, p.FieldOfViewDegrees, p.HearingMultiplier, p.HealThresholdHealth, p.ZoneLeadTimeSeconds,
                0f, p.LootSelection);
        }

        [Test]
        public void CalmBotsExploreOrPatrolInsteadOfChasingAnyone()
        {
            var state = Decide(Calm());
            Assert.That(state, Is.EqualTo(BotState.Exploring).Or.EqualTo(BotState.Patrolling));
        }

        [Test]
        public void TopLayerOverridesEverything()
        {
            var s = Calm();
            s.Alive = false;
            Assert.AreEqual(BotState.Eliminated, Decide(s));
            s = Calm();
            s.SecondsSinceSpawn = 0.2f;
            Assert.AreEqual(BotState.Spawning, Decide(s));
            s = Calm();
            s.Outside = true;
            s.DistanceToEdge = 12f;
            s.EnemyVisible = true;
            s.EnemyDistance = 20f;
            Assert.AreEqual(BotState.EscapingCircle, Decide(s), "outside: get back first (guns don't work there)");
        }

        [Test]
        public void VisibleEnemyInRangeIsEngaged()
        {
            var s = Calm();
            s.EnemyVisible = true;
            s.EnemyDistance = 20f;
            Assert.AreEqual(BotState.EngagingEnemy, Decide(s));
        }

        [Test]
        public void WoundedBotsRetreatOrHeal()
        {
            var s = Calm();
            s.Health = 20f;
            Assert.AreEqual(BotState.Healing, Decide(s, BotPersonality.Survivor));
            s.EnemyVisible = true;
            s.EnemyDistance = 15f;
            s.UnderFire = true;
            Assert.That(Decide(s, BotPersonality.Survivor), Is.EqualTo(BotState.Retreating).Or.EqualTo(BotState.SeekingCover));
        }

        [Test]
        public void GunfireIsInvestigated()
        {
            var s = Calm();
            s.EnemyRemembered = true;
            s.RememberedFromSound = true;
            s.RememberedConfidence = 0.45f;
            Assert.AreEqual(BotState.InvestigatingSound, Decide(s, BotPersonality.Hunter));
        }

        [Test]
        public void ProactiveBotsMoveBeforeTheShrinkEnds()
        {
            var s = Calm();
            s.InsideNextCircle = false;
            s.SecondsToShrinkEnd = 20f;
            Assert.AreEqual(BotState.SeekingSafeZone, Decide(s, BotPersonality.Strategist));
        }

        [Test]
        public void CampingWarningsMakeBotsMove()
        {
            var s = Calm();
            s.InactiveSeconds = 21f;
            Assert.AreEqual(BotState.AvoidingCampingPenalty, Decide(s, BotPersonality.Phantom));
            s.InactiveSeconds = 5f;
            s.Exposed = true;
            Assert.AreEqual(BotState.AvoidingCampingPenalty, Decide(s, BotPersonality.Phantom));
        }

        [Test]
        public void LootIsTakenWhenSafe()
        {
            var s = Calm();
            s.LootNearby = true;
            s.LootValue = 40f;
            Assert.AreEqual(BotState.Looting, Decide(s, BotPersonality.Scavenger));
            s.EnemyVisible = true;
            s.EnemyDistance = 30f;
            Assert.AreNotEqual(BotState.Looting, Decide(s, BotPersonality.Scavenger), "not while an enemy is in sight");
        }

        [Test]
        public void FinalCircleChangesPriorities()
        {
            var s = Calm();
            s.FinalStage = true;
            Assert.AreEqual(BotState.FinalCircleCombat, Decide(s));
        }

        [Test]
        public void EveryStateIsReachable()
        {
            // Spec 38.4: all 19 states exist and can be produced by real situations.
            var reached = new System.Collections.Generic.HashSet<BotState>();
            var random = new Random(9);
            foreach (var p in BotPersonality.All)
            {
                for (int i = 0; i < 4000; i++)
                {
                    var s = new BotSituation
                    {
                        Alive = random.NextDouble() > 0.02, SecondsSinceSpawn = (float)random.NextDouble() * 60f,
                        Health = (float)random.NextDouble() * 100f, MaxHealth = 100f, Medkits = random.Next(3), Healing = random.NextDouble() < 0.1,
                        UnderFire = random.NextDouble() < 0.3, HasRangedWithAmmo = random.NextDouble() < 0.8, RangedEngagementRange = 40f,
                        HasMelee = true, MeleeReach = 2f, ZoneActive = true, Outside = random.NextDouble() < 0.1,
                        DistanceToEdge = (float)random.NextDouble() * 40f - 20f, SecondsToShrinkEnd = (float)random.NextDouble() * 60f,
                        InsideNextCircle = random.NextDouble() < 0.6, FinalStage = random.NextDouble() < 0.1,
                        InactiveSeconds = (float)random.NextDouble() * 30f, Exposed = random.NextDouble() < 0.05,
                        EnemyVisible = random.NextDouble() < 0.35, EnemyDistance = (float)random.NextDouble() * 90f,
                        EnemyRemembered = random.NextDouble() < 0.5, RememberedConfidence = (float)random.NextDouble(),
                        RememberedFromSound = random.NextDouble() < 0.5, LootNearby = random.NextDouble() < 0.2,
                        LootValue = (float)random.NextDouble() * 50f, CoverNearby = random.NextDouble() < 0.6,
                    };
                    reached.Add(new BotBrain(p, WithoutMistakes(BotDifficulty.Elite), i).Decide(s));
                }
            }
            var missing = Enum.GetValues(typeof(BotState)).Cast<BotState>().Where(st => !reached.Contains(st)).ToList();
            CollectionAssert.IsEmpty(missing, "unreachable: " + string.Join(", ", missing));
        }

        [Test]
        public void HysteresisAvoidsFlickering()
        {
            var brain = new BotBrain(BotPersonality.Hunter, WithoutMistakes(BotDifficulty.Elite), 3);
            var s = Calm();
            s.EnemyVisible = true;
            s.EnemyDistance = 20f;
            Assert.AreEqual(BotState.EngagingEnemy, brain.Decide(s));
            s.LootNearby = true;
            s.LootValue = 10f;
            Assert.AreEqual(BotState.EngagingEnemy, brain.Decide(s), "a slightly different situation keeps the current plan");
        }

        [Test]
        public void PersonalitiesDisagree()
        {
            var s = Calm();
            s.EnemyVisible = true;
            s.EnemyDistance = 70f; // seen, but beyond rifle range
            s.CoverNearby = true;
            var hunter = Decide(s, BotPersonality.Hunter);
            Assert.AreEqual(BotState.StalkingTarget, hunter);
            Assert.AreEqual(6, BotPersonality.All.Select(p => p.Type).Distinct().Count());
        }
    }

    public class BotAimTests
    {
        [Test]
        public void ReactionTimeIsHumanAndAimIsNeverPerfect()
        {
            foreach (BotDifficulty d in Enum.GetValues(typeof(BotDifficulty)))
            {
                var profile = BotDifficultyCatalog.Get(d);
                var aim = new BotAim(profile, 5);
                aim.Reset(0f, 0f);
                aim.Track(1, 30f, 0f, 20f, now: 10f, dt: 1f / 60f);
                Assert.GreaterOrEqual(aim.ReadyAt - 10f, BotDifficultyProfile.HumanReactionFloorSeconds - 1e-4f, d.ToString());
                Assert.IsFalse(aim.CanShoot(30f, 0f, 20f, 10.05f), "no instant shots");

                float maxError = 0f;
                for (int i = 0; i < 600; i++)
                {
                    float now = 10f + i / 60f;
                    aim.Track(1, 30f, 0f, 20f, now, 1f / 60f);
                    maxError = Math.Max(maxError, Math.Abs(aim.Yaw - 30f));
                }
                Assert.Greater(maxError, 0.2f, $"{d}: no machine-perfect tracking");
            }
        }

        [Test]
        public void TurningSpeedIsLimited()
        {
            var aim = new BotAim(BotDifficultyCatalog.Get(BotDifficulty.Nightmare), 1);
            aim.Reset(0f, 0f);
            aim.Track(1, 180f, 0f, 10f, 0f, 1f / 60f);
            Assert.Less(Math.Abs(aim.Yaw), 20f, "no snapping to targets behind");
        }
    }

    public class BotLootTests
    {
        private static readonly Func<short, WeaponStats> W = WeaponCatalog.Get;
        private static short Id(string id) => WeaponCatalog.NetworkIdOf(id);

        private static WeaponState[] Slots(string primary, int magazine = 5)
        {
            var slots = new[] { WeaponState.Create(Id(primary)), WeaponState.Empty, WeaponState.Create(Id(WeaponCatalog.Katana)) };
            slots[0].Magazine = magazine;
            return slots;
        }

        [Test]
        public void WoundedBotsPreferMedkits()
        {
            var c = new LootContainer();
            c.Put(0, LootItem.Weapon(WeaponState.Create(Id(WeaponCatalog.ShadowSmg))));
            c.Put(1, LootItem.MedkitStack(MedkitType.Standard, 1));
            var inv = new PlayerInventory { RifleAmmo = 60 };
            Assert.AreEqual(1, BotLootPlanner.Choose(c, Slots(WeaponCatalog.Nx7AssaultRifle, 30), inv, 0.25f, LootSelectionStrategy.Optimized, new Random(1), W));
        }

        [Test]
        public void AmmoStarvedShotgunIsReplacedByARifle()
        {
            var slots = Slots(WeaponCatalog.CrimsonShotgun, magazine: 0);
            slots[1] = WeaponState.Create(Id(WeaponCatalog.NeonPistol)); // no empty slot left
            var inv = new PlayerInventory { ShellAmmo = 0, LightAmmo = 40 };
            var rifle = LootItem.Weapon(WeaponState.Create(Id(WeaponCatalog.Nx7AssaultRifle)));
            Assert.Greater(BotLootPlanner.Value(rifle, slots, inv, 1f, W), 0f, "spec 38.13 example");
        }

        [Test]
        public void UselessItemsAreIgnored()
        {
            var c = new LootContainer();
            c.Put(0, LootItem.ArmorPiece(1));
            var inv = new PlayerInventory { ArmorLevel = 2, RifleAmmo = 200 };
            Assert.AreEqual(-1, BotLootPlanner.Choose(c, Slots(WeaponCatalog.Nx7AssaultRifle, 30), inv, 1f, LootSelectionStrategy.Optimized, new Random(1), W));
        }

        [Test]
        public void StrategiesDifferButAllPickUsefulItems()
        {
            var c = new LootContainer();
            c.Put(2, LootItem.AmmoStack(AmmoType.Rifle, 60));
            c.Put(5, LootItem.ArmorPiece(3));
            c.Put(7, LootItem.MedkitStack(MedkitType.Small, 2));
            var inv = new PlayerInventory { RifleAmmo = 10, ArmorLevel = 1 };
            var slots = Slots(WeaponCatalog.Nx7AssaultRifle, 30);
            foreach (LootSelectionStrategy strategy in Enum.GetValues(typeof(LootSelectionStrategy)))
            {
                int pick = BotLootPlanner.Choose(c, slots, inv, 0.9f, strategy, new Random(2), W);
                Assert.That(pick, Is.EqualTo(2).Or.EqualTo(5).Or.EqualTo(7), strategy.ToString());
            }
            Assert.AreEqual(2, BotLootPlanner.Choose(c, slots, inv, 0.9f, LootSelectionStrategy.Basic, new Random(2), W), "basic: first useful");
        }

        [Test]
        public void BotNamesAreDistinctAndClearlyBots()
        {
            var names = BotNames.Generate(23, new Random(3));
            Assert.AreEqual(23, names.Distinct().Count());
            Assert.IsTrue(names.All(n => n.Contains("_")));
        }
    }
}

namespace WarOfNyx.Tests
{
    using System;
    using NUnit.Framework;
    using WarOfNyx.Core.Combat;
    using WarOfNyx.Core.Items;

    /// <summary>The shared loot claim used by the online server and the offline match.</summary>
    public class LootClaimTests
    {
        private static readonly Func<short, WeaponStats> W = WeaponCatalog.Get;
        private static short Id(string id) => WeaponCatalog.NetworkIdOf(id);

        [Test]
        public void TakeAllFillsButNeverSwapsWeaponsAway()
        {
            var container = new LootContainer();
            container.Put(0, LootItem.Weapon(WeaponState.Create(Id(WeaponCatalog.PhantomMarksman))));
            container.Put(1, LootItem.Weapon(WeaponState.Create(Id(WeaponCatalog.CrimsonShotgun))));
            container.Put(2, LootItem.AmmoStack(AmmoType.Rifle, 30));
            var slots = new[] { WeaponState.Create(Id(WeaponCatalog.Nx7AssaultRifle)), WeaponState.Empty, WeaponState.Create(Id(WeaponCatalog.Katana)) };
            int active = 0;
            var inv = new PlayerInventory();

            var result = LootClaims.Take(container, slots, ref active, ref inv, -1, W);

            Assert.AreEqual(2, result.Taken, "one weapon into the empty slot + the ammo");
            Assert.IsTrue(result.TookCombatItem);
            Assert.AreEqual(Id(WeaponCatalog.Nx7AssaultRifle), slots[0].WeaponId, "the primary was not swapped away");
            Assert.IsFalse(slots[1].IsEmptySlot);
            Assert.AreEqual(1, container.Count, "the second weapon stays in the capsule");
            Assert.AreEqual(30, inv.RifleAmmo);
        }

        [Test]
        public void SingleTakeMovesTheItemExactlyOnce()
        {
            var container = new LootContainer();
            container.Put(4, LootItem.MedkitStack(MedkitType.Small, 1));
            var slots = new[] { WeaponState.Empty, WeaponState.Empty, WeaponState.Create(Id(WeaponCatalog.Dagger)) };
            int active = 0;
            var inv = new PlayerInventory();
            Assert.AreEqual(1, LootClaims.Take(container, slots, ref active, ref inv, 4, W).Taken);
            Assert.AreEqual(2, active, "with no gun the melee slot becomes active");
            Assert.AreEqual("Already taken.", LootClaims.Take(container, slots, ref active, ref inv, 4, W).Message);
            Assert.AreEqual(1, inv.SmallMedkits);
        }
    }
}
