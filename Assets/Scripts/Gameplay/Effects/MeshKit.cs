using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>One cross-section of a <see cref="MeshKit.Loft"/>: height, elliptical radii and centre offset.</summary>
    public struct Ring
    {
        public float Y, Rx, Rz, X, Z;

        public Ring(float y, float rx, float rz, float x = 0f, float z = 0f)
        {
            Y = y;
            Rx = rx;
            Rz = rz;
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Procedural mesh building for detailed PLACEHOLDER characters and gear: smooth lofts (torso, limbs,
    /// boots, sleeves, scabbards), sculptable ellipsoids (heads, muscles, armour), tapered Bezier strands
    /// (hair), flat ribbons (scarves, coat tails, straps), discs (eyes, emblems), boxes (pouches, plates) and
    /// rings (belts, collars). Every vertex carries a colour (rgb) and a glow amount (1 - alpha) for
    /// WarOfNyx/NyxToonVC, so one mesh per body part holds a whole outfit. Smooth grid normals.
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;

        /// <summary>Glow is stored as 1 - alpha, so ordinary colours (alpha 1) never glow by accident.</summary>
        public static Color Glow(Color c, float amount) => new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01(amount));

        public static Color Solid(Color c) => new Color(c.r, c.g, c.b, 1f);

        public static Matrix4x4 TRS(Vector3 position, Vector3 euler, Vector3 scale) => Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);

        public static Matrix4x4 At(Vector3 position, Vector3 euler = default) => Matrix4x4.TRS(position, Quaternion.Euler(euler), Vector3.one);

        // ------------------------------------------------------------------ core: a grid of points with smooth normals

        /// <summary>Triangulates a rows x cols grid (cols wrap when <paramref name="wrap"/>), smooth normals from neighbours.</summary>
        public void Grid(Vector3[,] p, Func<int, int, Color> color, bool wrap, Matrix4x4 m, bool flip = false)
        {
            int rows = p.GetLength(0), cols = p.GetLength(1);
            var normalMatrix = m.inverse.transpose;
            int start = _v.Count;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int cPrev = wrap ? (c - 1 + cols) % cols : Mathf.Max(0, c - 1);
                int cNext = wrap ? (c + 1) % cols : Mathf.Min(cols - 1, c + 1);
                int rPrev = Mathf.Max(0, r - 1), rNext = Mathf.Min(rows - 1, r + 1);
                var du = p[r, cNext] - p[r, cPrev];
                var dv = p[rNext, c] - p[rPrev, c];
                var n = Vector3.Cross(du, dv);
                if (n.sqrMagnitude < 1e-12f)
                    n = Vector3.Cross(p[r, cNext] - p[r, c], p[Mathf.Min(rows - 1, r + 1), c] - p[Mathf.Max(0, r - 1), c]);
                if (flip)
                    n = -n;
                _v.Add(m.MultiplyPoint3x4(p[r, c]));
                _n.Add(normalMatrix.MultiplyVector(n).normalized);
                _c.Add(color(r, c));
            }
            int segs = wrap ? cols : cols - 1;
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < segs; c++)
            {
                int c2 = (c + 1) % cols;
                int a = start + r * cols + c, b = start + r * cols + c2, d = start + (r + 1) * cols + c, e = start + (r + 1) * cols + c2;
                Triangle(a, d, b);
                Triangle(b, d, e);
            }
        }

        /// <summary>
        /// Adds a triangle wound so its front face (Unity: clockwise, normal = cross(v1 - v0, v2 - v0)) agrees with the
        /// vertex normals, whatever direction the shape was built in. Degenerate triangles (strand tips) are skipped.
        /// </summary>
        private void Triangle(int i0, int i1, int i2)
        {
            var geo = Vector3.Cross(_v[i1] - _v[i0], _v[i2] - _v[i0]);
            if (geo.sqrMagnitude < 1e-16f)
                return;
            if (Vector3.Dot(geo, _n[i0] + _n[i1] + _n[i2]) >= 0f)
            {
                _t.Add(i0); _t.Add(i1); _t.Add(i2);
            }
            else
            {
                _t.Add(i0); _t.Add(i2); _t.Add(i1);
            }
        }

        private void Fan(Vector3 centre, IList<Vector3> rim, Vector3 normal, Color color, Matrix4x4 m)
        {
            var nm = m.inverse.transpose;
            int c0 = _v.Count;
            _v.Add(m.MultiplyPoint3x4(centre));
            _n.Add(nm.MultiplyVector(normal).normalized);
            _c.Add(color);
            for (int i = 0; i < rim.Count; i++)
            {
                _v.Add(m.MultiplyPoint3x4(rim[i]));
                _n.Add(nm.MultiplyVector(normal).normalized);
                _c.Add(color);
            }
            for (int i = 0; i < rim.Count; i++)
            {
                int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % rim.Count;
                // Orient by the normal.
                var cross = Vector3.Cross(rim[(i + 1) % rim.Count] - centre, rim[i] - centre);
                if (Vector3.Dot(cross, normal) >= 0f) { _t.Add(c0); _t.Add(b); _t.Add(a); }
                else { _t.Add(c0); _t.Add(a); _t.Add(b); }
            }
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>
        /// Smooth tube through elliptical rings (bottom to top, along +Y in part space), optional caps.
        /// <paramref name="color"/> gets (ring index, side index) for stripes and panels.
        /// </summary>
        public void Loft(IList<Ring> rings, int sides, Func<int, int, Color> color, Matrix4x4 m, bool capBottom = true, bool capTop = true)
        {
            var p = new Vector3[rings.Count, sides];
            for (int r = 0; r < rings.Count; r++)
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                p[r, s] = new Vector3(rings[r].X + Mathf.Sin(a) * rings[r].Rx, rings[r].Y, rings[r].Z + Mathf.Cos(a) * rings[r].Rz);
            }
            Grid(p, color, wrap: true, m);
            if (capBottom)
                Cap(p, 0, rings[0], Vector3.down, color(0, 0), m);
            if (capTop)
                Cap(p, rings.Count - 1, rings[rings.Count - 1], Vector3.up, color(rings.Count - 1, 0), m);
        }

        public void Loft(IList<Ring> rings, int sides, Color color, Matrix4x4 m, bool capBottom = true, bool capTop = true) =>
            Loft(rings, sides, (_, __) => color, m, capBottom, capTop);

        private void Cap(Vector3[,] p, int row, Ring ring, Vector3 normal, Color color, Matrix4x4 m)
        {
            int sides = p.GetLength(1);
            var rim = new Vector3[sides];
            for (int s = 0; s < sides; s++)
                rim[s] = p[row, s];
            if (ring.Rx < 1e-4f && ring.Rz < 1e-4f)
                return;
            Fan(new Vector3(ring.X, ring.Y, ring.Z), rim, normal, color, m);
        }

        /// <summary>Ellipsoid (lat x lon grid) with an optional sculpting function on the unit-sphere direction.</summary>
        public void Ellipsoid(Vector3 radii, int lat, int lon, Func<Vector3, Color> color, Matrix4x4 m, Func<Vector3, Vector3> sculpt = null)
        {
            var p = new Vector3[lat + 1, lon];
            var dirs = new Vector3[lat + 1, lon];
            for (int i = 0; i <= lat; i++)
            {
                float v = Mathf.PI * i / lat; // 0 = bottom pole
                for (int j = 0; j < lon; j++)
                {
                    float u = 2f * Mathf.PI * j / lon;
                    var d = new Vector3(Mathf.Sin(v) * Mathf.Sin(u), -Mathf.Cos(v), Mathf.Sin(v) * Mathf.Cos(u));
                    dirs[i, j] = d;
                    var q = Vector3.Scale(d, radii);
                    p[i, j] = sculpt != null ? sculpt(q) : q;
                }
            }
            Grid(p, (r, c) => color(dirs[r, c]), wrap: true, m);
        }

        public void Ellipsoid(Vector3 radii, int lat, int lon, Color color, Matrix4x4 m, Func<Vector3, Vector3> sculpt = null) =>
            Ellipsoid(radii, lat, lon, _ => color, m, sculpt);

        /// <summary>Axis-aligned box (flat faces) in part space.</summary>
        public void Box(Vector3 size, Color color, Matrix4x4 m)
        {
            var h = size * 0.5f;
            var nm = m.inverse.transpose;
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                int s = _v.Count;
                var centre = Vector3.Scale(n, h);
                var du = Vector3.Scale(u, h);
                var dv = Vector3.Scale(v, h);
                foreach (var q in new[] { centre - du - dv, centre + du - dv, centre + du + dv, centre - du + dv })
                {
                    _v.Add(m.MultiplyPoint3x4(q));
                    _n.Add(nm.MultiplyVector(n).normalized);
                    _c.Add(color);
                }
                // Front faces outward (normal = cross(v1 - v0, v2 - v0)).
                Triangle(s, s + 1, s + 2);
                Triangle(s, s + 2, s + 3);
            }
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
        }

        /// <summary>Flat ellipse facing part-space +Z (eyes, emblems, buttons).</summary>
        public void Disc(float rx, float ry, int segments, Color color, Matrix4x4 m)
        {
            var rim = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                rim[i] = new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f);
            }
            Fan(Vector3.zero, rim, Vector3.forward, color, m);
        }

        /// <summary>Ring (torus) around part-space +Y: belts, collars, cuffs, bands.</summary>
        public void Torus(float majorX, float majorZ, float minor, int segments, int sides, Color color, Matrix4x4 m)
        {
            var p = new Vector3[segments + 1, sides];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var centre = new Vector3(Mathf.Sin(a) * majorX, 0f, Mathf.Cos(a) * majorZ);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int j = 0; j < sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    p[i, j] = centre + outward * (Mathf.Cos(b) * minor) + Vector3.up * (Mathf.Sin(b) * minor);
                }
            }
            Grid(p, (_, __) => color, wrap: true, m, flip: true);
        }

        /// <summary>Point on a cubic Bezier.</summary>
        public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        /// <summary>
        /// Tapered strand along a cubic Bezier (hair locks, tails, cables): a flattened 6-sided tube from
        /// <paramref name="rootWidth"/> to a point, colour fading from root to tip.
        /// </summary>
        public void Strand(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float rootWidth, float flatness, Color root, Color tip, Matrix4x4 m,
            int stations = 7, Vector3? side = null)
        {
            const int sides = 6;
            var p = new Vector3[stations + 1, sides];
            var refSide = side ?? Vector3.right;
            for (int i = 0; i <= stations; i++)
            {
                float t = i / (float)stations;
                var centre = Bezier(a, b, c, d, t);
                var tangent = (Bezier(a, b, c, d, Mathf.Min(1f, t + 0.02f)) - Bezier(a, b, c, d, Mathf.Max(0f, t - 0.02f))).normalized;
                var s1 = Vector3.Cross(tangent, Vector3.Cross(refSide, tangent)).normalized;
                if (s1.sqrMagnitude < 0.5f)
                    s1 = Vector3.Cross(tangent, Vector3.up).normalized;
                var s2 = Vector3.Cross(tangent, s1).normalized;
                float w = rootWidth * Mathf.Pow(1f - t, 0.8f) + 0.0015f;
                for (int j = 0; j < sides; j++)
                {
                    float ang = j * Mathf.PI * 2f / sides;
                    p[i, j] = centre + s1 * (Mathf.Cos(ang) * w) + s2 * (Mathf.Sin(ang) * w * flatness);
                }
            }
            Grid(p, (r, _) => Color.Lerp(root, tip, r / (float)stations), wrap: true, m);
        }

        /// <summary>A flat band (thickness <paramref name="thickness"/>) along a polyline: scarf tails, straps, coat panels.</summary>
        public void Band(IList<Vector3> path, IList<float> widths, float thickness, Vector3 widthAxis, Func<int, Color> color, Matrix4x4 m)
        {
            var p = new Vector3[path.Count, 4];
            for (int i = 0; i < path.Count; i++)
            {
                var tangent = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                var across = Vector3.ProjectOnPlane(widthAxis, tangent).normalized * (widths[i] * 0.5f);
                var normal = Vector3.Cross(tangent, across).normalized * (thickness * 0.5f);
                p[i, 0] = path[i] - across - normal;
                p[i, 1] = path[i] + across - normal;
                p[i, 2] = path[i] + across + normal;
                p[i, 3] = path[i] - across + normal;
            }
            Grid(p, (r, _) => color(r), wrap: true, m);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
