using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>A built skinned character: one mesh per detail level (all on the same skeleton) and the painted face.</summary>
    public sealed class SkinnedCharacterMeshes
    {
        public NyxSkeleton Skeleton;
        public Mesh[] Lods;
        public FaceStyle Face;
        /// <summary>Head centre in bind space (for decals, framing and the facial animator).</summary>
        public Vector3 HeadCentre;
    }

    /// <summary>
    /// KAI, THE ABYSS WALKER: the first character of the skinned pipeline (section 40, phase B). A continuous skinned mesh
    /// on the shared humanoid skeleton, built in the bind T-pose and weighted with smooth joint blends:
    ///  * a sculpted male anime head (V jaw, cheekbones, nose bridge and tip, brow ridge, eye sockets, ears) with smooth
    ///    face shading, carrying painted face decals (eyes, brows, nose and mouth, switchable expressions);
    ///  * medium-length layered black hair with flowing asymmetric bangs, side locks and back layers (violet sheen),
    ///    weighted to helper bones so it sways;
    ///  * a long black twill coat with a purple lining, open at the front, split at the back, a high collar and glowing
    ///    violet piping; the skirt panels follow helper bones (secondary motion);
    ///  * fitted combat armour (lacquered chest plates with glowing seams, segmented abdominal plates), a belt with a
    ///    rune buckle, dark tactical trousers with knee guards, armoured boots with shin plates and buckles, and leather
    ///    gloves with five articulated fingers and knuckle plates.
    /// PLACEHOLDER-grade procedural art (no artist-made assets exist yet); the pipeline accepts imported models later.
    /// </summary>
    public static class KaiModel
    {
        public const int LodCount = 3;
        public static readonly float[] LodResolution = { 1f, 0.6f, 0.36f };

        public static readonly Vector3 HeadRadii = new Vector3(0.087f, 0.112f, 0.098f);

        // Palette.
        private static readonly Color CoatBlack = new Color(0.075f, 0.068f, 0.1f);
        private static readonly Color Lining = new Color(0.36f, 0.12f, 0.62f);
        private static readonly Color Armor = new Color(0.12f, 0.115f, 0.15f);
        private static readonly Color ArmorDark = new Color(0.07f, 0.065f, 0.09f);
        private static readonly Color Shirt = new Color(0.06f, 0.06f, 0.08f);
        private static readonly Color Trousers = new Color(0.075f, 0.072f, 0.095f);
        private static readonly Color Boots = new Color(0.06f, 0.055f, 0.07f);
        private static readonly Color Gloves = new Color(0.12f, 0.11f, 0.14f);
        private static readonly Color Steel = new Color(0.5f, 0.5f, 0.58f);
        private static readonly Color Violet = new Color(0.62f, 0.25f, 1f);
        private static readonly Color Skin = new Color(0.98f, 0.86f, 0.78f);
        private static readonly Color HairRoot = new Color(0.035f, 0.03f, 0.05f);
        private static readonly Color HairTip = new Color(0.13f, 0.08f, 0.24f);
        private static readonly Color HairShine = new Color(0.2f, 0.18f, 0.3f);
        private static readonly Color Thread = new Color(0.32f, 0.2f, 0.5f);

        public static FaceStyle Face => new FaceStyle
        {
            Iris = new Color(0.55f, 0.28f, 0.95f), Lash = new Color(0.07f, 0.04f, 0.1f), Brow = new Color(0.08f, 0.06f, 0.12f),
            Lip = new Color(0.78f, 0.42f, 0.42f), SkinShadow = new Color(0.78f, 0.5f, 0.48f), LidHeight = 0.19f, OuterLift = 0.04f, BrowThickness = 1.1f,
        };

        public static SkinnedCharacterMeshes Build()
        {
            var skeleton = NyxSkeleton.Build(RigProportions.Lean);
            var result = new SkinnedCharacterMeshes { Skeleton = skeleton, Face = Face, Lods = new Mesh[LodCount] };
            result.HeadCentre = HeadCentreOf(skeleton);
            for (int lod = 0; lod < LodCount; lod++)
                result.Lods[lod] = BuildLod(skeleton, lod);
            return result;
        }

        public static Vector3 HeadCentreOf(NyxSkeleton s) => s["Head"].Head + new Vector3(0f, 0.105f, 0.008f);

        private static Color S(Color c) => MeshKit.Solid(c);
        private static Color G(Color c, float glow) => MeshKit.Glow(c, glow);
        private static Matrix4x4 Along(Vector3 from, Vector3 dir) => Matrix4x4.TRS(from, Quaternion.FromToRotation(Vector3.up, dir.normalized), Vector3.one);

        // ------------------------------------------------------------------ the head

        /// <summary>Male anime skull sculpt on an ellipsoid point (relative to the head centre).</summary>
        public static Vector3 Skull(Vector3 q)
        {
            var R = HeadRadii;
            float y = q.y / R.y;
            if (y < 0f)
            {
                float k = Mathf.Pow(-y, 1.5f);
                q.x *= Mathf.Lerp(1f, 0.52f, k);
                if (q.z > 0f)
                    q.z *= Mathf.Lerp(1f, 0.94f, k);
            }
            if (q.z < 0f)
                q.z *= 1.07f;
            float fz = R.z * 0.62f;
            if (q.z > fz && y > -0.55f && y < 0.5f)
                q.z = fz + (q.z - fz) * 0.72f;
            if (q.z > 0f)
            {
                q.x *= 1f + 0.035f * Mathf.Exp(-Mathf.Pow((y + 0.05f) / 0.22f, 2f));
                // Nose: a bridge that grows into the tip.
                float bridge = y < -0.32f ? Mathf.Exp(-Mathf.Pow((y + 0.32f) / 0.06f, 2f)) : Mathf.Lerp(0.12f, 1f, Mathf.InverseLerp(0.06f, -0.32f, y));
                q.z += 0.011f * Mathf.Exp(-Mathf.Pow(q.x / 0.0105f, 2f)) * bridge;
                // Brow ridge, eye sockets.
                q.z += 0.003f * Mathf.Exp(-Mathf.Pow((q.y - 0.024f) / 0.012f, 2f)) * Mathf.Exp(-Mathf.Pow(q.x / 0.06f, 2f));
                for (int side = -1; side <= 1; side += 2)
                    q.z -= 0.004f * Mathf.Exp(-Mathf.Pow((q.x - side * 0.035f) / 0.02f, 2f) - Mathf.Pow((q.y + 0.006f) / 0.016f, 2f));
            }
            return q;
        }

        /// <summary>Depth of the sculpted face at (x, y) relative to the head centre (for decals).</summary>
        public static float FaceZ(float x, float y)
        {
            var R = HeadRadii;
            float xi = x;
            var q = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                float d = 1f - (xi / R.x) * (xi / R.x) - (y / R.y) * (y / R.y);
                q = Skull(new Vector3(xi, y, Mathf.Sqrt(Mathf.Max(0f, d)) * R.z));
                xi += x - q.x;
            }
            return q.z;
        }

        private static void Head(MeshKit k, NyxSkeleton s, Vector3 hc)
        {
            var R = HeadRadii;
            k.Skinning = p => s.Chain(p, 0.035f, "UpperChest", "Neck", "Head");
            k.Use(Surf.Skin).Surface(new[] { new Ring(1.44f, 0.058f, 0.056f, 0f, -0.012f), new Ring(1.53f, 0.052f, 0.05f, 0f, -0.01f), new Ring(1.64f, 0.05f, 0.05f, 0f, -0.01f) },
                14, S(Skin), Matrix4x4.identity, 0.03f, null, false, true);

            k.Skinning = p => s.Rigid("Head");
            int start = k.VertexCount;
            // Skin shading painted into the vertices: a soft blush and a shadow under the jaw.
            k.Ellipsoid(R, 16, 24, d =>
            {
                var c = Skin;
                for (int side = -1; side <= 1; side += 2)
                {
                    float cheek = Vector3.Dot(d, new Vector3(side * 0.55f, -0.32f, 0.77f).normalized);
                    c = Color.Lerp(c, new Color(1f, 0.72f, 0.68f), Mathf.InverseLerp(0.94f, 0.99f, cheek) * 0.22f);
                }
                if (d.y < -0.55f)
                    c *= Mathf.Lerp(1f, 0.86f, Mathf.InverseLerp(-0.55f, -0.9f, d.y));
                return S(c);
            }, Matrix4x4.Translate(hc), Skull);
            // Anime face shading: normals mostly from the smooth ellipsoid, so the face reads clean (no blotchy nose shadows).
            k.OverrideNormals(start, (p, n) =>
            {
                var q = p - hc;
                var smooth = new Vector3(q.x / (R.x * R.x), q.y / (R.y * R.y), q.z / (R.z * R.z)).normalized;
                return Vector3.Slerp(n, smooth, q.z > 0f ? 0.75f : 0.35f);
            });
            // Ears with an inner fold.
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = Matrix4x4.TRS(hc + new Vector3(side * 0.086f, -0.01f, -0.008f), Quaternion.Euler(0f, side * 18f, 0f), Vector3.one);
                k.Ellipsoid(new Vector3(0.011f, 0.025f, 0.017f), 6, 10, S(Skin * 0.96f), ear);
                k.Use(Surf.Skin, Weave.Plain, 0f).Ellipsoid(new Vector3(0.005f, 0.016f, 0.01f), 4, 8, S(Skin * 0.78f), ear * Matrix4x4.Translate(new Vector3(side * 0.007f, 0f, 0.002f)));
                k.Use(Surf.Skin);
            }
        }

        /// <summary>A painted face decal: a patch fitted to the face surface (submesh 1, WarOfNyx/NyxFace).</summary>
        private static void Decal(MeshKit k, Vector3 hc, int part, Vector2 centre, Vector2 size, float lift)
        {
            var R = HeadRadii;
            const int nx = 6, ny = 4;
            var index = new int[nx + 1, ny + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= ny; j++)
            {
                float u = i / (float)nx, v = j / (float)ny;
                float x = centre.x + (u - 0.5f) * size.x, y = centre.y + (v - 0.5f) * size.y;
                float z = FaceZ(x, y) + lift;
                var normal = new Vector3(x / (R.x * R.x), y / (R.y * R.y), z / (R.z * R.z)).normalized;
                index[i, j] = k.AddVertex(hc + new Vector3(x, y, z), normal, Color.white, new Vector2(u, v), new Vector4(part, 1f, 0f, 0f));
            }
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                k.AddTriangle(index[i, j], index[i + 1, j], index[i + 1, j + 1]);
                k.AddTriangle(index[i, j], index[i + 1, j + 1], index[i, j + 1]);
            }
        }

        /// <summary>Face part ids read by NyxFace (uv2.x): eyes, brows (character's right, then left, mirrored), mouth and nose.</summary>
        public const int PartEyeRight = 0, PartEyeLeft = 1, PartBrowRight = 2, PartBrowLeft = 3, PartMouth = 4;

        private static void FaceDecals(MeshKit k, NyxSkeleton s, Vector3 hc)
        {
            k.UseSubmesh(1);
            k.Skinning = p => s.Rigid("Head");
            Decal(k, hc, PartEyeRight, new Vector2(-0.036f, -0.006f), new Vector2(0.053f, 0.056f), 0.0016f);
            Decal(k, hc, PartEyeLeft, new Vector2(0.036f, -0.006f), new Vector2(0.053f, 0.056f), 0.0016f);
            Decal(k, hc, PartBrowRight, new Vector2(-0.038f, 0.031f), new Vector2(0.056f, 0.032f), 0.0018f);
            Decal(k, hc, PartBrowLeft, new Vector2(0.038f, 0.031f), new Vector2(0.056f, 0.032f), 0.0018f);
            Decal(k, hc, PartMouth, new Vector2(0f, -0.058f), new Vector2(0.05f, 0.072f), 0.0012f);
            k.UseSubmesh(0);
        }

        // ------------------------------------------------------------------ hair

        private static Vector3 Scalp(Vector3 hc, float theta, float phi, float lift)
        {
            var R = HeadRadii;
            float t = theta * Mathf.Deg2Rad, p = phi * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(p) * Mathf.Sin(t), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Cos(t));
            return hc + new Vector3(d.x * R.x * 1.04f, d.y * R.y, d.z * R.z * (d.z < 0f ? 1.07f : 1f)) * lift;
        }

        private static void Hair(MeshKit k, NyxSkeleton s, Vector3 hc, bool hi, bool lo)
        {
            var R = HeadRadii;
            var root = S(HairRoot);
            var tip = S(HairTip);
            var shine = S(HairShine);
            var random = new System.Random(2026);
            float Rn(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var up = Vector3.up;

            k.Skinning = p =>
            {
                var d = p - hc;
                var w = s.Rigid("Head");
                if (d.z < -0.03f && d.y < 0.03f)
                    w = NyxSkeleton.Mix(w, s.Chain(p, 0.03f, "HairBack1", "HairBack2"), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, -0.08f, d.y)));
                else if (Mathf.Abs(d.x) > 0.07f && d.y < 0.02f)
                    w = NyxSkeleton.Mix(w, s.Rigid(d.x < 0f ? "HairSideL" : "HairSideR"), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, -0.07f, d.y)));
                else if (d.z > 0.05f && d.y > -0.04f)
                    w = NyxSkeleton.Mix(w, s.Rigid("HairFront"), 0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.0f, d.y)));
                return w;
            };
            k.Use(Surf.Hair);

            // Cap: follows the skull, pulled inside over the face so only the hairline shows.
            k.Ellipsoid(R * 1.055f, 12, 20, d =>
            {
                float ring = Mathf.InverseLerp(0.35f, 0.5f, d.y) * (1f - Mathf.InverseLerp(0.55f, 0.7f, d.y));
                return S(Color.Lerp(HairRoot, HairShine, ring * 0.7f));
            }, Matrix4x4.Translate(hc + new Vector3(0f, 0.008f, -0.006f)), q =>
            {
                if (q.z > 0.035f && q.y < 0.05f)
                    q = Vector3.Lerp(q, q * 0.78f, Mathf.InverseLerp(0.035f, 0.075f, q.z));
                if (q.y < -0.03f && q.z > -0.03f)
                    q *= 0.86f;
                return q;
            });

            void Lock(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, Color rootColor, Color tipColor, float flat = 0.34f)
            {
                k.Strand(a, b, c, d, width, flat, rootColor, tipColor, Matrix4x4.identity, 8);
                if (lo)
                    return;
                var side = Vector3.Cross((d - a).normalized, (a - hc).normalized).normalized;
                for (int sgn = 1; sgn <= 1; sgn += 2)
                {
                    var off = side * (width * 0.6f * sgn);
                    k.Strand(a + off * 0.4f, b + off, c + off * 1.15f, d + off * 1.3f + (d - c) * -0.12f, width * 0.5f, flat, rootColor, tipColor, Matrix4x4.identity, 5);
                }
            }

            // Flowing bangs: asymmetric locks over the forehead, two longer ones falling between the eyes.
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(-0.07f, 0.07f, i / 7f) + Rn(-0.005f, 0.005f);
                var a = Scalp(hc, x * 520f, 24f + Mathf.Abs(x) * 150f, 1.0f);
                bool frame = i == 0 || i == 7; // longer locks framing the face at the temples
                float endY = frame ? -0.01f : 0.036f + Rn(-0.006f, 0.006f) + Mathf.Abs(x) * 0.08f;
                // Parted slightly off-centre: locks sweep away from the part.
                float sweep = (x < -0.012f ? -0.016f : 0.018f) + Rn(-0.004f, 0.004f);
                float ex = Mathf.Clamp(x * 1.08f + sweep, -0.082f, 0.082f);
                var end = hc + new Vector3(ex, endY, FaceZ(Mathf.Clamp(ex, -0.075f, 0.075f), endY) + 0.02f);
                Lock(a, a + Vector3.forward * 0.035f + up * 0.014f, end + up * 0.035f + Vector3.forward * 0.014f, end, Rn(0.026f, 0.032f), root, i % 4 == 1 ? shine : tip);
            }
            // Crown: layered locks swept back with lift.
            for (int i = 0; i < (lo ? 6 : 10); i++)
            {
                float theta = Rn(-40f, 40f) + (i % 2 == 0 ? 180f : 160f);
                var a = Scalp(hc, Rn(-30f, 30f), Rn(4f, 22f), 0.99f);
                var back = Scalp(hc, theta, Rn(40f, 70f), 1.18f);
                Lock(a, a + up * 0.04f, back + up * 0.03f, back + Vector3.back * 0.02f, Rn(0.032f, 0.042f), root, i % 3 == 0 ? shine : tip);
            }
            // Side locks: over the temples to the jaw, partly over the ears.
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 3; j++)
                {
                    var a = Scalp(hc, side * (62f + j * 18f), 46f + j * 6f, 1.0f);
                    var end = hc + new Vector3(side * (0.094f + j * 0.004f), -0.06f - j * 0.022f + Rn(-0.01f, 0.01f), 0.03f - j * 0.035f);
                    Lock(a, a + new Vector3(side * 0.02f, -0.025f, 0.01f), end + up * 0.04f + new Vector3(side * 0.008f, 0f, 0f), end, Rn(0.028f, 0.034f), root, tip);
                }
            // Back layers to the nape, tips flicking out.
            for (int layer = 0; layer < (lo ? 1 : 2); layer++)
                for (int i = 0; i < (layer == 0 ? 9 : 7); i++)
                {
                    float theta = Mathf.Lerp(110f, 250f, i / 8f) + layer * 8f;
                    var a = Scalp(hc, theta, Mathf.Lerp(28f, 46f, layer) + Rn(-4f, 4f), 1.0f + layer * 0.02f);
                    var outward = new Vector3(Mathf.Sin(theta * Mathf.Deg2Rad), 0f, Mathf.Cos(theta * Mathf.Deg2Rad));
                    var mid = hc + outward * (0.118f + layer * 0.008f) + up * (-0.03f);
                    var end = hc + outward * (0.1f + layer * 0.012f) + up * (-0.12f - layer * 0.02f + Rn(-0.012f, 0.012f)) + outward * 0.025f;
                    Lock(a, a + outward * 0.045f, mid, end, Rn(0.03f, 0.038f) * (lo ? 1.4f : 1f), root, layer == 1 && i % 3 == 1 ? shine : tip);
                }
        }

        // ------------------------------------------------------------------ the body and outfit

        private static Ring[] TorsoRings() => new[]
        {
            new Ring(0.92f, 0.165f, 0.12f), new Ring(1.0f, 0.158f, 0.116f), new Ring(1.08f, 0.15f, 0.11f),
            new Ring(1.2f, 0.172f, 0.12f, 0f, 0.008f), new Ring(1.32f, 0.19f, 0.122f, 0f, 0.006f), new Ring(1.41f, 0.2f, 0.112f, 0f, -0.008f),
            new Ring(1.465f, 0.15f, 0.095f, 0f, -0.012f), new Ring(1.5f, 0.07f, 0.068f, 0f, -0.012f),
        };

        private static BoneWeight TorsoWeight(NyxSkeleton s, Vector3 p)
        {
            var w = s.Chain(p, 0.06f, "Hips", "Spine", "Chest", "UpperChest", "Neck");
            float ax = Mathf.Abs(p.x);
            // Shoulders follow the arms.
            float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.2f, ax)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 1.42f, p.y));
            if (shoulder > 0f)
            {
                string L = p.x < 0f ? "Left" : "Right";
                w = NyxSkeleton.Mix(w, s.Chain(p, 0.05f, L + "Shoulder", L + "UpperArm", L + "LowerArm"), shoulder * 0.9f);
            }
            // Hips follow the legs a little.
            float hip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 0.86f, p.y)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.1f, ax));
            if (hip > 0f)
                w = NyxSkeleton.Mix(w, s.Rigid(p.x < 0f ? "LeftUpperLeg" : "RightUpperLeg"), hip * 0.6f);
            return w;
        }

        private static void Body(MeshKit k, NyxSkeleton s, bool hi, bool lo)
        {
            var torso = TorsoRings();
            // Fitted combat shirt (seen in the open coat front).
            k.Skinning = p => TorsoWeight(s, p);
            k.Use(Surf.Fabric, Weave.Hex).Surface(torso, 22, S(Shirt), Matrix4x4.identity, 0.03f, null, false, false);
            // Chest armour: two lacquered pectoral plates and segmented abdominal plates with glowing seams.
            for (int side = -1; side <= 1; side += 2)
            {
                var plate = Matrix4x4.TRS(new Vector3(side * 0.075f, 1.3f, 0.128f), Quaternion.Euler(-8f, side * 16f, 0f), Vector3.one);
                k.Use(Surf.Lacquer).RoundBox(new Vector3(0.12f, 0.11f, 0.022f), 0.012f, S(Armor), plate);
                if (!lo)
                    k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.1f, 0.004f, 0.004f), G(Violet, 0.9f), plate * Matrix4x4.Translate(new Vector3(0f, -0.045f, 0.012f)));
                k.Outline = 1f;
            }
            for (int i = 0; i < 3; i++)
            {
                var band = Matrix4x4.TRS(new Vector3(0f, 1.17f - i * 0.048f, 0.118f - i * 0.004f), Quaternion.Euler(-4f, 0f, 0f), Vector3.one);
                k.Use(Surf.Lacquer).RoundBox(new Vector3(0.15f, 0.042f, 0.018f), 0.008f, S(i % 2 == 0 ? Armor : ArmorDark), band);
            }
            if (!lo)
                k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.16f, 0.004f), G(Violet, 0.9f), Matrix4x4.Translate(new Vector3(0f, 1.15f, 0.13f)));
            k.Outline = 1f;

            // Belt with a rune buckle.
            k.Skinning = p => s.Rigid("Hips");
            k.Use(Surf.Leather).Torus(0.158f, 0.118f, 0.016f, 24, 6, S(new Color(0.09f, 0.075f, 0.08f)), Matrix4x4.Translate(new Vector3(0f, 0.985f, 0f)));
            var buckle = Matrix4x4.Translate(new Vector3(0f, 0.985f, 0.132f));
            k.Use(Surf.Metal).RoundBox(new Vector3(0.05f, 0.04f, 0.012f), 0.004f, S(Steel), buckle);
            k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.011f, 0.011f, 0.0022f, 14, 4, G(Violet, 0.95f), buckle * Matrix4x4.TRS(new Vector3(0f, 0f, 0.0065f), Quaternion.Euler(90f, 0f, 0f), Vector3.one));
            k.Outline = 1f;

            // Trousers: pelvis and legs, folds at the knees, armoured knee guards.
            k.Skinning = p => p.y > 0.9f ? TorsoWeight(s, p) : NyxSkeleton.Mix(s.Rigid("Hips"), s.Rigid(p.x < 0f ? "LeftUpperLeg" : "RightUpperLeg"),
                Mathf.InverseLerp(0.9f, 0.8f, p.y) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.06f, Mathf.Abs(p.x))));
            k.Use(Surf.Fabric, Weave.Twill).Surface(new[] { new Ring(0.8f, 0.14f, 0.1f), new Ring(0.9f, 0.155f, 0.112f), new Ring(1.0f, 0.15f, 0.11f) }, 20, S(Trousers),
                Matrix4x4.identity, 0.03f, null, true, false);
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                float x = side * 0.097f;
                var leg = new[]
                {
                    new Ring(0.1f, 0.047f, 0.05f, x * 1.06f), new Ring(0.2f, 0.05f, 0.055f, x * 1.05f, -0.004f), new Ring(0.36f, 0.06f, 0.064f, x * 1.04f, -0.006f),
                    new Ring(0.5f, 0.058f, 0.062f, x * 1.03f, 0.004f), new Ring(0.62f, 0.067f, 0.072f, x * 1.02f), new Ring(0.8f, 0.082f, 0.088f, x),
                    new Ring(0.93f, 0.09f, 0.096f, x * 0.97f),
                };
                k.Skinning = p => s.Chain(p, 0.07f, "Hips", L + "UpperLeg", L + "LowerLeg", L + "Foot");
                k.Use(Surf.Fabric, Weave.Twill).Surface(leg, 16, S(Trousers), Matrix4x4.identity, 0.035f,
                    (y, a) => -0.0025f * Mathf.Max(0f, Mathf.Sin((y - 0.42f) / 0.16f * Mathf.PI * 3f + Mathf.Sin(a * Mathf.Deg2Rad * 2f))) * (Mathf.Abs(y - 0.5f) < 0.08f ? 1f : 0f));
                var knee = Matrix4x4.TRS(new Vector3(x * 1.03f, 0.505f, 0.058f), Quaternion.Euler(-6f, 0f, 0f), Vector3.one);
                k.Use(Surf.Lacquer).Ellipsoid(new Vector3(0.05f, 0.06f, 0.022f), 6, 12, S(Armor), knee, q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.004f)));
                if (!lo)
                    k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.05f, 0.003f), G(Violet, 0.9f), knee * Matrix4x4.Translate(new Vector3(0f, 0f, 0.021f)));
                k.Outline = 1f;
                Boot(k, s, side, L, x, hi, lo);
            }
        }

        private static void Boot(MeshKit k, NyxSkeleton s, int side, string L, float x, bool hi, bool lo)
        {
            k.Skinning = p => s.Chain(p, 0.04f, L + "LowerLeg", L + "Foot", L + "Toes");
            float bx = x * 1.08f;
            k.Use(Surf.Leather).Surface(new[] { new Ring(0.06f, 0.052f, 0.056f, bx), new Ring(0.18f, 0.055f, 0.058f, bx, -0.004f), new Ring(0.32f, 0.062f, 0.066f, bx, -0.006f),
                new Ring(0.34f, 0.066f, 0.07f, bx, -0.006f) }, 16, S(Boots), Matrix4x4.identity, 0.03f, null, false, true);
            // Foot: heel to toe along +Z.
            var foot = Along(new Vector3(bx, 0.055f, -0.085f), Vector3.forward);
            k.Surface(new[] { new Ring(0f, 0.044f, 0.04f), new Ring(0.04f, 0.05f, 0.05f), new Ring(0.13f, 0.053f, 0.047f, 0f, 0.004f),
                new Ring(0.22f, 0.048f, 0.034f, 0f, 0.016f), new Ring(0.28f, 0.03f, 0.02f, 0f, 0.024f) }, 16, S(Boots), foot, 0.03f, null, true, true);
            // Sole, heel, armoured shin plate with a glowing line, two buckled straps.
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.11f, 0.024f, 0.29f), 0.01f, S(new Color(0.035f, 0.03f, 0.04f)), Matrix4x4.Translate(new Vector3(bx, 0.012f, 0.055f)));
            var shin = Matrix4x4.TRS(new Vector3(bx, 0.24f, 0.058f), Quaternion.Euler(-3f, 0f, 0f), Vector3.one);
            k.Use(Surf.Metal).RoundBox(new Vector3(0.07f, 0.17f, 0.016f), 0.008f, S(Armor), shin);
            if (!lo)
            {
                k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.13f, 0.003f), G(Violet, 0.9f), shin * Matrix4x4.Translate(new Vector3(0f, 0f, 0.009f)));
                k.Outline = 1f;
                for (int i = 0; i < 2; i++)
                {
                    k.Use(Surf.Leather).Torus(0.06f, 0.064f, 0.006f, 16, 4, S(new Color(0.1f, 0.08f, 0.09f)), Matrix4x4.Translate(new Vector3(bx, 0.12f + i * 0.1f, -0.004f)));
                    k.Use(Surf.Metal).RoundBox(new Vector3(0.022f, 0.016f, 0.008f), 0.002f, S(Steel), Matrix4x4.Translate(new Vector3(bx + side * 0.062f, 0.12f + i * 0.1f, 0f)));
                }
            }
        }

        private static void Coat(MeshKit k, NyxSkeleton s, bool hi, bool lo)
        {
            var torso = TorsoRings();
            var coat = new Ring[torso.Length];
            for (int i = 0; i < torso.Length; i++)
                coat[i] = new Ring(torso[i].Y, torso[i].Rx + 0.022f, torso[i].Rz + 0.022f, torso[i].X, torso[i].Z);
            coat[coat.Length - 1] = new Ring(1.5f, 0.1f, 0.095f, 0f, -0.012f);
            const float open = 26f;
            // Upper coat: outer black twill, purple lining inside, open at the front.
            k.Skinning = p => TorsoWeight(s, p);
            k.Use(Surf.Fabric, Weave.Twill).Surface(coat, 30, S(CoatBlack), Matrix4x4.identity, 0.03f,
                (y, a) => -0.002f * Mathf.Max(0f, Mathf.Sin((y - 1.0f) / 0.12f * Mathf.PI * 2f)) * (y < 1.12f ? 1f : 0f), false, false, 28f, null, open, 360f - open);
            var lining = new Ring[coat.Length];
            for (int i = 0; i < coat.Length; i++)
                lining[i] = new Ring(coat[i].Y, coat[i].Rx - 0.005f, coat[i].Rz - 0.005f, coat[i].X, coat[i].Z);
            k.Use(Surf.Fabric, Weave.Plain).Surface(lining, 26, S(Lining), Matrix4x4.identity, 0.03f, null, false, false, 28f, null, open, 360f - open, inward: true);

            // Skirt: two halves split at the back, flaring to the calves, following the coat-panel bones.
            var skirt = new[]
            {
                new Ring(0.36f, 0.285f, 0.235f), new Ring(0.46f, 0.272f, 0.222f), new Ring(0.64f, 0.245f, 0.2f), new Ring(0.8f, 0.215f, 0.17f),
                new Ring(0.92f, 0.19f, 0.145f),
            };
            k.Skinning = p => SkirtWeight(s, p);
            foreach (var (from, to) in new[] { (open, 176f), (184f, 360f - open) })
            {
                k.Use(Surf.Fabric, Weave.Twill).Surface(skirt, 32, S(CoatBlack), Matrix4x4.identity, 0.03f,
                    (y, a) => 0.006f * Mathf.Sin(a * Mathf.Deg2Rad * 7f) * Mathf.InverseLerp(0.85f, 0.4f, y), false, false, 28f, null, from, to);
                var inner = new Ring[skirt.Length];
                for (int i = 0; i < skirt.Length; i++)
                    inner[i] = new Ring(skirt[i].Y, skirt[i].Rx - 0.005f, skirt[i].Rz - 0.005f);
                k.Use(Surf.Fabric, Weave.Plain).Surface(inner, 26, S(Lining), Matrix4x4.identity, 0.03f,
                    (y, a) => 0.006f * Mathf.Sin(a * Mathf.Deg2Rad * 7f) * Mathf.InverseLerp(0.85f, 0.4f, y), false, false, 28f, null, from, to, inward: true);
            }
            // Glowing violet piping along the front edges and the hem.
            if (!lo)
            {
                k.Use(Surf.Circuit, Weave.Plain, 0f);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var edge = new List<Vector3>();
                    for (float y = 0.37f; y <= 1.49f; y += 0.04f)
                    {
                        var rings = y < 0.92f ? skirt : coat;
                        edge.Add(MeshKit.OnRings(rings, y, sgn * open, 0.003f));
                    }
                    k.Skinning = p => p.y < 0.92f ? SkirtWeight(s, p) : TorsoWeight(s, p);
                    k.Tube(edge, 0.0045f, G(Violet, 0.85f), Matrix4x4.identity, 5);
                }
                foreach (var (from, to) in new[] { (open, 176f), (184f, 360f - open) })
                {
                    var hem = new List<Vector3>();
                    for (float a = from; a <= to + 0.1f; a += 6f)
                        hem.Add(MeshKit.OnRings(skirt, 0.365f, a, 0.004f));
                    k.Skinning = p => SkirtWeight(s, p);
                    k.Tube(hem, 0.004f, G(Violet, 0.85f), Matrix4x4.identity, 5);
                }
                k.Outline = 1f;
            }
            // High stand collar, purple inside.
            k.Skinning = p => s.Chain(p, 0.04f, "UpperChest", "Neck");
            var collar = new[] { new Ring(1.47f, 0.105f, 0.1f, 0f, -0.012f), new Ring(1.55f, 0.098f, 0.092f, 0f, -0.012f), new Ring(1.61f, 0.096f, 0.09f, 0f, -0.016f) };
            k.Use(Surf.Fabric, Weave.Twill).Surface(collar, 24, S(CoatBlack), Matrix4x4.identity, 0f, null, false, false, 28f, null, 32f, 328f);
            var collarIn = new Ring[collar.Length];
            for (int i = 0; i < collar.Length; i++)
                collarIn[i] = new Ring(collar[i].Y, collar[i].Rx - 0.006f, collar[i].Rz - 0.006f, 0f, collar[i].Z);
            k.Use(Surf.Fabric, Weave.Plain).Surface(collarIn, 24, S(Lining), Matrix4x4.identity, 0f, null, false, false, 28f, null, 32f, 328f, inward: true);
            // Back emblem: a glowing violet ring with three shadow tendrils.
            if (!lo)
            {
                k.Skinning = p => TorsoWeight(s, p);
                var emblem = Matrix4x4.TRS(MeshKit.OnRings(coat, 1.3f, 180f, 0.002f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.045f, 0.045f, 0.0035f, 26, 4, G(Violet, 0.9f), emblem);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f * Mathf.Deg2Rad;
                    var c = MeshKit.OnRings(coat, 1.3f, 180f, 0.004f);
                    var p0 = c + new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, 0f);
                    var p1 = c + new Vector3(Mathf.Cos(a + 1f) * 0.03f, Mathf.Sin(a + 1f) * 0.03f, 0f);
                    k.Strand(p0, p1, p1 + (p1 - c) * 0.3f, c + new Vector3(Mathf.Cos(a + 1.6f) * 0.042f, Mathf.Sin(a + 1.6f) * 0.042f, 0f), 0.008f, 0.3f,
                        G(Violet, 0.9f), G(Violet, 0.6f), Matrix4x4.identity, 5);
                }
                k.Outline = 1f;
            }
        }

        private static BoneWeight SkirtWeight(NyxSkeleton s, Vector3 p)
        {
            if (p.y > 0.9f)
                return TorsoWeight(s, p);
            float angle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg; // 0 front, 90 right
            string Panel(float a) => a >= 0f ? (a < 90f ? "CoatFrontR" : "CoatBackR") : (a > -90f ? "CoatFrontL" : "CoatBackL");
            BoneWeight Of(string panel) => s.Chain(p, 0.08f, "Hips", panel + "1", panel + "2");
            // Blend across the panel boundaries at the sides (±90°); the front and back splits are real openings.
            float side = Mathf.Abs(Mathf.Abs(angle) - 90f);
            var w = Of(Panel(angle));
            if (side < 25f)
            {
                string other = Panel(angle >= 0f ? (Mathf.Abs(angle) < 90f ? 120f : 60f) : (Mathf.Abs(angle) < 90f ? -120f : -60f));
                w = NyxSkeleton.Mix(w, Of(other), 0.5f - 0.5f * side / 25f);
            }
            float top = Mathf.InverseLerp(0.9f, 0.82f, p.y);
            return NyxSkeleton.Mix(TorsoWeight(s, p), w, Mathf.SmoothStep(0f, 1f, top));
        }

        private static void Arms(MeshKit k, NyxSkeleton s, bool hi, bool lo)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                string L = side < 0 ? "Left" : "Right";
                var dir = new Vector3(side, 0f, 0f);
                var start = s[L + "UpperArm"].Head - dir * 0.045f;
                k.Skinning = p => s.Chain(p, 0.055f, L + "Shoulder", L + "UpperArm", L + "LowerArm", L + "Hand");
                // Coat sleeve over the arm: shoulder, biceps, elbow, forearm, a flared cuff.
                var sleeve = new[]
                {
                    new Ring(0f, 0.074f, 0.074f), new Ring(0.12f, 0.068f, 0.07f), new Ring(0.27f, 0.058f, 0.06f), new Ring(0.36f, 0.06f, 0.062f),
                    new Ring(0.47f, 0.053f, 0.055f), new Ring(0.545f, 0.058f, 0.06f),
                };
                k.Use(Surf.Fabric, Weave.Twill).Surface(sleeve, 18, S(CoatBlack), Along(start, dir), 0.03f,
                    (y, a) => -0.0025f * Mathf.Max(0f, Mathf.Sin((y - 0.2f) / 0.14f * Mathf.PI * 3f + Mathf.Sin(a * Mathf.Deg2Rad * 2f))) * (Mathf.Abs(y - 0.27f) < 0.07f ? 1f : 0f),
                    false, false);
                // Forearm guards: lacquered splints with violet seams over the sleeve (arms read against the coat).
                if (!lo)
                    for (int g = 0; g < 2; g++)
                    {
                        var at = start + dir * (0.39f + g * 0.075f);
                        var guard = Matrix4x4.TRS(at + Vector3.up * 0.052f + Vector3.forward * (g == 0 ? 0.012f : -0.006f), Quaternion.identity, Vector3.one);
                        k.Use(Surf.Lacquer).RoundBox(new Vector3(0.07f, 0.014f, 0.07f), 0.006f, S(new Color(0.17f, 0.16f, 0.21f)), guard);
                        k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.06f, 0.003f, 0.003f), G(Violet, 0.9f), guard * Matrix4x4.Translate(new Vector3(0f, 0.008f, 0.02f)));
                        k.Outline = 1f;
                    }
                var cuff = Along(start + dir * 0.53f, dir);
                k.Use(Surf.Fabric, Weave.Plain).Surface(new[] { new Ring(-0.02f, 0.054f, 0.056f), new Ring(0.02f, 0.054f, 0.056f) }, 18, S(Lining), cuff, 0f, null, false, false,
                    28f, null, 0f, 360f, inward: true);
                if (!lo)
                {
                    k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.059f, 0.061f, 0.0035f, 20, 4, G(Violet, 0.85f), cuff * Matrix4x4.Translate(new Vector3(0f, 0.016f, 0f)));
                    k.Outline = 1f;
                }
                Hand(k, s, side, L, hi, lo);
            }
        }

        private static void Hand(MeshKit k, NyxSkeleton s, int side, string L, bool hi, bool lo)
        {
            var hand = s[L + "Hand"].Head;
            var dir = new Vector3(side, 0f, 0f);
            // Glove: wrist, palm (flattened: thin vertically, wide front-to-back), knuckle plate.
            k.Skinning = p => s.Chain(p, 0.03f, L + "LowerArm", L + "Hand");
            k.Use(Surf.Leather).Surface(new[] { new Ring(-0.05f, 0.03f, 0.036f), new Ring(0f, 0.025f, 0.034f), new Ring(0.05f, 0.023f, 0.044f), new Ring(0.085f, 0.019f, 0.042f) },
                12, S(Gloves), Along(hand, dir), 0.02f, null, false, true);
            if (!lo)
            {
                k.Use(Surf.Metal).RoundBox(new Vector3(0.04f, 0.01f, 0.07f), 0.004f, S(Armor), Matrix4x4.Translate(hand + dir * 0.07f + Vector3.up * 0.021f));
                k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.03f, 0.002f, 0.003f), G(Violet, 0.9f), Matrix4x4.Translate(hand + dir * 0.07f + Vector3.up * 0.0265f));
                k.Outline = 1f;
            }
            // Five fingers: tapering capsules along each finger's bones.
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string f1 = L + finger + "1", f2 = L + finger + "2", f3 = L + finger + "3";
                k.Skinning = p => s.Chain(p, 0.008f, L + "Hand", f1, f2, f3);
                var a = s[f1].Head;
                var b = s[f3].Tail;
                var fdir = (b - a).normalized;
                float length = Vector3.Distance(a, b);
                float r = finger == "Thumb" ? 0.0115f : finger == "Little" ? 0.0082f : 0.0095f;
                k.Use(Surf.Leather).Surface(new[] { new Ring(-0.012f, r * 1.05f, r * 1.1f), new Ring(length * 0.45f, r, r * 1.02f), new Ring(length * 0.85f, r * 0.9f, r * 0.92f),
                    new Ring(length, r * 0.55f, r * 0.6f) }, lo ? 6 : 8, S(Gloves), Along(a, fdir), 0f, null, true, true);
            }
        }

        // ------------------------------------------------------------------ assembly

        private static Mesh BuildLod(NyxSkeleton s, int lod)
        {
            bool hi = lod == 0, lo = lod >= 2;
            var k = new MeshKit { Resolution = LodResolution[lod] };
            var hc = HeadCentreOf(s);
            Body(k, s, hi, lo);
            Coat(k, s, hi, lo);
            Arms(k, s, hi, lo);
            Head(k, s, hc);
            Hair(k, s, hc, hi, lo);
            FaceDecals(k, s, hc);
            k.Skinning = null;
            var mesh = k.ToMesh($"KAI_LOD{lod}");
            mesh.bindposes = s.BindPoses();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
