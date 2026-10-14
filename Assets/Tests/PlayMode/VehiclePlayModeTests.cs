using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WarOfNyx.Core.Vehicles;
using WarOfNyx.Gameplay.Offline;

namespace WarOfNyx.Tests.PlayMode
{
    /// <summary>
    /// Vehicle physics on flat ground (no match, no rider): every vehicle settles on its wheels, accelerates toward
    /// its top speed in a straight line, turns the way it is steered, and brakes.
    /// </summary>
    public class VehiclePlayModeTests
    {
        private GameObject _ground;

        [UnitySetUp]
        public IEnumerator EmptyWorld()
        {
            yield return TestWorld.Fresh();
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(600f, 1f, 600f);
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            foreach (var v in Object.FindObjectsByType<OfflineVehicle>(FindObjectsSortMode.None))
                Object.Destroy(v.gameObject);
            if (_ground != null)
                Object.Destroy(_ground);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryVehicle_Settles_Drives_Turns_AndBrakes([Values(0, 1, 2)] int type)
        {
            var v = OfflineVehicle.Spawn(null, type, 0, Vector3.zero, 0f);
            v.HoldBrakeWhenEmpty = false;
            yield return new WaitForSeconds(1.5f);
            Assert.That(v.transform.up.y, Is.GreaterThan(0.97f), "upright on its wheels");
            Assert.That(v.transform.position.y, Is.InRange(-0.15f, 0.25f), "resting on the ground");
            Assert.That(v.Grounded, "wheels on the ground");

            var start = v.transform.position;
            v.SetInput(1f, 0f, false);
            yield return new WaitForSeconds(4f);
            float speed = v.Speed;
            TestContext.WriteLine($"{v.Stats.Id}: {speed * 3.6f:0} km/h after 4 s, {(v.transform.position - start).magnitude:0.0} m");
            Assert.That(speed, Is.GreaterThan(v.Stats.TopSpeed * 0.45f), "accelerates");
            Assert.That(speed, Is.LessThanOrEqualTo(v.Stats.TopSpeed * 1.05f), "top speed respected");
            Assert.That(Vector3.Dot(v.transform.position - start, Vector3.forward), Is.GreaterThan(20f), "drove forward");
            Assert.That(Mathf.Abs(v.transform.position.x - start.x), Is.LessThan(3f), "in a straight line");

            float yaw = v.transform.eulerAngles.y;
            v.SetInput(0.6f, 1f, false);
            yield return new WaitForSeconds(1.5f);
            float turned = Mathf.DeltaAngle(yaw, v.transform.eulerAngles.y);
            Assert.That(turned, Is.GreaterThan(15f), "steering right turns right");
            Assert.That(v.transform.up.y, Is.GreaterThan(0.85f), "stays on its wheels in the turn");

            v.SetInput(-1f, 0f, false);
            float before = v.Speed;
            yield return new WaitForSeconds(1f);
            Assert.That(v.Speed, Is.LessThan(before - 5f), "brakes");
        }

        [Test]
        public void SeatsAndExitPoints_MatchTheCatalog()
        {
            for (int type = 0; type < VehicleCatalog.All.Length; type++)
            {
                var v = OfflineVehicle.Spawn(null, type, 0, new Vector3(type * 20f, 0f, 0f), 0f);
                Assert.That(v.Occupants.Length, Is.EqualTo(v.Stats.Seats));
                Assert.That(v.FreeSeat(), Is.EqualTo(0), "driver seat first");
                for (int s = 0; s < v.Stats.Seats; s++)
                {
                    var exit = v.ExitPosition(s);
                    var offset = exit - v.transform.position;
                    offset.y = 0f;
                    Assert.That(offset.magnitude, Is.GreaterThan(v.Stats.Width * 0.5f), $"{v.Stats.Id} seat {s}: steps out beside the vehicle");
                    Assert.That(v.Overlaps(exit + Vector3.up * 0.6f, 0f), Is.False, $"{v.Stats.Id} seat {s}: not inside the body");
                }
                Object.DestroyImmediate(v.gameObject);
            }
        }
    }
}
