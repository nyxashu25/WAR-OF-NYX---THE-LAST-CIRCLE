using System;
using System.Collections.Generic;

namespace WarOfNyx.Core.Controls
{
    /// <summary>One moved touch button (offset from its default position, in canvas units).</summary>
    [Serializable]
    public sealed class TouchButtonOffset
    {
        public string id = string.Empty;
        public float x;
        public float y;
    }

    /// <summary>
    /// The player's touch-control layout (spec 18 "adjustable touch controls"): overall button size,
    /// opacity, and per-button position offsets. Saved with the user settings; purely local.
    /// Values are clamped so a layout can never push a button off screen or make it unusable.
    /// </summary>
    [Serializable]
    public sealed class TouchLayout
    {
        public const float MinScale = 0.6f, MaxScale = 1.6f;
        public const float MinOpacity = 0.2f, MaxOpacity = 1f;
        public const float MaxOffsetX = 700f, MaxOffsetY = 420f;

        public float scale = 1f;
        public float opacity = 0.8f;
        public List<TouchButtonOffset> offsets = new List<TouchButtonOffset>();

        public void Offset(string id, out float x, out float y)
        {
            x = y = 0f;
            if (offsets == null)
                return;
            foreach (var o in offsets)
            {
                if (o != null && o.id == id)
                {
                    x = o.x;
                    y = o.y;
                    return;
                }
            }
        }

        public void SetOffset(string id, float x, float y)
        {
            if (string.IsNullOrEmpty(id))
                return;
            offsets ??= new List<TouchButtonOffset>();
            x = Clamp(Finite(x), -MaxOffsetX, MaxOffsetX);
            y = Clamp(Finite(y), -MaxOffsetY, MaxOffsetY);
            foreach (var o in offsets)
            {
                if (o != null && o.id == id)
                {
                    o.x = x;
                    o.y = y;
                    return;
                }
            }
            offsets.Add(new TouchButtonOffset { id = id, x = x, y = y });
        }

        public void Reset()
        {
            scale = 1f;
            opacity = 0.8f;
            offsets = new List<TouchButtonOffset>();
        }

        /// <summary>Repairs anything out of range (hand-edited or corrupted save data).</summary>
        public void Sanitize()
        {
            scale = Clamp(Finite(scale, 1f), MinScale, MaxScale);
            opacity = Clamp(Finite(opacity, 0.8f), MinOpacity, MaxOpacity);
            offsets ??= new List<TouchButtonOffset>();
            offsets.RemoveAll(o => o == null || string.IsNullOrEmpty(o.id));
            var seen = new HashSet<string>();
            for (int i = offsets.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(offsets[i].id))
                {
                    offsets.RemoveAt(i); // keep the last saved position per button
                    continue;
                }
                offsets[i].x = Clamp(Finite(offsets[i].x), -MaxOffsetX, MaxOffsetX);
                offsets[i].y = Clamp(Finite(offsets[i].y), -MaxOffsetY, MaxOffsetY);
            }
        }

        private static float Finite(float v, float fallback = 0f) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>Gyro aim mapping (spec 18 optional gyro): device angular velocity → yaw/pitch deltas in landscape.</summary>
    public static class GyroAim
    {
        /// <summary>
        /// <paramref name="rateX"/>/<paramref name="rateY"/> are the device's angular velocity (rad/s) around its
        /// own x (short edge) and y (long edge) axes. In landscape the device x axis is vertical, so turning
        /// the phone left/right is a rotation about x. <paramref name="landscapeLeft"/> flips the signs for the
        /// two landscape orientations.
        /// </summary>
        public static void Delta(float rateX, float rateY, bool landscapeLeft, float sensitivity, float dt, out float yaw, out float pitch)
        {
            const float RadToDeg = 57.29578f;
            float sign = landscapeLeft ? 1f : -1f;
            yaw = -rateX * sign * RadToDeg * sensitivity * dt;
            pitch = -rateY * sign * RadToDeg * sensitivity * dt;
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) yaw = 0f;
            if (float.IsNaN(pitch) || float.IsInfinity(pitch)) pitch = 0f;
        }
    }
}
