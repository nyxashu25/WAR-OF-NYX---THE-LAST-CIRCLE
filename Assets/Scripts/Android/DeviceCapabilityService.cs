using UnityEngine;
using WarOfNyx.Core.Offline;
using WarOfNyx.Settings;

namespace WarOfNyx.Platform
{
    /// <summary>
    /// Reads hardware facts once and turns them into defaults
    /// (graphics tier, maximum offline bot count). Thresholds are provisional
    /// until measured on physical devices (see KNOWN_ISSUES.md).
    /// </summary>
    public static class DeviceCapabilityService
    {
        public static DeviceProfile Current =>
            new DeviceProfile(Application.isMobilePlatform, SystemInfo.systemMemorySize, SystemInfo.processorCount);

        public static int MaxOfflineBots => OfflineCapacityPolicy.MaxBotsFor(Current);

        public static GraphicsQualityTier RecommendedGraphicsTier
        {
            get
            {
                var device = Current;
                if (!device.IsMobile)
                    return GraphicsQualityTier.High;
                if (device.SystemMemoryMB < OfflineCapacityPolicy.MidTierMemoryMB)
                    return GraphicsQualityTier.Low;
                if (device.SystemMemoryMB < OfflineCapacityPolicy.HighTierMemoryMB)
                    return GraphicsQualityTier.Medium;
                return GraphicsQualityTier.High;
            }
        }

        public static string Describe()
        {
            var d = Current;
            return $"{SystemInfo.deviceModel} | {d.SystemMemoryMB} MB RAM | {d.ProcessorCount} cores | " +
                   $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})";
        }
    }
}
