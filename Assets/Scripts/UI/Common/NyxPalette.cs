using UnityEngine;

namespace WarOfNyx.UI.Common
{
    /// <summary>Brand and HUD colours from spec sections 19 and 32.</summary>
    public static class NyxPalette
    {
        public static readonly Color DeepBlack = Hex(0x08, 0x08, 0x12);
        public static readonly Color HudBackground = Hex(0x0B, 0x0B, 0x18);
        public static readonly Color MidnightNavy = Hex(0x11, 0x15, 0x2A);
        public static readonly Color NyxPurple = Hex(0x8A, 0x2B, 0xE2);
        public static readonly Color ElectricCyan = Hex(0x00, 0xE5, 0xFF);
        public static readonly Color Crimson = Hex(0xFF, 0x30, 0x4F);
        public static readonly Color VictoryGold = Hex(0xFF, 0xC8, 0x57);

        public static readonly Color TextPrimary = new Color(0.94f, 0.94f, 1f, 1f);
        public static readonly Color TextMuted = new Color(0.60f, 0.62f, 0.75f, 1f);
        public static readonly Color Disabled = new Color(0.16f, 0.17f, 0.24f, 1f);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
