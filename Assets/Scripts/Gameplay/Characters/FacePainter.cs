using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>Per-character colours and shapes of the painted anime face.</summary>
    public struct FaceStyle
    {
        public Color Iris;
        public Color Lash;
        public Color Brow;
        public Color Lip;
        public Color SkinShadow;
        /// <summary>Upper-lid height of the open eye (0.2 = neutral male, larger = rounder eyes).</summary>
        public float LidHeight;
        /// <summary>Outer-corner lift (sharp, upturned eyes are positive).</summary>
        public float OuterLift;
        public float BrowThickness;
    }

    /// <summary>Expression tiles in the face atlas (4 x 4 tiles): 8 eye shapes, 4 brow shapes, 4 mouths (with the nose).</summary>
    public static class FaceTiles
    {
        public const int EyeNeutral = 0, EyeFocused = 1, EyeAngry = 2, EyeClosed = 3, EyeWide = 4, EyeHappy = 5, EyePained = 6, EyeDetermined = 7;
        public const int BrowNeutral = 8, BrowAngry = 9, BrowWorried = 10, BrowRaised = 11;
        public const int MouthNeutral = 12, MouthSmirk = 13, MouthOpen = 14, MouthGrit = 15;
    }

    /// <summary>
    /// Paints a character's face atlas (1024 x 1024, sixteen 256 px tiles) with anti-aliased vector shapes: almond eyes
    /// with a lid shadow, a layered iris (dark upper band, saturated middle, light lower half, limbal ring, radial fibres,
    /// pupil, two highlights), a heavy upper lash line with a flick, a thin lower lash and a lid crease; tapered brows;
    /// a small side-shaded nose and the mouth. Painted faces on a modelled head are how anime games get crisp eyes; the
    /// expression is changed by switching tiles (<see cref="CharacterFacialAnimator"/>). Eye tiles are painted for the
    /// character's right eye (outer corner on the left of the tile) and mirrored for the left eye.
    /// </summary>
    public static class FacePainter
    {
        public const int Size = 1024;
        public const int Tile = 256;

        public static Texture2D Paint(FaceStyle style)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "FaceAtlas", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(PaintPixels(style));
            texture.Apply(true);
            return texture;
        }

        /// <summary>The atlas pixels (bottom row first, as Texture2D expects).</summary>
        public static Color32[] PaintPixels(FaceStyle style)
        {
            var pixels = new Color32[Size * Size];
            for (int tile = 0; tile < 16; tile++)
            {
                int col = tile % 4, row = tile / 4;
                int x0 = col * Tile, y0 = (3 - row) * Tile; // row 0 at the top of the texture
                for (int py = 0; py < Tile; py++)
                for (int px = 0; px < Tile; px++)
                {
                    float u = (px + 0.5f) / Tile, v = (py + 0.5f) / Tile;
                    Color c = tile < 8 ? Eye(tile, u, v, style) : tile < 12 ? Brow(tile, u, v, style) : Mouth(tile, u, v, style);
                    pixels[(y0 + py) * Size + x0 + px] = c;
                }
            }
            return pixels;
        }

        private const float Aa = 1.6f / Tile;

        private static float Inside(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance / Aa);

        private static Color Over(Color under, Color over, float alpha)
        {
            alpha = Mathf.Clamp01(alpha * over.a);
            float a = alpha + under.a * (1f - alpha);
            if (a <= 1e-5f)
                return new Color(0f, 0f, 0f, 0f);
            var rgb = (new Color(over.r, over.g, over.b) * alpha + new Color(under.r, under.g, under.b) * under.a * (1f - alpha)) / a;
            return new Color(rgb.r, rgb.g, rgb.b, a);
        }

        // ------------------------------------------------------------------ eyes

        private struct EyeShape
        {
            public float OuterV, InnerV, Up, UpPow, Low, LowPow, Slant, Droop;
        }

        private static EyeShape ShapeOf(int tile, FaceStyle s)
        {
            float up = s.LidHeight, lift = s.OuterLift;
            switch (tile)
            {
                case FaceTiles.EyeFocused: return new EyeShape { OuterV = 0.52f + lift, InnerV = 0.47f, Up = up * 0.66f, UpPow = 0.6f, Low = 0.11f, LowPow = 1.2f };
                case FaceTiles.EyeAngry: return new EyeShape { OuterV = 0.55f + lift, InnerV = 0.44f, Up = up * 0.8f, UpPow = 0.6f, Low = 0.12f, LowPow = 1.2f, Slant = 0.11f };
                case FaceTiles.EyeWide: return new EyeShape { OuterV = 0.5f + lift, InnerV = 0.46f, Up = up * 1.3f, UpPow = 0.8f, Low = 0.18f, LowPow = 1.1f };
                case FaceTiles.EyePained: return new EyeShape { OuterV = 0.47f + lift * 0.5f, InnerV = 0.47f, Up = up * 0.5f, UpPow = 0.9f, Low = 0.1f, LowPow = 1.2f, Droop = 0.06f };
                case FaceTiles.EyeDetermined: return new EyeShape { OuterV = 0.53f + lift, InnerV = 0.46f, Up = up * 0.78f, UpPow = 0.55f, Low = 0.13f, LowPow = 1.25f, Slant = 0.05f };
                default: return new EyeShape { OuterV = 0.52f + lift, InnerV = 0.46f, Up = up, UpPow = 0.7f, Low = 0.14f, LowPow = 1.3f };
            }
        }

        private const float OuterU = 0.07f, InnerU = 0.93f;

        private static float Upper(in EyeShape e, float x) =>
            Mathf.Lerp(e.OuterV, e.InnerV, x) + e.Up * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.85f)), e.UpPow) - e.Slant * x - e.Droop * (1f - x);

        private static float Lower(in EyeShape e, float x) =>
            Mathf.Lerp(e.OuterV, e.InnerV, x) - e.Low * Mathf.Pow(Mathf.Sin(Mathf.PI * x), e.LowPow);

        private static Color Eye(int tile, float u, float v, FaceStyle s)
        {
            var lash = s.Lash;
            if (tile == FaceTiles.EyeClosed || tile == FaceTiles.EyeHappy)
            {
                // A closed lid line (slightly curved down), or the happy "^" arc.
                float x = Mathf.InverseLerp(OuterU, InnerU, u);
                if (x <= 0f || x >= 1f)
                    return Color.clear;
                float baseV = Mathf.Lerp(0.5f + s.OuterLift * 0.5f, 0.46f, x);
                float line = tile == FaceTiles.EyeHappy ? baseV - 0.04f + 0.12f * Mathf.Sin(Mathf.PI * x) : baseV - 0.05f * Mathf.Sin(Mathf.PI * x);
                float thick = Mathf.Lerp(0.05f, 0.02f, x);
                var c = Color.clear;
                c = Over(c, lash, Inside(Mathf.Abs(v - line) - thick * 0.5f));
                // A few lashes at the outer corner.
                if (tile == FaceTiles.EyeClosed && x < 0.35f)
                    for (int i = 0; i < 3; i++)
                    {
                        float lx = 0.08f + i * 0.09f;
                        float lv = Mathf.Lerp(0.5f, 0.46f, lx) - 0.05f * Mathf.Sin(Mathf.PI * lx);
                        float d = DistanceToSegment(new Vector2(u, v), new Vector2(Mathf.Lerp(OuterU, InnerU, lx), lv), new Vector2(Mathf.Lerp(OuterU, InnerU, lx) - 0.03f, lv - 0.06f));
                        c = Over(c, lash, Inside(d - 0.008f));
                    }
                return c;
            }

            var e = ShapeOf(tile, s);
            float xx = (u - OuterU) / (InnerU - OuterU);
            var col = Color.clear;
            if (xx > 0f && xx < 1f)
            {
                float up = Upper(e, xx), low = Lower(e, xx);
                float open = Mathf.Min(up - v, v - low); // positive inside the opening
                float coverage = Inside(-open);
                if (coverage > 0f)
                {
                    // Sclera with the shadow of the upper lid.
                    var sclera = Color.Lerp(new Color(0.97f, 0.96f, 0.99f), new Color(0.72f, 0.68f, 0.82f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(up - 0.13f, up, v)));
                    var c = sclera;
                    // Iris: centred under the upper lid, taller than the opening (cut by the lids, anime style).
                    float cx = 0.53f;
                    float xc = (cx - OuterU) / (InnerU - OuterU);
                    float cy = (Upper(e, xc) + Lower(e, xc)) * 0.5f - 0.025f;
                    var d = new Vector2((u - cx) / 0.165f, (v - cy) / 0.215f);
                    float r = d.magnitude;
                    if (r < 1f + Aa * 6f)
                    {
                        float t = Mathf.InverseLerp(cy + 0.2f, cy - 0.2f, v); // 0 top, 1 bottom
                        var iris = Color.Lerp(s.Iris * 0.5f, s.Iris * 1.15f, Mathf.SmoothStep(0f, 1f, t * 1.5f));
                        iris = Color.Lerp(iris, Color.Lerp(s.Iris, Color.white, 0.55f), Mathf.SmoothStep(0.5f, 1f, t));
                        // Radial fibres.
                        float angle = Mathf.Atan2(d.y, d.x);
                        iris = Color.Lerp(iris, Color.Lerp(s.Iris, Color.white, 0.3f), Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 9f)), 8f) * 0.25f * Mathf.InverseLerp(0.3f, 0.85f, r));
                        // Limbal ring.
                        iris = Color.Lerp(iris, s.Iris * 0.18f, Mathf.SmoothStep(0.8f, 0.97f, r));
                        // Pupil.
                        float pupil = new Vector2((u - cx) / 0.06f, (v - (cy + 0.01f)) / 0.1f).magnitude;
                        iris = Color.Lerp(iris, new Color(0.04f, 0.02f, 0.07f), Inside((pupil - 1f) * 0.06f));
                        iris.a = 1f;
                        c = Color.Lerp(c, iris, Inside((r - 1f) * 0.165f));
                        // Highlights: a large one up and outward, a small one low and inward, a soft reflected arc.
                        float h1 = new Vector2((u - (cx - 0.06f)) / 0.055f, (v - (cy + 0.09f)) / 0.045f).magnitude;
                        float h2 = new Vector2((u - (cx + 0.06f)) / 0.022f, (v - (cy - 0.09f)) / 0.022f).magnitude;
                        c = Color.Lerp(c, Color.white, Inside((h1 - 1f) * 0.05f));
                        c = Color.Lerp(c, Color.white, Inside((h2 - 1f) * 0.022f) * 0.9f);
                        if (r > 0.55f && r < 0.85f && d.y < -0.2f)
                            c = Color.Lerp(c, Color.Lerp(s.Iris, Color.white, 0.6f), 0.35f * Mathf.Sin(Mathf.InverseLerp(0.55f, 0.85f, r) * Mathf.PI));
                    }
                    c.a = 1f;
                    col = Over(col, c, coverage);
                }

                // Upper lash line: heavy, thicker toward the outer corner, sitting on the lid edge.
                float thick = Mathf.Lerp(0.085f, 0.032f, Mathf.Pow(xx, 0.8f));
                float dv = v - up;
                col = Over(col, lash, Inside(Mathf.Max(-0.012f - dv, dv - thick)));
                // Lower lash: thin, outer half only, fading inward.
                if (xx < 0.5f)
                    col = Over(col, lash, Inside(Mathf.Abs(v - low) - 0.008f * (1f - xx / 0.5f)) * 0.8f);
                // Lid crease above the eye.
                if (xx > 0.15f && xx < 0.85f)
                    col = Over(col, s.SkinShadow, Inside(Mathf.Abs(v - (up + 0.1f)) - 0.006f) * 0.45f * Mathf.Sin(Mathf.InverseLerp(0.15f, 0.85f, xx) * Mathf.PI));
            }
            // Outer flick of the lash line.
            {
                var a = new Vector2(OuterU + 0.03f, e.OuterV + 0.015f);
                var b = new Vector2(0.0f, e.OuterV + 0.085f + s.OuterLift);
                float d = DistanceToSegment(new Vector2(u, v), a, b);
                float w = Mathf.Lerp(0.032f, 0.004f, Mathf.InverseLerp(a.x, b.x, u));
                col = Over(col, lash, Inside(d - w * 0.5f));
            }
            return col;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ brows (outer end on the left of the tile)

        private static Color Brow(int tile, float u, float v, FaceStyle s)
        {
            float outer, inner, arch;
            switch (tile)
            {
                case FaceTiles.BrowAngry: outer = 0.66f; inner = 0.3f; arch = 0.03f; break;
                case FaceTiles.BrowWorried: outer = 0.36f; inner = 0.64f; arch = 0.03f; break;
                case FaceTiles.BrowRaised: outer = 0.58f; inner = 0.62f; arch = 0.12f; break;
                default: outer = 0.5f; inner = 0.44f; arch = 0.08f; break;
            }
            float x = Mathf.InverseLerp(0.06f, 0.94f, u);
            if (x <= 0f || x >= 1f)
                return Color.clear;
            float line = Mathf.Lerp(outer, inner, x) + arch * Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f));
            float thick = Mathf.Lerp(0.03f, 0.1f, Mathf.Pow(x, 0.6f)) * s.BrowThickness;
            // Tapered tail and a square-ish inner end.
            float end = Mathf.Min(x / 0.12f, (1f - x) / 0.04f);
            float coverage = Inside(Mathf.Abs(v - line) - thick * 0.5f) * Mathf.Clamp01(end);
            // Hair-like stroke texture.
            float strands = 0.85f + 0.15f * Mathf.Sin((u * 90f) + v * 30f);
            var c = s.Brow * strands;
            c.a = 1f;
            return Over(Color.clear, c, coverage);
        }

        // ------------------------------------------------------------------ nose and mouth

        private static Color Mouth(int tile, float u, float v, FaceStyle s)
        {
            var col = Color.clear;
            // Nose: a side-shadow line and a soft tip shade (top of the tile).
            col = Over(col, s.SkinShadow, Inside(DistanceToSegment(new Vector2(u, v), new Vector2(0.535f, 0.93f), new Vector2(0.51f, 0.8f)) - 0.008f) * 0.55f);
            col = Over(col, s.SkinShadow, Inside(new Vector2((u - 0.49f) / 0.045f, (v - 0.775f) / 0.016f).magnitude - 1f) * 0.35f);

            var line = Color.Lerp(s.Lip, Color.black, 0.45f);
            line.a = 1f;
            switch (tile)
            {
                case FaceTiles.MouthSmirk:
                {
                    float x = Mathf.InverseLerp(0.38f, 0.64f, u);
                    if (x > 0f && x < 1f)
                    {
                        float mv = 0.25f - 0.012f * Mathf.Sin(Mathf.PI * x) + 0.05f * Mathf.Pow(x, 3f);
                        col = Over(col, line, Inside(Mathf.Abs(v - mv) - 0.007f * (0.6f + 0.4f * Mathf.Sin(Mathf.PI * x))));
                    }
                    break;
                }
                case FaceTiles.MouthOpen:
                {
                    float r = new Vector2((u - 0.5f) / 0.1f, (v - 0.22f) / 0.085f).magnitude;
                    var inside = new Color(0.25f, 0.05f, 0.08f, 1f);
                    if (v < 0.17f)
                        inside = Color.Lerp(inside, new Color(0.78f, 0.32f, 0.36f, 1f), Mathf.InverseLerp(0.17f, 0.14f, v));
                    if (v > 0.27f)
                        inside = new Color(0.96f, 0.95f, 0.97f, 1f); // upper teeth
                    col = Over(col, inside, Inside((r - 1f) * 0.08f));
                    col = Over(col, line, Inside(Mathf.Abs(r - 1f) * 0.08f - 0.005f));
                    break;
                }
                case FaceTiles.MouthGrit:
                {
                    var d = new Vector2(Mathf.Abs(u - 0.5f) - 0.12f, Mathf.Abs(v - 0.24f) - 0.03f);
                    float box = Mathf.Max(d.x, d.y);
                    col = Over(col, new Color(0.95f, 0.94f, 0.96f, 1f), Inside(box));
                    col = Over(col, line, Inside(Mathf.Abs(box) - 0.005f));
                    col = Over(col, line, Inside(Mathf.Abs(v - 0.24f) - 0.003f) * Inside(Mathf.Abs(u - 0.5f) - 0.12f));
                    break;
                }
                default:
                {
                    float x = Mathf.InverseLerp(0.39f, 0.61f, u);
                    if (x > 0f && x < 1f)
                    {
                        float mv = 0.25f - 0.016f * Mathf.Sin(Mathf.PI * x) + 0.01f;
                        col = Over(col, line, Inside(Mathf.Abs(v - mv) - 0.006f * (0.5f + 0.5f * Mathf.Sin(Mathf.PI * x))));
                    }
                    break;
                }
            }
            // Lower-lip shade.
            if (tile != FaceTiles.MouthOpen)
                col = Over(col, s.SkinShadow, Inside(new Vector2((u - 0.5f) / 0.06f, (v - 0.17f) / 0.014f).magnitude - 1f) * 0.3f);
            return col;
        }
    }
}
