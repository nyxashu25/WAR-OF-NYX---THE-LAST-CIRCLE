using UnityEngine;
using WarOfNyx.Core.Items;

namespace WarOfNyx.UI.Common
{
    /// <summary>Spec 14: Common white, Uncommon green, Rare blue, Epic purple, Legendary gold.</summary>
    public static class RarityColors
    {
        public static Color For(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return new Color(0.30f, 0.90f, 0.40f);
                case Rarity.Rare: return new Color(0.25f, 0.55f, 1.00f);
                case Rarity.Epic: return NyxPalette.NyxPurple;
                case Rarity.Legendary: return NyxPalette.VictoryGold;
                default: return new Color(0.92f, 0.92f, 0.95f);
            }
        }

        public static string Name(Rarity rarity) => rarity.ToString().ToUpperInvariant();
    }
}
