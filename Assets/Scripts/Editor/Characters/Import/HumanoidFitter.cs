using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>Joint positions, leaf tips and body-part radii measured on a model (feet at y = 0, facing +Z).</summary>
    public sealed class HumanoidFit
    {
        public readonly Dictionary<string, Vector3> Heads = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, Vector3> Tails = new Dictionary<string, Vector3>();
        /// <summary>Typical distance from the bone to the surface (skinning uses distance / radius).</summary>
        public readonly Dictionary<string, float> Radius = new Dictionary<string, float>();
        public readonly List<string> Notes = new List<string>();
        public float Height, CrotchY, HipY, ShoulderY, NeckY;
        public readonly float[] ArmpitX = new float[2], WristX = new float[2], ShoulderX = new float[2];
        public bool FingersMeasured;

        public static int SideIndex(int side) => side < 0 ? 0 : 1;
    }

    /// <summary>
    /// Measures a humanoid in a T-pose (arms out along ±X, palms down, legs down, facing +Z, feet at y = 0): cross
    /// sections along the arms and legs find the armpits, wrists (narrowest section near the hands), crotch (where the
    /// legs separate), ankles and feet; the spine and neck follow body proportions and the torso's centre line; the
    /// fingers are found by clustering the separated finger tips and tracing the webs between them. The joint names
    /// are those of <see cref="Gameplay.Characters.NyxSkeleton"/>. Works on a dense surface (tens of thousands of
    /// vertices or more); every guess is written to <see cref="HumanoidFit.Notes"/> for the import report.
    /// </summary>
    public sealed class HumanoidFitter
    {
        private readonly Vector3[] _p;
        private readonly int[] _byX, _byY;
        private readonly float[] _xs, _ys;
        private readonly float _h;
        private readonly HumanoidFit _fit = new HumanoidFit();

        private HumanoidFitter(Vector3[] positions)
        {
            _p = positions;
            _byX = new int[positions.Length];
            _byY = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                _byX[i] = _byY[i] = i;
            Array.Sort(_byX, (a, b) => positions[a].x.CompareTo(positions[b].x));
            Array.Sort(_byY, (a, b) => positions[a].y.CompareTo(positions[b].y));
            _xs = new float[positions.Length];
            _ys = new float[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                _xs[i] = positions[_byX[i]].x;
                _ys[i] = positions[_byY[i]].y;
                _h = Mathf.Max(_h, positions[i].y);
            }
            _fit.Height = _h;
        }

        public static HumanoidFit FitTPose(Vector3[] positions) => FitTPose(positions, null);

        /// <summary>
        /// With <paramref name="triangles"/> (indices into <paramref name="positions"/>), the surface is first sampled
        /// about every 4 mm, so cross-sections are reliable on sparse meshes too (rings of a procedural limb, low-poly
        /// files), not only on dense scans.
        /// </summary>
        public static HumanoidFit FitTPose(Vector3[] positions, IList<int> triangles)
        {
            var f = new HumanoidFitter(triangles != null ? Densify(positions, triangles, 0.004f) : positions);
            f.Arms();
            f.Legs();
            f.Spine();
            for (int side = -1; side <= 1; side += 2)
                f.Fingers(side);
            f.Radii();
            return f._fit;
        }

        /// <summary>The triangles' vertices plus deterministic samples inside each triangle, about one per spacing².</summary>
        public static Vector3[] Densify(Vector3[] positions, IList<int> triangles, float spacing)
        {
            var points = new List<Vector3>();
            var used = new HashSet<int>(triangles);
            foreach (int i in used)
                points.Add(positions[i]);
            var random = new System.Random(1234);
            float cell = spacing * spacing;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                var a = positions[triangles[t]];
                var b = positions[triangles[t + 1]];
                var c = positions[triangles[t + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                int n = Mathf.Min((int)(area / cell), 400);
                for (int k = 0; k < n; k++)
                {
                    float u = (float)random.NextDouble(), v = (float)random.NextDouble();
                    if (u + v > 1f)
                    {
                        u = 1f - u;
                        v = 1f - v;
                    }
                    points.Add(a + (b - a) * u + (c - a) * v);
                }
            }
            return points.ToArray();
        }

        // ------------------------------------------------------------------ slicing

        private IEnumerable<Vector3> SliceX(float x0, float x1)
        {
            int i = Lower(_xs, x0);
            for (; i < _xs.Length && _xs[i] <= x1; i++)
                yield return _p[_byX[i]];
        }

        private IEnumerable<Vector3> SliceY(float y0, float y1)
        {
            int i = Lower(_ys, y0);
            for (; i < _ys.Length && _ys[i] <= y1; i++)
                yield return _p[_byY[i]];
        }

        /// <summary>Vertices with side·x in [a, b] (side = -1 left, +1 right).</summary>
        private IEnumerable<Vector3> SideX(int side, float a, float b) => side > 0 ? SliceX(a, b) : SliceX(-b, -a);

        private static int Lower(float[] sorted, float value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private struct Section
        {
            public int Count;
            public Vector3 Mean, Min, Max;
            public float SpanY => Max.y - Min.y;
            public float SpanZ => Max.z - Min.z;
            public float SpanX => Max.x - Min.x;
        }

        private static Section Measure(IEnumerable<Vector3> points, Func<Vector3, bool> keep = null)
        {
            var s = new Section { Min = Vector3.one * float.MaxValue, Max = Vector3.one * float.MinValue };
            var sum = Vector3.zero;
            foreach (var p in points)
            {
                if (keep != null && !keep(p))
                    continue;
                s.Count++;
                sum += p;
                s.Min = Vector3.Min(s.Min, p);
                s.Max = Vector3.Max(s.Max, p);
            }
            s.Mean = s.Count > 0 ? sum / s.Count : Vector3.zero;
            return s;
        }

        // ------------------------------------------------------------------ arms

        private void Arms()
        {
            float h = _h;
            float tipMin = float.MaxValue;
            for (int side = -1; side <= 1; side += 2)
            {
                int si = HumanoidFit.SideIndex(side);
                string L = side < 0 ? "Left" : "Right";
                float tip = 0f;
                foreach (var p in SideX(side, 0f, 10f))
                    tip = Mathf.Max(tip, side * p.x);
                tipMin = Mathf.Min(tipMin, tip);

                Section Arm(float x, float w = 0.006f)
                {
                    // Thin slices on dense scans; wider where the surface is built from sparse rings.
                    var s = Measure(SideX(side, x - w, x + w), p => p.y > 0.5f * h);
                    for (float wider = w * 2f; s.Count < 6 && wider <= 0.05f; wider *= 2f)
                        s = Measure(SideX(side, x - wider, x + wider), p => p.y > 0.5f * h);
                    return s;
                }
                // Arm axis and thickness from the middle of the arm (upper arm to forearm), well clear of the body.
                var mid = new List<Section>();
                for (int k = 0; k <= 12; k++)
                {
                    var s = Arm(tip * Mathf.Lerp(0.45f, 0.75f, k / 12f));
                    if (s.Count > 4)
                        mid.Add(s);
                }
                if (mid.Count < 6)
                    throw new InvalidOperationException("The model is not in a T-pose (no arm found reaching out from the body). " +
                                                        "Re-pose it to a T-pose, or import a rigged file.");
                mid.Sort((a, b) => a.Mean.y.CompareTo(b.Mean.y));
                float armY = mid[mid.Count / 2].Mean.y;
                var spans = mid.ConvertAll(s => s.SpanY * 0.5f);
                spans.Sort();
                float armR = spans[spans.Count / 2];
                if (armY < 0.62f * h)
                    throw new InvalidOperationException($"The model is not in a T-pose (the arms hang at {armY / h * 100f:0}% of the height). " +
                                                        "Re-pose it to a T-pose, or import a rigged file.");

                // Armpit: walking out from the torso, the first x with nothing below the arm.
                float armpit = 0.3f * tip;
                for (float x = 0.03f * h; x < tip; x += 0.002f)
                {
                    bool occupied = false;
                    foreach (var p in SideX(side, x - 0.004f, x + 0.004f))
                        if (p.y < armY - 1.6f * armR && p.y > 0.5f * h)
                        {
                            occupied = true;
                            break;
                        }
                    if (!occupied)
                    {
                        armpit = x;
                        break;
                    }
                }
                _fit.ArmpitX[si] = armpit;

                // Wrist: hand length is about 10.8 % of stature; refine to the narrowest section nearby.
                float guess = tip - 0.108f * h;
                float wrist = guess, bestArea = float.MaxValue;
                Section wristSection = Arm(guess);
                for (float x = guess - 0.022f * h; x <= guess + 0.022f * h; x += 0.002f)
                {
                    var s = Arm(x);
                    if (s.Count < 5)
                        continue;
                    float area = s.SpanY * s.SpanZ;
                    if (area < bestArea)
                    {
                        bestArea = area;
                        wrist = x;
                        wristSection = s;
                    }
                }
                _fit.WristX[si] = wrist;
                float shoulderX = armpit - 0.6f * armR;
                _fit.ShoulderX[si] = shoulderX;
                var atShoulder = Arm(armpit + 0.01f);
                var shoulder = new Vector3(side * shoulderX, atShoulder.Count > 0 ? atShoulder.Mean.y : armY, atShoulder.Count > 0 ? atShoulder.Mean.z : 0f);
                float elbowX = shoulderX + 0.53f * (wrist - shoulderX);
                var atElbow = Arm(elbowX);
                var elbow = new Vector3(side * elbowX, atElbow.Mean.y, atElbow.Mean.z - 0.1f * atElbow.SpanZ * 0.5f);
                var hand = new Vector3(side * wrist, wristSection.Mean.y, wristSection.Mean.z);
                _fit.Heads[L + "UpperArm"] = shoulder;
                _fit.Heads[L + "LowerArm"] = elbow;
                _fit.Heads[L + "Hand"] = hand;
                _fit.Tails[L + "Hand"] = new Vector3(side * tip, hand.y, hand.z);
                _fit.Radius[L + "UpperArm"] = Mathf.Max(armR, 0.02f);
                _fit.Radius[L + "LowerArm"] = Mathf.Max(Mathf.Min(atElbow.SpanY, atElbow.SpanZ) * 0.5f, 0.018f);
                _fit.Notes.Add($"{L} arm: armpit at {armpit:0.000} m, shoulder {shoulder}, elbow {elbow}, wrist {hand}, fingertips at {tip:0.000} m");
            }
            _fit.ShoulderY = (_fit.Heads["LeftUpperArm"].y + _fit.Heads["RightUpperArm"].y) * 0.5f;
            float slope = Mathf.Atan2(_fit.ShoulderY - (_fit.Heads["LeftHand"].y + _fit.Heads["RightHand"].y) * 0.5f, tipMin) * Mathf.Rad2Deg;
            _fit.Notes.Add($"Arm slope below horizontal: {slope:0.0}°");
        }

        // ------------------------------------------------------------------ legs

        private void Legs()
        {
            float h = _h;
            // Crotch: the lowest surface point on the centre line close to the body, between the knees and mid-body
            // (the underside of the pelvis). Coat tails and skirt panels hang further in front or behind and are ignored.
            float crotch = float.MaxValue;
            foreach (var p in SliceY(0.3f * h, 0.62f * h))
                if (Mathf.Abs(p.x) < 0.01f && Mathf.Abs(p.z) < 0.07f)
                    crotch = Mathf.Min(crotch, p.y);
            if (crotch == float.MaxValue)
            {
                crotch = 0.46f * h;
                _fit.Notes.Add("Crotch not found on the centre line: assumed at 46% of the height");
            }
            _fit.CrotchY = crotch;
            float hipY = crotch + 0.045f * h;
            _fit.HipY = hipY;
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                Section Leg(float y, float w = 0.006f)
                {
                    var s = Measure(SliceY(y - w, y + w), p => side * p.x > 0.005f && Mathf.Abs(p.x) < 0.35f * h);
                    for (float wider = w * 2f; s.Count < 6 && wider <= 0.05f; wider *= 2f)
                        s = Measure(SliceY(y - wider, y + wider), p => side * p.x > 0.005f && Mathf.Abs(p.x) < 0.35f * h);
                    return s;
                }
                var top = Leg(crotch - 0.05f * h);
                var hip = new Vector3(side * Mathf.Abs(top.Mean.x) * 0.92f, hipY, top.Mean.z);
                // Ankle: just under where the leg section starts to flare into the foot.
                var shin = Leg(0.12f * h);
                float ankleY = 0.05f * h;
                for (float y = 0.12f * h; y > 0.03f * h; y -= 0.002f)
                {
                    var s = Leg(y, 0.003f);
                    if (s.Count > 3 && s.SpanZ > 1.7f * shin.SpanZ)
                    {
                        ankleY = Mathf.Clamp(y, 0.035f * h, 0.065f * h);
                        break;
                    }
                }
                var atAnkle = Leg(ankleY + 0.03f * h);
                var ankle = new Vector3(atAnkle.Mean.x, ankleY, atAnkle.Mean.z);
                float kneeY = ankleY + 0.5f * (hipY - ankleY);
                var atKnee = Leg(kneeY);
                var knee = new Vector3(atKnee.Mean.x, kneeY, atKnee.Mean.z);
                var foot = Measure(SliceY(-1f, ankleY), p => side * p.x > 0.01f);
                float ballZ = foot.Min.z + 0.74f * (foot.Max.z - foot.Min.z);
                var ball = Measure(SliceY(-1f, ankleY), p => side * p.x > 0.01f && Mathf.Abs(p.z - ballZ) < 0.012f);
                var toes = new Vector3(ball.Mean.x, Mathf.Min(0.25f * ankleY, ball.Mean.y), ballZ);
                _fit.Heads[L + "UpperLeg"] = hip;
                _fit.Heads[L + "LowerLeg"] = knee;
                _fit.Heads[L + "Foot"] = ankle;
                _fit.Heads[L + "Toes"] = toes;
                _fit.Tails[L + "Toes"] = new Vector3(toes.x, toes.y, foot.Max.z);
                var midThigh = Leg(Mathf.Lerp(hipY, kneeY, 0.45f));
                var midShin = Leg(Mathf.Lerp(kneeY, ankleY, 0.45f));
                _fit.Radius[L + "UpperLeg"] = Mathf.Max(0.45f * (midThigh.SpanX + midThigh.SpanZ) * 0.5f, 0.04f);
                _fit.Radius[L + "LowerLeg"] = Mathf.Max(0.45f * (midShin.SpanX + midShin.SpanZ) * 0.5f, 0.03f);
                _fit.Radius[L + "Foot"] = Mathf.Max(0.5f * foot.SpanX, 0.03f);
                _fit.Radius[L + "Toes"] = Mathf.Max(0.4f * foot.SpanX, 0.025f);
                _fit.Notes.Add($"{L} leg: hip {hip}, knee {knee}, ankle {ankle}, ball of the foot {toes}, toe tip z {foot.Max.z:0.000}");
            }
            _fit.Notes.Add($"Crotch at {crotch:0.000} m ({crotch / h * 100f:0}% of the height)");
        }

        // ------------------------------------------------------------------ spine

        private void Spine()
        {
            float h = _h;
            float chin = h - 0.13f * h;
            float shoulderY = _fit.ShoulderY;
            float neckY = shoulderY + 0.35f * (chin - shoulderY);
            _fit.NeckY = neckY;
            var pelvis = Measure(SliceY(_fit.HipY - 0.006f, _fit.HipY + 0.006f), p => Mathf.Abs(p.x) < 0.3f * h);
            var hips = new Vector3(0f, _fit.HipY + 0.027f * h, pelvis.Mean.z);
            _fit.Heads["Hips"] = hips;
            foreach (var (name, f) in new[] { ("Spine", 0.19f), ("Chest", 0.415f), ("UpperChest", 0.64f) })
            {
                float y = hips.y + f * (neckY - hips.y);
                var s = Measure(SliceY(y - 0.006f, y + 0.006f), p => Mathf.Abs(p.x) < 0.06f);
                _fit.Heads[name] = new Vector3(0f, y, s.Mean.z - 0.3f * s.SpanZ * 0.5f);
            }
            var neck = Measure(SliceY(neckY - 0.006f, neckY + 0.006f), p => Mathf.Abs(p.x) < 0.04f);
            float neckZ = neck.Count > 0 ? (neck.Min.z + neck.Max.z) * 0.5f : _fit.Heads["UpperChest"].z;
            _fit.Heads["Neck"] = new Vector3(0f, neckY, neckZ);
            _fit.Heads["Head"] = new Vector3(0f, chin + 0.015f * h, neckZ);
            _fit.Tails["Head"] = new Vector3(0f, h, neckZ);
            var upperChest = _fit.Heads["UpperChest"];
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                var arm = _fit.Heads[L + "UpperArm"];
                _fit.Heads[L + "Shoulder"] = new Vector3(side * 0.022f * h, shoulderY + 0.008f * h, (upperChest.z + arm.z) * 0.5f);
            }
            _fit.Notes.Add($"Spine: hips {hips}, neck {_fit.Heads["Neck"]}, head {_fit.Heads["Head"]} (head length assumed 13% of the height)");
        }

        // ------------------------------------------------------------------ fingers

        private static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Little" };

        private void Fingers(int side)
        {
            int si = HumanoidFit.SideIndex(side);
            string L = side < 0 ? "Left" : "Right";
            var wrist = _fit.Heads[L + "Hand"];
            float wx = _fit.WristX[si];
            float tip = side * _fit.Tails[L + "Hand"].x;
            float len = tip - wx;
            var hand = new List<Vector3>();
            foreach (var p in SideX(side, wx - 0.005f, tip + 0.01f))
                if (Mathf.Abs(p.y - wrist.y) < 0.6f * len && Mathf.Abs(p.z - wrist.z) < 0.8f * len)
                    hand.Add(p);

            bool ok = TryFingers(side, L, wrist, wx, tip, len, hand);
            _fit.FingersMeasured = si == 0 ? ok : _fit.FingersMeasured && ok;
            if (!ok)
                ProportionalFingers(side, L, wrist, len);
        }

        private bool TryFingers(int side, string L, Vector3 wrist, float wx, float tip, float len, List<Vector3> hand)
        {
            // Distal finger region: separated fingers, clustered across the hand (along Z).
            var distal = hand.FindAll(p => side * p.x > wx + 0.62f * len);
            if (distal.Count < 60)
            {
                _fit.Notes.Add($"{L} hand: too few vertices in the finger region ({distal.Count}); fingers placed by proportion");
                return false;
            }
            var zs = distal.ConvertAll(p => p.z);
            zs.Sort();
            var centers = new float[4];
            for (int k = 0; k < 4; k++)
                centers[k] = zs[(int)((k + 0.5f) / 4f * zs.Count)];
            var members = new List<Vector3>[4];
            for (int iteration = 0; iteration < 20; iteration++)
            {
                for (int k = 0; k < 4; k++)
                    members[k] = new List<Vector3>();
                foreach (var p in distal)
                {
                    int best = 0;
                    for (int k = 1; k < 4; k++)
                        if (Mathf.Abs(p.z - centers[k]) < Mathf.Abs(p.z - centers[best]))
                            best = k;
                    members[best].Add(p);
                }
                for (int k = 0; k < 4; k++)
                    if (members[k].Count > 0)
                    {
                        float sum = 0f;
                        foreach (var p in members[k])
                            sum += p.z;
                        centers[k] = sum / members[k].Count;
                    }
            }
            // Order across the hand: palms down with the thumb forward, so the index finger has the largest Z.
            var order = new[] { 0, 1, 2, 3 };
            Array.Sort(order, (a, b) => centers[b].CompareTo(centers[a]));
            float spacing = (centers[order[0]] - centers[order[3]]) / 3f;
            for (int k = 0; k < 4; k++)
                if (members[order[k]].Count < distal.Count * 0.06f || spacing < 0.004f)
                {
                    _fit.Notes.Add($"{L} hand: finger tips not separated (cluster sizes {members[0].Count}/{members[1].Count}/{members[2].Count}/{members[3].Count}); fingers placed by proportion");
                    return false;
                }
            // Webs between neighbouring fingers: the furthest flesh in each gap.
            var webs = new float[3];
            for (int g = 0; g < 3; g++)
            {
                float gz = (centers[order[g]] + centers[order[g + 1]]) * 0.5f;
                float web = wx + 0.35f * len;
                foreach (var p in hand)
                    if (Mathf.Abs(p.z - gz) < 0.0015f && side * p.x > web)
                        web = side * p.x;
                webs[g] = web;
            }
            var mcpRow = new float[4];
            for (int k = 0; k < 4; k++)
            {
                var m = members[order[k]];
                float fingerTip = 0f;
                float y = 0f;
                foreach (var p in m)
                {
                    fingerTip = Mathf.Max(fingerTip, side * p.x);
                    y += p.y;
                }
                y /= m.Count;
                float web = k == 0 ? webs[0] : k == 3 ? webs[2] : (webs[k - 1] + webs[k]) * 0.5f;
                float length = Mathf.Max((fingerTip - web) / 0.78f, 0.25f * len);
                float mcp = Mathf.Max(fingerTip - length, wx + 0.35f * len);
                mcpRow[k] = mcp;
                length = fingerTip - mcp;
                float z = centers[order[k]];
                string name = FingerNames[k];
                Vector3 At(float x)
                {
                    // Local centre of this finger near x (falls back to the cluster line).
                    var s = Measure(hand, p => Mathf.Abs(side * p.x - x) < 0.004f && Mathf.Abs(p.z - z) < spacing * 0.45f);
                    return s.Count > 2 ? new Vector3(side * x, s.Mean.y, s.Mean.z) : new Vector3(side * x, y, z);
                }
                _fit.Heads[L + name + "1"] = At(mcp);
                _fit.Heads[L + name + "2"] = At(mcp + 0.45f * length);
                _fit.Heads[L + name + "3"] = At(mcp + 0.75f * length);
                _fit.Tails[L + name + "3"] = new Vector3(side * fingerTip, _fit.Heads[L + name + "3"].y, _fit.Heads[L + name + "3"].z);
                _fit.Radius[L + name + "1"] = _fit.Radius[L + name + "2"] = _fit.Radius[L + name + "3"] = Mathf.Max(spacing * 0.45f, 0.006f);
            }
            // The hand bone ends at the middle finger's knuckle.
            _fit.Tails[L + "Hand"] = _fit.Heads[L + "Middle1"];

            // Thumb: flesh in front of (larger Z than) the index finger, nearer the wrist than the knuckles.
            float indexZ = centers[order[0]];
            var thumb = hand.FindAll(p => p.z > indexZ + spacing * 0.6f && side * p.x < mcpRow[0] + 0.1f * len);
            if (thumb.Count < 20)
            {
                _fit.Notes.Add($"{L} hand: thumb not found ({thumb.Count} vertices); thumb placed by proportion");
                ProportionalThumb(side, L, wrist, len);
            }
            else
            {
                // Tip: furthest from the wrist along the thumb's out-and-forward direction.
                var tipP = thumb[0];
                float best = float.MinValue;
                foreach (var p in thumb)
                {
                    float score = (side * p.x - wx) * 0.6f + (p.z - wrist.z);
                    if (score > best)
                    {
                        best = score;
                        tipP = p;
                    }
                }
                var cmc = new Vector3(side * (wx + 0.12f * len), wrist.y - 0.01f * len, Mathf.Lerp(wrist.z, indexZ, 0.9f));
                _fit.Heads[L + "Thumb1"] = cmc;
                _fit.Heads[L + "Thumb2"] = Vector3.Lerp(cmc, tipP, 0.42f);
                _fit.Heads[L + "Thumb3"] = Vector3.Lerp(cmc, tipP, 0.72f);
                _fit.Tails[L + "Thumb3"] = tipP;
                _fit.Radius[L + "Thumb1"] = Mathf.Max(spacing * 0.7f, 0.009f);
                _fit.Radius[L + "Thumb2"] = _fit.Radius[L + "Thumb3"] = Mathf.Max(spacing * 0.5f, 0.007f);
            }
            _fit.Notes.Add($"{L} hand: four fingers measured (spacing {spacing * 1000f:0} mm, knuckles at {string.Join(", ", Array.ConvertAll(mcpRow, v => v.ToString("0.000")))})");
            return true;
        }

        private void ProportionalFingers(int side, string L, Vector3 wrist, float len)
        {
            var offsets = new[] { 0.022f, 0f, -0.021f, -0.04f };
            var lengths = new[] { 0.95f, 1f, 0.94f, 0.76f };
            float scale = len / 0.19f;
            for (int k = 0; k < 4; k++)
            {
                var mcp = wrist + new Vector3(side * 0.45f * len, 0f, offsets[k] * scale);
                float f = 0.55f * len * lengths[k];
                _fit.Heads[L + FingerNames[k] + "1"] = mcp;
                _fit.Heads[L + FingerNames[k] + "2"] = mcp + new Vector3(side * 0.45f * f, 0f, 0f);
                _fit.Heads[L + FingerNames[k] + "3"] = mcp + new Vector3(side * 0.75f * f, 0f, 0f);
                _fit.Tails[L + FingerNames[k] + "3"] = mcp + new Vector3(side * f, 0f, 0f);
                _fit.Radius[L + FingerNames[k] + "1"] = _fit.Radius[L + FingerNames[k] + "2"] = _fit.Radius[L + FingerNames[k] + "3"] = 0.009f * scale;
            }
            _fit.Tails[L + "Hand"] = _fit.Heads[L + "Middle1"];
            ProportionalThumb(side, L, wrist, len);
        }

        private void ProportionalThumb(int side, string L, Vector3 wrist, float len)
        {
            var cmc = wrist + new Vector3(side * 0.12f * len, -0.01f * len, 0.12f * len);
            var dir = new Vector3(side * 0.55f, -0.15f, 0.82f).normalized;
            _fit.Heads[L + "Thumb1"] = cmc;
            _fit.Heads[L + "Thumb2"] = cmc + dir * 0.2f * len;
            _fit.Heads[L + "Thumb3"] = cmc + dir * 0.37f * len;
            _fit.Tails[L + "Thumb3"] = cmc + dir * 0.5f * len;
            _fit.Radius[L + "Thumb1"] = _fit.Radius[L + "Thumb2"] = _fit.Radius[L + "Thumb3"] = 0.05f * len;
        }

        // ------------------------------------------------------------------ radii of the trunk

        private void Radii()
        {
            float h = _h;
            float Half(float y) => Measure(SliceY(y - 0.006f, y + 0.006f), p => Mathf.Abs(p.x) < Mathf.Min(_fit.ArmpitX[0], _fit.ArmpitX[1])).SpanX * 0.5f;
            float torso = Mathf.Max(Half(_fit.Heads["Chest"].y), 0.08f);
            _fit.Radius["Hips"] = Mathf.Max(Half(_fit.HipY), 0.09f) * 0.85f;
            _fit.Radius["Spine"] = torso * 0.85f;
            _fit.Radius["Chest"] = torso * 0.85f;
            _fit.Radius["UpperChest"] = torso * 0.85f;
            _fit.Radius["Neck"] = 0.03f * h;
            _fit.Radius["Head"] = 0.06f * h;
            _fit.Radius["LeftShoulder"] = _fit.Radius["RightShoulder"] = 0.035f * h;
            foreach (var L in new[] { "Left", "Right" })
                _fit.Radius[L + "Hand"] = Mathf.Max(_fit.Radius[L + "LowerArm"] * 0.8f, 0.015f);
        }
    }
}
