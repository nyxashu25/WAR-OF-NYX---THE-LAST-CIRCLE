using System;
using WarOfNyx.Core.Combat;
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
            // Combat (Phase 5)
            FireHeld = 1u << 5,
            FirePressed = 1u << 6,
            Reload = 1u << 7,
            Melee = 1u << 8,
            Slot1 = 1u << 9,
            Slot2 = 1u << 10,
            Slot3 = 1u << 11,
            NextWeapon = 1u << 12,
            Medkit = 1u << 13,
            // Interact is client-side only (opens the loot panel); taking items is a validated RPC.
        }

        public static uint PackButtons(in MovementCommand movement, in CombatCommand combat)
        {
            var b = (Buttons)PackButtons(movement);
            if (combat.FireHeld) b |= Buttons.FireHeld;
            if (combat.FirePressed) b |= Buttons.FirePressed;
            if (combat.ReloadPressed) b |= Buttons.Reload;
            if (combat.MeleePressed) b |= Buttons.Melee;
            if (combat.SelectSlot == 1) b |= Buttons.Slot1;
            else if (combat.SelectSlot == 2) b |= Buttons.Slot2;
            else if (combat.SelectSlot == 3) b |= Buttons.Slot3;
            if (combat.NextWeaponPressed) b |= Buttons.NextWeapon;
            if (combat.MedkitPressed) b |= Buttons.Medkit;
            return (uint)b;
        }

        /// <summary>Decodes combat intent. If several slot bits are set (forged input), the lowest wins.</summary>
        public static CombatCommand UnpackCombat(uint buttons)
        {
            var b = (Buttons)buttons;
            return new CombatCommand
            {
                FireHeld = (b & Buttons.FireHeld) != 0,
                FirePressed = (b & Buttons.FirePressed) != 0,
                ReloadPressed = (b & Buttons.Reload) != 0,
                MeleePressed = (b & Buttons.Melee) != 0,
                SelectSlot = (b & Buttons.Slot1) != 0 ? 1 : (b & Buttons.Slot2) != 0 ? 2 : (b & Buttons.Slot3) != 0 ? 3 : 0,
                NextWeaponPressed = (b & Buttons.NextWeapon) != 0,
                MedkitPressed = (b & Buttons.Medkit) != 0,
            };
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
            command.SpeedMultiplier = 0f; // authority-only field: never accepted from a client
            command.LateralWobble = 0f;   // authority-only: the client can neither add nor remove instability
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
