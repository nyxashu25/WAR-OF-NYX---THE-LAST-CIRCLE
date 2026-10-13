using System;
using UnityEngine;

namespace WarOfNyx.Settings
{
    /// <summary>
    /// Applies the Low / Medium / High presets (spec 25 "Graphics Presets").
    /// Each preset maps to a Unity Quality level of the same name; the project setup tool
    /// creates those levels and assigns a dedicated URP asset to each one
    /// (shadows, render scale, MSAA, additional lights). This service only switches the level
    /// and sets the frame-rate target, so it works on any device without reallocating assets.
    /// </summary>
    public sealed class GraphicsQualityService
    {
        public GraphicsQualityTier CurrentTier { get; private set; } = GraphicsQualityTier.Medium;

        public static int TargetFrameRate(GraphicsQualityTier tier)
        {
            switch (tier)
            {
                case GraphicsQualityTier.Low: return 30;
                case GraphicsQualityTier.Medium: return 45;
                case GraphicsQualityTier.High: return 60;
                default: return 30;
            }
        }

        public void Apply(GraphicsQualityTier tier)
        {
            int level = FindQualityLevel(tier, QualitySettings.names);
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);

            // targetFrameRate is ignored while vSync is on (desktop); mobile always uses targetFrameRate.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate(tier);
            CurrentTier = tier;

            // Phase 10 visuals: the cel-shader outline and post-processing are skipped on Low (mobile cost).
            Shader.SetGlobalFloat(OutlineDisableId, tier == GraphicsQualityTier.Low ? 1f : 0f);
            TierChanged?.Invoke(tier);
        }

        private static readonly int OutlineDisableId = Shader.PropertyToID("_NyxOutlineDisable");

        /// <summary>Raised after a tier is applied (post-processing volumes listen to it).</summary>
        public static event Action<GraphicsQualityTier> TierChanged;

        /// <summary>Post-processing (bloom, grading) runs on Medium and High only.</summary>
        public static bool PostProcessingAllowed(GraphicsQualityTier tier) => tier != GraphicsQualityTier.Low;

        /// <summary>
        /// Index of the quality level named after the tier, or a positional fallback
        /// (first / middle / last) when the project still has Unity's default level names.
        /// </summary>
        public static int FindQualityLevel(GraphicsQualityTier tier, string[] levelNames)
        {
            if (levelNames == null || levelNames.Length == 0)
                return -1;

            string wanted = tier.ToString();
            for (int i = 0; i < levelNames.Length; i++)
            {
                if (string.Equals(levelNames[i], wanted, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            switch (tier)
            {
                case GraphicsQualityTier.Low: return 0;
                case GraphicsQualityTier.High: return levelNames.Length - 1;
                default: return levelNames.Length / 2;
            }
        }
    }
}
