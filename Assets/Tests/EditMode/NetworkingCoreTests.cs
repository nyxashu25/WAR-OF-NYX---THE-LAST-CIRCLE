using System;
using NUnit.Framework;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Tests
{
    public class ServerLaunchOptionsTests
    {
        [Test]
        public void DefaultsWithoutArguments()
        {
            var o = ServerLaunchOptions.Parse(new[] { "WarOfNyx.exe" });
            Assert.IsFalse(o.IsServer);
            Assert.AreEqual(ServerLaunchOptions.DefaultSession, o.SessionName);
            Assert.AreEqual(12, o.MaxPlayers);
            Assert.AreEqual(27015, o.Port);
            Assert.AreEqual(string.Empty, o.Warnings);
        }

        [Test]
        public void ParsesAFullServerCommandLine()
        {
            var o = ServerLaunchOptions.Parse(new[]
            {
                "WarOfNyxServer", "-batchmode", "-nographics", "-server", "-session", "eu-match-7",
                "-maxPlayers", "24", "-port", "30000", "-region", "EU", "-logFile", "-",
            });
            Assert.IsTrue(o.IsServer);
            Assert.AreEqual("EU-MATCH-7", o.SessionName);
            Assert.AreEqual(24, o.MaxPlayers);
            Assert.AreEqual(30000, o.Port);
            Assert.AreEqual("eu", o.Region);
            Assert.AreEqual(string.Empty, o.Warnings);
        }

        [TestCase("1")]
        [TestCase("61")]
        [TestCase("lots")]
        public void RejectsInvalidPlayerCounts(string value)
        {
            var o = ServerLaunchOptions.Parse(new[] { "-server", "-maxPlayers", value });
            Assert.AreEqual(ServerLaunchOptions.DefaultMaxPlayers, o.MaxPlayers);
            StringAssert.Contains("maxPlayers", o.Warnings);
        }

        [Test]
        public void RejectsInvalidPortAndSession()
        {
            var o = ServerLaunchOptions.Parse(new[] { "-server", "-port", "70000", "-session", "bad$name" });
            Assert.AreEqual(ServerLaunchOptions.DefaultPort, o.Port);
            Assert.AreEqual(ServerLaunchOptions.DefaultSession, o.SessionName);
            StringAssert.Contains("port", o.Warnings);
            StringAssert.Contains("session", o.Warnings);
        }

        [Test]
        public void MissingValueDoesNotSwallowTheNextFlag()
        {
            var o = ServerLaunchOptions.Parse(new[] { "-session", "-server" });
            Assert.IsTrue(o.IsServer);
            Assert.AreEqual(ServerLaunchOptions.DefaultSession, o.SessionName);
        }

        [Test]
        public void NullArgumentsAreSafe()
        {
            Assert.IsFalse(ServerLaunchOptions.Parse(null).IsServer);
        }
    }

    public class SessionCodeAndNameTests
    {
        [TestCase("abcd", "ABCD")]
        [TestCase("  won dev  ", "WON-DEV")]
        [TestCase("room_42", "ROOM-42")]
        public void SanitizesValidCodes(string input, string expected)
        {
            Assert.IsTrue(SessionCodes.TrySanitize(input, out var code));
            Assert.AreEqual(expected, code);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("ABCDEFGHIJKLMNOPQ")]
        [TestCase("hack;drop")]
        public void RejectsInvalidCodes(string input)
        {
            Assert.IsFalse(SessionCodes.TrySanitize(input, out _));
        }

        [Test]
        public void GeneratedCodesAreReadableAndValid()
        {
            var random = new Random(1234);
            for (int i = 0; i < 200; i++)
            {
                string code = SessionCodes.Generate(random);
                Assert.AreEqual(SessionCodes.GeneratedLength, code.Length);
                Assert.IsTrue(SessionCodes.TrySanitize(code, out var same));
                Assert.AreEqual(code, same);
                StringAssert.DoesNotMatch("[01OIL]", code);
            }
        }

        [Test]
        public void NamesAreCleanedAndLimited()
        {
            Assert.AreEqual("Kai", PlayerNames.Sanitize("  Kai  ", 1));
            Assert.AreEqual("Shadow Ronin", PlayerNames.Sanitize("Shadow    Ronin", 1));
            Assert.AreEqual("abcdefghijklmnop", PlayerNames.Sanitize("abcdefghijklmnopqrstuvwxyz", 1));
            Assert.AreEqual("bevilb", PlayerNames.Sanitize("<b>evil</b>", 1), "markup characters are stripped");
        }

        [TestCase(null, 7, "NYX-0007")]
        [TestCase("", 12, "NYX-0012")]
        [TestCase("!!", 3, "NYX-0003")]
        [TestCase("x", 3, "NYX-0003")]
        public void UnusableNamesFallBack(string input, int id, string expected)
        {
            Assert.AreEqual(expected, PlayerNames.Sanitize(input, id));
        }
    }

    public class InputCodecTests
    {
        [Test]
        public void ButtonsRoundTrip()
        {
            var original = new MovementCommand
            {
                MoveX = 0.25f, MoveY = -0.5f, Yaw = 123f, Pitch = -20f,
                SprintHeld = true, AimHeld = false, JumpPressed = true, CrouchPressed = false, SlidePressed = true,
            };
            uint bits = InputCodec.PackButtons(original);
            var decoded = InputCodec.Unpack(original.MoveX, original.MoveY, original.Yaw, original.Pitch, bits);
            Assert.AreEqual(original, decoded);
        }

        [Test]
        public void EveryButtonHasItsOwnBit()
        {
            Assert.AreEqual((uint)InputCodec.Buttons.Sprint, InputCodec.PackButtons(new MovementCommand { SprintHeld = true }));
            Assert.AreEqual((uint)InputCodec.Buttons.Aim, InputCodec.PackButtons(new MovementCommand { AimHeld = true }));
            Assert.AreEqual((uint)InputCodec.Buttons.Jump, InputCodec.PackButtons(new MovementCommand { JumpPressed = true }));
            Assert.AreEqual((uint)InputCodec.Buttons.Crouch, InputCodec.PackButtons(new MovementCommand { CrouchPressed = true }));
            Assert.AreEqual((uint)InputCodec.Buttons.Slide, InputCodec.PackButtons(new MovementCommand { SlidePressed = true }));
        }

        [Test]
        public void OversizedStickIsClampedOnTheServer()
        {
            // A modified client sending a 10x stick must not move faster.
            var cmd = InputCodec.Unpack(10f, 0f, 0f, 0f, 0u);
            Assert.AreEqual(1f, cmd.MoveX, 1e-5f);

            cmd = InputCodec.Unpack(3f, 4f, 0f, 0f, 0u);
            Assert.AreEqual(0.6f, cmd.MoveX, 1e-5f);
            Assert.AreEqual(0.8f, cmd.MoveY, 1e-5f);
        }

        [Test]
        public void NonFiniteValuesAreZeroed()
        {
            var cmd = InputCodec.Unpack(float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 0u);
            Assert.AreEqual(0f, cmd.MoveX);
            Assert.AreEqual(0f, cmd.MoveY);
            Assert.AreEqual(0f, cmd.Yaw);
            Assert.AreEqual(0f, cmd.Pitch);
        }

        [TestCase(370f, 10f)]
        [TestCase(-90f, 270f)]
        [TestCase(720f, 0f)]
        public void YawIsWrapped(float input, float expected)
        {
            Assert.AreEqual(expected, InputCodec.Unpack(0f, 0f, input, 0f, 0u).Yaw, 1e-4f);
        }

        [Test]
        public void PitchIsClamped()
        {
            Assert.AreEqual(InputCodec.MaxPitch, InputCodec.Unpack(0f, 0f, 0f, 500f, 0u).Pitch);
            Assert.AreEqual(InputCodec.MinPitch, InputCodec.Unpack(0f, 0f, 0f, -500f, 0u).Pitch);
        }

        [Test]
        public void UnknownBitsAreIgnored()
        {
            var cmd = InputCodec.Unpack(0f, 0f, 0f, 0f, 0xFFFFFFE0u);
            Assert.IsFalse(cmd.SprintHeld || cmd.AimHeld || cmd.JumpPressed || cmd.CrouchPressed || cmd.SlidePressed);
        }

        [Test]
        public void NeutralCommandKeepsFacing()
        {
            var cmd = InputCodec.Neutral(45f, 10f);
            Assert.AreEqual(0f, cmd.MoveX);
            Assert.AreEqual(0f, cmd.MoveY);
            Assert.AreEqual(45f, cmd.Yaw);
            Assert.IsFalse(cmd.JumpPressed);
        }
    }

    public class SpawnRingTests
    {
        [Test]
        public void SlotsAreEvenlySeparated()
        {
            const int slots = 12;
            const float radius = 20f;
            float expected = SpawnRing.NeighbourDistance(slots, radius);
            for (int i = 0; i < slots; i++)
            {
                SpawnRing.Slot(i, slots, radius, out float ax, out float az, out _);
                SpawnRing.Slot(i + 1, slots, radius, out float bx, out float bz, out _);
                float d = MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
                Assert.AreEqual(expected, d, 1e-3f);
                Assert.AreEqual(radius, MathF.Sqrt(ax * ax + az * az), 1e-3f);
            }
        }

        [Test]
        public void SpawnsFaceTheCentre()
        {
            SpawnRing.Slot(0, 4, 10f, out float x, out float z, out float yaw);
            Assert.AreEqual(0f, x, 1e-4f);
            Assert.AreEqual(10f, z, 1e-4f);
            Assert.AreEqual(180f, yaw, 1e-3f, "north slot looks south");

            SpawnRing.Slot(1, 4, 10f, out x, out _, out yaw);
            Assert.AreEqual(10f, x, 1e-4f);
            Assert.AreEqual(270f, yaw, 1e-3f, "east slot looks west");
        }

        [Test]
        public void IndexWrapsAround()
        {
            SpawnRing.Slot(5, 4, 10f, out float ax, out float az, out _);
            SpawnRing.Slot(1, 4, 10f, out float bx, out float bz, out _);
            Assert.AreEqual(bx, ax, 1e-5f);
            Assert.AreEqual(bz, az, 1e-5f);
        }
    }
}
