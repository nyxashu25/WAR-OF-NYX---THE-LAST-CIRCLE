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

    /// <summary>A built character: one mesh per joint ("Pelvis", "Spine", "HeadJoint", "Eyes", limbs, chain segments).</summary>
    public sealed class CharacterModel
    {
        public readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        public readonly List<DangleSpec> Dangles = new List<DangleSpec>();
        public BodyShape Shape;
        public int Vertices;
    }

    /// <summary>
    /// Detailed procedural PLACEHOLDER models for the roster (original designs, see <see cref="CharacterRoster"/>):
    /// sculpted torso and limbs, gloved hands with fingers, an anime face (layered eyes with iris, pupil and
    /// highlights, lashes, brows, nose, mouth, ears), strand-built hair per character, and each character's outfit
    /// and gear. Built once per character and shared by every instance.
    /// </summary>
    public static class CharacterModels
    {
        private static readonly Dictionary<int, CharacterModel> Cache = new Dictionary<int, CharacterModel>();

        public const float HeadCentreY = 0.14f;       // head centre above the head joint
        public const float EyePivotY = 0.134f;        // blink pivot (eye centre height) above the head joint
        private static readonly Vector3 HeadRadii = new Vector3(0.1f, 0.122f, 0.112f);

        public static CharacterModel Get(CharacterLook look)
        {
            if (Cache.TryGetValue(look.Id, out var model) && model.Meshes.Count > 0 && AllMeshesAlive(model))
                return model;
            model = Build(look);
            Cache[look.Id] = model;
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

        private static CharacterModel Build(CharacterLook look)
        {
            var model = new CharacterModel { Shape = BodyShape.For(look.Build) };
            var kits = new Dictionary<string, MeshKit>();
            MeshKit K(string joint)
            {
                if (!kits.TryGetValue(joint, out var kit))
                    kits[joint] = kit = new MeshKit();
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
                var mesh = pair.Value.ToMesh($"{look.Name}_{pair.Key}");
                model.Meshes[pair.Key] = mesh;
                model.Vertices += mesh.vertexCount;
            }
            return model;
        }

        // ------------------------------------------------------------------ outfit colours

        private struct Outfit
        {
            public Color Shirt, Pants, Boots, Soles, Gloves, Belt, Sleeve, Trim;
            public bool BareForearms, Fingerless, WidePants;

            public static Outfit For(CharacterLook l)
            {
                switch (l.Id)
                {
                    case 0: // KAI: black uniform, violet haori sleeves, bandaged forearms, hakama
                        return new Outfit { Shirt = new Color(0.07f, 0.06f, 0.1f), Pants = new Color(0.1f, 0.08f, 0.14f), Boots = new Color(0.08f, 0.07f, 0.1f),
                            Soles = new Color(0.05f, 0.04f, 0.06f), Gloves = new Color(0.08f, 0.07f, 0.1f), Belt = l.Secondary, Sleeve = l.Primary * 1.6f,
                            Trim = l.Accent, BareForearms = true, Fingerless = true, WidePants = true };
                    case 1: // AKIRA: dark grey shirt and cargo trousers, black jacket
                        return new Outfit { Shirt = new Color(0.22f, 0.21f, 0.24f), Pants = new Color(0.2f, 0.2f, 0.22f), Boots = new Color(0.12f, 0.1f, 0.1f),
                            Soles = new Color(0.06f, 0.05f, 0.05f), Gloves = new Color(0.1f, 0.09f, 0.1f), Belt = new Color(0.12f, 0.1f, 0.09f),
                            Sleeve = new Color(0.09f, 0.08f, 0.1f), Trim = l.Secondary, Fingerless = true };
                    case 2: // YUNA: black bodysuit, white jacket, white boots with glowing soles
                        return new Outfit { Shirt = new Color(0.1f, 0.1f, 0.14f), Pants = new Color(0.12f, 0.12f, 0.17f), Boots = new Color(0.9f, 0.92f, 0.97f),
                            Soles = l.Accent, Gloves = new Color(0.9f, 0.92f, 0.97f), Belt = new Color(0.15f, 0.16f, 0.22f), Sleeve = l.Primary, Trim = l.Accent };
                    default: // REN: grey fatigues, armour
                        return new Outfit { Shirt = new Color(0.2f, 0.21f, 0.2f), Pants = new Color(0.26f, 0.27f, 0.24f), Boots = new Color(0.14f, 0.12f, 0.1f),
                            Soles = new Color(0.07f, 0.06f, 0.05f), Gloves = new Color(0.16f, 0.14f, 0.12f), Belt = new Color(0.2f, 0.16f, 0.12f),
                            Sleeve = new Color(0.2f, 0.21f, 0.2f), Trim = l.Accent };
                }
            }
        }

        // ------------------------------------------------------------------ anatomy

        private static void Body(System.Func<string, MeshKit> K, BodyShape b, CharacterLook l, Outfit o)
        {
            var id = Matrix4x4.identity;
            // Pelvis: hips and seat (trousers).
            K("Pelvis").Loft(new[]
            {
                new Ring(-0.21f, b.Hip * 0.8f, b.HipDepth * 0.82f), new Ring(-0.13f, b.Hip, b.HipDepth), new Ring(-0.05f, b.Hip * 0.98f, b.HipDepth * 0.97f),
                new Ring(0.04f, b.Waist, b.WaistDepth), new Ring(0.09f, b.Waist * 0.98f, b.WaistDepth * 0.98f),
            }, 18, S(o.Pants), id);
            K("Pelvis").Ellipsoid(new Vector3(b.Hip * 0.72f, 0.05f, b.HipDepth * 0.72f), 6, 12, S(o.Pants), MeshKit.At(new Vector3(0f, -0.2f, 0f)));

            // Torso (spine space: 0 = 1.0 m above the ground, neck base at 0.48).
            K("Spine").Loft(new[]
            {
                new Ring(-0.07f, b.Waist, b.WaistDepth), new Ring(0.05f, b.Waist * 1.01f, b.WaistDepth),
                new Ring(0.15f, Mathf.Lerp(b.Waist, b.Chest, 0.6f), Mathf.Lerp(b.WaistDepth, b.ChestDepth, 0.6f), 0f, 0.005f),
                new Ring(0.25f, b.Chest, b.ChestDepth, 0f, 0.012f), new Ring(0.33f, b.Chest * 1.02f, b.ChestDepth * 1.02f, 0f, 0.015f),
                new Ring(0.4f, b.Shoulder, b.ChestDepth * 0.9f), new Ring(0.45f, b.Shoulder * 0.55f, b.ChestDepth * 0.72f, 0f, -0.01f),
                new Ring(0.48f, b.Neck * 1.3f, b.Neck * 1.2f, 0f, -0.01f),
            }, 20, S(o.Shirt), id);
            // Neck up into the head.
            K("Spine").Loft(new[] { new Ring(0.44f, b.Neck, b.Neck * 0.95f, 0f, -0.005f), new Ring(0.58f, b.Neck * 0.92f, b.Neck * 0.9f, 0f, 0.0f) }, 12,
                S(l.Skin), id, capBottom: false);

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Upper arm: deltoid cap and a tapering tube (sleeve or skin).
                K("UpperArm" + sfx).Ellipsoid(new Vector3(b.Arm * 1.35f, b.Arm * 1.25f, b.Arm * 1.3f), 8, 14, S(o.Sleeve), MeshKit.At(new Vector3(0f, -0.01f, 0f)));
                K("UpperArm" + sfx).Loft(new[]
                {
                    new Ring(-0.29f, b.Arm * 0.78f, b.Arm * 0.8f), new Ring(-0.2f, b.Arm * 0.92f, b.Arm * 0.95f),
                    new Ring(-0.1f, b.Arm * 1.05f, b.Arm * 1.08f), new Ring(0.0f, b.Arm * 1.1f, b.Arm * 1.1f),
                }, 14, S(o.Sleeve), id);
                // Forearm: muscle swell near the elbow, slim wrist.
                var forearm = o.BareForearms ? l.Skin : o.Sleeve;
                K("Forearm" + sfx).Loft(new[]
                {
                    new Ring(-0.27f, b.Forearm * 0.66f, b.Forearm * 0.6f), new Ring(-0.18f, b.Forearm * 0.82f, b.Forearm * 0.78f),
                    new Ring(-0.06f, b.Forearm * 1.12f, b.Forearm * 1.05f, 0f, 0.004f), new Ring(0.02f, b.Forearm * 0.95f, b.Forearm * 0.95f),
                }, 14, S(forearm), id);
                Hand(K("Hand" + sfx), side, o, l);

                // Legs.
                K("Thigh" + sfx).Loft(new[]
                {
                    new Ring(-0.46f, b.Thigh * 0.62f, b.Thigh * 0.66f), new Ring(-0.35f, b.Thigh * 0.78f, b.Thigh * 0.8f),
                    new Ring(-0.2f, b.Thigh * 0.95f, b.Thigh * 0.98f, side * 0.004f), new Ring(-0.05f, b.Thigh * 1.08f, b.Thigh * 1.05f),
                    new Ring(0.04f, b.Thigh * 1.05f, b.Thigh * 1.02f),
                }, 16, S(o.Pants), id);
                float flare = o.WidePants ? 1.6f : 1f;
                K("Shin" + sfx).Loft(new[]
                {
                    new Ring(-0.42f, b.Calf * 0.56f * flare, b.Calf * 0.6f * flare), new Ring(-0.36f, b.Calf * 0.6f * flare, b.Calf * 0.62f * flare),
                    new Ring(-0.22f, b.Calf * 0.86f * Mathf.Lerp(1f, flare, 0.6f), b.Calf * 0.85f * Mathf.Lerp(1f, flare, 0.6f)),
                    new Ring(-0.08f, b.Calf * 1.1f, b.Calf * 1.06f, 0f, -0.012f), new Ring(0.03f, b.Calf * 0.98f, b.Calf * 0.98f),
                }, 16, S(o.Pants), id);
                Boot(K("Foot" + sfx), b, o, l);
            }
        }

        private static void Hand(MeshKit k, int side, Outfit o, CharacterLook l)
        {
            var glove = S(o.Gloves);
            var skin = S(l.Skin);
            // Palm (back of the hand faces outward), fingers curled into a grip, thumb across.
            k.Ellipsoid(new Vector3(0.036f, 0.048f, 0.02f), 8, 12, glove, MeshKit.At(new Vector3(0f, -0.05f, 0f)));
            k.Torus(0.034f, 0.024f, 0.006f, 12, 6, S(Mul(o.Gloves, 0.7f)), MeshKit.At(new Vector3(0f, -0.006f, 0f)));
            for (int f = 0; f < 4; f++)
            {
                float x = (f - 1.5f) * 0.018f;
                var finger = o.Fingerless ? skin : glove;
                k.Ellipsoid(new Vector3(0.0095f, 0.03f, 0.011f), 6, 8, glove, MeshKit.TRS(new Vector3(x, -0.095f, 0.01f), new Vector3(35f, 0f, 0f), Vector3.one));
                k.Ellipsoid(new Vector3(0.0085f, 0.022f, 0.01f), 6, 8, finger, MeshKit.TRS(new Vector3(x, -0.118f, 0.035f), new Vector3(100f, 0f, 0f), Vector3.one));
            }
            k.Ellipsoid(new Vector3(0.011f, 0.03f, 0.012f), 6, 8, o.Fingerless ? skin : glove,
                MeshKit.TRS(new Vector3(-side * 0.03f, -0.055f, 0.022f), new Vector3(30f, 0f, side * 35f), Vector3.one));
        }

        private static void Boot(MeshKit k, BodyShape b, Outfit o, CharacterLook l)
        {
            // Foot joint is at the ankle, 6 cm above the ground. The boot runs heel → toe along +Z (loft along Y, rotated).
            var along = MeshKit.TRS(new Vector3(0f, -0.025f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);
            k.Loft(new[]
            {
                new Ring(-0.075f, 0.045f, 0.032f, 0f, -0.0f), new Ring(-0.05f, 0.055f, 0.045f), new Ring(0.04f, 0.058f, 0.04f, 0f, 0.01f),
                new Ring(0.13f, 0.052f, 0.03f, 0f, 0.018f), new Ring(0.185f, 0.03f, 0.018f, 0f, 0.02f),
            }, 14, S(o.Boots), along);
            // Shaft around the ankle and lower shin.
            k.Loft(new[] { new Ring(-0.03f, b.Calf * 0.82f, b.Calf * 0.85f), new Ring(0.12f, b.Calf * 0.78f, b.Calf * 0.8f) }, 14, S(o.Boots), Matrix4x4.identity,
                capBottom: false);
            k.Torus(b.Calf * 0.8f, b.Calf * 0.83f, 0.008f, 14, 6, S(o.Trim * 0.8f), MeshKit.At(new Vector3(0f, 0.115f, 0f)));
            // Sole and heel.
            k.Box(new Vector3(0.11f, 0.022f, 0.27f), S(o.Soles), MeshKit.At(new Vector3(0f, -0.058f, 0.05f)));
            k.Box(new Vector3(0.09f, 0.025f, 0.07f), S(o.Soles), MeshKit.At(new Vector3(0f, -0.045f, -0.06f)));
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

        private static void Head(System.Func<string, MeshKit> K, CharacterLook l)
        {
            var head = K(CharacterBodyNames.Head);
            var centre = MeshKit.At(new Vector3(0f, HeadCentreY, 0f));
            var skin = S(l.Skin);
            // Skin shading painted into the vertices: cheek blush, a soft shadow under the jaw, a warmer nose / lip area,
            // and (REN) stubble on the lower face.
            var blush = Color.Lerp(l.Skin, new Color(1f, 0.55f, 0.55f), 0.35f);
            head.Ellipsoid(HeadRadii, 16, 24, d =>
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
            // Ears, nose, mouth.
            for (int side = -1; side <= 1; side += 2)
                head.Ellipsoid(new Vector3(0.012f, 0.026f, 0.019f), 6, 8, S(Mul(l.Skin, 0.95f)), MeshKit.TRS(new Vector3(side * 0.097f, HeadCentreY - 0.008f, -0.006f),
                    new Vector3(0f, side * 15f, 0f), Vector3.one));
            // Inner ears.
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
            // Lips: upper lip line with a cupid's-bow dip, fuller lower lip, a small shadow under it.
            var mouthZ = FaceZ(0f, -0.06f);
            var lipColor = Color.Lerp(l.Skin, new Color(0.75f, 0.3f, 0.32f), l.HairStyle == HairStyle.Crest ? 0.3f : 0.55f);
            head.Band(new[]
            {
                new Vector3(-0.015f, HeadCentreY - 0.059f, mouthZ - 0.003f), new Vector3(-0.005f, HeadCentreY - 0.057f, mouthZ), new Vector3(0f, HeadCentreY - 0.0585f, mouthZ + 0.0005f),
                new Vector3(0.005f, HeadCentreY - 0.057f, mouthZ), new Vector3(0.015f, HeadCentreY - 0.059f, mouthZ - 0.003f),
            }, new[] { 0.0025f, 0.004f, 0.004f, 0.004f, 0.0025f }, 0.002f, Vector3.up, _ => S(new Color(0.38f, 0.15f, 0.17f)), Matrix4x4.identity);
            head.Ellipsoid(new Vector3(0.011f, 0.0045f, 0.004f), 5, 10, S(lipColor), MeshKit.At(new Vector3(0f, HeadCentreY - 0.0645f, FaceZ(0f, -0.0645f) - 0.001f)));
            head.Band(new[]
            {
                new Vector3(-0.007f, HeadCentreY - 0.072f, FaceZ(0f, -0.072f) - 0.0005f), new Vector3(0.007f, HeadCentreY - 0.072f, FaceZ(0f, -0.072f) - 0.0005f),
            }, new[] { 0.004f, 0.004f }, 0.0015f, Vector3.up, _ => S(Mul(l.Skin, 0.85f)), Matrix4x4.identity);

            // Brows (hair colour): angle sets the character's expression.
            float browTilt = l.Id == 0 ? 0.012f : l.Id == 3 ? 0.009f : l.Id == 2 ? -0.004f : 0.006f; // stern → soft
            for (int side = -1; side <= 1; side += 2)
            {
                var inner = new Vector3(side * 0.017f, HeadCentreY + 0.04f - browTilt, 0f);
                var mid = new Vector3(side * 0.042f, HeadCentreY + 0.05f, 0f);
                var outer = new Vector3(side * 0.066f, HeadCentreY + 0.043f, 0f);
                inner.z = FaceZ(inner.x, inner.y - HeadCentreY) + 0.002f;
                mid.z = FaceZ(mid.x, mid.y - HeadCentreY) + 0.002f;
                outer.z = FaceZ(outer.x, outer.y - HeadCentreY) + 0.002f;
                // Three overlapping strokes give the brow a hair-like texture and a tapered tail.
                for (int stroke = 0; stroke < 3; stroke++)
                {
                    var shift = new Vector3(0f, (stroke - 1) * 0.0025f, 0.0005f * stroke);
                    head.Band(new[] { inner + shift, mid + shift * 1.2f, outer + shift * 0.5f }, new[] { 0.006f, 0.0055f, 0.002f }, 0.003f, Vector3.up,
                        _ => S(stroke == 1 ? l.Hair : Mul(l.Hair, 1.25f)), Matrix4x4.identity);
                }
            }
            CharacterMarks(head, l);

            // Eyes (own mesh, so they can blink): sclera, iris, pupil, two highlights, upper lash line with a flick.
            var eyes = K(CharacterBodyNames.Eyes);
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side * 0.042f, ey = -0.006f;
                float ez = FaceZ(ex, ey) + 0.003f - (HeadCentreY - EyePivotY) * 0f;
                var face = Quaternion.Euler(0f, side * 14f, 0f);
                Matrix4x4 Layer(float dz, Vector2 offset) =>
                    Matrix4x4.TRS(new Vector3(ex, ey + (HeadCentreY - EyePivotY), ez) + face * new Vector3(offset.x, offset.y, dz), face, Vector3.one);
                // Sclera with a soft shadow under the upper lid; iris in layers (dark limbal ring, colour, lighter lower
                // half, darker upper band), pupil, two highlights.
                eyes.Disc(0.024f, 0.031f, 18, S(new Color(0.97f, 0.96f, 0.98f)), Layer(0f, Vector2.zero));
                eyes.Disc(0.022f, 0.008f, 12, S(new Color(0.8f, 0.78f, 0.86f)), Layer(0.0006f, new Vector2(0f, 0.021f)));
                eyes.Disc(0.0185f, 0.0265f, 18, S(Mul(l.Eyes, 0.35f)), Layer(0.0011f, new Vector2(side * -0.002f, -0.002f)));
                eyes.Disc(0.0165f, 0.0245f, 18, S(l.Eyes), Layer(0.0014f, new Vector2(side * -0.002f, -0.002f)));
                eyes.Disc(0.013f, 0.011f, 14, S(Color.Lerp(l.Eyes, Color.white, 0.35f)), Layer(0.0017f, new Vector2(side * -0.002f, -0.011f)));
                eyes.Disc(0.0155f, 0.008f, 14, S(Mul(l.Eyes, 0.5f)), Layer(0.0018f, new Vector2(side * -0.002f, 0.014f)));
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
                eyes.Band(lower, lowerWidths, 0.002f, Vector3.forward, _ => S(new Color(0.18f, 0.1f, 0.14f)), Matrix4x4.identity);
                head.Band(crease, creaseWidths, 0.002f, Vector3.forward, _ => S(Mul(l.Skin, 0.8f)), Matrix4x4.TRS(new Vector3(0f, EyePivotY, 0f), Quaternion.identity, Vector3.one));
                // Upper lash line over the top of the eye, inner corner → outer corner, thicker outside with a flick.
                var lash = new List<Vector3>();
                var widths = new List<float>();
                var eyeFrame = Layer(0f, Vector2.zero);
                for (int i = 0; i <= 6; i++)
                {
                    float u = Mathf.Lerp(-1f, 1f, i / 6f);                       // -1 inner, +1 outer
                    var p = new Vector3(u * 0.026f * side, 0.031f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * 0.9f)) - 0.002f, 0.0035f);
                    if (i == 6)
                        p += new Vector3(side * 0.007f, 0.006f, 0f);
                    lash.Add(eyeFrame.MultiplyPoint3x4(p));
                    widths.Add(Mathf.Lerp(0.0035f, 0.0075f, i / 6f));
                }
                eyes.Band(lash, widths, 0.003f, Vector3.forward, _ => S(new Color(0.06f, 0.04f, 0.08f)), Matrix4x4.identity);
            }
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
                // Anime "angel ring": a lighter band around the upper head.
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
            var root = S(l.Hair);
            var tip = S(l.HairTips);
            var shine = S(Color.Lerp(l.Hair, Color.white, 0.35f));   // highlight streaks
            var random = new System.Random(l.Id * 13 + 5);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var up = Vector3.up;
            var centre = new Vector3(0f, HeadCentreY, 0f);

            // A lock = a main strand plus two thinner strands fanned around it, so hair reads as layered clumps.
            void Lock(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float flat, Color rootColor, Color tipColor)
            {
                k.Strand(a, b, c, d, width, flat, rootColor, tipColor, Matrix4x4.identity);
                var side = Vector3.Cross((d - a).normalized, (a - centre).normalized).normalized;
                for (int s = -1; s <= 1; s += 2)
                {
                    var off = side * (width * 0.7f * s);
                    k.Strand(a + off * 0.5f, b + off, c + off * 1.2f, d + off * 1.5f + (d - c) * -0.15f, width * 0.55f, flat, rootColor, tipColor, Matrix4x4.identity, 6);
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
                        else
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
                    // Ponytail tie at the back of the head (the tail itself swings: cloth chain).
                    k.Torus(0.016f, 0.014f, 0.006f, 12, 5, S(l.Secondary), MeshKit.TRS(new Vector3(0f, HeadCentreY - 0.02f, -0.118f), new Vector3(70f, 0f, 0f), Vector3.one));
                    break;

                case HairStyle.Short: // YUNA: layered bob (under + over layer), straight fringe, longer side locks, ahoge, glowing clip
                    HairCap(k, l, 1.05f, 0.05f);
                    for (int layer = 0; layer < 2; layer++)
                    {
                        int count = layer == 0 ? 20 : 24;
                        for (int i = 0; i < count; i++)
                        {
                            float theta = 55f + i * (250f / (count - 1)) + (layer == 1 ? 5f : 0f);
                            var a = Scalp(theta, layer == 0 ? R(40f, 55f) : R(24f, 36f), layer == 0 ? 0.99f : 1.01f);
                            var outward = new Vector3(Mathf.Sin(theta * Mathf.Deg2Rad), 0f, Mathf.Cos(theta * Mathf.Deg2Rad));
                            float reach = layer == 0 ? 0.118f : 0.128f;
                            var mid = new Vector3(outward.x * reach, HeadCentreY + 0.02f, outward.z * (reach + 0.005f) - 0.01f);
                            float length = layer == 0 ? 0.11f : 0.1f;
                            var end = new Vector3(outward.x * (reach - 0.018f), HeadCentreY - length + R(-0.008f, 0.008f), outward.z * (reach - 0.012f) - 0.01f);
                            k.Strand(a, a + outward * 0.05f, mid + Vector3.down * 0.03f, end, layer == 0 ? R(0.03f, 0.036f) : R(0.024f, 0.032f), 0.38f,
                                layer == 1 && i % 5 == 2 ? shine : root, tip, Matrix4x4.identity, 8);
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
                    k.Box(new Vector3(0.008f, 0.035f, 0.014f), G(l.Accent, 0.8f), MeshKit.TRS(new Vector3(0.09f, HeadCentreY + 0.045f, 0.05f), new Vector3(0f, 25f, -20f), Vector3.one));
                    k.Box(new Vector3(0.008f, 0.035f, 0.014f), G(l.Accent, 0.8f), MeshKit.TRS(new Vector3(0.092f, HeadCentreY + 0.03f, 0.035f), new Vector3(0f, 25f, -20f), Vector3.one));
                    break;

                default: // REN: undercut with shaved-side lines, a two-row silver crest, full beard with a braided chin tuft, moustache
                    HairCap(k, l, 1.02f, 0.035f);
                    // Shaved sides: darker stubble cap and two lines cut into each side.
                    for (int side = -1; side <= 1; side += 2)
                        for (int line = 0; line < 2; line++)
                        {
                            var pts = new List<Vector3>();
                            var widths = new List<float>();
                            for (int i = 0; i <= 4; i++)
                            {
                                pts.Add(Scalp(side * Mathf.Lerp(60f, 130f, i / 4f), 62f + line * 10f, 1.045f));
                                widths.Add(0.003f);
                            }
                            k.Band(pts, widths, 0.002f, Vector3.up, _ => S(Mul(l.Skin, 0.85f)), Matrix4x4.identity);
                        }
                    // Crest: two staggered rows of sharp spikes along the midline, tallest at the crown.
                    for (int row = 0; row < 2; row++)
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
                    for (int i = 0; i < 17; i++)
                    {
                        float t = i / 16f;
                        float x = Mathf.Lerp(-0.08f, 0.08f, t);
                        float y = -0.025f - 0.065f * (1f - Mathf.Abs(t * 2f - 1f));
                        var a = new Vector3(x, HeadCentreY + y, FaceZ(x, y) - 0.008f);
                        k.Strand(a, a + Vector3.down * 0.015f, a + Vector3.down * 0.03f + Vector3.forward * 0.008f, a + Vector3.down * R(0.035f, 0.055f) + Vector3.forward * 0.01f,
                            0.014f, 0.5f, beard, beardTip, Matrix4x4.identity, 5);
                    }
                    // Braided chin tuft: a strand with three bands.
                    {
                        var a = new Vector3(0f, HeadCentreY - 0.1f, FaceZ(0f, -0.1f) - 0.005f);
                        var end = a + new Vector3(0f, -0.07f, 0.01f);
                        k.Strand(a, a + Vector3.down * 0.025f, end + up * 0.02f, end, 0.014f, 0.8f, beard, beardTip, Matrix4x4.identity, 6);
                        for (int b = 0; b < 3; b++)
                            k.Torus(0.008f - b * 0.0015f, 0.008f - b * 0.0015f, 0.002f, 8, 4, S(l.Accent * 0.8f), MeshKit.At(Vector3.Lerp(a, end, 0.25f + b * 0.22f)));
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var a = new Vector3(side * 0.005f, HeadCentreY - 0.045f, FaceZ(0.005f, -0.045f) + 0.003f);
                        k.Strand(a, a + new Vector3(side * 0.01f, -0.002f, 0f), a + new Vector3(side * 0.02f, -0.008f, -0.003f), a + new Vector3(side * 0.03f, -0.02f, -0.008f),
                            0.007f, 0.6f, beard, beardTip, Matrix4x4.identity, 5);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ outfits

        /// <summary>A hanging chain: one band mesh per segment ("{name}_{index}"), each in its segment joint's space.</summary>
        private static void Chain(System.Func<string, MeshKit> K, CharacterModel model, DangleSpec spec, float widthTop, float widthBottom, float thickness,
            Color color, Color edge)
        {
            model.Dangles.Add(spec);
            for (int i = 0; i < spec.Segments; i++)
            {
                float t0 = i / (float)spec.Segments, t1 = (i + 1) / (float)spec.Segments;
                var path = new List<Vector3>();
                var widths = new List<float>();
                for (int s = 0; s <= 3; s++)
                {
                    float t = s / 3f;
                    path.Add(new Vector3(0f, -spec.SegmentLength * t * 1.02f, 0f));
                    widths.Add(Mathf.Lerp(Mathf.Lerp(widthTop, widthBottom, t0), Mathf.Lerp(widthTop, widthBottom, t1), t));
                }
                bool last = i == spec.Segments - 1;
                K($"{spec.Name}_{i}").Band(path, widths, thickness, Vector3.right, r => last && r == path.Count - 1 ? edge : color, Matrix4x4.identity);
                if (last)
                    K($"{spec.Name}_{i}").Box(new Vector3(Mathf.Lerp(widthTop, widthBottom, 1f) * 0.95f, 0.012f, thickness * 1.6f), edge,
                        MeshKit.At(new Vector3(0f, -spec.SegmentLength + 0.004f, 0f)));
            }
        }

        /// <summary>A hair tail as a swinging chain: tapering tube segments, a pointed tip on the last one.</summary>
        private static void HairChain(System.Func<string, MeshKit> K, CharacterModel model, DangleSpec spec, float width, Color root, Color tip)
        {
            model.Dangles.Add(spec);
            for (int i = 0; i < spec.Segments; i++)
            {
                float t0 = i / (float)spec.Segments, t1 = (i + 1) / (float)spec.Segments;
                float w0 = Mathf.Lerp(width, width * 0.35f, t0), w1 = Mathf.Lerp(width, width * 0.35f, t1);
                var c0 = Color.Lerp(root, tip, t0);
                var c1 = Color.Lerp(root, tip, t1);
                var kit = K($"{spec.Name}_{i}");
                kit.Loft(new[]
                {
                    new Ring(-spec.SegmentLength * 1.04f, w1, w1 * 0.75f), new Ring(-spec.SegmentLength * 0.5f, (w0 + w1) * 0.5f * 1.05f, (w0 + w1) * 0.4f),
                    new Ring(0.01f, w0, w0 * 0.75f),
                }, 10, (r, _) => S(r == 0 ? c1 : c0), Matrix4x4.identity);
                if (i == spec.Segments - 1)
                    kit.Strand(new Vector3(0f, -spec.SegmentLength, 0f), new Vector3(0f, -spec.SegmentLength - 0.02f, 0.004f), new Vector3(0f, -spec.SegmentLength - 0.04f, 0.01f),
                        new Vector3(0f, -spec.SegmentLength - 0.06f, 0.02f), w1, 0.75f, S(c1), S(tip), Matrix4x4.identity);
            }
        }

        private static void Kai(System.Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var coat = S(l.Primary * 1.6f);
            var trim = S(l.Secondary);
            var glow = G(l.Accent, 0.5f);
            // Haori over the torso: open at the front (the inner uniform shows through), trimmed lapels.
            K("Spine").Loft(new[]
            {
                new Ring(-0.08f, b.Waist + 0.016f, b.WaistDepth + 0.016f), new Ring(0.15f, Mathf.Lerp(b.Waist, b.Chest, 0.6f) + 0.016f, Mathf.Lerp(b.WaistDepth, b.ChestDepth, 0.6f) + 0.016f, 0f, 0.005f),
                new Ring(0.3f, b.Chest + 0.018f, b.ChestDepth + 0.018f, 0f, 0.012f), new Ring(0.4f, b.Shoulder + 0.012f, b.ChestDepth * 0.9f + 0.016f),
                new Ring(0.455f, b.Shoulder * 0.58f, b.ChestDepth * 0.76f, 0f, -0.01f),
            }, 22, (r, s) =>
            {
                float a = Mathf.Repeat(s * 360f / 22f + 180f, 360f) - 180f; // 0 = front
                if (Mathf.Abs(a) < 20f) return S(o.Shirt);            // open front
                if (Mathf.Abs(a) < 34f) return trim;                   // lapels
                return coat;
            }, Matrix4x4.identity, capBottom: false, capTop: false);
            // Clan crest on the back.
            K("Spine").Disc(0.05f, 0.05f, 16, glow, MeshKit.TRS(new Vector3(0f, 0.3f, -(b.ChestDepth + 0.022f)), new Vector3(0f, 180f, 0f), Vector3.one));
            K("Spine").Disc(0.036f, 0.036f, 16, coat, MeshKit.TRS(new Vector3(0f, 0.3f, -(b.ChestDepth + 0.0235f)), new Vector3(0f, 180f, 0f), Vector3.one));
            // Scarf wrapped around the neck (tails are cloth chains).
            K("Spine").Torus(b.Neck * 1.9f, b.Neck * 1.8f, 0.03f, 18, 8, trim, MeshKit.At(new Vector3(0f, 0.47f, -0.005f)));
            K("Spine").Torus(b.Neck * 1.75f, b.Neck * 1.65f, 0.025f, 18, 8, S(l.Secondary * 0.8f), MeshKit.At(new Vector3(0f, 0.505f, -0.005f)));
            for (int side = -1; side <= 1; side += 2)
                Chain(K, model, new DangleSpec { Name = side < 0 ? "ScarfL" : "ScarfR", Parent = "Spine", LocalPosition = new Vector3(side * 0.035f, 0.46f, -0.1f),
                    Segments = 3, SegmentLength = 0.17f, Stiffness = 10f }, 0.075f, 0.06f, 0.012f, trim, S(l.Accent));

            // Obi sash with a knot, tassels; coat skirt panels follow the legs.
            K("Pelvis").Torus(b.Waist + 0.02f, b.WaistDepth + 0.02f, 0.035f, 22, 8, trim, MeshKit.At(new Vector3(0f, 0.045f, 0f)));
            K("Pelvis").Ellipsoid(new Vector3(0.04f, 0.035f, 0.025f), 6, 10, S(l.Secondary * 1.2f), MeshKit.At(new Vector3(0.07f, 0.04f, b.WaistDepth + 0.04f)));
            K("Pelvis").Strand(new Vector3(0.07f, 0.02f, b.WaistDepth + 0.045f), new Vector3(0.075f, -0.03f, b.WaistDepth + 0.05f),
                new Vector3(0.08f, -0.08f, b.WaistDepth + 0.045f), new Vector3(0.085f, -0.13f, b.WaistDepth + 0.04f), 0.008f, 1f, S(l.Accent), G(l.Accent, 0.4f), Matrix4x4.identity, 5);
            Chain(K, model, new DangleSpec { Name = "CoatBack", Parent = "Pelvis", LocalPosition = new Vector3(0f, 0.0f, -(b.HipDepth + 0.025f)), Segments = 2, SegmentLength = 0.24f,
                Stiffness = 14f }, 0.3f, 0.34f, 0.014f, coat, trim);
            for (int side = -1; side <= 1; side += 2)
                Chain(K, model, new DangleSpec { Name = side < 0 ? "CoatFrontL" : "CoatFrontR", Parent = "Pelvis", LocalPosition = new Vector3(side * 0.1f, 0.0f, b.HipDepth + 0.02f),
                    Segments = 2, SegmentLength = 0.23f, Driver = side < 0 ? "ThighL" : "ThighR", DriverWeight = 0.75f, Stiffness = 16f }, 0.12f, 0.13f, 0.014f, coat, trim);

            // Katana scabbard on the LEFT hip (right-handed draw): lacquered saya with gold bands and a cord.
            // Mouth (+Y) forward and up at the hip, tip trailing down and back.
            var saya = MeshKit.TRS(new Vector3(-(b.Hip + 0.03f), -0.03f, 0.04f), new Vector3(62f, 0f, 8f), Vector3.one);
            K("Pelvis").Loft(new[] { new Ring(-0.74f, 0.012f, 0.008f), new Ring(-0.7f, 0.017f, 0.012f), new Ring(0f, 0.02f, 0.014f), new Ring(0.02f, 0.021f, 0.015f) },
                10, (r, s) => r == 1 ? S(new Color(0.75f, 0.6f, 0.28f)) : S(new Color(0.06f, 0.05f, 0.08f)), saya);
            K("Pelvis").Torus(0.022f, 0.016f, 0.004f, 10, 5, S(new Color(0.78f, 0.6f, 0.28f)), saya * MeshKit.At(new Vector3(0f, -0.08f, 0f)));
            K("Pelvis").Torus(0.022f, 0.016f, 0.004f, 10, 5, S(new Color(0.78f, 0.6f, 0.28f)), saya * MeshKit.At(new Vector3(0f, -0.02f, 0f)));

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Wide haori sleeve, open at the elbow.
                K("UpperArm" + sfx).Loft(new[]
                {
                    new Ring(-0.3f, b.Arm * 1.9f, b.Arm * 1.75f), new Ring(-0.15f, b.Arm * 1.5f, b.Arm * 1.45f), new Ring(0.02f, b.Arm * 1.45f, b.Arm * 1.4f),
                }, 14, (r, s) => r == 0 ? trim : coat, Matrix4x4.identity, capBottom: false);
                // Bandaged forearm: wraps every 3 cm.
                K("Forearm" + sfx).Loft(new[]
                {
                    new Ring(-0.26f, b.Forearm * 0.72f, b.Forearm * 0.67f), new Ring(-0.2f, b.Forearm * 0.84f, b.Forearm * 0.8f), new Ring(-0.14f, b.Forearm * 0.98f, b.Forearm * 0.92f),
                    new Ring(-0.08f, b.Forearm * 1.15f, b.Forearm * 1.08f, 0f, 0.004f), new Ring(-0.03f, b.Forearm * 1.1f, b.Forearm * 1.04f),
                }, 14, (r, s) => S(r % 2 == 0 ? new Color(0.92f, 0.9f, 0.86f) : new Color(0.82f, 0.8f, 0.75f)), Matrix4x4.identity, capBottom: false, capTop: false);
                // Tabi-style ankle wraps.
                K("Foot" + sfx).Torus(b.Calf * 0.85f, b.Calf * 0.88f, 0.012f, 14, 6, trim, MeshKit.At(new Vector3(0f, 0.08f, 0f)));
            }
            // Scar across the left eye (pale line on the face).
            K(CharacterBodyNames.Head).Band(new[]
            {
                new Vector3(-0.062f, HeadCentreY + 0.06f, FaceZ(-0.062f, 0.06f) + 0.004f), new Vector3(-0.046f, HeadCentreY + 0.0f, FaceZ(-0.046f, 0f) + 0.006f),
                new Vector3(-0.034f, HeadCentreY - 0.045f, FaceZ(-0.034f, -0.045f) + 0.003f),
            }, new[] { 0.003f, 0.004f, 0.002f }, 0.002f, Vector3.right, _ => S(new Color(0.85f, 0.6f, 0.6f)), Matrix4x4.identity);
        }

        private static void Akira(System.Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var jacket = S(new Color(0.09f, 0.08f, 0.1f));
            var crimson = S(l.Secondary);
            var webbing = S(new Color(0.16f, 0.15f, 0.14f));
            // Cropped tactical jacket with crimson side panels.
            K("Spine").Loft(new[]
            {
                new Ring(0.13f, Mathf.Lerp(b.Waist, b.Chest, 0.55f) + 0.016f, Mathf.Lerp(b.WaistDepth, b.ChestDepth, 0.55f) + 0.016f, 0f, 0.005f),
                new Ring(0.3f, b.Chest + 0.018f, b.ChestDepth + 0.018f, 0f, 0.012f), new Ring(0.4f, b.Shoulder + 0.012f, b.ChestDepth * 0.9f + 0.016f),
                new Ring(0.455f, b.Shoulder * 0.58f, b.ChestDepth * 0.76f, 0f, -0.01f),
            }, 22, (r, s) =>
            {
                float a = Mathf.Abs(Mathf.Repeat(s * 360f / 22f + 180f, 360f) - 180f);
                if (r == 0) return crimson;                          // hem
                return a > 70f && a < 110f ? crimson : jacket;       // side panels
            }, Matrix4x4.identity, capBottom: false, capTop: false);
            // High collar, open at the front.
            K("Spine").Loft(new[] { new Ring(0.44f, b.Neck * 1.7f, b.Neck * 1.6f), new Ring(0.55f, b.Neck * 1.55f, b.Neck * 1.45f) }, 16,
                (r, s) => s == 0 || s == 1 || s == 15 ? S(o.Shirt) : (r == 1 ? crimson : jacket), Matrix4x4.identity, capBottom: false, capTop: false);
            // Chest rig: plate with three rifle magazine pouches (flaps, buckles), shoulder straps.
            float front = b.ChestDepth + 0.03f;
            K("Spine").Box(new Vector3(b.Chest * 1.5f, 0.17f, 0.03f), webbing, MeshKit.At(new Vector3(0f, 0.22f, front)));
            for (int m = -1; m <= 1; m++)
            {
                var at = new Vector3(m * 0.072f, 0.205f, front + 0.032f);
                K("Spine").Box(new Vector3(0.062f, 0.12f, 0.035f), S(new Color(0.24f, 0.22f, 0.2f)), MeshKit.At(at));
                K("Spine").Box(new Vector3(0.066f, 0.035f, 0.04f), S(new Color(0.2f, 0.18f, 0.16f)), MeshKit.At(at + new Vector3(0f, 0.06f, 0.003f)));
                K("Spine").Box(new Vector3(0.016f, 0.02f, 0.008f), crimson, MeshKit.At(at + new Vector3(0f, 0.05f, 0.025f)));
            }
            for (int side = -1; side <= 1; side += 2)
                K("Spine").Band(new[]
                {
                    new Vector3(side * 0.09f, 0.3f, front), new Vector3(side * 0.12f, 0.43f, b.ChestDepth * 0.6f), new Vector3(side * 0.11f, 0.45f, 0f),
                    new Vector3(side * 0.1f, 0.36f, -b.ChestDepth - 0.02f), new Vector3(side * 0.09f, 0.2f, -b.ChestDepth - 0.018f),
                }, new[] { 0.04f, 0.04f, 0.04f, 0.04f, 0.04f }, 0.008f, Vector3.right, _ => webbing, Matrix4x4.identity);

            // Belt with buckle, dump pouch on the left hip, cargo pockets, holster on the RIGHT thigh.
            K("Pelvis").Torus(b.Waist + 0.012f, b.WaistDepth + 0.012f, 0.016f, 22, 6, S(o.Belt), MeshKit.At(new Vector3(0f, 0.03f, 0f)));
            K("Pelvis").Box(new Vector3(0.05f, 0.035f, 0.012f), S(new Color(0.6f, 0.58f, 0.55f)), MeshKit.At(new Vector3(0f, 0.03f, b.WaistDepth + 0.026f)));
            K("Pelvis").Box(new Vector3(0.07f, 0.09f, 0.06f), webbing, MeshKit.At(new Vector3(-(b.Hip + 0.02f), -0.05f, -0.02f)));
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                K("Thigh" + sfx).Box(new Vector3(0.03f, 0.1f, 0.08f), S(Mul(o.Pants, 0.85f)), MeshKit.At(new Vector3(side * (b.Thigh + 0.008f), -0.2f, 0f)));
                K("Shin" + sfx).Ellipsoid(new Vector3(b.Calf * 1.1f, 0.05f, 0.03f), 6, 10, S(new Color(0.1f, 0.09f, 0.1f)), MeshKit.At(new Vector3(0f, -0.01f, b.Calf * 0.9f)));
                K("Shin" + sfx).Torus(b.Calf * 1.05f, b.Calf * 1.02f, 0.006f, 12, 5, crimson, MeshKit.At(new Vector3(0f, -0.01f, 0f)));
                // Laces on the boot tongue.
                for (int i = 0; i < 4; i++)
                    K("Foot" + sfx).Box(new Vector3(0.045f, 0.005f, 0.008f), S(new Color(0.75f, 0.72f, 0.68f)), MeshKit.TRS(new Vector3(0f, 0.005f + i * 0.025f, 0.05f + i * 0.004f),
                        new Vector3(-20f, 0f, 0f), Vector3.one));
            }
            K("ThighR").Box(new Vector3(0.05f, 0.14f, 0.07f), webbing, MeshKit.At(new Vector3(b.Thigh + 0.03f, -0.13f, 0.01f)));
            K("ThighR").Box(new Vector3(0.025f, 0.06f, 0.035f), S(new Color(0.08f, 0.08f, 0.09f)), MeshKit.TRS(new Vector3(b.Thigh + 0.035f, -0.04f, 0.0f), new Vector3(-15f, 0f, 0f), Vector3.one));
            K("ThighR").Torus(b.Thigh * 1.02f, b.Thigh * 1.0f, 0.007f, 14, 5, webbing, MeshKit.At(new Vector3(0f, -0.1f, 0f)));
            K("ThighR").Torus(b.Thigh * 0.92f, b.Thigh * 0.9f, 0.007f, 14, 5, webbing, MeshKit.At(new Vector3(0f, -0.2f, 0f)));

            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                // Jacket sleeves pushed up to the forearm, crimson cuff.
                K("Forearm" + sfx).Loft(new[] { new Ring(-0.14f, b.Forearm * 1.25f, b.Forearm * 1.2f), new Ring(0.03f, b.Forearm * 1.3f, b.Forearm * 1.25f) }, 14,
                    (r, s) => r == 0 ? crimson : jacket, Matrix4x4.identity, capBottom: false);
            }
            // Hunter's armband on the left arm.
            K("UpperArmL").Torus(b.Arm * 1.2f, b.Arm * 1.2f, 0.016f, 14, 6, crimson, MeshKit.At(new Vector3(0f, -0.12f, 0f)));
            K("UpperArmL").Disc(0.016f, 0.016f, 10, G(l.Accent, 0.5f), MeshKit.TRS(new Vector3(-b.Arm * 1.3f, -0.12f, 0f), new Vector3(0f, -90f, 0f), Vector3.one));
            // Ponytail tied at the back of the head: hunters keep hair out of the sights. It swings (cloth chain).
            HairChain(K, model, new DangleSpec { Name = "Ponytail", Parent = CharacterBodyNames.Head, LocalPosition = new Vector3(0f, HeadCentreY - 0.03f, -0.12f),
                Segments = 3, SegmentLength = 0.085f, Stiffness = 9f }, 0.03f, l.Hair, l.HairTips);
            // Earring (right ear).
            K(CharacterBodyNames.Head).Torus(0.007f, 0.007f, 0.0018f, 10, 4, S(new Color(0.85f, 0.8f, 0.7f)),
                MeshKit.TRS(new Vector3(0.1f, HeadCentreY - 0.04f, -0.004f), new Vector3(0f, 0f, 90f), Vector3.one));
        }

        private static void Yuna(System.Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var white = S(l.Primary);
            var piping = G(l.Accent, 0.9f);
            // Techwear jacket with light piping (glowing lines at the seams).
            K("Spine").Loft(new[]
            {
                new Ring(0.02f, b.Waist + 0.016f, b.WaistDepth + 0.016f), new Ring(0.15f, Mathf.Lerp(b.Waist, b.Chest, 0.6f) + 0.016f, Mathf.Lerp(b.WaistDepth, b.ChestDepth, 0.6f) + 0.016f, 0f, 0.005f),
                new Ring(0.3f, b.Chest + 0.016f, b.ChestDepth + 0.016f, 0f, 0.012f), new Ring(0.4f, b.Shoulder + 0.012f, b.ChestDepth * 0.9f + 0.014f),
                new Ring(0.455f, b.Shoulder * 0.58f, b.ChestDepth * 0.76f, 0f, -0.01f),
            }, 24, (r, s) =>
            {
                if (r == 0) return piping;                               // hem line
                if (s == 2 || s == 22) return piping;                    // front seams
                if (s == 0 || s == 1 || s == 23) return S(o.Shirt);      // zipper opening
                return white;
            }, Matrix4x4.identity, capBottom: false, capTop: false);
            // Hood folded down at the back of the neck.
            K("Spine").Ellipsoid(new Vector3(0.11f, 0.07f, 0.06f), 8, 14, white, MeshKit.At(new Vector3(0f, 0.47f, -0.07f)));
            K("Spine").Torus(0.11f, 0.06f, 0.006f, 16, 5, piping, MeshKit.At(new Vector3(0f, 0.5f, -0.07f)));
            // Back tail of the jacket (cloth chain), shorts, thigh straps with lights.
            Chain(K, model, new DangleSpec { Name = "JacketTail", Parent = "Spine", LocalPosition = new Vector3(0f, 0.04f, -(b.WaistDepth + 0.02f)), Segments = 2,
                SegmentLength = 0.17f, Stiffness = 14f }, 0.2f, 0.12f, 0.012f, white, piping);
            K("Pelvis").Loft(new[] { new Ring(-0.2f, b.Hip * 0.85f + 0.01f, b.HipDepth * 0.87f + 0.01f), new Ring(-0.1f, b.Hip + 0.012f, b.HipDepth + 0.012f),
                new Ring(0.05f, b.Waist + 0.012f, b.WaistDepth + 0.012f) }, 18, white, Matrix4x4.identity, capBottom: false, capTop: false);
            K("Pelvis").Torus(b.Waist + 0.018f, b.WaistDepth + 0.018f, 0.008f, 18, 5, piping, MeshKit.At(new Vector3(0f, 0.04f, 0f)));
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                K("Thigh" + sfx).Loft(new[] { new Ring(-0.09f, b.Thigh * 1.12f, b.Thigh * 1.1f), new Ring(0.04f, b.Thigh * 1.12f, b.Thigh * 1.08f) }, 16, white,
                    Matrix4x4.identity, capBottom: false, capTop: false);
                K("Thigh" + sfx).Torus(b.Thigh * 0.98f, b.Thigh * 0.96f, 0.006f, 14, 5, piping, MeshKit.At(new Vector3(0f, -0.2f, 0f)));
                K("Foot" + sfx).Torus(b.Calf * 0.82f, b.Calf * 0.85f, 0.006f, 14, 5, piping, MeshKit.At(new Vector3(0f, 0.06f, 0f)));
                // Light seams on the sleeves.
                K("UpperArm" + sfx).Torus(b.Arm * 1.12f, b.Arm * 1.12f, 0.005f, 12, 5, piping, MeshKit.At(new Vector3(0f, -0.22f, 0f)));
            }
            // Wrist computer on the LEFT forearm: casing and glowing screen facing up / outward.
            K("ForearmL").Box(new Vector3(0.05f, 0.08f, 0.06f), S(new Color(0.18f, 0.2f, 0.26f)), MeshKit.At(new Vector3(-b.Forearm * 0.9f, -0.18f, 0f)));
            K("ForearmL").Box(new Vector3(0.004f, 0.065f, 0.048f), G(l.Accent, 0.9f), MeshKit.At(new Vector3(-b.Forearm * 0.9f - 0.026f, -0.18f, 0f)));
            // Holographic half-visor over the right eye, earpiece with antenna on the right ear.
            var head = K(CharacterBodyNames.Head);
            // Holographic half-visor: a glowing frame and a targeting reticle in front of the right eye (the eye stays visible).
            const float ex = 0.042f, ey = -0.006f;
            float vz = FaceZ(ex, ey) + 0.014f;
            Vector3 V(float x, float y) => new Vector3(x, HeadCentreY + y, Mathf.Min(vz, FaceZ(Mathf.Min(x, 0.09f), y) + 0.014f) - Mathf.Max(0f, x - 0.08f) * 0.6f);
            head.Band(new[] { V(0.012f, ey + 0.033f), V(0.045f, ey + 0.037f), V(0.08f, ey + 0.032f), V(0.104f, ey + 0.02f) }, new[] { 0.004f, 0.005f, 0.005f, 0.004f }, 0.003f,
                Vector3.up, _ => G(l.Accent, 0.9f), Matrix4x4.identity);
            head.Band(new[] { V(0.02f, ey - 0.034f), V(0.05f, ey - 0.036f), V(0.078f, ey - 0.03f) }, new[] { 0.0022f, 0.0025f, 0.0022f }, 0.002f,
                Vector3.up, _ => G(l.Accent, 0.9f), Matrix4x4.identity);
            head.Band(new[] { V(0.08f, ey + 0.032f), V(0.09f, ey), V(0.104f, ey - 0.008f) }, new[] { 0.004f, 0.004f, 0.004f }, 0.003f,
                Vector3.right, _ => S(new Color(0.85f, 0.88f, 0.95f)), Matrix4x4.identity);
            var reticle = Matrix4x4.TRS(V(ex, ey) + new Vector3(0f, 0f, 0.002f), Quaternion.Euler(90f, 14f, 0f), Vector3.one);
            head.Torus(0.0145f, 0.0145f, 0.0011f, 20, 4, G(l.Accent, 1f), reticle);
            for (int tick = 0; tick < 4; tick++)
            {
                float a = tick * 90f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                head.Band(new[] { V(ex, ey) + dir * 0.016f + Vector3.forward * 0.002f, V(ex, ey) + dir * 0.021f + Vector3.forward * 0.002f }, new[] { 0.0015f, 0.0015f }, 0.0012f,
                    Vector3.forward, _ => G(l.Accent, 1f), Matrix4x4.identity);
            }
            head.Box(new Vector3(0.022f, 0.04f, 0.03f), S(new Color(0.85f, 0.88f, 0.95f)), MeshKit.At(new Vector3(0.106f, HeadCentreY - 0.01f, -0.005f)));
            head.Disc(0.008f, 0.008f, 8, G(l.Accent, 0.9f), MeshKit.TRS(new Vector3(0.118f, HeadCentreY - 0.01f, -0.005f), new Vector3(0f, 90f, 0f), Vector3.one));
            head.Strand(new Vector3(0.11f, HeadCentreY + 0.01f, -0.01f), new Vector3(0.115f, HeadCentreY + 0.05f, -0.02f), new Vector3(0.115f, HeadCentreY + 0.09f, -0.03f),
                new Vector3(0.112f, HeadCentreY + 0.12f, -0.035f), 0.004f, 1f, S(new Color(0.8f, 0.82f, 0.9f)), G(l.Accent, 0.9f), Matrix4x4.identity, 5);
        }

        private static void Ren(System.Func<string, MeshKit> K, CharacterModel model, CharacterLook l, Outfit o)
        {
            var b = model.Shape;
            var plate = S(l.Secondary);
            var dark = S(l.Primary);
            var metal = S(new Color(0.5f, 0.52f, 0.56f));
            var amber = G(l.Accent, 0.9f);
            // Ribbed chest and back plates, abdominal plates.
            K("Spine").Ellipsoid(new Vector3(b.Chest * 0.95f, 0.13f, 0.06f), 8, 16, (d) => S(Mathf.Abs(d.y) % 0.3f < 0.05f ? Mul(l.Secondary, 0.75f) : l.Secondary),
                MeshKit.At(new Vector3(0f, 0.28f, b.ChestDepth - 0.01f)), q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.01f)));
            K("Spine").Ellipsoid(new Vector3(b.Chest * 0.95f, 0.14f, 0.06f), 8, 16, plate,
                MeshKit.At(new Vector3(0f, 0.28f, -(b.ChestDepth - 0.01f))), q => new Vector3(q.x, q.y, Mathf.Min(q.z, 0.01f)));
            for (int i = 0; i < 3; i++)
                K("Spine").Box(new Vector3(b.Waist * 1.3f, 0.045f, 0.025f), i % 2 == 0 ? plate : dark, MeshKit.At(new Vector3(0f, 0.12f - i * 0.05f, b.WaistDepth + 0.012f)));
            // Wolf-fur mantle around the collar.
            var fur = S(new Color(0.55f, 0.55f, 0.58f));
            var furTip = S(new Color(0.85f, 0.85f, 0.88f));
            var random = new System.Random(77);
            for (int i = 0; i < 26; i++)
            {
                float a = (i / 26f) * Mathf.PI * 2f;
                var root = new Vector3(Mathf.Sin(a) * b.Shoulder * 0.62f, 0.45f, Mathf.Cos(a) * b.ChestDepth * 0.95f);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float len = 0.06f + (float)random.NextDouble() * 0.04f;
                k_Fur(K("Spine"), root, outward, len, fur, furTip);
            }
            // Shell bandolier: right shoulder to left hip, red shells with brass caps.
            var path = new[] { new Vector3(0.14f, 0.43f, 0.05f), new Vector3(0.06f, 0.3f, b.ChestDepth + 0.05f), new Vector3(-0.06f, 0.16f, b.ChestDepth + 0.04f),
                new Vector3(-0.14f, 0.02f, b.WaistDepth + 0.02f) };
            K("Spine").Band(path, new[] { 0.05f, 0.05f, 0.05f, 0.05f }, 0.01f, Vector3.right, _ => S(new Color(0.3f, 0.22f, 0.15f)), Matrix4x4.identity);
            for (int i = 0; i < 7; i++)
            {
                float t = 0.15f + i * 0.11f;
                int seg = Mathf.Min(2, (int)(t * 3f));
                var p = Vector3.Lerp(path[seg], path[seg + 1], t * 3f - seg) + Vector3.forward * 0.012f;
                var dir = (path[seg + 1] - path[seg]).normalized;
                var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Cross(dir, Vector3.forward).normalized);
                K("Spine").Loft(new[] { new Ring(-0.022f, 0.008f, 0.008f), new Ring(0.016f, 0.008f, 0.008f), new Ring(0.017f, 0.0085f, 0.0085f), new Ring(0.026f, 0.0085f, 0.0085f) },
                    8, (r, s) => r >= 2 ? S(new Color(0.85f, 0.65f, 0.3f)) : S(new Color(0.7f, 0.12f, 0.12f)), Matrix4x4.TRS(p, rot, Vector3.one));
            }
            // Dog tags on a chain.
            K("Spine").Strand(new Vector3(-0.04f, 0.46f, b.ChestDepth * 0.7f), new Vector3(-0.03f, 0.38f, b.ChestDepth + 0.03f), new Vector3(0.0f, 0.34f, b.ChestDepth + 0.03f),
                new Vector3(0.01f, 0.33f, b.ChestDepth + 0.035f), 0.002f, 1f, metal, metal, Matrix4x4.identity, 6);
            K("Spine").Box(new Vector3(0.022f, 0.035f, 0.003f), metal, MeshKit.TRS(new Vector3(0.012f, 0.31f, b.ChestDepth + 0.04f), new Vector3(0f, 0f, 8f), Vector3.one));
            K("Spine").Box(new Vector3(0.022f, 0.035f, 0.003f), S(new Color(0.6f, 0.62f, 0.66f)), MeshKit.TRS(new Vector3(0.022f, 0.305f, b.ChestDepth + 0.044f), new Vector3(0f, 0f, -10f), Vector3.one));
            // Pauldrons: oversized layered LEFT, smaller right.
            for (int layer = 0; layer < 3; layer++)
                K("UpperArmL").Ellipsoid(new Vector3(b.Arm * 2.3f - layer * 0.012f, 0.05f, b.Arm * 2.1f - layer * 0.01f), 6, 14, layer == 0 ? plate : (layer == 1 ? dark : plate),
                    MeshKit.TRS(new Vector3(-0.01f, 0.03f - layer * 0.045f, 0f), new Vector3(0f, 0f, -18f - layer * 6f), Vector3.one), q => new Vector3(q.x, Mathf.Max(q.y, -0.01f), q.z));
            K("UpperArmL").Disc(0.02f, 0.02f, 12, amber, MeshKit.TRS(new Vector3(-b.Arm * 2.1f, 0.0f, 0f), new Vector3(0f, -90f, 0f), Vector3.one));
            K("UpperArmR").Ellipsoid(new Vector3(b.Arm * 1.7f, 0.04f, b.Arm * 1.6f), 6, 14, plate, MeshKit.TRS(new Vector3(0.008f, 0.03f, 0f), new Vector3(0f, 0f, 15f), Vector3.one),
                q => new Vector3(q.x, Mathf.Max(q.y, -0.01f), q.z));

            // Mechanical LEFT arm: metal segments with glowing amber joints (elbow, wrist, knuckles).
            K("UpperArmL").Loft(new[] { new Ring(-0.29f, b.Arm * 0.85f, b.Arm * 0.85f), new Ring(-0.05f, b.Arm * 1.05f, b.Arm * 1.05f) }, 10,
                (r, s) => s % 5 == 0 ? dark : metal, Matrix4x4.identity);
            K("ForearmL").Loft(new[]
            {
                new Ring(-0.26f, b.Forearm * 0.75f, b.Forearm * 0.72f), new Ring(-0.16f, b.Forearm * 0.95f, b.Forearm * 0.9f),
                new Ring(-0.05f, b.Forearm * 1.18f, b.Forearm * 1.1f), new Ring(0.02f, b.Forearm, b.Forearm),
            }, 10, (r, s) => s % 5 == 0 ? dark : metal, Matrix4x4.identity);
            K("ForearmL").Torus(b.Forearm * 0.95f, b.Forearm * 0.95f, 0.01f, 12, 6, amber, MeshKit.At(new Vector3(0f, 0.01f, 0f)));
            K("ForearmL").Torus(b.Forearm * 0.78f, b.Forearm * 0.75f, 0.008f, 12, 6, amber, MeshKit.At(new Vector3(0f, -0.25f, 0f)));
            K("ForearmL").Box(new Vector3(0.01f, 0.12f, 0.04f), amber, MeshKit.At(new Vector3(-b.Forearm * 1.08f, -0.12f, 0f)));
            for (int f = 0; f < 4; f++)
                K("HandL").Disc(0.005f, 0.005f, 6, amber, MeshKit.TRS(new Vector3((f - 1.5f) * 0.018f, -0.085f, 0.022f), Vector3.zero, Vector3.one));
            // Heavy belt with shell pouches, knee armour.
            K("Pelvis").Torus(b.Waist + 0.02f, b.WaistDepth + 0.02f, 0.022f, 22, 6, S(o.Belt), MeshKit.At(new Vector3(0f, 0.03f, 0f)));
            for (int i = 0; i < 4; i++)
            {
                float a = (i - 1.5f) * 0.38f + Mathf.PI;
                K("Pelvis").Box(new Vector3(0.05f, 0.06f, 0.035f), S(new Color(0.24f, 0.2f, 0.16f)),
                    MeshKit.TRS(new Vector3(Mathf.Sin(a) * (b.Waist + 0.04f), 0.01f, Mathf.Cos(a) * (b.WaistDepth + 0.04f)), new Vector3(0f, a * Mathf.Rad2Deg, 0f), Vector3.one));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                K("Shin" + sfx).Ellipsoid(new Vector3(b.Calf * 1.25f, 0.065f, 0.04f), 6, 12, plate, MeshKit.At(new Vector3(0f, -0.01f, b.Calf * 0.85f)),
                    q => new Vector3(q.x, q.y, Mathf.Max(q.z, -0.01f)));
                K("Thigh" + sfx).Box(new Vector3(0.035f, 0.11f, 0.09f), S(Mul(o.Pants, 0.85f)), MeshKit.At(new Vector3(side * (b.Thigh + 0.01f), -0.22f, 0f)));
            }
            // Battle scar on the right cheek.
            K(CharacterBodyNames.Head).Band(new[]
            {
                new Vector3(0.045f, HeadCentreY - 0.012f, FaceZ(0.045f, -0.012f) + 0.004f), new Vector3(0.062f, HeadCentreY - 0.04f, FaceZ(0.062f, -0.04f) + 0.004f),
            }, new[] { 0.004f, 0.003f }, 0.002f, Vector3.right, _ => S(new Color(0.75f, 0.52f, 0.48f)), Matrix4x4.identity);
        }

        private static void k_Fur(MeshKit k, Vector3 root, Vector3 outward, float length, Color fur, Color tip)
        {
            k.Strand(root, root + outward * length * 0.4f + Vector3.up * 0.01f, root + outward * length * 0.8f, root + outward * length + Vector3.down * 0.03f,
                0.022f, 0.6f, fur, tip, Matrix4x4.identity, 4);
        }
    }

    /// <summary>Joint names shared by the styler, the models and the animator.</summary>
    public static class CharacterBodyNames
    {
        public const string Head = "HeadJoint";
        public const string Eyes = "Eyes";
    }
}
