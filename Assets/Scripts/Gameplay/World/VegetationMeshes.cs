using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>Grass clump looks (all shorter than <see cref="VegetationMeshes.MaxGrassHeight"/>).</summary>
    public enum GrassStyle : byte
    {
        /// <summary>Mixed meadow blades, a few dry ones.</summary>
        Meadow = 0,
        /// <summary>Short, thin, dense lawn grass (paths, forest floor).</summary>
        Fine = 1,
        /// <summary>Taller tuft with seed heads.</summary>
        Tuft = 2,
    }

    public enum TreeKind : byte
    {
        Broadleaf = 0,
        Pine = 1,
        Blossom = 2,
        Palm = 3,
    }

    /// <summary>
    /// Procedural vegetation and rock meshes (PLACEHOLDER art, original geometry): stylized anime
    /// trees, grass clumps, flowers, bushes and rocks. Deterministic per seed. Every mesh uses
    /// vertex colours (rgb = colour, a = wind weight: 0 at roots / trunk base, up to 1 at tips) for
    /// the WarOfNyx/NyxFoliage shader. Trees have two submeshes: 0 = bark, 1 = leaves.
    /// Canopy normals point away from the canopy centre, the usual anime-tree trick that turns a
    /// lumpy mesh into a few large, clean light / shade shapes.
    /// </summary>
    public static class VegetationMeshes
    {
        /// <summary>Tallest grass / flower (metres). Kept well under a crouched character so grass never hides anyone.</summary>
        public const float MaxGrassHeight = 0.55f;

        // ------------------------------------------------------------------ public builders

        public static Mesh Tree(TreeKind kind, int seed, bool lowDetail)
        {
            var b = new Builder(2);
            // LOD0 and LOD1 consume the same random sequence, so both levels have the same silhouette.
            var random = new System.Random(seed * 97 + (int)kind * 7);
            switch (kind)
            {
                case TreeKind.Pine: PineTree(b, random, lowDetail); break;
                case TreeKind.Blossom: LeafyTree(b, random, lowDetail, blossom: true); break;
                case TreeKind.Palm: PalmTree(b, random, lowDetail); break;
                default: LeafyTree(b, random, lowDetail, blossom: false); break;
            }
            return b.ToMesh($"Tree_{kind}_{seed}{(lowDetail ? "_LOD1" : "")}");
        }

        public static Mesh GrassClump(int seed) => GrassClump(seed, GrassStyle.Meadow);

        private static readonly Color[] GrassTips =
        {
            new Color(0.56f, 0.78f, 0.36f), // yellow-green
            new Color(0.42f, 0.7f, 0.34f),  // green
            new Color(0.36f, 0.62f, 0.4f),  // blue-green
            new Color(0.62f, 0.8f, 0.42f),  // fresh
        };

        public static Mesh GrassClump(int seed, GrassStyle style)
        {
            var b = new Builder(1);
            var random = new System.Random(seed * 3 + (int)style);
            int blades; float minH, maxH, minW, maxW, radius;
            switch (style)
            {
                case GrassStyle.Fine: blades = 20 + random.Next(0, 5); minH = 0.1f; maxH = 0.26f; minW = 0.012f; maxW = 0.02f; radius = 0.3f; break;
                case GrassStyle.Tuft: blades = 9 + random.Next(0, 4); minH = 0.3f; maxH = MaxGrassHeight - 0.08f; minW = 0.025f; maxW = 0.04f; radius = 0.18f; break;
                default: blades = 14 + random.Next(0, 4); minH = 0.18f; maxH = MaxGrassHeight - 0.05f; minW = 0.02f; maxW = 0.035f; radius = 0.28f; break;
            }
            for (int i = 0; i < blades; i++)
            {
                float a = (float)(random.NextDouble() * Mathf.PI * 2f);
                float r = radius * Mathf.Sqrt((float)random.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                // Taller blades in the middle of the clump, shorter at the rim.
                float height = Mathf.Lerp(minH, maxH, (float)random.NextDouble()) * Mathf.Lerp(1f, 0.75f, r / radius);
                float width = Mathf.Lerp(minW, maxW, (float)random.NextDouble());
                float tint = Mathf.Lerp(0.85f, 1.1f, (float)random.NextDouble());
                var tip = GrassTips[random.Next(GrassTips.Length)] * tint;
                if (random.NextDouble() < (style == GrassStyle.Tuft ? 0.18 : 0.08))
                    tip = new Color(0.72f, 0.66f, 0.38f) * tint; // dry blade
                tip.a = 1f;
                Blade(b, random, root, height, width, new Color(0.15f, 0.28f, 0.15f), tip);
            }
            if (style == GrassStyle.Tuft)
            {
                // Seed stalks: thin stems ending in an elongated seed head.
                int stalks = 2 + random.Next(0, 3);
                for (int i = 0; i < stalks; i++)
                {
                    float a = (float)(random.NextDouble() * Mathf.PI * 2f);
                    var root = new Vector3(Mathf.Cos(a) * 0.06f, 0f, Mathf.Sin(a) * 0.06f);
                    float height = Mathf.Lerp(0.38f, MaxGrassHeight - 0.08f, (float)random.NextDouble());
                    var top = Blade(b, random, root, height, 0.008f, new Color(0.2f, 0.32f, 0.18f), new Color(0.6f, 0.62f, 0.36f));
                    SeedHead(b, top, (float)(random.NextDouble() * Mathf.PI), new Color(0.78f, 0.72f, 0.48f));
                }
            }
            return b.ToMesh($"Grass_{style}_{seed}");
        }

        /// <summary>A small elongated seed head (crossed diamonds) on top of a stalk; stays under the grass height limit.</summary>
        private static void SeedHead(Builder b, Vector3 top, float spin, Color color)
        {
            color.a = 1f;
            const float length = 0.07f, width = 0.018f;
            for (int k = 0; k < 2; k++)
            {
                float a = spin + k * Mathf.PI * 0.5f;
                var side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * width;
                int i0 = b.Vertex(0, top, Vector3.up, color);
                b.Vertex(0, top + Vector3.up * length * 0.45f + side, Vector3.up, color);
                b.Vertex(0, top + Vector3.up * length, Vector3.up, color);
                b.Vertex(0, top + Vector3.up * length * 0.45f - side, Vector3.up, color);
                b.Triangle(0, i0, i0 + 1, i0 + 2);
                b.Triangle(0, i0, i0 + 2, i0 + 3);
            }
        }

        public static Mesh FlowerClump(int seed, Color petal)
        {
            var b = new Builder(1);
            var random = new System.Random(seed);
            int stems = 3 + random.Next(0, 3);
            for (int i = 0; i < stems; i++)
            {
                float a = (float)(random.NextDouble() * Mathf.PI * 2f);
                float r = 0.18f * Mathf.Sqrt((float)random.NextDouble());
                var root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float height = Mathf.Lerp(0.24f, 0.42f, (float)random.NextDouble());
                var top = Blade(b, random, root, height, 0.02f, new Color(0.18f, 0.34f, 0.24f), new Color(0.35f, 0.6f, 0.35f));
                // Five-petal star head facing up (two-sided through Cull Off).
                float size = Mathf.Lerp(0.05f, 0.08f, (float)random.NextDouble());
                float spin = (float)(random.NextDouble() * Mathf.PI);
                int centre = b.Vertex(0, top + Vector3.up * 0.01f, Vector3.up, new Color(1f, 0.92f, 0.55f, 1f));
                for (int p = 0; p < 10; p++)
                {
                    float pa = spin + p * Mathf.PI / 5f;
                    float pr = p % 2 == 0 ? size : size * 0.4f;
                    var pos = top + new Vector3(Mathf.Cos(pa) * pr, -0.005f, Mathf.Sin(pa) * pr);
                    b.Vertex(0, pos, Vector3.up, new Color(petal.r, petal.g, petal.b, 1f));
                }
                for (int p = 0; p < 10; p++)
                    b.Triangle(0, centre, centre + 1 + (p + 1) % 10, centre + 1 + p);
            }
            return b.ToMesh($"Flowers_{seed}");
        }

        public static Mesh Bush(int seed, Color leaf, bool lowDetail)
        {
            var b = new Builder(1);
            var random = new System.Random(seed);
            int blobs = 3 + random.Next(0, 3);
            var centre = new Vector3(0f, 0.65f, 0f);
            for (int i = 0; i < blobs; i++)
            {
                float a = (float)(random.NextDouble() * Mathf.PI * 2f);
                float d = i == 0 ? 0f : Mathf.Lerp(0.35f, 0.7f, (float)random.NextDouble());
                float radius = Mathf.Lerp(0.62f, 0.95f, (float)random.NextDouble());
                var c = new Vector3(Mathf.Cos(a) * d, radius * 0.8f + (float)random.NextDouble() * 0.25f, Mathf.Sin(a) * d);
                Blob(b, 0, random, c, new Vector3(radius, radius * 0.85f, radius), centre, leaf, lowDetail ? 1 : 2, 0.2f, 0.15f, 0.45f, 0f);
            }
            return b.ToMesh($"Bush_{seed}{(lowDetail ? "_LOD1" : "")}");
        }

        /// <summary>Faceted rock (unit size; scale it). Wind weight 0. Mossy tops when <paramref name="moss"/>.</summary>
        public static Mesh Rock(int seed, bool moss)
        {
            var random = new System.Random(seed);
            Icosphere(2, out var unit, out var tris);
            var offset = new Vector3((float)random.NextDouble() * 50f, (float)random.NextDouble() * 50f, (float)random.NextDouble() * 50f);
            var squash = new Vector3(Mathf.Lerp(0.9f, 1.3f, (float)random.NextDouble()), Mathf.Lerp(0.5f, 0.8f, (float)random.NextDouble()), Mathf.Lerp(0.8f, 1.1f, (float)random.NextDouble()));
            var positions = new Vector3[unit.Count];
            for (int i = 0; i < unit.Count; i++)
            {
                var v = unit[i];
                float n = Noise3(v * 1.7f + offset) * 0.32f + Noise3(v * 4.1f + offset) * 0.12f;
                var p = v * (0.85f + n);
                p = Vector3.Scale(p, squash);
                if (p.y < -0.15f)
                    p.y = -0.15f - (p.y + 0.15f) * 0.2f; // flat-ish bottom so it sits on the ground
                positions[i] = p;
            }

            var b = new Builder(1);
            var stone = new Color(0.47f, 0.44f, 0.53f);
            var mossColor = new Color(0.33f, 0.47f, 0.33f);
            for (int t = 0; t < tris.Count; t += 3)
            {
                var a = positions[tris[t]];
                var c = positions[tris[t + 1]];
                var d = positions[tris[t + 2]];
                var normal = Vector3.Cross(c - a, d - a).normalized;
                float shade = Mathf.Lerp(0.85f, 1.08f, Noise3((a + c + d) * 2.3f + offset) + 0.5f);
                var color = stone * shade;
                if (moss && normal.y > 0.55f)
                    color = Color.Lerp(color, mossColor, Mathf.InverseLerp(0.55f, 0.85f, normal.y));
                color.a = 0f;
                int i0 = b.Vertex(0, a, normal, color);
                b.Vertex(0, c, normal, color);
                b.Vertex(0, d, normal, color);
                b.Triangle(0, i0, i0 + 1, i0 + 2);
            }
            return b.ToMesh($"Rock_{seed}");
        }

        // ------------------------------------------------------------------ tree recipes

        private static void LeafyTree(Builder b, System.Random random, bool low, bool blossom)
        {
            float height = Mathf.Lerp(5.5f, 8.5f, (float)random.NextDouble()) * (blossom ? 0.85f : 1f);
            float trunkRadius = Mathf.Lerp(0.28f, 0.4f, (float)random.NextDouble());
            var lean = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.6f;
            var bark = blossom ? new Color(0.33f, 0.22f, 0.24f) : new Color(0.38f, 0.27f, 0.21f);
            var top = Trunk(b, random, Vector3.zero, height, trunkRadius, lean, bark, low ? 5 : 7, low ? 3 : 6, 0.12f);

            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + (float)random.NextDouble();
                var start = Vector3.Lerp(Vector3.zero, top, 0.6f + 0.1f * i);
                var dir = new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)).normalized;
                Trunk(b, random, start, 2.2f, trunkRadius * 0.4f, dir * 0.9f, bark, low ? 4 : 5, low ? 1 : 2, 0.25f);
            }

            var canopyCentre = top + Vector3.up * (blossom ? 0.8f : 1.2f);
            var leaf = blossom
                ? Color.Lerp(new Color(1f, 0.62f, 0.8f), new Color(0.98f, 0.78f, 0.9f), (float)random.NextDouble())
                : Color.Lerp(new Color(0.3f, 0.58f, 0.42f), new Color(0.42f, 0.66f, 0.38f), (float)random.NextDouble());
            int blobs = 6 + random.Next(0, 3);
            float spread = blossom ? 2.6f : 2.1f;
            for (int i = 0; i < blobs; i++)
            {
                Vector3 c;
                float radius;
                if (i == 0)
                {
                    c = canopyCentre;
                    radius = blossom ? 2.5f : 2.3f;
                }
                else
                {
                    float a = (float)(random.NextDouble() * Mathf.PI * 2f);
                    float up = Mathf.Lerp(-0.6f, 1.2f, (float)random.NextDouble());
                    c = canopyCentre + new Vector3(Mathf.Cos(a) * spread, up, Mathf.Sin(a) * spread) * Mathf.Lerp(0.7f, 1f, (float)random.NextDouble());
                    radius = Mathf.Lerp(1.4f, 2.1f, (float)random.NextDouble());
                }
                var scale = new Vector3(radius, radius * (blossom ? 0.75f : 0.85f), radius);
                Blob(b, 1, random, c, scale, canopyCentre, leaf, low ? 1 : 2, 0.16f, 0.35f, 0.75f, 0.55f);
            }
        }

        private static void PineTree(Builder b, System.Random random, bool low)
        {
            float height = Mathf.Lerp(8f, 12f, (float)random.NextDouble());
            var bark = new Color(0.32f, 0.23f, 0.2f);
            Trunk(b, random, Vector3.zero, height * 0.92f, 0.32f, Vector3.zero, bark, low ? 5 : 7, low ? 2 : 4, 0.1f);

            var needles = Color.Lerp(new Color(0.18f, 0.4f, 0.4f), new Color(0.22f, 0.46f, 0.36f), (float)random.NextDouble());
            const int tiers = 6;
            int sides = low ? 8 : 14;
            float bottom = height * 0.22f;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);
                float y = Mathf.Lerp(bottom, height * 0.78f, f);
                float radius = Mathf.Lerp(3.1f, 1.0f, f) * Mathf.Lerp(0.9f, 1.1f, (float)random.NextDouble());
                float coneHeight = Mathf.Lerp(3.2f, 2.2f, f);
                var apex = new Vector3(0f, y + coneHeight, 0f);
                float spin = (float)(random.NextDouble() * Mathf.PI);
                float shade = Mathf.Lerp(0.78f, 1.08f, f);
                float sway = Mathf.Lerp(0.3f, 0.7f, f);
                int apexIndex = b.Vertex(1, apex, Vector3.up, Tint(needles, shade * 1.1f, sway));
                int ringStart = b.VertexCount(1);
                for (int s = 0; s < sides; s++)
                {
                    float a = spin + s * Mathf.PI * 2f / sides;
                    float r = s % 2 == 0 ? radius : radius * 0.72f; // jagged needle rim
                    var p = new Vector3(Mathf.Cos(a) * r, y - (s % 2 == 0 ? 0.35f : 0f), Mathf.Sin(a) * r);
                    var normal = (new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a))).normalized;
                    b.Vertex(1, p, normal, Tint(needles, shade * 0.85f, sway));
                }
                // Underside: its own rim vertices with downward normals (shared rim vertices would light it like the top).
                int underCentre = b.Vertex(1, new Vector3(0f, y - 0.1f, 0f), Vector3.down, Tint(needles, shade * 0.55f, sway * 0.5f));
                int underStart = b.VertexCount(1);
                for (int s = 0; s < sides; s++)
                    b.Vertex(1, b.Position(ringStart + s), Vector3.down, Tint(needles, shade * 0.6f, sway));
                for (int s = 0; s < sides; s++)
                {
                    int i0 = ringStart + s;
                    int i1 = ringStart + (s + 1) % sides;
                    b.Triangle(1, apexIndex, i1, i0);
                    b.Triangle(1, underCentre, underStart + s, underStart + (s + 1) % sides);
                }
            }
        }

        private static void PalmTree(Builder b, System.Random random, bool low)
        {
            float height = Mathf.Lerp(6f, 9f, (float)random.NextDouble());
            float bendAngle = (float)(random.NextDouble() * Mathf.PI * 2f);
            var bend = new Vector3(Mathf.Cos(bendAngle), 0f, Mathf.Sin(bendAngle)) * Mathf.Lerp(1.2f, 2.2f, (float)random.NextDouble());
            var top = Trunk(b, random, Vector3.zero, height, 0.26f, bend, new Color(0.5f, 0.4f, 0.32f), low ? 5 : 7, low ? 4 : 9, 0.15f, curved: true);

            var frond = Color.Lerp(new Color(0.28f, 0.58f, 0.36f), new Color(0.36f, 0.64f, 0.32f), (float)random.NextDouble());
            const int fronds = 8;
            int segments = low ? 3 : 6;
            for (int f = 0; f < fronds; f++)
            {
                float a = f * Mathf.PI * 2f / fronds + (float)random.NextDouble() * 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                float length = Mathf.Lerp(3.2f, 4.2f, (float)random.NextDouble());
                float lift = Mathf.Lerp(0.6f, 1.4f, (float)random.NextDouble());
                int prevL = -1, prevR = -1, prevC = -1;
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    // Arc: rises, then droops.
                    var c = top + dir * (length * t) + Vector3.up * (lift * Mathf.Sin(t * Mathf.PI * 0.8f) - t * t * 1.6f);
                    float width = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI) * 0.75f + 0.05f;
                    var color = Tint(frond, Mathf.Lerp(0.85f, 1.1f, t), Mathf.Lerp(0.3f, 1f, t));
                    var normal = (Vector3.up + dir * 0.2f).normalized;
                    int cIndex = b.Vertex(1, c + Vector3.up * 0.08f, normal, color);
                    int lIndex = b.Vertex(1, c + side * width - Vector3.up * width * 0.35f, normal, color);
                    int rIndex = b.Vertex(1, c - side * width - Vector3.up * width * 0.35f, normal, color);
                    if (s > 0)
                    {
                        b.Triangle(1, prevC, cIndex, prevL);
                        b.Triangle(1, prevL, cIndex, lIndex);
                        b.Triangle(1, prevR, rIndex, prevC);
                        b.Triangle(1, prevC, rIndex, cIndex);
                    }
                    prevC = cIndex;
                    prevL = lIndex;
                    prevR = rIndex;
                }
            }
            // Coconuts.
            if (!low)
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.1f;
                    Blob(b, 0, random, top + new Vector3(Mathf.Cos(a) * 0.3f, -0.35f, Mathf.Sin(a) * 0.3f), Vector3.one * 0.2f, top,
                        new Color(0.36f, 0.27f, 0.2f), 1, 0f, 0.15f, 0.15f, 0f);
                }
        }

        // ------------------------------------------------------------------ parts

        /// <summary>Tapered, slightly wobbly tube. Returns the top centre.</summary>
        private static Vector3 Trunk(Builder b, System.Random random, Vector3 start, float height, float radius, Vector3 lean, Color bark,
            int sides, int rings, float maxSway, bool curved = false)
        {
            int baseIndex = b.VertexCount(0);
            var top = start;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                var axis = start + Vector3.up * (height * t) + lean * (curved ? t * t : t);
                if (r == rings)
                    top = axis;
                float radiusHere = radius * Mathf.Lerp(1.25f, 0.55f, t) * (r == 0 ? 1.15f : 1f);
                if (curved && r % 2 == 1)
                    radiusHere *= 1.12f; // palm rings
                float shade = Mathf.Lerp(0.75f, 1.05f, t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float wobble = 1f + 0.08f * Mathf.Sin(a * 3f + t * 5f);
                    var color = Tint(bark, shade * (1f + Noise3(axis * 1.3f + outward * 2f) * 0.18f), maxSway * t * t);
                    b.Vertex(0, axis + outward * radiusHere * wobble, outward, color);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                int i0 = baseIndex + r * (sides + 1) + s;
                int i1 = i0 + 1;
                int i2 = i0 + sides + 1;
                int i3 = i2 + 1;
                b.Triangle(0, i0, i2, i1);
                b.Triangle(0, i1, i2, i3);
            }
            return top;
        }

        /// <summary>
        /// Noise-displaced ellipsoid. Normals blend from the blob's own centre to <paramref name="normalCentre"/>
        /// (canopy centre) so several blobs light as one soft shape. Undersides are darkened (baked occlusion).
        /// </summary>
        private static void Blob(Builder b, int submesh, System.Random random, Vector3 centre, Vector3 scale, Vector3 normalCentre, Color color,
            int subdivisions, float displacement, float swayLow, float swayHigh, float normalBlend)
        {
            Icosphere(subdivisions, out var unit, out var tris);
            int baseIndex = b.VertexCount(submesh);
            var offset = new Vector3((float)random.NextDouble() * 40f, (float)random.NextDouble() * 40f, (float)random.NextDouble() * 40f);
            float tint = Mathf.Lerp(0.92f, 1.08f, (float)random.NextDouble());
            foreach (var v in unit)
            {
                float n = Noise3(v * 2.2f + offset);
                var p = centre + Vector3.Scale(v * (1f + n * displacement * 2f), scale);
                var own = v;
                var shared = (p - normalCentre).normalized;
                var normal = Vector3.Slerp(own, shared, normalBlend).normalized;
                float occlusion = Mathf.Lerp(0.62f, 1.05f, Mathf.Clamp01(v.y * 0.5f + 0.5f));
                float speckle = 1f + n * 0.25f;
                float sway = Mathf.Lerp(swayLow, swayHigh, Mathf.Clamp01(v.y * 0.5f + 0.5f));
                b.Vertex(submesh, p, normal, Tint(color, tint * occlusion * speckle, sway));
            }
            for (int t = 0; t < tris.Count; t += 3)
                b.Triangle(submesh, baseIndex + tris[t], baseIndex + tris[t + 1], baseIndex + tris[t + 2]);
        }

        /// <summary>One curved, tapered grass blade (3 segments + tip). Returns the tip position.</summary>
        private static Vector3 Blade(Builder b, System.Random random, Vector3 root, float height, float width, Color rootColor, Color tipColor)
        {
            float facing = (float)(random.NextDouble() * Mathf.PI * 2f);
            var across = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
            var leanDir = new Vector3(-across.z, 0f, across.x) * Mathf.Lerp(-1f, 1f, (float)random.NextDouble());
            float leanAmount = Mathf.Lerp(0.05f, 0.18f, (float)random.NextDouble());
            const int segments = 3;
            int first = b.VertexCount(0);
            Vector3 tip = root;
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                var centre = root + Vector3.up * (height * t) + leanDir * (leanAmount * t * t);
                var color = Color.Lerp(rootColor, tipColor, t);
                color.a = Mathf.Pow(t, 1.5f);
                // Mostly-up normals: grass lights like the ground it grows on (no stripey blades).
                var normal = (Vector3.up * 2f + leanDir).normalized;
                if (s == segments)
                {
                    tip = centre;
                    b.Vertex(0, centre, normal, color);
                }
                else
                {
                    float w = width * (1f - t * 0.75f);
                    b.Vertex(0, centre - across * w, normal, color);
                    b.Vertex(0, centre + across * w, normal, color);
                }
            }
            for (int s = 0; s < segments - 1; s++)
            {
                int i0 = first + s * 2;
                b.Triangle(0, i0, i0 + 2, i0 + 1);
                b.Triangle(0, i0 + 1, i0 + 2, i0 + 3);
            }
            int last = first + (segments - 1) * 2;
            b.Triangle(0, last, last + 2, last + 1);
            return tip;
        }

        private static Color Tint(Color c, float shade, float sway) =>
            new Color(Mathf.Clamp01(c.r * shade), Mathf.Clamp01(c.g * shade), Mathf.Clamp01(c.b * shade), Mathf.Clamp01(sway));

        // ------------------------------------------------------------------ geometry helpers

        private static readonly Dictionary<int, (List<Vector3>, List<int>)> IcoCache = new Dictionary<int, (List<Vector3>, List<int>)>();

        /// <summary>Unit icosphere (shared vertices). Cached per subdivision level; callers must not modify the lists.</summary>
        private static void Icosphere(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
        {
            if (IcoCache.TryGetValue(subdivisions, out var cached))
            {
                vertices = cached.Item1;
                triangles = cached.Item2;
                return;
            }
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;
            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 4);
                var verts = vertices;
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out int index))
                        return index;
                    verts.Add(((verts[a] + verts[c]) * 0.5f).normalized);
                    midpoints[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], c = triangles[i + 1], d = triangles[i + 2];
                    int ab = Mid(a, c), bc = Mid(c, d), ca = Mid(d, a);
                    next.AddRange(new[] { a, ab, ca, c, bc, ab, d, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }
            IcoCache[subdivisions] = (vertices, triangles);
        }

        /// <summary>Smooth 3-D value noise in roughly [-0.5, 0.5].</summary>
        public static float Noise3(Vector3 p)
        {
            var i = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
            var f = p - i;
            var u = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));
            float H(float x, float y, float z)
            {
                uint h = (uint)((int)x * 73856093 ^ (int)y * 19349663 ^ (int)z * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
            float x00 = Mathf.Lerp(H(i.x, i.y, i.z), H(i.x + 1, i.y, i.z), u.x);
            float x10 = Mathf.Lerp(H(i.x, i.y + 1, i.z), H(i.x + 1, i.y + 1, i.z), u.x);
            float x01 = Mathf.Lerp(H(i.x, i.y, i.z + 1), H(i.x + 1, i.y, i.z + 1), u.x);
            float x11 = Mathf.Lerp(H(i.x, i.y + 1, i.z + 1), H(i.x + 1, i.y + 1, i.z + 1), u.x);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, u.y), Mathf.Lerp(x01, x11, u.y), u.z) - 0.5f;
        }

        /// <summary>Collects vertices for one mesh with several submeshes (indices are global).</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _positions = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int>[] _triangles;

            public Builder(int submeshes)
            {
                _triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                    _triangles[i] = new List<int>();
            }

            /// <summary>Index the next vertex will get (indices are shared across submeshes).</summary>
            public int VertexCount(int submesh) => _positions.Count;

            public int Vertex(int submesh, Vector3 position, Vector3 normal, Color color)
            {
                _positions.Add(position);
                _normals.Add(normal);
                _colors.Add(color);
                return _positions.Count - 1;
            }

            public Vector3 Position(int index) => _positions[index];

            public void Triangle(int submesh, int a, int b, int c)
            {
                _triangles[submesh].Add(a);
                _triangles[submesh].Add(b);
                _triangles[submesh].Add(c);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (_positions.Count > 65000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_positions);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++)
                    mesh.SetTriangles(_triangles[i], i, false);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
