using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Secondary motion for hanging cloth (scarf tails, coat panels, jacket tails): each chain segment's tip
    /// is a damped point pulled toward its rest direction, with gravity and inertia, so cloth trails behind
    /// when running, swings on turns and settles when standing. Panels with a driver (KAI's front coat
    /// panels follow his thighs) partly follow that limb so legs don't punch through. Visual only.
    /// </summary>
    [DefaultExecutionOrder(60)] // after CharacterAnimator (50) has posed the body
    public sealed class ClothDynamics : MonoBehaviour
    {
        private sealed class Chain
        {
            public Transform[] Joints;
            public Vector3[] Tips;
            public Vector3[] PreviousTips;
            public float Length;
            public float Stiffness;
            public Transform Driver;
            public float DriverWeight;
        }

        private readonly List<Chain> _chains = new List<Chain>();
        private Vector3 _lastRoot;
        private bool _initialized;

        public void AddChain(Transform[] joints, float segmentLength, float stiffness, Transform driver, float driverWeight)
        {
            _chains.Add(new Chain
            {
                Joints = joints, Length = segmentLength, Stiffness = stiffness, Driver = driver, DriverWeight = driverWeight,
                Tips = new Vector3[joints.Length], PreviousTips = new Vector3[joints.Length],
            });
            _initialized = false;
        }

        private void ResetTips()
        {
            foreach (var c in _chains)
                for (int i = 0; i < c.Joints.Length; i++)
                {
                    c.Tips[i] = c.Joints[i].position + RestDirection(c, i) * c.Length;
                    c.PreviousTips[i] = c.Tips[i];
                }
            _lastRoot = transform.position;
            _initialized = true;
        }

        private static Vector3 RestDirection(Chain c, int i)
        {
            var parent = c.Joints[i].parent;
            var down = parent != null ? parent.rotation * Vector3.down : Vector3.down;
            if (i == 0 && c.Driver != null)
                down = Vector3.Slerp(down, c.Driver.rotation * Vector3.down, c.DriverWeight);
            return down;
        }

        private void LateUpdate()
        {
            if (_chains.Count == 0)
                return;
            if (!_initialized || (transform.position - _lastRoot).sqrMagnitude > 4f)
                ResetTips(); // first frame or a teleport
            _lastRoot = transform.position;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f)
                return;
            var gravity = Vector3.down * 9.8f * dt * dt;

            foreach (var c in _chains)
            {
                for (int i = 0; i < c.Joints.Length; i++)
                {
                    var joint = c.Joints[i];
                    var origin = joint.position;
                    var rest = RestDirection(c, i);
                    var tip = c.Tips[i];
                    var velocity = (tip - c.PreviousTips[i]) * 0.86f;
                    c.PreviousTips[i] = tip;
                    tip += velocity + gravity;
                    tip += (origin + rest * c.Length - tip) * Mathf.Clamp01(c.Stiffness * dt);
                    var dir = tip - origin;
                    if (dir.sqrMagnitude < 1e-8f)
                        dir = rest;
                    dir.Normalize();
                    // Never swing up past horizontal relative to the rest direction (cloth hangs).
                    if (Vector3.Dot(dir, rest) < -0.1f)
                        dir = Vector3.Slerp(dir, rest, 0.5f).normalized;
                    tip = origin + dir * c.Length;
                    c.Tips[i] = tip;
                    var parent = joint.parent;
                    joint.rotation = Ik.Limb(dir, parent != null ? parent.forward : Vector3.forward);
                }
            }
        }
    }
}
