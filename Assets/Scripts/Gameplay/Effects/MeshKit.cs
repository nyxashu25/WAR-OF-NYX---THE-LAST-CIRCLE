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
    /// Surface material ids read by WarOfNyx/NyxToonVC (mesh uv2.x): each one has its own lighting
    /// (specular, sheen, reflections, hair highlights) and, for fabrics, a woven micro-pattern.
    /// </summary>
    public static class Surf
    {
        public const float Cloth = 0f;     // plain matte cloth with a soft sheen
        public const float Skin = 1f;      // warm, soft shadow edge
        public const float Fabric = 2f;    // woven fabric: pattern from Weave
        public const float Leather = 3f;   // grain + glossy highlight
        public const float Metal = 4f;     // brushed metal, tinted reflections
        public const float Polymer = 5f;   // hard armour plate with a clear coat
        public const float Hair = 6f;      // anisotropic (strand-direction) highlight bands
        public const float Carbon = 7f;    // carbon-fibre twill under a clear coat
        public const float Rubber = 8f;    // dull soles, grips
        public const float Fur = 9f;       // soft, strand-direction sheen
        public const float Circuit = 10f;  // glowing lines with light flowing along them
        public const float Eye = 11f;      // wet, very glossy
        public const float Gold = 12f;     // polished metal (brass, gold)
        public const float Lacquer = 13f;  // glossy paint (scabbard)
    }

    /// <summary>Woven micro-patterns for <see cref="Surf.Fabric"/> (mesh uv2.w).</summary>
    public static class Weave
    {
        public const float Ripstop = 0f;   // tactical grid
        public const float Twill = 1f;     // diagonal ribs (trousers, coats)
        public const float Rib = 2f;       // knit ribs (cuffs, undersuits)
        public const float Quilt = 3f;     // stitched diamond padding
        public const float Hex = 4f;       // hex mesh (bodysuits)
        public const float Plain = 5f;     // fine plain weave
    }

    /// <summary>
    /// Procedural mesh building for detailed PLACEHOLDER characters and gear: smooth lofts and resampled surfaces
    /// with folds (torso, limbs, sleeves), sculptable ellipsoids (heads, muscles, armour), bevelled boxes (pouches,
    /// plates), tubes (cables, pistons), tapered Bezier strands (hair), flat ribbons (scarves, straps), discs and
    /// rings. Every vertex carries a colour (rgb) and a glow amount (1 - alpha), and in uv2 its surface material
    /// (<see cref="Surf"/>), ambient occlusion, outline weight and weave pattern, for WarOfNyx/NyxToonVC. uv0 is in
    /// metres along the surface (fabric patterns keep their real size); tangents follow the length of each shape
    /// (strand direction for hair highlights). <see cref="Resolution"/> scales the segment counts (detail levels).
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Color> _c = new List<Color>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<Vector4> _surf = new List<Vector4>();
        private readonly List<Vector4> _tan = new List<Vector4>();
        private readonly List<BoneWeight> _w = new List<BoneWeight>();
        private readonly List<List<int>> _submeshes = new List<List<int>> { new List<int>() };
        private List<int> _t;
        private bool _skinned;

        public MeshKit() => _t = _submeshes[0];

        /// <summary>
        /// Skinning: when set, every vertex added gets the bone weights this returns for its (bind-pose) position, and
        /// <see cref="ToMesh"/> writes them (skinned characters). Sticky; change it per part (arm, leg, coat panel...).
        /// </summary>
        public Func<Vector3, BoneWeight> Skinning;

        /// <summary>Triangles that follow go to this submesh (a second material, e.g. the painted face).</summary>
        public void UseSubmesh(int index)
        {
            while (_submeshes.Count <= index)
                _submeshes.Add(new List<int>());
            _t = _submeshes[index];
        }

        /// <summary>Adds one vertex with explicit uvs and material data; returns its index (decals, custom patches).</summary>
        public int AddVertex(Vector3 position, Vector3 normal, Color color, Vector2 uv, Vector4 surf)
        {
            Add(position, normal.normalized, color, uv, TangentOf(Vector3.up, Vector3.right, normal.normalized));
            _surf[_surf.Count - 1] = surf;
            return _v.Count - 1;
        }

        /// <summary>Adds a triangle oriented by its vertices' normals.</summary>
        public void AddTriangle(int a, int b, int c) => Triangle(a, b, c);

        /// <summary>Replaces the normals of the vertices from <paramref name="from"/> on (e.g. smooth anime face shading).</summary>
        public void OverrideNormals(int from, Func<Vector3, Vector3, Vector3> normal)
        {
            for (int i = from; i < _v.Count; i++)
                _n[i] = normal(_v[i], _n[i]).normalized;
            // Keep every affected triangle's front face on the side of its (new) normals.
            foreach (var tris in _submeshes)
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    if (a < from && b < from && c < from)
                        continue;
                    var geo = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
                    if (Vector3.Dot(geo, _n[a] + _n[b] + _n[c]) < 0f)
                    {
                        tris[t + 1] = c;
                        tris[t + 2] = b;
                    }
                }
        }

        /// <summary>Positions of the vertices added so far (read only; e.g. to fit decals to a surface).</summary>
        public Vector3 VertexAt(int i) => _v[i];

        /// <summary>Surface material for the shapes that follow (sticky until changed).</summary>
        public float Material = Surf.Cloth;
        /// <summary>Weave pattern for fabric (sticky).</summary>
        public float Pattern = Weave.Plain;
        /// <summary>Outline weight for the shapes that follow: 0 for tiny details (stitches, rivets) so they stay crisp.</summary>
        public float Outline = 1f;
        /// <summary>Segment-count multiplier (detail level): 1 = normal, &gt; 1 smoother, &lt; 1 cheaper.</summary>
        public float Resolution = 1f;

        public int VertexCount => _v.Count;

        /// <summary>Glow is stored as 1 - alpha, so ordinary colours (alpha 1) never glow by accident.</summary>
        public static Color Glow(Color c, float amount) => new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01(amount));

        public static Color Solid(Color c) => new Color(c.r, c.g, c.b, 1f);

        public static Matrix4x4 TRS(Vector3 position, Vector3 euler, Vector3 scale) => Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);

        public static Matrix4x4 At(Vector3 position, Vector3 euler = default) => Matrix4x4.TRS(position, Quaternion.Euler(euler), Vector3.one);

        /// <summary>Sets the material (and weave) for the shapes that follow.</summary>
        public MeshKit Use(float surface, float pattern = Weave.Plain, float outline = 1f)
        {
            Material = surface;
            Pattern = pattern;
            Outline = outline;
            return this;
        }

        /// <summary>A segment count scaled by <see cref="Resolution"/>.</summary>
        public int Res(int n, int min = 3) => Mathf.Max(min, Mathf.RoundToInt(n * Resolution));

        private void Add(Vector3 position, Vector3 normal, Color color, Vector2 uv, Vector4 tangent, float ao = 1f, Vector2? material = null)
        {
            _v.Add(position);
            _n.Add(normal);
            _c.Add(color);
            _uv.Add(uv);
            _tan.Add(tangent);
            var mat = material ?? new Vector2(Material, Pattern);
            _surf.Add(new Vector4(mat.x, Mathf.Clamp01(ao), Outline, mat.y));
            if (Skinning != null)
            {
                _skinned = true;
                _w.Add(Skinning(position));
            }
            else
            {
                _w.Add(default);
            }
        }

        private static Vector4 TangentOf(Vector3 along, Vector3 across, Vector3 normal)
        {
            var t = Vector3.ProjectOnPlane(along, normal);
            if (t.sqrMagnitude < 1e-12f)
                t = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (t.sqrMagnitude < 1e-12f)
                t = Vector3.ProjectOnPlane(Vector3.right, normal);
            t.Normalize();
            float w = Vector3.Dot(Vector3.Cross(normal, t), across) >= 0f ? 1f : -1f;
            return new Vector4(t.x, t.y, t.z, w);
        }

        // ------------------------------------------------------------------ core: a grid of points with smooth normals

        /// <summary>
        /// Triangulates a rows x cols grid (cols wrap when <paramref name="wrap"/>; the seam column is duplicated so the
        /// metre-based uvs stay continuous), smooth normals from neighbours, tangents along the rows' direction.
        /// </summary>
        public void Grid(Vector3[,] p, Func<int, int, Color> color, bool wrap, Matrix4x4 m, bool flip = false, Func<int, int, float> ao = null,
            Func<int, int, Vector2> material = null)
        {
            int rows = p.GetLength(0), cols = p.GetLength(1);
            var normalMatrix = m.inverse.transpose;
            int outCols = wrap ? cols + 1 : cols;
            var normals = new Vector3[rows, cols];
            var tangents = new Vector3[rows, cols];
            var acrosses = new Vector3[rows, cols];
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
                normals[r, c] = n;
                tangents[r, c] = dv.sqrMagnitude > 1e-12f ? dv : Vector3.Cross(n, du);
                acrosses[r, c] = du;
            }

            // Metre uvs: u along each row (around), v along each column (length).
            var u = new float[rows, outCols];
            var v = new float[rows, outCols];
            for (int r = 0; r < rows; r++)
                for (int c = 1; c < outCols; c++)
                    u[r, c] = u[r, c - 1] + Vector3.Distance(m.MultiplyPoint3x4(p[r, c % cols]), m.MultiplyPoint3x4(p[r, c - 1]));
            for (int c = 0; c < outCols; c++)
                for (int r = 1; r < rows; r++)
                    v[r, c] = v[r - 1, c] + Vector3.Distance(m.MultiplyPoint3x4(p[r, c % cols]), m.MultiplyPoint3x4(p[r - 1, c % cols]));

            int start = _v.Count;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < outCols; c++)
            {
                int cc = c % cols;
                var n = normalMatrix.MultiplyVector(normals[r, cc]).normalized;
                var tangent = TangentOf(m.MultiplyVector(tangents[r, cc]), m.MultiplyVector(acrosses[r, cc]), n);
                Add(m.MultiplyPoint3x4(p[r, cc]), n, color(r, cc), new Vector2(u[r, c], v[r, c]), tangent, ao != null ? ao(r, cc) : 1f,
                    material != null ? material(r, cc) : (Vector2?)null);
            }
            int segs = outCols - 1;
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < segs; c++)
            {
                int a = start + r * outCols + c, b = start + r * outCols + c + 1, d = start + (r + 1) * outCols + c, e = start + (r + 1) * outCols + c + 1;
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
            var n = nm.MultiplyVector(normal).normalized;
            var axisU = Vector3.ProjectOnPlane(rim[0] - centre, normal).normalized;
            if (axisU.sqrMagnitude < 0.5f)
                axisU = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            var axisV = Vector3.Cross(normal, axisU);
            var tangent = TangentOf(m.MultiplyVector(axisV), m.MultiplyVector(axisU), n);
            int c0 = _v.Count;
            Add(m.MultiplyPoint3x4(centre), n, color, Vector2.zero, tangent);
            for (int i = 0; i < rim.Count; i++)
            {
                var d = rim[i] - centre;
                Add(m.MultiplyPoint3x4(rim[i]), n, color, new Vector2(Vector3.Dot(d, axisU), Vector3.Dot(d, axisV)), tangent);
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
        /// <paramref name="color"/> gets (ring index, side index) for stripes and panels; the side count is used as given.
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

        /// <summary>
        /// Smooth surface of revolution for clothing and anatomy: the rings are resampled every <paramref name="step"/>
        /// metres (Catmull-Rom, 0 = as given), the side count scales with <see cref="Resolution"/>, and an optional radial
        /// <paramref name="displace"/>(y, angle) carves folds, pleats, quilting and panel grooves (deeper = darker: baked
        /// occlusion). Colour by (y, angle in degrees: 0 = front +Z, 90 = right +X, ±180 = back); an optional
        /// <paramref name="material"/>(y, angle) = (Surf, Weave) mixes materials in one surface (panels are flat-shaded per triangle).
        /// </summary>
        public void Surface(IList<Ring> rings, int sides, Func<float, float, Color> color, Matrix4x4 m, float step = 0f,
            Func<float, float, float> displace = null, bool capBottom = false, bool capTop = false, float occlusion = 28f,
            Func<float, float, Vector2> material = null, float arcFrom = 0f, float arcTo = 360f, bool inward = false)
        {
            var rs = step > 0f ? Resample(rings, step / Mathf.Max(0.5f, Resolution)) : rings;
            bool partial = arcTo - arcFrom < 359.9f;
            int n = partial ? Res(Mathf.Max(2, Mathf.RoundToInt(sides * (arcTo - arcFrom) / 360f)), 2) + 1 : Res(sides, 6);
            var p = new Vector3[rs.Count, n];
            var ao = new float[rs.Count, n];
            var cols = new Color[rs.Count, n];
            var mats = material != null ? new Vector2[rs.Count, n] : null;
            for (int r = 0; r < rs.Count; r++)
            for (int s = 0; s < n; s++)
            {
                float a = partial ? Mathf.Lerp(arcFrom, arcTo, s / (float)(n - 1)) * Mathf.Deg2Rad : s * Mathf.PI * 2f / n;
                var ring = rs[r];
                var outward = new Vector3(Mathf.Sin(a) / Mathf.Max(1e-4f, ring.Rx), 0f, Mathf.Cos(a) / Mathf.Max(1e-4f, ring.Rz)).normalized;
                float d = displace != null ? displace(ring.Y, a * Mathf.Rad2Deg) : 0f;
                p[r, s] = new Vector3(ring.X + Mathf.Sin(a) * ring.Rx, ring.Y, ring.Z + Mathf.Cos(a) * ring.Rz) + outward * d;
                ao[r, s] = 1f + Mathf.Min(0f, d) * occlusion;
                float deg = Mathf.Repeat(a * Mathf.Rad2Deg + 180f, 360f) - 180f;
                cols[r, s] = color(ring.Y, deg);
                if (mats != null)
                    mats[r, s] = material(ring.Y, deg);
            }
            Grid(p, (r, s) => cols[r, s], wrap: !partial, m, flip: inward, ao: (r, s) => ao[r, s], material: mats != null ? (r, s) => mats[r, s] : (Func<int, int, Vector2>)null);
            if (partial)
                return;
            if (capBottom)
                Cap(p, 0, rs[0], Vector3.down, cols[0, 0], m);
            if (capTop)
                Cap(p, rs.Count - 1, rs[rs.Count - 1], Vector3.up, cols[rs.Count - 1, 0], m);
        }

        public void Surface(IList<Ring> rings, int sides, Color color, Matrix4x4 m, float step = 0f, Func<float, float, float> displace = null,
            bool capBottom = false, bool capTop = false, float occlusion = 28f, Func<float, float, Vector2> material = null, float arcFrom = 0f, float arcTo = 360f,
            bool inward = false) =>
            Surface(rings, sides, (_, __) => color, m, step, displace, capBottom, capTop, occlusion, material, arcFrom, arcTo, inward);

        /// <summary>Catmull-Rom resampling of a ring profile: one ring every <paramref name="step"/> metres of height.</summary>
        public static List<Ring> Resample(IList<Ring> rings, float step)
        {
            var result = new List<Ring>();
            if (rings.Count < 2 || step <= 0f)
            {
                result.AddRange(rings);
                return result;
            }
            float CR(float p0, float p1, float p2, float p3, float t) =>
                0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
            for (int i = 0; i < rings.Count - 1; i++)
            {
                var r0 = rings[Mathf.Max(0, i - 1)];
                var r1 = rings[i];
                var r2 = rings[i + 1];
                var r3 = rings[Mathf.Min(rings.Count - 1, i + 2)];
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(r2.Y - r1.Y) / step));
                for (int k = 0; k < pieces; k++)
                {
                    float t = k / (float)pieces;
                    result.Add(new Ring(Mathf.Lerp(r1.Y, r2.Y, t),
                        Mathf.Max(0f, CR(r0.Rx, r1.Rx, r2.Rx, r3.Rx, t)), Mathf.Max(0f, CR(r0.Rz, r1.Rz, r2.Rz, r3.Rz, t)),
                        CR(r0.X, r1.X, r2.X, r3.X, t), CR(r0.Z, r1.Z, r2.Z, r3.Z, t)));
                }
            }
            result.Add(rings[rings.Count - 1]);
            return result;
        }

        /// <summary>A ring of <paramref name="rings"/> at height <paramref name="y"/> (linear), for placing details on a surface.</summary>
        public static Ring RingAt(IList<Ring> rings, float y)
        {
            if (y <= rings[0].Y)
                return rings[0];
            for (int i = 0; i < rings.Count - 1; i++)
            {
                if (y > rings[i + 1].Y)
                    continue;
                float t = Mathf.InverseLerp(rings[i].Y, rings[i + 1].Y, y);
                return new Ring(y, Mathf.Lerp(rings[i].Rx, rings[i + 1].Rx, t), Mathf.Lerp(rings[i].Rz, rings[i + 1].Rz, t),
                    Mathf.Lerp(rings[i].X, rings[i + 1].X, t), Mathf.Lerp(rings[i].Z, rings[i + 1].Z, t));
            }
            return rings[rings.Count - 1];
        }

        /// <summary>Point on a ring profile's surface at height y and angle (degrees, 0 = front), lifted <paramref name="lift"/> metres outward.</summary>
        public static Vector3 OnRings(IList<Ring> rings, float y, float angle, float lift = 0f)
        {
            var ring = RingAt(rings, y);
            float a = angle * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a) / Mathf.Max(1e-4f, ring.Rx), 0f, Mathf.Cos(a) / Mathf.Max(1e-4f, ring.Rz)).normalized;
            return new Vector3(ring.X + Mathf.Sin(a) * ring.Rx, y, ring.Z + Mathf.Cos(a) * ring.Rz) + outward * lift;
        }

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

        /// <summary>Ellipsoid (lat x lon grid, scaled by <see cref="Resolution"/>) with an optional sculpting function on the unit-sphere direction.</summary>
        public void Ellipsoid(Vector3 radii, int lat, int lon, Func<Vector3, Color> color, Matrix4x4 m, Func<Vector3, Vector3> sculpt = null)
        {
            lat = Res(lat, 4);
            lon = Res(lon, 6);
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
                var wn = nm.MultiplyVector(n).normalized;
                var tangent = TangentOf(m.MultiplyVector(v), m.MultiplyVector(u), wn);
                float su = du.magnitude, sv = dv.magnitude;
                var corners = new[] { centre - du - dv, centre + du - dv, centre + du + dv, centre - du + dv };
                var uvs = new[] { new Vector2(-su, -sv), new Vector2(su, -sv), new Vector2(su, sv), new Vector2(-su, sv) };
                for (int i = 0; i < 4; i++)
                    Add(m.MultiplyPoint3x4(corners[i]), wn, color, uvs[i], tangent);
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

        /// <summary>
        /// Box with rounded (bevelled) edges and corners: hard-surface parts catch a highlight along every edge.
        /// Falls back to a plain box at low <see cref="Resolution"/>.
        /// </summary>
        public void RoundBox(Vector3 size, float radius, Color color, Matrix4x4 m)
        {
            var h = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            if (Resolution < 0.6f || r < 2e-4f)
            {
                Box(size, color, m);
                return;
            }
            // Two bevel steps only on large parts at high detail; small parts read the same with one.
            int steps = Resolution >= 1.2f && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 0.06f ? 2 : 1;
            var inner = h - Vector3.one * r;
            var nm = m.inverse.transpose;
            float[] Axis(float half)
            {
                var list = new List<float>();
                for (int i = 0; i <= steps; i++)
                    list.Add(-half + r * i / steps);
                for (int i = 0; i <= steps; i++)
                    list.Add(half - r + r * i / steps);
                return list.ToArray();
            }
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                float hu = Mathf.Abs(Vector3.Dot(h, u)), hv = Mathf.Abs(Vector3.Dot(h, v)), hn = Mathf.Abs(Vector3.Dot(h, n));
                var au = Axis(hu);
                var av = Axis(hv);
                int start = _v.Count;
                for (int i = 0; i < av.Length; i++)
                for (int j = 0; j < au.Length; j++)
                {
                    var q = n * hn + u * au[j] + v * av[i];
                    var clamped = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                    var dir = q - clamped;
                    dir = dir.sqrMagnitude > 1e-12f ? dir.normalized : n;
                    var pos = clamped + dir * r;
                    var wn = nm.MultiplyVector(dir).normalized;
                    Add(m.MultiplyPoint3x4(pos), wn, color, new Vector2(au[j], av[i]), TangentOf(m.MultiplyVector(v), m.MultiplyVector(u), wn));
                }
                int w = au.Length;
                for (int i = 0; i < av.Length - 1; i++)
                for (int j = 0; j < w - 1; j++)
                {
                    int a = start + i * w + j, b = a + 1, c = a + w, d = c + 1;
                    Triangle(a, b, d);
                    Triangle(a, d, c);
                }
            }
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
        }

        /// <summary>A small flat quad lying on a surface (stitches, scratches, tacks): 4 vertices facing <paramref name="normal"/>.</summary>
        public void Dash(Vector3 a, Vector3 b, Vector3 normal, float width, Color color, Matrix4x4 m)
        {
            var dir = b - a;
            float len = dir.magnitude;
            var across = Vector3.Cross(normal, dir).normalized * (width * 0.5f);
            if (len < 1e-6f || across.sqrMagnitude < 1e-12f)
                return;
            var n = m.inverse.transpose.MultiplyVector(normal).normalized;
            var tangent = TangentOf(m.MultiplyVector(dir), m.MultiplyVector(across), n);
            int s = _v.Count;
            Add(m.MultiplyPoint3x4(a - across), n, color, new Vector2(0f, 0f), tangent);
            Add(m.MultiplyPoint3x4(a + across), n, color, new Vector2(width, 0f), tangent);
            Add(m.MultiplyPoint3x4(b + across), n, color, new Vector2(width, len), tangent);
            Add(m.MultiplyPoint3x4(b - across), n, color, new Vector2(0f, len), tangent);
            Triangle(s, s + 1, s + 2);
            Triangle(s, s + 2, s + 3);
        }

        /// <summary>Flat ellipse facing part-space +Z (eyes, emblems, buttons).</summary>
        public void Disc(float rx, float ry, int segments, Color color, Matrix4x4 m)
        {
            segments = Res(segments, 5);
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
            segments = Res(segments, 6);
            sides = Res(sides, 3);
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
            stations = Res(stations, 3);
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

        /// <summary>Round tube of constant radius along a polyline (cables, hoses, piping, pistons), parallel-transported frames.</summary>
        public void Tube(IList<Vector3> path, float radius, Color color, Matrix4x4 m, int sides = 8)
        {
            sides = Res(sides, 4);
            var p = new Vector3[path.Count, sides];
            var first = (path[1] - path[0]).normalized;
            var normal = Vector3.Cross(first, Mathf.Abs(first.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < path.Count; i++)
            {
                var tangent = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    p[i, j] = path[i] + (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * radius;
                }
            }
            Grid(p, (_, __) => color, wrap: true, m);
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
            mesh.SetUVs(0, _uv);
            mesh.SetUVs(1, _surf);
            mesh.SetTangents(_tan);
            mesh.subMeshCount = _submeshes.Count;
            for (int i = 0; i < _submeshes.Count; i++)
                mesh.SetTriangles(_submeshes[i], i);
            if (_skinned)
                mesh.boneWeights = _w.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
