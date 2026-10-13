using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using WarOfNyx.App;
using WarOfNyx.Controls;
using WarOfNyx.Core.Offline;
using WarOfNyx.Settings;

namespace WarOfNyx.Tests
{
    public class GameConfigTests
    {
        [Test]
        public void DefaultsMatchTheGhostProtocolConfiguration()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(23, config.defaultOfflineBotCount);
                Assert.AreEqual(BotDifficulty.Nightmare, config.defaultOfflineDifficulty);
                Assert.IsTrue(config.DefaultOfflineSettings.IsValid);
                Assert.AreEqual("MainMenu", config.mainMenuScene);
                Assert.AreEqual(30f, config.matchmakingOfflineOfferSeconds);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }

    public class GraphicsQualityTests
    {
        [Test]
        public void FindsLevelsByName()
        {
            var names = new[] { "Low", "Medium", "High" };
            Assert.AreEqual(0, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.Low, names));
            Assert.AreEqual(1, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.Medium, names));
            Assert.AreEqual(2, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.High, names));
        }

        [Test]
        public void FallsBackToPositionWithUnityDefaultNames()
        {
            var names = new[] { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" };
            Assert.AreEqual(1, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.Low, names));

            var unnamed = new[] { "A", "B", "C", "D", "E" };
            Assert.AreEqual(0, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.Low, unnamed));
            Assert.AreEqual(2, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.Medium, unnamed));
            Assert.AreEqual(4, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.High, unnamed));
        }

        [Test]
        public void EmptyLevelListIsHandled()
        {
            Assert.AreEqual(-1, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.High, new string[0]));
            Assert.AreEqual(-1, GraphicsQualityService.FindQualityLevel(GraphicsQualityTier.High, null));
        }

        [Test]
        public void FrameRateTargetsMatchSpec()
        {
            Assert.AreEqual(30, GraphicsQualityService.TargetFrameRate(GraphicsQualityTier.Low));
            Assert.AreEqual(45, GraphicsQualityService.TargetFrameRate(GraphicsQualityTier.Medium));
            Assert.AreEqual(60, GraphicsQualityService.TargetFrameRate(GraphicsQualityTier.High));
        }
    }

    public class InputActionsTests
    {
        private InputActionAsset _asset;

        [SetUp]
        public void SetUp() => _asset = GameInputActions.CreateDefaultAsset();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        [Test]
        public void EveryGameplayActionExists()
        {
            var map = _asset.FindActionMap(GameInputActions.PlayerMap, throwIfNotFound: true);
            foreach (var name in GameInputActions.AllPlayerActions)
                Assert.IsNotNull(map.FindAction(name), name);
        }

        // Desktop development bindings from spec section 18.
        [TestCase(GameInputActions.Fire, "<Mouse>/leftButton")]
        [TestCase(GameInputActions.Aim, "<Mouse>/rightButton")]
        [TestCase(GameInputActions.Sprint, "<Keyboard>/leftShift")]
        [TestCase(GameInputActions.Jump, "<Keyboard>/space")]
        [TestCase(GameInputActions.Crouch, "<Keyboard>/c")]
        [TestCase(GameInputActions.Reload, "<Keyboard>/r")]
        [TestCase(GameInputActions.Interact, "<Keyboard>/e")]
        [TestCase(GameInputActions.Melee, "<Keyboard>/f")]
        [TestCase(GameInputActions.WeaponSlot1, "<Keyboard>/1")]
        [TestCase(GameInputActions.WeaponSlot2, "<Keyboard>/2")]
        [TestCase(GameInputActions.WeaponSlot3, "<Keyboard>/3")]
        [TestCase(GameInputActions.Medkit, "<Keyboard>/h")]
        public void DesktopBindingMatchesSpec(string action, string path)
        {
            var bindings = _asset.FindAction($"{GameInputActions.PlayerMap}/{action}", throwIfNotFound: true).bindings;
            bool found = false;
            foreach (var b in bindings)
                found |= b.path == path;
            Assert.IsTrue(found, $"{action} should be bound to {path}");
        }

        [Test]
        public void MoveUsesWasd()
        {
            var bindings = _asset.FindAction($"{GameInputActions.PlayerMap}/{GameInputActions.Move}", true).bindings;
            var paths = new System.Collections.Generic.List<string>();
            foreach (var b in bindings)
                paths.Add(b.path);

            CollectionAssert.IsSubsetOf(new[] { "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d" }, paths);
        }

        [Test]
        public void EveryActionHasATouchCompatibleGamepadBinding()
        {
            // On-screen controls emit Gamepad controls, so a gamepad binding is what makes an action usable on Android.
            foreach (var name in GameInputActions.AllPlayerActions)
            {
                bool hasGamepad = false;
                foreach (var b in _asset.FindAction($"{GameInputActions.PlayerMap}/{name}", true).bindings)
                    hasGamepad |= b.path != null && b.path.StartsWith("<Gamepad>");
                Assert.IsTrue(hasGamepad, name);
            }
        }
    }
}
