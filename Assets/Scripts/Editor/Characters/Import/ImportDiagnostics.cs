using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>QA for imported characters: which bones own the vertices, and whether bending a bone moves its part of the mesh.</summary>
    public static class ImportDiagnostics
    {
        public static void Run()
        {
            foreach (var spec in ImportedCharacterCatalog.All)
            {
                var profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>($"{CharacterAssetBuilder.ResourcesRoot}/{spec.Slot}.asset");
                if (profile == null)
                    continue;
                var go = (GameObject)Object.Instantiate(profile.prefab);
                var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>()[0];
                var mesh = smr.sharedMesh;
                var bones = smr.bones;
                var counts = new Dictionary<string, int>();
                var weights = mesh.boneWeights;
                foreach (var w in weights)
                {
                    string n = bones[w.boneIndex0].name;
                    counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
                }
                var list = new List<KeyValuePair<string, int>>(counts);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                Debug.Log($"[Diag] {spec.Slot}: {mesh.vertexCount} vertices, bindposes {mesh.bindposes.Length}, bones {bones.Length}, dominant: " +
                          string.Join(", ", list.ConvertAll(p => p.Key + "=" + p.Value)));
                var rest = new Mesh();
                smr.BakeMesh(rest, true);
                var thigh = System.Array.Find(bones, b => b.name == "LeftUpperLeg");
                var forearm = System.Array.Find(bones, b => b.name == "RightLowerArm");
                thigh.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                forearm.localRotation = Quaternion.Euler(0f, 70f, 0f);
                var posed = new Mesh();
                smr.BakeMesh(posed, true);
                var a = rest.vertices;
                var b2 = posed.vertices;
                float maxMove = 0f;
                int moved = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    float d = Vector3.Distance(a[i], b2[i]);
                    maxMove = Mathf.Max(maxMove, d);
                    if (d > 0.05f)
                        moved++;
                }
                // Foot vertices (lowest on the left) should swing forward with the thigh.
                int foot = 0;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].y < 0.1f && a[i].x < -0.02f && b2[i].z - a[i].z > 0.3f)
                        foot++;
                Debug.Log($"[Diag] {spec.Slot}: thigh bent 60° and forearm 70°: {moved} vertices moved > 5 cm (max {maxMove:0.00} m), left-foot vertices swung forward: {foot}; " +
                          $"thigh bone at {thigh.position}, rest pos of bone vs bindpose: {(bones[0].localToWorldMatrix * mesh.bindposes[0]).GetColumn(3)}");
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Debug.Log($"[Diag] {spec.Slot} {r.sharedMesh.name}: open edges (cracks) {OpenEdges(r.sharedMesh, out var sample)} e.g. at {sample}");
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Edges used by one triangle only, after merging vertices that share a position (UV seams are not cracks).</summary>
        public static int OpenEdges(Mesh mesh, out Vector3 sample)
        {
            var v = mesh.vertices;
            var id = new Dictionary<Vector3, int>();
            var pos = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                if (!id.TryGetValue(v[i], out int k))
                    id[v[i]] = k = id.Count;
                pos[i] = k;
            }
            var count = new Dictionary<long, int>();
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = pos[t[i + e]], b = pos[t[i + (e + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            int open = 0;
            sample = Vector3.zero;
            var byPos = new Vector3[id.Count];
            foreach (var pair in id)
                byPos[pair.Value] = pair.Key;
            foreach (var pair in count)
                if (pair.Value == 1)
                {
                    if (open == 0)
                        sample = byPos[(int)(pair.Key >> 32)];
                    open++;
                }
            return open;
        }
    }
}
