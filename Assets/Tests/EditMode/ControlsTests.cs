using NUnit.Framework;
using WarOfNyx.Core.Controls;

namespace WarOfNyx.Tests
{
    /// <summary>Phase 11: adjustable touch layout and gyro mapping.</summary>
    public class TouchLayoutTests
    {
        [Test]
        public void OffsetsAreStoredPerButtonAndClamped()
        {
            var layout = new TouchLayout();
            layout.SetOffset("FIRE", 120f, -40f);
            layout.SetOffset("FIRE", 150f, -60f);
            layout.SetOffset("JUMP", 99999f, -99999f);
            layout.Offset("FIRE", out float fx, out float fy);
            Assert.AreEqual(150f, fx);
            Assert.AreEqual(-60f, fy);
            layout.Offset("JUMP", out float jx, out float jy);
            Assert.AreEqual(TouchLayout.MaxOffsetX, jx, "never off screen");
            Assert.AreEqual(-TouchLayout.MaxOffsetY, jy);
            Assert.AreEqual(2, layout.offsets.Count);
            layout.Offset("MISSING", out float mx, out float my);
            Assert.AreEqual(0f, mx);
            Assert.AreEqual(0f, my);
        }

        [Test]
        public void CorruptedLayoutsAreRepaired()
        {
            var layout = new TouchLayout { scale = float.NaN, opacity = 5f };
            layout.offsets.Add(null);
            layout.offsets.Add(new TouchButtonOffset { id = "", x = 1f });
            layout.offsets.Add(new TouchButtonOffset { id = "AIM", x = 1f, y = float.PositiveInfinity });
            layout.offsets.Add(new TouchButtonOffset { id = "AIM", x = 5f, y = 5f });
            layout.Sanitize();
            Assert.AreEqual(1f, layout.scale);
            Assert.AreEqual(TouchLayout.MaxOpacity, layout.opacity);
            Assert.AreEqual(1, layout.offsets.Count, "nulls, empty ids and duplicates removed");
            Assert.AreEqual(5f, layout.offsets[0].x, "the last saved position wins");
        }

        [Test]
        public void ResetRestoresDefaults()
        {
            var layout = new TouchLayout { scale = 1.5f, opacity = 0.3f };
            layout.SetOffset("MELEE", 10f, 10f);
            layout.Reset();
            Assert.AreEqual(1f, layout.scale);
            Assert.AreEqual(0.8f, layout.opacity);
            Assert.IsEmpty(layout.offsets);
        }

        [Test]
        public void GyroTurnsTheViewProportionallyAndHandlesBothLandscapes()
        {
            GyroAim.Delta(1f, 0f, landscapeLeft: true, sensitivity: 1f, dt: 0.1f, out float yaw, out float pitch);
            Assert.AreEqual(-5.7296f, yaw, 1e-3f);
            Assert.AreEqual(0f, pitch);
            GyroAim.Delta(1f, 0f, landscapeLeft: false, sensitivity: 1f, dt: 0.1f, out float yaw2, out _);
            Assert.AreEqual(-yaw, yaw2, 1e-5f, "the other landscape orientation mirrors the axis");
            GyroAim.Delta(float.NaN, 0f, true, 1f, 0.1f, out float bad, out _);
            Assert.AreEqual(0f, bad);
        }
    }
}
