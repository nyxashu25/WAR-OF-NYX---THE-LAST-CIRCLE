namespace WarOfNyx.Gameplay.World
{
    /// <summary>Physics layers used by the environment (named by the project setup tool).</summary>
    public static class EnvironmentLayers
    {
        /// <summary>
        /// Bushes: trigger volumes that block SIGHT (bot perception) but not bullets or movement,
        /// matching what a human sees: a player hidden in a bush is hidden from bots too.
        /// </summary>
        public const int Foliage = 8;
        public const string FoliageName = "Foliage";
        public const int FoliageMask = 1 << Foliage;
    }
}
