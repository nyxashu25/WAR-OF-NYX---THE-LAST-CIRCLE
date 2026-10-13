using Fusion;
using UnityEngine;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// One tick of player input as sent to the authority (client → server).
    /// Only intent travels: stick, aim angles, button bits. Never positions, hits or results.
    /// The server decodes it with <see cref="InputCodec.Unpack"/>, which also sanitizes it.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public uint Buttons;

        public static NetworkInputData From(in MovementCommand command) => From(command, default);

        public static NetworkInputData From(in MovementCommand command, in CombatCommand combat) => new NetworkInputData
        {
            Move = new Vector2(command.MoveX, command.MoveY),
            Yaw = command.Yaw,
            Pitch = command.Pitch,
            Buttons = InputCodec.PackButtons(command, combat),
        };

        public MovementCommand ToCommand() => InputCodec.Unpack(Move.x, Move.y, Yaw, Pitch, Buttons);

        public CombatCommand ToCombat() => InputCodec.UnpackCombat(Buttons);
    }

    /// <summary>
    /// Networked copy of the full movement simulation state (server → clients).
    /// Fusion rolls it back for client prediction, so every field the movement model reads
    /// must be in here (timers, toggles, grounded result, position), not only the visuals.
    /// </summary>
    public struct NetworkMovementState : INetworkStruct
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public byte Mode;
        public NetworkBool CrouchToggled;
        public NetworkBool IsSprinting;
        public NetworkBool Grounded;
        public float SlideTimeRemaining;
        public Vector2 SlideDirection;
        public float SlideCooldownRemaining;
        public float CoyoteTimeRemaining;
        public float JumpBufferRemaining;

        public MovementState ToState() => new MovementState
        {
            Mode = (LocomotionMode)Mode,
            VelocityX = Velocity.x,
            VelocityY = Velocity.y,
            VelocityZ = Velocity.z,
            Yaw = Yaw,
            CrouchToggled = CrouchToggled,
            IsSprinting = IsSprinting,
            SlideTimeRemaining = SlideTimeRemaining,
            SlideDirX = SlideDirection.x,
            SlideDirZ = SlideDirection.y,
            SlideCooldownRemaining = SlideCooldownRemaining,
            CoyoteTimeRemaining = CoyoteTimeRemaining,
            JumpBufferRemaining = JumpBufferRemaining,
        };

        public static NetworkMovementState From(in MovementState s, bool grounded, Vector3 position) => new NetworkMovementState
        {
            Position = position,
            Velocity = new Vector3(s.VelocityX, s.VelocityY, s.VelocityZ),
            Yaw = s.Yaw,
            Mode = (byte)s.Mode,
            CrouchToggled = s.CrouchToggled,
            IsSprinting = s.IsSprinting,
            Grounded = grounded,
            SlideTimeRemaining = s.SlideTimeRemaining,
            SlideDirection = new Vector2(s.SlideDirX, s.SlideDirZ),
            SlideCooldownRemaining = s.SlideCooldownRemaining,
            CoyoteTimeRemaining = s.CoyoteTimeRemaining,
            JumpBufferRemaining = s.JumpBufferRemaining,
        };
    }
}
