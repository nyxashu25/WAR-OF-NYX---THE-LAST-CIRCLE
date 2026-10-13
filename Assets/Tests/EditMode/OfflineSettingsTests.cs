using NUnit.Framework;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Tests
{
    public class OfflineSettingsTests
    {
        [Test]
        public void AllowedBotCountsMatchSpec()
        {
            CollectionAssert.AreEqual(new[] { 5, 11, 15, 23 }, OfflineMatchSettings.AllowedBotCounts);
            Assert.AreEqual(11, OfflineMatchSettings.SpecDefaultBotCount);
        }

        [Test]
        public void EveryBotCountsAsACompetitor()
        {
            Assert.AreEqual(12, new OfflineMatchSettings(11, BotDifficulty.Warrior).TotalCompetitors);
            Assert.AreEqual(24, new OfflineMatchSettings(23, BotDifficulty.Nightmare).TotalCompetitors);
        }

        [TestCase(5, true)]
        [TestCase(11, true)]
        [TestCase(23, true)]
        [TestCase(0, false)]
        [TestCase(12, false)]
        [TestCase(24, false)]
        public void Validation(int bots, bool valid)
        {
            Assert.AreEqual(valid, new OfflineMatchSettings(bots, BotDifficulty.Elite).IsValid);
        }

        [Test]
        public void ClampKeepsRequestWhenDeviceCanRunIt()
        {
            var s = new OfflineMatchSettings(23, BotDifficulty.Nightmare).ClampToCapacity(23);
            Assert.AreEqual(23, s.BotCount);
            Assert.AreEqual(BotDifficulty.Nightmare, s.Difficulty);
        }

        [TestCase(15, 15)]
        [TestCase(11, 11)]
        [TestCase(14, 11)]
        [TestCase(5, 5)]
        [TestCase(0, 5)] // never below the smallest offered match
        public void ClampFallsBackToLargestSupportedCount(int capacity, int expected)
        {
            var s = new OfflineMatchSettings(23, BotDifficulty.Nightmare).ClampToCapacity(capacity);
            Assert.AreEqual(expected, s.BotCount);
            Assert.AreEqual(BotDifficulty.Nightmare, s.Difficulty, "difficulty is never downgraded by capacity");
        }

        [Test]
        public void ClampNeverRaisesTheRequest()
        {
            Assert.AreEqual(5, new OfflineMatchSettings(5, BotDifficulty.Rookie).ClampToCapacity(23).BotCount);
        }

        [Test]
        public void ClampRepairsInvalidRequestToSpecDefault()
        {
            Assert.AreEqual(11, new OfflineMatchSettings(99, BotDifficulty.Rookie).ClampToCapacity(23).BotCount);
        }

        [Test]
        public void CapacityPolicyTiers()
        {
            Assert.AreEqual(23, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(false, 2048, 2)), "desktop/editor");
            Assert.AreEqual(5, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(true, 2048, 8)));
            Assert.AreEqual(11, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(true, 3500, 8)));
            Assert.AreEqual(15, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(true, 5000, 8)));
            Assert.AreEqual(15, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(true, 8192, 6)));
            Assert.AreEqual(23, OfflineCapacityPolicy.MaxBotsFor(new DeviceProfile(true, 8192, 8)));
        }
    }

    public class BotDifficultyTests
    {
        [Test]
        public void CatalogCoversEveryDifficulty()
        {
            foreach (var d in BotDifficultyCatalog.All)
                Assert.AreEqual(d, BotDifficultyCatalog.Get(d).Difficulty);
        }

        [Test]
        public void NightmareIsSmartButNotSuperhuman()
        {
            var n = BotDifficultyCatalog.Nightmare;
            Assert.GreaterOrEqual(n.ReactionTimeMinSeconds, BotDifficultyProfile.HumanReactionFloorSeconds);
            Assert.GreaterOrEqual(n.BaseAimErrorDegrees, BotDifficultyProfile.MinimumAimErrorDegrees);
            Assert.Less(n.RecoilControl, 1f);
            Assert.LessOrEqual(n.VisionRangeMeters, BotDifficultyProfile.MaxVisionRangeMeters);
            Assert.AreEqual(LootSelectionStrategy.Optimized, n.LootSelection);
        }

        [Test]
        public void HigherDifficultyIsNeverWorse()
        {
            var all = BotDifficultyCatalog.All;
            for (int i = 1; i < all.Length; i++)
            {
                var easier = BotDifficultyCatalog.Get(all[i - 1]);
                var harder = BotDifficultyCatalog.Get(all[i]);
                string pair = $"{easier.Difficulty} -> {harder.Difficulty}";

                Assert.LessOrEqual(harder.ReactionTimeMinSeconds, easier.ReactionTimeMinSeconds, pair);
                Assert.LessOrEqual(harder.BaseAimErrorDegrees, easier.BaseAimErrorDegrees, pair);
                Assert.GreaterOrEqual(harder.CoverUsage, easier.CoverUsage, pair);
                Assert.GreaterOrEqual(harder.FlankingTendency, easier.FlankingTendency, pair);
                Assert.GreaterOrEqual(harder.ZoneLeadTimeSeconds, easier.ZoneLeadTimeSeconds, pair);
                Assert.LessOrEqual(harder.MistakeChance, easier.MistakeChance, pair);
                Assert.LessOrEqual(harder.DecisionIntervalSeconds, easier.DecisionIntervalSeconds, pair);
            }
        }

        [Test]
        public void AimErrorGrowsWithDistance()
        {
            var p = BotDifficultyCatalog.Elite;
            Assert.Greater(p.AimErrorAtDistance(80f), p.AimErrorAtDistance(10f));
            Assert.AreEqual(p.BaseAimErrorDegrees, p.AimErrorAtDistance(-5f), 1e-5f);
        }

        [Test]
        public void ProfilesBelowTheHumanFloorAreRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BotDifficultyProfile(
                BotDifficulty.Nightmare, 0.05f, 0.1f, 2f, 0.5f, 0.5f, 1f, 1f, 0.2f, 100f, 120f, 1f, 50f, 30f, 0f,
                LootSelectionStrategy.Optimized));
        }

        [Test]
        public void PerfectAimIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BotDifficultyProfile(
                BotDifficulty.Nightmare, 0.25f, 0.3f, 0f, 0f, 0.5f, 1f, 1f, 0.2f, 100f, 120f, 1f, 50f, 30f, 0f,
                LootSelectionStrategy.Optimized));
        }
    }
}
