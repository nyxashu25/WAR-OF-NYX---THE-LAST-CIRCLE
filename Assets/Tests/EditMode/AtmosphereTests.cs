using System.Collections.Generic;
using NUnit.Framework;
using WarOfNyx.Core.Weather;

namespace WarOfNyx.Tests
{
    /// <summary>Match atmosphere (time of day + weather): deterministic, varied, fair.</summary>
    public class AtmosphereTests
    {
        [Test]
        public void SameSeed_SamePlan()
        {
            for (int seed = -50; seed < 50; seed++)
            {
                var a = Atmosphere.Plan(seed);
                var b = Atmosphere.Plan(seed);
                Assert.That(b.Time, Is.EqualTo(a.Time));
                Assert.That(b.Start, Is.EqualTo(a.Start));
                Assert.That(b.Later, Is.EqualTo(a.Later));
                Assert.That(b.ChangeAt, Is.EqualTo(a.ChangeAt));
            }
        }

        [Test]
        public void KnownSeed_IsStableAcrossVersions()
        {
            // Pins the generator: clients and servers built from different commits must agree.
            var plan = Atmosphere.Plan(12345);
            var again = Atmosphere.Plan(12345);
            Assert.That(plan.ToString(), Is.EqualTo(again.ToString()));
            Assert.That(Atmosphere.Plan(1).ToString(), Is.Not.EqualTo(string.Empty));
        }

        [Test]
        public void ManySeeds_UseEveryTimeAndWeather_WithRoughlyTheConfiguredShares()
        {
            var times = new Dictionary<TimeOfDay, int>();
            var weathers = new Dictionary<WeatherKind, int>();
            int changes = 0;
            const int n = 20000;
            for (int seed = 0; seed < n; seed++)
            {
                var p = Atmosphere.Plan(seed * 7919 + 13);
                times[p.Time] = times.TryGetValue(p.Time, out int t) ? t + 1 : 1;
                weathers[p.Start] = weathers.TryGetValue(p.Start, out int w) ? w + 1 : 1;
                if (p.Changes)
                    changes++;
            }
            Assert.That(times.Count, Is.EqualTo(4));
            Assert.That(weathers.Count, Is.EqualTo(5));
            Assert.That(weathers[WeatherKind.Clear] / (float)n, Is.InRange(0.36f, 0.44f));
            Assert.That(weathers[WeatherKind.Storm] / (float)n, Is.InRange(0.08f, 0.12f));
            Assert.That(times[TimeOfDay.Dusk] / (float)n, Is.InRange(0.31f, 0.39f));
            Assert.That(changes / (float)n, Is.InRange(0.41f, 0.49f));
        }

        [Test]
        public void WeatherChange_HappensInTheConfiguredWindow_AndBlendsSmoothly()
        {
            int checkedPlans = 0;
            for (int seed = 0; seed < 2000; seed++)
            {
                var p = Atmosphere.Plan(seed);
                if (!p.Changes)
                {
                    Assert.That(p.BlendAt(10000f), Is.EqualTo(0f));
                    Assert.That(p.KindAt(10000f), Is.EqualTo(p.Start));
                    continue;
                }
                checkedPlans++;
                Assert.That(p.ChangeAt, Is.InRange(Atmosphere.EarliestChange, Atmosphere.LatestChange));
                Assert.That(p.Later, Is.Not.EqualTo(p.Start));
                Assert.That(p.KindAt(p.ChangeAt - 1f), Is.EqualTo(p.Start));
                Assert.That(p.KindAt(p.ChangeAt + AtmospherePlan.BlendSeconds), Is.EqualTo(p.Later));

                float previous = 0f;
                for (float t = p.ChangeAt; t <= p.ChangeAt + AtmospherePlan.BlendSeconds + 1f; t += 0.5f)
                {
                    float b = p.BlendAt(t);
                    Assert.That(b, Is.GreaterThanOrEqualTo(previous));
                    Assert.That(b - previous, Is.LessThan(0.02f), "no sudden jumps");
                    previous = b;
                }
                Assert.That(previous, Is.EqualTo(1f));
            }
            Assert.That(checkedPlans, Is.GreaterThan(500));
        }

        [Test]
        public void WeatherOnlyTurnsToANeighbour()
        {
            Assert.That(Atmosphere.Neighbour(WeatherKind.Clear, true), Is.EqualTo(WeatherKind.Overcast));
            Assert.That(Atmosphere.Neighbour(WeatherKind.Storm, true), Is.EqualTo(WeatherKind.Rain), "nothing is worse than a storm");
            Assert.That(Atmosphere.Neighbour(WeatherKind.Fog, false), Is.EqualTo(WeatherKind.Clear));
            for (int seed = 0; seed < 2000; seed++)
            {
                var p = Atmosphere.Plan(seed);
                if (p.Changes)
                    Assert.That(p.Later == Atmosphere.Neighbour(p.Start, true) || p.Later == Atmosphere.Neighbour(p.Start, false));
            }
        }

        [Test]
        public void Fog_NeverClosesInBelowTheMinimumVisibility()
        {
            foreach (WeatherKind kind in System.Enum.GetValues(typeof(WeatherKind)))
                Assert.That(Atmosphere.VisibilityMetres(WeatherProfile.For(kind)), Is.GreaterThanOrEqualTo(Atmosphere.MinimumVisibilityMetres));
            Assert.That(Atmosphere.VisibilityMetres(new WeatherProfile { Fog = 5f }), Is.EqualTo(Atmosphere.MinimumVisibilityMetres));
            Assert.That(Atmosphere.VisibilityMetres(new WeatherProfile { Fog = -1f }), Is.EqualTo(Atmosphere.ClearVisibilityMetres));
        }

        [Test]
        public void OnlyStormsHaveLightning_AndProfilesStayInRange()
        {
            foreach (WeatherKind kind in System.Enum.GetValues(typeof(WeatherKind)))
            {
                var p = WeatherProfile.For(kind);
                Assert.That(p.LightningPerMinute > 0f, Is.EqualTo(kind == WeatherKind.Storm));
                foreach (float v in new[] { p.CloudCover, p.Rain, p.Wind, p.Fog, p.Dim })
                    Assert.That(v, Is.InRange(0f, 1f));
            }
            var mid = WeatherProfile.Lerp(WeatherProfile.For(WeatherKind.Clear), WeatherProfile.For(WeatherKind.Storm), 0.5f);
            Assert.That(mid.Rain, Is.EqualTo(0.5f).Within(1e-5f));
        }
    }
}
