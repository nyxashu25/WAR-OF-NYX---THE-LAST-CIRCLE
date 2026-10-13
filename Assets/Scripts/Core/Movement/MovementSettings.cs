using System;

namespace WarOfNyx.Core.Movement
{
    /// <summary>
    /// Tunable movement values (master spec section 8). Serializable so Unity can edit it inside
    /// a ScriptableObject (MovementConfig), while staying engine-independent for tests, the
    /// Fusion server and the offline simulation. All values are starting points [T].
    /// </summary>
    [Serializable]
    public sealed class MovementSettings
    {
        // Speeds (m/s). Analog stick magnitude scales the target, so a light push is a slow walk.
        public float walkSpeed = 4f;
        public float sprintSpeed = 6.5f;
        public float crouchSpeed = 2.5f;
        public float aimSpeedMultiplier = 0.75f;

        /// <summary>Sprint needs this much of the input pointing forward (0..1 of the normalized stick).</summary>
        public float sprintForwardThreshold = 0.5f;
        public float inputDeadzone = 0.1f;

        // Acceleration (m/s²)
        public float groundAcceleration = 40f;
        public float groundDeceleration = 50f;
        public float airAcceleration = 10f;

        // Vertical
        public float gravity = 20f;
        public float jumpHeight = 1.5f;
        public float terminalFallSpeed = 40f;
        /// <summary>Small downward speed while grounded so the capsule stays glued to slopes and steps.</summary>
        public float groundStickSpeed = 2f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        // Slide
        public float slideDuration = 0.7f;
        public float slideStartSpeed = 9f;
        public float slideEndSpeed = 2.5f;
        public float slideCooldown = 0.4f;
        /// <summary>Minimum horizontal speed to start a slide (in practice: sprinting).</summary>
        public float slideMinEntrySpeed = 5f;
        /// <summary>Pressing crouch while sprinting starts a slide (common mobile/PC convention).</summary>
        public bool crouchWhileSprintingSlides = true;

        // Capsule
        public float standingHeight = 1.8f;
        public float crouchHeight = 1.15f;
        public float capsuleRadius = 0.35f;

        /// <summary>Initial upward speed that reaches <see cref="jumpHeight"/>: v = sqrt(2 g h).</summary>
        public float JumpVelocity => MathF.Sqrt(2f * gravity * jumpHeight);

        public MovementSettings Clone() => (MovementSettings)MemberwiseClone();
    }
}
