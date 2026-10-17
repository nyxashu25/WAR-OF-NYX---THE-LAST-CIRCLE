using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>
    /// Minimal JSON reader for glTF documents: objects become <see cref="Dictionary{TKey,TValue}"/>, arrays
    /// <see cref="List{T}"/>, numbers double, plus string / bool / null. No dependencies.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            var value = Value(text, ref i);
            return value;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var obj = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}')
                {
                    i++;
                    return obj;
                }
                while (true)
                {
                    Skip(s, ref i);
                    string key = String(s, ref i);
                    Skip(s, ref i);
                    i++; // ':'
                    obj[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i++] == '}')
                        return obj;
                }
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    list.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i++] == ']')
                        return list;
                }
            }
            if (c == '"')
                return String(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0)
            {
                i += 4;
                return true;
            }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0)
            {
                i += 5;
                return false;
            }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0)
            {
                i += 4;
                return null;
            }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
                i++;
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string String(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }
    }

    /// <summary>
    /// Reads a binary glTF 2.0 file (.glb): the JSON document, the binary buffer, typed accessors, node transforms and
    /// embedded images. Converts to Unity space on request: glTF is right-handed (+Y up, the model faces +Z), Unity
    /// left-handed, so X is mirrored and triangle winding reversed. Supports what character models from Sketchfab, Tripo,
    /// VRoid and Blender exports use (no Draco / meshopt compression, no sparse accessors).
    /// </summary>
    public sealed class GlbReader
    {
        public readonly Dictionary<string, object> Json;
        public readonly byte[] Bin;
        public readonly string Path;

        private GlbReader(string path, Dictionary<string, object> json, byte[] bin)
        {
            Path = path;
            Json = json;
            Bin = bin;
        }

        public static GlbReader Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 20 || BitConverter.ToUInt32(data, 0) != 0x46546C67)
                throw new InvalidDataException(path + " is not a binary glTF (.glb) file.");
            uint version = BitConverter.ToUInt32(data, 4);
            if (version != 2)
                throw new InvalidDataException($"{path}: glTF version {version} is not supported (2.0 only).");
            int length = (int)BitConverter.ToUInt32(data, 8);
            int offset = 12;
            Dictionary<string, object> json = null;
            byte[] bin = null;
            while (offset < length)
            {
                int chunkLength = (int)BitConverter.ToUInt32(data, offset);
                uint type = BitConverter.ToUInt32(data, offset + 4);
                if (type == 0x4E4F534A)
                    json = (Dictionary<string, object>)MiniJson.Parse(Encoding.UTF8.GetString(data, offset + 8, chunkLength));
                else if (type == 0x004E4942)
                {
                    bin = new byte[chunkLength];
                    Buffer.BlockCopy(data, offset + 8, bin, 0, chunkLength);
                }
                offset += 8 + chunkLength;
            }
            if (json == null)
                throw new InvalidDataException(path + ": no JSON chunk.");
            var required = List(json, "extensionsRequired");
            if (required.Count > 0)
                throw new NotSupportedException($"{path}: required glTF extensions are not supported: {string.Join(", ", required)}");
            return new GlbReader(path, json, bin ?? System.Array.Empty<byte>());
        }

        // ------------------------------------------------------------------ JSON helpers

        public static List<object> List(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static Dictionary<string, object> Obj(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static int Int(Dictionary<string, object> o, string key, int fallback = -1) =>
            o != null && o.TryGetValue(key, out var v) && v is double d ? (int)d : fallback;

        public static float Float(Dictionary<string, object> o, string key, float fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is double d ? (float)d : fallback;

        public static string Str(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as string : null;

        public static float[] Floats(Dictionary<string, object> o, string key)
        {
            var l = List(o, key);
            if (l.Count == 0)
                return null;
            var a = new float[l.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = (float)(double)l[i];
            return a;
        }

        public List<object> Array(string key) => List(Json, key);
        public Dictionary<string, object> Item(string key, int index) => (Dictionary<string, object>)List(Json, key)[index];

        // ------------------------------------------------------------------ accessors

        private static int Components(string type)
        {
            switch (type)
            {
                case "SCALAR": return 1;
                case "VEC2": return 2;
                case "VEC3": return 3;
                case "VEC4": return 4;
                case "MAT4": return 16;
                default: throw new NotSupportedException("accessor type " + type);
            }
        }

        public int Count(int accessor) => Int(Item("accessors", accessor), "count");

        /// <summary>Reads an accessor as floats (normalized integers are mapped to 0..1 / -1..1), count × components.</summary>
        public float[] ReadFloats(int accessor, out int components)
        {
            var a = Item("accessors", accessor);
            if (a.ContainsKey("sparse"))
                throw new NotSupportedException("sparse accessors are not supported");
            int count = Int(a, "count");
            components = Components(Str(a, "type"));
            int componentType = Int(a, "componentType");
            bool normalized = a.TryGetValue("normalized", out var n) && n is bool b && b;
            var result = new float[count * components];
            if (!a.ContainsKey("bufferView"))
                return result;
            var view = Item("bufferViews", Int(a, "bufferView"));
            int size = ComponentSize(componentType);
            int stride = Int(view, "byteStride", 0);
            if (stride == 0)
                stride = size * components;
            int start = Int(view, "byteOffset", 0) + Int(a, "byteOffset", 0);
            for (int i = 0; i < count; i++)
            {
                int at = start + i * stride;
                for (int c = 0; c < components; c++)
                    result[i * components + c] = Component(componentType, at + c * size, normalized);
            }
            return result;
        }

        /// <summary>Reads an integer accessor (indices, joint ids).</summary>
        public int[] ReadInts(int accessor)
        {
            var a = Item("accessors", accessor);
            int count = Int(a, "count");
            int components = Components(Str(a, "type"));
            int componentType = Int(a, "componentType");
            var result = new int[count * components];
            var view = Item("bufferViews", Int(a, "bufferView"));
            int size = ComponentSize(componentType);
            int stride = Int(view, "byteStride", 0);
            if (stride == 0)
                stride = size * components;
            int start = Int(view, "byteOffset", 0) + Int(a, "byteOffset", 0);
            for (int i = 0; i < count; i++)
            {
                int at = start + i * stride;
                for (int c = 0; c < components; c++)
                {
                    int p = at + c * size;
                    result[i * components + c] = componentType switch
                    {
                        5121 => Bin[p],
                        5123 => BitConverter.ToUInt16(Bin, p),
                        5125 => (int)BitConverter.ToUInt32(Bin, p),
                        5120 => (sbyte)Bin[p],
                        5122 => BitConverter.ToInt16(Bin, p),
                        _ => throw new NotSupportedException("integer component type " + componentType),
                    };
                }
            }
            return result;
        }

        private static int ComponentSize(int componentType) => componentType switch
        {
            5120 or 5121 => 1,
            5122 or 5123 => 2,
            5125 or 5126 => 4,
            _ => throw new NotSupportedException("component type " + componentType),
        };

        private float Component(int componentType, int p, bool normalized)
        {
            switch (componentType)
            {
                case 5126: return BitConverter.ToSingle(Bin, p);
                case 5121: return normalized ? Bin[p] / 255f : Bin[p];
                case 5123: return normalized ? BitConverter.ToUInt16(Bin, p) / 65535f : BitConverter.ToUInt16(Bin, p);
                case 5125: return BitConverter.ToUInt32(Bin, p);
                case 5120: return normalized ? Mathf.Max((sbyte)Bin[p] / 127f, -1f) : (sbyte)Bin[p];
                case 5122: return normalized ? Mathf.Max(BitConverter.ToInt16(Bin, p) / 32767f, -1f) : BitConverter.ToInt16(Bin, p);
                default: throw new NotSupportedException("component type " + componentType);
            }
        }

        // ------------------------------------------------------------------ nodes

        /// <summary>The node's local transform as a glTF (right-handed) matrix.</summary>
        public Matrix4x4 LocalMatrix(int node)
        {
            var n = Item("nodes", node);
            var m = Floats(n, "matrix");
            if (m != null)
            {
                var r = new Matrix4x4();
                for (int i = 0; i < 16; i++)
                    r[i % 4, i / 4] = m[i]; // column-major
                return r;
            }
            var t = Floats(n, "translation") ?? new[] { 0f, 0f, 0f };
            var q = Floats(n, "rotation") ?? new[] { 0f, 0f, 0f, 1f };
            var s = Floats(n, "scale") ?? new[] { 1f, 1f, 1f };
            return Matrix4x4.TRS(new Vector3(t[0], t[1], t[2]), new Quaternion(q[0], q[1], q[2], q[3]).normalized, new Vector3(s[0], s[1], s[2]));
        }

        /// <summary>World matrices (glTF space) of every node in the default scene's hierarchy.</summary>
        public Matrix4x4[] WorldMatrices(out int[] parents)
        {
            var nodes = Array("nodes");
            var world = new Matrix4x4[nodes.Count];
            parents = new int[nodes.Count];
            for (int i = 0; i < parents.Length; i++)
            {
                parents[i] = -1;
                world[i] = Matrix4x4.identity;
            }
            for (int i = 0; i < nodes.Count; i++)
                foreach (var c in List((Dictionary<string, object>)nodes[i], "children"))
                    parents[(int)(double)c] = i;
            var done = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                Resolve(i, world, parents, done);
            return world;
        }

        private void Resolve(int i, Matrix4x4[] world, int[] parents, bool[] done)
        {
            if (done[i])
                return;
            if (parents[i] >= 0)
            {
                Resolve(parents[i], world, parents, done);
                world[i] = world[parents[i]] * LocalMatrix(i);
            }
            else
            {
                world[i] = LocalMatrix(i);
            }
            done[i] = true;
        }

        public string NodeName(int node) => Str(Item("nodes", node), "name") ?? "node" + node;

        // ------------------------------------------------------------------ images

        /// <summary>The encoded bytes (PNG / JPEG) of an embedded image, and its MIME type.</summary>
        public byte[] ImageBytes(int image, out string mime)
        {
            var img = Item("images", image);
            mime = Str(img, "mimeType") ?? "image/png";
            if (!img.ContainsKey("bufferView"))
                throw new NotSupportedException("external image files are not supported (embed them in the .glb)");
            var view = Item("bufferViews", Int(img, "bufferView"));
            var bytes = new byte[Int(view, "byteLength")];
            Buffer.BlockCopy(Bin, Int(view, "byteOffset", 0), bytes, 0, bytes.Length);
            if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                mime = "image/jpeg";
            else if (bytes.Length > 3 && bytes[0] == 0x89 && bytes[1] == 0x50)
                mime = "image/png";
            return bytes;
        }

        // ------------------------------------------------------------------ space conversion

        private static readonly Matrix4x4 Mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        /// <summary>glTF (right-handed) matrix to Unity (left-handed): mirror X on both sides.</summary>
        public static Matrix4x4 ToUnity(Matrix4x4 m) => Mirror * m * Mirror;

        public static Vector3 ToUnity(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>The asset's credit (Sketchfab writes title, author, license and source into asset.extras).</summary>
        public ModelCredit Credit()
        {
            var extras = Obj(Obj(Json, "asset"), "extras");
            return new ModelCredit
            {
                Title = Str(extras, "title") ?? System.IO.Path.GetFileNameWithoutExtension(Path),
                Author = Str(extras, "author") ?? "unknown",
                License = Str(extras, "license") ?? "unknown",
                Source = Str(extras, "source") ?? string.Empty,
                Generator = Str(Obj(Json, "asset"), "generator") ?? string.Empty,
            };
        }
    }

    /// <summary>Attribution of an imported model (shown on the CHARACTERS screen and in the licensing record).</summary>
    [Serializable]
    public struct ModelCredit
    {
        public string Title, Author, License, Source, Generator;

        public override string ToString() => $"\"{Title}\" by {Author}, {License}" + (string.IsNullOrEmpty(Source) ? string.Empty : $", {Source}");
    }
}
