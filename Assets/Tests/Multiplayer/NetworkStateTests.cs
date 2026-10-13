using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Core.Movement;
using WarOfNyx.Networking;

namespace WarOfNyx.Tests.Multiplayer
{
    /// <summary>
    /// The networked structs must carry every field the movement model reads, otherwise client
    /// re-simulation after a server correction would diverge (rubber-banding).
    /// Compiles only with the Fusion SDK installed (WON_FUSION).
    /// </summary>
    public class NetworkStateTests
    {
        [Test]
        public void MovementStateSurvivesTheNetworkRoundTrip()
        {
            var original = new MovementState
            {
                Mode = LocomotionMode.Sliding,
                VelocityX = 1.5f, VelocityY = -2f, VelocityZ = 8.25f,
                Yaw = 271.5f,
                CrouchToggled = true,
                IsSprinting = false,
                SlideTimeRemaining = 0.31f,
                SlideDirX = 0.6f, SlideDirZ = 0.8f,
                SlideCooldownRemaining = 0.7f,
                CoyoteTimeRemaining = 0.05f,
                JumpBufferRemaining = 0.02f,
            };

            var net = NetworkMovementState.From(original, true, new Vector3(3f, 4f, 5f));
            var restored = net.ToState();

            Assert.AreEqual(original, restored);
            Assert.IsTrue((bool)net.Grounded);
            Assert.AreEqual(new Vector3(3f, 4f, 5f), net.Position);
        }

        [Test]
        public void InputSurvivesTheNetworkRoundTrip()
        {
            var command = new MovementCommand
            {
                MoveX = -0.3f, MoveY = 0.9f, Yaw = 45f, Pitch = 12f,
                SprintHeld = true, JumpPressed = true, SlidePressed = true,
            };
            var decoded = NetworkInputData.From(command).ToCommand();
            Assert.AreEqual(command, decoded);
        }

        [Test]
        public void ForgedInputIsSanitizedOnDecode()
        {
            var forged = new NetworkInputData { Move = new Vector2(50f, 50f), Yaw = float.NaN, Pitch = 400f, Buttons = uint.MaxValue };
            var command = forged.ToCommand();
            Assert.LessOrEqual(new Vector2(command.MoveX, command.MoveY).magnitude, 1.0001f);
            Assert.AreEqual(0f, command.Yaw);
            Assert.LessOrEqual(command.Pitch, 89f);
        }

        [Test]
        public void WeaponSlotSurvivesTheNetworkRoundTrip()
        {
            var state = new WarOfNyx.Core.Combat.WeaponState
            {
                WeaponId = 4, Magazine = 17, Reserve = 51, NextFireAt = 12.5f, ReloadEndsAt = 13.25f,
                BurstRemaining = 2, NextBurstShotAt = 12.57f, Bloom = 1.4f, BloomUpdatedAt = 12.4f, ShotsFired = 93,
            };
            Assert.AreEqual(state, NetworkWeaponSlot.From(state).ToState());
        }

        [Test]
        public void CombatButtonsSurviveTheNetworkRoundTrip()
        {
            var combat = new WarOfNyx.Core.Combat.CombatCommand { FireHeld = true, ReloadPressed = true, SelectSlot = 3 };
            var data = NetworkInputData.From(new MovementCommand { MoveY = 1f }, combat);
            Assert.AreEqual(combat, data.ToCombat());
        }
    }
}
