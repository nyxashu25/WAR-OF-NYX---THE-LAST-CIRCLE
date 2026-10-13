namespace WarOfNyx.Core.Movement
{
    /// <summary>The movement state machine's states. Exactly one is active at a time.</summary>
    public enum LocomotionMode
    {
        Standing = 0,
        Crouching = 1,
        Sliding = 2,
        Airborne = 3,
    }

    /// <summary>
    /// One tick of movement intent. Produced identically by the human input router
    /// (touch, keyboard/mouse, gamepad) and by AI bots, then simulated by the authority.
    /// "Pressed" fields are edges: true only on the tick the button went down.
    /// </summary>
    public struct MovementCommand
    {
        /// <summary>Local stick: X = strafe right, Y = forward. Magnitude is clamped to 1.</summary>
        public float MoveX;
        public float MoveY;

        /// <summary>Aim direction in degrees. Yaw 0 = world +Z, increasing clockwise seen from above (Unity convention).</summary>
        public float Yaw;
        public float Pitch;

        public bool SprintHeld;
        public bool AimHeld;
        public bool JumpPressed;
        public bool CrouchPressed;
        public bool SlidePressed;
    }

    /// <summary>Facts about the world that the physics layer reports before a step.</summary>
    public struct GroundInfo
    {
        public bool IsGrounded;
        /// <summary>False when there is no headroom for a standing capsule (e.g. inside a low tunnel).</summary>
        public bool CanStand;

        public GroundInfo(bool isGrounded, bool canStand)
        {
            IsGrounded = isGrounded;
            CanStand = canStand;
        }
    }

    /// <summary>
    /// Complete movement state of one character. A plain value type so it can be copied into
    /// networked state, rolled back for prediction, and compared in tests.
    /// </summary>
    public struct MovementState
    {
        public LocomotionMode Mode;

        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
        public float Yaw;

        /// <summary>The player's crouch toggle. The capsule auto-stands when it clears and there is headroom.</summary>
        public bool CrouchToggled;
        public bool IsSprinting;

        public float SlideTimeRemaining;
        public float SlideDirX;
        public float SlideDirZ;
        public float SlideCooldownRemaining;

        public float CoyoteTimeRemaining;
        public float JumpBufferRemaining;

        // One-step event flags for animation, audio and VFX. Reset at the start of every step.
        public bool JumpedThisStep;
        public bool LandedThisStep;
        public bool SlideStartedThisStep;

        public float HorizontalSpeed => System.MathF.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);

        /// <summary>True when the capsule should use the short (crouch) height.</summary>
        public bool IsLowStance => Mode == LocomotionMode.Crouching || Mode == LocomotionMode.Sliding;

        public static MovementState Initial(float yaw) => new MovementState { Mode = LocomotionMode.Standing, Yaw = yaw };
    }
}
