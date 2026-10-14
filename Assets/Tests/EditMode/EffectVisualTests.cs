using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Tests
{
    /// <summary>Show / hide state of the per-character effect visuals.</summary>
    public class EffectVisualTests
    {
        private GameObject _character;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _character = new GameObject("Character");
            _material = new Material(Shader.Find("Sprites/Default"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_character);
            Object.DestroyImmediate(_material);
        }

        private int EnabledLines() =>
            System.Array.FindAll(_character.GetComponentsInChildren<LineRenderer>(true), l => l.enabled).Length;

        [Test]
        public void HuntBeacon_IsHiddenWhenCreated()
        {
            // Regression: the beacon used to be built visible and Attach's SetActive(false) was a no-op,
            // so every character showed a beacon from spawn.
            HuntBeaconVisual.Create(_character.transform, _material);
            Assert.That(_character.GetComponentsInChildren<LineRenderer>(true).Length, Is.GreaterThan(0));
            Assert.That(EnabledLines(), Is.EqualTo(0));
        }

        [Test]
        public void HuntBeacon_ShowsOnlyWhileExposed()
        {
            var beacon = HuntBeaconVisual.Create(_character.transform, _material);
            beacon.SetActive(true);
            Assert.That(EnabledLines(), Is.EqualTo(4));
            beacon.SetActive(false);
            Assert.That(EnabledLines(), Is.EqualTo(0));
        }
    }
}
