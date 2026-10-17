using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>
    /// Writes a small but complete VRM file (1.0 or 0.x) from KAI's generated, rigged body: humanoid bone map, skin,
    /// a blink blend shape bound to the "blink" expression, a hair spring chain with a head collider, an MToon
    /// material and a texture, with VRoid-style J_Bip_ / J_Sec_ joint names. VRM 0.x files face -Z like UniVRM's.
    /// Used to test the VRM import path without third-party files; never shipped.
    /// </summary>
    public static class SyntheticVrm
    {
        private sealed class Builder
        {
            public readonly List<byte> Bin = new List<byte>();
            public readonly List<string> Views = new List<string>();
            public readonly List<string> Accessors = new List<string>();

            private int View(byte[] data)
            {
                while (Bin.Count % 4 != 0)
                    Bin.Add(0);
                int offset = Bin.Count;
                Bin.AddRange(data);
                Views.Add($"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{data.Length}}}");
                return Views.Count - 1;
            }

            public int Floats(float[] values, string type, int count, string extra = "")
            {
                var bytes = new byte[values.Length * 4];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                int view = View(bytes);
                Accessors.Add($"{{\"bufferView\":{view},\"componentType\":5126,\"count\":{count},\"type\":\"{type}\"{extra}}}");
                return Accessors.Count - 1;
            }

            public int Ushorts(int[] values, string type, int count)
            {
                var bytes = new byte[values.Length * 2];
                for (int i = 0; i < values.Length; i++)
                    BitConverter.GetBytes((ushort)values[i]).CopyTo(bytes, i * 2);
                int view = View(bytes);
                Accessors.Add($"{{\"bufferView\":{view},\"componentType\":5123,\"count\":{count},\"type\":\"{type}\"}}");
                return Accessors.Count - 1;
            }

            public int Uints(int[] values)
            {
                var bytes = new byte[values.Length * 4];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                int view = View(bytes);
                Accessors.Add($"{{\"bufferView\":{view},\"componentType\":5125,\"count\":{values.Length},\"type\":\"SCALAR\"}}");
                return Accessors.Count - 1;
            }

            public int Image(byte[] png) => View(png);
        }

        private static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>VRM humanoid name of a shared-skeleton bone, or null for helpers.</summary>
        private static string VrmName(string nyx, int version)
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                if (!nyx.StartsWith(side))
                    continue;
                string s = side == "Left" ? "left" : "right";
                string rest = nyx.Substring(side.Length);
                foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    if (!rest.StartsWith(finger))
                        continue;
                    int n = rest[rest.Length - 1] - '0';
                    string joint = finger == "Thumb"
                        ? version == 1 ? (n == 1 ? "Metacarpal" : n == 2 ? "Proximal" : "Distal") : (n == 1 ? "Proximal" : n == 2 ? "Intermediate" : "Distal")
                        : n == 1 ? "Proximal" : n == 2 ? "Intermediate" : "Distal";
                    return s + finger + joint;
                }
                return s + rest;
            }
            return char.ToLowerInvariant(nyx[0]) + nyx.Substring(1);
        }

        /// <summary>Writes the file; returns the number of joints.</summary>
        public static int Write(string path, int version)
        {
            var kai = KaiModel.Build();
            var mesh = kai.Lods[1];
            var skeleton = kai.Skeleton;
            // Unity → glTF: VRM 1.0 mirrors X (faces +Z); VRM 0.x mirrors Z (faces -Z, like UniVRM 0.x exports).
            Vector3 G(Vector3 v) => version == 1 ? new Vector3(-v.x, v.y, v.z) : new Vector3(v.x, v.y, -v.z);
            var b = new Builder();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            int n = vertices.Length;
            var pos = new float[n * 3];
            var nrm = new float[n * 3];
            var uv = new float[n * 2];
            var joints = new int[n * 4];
            var wts = new float[n * 4];
            var blink = new float[n * 3];
            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            var headTop = skeleton["Head"].Tail.y;
            for (int i = 0; i < n; i++)
            {
                var p = G(vertices[i]);
                var q = G(normals[i]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                pos[i * 3] = p.x; pos[i * 3 + 1] = p.y; pos[i * 3 + 2] = p.z;
                nrm[i * 3] = q.x; nrm[i * 3 + 1] = q.y; nrm[i * 3 + 2] = q.z;
                uv[i * 2] = Mathf.Repeat(vertices[i].x * 2f + 0.5f, 1f);
                uv[i * 2 + 1] = vertices[i].y / headTop;
                var w = weights[i];
                joints[i * 4] = w.boneIndex0; joints[i * 4 + 1] = w.boneIndex1; joints[i * 4 + 2] = w.boneIndex2; joints[i * 4 + 3] = w.boneIndex3;
                wts[i * 4] = w.weight0; wts[i * 4 + 1] = w.weight1; wts[i * 4 + 2] = w.weight2; wts[i * 4 + 3] = w.weight3;
                // "Blink": the front of the head (eye level) moves down 4 mm.
                var u = vertices[i];
                if (u.y > headTop - 0.16f && u.y < headTop - 0.09f && u.z > 0.04f)
                    blink[i * 3 + 1] = -0.004f;
            }
            var tris = mesh.GetTriangles(0);
            var indices = new int[tris.Length];
            for (int t = 0; t < tris.Length; t += 3)
            {
                indices[t] = tris[t];
                indices[t + 1] = tris[t + 2]; // mirroring flips handedness: reverse the winding
                indices[t + 2] = tris[t + 1];
            }
            int aPos = b.Floats(pos, "VEC3", n, $",\"min\":[{F(min.x)},{F(min.y)},{F(min.z)}],\"max\":[{F(max.x)},{F(max.y)},{F(max.z)}]");
            int aNrm = b.Floats(nrm, "VEC3", n);
            int aUv = b.Floats(uv, "VEC2", n);
            int aJoints = b.Ushorts(joints, "VEC4", n);
            int aWeights = b.Floats(wts, "VEC4", n);
            int aIndices = b.Uints(indices);
            int aBlink = b.Floats(blink, "VEC3", n);

            // Nodes: 0 = root, 1 = mesh, 2.. = bones.
            var nodes = new List<string> { null, "{\"name\":\"Body\",\"mesh\":0,\"skin\":0}" };
            var boneNode = new Dictionary<string, int>();
            for (int i = 0; i < skeleton.Bones.Count; i++)
                boneNode[skeleton.Bones[i].Name] = 2 + i;
            var children = new Dictionary<int, List<int>>();
            var ibm = new float[skeleton.Bones.Count * 16];
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                var bone = skeleton.Bones[i];
                var parentHead = bone.Parent != null ? skeleton[bone.Parent].Head : Vector3.zero;
                var local = G(bone.Head) - G(parentHead);
                string name = bone.Human != null ? "J_Bip_" + bone.Name : "J_Sec_" + bone.Name;
                nodes.Add($"{{\"name\":\"{name}\",\"translation\":[{F(local.x)},{F(local.y)},{F(local.z)}],\"children\":CHILDREN{i}}}");
                int parent = bone.Parent != null ? boneNode[bone.Parent] : 0;
                if (!children.TryGetValue(parent, out var list))
                    children[parent] = list = new List<int>();
                list.Add(2 + i);
                var head = G(bone.Head);
                var m = Matrix4x4.Translate(-head);
                for (int k = 0; k < 16; k++)
                    ibm[i * 16 + k] = m[k % 4, k / 4];
            }
            nodes[0] = $"{{\"name\":\"Root\",\"children\":[1,{string.Join(",", children[0])}]}}";
            for (int i = 0; i < skeleton.Bones.Count; i++)
                nodes[2 + i] = nodes[2 + i].Replace($"CHILDREN{i}", "[" + (children.TryGetValue(2 + i, out var c) ? string.Join(",", c) : string.Empty) + "]");
            int aIbm = b.Floats(ibm, "MAT4", skeleton.Bones.Count);
            var jointList = new List<int>();
            for (int i = 0; i < skeleton.Bones.Count; i++)
                jointList.Add(2 + i);

            // Texture: a violet-black gradient with a stripe (enough to see UVs and the shade map work).
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, x % 16 < 2 ? new Color(0.55f, 0.3f, 0.9f) : Color.Lerp(new Color(0.08f, 0.07f, 0.11f), new Color(0.95f, 0.85f, 0.78f), y / 63f));
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            int imageView = b.Image(png);

            // VRM extension.
            var human = new StringBuilder();
            foreach (var bone in skeleton.Bones)
            {
                if (bone.Human == null)
                    continue;
                string vrm = VrmName(bone.Name, version);
                if (human.Length > 0)
                    human.Append(',');
                human.Append(version == 1 ? $"\"{vrm}\":{{\"node\":{boneNode[bone.Name]}}}" : $"{{\"bone\":\"{vrm}\",\"node\":{boneNode[bone.Name]},\"useDefaultValues\":true}}");
            }
            int hair1 = boneNode["HairBack1"], hair2 = boneNode["HairBack2"], headNode = boneNode["Head"];
            string extensions, extensionsUsed, materialExt;
            if (version == 1)
            {
                extensionsUsed = "[\"VRMC_vrm\",\"VRMC_springBone\",\"VRMC_materials_mtoon\"]";
                extensions = "{\"VRMC_vrm\":{\"specVersion\":\"1.0\",\"meta\":{\"name\":\"Synthetic KAI\",\"version\":\"1\",\"authors\":[\"WAR OF NYX tests\"],\"licenseUrl\":\"https://vrm.dev/licenses/1.0/\",\"commercialUsage\":\"corporation\",\"avatarPermission\":\"everyone\"}," +
                             $"\"humanoid\":{{\"humanBones\":{{{human}}}}}," +
                             "\"expressions\":{\"preset\":{\"blink\":{\"morphTargetBinds\":[{\"node\":1,\"index\":0,\"weight\":1}],\"isBinary\":false}}}}," +
                             $"\"VRMC_springBone\":{{\"specVersion\":\"1.0\",\"colliders\":[{{\"node\":{headNode},\"shape\":{{\"sphere\":{{\"offset\":[0,0.08,0],\"radius\":0.1}}}}}}],\"colliderGroups\":[{{\"colliders\":[0]}}]," +
                             $"\"springs\":[{{\"joints\":[{{\"node\":{hair1},\"stiffness\":1,\"dragForce\":0.4,\"gravityPower\":0.2,\"hitRadius\":0.02}},{{\"node\":{hair2}}}],\"colliderGroups\":[0]}}]}}}}";
                materialExt = ",\"extensions\":{\"VRMC_materials_mtoon\":{\"specVersion\":\"1.0\",\"shadeColorFactor\":[0.62,0.55,0.72],\"shadeMultiplyTexture\":{\"index\":0},\"shadingShiftFactor\":-0.05,\"shadingToonyFactor\":0.9,\"outlineWidthMode\":\"worldCoordinates\",\"outlineWidthFactor\":0.0015,\"outlineColorFactor\":[0.05,0.03,0.1]}}";
            }
            else
            {
                extensionsUsed = "[\"VRM\"]";
                extensions = "{\"VRM\":{\"specVersion\":\"0.0\",\"meta\":{\"title\":\"Synthetic KAI\",\"author\":\"WAR OF NYX tests\",\"licenseName\":\"CC0\",\"commercialUssageName\":\"Allow\"}," +
                             $"\"humanoid\":{{\"humanBones\":[{human}]}}," +
                             "\"blendShapeMaster\":{\"blendShapeGroups\":[{\"name\":\"Blink\",\"presetName\":\"blink\",\"binds\":[{\"mesh\":0,\"index\":0,\"weight\":100}],\"isBinary\":false}]}," +
                             $"\"secondaryAnimation\":{{\"boneGroups\":[{{\"stiffiness\":1,\"gravityPower\":0.2,\"dragForce\":0.4,\"hitRadius\":0.02,\"bones\":[{hair1}],\"colliderGroups\":[0]}}]," +
                             $"\"colliderGroups\":[{{\"node\":{headNode},\"colliders\":[{{\"offset\":{{\"x\":0,\"y\":0.08,\"z\":0}},\"radius\":0.1}}]}}]}}," +
                             "\"materialProperties\":[{\"name\":\"Body\",\"shader\":\"VRM/MToon\",\"floatProperties\":{\"_BlendMode\":0,\"_ShadeShift\":-0.05,\"_ShadeToony\":0.9,\"_OutlineWidthMode\":1,\"_OutlineWidth\":0.15,\"_CullMode\":2}," +
                             "\"vectorProperties\":{\"_ShadeColor\":[0.62,0.55,0.72,1],\"_OutlineColor\":[0.05,0.03,0.1,1]},\"textureProperties\":{\"_MainTex\":0,\"_ShadeTexture\":0}}]}}";
                materialExt = string.Empty;
            }
            string json = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"WAR OF NYX SyntheticVrm\"}," +
                          $"\"extensionsUsed\":{extensionsUsed},\"extensions\":{extensions}," +
                          "\"scene\":0,\"scenes\":[{\"nodes\":[0]}]," +
                          $"\"nodes\":[{string.Join(",", nodes)}]," +
                          $"\"skins\":[{{\"joints\":[{string.Join(",", jointList)}],\"inverseBindMatrices\":{aIbm},\"skeleton\":{boneNode["Hips"]}}}]," +
                          $"\"meshes\":[{{\"name\":\"Body\",\"extras\":{{\"targetNames\":[\"Fcl_EYE_Close\"]}},\"primitives\":[{{\"attributes\":{{\"POSITION\":{aPos},\"NORMAL\":{aNrm},\"TEXCOORD_0\":{aUv},\"JOINTS_0\":{aJoints},\"WEIGHTS_0\":{aWeights}}},\"indices\":{aIndices},\"material\":0,\"targets\":[{{\"POSITION\":{aBlink}}}]}}]}}]," +
                          $"\"materials\":[{{\"name\":\"Body\",\"pbrMetallicRoughness\":{{\"baseColorTexture\":{{\"index\":0}},\"metallicFactor\":0,\"roughnessFactor\":1}}{materialExt}}}]," +
                          "\"textures\":[{\"source\":0}]," +
                          $"\"images\":[{{\"bufferView\":{imageView},\"mimeType\":\"image/png\"}}]," +
                          $"\"buffers\":[{{\"byteLength\":{b.Bin.Count}}}]," +
                          $"\"bufferViews\":[{string.Join(",", b.Views)}]," +
                          $"\"accessors\":[{string.Join(",", b.Accessors)}]}}";
            var jsonBytes = new List<byte>(Encoding.UTF8.GetBytes(json));
            while (jsonBytes.Count % 4 != 0)
                jsonBytes.Add((byte)' ');
            while (b.Bin.Count % 4 != 0)
                b.Bin.Add(0);
            var file = new List<byte>();
            file.AddRange(BitConverter.GetBytes(0x46546C67u));
            file.AddRange(BitConverter.GetBytes(2u));
            file.AddRange(BitConverter.GetBytes((uint)(12 + 8 + jsonBytes.Count + 8 + b.Bin.Count)));
            file.AddRange(BitConverter.GetBytes((uint)jsonBytes.Count));
            file.AddRange(BitConverter.GetBytes(0x4E4F534Au));
            file.AddRange(jsonBytes);
            file.AddRange(BitConverter.GetBytes((uint)b.Bin.Count));
            file.AddRange(BitConverter.GetBytes(0x004E4942u));
            file.AddRange(b.Bin);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, file.ToArray());
            return skeleton.Bones.Count;
        }
    }
}
