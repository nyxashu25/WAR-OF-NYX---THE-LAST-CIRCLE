using System;
using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>Visual proportions per build (metres). Heights are shared; only widths change (hit shapes are identical).</summary>
    public sealed class BodyShape
    {
        public float Shoulder, Chest, ChestDepth, Waist, WaistDepth, Hip, HipDepth, Thigh, Calf, Arm, Forearm, Neck;

        public static BodyShape For(BodyBuild build)
        {
            switch (build)
            {
                case BodyBuild.Athletic:
                    return new BodyShape { Shoulder = 0.235f, Chest = 0.2f, ChestDepth = 0.125f, Waist = 0.155f, WaistDepth = 0.105f, Hip = 0.17f, HipDepth = 0.11f,
                        Thigh = 0.09f, Calf = 0.064f, Arm = 0.055f, Forearm = 0.047f, Neck = 0.05f };
                case BodyBuild.Slender:
                    return new BodyShape { Shoulder = 0.205f, Chest = 0.172f, ChestDepth = 0.115f, Waist = 0.13f, WaistDepth = 0.09f, Hip = 0.165f, HipDepth = 0.108f,
                        Thigh = 0.08f, Calf = 0.057f, Arm = 0.045f, Forearm = 0.039f, Neck = 0.043f };
                case BodyBuild.Heavy:
                    return new BodyShape { Shoulder = 0.27f, Chest = 0.23f, ChestDepth = 0.14f, Waist = 0.19f, WaistDepth = 0.13f, Hip = 0.19f, HipDepth = 0.12f,
                        Thigh = 0.1f, Calf = 0.072f, Arm = 0.064f, Forearm = 0.055f, Neck = 0.058f };
                default:
                    return new BodyShape { Shoulder = 0.225f, Chest = 0.19f, ChestDepth = 0.12f, Waist = 0.148f, WaistDepth = 0.1f, Hip = 0.168f, HipDepth = 0.11f,
                        Thigh = 0.086f, Calf = 0.061f, Arm = 0.051f, Forearm = 0.044f, Neck = 0.048f };
            }
        }
    }

    /// <summary>A hanging chain (scarf tail, coat panel) animated by <see cref="ClothDynamics"/>.</summary>
    public sealed class DangleSpec
    {
        public string Name;
        public string Parent;
        public Vector3 LocalPosition;
        public int Segments;
        public float SegmentLength;
        /// <summary>Joint whose swing the chain partly follows (coat panels follow the thighs), or null.</summary>
        public string Driver;
        public float DriverWeight;
        public float Stiffness = 18f;
    }

    /// <summary>A built character at one detail level: one mesh per joint ("Pelvis", "Spine", "HeadJoint", "Eyes", limbs, chain segments).</summary>
    public sealed class CharacterModel
    {
        public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        public readonly List<DangleSpec> Dangles = new List<DangleSpec>();
        public BodyShape Shape;
        public int Vertices;
        public int Lod;
    }

    /// <summary>
    /// Detailed procedural PLACEHOLDER models for the roster (original designs, see <see cref="CharacterRoster"/>), built
    /// at three detail levels (<see cref="LodCount"/>): LOD 0 for close-ups (smooth resampled surfaces, cloth folds,
    /// stitched seams, zippers, rivets, lamellar plates, embroidery, circuit traces), LOD 1 for mid range, LOD 2 for
    /// distance. Every surface carries a material (<see cref="Surf"/>: woven fabric, leather, brushed metal, polymer
    /// armour, carbon fibre, lacquer, hair, fur, glowing circuits) lit by WarOfNyx/NyxToonVC. Built once per character
    /// and level, shared by every instance.
    /// </summary>
    public static class CharacterModels
    {
        public const int LodCount = 3;
        private static readonly float[] LodResolution = { 1.5f, 1f, 0.5f };
        private static readonly Dictionary<int, CharacterModel> Cache = new Dictionary<int, CharacterModel>();

        public const float HeadCentreY = 0.14f;       // head centre above the head joint
        public const float EyePivotY = 0.134f;        // blink pivot (eye centre height) above the head joint
        private static readonly Vector3 HeadRadii = new Vector3(0.1f, 0.122f, 0.112f);

        // Detail level of the model being built (builds are synchronous on the main thread).
        private static int _lod;
        private static bool Hi => _lod == 0;
        private static bool Lo => _lod >= 2;
        /// <summary>Ring resampling step for smooth surfaces at this level (0 = rings as written).</summary>
        private static float Step => _lod == 0 ? 0.014f : _lod == 1 ? 0.035f : 0f;

        public static CharacterModel Get(CharacterLook look, int lod = 0)
        {
            lod = Mathf.Clamp(lod, 0, LodCount - 1);
            int key = look.Id * LodCount + lod;
            if (Cache.TryGetValue(key, out var model) && model.Meshes.Count > 0 && AllMeshesAlive(model))
                return model;
            model = Build(look, lod);
            Cache[key] = model;
            return model;
        }

        private static bool AllMeshesAlive(CharacterModel model)
        {
            foreach (var m in model.Meshes.Values)
                if (m == null)
                    return false;
            return true;
        }

        private static Color S(Color c) => MeshKit.Solid(c);
        private static Color G(Color c, float glow) => MeshKit.Glow(c, glow);
        private static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        private static Vector2 M(float surf, float weave = Weave.Plain) => new Vector2(surf, weave);

        private static CharacterModel Build(CharacterLook look, int lod)
        {
            _lod = lod;
            var model = new CharacterModel { Shape = BodyShape.For(look.Build), Lod = lod };
            var kits = new Dictionary<string, MeshKit>();
            MeshKit K(string joint)
            {
                if (!kits.TryGetValue(joint, out var kit))
                    kits[joint] = kit = new MeshKit { Resolution = LodResolution[lod] };
                return kit;
            }

            var o = Outfit.For(look);
            Body(K, model.Shape, look, o);
            Head(K, look);
            Hair(K(CharacterBodyNames.Head), look);
            switch (look.Id)
            {
                case 0: Kai(K, model, look, o); break;
                case 1: Akira(K, model, look, o); break;
                case 2: Yuna(K, model, look, o); break;
                default: Ren(K, model, look, o); break;
            }

            foreach (var pair in kits)
            {
                var mesh = pair.Value.ToMesh($"{look.Name}_{pair.Key}_LOD{lod}");
                model.Meshes[pair.Key] = mesh;
                model.Vertices += mesh.vertexCount;
            }
            _lod = 0;
            return model;
        }

        // ------------------------------------------------------------------ outfit materials

        private struct Outfit
        {
            public Color Shirt, Pants, Boots, Soles, Gloves, Belt, Sleeve, Trim, Thread;
            public float ShirtSurf, ShirtWeave, PantsWeave, SleeveSurf, SleeveWeave, GloveSurf, BootSurf;
            public bool BareForearms, Fingerless, WidePants, Laces, MetalToe;
            public int Knuckles; // 0 none, 1 polymer guard, 2 metal plate

            public static Outfit For(CharacterLook l)
            {
                switch (l.Id)
                {
                    case 0: // KAI: hex-mesh black undersuit, hakama, bandaged forearms, leather tabi boots
                        return new Outfit { Shirt = new Color(0.07f, 0.06f, 0.1f), Pants = new Color(0.1f, 0.08f, 0.14f), Boots = new Color(0.08f, 0.07f, 0.1f),
                            Soles = new Color(0.05f, 0.04f, 0.06f), Gloves = new Color(0.08f, 0.07f, 0.1f), Belt = l.Secondary, Sleeve = l.Primary * 1.6f,
                            Trim = l.Accent, Thread = new Color(0.55f, 0.35f, 0.8f), ShirtSurf = Surf.Fabric, ShirtWeave = Weave.Hex, PantsWeave = Weave.Twill,
                            SleeveSurf = Surf.Fabric, SleeveWeave = Weave.Twill, GloveSurf = Surf.Leather, BootSurf = Surf.Leather,
                            BareForearms = true, Fingerless = true, WidePants = true, Knuckles = 2 };
                    case 1: // AKIRA: knit compression shirt, ripstop cargo trousers, leather jacket, tactical boots
                        return new Outfit { Shirt = new Color(0.22f, 0.21f, 0.24f), Pants = new Color(0.2f, 0.2f, 0.22f), Boots = new Color(0.12f, 0.1f, 0.1f),
                            Soles = new Color(0.06f, 0.05f, 0.05f), Gloves = new Color(0.1f, 0.09f, 0.1f), Belt = new Color(0.12f, 0.1f, 0.09f),
                            Sleeve = new Color(0.09f, 0.08f, 0.1f), Trim = l.Secondary, Thread = new Color(0.55f, 0.15f, 0.18f),
                            ShirtSurf = Surf.Fabric, ShirtWeave = Weave.Rib, PantsWeave = Weave.Ripstop, SleeveSurf = Surf.Leather, SleeveWeave = Weave.Plain,
                            GloveSurf = Surf.Leather, BootSurf = Surf.Leather, Fingerless = true, Laces = true, Knuckles = 1 };
                    case 2: // YUNA: hex-mesh bodysuit, white ripstop jacket, polymer boots with glowing soles
                        return new Outfit { Shirt = new Color(0.1f, 0.1f, 0.14f), Pants = new Color(0.12f, 0.12f, 0.17f), Boots = new Color(0.9f, 0.92f, 0.97f),
                            Soles = l.Accent, Gloves = new Color(0.9f, 0.92f, 0.97f), Belt = new Color(0.15f, 0.16f, 0.22f), Sleeve = l.Primary, Trim = l.Accent,
                            Thread = new Color(0.6f, 0.65f, 0.75f), ShirtSurf = Surf.Fabric, ShirtWeave = Weave.Hex, PantsWeave = Weave.Hex,
                            SleeveSurf = Surf.Fabric, SleeveWeave = Weave.Ripstop, GloveSurf = Surf.Polymer, BootSurf = Surf.Polymer };
                    default: // REN: ripstop fatigues, heavy leather boots with steel toe caps
                        return new Outfit { Shirt = new Color(0.2f, 0.21f, 0.2f), Pants = new Color(0.26f, 0.27f, 0.24f), Boots = new Color(0.14f, 0.12f, 0.1f),
                            Soles = new Color(0.07f, 0.06f, 0.05f), Gloves = new Color(0.16f, 0.14f, 0.12f), Belt = new Color(0.2f, 0.16f, 0.12f),
                            Sleeve = new Color(0.2f, 0.21f, 0.2f), Trim = l.Accent, Thread = new Color(0.45f, 0.42f, 0.35f),
                            ShirtSurf = Surf.Fabric, ShirtWeave = Weave.Ripstop, PantsWeave = Weave.Twill, SleeveSurf = Surf.Fabric, SleeveWeave = Weave.Ripstop,
                            GloveSurf = Surf.Leather, BootSurf = Surf.Leather, Laces = true, MetalToe = true };
                }
            }
        }

        // ------------------------------------------------------------------ cloth folds (radial displacement, metres)

        /// <summary>Creases bunched around a joint at <paramref name="y0"/>: a few slanted rings fading over <paramref name="range"/>.</summary>
        private static float Creases(float y, float a, float y0, float range, float depth, int count = 3)
        {
            float t = Mathf.Abs(y - y0) / range;
            if (t >= 1f)
                return 0f;
            float wave = Mathf.Sin((y - y0) / range * count * Mathf.PI + Mathf.Sin(a * Mathf.Deg2Rad * 2f) * 1.3f);
            return -depth * (1f - t) * Mathf.Max(0f, wave);
        }

        /// <summary>Vertical drape folds (coats, sleeves), growing from <paramref name="top"/> towards the hem.</summary>
        private static float Drape(float y, float a, float top, float hem, float depth, float folds)
        {
            float t = Mathf.InverseLerp(top, hem, y);
            return depth * t * t * Mathf.Sin(a * Mathf.Deg2Rad * folds);
        }

        /// <summary>Knife pleats (hakama): a sawtooth around the leg.</summary>
        private static float Pleats(float a, float count, float depth) => -depth * Mathf.Repeat(a / (360f / count), 1f);

        // ------------------------------------------------------------------ small details (LOD 0 unless noted)

        private static Vector3 Outward(IList<Ring> rings, float y, float angle) =>
            (MeshKit.OnRings(rings, y, angle, 0.01f) - MeshKit.OnRings(rings, y, angle, 0f)).normalized;

        /// <summary>Running stitches along a path on a surface (outline weight 0, so they stay crisp).</summary>
        private static void Stitches(MeshKit k, IList<Vector3> path, IList<Vector3> normals, Color thread, Matrix4x4 m, float spacing = 0.008f)
        {
            if (!Hi)
                return;
            float surf = k.Material, weave = k.Pattern, outline = k.Outline;
            k.Use(Surf.Cloth, Weave.Plain, 0f);
            float carry = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var a = path[i];
                var d = path[i + 1] - a;
                float len = d.magnitude;
                if (len < 1e-5f)
                    continue;
                var dir = d / len;
                for (float s = carry; s + spacing * 0.55f <= len; s += spacing)
                {
                    var p0 = a + dir * s;
                    var p1 = a + dir * (s + spacing * 0.55f);
                    var n = Vector3.Lerp(normals[i], normals[i + 1], s / len).normalized;
                    k.Dash(p0 + n * 0.0004f, p1 + n * 0.0004f, n, 0.0013f, thread, m);
                }
                carry = Mathf.Repeat(carry - len, spacing);
            }
            k.Use(surf, weave, outline);
        }

        /// <summary>A vertical seam on a ring surface: double running stitches either side of the seam line.</summary>
        private static void Seam(MeshKit k, IList<Ring> rings, float y0, float y1, float angle, float lift, Color thread, Matrix4x4 m)
        {
            if (!Hi)
                return;
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(y1 - y0) / 0.02f));
            var ring = MeshKit.RingAt(rings, (y0 + y1) * 0.5f);
            float offset = 0.0035f / Mathf.Max(0.02f, (ring.Rx + ring.Rz) * 0.5f) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= n; i++)
                {
                    float y = Mathf.Lerp(y0, y1, i / (float)n);
                    path.Add(MeshKit.OnRings(rings, y, angle + side * offset, lift));
                    normals.Add(Outward(rings, y, angle + side * offset));
                }
                Stitches(k, path, normals, thread, m);
            }
        }

        /// <summary>Stitches around a ring surface at height <paramref name="y"/> between two angles.</summary>
        private static void RingStitches(MeshKit k, IList<Ring> rings, float y, float lift, Color thread, Matrix4x4 m, float from = -180f, float to = 180f)
        {
            if (!Hi)
                return;
            var path = new List<Vector3>();
            var normals = new List<Vector3>();
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(to - from) / 8f));
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)n);
                path.Add(MeshKit.OnRings(rings, y, a, lift));
                normals.Add(Outward(rings, y, a));
            }
            Stitches(k, path, normals, thread, m);
        }

        /// <summary>A raised rivet / bolt head.</summary>
        private static void Rivet(MeshKit k, Vector3 at, Vector3 normal, float radius, Color color, Matrix4x4 m)
        {
            if (!Hi)
                return;
            float surf = k.Material, weave = k.Pattern, outline = k.Outline;
            k.Use(Surf.Metal, Weave.Plain, 0f);
            k.Ellipsoid(new Vector3(radius, radius * 0.6f, radius), 3, 6, color, m * Matrix4x4.TRS(at, Quaternion.FromToRotation(Vector3.up, normal), Vector3.one),
                q => new Vector3(q.x, Mathf.Max(q.y, 0f), q.z));
            k.Use(surf, weave, outline);
        }

        /// <summary>Hard part with bevelled edges at LOD 0/1 (plain box at LOD 2).</summary>
        private static void Plate(MeshKit k, Vector3 size, float bevel, Color color, Matrix4x4 m)
        {
            if (Hi)
                k.RoundBox(size, bevel, color, m);
            else
                k.Box(size, color, m);
        }

        /// <summary>Zipper along a path: dark tape, metal teeth (LOD 0) and a pull tab at the end.</summary>
        private static void Zipper(MeshKit k, IList<Vector3> path, IList<Vector3> normals, Color tape, Color metal, Matrix4x4 m)
        {
            float surf = k.Material, weave = k.Pattern, outline = k.Outline;
            var widths = new float[path.Count];
            for (int i = 0; i < widths.Length; i++)
                widths[i] = 0.012f;
            k.Use(Surf.Fabric, Weave.Rib, 0f);
            var lifted = new List<Vector3>();
            for (int i = 0; i < path.Count; i++)
                lifted.Add(path[i] + normals[i] * 0.001f);
            k.Band(lifted, widths, 0.0015f, Vector3.Cross(normals[0], path[path.Count - 1] - path[0]), _ => tape, m);
            k.Use(Surf.Metal, Weave.Plain, 0f);
            if (Hi)
            {
                int tooth = 0;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var d = path[i + 1] - path[i];
                    float len = d.magnitude;
                    var dir = d / Mathf.Max(1e-5f, len);
                    var across = Vector3.Cross(normals[i], dir).normalized;
                    for (float s = 0f; s < len; s += 0.004f, tooth++)
                    {
                        var at = path[i] + dir * s + normals[i] * 0.0022f + across * (tooth % 2 == 0 ? 0.0016f : -0.0016f);
                        k.Box(new Vector3(0.004f, 0.0022f, 0.0014f), metal, m * Matrix4x4.TRS(at, Quaternion.LookRotation(normals[i], dir), Vector3.one));
                    }
                }
            }
            var end = path[path.Count - 1];
            var endDir = (path[path.Count - 1] - path[path.Count - 2]).normalized;
            if (!Lo)
                k.RoundBox(new Vector3(0.008f, 0.02f, 0.003f), 0.0012f, metal,
                    m * Matrix4x4.TRS(end + normals[normals.Count - 1] * 0.004f - endDir * 0.01f, Quaternion.LookRotation(normals[normals.Count - 1], endDir), Vector3.one));
            k.Use(surf, weave, outline);
        }

        /// <summary>A path straight along a ring surface at a fixed angle (for zippers, piping and seams).</summary>
        private static void PathOnRings(IList<Ring> rings, float y0, float y1, float angle, float lift, List<Vector3> path, List<Vector3> normals, float step = 0.02f)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(y1 - y0) / step));
            for (int i = 0; i <= n; i++)
            {
                float y = Mathf.Lerp(y0, y1, i / (float)n);
                path.Add(MeshKit.OnRings(rings, y, angle, lift));
                normals.Add(Outward(rings, y, angle));
            }
        }

        /// <summary>A thin line on a ring surface following (y, angle) points: circuit traces, embroidery, piping.</summary>
        private static void Trace(MeshKit k, IList<Ring> rings, IList<Vector2> points, float width, float lift, Color color, Matrix4x4 m)
        {
            var path = new List<Vector3>();
            var widths = new List<float>();
            Vector3 normal = Vector3.forward;
            for (int i = 0; i < points.Count; i++)
            {
                path.Add(MeshKit.OnRings(rings, points[i].x, points[i].y, lift));
                widths.Add(width);
                normal = Outward(rings, points[i].x, points[i].y);
            }
            var dir = (path[path.Count - 1] - path[0]).normalized;
            k.Band(path, widths, width * 0.4f, Vector3.Cross(normal, dir), _ => color, m);
        }

        /// <summary>Places a part on a ring surface at (y, angle), facing outward, then <paramref name="local"/>.</summary>
        private static Matrix4x4 OnSurface(IList<Ring> rings, float y, float angle, float lift, Matrix4x4 local)
        {
            var at = MeshKit.OnRings(rings, y, angle, lift);
            var outward = Outward(rings, y, angle);
            return Matrix4x4.TRS(at, Quaternion.LookRotation(outward, Vector3.up), Vector3.one) * local;
        }

        // ------------------------------------------------------------------ anatomy and base clothing

        private static Ring[] PelvisRings(BodyShape b) => new[]
        {
            new Ring(-0.21f, b.Hip * 0.8f, b.HipDepth * 0.82f), new Ring(-0.13f, b.Hip, b.HipDepth), new Ring(-0.05f, b.Hip * 0.98f, b.HipDepth * 0.97f),
            new Ring(0.04f, b.Waist, b.WaistDepth), new Ring(0.09f, b.Waist * 0.98f, b.WaistDepth * 0.98f),
        };

        private static Ring[] TorsoRings(BodyShape b) => new[]
        {
            new Ring(-0.07f, b.Waist, b.WaistDepth), new Ring(0.05f, b.Waist * 1.01f, b.WaistDepth),
            new Ring(0.15f, Mathf.Lerp(b.Waist, b.Chest, 0.6f), Mathf.Lerp(b.WaistDepth, b.ChestDepth, 0.6f), 0f, 0.005f),
            new Ring(0.25f, b.Chest, b.ChestDepth, 0f, 0.012f), new Ring(0.33f, b.Chest * 1.02f, b.ChestDepth * 1.02f, 0f, 0.015f),
            new Ring(0.4f, b.Shoulder, b.ChestDepth * 0.9f), new Ring(0.45f, b.Shoulder * 0.55f, b.ChestDepth * 0.72f, 0f, -0.01f),
            new Ring(0.48f, b.Neck * 1.3f, b.Neck * 1.2f, 0f, -0.01f),
        };

        /// <summary>A garment layer over the torso: the torso rings pushed out by <paramref name="gap"/>, between two heights.</summary>
        private static Ring[] Layer(Ring[] rings, float gap, float from, float to)
        {
            var list = new List<Ring>();
            foreach (var r in MeshKit.Resample(rings, 0.05f))
                if (r.Y >= from - 1e-4f && r.Y <= to + 1e-4f)
                    list.Add(new Ring(r.Y, r.Rx + gap, r.Rz + gap, r.X, r.Z));
            if (list.Count == 0 || list[0].Y > from + 1e-4f)
            {
                var r = MeshKit.RingAt(rings, from);
                list.Insert(0, new Ring(from, r.Rx + gap, r.Rz + gap, r.X, r.Z));
            }
            if (list[list.Count - 1].Y < to - 1e-4f)
            {
                var r = MeshKit.RingAt(rings, to);
                list.Add(new Ring(to, r.Rx + gap, r.Rz + gap, r.X, r.Z));
            }
            return list.ToArray();
        }

        private static Ring[] UpperArmRings(BodyShape b) => new[]
        {
            new Ring(-0.29f, b.Arm * 0.78f, b.Arm * 0.8f), new Ring(-0.2f, b.Arm * 0.92f, b.Arm * 0.95f),
            new Ring(-0.1f, b.Arm * 1.05f, b.Arm * 1.08f), new Ring(0.0f, b.Arm * 1.1f, b.Arm * 1.1f),
        };

        private static Ring[] ForearmRings(BodyShape b) => new[]
        {
            new Ring(-0.27f, b.Forearm * 0.66f, b.Forearm * 0.6f), new Ring(-0.18f, b.Forearm * 0.82f, b.Forearm * 0.78f),
            new Ring(-0.06f, b.Forearm * 1.12f, b.Forearm * 1.05f, 0f, 0.004f), new Ring(0.02f, b.Forearm * 0.95f, b.Forearm * 0.95f),
        };

        private static Ring[] ThighRings(BodyShape b, int side) => new[]
        {
            new Ring(-0.46f, b.Thigh * 0.62f, b.Thigh * 0.66f), new Ring(-0.35f, b.Thigh * 0.78f, b.Thigh * 0.8f),
            new Ring(-0.2f, b.Thigh * 0.95f, b.Thigh * 0.98f, side * 0.004f), new Ring(-0.05f, b.Thigh * 1.08f, b.Thigh * 1.05f),
            new Ring(0.04f, b.Thigh * 1.05f, b.Thigh * 1.02f),
        };

        private static Ring[] ShinRings(BodyShape b, float flare) => new[]
        {
            new Ring(-0.42f, b.Calf * 0.56f * flare, b.Calf * 0.6f * flare), new Ring(-0.36f, b.Calf * 0.6f * flare, b.Calf * 0.62f * flare),
            new Ring(-0.22f, b.Calf * 0.86f * Mathf.Lerp(1f, flare, 0.6f), b.Calf * 0.85f * Mathf.Lerp(1f, flare, 0.6f)),
            new Ring(-0.08f, b.Calf * 1.1f, b.Calf * 1.06f, 0f, -0.012f), new Ring(0.03f, b.Calf * 0.98f, b.Calf * 0.98f),
        };

        private static void Body(Func<string, MeshKit> K, BodyShape b, CharacterLook l, Outfit o)
        {
            var id = Matrix4x4.identity;
            var thread = S(o.Thread);
            // Pelvis: hips and seat (trousers) with a fly seam and creases at the hip joints.
            var pelvis = PelvisRings(b);
            K("Pelvis").Use(Surf.Fabric, o.PantsWeave).Surface(pelvis, 18, S(o.Pants), id, Step,
                (y, a) => Creases(y, Mathf.Abs(a), -0.19f, 0.04f, 0.0018f, 2), capBottom: true, capTop: true);
            K("Pelvis").Ellipsoid(new Vector3(b.Hip * 0.72f, 0.05f, b.HipDepth * 0.72f), 6, 12, S(o.Pants), MeshKit.At(new Vector3(0f, -0.2f, 0f)));
            Seam(K("Pelvis"), pelvis, -0.17f, -0.02f, 0f, 0.0006f, thread, id);
            Seam(K("Pelvis"), pelvis, -0.2f, 0.03f, 180f, 0.0006f, thread, id);

            // Torso (spine space: 0 = 1.0 m above the ground, neck base at 0.48): fabric with soft creases at the waist.
            var torso = TorsoRings(b);
            K("Spine").Use(o.ShirtSurf, o.ShirtWeave).Surface(torso, 20, S(o.Shirt), id, Step,
                (y, a) => Creases(y, a, 0.02f, 0.09f, 0.0018f, 3), capBottom: true, capTop: true);
            Seam(K("Spine"), torso, -0.06f, 0.4f, 90f, 0.0005f, thread, id);
            Seam(K("Spine"), torso, -0.06f, 0.4f, -90f, 0.0005f, thread, id);
            // Neck up into the head.
            K("Spine").Use(Surf.Skin).Surface(new[] { new Ring(0.44f, b.Neck, b.Neck * 0.95f, 0f, -0.005f), new Ring(0.51f, b.Neck * 0.95f, b.Neck * 0.92f, 0f, -0.002f),
                new Ring(0.58f, b.Neck * 0.92f, b.Neck * 0.9f, 0f, 0.0f) }, 12, S(l.Skin), id, Step, capTop: true);

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Upper arm: deltoid cap and a tapering sleeve with creases at the elbow.
                var upper = UpperArmRings(b);
                K("UpperArm" + sfx).Use(o.SleeveSurf, o.SleeveWeave).Ellipsoid(new Vector3(b.Arm * 1.35f, b.Arm * 1.25f, b.Arm * 1.3f), 8, 14, S(o.Sleeve),
                    MeshKit.At(new Vector3(0f, -0.01f, 0f)));
                K("UpperArm" + sfx).Surface(upper, 14, S(o.Sleeve), id, Step, (y, a) => Creases(y, a, -0.27f, 0.07f, 0.0025f), capBottom: true, capTop: true);
                Seam(K("UpperArm" + sfx), upper, -0.28f, -0.02f, -side * 90f + 180f, 0.0005f, thread, id);
                // Forearm: muscle swell near the elbow, slim wrist (skin, or sleeve with creases at the elbow).
                var fore = ForearmRings(b);
                if (o.BareForearms)
                    K("Forearm" + sfx).Use(Surf.Skin).Surface(fore, 14, S(l.Skin), id, Step, null, true, true);
                else
                    K("Forearm" + sfx).Use(o.SleeveSurf, o.SleeveWeave).Surface(fore, 14, S(o.Sleeve), id, Step, (y, a) => Creases(y, a, -0.01f, 0.06f, 0.002f), true, true);
                Hand(K("Hand" + sfx), side, o, l);

                // Legs: side seams, knee creases; KAI's hakama has knife pleats.
                var thigh = ThighRings(b, side);
                K("Thigh" + sfx).Use(Surf.Fabric, o.PantsWeave).Surface(thigh, 16, S(o.Pants), id, Step,
                    (y, a) => Creases(y, a, -0.42f, 0.06f, 0.0025f) + Creases(y, a, 0.0f, 0.05f, 0.0015f, 2), true, true);
                Seam(K("Thigh" + sfx), thigh, -0.44f, 0.0f, side * 90f, 0.0005f, thread, id);
                float flare = o.WidePants ? 1.6f : 1f;
                var shin = ShinRings(b, flare);
                if (o.WidePants)
                    K("Shin" + sfx).Use(Surf.Fabric, o.PantsWeave).Surface(shin, 24, S(o.Pants), id, Step,
                        (y, a) => Pleats(a, 10f, 0.006f) * Mathf.InverseLerp(-0.05f, -0.3f, y), true, true);
                else
                    K("Shin" + sfx).Use(Surf.Fabric, o.PantsWeave).Surface(shin, 16, S(o.Pants), id, Step,
                        (y, a) => Creases(y, a, 0.0f, 0.06f, 0.0022f) + Creases(y, a, -0.36f, 0.06f, 0.003f, 4), true, true);
                Seam(K("Shin" + sfx), shin, -0.4f, 0.02f, side * 90f, 0.0005f, thread, id);
                Boot(K("Foot" + sfx), b, o, l);
            }
        }

        private static void Hand(MeshKit k, int side, Outfit o, CharacterLook l)
        {
            var glove = S(o.Gloves);
            var skin = S(l.Skin);
            // Palm (back of the hand faces outward), fingers curled into a grip, thumb across, a cuff with a strap.
            k.Use(o.GloveSurf).Ellipsoid(new Vector3(0.036f, 0.048f, 0.02f), 8, 12, glove, MeshKit.At(new Vector3(0f, -0.05f, 0f)));
            k.Torus(0.034f, 0.024f, 0.006f, 12, 6, S(Mul(o.Gloves, 0.7f)), MeshKit.At(new Vector3(0f, -0.006f, 0f)));
            if (!Lo)
                k.RoundBox(new Vector3(0.022f, 0.012f, 0.006f), 0.002f, S(Mul(o.Gloves, 0.6f)), MeshKit.At(new Vector3(0f, -0.008f, 0.026f)));
            for (int f = 0; f < 4; f++)
            {
                float x = (f - 1.5f) * 0.018f;
                k.Use(o.GloveSurf).Ellipsoid(new Vector3(0.0095f, 0.03f, 0.011f), 6, 8, glove, MeshKit.TRS(new Vector3(x, -0.095f, 0.01f), new Vector3(35f, 0f, 0f), Vector3.one));
                k.Use(o.Fingerless ? Surf.Skin : o.GloveSurf).Ellipsoid(new Vector3(0.0085f, 0.022f, 0.01f), 6, 8, o.Fingerless ? skin : glove,
                    MeshKit.TRS(new Vector3(x, -0.118f, 0.035f), new Vector3(100f, 0f, 0f), Vector3.one));
            }
            k.Use(o.Fingerless ? Surf.Skin : o.GloveSurf).Ellipsoid(new Vector3(0.011f, 0.03f, 0.012f), 6, 8, o.Fingerless ? skin : glove,
                MeshKit.TRS(new Vector3(-side * 0.03f, -0.055f, 0.022f), new Vector3(30f, 0f, side * 35f), Vector3.one));
            // Knuckle guard: polymer shell or a riveted metal plate.
            if (o.Knuckles > 0 && !Lo)
            {
                var guard = MeshKit.TRS(new Vector3(0f, -0.088f, 0.024f), new Vector3(35f, 0f, 0f), Vector3.one);
                k.Use(o.Knuckles == 2 ? Surf.Metal : Surf.Polymer).RoundBox(new Vector3(0.07f, 0.02f, 0.008f), 0.003f,
                    S(o.Knuckles == 2 ? new Color(0.45f, 0.45f, 0.5f) : Mul(o.Gloves, 1.5f)), guard);
                for (int f = 0; f < 4; f++)
                    Rivet(k, new Vector3((f - 1.5f) * 0.018f, 0f, 0.004f), Vector3.forward, 0.0022f, S(new Color(0.6f, 0.6f, 0.65f)), guard);
            }
            // Stitching over the back of the glove.
            if (Hi)
            {
                k.Use(o.GloveSurf);
                for (int f = 0; f < 3; f++)
                {
                    float x = (f - 1f) * 0.018f;
                    Stitches(k, new[] { new Vector3(x, -0.03f, 0.021f), new Vector3(x, -0.07f, 0.02f) }, new[] { Vector3.forward, Vector3.forward }, S(o.Thread),
                        Matrix4x4.identity, 0.006f);
                }
            }
        }

        private static void Boot(MeshKit k, BodyShape b, Outfit o, CharacterLook l)
        {
            var id = Matrix4x4.identity;
            // Foot joint is at the ankle, 6 cm above the ground. The boot runs heel → toe along +Z (loft along Y, rotated).
            var along = MeshKit.TRS(new Vector3(0f, -0.025f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);
            var foot = new[]
            {
                new Ring(-0.075f, 0.045f, 0.032f), new Ring(-0.05f, 0.055f, 0.045f), new Ring(0.04f, 0.058f, 0.04f, 0f, 0.01f),
                new Ring(0.13f, 0.052f, 0.03f, 0f, 0.018f), new Ring(0.185f, 0.03f, 0.018f, 0f, 0.02f),
            };
            k.Use(o.BootSurf).Surface(foot, 14, S(o.Boots), along, Step * 0.6f, (y, a) => y > 0.02f && y < 0.12f ? Creases(y, a, 0.07f, 0.05f, 0.0012f, 3) : 0f, true, true);
            // Toe cap (darker leather, or steel) and heel counter.
            if (!Lo)
            {
                k.Use(o.MetalToe ? Surf.Metal : o.BootSurf).Ellipsoid(new Vector3(0.05f, 0.032f, 0.045f), 6, 12,
                    S(o.MetalToe ? new Color(0.5f, 0.5f, 0.54f) : Mul(o.Boots, 0.75f)), MeshKit.At(new Vector3(0f, -0.022f, 0.14f)),
                    q => new Vector3(q.x, Mathf.Max(q.y, -0.006f), Mathf.Max(q.z, -0.01f)));
                k.Use(o.BootSurf).Ellipsoid(new Vector3(0.048f, 0.03f, 0.03f), 6, 12, S(Mul(o.Boots, 0.8f)), MeshKit.At(new Vector3(0f, -0.025f, -0.065f)),
                    q => new Vector3(q.x, Mathf.Max(q.y, -0.01f), Mathf.Min(q.z, 0.008f)));
            }
            // Shaft around the ankle and lower shin, with a trim ring and a pull tab.
            var shaft = new[] { new Ring(-0.03f, b.Calf * 0.82f, b.Calf * 0.85f), new Ring(0.05f, b.Calf * 0.8f, b.Calf * 0.83f), new Ring(0.12f, b.Calf * 0.78f, b.Calf * 0.8f) };
            k.Use(o.BootSurf).Surface(shaft, 14, S(o.Boots), id, Step, (y, a) => Creases(y, a, 0.0f, 0.04f, 0.0012f, 2), capTop: true);
            k.Torus(b.Calf * 0.8f, b.Calf * 0.83f, 0.008f, 14, 6, S(o.Trim * 0.8f), MeshKit.At(new Vector3(0f, 0.115f, 0f)));
            if (!Lo)
                k.Use(Surf.Leather).RoundBox(new Vector3(0.02f, 0.035f, 0.005f), 0.002f, S(Mul(o.Boots, 0.7f)), MeshKit.At(new Vector3(0f, 0.13f, -b.Calf * 0.82f)));
            RingStitches(k, shaft, 0.1f, 0.0005f, S(o.Thread), id);
            // Rubber sole with a welt, a heel block and tread lugs (LOD 0).
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.11f, 0.022f, 0.27f), 0.008f, S(o.Soles), MeshKit.At(new Vector3(0f, -0.058f, 0.05f)));
            k.RoundBox(new Vector3(0.09f, 0.025f, 0.07f), 0.006f, S(o.Soles), MeshKit.At(new Vector3(0f, -0.045f, -0.06f)));
            if (Hi)
                for (int i = 0; i < 7; i++)
                    for (int s = -1; s <= 1; s += 2)
                        k.Box(new Vector3(0.006f, 0.012f, 0.014f), S(Mul(o.Soles, 0.7f)), MeshKit.At(new Vector3(s * 0.056f, -0.058f, -0.05f + i * 0.035f)));
            if (Hi)
            {
                k.Use(Surf.Cloth);
                var welt = new List<Vector3>();
                var up = new List<Vector3>();
                for (int i = 0; i <= 16; i++)
                {
                    float t = i / 16f * Mathf.PI * 2f;
                    welt.Add(new Vector3(Mathf.Sin(t) * 0.056f, -0.046f, 0.05f + Mathf.Cos(t) * 0.13f));
                    up.Add(new Vector3(Mathf.Sin(t), 0.2f, Mathf.Cos(t)).normalized);
                }
                Stitches(k, welt, up, S(o.Thread), id, 0.008f);
            }
            // Laces: brass eyelets and criss-crossed laces up the tongue.
            if (o.Laces && !Lo)
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.0f + i * 0.026f, z = 0.052f + i * 0.004f;
                    k.Use(Surf.Fabric, Weave.Plain).Box(new Vector3(0.045f, 0.005f, 0.008f), S(new Color(0.75f, 0.72f, 0.68f)),
                        MeshKit.TRS(new Vector3(0f, y + 0.006f, z), new Vector3(-20f, 0f, i % 2 == 0 ? 18f : -18f), Vector3.one));
                    if (Hi)
                        for (int s = -1; s <= 1; s += 2)
                        {
                            k.Use(Surf.Gold, Weave.Plain, 0f).Torus(0.003f, 0.003f, 0.0012f, 8, 4, S(new Color(0.8f, 0.65f, 0.35f)),
                                MeshKit.TRS(new Vector3(s * 0.024f, y, z - 0.002f), new Vector3(70f, 0f, 0f), Vector3.one));
                            k.Outline = 1f;
                        }
                }
            }
        }

        // ------------------------------------------------------------------ head and face

        private static Vector3 Skull(Vector3 q)
        {
            float y = q.y / HeadRadii.y;
            if (y < 0f)
            {
                // Anime jaw: the lower half narrows to a soft V chin.
                float k = Mathf.Pow(-y, 1.6f);
                q.x *= Mathf.Lerp(1f, 0.5f, k);
                if (q.z > 0f)
                    q.z *= Mathf.Lerp(1f, 0.93f, k);
            }
            if (q.z < 0f)
                q.z *= 1.06f;
            // Flatter face plane (eyes sit on it).
            if (q.z > 0.07f && y > -0.35f && y < 0.45f)
                q.z = 0.07f + (q.z - 0.07f) * 0.7f;
            return q;
        }

        /// <summary>Face-plane depth (z) at a face position, for placing eyes, brows and the mouth on the surface.</summary>
        private static float FaceZ(float x, float y)
        {
            float nx = x / HeadRadii.x, ny = y / HeadRadii.y;
            float d = Mathf.Max(0f, 1f - nx * nx - ny * ny);
            var q = Skull(new Vector3(x, y, Mathf.Sqrt(d) * HeadRadii.z));
            return q.z;
        }

        private static void Head(Func<string, MeshKit> K, CharacterLook l)
        {
            var head = K(CharacterBodyNames.Head);
            var centre = MeshKit.At(new Vector3(0f, HeadCentreY, 0f));
            // Skin shading painted into the vertices: cheek blush, a soft shadow under the jaw, and (REN) stubble on the lower face.
            var blush = Color.Lerp(l.Skin, new Color(1f, 0.55f, 0.55f), 0.35f);
            head.Use(Surf.Skin).Ellipsoid(HeadRadii, 16, 24, d =>
            {
                var c = l.Skin;
                for (int side = -1; side <= 1; side += 2)
                {
                    float cheek = Vector3.Dot(d, new Vector3(side * 0.56f, -0.3f, 0.77f).normalized);
                    c = Color.Lerp(c, blush, Mathf.InverseLerp(0.93f, 0.985f, cheek) * (l.HairStyle == HairStyle.Crest ? 0.4f : 0.85f));
                }
                if (d.y < -0.5f)
                    c = Mul(c, Mathf.Lerp(1f, 0.82f, Mathf.InverseLerp(-0.5f, -0.85f, d.y)));
                if (l.HairStyle == HairStyle.Crest && d.z > 0.2f && d.y < -0.15f)
                    c = Color.Lerp(c, new Color(0.55f, 0.52f, 0.55f), 0.28f);
                return S(c);
            }, centre, Skull);
            // Ears and inner ears.
            for (int side = -1; side <= 1; side += 2)
                head.Ellipsoid(new Vector3(0.012f, 0.026f, 0.019f), 6, 8, S(Mul(l.Skin, 0.95f)), MeshKit.TRS(new Vector3(side * 0.097f, HeadCentreY - 0.008f, -0.006f),
                    new Vector3(0f, side * 15f, 0f), Vector3.one));
            head.Outline = 0f;
            for (int side = -1; side <= 1; side += 2)
                head.Disc(0.007f, 0.016f, 10, S(Mul(l.Skin, 0.78f)), MeshKit.TRS(new Vector3(side * 0.104f, HeadCentreY - 0.008f, -0.002f),
                    new Vector3(0f, side * 75f, 0f), Vector3.one));
            // Nose: bridge, tip with a highlight, nostrils.
            head.Band(new[]
            {
                new Vector3(0.003f, HeadCentreY + 0.012f, FaceZ(0.003f, 0.012f) + 0.0015f), new Vector3(0.004f, HeadCentreY - 0.012f, FaceZ(0.004f, -0.012f) + 0.004f),
                new Vector3(0.003f, HeadCentreY - 0.028f, FaceZ(0f, -0.028f) + 0.006f),
            }, new[] { 0.004f, 0.005f, 0.006f }, 0.003f, Vector3.right, _ => S(Mul(l.Skin, 0.9f)), Matrix4x4.identity);
            head.Ellipsoid(new Vector3(0.008f, 0.009f, 0.008f), 6, 10, S(Mul(l.Skin, 0.97f)), MeshKit.At(new Vector3(0f, HeadCentreY - 0.03f, FaceZ(0f, -0.03f) + 0.002f)));
            head.Disc(0.0025f, 0.0025f, 6, S(Color.Lerp(l.Skin, Color.white, 0.5f)), MeshKit.At(new Vector3(0.002f, HeadCentreY - 0.026f, FaceZ(0f, -0.03f) + 0.0105f)));
            for (int side = -1; side <= 1; side += 2)
                head.Disc(0.0028f, 0.0018f, 6, S(Mul(l.Skin, 0.55f)), MeshKit.TRS(new Vector3(side * 0.0045f, HeadCentreY - 0.037f, FaceZ(0f, -0.037f) + 0.006f),
                    new Vector3(60f, 0f, 0f), Vector3.one));
            // Lips: upper lip line with a cupid's-bow dip, fuller lower lip with a soft gloss, a small shadow under it.
            var mouthZ = FaceZ(0f, -0.06f);
            var lipColor = Color.Lerp(l.Skin, new Color(0.75f, 0.3f, 0.32f), l.HairStyle == HairStyle.Crest ? 0.3f : 0.55f);
            head.Band(new[]
            {
                new Vector3(-0.015f, HeadCentreY - 0.059f, mouthZ - 0.003f), new Vector3(-0.005f, HeadCentreY - 0.057f, mouthZ), new Vector3(0f, HeadCentreY - 0.0585f, mouthZ + 0.0005f),
                new Vector3(0.005f, HeadCentreY - 0.057f, mouthZ), new Vector3(0.015f, HeadCentreY - 0.059f, mouthZ - 0.003f),
            }, new[] { 0.0025f, 0.004f, 0.004f, 0.004f, 0.0025f }, 0.002f, Vector3.up, _ => S(new Color(0.38f, 0.15f, 0.17f)), Matrix4x4.identity);
            head.Use(Surf.Skin, Weave.Plain, 0f).Ellipsoid(new Vector3(0.011f, 0.0045f, 0.004f), 5, 10, S(lipColor),
                MeshKit.At(new Vector3(0f, HeadCentreY - 0.0645f, FaceZ(0f, -0.0645f) - 0.001f)));
            head.Use(Surf.Skin, Weave.Plain, 0f).Band(new[]
            {
                new Vector3(-0.007f, HeadCentreY - 0.072f, FaceZ(0f, -0.072f) - 0.0005f), new Vector3(0.007f, HeadCentreY - 0.072f, FaceZ(0f, -0.072f) - 0.0005f),
            }, new[] { 0.004f, 0.004f }, 0.0015f, Vector3.up, _ => S(Mul(l.Skin, 0.85f)), Matrix4x4.identity);

            // Brows (hair colour): angle sets the character's expression.
            float browTilt = l.Id == 0 ? 0.012f : l.Id == 3 ? 0.009f : l.Id == 2 ? -0.004f : 0.006f; // stern → soft
            head.Use(Surf.Hair, Weave.Plain, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                var inner = new Vector3(side * 0.017f, HeadCentreY + 0.04f - browTilt, 0f);
                var mid = new Vector3(side * 0.042f, HeadCentreY + 0.05f, 0f);
                var outer = new Vector3(side * 0.066f, HeadCentreY + 0.043f, 0f);
                inner.z = FaceZ(inner.x, inner.y - HeadCentreY) + 0.002f;
                mid.z = FaceZ(mid.x, mid.y - HeadCentreY) + 0.002f;
                outer.z = FaceZ(outer.x, outer.y - HeadCentreY) + 0.002f;
                // Three overlapping strokes give the brow a hair-like texture and a tapered tail.
                for (int stroke = 0; stroke < (Lo ? 1 : 3); stroke++)
                {
                    var shift = new Vector3(0f, (stroke - 1) * 0.0025f, 0.0005f * stroke);
                    head.Band(new[] { inner + shift, mid + shift * 1.2f, outer + shift * 0.5f }, new[] { 0.006f, 0.0055f, 0.002f }, 0.003f, Vector3.up,
                        _ => S(stroke == 1 ? l.Hair : Mul(l.Hair, 1.25f)), Matrix4x4.identity);
                }
            }
            head.Use(Surf.Skin, Weave.Plain, 0f);
            CharacterMarks(head, l);
            head.Use(Surf.Skin);

            // Eyes (own mesh, so they can blink): wet sclera, layered iris, pupil, two highlights, lashes.
            var eyes = K(CharacterBodyNames.Eyes);
            eyes.Use(Surf.Eye, Weave.Plain, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side * 0.042f, ey = -0.006f;
                float ez = FaceZ(ex, ey) + 0.003f;
                var face = Quaternion.Euler(0f, side * 14f, 0f);
                Matrix4x4 Layer(float dz, Vector2 offset) =>
                    Matrix4x4.TRS(new Vector3(ex, ey + (HeadCentreY - EyePivotY), ez) + face * new Vector3(offset.x, offset.y, dz), face, Vector3.one);
                eyes.Disc(0.024f, 0.031f, 18, S(new Color(0.97f, 0.96f, 0.98f)), Layer(0f, Vector2.zero));
                eyes.Disc(0.022f, 0.008f, 12, S(new Color(0.8f, 0.78f, 0.86f)), Layer(0.0006f, new Vector2(0f, 0.021f)));
                eyes.Disc(0.0185f, 0.0265f, 18, S(Mul(l.Eyes, 0.35f)), Layer(0.0011f, new Vector2(side * -0.002f, -0.002f)));
                eyes.Disc(0.0165f, 0.0245f, 18, S(l.Eyes), Layer(0.0014f, new Vector2(side * -0.002f, -0.002f)));
                if (!Lo)
                {
                    eyes.Disc(0.013f, 0.011f, 14, S(Color.Lerp(l.Eyes, Color.white, 0.35f)), Layer(0.0017f, new Vector2(side * -0.002f, -0.011f)));
                    eyes.Disc(0.0155f, 0.008f, 14, S(Mul(l.Eyes, 0.5f)), Layer(0.0018f, new Vector2(side * -0.002f, 0.014f)));
                }
                // Iris fibres radiating from the pupil (close-ups).
                if (Hi)
                    for (int f = 0; f < 12; f++)
                    {
                        float ang = f * 30f * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Cos(ang) * 0.68f, Mathf.Sin(ang));
                        var c0 = Layer(0.002f, new Vector2(side * -0.002f, -0.002f)).MultiplyPoint3x4(new Vector3(dir.x * 0.008f, dir.y * 0.012f, 0f));
                        var c1 = Layer(0.002f, new Vector2(side * -0.002f, -0.002f)).MultiplyPoint3x4(new Vector3(dir.x * 0.015f, dir.y * 0.022f, 0f));
                        eyes.Band(new[] { c0, c1 }, new[] { 0.0012f, 0.0006f }, 0.0004f, face * Vector3.forward, _ => S(Color.Lerp(l.Eyes, Color.white, 0.5f)), Matrix4x4.identity);
                    }
                eyes.Disc(0.0072f, 0.011f, 12, S(new Color(0.04f, 0.03f, 0.07f)), Layer(0.0024f, new Vector2(side * -0.002f, -0.001f)));
                eyes.Disc(0.0055f, 0.0055f, 8, G(Color.white, 0.6f), Layer(0.003f, new Vector2(side * 0.005f, 0.01f)));
                eyes.Disc(0.0028f, 0.0028f, 6, G(Color.white, 0.4f), Layer(0.003f, new Vector2(side * -0.007f, -0.01f)));
                // Lower lash (thin, outer half) and the eyelid crease above the eye.
                var lower = new List<Vector3>();
                var lowerWidths = new List<float>();
                var crease = new List<Vector3>();
                var creaseWidths = new List<float>();
                var frame0 = Layer(0f, Vector2.zero);
                for (int i = 0; i <= 4; i++)
                {
                    float u = Mathf.Lerp(0.1f, 1f, i / 4f);
                    lower.Add(frame0.MultiplyPoint3x4(new Vector3(u * 0.024f * side, -0.029f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * 0.85f)), 0.003f)));
                    lowerWidths.Add(Mathf.Lerp(0.0012f, 0.0025f, i / 4f));
                    float c = Mathf.Lerp(-0.7f, 1f, i / 4f);
                    crease.Add(frame0.MultiplyPoint3x4(new Vector3(c * 0.027f * side, 0.045f * Mathf.Sqrt(Mathf.Max(0f, 1f - c * c * 0.8f)) + 0.002f, 0.001f)));
                    creaseWidths.Add(0.0018f);
                }
                eyes.Use(Surf.Hair, Weave.Plain, 0f).Band(lower, lowerWidths, 0.002f, Vector3.forward, _ => S(new Color(0.18f, 0.1f, 0.14f)), Matrix4x4.identity);
                head.Use(Surf.Skin, Weave.Plain, 0f).Band(crease, creaseWidths, 0.002f, Vector3.forward, _ => S(Mul(l.Skin, 0.8f)),
                    Matrix4x4.TRS(new Vector3(0f, EyePivotY, 0f), Quaternion.identity, Vector3.one));
                head.Use(Surf.Skin);
                // Upper lash line over the top of the eye, inner corner → outer corner, thicker outside with a flick.
                var lash = new List<Vector3>();
                var widths = new List<float>();
                for (int i = 0; i <= 6; i++)
                {
                    float u = Mathf.Lerp(-1f, 1f, i / 6f);                       // -1 inner, +1 outer
                    var p = new Vector3(u * 0.026f * side, 0.031f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * 0.9f)) - 0.002f, 0.0035f);
                    if (i == 6)
                        p += new Vector3(side * 0.007f, 0.006f, 0f);
                    lash.Add(frame0.MultiplyPoint3x4(p));
                    widths.Add(Mathf.Lerp(0.0035f, 0.0075f, i / 6f));
                }
                eyes.Band(lash, widths, 0.003f, Vector3.forward, _ => S(new Color(0.06f, 0.04f, 0.08f)), Matrix4x4.identity);
                // Individual upper lashes fanning out at the outer corner (close-ups).
                if (Hi)
                    for (int i = 0; i < 4; i++)
                    {
                        float u = Mathf.Lerp(0.35f, 0.95f, i / 3f);
                        var root = new Vector3(u * 0.026f * side, 0.031f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * 0.9f)) - 0.001f, 0.0038f);
                        var tipP = root + new Vector3(side * (0.004f + i * 0.002f), 0.006f, 0.002f);
                        eyes.Band(new[] { frame0.MultiplyPoint3x4(root), frame0.MultiplyPoint3x4(tipP) }, new[] { 0.0016f, 0.0004f }, 0.0008f, face * Vector3.forward,
                            _ => S(new Color(0.06f, 0.04f, 0.08f)), Matrix4x4.identity);
                    }
                eyes.Use(Surf.Eye, Weave.Plain, 0f);
            }
            head.Use(Surf.Skin);
        }

        /// <summary>Personal marks: KAI's stitched scar, AKIRA's beauty mark, YUNA's freckles, REN's crow's feet and forehead line.</summary>
        private static void CharacterMarks(MeshKit head, CharacterLook l)
        {
            Vector3 OnFace(float x, float y, float lift = 0.004f) => new Vector3(x, HeadCentreY + y, FaceZ(x, y) + lift);
            switch (l.Id)
            {
                case 0: // stitch marks across KAI's scar
                    for (int i = 0; i < 4; i++)
                    {
                        float t = 0.15f + i * 0.22f;
                        var c = Vector3.Lerp(OnFace(-0.062f, 0.06f), OnFace(-0.034f, -0.045f), t);
                        head.Band(new[] { c + new Vector3(-0.004f, -0.0015f, 0.0005f), c + new Vector3(0.004f, 0.0015f, 0.0005f) }, new[] { 0.0015f, 0.0015f }, 0.0015f,
                            Vector3.up, _ => S(new Color(0.7f, 0.45f, 0.45f)), Matrix4x4.identity);
                    }
                    break;
                case 1: // beauty mark under AKIRA's left eye
                    head.Disc(0.0022f, 0.0022f, 8, S(new Color(0.25f, 0.12f, 0.1f)), MeshKit.TRS(OnFace(-0.052f, -0.036f), new Vector3(0f, -12f, 0f), Vector3.one));
                    break;
                case 2: // freckles across YUNA's nose and cheeks
                    var freckle = S(Color.Lerp(l.Skin, new Color(0.6f, 0.38f, 0.3f), 0.55f));
                    foreach (var f in new[] { new Vector2(-0.03f, -0.026f), new Vector2(-0.022f, -0.032f), new Vector2(-0.038f, -0.033f), new Vector2(0.03f, -0.026f),
                                 new Vector2(0.022f, -0.032f), new Vector2(0.038f, -0.033f), new Vector2(-0.008f, -0.02f), new Vector2(0.008f, -0.02f) })
                        head.Disc(0.0016f, 0.0016f, 6, freckle, MeshKit.TRS(OnFace(f.x, f.y, 0.0035f), new Vector3(0f, f.x * 300f, 0f), Vector3.one));
                    break;
                default: // REN: crow's feet at the outer eye corners, a forehead line
                    for (int side = -1; side <= 1; side += 2)
                        for (int k = 0; k < 2; k++)
                            head.Band(new[] { OnFace(side * 0.07f, -0.005f + k * 0.008f), OnFace(side * 0.082f, -0.01f + k * 0.012f) }, new[] { 0.0015f, 0.001f }, 0.0015f,
                                Vector3.up, _ => S(Mul(l.Skin, 0.78f)), Matrix4x4.identity);
                    head.Band(new[] { OnFace(-0.035f, 0.075f), OnFace(0f, 0.078f), OnFace(0.035f, 0.075f) }, new[] { 0.0015f, 0.0018f, 0.0015f }, 0.0015f,
                        Vector3.up, _ => S(Mul(l.Skin, 0.8f)), Matrix4x4.identity);
                    break;
            }
        }

        // ------------------------------------------------------------------ hair

        private static Vector3 Scalp(float theta, float phi, float lift = 1.02f)
        {
            // theta: 0 = front (+Z), 90 = right (+X); phi: 0 = top.
            float t = theta * Mathf.Deg2Rad, p = phi * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(p) * Mathf.Sin(t), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Cos(t));
            return new Vector3(d.x * HeadRadii.x * 1.04f, d.y * HeadRadii.y, d.z * HeadRadii.z * (d.z < 0f ? 1.06f : 1f)) * lift + new Vector3(0f, HeadCentreY, 0f);
        }

        private static void HairCap(MeshKit k, CharacterLook l, float scale, float frontCut)
        {
            var hair = l.Hair;
            k.Ellipsoid(HeadRadii * scale, 12, 18, d =>
            {
                // Anime "angel ring": a lighter band around the upper head (the shader adds a moving highlight on top).
                float ring = Mathf.InverseLerp(0.35f, 0.5f, d.y) * (1f - Mathf.InverseLerp(0.55f, 0.7f, d.y));
                return S(Color.Lerp(hair, Mul(hair, 1.6f) + new Color(0.05f, 0.05f, 0.06f), ring * 0.6f));
            }, MeshKit.At(new Vector3(0f, HeadCentreY + 0.006f, -0.004f)), q =>
            {
                // Pull the cap inside the skull over the face so only the hairline shows.
                if (q.z > frontCut && q.y < 0.06f)
                    q = Vector3.Lerp(q, q * 0.8f, Mathf.InverseLerp(frontCut, frontCut + 0.04f, q.z));
                if (q.y < -0.04f && q.z > -0.02f)
                    q *= 0.85f;
                return q;
            });
        }

        private static void Hair(MeshKit k, CharacterLook l)
        {
            k.Use(Surf.Hair);
            var root = S(l.Hair);
            var tip = S(l.HairTips);
            var shine = S(Color.Lerp(l.Hair, Color.white, 0.35f));   // highlight streaks
            var random = new System.Random(l.Id * 13 + 5);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var up = Vector3.up;
            var centre = new Vector3(0f, HeadCentreY, 0f);

            // A lock = a main strand plus two thinner strands fanned around it, so hair reads as layered clumps
            // (plus a fine wisp at LOD 0; the main strand alone at LOD 2).
            void Lock(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float flat, Color rootColor, Color tipColor)
            {
                k.Strand(a, b, c, d, width, flat, rootColor, tipColor, Matrix4x4.identity);
                if (Lo)
                    return;
                var side = Vector3.Cross((d - a).normalized, (a - centre).normalized).normalized;
                for (int s = -1; s <= 1; s += 2)
                {
                    var off = side * (width * 0.7f * s);
                    k.Strand(a + off * 0.5f, b + off, c + off * 1.2f, d + off * 1.5f + (d - c) * -0.15f, width * 0.55f, flat, rootColor, tipColor, Matrix4x4.identity, 6);
                }
                if (Hi)
                {
                    var off = side * (width * 0.25f);
                    k.Strand(a + off, b + off * 1.5f, c + off * 2.5f, d + off * 3.5f + (d - a) * 0.08f, width * 0.22f, flat, rootColor, tipColor, Matrix4x4.identity, 6);
                }
            }

            switch (l.HairStyle)
            {
                case HairStyle.Spiky: // KAI: layered spikes swept back, crown spikes, fringe locks, an ahoge, violet tips
                    HairCap(k, l, 1.04f, 0.03f);
                    for (int i = 0; i < 18; i++)
                    {
                        float theta = R(95f, 265f), phi = R(25f, 85f);
                        var a = Scalp(theta, phi, 0.98f);
                        var d = (a - centre).normalized;
                        float len = R(0.1f, 0.17f);
                        var end = a + d * len + new Vector3(R(-0.02f, 0.02f), R(0.02f, 0.05f), -R(0.02f, 0.05f));
                        Lock(a, a + d * 0.04f, a + d * len * 0.7f + up * 0.02f, end, R(0.024f, 0.032f), 0.5f, root, tip);
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        var a = Scalp(R(-70f, 70f), R(5f, 28f), 0.98f);
                        k.Strand(a, a + up * 0.05f, a + up * 0.09f + Vector3.back * 0.03f, a + up * R(0.09f, 0.13f) + new Vector3(R(-0.04f, 0.04f), 0f, -0.07f),
                            0.028f, 0.5f, root, tip, Matrix4x4.identity);
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        float x = (i - 3f) * 0.022f;
                        var a = Scalp(x * 560f, 38f, 0.99f);
                        var end = new Vector3(x * 1.3f + R(-0.008f, 0.008f), HeadCentreY + R(-0.005f, 0.03f), FaceZ(x, 0.03f) + 0.018f);
                        Lock(a, a + Vector3.forward * 0.03f + up * 0.01f, end + up * 0.04f + Vector3.forward * 0.012f, end, 0.02f, 0.45f, root, tip);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a = Scalp(side * 75f, 70f);
                        var end = new Vector3(side * 0.094f, HeadCentreY - 0.085f, 0.04f);
                        Lock(a, a + Vector3.down * 0.03f, end + up * 0.03f, end, 0.018f, 0.5f, root, tip);
                    }
                    // Ahoge: one rebellious strand curling up from the crown.
                    {
                        var a = Scalp(10f, 8f, 0.99f);
                        k.Strand(a, a + up * 0.06f + Vector3.forward * 0.03f, a + up * 0.1f + Vector3.forward * 0.01f, a + up * 0.08f + Vector3.back * 0.02f,
                            0.01f, 0.5f, root, tip, Matrix4x4.identity, 8);
                    }
                    // Highlight streaks over the top.
                    if (!Lo)
                        for (int i = 0; i < 6; i++)
                        {
                            var a = Scalp(R(150f, 210f), R(15f, 45f), 1.02f);
                            var d = (a - centre).normalized;
                            k.Strand(a, a + d * 0.03f, a + d * 0.07f + Vector3.back * 0.02f, a + d * 0.1f + new Vector3(0f, 0.02f, -0.05f), 0.01f, 0.3f, shine, tip, Matrix4x4.identity, 5);
                        }
                    break;

                case HairStyle.SweptBack: // AKIRA: swept back in layered locks over the top, side locks, nape flicks (+ ponytail chain)
                    HairCap(k, l, 1.035f, 0.045f);
                    for (int i = 0; i < 22; i++)
                    {
                        float theta = (i / 21f - 0.5f) * 120f + R(-4f, 4f);
                        var a = Scalp(theta, R(36f, 52f), 0.99f);
                        var top = Scalp(theta * 0.7f, 5f, 1.12f);
                        var back = Scalp(180f - theta * 0.6f, R(65f, 90f), 1.12f);
                        var end = back + new Vector3(0f, R(0.0f, 0.03f), -R(0.02f, 0.05f));
                        if (i % 3 == 0)
                            Lock(a, a + up * 0.05f + Vector3.forward * 0.02f, top + Vector3.back * 0.05f, end, R(0.02f, 0.026f), 0.45f, root, tip);
                        else if (!Lo || i % 2 == 0)
                            k.Strand(a, a + up * 0.05f + Vector3.forward * 0.02f, top + Vector3.back * 0.05f, end, R(0.018f, 0.024f), 0.45f, i % 4 == 1 ? shine : root, tip, Matrix4x4.identity);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        var a = Scalp(R(130f, 230f), R(95f, 112f), 0.98f);
                        k.Strand(a, a + Vector3.back * 0.03f, a + Vector3.back * 0.05f + Vector3.down * 0.02f, a + Vector3.back * 0.07f + up * 0.02f,
                            0.018f, 0.5f, root, tip, Matrix4x4.identity);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a = Scalp(side * 30f, 42f);
                        var end = new Vector3(side * 0.062f, HeadCentreY - 0.03f, FaceZ(side * 0.06f, -0.03f) + 0.015f);
                        Lock(a, a + Vector3.forward * 0.03f + up * 0.02f, end + up * 0.05f, end, 0.012f, 0.5f, root, tip);
                        var sideA = Scalp(side * 80f, 60f);
                        k.Strand(sideA, sideA + new Vector3(side * 0.01f, -0.03f, 0f), sideA + new Vector3(side * 0.015f, -0.06f, -0.01f), sideA + new Vector3(side * 0.01f, -0.09f, -0.02f),
                            0.016f, 0.5f, root, tip, Matrix4x4.identity);
                    }
                    // Ponytail tie at the back of the head: a gold ring (the tail itself swings: cloth chain).
                    k.Use(Surf.Gold).Torus(0.016f, 0.014f, 0.006f, 12, 5, S(new Color(0.85f, 0.65f, 0.3f)),
                        MeshKit.TRS(new Vector3(0f, HeadCentreY - 0.02f, -0.118f), new Vector3(70f, 0f, 0f), Vector3.one));
                    k.Use(Surf.Hair);
                    break;

                case HairStyle.Short: // YUNA: layered bob (under + over layer), straight fringe, longer side locks, ahoge, glowing clip
                    HairCap(k, l, 1.05f, 0.05f);
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int count = (layer == 0 ? 20 : 24) / (Lo ? 2 : 1);
                        for (int i = 0; i < count; i++)
                        {
                            float theta = 55f + i * (250f / (count - 1)) + (layer == 1 ? 5f : 0f);
                            var a = Scalp(theta, layer == 0 ? R(40f, 55f) : R(24f, 36f), layer == 0 ? 0.99f : 1.01f);
                            var outward = new Vector3(Mathf.Sin(theta * Mathf.Deg2Rad), 0f, Mathf.Cos(theta * Mathf.Deg2Rad));
                            float reach = layer == 0 ? 0.118f : 0.128f;
                            var mid = new Vector3(outward.x * reach, HeadCentreY + 0.02f, outward.z * (reach + 0.005f) - 0.01f);
                            float length = layer == 0 ? 0.11f : 0.1f;
                            var end = new Vector3(outward.x * (reach - 0.018f), HeadCentreY - length + R(-0.008f, 0.008f), outward.z * (reach - 0.012f) - 0.01f);
                            float width = (layer == 0 ? R(0.03f, 0.036f) : R(0.024f, 0.032f)) * (Lo ? 1.8f : 1f);
                            k.Strand(a, a + outward * 0.05f, mid + Vector3.down * 0.03f, end, width, 0.38f, layer == 1 && i % 5 == 2 ? shine : root, tip, Matrix4x4.identity, 8);
                        }
                    }
                    for (int i = 0; i < 11; i++)
                    {
                        float x = (i - 5f) * 0.016f;
                        var a = Scalp(x * 480f, 30f, 1.0f);
                        var end = new Vector3(x * 1.15f, HeadCentreY + 0.026f + (i % 2) * 0.004f, FaceZ(x, 0.028f) + 0.02f);
                        k.Strand(a, a + Vector3.forward * 0.04f, end + up * 0.035f + Vector3.forward * 0.01f, end, 0.019f, 0.38f, root, tip, Matrix4x4.identity);
                    }
                    // Side locks in front of the ears, the left one longer (asymmetric cut).
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a = Scalp(side * 62f, 48f, 1.0f);
                        float length = side < 0 ? 0.15f : 0.11f;
                        var end = new Vector3(side * 0.09f, HeadCentreY - length, 0.045f);
                        Lock(a, a + new Vector3(side * 0.02f, -0.03f, 0.01f), end + up * 0.05f, end, 0.017f, 0.4f, root, tip);
                    }
                    {
                        var a = Scalp(-20f, 6f, 1.0f);
                        k.Strand(a, a + up * 0.05f + Vector3.left * 0.01f, a + up * 0.08f + Vector3.forward * 0.02f, a + up * 0.06f + Vector3.forward * 0.05f,
                            0.009f, 0.5f, root, tip, Matrix4x4.identity, 8);
                    }
                    k.Use(Surf.Circuit, Weave.Plain, 0f);
                    k.RoundBox(new Vector3(0.008f, 0.035f, 0.014f), 0.002f, G(l.Accent, 0.8f), MeshKit.TRS(new Vector3(0.09f, HeadCentreY + 0.045f, 0.05f), new Vector3(0f, 25f, -20f), Vector3.one));
                    k.RoundBox(new Vector3(0.008f, 0.035f, 0.014f), 0.002f, G(l.Accent, 0.8f), MeshKit.TRS(new Vector3(0.092f, HeadCentreY + 0.03f, 0.035f), new Vector3(0f, 25f, -20f), Vector3.one));
                    k.Use(Surf.Hair);
                    break;

                default: // REN: undercut with shaved-side lines, a two-row silver crest, full beard with a braided chin tuft, moustache
                    HairCap(k, l, 1.02f, 0.035f);
                    // Shaved sides: two lines cut into each side.
                    k.Outline = 0f;
                    for (int side = -1; side <= 1; side += 2)
                        for (int line = 0; line < 2; line++)
                        {
                            var pts = new List<Vector3>();
                            var widths = new List<float>();
                            for (int i = 0; i <= 4; i++)
                            {
                                pts.Add(Scalp(side * Mathf.Lerp(60f, 130f, i / 4f), 62f + line * 10f, 1.028f));
                                widths.Add(0.003f);
                            }
                            k.Use(Surf.Skin, Weave.Plain, 0f).Band(pts, widths, 0.002f, Vector3.up, _ => S(Mul(l.Skin, 0.85f)), Matrix4x4.identity);
                        }
                    k.Use(Surf.Hair);
                    // Crest: two staggered rows of sharp spikes along the midline, tallest at the crown.
                    for (int row = 0; row < (Lo ? 1 : 2); row++)
                        for (int i = 0; i < 7; i++)
                        {
                            float f = i / 6f;
                            var a = f < 0.45f ? Scalp(row * 8f - 4f, Mathf.Lerp(42f, 6f, f / 0.45f), 1.0f) : Scalp(180f + row * 8f - 4f, Mathf.Lerp(0f, 60f, (f - 0.45f) / 0.55f), 1.0f);
                            float tall = Mathf.Lerp(0.07f, 0.13f, Mathf.Sin(f * Mathf.PI)) * (row == 0 ? 1f : 0.8f);
                            k.Strand(a, a + up * tall * 0.4f, a + up * tall * 0.8f + Vector3.back * 0.03f, a + up * tall + Vector3.back * (0.05f + f * 0.03f),
                                row == 0 ? 0.024f : 0.02f, 0.45f, root, row == 0 ? tip : shine, Matrix4x4.identity);
                        }
                    var beard = S(Mul(l.Hair, 0.85f));
                    var beardTip = S(Mul(l.Hair, 1.05f));
                    for (int i = 0; i < (Lo ? 9 : Hi ? 25 : 17); i++)
                    {
                        int count = Lo ? 9 : Hi ? 25 : 17;
                        float t = i / (count - 1f);
                        float x = Mathf.Lerp(-0.08f, 0.08f, t);
                        float y = -0.025f - 0.065f * (1f - Mathf.Abs(t * 2f - 1f));
                        var a = new Vector3(x, HeadCentreY + y, FaceZ(x, y) - 0.008f);
                        k.Strand(a, a + Vector3.down * 0.015f, a + Vector3.down * 0.03f + Vector3.forward * 0.008f, a + Vector3.down * R(0.035f, 0.055f) + Vector3.forward * 0.01f,
                            0.014f, 0.5f, beard, beardTip, Matrix4x4.identity, 5);
                    }
                    // Braided chin tuft: a strand with three brass bands.
                    {
                        var a = new Vector3(0f, HeadCentreY - 0.1f, FaceZ(0f, -0.1f) - 0.005f);
                        var end = a + new Vector3(0f, -0.07f, 0.01f);
                        k.Strand(a, a + Vector3.down * 0.025f, end + up * 0.02f, end, 0.014f, 0.8f, beard, beardTip, Matrix4x4.identity, 6);
                        k.Use(Surf.Gold);
                        for (int b = 0; b < 3; b++)
                            k.Torus(0.008f - b * 0.0015f, 0.008f - b * 0.0015f, 0.002f, 8, 4, S(new Color(0.8f, 0.6f, 0.3f)), MeshKit.At(Vector3.Lerp(a, end, 0.25f + b * 0.22f)));
                        k.Use(Surf.Hair);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a = new Vector3(side * 0.005f, HeadCentreY - 0.045f, FaceZ(0.005f, -0.045f) + 0.003f);
                        k.Strand(a, a + new Vector3(side * 0.01f, -0.002f, 0f), a + new Vector3(side * 0.02f, -0.008f, -0.003f), a + new Vector3(side * 0.03f, -0.02f, -0.008f),
                            0.007f, 0.6f, beard, beardTip, Matrix4x4.identity, 5);
                    }
                    break;
            }
            k.Use(Surf.Skin);
        }

        // ------------------------------------------------------------------ cloth chains

        /// <summary>A hanging chain: one band mesh per segment ("{name}_{index}"), each in its segment joint's space.</summary>
        private static void Chain(Func<string, MeshKit> K, CharacterModel model, DangleSpec spec, float widthTop, float widthBottom, float thickness,
            Color color, Color edge, float surf = Surf.Fabric, float weave = Weave.Twill)
        {
            model.Dangles.Add(spec);
            for (int i = 0; i < spec.Segments; i++)
            {
                float t0 = i / (float)spec.Segments, t1 = (i + 1) / (float)spec.Segments;
                var path = new List<Vector3>();
                var widths = new List<float>();
                int stations = Hi ? 6 : 3;
                for (int s = 0; s <= stations; s++)
                {
                    float t = s / (float)stations;
                    path.Add(new Vector3(0f, -spec.SegmentLength * t * 1.02f, 0f));
                    widths.Add(Mathf.Lerp(Mathf.Lerp(widthTop, widthBottom, t0), Mathf.Lerp(widthTop, widthBottom, t1), t));
                }
                bool last = i == spec.Segments - 1;
                var kit = K($"{spec.Name}_{i}");
                kit.Use(surf, weave).Band(path, widths, thickness, Vector3.right, r => last && r == path.Count - 1 ? edge : color, Matrix4x4.identity);
                if (last)
                {
                    float w = Mathf.Lerp(widthTop, widthBottom, 1f);
                    kit.Box(new Vector3(w * 0.95f, 0.012f, thickness * 1.6f), edge, MeshKit.At(new Vector3(0f, -spec.SegmentLength + 0.004f, 0f)));
                    // Fringe threads at the end (close-ups).
                    if (Hi)
                    {
                        kit.Outline = 0f;
                        for (int f = 0; f < 7; f++)
                        {
                            float x = Mathf.Lerp(-w * 0.42f, w * 0.42f, f / 6f);
                            var a = new Vector3(x, -spec.SegmentLength - 0.002f, 0f);
                            kit.Strand(a, a + Vector3.down * 0.01f, a + Vector3.down * 0.02f, a + new Vector3(x * 0.1f, -0.03f, 0.002f), 0.0025f, 1f, edge, edge, Matrix4x4.identity, 3);
                        }
                        kit.Outline = 1f;
                    }
                }
                // Hem stitches along both long edges.
                if (Hi)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var stitches = new List<Vector3>();
                        var normals = new List<Vector3>();
                        for (int p = 0; p < path.Count; p++)
                        {
                            stitches.Add(path[p] + new Vector3(s * (widths[p] * 0.5f - 0.005f), 0f, thickness * 0.5f + 0.0004f));
                            normals.Add(Vector3.forward);
                        }
                        Stitches(kit, stitches, normals, S(Color.Lerp(color, Color.white, 0.25f)), Matrix4x4.identity, 0.008f);
                    }
            }
        }

        /// <summary>A hair tail as a swinging chain: tapering tube segments, a pointed tip on the last one.</summary>
        private static void HairChain(Func<string, MeshKit> K, CharacterModel model, DangleSpec spec, float width, Color root, Color tip)
        {
            model.Dangles.Add(spec);
            for (int i = 0; i < spec.Segments; i++)
            {
                float t0 = i / (float)spec.Segments, t1 = (i + 1) / (float)spec.Segments;
                float w0 = Mathf.Lerp(width, width * 0.35f, t0), w1 = Mathf.Lerp(width, width * 0.35f, t1);
                var c0 = Color.Lerp(root, tip, t0);
                var c1 = Color.Lerp(root, tip, t1);
                var kit = K($"{spec.Name}_{i}");
                kit.Use(Surf.Hair).Surface(new[]
                {
                    new Ring(-spec.SegmentLength * 1.04f, w1, w1 * 0.75f), new Ring(-spec.SegmentLength * 0.5f, (w0 + w1) * 0.5f * 1.05f, (w0 + w1) * 0.4f),
                    new Ring(0.01f, w0, w0 * 0.75f),
                }, 10, (y, _) => S(y < -spec.SegmentLength * 0.75f ? c1 : c0), Matrix4x4.identity, Step, null, true, true);
                // Strands twisting around the tail (LOD 0/1) so it reads as hair, not a tube.
                if (!Lo)
                    for (int s = 0; s < 3; s++)
                    {
                        float a = s * 120f * Mathf.Deg2Rad;
                        var off = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        kit.Strand(off * w0 * 0.7f, off * w0 * 0.8f + Vector3.down * spec.SegmentLength * 0.35f, off * w1 * 0.8f + Vector3.down * spec.SegmentLength * 0.7f,
                            off * w1 * 0.5f + Vector3.down * spec.SegmentLength * 1.05f, w0 * 0.55f, 0.6f, S(c0), S(c1), Matrix4x4.identity, 5);
                    }
                if (i == spec.Segments - 1)
                    kit.Strand(new Vector3(0f, -spec.SegmentLength, 0f), new Vector3(0f, -spec.SegmentLength - 0.02f, 0.004f), new Vector3(0f, -spec.SegmentLength - 0.04f, 0.01f),
                        new Vector3(0f, -spec.SegmentLength - 0.06f, 0.02f), w1, 0.75f, S(c1), S(tip), Matrix4x4.identity);
            }
        }

        // ------------------------------------------------------------------ KAI: the Shadow Ronin

        private static void Kai(Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var id = Matrix4x4.identity;
            var spine = K("Spine");
            var trim = S(l.Secondary);
            var dyeTop = Mul(l.Primary, 0.7f);
            var dyeHem = l.Primary * 2.2f;
            var lacquer = S(new Color(0.07f, 0.05f, 0.1f));
            var cord = S(Color.Lerp(l.Secondary, l.Accent, 0.4f));
            var silver = S(new Color(0.78f, 0.8f, 0.88f));
            var gold = S(new Color(0.8f, 0.62f, 0.3f));
            var circuit = G(l.Accent, 0.85f);

            // Haori: bokashi-dyed twill (violet at the hem fading to near black at the shoulders), open at the front,
            // satin lapels with a glowing thread, drape folds below the chest.
            var torso = TorsoRings(b);
            var haori = Layer(torso, 0.018f, -0.08f, 0.455f);
            spine.Use(Surf.Fabric, Weave.Twill).Surface(haori, 30, (y, a) =>
            {
                float aa = Mathf.Abs(a);
                if (aa < 20f) return S(o.Shirt);
                if (aa < 34f) return trim;
                return S(Color.Lerp(dyeHem, dyeTop, Mathf.InverseLerp(-0.08f, 0.32f, y)));
            }, id, Step, (y, a) => Mathf.Abs(a) < 34f ? 0f : Drape(y, a, 0.24f, -0.08f, 0.005f, 9f),
                material: (y, a) => Mathf.Abs(a) < 34f && Mathf.Abs(a) >= 20f ? M(Surf.Cloth) : M(Surf.Fabric, Weave.Twill));
            for (int side = -1; side <= 1; side += 2)
            {
                spine.Use(Surf.Circuit, Weave.Plain, 0f);
                Trace(spine, haori, new[] { new Vector2(-0.07f, side * 34f), new Vector2(0.15f, side * 34f), new Vector2(0.3f, side * 34f), new Vector2(0.44f, side * 30f) },
                    0.003f, 0.0012f, circuit, id);
                spine.Use(Surf.Fabric, Weave.Twill);
                Seam(spine, haori, -0.07f, 0.42f, side * 36f, 0.0008f, S(o.Thread), id);
            }

            // Lamellar chest armour (do) in the open front: rows of lacquered plates laced with violet cord.
            if (!Lo)
                for (int row = 0; row < 5; row++)
                {
                    float y = 0.07f + row * 0.062f;
                    for (int c = -2; c <= 2; c++)
                    {
                        var m = OnSurface(haori, y, c * 8.5f, 0.007f, Matrix4x4.Rotate(Quaternion.Euler(-8f, 0f, 0f)));
                        spine.Use(Surf.Lacquer);
                        Plate(spine, new Vector3(0.028f, 0.058f, 0.007f), 0.0025f, row % 2 == 0 ? lacquer : S(Mul(l.Primary, 0.9f)), m);
                        if (Hi)
                        {
                            spine.Use(Surf.Gold, Weave.Plain, 0f).Box(new Vector3(0.026f, 0.003f, 0.002f), gold, m * MeshKit.At(new Vector3(0f, -0.027f, 0.0042f)));
                            spine.Use(Surf.Fabric, Weave.Rib, 0f);
                            for (int s = -1; s <= 1; s += 2)
                                spine.Box(new Vector3(0.003f, 0.016f, 0.002f), cord, m * MeshKit.At(new Vector3(s * 0.007f, 0.024f, 0.0042f)));
                            spine.Outline = 1f;
                        }
                    }
                    if (Hi)
                    {
                        var lace = new List<Vector3>();
                        for (int c = -3; c <= 3; c++)
                            lace.Add(MeshKit.OnRings(haori, y + 0.031f, c * 7f, 0.0105f));
                        spine.Use(Surf.Fabric, Weave.Rib).Tube(lace, 0.0018f, cord, id, 5);
                    }
                }

            // Clan crest on the back: a glowing ring with three tomoe commas.
            {
                var crest = OnSurface(haori, 0.3f, 180f, 0.002f, Matrix4x4.identity);
                spine.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.046f, 0.046f, 0.004f, 24, 5, circuit, crest * Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)));
                if (!Lo)
                    for (int t = 0; t < 3; t++)
                    {
                        float a = t * 120f * Mathf.Deg2Rad;
                        var p0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.012f;
                        var p1 = new Vector3(Mathf.Cos(a + 0.9f), Mathf.Sin(a + 0.9f), 0f) * 0.026f;
                        var p2 = new Vector3(Mathf.Cos(a + 1.8f), Mathf.Sin(a + 1.8f), 0f) * 0.03f;
                        spine.Strand(p0 + Vector3.forward * 0.002f, p1 + Vector3.forward * 0.002f, p2 + Vector3.forward * 0.002f, p2 * 0.9f + Vector3.forward * 0.002f,
                            0.009f, 0.3f, circuit, circuit, crest, 6);
                    }
            }
            // Seigaiha wave embroidery in silver thread around the hem (back and sides).
            if (Hi)
            {
                spine.Use(Surf.Gold, Weave.Plain, 0f);
                for (int row = 0; row < 2; row++)
                    for (float a0 = -180f + row * 7f; a0 < 180f; a0 += 14f)
                    {
                        if (Mathf.Abs(a0) < 44f)
                            continue;
                        var ring = MeshKit.RingAt(haori, -0.04f);
                        float radius = 0.02f;
                        var pts = new List<Vector2>();
                        for (int i = 0; i <= 6; i++)
                        {
                            float t = Mathf.PI * i / 6f;
                            pts.Add(new Vector2(-0.06f + row * 0.016f + Mathf.Sin(t) * radius, a0 + Mathf.Cos(t) * radius / ((ring.Rx + ring.Rz) * 0.5f) * Mathf.Rad2Deg));
                        }
                        Trace(spine, haori, pts, 0.0022f, 0.0015f, silver, id);
                    }
            }

            // Knit scarf wrapped around the neck (tails are cloth chains with fringes).
            spine.Use(Surf.Fabric, Weave.Rib).Torus(b.Neck * 1.9f, b.Neck * 1.8f, 0.03f, 18, 8, trim, MeshKit.At(new Vector3(0f, 0.47f, -0.005f)));
            spine.Torus(b.Neck * 1.75f, b.Neck * 1.65f, 0.025f, 18, 8, S(l.Secondary * 0.8f), MeshKit.At(new Vector3(0f, 0.505f, -0.005f)));
            for (int side = -1; side <= 1; side += 2)
                Chain(K, model, new DangleSpec { Name = side < 0 ? "ScarfL" : "ScarfR", Parent = "Spine", LocalPosition = new Vector3(side * 0.035f, 0.46f, -0.1f),
                    Segments = 3, SegmentLength = 0.17f, Stiffness = 10f }, 0.075f, 0.06f, 0.012f, trim, S(l.Accent), Surf.Fabric, Weave.Rib);

            // Obi sash with a twisted cord, a silver clan plate and tassels; coat skirt panels follow the legs.
            var pelvis = K("Pelvis");
            var obi = new[] { new Ring(0.005f, b.Waist + 0.02f, b.WaistDepth + 0.02f), new Ring(0.045f, b.Waist + 0.024f, b.WaistDepth + 0.024f),
                new Ring(0.085f, b.Waist + 0.02f, b.WaistDepth + 0.02f) };
            pelvis.Use(Surf.Fabric, Weave.Twill).Surface(obi, 24, trim, id, Step, (y, a) => Creases(y, a, 0.045f, 0.04f, 0.0012f, 3), true, true);
            RingStitches(pelvis, obi, 0.012f, 0.0006f, S(o.Thread), id);
            RingStitches(pelvis, obi, 0.078f, 0.0006f, S(o.Thread), id);
            if (Hi)
            {
                for (int strand = 0; strand < 2; strand++)
                {
                    var twist = new List<Vector3>();
                    for (int i = 0; i <= 64; i++)
                    {
                        float a = i / 64f * 360f;
                        var p = MeshKit.OnRings(obi, 0.045f, a, 0.006f);
                        twist.Add(p + Vector3.up * (Mathf.Sin(a * Mathf.Deg2Rad * 24f + strand * Mathf.PI) * 0.003f));
                    }
                    pelvis.Use(Surf.Fabric, Weave.Rib).Tube(twist, 0.0028f, strand == 0 ? cord : S(l.Accent * 0.8f), id, 5);
                }
            }
            else
                pelvis.Use(Surf.Fabric, Weave.Rib).Torus(b.Waist + 0.028f, b.WaistDepth + 0.028f, 0.004f, 24, 4, cord, MeshKit.At(new Vector3(0f, 0.045f, 0f)));
            pelvis.Use(Surf.Fabric, Weave.Twill).Ellipsoid(new Vector3(0.04f, 0.035f, 0.025f), 6, 10, S(l.Secondary * 1.2f), MeshKit.At(new Vector3(0.07f, 0.04f, b.WaistDepth + 0.04f)));
            pelvis.Strand(new Vector3(0.07f, 0.02f, b.WaistDepth + 0.045f), new Vector3(0.075f, -0.03f, b.WaistDepth + 0.05f),
                new Vector3(0.08f, -0.08f, b.WaistDepth + 0.045f), new Vector3(0.085f, -0.13f, b.WaistDepth + 0.04f), 0.008f, 1f, S(l.Accent), G(l.Accent, 0.4f), Matrix4x4.identity, 5);
            if (!Lo)
            {
                var plate = OnSurface(obi, 0.045f, -14f, 0.004f, Matrix4x4.identity);
                pelvis.Use(Surf.Metal).RoundBox(new Vector3(0.036f, 0.03f, 0.005f), 0.003f, silver, plate);
                pelvis.Use(Surf.Circuit, Weave.Plain, 0f).Disc(0.006f, 0.006f, 10, circuit, plate * MeshKit.At(new Vector3(0f, 0f, 0.0031f)));
                pelvis.Outline = 1f;
            }
            Chain(K, model, new DangleSpec { Name = "CoatBack", Parent = "Pelvis", LocalPosition = new Vector3(0f, 0.0f, -(b.HipDepth + 0.025f)), Segments = 2, SegmentLength = 0.24f,
                Stiffness = 14f }, 0.3f, 0.34f, 0.014f, S(dyeHem), trim);
            for (int side = -1; side <= 1; side += 2)
                Chain(K, model, new DangleSpec { Name = side < 0 ? "CoatFrontL" : "CoatFrontR", Parent = "Pelvis", LocalPosition = new Vector3(side * 0.1f, 0.0f, b.HipDepth + 0.02f),
                    Segments = 2, SegmentLength = 0.23f, Driver = side < 0 ? "ThighL" : "ThighR", DriverWeight = 0.75f, Stiffness = 16f }, 0.12f, 0.13f, 0.014f, S(dyeHem), trim);

            // Katana scabbard on the LEFT hip (right-handed draw): black lacquered saya, gold fittings, a wrapped sageo cord.
            // Mouth (+Y) forward and up at the hip, tip trailing down and back.
            var saya = MeshKit.TRS(new Vector3(-(b.Hip + 0.03f), -0.03f, 0.04f), new Vector3(62f, 0f, 8f), Vector3.one);
            pelvis.Use(Surf.Lacquer).Surface(new[] { new Ring(-0.74f, 0.012f, 0.008f), new Ring(-0.7f, 0.017f, 0.012f), new Ring(-0.3f, 0.019f, 0.013f),
                new Ring(0f, 0.02f, 0.014f), new Ring(0.02f, 0.021f, 0.015f) }, 12, S(new Color(0.06f, 0.05f, 0.08f)), saya, Step, null, true, true);
            pelvis.Use(Surf.Gold);
            pelvis.Surface(new[] { new Ring(-0.745f, 0.009f, 0.006f), new Ring(-0.735f, 0.0125f, 0.0085f), new Ring(-0.69f, 0.0175f, 0.0125f) }, 12, gold, saya, 0f, null, true, false);
            pelvis.Torus(0.022f, 0.016f, 0.004f, 10, 5, gold, saya * MeshKit.At(new Vector3(0f, -0.08f, 0f)));
            pelvis.Torus(0.022f, 0.016f, 0.004f, 10, 5, gold, saya * MeshKit.At(new Vector3(0f, -0.02f, 0f)));
            pelvis.RoundBox(new Vector3(0.008f, 0.02f, 0.012f), 0.002f, gold, saya * MeshKit.At(new Vector3(0f, -0.13f, 0.017f)));
            if (Hi)
            {
                var sageo = new List<Vector3>();
                for (int i = 0; i <= 40; i++)
                {
                    float t = i / 40f;
                    float a = t * Mathf.PI * 2f * 4f;
                    sageo.Add(new Vector3(Mathf.Sin(a) * 0.0225f, -0.13f - t * 0.1f, Mathf.Cos(a) * 0.017f));
                }
                pelvis.Use(Surf.Fabric, Weave.Rib).Tube(sageo, 0.0024f, cord, saya, 5);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Wide haori sleeve with the same dye, open at the elbow, drape folds and a trimmed hem.
                var sleeve = new[] { new Ring(-0.3f, b.Arm * 1.9f, b.Arm * 1.75f), new Ring(-0.15f, b.Arm * 1.55f, b.Arm * 1.45f), new Ring(0.02f, b.Arm * 1.45f, b.Arm * 1.4f) };
                var arm = K("UpperArm" + sfx);
                arm.Use(Surf.Fabric, Weave.Twill).Surface(sleeve, 18, (y, a) => S(Color.Lerp(dyeHem, dyeTop, Mathf.InverseLerp(-0.3f, 0.0f, y))), id, Step,
                    (y, a) => Drape(y, a, -0.05f, -0.3f, 0.004f, 6f), capTop: true);
                arm.Use(Surf.Cloth).Torus(b.Arm * 1.9f, b.Arm * 1.75f, 0.006f, 18, 5, trim, MeshKit.At(new Vector3(0f, -0.297f, 0f)));
                RingStitches(arm, sleeve, -0.28f, 0.0008f, S(o.Thread), id);
                // Bandaged forearm: a spiral wrap with overlapping edges in two shades of linen.
                var wrap = new[] { new Ring(-0.26f, b.Forearm * 0.72f, b.Forearm * 0.67f), new Ring(-0.2f, b.Forearm * 0.84f, b.Forearm * 0.8f),
                    new Ring(-0.14f, b.Forearm * 0.98f, b.Forearm * 0.92f), new Ring(-0.08f, b.Forearm * 1.15f, b.Forearm * 1.08f, 0f, 0.004f),
                    new Ring(-0.03f, b.Forearm * 1.1f, b.Forearm * 1.04f) };
                float Spiral(float y, float a) => Mathf.Repeat(y / 0.026f + a / 360f, 1f);
                K("Forearm" + sfx).Use(Surf.Fabric, Weave.Plain).Surface(wrap, 16,
                    (y, a) => S(Color.Lerp(new Color(0.93f, 0.91f, 0.87f), new Color(0.78f, 0.76f, 0.71f), Spiral(y, a))), id, Step * 0.5f,
                    (y, a) => -0.0016f * Spiral(y, a));
                // Ankle wraps.
                K("Foot" + sfx).Use(Surf.Fabric, Weave.Rib).Torus(b.Calf * 0.85f, b.Calf * 0.88f, 0.012f, 14, 6, trim, MeshKit.At(new Vector3(0f, 0.08f, 0f)));
                // Kyahan shin guards: three lacquered splints laced over the hakama.
                if (!Lo)
                {
                    var shin = ShinRings(b, 1.6f);
                    for (int s = -1; s <= 1; s++)
                    {
                        var m = OnSurface(shin, -0.2f, s * 22f, 0.004f, Matrix4x4.identity);
                        K("Shin" + sfx).Use(Surf.Lacquer);
                        Plate(K("Shin" + sfx), new Vector3(0.022f, 0.14f, 0.006f), 0.003f, lacquer, m);
                        if (Hi)
                            K("Shin" + sfx).Use(Surf.Gold, Weave.Plain, 0f).Box(new Vector3(0.018f, 0.004f, 0.002f), gold, m * MeshKit.At(new Vector3(0f, 0.062f, 0.0035f)));
                        K("Shin" + sfx).Outline = 1f;
                    }
                }
            }
            // Sode: a lamellar shoulder guard over the left sleeve, three rows of plates hanging from cords.
            if (!Lo)
            {
                var sleeveL = new[] { new Ring(-0.3f, b.Arm * 1.9f, b.Arm * 1.75f), new Ring(-0.15f, b.Arm * 1.55f, b.Arm * 1.45f), new Ring(0.02f, b.Arm * 1.45f, b.Arm * 1.4f) };
                var armL = K("UpperArmL");
                for (int row = 0; row < 3; row++)
                    for (int c = 0; c < 4; c++)
                    {
                        float a = -60f - c * 20f;
                        var m = OnSurface(sleeveL, 0.0f - row * 0.05f, a, 0.008f + row * 0.003f, Matrix4x4.Rotate(Quaternion.Euler(-12f, 0f, 0f)));
                        armL.Use(Surf.Lacquer);
                        Plate(armL, new Vector3(0.034f, 0.05f, 0.007f), 0.0025f, row == 2 ? S(Mul(l.Secondary, 0.7f)) : lacquer, m);
                        if (Hi)
                            armL.Use(Surf.Fabric, Weave.Rib, 0f).Box(new Vector3(0.003f, 0.012f, 0.002f), cord, m * MeshKit.At(new Vector3(0f, 0.022f, 0.0042f)));
                        armL.Outline = 1f;
                    }
                // Kote bracer on the left forearm: lacquered splints over the bandages, gold-capped.
                var foreL = K("ForearmL");
                var wrapRings = ForearmRings(b);
                for (int s = 0; s < 3; s++)
                {
                    var m = OnSurface(wrapRings, -0.15f, -70f - s * 20f, 0.006f, Matrix4x4.identity);
                    foreL.Use(Surf.Lacquer);
                    Plate(foreL, new Vector3(0.016f, 0.13f, 0.006f), 0.0025f, lacquer, m);
                    foreL.Use(Surf.Gold);
                    Plate(foreL, new Vector3(0.017f, 0.008f, 0.007f), 0.002f, gold, m * MeshKit.At(new Vector3(0f, 0.062f, 0f)));
                }
            }
            // Scar across the left eye (pale line on the face).
            K(CharacterBodyNames.Head).Use(Surf.Skin, Weave.Plain, 0f).Band(new[]
            {
                new Vector3(-0.062f, HeadCentreY + 0.06f, FaceZ(-0.062f, 0.06f) + 0.004f), new Vector3(-0.046f, HeadCentreY + 0.0f, FaceZ(-0.046f, 0f) + 0.006f),
                new Vector3(-0.034f, HeadCentreY - 0.045f, FaceZ(-0.034f, -0.045f) + 0.003f),
            }, new[] { 0.003f, 0.004f, 0.002f }, 0.002f, Vector3.right, _ => S(new Color(0.85f, 0.6f, 0.6f)), Matrix4x4.identity);
        }

        // ------------------------------------------------------------------ AKIRA: the Crimson Hunter

        private static void Akira(Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var id = Matrix4x4.identity;
            var spine = K("Spine");
            var leather = S(new Color(0.09f, 0.08f, 0.1f));
            var crimson = S(l.Secondary);
            var webbing = S(new Color(0.16f, 0.15f, 0.14f));
            var ripstop = S(new Color(0.2f, 0.19f, 0.18f));
            var metal = S(new Color(0.55f, 0.55f, 0.58f));
            var polymer = S(new Color(0.12f, 0.12f, 0.13f));
            var thread = S(o.Thread);
            var glow = G(l.Accent, 0.85f);

            // Cropped leather jacket: quilted crimson side panels, a crimson hem, half-open front zipper, stitched seams.
            var torso = TorsoRings(b);
            var jacket = Layer(torso, 0.017f, 0.13f, 0.455f);
            spine.Use(Surf.Leather).Surface(jacket, 28, (y, a) =>
            {
                float aa = Mathf.Abs(a);
                if (y > 0.36f && aa < 9f + (y - 0.36f) * 160f) return S(o.Shirt);   // open V above the zipper
                if (y < 0.155f) return crimson;                                       // hem
                return aa > 68f && aa < 112f ? crimson : leather;                     // side panels
            }, id, Step, (y, a) => Creases(y, a, 0.2f, 0.06f, 0.0015f, 2),
                material: (y, a) => Mathf.Abs(a) > 68f && Mathf.Abs(a) < 112f && y >= 0.155f ? M(Surf.Fabric, Weave.Quilt) : M(Surf.Leather));
            for (int side = -1; side <= 1; side += 2)
            {
                Seam(spine, jacket, 0.16f, 0.42f, side * 68f, 0.0008f, thread, id);
                Seam(spine, jacket, 0.16f, 0.42f, side * 112f, 0.0008f, thread, id);
            }
            RingStitches(spine, jacket, 0.158f, 0.0008f, thread, id);
            {
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                PathOnRings(jacket, 0.135f, 0.36f, 0f, 0.0008f, path, normals, 0.01f);
                Zipper(spine, path, normals, S(new Color(0.05f, 0.05f, 0.06f)), metal, id);
            }
            // High leather collar, open at the front, crimson lining, a brass snap.
            var collar = new[] { new Ring(0.44f, b.Neck * 1.7f, b.Neck * 1.6f), new Ring(0.5f, b.Neck * 1.62f, b.Neck * 1.52f), new Ring(0.55f, b.Neck * 1.55f, b.Neck * 1.45f) };
            spine.Use(Surf.Leather).Surface(collar, 18, (y, a) => Mathf.Abs(a) < 24f ? S(o.Shirt) : (y > 0.53f ? crimson : leather), id, Step);
            RingStitches(spine, collar, 0.535f, 0.0008f, thread, id, -150f, 150f);
            if (!Lo)
                spine.Use(Surf.Gold, Weave.Plain, 0f).Disc(0.005f, 0.005f, 10, S(new Color(0.8f, 0.62f, 0.32f)),
                    OnSurface(collar, 0.5f, 30f, 0.002f, Matrix4x4.identity));
            spine.Outline = 1f;

            // Plate carrier chest rig: ripstop panel with MOLLE webbing, three open-top magazine pouches with rifle mags,
            // shock cords, a glowing hunter's patch, padded shoulder straps with side-release buckles.
            float front = b.ChestDepth + 0.03f;
            spine.Use(Surf.Fabric, Weave.Ripstop).RoundBox(new Vector3(b.Chest * 1.5f, 0.17f, 0.03f), 0.008f, ripstop, MeshKit.At(new Vector3(0f, 0.22f, front)));
            if (Hi)
                for (int row = 0; row < 3; row++)
                {
                    float y = 0.165f + row * 0.025f;
                    spine.Use(Surf.Fabric, Weave.Rib).Box(new Vector3(b.Chest * 1.46f, 0.009f, 0.003f), webbing, MeshKit.At(new Vector3(0f, y, front + 0.0155f)));
                    for (int t = -4; t <= 4; t++)
                        spine.Use(Surf.Cloth, Weave.Plain, 0f).Box(new Vector3(0.0025f, 0.011f, 0.0035f), S(Mul(webbing, 0.6f)), MeshKit.At(new Vector3(t * 0.035f, y, front + 0.0158f)));
                    spine.Outline = 1f;
                }
            for (int m = -1; m <= 1; m++)
            {
                var at = new Vector3(m * 0.072f, 0.215f, front + 0.032f);
                spine.Use(Surf.Fabric, Weave.Ripstop).RoundBox(new Vector3(0.062f, 0.11f, 0.035f), 0.006f, S(new Color(0.24f, 0.22f, 0.2f)), MeshKit.At(at));
                // Rifle magazine: curved polymer body with ribs and a metal feed lip.
                spine.Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.04f, 0.024f), 0.004f, polymer, MeshKit.TRS(at + new Vector3(0f, 0.07f, -0.002f), new Vector3(8f, 0f, 0f), Vector3.one));
                spine.Use(Surf.Metal).Box(new Vector3(0.04f, 0.006f, 0.016f), metal, MeshKit.At(at + new Vector3(0f, 0.091f, -0.001f)));
                if (Hi)
                    for (int r = 0; r < 3; r++)
                        spine.Use(Surf.Polymer, Weave.Plain, 0f).Box(new Vector3(0.052f, 0.003f, 0.025f), S(Mul(polymer, 0.7f)), MeshKit.At(at + new Vector3(0f, 0.058f + r * 0.009f, -0.002f)));
                spine.Outline = 1f;
                // Shock-cord loop over the magazine and a pull tab.
                if (!Lo)
                {
                    spine.Use(Surf.Rubber).Tube(new[] { at + new Vector3(-0.024f, 0.055f, 0.019f), at + new Vector3(-0.01f, 0.08f, 0.016f), at + new Vector3(0.01f, 0.08f, 0.016f),
                        at + new Vector3(0.024f, 0.055f, 0.019f) }, 0.0022f, crimson, id, 5);
                    spine.Use(Surf.Fabric, Weave.Rib).RoundBox(new Vector3(0.012f, 0.018f, 0.003f), 0.0015f, crimson, MeshKit.At(at + new Vector3(0f, 0.084f, 0.017f)));
                }
                if (Hi)
                {
                    spine.Use(Surf.Fabric, Weave.Ripstop);
                    Stitches(spine, new[] { at + new Vector3(-0.027f, -0.05f, 0.0178f), at + new Vector3(-0.027f, 0.05f, 0.0178f) }, new[] { Vector3.forward, Vector3.forward }, thread, id);
                    Stitches(spine, new[] { at + new Vector3(0.027f, -0.05f, 0.0178f), at + new Vector3(0.027f, 0.05f, 0.0178f) }, new[] { Vector3.forward, Vector3.forward }, thread, id);
                }
            }
            // Hunter's patch: a crimson patch with three glowing claw marks.
            if (!Lo)
            {
                var patch = MeshKit.At(new Vector3(-0.1f, 0.33f, b.ChestDepth + 0.024f), new Vector3(-10f, 0f, 0f));
                spine.Use(Surf.Fabric, Weave.Plain).RoundBox(new Vector3(0.05f, 0.035f, 0.003f), 0.003f, S(Mul(l.Secondary, 0.6f)), patch);
                spine.Use(Surf.Circuit, Weave.Plain, 0f);
                for (int c = -1; c <= 1; c++)
                    spine.Band(new[] { new Vector3(c * 0.011f - 0.006f, 0.012f, 0.0018f), new Vector3(c * 0.011f + 0.006f, -0.012f, 0.0018f) }, new[] { 0.004f, 0.0015f }, 0.001f,
                        Vector3.right, _ => glow, patch);
                spine.Outline = 1f;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var strap = new[]
                {
                    new Vector3(side * 0.09f, 0.3f, front), new Vector3(side * 0.12f, 0.43f, b.ChestDepth * 0.6f), new Vector3(side * 0.11f, 0.45f, 0f),
                    new Vector3(side * 0.1f, 0.36f, -b.ChestDepth - 0.02f), new Vector3(side * 0.09f, 0.2f, -b.ChestDepth - 0.018f),
                };
                spine.Use(Surf.Fabric, Weave.Quilt).Band(strap, new[] { 0.045f, 0.045f, 0.045f, 0.045f, 0.045f }, 0.01f, Vector3.right, _ => webbing, id);
                if (!Lo)
                    spine.Use(Surf.Polymer).RoundBox(new Vector3(0.03f, 0.024f, 0.012f), 0.004f, polymer, MeshKit.At(new Vector3(side * 0.095f, 0.33f, front + 0.012f), new Vector3(-20f, 0f, 0f)));
            }

            // Leather belt with a cobra buckle and a carabiner, dump pouch on the left hip, cargo pockets, holster on the RIGHT thigh.
            var pelvis = K("Pelvis");
            var belt = new[] { new Ring(0.014f, b.Waist + 0.012f, b.WaistDepth + 0.012f), new Ring(0.046f, b.Waist + 0.012f, b.WaistDepth + 0.012f) };
            pelvis.Use(Surf.Leather).Surface(belt, 24, S(o.Belt), id, 0f, null, true, true);
            RingStitches(pelvis, belt, 0.018f, 0.0006f, thread, id);
            RingStitches(pelvis, belt, 0.042f, 0.0006f, thread, id);
            pelvis.Use(Surf.Metal).RoundBox(new Vector3(0.05f, 0.036f, 0.01f), 0.003f, metal, MeshKit.At(new Vector3(0f, 0.03f, b.WaistDepth + 0.022f)));
            if (!Lo)
            {
                pelvis.Use(Surf.Polymer).RoundBox(new Vector3(0.03f, 0.022f, 0.006f), 0.003f, polymer, MeshKit.At(new Vector3(0f, 0.03f, b.WaistDepth + 0.029f)));
                pelvis.Use(Surf.Metal).Torus(0.012f, 0.008f, 0.0022f, 12, 5, metal, MeshKit.TRS(new Vector3(0.09f, -0.0f, b.WaistDepth + 0.02f), new Vector3(0f, 0f, 90f), Vector3.one));
            }
            pelvis.Use(Surf.Fabric, Weave.Ripstop).RoundBox(new Vector3(0.07f, 0.09f, 0.06f), 0.012f, ripstop, MeshKit.At(new Vector3(-(b.Hip + 0.02f), -0.05f, -0.02f)));
            if (Hi)
                pelvis.Use(Surf.Fabric, Weave.Rib).Tube(new[] { new Vector3(-(b.Hip + 0.055f), -0.005f, -0.04f), new Vector3(-(b.Hip + 0.058f), 0.0f, -0.02f),
                    new Vector3(-(b.Hip + 0.055f), -0.005f, 0.0f) }, 0.0018f, crimson, id, 5);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                var thigh = K("Thigh" + sfx);
                // Cargo pocket: bellows pocket, flap, a snap, stitched edges.
                var pocket = MeshKit.At(new Vector3(side * (b.Thigh + 0.008f), -0.2f, 0f));
                thigh.Use(Surf.Fabric, Weave.Ripstop).RoundBox(new Vector3(0.03f, 0.1f, 0.08f), 0.008f, S(Mul(o.Pants, 0.85f)), pocket);
                thigh.RoundBox(new Vector3(0.034f, 0.03f, 0.084f), 0.006f, S(Mul(o.Pants, 0.75f)), pocket * MeshKit.At(new Vector3(0f, 0.042f, 0f)));
                if (!Lo)
                    thigh.Use(Surf.Metal, Weave.Plain, 0f).Disc(0.004f, 0.004f, 8, metal, pocket * MeshKit.TRS(new Vector3(side * 0.0175f, 0.036f, 0f), new Vector3(0f, side * 90f, 0f), Vector3.one));
                thigh.Outline = 1f;
                // Knee pads: polymer shell over a rubber base with a strap.
                var shin = K("Shin" + sfx);
                shin.Use(Surf.Rubber).Ellipsoid(new Vector3(b.Calf * 1.15f, 0.055f, 0.032f), 6, 10, S(new Color(0.08f, 0.08f, 0.09f)),
                    MeshKit.At(new Vector3(0f, -0.01f, b.Calf * 0.88f)), q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.008f)));
                shin.Use(Surf.Polymer).Ellipsoid(new Vector3(b.Calf * 0.9f, 0.045f, 0.03f), 8, 12, polymer,
                    MeshKit.At(new Vector3(0f, -0.005f, b.Calf * 0.95f)), q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.006f)));
                shin.Use(Surf.Fabric, Weave.Rib).Torus(b.Calf * 1.05f, b.Calf * 1.02f, 0.006f, 12, 5, crimson, MeshKit.At(new Vector3(0f, -0.04f, 0f)));
            }
            // Holster on the RIGHT thigh: polymer shell, pistol grip with stippling and slide, two leg straps with buckles.
            var thighR = K("ThighR");
            thighR.Use(Surf.Polymer).RoundBox(new Vector3(0.04f, 0.14f, 0.07f), 0.01f, polymer, MeshKit.At(new Vector3(b.Thigh + 0.03f, -0.13f, 0.01f)));
            thighR.RoundBox(new Vector3(0.026f, 0.065f, 0.036f), 0.006f, S(new Color(0.07f, 0.07f, 0.08f)), MeshKit.TRS(new Vector3(b.Thigh + 0.035f, -0.035f, -0.01f), new Vector3(-15f, 0f, 0f), Vector3.one));
            if (!Lo)
                thighR.Use(Surf.Metal).RoundBox(new Vector3(0.02f, 0.02f, 0.05f), 0.003f, metal, MeshKit.At(new Vector3(b.Thigh + 0.035f, -0.06f, 0.01f)));
            thighR.Use(Surf.Fabric, Weave.Rib).Torus(b.Thigh * 1.02f, b.Thigh * 1.0f, 0.007f, 14, 5, webbing, MeshKit.At(new Vector3(0f, -0.1f, 0f)));
            thighR.Torus(b.Thigh * 0.92f, b.Thigh * 0.9f, 0.007f, 14, 5, webbing, MeshKit.At(new Vector3(0f, -0.2f, 0f)));
            if (!Lo)
                for (int s = 0; s < 2; s++)
                    thighR.Use(Surf.Polymer).RoundBox(new Vector3(0.02f, 0.016f, 0.008f), 0.003f, polymer, MeshKit.At(new Vector3(0f, -0.1f - s * 0.1f, b.Thigh * (1.02f - s * 0.1f) + 0.006f)));

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Leather sleeves pushed up to the forearm with a crimson cuff, a polymer elbow pad, quilted shoulders.
                var push = new[] { new Ring(-0.14f, b.Forearm * 1.25f, b.Forearm * 1.2f), new Ring(-0.06f, b.Forearm * 1.32f, b.Forearm * 1.27f),
                    new Ring(0.03f, b.Forearm * 1.3f, b.Forearm * 1.25f) };
                K("Forearm" + sfx).Use(Surf.Leather).Surface(push, 16, (y, a) => y < -0.125f ? crimson : leather, id, Step,
                    (y, a) => -0.002f * Mathf.Max(0f, Mathf.Sin((y + 0.14f) / 0.17f * Mathf.PI * 5f + Mathf.Sin(a * Mathf.Deg2Rad * 3f))), capTop: true);
                K("Forearm" + sfx).Use(Surf.Polymer).Ellipsoid(new Vector3(b.Forearm * 0.9f, 0.04f, 0.025f), 6, 10, polymer,
                    MeshKit.At(new Vector3(0f, -0.005f, -b.Forearm * 1.25f)), q => new Vector3(q.x, q.y, Mathf.Min(q.z, 0.006f)));
                var shoulder = new[] { new Ring(-0.12f, b.Arm * 1.18f, b.Arm * 1.2f), new Ring(-0.04f, b.Arm * 1.28f, b.Arm * 1.3f), new Ring(0.02f, b.Arm * 1.3f, b.Arm * 1.32f) };
                K("UpperArm" + sfx).Use(Surf.Fabric, Weave.Quilt).Surface(shoulder, 16, crimson, id, Step);
                RingStitches(K("UpperArm" + sfx), shoulder, -0.115f, 0.0008f, thread, id);
            }
            // Hunter's armband on the left arm with a glowing tracker light.
            K("UpperArmL").Use(Surf.Fabric, Weave.Rib).Torus(b.Arm * 1.2f, b.Arm * 1.2f, 0.016f, 14, 6, crimson, MeshKit.At(new Vector3(0f, -0.16f, 0f)));
            K("UpperArmL").Use(Surf.Circuit, Weave.Plain, 0f).Disc(0.016f, 0.016f, 10, glow, MeshKit.TRS(new Vector3(-b.Arm * 1.33f, -0.16f, 0f), new Vector3(0f, -90f, 0f), Vector3.one));
            K("UpperArmL").Outline = 1f;
            // Ponytail tied at the back of the head: hunters keep hair out of the sights. It swings (cloth chain).
            HairChain(K, model, new DangleSpec { Name = "Ponytail", Parent = CharacterBodyNames.Head, LocalPosition = new Vector3(0f, HeadCentreY - 0.03f, -0.12f),
                Segments = 3, SegmentLength = 0.085f, Stiffness = 9f }, 0.03f, l.Hair, l.HairTips);
            // Earring (right ear).
            K(CharacterBodyNames.Head).Use(Surf.Gold, Weave.Plain, 0f).Torus(0.007f, 0.007f, 0.0018f, 10, 4, S(new Color(0.85f, 0.7f, 0.45f)),
                MeshKit.TRS(new Vector3(0.1f, HeadCentreY - 0.04f, -0.004f), new Vector3(0f, 0f, 90f), Vector3.one));
        }

        // ------------------------------------------------------------------ YUNA: the Neon Phantom

        private static void Yuna(Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var id = Matrix4x4.identity;
            var spine = K("Spine");
            var white = S(l.Primary);
            var plate = S(Color.Lerp(l.Primary, Color.white, 0.4f));
            var dark = S(new Color(0.16f, 0.17f, 0.22f));
            var circuit = G(l.Accent, 0.9f);
            var thread = S(o.Thread);

            // Techwear jacket: white ripstop with polymer yoke panels, a glowing zipper, glowing hem.
            var torso = TorsoRings(b);
            var jacket = Layer(torso, 0.016f, 0.02f, 0.455f);
            spine.Use(Surf.Fabric, Weave.Ripstop).Surface(jacket, 28, (y, a) =>
            {
                if (y < 0.035f) return circuit;
                return y > 0.37f ? plate : white;
            }, id, Step, (y, a) => Creases(y, a, 0.1f, 0.06f, 0.0012f, 2),
                material: (y, a) => y < 0.035f ? M(Surf.Circuit) : y > 0.37f ? M(Surf.Polymer) : M(Surf.Fabric, Weave.Ripstop));
            RingStitches(spine, jacket, 0.365f, 0.0008f, thread, id);
            for (int side = -1; side <= 1; side += 2)
                Seam(spine, jacket, 0.04f, 0.36f, side * 100f, 0.0008f, thread, id);
            {
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                PathOnRings(jacket, 0.03f, 0.44f, 0f, 0.0008f, path, normals, 0.01f);
                spine.Use(Surf.Circuit, Weave.Plain, 0f);
                var widths = new float[path.Count];
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = 0.006f;
                spine.Band(path, widths, 0.002f, Vector3.right, _ => circuit, id);
                spine.Outline = 1f;
            }
            // Circuit traces across the back and sides: right-angled runs with nodes, light flowing along them.
            spine.Use(Surf.Circuit, Weave.Plain, 0f);
            int traces = Lo ? 2 : Hi ? 7 : 4;
            for (int t = 0; t < traces; t++)
            {
                float a0 = 130f + t * (100f / Mathf.Max(1, traces - 1));
                float jog = (t % 2 == 0 ? 1f : -1f) * 12f;
                var pts = new[] { new Vector2(0.05f, a0), new Vector2(0.17f + t * 0.012f, a0), new Vector2(0.22f + t * 0.012f, a0 + jog), new Vector2(0.34f, a0 + jog) };
                Trace(spine, jacket, pts, 0.0035f, 0.0012f, circuit, id);
                if (!Lo)
                    spine.Disc(0.005f, 0.005f, 10, circuit, OnSurface(jacket, 0.34f, a0 + jog, 0.0016f, Matrix4x4.identity));
            }
            spine.Outline = 1f;
            // Hood folded down at the back of the neck, glowing piping.
            spine.Use(Surf.Fabric, Weave.Ripstop).Ellipsoid(new Vector3(0.11f, 0.07f, 0.06f), 8, 14, white, MeshKit.At(new Vector3(0f, 0.47f, -0.07f)));
            spine.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.11f, 0.06f, 0.006f, 16, 5, circuit, MeshKit.At(new Vector3(0f, 0.5f, -0.07f)));
            spine.Outline = 1f;
            // Data spine on the back: a polymer module with cooling fins, glowing slots and two cables into the jacket.
            {
                var at = new Vector3(0f, 0.25f, -(b.ChestDepth + 0.045f));
                spine.Use(Surf.Polymer).RoundBox(new Vector3(0.1f, 0.2f, 0.04f), 0.01f, plate, MeshKit.At(at));
                spine.RoundBox(new Vector3(0.07f, 0.16f, 0.012f), 0.004f, dark, MeshKit.At(at + new Vector3(0f, 0f, -0.022f)));
                if (!Lo)
                    for (int f = 0; f < 4; f++)
                        spine.Use(Surf.Carbon).RoundBox(new Vector3(0.006f, 0.12f, 0.02f), 0.002f, S(new Color(0.12f, 0.12f, 0.15f)),
                            MeshKit.At(at + new Vector3((f - 1.5f) * 0.016f, 0f, -0.035f)));
                spine.Use(Surf.Circuit, Weave.Plain, 0f);
                for (int s = 0; s < 3; s++)
                    spine.Box(new Vector3(0.05f, 0.004f, 0.002f), circuit, MeshKit.At(at + new Vector3(0f, 0.085f - s * 0.012f, -0.0205f)));
                spine.Outline = 1f;
                if (!Lo)
                    for (int s = -1; s <= 1; s += 2)
                        spine.Use(Surf.Rubber).Tube(new[] { at + new Vector3(s * 0.05f, -0.07f, 0.0f), at + new Vector3(s * 0.07f, -0.1f, 0.015f), at + new Vector3(s * 0.08f, -0.14f, 0.03f) },
                            0.005f, dark, id, 6);
            }
            // Back tail of the jacket (cloth chain), shorts, thigh plates and glowing straps.
            Chain(K, model, new DangleSpec { Name = "JacketTail", Parent = "Spine", LocalPosition = new Vector3(0f, 0.04f, -(b.WaistDepth + 0.02f)), Segments = 2,
                SegmentLength = 0.17f, Stiffness = 14f }, 0.2f, 0.12f, 0.012f, white, circuit, Surf.Fabric, Weave.Ripstop);
            var pelvis = K("Pelvis");
            var shorts = new[] { new Ring(-0.2f, b.Hip * 0.85f + 0.01f, b.HipDepth * 0.87f + 0.01f), new Ring(-0.1f, b.Hip + 0.012f, b.HipDepth + 0.012f),
                new Ring(0.05f, b.Waist + 0.012f, b.WaistDepth + 0.012f) };
            pelvis.Use(Surf.Fabric, Weave.Ripstop).Surface(shorts, 20, white, id, Step);
            Seam(pelvis, shorts, -0.19f, 0.04f, 90f, 0.0008f, thread, id);
            Seam(pelvis, shorts, -0.19f, 0.04f, -90f, 0.0008f, thread, id);
            pelvis.Use(Surf.Circuit, Weave.Plain, 0f).Torus(b.Waist + 0.018f, b.WaistDepth + 0.018f, 0.008f, 18, 5, circuit, MeshKit.At(new Vector3(0f, 0.04f, 0f)));
            pelvis.Outline = 1f;
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                var thigh = K("Thigh" + sfx);
                var cuff = new[] { new Ring(-0.09f, b.Thigh * 1.12f, b.Thigh * 1.1f), new Ring(0.04f, b.Thigh * 1.12f, b.Thigh * 1.08f) };
                thigh.Use(Surf.Fabric, Weave.Ripstop).Surface(cuff, 18, white, id, Step, null, true, false);
                // Polymer side plate with a glowing strip.
                thigh.Use(Surf.Polymer).RoundBox(new Vector3(0.012f, 0.12f, 0.06f), 0.005f, plate, MeshKit.At(new Vector3(side * (b.Thigh * 1.12f + 0.004f), -0.12f, 0f)));
                thigh.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.1f, 0.006f), circuit, MeshKit.At(new Vector3(side * (b.Thigh * 1.12f + 0.011f), -0.12f, 0f)));
                thigh.Torus(b.Thigh * 0.98f, b.Thigh * 0.96f, 0.006f, 14, 5, circuit, MeshKit.At(new Vector3(0f, -0.2f, 0f)));
                thigh.Outline = 1f;
                // Shin guards: polymer shells with a glowing line.
                var shinRings = ShinRings(b, 1f);
                var guard = OnSurface(shinRings, -0.2f, 0f, 0.006f, Matrix4x4.identity);
                K("Shin" + sfx).Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.16f, 0.012f), 0.006f, plate, guard);
                K("Shin" + sfx).Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.12f, 0.003f), circuit, guard * MeshKit.At(new Vector3(0f, 0f, 0.0065f)));
                K("Shin" + sfx).Outline = 1f;
                // Glowing soles.
                K("Foot" + sfx).Use(Surf.Circuit, Weave.Plain, 0f).Torus(b.Calf * 0.82f, b.Calf * 0.85f, 0.006f, 14, 5, circuit, MeshKit.At(new Vector3(0f, 0.06f, 0f)));
                K("Foot" + sfx).Box(new Vector3(0.112f, 0.005f, 0.26f), circuit, MeshKit.At(new Vector3(0f, -0.047f, 0.05f)));
                K("Foot" + sfx).Outline = 1f;
                // Shoulder caps: polymer shells with a glowing edge; light seams on the sleeves.
                var arm = K("UpperArm" + sfx);
                arm.Use(Surf.Polymer).Ellipsoid(new Vector3(b.Arm * 1.55f, 0.05f, b.Arm * 1.5f), 6, 14, plate,
                    MeshKit.TRS(new Vector3(side * 0.004f, 0.01f, 0f), new Vector3(0f, 0f, side * 14f), Vector3.one), q => new Vector3(q.x, Mathf.Max(q.y, -0.008f), q.z));
                arm.Use(Surf.Circuit, Weave.Plain, 0f).Torus(b.Arm * 1.5f, b.Arm * 1.45f, 0.003f, 18, 4, circuit,
                    MeshKit.TRS(new Vector3(side * 0.004f, 0.0f, 0f), new Vector3(0f, 0f, side * 14f), Vector3.one));
                arm.Torus(b.Arm * 1.12f, b.Arm * 1.12f, 0.005f, 12, 5, circuit, MeshKit.At(new Vector3(0f, -0.22f, 0f)));
                arm.Outline = 1f;
                if (!Lo)
                {
                    arm.Use(Surf.Circuit, Weave.Plain, 0f);
                    var upper = UpperArmRings(b);
                    Trace(arm, upper, new[] { new Vector2(-0.02f, side * 90f), new Vector2(-0.12f, side * 90f), new Vector2(-0.16f, side * 110f), new Vector2(-0.21f, side * 110f) },
                        0.003f, 0.0022f, circuit, id);
                    arm.Outline = 1f;
                }
            }
            // Wrist computer on the LEFT forearm: polymer casing, glowing screen, buttons; a guard on the right forearm.
            var foreL = K("ForearmL");
            foreL.Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.085f, 0.064f), 0.008f, S(new Color(0.18f, 0.2f, 0.26f)), MeshKit.At(new Vector3(-b.Forearm * 0.9f, -0.18f, 0f)));
            foreL.Use(Surf.Circuit, Weave.Plain, 0f).RoundBox(new Vector3(0.004f, 0.065f, 0.048f), 0.0015f, circuit, MeshKit.At(new Vector3(-b.Forearm * 0.9f - 0.026f, -0.18f, 0f)));
            foreL.Outline = 1f;
            if (Hi)
                for (int i = 0; i < 3; i++)
                    foreL.Use(Surf.Polymer, Weave.Plain, 0f).RoundBox(new Vector3(0.004f, 0.008f, 0.008f), 0.0015f, plate,
                        MeshKit.At(new Vector3(-b.Forearm * 0.9f - 0.026f, -0.226f, (i - 1) * 0.014f)));
            foreL.Outline = 1f;
            var foreRings = ForearmRings(b);
            K("ForearmR").Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.1f, 0.01f), 0.004f, plate, OnSurface(foreRings, -0.16f, 90f, 0.006f, Matrix4x4.identity));

            // Holographic half-visor over the right eye, earpiece with antenna on the right ear.
            var head = K(CharacterBodyNames.Head);
            const float ex = 0.042f, ey = -0.006f;
            float vz = FaceZ(ex, ey) + 0.014f;
            Vector3 V(float x, float y) => new Vector3(x, HeadCentreY + y, Mathf.Min(vz, FaceZ(Mathf.Min(x, 0.09f), y) + 0.014f) - Mathf.Max(0f, x - 0.08f) * 0.6f);
            head.Use(Surf.Circuit, Weave.Plain, 0f);
            head.Band(new[] { V(0.012f, ey + 0.033f), V(0.045f, ey + 0.037f), V(0.08f, ey + 0.032f), V(0.104f, ey + 0.02f) }, new[] { 0.004f, 0.005f, 0.005f, 0.004f }, 0.003f,
                Vector3.up, _ => G(l.Accent, 0.9f), Matrix4x4.identity);
            head.Band(new[] { V(0.02f, ey - 0.034f), V(0.05f, ey - 0.036f), V(0.078f, ey - 0.03f) }, new[] { 0.0022f, 0.0025f, 0.0022f }, 0.002f,
                Vector3.up, _ => G(l.Accent, 0.9f), Matrix4x4.identity);
            head.Use(Surf.Polymer, Weave.Plain, 0f).Band(new[] { V(0.08f, ey + 0.032f), V(0.09f, ey), V(0.104f, ey - 0.008f) }, new[] { 0.004f, 0.004f, 0.004f }, 0.003f,
                Vector3.right, _ => S(new Color(0.85f, 0.88f, 0.95f)), Matrix4x4.identity);
            head.Use(Surf.Circuit, Weave.Plain, 0f);
            var reticle = Matrix4x4.TRS(V(ex, ey) + new Vector3(0f, 0f, 0.002f), Quaternion.Euler(90f, 14f, 0f), Vector3.one);
            head.Torus(0.0145f, 0.0145f, 0.0011f, 20, 4, G(l.Accent, 1f), reticle);
            for (int tick = 0; tick < 4; tick++)
            {
                float a = tick * 90f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                head.Band(new[] { V(ex, ey) + dir * 0.016f + Vector3.forward * 0.002f, V(ex, ey) + dir * 0.021f + Vector3.forward * 0.002f }, new[] { 0.0015f, 0.0015f }, 0.0012f,
                    Vector3.forward, _ => G(l.Accent, 1f), Matrix4x4.identity);
            }
            head.Use(Surf.Polymer).RoundBox(new Vector3(0.022f, 0.04f, 0.03f), 0.006f, S(new Color(0.85f, 0.88f, 0.95f)), MeshKit.At(new Vector3(0.106f, HeadCentreY - 0.01f, -0.005f)));
            head.Use(Surf.Circuit, Weave.Plain, 0f).Disc(0.008f, 0.008f, 8, G(l.Accent, 0.9f), MeshKit.TRS(new Vector3(0.118f, HeadCentreY - 0.01f, -0.005f), new Vector3(0f, 90f, 0f), Vector3.one));
            head.Use(Surf.Metal, Weave.Plain, 1f).Strand(new Vector3(0.11f, HeadCentreY + 0.01f, -0.01f), new Vector3(0.115f, HeadCentreY + 0.05f, -0.02f), new Vector3(0.115f, HeadCentreY + 0.09f, -0.03f),
                new Vector3(0.112f, HeadCentreY + 0.12f, -0.035f), 0.004f, 1f, S(new Color(0.8f, 0.82f, 0.9f)), G(l.Accent, 0.9f), Matrix4x4.identity, 5);
            head.Use(Surf.Skin);
        }

        // ------------------------------------------------------------------ REN: the Iron Wolf

        private static void Ren(Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var id = Matrix4x4.identity;
            var spine = K("Spine");
            var steel = S(l.Secondary);
            var darkSteel = S(Mul(l.Secondary, 0.6f));
            var dark = S(l.Primary);
            var metal = S(new Color(0.5f, 0.52f, 0.56f));
            var brass = S(new Color(0.85f, 0.65f, 0.3f));
            var amber = G(l.Accent, 0.9f);
            var rivet = S(new Color(0.62f, 0.62f, 0.66f));
            var scratch = S(Color.Lerp(l.Secondary, Color.white, 0.45f));

            // Brushed-steel chest and back plates with pectoral plates, panel lines, rivets and battle scratches.
            var chestM = MeshKit.At(new Vector3(0f, 0.28f, b.ChestDepth - 0.01f));
            spine.Use(Surf.Metal).Ellipsoid(new Vector3(b.Chest * 0.95f, 0.13f, 0.06f), 8, 16, steel, chestM, q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.01f)));
            spine.Ellipsoid(new Vector3(b.Chest * 0.95f, 0.14f, 0.06f), 8, 16, steel,
                MeshKit.At(new Vector3(0f, 0.28f, -(b.ChestDepth - 0.01f))), q => new Vector3(q.x, q.y, Mathf.Min(q.z, 0.01f)));
            for (int side = -1; side <= 1; side += 2)
                spine.RoundBox(new Vector3(b.Chest * 0.8f, 0.09f, 0.018f), 0.008f, steel,
                    MeshKit.TRS(new Vector3(side * b.Chest * 0.45f, 0.32f, b.ChestDepth + 0.046f), new Vector3(-8f, side * 14f, 0f), Vector3.one));
            if (!Lo)
            {
                spine.Use(Surf.Metal, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.2f, 0.003f), darkSteel, MeshKit.At(new Vector3(0f, 0.27f, b.ChestDepth + 0.05f)));
                spine.Outline = 1f;
            }
            if (Hi)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int r = 0; r < 4; r++)
                    {
                        var at = new Vector3(side * (b.Chest * 0.12f + r * b.Chest * 0.22f), 0.365f - r * 0.004f, b.ChestDepth + 0.058f);
                        Rivet(spine, at, new Vector3(side * 0.24f, 0.1f, 1f).normalized, 0.0035f, rivet, id);
                    }
                spine.Use(Surf.Metal, Weave.Plain, 0f);
                var rng = new System.Random(31);
                for (int s = 0; s < 6; s++)
                {
                    float x = ((float)rng.NextDouble() - 0.5f) * b.Chest * 1.2f, y = 0.24f + (float)rng.NextDouble() * 0.12f;
                    var p0 = new Vector3(x, y, b.ChestDepth + 0.057f);
                    var p1 = p0 + new Vector3(0.03f + (float)rng.NextDouble() * 0.03f, -0.012f, -0.002f);
                    spine.Band(new[] { p0, p1 }, new[] { 0.0018f, 0.0006f }, 0.0006f, Vector3.up, _ => scratch, id);
                }
                spine.Outline = 1f;
            }
            // Abdominal plates: overlapping steel bands.
            spine.Use(Surf.Metal);
            for (int i = 0; i < 3; i++)
                spine.RoundBox(new Vector3(b.Waist * 1.3f, 0.048f, 0.022f), 0.007f, i % 2 == 0 ? steel : darkSteel,
                    MeshKit.TRS(new Vector3(0f, 0.12f - i * 0.05f, b.WaistDepth + 0.012f), new Vector3(-6f, 0f, 0f), Vector3.one));
            // Wolf-fur mantle around the collar.
            var fur = S(new Color(0.55f, 0.55f, 0.58f));
            var furTip = S(new Color(0.85f, 0.85f, 0.88f));
            var random = new System.Random(77);
            int tufts = Lo ? 14 : Hi ? 44 : 26;
            spine.Use(Surf.Fur);
            for (int i = 0; i < tufts; i++)
            {
                float a = (i / (float)tufts) * Mathf.PI * 2f;
                var root = new Vector3(Mathf.Sin(a) * b.Shoulder * 0.62f, 0.45f + (i % 2) * 0.012f, Mathf.Cos(a) * b.ChestDepth * 0.95f);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float len = 0.06f + (float)random.NextDouble() * 0.04f;
                spine.Strand(root, root + outward * len * 0.4f + Vector3.up * 0.01f, root + outward * len * 0.8f, root + outward * len + Vector3.down * 0.03f,
                    Lo ? 0.03f : 0.022f, 0.6f, fur, furTip, Matrix4x4.identity, 4);
            }
            // Shell bandolier: right shoulder to left hip, stitched leather, red shells with brass caps.
            var path = new[] { new Vector3(0.14f, 0.43f, 0.05f), new Vector3(0.06f, 0.3f, b.ChestDepth + 0.07f), new Vector3(-0.06f, 0.16f, b.ChestDepth + 0.05f),
                new Vector3(-0.14f, 0.02f, b.WaistDepth + 0.03f) };
            spine.Use(Surf.Leather).Band(path, new[] { 0.05f, 0.05f, 0.05f, 0.05f }, 0.01f, Vector3.right, _ => S(new Color(0.3f, 0.22f, 0.15f)), id);
            if (Hi)
                for (int s = -1; s <= 1; s += 2)
                {
                    var stitches = new List<Vector3>();
                    var normals = new List<Vector3>();
                    foreach (var p in path)
                    {
                        stitches.Add(p + new Vector3(s * 0.021f, 0f, 0.0056f));
                        normals.Add(Vector3.forward);
                    }
                    Stitches(spine, stitches, normals, S(new Color(0.65f, 0.55f, 0.4f)), id);
                }
            int shells = Lo ? 4 : 7;
            for (int i = 0; i < shells; i++)
            {
                float t = 0.15f + i * (0.66f / (shells - 1));
                int seg = Mathf.Min(2, (int)(t * 3f));
                var p = Vector3.Lerp(path[seg], path[seg + 1], t * 3f - seg) + Vector3.forward * 0.012f;
                var dir = (path[seg + 1] - path[seg]).normalized;
                var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Cross(dir, Vector3.forward).normalized);
                var shellM = Matrix4x4.TRS(p, rot, Vector3.one);
                spine.Use(Surf.Polymer).Loft(new[] { new Ring(-0.022f, 0.008f, 0.008f), new Ring(0.016f, 0.008f, 0.008f) }, 10, S(new Color(0.7f, 0.12f, 0.12f)), shellM);
                spine.Use(Surf.Gold).Loft(new[] { new Ring(0.016f, 0.0085f, 0.0085f), new Ring(0.026f, 0.0085f, 0.0085f) }, 10, brass, shellM);
            }
            // Dog tags on a ball chain.
            spine.Use(Surf.Metal).Tube(new[] { new Vector3(-0.04f, 0.46f, b.ChestDepth * 0.7f), new Vector3(-0.03f, 0.4f, b.ChestDepth + 0.05f),
                new Vector3(0.0f, 0.35f, b.ChestDepth + 0.06f), new Vector3(0.012f, 0.335f, b.ChestDepth + 0.062f) }, 0.0012f, metal, id, 5);
            spine.RoundBox(new Vector3(0.022f, 0.035f, 0.003f), 0.004f, metal, MeshKit.TRS(new Vector3(0.012f, 0.31f, b.ChestDepth + 0.064f), new Vector3(0f, 0f, 8f), Vector3.one));
            spine.RoundBox(new Vector3(0.022f, 0.035f, 0.003f), 0.004f, S(new Color(0.6f, 0.62f, 0.66f)), MeshKit.TRS(new Vector3(0.022f, 0.305f, b.ChestDepth + 0.068f), new Vector3(0f, 0f, -10f), Vector3.one));

            // Pauldrons: oversized layered LEFT with rivets and three glowing claw marks, smaller right.
            var armL = K("UpperArmL");
            armL.Use(Surf.Metal);
            for (int layer = 0; layer < 3; layer++)
                armL.Ellipsoid(new Vector3(b.Arm * 2.3f - layer * 0.012f, 0.05f, b.Arm * 2.1f - layer * 0.01f), 6, 14, layer == 1 ? darkSteel : steel,
                    MeshKit.TRS(new Vector3(-0.01f, 0.03f - layer * 0.045f, 0f), new Vector3(0f, 0f, -18f - layer * 6f), Vector3.one), q => new Vector3(q.x, Mathf.Max(q.y, -0.01f), q.z));
            if (Hi)
                for (int r = 0; r < 7; r++)
                {
                    float a = (r / 6f - 0.5f) * 2.4f;
                    var at = new Vector3(-0.01f + Mathf.Sin(a - Mathf.PI * 0.5f) * b.Arm * 2.15f, 0.03f + 0.012f, Mathf.Cos(a - Mathf.PI * 0.5f) * b.Arm * 1.95f);
                    Rivet(armL, at, new Vector3(at.x, 0.6f, at.z).normalized, 0.0035f, rivet, MeshKit.TRS(Vector3.zero, new Vector3(0f, 0f, -18f), Vector3.one));
                }
            if (!Lo)
            {
                armL.Use(Surf.Circuit, Weave.Plain, 0f);
                for (int c = -1; c <= 1; c++)
                {
                    var p0 = new Vector3(-b.Arm * 2.05f, 0.03f + c * 0.012f + 0.016f, -0.025f + c * 0.012f);
                    var p1 = new Vector3(-b.Arm * 2.0f, 0.0f + c * 0.012f - 0.01f, 0.025f + c * 0.012f);
                    armL.Band(new[] { p0, (p0 + p1) * 0.5f + new Vector3(-0.004f, 0f, 0f), p1 }, new[] { 0.0015f, 0.005f, 0.0015f }, 0.0015f, Vector3.up, _ => amber,
                        MeshKit.TRS(Vector3.zero, new Vector3(0f, 0f, -18f), Vector3.one));
                }
                armL.Outline = 1f;
            }
            K("UpperArmR").Use(Surf.Metal).Ellipsoid(new Vector3(b.Arm * 1.7f, 0.04f, b.Arm * 1.6f), 6, 14, steel, MeshKit.TRS(new Vector3(0.008f, 0.03f, 0f), new Vector3(0f, 0f, 15f), Vector3.one),
                q => new Vector3(q.x, Mathf.Max(q.y, -0.01f), q.z));

            // Mechanical LEFT arm: carbon-fibre core, steel armour segments, hydraulic pistons, cables, glowing amber joints.
            armL.Use(Surf.Carbon).Surface(new[] { new Ring(-0.29f, b.Arm * 0.8f, b.Arm * 0.8f), new Ring(-0.05f, b.Arm * 0.95f, b.Arm * 0.95f) }, 12, S(new Color(0.12f, 0.12f, 0.14f)), id, Step, null, true, true);
            armL.Use(Surf.Metal);
            for (int s = 0; s < 3; s++)
                armL.Surface(new[] { new Ring(-0.27f + s * 0.08f, b.Arm * 1.02f, b.Arm * 1.02f), new Ring(-0.215f + s * 0.08f, b.Arm * 1.08f, b.Arm * 1.06f) }, 14,
                    (y, a) => Mathf.Abs(a) < 30f ? darkSteel : metal, id, 0f, null, true, true);
            var foreL = K("ForearmL");
            foreL.Use(Surf.Carbon).Surface(new[]
            {
                new Ring(-0.26f, b.Forearm * 0.7f, b.Forearm * 0.68f), new Ring(-0.16f, b.Forearm * 0.85f, b.Forearm * 0.8f),
                new Ring(-0.05f, b.Forearm * 1.0f, b.Forearm * 0.95f), new Ring(0.02f, b.Forearm * 0.9f, b.Forearm * 0.9f),
            }, 12, S(new Color(0.12f, 0.12f, 0.14f)), id, Step, null, true, true);
            var foreRings = ForearmRings(b);
            foreL.Use(Surf.Metal);
            for (int s = 0; s < 3; s++)
                foreL.RoundBox(new Vector3(0.036f, 0.1f, 0.014f), 0.005f, s == 1 ? darkSteel : metal, OnSurface(foreRings, -0.13f, -60f - s * 60f, 0.006f, Matrix4x4.identity));
            if (!Lo)
                for (int s = -1; s <= 1; s += 2)
                {
                    // Piston: a steel cylinder with a polished rod, mounted on the inner forearm.
                    float x = s * b.Forearm * 0.55f, z = b.Forearm * 0.75f;
                    foreL.Use(Surf.Metal).Surface(new[] { new Ring(-0.2f, 0.0075f, 0.0075f), new Ring(-0.1f, 0.0075f, 0.0075f) }, 10, darkSteel,
                        MeshKit.At(new Vector3(x, 0f, z)), 0f, null, true, true);
                    foreL.Use(Surf.Gold).Surface(new[] { new Ring(-0.1f, 0.0038f, 0.0038f), new Ring(-0.02f, 0.0038f, 0.0038f) }, 8, S(new Color(0.85f, 0.85f, 0.88f)),
                        MeshKit.At(new Vector3(x, 0f, z)), 0f, null, false, true);
                    foreL.Use(Surf.Rubber).Tube(new[] { new Vector3(x * 1.4f, -0.02f, -b.Forearm * 0.6f), new Vector3(x * 1.6f, -0.1f, -b.Forearm * 0.9f),
                        new Vector3(x * 1.3f, -0.2f, -b.Forearm * 0.75f) }, 0.004f, S(new Color(0.08f, 0.08f, 0.09f)), id, 6);
                }
            foreL.Use(Surf.Circuit, Weave.Plain, 0f);
            foreL.Torus(b.Forearm * 0.95f, b.Forearm * 0.95f, 0.01f, 12, 6, amber, MeshKit.At(new Vector3(0f, 0.01f, 0f)));
            foreL.Torus(b.Forearm * 0.78f, b.Forearm * 0.75f, 0.008f, 12, 6, amber, MeshKit.At(new Vector3(0f, -0.25f, 0f)));
            foreL.Box(new Vector3(0.01f, 0.12f, 0.04f), amber, MeshKit.At(new Vector3(-b.Forearm * 1.08f, -0.12f, 0f)));
            foreL.Outline = 1f;
            var handL = K("HandL");
            handL.Use(Surf.Metal).RoundBox(new Vector3(0.068f, 0.05f, 0.012f), 0.005f, metal, MeshKit.At(new Vector3(0f, -0.05f, 0.016f)));
            handL.Use(Surf.Circuit, Weave.Plain, 0f);
            for (int f = 0; f < 4; f++)
                handL.Disc(0.005f, 0.005f, 6, amber, MeshKit.TRS(new Vector3((f - 1.5f) * 0.018f, -0.085f, 0.03f), Vector3.zero, Vector3.one));
            handL.Outline = 1f;

            // Heavy leather belt with pouches (flaps, snaps), a steel buckle; knee armour; thigh pockets.
            var pelvis = K("Pelvis");
            var belt = new[] { new Ring(0.008f, b.Waist + 0.02f, b.WaistDepth + 0.02f), new Ring(0.052f, b.Waist + 0.02f, b.WaistDepth + 0.02f) };
            pelvis.Use(Surf.Leather).Surface(belt, 24, S(o.Belt), id, 0f, null, true, true);
            RingStitches(pelvis, belt, 0.013f, 0.0006f, S(new Color(0.65f, 0.55f, 0.4f)), id);
            RingStitches(pelvis, belt, 0.047f, 0.0006f, S(new Color(0.65f, 0.55f, 0.4f)), id);
            pelvis.Use(Surf.Metal).RoundBox(new Vector3(0.06f, 0.048f, 0.012f), 0.004f, metal, MeshKit.At(new Vector3(0f, 0.03f, b.WaistDepth + 0.026f)));
            for (int i = 0; i < 4; i++)
            {
                float a = (i - 1.5f) * 0.38f + Mathf.PI;
                var pm = MeshKit.TRS(new Vector3(Mathf.Sin(a) * (b.Waist + 0.04f), 0.01f, Mathf.Cos(a) * (b.WaistDepth + 0.04f)), new Vector3(0f, a * Mathf.Rad2Deg, 0f), Vector3.one);
                pelvis.Use(Surf.Leather).RoundBox(new Vector3(0.05f, 0.06f, 0.035f), 0.006f, S(new Color(0.24f, 0.2f, 0.16f)), pm);
                pelvis.RoundBox(new Vector3(0.054f, 0.02f, 0.038f), 0.005f, S(new Color(0.2f, 0.16f, 0.12f)), pm * MeshKit.At(new Vector3(0f, 0.025f, 0f)));
                if (!Lo)
                    pelvis.Use(Surf.Gold, Weave.Plain, 0f).Disc(0.004f, 0.004f, 8, brass, pm * MeshKit.At(new Vector3(0f, 0.02f, 0.0195f)));
                pelvis.Outline = 1f;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                var shin = K("Shin" + sfx);
                shin.Use(Surf.Metal).Ellipsoid(new Vector3(b.Calf * 1.25f, 0.065f, 0.04f), 6, 12, steel, MeshKit.At(new Vector3(0f, -0.01f, b.Calf * 0.85f)),
                    q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.01f)));
                if (Hi)
                    for (int r = -1; r <= 1; r += 2)
                        Rivet(shin, new Vector3(r * b.Calf * 0.95f, -0.01f, b.Calf * 0.85f + 0.02f), new Vector3(r * 0.6f, 0f, 1f).normalized, 0.0035f, rivet, id);
                var pocket = MeshKit.At(new Vector3(side * (b.Thigh + 0.01f), -0.22f, 0f));
                K("Thigh" + sfx).Use(Surf.Fabric, Weave.Ripstop).RoundBox(new Vector3(0.035f, 0.11f, 0.09f), 0.008f, S(Mul(o.Pants, 0.85f)), pocket);
                K("Thigh" + sfx).RoundBox(new Vector3(0.038f, 0.03f, 0.094f), 0.006f, S(Mul(o.Pants, 0.75f)), pocket * MeshKit.At(new Vector3(0f, 0.045f, 0f)));
            }
            // Battle scar on the right cheek.
            K(CharacterBodyNames.Head).Use(Surf.Skin, Weave.Plain, 0f).Band(new[]
            {
                new Vector3(0.045f, HeadCentreY - 0.012f, FaceZ(0.045f, -0.012f) + 0.004f), new Vector3(0.062f, HeadCentreY - 0.04f, FaceZ(0.062f, -0.04f) + 0.004f),
            }, new[] { 0.004f, 0.003f }, 0.002f, Vector3.right, _ => S(new Color(0.75f, 0.52f, 0.48f)), Matrix4x4.identity);
            K(CharacterBodyNames.Head).Use(Surf.Skin);
        }
    }

    /// <summary>Joint names shared by the styler, the models and the animator.</summary>
    public static class CharacterBodyNames
    {
        public const string Head = "HeadJoint";
        public const string Eyes = "Eyes";
    }
}
