using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>One bone of the shared WAR OF NYX skeleton: bind (T-pose) position, parent, and its Unity humanoid name.</summary>
    public sealed class RigBone
    {
        public string Name;
        public string Parent;
        public Vector3 Head;
        /// <summary>End point for skinning (the child joint, or a tip for leaf bones).</summary>
        public Vector3 Tail;
        /// <summary>Unity <see cref="HumanBodyBones"/> name (HumanTrait.BoneName), or null for helper bones (hair, coat).</summary>
        public string Human;
    }

    /// <summary>Body measurements of one character (metres, T-pose). The hit shapes are the same for everyone.</summary>
    public struct RigProportions
    {
        public float Height;        // crown of the head
        public float HipHeight;     // hip joints
        public float ShoulderHalf;  // centre to the shoulder joint
        public float HipHalf;       // centre to the hip joint
        public float UpperArm, Forearm, HandLength;
        public float Neck;

        /// <summary>KAI: tall, lean, long-limbed (about 7.5 heads).</summary>
        public static RigProportions Lean => new RigProportions
        {
            Height = 1.8f, HipHeight = 0.925f, ShoulderHalf = 0.185f, HipHalf = 0.095f, UpperArm = 0.28f, Forearm = 0.26f, HandLength = 0.19f, Neck = 0.085f,
        };
    }

    /// <summary>
    /// The shared humanoid skeleton of the skinned WAR OF NYX characters (Unity Humanoid compatible, so authored clips
    /// and licensed humanoid animation retarget across the roster): root, hips, spine, chest, upper chest, neck, head,
    /// shoulders, arms, hands with five three-joint fingers, legs, feet and toes, plus helper bones for hair and the long
    /// coat panels (driven by <see cref="CharacterSecondaryMotion"/>). Bind pose is a T-pose with palms down, every bone
    /// with identity rotation (so authored rotations read directly in body space). +Z forward, +Y up, feet at y = 0.
    /// </summary>
    public sealed class NyxSkeleton
    {
        public readonly List<RigBone> Bones = new List<RigBone>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        public int IndexOf(string name) => _index.TryGetValue(name, out int i) ? i : -1;
        public RigBone this[string name] => Bones[_index[name]];
        public bool Has(string name) => _index.ContainsKey(name);

        private void Add(string name, string parent, Vector3 head, string human = null)
        {
            _index[name] = Bones.Count;
            Bones.Add(new RigBone { Name = name, Parent = parent, Head = head, Tail = head, Human = human });
        }

        public static NyxSkeleton Build(RigProportions p)
        {
            var s = new NyxSkeleton();
            float h = p.Height;
            float hips = p.HipHeight + 0.045f;
            float shoulderY = h * 0.8f;
            s.Add("Hips", null, new Vector3(0f, hips, 0f), "Hips");
            s.Add("Spine", "Hips", new Vector3(0f, hips + 0.1f, 0.005f), "Spine");
            s.Add("Chest", "Spine", new Vector3(0f, hips + 0.22f, 0.01f), "Chest");
            s.Add("UpperChest", "Chest", new Vector3(0f, hips + 0.34f, 0.005f), "UpperChest");
            s.Add("Neck", "UpperChest", new Vector3(0f, shoulderY + 0.06f, -0.01f), "Neck");
            s.Add("Head", "Neck", new Vector3(0f, shoulderY + 0.06f + p.Neck, 0f), "Head");
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                float x = side;
                var shoulder = new Vector3(x * 0.035f, shoulderY + 0.015f, -0.01f);
                var upper = new Vector3(x * p.ShoulderHalf, shoulderY, -0.02f);
                var lower = upper + new Vector3(x * p.UpperArm, 0f, -0.005f);
                var hand = lower + new Vector3(x * p.Forearm, 0f, 0.005f);
                s.Add(L + "Shoulder", "UpperChest", shoulder, L + "Shoulder");
                s.Add(L + "UpperArm", L + "Shoulder", upper, L + "UpperArm");
                s.Add(L + "LowerArm", L + "UpperArm", lower, L + "LowerArm");
                s.Add(L + "Hand", L + "LowerArm", hand, L + "Hand");
                // Fingers: palm length ~45 % of the hand; three joints each; thumb forward and out.
                float palm = p.HandLength * 0.45f;
                var names = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
                var offsets = new[] { 0.028f, 0.022f, 0f, -0.021f, -0.04f };
                var lengths = new[] { new[] { 0.035f, 0.03f, 0.025f }, new[] { 0.045f, 0.028f, 0.022f }, new[] { 0.05f, 0.032f, 0.024f },
                    new[] { 0.046f, 0.03f, 0.022f }, new[] { 0.036f, 0.022f, 0.018f } };
                for (int f = 0; f < 5; f++)
                {
                    bool thumb = f == 0;
                    var start = hand + (thumb ? new Vector3(x * 0.025f, -0.015f, 0.02f) : new Vector3(x * palm * (f == 4 ? 0.9f : 1f), 0f, offsets[f]));
                    var dir = thumb ? new Vector3(x * 0.55f, -0.15f, 0.82f).normalized : new Vector3(x, 0f, 0f);
                    string prev = L + "Hand";
                    var at = start;
                    string[] joints = { "Proximal", "Intermediate", "Distal" };
                    for (int j = 0; j < 3; j++)
                    {
                        string name = $"{L}{names[f]}{j + 1}";
                        s.Add(name, prev, at, $"{L} {names[f]} {joints[j]}");
                        at += dir * lengths[f][j];
                        prev = name;
                    }
                    s[prev].Tail = at;
                }
                var thigh = new Vector3(x * p.HipHalf, p.HipHeight, 0f);
                var knee = new Vector3(x * (p.HipHalf + 0.005f), p.HipHeight * 0.54f, 0.01f);
                var ankle = new Vector3(x * (p.HipHalf + 0.01f), 0.085f, -0.01f);
                var toes = new Vector3(x * (p.HipHalf + 0.015f), 0.022f, 0.12f);
                s.Add(L + "UpperLeg", "Hips", thigh, L + "UpperLeg");
                s.Add(L + "LowerLeg", L + "UpperLeg", knee, L + "LowerLeg");
                s.Add(L + "Foot", L + "LowerLeg", ankle, L + "Foot");
                s.Add(L + "Toes", L + "Foot", toes, L + "Toes");
                s[L + "Toes"].Tail = toes + new Vector3(0f, 0f, 0.075f);
                s[L + "Hand"].Tail = hand + new Vector3(x * (palm + 0.08f), 0f, 0f);
            }
            s["Head"].Tail = new Vector3(0f, h, 0f);

            // Helper bones: hair (back, sides) and four coat panels with two segments each.
            var head = s["Head"].Head;
            s.Add("HairBack1", "Head", head + new Vector3(0f, 0.06f, -0.1f));
            s.Add("HairBack2", "HairBack1", head + new Vector3(0f, -0.04f, -0.12f));
            s["HairBack2"].Tail = head + new Vector3(0f, -0.14f, -0.12f);
            s.Add("HairSideL", "Head", head + new Vector3(-0.085f, 0.05f, 0.02f));
            s["HairSideL"].Tail = head + new Vector3(-0.09f, -0.06f, 0.03f);
            s.Add("HairSideR", "Head", head + new Vector3(0.085f, 0.05f, 0.02f));
            s["HairSideR"].Tail = head + new Vector3(0.09f, -0.06f, 0.03f);
            s.Add("HairFront", "Head", head + new Vector3(0f, 0.17f, 0.09f));
            s["HairFront"].Tail = head + new Vector3(0f, 0.09f, 0.12f);
            foreach (var (name, x, z) in new[] { ("CoatFrontL", -1f, 1f), ("CoatFrontR", 1f, 1f), ("CoatBackL", -1f, -1f), ("CoatBackR", 1f, -1f) })
            {
                var top = new Vector3(x * 0.12f, hips - 0.06f, z * 0.13f);
                s.Add(name + "1", "Hips", top);
                s.Add(name + "2", name + "1", top + new Vector3(x * 0.02f, -0.24f, z * 0.025f));
                s[name + "2"].Tail = top + new Vector3(x * 0.04f, -0.52f, z * 0.05f);
            }

            // Tails = the first child's head where not set.
            foreach (var bone in s.Bones)
                if (bone.Parent != null && s[bone.Parent].Tail == s[bone.Parent].Head)
                    s[bone.Parent].Tail = bone.Head;
            s["Hips"].Tail = s["Spine"].Head;
            s["Spine"].Tail = s["Chest"].Head;
            s["Chest"].Tail = s["UpperChest"].Head;
            s["UpperChest"].Tail = s["Neck"].Head;
            s["Neck"].Tail = s["Head"].Head;
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                s[L + "Shoulder"].Tail = s[L + "UpperArm"].Head;
                s[L + "UpperArm"].Tail = s[L + "LowerArm"].Head;
                s[L + "LowerArm"].Tail = s[L + "Hand"].Head;
                s[L + "UpperLeg"].Tail = s[L + "LowerLeg"].Head;
                s[L + "LowerLeg"].Tail = s[L + "Foot"].Head;
                s[L + "Foot"].Tail = s[L + "Toes"].Head;
            }
            return s;
        }

        /// <summary>
        /// The same skeleton (bone names, hierarchy, humanoid mapping) with joint positions measured on another model:
        /// used for imported characters, so the authored clips, the animation controller and the runtime layers work on
        /// them unchanged. <paramref name="heads"/> must give every humanoid bone; <paramref name="tails"/> the end of
        /// leaf bones (head top, finger tips, toe tips). Helper bones (hair, coat) are left out unless given.
        /// </summary>
        public static NyxSkeleton Fitted(IReadOnlyDictionary<string, Vector3> heads, IReadOnlyDictionary<string, Vector3> tails)
        {
            var template = Build(RigProportions.Lean);
            var s = new NyxSkeleton();
            foreach (var bone in template.Bones)
            {
                if (!heads.TryGetValue(bone.Name, out var head))
                {
                    if (bone.Human != null)
                        throw new System.ArgumentException("Fitted skeleton: no position for " + bone.Name);
                    continue;
                }
                s.Add(bone.Name, bone.Parent, head, bone.Human);
            }
            foreach (var bone in s.Bones)
                if (bone.Parent != null && s[bone.Parent].Tail == s[bone.Parent].Head)
                    s[bone.Parent].Tail = bone.Head;
            // Main chains point at their main child (not a finger or a clavicle).
            void Link(string a, string b)
            {
                if (s.Has(a) && s.Has(b))
                    s[a].Tail = s[b].Head;
            }
            Link("Hips", "Spine");
            Link("Spine", "Chest");
            Link("Chest", "UpperChest");
            Link("UpperChest", "Neck");
            Link("Neck", "Head");
            foreach (var L in new[] { "Left", "Right" })
            {
                Link(L + "Shoulder", L + "UpperArm");
                Link(L + "UpperArm", L + "LowerArm");
                Link(L + "LowerArm", L + "Hand");
                Link(L + "Hand", L + "Middle1");
                Link(L + "UpperLeg", L + "LowerLeg");
                Link(L + "LowerLeg", L + "Foot");
                Link(L + "Foot", L + "Toes");
            }
            foreach (var pair in tails)
                if (s.Has(pair.Key))
                    s[pair.Key].Tail = pair.Value;
            return s;
        }

        /// <summary>Creates the bone transforms (identity rotations, T-pose) under <paramref name="parent"/>, in bone order.</summary>
        public Transform[] Instantiate(Transform parent)
        {
            var transforms = new Transform[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                var b = Bones[i];
                var t = new GameObject(b.Name).transform;
                var p = b.Parent != null ? transforms[_index[b.Parent]] : parent;
                t.SetParent(p, false);
                t.position = parent.TransformPoint(b.Head);
                t.localRotation = Quaternion.identity;
                transforms[i] = t;
            }
            return transforms;
        }

        /// <summary>Bind poses for a skinned mesh whose vertices are in the same (root) space as the bone heads.</summary>
        public Matrix4x4[] BindPoses()
        {
            var poses = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
                poses[i] = Matrix4x4.Translate(-Bones[i].Head);
            return poses;
        }

        // ------------------------------------------------------------------ skin weights

        /// <summary>All weight on one bone.</summary>
        public BoneWeight Rigid(string bone) => new BoneWeight { boneIndex0 = IndexOf(bone), weight0 = 1f };

        /// <summary>
        /// Weights for a point on a part that follows a chain of bones (a limb, the spine, a coat panel, a hair lock):
        /// the nearest bone segment, blended smoothly with its neighbour within <paramref name="blend"/> metres of a joint,
        /// so elbows, knees and the waist bend without creasing or gaps.
        /// </summary>
        public BoneWeight Chain(Vector3 p, float blend, params string[] chain)
        {
            int best = 0;
            float bestDistance = float.MaxValue, bestT = 0f, bestLength = 1f;
            for (int i = 0; i < chain.Length; i++)
            {
                var b = this[chain[i]];
                var seg = b.Tail - b.Head;
                float len = Mathf.Max(1e-4f, seg.magnitude);
                float t = Mathf.Clamp01(Vector3.Dot(p - b.Head, seg) / (len * len));
                float d = Vector3.Distance(p, b.Head + seg * t);
                if (d < bestDistance - 1e-5f)
                {
                    bestDistance = d;
                    best = i;
                    bestT = t;
                    bestLength = len;
                }
            }
            float fromHead = bestT * bestLength, toTail = (1f - bestT) * bestLength;
            if (fromHead < blend && best > 0)
            {
                float w = Mathf.SmoothStep(0.5f, 1f, fromHead / blend);
                return Pair(chain[best - 1], 1f - w, chain[best], w);
            }
            if (toTail < blend && best < chain.Length - 1)
            {
                float w = Mathf.SmoothStep(0.5f, 1f, toTail / blend);
                return Pair(chain[best], w, chain[best + 1], 1f - w);
            }
            return Rigid(chain[best]);
        }

        public BoneWeight Pair(string a, float wa, string b, float wb)
        {
            float sum = Mathf.Max(1e-6f, wa + wb);
            return new BoneWeight { boneIndex0 = IndexOf(a), weight0 = wa / sum, boneIndex1 = IndexOf(b), weight1 = wb / sum };
        }

        /// <summary>Blends two weight sets (t = share of <paramref name="b"/>), keeping the four strongest influences.</summary>
        public static BoneWeight Mix(BoneWeight a, BoneWeight b, float t)
        {
            if (t <= 0f)
                return a;
            if (t >= 1f)
                return b;
            var map = new Dictionary<int, float>();
            void Put(int i, float w)
            {
                if (w <= 0f)
                    return;
                map[i] = map.TryGetValue(i, out float v) ? v + w : w;
            }
            Put(a.boneIndex0, a.weight0 * (1f - t)); Put(a.boneIndex1, a.weight1 * (1f - t)); Put(a.boneIndex2, a.weight2 * (1f - t)); Put(a.boneIndex3, a.weight3 * (1f - t));
            Put(b.boneIndex0, b.weight0 * t); Put(b.boneIndex1, b.weight1 * t); Put(b.boneIndex2, b.weight2 * t); Put(b.boneIndex3, b.weight3 * t);
            var list = new List<KeyValuePair<int, float>>(map);
            list.Sort((x, y) => y.Value.CompareTo(x.Value));
            float sum = 0f;
            for (int i = 0; i < list.Count && i < 4; i++)
                sum += list[i].Value;
            var r = new BoneWeight();
            for (int i = 0; i < list.Count && i < 4; i++)
            {
                float w = list[i].Value / sum;
                switch (i)
                {
                    case 0: r.boneIndex0 = list[i].Key; r.weight0 = w; break;
                    case 1: r.boneIndex1 = list[i].Key; r.weight1 = w; break;
                    case 2: r.boneIndex2 = list[i].Key; r.weight2 = w; break;
                    default: r.boneIndex3 = list[i].Key; r.weight3 = w; break;
                }
            }
            return r;
        }
    }

    /// <summary>Two-bone IK for any bind orientation (arms along X, legs along -Y): rotates the upper and lower bone.</summary>
    public static class LimbIk
    {
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            float la = Vector3.Distance(upper.position, lower.position);
            float lb = Vector3.Distance(lower.position, end.position);
            var toTarget = target - upper.position;
            float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.999f);
            var dir = toTarget.normalized;
            var bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            bend.Normalize();
            float x = (la * la - lb * lb + d * d) / (2f * d);
            float y = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
            var elbow = upper.position + dir * x + bend * y;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            var reach = upper.position + dir * d;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, reach - lower.position) * lower.rotation;
        }
    }
}
