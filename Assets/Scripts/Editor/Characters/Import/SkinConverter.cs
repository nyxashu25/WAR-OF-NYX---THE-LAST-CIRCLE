using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

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
        }

        /// <summary>Maps the joints, then raises the arms to a T-pose (mesh, normals and joints) when they hang lower than 8°.</summary>
        public static Result Convert(SourceModel model)
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
            foreach (var required in new[] { "Hips", "Spine", "Head", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
                         "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot" })
                if (!r.Heads.ContainsKey(required))
                    throw new InvalidOperationException($"The model's skeleton has no recognisable '{required}' joint (joints: " +
                                                        string.Join(", ", model.SkinJoints.ConvertAll(j => j.Name)) + ")");
            if (unknown.Count > 0)
                r.Notes.Add("Helper joints merged into their parent part: " + string.Join(", ", unknown));
            r.Notes.Add($"Source skeleton: {model.SkinJoints.Count} joints mapped onto the shared humanoid skeleton.");
            RaiseArms(model, r);
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
