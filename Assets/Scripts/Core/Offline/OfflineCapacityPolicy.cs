namespace WarOfNyx.Core.Offline
{
    /// <summary>Hardware facts the capacity policy needs. Filled from UnityEngine.SystemInfo by the app layer.</summary>
    public readonly struct DeviceProfile
    {
        public readonly bool IsMobile;
        public readonly int SystemMemoryMB;
        public readonly int ProcessorCount;

        public DeviceProfile(bool isMobile, int systemMemoryMB, int processorCount)
        {
            IsMobile = isMobile;
            SystemMemoryMB = systemMemoryMB;
            ProcessorCount = processorCount;
        }
    }

    /// <summary>
    /// Decides the largest offline bot count a device may select (master spec 38.3 / 38.15:
    /// "The 23-bot option should only be enabled when device performance supports it").
    ///
    /// The thresholds below are PROVISIONAL. They are a first guess from RAM and core count and
    /// must be replaced with measured numbers once the Ghost Protocol phase is profiled on
    /// physical devices (see KNOWN_ISSUES.md).
    /// </summary>
    public static class OfflineCapacityPolicy
    {
        public const int LowTierMemoryMB = 3072;
        public const int MidTierMemoryMB = 4096;
        public const int HighTierMemoryMB = 6144;

        public static int MaxBotsFor(DeviceProfile device)
        {
            // Desktop/editor builds are development tools; allow the full range.
            if (!device.IsMobile)
                return 23;

            if (device.SystemMemoryMB < LowTierMemoryMB || device.ProcessorCount < 4)
                return 5;
            if (device.SystemMemoryMB < MidTierMemoryMB || device.ProcessorCount < 6)
                return 11;
            if (device.SystemMemoryMB < HighTierMemoryMB || device.ProcessorCount < 8)
                return 15;
            return 23;
        }
    }
}
