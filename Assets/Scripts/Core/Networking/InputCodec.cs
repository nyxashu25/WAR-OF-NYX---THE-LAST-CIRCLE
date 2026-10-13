using System;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Core.Networking
{
    /// <summary>
    /// Converts a <see cref="MovementCommand"/> to/from the compact form sent over the network
    /// each tick (stick, aim angles, one button bitmask), and sanitizes commands on the
    /// authority. The server never trusts the raw values: NaN/∞ are zeroed, the stick is clamped
    /// to length 1, and the aim angles are wrapped/clamped.
    ///
    /// Button bits for one-shot actions (jump, crouch, slide) are EDGES: set only on the tick the
    /// player pressed the button. Fusion delivers input per tick, so an edge is processed exactly
    /// once, including during client re-simulation of that tick.
    /// </summary>
    public static class InputCodec
    {
        [Flags]
        public enum Buttons : uint
        {
            None = 0,
            Sprint = 1u << 0,
            Aim = 1u << 1,
            Jump = 1u << 2,
            Crouch = 1u << 3,
            Slide = 1u << 4,
            // Reserved for later phases: Fire, Reload, Melee, Interact, Medkit, weapon slots.
        }

        public const float MinPitch = -89f;
        public const float MaxPitch = 89f;

        public static uint PackButtons(in MovementCommand command)
        {
            var b = Buttons.None;
            if (command.SprintHeld) b |= Buttons.Sprint;
            if (command.AimHeld) b |= Buttons.Aim;
            if (command.JumpPressed) b |= Buttons.Jump;
            if (command.CrouchPressed) b |= Buttons.Crouch;
            if (command.SlidePressed) b |= Buttons.Slide;
            return (uint)b;
        }

        public static MovementCommand Unpack(float moveX, float moveY, float yaw, float pitch, uint buttons)
        {
            var b = (Buttons)buttons;
            var command = new MovementCommand
            {
                MoveX = moveX,
                MoveY = moveY,
                Yaw = yaw,
                Pitch = pitch,
                SprintHeld = (b & Buttons.Sprint) != 0,
                AimHeld = (b & Buttons.Aim) != 0,
                JumpPressed = (b & Buttons.Jump) != 0,
                CrouchPressed = (b & Buttons.Crouch) != 0,
                SlidePressed = (b & Buttons.Slide) != 0,
            };
            return Sanitize(command);
        }

        /// <summary>Authority-side validation of a received command.</summary>
        public static MovementCommand Sanitize(MovementCommand command)
        {
            float x = Finite(command.MoveX);
            float y = Finite(command.MoveY);
            float length = MathF.Sqrt(x * x + y * y);
            if (length > 1f)
            {
                x /= length;
                y /= length;
            }

            command.MoveX = x;
            command.MoveY = y;
            command.Yaw = WrapDegrees(Finite(command.Yaw));
            command.Pitch = Math.Clamp(Finite(command.Pitch), MinPitch, MaxPitch);
            return command;
        }

        /// <summary>The command used when a player's input for a tick never arrived: stand still, keep facing.</summary>
        public static MovementCommand Neutral(float yaw, float pitch) =>
            new MovementCommand { Yaw = WrapDegrees(Finite(yaw)), Pitch = Math.Clamp(Finite(pitch), MinPitch, MaxPitch) };

        public static float WrapDegrees(float degrees)
        {
            float wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        private static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
    }
}
