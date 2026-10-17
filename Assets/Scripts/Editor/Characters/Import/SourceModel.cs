using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>A glTF material reduced to what the URP Lit conversion needs.</summary>
    public sealed class SourceMaterial
    {
        public string Name;
        public int BaseColorImage = -1;
        public int MetallicRoughnessImage = -1;
        public int NormalImage = -1;
        public Color BaseColor = Color.white;
        public float Metallic = 1f, Roughness = 1f;
        public bool DoubleSided;
        public string AlphaMode = "OPAQUE";
        public float AlphaCutoff = 0.5f;
    }

    /// <summary>A joint of an embedded skin, in Unity space at the file's rest pose.</summary>
    public sealed class SourceJoint
    {
        public string Name;
        public int Node;
        public int Parent = -1;           // index into the joint list, -1 at the top
        public Matrix4x4 World;           // rest pose (Unity space)
        public Vector3 Position => World.GetColumn(3);
    }

    /// <summary>
    /// A character model read from a .glb and flattened into one vertex list in Unity space, with one triangle list
    /// per material: positions at the file's rest pose (skinned primitives are posed by their joints), normals,
    /// UVs, vertex colours, and the embedded skin (joint indices and weights) when there is one.
    /// </summary>
    public sealed class SourceModel
    {
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector2[] Uv;            // null when the file has none
        public Color[] Colors;          // null when the file has none
        public int[] Joints;            // 4 per vertex (index into SkinJoints), null when unskinned
        public float[] Weights;         // 4 per vertex
        public List<int>[] Triangles;   // per material (Unity winding)
        public SourceMaterial[] Materials;
        public List<SourceJoint> SkinJoints = new List<SourceJoint>();
        public ModelCredit Credit;
        public GlbReader File;
        public int SourceTriangles;

        public int VertexCount => Positions.Length;
        public bool Skinned => Joints != null;

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (var t in Triangles)
                    n += t.Count / 3;
                return n;
            }
        }

        public Bounds Bounds
        {
            get
            {
                var b = new Bounds(Positions[0], Vector3.zero);
                foreach (var p in Positions)
                    b.Encapsulate(p);
                return b;
            }
        }

        public static SourceModel Load(string path)
        {
            var glb = GlbReader.Load(path);
            var model = new SourceModel { File = glb, Credit = glb.Credit() };
            var world = glb.WorldMatrices(out var parents);

            // Materials.
            var materials = new List<SourceMaterial>();
            foreach (Dictionary<string, object> m in glb.Array("materials"))
            {
                var pbr = GlbReader.Obj(m, "pbrMetallicRoughness");
                var sm = new SourceMaterial
                {
                    Name = GlbReader.Str(m, "name") ?? "material" + materials.Count,
                    BaseColorImage = ImageOf(glb, GlbReader.Obj(pbr, "baseColorTexture")),
                    MetallicRoughnessImage = ImageOf(glb, GlbReader.Obj(pbr, "metallicRoughnessTexture")),
                    NormalImage = ImageOf(glb, GlbReader.Obj(m, "normalTexture")),
                    Metallic = GlbReader.Float(pbr, "metallicFactor", 1f),
                    Roughness = GlbReader.Float(pbr, "roughnessFactor", 1f),
                    DoubleSided = m.TryGetValue("doubleSided", out var ds) && ds is bool b && b,
                    AlphaMode = GlbReader.Str(m, "alphaMode") ?? "OPAQUE",
                    AlphaCutoff = GlbReader.Float(m, "alphaCutoff", 0.5f),
                };
                var factor = GlbReader.Floats(pbr, "baseColorFactor");
                if (factor != null)
                    sm.BaseColor = new Color(factor[0], factor[1], factor[2], factor[3]);
                materials.Add(sm);
            }
            if (materials.Count == 0)
                materials.Add(new SourceMaterial { Name = "default" });
            model.Materials = materials.ToArray();

            // Skin joints (the first skin; character files carry one).
            var skins = glb.Array("skins");
            Matrix4x4[] skinMatrices = null;
            if (skins.Count > 0)
            {
                var skin = (Dictionary<string, object>)skins[0];
                var jointNodes = GlbReader.List(skin, "joints");
                var inverseBind = GlbReader.Int(skin, "inverseBindMatrices") >= 0
                    ? glb.ReadFloats(GlbReader.Int(skin, "inverseBindMatrices"), out _)
                    : null;
                skinMatrices = new Matrix4x4[jointNodes.Count];
                var nodeToJoint = new Dictionary<int, int>();
                for (int j = 0; j < jointNodes.Count; j++)
                {
                    int node = (int)(double)jointNodes[j];
                    nodeToJoint[node] = j;
                    var ibm = Matrix4x4.identity;
                    if (inverseBind != null)
                        for (int k = 0; k < 16; k++)
                            ibm[k % 4, k / 4] = inverseBind[j * 16 + k];
                    skinMatrices[j] = world[node] * ibm; // glTF space, rest pose
                    model.SkinJoints.Add(new SourceJoint { Name = glb.NodeName(node), Node = node, World = GlbReader.ToUnity(world[node]) });
                }
                for (int j = 0; j < model.SkinJoints.Count; j++)
                {
                    int p = parents[model.SkinJoints[j].Node];
                    while (p >= 0 && !nodeToJoint.ContainsKey(p))
                        p = parents[p];
                    model.SkinJoints[j].Parent = p >= 0 ? nodeToJoint[p] : -1;
                }
            }

            // Primitives, flattened in Unity space.
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var joints = new List<int>();
            var weights = new List<float>();
            bool anyUv = false, anyColor = false, anySkin = false;
            var triangles = new List<int>[model.Materials.Length];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new List<int>();

            var nodes = glb.Array("nodes");
            for (int ni = 0; ni < nodes.Count; ni++)
            {
                var node = (Dictionary<string, object>)nodes[ni];
                int meshIndex = GlbReader.Int(node, "mesh");
                if (meshIndex < 0)
                    continue;
                bool skinned = GlbReader.Int(node, "skin") >= 0 && skinMatrices != null;
                var mesh = glb.Item("meshes", meshIndex);
                foreach (Dictionary<string, object> prim in GlbReader.List(mesh, "primitives"))
                {
                    int mode = GlbReader.Int(prim, "mode", 4);
                    if (mode != 4)
                        continue; // triangles only
                    var attributes = GlbReader.Obj(prim, "attributes");
                    int baseVertex = positions.Count;
                    var pos = glb.ReadFloats(GlbReader.Int(attributes, "POSITION"), out _);
                    int count = pos.Length / 3;
                    var nrm = GlbReader.Int(attributes, "NORMAL") >= 0 ? glb.ReadFloats(GlbReader.Int(attributes, "NORMAL"), out _) : null;
                    var uv = GlbReader.Int(attributes, "TEXCOORD_0") >= 0 ? glb.ReadFloats(GlbReader.Int(attributes, "TEXCOORD_0"), out _) : null;
                    int colorComponents = 0;
                    var col = GlbReader.Int(attributes, "COLOR_0") >= 0 ? glb.ReadFloats(GlbReader.Int(attributes, "COLOR_0"), out colorComponents) : null;
                    int[] jnt = null;
                    float[] wgt = null;
                    if (skinned && GlbReader.Int(attributes, "JOINTS_0") >= 0)
                    {
                        jnt = glb.ReadInts(GlbReader.Int(attributes, "JOINTS_0"));
                        wgt = glb.ReadFloats(GlbReader.Int(attributes, "WEIGHTS_0"), out _);
                    }
                    var nodeMatrix = world[ni];
                    for (int v = 0; v < count; v++)
                    {
                        var p = new Vector3(pos[v * 3], pos[v * 3 + 1], pos[v * 3 + 2]);
                        var n = nrm != null ? new Vector3(nrm[v * 3], nrm[v * 3 + 1], nrm[v * 3 + 2]) : Vector3.up;
                        Matrix4x4 m;
                        if (jnt != null)
                        {
                            m = Blend(skinMatrices, jnt, wgt, v);
                        }
                        else
                        {
                            m = nodeMatrix;
                        }
                        p = m.MultiplyPoint3x4(p);
                        n = m.MultiplyVector(n).normalized;
                        positions.Add(GlbReader.ToUnity(p));
                        normals.Add(GlbReader.ToUnity(n));
                        if (uv != null)
                        {
                            uvs.Add(new Vector2(uv[v * 2], 1f - uv[v * 2 + 1])); // glTF UV origin is top-left
                            anyUv = true;
                        }
                        else
                        {
                            uvs.Add(Vector2.zero);
                        }
                        if (col != null)
                        {
                            colors.Add(colorComponents == 4
                                ? new Color(col[v * 4], col[v * 4 + 1], col[v * 4 + 2], col[v * 4 + 3])
                                : new Color(col[v * 3], col[v * 3 + 1], col[v * 3 + 2], 1f));
                            anyColor = true;
                        }
                        else
                        {
                            colors.Add(Color.white);
                        }
                        for (int k = 0; k < 4; k++)
                        {
                            joints.Add(jnt != null ? jnt[v * 4 + k] : 0);
                            weights.Add(jnt != null ? wgt[v * 4 + k] : (k == 0 ? 1f : 0f));
                        }
                        if (jnt != null)
                            anySkin = true;
                    }
                    int material = Mathf.Clamp(GlbReader.Int(prim, "material", 0), 0, model.Materials.Length - 1);
                    var list = triangles[material];
                    if (GlbReader.Int(prim, "indices") >= 0)
                    {
                        var idx = glb.ReadInts(GlbReader.Int(prim, "indices"));
                        for (int t = 0; t + 2 < idx.Length; t += 3)
                        {
                            // Mirroring X flips handedness: reverse the winding.
                            list.Add(baseVertex + idx[t]);
                            list.Add(baseVertex + idx[t + 2]);
                            list.Add(baseVertex + idx[t + 1]);
                        }
                    }
                    else
                    {
                        for (int t = 0; t + 2 < count; t += 3)
                        {
                            list.Add(baseVertex + t);
                            list.Add(baseVertex + t + 2);
                            list.Add(baseVertex + t + 1);
                        }
                    }
                }
            }
            if (positions.Count == 0)
                throw new InvalidOperationException(path + ": no triangle meshes found.");
            model.Positions = positions.ToArray();
            model.Normals = normals.ToArray();
            model.Uv = anyUv ? uvs.ToArray() : null;
            model.Colors = anyColor ? colors.ToArray() : null;
            if (anySkin)
            {
                model.Joints = joints.ToArray();
                model.Weights = weights.ToArray();
            }
            model.Triangles = triangles;
            model.SourceTriangles = model.TriangleCount;
            return model;
        }

        private static int ImageOf(GlbReader glb, Dictionary<string, object> textureInfo)
        {
            if (textureInfo == null)
                return -1;
            int texture = GlbReader.Int(textureInfo, "index");
            return texture >= 0 ? GlbReader.Int(glb.Item("textures", texture), "source") : -1;
        }

        private static Matrix4x4 Blend(Matrix4x4[] skin, int[] joints, float[] weights, int v)
        {
            var m = new Matrix4x4();
            float total = 0f;
            for (int k = 0; k < 4; k++)
            {
                float w = weights[v * 4 + k];
                if (w <= 0f)
                    continue;
                var j = skin[joints[v * 4 + k]];
                for (int e = 0; e < 16; e++)
                    m[e] += j[e] * w;
                total += w;
            }
            if (total <= 1e-6f)
                return Matrix4x4.identity;
            for (int e = 0; e < 16; e++)
                m[e] /= total;
            return m;
        }

        // ------------------------------------------------------------------ clean-up

        /// <summary>
        /// Moves the feet to y = 0 and the hips over the origin, then scales to <paramref name="height"/> metres
        /// (skin joints follow). Returns the uniform scale applied.
        /// </summary>
        public float Normalize(float height)
        {
            var b = Bounds;
            float scale = height / Mathf.Max(b.size.y, 1e-4f);
            // Hips over the origin: the median of the vertices at 40-60 % of the height.
            var zs = new List<float>();
            foreach (var p in Positions)
                if (p.y > b.min.y + b.size.y * 0.4f && p.y < b.min.y + b.size.y * 0.6f)
                    zs.Add(p.z);
            zs.Sort();
            float z = zs.Count > 0 ? zs[zs.Count / 2] : b.center.z;
            var offset = new Vector3(-b.center.x, -b.min.y, -z);
            for (int i = 0; i < Positions.Length; i++)
                Positions[i] = (Positions[i] + offset) * scale;
            var m = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(offset);
            foreach (var j in SkinJoints)
                j.World = m * j.World;
            return scale;
        }

        /// <summary>
        /// Merges duplicated vertices (exporters split meshes at 65k vertices and duplicate the seams between parts),
        /// so the surface is one connected piece again for simplification and skin smoothing. With
        /// <paramref name="positionOnly"/> faceted files (one vertex per triangle corner) are welded by position and get
        /// smooth normals. Returns the number of vertices removed.
        /// </summary>
        public int Weld(bool positionOnly)
        {
            var map = new Dictionary<VertexKey, int>(Positions.Length);
            var remap = new int[Positions.Length];
            var order = new List<int>(Positions.Length);
            for (int i = 0; i < Positions.Length; i++)
            {
                var key = new VertexKey(this, i, positionOnly);
                if (!map.TryGetValue(key, out int at))
                {
                    at = order.Count;
                    map[key] = at;
                    order.Add(i);
                }
                remap[i] = at;
            }
            int removed = Positions.Length - order.Count;
            Positions = Pick(Positions, order);
            Normals = Pick(Normals, order);
            if (Uv != null)
                Uv = Pick(Uv, order);
            if (Colors != null)
            {
                if (positionOnly)
                {
                    // Average the colour of the merged corners.
                    var sum = new Color[order.Count];
                    var count = new int[order.Count];
                    for (int i = 0; i < remap.Length; i++)
                    {
                        sum[remap[i]] += Colors[i];
                        count[remap[i]]++;
                    }
                    for (int i = 0; i < sum.Length; i++)
                        sum[i] /= Mathf.Max(1, count[i]);
                    Colors = sum;
                }
                else
                {
                    Colors = Pick(Colors, order);
                }
            }
            if (Joints != null)
            {
                var j = new int[order.Count * 4];
                var w = new float[order.Count * 4];
                for (int i = 0; i < order.Count; i++)
                    for (int k = 0; k < 4; k++)
                    {
                        j[i * 4 + k] = Joints[order[i] * 4 + k];
                        w[i * 4 + k] = Weights[order[i] * 4 + k];
                    }
                Joints = j;
                Weights = w;
            }
            foreach (var list in Triangles)
            {
                var kept = new List<int>(list.Count);
                for (int t = 0; t < list.Count; t += 3)
                {
                    int a = remap[list[t]], b = remap[list[t + 1]], c = remap[list[t + 2]];
                    if (a == b || b == c || a == c)
                        continue; // collapsed by the weld
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }
                list.Clear();
                list.AddRange(kept);
            }
            if (positionOnly)
                Normals = SmoothNormals();
            return removed;
        }

        private static T[] Pick<T>(T[] source, List<int> order)
        {
            var r = new T[order.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = source[order[i]];
            return r;
        }

        /// <summary>Area-weighted vertex normals from the triangles.</summary>
        public Vector3[] SmoothNormals()
        {
            var n = new Vector3[Positions.Length];
            foreach (var list in Triangles)
                for (int t = 0; t < list.Count; t += 3)
                {
                    var face = Vector3.Cross(Positions[list[t + 1]] - Positions[list[t]], Positions[list[t + 2]] - Positions[list[t]]);
                    n[list[t]] += face;
                    n[list[t + 1]] += face;
                    n[list[t + 2]] += face;
                }
            for (int i = 0; i < n.Length; i++)
                n[i] = n[i].sqrMagnitude > 1e-20f ? n[i].normalized : Vector3.up;
            return n;
        }

        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            private readonly long _p, _n, _uv, _j;

            public VertexKey(SourceModel m, int i, bool positionOnly)
            {
                _p = Pack(m.Positions[i], 1e5f);
                _n = positionOnly ? 0 : Pack(m.Normals[i], 2e3f);
                _uv = 0;
                if (!positionOnly && m.Uv != null)
                    _uv = ((long)Mathf.RoundToInt(m.Uv[i].x * 1e6f) << 32) | (uint)Mathf.RoundToInt(m.Uv[i].y * 1e6f);
                _j = 0;
                if (!positionOnly && m.Joints != null)
                    _j = ((long)m.Joints[i * 4] << 32) | (uint)Mathf.RoundToInt(m.Weights[i * 4] * 1e4f);
            }

            private static long Pack(Vector3 v, float q)
            {
                long x = Mathf.RoundToInt(v.x * q), y = Mathf.RoundToInt(v.y * q), z = Mathf.RoundToInt(v.z * q);
                return (x & 0x1FFFFF) | ((y & 0x1FFFFF) << 21) | ((z & 0x1FFFFF) << 42);
            }

            public bool Equals(VertexKey o) => _p == o._p && _n == o._n && _uv == o._uv && _j == o._j;
            public override bool Equals(object obj) => obj is VertexKey k && Equals(k);
            public override int GetHashCode() => (_p * 486187739L + _n * 31L + _uv * 17L + _j).GetHashCode();
        }
    }
}
