using System;

namespace WarOfNyx.Core.Movement
{
    /// <summary>
    /// Deterministic movement state machine (master spec section 8): Standing, Crouching,
    /// Sliding, Airborne. It turns (previous state, command, ground facts, dt) into the next
    /// state and the velocity the physics layer should try to apply.
    ///
    /// Collision is NOT handled here. The caller moves a capsule (Unity CharacterController now,
    /// a Fusion KCC in Phase 3) and then feeds back what actually happened via
    /// <see cref="ApplyCollisionResult"/>. Keeping this pure lets the same code run on the
    /// dedicated server, in client prediction, in the offline simulation and in unit tests.
    ///
    /// Each step runs in a fixed order so states can't fight each other:
    ///   1 timers → 2 ground enter/leave → 3 crouch/slide input → 4 slide end → 5 jump
    ///   → 6 sprint → 7 horizontal velocity → 8 vertical velocity
    /// </summary>
    public static class MovementModel
    {
        public static MovementState Step(MovementState previous, in MovementCommand command, in GroundInfo ground,
            MovementSettings settings, float dt)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var s = previous;
            s.JumpedThisStep = false;
            s.LandedThisStep = false;
            s.SlideStartedThisStep = false;
            s.Yaw = command.Yaw;

            if (dt <= 0f)
                return s;

            // 1. Timers and buffered input
            s.SlideCooldownRemaining = MathF.Max(0f, s.SlideCooldownRemaining - dt);
            s.CoyoteTimeRemaining = MathF.Max(0f, s.CoyoteTimeRemaining - dt);
            s.JumpBufferRemaining = MathF.Max(0f, s.JumpBufferRemaining - dt);
            if (command.JumpPressed)
                s.JumpBufferRemaining = settings.jumpBufferTime;

            ReadMoveInput(command, settings, out float inputX, out float inputZ, out float inputMagnitude, out float forwardFraction);

            // 2. Ground enter / leave
            if (s.Mode == LocomotionMode.Airborne)
            {
                if (ground.IsGrounded && s.VelocityY <= 0f)
                {
                    s.Mode = s.CrouchToggled ? LocomotionMode.Crouching : LocomotionMode.Standing;
                    s.LandedThisStep = true;
                }
            }

            if (s.Mode != LocomotionMode.Airborne)
            {
                if (ground.IsGrounded)
                    s.CoyoteTimeRemaining = settings.coyoteTime;
                else if (s.CoyoteTimeRemaining <= 0f)
                {
                    s.Mode = LocomotionMode.Airborne;
                    s.SlideTimeRemaining = 0f;
                }
            }

            // 3. Crouch toggle / slide request
            bool slideRequested = command.SlidePressed;
            if (command.CrouchPressed)
            {
                if (s.Mode == LocomotionMode.Standing && s.IsSprinting && settings.crouchWhileSprintingSlides)
                    slideRequested = true;
                else if (s.Mode != LocomotionMode.Sliding)
                    s.CrouchToggled = !s.CrouchToggled;
            }

            if (slideRequested && CanStartSlide(s, ground, settings))
                StartSlide(ref s, settings);

            switch (s.Mode)
            {
                case LocomotionMode.Standing when s.CrouchToggled:
                    s.Mode = LocomotionMode.Crouching;
                    break;
                case LocomotionMode.Crouching when !s.CrouchToggled && ground.CanStand:
                    s.Mode = LocomotionMode.Standing;
                    break;
            }

            // 4. Slide end
            if (s.Mode == LocomotionMode.Sliding && !s.SlideStartedThisStep)
            {
                s.SlideTimeRemaining -= dt;
                if (s.SlideTimeRemaining <= 0f)
                {
                    s.SlideTimeRemaining = 0f;
                    s.Mode = ground.CanStand && !s.CrouchToggled ? LocomotionMode.Standing : LocomotionMode.Crouching;
                }
            }

            // 5. Jump (grounded or within coyote time; needs headroom when crouched/sliding)
            if (s.JumpBufferRemaining > 0f && s.Mode != LocomotionMode.Airborne && s.CoyoteTimeRemaining > 0f
                && (!s.IsLowStance || ground.CanStand))
            {
                s.Mode = LocomotionMode.Airborne;
                s.VelocityY = settings.JumpVelocity;
                s.JumpBufferRemaining = 0f;
                s.CoyoteTimeRemaining = 0f;
                s.SlideTimeRemaining = 0f;
                s.CrouchToggled = false;
                s.JumpedThisStep = true;
            }

            // 6. Sprint: only standing, moving mostly forward, not aiming
            s.IsSprinting = s.Mode == LocomotionMode.Standing && command.SprintHeld && !command.AimHeld
                            && inputMagnitude > 0f && forwardFraction >= settings.sprintForwardThreshold;

            // 7. Horizontal velocity
            switch (s.Mode)
            {
                case LocomotionMode.Standing:
                case LocomotionMode.Crouching:
                {
                    float speed = s.Mode == LocomotionMode.Crouching ? settings.crouchSpeed
                        : s.IsSprinting ? settings.sprintSpeed
                        : settings.walkSpeed;
                    if (command.SpeedMultiplier > 0f)
                        speed *= command.SpeedMultiplier;
                    if (command.AimHeld)
                        speed *= settings.aimSpeedMultiplier;

                    float targetX = inputX * speed;
                    float targetZ = inputZ * speed;
                    float targetSpeed = speed * inputMagnitude;
                    float rate = targetSpeed >= s.HorizontalSpeed ? settings.groundAcceleration : settings.groundDeceleration;
                    MoveTowards(ref s.VelocityX, ref s.VelocityZ, targetX, targetZ, rate * dt);
                    break;
                }
                case LocomotionMode.Sliding:
                {
                    float t = 1f - s.SlideTimeRemaining / MathF.Max(settings.slideDuration, 0.0001f);
                    float speed = Lerp(settings.slideStartSpeed, settings.slideEndSpeed, Clamp01(t));
                    s.VelocityX = s.SlideDirX * speed;
                    s.VelocityZ = s.SlideDirZ * speed;
                    break;
                }
                case LocomotionMode.Airborne:
                {
                    // Air control: steer toward the input, never faster than walking or the take-off speed.
                    if (inputMagnitude > 0f)
                    {
                        float maxSpeed = MathF.Max(settings.walkSpeed, s.HorizontalSpeed);
                        MoveTowards(ref s.VelocityX, ref s.VelocityZ, inputX * maxSpeed, inputZ * maxSpeed,
                            settings.airAcceleration * dt);
                    }
                    break;
                }
            }

