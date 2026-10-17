using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>A spring-bone chain of a VRM model (hair lock, skirt or coat panel), as glTF node indices root → tip.</summary>
    public sealed class VrmSpring
    {
        public readonly List<int> Nodes = new List<int>();
        public float Stiffness = 1f, Drag = 0.4f, Gravity, HitRadius = 0.02f;
        public readonly List<int> ColliderGroups = new List<int>();
    }

    /// <summary>A collider sphere of a VRM model (node, offset in the node's frame, radius), in glTF space.</summary>
    public sealed class VrmCollider
    {
        public int Node;
        public Vector3 Offset;
        public float Radius;
    }

    /// <summary>The MToon look of one material (the parts the game's character toon shader uses).</summary>
    public sealed class VrmMaterial
    {
        public Color ShadeColor = new Color(0.7f, 0.65f, 0.75f, 1f);
        public int ShadeImage = -1;
        public float ShadingShift, ShadingToony = 0.9f;
        public Color RimColor = Color.black;
        public float OutlineWidth;          // metres (0 = no outline)
        public Color OutlineColor = Color.black;
        public string BlendMode;            // OPAQUE / MASK / BLEND (VRM 0 keeps it here)
        public float Cutoff = 0.5f;
        public int Cull = 2;                // UnityEngine.Rendering.CullMode
    }

    /// <summary>
    /// What a .vrm file (VRoid Studio, UniVRM) adds to glTF: the humanoid bone map, expressions (blend-shape presets),
    /// spring bones and colliders, MToon material settings and the licence. Reads VRM 0.x ("VRM") and 1.0
    /// ("VRMC_vrm", "VRMC_springBone", "VRMC_materials_mtoon").
    /// </summary>
    public sealed class VrmInfo
    {
        public int Version;
        /// <summary>VRM humanoid bone name (VRM 1.0 spelling) → glTF node.</summary>
        public readonly Dictionary<string, int> HumanNodes = new Dictionary<string, int>();
        /// <summary>Expression preset (VRM 1.0 names: blink, happy, angry, sad, relaxed, surprised, aa…) → (mesh, morph target, weight 0..1).</summary>
        public readonly Dictionary<string, List<(int mesh, int target, float weight)>> Expressions = new Dictionary<string, List<(int, int, float)>>();
        public readonly List<VrmSpring> Springs = new List<VrmSpring>();
        public readonly List<VrmCollider> Colliders = new List<VrmCollider>();
        /// <summary>Collider group index → collider indices.</summary>
        public readonly List<List<int>> ColliderGroups = new List<List<int>>();
        public readonly Dictionary<int, VrmMaterial> Materials = new Dictionary<int, VrmMaterial>();
        public string Title, Author, Licence, CommercialUse;

        private static readonly Dictionary<string, string> PresetNames = new Dictionary<string, string>
        {
            { "joy", "happy" }, { "sorrow", "sad" }, { "fun", "relaxed" }, { "a", "aa" }, { "i", "ih" }, { "u", "ou" }, { "e", "ee" },
            { "o", "oh" }, { "blink_l", "blinkLeft" }, { "blink_r", "blinkRight" }, { "lookup", "lookUp" }, { "lookdown", "lookDown" },
            { "lookleft", "lookLeft" }, { "lookright", "lookRight" },
        };

        /// <summary>VRM 0.x thumb bones are named one joint further out than VRM 1.0 (proximal = 1.0 metacarpal).</summary>
        private static string HumanName0(string bone)
        {
            if (bone.EndsWith("ThumbProximal")) return bone.Replace("ThumbProximal", "ThumbMetacarpal");
            if (bone.EndsWith("ThumbIntermediate")) return bone.Replace("ThumbIntermediate", "ThumbProximal");
            return bone;
        }

        public static VrmInfo Read(GlbReader glb)
        {
            var ext = GlbReader.Obj(glb.Json, "extensions");
            var v1 = GlbReader.Obj(ext, "VRMC_vrm");
            var v0 = GlbReader.Obj(ext, "VRM");
            if (v1 == null && v0 == null)
                return null;
            var info = new VrmInfo { Version = v1 != null ? 1 : 0 };
            var nodes = glb.Array("nodes");
            int MeshOfNode(int node) => node >= 0 && node < nodes.Count ? GlbReader.Int((Dictionary<string, object>)nodes[node], "mesh") : -1;

            if (v1 != null)
            {
                var bones = GlbReader.Obj(GlbReader.Obj(v1, "humanoid"), "humanBones");
                if (bones != null)
                    foreach (var pair in bones)
                        info.HumanNodes[pair.Key] = GlbReader.Int(pair.Value as Dictionary<string, object>, "node");
                var meta = GlbReader.Obj(v1, "meta");
                info.Title = GlbReader.Str(meta, "name");
                var authors = GlbReader.List(meta, "authors");
                info.Author = authors.Count > 0 ? string.Join(", ", authors) : null;
                info.Licence = GlbReader.Str(meta, "licenseUrl");
                info.CommercialUse = GlbReader.Str(meta, "commercialUsage");
                var expressions = GlbReader.Obj(v1, "expressions");
                foreach (var group in new[] { GlbReader.Obj(expressions, "preset"), GlbReader.Obj(expressions, "custom") })
                {
                    if (group == null)
                        continue;
                    foreach (var pair in group)
                    {
                        var list = new List<(int, int, float)>();
                        foreach (Dictionary<string, object> bind in GlbReader.List(pair.Value as Dictionary<string, object>, "morphTargetBinds"))
                            list.Add((MeshOfNode(GlbReader.Int(bind, "node")), GlbReader.Int(bind, "index"), GlbReader.Float(bind, "weight", 1f)));
                        if (list.Count > 0)
                            info.Expressions[pair.Key] = list;
                    }
                }
                var spring = GlbReader.Obj(ext, "VRMC_springBone");
                foreach (Dictionary<string, object> c in GlbReader.List(spring, "colliders"))
                {
                    var shape = GlbReader.Obj(c, "shape");
                    var sphere = GlbReader.Obj(shape, "sphere") ?? GlbReader.Obj(shape, "capsule");
                    var offset = GlbReader.Floats(sphere, "offset") ?? new[] { 0f, 0f, 0f };
                    info.Colliders.Add(new VrmCollider { Node = GlbReader.Int(c, "node"), Offset = new Vector3(offset[0], offset[1], offset[2]), Radius = GlbReader.Float(sphere, "radius", 0.05f) });
                }
                foreach (Dictionary<string, object> g in GlbReader.List(spring, "colliderGroups"))
                    info.ColliderGroups.Add(GlbReader.List(g, "colliders").ConvertAll(x => (int)(double)x));
                foreach (Dictionary<string, object> s in GlbReader.List(spring, "springs"))
                {
                    var chain = new VrmSpring();
                    var joints = GlbReader.List(s, "joints");
                    foreach (Dictionary<string, object> j in joints)
                        chain.Nodes.Add(GlbReader.Int(j, "node"));
                    if (joints.Count > 0)
                    {
                        var first = (Dictionary<string, object>)joints[0];
                        chain.Stiffness = GlbReader.Float(first, "stiffness", 1f);
                        chain.Drag = GlbReader.Float(first, "dragForce", 0.4f);
                        chain.Gravity = GlbReader.Float(first, "gravityPower", 0f);
                        chain.HitRadius = GlbReader.Float(first, "hitRadius", 0.02f);
                    }
                    foreach (var g in GlbReader.List(s, "colliderGroups"))
                        chain.ColliderGroups.Add((int)(double)g);
                    if (chain.Nodes.Count > 0)
                        info.Springs.Add(chain);
                }
                var materials = glb.Array("materials");
                for (int m = 0; m < materials.Count; m++)
                {
                    var mtoon = GlbReader.Obj(GlbReader.Obj((Dictionary<string, object>)materials[m], "extensions"), "VRMC_materials_mtoon");
                    if (mtoon == null)
                        continue;
                    var vm = new VrmMaterial
                    {
                        ShadeColor = ColorOf(GlbReader.Floats(mtoon, "shadeColorFactor"), new Color(0.7f, 0.65f, 0.75f)),
                        ShadeImage = ImageOf(glb, GlbReader.Obj(mtoon, "shadeMultiplyTexture")),
                        ShadingShift = GlbReader.Float(mtoon, "shadingShiftFactor", 0f),
                        ShadingToony = GlbReader.Float(mtoon, "shadingToonyFactor", 0.9f),
                        RimColor = ColorOf(GlbReader.Floats(mtoon, "parametricRimColorFactor"), Color.black),
                        OutlineColor = ColorOf(GlbReader.Floats(mtoon, "outlineColorFactor"), Color.black),
                    };
                    string mode = GlbReader.Str(mtoon, "outlineWidthMode") ?? "none";
                    vm.OutlineWidth = mode == "none" ? 0f : Mathf.Clamp(GlbReader.Float(mtoon, "outlineWidthFactor", 0f) * (mode == "screenCoordinates" ? 0.5f : 1f), 0f, 0.004f);
                    info.Materials[m] = vm;
                }
            }
            else
            {
                foreach (Dictionary<string, object> b in GlbReader.List(GlbReader.Obj(v0, "humanoid"), "humanBones"))
                {
                    string bone = GlbReader.Str(b, "bone");
                    if (bone != null)
                        info.HumanNodes[HumanName0(bone)] = GlbReader.Int(b, "node");
                }
                var meta = GlbReader.Obj(v0, "meta");
                info.Title = GlbReader.Str(meta, "title");
                info.Author = GlbReader.Str(meta, "author");
                info.Licence = GlbReader.Str(meta, "licenseName");
                info.CommercialUse = GlbReader.Str(meta, "commercialUssageName");
                foreach (Dictionary<string, object> g in GlbReader.List(GlbReader.Obj(v0, "blendShapeMaster"), "blendShapeGroups"))
                {
                    string preset = (GlbReader.Str(g, "presetName") ?? "unknown").ToLowerInvariant();
                    if (preset == "unknown")
                        preset = GlbReader.Str(g, "name") ?? "custom";
                    if (PresetNames.TryGetValue(preset, out var renamed))
                        preset = renamed;
                    var list = new List<(int, int, float)>();
                    foreach (Dictionary<string, object> bind in GlbReader.List(g, "binds"))
                        list.Add((GlbReader.Int(bind, "mesh"), GlbReader.Int(bind, "index"), GlbReader.Float(bind, "weight", 100f) / 100f));
                    if (list.Count > 0)
                        info.Expressions[preset] = list;
                }
                var secondary = GlbReader.Obj(v0, "secondaryAnimation");
                foreach (Dictionary<string, object> g in GlbReader.List(secondary, "colliderGroups"))
                {
                    var indices = new List<int>();
                    int node = GlbReader.Int(g, "node");
                    foreach (Dictionary<string, object> c in GlbReader.List(g, "colliders"))
                    {
                        var o = GlbReader.Obj(c, "offset");
                        indices.Add(info.Colliders.Count);
                        // VRM 0.x stores collider offsets in Unity's left-handed frame: mirror back to glTF.
                        info.Colliders.Add(new VrmCollider
                        {
                            Node = node, Offset = new Vector3(-GlbReader.Float(o, "x", 0f), GlbReader.Float(o, "y", 0f), GlbReader.Float(o, "z", 0f)),
                            Radius = GlbReader.Float(c, "radius", 0.05f),
                        });
                    }
                    info.ColliderGroups.Add(indices);
                }
                foreach (Dictionary<string, object> g in GlbReader.List(secondary, "boneGroups"))
                {
                    var groups = GlbReader.List(g, "colliderGroups").ConvertAll(x => (int)(double)x);
                    foreach (var root in GlbReader.List(g, "bones"))
                    {
                        // Each root's descendants, split into simple chains (the first child continues a chain).
                        foreach (var chainNodes in Chains(glb, (int)(double)root))
                        {
                            var chain = new VrmSpring
                            {
                                Stiffness = GlbReader.Float(g, "stiffiness", 1f), Drag = GlbReader.Float(g, "dragForce", 0.4f),
                                Gravity = GlbReader.Float(g, "gravityPower", 0f), HitRadius = GlbReader.Float(g, "hitRadius", 0.02f),
                            };
                            chain.Nodes.AddRange(chainNodes);
                            chain.ColliderGroups.AddRange(groups);
                            info.Springs.Add(chain);
                        }
                    }
                }
                var props = GlbReader.List(v0, "materialProperties");
                var materials = glb.Array("materials");
                foreach (Dictionary<string, object> p in props)
                {
                    string name = GlbReader.Str(p, "name");
                    int m = materials.FindIndex(x => GlbReader.Str((Dictionary<string, object>)x, "name") == name);
                    if (m < 0)
                        continue;
                    var f = GlbReader.Obj(p, "floatProperties");
                    var vec = GlbReader.Obj(p, "vectorProperties");
                    var tex = GlbReader.Obj(p, "textureProperties");
                    int blend = (int)GlbReader.Float(f, "_BlendMode", 0f);
                    var vm = new VrmMaterial
                    {
                        ShadeColor = ColorOf(GlbReader.Floats(vec, "_ShadeColor"), new Color(0.7f, 0.65f, 0.75f)),
                        ShadeImage = GlbReader.Int(tex, "_ShadeTexture") >= 0 ? GlbReader.Int(glb.Item("textures", GlbReader.Int(tex, "_ShadeTexture")), "source") : -1,
                        ShadingShift = GlbReader.Float(f, "_ShadeShift", 0f),
                        ShadingToony = GlbReader.Float(f, "_ShadeToony", 0.9f),
                        RimColor = ColorOf(GlbReader.Floats(vec, "_RimColor"), Color.black),
                        OutlineColor = ColorOf(GlbReader.Floats(vec, "_OutlineColor"), Color.black),
                        OutlineWidth = GlbReader.Float(f, "_OutlineWidthMode", 0f) > 0.5f ? Mathf.Clamp(GlbReader.Float(f, "_OutlineWidth", 0f) * 0.01f, 0f, 0.004f) : 0f,
                        BlendMode = blend == 0 ? "OPAQUE" : blend == 1 ? "MASK" : "BLEND",
                        Cutoff = GlbReader.Float(f, "_Cutoff", 0.5f),
                        Cull = (int)GlbReader.Float(f, "_CullMode", 2f),
                    };
                    info.Materials[m] = vm;
                }
            }
            return info;
        }

        private static IEnumerable<List<int>> Chains(GlbReader glb, int root)
        {
            var result = new List<List<int>>();
            var pending = new Stack<int>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var chain = new List<int>();
                int node = pending.Pop();
                while (node >= 0)
                {
                    chain.Add(node);
                    var children = GlbReader.List(glb.Item("nodes", node), "children");
                    for (int c = children.Count - 1; c >= 1; c--)
                        pending.Push((int)(double)children[c]);
                    node = children.Count > 0 ? (int)(double)children[0] : -1;
                }
                result.Add(chain);
            }
            return result;
        }

        private static Color ColorOf(float[] f, Color fallback) =>
            f == null || f.Length < 3 ? fallback : new Color(f[0], f[1], f[2], f.Length > 3 ? f[3] : 1f);

        private static int ImageOf(GlbReader glb, Dictionary<string, object> textureInfo)
        {
            if (textureInfo == null)
                return -1;
            int texture = GlbReader.Int(textureInfo, "index");
            return texture >= 0 ? GlbReader.Int(glb.Item("textures", texture), "source") : -1;
        }

        /// <summary>VRM humanoid bone name (1.0 spelling) → the shared skeleton's bone name, or null.</summary>
        public static string NyxBone(string vrmBone)
        {
            switch (vrmBone)
            {
                case "hips": return "Hips";
                case "spine": return "Spine";
                case "chest": return "Chest";
                case "upperChest": return "UpperChest";
                case "neck": return "Neck";
                case "head": return "Head";
            }
            foreach (var side in new[] { "left", "right" })
            {
                if (!vrmBone.StartsWith(side))
                    continue;
                string L = side == "left" ? "Left" : "Right";
                string rest = vrmBone.Substring(side.Length);
                switch (rest)
                {
                    case "Shoulder": return L + "Shoulder";
                    case "UpperArm": return L + "UpperArm";
                    case "LowerArm": return L + "LowerArm";
                    case "Hand": return L + "Hand";
                    case "UpperLeg": return L + "UpperLeg";
                    case "LowerLeg": return L + "LowerLeg";
                    case "Foot": return L + "Foot";
                    case "Toes": return L + "Toes";
                }
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    if (!rest.StartsWith(finger))
                        continue;
                    string joint = rest.Substring(finger.Length);
                    int n = finger == "Thumb"
                        ? joint == "Metacarpal" ? 1 : joint == "Proximal" ? 2 : joint == "Distal" ? 3 : 0
                        : joint == "Proximal" ? 1 : joint == "Intermediate" ? 2 : joint == "Distal" ? 3 : 0;
                    return n > 0 ? L + finger + n : null;
                }
            }
            return null;
        }
    }
}
