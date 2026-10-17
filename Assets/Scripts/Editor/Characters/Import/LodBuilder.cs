using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>One simplified level: triangles per material, indexing the source model's vertices.</summary>
    public sealed class LodLevel
    {
        public List<int>[] Triangles;

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
    }

    /// <summary>
    /// Simplifies a dense source mesh with Unity's own Mesh LOD generator (<see cref="MeshLodUtility"/>, attribute-aware
    /// edge collapse that keeps UV seams and silhouettes) and returns every generated level as index lists into the
    /// original vertices, so per-vertex data (UVs, colours, skin weights) carries over exactly.
    /// </summary>
    public static class LodBuilder
    {
        public static List<LodLevel> Generate(SourceModel model)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "lod-source" };
            mesh.SetVertices(model.Positions);
            mesh.SetNormals(model.Normals);
            if (model.Uv != null)
                mesh.SetUVs(0, model.Uv);
            if (model.Colors != null)
                mesh.SetColors(model.Colors);
            mesh.subMeshCount = model.Triangles.Length;
            for (int s = 0; s < model.Triangles.Length; s++)
                mesh.SetTriangles(model.Triangles[s], s, false);
            mesh.RecalculateBounds();
            try
            {
                MeshLodUtility.GenerateMeshLods(mesh, -1);
                int levels = Mathf.Max(1, mesh.lodCount);
                var result = new List<LodLevel>();
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var indices = data[0].GetIndexData<int>();
                    var ranges = new MeshLodRange[mesh.subMeshCount][];
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        ranges[s] = mesh.GetLods(s);
                    for (int level = 0; level < levels; level++)
                    {
                        var lod = new LodLevel { Triangles = new List<int>[mesh.subMeshCount] };
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            var descriptor = mesh.GetSubMesh(s);
                            var list = new List<int>();
                            if (level < ranges[s].Length)
                            {
                                var r = ranges[s][level];
                                int start = descriptor.indexStart + (int)r.indexStart;
                                for (int i = 0; i < r.indexCount; i++)
                                    list.Add(indices[start + i] + descriptor.baseVertex);
                            }
                            else if (level == 0)
                            {
                                list.AddRange(model.Triangles[s]);
                            }
                            lod.Triangles[s] = list;
                        }
                        result.Add(lod);
                    }
                }
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>The level whose triangle count is closest to (and at most about) the target.</summary>
        public static LodLevel Pick(List<LodLevel> levels, int targetTriangles)
        {
            LodLevel best = levels[levels.Count - 1];
            foreach (var l in levels)
            {
                if (l.TriangleCount <= targetTriangles * 1.08f)
                {
                    best = l;
                    break;
                }
            }
            return best;
        }

        /// <summary>Batch probe: -executeMethod ...LodBuilder.Probe -glb path [-glbHeight 1.7]</summary>
        public static void Probe()
        {
            var args = Environment.GetCommandLineArgs();
            string path = null;
            float height = 1.7f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-glb")
                    path = args[i + 1];
                if (args[i] == "-glbHeight")
                    height = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var model = SourceModel.Load(path);
            Debug.Log($"[Probe] loaded {model.VertexCount} vertices, {model.TriangleCount} triangles, {model.Materials.Length} materials, skin joints {model.SkinJoints.Count} in {clock.ElapsedMilliseconds} ms; credit {model.Credit}");
            clock.Restart();
            model.Normalize(height);
            int removed = model.Weld(model.Uv == null);
            Debug.Log($"[Probe] weld removed {removed} -> {model.VertexCount} vertices, {model.TriangleCount} triangles, bounds {model.Bounds} in {clock.ElapsedMilliseconds} ms");
            clock.Restart();
            var levels = Generate(model);
            Debug.Log($"[Probe] {levels.Count} LOD levels in {clock.ElapsedMilliseconds} ms");
            HashSet<int> previous = null;
            for (int l = 0; l < levels.Count; l++)
            {
                var used = new HashSet<int>();
                foreach (var t in levels[l].Triangles)
                    used.UnionWith(t);
                bool subset = previous == null || previous.IsSupersetOf(used);
                Debug.Log($"[Probe]   level {l}: {levels[l].TriangleCount} triangles, {used.Count} vertices, subset of previous: {subset}");
                previous = used;
            }
        }
    }
}
