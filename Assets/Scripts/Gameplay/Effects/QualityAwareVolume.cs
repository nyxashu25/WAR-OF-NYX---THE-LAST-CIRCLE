using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.App;
using WarOfNyx.Settings;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Turns the scene's post-processing volume (bloom, colour grading, vignette) on for the Medium
    /// and High tiers and off for Low, so weak phones keep their frame rate (spec 10 / 25). Visual only.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public sealed class QualityAwareVolume : MonoBehaviour
    {
        private Volume _volume;

        private void Awake() => _volume = GetComponent<Volume>();

        private void OnEnable()
        {
            GraphicsQualityService.TierChanged += Apply;
            Apply(AppServices.IsInitialized ? AppServices.Graphics.CurrentTier : GraphicsQualityTier.Medium);
        }

        private void OnDisable() => GraphicsQualityService.TierChanged -= Apply;

        private void Apply(GraphicsQualityTier tier)
        {
            if (_volume != null)
                _volume.enabled = GraphicsQualityService.PostProcessingAllowed(tier);
        }
    }
}
