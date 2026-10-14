namespace WarOfNyx.Core.Weather
{
    /// <summary>Lighting mood of a match. Every one stays readable: night is moonlit, not black.</summary>
    public enum TimeOfDay : byte
    {
        Dawn = 0,
        Day = 1,
        Dusk = 2,
        Night = 3,
    }

    public enum WeatherKind : byte
    {
        Clear = 0,
        Overcast = 1,
        Rain = 2,
        Storm = 3,
        Fog = 4,
    }

    /// <summary>
    /// The numbers a weather state drives (all 0..1 except where noted). Purely visual and audio:
    /// weather never changes movement, damage, the circle or loot, so it can't decide a fight.
    /// </summary>
    public struct WeatherProfile
    {
        /// <summary>Share of the sky covered by clouds.</summary>
        public float CloudCover;
        /// <summary>Rain particles and rain sound.</summary>
        public float Rain;
        /// <summary>Grass, leaves and rain slant.</summary>
        public float Wind;
        /// <summary>0 = light haze, 1 = thickest allowed fog (see <see cref="Atmosphere.VisibilityMetres"/>).</summary>
        public float Fog;
        /// <summary>How much the sun / moon is dimmed by clouds.</summary>
        public float Dim;
        /// <summary>Lightning flashes per minute (storms only).</summary>
        public float LightningPerMinute;

        public static WeatherProfile For(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Overcast:
                    return new WeatherProfile { CloudCover = 0.75f, Rain = 0f, Wind = 0.45f, Fog = 0.25f, Dim = 0.35f };
                case WeatherKind.Rain:
                    return new WeatherProfile { CloudCover = 0.9f, Rain = 0.65f, Wind = 0.6f, Fog = 0.4f, Dim = 0.5f };
                case WeatherKind.Storm:
                    return new WeatherProfile { CloudCover = 1f, Rain = 1f, Wind = 1f, Fog = 0.5f, Dim = 0.62f, LightningPerMinute = 5f };
                case WeatherKind.Fog:
                    return new WeatherProfile { CloudCover = 0.45f, Rain = 0f, Wind = 0.15f, Fog = 1f, Dim = 0.25f };
                default:
                    return new WeatherProfile { CloudCover = 0.25f, Rain = 0f, Wind = 0.3f, Fog = 0f, Dim = 0f };
            }
        }

        public static WeatherProfile Lerp(WeatherProfile a, WeatherProfile b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new WeatherProfile
            {
                CloudCover = a.CloudCover + (b.CloudCover - a.CloudCover) * t,
                Rain = a.Rain + (b.Rain - a.Rain) * t,
                Wind = a.Wind + (b.Wind - a.Wind) * t,
                Fog = a.Fog + (b.Fog - a.Fog) * t,
                Dim = a.Dim + (b.Dim - a.Dim) * t,
                LightningPerMinute = a.LightningPerMinute + (b.LightningPerMinute - a.LightningPerMinute) * t,
            };
        }
    }

    /// <summary>
    /// The atmosphere of one match: time of day plus a weather that may turn once, blending over
    /// <see cref="BlendSeconds"/>. Derived only from the match seed, so every client of a match (and
    /// an offline replay with the same seed) sees the same sky.
    /// </summary>
    public struct AtmospherePlan
    {
        public const float BlendSeconds = 45f;

        public TimeOfDay Time;
        public WeatherKind Start;
        public WeatherKind Later;
        /// <summary>Match seconds when the weather starts turning; <see cref="float.PositiveInfinity"/> = never.</summary>
        public float ChangeAt;

        public bool Changes => !float.IsInfinity(ChangeAt) && Later != Start;

        /// <summary>0 before the change, 1 once the new weather has fully arrived.</summary>
        public float BlendAt(float matchSeconds)
        {
            if (!Changes || matchSeconds <= ChangeAt)
                return 0f;
            float t = (matchSeconds - ChangeAt) / BlendSeconds;
            return t >= 1f ? 1f : t;
        }

        public WeatherKind KindAt(float matchSeconds) => BlendAt(matchSeconds) >= 0.5f ? Later : Start;

        public WeatherProfile ProfileAt(float matchSeconds) =>
            WeatherProfile.Lerp(WeatherProfile.For(Start), WeatherProfile.For(Later), BlendAt(matchSeconds));

        public override string ToString() =>
            Changes ? $"{Time}, {Start} → {Later} at {ChangeAt:0}s" : $"{Time}, {Start}";
    }

    public static class Atmosphere
    {
        /// <summary>Fog may never close in further than this (fairness: long guns stay useful).</summary>
        public const float MinimumVisibilityMetres = 250f;
        public const float ClearVisibilityMetres = 1000f;

        /// <summary>Distance at which the fog is fully opaque for a profile.</summary>
        public static float VisibilityMetres(WeatherProfile p)
        {
            float fog = p.Fog < 0f ? 0f : (p.Fog > 1f ? 1f : p.Fog);
            return ClearVisibilityMetres + (MinimumVisibilityMetres - ClearVisibilityMetres) * fog;
        }

        private static readonly int[] TimeWeights = { 20, 25, 35, 20 };          // Dawn, Day, Dusk, Night
        private static readonly int[] WeatherWeights = { 40, 20, 18, 10, 12 };   // Clear, Overcast, Rain, Storm, Fog

        /// <summary>Chance (percent) that the weather turns once during a match.</summary>
        public const int ChangePercent = 45;
        public const float EarliestChange = 150f;
        public const float LatestChange = 330f;

        /// <summary>Deterministic plan for a match seed (same result on every platform).</summary>
        public static AtmospherePlan Plan(int seed)
        {
            ulong state = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x5DEECE66DUL);
            var plan = new AtmospherePlan
            {
                Time = (TimeOfDay)Pick(ref state, TimeWeights),
                Start = (WeatherKind)Pick(ref state, WeatherWeights),
                ChangeAt = float.PositiveInfinity,
            };
            plan.Later = plan.Start;

            if (Next(ref state) % 100 < ChangePercent)
            {
                plan.Later = Neighbour(plan.Start, Next(ref state) % 2 == 0);
                plan.ChangeAt = EarliestChange + (float)(Next(ref state) % 1000) / 1000f * (LatestChange - EarliestChange);
            }
            return plan;
        }

        /// <summary>Weather turns gradually: Clear ↔ Overcast ↔ Rain ↔ Storm, Fog lifts to Clear or thickens to Overcast.</summary>
        public static WeatherKind Neighbour(WeatherKind kind, bool worse)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return worse ? WeatherKind.Overcast : WeatherKind.Fog;
                case WeatherKind.Overcast: return worse ? WeatherKind.Rain : WeatherKind.Clear;
                case WeatherKind.Rain: return worse ? WeatherKind.Storm : WeatherKind.Overcast;
                case WeatherKind.Storm: return WeatherKind.Rain;
                case WeatherKind.Fog: return worse ? WeatherKind.Overcast : WeatherKind.Clear;
                default: return WeatherKind.Clear;
            }
        }

        private static int Pick(ref ulong state, int[] weights)
        {
            int total = 0;
            foreach (int w in weights)
                total += w;
            int roll = (int)(Next(ref state) % (ulong)total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (roll < weights[i])
                    return i;
                roll -= weights[i];
            }
            return weights.Length - 1;
        }

        // SplitMix64: tiny, fast, identical on Mono, IL2CPP and .NET.
        private static ulong Next(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
