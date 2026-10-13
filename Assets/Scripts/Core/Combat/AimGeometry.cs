using System;

namespace WarOfNyx.Core.Combat
{
    /// <summary>
    /// Where shots start. The third-person camera sits behind a shoulder point and looks along the
    /// aim direction, so a ray from that SAME shoulder point along the aim direction is exactly the
    /// crosshair line. The server uses this to fire, so what you see is what you hit.
    /// Shared by the camera (client) and the authority (server) so they can never disagree.
    /// </summary>
    public static class AimGeometry
    {
        public const float ShoulderOffsetRight = 0.55f;
        public const float PivotHeightStanding = 1.6f;
        public const float PivotHeightCrouched = 1.05f;

        /// <summary>Forward unit vector for yaw/pitch in degrees (Unity convention, +pitch looks down).</summary>
        public static void Forward(float yawDeg, float pitchDeg, out float x, out float y, out float z)
        {
            double yaw = yawDeg * Math.PI / 180.0;
            double pitch = pitchDeg * Math.PI / 180.0;
            double cp = Math.Cos(pitch);
            x = (float)(Math.Sin(yaw) * cp);
            y = (float)(-Math.Sin(pitch));
            z = (float)(Math.Cos(yaw) * cp);
        }

        /// <summary>Shoulder point relative to the character's feet (rotation by yaw and pitch, like the camera).</summary>
        public static void ShoulderOffset(float yawDeg, bool lowStance, out float x, out float y, out float z)
        {
            // (offsetRight, 0, 0) rotated by Euler(pitch, yaw, 0): pitch turns about the right axis, so only yaw matters.
            double yaw = yawDeg * Math.PI / 180.0;
            x = (float)(Math.Cos(yaw) * ShoulderOffsetRight);
            y = lowStance ? PivotHeightCrouched : PivotHeightStanding;
            z = (float)(-Math.Sin(yaw) * ShoulderOffsetRight);
        }
    }
}
