using UnityEngine;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER weapon models (Phase 10 "improve weapon models"): a distinct silhouette per catalog
    /// weapon built from cel-shaded parts, so players can tell an SMG from a marksman rifle at a glance.
    /// Returns the muzzle point for tracers and flashes. Visual only.
    /// </summary>
    public static class WeaponModelFactory
    {
        private static readonly Color Metal = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color Grip = new Color(0.1f, 0.09f, 0.12f);

        public static Transform Build(WeaponStats weapon, Transform hand, out Vector3 muzzleLocal)
        {
            var root = new GameObject("Model (PLACEHOLDER)").transform;
            root.SetParent(hand, false);
            muzzleLocal = new Vector3(0f, 0.02f, 0.55f);
            if (weapon == null)
                return root;

            var accent = AccentFor(weapon);
            var glow = accent * 1.5f;
            switch (weapon.id)
            {
                case WeaponCatalog.Nx7AssaultRifle:
                    Receiver(root, 0.42f, 0.12f, accent, glow);
                    Barrel(root, 0.2f, 0.025f, 0.41f);
                    Mag(root, 0.15f, 0.16f, 15f);
                    Stock(root, 0.2f);
                    StyleKit.Part(PrimitiveType.Cube, "Sight", root, new Vector3(0f, 0.09f, 0.16f), new Vector3(0.04f, 0.04f, 0.1f), Metal);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.62f);
                    break;
                case WeaponCatalog.ShadowSmg:
                case WeaponCatalog.StormLightSmg:
                    Receiver(root, 0.3f, 0.11f, accent, glow);
                    Barrel(root, 0.12f, 0.022f, 0.3f);
                    Mag(root, 0.1f, 0.22f, 0f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.43f);
                    break;
                case WeaponCatalog.CrimsonShotgun:
                    Receiver(root, 0.45f, 0.13f, accent, glow);
                    Barrel(root, 0.3f, 0.04f, 0.47f);
                    StyleKit.Part(PrimitiveType.Cube, "Pump", root, new Vector3(0f, -0.06f, 0.42f), new Vector3(0.07f, 0.06f, 0.18f), accent * 0.8f);
                    Stock(root, 0.22f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.78f);
                    break;
                case WeaponCatalog.PhantomMarksman:
                    Receiver(root, 0.5f, 0.11f, accent, glow);
                    Barrel(root, 0.45f, 0.022f, 0.5f);
                    StyleKit.Part(PrimitiveType.Cylinder, "Scope", root, new Vector3(0f, 0.1f, 0.2f), new Vector3(0.05f, 0.12f, 0.05f), Metal,
                        new Vector3(90f, 0f, 0f));
                    StyleKit.Part(PrimitiveType.Sphere, "Lens", root, new Vector3(0f, 0.1f, 0.33f), Vector3.one * 0.045f, accent, default, glow);
                    Mag(root, 0.12f, 0.1f, 0f);
                    Stock(root, 0.26f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.95f);
                    break;
                case WeaponCatalog.StormBurstRifle:
                    Receiver(root, 0.4f, 0.13f, accent, glow);
                    Barrel(root, 0.22f, 0.026f, 0.4f);
                    Mag(root, 0.14f, 0.14f, 10f);
                    for (int i = 0; i < 3; i++)
                        StyleKit.Part(PrimitiveType.Cube, "Stripe", root, new Vector3(0.045f, 0.02f, 0.08f + i * 0.08f), new Vector3(0.01f, 0.08f, 0.03f),
                            accent, default, glow);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.63f);
                    break;
                case WeaponCatalog.NeonPistol:
                    StyleKit.Part(PrimitiveType.Cube, "Slide", root, new Vector3(0f, 0.03f, 0.08f), new Vector3(0.05f, 0.07f, 0.22f), Metal);
                    StyleKit.Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.07f, 0f), new Vector3(0.045f, 0.14f, 0.06f), Grip,
                        new Vector3(-15f, 0f, 0f));
                    StyleKit.Part(PrimitiveType.Cube, "Neon", root, new Vector3(0.027f, 0.03f, 0.08f), new Vector3(0.005f, 0.02f, 0.18f), accent, default, glow);
                    muzzleLocal = new Vector3(0f, 0.04f, 0.2f);
                    break;
                default:
                    if (weapon.IsMelee)
                        BuildBlade(root, weapon, accent, glow);
                    else
                    {
                        Receiver(root, 0.4f, 0.12f, accent, glow);
                        Barrel(root, 0.2f, 0.025f, 0.4f);
                    }
                    break;
            }
            return root;
        }

        private static readonly Color Steel = new Color(0.62f, 0.66f, 0.76f);
        private static readonly Color Edge = new Color(0.94f, 0.96f, 1f);
        private static readonly Color Gold = new Color(0.78f, 0.6f, 0.28f);

        /// <summary>
        /// Melee weapons, held at the origin (hand) with the blade along +Y and the cutting edge facing +Z:
        ///  * katana: wrapped handle with diamond pattern and pommel cap, round tsuba, habaki collar and a
        ///    curved two-tone blade (steel body, polished edge) with an angled kissaki tip;
        ///  * knife / dagger: tanto-style single-edged blade, guard, ribbed grip;
        ///  * energy blade: emitter hilt and a glowing blade with a bright core.
        /// A "TrailTip" child at the blade tip anchors the slash trail.
        /// </summary>
        private static void BuildBlade(Transform root, WeaponStats weapon, Color accent, Color glow)
        {
            bool katana = weapon.id == WeaponCatalog.Katana;
            bool energy = weapon.id == WeaponCatalog.EnergyBlade;
            bool knife = !katana && !energy;
            float handle = katana ? 0.27f : energy ? 0.22f : 0.12f;
            float length = katana ? 0.74f : energy ? 0.72f : weapon.id == WeaponCatalog.CombatKnife ? 0.24f : 0.19f;
            float width = katana ? 0.042f : energy ? 0.046f : 0.034f;
            float curve = katana ? 0.035f : energy ? 0f : 0.004f;
            float bladeStart = 0.115f;

            // Handle: the hand holds the top of it (origin); the second hand lower down (two-handed weapons).
            float handleTop = 0.1f, handleBottom = handleTop - handle;
            StyleKit.Part(PrimitiveType.Cylinder, "Handle", root, new Vector3(0f, (handleTop + handleBottom) * 0.5f, 0f),
                new Vector3(0.036f, handle * 0.5f, 0.03f), Grip);
            int wraps = Mathf.RoundToInt(handle / 0.032f);
            for (int i = 0; i < wraps; i++)
            {
                // Diamond wrap (tsuka-ito) / ribbed grip.
                float y = handleTop - 0.016f - i * (handle / wraps);
                StyleKit.Part(PrimitiveType.Cube, "Wrap", root, new Vector3(0f, y, 0f), new Vector3(0.039f, 0.012f, 0.034f),
                    knife ? Metal : accent * 0.55f, new Vector3(0f, 45f, 0f));
            }
            StyleKit.Part(PrimitiveType.Cylinder, "Pommel", root, new Vector3(0f, handleBottom - 0.008f, 0f), new Vector3(0.042f, 0.01f, 0.036f), energy ? Metal : Gold);

            if (energy)
            {
                StyleKit.Part(PrimitiveType.Cylinder, "Emitter", root, new Vector3(0f, 0.1f, 0f), new Vector3(0.05f, 0.025f, 0.05f), Metal);
                StyleKit.Part(PrimitiveType.Cube, "EmitterRing", root, new Vector3(0f, 0.08f, 0f), new Vector3(0.055f, 0.008f, 0.055f), accent, default, glow);
            }
            else if (katana)
            {
                StyleKit.Part(PrimitiveType.Cylinder, "Tsuba", root, new Vector3(0f, 0.105f, 0f), new Vector3(0.085f, 0.006f, 0.075f), new Color(0.12f, 0.11f, 0.14f));
                StyleKit.Part(PrimitiveType.Cylinder, "TsubaRim", root, new Vector3(0f, 0.105f, 0f), new Vector3(0.09f, 0.004f, 0.08f), Gold);
                StyleKit.Part(PrimitiveType.Cube, "Habaki", root, new Vector3(0f, bladeStart + 0.012f, 0.002f), new Vector3(0.012f, 0.026f, width + 0.006f), Gold);
            }
            else
            {
                StyleKit.Part(PrimitiveType.Cube, "Guard", root, new Vector3(0f, 0.105f, 0f), new Vector3(0.022f, 0.014f, 0.085f), Metal);
            }

            // Blade: two meshes (spine side and edge side) so the polished edge reads as a separate bright line.
            var body = BladeMeshes(length, width, curve, knife, out var edge);
            BladePart(root, "Blade", body, bladeStart, energy ? accent : Steel, energy ? glow * 1.2f : default);
            BladePart(root, "BladeEdge", edge, bladeStart, energy ? Color.white : Edge, energy ? glow * 2f : Edge * 0.15f);

            var tip = new GameObject("TrailTip").transform;
            tip.SetParent(root, false);
            tip.localPosition = new Vector3(0f, bladeStart + length * 0.92f, -curve * 0.85f);
        }

        private static void BladePart(Transform root, string name, Mesh mesh, float y, Color color, Color emission)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StyleKit.Toon;
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            StyleKit.Paint(renderer, color, emission);
        }

        private static readonly System.Collections.Generic.Dictionary<string, (Mesh body, Mesh edge)> BladeCache =
            new System.Collections.Generic.Dictionary<string, (Mesh, Mesh)>();

        /// <summary>
        /// A single-edged blade along +Y (length), edge toward +Z, spine toward -Z, curving back toward the spine
        /// (<paramref name="curve"/> metres at the tip). Cross-section: spine flats, a ridge line (shinogi) and
        /// the edge bevel; the last 12 % angles into the tip (kissaki / tanto point).
        /// </summary>
        private static Mesh BladeMeshes(float length, float width, float curve, bool tanto, out Mesh edgeMesh)
        {
            string key = $"{length:0.000}_{width:0.000}_{curve:0.000}_{tanto}";
            if (BladeCache.TryGetValue(key, out var cached) && cached.body != null)
            {
                edgeMesh = cached.edge;
                return cached.body;
            }
            const int stations = 14;
            float thickness = width * 0.22f;
            var spineVerts = new System.Collections.Generic.List<Vector3>();
            var edgeVerts = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= stations; i++)
            {
                float t = i / (float)stations;
                float y = t * length;
                float back = -curve * t * t;                        // curvature toward the spine
                float tipT = Mathf.InverseLerp(tanto ? 0.8f : 0.88f, 1f, t);
                float w = Mathf.Lerp(width, width * 0.72f, t) * (1f - tipT * 0.97f);
                float edgeZ = back + w * 0.5f;
                float spineZ = back - w * 0.5f + w * tipT * 0.9f;    // the spine sweeps down into the point
                float ridgeZ = Mathf.Lerp(spineZ, edgeZ, 0.45f);
                float th = thickness * (1f - tipT * 0.8f);
                // Spine side: spine flat (two points) to the ridge (two points).
                spineVerts.Add(new Vector3(-th * 0.45f, y, spineZ));
                spineVerts.Add(new Vector3(th * 0.45f, y, spineZ));
                spineVerts.Add(new Vector3(th * 0.5f, y, ridgeZ));
                spineVerts.Add(new Vector3(-th * 0.5f, y, ridgeZ));
                // Edge side: ridge to the cutting edge.
                edgeVerts.Add(new Vector3(th * 0.5f, y, ridgeZ));
                edgeVerts.Add(new Vector3(0f, y, edgeZ));
                edgeVerts.Add(new Vector3(-th * 0.5f, y, ridgeZ));
            }
            var body = Strip(spineVerts, 4, stations, closedLoop: true, "BladeBody");
            edgeMesh = Strip(edgeVerts, 3, stations, closedLoop: false, "BladeEdge");
            BladeCache[key] = (body, edgeMesh);
            return body;
        }

        /// <summary>Joins consecutive cross-sections of <paramref name="ring"/> points into quads; flat shaded.</summary>
        private static Mesh Strip(System.Collections.Generic.List<Vector3> points, int ring, int stations, bool closedLoop, string name)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            int segments = closedLoop ? ring : ring - 1;
            for (int s = 0; s < stations; s++)
            for (int j = 0; j < segments; j++)
            {
                int j2 = (j + 1) % ring;
                var a = points[s * ring + j];
                var b = points[s * ring + j2];
                var c = points[(s + 1) * ring + j2];
                var d = points[(s + 1) * ring + j];
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f)
                    n = Vector3.Cross(c - a, d - a);
                n.Normalize();
                // Two-sided with separate vertices per side (shared vertices would average the normals to zero).
                int i0 = verts.Count;
                verts.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { n, n, n, n });
                tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                int i1 = verts.Count;
                verts.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { -n, -n, -n, -n });
                tris.AddRange(new[] { i1, i1 + 2, i1 + 1, i1, i1 + 3, i1 + 2 });
            }
            var mesh = new Mesh { name = name + " (procedural)" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Receiver(Transform root, float length, float height, Color accent, Color glow)
        {
            StyleKit.Part(PrimitiveType.Cube, "Receiver", root, new Vector3(0f, 0f, length * 0.4f), new Vector3(0.08f, height, length), Metal);
            StyleKit.Part(PrimitiveType.Cube, "Accent", root, new Vector3(0.042f, 0.01f, length * 0.4f), new Vector3(0.006f, height * 0.25f, length * 0.8f),
                accent, default, glow);
            StyleKit.Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.09f, 0.02f), new Vector3(0.05f, 0.13f, 0.06f), Grip,
                new Vector3(-15f, 0f, 0f));
        }

        private static void Barrel(Transform root, float length, float radius, float start)
        {
            StyleKit.Part(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.02f, start + length * 0.5f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), Metal, new Vector3(90f, 0f, 0f));
        }

        private static void Mag(Transform root, float z, float height, float tilt)
        {
            StyleKit.Part(PrimitiveType.Cube, "Magazine", root, new Vector3(0f, -0.06f - height * 0.4f, z), new Vector3(0.05f, height, 0.07f), Grip,
                new Vector3(tilt, 0f, 0f));
        }

        private static void Stock(Transform root, float length)
        {
            StyleKit.Part(PrimitiveType.Cube, "Stock", root, new Vector3(0f, -0.02f, -length * 0.5f), new Vector3(0.06f, 0.1f, length), Metal);
        }

        private static Color AccentFor(WeaponStats weapon)
        {
            switch (weapon.rarity)
            {
                case Core.Items.Rarity.Legendary: return new Color(1f, 0.78f, 0.34f);
                case Core.Items.Rarity.Epic: return new Color(0.62f, 0.25f, 1f);
                case Core.Items.Rarity.Rare: return new Color(0.2f, 0.55f, 1f);
                case Core.Items.Rarity.Uncommon: return new Color(0.3f, 0.95f, 0.5f);
                default: return new Color(0f, 0.9f, 1f);
            }
        }
    }
}
