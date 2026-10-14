using UnityEngine;
using WarOfNyx.Core.Weather;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Colour script for each <see cref="TimeOfDay"/> (sun / moon, ambient, fog, sky). Dusk is the
    /// original WAR OF NYX look. Night is moonlit with a neon horizon and stays bright enough to play:
    /// ambient never drops below what makes a character readable at 60 m.
    /// </summary>
    public struct TimeOfDayLighting
    {
        public float Elevation;
        public float Yaw;
        public Color SunColor;
        public float SunIntensity;
        /// <summary>Colour of the sun / moon disc and halo in the sky.</summary>
        public Color Disc;
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public Color Fog;
        public Color SkyTop;
        public Color SkyHorizon;
        public Color SkyBottom;
        public Color Glow;
        public Color Glow2;
        public float GlowHeight;
        public Color CloudLit;
        public Color CloudShade;
        /// <summary>Stars, 0..1.</summary>
        public float Night;

        public static TimeOfDayLighting For(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Dawn:
                    return new TimeOfDayLighting
                    {
                        Elevation = 16f, Yaw = 75f, SunColor = new Color(1f, 0.8f, 0.72f), SunIntensity = 1.1f, Disc = new Color(1f, 0.75f, 0.6f),
                        AmbientSky = new Color(0.56f, 0.56f, 0.76f), AmbientEquator = new Color(0.52f, 0.46f, 0.56f), AmbientGround = new Color(0.2f, 0.17f, 0.22f),
                        Fog = new Color(0.68f, 0.6f, 0.72f),
                        SkyTop = new Color(0.25f, 0.36f, 0.64f), SkyHorizon = new Color(0.96f, 0.72f, 0.72f), SkyBottom = new Color(0.22f, 0.2f, 0.32f),
                        Glow = new Color(1f, 0.62f, 0.45f), Glow2 = new Color(0.62f, 0.76f, 1f), GlowHeight = 0.05f,
                        CloudLit = new Color(1f, 0.84f, 0.82f), CloudShade = new Color(0.52f, 0.46f, 0.62f), Night = 0f,
                    };
                case TimeOfDay.Day:
                    return new TimeOfDayLighting
                    {
                        Elevation = 52f, Yaw = -35f, SunColor = new Color(1f, 0.96f, 0.9f), SunIntensity = 1.2f, Disc = new Color(1f, 0.97f, 0.88f),
                        AmbientSky = new Color(0.56f, 0.68f, 0.9f), AmbientEquator = new Color(0.46f, 0.53f, 0.6f), AmbientGround = new Color(0.22f, 0.22f, 0.24f),
                        Fog = new Color(0.64f, 0.74f, 0.9f),
                        SkyTop = new Color(0.17f, 0.4f, 0.86f), SkyHorizon = new Color(0.64f, 0.82f, 0.98f), SkyBottom = new Color(0.32f, 0.38f, 0.52f),
                        Glow = new Color(0.92f, 0.96f, 1f), Glow2 = new Color(0.72f, 0.9f, 1f), GlowHeight = 0.02f,
                        CloudLit = new Color(1f, 1f, 1f), CloudShade = new Color(0.6f, 0.66f, 0.8f), Night = 0f,
                    };
                case TimeOfDay.Night:
                    return new TimeOfDayLighting
                    {
                        Elevation = 40f, Yaw = 140f, SunColor = new Color(0.62f, 0.7f, 1f), SunIntensity = 0.6f, Disc = new Color(0.82f, 0.86f, 1f),
                        AmbientSky = new Color(0.3f, 0.32f, 0.54f), AmbientEquator = new Color(0.22f, 0.24f, 0.4f), AmbientGround = new Color(0.09f, 0.09f, 0.15f),
                        Fog = new Color(0.1f, 0.12f, 0.25f),
                        SkyTop = new Color(0.02f, 0.03f, 0.1f), SkyHorizon = new Color(0.12f, 0.14f, 0.32f), SkyBottom = new Color(0.02f, 0.02f, 0.06f),
                        Glow = new Color(0.55f, 0.28f, 0.95f), Glow2 = new Color(0.2f, 0.55f, 0.95f), GlowHeight = 0.03f,
                        CloudLit = new Color(0.36f, 0.4f, 0.58f), CloudShade = new Color(0.1f, 0.1f, 0.2f), Night = 1f,
                    };
                default: // Dusk: the original look, with a lower sun for longer shadows.
                    return new TimeOfDayLighting
                    {
                        Elevation = 24f, Yaw = -35f, SunColor = new Color(1f, 0.8f, 0.66f), SunIntensity = 1.25f, Disc = new Color(1f, 0.62f, 0.5f),
                        AmbientSky = new Color(0.5f, 0.45f, 0.72f), AmbientEquator = new Color(0.32f, 0.36f, 0.5f), AmbientGround = new Color(0.16f, 0.13f, 0.22f),
                        Fog = new Color(0.36f, 0.27f, 0.52f),
                        SkyTop = new Color(0.08f, 0.06f, 0.22f), SkyHorizon = new Color(0.45f, 0.3f, 0.66f), SkyBottom = new Color(0.06f, 0.05f, 0.14f),
                        Glow = new Color(1f, 0.42f, 0.75f), Glow2 = new Color(0.25f, 0.85f, 1f), GlowHeight = 0.04f,
                        CloudLit = new Color(1f, 0.7f, 0.76f), CloudShade = new Color(0.34f, 0.25f, 0.5f), Night = 0f,
                    };
            }
        }
    }
}
