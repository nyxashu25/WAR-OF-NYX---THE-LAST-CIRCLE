using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// Bone-driven secondary motion for hair and long coats (sections 40.3, 40.4): each helper-bone chain (hair locks,
    /// coat panels) follows the animated pose through a damped spring, so it swings on running, sprinting, jumping,
    /// landing and sudden turns, and settles when the character stops. Coat panels are pushed out of the thighs so
    /// the coat does not cut through the legs. Runs after the Animator; costs a few vector operations per bone.
    /// <see cref="Quality"/>: 0 off (reduced motion), 1 hair only, 2 hair and coat. Far away it snaps to the pose.
    /// </summary>
    [DefaultExecutionOrder(70)]
    public sealed class CharacterSecondaryMotion : MonoBehaviour
    {
        /// <summary>Global quality: 2 full, 1 hair only (Low graphics tier), 0 off (reduced-motion setting).</summary>
        public static int Quality = 2;

        private sealed class Chain
        {
            public Transform[] Bones;
            public Vector3[] Tips;      // simulated tip of each bone
            public Vector3[] Previous;
            public float[] Lengths;
            public Vector3[] LocalTail; // bind-pose tail direction in the bone's own frame
            public Quaternion[] BindLocal; // helper bones are not animated: reset to bind each frame
            public float Stiffness, Damping, Gravity;
            public bool Coat;
            public bool Initialized;
        }

        private readonly List<Chain> _chains = new List<Chain>();
        private Transform _leftThigh, _rightThigh, _leftKnee, _rightKnee;

        private void Start()
        {
            var animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                _leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            Add(false, 22f, 0.82f, 3f, "HairBack1", "HairBack2");
            Add(false, 30f, 0.8f, 2f, "HairSideL");
            Add(false, 30f, 0.8f, 2f, "HairSideR");
            Add(false, 40f, 0.78f, 1.5f, "HairFront");
            foreach (var panel in new[] { "CoatFrontL", "CoatFrontR", "CoatBackL", "CoatBackR" })
                Add(true, 16f, 0.86f, 6f, panel + "1", panel + "2");
        }

        private Transform FindBone(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void Add(bool coat, float stiffness, float damping, float gravity, params string[] names)
        {
            var bones = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                bones[i] = FindBone(names[i]);
                if (bones[i] == null)
                    return;
            }
            var chain = new Chain
            {
                Bones = bones, Tips = new Vector3[bones.Length], Previous = new Vector3[bones.Length], Lengths = new float[bones.Length],
                LocalTail = new Vector3[bones.Length], BindLocal = new Quaternion[bones.Length], Stiffness = stiffness, Damping = damping, Gravity = gravity, Coat = coat,
            };
            for (int i = 0; i < bones.Length; i++)
            {
                // Tail: the next bone, or an extension of the chain (hair and coat helpers point down / back).
                var tail = i + 1 < bones.Length ? bones[i + 1].position : bones[i].position + (i > 0 ? bones[i].position - bones[i - 1].position : Vector3.down * 0.12f);
                chain.Lengths[i] = Mathf.Max(0.02f, Vector3.Distance(bones[i].position, tail));
                chain.LocalTail[i] = bones[i].InverseTransformDirection((tail - bones[i].position).normalized);
                chain.BindLocal[i] = bones[i].localRotation;
            }
            _chains.Add(chain);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f)
                return;
            var cam = Camera.main;
            bool near = cam == null || (cam.transform.position - transform.position).sqrMagnitude < 35f * 35f;
            int quality = App.AppServices.IsInitialized && App.AppServices.UserSettings.Data.reduceMotion ? 0 : Quality;
            foreach (var c in _chains)
            {
                bool enabled = near && (quality >= 2 || (quality == 1 && !c.Coat));
                for (int i = 0; i < c.Bones.Length; i++)
                {
                    var bone = c.Bones[i];
                    bone.localRotation = c.BindLocal[i];
                    var animatedDir = bone.rotation * c.LocalTail[i];
                    var rest = bone.position + animatedDir * c.Lengths[i];
                    if (!enabled || !c.Initialized)
                    {
                        c.Tips[i] = c.Previous[i] = rest;
                        continue;
                    }
                    // Verlet: inertia, gravity, a spring back to the animated pose.
                    var velocity = (c.Tips[i] - c.Previous[i]) * c.Damping;
                    c.Previous[i] = c.Tips[i];
                    var next = c.Tips[i] + velocity + Vector3.down * (c.Gravity * dt * dt) + (rest - c.Tips[i]) * Mathf.Clamp01(c.Stiffness * dt);
                    // Keep the length; coat panels stay outside the thighs.
                    next = bone.position + (next - bone.position).normalized * c.Lengths[i];
                    if (c.Coat)
                        next = PushOutOfLegs(next, bone.position, c.Lengths[i]);
                    c.Tips[i] = next;
                    bone.rotation = Quaternion.FromToRotation(animatedDir, (next - bone.position).normalized) * bone.rotation;
                }
                c.Initialized = true;
            }
        }

        private Vector3 PushOutOfLegs(Vector3 p, Vector3 pivot, float length)
        {
            foreach (var (top, knee) in new[] { (_leftThigh, _leftKnee), (_rightThigh, _rightKnee) })
            {
                if (top == null || knee == null)
                    continue;
                var a = top.position;
                var b = knee.position;
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                var closest = a + ab * t;
                var away = p - closest;
                const float radius = 0.13f;
                if (away.sqrMagnitude < radius * radius)
                {
                    p = closest + (away.sqrMagnitude > 1e-8f ? away.normalized : Vector3.forward) * radius;
                    p = pivot + (p - pivot).normalized * length;
                }
            }
            return p;
        }
    }
}
