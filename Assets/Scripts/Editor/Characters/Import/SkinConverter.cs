using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>
    /// Brings a model that already has a skeleton (Character Creator / AccuRIG, Mixamo, VRoid, Unreal-style names) onto
    /// the shared WAR OF NYX skeleton: every source joint is mapped to a bone (twist, share, facial and breast helpers to
    /// their parent body part), the skin weights are merged accordingly, and an A-pose is raised to the T-pose the
    /// skeleton binds in, using the model's own weights (so the mesh deforms exactly as the artist's rig would).
    /// </summary>
    public static class SkinConverter
    {
        /// <summary>The shared-skeleton bone a source joint belongs to, or null when the name is not recognised.</summary>
        public static string Map(string jointName, out bool primary)
        {
            primary = false;
            string n = jointName.ToLowerInvariant();
            n = Regex.Replace(n, @"^(cc_base_|mixamorig[:_]|j_bip_|def[-_]|bip0?1[ _])", string.Empty);
            n = Regex.Replace(n, @"_\d+$", string.Empty);
            string side = null;
            var m = Regex.Match(n, @"^(l|r)_(.*)$");
            if (m.Success)
            {
                side = m.Groups[1].Value == "l" ? "Left" : "Right";
                n = m.Groups[2].Value;
            }
            else if (n.StartsWith("left"))
            {
                side = "Left";
                n = n.Substring(4);
            }
            else if (n.StartsWith("right"))
            {
                side = "Right";
                n = n.Substring(5);
            }
            else if (Regex.IsMatch(n, @"[._](l|r)$"))
            {
                side = n.EndsWith("l") ? "Left" : "Right";
                n = n.Substring(0, n.Length - 2);
            }
            n = n.Trim('_', '.', ' ');

            // Fingers (thumb, index, middle / mid, ring, pinky / little).
            var f = Regex.Match(n, @"^(?:hand)?(thumb|index|middle|mid|ring|pinky|little)[_]?(\d)?$");
            if (f.Success && side != null)
            {
                string finger = f.Groups[1].Value switch
                {
                    "thumb" => "Thumb", "index" => "Index", "middle" => "Middle", "mid" => "Middle", "ring" => "Ring", _ => "Little",
                };
                int joint = f.Groups[2].Success ? Mathf.Clamp(int.Parse(f.Groups[2].Value), 1, 3) : 1;
                primary = true;
                return side + finger + joint;
            }
            if (side != null)
            {
                switch (n)
                {
                    case "clavicle": case "shoulder": primary = true; return side + "Shoulder";
                    case "upperarm": case "arm": case "upper_arm": primary = true; return side + "UpperArm";
                    case "forearm": case "lowerarm": case "lower_arm": primary = true; return side + "LowerArm";
                    case "hand": primary = true; return side + "Hand";
                    case "thigh": case "upleg": case "upperleg": case "upper_leg": primary = true; return side + "UpperLeg";
                    case "calf": case "leg": case "lowerleg": case "lower_leg": case "shin": primary = true; return side + "LowerLeg";
                    case "foot": primary = true; return side + "Foot";
                    case "toebase": case "toe": case "toes": case "ball": primary = true; return side + "Toes";
                }
                if (n.StartsWith("upperarmtwist") || n.StartsWith("upperarm_twist")) return side + "UpperArm";
                if (n.StartsWith("forearmtwist") || n.StartsWith("forearm_twist") || n.StartsWith("elbowshare")) return side + "LowerArm";
                if (n.StartsWith("thightwist") || n.StartsWith("thigh_twist")) return side + "UpperLeg";
                if (n.StartsWith("calftwist") || n.StartsWith("calf_twist") || n.StartsWith("kneeshare")) return side + "LowerLeg";
                if (n.Contains("toe")) return side + "Toes";
                if (n.Contains("breast") || n.Contains("bust")) return "UpperChest";
                if (n.Contains("ribs")) return "Chest";
                if (n.Contains("eye") || n.Contains("ear")) return "Head";
                return null;
            }
            switch (n)
            {
                case "hip": case "hips": case "pelvis": primary = n != "pelvis"; return "Hips";
                case "waist": case "spine": case "spine_01": primary = true; return "Spine";
                case "spine01": case "spine1": case "chest": case "spine_02": primary = true; return "Chest";
                case "spine02": case "spine2": case "upperchest": case "upper_chest": case "spine_03": primary = true; return "UpperChest";
                case "necktwist01": case "neck": case "neck_01": primary = true; return "Neck";
                case "necktwist02": return "Neck";
                case "head": primary = true; return "Head";
            }
            if (n.Contains("jaw") || n.Contains("teeth") || n.Contains("tongue") || n.Contains("facial") || n.Contains("eye") || n.Contains("head"))
                return "Head";
            if (n.Contains("root") || n.Contains("rl_bone"))
                return "Hips";
            return null;
        }

        public sealed class Result
        {
            /// <summary>Shared-skeleton bone per source joint (index into SkinJoints).</summary>
            public string[] JointBone;
            /// <summary>Bone heads measured from the source rig (primary joints only).</summary>
            public readonly Dictionary<string, Vector3> Heads = new Dictionary<string, Vector3>();
            public readonly List<string> Notes = new List<string>();
            /// <summary>Extra bones kept from the file (VRM spring bones: hair, coat / skirt, bust), parents first.</summary>
            public readonly List<RigBone> Extras = new List<RigBone>();
            /// <summary>Spring chains over the extra bones, and collider spheres (bone, offset at bind, radius).</summary>
            public readonly List<SpringChainDef> Springs = new List<SpringChainDef>();
            public readonly List<SpringColliderDef> Colliders = new List<SpringColliderDef>();
            /// <summary>True when the file's skeleton includes finger joints.</summary>
            public bool HasFingers;
        }

        /// <summary>Maps the joints, then raises the arms to a T-pose (mesh, normals and joints) when they hang lower than 8°.</summary>
        public static Result Convert(SourceModel model)
        {
            var r = model.Vrm != null && model.Vrm.HumanNodes.Count > 0 ? ConvertVrm(model) : ConvertByNames(model);
            r.HasFingers = r.Heads.ContainsKey("LeftIndex1") && r.Heads.ContainsKey("RightIndex1");
            foreach (var required in new[] { "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
                         "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" })
                if (!r.Heads.ContainsKey(required))
                    throw new InvalidOperationException($"The model's skeleton has no recognisable '{required}' joint (joints: " +
                                                        string.Join(", ", model.SkinJoints.ConvertAll(j => j.Name)) + ")");
            RaiseArms(model, r);
            return r;
        }

        /// <summary>
        /// VRM: the file names its humanoid bones explicitly (VRoid's J_Bip_* joints), so the map is exact. Spring-bone
        /// joints (J_Sec_* hair, skirt / coat, bust) stay as extra bones with their chains and colliders; any other
        /// helper joint (J_Adj_*) belongs to its nearest mapped ancestor.
        /// </summary>
        private static Result ConvertVrm(SourceModel model)
        {
            var vrm = model.Vrm;
            var r = new Result { JointBone = new string[model.SkinJoints.Count] };
            var nodeBone = new Dictionary<int, string>();
            foreach (var pair in vrm.HumanNodes)
            {
                string bone = VrmInfo.NyxBone(pair.Key);
                if (bone == null || pair.Value < 0)
                    continue;
                nodeBone[pair.Value] = bone;
                r.Heads[bone] = model.NodeWorld[pair.Value].GetColumn(3);
            }
            // Spring joints become extra bones (unique names, parents first).
            var used = new HashSet<string>();
            foreach (var b in NyxSkeleton.Build(RigProportions.Lean).Bones)
                used.Add(b.Name);
            Vector3 Pos(int node) => model.NodeWorld[node].GetColumn(3);
            string MappedAncestor(int node)
            {
                for (int p = model.NodeParent[node]; p >= 0; p = model.NodeParent[p])
                    if (nodeBone.TryGetValue(p, out var name))
                        return name;
                return "Hips";
            }
            foreach (var spring in vrm.Springs)
            {
                var names = new List<string>();
                for (int i = 0; i < spring.Nodes.Count; i++)
                {
                    int node = spring.Nodes[i];
                    if (node < 0 || node >= model.NodeWorld.Length)
                        continue;
                    if (!nodeBone.TryGetValue(node, out var name))
                    {
                        name = model.File.NodeName(node);
                        while (used.Contains(name))
                            name += "_";
                        used.Add(name);
                        nodeBone[node] = name;
                        // Tail: the next joint, a child node, or the bone extended.
                        Vector3 tail;
                        if (i + 1 < spring.Nodes.Count)
                            tail = Pos(spring.Nodes[i + 1]);
                        else
                        {
                            var children = GlbReader.List(model.File.Item("nodes", node), "children");
                            tail = children.Count > 0 ? Pos((int)(double)children[0]) : Pos(node) + (Pos(node) - Pos(Mathf.Max(0, model.NodeParent[node]))) * 0.5f;
                        }
                        r.Extras.Add(new RigBone { Name = name, Parent = MappedAncestor(node), Head = Pos(node), Tail = tail });
                    }
                    names.Add(name);
                }
                // The last joint of a VRM chain is usually an unweighted end point: keep it only as the tail.
                if (names.Count > 1 && !IsSkinJoint(model, spring.Nodes[spring.Nodes.Count - 1]))
                    names.RemoveAt(names.Count - 1);
                if (names.Count == 0)
                    continue;
                string root = r.Extras.Find(e => e.Name == names[0])?.Parent ?? "Head";
                r.Springs.Add(new SpringChainDef
                {
                    bones = names.ToArray(),
                    stiffness = 8f + spring.Stiffness * 20f,
                    damping = 1f - Mathf.Lerp(0.08f, 0.35f, Mathf.Clamp01(spring.Drag)),
                    gravity = 1f + spring.Gravity * 4f,
                    radius = Mathf.Max(0.005f, spring.HitRadius * Scale(model)),
                    coat = root == "Hips" || root.EndsWith("UpperLeg"),
                });
            }
            // Drop extras that ended up unused (chain end points).
            var simulated = new HashSet<string>();
            foreach (var c in r.Springs)
                simulated.UnionWith(c.bones);
            r.Extras.RemoveAll(e => !simulated.Contains(e.Name));
            foreach (var c in vrm.Colliders)
            {
                if (c.Node < 0 || c.Node >= model.NodeWorld.Length)
                    continue;
                string bone = nodeBone.TryGetValue(c.Node, out var b) ? b : MappedAncestor(c.Node);
                var centre = model.NodeWorld[c.Node].MultiplyPoint3x4(GlbReader.ToUnity(c.Offset));
                var head = r.Heads.TryGetValue(bone, out var h) ? h : r.Extras.Find(e => e.Name == bone)?.Head ?? Vector3.zero;
                float scale = ((Vector3)model.NodeWorld[c.Node].GetColumn(0)).magnitude;
                r.Colliders.Add(new SpringColliderDef { bone = bone, offset = centre - head, radius = c.Radius * scale });
            }
            // Skin joints: humanoid, spring, or merged into the nearest mapped ancestor (J_Adj_*, eyes, unused ends).
            var merged = new List<string>();
            for (int j = 0; j < model.SkinJoints.Count; j++)
            {
                int node = model.SkinJoints[j].Node;
                if (nodeBone.TryGetValue(node, out var name) && (simulated.Contains(name) || r.Heads.ContainsKey(name)))
                {
                    r.JointBone[j] = name;
                    continue;
                }
                r.JointBone[j] = MappedAncestor(node);
                merged.Add(model.SkinJoints[j].Name + "->" + r.JointBone[j]);
            }
            r.Notes.Add($"VRM {vrm.Version}.x: {vrm.HumanNodes.Count} humanoid bones from the file's own map, {r.Extras.Count} spring bones in {r.Springs.Count} chains, {r.Colliders.Count} colliders, {vrm.Expressions.Count} expressions.");
            if (merged.Count > 0)
                r.Notes.Add("Helper joints merged into their parent part: " + string.Join(", ", merged));
            return r;
        }

        private static bool IsSkinJoint(SourceModel model, int node) => model.SkinJoints.Exists(j => j.Node == node);

        private static float Scale(SourceModel model) =>
            model.NodeWorld != null && model.NodeWorld.Length > 0 ? ((Vector3)model.NodeWorld[0].GetColumn(0)).magnitude : 1f;

        private static Result ConvertByNames(SourceModel model)
        {
            var r = new Result { JointBone = new string[model.SkinJoints.Count] };
            var unknown = new List<string>();
            for (int j = 0; j < model.SkinJoints.Count; j++)
            {
                string bone = Map(model.SkinJoints[j].Name, out bool primary);
                if (bone == null)
                {
                    // Unrecognised helper: belongs to the nearest recognised ancestor.
                    int p = model.SkinJoints[j].Parent;
                    while (p >= 0 && bone == null)
                    {
                        bone = Map(model.SkinJoints[p].Name, out _);
                        p = model.SkinJoints[p].Parent;
                    }
                    bone ??= "Hips";
                    unknown.Add(model.SkinJoints[j].Name + "->" + bone);
                }
                else if (primary && !r.Heads.ContainsKey(bone))
                {
                    r.Heads[bone] = model.SkinJoints[j].Position;
                }
                r.JointBone[j] = bone;
            }
            if (unknown.Count > 0)
                r.Notes.Add("Helper joints merged into their parent part: " + string.Join(", ", unknown));
            r.Notes.Add($"Source skeleton: {model.SkinJoints.Count} joints mapped onto the shared humanoid skeleton.");
            return r;
        }

        /// <summary>Per-vertex weight of every shared bone touched by a vertex (merged from the source joints).</summary>
        public static Dictionary<string, float> VertexBones(SourceModel model, Result r, int v)
        {
            var d = new Dictionary<string, float>(4);
            for (int k = 0; k < 4; k++)
            {
                float w = model.Weights[v * 4 + k];
                if (w <= 0f)
                    continue;
                string bone = r.JointBone[model.Joints[v * 4 + k]];
                d[bone] = d.TryGetValue(bone, out float x) ? x + w : w;
            }
            return d;
        }

        private static void RaiseArms(SourceModel model, Result r)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                var s = r.Heads[L + "UpperArm"];
                var e = r.Heads[L + "LowerArm"];
                var w = r.Heads[L + "Hand"];
                var target = new Vector3(side, 0f, 0f);
                float drop = Vector3.Angle(e - s, target);
                if (drop < 8f)
                {
                    r.Notes.Add($"{L} arm already near the T-pose ({drop:0.0}° from horizontal).");
                    continue;
                }
                var r1 = Quaternion.FromToRotation(e - s, target);
                var e1 = s + r1 * (e - s);
                var w1 = e1 + r1 * (w - e);
                var r2 = Quaternion.FromToRotation(w1 - e1, target);
                // Deltas: upper arm rotates about the shoulder; the forearm, hand and fingers also about the elbow.
                Vector3 Upper(Vector3 p) => s + r1 * (p - s);
                Vector3 Lower(Vector3 p) => e1 + r2 * (Upper(p) - e1);
                var chainUpper = new HashSet<string> { L + "UpperArm" };
                var chainLower = new HashSet<string> { L + "LowerArm", L + "Hand" };
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    for (int j = 1; j <= 3; j++)
                        chainLower.Add(L + finger + j);
                for (int v = 0; v < model.VertexCount; v++)
                {
                    var bones = VertexBones(model, r, v);
                    float wu = 0f, wl = 0f, total = 0f;
                    foreach (var pair in bones)
                    {
                        total += pair.Value;
                        if (chainUpper.Contains(pair.Key))
                            wu += pair.Value;
                        else if (chainLower.Contains(pair.Key))
                            wl += pair.Value;
                    }
                    if (wu + wl <= 0f || total <= 0f)
                        continue;
                    wu /= total;
                    wl /= total;
                    var p = model.Positions[v];
                    model.Positions[v] = p * (1f - wu - wl) + Upper(p) * wu + Lower(p) * wl;
                    var n = model.Normals[v];
                    model.Normals[v] = (n * (1f - wu - wl) + (r1 * n) * wu + (r2 * (r1 * n)) * wl).normalized;
                }
                // Joints: each follows its parent part.
                foreach (var key in new List<string>(r.Heads.Keys))
                {
                    if (key == L + "LowerArm")
                        r.Heads[key] = Upper(r.Heads[key]);
                    else if (chainLower.Contains(key))
                        r.Heads[key] = Lower(r.Heads[key]);
                }
                r.Notes.Add($"{L} arm raised {drop:0.0}° from the file's A-pose to the T-pose (elbow straightened {Vector3.Angle(w1 - e1, target):0.0}°).");
            }
        }
    }
}
