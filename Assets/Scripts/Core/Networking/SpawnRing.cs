using System;

namespace WarOfNyx.Core.Networking
{
    /// <summary>
    /// TEMPORARY Phase 3 spawn placement: spreads connected players evenly around a circle so
    /// two test clients never spawn inside each other. Phase 4 replaces this with the real
    /// server-side random spawn system (navmesh validation, 65 m separation, spec 6).
    /// </summary>
    public static class SpawnRing
    {
        /// <summary>
        /// Position on the ring for slot <paramref name="index"/> of <paramref name="slots"/>.
        /// Returns X/Z offsets from the ring centre and the yaw (degrees) that faces the centre.
        /// </summary>
        public static void Slot(int index, int slots, float radius, out float offsetX, out float offsetZ, out float yawToCentre)
        {
            if (slots < 1)
                slots = 1;
            index = ((index % slots) + slots) % slots;

            float angle = index * (2f * MathF.PI / slots);
            offsetX = MathF.Sin(angle) * radius;
            offsetZ = MathF.Cos(angle) * radius;

            // Yaw convention: 0° = +Z, 90° = +X. Facing the centre means looking along -offset.
            float yaw = MathF.Atan2(-offsetX, -offsetZ) * (180f / MathF.PI);
            yawToCentre = yaw < 0f ? yaw + 360f : yaw;
        }

        /// <summary>Straight-line distance between two neighbouring slots.</summary>
        public static float NeighbourDistance(int slots, float radius) =>
            slots <= 1 ? float.PositiveInfinity : 2f * radius * MathF.Sin(MathF.PI / slots);
    }
}
