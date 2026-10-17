using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>
    /// Automatic skin weights for a T-pose model on a fitted <see cref="NyxSkeleton"/>. Each vertex takes the bones
    /// whose segment it is closest to, measured in units of that body part's thickness (so an arm bone does not pull
    /// the side of the chest, and joints blend over about one limb radius, like hand-painted weights), restricted to
    /// the bones that can own that region (left leg bones never reach the right thigh, hair stays on the head), then
    /// smoothed over the surface and limited to four influences.
    /// </summary>
    public static class AutoSkinner
    {
        private const float Falloff = 6f;

        public static BoneWeight[] Skin(NyxSkeleton skeleton, HumanoidFit fit, Vector3[] positions, int[] vertices, List<int>[] triangles,
            int smoothing = 4, bool fingers = true)
        {
            int boneCount = skeleton.Bones.Count;
            if (boneCount > 64)
                throw new System.InvalidOperationException("AutoSkinner supports up to 64 bones.");
            var heads = new Vector3[boneCount];
            var tails = new Vector3[boneCount];
            var radius = new float[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                var bone = skeleton.Bones[b];
                heads[b] = bone.Head;
                tails[b] = bone.Tail;
                radius[b] = fit.Radius.TryGetValue(bone.Name, out float r) ? r : 0.03f * fit.Height;
            }
            int I(string name) => skeleton.IndexOf(name);
            var torso = new[] { I("Spine"), I("Chest"), I("UpperChest") };
            int hips = I("Hips"), neck = I("Neck"), head = I("Head");
            float chestY = skeleton["Chest"].Head.y, upperChestY = skeleton["UpperChest"].Head.y, headY = skeleton["Head"].Head.y;
            float neckR = radius[neck];

            ulong Allowed(Vector3 p)
            {
                ulong m = 0;
                void Add(int b)
                {
                    if (b >= 0)
                        m |= 1UL << b;
                }
                int sideOf = p.x < 0f ? 0 : 1;
                bool inTorso = Mathf.Abs(p.x) < fit.ArmpitX[sideOf] + 0.03f && p.y > fit.CrotchY - 0.08f;
                // Hair and anything else hanging off the head, beside or behind the neck.
                if (inTorso && p.y > fit.NeckY + 0.03f && new Vector2(p.x, p.z - skeleton["Neck"].Head.z).magnitude > 1.5f * neckR)
                {
                    Add(head);
                    return m;
                }
                if (inTorso)
                {
                    foreach (int b in torso)
                        Add(b);
                    if (p.y < chestY)
                        Add(hips);
                    if (p.y > upperChestY - 0.03f)
                    {
                        Add(neck);
                        Add(head);
                    }
                    if (p.y > chestY)
                        Add(I(sideOf == 0 ? "LeftShoulder" : "RightShoulder"));
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    string L = side < 0 ? "Left" : "Right";
                    int si = HumanoidFit.SideIndex(side);
                    if (p.y < fit.HipY + 0.1f && side * p.x > -0.01f)
                    {
                        Add(I(L + "UpperLeg"));
                        Add(I(L + "LowerLeg"));
                        Add(I(L + "Foot"));
                        Add(I(L + "Toes"));
                        if (p.y > fit.CrotchY - 0.12f)
                            Add(hips);
                    }
                    if (side * p.x > fit.ShoulderX[si] - 0.05f && p.y > chestY - 0.05f)
                    {
                        Add(I(L + "Shoulder"));
                        Add(I(L + "UpperArm"));
                        Add(I(L + "LowerArm"));
                        Add(I(L + "Hand"));
                        if (fingers && side * p.x > fit.WristX[si] - 0.01f)
                            foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                                for (int j = 1; j <= 3; j++)
                                    Add(I(L + f + j));
                    }
                }
                if (m == 0)
                    for (int b = 0; b < boneCount; b++)
                        Add(b);
                return m;
            }

            // Work per surface position: copies of a vertex on a UV seam must get identical weights, or the seam
            // opens into a crack when the body bends.
            var pidOf = new Dictionary<Vector3, int>(vertices.Length);
            var vertexPid = new int[vertices.Length];
            var points = new List<Vector3>(vertices.Length);
            for (int v = 0; v < vertices.Length; v++)
            {
                var p = positions[vertices[v]];
                if (!pidOf.TryGetValue(p, out int pid))
                {
                    pid = points.Count;
                    pidOf[p] = pid;
                    points.Add(p);
                }
                vertexPid[v] = pid;
            }
            int n = points.Count;
            var allowed = new ulong[n];
            var w = new float[n * boneCount];
            for (int v = 0; v < n; v++)
            {
                var p = points[v];
                ulong m = Allowed(p);
                allowed[v] = m;
                float sum = 0f;
                for (int b = 0; b < boneCount; b++)
                {
                    if ((m & (1UL << b)) == 0)
                        continue;
                    float d = SegmentDistance(p, heads[b], tails[b]) / radius[b];
                    float x = 1f / Mathf.Pow(Mathf.Max(d, 0.3f), Falloff);
                    w[v * boneCount + b] = x;
                    sum += x;
                }
                for (int b = 0; b < boneCount; b++)
                    w[v * boneCount + b] /= sum;
            }

            // Smoothing over the surface (removes speckles where two bones are about equally near).
            if (smoothing > 0)
            {
                var local = new Dictionary<int, int>(vertices.Length);
                for (int v = 0; v < vertices.Length; v++)
                    local[vertices[v]] = vertexPid[v];
                var neighbours = new List<int>[n];
                for (int v = 0; v < n; v++)
                    neighbours[v] = new List<int>(6);
                foreach (var list in triangles)
                    for (int t = 0; t < list.Count; t += 3)
                        for (int e = 0; e < 3; e++)
                        {
                            if (!local.TryGetValue(list[t + e], out int a) || !local.TryGetValue(list[t + (e + 1) % 3], out int c) || a == c)
                                continue;
                            if (!neighbours[a].Contains(c))
                                neighbours[a].Add(c);
                            if (!neighbours[c].Contains(a))
                                neighbours[c].Add(a);
                        }
                var next = new float[w.Length];
                for (int iteration = 0; iteration < smoothing; iteration++)
                {
                    for (int v = 0; v < n; v++)
                    {
                        var nb = neighbours[v];
                        int o = v * boneCount;
                        float sum = 0f;
                        for (int b = 0; b < boneCount; b++)
                        {
                            float x = 0f;
                            if ((allowed[v] & (1UL << b)) != 0)
                            {
                                float avg = 0f;
                                foreach (int u in nb)
                                    avg += w[u * boneCount + b];
                                x = nb.Count > 0 ? 0.55f * w[o + b] + 0.45f * avg / nb.Count : w[o + b];
                            }
                            next[o + b] = x;
                            sum += x;
                        }
                        if (sum > 1e-6f)
                            for (int b = 0; b < boneCount; b++)
                                next[o + b] /= sum;
                        else
                            for (int b = 0; b < boneCount; b++)
                                next[o + b] = w[o + b];
                    }
                    var swap = w;
                    w = next;
                    next = swap;
                }
            }

            var byPosition = new BoneWeight[n];
            for (int v = 0; v < n; v++)
                byPosition[v] = Top4(w, v * boneCount, boneCount);
            var result = new BoneWeight[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
                result[v] = byPosition[vertexPid[v]];
            return result;
        }

        public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>The four strongest influences of a dense weight row, renormalised (tiny weights dropped).</summary>
        public static BoneWeight Top4(float[] w, int offset, int count)
        {
            int i0 = -1, i1 = -1, i2 = -1, i3 = -1;
            for (int b = 0; b < count; b++)
            {
                float x = w[offset + b];
                if (x <= 0f)
                    continue;
                if (i0 < 0 || x > w[offset + i0]) { i3 = i2; i2 = i1; i1 = i0; i0 = b; }
                else if (i1 < 0 || x > w[offset + i1]) { i3 = i2; i2 = i1; i1 = b; }
                else if (i2 < 0 || x > w[offset + i2]) { i3 = i2; i2 = b; }
                else if (i3 < 0 || x > w[offset + i3]) { i3 = b; }
            }
            float W(int i) => i >= 0 && w[offset + i] > 0.01f ? w[offset + i] : 0f;
            float w0 = W(i0), w1 = W(i1), w2 = W(i2), w3 = W(i3);
            float sum = w0 + w1 + w2 + w3;
            if (sum <= 0f)
                return new BoneWeight { boneIndex0 = System.Math.Max(i0, 0), weight0 = 1f };
            return new BoneWeight
            {
                boneIndex0 = System.Math.Max(i0, 0), weight0 = w0 / sum,
                boneIndex1 = System.Math.Max(i1, 0), weight1 = w1 / sum,
                boneIndex2 = System.Math.Max(i2, 0), weight2 = w2 / sum,
                boneIndex3 = System.Math.Max(i3, 0), weight3 = w3 / sum,
            };
        }
    }
}
