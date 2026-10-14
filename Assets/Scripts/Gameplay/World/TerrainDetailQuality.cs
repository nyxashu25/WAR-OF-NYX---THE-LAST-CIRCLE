using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Settings;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Grass and flower density / draw distance per graphics tier. Fair at every tier: grass is
    /// shorter than a crouched character (<see cref="VegetationMeshes.MaxGrassHeight"/>), so a lower
    /// tier only shows less decoration, never more of an opponent. Bushes and trees (which do give
    /// cover) are regular objects with the same culling on every tier.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public sealed class TerrainDetailQuality : MonoBehaviour
    {
        private Terrain _terrain;

        public static (float density, float distance) For(GraphicsQualityTier tier)
        {
            switch (tier)
            {
                // Budget: roughly 2 / 6 / 14 thousand grass clumps (~80 vertices each) in view.
                case GraphicsQualityTier.Low: return (0.3f, 25f);
                case GraphicsQualityTier.High: return (0.9f, 80f);
                default: return (0.55f, 45f);
            }
        }

        private void Awake() => _terrain = GetComponent<Terrain>();

        private void OnEnable()
        {
            GraphicsQualityService.TierChanged += Apply;
            Apply(AppServices.IsInitialized ? AppServices.Graphics.CurrentTier : GraphicsQualityTier.Medium);
        }

        private void OnDisable() => GraphicsQualityService.TierChanged -= Apply;

        private void Apply(GraphicsQualityTier tier)
        {
            if (_terrain == null)
                return;
            var (density, distance) = For(tier);
            _terrain.detailObjectDensity = density;
            _terrain.detailObjectDistance = distance;
        }
    }
}
