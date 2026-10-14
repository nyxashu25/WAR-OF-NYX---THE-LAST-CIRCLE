using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// NYX ISLAND detail pass (build version 3): props and dressing for every location, built from
    /// cel-shaded primitives and a few procedural meshes (PLACEHOLDER art, original geometry):
    ///  * Neon District: asphalt streets with lane markings and kerbs, street lamps, parked cars, benches,
    ///    vending machines, bins, barricades, rooftop AC units / water tanks / antennas / billboards,
    ///    balconies and shop awnings on the windowed facades;
    ///  * Crimson Temple: curved pagoda roof, upturned gate beams, paper lanterns, stone lanterns, banners;
    ///  * Iron Fortress: battlements, sandbag nests, crates, barrels, tents, flags, floodlights;
    ///  * Phantom Docks: bollards, lamp posts, crate and barrel stacks, moored boats, buoys, crane hooks;
    ///  * NYX CORE: pipework, consoles with glowing screens, a ring of glowing runes;
    ///  * routes and fields: fences, signposts, sandbag positions; Eclipse Valley: broken columns.
    /// Props that look solid (cars, crates, barrels, sandbags, machines) have colliders: they are cover and
    /// part of the NavMesh. Small decoration has none.
    /// </summary>
    public static partial class NyxIslandBuilder
    {
        private static readonly List<(Vector3 centre, float width, float height, bool westRamp)> Buildings =
            new List<(Vector3, float, float, bool)>();

        private const string PropMeshFolder = "Assets/Art/Props";
        private static System.Random _detailRandom;

        private static void BuildDetails(Transform world)
        {
            _detailRandom = new System.Random(4096);
            ProjectSetupWizard.EnsureFolder(PropMeshFolder);
            var root = new GameObject("Details").transform;
            root.SetParent(world, false);
            NeonDistrictDetails(Sub(root, "Neon District details"));
            TempleDetails(Sub(root, "Crimson Temple details"));
            FortressDetails(Sub(root, "Iron Fortress details"));
            DocksDetails(Sub(root, "Phantom Docks details"));
            CoreDetails(Sub(root, "NYX CORE details"));
            FieldDetails(Sub(root, "Routes and fields details"));
            int count = root.GetComponentsInChildren<Renderer>().Length;
            Debug.Log($"[WarOfNyx] NYX ISLAND details: {count} prop parts.");
        }

        private static Transform Sub(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static float R(float a, float b) => a + (float)_detailRandom.NextDouble() * (b - a);

        /// <summary>True on (or right next to) a rooftop ramp of a Neon District building: keep street props off it.</summary>
        private static bool OnRamp(float x, float z)
        {
            foreach (var (c, w, h, westRamp) in Buildings)
            {
                if (!westRamp)
                    continue;
                float half = w * 0.5f;
                float run = h / Mathf.Tan(32f * Mathf.Deg2Rad);
                if (x > c.x - half - 6f && x < c.x - half + 0.5f && Mathf.Abs(z - c.z) < run * 0.5f + 3f)
                    return true;
            }
            return false;
        }

        /// <summary>An empty holder at a world position (ground height when y is NaN) facing yaw.</summary>
        private static Transform At(Transform parent, string name, float x, float z, float yaw, float y = float.NaN)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = new Vector3(x, float.IsNaN(y) ? Height(x, z) : y, z);
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>A prop part; <paramref name="solid"/> keeps its collider (cover, NavMesh obstacle).</summary>
        private static GameObject P(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, string material,
            Vector3 euler = default, bool solid = false, bool shadows = true)
        {
            var go = Prim(type, name, parent, position, scale, material, euler);
            if (!solid)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ props

        private static void StreetLamp(Transform parent, float x, float z, float yaw, float y = float.NaN)
        {
            var t = At(parent, "StreetLamp", x, z, yaw, y);
            P(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.4f), "PropDark");
            P(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, 2.6f, 0f), new Vector3(0.14f, 2.5f, 0.14f), "Metal", solid: true);
            P(PrimitiveType.Cube, "Arm", t, new Vector3(0f, 5.05f, 0.55f), new Vector3(0.08f, 0.08f, 1.2f), "PropDark");
            P(PrimitiveType.Cube, "Head", t, new Vector3(0f, 4.95f, 1.1f), new Vector3(0.36f, 0.12f, 0.6f), "PropDark");
            P(PrimitiveType.Cube, "Light", t, new Vector3(0f, 4.88f, 1.1f), new Vector3(0.3f, 0.03f, 0.52f), "LampGlow", shadows: false);
        }

        private static void Bench(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "Bench", x, z, yaw);
            P(PrimitiveType.Cube, "Seat", t, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), "Wood", solid: true);
            P(PrimitiveType.Cube, "Back", t, new Vector3(0f, 0.78f, -0.22f), new Vector3(1.8f, 0.36f, 0.06f), "Wood", new Vector3(-12f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
                P(PrimitiveType.Cube, "Leg", t, new Vector3(0.8f * s, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.45f), "PropDark");
        }

        private static void VendingMachine(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "VendingMachine", x, z, yaw);
            P(PrimitiveType.Cube, "Body", t, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.8f), _detailRandom.Next(2) == 0 ? "PropRed" : "PropBlue", solid: true);
            P(PrimitiveType.Cube, "Display", t, new Vector3(-0.1f, 1.15f, 0.41f), new Vector3(0.62f, 1.1f, 0.02f), _detailRandom.Next(2) == 0 ? "ScreenGlow" : "NeonPink", shadows: false);
            P(PrimitiveType.Cube, "Slot", t, new Vector3(0f, 0.3f, 0.41f), new Vector3(0.7f, 0.2f, 0.04f), "PropDark");
            P(PrimitiveType.Cube, "Buttons", t, new Vector3(0.36f, 1.2f, 0.41f), new Vector3(0.12f, 0.5f, 0.03f), "PropLight");
        }

        private static void Bin(Transform parent, float x, float z)
        {
            var t = At(parent, "Bin", x, z, R(0f, 360f));
            P(PrimitiveType.Cylinder, "Bin", t, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.45f, 0.55f), "PropDark", solid: true);
            P(PrimitiveType.Cylinder, "Lid", t, new Vector3(0f, 0.92f, 0f), new Vector3(0.6f, 0.03f, 0.6f), "Metal");
        }

        private static void Car(Transform parent, float x, float z, float yaw, string paint, bool wreck = false)
        {
            var t = At(parent, wreck ? "CarWreck" : "Car", x, z, yaw);
            if (wreck)
                t.rotation *= Quaternion.Euler(0f, 0f, R(-8f, 8f));
            P(PrimitiveType.Cube, "Body", t, new Vector3(0f, 0.68f, 0f), new Vector3(1.85f, 0.72f, 4.3f), paint, solid: true);
            P(PrimitiveType.Cube, "Cabin", t, new Vector3(0f, 1.3f, -0.25f), new Vector3(1.6f, 0.6f, 2.2f), wreck ? "PropDark" : "PropGlass");
            P(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 1.62f, -0.3f), new Vector3(1.62f, 0.05f, 1.9f), paint);
            P(PrimitiveType.Cube, "Bumper", t, new Vector3(0f, 0.45f, 2.17f), new Vector3(1.9f, 0.2f, 0.1f), "PropDark");
            P(PrimitiveType.Cube, "BumperRear", t, new Vector3(0f, 0.45f, -2.17f), new Vector3(1.9f, 0.2f, 0.1f), "PropDark");
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                P(PrimitiveType.Cylinder, "Wheel", t, new Vector3(0.88f * sx, 0.36f, 1.35f * sz), new Vector3(0.72f, 0.12f, 0.72f), "PropDark",
                    new Vector3(0f, 0f, 90f));
            if (!wreck)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    P(PrimitiveType.Cube, "Headlight", t, new Vector3(0.62f * s, 0.75f, 2.16f), new Vector3(0.36f, 0.14f, 0.04f), "LampGlow", shadows: false);
                    P(PrimitiveType.Cube, "Taillight", t, new Vector3(0.66f * s, 0.78f, -2.16f), new Vector3(0.3f, 0.12f, 0.04f), "RedGlow", shadows: false);
                }
            }
        }

        private static void Barricade(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "Barricade", x, z, yaw);
            for (int s = -1; s <= 1; s += 2)
                P(PrimitiveType.Cube, "Leg", t, new Vector3(0.9f * s, 0.45f, 0f), new Vector3(0.08f, 0.9f, 0.5f), "PropDark");
            for (int i = 0; i < 6; i++)
                P(PrimitiveType.Cube, "Stripe", t, new Vector3(-0.75f + i * 0.3f, 0.8f, 0f), new Vector3(0.3f, 0.22f, 0.06f), i % 2 == 0 ? "PropRed" : "PropWhite",
                    solid: i == 0);
        }

        private static void Crate(Transform parent, float x, float z, float yaw, float size = 1.2f, float y = float.NaN)
        {
            var t = At(parent, "Crate", x, z, yaw, y);
            P(PrimitiveType.Cube, "Box", t, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, "Wood", solid: true);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                P(PrimitiveType.Cube, "Edge", t, new Vector3(sx * size * 0.49f, size * 0.5f, sz * size * 0.49f), new Vector3(0.07f, size * 1.01f, 0.07f), "PropDark");
            P(PrimitiveType.Cube, "Brace", t, new Vector3(0f, size * 0.5f, size * 0.505f), new Vector3(size * 1.3f, 0.08f, 0.02f), "PropDark",
                new Vector3(0f, 0f, 45f));
        }

        private static void Barrel(Transform parent, float x, float z, string paint, float y = float.NaN)
        {
            var t = At(parent, "Barrel", x, z, R(0f, 360f), y);
            P(PrimitiveType.Cylinder, "Drum", t, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), paint, solid: true);
            for (int i = 0; i < 2; i++)
                P(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, 0.28f + i * 0.36f, 0f), new Vector3(0.62f, 0.02f, 0.62f), "PropDark");
            P(PrimitiveType.Cylinder, "Cap", t, new Vector3(0.14f, 0.91f, 0.1f), new Vector3(0.1f, 0.02f, 0.1f), "PropLight");
        }

        private static void Sandbags(Transform parent, float x, float z, float yaw, int length = 4, int rows = 2)
        {
            var t = At(parent, "Sandbags", x, z, yaw);
            float width = length * 0.55f;
            var wall = P(PrimitiveType.Cube, "Cover", t, new Vector3(0f, rows * 0.17f, 0f), new Vector3(width, rows * 0.34f, 0.5f), "PropKhaki", solid: true);
            wall.GetComponent<Renderer>().enabled = false; // the collider of the whole stack; the bags are what you see
            for (int r = 0; r < rows; r++)
            for (int i = 0; i < length - (r % 2); i++)
            {
                float bx = -width * 0.5f + 0.275f + i * 0.55f + (r % 2) * 0.275f;
                P(PrimitiveType.Capsule, "Bag", t, new Vector3(bx, 0.15f + r * 0.3f, 0f), new Vector3(0.36f, 0.3f, 0.22f), "PropKhaki", new Vector3(0f, 0f, 90f));
            }
        }

        private static void Flag(Transform parent, float x, float z, float yaw, string cloth, float y = float.NaN, float height = 5f)
        {
            var t = At(parent, "Flag", x, z, yaw, y);
            P(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.08f, height * 0.5f, 0.08f), "PropLight");
            P(PrimitiveType.Cube, "Cloth", t, new Vector3(0.75f, height - 0.55f, 0f), new Vector3(1.4f, 0.9f, 0.03f), cloth, new Vector3(0f, 0f, -4f));
            P(PrimitiveType.Sphere, "Finial", t, new Vector3(0f, height + 0.06f, 0f), Vector3.one * 0.14f, "PropYellow");
        }

        private static void StoneLantern(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "StoneLantern", x, z, yaw);
            P(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.12f, 0f), new Vector3(0.7f, 0.24f, 0.7f), "TempleStone", solid: true);
            P(PrimitiveType.Cylinder, "Pillar", t, new Vector3(0f, 0.6f, 0f), new Vector3(0.24f, 0.36f, 0.24f), "TempleStone");
            P(PrimitiveType.Cube, "Box", t, new Vector3(0f, 1.15f, 0f), new Vector3(0.52f, 0.4f, 0.52f), "TempleStone");
            P(PrimitiveType.Cube, "Glow", t, new Vector3(0f, 1.15f, 0f), new Vector3(0.3f, 0.24f, 0.54f), "Lantern", shadows: false);
            P(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 1.42f, 0f), new Vector3(0.75f, 0.1f, 0.75f), "TempleStone", new Vector3(0f, 45f, 0f));
            P(PrimitiveType.Cube, "Cap", t, new Vector3(0f, 1.52f, 0f), new Vector3(0.36f, 0.12f, 0.36f), "TempleStone", new Vector3(0f, 45f, 0f));
            P(PrimitiveType.Sphere, "Finial", t, new Vector3(0f, 1.66f, 0f), Vector3.one * 0.16f, "TempleStone");
        }

        private static void PaperLantern(Transform parent, Vector3 hangPoint, float drop)
        {
            var t = new GameObject("PaperLantern").transform;
            t.SetParent(parent, false);
            t.position = hangPoint;
            P(PrimitiveType.Cylinder, "String", t, new Vector3(0f, -drop * 0.5f, 0f), new Vector3(0.02f, drop * 0.5f, 0.02f), "PropDark", shadows: false);
            P(PrimitiveType.Sphere, "Lantern", t, new Vector3(0f, -drop - 0.35f, 0f), new Vector3(0.55f, 0.72f, 0.55f), "PaperLantern", shadows: false);
            P(PrimitiveType.Cylinder, "Top", t, new Vector3(0f, -drop + 0.02f, 0f), new Vector3(0.3f, 0.04f, 0.3f), "PropDark", shadows: false);
            P(PrimitiveType.Cylinder, "Bottom", t, new Vector3(0f, -drop - 0.72f, 0f), new Vector3(0.3f, 0.04f, 0.3f), "PropDark", shadows: false);
        }

        private static void Bollard(Transform parent, float x, float z, float y)
        {
            var t = At(parent, "Bollard", x, z, 0f, y);
            P(PrimitiveType.Cylinder, "Post", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.35f, 0.3f, 0.35f), "PropDark", solid: true);
            P(PrimitiveType.Cylinder, "Cap", t, new Vector3(0f, 0.63f, 0f), new Vector3(0.45f, 0.04f, 0.45f), "PropYellow");
        }

        private static void Boat(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "Boat", x, z, yaw, WaterLevel - 0.25f);
            P(PrimitiveType.Cube, "Hull", t, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.9f, 6f), "PropWhite", solid: true);
            P(PrimitiveType.Cube, "Bow", t, new Vector3(0f, 0.45f, 3f), new Vector3(1.56f, 0.9f, 1.56f), "PropWhite", new Vector3(0f, 45f, 0f));
            P(PrimitiveType.Cube, "Stripe", t, new Vector3(0f, 0.75f, 0f), new Vector3(2.24f, 0.14f, 6.04f), "PropBlue");
            P(PrimitiveType.Cube, "Cabin", t, new Vector3(0f, 1.35f, -0.8f), new Vector3(1.6f, 1f, 2f), "PropLight");
            P(PrimitiveType.Cube, "Window", t, new Vector3(0f, 1.5f, 0.21f), new Vector3(1.4f, 0.4f, 0.02f), "PropGlass");
        }

        private static void Buoy(Transform parent, float x, float z)
        {
            var t = At(parent, "Buoy", x, z, 0f, WaterLevel);
            P(PrimitiveType.Sphere, "Float", t, new Vector3(0f, 0.2f, 0f), Vector3.one * 0.9f, "PropRed");
            P(PrimitiveType.Cylinder, "Band", t, new Vector3(0f, 0.35f, 0f), new Vector3(0.92f, 0.08f, 0.92f), "PropWhite");
            P(PrimitiveType.Cylinder, "Mast", t, new Vector3(0f, 1f, 0f), new Vector3(0.08f, 0.5f, 0.08f), "PropDark");
            P(PrimitiveType.Sphere, "Light", t, new Vector3(0f, 1.55f, 0f), Vector3.one * 0.16f, "RedGlow", shadows: false);
        }

        private static void Tent(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "Tent", x, z, yaw);
            for (int s = -1; s <= 1; s += 2)
                P(PrimitiveType.Cube, "Canvas", t, new Vector3(0.95f * s, 1.05f, 0f), new Vector3(0.06f, 2.5f, 4f), "PropKhaki", new Vector3(0f, 0f, -38f * s), solid: true);
            P(PrimitiveType.Cube, "Ridge", t, new Vector3(0f, 2.05f, 0f), new Vector3(0.12f, 0.12f, 4.1f), "PropDark");
            P(PrimitiveType.Cube, "Back", t, new Vector3(0f, 0.9f, -1.98f), new Vector3(2.6f, 1.8f, 0.05f), "PropKhaki");
        }

        private static void Fence(Transform parent, Vector3 from, Vector3 to)
        {
            var dir = to - from;
            float length = dir.magnitude;
            int posts = Mathf.Max(2, Mathf.CeilToInt(length / 2.5f) + 1);
            var t = new GameObject("Fence").transform;
            t.SetParent(parent, false);
            for (int i = 0; i < posts; i++)
            {
                var p = Vector3.Lerp(from, to, i / (posts - 1f));
                p.y = Height(p.x, p.z);
                P(PrimitiveType.Cube, "Post", t, p + Vector3.up * 0.55f, new Vector3(0.12f, 1.1f, 0.12f), "Wood");
            }
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var mid = (from + to) * 0.5f;
            mid.y = Height(mid.x, mid.z);
            for (int r = 0; r < 2; r++)
                P(PrimitiveType.Cube, "Rail", t, mid + Vector3.up * (0.45f + r * 0.4f), new Vector3(0.06f, 0.1f, length), "Wood", new Vector3(0f, yaw, 0f));
        }

        private static void Signpost(Transform parent, float x, float z, float yaw)
        {
            var t = At(parent, "Signpost", x, z, yaw);
            P(PrimitiveType.Cube, "Post", t, new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f), "Wood");
            P(PrimitiveType.Cube, "Board", t, new Vector3(0.45f, 2f, 0f), new Vector3(1f, 0.28f, 0.05f), "Wood", new Vector3(0f, 0f, 4f));
            P(PrimitiveType.Cube, "Board2", t, new Vector3(-0.4f, 1.6f, 0f), new Vector3(0.9f, 0.26f, 0.05f), "Wood", new Vector3(0f, 25f, -3f));
        }

        // ------------------------------------------------------------------ procedural meshes

        /// <summary>
        /// Hip roof with a concave profile and upturned eave corners (pagoda style). Base rectangle
        /// <paramref name="width"/> x <paramref name="depth"/> at y = 0, ridge at y = <paramref name="height"/>.
        /// </summary>
        private static Mesh PagodaRoof(string name, float width, float depth, float height, float curl)
        {
            string path = $"{PropMeshFolder}/{name}.asset";
            float hw = width * 0.5f, hd = depth * 0.5f, ridge = Mathf.Max(0f, (width - depth) * 0.5f);
            var faces = new[]
            {
                (new Vector3(-hw, 0, hd), new Vector3(hw, 0, hd), new Vector3(-ridge, height, 0), new Vector3(ridge, height, 0)),
                (new Vector3(hw, 0, -hd), new Vector3(-hw, 0, -hd), new Vector3(ridge, height, 0), new Vector3(-ridge, height, 0)),
                (new Vector3(hw, 0, hd), new Vector3(hw, 0, -hd), new Vector3(ridge, height, 0), new Vector3(ridge, height, 0)),
                (new Vector3(-hw, 0, -hd), new Vector3(-hw, 0, hd), new Vector3(-ridge, height, 0), new Vector3(-ridge, height, 0)),
            };
            const int n = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var centre = new Vector3(0f, height * 0.3f, 0f);
            foreach (var (a, b, c, d) in faces)
            {
                int start = verts.Count;
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float s = i / (float)n, t = j / (float)n;
                    var p = Vector3.Lerp(Vector3.Lerp(a, b, s), Vector3.Lerp(c, d, s), t);
                    p.y = height * Mathf.Pow(t, 1.7f);                                        // concave: flat eaves, steep top
                    p.y += curl * Mathf.Pow(Mathf.Abs(2f * s - 1f), 4f) * (1f - t) * (1f - t); // upturned corners
                    verts.Add(p);
                }
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int v0 = start + j * (n + 1) + i, v1 = v0 + 1, v2 = v0 + n + 1, v3 = v2 + 1;
                    // Outward winding (checked against the roof centre).
                    var normal = Vector3.Cross(verts[v1] - verts[v0], verts[v2] - verts[v0]);
                    bool outward = Vector3.Dot(normal, (verts[v0] + verts[v3]) * 0.5f - centre) > 0f;
                    if (outward) tris.AddRange(new[] { v0, v1, v2, v1, v3, v2 });
                    else tris.AddRange(new[] { v0, v2, v1, v1, v2, v3 });
                }
            }
            // Flat underside.
            int u = verts.Count;
            verts.AddRange(new[] { new Vector3(-hw, 0.01f, -hd), new Vector3(hw, 0.01f, -hd), new Vector3(hw, 0.01f, hd), new Vector3(-hw, 0.01f, hd) });
            tris.AddRange(new[] { u, u + 1, u + 2, u, u + 2, u + 3 });
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static GameObject MeshProp(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 euler, string material, bool solid)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _materials[material];
            if (solid)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // ------------------------------------------------------------------ locations

        private static void NeonDistrictDetails(Transform g)
        {
            var poi = PoiNamed("Neon District");
            float cx = poi.Centre.x, cz = poi.Centre.y, y = PlateauHeight;
            string[] paints = { "PropRed", "PropBlue", "PropWhite", "PropYellow", "PropDark" };
            string[] neon = { "NeonPink", "NeonCyan", "NeonViolet" };

            // Streets between the blocks (asphalt, kerbs, dashed centre lines).
            for (int i = -2; i <= 1; i++)
            {
                float lane = i * 30f + 15f;
                for (int axis = 0; axis < 2; axis++)
                {
                    var centre = axis == 0 ? new Vector3(cx + lane, y + 0.03f, cz) : new Vector3(cx, y + 0.03f, cz + lane);
                    var size = axis == 0 ? new Vector3(9f, 0.06f, 150f) : new Vector3(150f, 0.06f, 9f);
                    P(PrimitiveType.Cube, "Asphalt", g, centre, size, "PropDark", shadows: false);
                    for (int d = -7; d <= 7; d++)
                    {
                        var dash = centre + (axis == 0 ? new Vector3(0f, 0.035f, d * 10f) : new Vector3(d * 10f, 0.035f, 0f));
                        P(PrimitiveType.Cube, "LaneMark", g, dash, axis == 0 ? new Vector3(0.18f, 0.01f, 3f) : new Vector3(3f, 0.01f, 0.18f), "PropWhite", shadows: false);
                    }
                }
            }

            foreach (var (c, w, h, westRamp) in Buildings)
            {
                float half = w * 0.5f;
                // Rooftop clutter.
                for (int k = 0; k < 2; k++)
                {
                    var p = c + new Vector3(R(-half + 2f, half - 2f), h, R(-half + 2f, half - 2f));
                    var ac = At(g, "ACUnit", p.x, p.z, R(0f, 360f), p.y);
                    P(PrimitiveType.Cube, "Box", ac, new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 0.8f), "PropLight");
                    P(PrimitiveType.Cylinder, "Fan", ac, new Vector3(0f, 0.71f, 0f), new Vector3(0.55f, 0.02f, 0.55f), "PropDark");
                }
                if (_detailRandom.NextDouble() < 0.6)
                {
                    var p = c + new Vector3(R(-half + 2.5f, half - 2.5f), h, R(-half + 2.5f, half - 2.5f));
                    var tank = At(g, "WaterTank", p.x, p.z, 0f, p.y);
                    for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        P(PrimitiveType.Cube, "Leg", tank, new Vector3(0.6f * sx, 0.6f, 0.6f * sz), new Vector3(0.12f, 1.2f, 0.12f), "PropDark");
                    P(PrimitiveType.Cylinder, "Tank", tank, new Vector3(0f, 2.1f, 0f), new Vector3(1.7f, 0.9f, 1.7f), "Wood");
                    P(PrimitiveType.Cylinder, "Lid", tank, new Vector3(0f, 3.05f, 0f), new Vector3(1.2f, 0.08f, 1.2f), "Roof");
                }
                {
                    var p = c + new Vector3(R(-half + 1f, half - 1f), h, R(-half + 1f, half - 1f));
                    var ant = At(g, "Antenna", p.x, p.z, 0f, p.y);
                    float tall = R(2f, 4f);
                    P(PrimitiveType.Cylinder, "Mast", ant, new Vector3(0f, tall * 0.5f, 0f), new Vector3(0.07f, tall * 0.5f, 0.07f), "Metal");
                    P(PrimitiveType.Cube, "Bar", ant, new Vector3(0f, tall * 0.75f, 0f), new Vector3(0.9f, 0.04f, 0.04f), "Metal");
                    P(PrimitiveType.Sphere, "Beacon", ant, new Vector3(0f, tall + 0.08f, 0f), Vector3.one * 0.16f, "RedGlow", shadows: false);
                }
                if (h >= 16f && _detailRandom.NextDouble() < 0.45)
                {
                    var bb = At(g, "Billboard", c.x, c.z, R(0f, 4f) > 2f ? 0f : 90f, c.y + h);
                    for (int s = -1; s <= 1; s += 2)
                        P(PrimitiveType.Cube, "Leg", bb, new Vector3(2.4f * s, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f), "PropDark");
                    P(PrimitiveType.Cube, "Frame", bb, new Vector3(0f, 4.4f, -0.08f), new Vector3(7.4f, 3.4f, 0.2f), "PropDark");
                    P(PrimitiveType.Cube, "Panel", bb, new Vector3(0f, 4.4f, 0.03f), new Vector3(7f, 3f, 0.05f), neon[_detailRandom.Next(neon.Length)], shadows: false);
                }
                // Balconies on two faces (never on the ramp side) and an awning over each door.
                for (int face = 0; face < 4; face++)
                {
                    float yaw = face * 90f;
                    var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    var along = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
                    bool west = Vector3.Dot(outward, Vector3.left) > 0.9f;
                    var faceCentre = c + outward * half;
                    if (!(west && westRamp))
                    {
                        var aw = new GameObject("Awning").transform;
                        aw.SetParent(g, false);
                        aw.position = faceCentre + outward * 0.6f + Vector3.up * 2.75f;
                        aw.rotation = Quaternion.LookRotation(outward);
                        P(PrimitiveType.Cube, "Canopy", aw, Vector3.zero, new Vector3(2.8f, 0.08f, 1.2f), neon[_detailRandom.Next(neon.Length)], new Vector3(14f, 0f, 0f));
                    }
                    if ((west && westRamp) || face % 2 == 1 || h < 12f)
                        continue;
                    int floors = Mathf.FloorToInt((h - 3.6f - 1.3f) / 3.2f);
                    for (int b = 0; b < 2 && floors > 0; b++)
                    {
                        int floor = _detailRandom.Next(0, floors);
                        float off = R(-half + 3f, half - 3f);
                        var bal = new GameObject("Balcony").transform;
                        bal.SetParent(g, false);
                        bal.position = faceCentre + along * off + outward * 0.55f + Vector3.up * (3.6f + floor * 3.2f + 0.1f);
                        bal.rotation = Quaternion.LookRotation(outward);
                        P(PrimitiveType.Cube, "Slab", bal, Vector3.zero, new Vector3(2.4f, 0.14f, 1.1f), "Concrete");
                        P(PrimitiveType.Cube, "Rail", bal, new Vector3(0f, 0.95f, 0.52f), new Vector3(2.4f, 0.06f, 0.06f), "PropDark");
                        for (int r = 0; r < 7; r++)
                            P(PrimitiveType.Cube, "Bar", bal, new Vector3(-1.15f + r * 0.383f, 0.5f, 0.52f), new Vector3(0.04f, 0.9f, 0.04f), "PropDark", shadows: false);
                    }
                }
            }

            // Street furniture along the streets.
            for (int i = -2; i <= 1; i++)
            for (int j = -2; j <= 2; j++)
            {
                float lane = i * 30f + 15f;
                float along = j * 30f + 8f;
                // Every roll is drawn up front so skipping one prop never changes the others.
                bool car1 = _detailRandom.NextDouble() < 0.45, car2 = _detailRandom.NextDouble() < 0.35;
                bool vend = _detailRandom.NextDouble() < 0.4, bin = _detailRandom.NextDouble() < 0.5, bench = _detailRandom.NextDouble() < 0.3;
                float yaw1 = _detailRandom.Next(2) == 0 ? 0f : 180f, yaw2 = _detailRandom.Next(2) == 0 ? 90f : 270f;
                string paint1 = paints[_detailRandom.Next(paints.Length)], paint2 = paints[_detailRandom.Next(paints.Length)];
                void PlaceIfClear(System.Action place, float x, float z)
                {
                    if (!OnRamp(x, z))
                        place();
                }
                PlaceIfClear(() => StreetLamp(g, cx + lane - 5f, cz + along, 90f, y), cx + lane - 5f, cz + along);
                PlaceIfClear(() => StreetLamp(g, cx + along, cz + lane + 5f, 180f, y), cx + along, cz + lane + 5f);
                if (car1)
                    PlaceIfClear(() => Car(g, cx + lane + 2.1f, cz + along + 6f, yaw1, paint1), cx + lane + 2.1f, cz + along + 6f);
                if (car2)
                    PlaceIfClear(() => Car(g, cx + along - 8f, cz + lane - 2.1f, yaw2, paint2), cx + along - 8f, cz + lane - 2.1f);
                if (vend)
                    PlaceIfClear(() => VendingMachine(g, cx + lane - 5.6f, cz + along + 3f, 90f), cx + lane - 5.6f, cz + along + 3f);
                if (bin)
                    PlaceIfClear(() => Bin(g, cx + lane - 5.4f, cz + along - 2.5f), cx + lane - 5.4f, cz + along - 2.5f);
                if (bench)
                    PlaceIfClear(() => Bench(g, cx + along + 3f, cz + lane + 5.5f, 180f), cx + along + 3f, cz + lane + 5.5f);
            }
            // Central plaza: benches around the hologram, barricades at the entrances.
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Bench(g, cx + d.x * 7f, cz + d.z * 7f, a + 180f);
            }
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward;
                Barricade(g, cx + d.x * 13f + d.z * 2f, cz + d.z * 13f - d.x * 2f, k * 90f + 90f);
            }
        }

        private static void TempleDetails(Transform g)
        {
            var poi = PoiNamed("Crimson Temple");
            float cx = poi.Centre.x, cz = poi.Centre.y, y = PlateauHeight;
            // Pagoda roofs: main shrine roof over the existing flat roof, a second tier above.
            var main = PagodaRoof("PagodaRoof_Main", 19f, 17f, 4.2f, 1.1f);
            MeshProp("PagodaRoof", g, main, new Vector3(cx, y + 15.9f, cz), Vector3.zero, "Roof", solid: false);
            var top = PagodaRoof("PagodaRoof_Top", 9f, 8f, 3.2f, 0.7f);
            P(PrimitiveType.Cube, "UpperShrine", g, new Vector3(cx, y + 20.6f, cz), new Vector3(6f, 2.2f, 5f), "TempleRed");
            MeshProp("PagodaRoof2", g, top, new Vector3(cx, y + 21.6f, cz), Vector3.zero, "Roof", solid: false);
            P(PrimitiveType.Sphere, "RoofFinial", g, new Vector3(cx, y + 25f, cz), new Vector3(0.6f, 0.9f, 0.6f), "PropYellow");
            // Gates (torii): upturned top beams and lanterns.
            for (int i = 0; i < 3; i++)
            {
                var gate = new Vector3(cx, y, cz - 38f - i * 12f);
                for (int s = -1; s <= 1; s += 2)
                {
                    P(PrimitiveType.Cube, "KasagiEnd", g, gate + new Vector3(6.2f * s, 7.85f, 0f), new Vector3(2.4f, 0.55f, 1.3f), "Roof",
                        new Vector3(0f, 0f, 12f * s));
                    PaperLantern(g, gate + new Vector3(2.5f * s, 7.2f, 0f), 0.6f);
                }
                P(PrimitiveType.Cube, "Kasagi", g, gate + new Vector3(0f, 7.75f, 0f), new Vector3(10.5f, 0.5f, 1.3f), "Roof");
                P(PrimitiveType.Cube, "Nuki", g, gate + new Vector3(0f, 6.3f, 0f), new Vector3(9.5f, 0.35f, 0.5f), "TempleRed");
                P(PrimitiveType.Cube, "Plaque", g, gate + new Vector3(0f, 7f, 0.3f), new Vector3(1f, 1.2f, 0.12f), "Roof");
            }
            // Stone lanterns lining the approach, banners on the courtyard walls.
            for (int i = 0; i < 6; i++)
                for (int s = -1; s <= 1; s += 2)
                    StoneLantern(g, cx + 7f * s, cz - 30f - i * 8f, 0f);
            for (int i = 0; i < 4; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var b = new GameObject("Banner").transform;
                    b.SetParent(g, false);
                    b.position = new Vector3(cx + 34.6f * s, y + 2.2f, cz - 18f + i * 12f);
                    P(PrimitiveType.Cube, "Pole", b, new Vector3(0f, 1.8f, 0f), new Vector3(0.1f, 3.6f, 0.1f), "Roof");
                    P(PrimitiveType.Cube, "Cloth", b, new Vector3(-0.1f * s, 1.6f, 0f), new Vector3(0.04f, 2.6f, 0.9f), i % 2 == 0 ? "Banner" : "TempleRed");
                }
            // Lanterns hanging along the shrine eaves.
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f + 22.5f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                PaperLantern(g, new Vector3(cx + d.x * 8.5f, y + 15.6f, cz + d.z * 7.5f), 0.5f);
            }
        }

        private static void FortressDetails(Transform g)
        {
            var poi = PoiNamed("Iron Fortress");
            float cx = poi.Centre.x, cz = poi.Centre.y, y = PlateauHeight;
            for (int side = 0; side < 4; side++)
            {
                float a = side * 90f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var right = Quaternion.Euler(0f, a + 90f, 0f) * Vector3.forward;
                var wallCentre = new Vector3(cx, y, cz) + dir * 55f;
                // Battlements on both wall halves.
                for (int half = -1; half <= 1; half += 2)
                    for (int m = 0; m < 10; m++)
                    {
                        var p = wallCentre + right * (half * 30f + (m - 4.5f) * 4.9f) + Vector3.up * 5.4f;
                        P(PrimitiveType.Cube, "Merlon", g, p, new Vector3(1.6f, 0.8f, 1.5f), "Metal", new Vector3(0f, a, 0f), solid: true);
                    }
                // Sandbag nest inside each gate, flag and floodlight on each tower.
                var gateInside = new Vector3(cx, y, cz) + dir * 47f;
                Sandbags(g, gateInside.x + right.x * 4f, gateInside.z + right.z * 4f, a + 90f, 5, 3);
                Sandbags(g, gateInside.x - right.x * 4f, gateInside.z - right.z * 4f, a + 90f, 5, 3);
                var corner = new Vector3(cx, y, cz) + dir * 50f + right * 50f;
                Flag(g, corner.x, corner.z, a, side % 2 == 0 ? "PropRed" : "Banner", y + 8.4f, 4f);
                var lamp = new GameObject("Floodlight").transform;
                lamp.SetParent(g, false);
                lamp.position = corner + Vector3.up * 9.6f - dir * 2.6f;
                lamp.rotation = Quaternion.LookRotation(-(dir + right).normalized) * Quaternion.Euler(25f, 0f, 0f);
                P(PrimitiveType.Cube, "Housing", lamp, Vector3.zero, new Vector3(0.9f, 0.6f, 0.5f), "PropDark");
                P(PrimitiveType.Cube, "Lens", lamp, new Vector3(0f, 0f, 0.26f), new Vector3(0.75f, 0.45f, 0.02f), "LampGlow", shadows: false);
            }
            // Supply yard: crates, barrels, tents.
            for (int i = 0; i < 10; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad;
                float r = R(18f, 40f);
                float x = cx + Mathf.Cos(a) * r, z = cz + Mathf.Sin(a) * r;
                if (_detailRandom.NextDouble() < 0.5)
                {
                    Crate(g, x, z, R(0f, 90f), R(1f, 1.4f), y);
                    if (_detailRandom.NextDouble() < 0.4)
                        Crate(g, x, z, R(0f, 90f), 0.9f, y + 1.1f);
                }
                else
                {
                    for (int b = 0; b < 3; b++)
                        Barrel(g, x + (b % 2) * 0.7f, z + (b / 2) * 0.7f, b == 2 ? "PropRed" : "PropBlue", y);
                }
            }
            Tent(g, cx - 22f, cz + 22f, 45f);
            Tent(g, cx + 24f, cz + 20f, -45f);
            Tent(g, cx - 24f, cz - 24f, 135f);
        }

        private static void DocksDetails(Transform g)
        {
            var poi = PoiNamed("Phantom Docks");
            float cx = poi.Centre.x, cz = poi.Centre.y, y = PlateauHeight;
            // Piers: bollards and lamps along the edges, boats moored alongside, buoys out at sea.
            for (int p = -2; p <= 2; p++)
            {
                float px = cx + p * 16f;
                for (int k = 0; k < 4; k++)
                {
                    float pz = cz - 58f - k * 7f;
                    Bollard(g, px - 2.2f, pz, y - 0.0f);
                    Bollard(g, px + 2.2f, pz, y - 0.0f);
                }
                StreetLamp(g, px + 2.1f, cz - 80f, 270f, y);
                if (p % 2 == 0)
                    Boat(g, px + 5.5f, cz - 72f, R(-6f, 6f));
            }
            for (int b = 0; b < 8; b++)
                Buoy(g, cx + R(-80f, 80f), cz - R(95f, 140f));
            // Cargo between the container rows.
            for (int i = 0; i < 14; i++)
            {
                float x = cx + R(-30f, 30f), z = cz + R(-30f, 30f);
                if (_detailRandom.NextDouble() < 0.55)
                    Crate(g, x, z, R(0f, 90f), R(0.9f, 1.3f), y);
                else
                    Barrel(g, x, z, _detailRandom.Next(2) == 0 ? "PropBlue" : "PropYellow", y);
            }
            // Crane hooks.
            for (int c = -1; c <= 1; c += 2)
            {
                var hook = new Vector3(cx + c * 40f, y, cz - 50f);
                P(PrimitiveType.Cylinder, "Cable", g, hook + Vector3.up * 18f, new Vector3(0.06f, 5.5f, 0.06f), "PropDark", shadows: false);
                P(PrimitiveType.Cube, "Hook", g, hook + Vector3.up * 12.3f, new Vector3(0.8f, 0.6f, 0.5f), "PropYellow");
            }
            for (int k = -2; k <= 2; k++)
                StreetLamp(g, cx + k * 14f, cz + 44f, 180f, y);
        }

        private static void CoreDetails(Transform g)
        {
            float y = PlateauHeight;
            // Pipework running out from the reactor, with joints.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var from = d * 9f + Vector3.up * (y + 0.45f);
                var to = d * 24f + Vector3.up * (y + 0.45f);
                P(PrimitiveType.Cylinder, "Pipe", g, (from + to) * 0.5f, new Vector3(0.7f, 7.5f, 0.7f), "Metal", new Vector3(90f, a, 0f), solid: true);
                P(PrimitiveType.Sphere, "Joint", g, to, Vector3.one * 0.95f, "PropDark");
                P(PrimitiveType.Cube, "Valve", g, (from + to) * 0.5f + Vector3.up * 0.55f, new Vector3(0.5f, 0.25f, 0.5f), "RedGlow");
            }
            // Consoles on the platforms.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var t = At(g, "Console", d.x * 28f, d.z * 28f, a + 180f, y + 3.3f);
                P(PrimitiveType.Cube, "Desk", t, new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1f, 0.9f), "PropDark", solid: true);
                P(PrimitiveType.Cube, "Screen", t, new Vector3(0f, 1.25f, -0.2f), new Vector3(2f, 0.8f, 0.06f), "ScreenGlow", new Vector3(-15f, 0f, 0f), shadows: false);
            }
            // Ring of glowing runes around the plaza.
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                P(PrimitiveType.Cube, "Rune", g, d * 40f + Vector3.up * (y + 0.04f), new Vector3(i % 3 == 0 ? 1.2f : 0.5f, 0.04f, 0.5f), "CoreGlow",
                    new Vector3(0f, a, 0f), shadows: false);
            }
        }

        private static void FieldDetails(Transform g)
        {
            // Signposts where routes leave each location, fences along a few path stretches, sandbag positions.
            foreach (var (a, b) in Routes())
            {
                var dir = (b - a).normalized;
                var side = new Vector2(-dir.y, dir.x);
                foreach (var (from, sign) in new[] { (a, 1f), (b, -1f) })
                {
                    var p = from + dir * sign * 110f + side * 5f;
                    if (!InBuiltPoi(p.x, p.y, 4f))
                        Signpost(g, p.x, p.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
                }
                for (float t = 0.3f; t < 0.75f; t += 0.22f)
                {
                    var p = Vector2.Lerp(a, b, t) + side * 9f;
                    if (InBuiltPoi(p.x, p.y, 6f))
                        continue;
                    var q = p + dir * 12f;
                    Fence(g, new Vector3(p.x, 0f, p.y), new Vector3(q.x, 0f, q.y));
                    var s = Vector2.Lerp(a, b, t + 0.1f) - side * 8f;
                    if (!InBuiltPoi(s.x, s.y, 6f))
                        Sandbags(g, s.x, s.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 4, 2);
                }
            }
            // A few wrecked cars in the open fields (cover between locations).
            for (int i = 0; i < 10; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad, r = R(120f, 420f);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (InBuiltPoi(x, z, 15f) || Height(x, z) < WaterLevel + 1.5f || RouteDistance(x, z) < 8f)
                    continue;
                Car(g, x, z, R(0f, 360f), "PropRed", wreck: true);
            }
            // Eclipse Valley: broken columns.
            for (int i = 0; i < 10; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad, r = R(20f, 140f);
                float x = ValleyCentre.x + Mathf.Cos(a) * r, z = ValleyCentre.y + Mathf.Sin(a) * r;
                if (RouteDistance(x, z) < 6f)
                    continue;
                var t = At(g, "BrokenColumn", x, z, R(0f, 360f));
                float h = R(1.5f, 5f);
                P(PrimitiveType.Cube, "Plinth", t, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.5f, 1.6f), "TempleStone", solid: true);
                P(PrimitiveType.Cylinder, "Shaft", t, new Vector3(0f, 0.5f + h * 0.5f, 0f), new Vector3(0.9f, h * 0.5f, 0.9f), "TempleStone", solid: true);
                if (_detailRandom.NextDouble() < 0.5)
                    P(PrimitiveType.Cylinder, "Fallen", t, new Vector3(1.8f, 0.45f, 0.4f), new Vector3(0.9f, 1.4f, 0.9f), "TempleStone", new Vector3(0f, 30f, 90f), solid: true);
            }
        }
    }
}
