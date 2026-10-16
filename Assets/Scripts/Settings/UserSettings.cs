using System;
using UnityEngine;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Settings
{
    public enum GraphicsQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>
    /// Per-device player preferences. Purely local and cosmetic/comfort-related:
    /// nothing here may change gameplay rules (spec 12: reducing visual effects never
    /// removes the underlying penalty).
    /// </summary>
    [Serializable]
    public sealed class UserSettingsData
    {
        /// <summary>-1 = automatic (picked from device capability).</summary>
        public int graphicsTier = -1;

        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float uiVolume = 1f;

        public float joystickSensitivity = 1f;
        public float aimSensitivity = 1f;
        public bool gyroAim;

        public bool reduceCameraShake;
        public bool reduceFlashing;

        /// <summary>Blood splatter on the screen after critical hits, cleared by healing (on by default).</summary>
        public bool bloodEffects = true;

        /// <summary>Section 40: hair and coat secondary motion off (accessibility / low-end phones).</summary>
        public bool reduceMotion;

        /// <summary>Phase 13: FPS / frame-time / memory overlay, and per-second CSV recording for device tests.</summary>
        public bool showPerformance;
        public bool recordPerformance;

        /// <summary>Touch control layout: size, opacity, moved buttons (Phase 11).</summary>
        public WarOfNyx.Core.Controls.TouchLayout touchLayout = new WarOfNyx.Core.Controls.TouchLayout();

        /// <summary>Cosmetic character (CharacterRoster id). No gameplay effect.</summary>
        public int characterId;

        /// <summary>Display name sent to servers (sanitized again server-side).</summary>
        public string playerName = string.Empty;

        /// <summary>Offline map: 0 = NYX ISLAND (default), 1 = training arena.</summary>
        public int offlineMap;

        /// <summary>-1 = use GameConfig default.</summary>
        public int offlineBotCount = -1;
        public int offlineDifficulty = -1;
    }

    /// <summary>Loads/saves <see cref="UserSettingsData"/> as JSON in PlayerPrefs.</summary>
    public sealed class UserSettingsService
    {
        private const string PrefsKey = "won.settings.v1";

        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        public UserSettingsData Data { get; private set; } = new UserSettingsData();

        public event Action<UserSettingsData> Changed;

        public void Load()
        {
            var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                Data = new UserSettingsData();
                return;
            }

            try
            {
                Data = JsonUtility.FromJson<UserSettingsData>(json) ?? new UserSettingsData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WarOfNyx] Settings were unreadable and have been reset: {ex.Message}");
                Data = new UserSettingsData();
            }

            Sanitize(Data);
        }

        public void Save()
        {
            Sanitize(Data);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke(Data);
        }

        /// <summary>The saved offline choice, or the config default when none is saved.</summary>
        public OfflineMatchSettings GetOfflineSettings(OfflineMatchSettings fallback)
        {
            int bots = OfflineMatchSettings.IsAllowedBotCount(Data.offlineBotCount) ? Data.offlineBotCount : fallback.BotCount;
            var difficulty = Enum.IsDefined(typeof(BotDifficulty), Data.offlineDifficulty)
                ? (BotDifficulty)Data.offlineDifficulty
                : fallback.Difficulty;
            return new OfflineMatchSettings(bots, difficulty);
        }

        public void SetOfflineSettings(OfflineMatchSettings settings)
        {
            Data.offlineBotCount = settings.BotCount;
            Data.offlineDifficulty = (int)settings.Difficulty;
            Save();
        }

        private static void Sanitize(UserSettingsData d)
        {
            d.masterVolume = Mathf.Clamp01(d.masterVolume);
            d.musicVolume = Mathf.Clamp01(d.musicVolume);
            d.sfxVolume = Mathf.Clamp01(d.sfxVolume);
            d.uiVolume = Mathf.Clamp01(d.uiVolume);
            d.joystickSensitivity = Mathf.Clamp(d.joystickSensitivity, MinSensitivity, MaxSensitivity);
            d.aimSensitivity = Mathf.Clamp(d.aimSensitivity, MinSensitivity, MaxSensitivity);
            if (d.graphicsTier < -1 || d.graphicsTier > (int)GraphicsQualityTier.High)
                d.graphicsTier = -1;
            d.touchLayout ??= new WarOfNyx.Core.Controls.TouchLayout();
            d.touchLayout.Sanitize();
        }
    }
}
