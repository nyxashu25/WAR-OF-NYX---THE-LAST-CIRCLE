using NUnit.Framework;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Tests
{
    /// <summary>
    /// Movement state machine tests. A tiny fake world stands in for physics:
    /// a flat floor at y = 0 and an optional low ceiling that blocks standing up.
    /// </summary>
    public class MovementModelTests
    {
        private const float Dt = 1f / 60f;
        private const float SpeedTolerance = 0.05f;

        private MovementSettings _settings;
        private Sim _sim;

        [SetUp]
        public void SetUp()
        {
            _settings = new MovementSettings();
            _sim = new Sim(_settings);
        }

        private sealed class Sim
        {
            private readonly MovementSettings _settings;
            public MovementState State = MovementState.Initial(0f);
            public float Y;
            public float MaxY;
            public bool Grounded = true;
            public bool LowCeiling;
            /// <summary>When false the floor is gone (walked off a ledge).</summary>
            public bool FloorExists = true;

            public Sim(MovementSettings settings) => _settings = settings;

            public MovementState Step(MovementCommand cmd)
            {
                State = MovementModel.Step(State, cmd, new GroundInfo(Grounded, !LowCeiling), _settings, Dt);
                Y += State.VelocityY * Dt;
                if (FloorExists && Y <= 0f)
                {
                    Y = 0f;
                    Grounded = true;
                }
                else
                {
                    Grounded = false;
                }

                if (Y > MaxY)
                    MaxY = Y;
                return State;
            }

            public MovementState Run(MovementCommand cmd, float seconds)
            {
                int steps = (int)System.Math.Round(seconds / Dt);
                for (int i = 0; i < steps; i++)
                    Step(cmd);
                return State;
            }
        }

        private static MovementCommand Forward(bool sprint = false, bool aim = false) =>
            new MovementCommand { MoveY = 1f, SprintHeld = sprint, AimHeld = aim };

        // ------------------------------------------------------------ speeds

        [Test]
        public void WalkReachesWalkSpeed()
        {
            var s = _sim.Run(Forward(), 1f);
            Assert.AreEqual(LocomotionMode.Standing, s.Mode);
            Assert.AreEqual(4f, s.HorizontalSpeed, SpeedTolerance);
            Assert.IsFalse(s.IsSprinting);
        }

        [Test]
        public void SprintReachesSprintSpeed()
        {
            var s = _sim.Run(Forward(sprint: true), 1f);
            Assert.IsTrue(s.IsSprinting);
            Assert.AreEqual(6.5f, s.HorizontalSpeed, SpeedTolerance);
        }

        [Test]
        public void SprintRequiresMovingForward()
        {
            var s = _sim.Run(new MovementCommand { MoveY = -1f, SprintHeld = true }, 1f);
            Assert.IsFalse(s.IsSprinting);
            Assert.AreEqual(4f, s.HorizontalSpeed, SpeedTolerance);

            s = _sim.Run(new MovementCommand { MoveX = 1f, SprintHeld = true }, 1f);
            Assert.IsFalse(s.IsSprinting, "pure strafe cannot sprint");
        }

        [Test]
        public void AimingCancelsSprintAndSlowsDown()
        {
            var s = _sim.Run(Forward(sprint: true, aim: true), 1f);
            Assert.IsFalse(s.IsSprinting);
            Assert.AreEqual(4f * 0.75f, s.HorizontalSpeed, SpeedTolerance);
        }

        [Test]
        public void AnalogStickScalesSpeed()
        {
            var s = _sim.Run(new MovementCommand { MoveY = 0.5f }, 1f);
            Assert.AreEqual(2f, s.HorizontalSpeed, SpeedTolerance);
        }

        [Test]
        public void DiagonalInputIsNotFaster()
        {
            var s = _sim.Run(new MovementCommand { MoveX = 1f, MoveY = 1f }, 1f);
            Assert.AreEqual(4f, s.HorizontalSpeed, SpeedTolerance);
        }

        [Test]
        public void InputInsideDeadzoneIsIgnored()
        {
            var s = _sim.Run(new MovementCommand { MoveY = 0.05f }, 0.5f);
            Assert.AreEqual(0f, s.HorizontalSpeed, 1e-4f);
        }

        [Test]
        public void ReleasingInputStops()
        {
            _sim.Run(Forward(sprint: true), 1f);
            var s = _sim.Run(default, 0.5f);
            Assert.AreEqual(0f, s.HorizontalSpeed, 1e-4f);
        }

        [Test]
        public void YawRotatesMovement()
        {
            var s = _sim.Run(new MovementCommand { MoveY = 1f, Yaw = 90f }, 1f);
            Assert.AreEqual(4f, s.VelocityX, SpeedTolerance, "yaw 90 forward = +X");
            Assert.AreEqual(0f, s.VelocityZ, SpeedTolerance);

            s = _sim.Run(new MovementCommand { MoveX = 1f, Yaw = 0f }, 1f);
            Assert.AreEqual(4f, s.VelocityX, SpeedTolerance, "yaw 0 strafe right = +X");
        }

        [Test]
        public void InvalidInputIsSanitized()
        {
            var s = _sim.Run(new MovementCommand { MoveX = float.NaN, MoveY = float.PositiveInfinity }, 0.2f);
            Assert.IsFalse(float.IsNaN(s.VelocityX) || float.IsNaN(s.VelocityZ));
            Assert.AreEqual(0f, s.HorizontalSpeed, 1e-4f);
        }

        // ------------------------------------------------------------ crouch

        [Test]
        public void CrouchToggles()
        {
            _sim.Step(new MovementCommand { CrouchPressed = true });
            var s = _sim.Run(Forward(), 1f);
            Assert.AreEqual(LocomotionMode.Crouching, s.Mode);
            Assert.AreEqual(2.5f, s.HorizontalSpeed, SpeedTolerance);
            Assert.AreEqual(_settings.crouchHeight, MovementModel.CapsuleHeight(s, _settings));

            _sim.Step(new MovementCommand { MoveY = 1f, CrouchPressed = true });
            s = _sim.Run(Forward(), 1f);
            Assert.AreEqual(LocomotionMode.Standing, s.Mode);
            Assert.AreEqual(_settings.standingHeight, MovementModel.CapsuleHeight(s, _settings));
        }

        [Test]
        public void CannotStandUpWithoutHeadroom()
        {
            _sim.Step(new MovementCommand { CrouchPressed = true });
            _sim.LowCeiling = true;
            _sim.Step(new MovementCommand { CrouchPressed = true }); // try to stand
            var s = _sim.Run(Forward(), 0.5f);
            Assert.AreEqual(LocomotionMode.Crouching, s.Mode, "blocked by the ceiling");

            _sim.LowCeiling = false;
            s = _sim.Step(default);
            Assert.AreEqual(LocomotionMode.Standing, s.Mode, "stands automatically once there is room");
        }

        [Test]
        public void CannotJumpWithoutHeadroom()
        {
            _sim.Step(new MovementCommand { CrouchPressed = true });
            _sim.LowCeiling = true;
            var s = _sim.Step(new MovementCommand { JumpPressed = true });
            Assert.AreNotEqual(LocomotionMode.Airborne, s.Mode);
            Assert.IsFalse(s.JumpedThisStep);
        }

        // ------------------------------------------------------------ jump

        [Test]
        public void JumpReachesConfiguredHeightAndLands()
        {
            var s = _sim.Step(new MovementCommand { JumpPressed = true });
            Assert.IsTrue(s.JumpedThisStep);
            Assert.AreEqual(LocomotionMode.Airborne, s.Mode);

            bool landed = false;
            for (int i = 0; i < 120 && !landed; i++)
                landed = _sim.Step(default).LandedThisStep;

            Assert.IsTrue(landed, "landed within 2 s");
            Assert.AreEqual(1.5f, _sim.MaxY, 0.1f, "apex height");
            Assert.AreEqual(LocomotionMode.Standing, _sim.State.Mode);
        }

        [Test]
        public void NoDoubleJump()
        {
            _sim.Step(new MovementCommand { JumpPressed = true });
            _sim.Run(default, 0.2f); // rising, buffer expired
            float vy = _sim.State.VelocityY;
            var s = _sim.Step(new MovementCommand { JumpPressed = true });
            Assert.IsFalse(s.JumpedThisStep);
            Assert.Less(s.VelocityY, vy);
        }

        [Test]
        public void CoyoteTimeAllowsLateJump()
        {
            _sim.Run(Forward(), 0.5f);
            _sim.FloorExists = false; // walked off a ledge
            _sim.Run(Forward(), 0.05f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, JumpPressed = true });
            Assert.IsTrue(s.JumpedThisStep);
        }

        [Test]
        public void NoJumpAfterCoyoteTimeExpires()
        {
            _sim.Run(Forward(), 0.5f);
            _sim.FloorExists = false;
            _sim.Run(Forward(), 0.25f);
            Assert.AreEqual(LocomotionMode.Airborne, _sim.State.Mode);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, JumpPressed = true });
            Assert.IsFalse(s.JumpedThisStep);
        }

        [Test]
        public void BufferedJumpFiresOnLanding()
        {
            _sim.Step(new MovementCommand { JumpPressed = true });
            // Fall until just before landing, then press jump slightly early.
            for (int i = 0; i < 200; i++)
            {
                _sim.Step(default);
                if (_sim.State.VelocityY < 0f && _sim.Y < 0.25f)
                    break;
            }

            _sim.Step(new MovementCommand { JumpPressed = true });
            bool jumpedAgain = false;
            for (int i = 0; i < 10 && !jumpedAgain; i++)
                jumpedAgain = _sim.Step(default).JumpedThisStep;
            Assert.IsTrue(jumpedAgain);
        }

        [Test]
        public void SprintMomentumIsKeptInTheAir()
        {
            _sim.Run(Forward(sprint: true), 1f);
            _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, JumpPressed = true });
            var s = _sim.Run(Forward(sprint: true), 0.3f);
            Assert.AreEqual(LocomotionMode.Airborne, s.Mode);
            Assert.AreEqual(6.5f, s.HorizontalSpeed, SpeedTolerance);
        }

        // ------------------------------------------------------------ slide

        [Test]
        public void SlideFromSprintLastsSlideDuration()
        {
            _sim.Run(Forward(sprint: true), 1f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            Assert.IsTrue(s.SlideStartedThisStep);
            Assert.AreEqual(LocomotionMode.Sliding, s.Mode);
            Assert.AreEqual(9f, s.HorizontalSpeed, 0.2f);
            Assert.AreEqual(_settings.crouchHeight, MovementModel.CapsuleHeight(s, _settings));

            int slideSteps = 1;
            while (_sim.Step(Forward(sprint: true)).Mode == LocomotionMode.Sliding && slideSteps < 200)
                slideSteps++;

            Assert.AreEqual(0.7f, slideSteps * Dt, 2 * Dt, "slide duration");
            Assert.AreEqual(LocomotionMode.Standing, _sim.State.Mode);
        }

        [Test]
        public void CrouchWhileSprintingStartsSlide()
        {
            _sim.Run(Forward(sprint: true), 1f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, CrouchPressed = true });
            Assert.AreEqual(LocomotionMode.Sliding, s.Mode);
            Assert.IsFalse(s.CrouchToggled, "a slide is not a crouch toggle");
        }

        [Test]
        public void CannotSlideFromWalking()
        {
            _sim.Run(Forward(), 1f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, SlidePressed = true });
            Assert.AreNotEqual(LocomotionMode.Sliding, s.Mode);
        }

        [Test]
        public void SlideHasCooldown()
        {
            _sim.Run(Forward(sprint: true), 1f);
            _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            while (_sim.Step(Forward(sprint: true)).Mode == LocomotionMode.Sliding) { }

            _sim.Run(Forward(sprint: true), 0.2f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            Assert.AreNotEqual(LocomotionMode.Sliding, s.Mode, "still cooling down");

            _sim.Run(Forward(sprint: true), 0.5f);
            s = _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            Assert.AreEqual(LocomotionMode.Sliding, s.Mode, "cooldown over");
        }

        [Test]
        public void SlideEndsInCrouchUnderLowCeiling()
        {
            _sim.Run(Forward(sprint: true), 1f);
            _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            _sim.LowCeiling = true; // slid into a tunnel
            _sim.Run(Forward(sprint: true), 1f);
            Assert.AreEqual(LocomotionMode.Crouching, _sim.State.Mode);
        }

        [Test]
        public void SlidingIntoAWallEndsTheSlide()
        {
            _sim.Run(Forward(sprint: true), 1f);
            var s = _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            MovementModel.ApplyCollisionResult(ref s, 0f, 0f, false);
            _sim.State = s;
            s = _sim.Step(Forward(sprint: true));
            Assert.AreNotEqual(LocomotionMode.Sliding, s.Mode);
        }

        [Test]
        public void JumpCancelsSlide()
        {
            _sim.Run(Forward(sprint: true), 1f);
            _sim.Step(new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true });
            var s = _sim.Step(new MovementCommand { MoveY = 1f, JumpPressed = true });
            Assert.IsTrue(s.JumpedThisStep);
            Assert.AreEqual(LocomotionMode.Airborne, s.Mode);
        }

        // ------------------------------------------------------------ collision feedback / determinism

        [Test]
        public void CollisionOnlyReducesSpeed()
        {
            var s = _sim.Run(Forward(), 1f);
            MovementModel.ApplyCollisionResult(ref s, 0f, 10f, false);
            Assert.AreEqual(4f, s.HorizontalSpeed, SpeedTolerance, "collision never adds speed");

            MovementModel.ApplyCollisionResult(ref s, 0f, 1f, false);
            Assert.AreEqual(1f, s.HorizontalSpeed, SpeedTolerance);
        }

        [Test]
        public void CeilingStopsUpwardVelocity()
        {
            var s = _sim.Step(new MovementCommand { JumpPressed = true });
            MovementModel.ApplyCollisionResult(ref s, s.VelocityX, s.VelocityZ, hitCeiling: true);
            Assert.AreEqual(0f, s.VelocityY);
        }

        [Test]
        public void SameInputsGiveIdenticalResults()
        {
            var commands = new[]
            {
                Forward(sprint: true), new MovementCommand { MoveY = 1f, SprintHeld = true, SlidePressed = true },
                new MovementCommand { MoveX = 0.3f, MoveY = 0.8f, Yaw = 33f, JumpPressed = true },
                new MovementCommand { MoveX = -1f, Yaw = 270f, CrouchPressed = true },
            };

            var a = new Sim(_settings);
            var b = new Sim(_settings);
            for (int i = 0; i < 600; i++)
            {
                var cmd = commands[(i / 37) % commands.Length];
                var sa = a.Step(cmd);
                var sb = b.Step(cmd);
                Assert.AreEqual(sa, sb, $"diverged at step {i}");
            }
        }

        [Test]
        public void ZeroDeltaTimeChangesNothing()
        {
            var before = _sim.Run(Forward(sprint: true), 0.5f);
            var after = MovementModel.Step(before, Forward(sprint: true), new GroundInfo(true, true), _settings, 0f);
            Assert.AreEqual(before.VelocityX, after.VelocityX);
            Assert.AreEqual(before.VelocityZ, after.VelocityZ);
            Assert.AreEqual(before.Mode, after.Mode);
        }
    }
}