            // 8. Vertical velocity
            if (s.Mode == LocomotionMode.Airborne || !ground.IsGrounded)
            {
                s.VelocityY -= settings.gravity * dt;
                if (s.VelocityY < -settings.terminalFallSpeed)
                    s.VelocityY = -settings.terminalFallSpeed;
            }
            else
            {
                s.VelocityY = -settings.groundStickSpeed;
            }

            return s;
        }

        /// <summary>
        /// Called after the physics layer moved the capsule. Replaces the intended velocity with
        /// what really happened, so speed doesn't keep building against walls or ceilings.
        /// </summary>
        public static void ApplyCollisionResult(ref MovementState state, float actualVelocityX, float actualVelocityZ, bool hitCeiling)
        {
            // Only ever reduce horizontal speed (collision can block, not accelerate).
            // Small losses (stairs, step-offset climbs, slope projection) are ignored so movement
            // doesn't feel sticky; only a real block (more than 10 % lost) feeds back.
            float intended = state.HorizontalSpeed;
            float actual = MathF.Sqrt(actualVelocityX * actualVelocityX + actualVelocityZ * actualVelocityZ);
            if (actual < intended * 0.9f - 0.01f)
            {
                state.VelocityX = actualVelocityX;
                state.VelocityZ = actualVelocityZ;
                if (state.Mode == LocomotionMode.Sliding && actual < 0.5f)
                    state.SlideTimeRemaining = 0f; // slid into a wall: end the slide next step
            }

            if (hitCeiling && state.VelocityY > 0f)
                state.VelocityY = 0f;
        }

        /// <summary>Capsule height for the current stance.</summary>
        public static float CapsuleHeight(in MovementState state, MovementSettings settings) =>
            state.IsLowStance ? settings.crouchHeight : settings.standingHeight;

        /// <summary>World-space unit vectors for a yaw angle (Unity convention: 0° = +Z, 90° = +X).</summary>
        public static void YawToBasis(float yawDegrees, out float forwardX, out float forwardZ, out float rightX, out float rightZ)
        {
            float rad = yawDegrees * (MathF.PI / 180f);
            forwardX = MathF.Sin(rad);
            forwardZ = MathF.Cos(rad);
            rightX = forwardZ;
            rightZ = -forwardX;
        }

        private static bool CanStartSlide(in MovementState s, in GroundInfo ground, MovementSettings settings) =>
            s.Mode == LocomotionMode.Standing
            && ground.IsGrounded
            && s.SlideCooldownRemaining <= 0f
            && s.HorizontalSpeed >= settings.slideMinEntrySpeed;

        private static void StartSlide(ref MovementState s, MovementSettings settings)
        {
            float speed = s.HorizontalSpeed;
            s.SlideDirX = s.VelocityX / speed;
            s.SlideDirZ = s.VelocityZ / speed;
            s.Mode = LocomotionMode.Sliding;
            s.SlideTimeRemaining = settings.slideDuration;
            s.SlideCooldownRemaining = settings.slideDuration + settings.slideCooldown;
            s.IsSprinting = false;
            s.SlideStartedThisStep = true;
        }

        /// <summary>Converts the local stick to a world direction scaled by stick magnitude (0..1).</summary>
        private static void ReadMoveInput(in MovementCommand command, MovementSettings settings,
            out float worldX, out float worldZ, out float magnitude, out float forwardFraction)
        {
            float x = command.MoveX;
            float y = command.MoveY;
            if (float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
            if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;

            magnitude = MathF.Sqrt(x * x + y * y);
            if (magnitude < settings.inputDeadzone)
            {
                worldX = worldZ = magnitude = forwardFraction = 0f;
                return;
            }

            if (magnitude > 1f)
            {
                x /= magnitude;
                y /= magnitude;
                magnitude = 1f;
            }

            forwardFraction = y / magnitude;
            YawToBasis(command.Yaw, out float fx, out float fz, out float rx, out float rz);
            worldX = rx * x + fx * y;
            worldZ = rz * x + fz * y;

            // Outside-circle instability: bend the direction sideways, keep the length (no speed change).
            float wobble = command.LateralWobble;
            if (wobble != 0f && !float.IsNaN(wobble) && !float.IsInfinity(wobble))
            {
                wobble = Math.Clamp(wobble, -0.25f, 0.25f);
                float bentX = worldX + worldZ * wobble;
                float bentZ = worldZ - worldX * wobble;
                float norm = 1f / MathF.Sqrt(1f + wobble * wobble);
                worldX = bentX * norm;
                worldZ = bentZ * norm;
            }
        }

        private static void MoveTowards(ref float x, ref float z, float targetX, float targetZ, float maxDelta)
        {
            float dx = targetX - x;
            float dz = targetZ - z;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= maxDelta || distance < 1e-6f)
            {
                x = targetX;
                z = targetZ;
                return;
            }

            x += dx / distance * maxDelta;
            z += dz / distance * maxDelta;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
