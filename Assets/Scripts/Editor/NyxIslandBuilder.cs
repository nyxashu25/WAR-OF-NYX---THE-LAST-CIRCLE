using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarOfNyx.Gameplay;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// NYX ISLAND: THE FALLEN REALM (spec 16), environment stages 1-2: a 1.2 km island terrain with
    /// coastline, hills and plateaus, the seven named locations built from primitives (cel-shaded,
    /// neon accents), routes and scattered cover, an invisible sea boundary, the spawn area / first
    /// circle (radius 480 m), the offline match authority, and a NavMesh for bots.
    /// Stage 3+ (stylized building assets, painted textures) replaces the primitives later.
    /// Everything is original geometry; nothing copies existing maps.
    /// </summary>
    public static class NyxIslandBuilder
    {
        public const string ScenePath = ProjectSetupWizard.ScenesFolder + "/NyxIsland.unity";
        public const string NavMeshPath = ProjectSetupWizard.ScenesFolder + "/NyxIsland_NavMesh.asset";
        private const string TerrainFolder = "Assets/Art/Terrain";
        private const string MaterialFolder = "Assets/Art/Materials/Island";
        private const float Size = 1300f;
        private const float Base = -20f;     // terrain object y (heights are stored above it)
        private const float HeightRange = 80f;
        public const float PlateauHeight = 6f;
        public const float WaterLevel = 3f;
        public const float StartRadius = 480f;

        private struct Poi
        {
            public string Name;
            public Vector2 Centre;
            public float Radius;
        }

        private static readonly Poi[] Pois =
        {
            new Poi { Name = "NYX CORE", Centre = new Vector2(0f, 0f), Radius = 60f },
            new Poi { Name = "Neon District", Centre = new Vector2(-280f, 230f), Radius = 95f },
            new Poi { Name = "Crimson Temple", Centre = new Vector2(280f, 240f), Radius = 80f },
            new Poi { Name = "Shadow Forest", Centre = new Vector2(-330f, -120f), Radius = 110f },
            new Poi { Name = "Iron Fortress", Centre = new Vector2(330f, -110f), Radius = 90f },
            new Poi { Name = "Phantom Docks", Centre = new Vector2(0f, -440f), Radius = 90f },
        };

        private static readonly Vector2 ValleyCentre = new Vector2(0f, 310f); // Eclipse Valley: hills, no plateau

        private static Dictionary<string, Material> _materials;
        private static System.Random _random;

        public static void Build(bool overwrite)
        {
            if (!overwrite && File.Exists(ScenePath))
                return;

            ProjectSetupWizard.EnsureFolder(TerrainFolder);
            ProjectSetupWizard.EnsureFolder(MaterialFolder);
            _random = new System.Random(2026);
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 900f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ThirdPersonCamera>();
            camGo.transform.position = new Vector3(0f, 40f, -80f);

            CreateTerrain();
            CreateSea();

            var world = new GameObject("NYX ISLAND (graybox stage 2)").transform;
            BuildCore(Group(world, Pois[0]));
            BuildNeonDistrict(Group(world, Pois[1]));
            BuildTemple(Group(world, Pois[2]));
            BuildForest(Group(world, Pois[3]));
            BuildFortress(Group(world, Pois[4]));
            BuildDocks(Group(world, Pois[5]));
            BuildValley(new GameObject("Eclipse Valley").transform, world);
            BuildRoutes(new GameObject("Routes & cover").transform, world);

            foreach (var t in world.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            var area = new GameObject("SpawnArea").AddComponent<ArenaSpawnArea>();
            area.transform.position = new Vector3(0f, PlateauHeight, 0f);
            // Reachability starts on the open plaza south of the reactor (the centre itself is the reactor).
            var anchor = new GameObject("ReachabilityAnchor (core plaza)").transform;
            anchor.SetParent(area.transform, false);
            anchor.position = new Vector3(0f, PlateauHeight, -40f);
            var areaSo = new SerializedObject(area);
            areaSo.FindProperty("zoneRadius").floatValue = StartRadius;
            areaSo.FindProperty("reachabilityAnchor").objectReferenceValue = anchor;
            areaSo.ApplyModifiedPropertiesWithoutUndo();

            var match = new GameObject("GhostProtocol");
            var offline = match.AddComponent<WarOfNyx.Gameplay.Offline.OfflineMatch>();
            match.AddComponent<WarOfNyx.Gameplay.Offline.OfflineMatchHud>();
            var so = new SerializedObject(offline);
            so.FindProperty("characterPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WarOfNyx.Characters.PlayerController>(PracticeArenaBuilder.PrefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            BakeNavMesh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[WarOfNyx] NYX ISLAND built: " + ScenePath);
        }

        // ------------------------------------------------------------------ terrain

        /// <summary>Island height (metres, world) at x/z: coast, gentle noise, Eclipse Valley hills, flat POI plateaus.</summary>
        public static float Height(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);
            float angle = Mathf.Atan2(z, x);
            float edge = 585f + 25f * Mathf.Sin(angle * 3f + 1.3f) + 15f * Mathf.Sin(angle * 7f);
            float land = Smooth(edge + 15f, edge - 25f, d);
            float h = Mathf.Lerp(-6f, PlateauHeight, land);
            h += land * (1.2f * Mathf.Sin(x * 0.021f + 0.7f) * Mathf.Cos(z * 0.017f) + 0.8f * Mathf.Sin(x * 0.05f) * Mathf.Sin(z * 0.043f));

            float vd = Vector2.Distance(new Vector2(x, z), ValleyCentre);
            float valley = Smooth(175f, 90f, vd);
            h += land * valley * (6f + 5f * Mathf.Sin(x * 0.03f) * Mathf.Cos(z * 0.025f) + 3f * Mathf.Sin((x + z) * 0.045f));

            foreach (var poi in Pois)
            {
                float w = Smooth(poi.Radius + 30f, poi.Radius, Vector2.Distance(new Vector2(x, z), poi.Centre));
                h = Mathf.Lerp(h, PlateauHeight, w);
            }
            return h;
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static void CreateTerrain()
        {
            var data = new TerrainData { heightmapResolution = 513 };
            data.size = new Vector3(Size, HeightRange, Size);
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float x = -Size * 0.5f + xi / (res - 1f) * Size;
                float z = -Size * 0.5f + zi / (res - 1f) * Size;
                heights[zi, xi] = Mathf.Clamp01((Height(x, z) - Base) / HeightRange);
            }
            data.SetHeights(0, 0, heights);

            var grass = Layer("Island_Grass", new Color(0.24f, 0.5f, 0.42f));
            var sand = Layer("Island_Sand", new Color(0.78f, 0.66f, 0.52f));
            var rock = Layer("Island_Rock", new Color(0.36f, 0.31f, 0.44f));
            var plaza = Layer("Island_Plaza", new Color(0.3f, 0.28f, 0.38f));
            data.terrainLayers = new[] { grass, sand, rock, plaza };

            data.alphamapResolution = 256;
            int ar = data.alphamapResolution;
            var maps = new float[ar, ar, 4];
            for (int zi = 0; zi < ar; zi++)
            for (int xi = 0; xi < ar; xi++)
            {
                float nx = xi / (ar - 1f), nz = zi / (ar - 1f);
                float x = -Size * 0.5f + nx * Size, z = -Size * 0.5f + nz * Size;
                float h = Height(x, z);
                float steep = data.GetSteepness(nx, nz);
                float w0 = 1f, w1 = 0f, w2 = 0f, w3 = 0f;
                if (h < WaterLevel + 1.5f) { w0 = 0f; w1 = 1f; }
                else if (steep > 22f) { w0 = 0f; w2 = 1f; }
                foreach (var poi in Pois)
                    if (poi.Name != "Shadow Forest" && Vector2.Distance(new Vector2(x, z), poi.Centre) < poi.Radius * 0.9f)
                    {
                        w0 = w1 = w2 = 0f;
                        w3 = 1f;
                    }
                maps[zi, xi, 0] = w0;
                maps[zi, xi, 1] = w1;
                maps[zi, xi, 2] = w2;
                maps[zi, xi, 3] = w3;
            }
            data.SetAlphamaps(0, 0, maps);

            AssetDatabase.DeleteAsset(TerrainFolder + "/NyxIsland_Terrain.asset");
            AssetDatabase.CreateAsset(data, TerrainFolder + "/NyxIsland_Terrain.asset");

            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain (NYX ISLAND)";
            terrainGo.transform.position = new Vector3(-Size * 0.5f, Base, -Size * 0.5f);
            var terrain = terrainGo.GetComponent<Terrain>();
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var material = new Material(shader) { name = "NyxIsland_Terrain" };
                AssetDatabase.DeleteAsset(TerrainFolder + "/NyxIsland_TerrainMaterial.mat");
                AssetDatabase.CreateAsset(material, TerrainFolder + "/NyxIsland_TerrainMaterial.mat");
                terrain.materialTemplate = material;
            }
            terrain.basemapDistance = 600f;
            terrain.heightmapPixelError = 8f;
            GameObjectUtility.SetStaticEditorFlags(terrainGo, StaticEditorFlags.BatchingStatic);
        }

        private static TerrainLayer Layer(string name, Color color)
        {
            string texPath = $"{TerrainFolder}/{name}_Tex.asset";
            string layerPath = $"{TerrainFolder}/{name}.terrainlayer";
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, true) { name = name };
            var pixels = new Color[256];
            for (int i = 0; i < pixels.Length; i++)
            {
                float n = 0.92f + 0.16f * (float)_random.NextDouble(); // painterly speckle
                pixels[i] = new Color(color.r * n, color.g * n, color.b * n, 0.05f); // alpha = smoothness in URP terrain: matte
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(texture, texPath);

            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(6f, 6f), name = name, smoothness = 0f, metallic = 0f };
            AssetDatabase.DeleteAsset(layerPath);
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static void CreateSea()
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea (visual)";
            Object.DestroyImmediate(sea.GetComponent<Collider>());
            sea.transform.position = new Vector3(0f, WaterLevel, 0f);
            sea.transform.localScale = new Vector3(400f, 1f, 400f);
            sea.layer = 4; // Water
            sea.GetComponent<Renderer>().sharedMaterial = _materials["Water"];

            // Invisible sea wall: nobody walks off the island (the circle never extends beyond it).
            var wall = new GameObject("Sea Boundary (invisible)").transform;
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var go = new GameObject("Boundary");
                go.transform.SetParent(wall, false);
                go.transform.position = new Vector3(Mathf.Cos(a) * 630f, 10f, Mathf.Sin(a) * 630f);
                go.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                go.AddComponent<BoxCollider>().size = new Vector3(4f, 50f, 2f * Mathf.PI * 630f / segments + 4f);
            }
        }

        // ------------------------------------------------------------------ locations

        private static Transform Group(Transform world, Poi poi)
        {
            var g = new GameObject(poi.Name).transform;
            g.SetParent(world, false);
            g.position = new Vector3(poi.Centre.x, PlateauHeight, poi.Centre.y);
            return g;
        }

        /// <summary>NYX CORE: the reactor at the island centre (dramatic final-circle location).</summary>
        private static void BuildCore(Transform g)
        {
            Prim(PrimitiveType.Cylinder, "Reactor", g, new Vector3(0f, 15f, 0f), new Vector3(16f, 15f, 16f), "Metal");
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cylinder, "EnergyRing", g, new Vector3(0f, 6f + i * 7f, 0f), new Vector3(17.5f, 0.4f, 17.5f), "CoreGlow");
            Deco(PrimitiveType.Sphere, "CoreOrb", g, new Vector3(0f, 34f, 0f), Vector3.one * 9f, "CoreGlow");
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var right = Quaternion.Euler(0f, a + 90f, 0f) * Vector3.forward;
                var p = dir * 32f;
                Prim(PrimitiveType.Cube, "Platform", g, p + Vector3.up * 3f, new Vector3(12f, 0.6f, 12f), "Concrete", new Vector3(0f, a, 0f));
                Ramp(g, p - dir * 12.5f + Vector3.up * 1.4f, a, 14f, 4f, 3f, "Metal");
                Deco(PrimitiveType.Cube, "Conduit", g, dir * 18f + Vector3.up * 0.3f, new Vector3(1f, 0.5f, 8f), "NeonCyan", new Vector3(0f, a, 0f));
                // Low cover walls around the core
                Prim(PrimitiveType.Cube, "CoverWall", g, dir * 48f + right * 8f + Vector3.up * 0.75f, new Vector3(6f, 1.5f, 0.8f), "Concrete", new Vector3(0f, a + 90f, 0f));
                Prim(PrimitiveType.Cube, "CoverWall", g, dir * 48f - right * 8f + Vector3.up * 0.75f, new Vector3(6f, 1.5f, 0.8f), "Concrete", new Vector3(0f, a + 90f, 0f));
            }
        }

        /// <summary>Neon District: city blocks, streets and alleys, rooftops reachable by ramps, neon signs.</summary>
        private static void BuildNeonDistrict(Transform g)
        {
            string[] neon = { "NeonPink", "NeonCyan", "NeonViolet" };
            for (int bx = -2; bx <= 2; bx++)
            for (int bz = -2; bz <= 2; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue; // central plaza
                var p = new Vector3(bx * 30f, 0f, bz * 30f);
                float height = 8f + _random.Next(0, 5) * 4f;
                float w = 14f + _random.Next(0, 3) * 2f;
                string[] facades = { "Concrete", "ConcreteTeal", "ConcreteRose" };
                Prim(PrimitiveType.Cube, "Building", g, p + Vector3.up * height * 0.5f, new Vector3(w, height, w), facades[_random.Next(facades.Length)]);
                Deco(PrimitiveType.Cube, "NeonSign", g, p + new Vector3(0f, height * 0.6f, w * 0.5f + 0.15f), new Vector3(w * 0.6f, 1.2f, 0.2f),
                    neon[_random.Next(neon.Length)]);
                Deco(PrimitiveType.Cube, "RoofTrim", g, p + Vector3.up * (height + 0.1f), new Vector3(w + 0.4f, 0.2f, w + 0.4f), neon[_random.Next(neon.Length)]);
                if ((bx + bz) % 2 == 0)
                    Ramp(g, p + new Vector3(-w * 0.5f - 2.5f, height * 0.5f, 0f), 0f, height / Mathf.Tan(32f * Mathf.Deg2Rad), height, 3.5f, "Metal");
                // Street cover
                Prim(PrimitiveType.Cube, "Barrier", g, p + new Vector3(w * 0.5f + 4f, 0.6f, 4f), new Vector3(0.8f, 1.2f, 4f), "Metal");
            }
            Deco(PrimitiveType.Cylinder, "PlazaHolo", g, new Vector3(0f, 5f, 0f), new Vector3(3f, 5f, 3f), "NeonViolet");
        }

        /// <summary>Crimson Temple: stepped platform with stairs, shrine, gates, courtyard walls, lanterns.</summary>
        private static void BuildTemple(Transform g)
        {
            for (int tier = 0; tier < 3; tier++)
            {
                float size = 50f - tier * 14f;
                float y = tier * 3f;
                Prim(PrimitiveType.Cube, "Tier", g, new Vector3(0f, y + 1.5f, 0f), new Vector3(size, 3f, size), "TempleStone");
                Stairs(g, new Vector3(0f, y, -size * 0.5f - 2.5f), 0f, 3f, 6f);
            }
            Prim(PrimitiveType.Cube, "Shrine", g, new Vector3(0f, 12f, 0f), new Vector3(12f, 6f, 10f), "TempleRed");
            Prim(PrimitiveType.Cube, "Roof", g, new Vector3(0f, 15.6f, 0f), new Vector3(16f, 1.2f, 14f), "Metal");
            for (int i = 0; i < 3; i++)
            {
                var gate = new Vector3(0f, 0f, -38f - i * 12f);
                Prim(PrimitiveType.Cube, "GatePillar", g, gate + new Vector3(-4f, 3.5f, 0f), new Vector3(1f, 7f, 1f), "TempleRed");
                Prim(PrimitiveType.Cube, "GatePillar", g, gate + new Vector3(4f, 3.5f, 0f), new Vector3(1f, 7f, 1f), "TempleRed");
                Prim(PrimitiveType.Cube, "GateBeam", g, gate + new Vector3(0f, 7.2f, 0f), new Vector3(11f, 0.8f, 1.2f), "TempleRed");
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Prim(PrimitiveType.Cube, "CourtyardWall", g, new Vector3(side * 34f, 1.5f, 0f), new Vector3(1f, 3f, 50f), "TempleStone");
                for (int i = 0; i < 4; i++)
                    Deco(PrimitiveType.Sphere, "Lantern", g, new Vector3(side * 28f, 2.4f, -18f + i * 12f), Vector3.one * 0.9f, "Lantern");
            }
        }

        /// <summary>Shadow Forest: stylized trees, rocks and natural cover along winding paths.</summary>
        private static void BuildForest(Transform g)
        {
            for (int i = 0; i < 140; i++)
            {
                float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float r = 105f * Mathf.Sqrt((float)_random.NextDouble());
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                // Keep a winding path open (sine-shaped lane through the forest).
                if (Mathf.Abs(p.z - 25f * Mathf.Sin(p.x * 0.04f)) < 4f)
                    continue;
                float trunk = 5f + (float)_random.NextDouble() * 4f;
                Prim(PrimitiveType.Cylinder, "Trunk", g, p + Vector3.up * trunk * 0.5f, new Vector3(0.9f, trunk * 0.5f, 0.9f), "Wood");
                Deco(PrimitiveType.Sphere, "Canopy", g, p + Vector3.up * (trunk + 2f), new Vector3(6f, 5f, 6f), "Foliage");
            }
            for (int i = 0; i < 40; i++)
            {
                float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float r = 100f * Mathf.Sqrt((float)_random.NextDouble());
                float s = 1.5f + (float)_random.NextDouble() * 2.5f;
                Prim(PrimitiveType.Sphere, "Rock", g, new Vector3(Mathf.Cos(a) * r, s * 0.35f, Mathf.Sin(a) * r), new Vector3(s * 1.4f, s, s * 1.2f), "Rock");
            }
        }

        /// <summary>Iron Fortress: perimeter walls with gaps, watchtowers with ramps, barriers, a central bunker.</summary>
        private static void BuildFortress(Transform g)
        {
            for (int side = 0; side < 4; side++)
            {
                float a = side * 90f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var right = Quaternion.Euler(0f, a + 90f, 0f) * Vector3.forward;
                // Wall in two halves leaving a 10 m gate.
                Prim(PrimitiveType.Cube, "Wall", g, dir * 55f + right * 30f + Vector3.up * 2.5f, new Vector3(50f, 5f, 1.5f), "Metal", new Vector3(0f, a, 0f));
                Prim(PrimitiveType.Cube, "Wall", g, dir * 55f - right * 30f + Vector3.up * 2.5f, new Vector3(50f, 5f, 1.5f), "Metal", new Vector3(0f, a, 0f));
                // Watchtower in the corner
                var corner = dir * 50f + right * 50f;
                for (int lx = -1; lx <= 1; lx += 2)
                for (int lz = -1; lz <= 1; lz += 2)
                    Prim(PrimitiveType.Cube, "TowerLeg", g, corner + new Vector3(lx * 2f, 4f, lz * 2f), new Vector3(0.6f, 8f, 0.6f), "Metal");
                Prim(PrimitiveType.Cube, "TowerDeck", g, corner + Vector3.up * 8.2f, new Vector3(6f, 0.4f, 6f), "Concrete");
                Prim(PrimitiveType.Cube, "TowerRail", g, corner + new Vector3(0f, 9f, 3f), new Vector3(6f, 1.2f, 0.3f), "Metal");
                Ramp(g, corner - dir * 10.5f + Vector3.up * 4f, a, 14f, 8f, 2.5f, "Metal");
                Deco(PrimitiveType.Cube, "Beacon", g, corner + Vector3.up * 10f, Vector3.one * 0.8f, "Lantern");
                for (int b = 0; b < 3; b++)
                    Prim(PrimitiveType.Cube, "Barrier", g, dir * 25f + right * (b - 1) * 12f + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 1f), "Concrete", new Vector3(0f, a, 0f));
            }
            // Bunker with a doorway (two wall pieces + lintel) and a roof.
            Prim(PrimitiveType.Cube, "BunkerBack", g, new Vector3(0f, 2f, 8f), new Vector3(20f, 4f, 1f), "Concrete");
            Prim(PrimitiveType.Cube, "BunkerSide", g, new Vector3(-10f, 2f, 0f), new Vector3(1f, 4f, 16f), "Concrete");
            Prim(PrimitiveType.Cube, "BunkerSide", g, new Vector3(10f, 2f, 0f), new Vector3(1f, 4f, 16f), "Concrete");
            Prim(PrimitiveType.Cube, "BunkerFrontL", g, new Vector3(-6.5f, 2f, -8f), new Vector3(7f, 4f, 1f), "Concrete");
            Prim(PrimitiveType.Cube, "BunkerFrontR", g, new Vector3(6.5f, 2f, -8f), new Vector3(7f, 4f, 1f), "Concrete");
            Prim(PrimitiveType.Cube, "BunkerRoof", g, new Vector3(0f, 4.3f, 0f), new Vector3(21f, 0.6f, 17f), "Metal");
        }

        /// <summary>Phantom Docks: container stacks, cranes, catwalks with ramps, piers.</summary>
        private static void BuildDocks(Transform g)
        {
            string[] colours = { "ContainerA", "ContainerB", "ContainerC" };
            for (int row = -3; row <= 3; row++)
            for (int col = -2; col <= 2; col++)
            {
                if ((row + col) % 3 == 0)
                    continue; // lanes between stacks
                int stack = 1 + _random.Next(0, 3);
                for (int s = 0; s < stack; s++)
                    Prim(PrimitiveType.Cube, "Container", g, new Vector3(col * 12f, 1.3f + s * 2.6f, row * 9f), new Vector3(6f, 2.6f, 2.5f),
                        colours[_random.Next(colours.Length)], new Vector3(0f, (row % 2) * 90f, 0f));
            }
            for (int c = -1; c <= 1; c += 2)
            {
                var basePos = new Vector3(c * 40f, 0f, -30f);
                Prim(PrimitiveType.Cube, "CraneLegL", g, basePos + new Vector3(-4f, 12f, 0f), new Vector3(1.2f, 24f, 1.2f), "ContainerB");
                Prim(PrimitiveType.Cube, "CraneLegR", g, basePos + new Vector3(4f, 12f, 0f), new Vector3(1.2f, 24f, 1.2f), "ContainerB");
                Prim(PrimitiveType.Cube, "CraneBoom", g, basePos + new Vector3(0f, 24f, -10f), new Vector3(2f, 1.5f, 30f), "ContainerB");
                Deco(PrimitiveType.Cube, "CraneLight", g, basePos + new Vector3(0f, 25f, -24f), Vector3.one, "Lantern");
            }
            // Catwalk with ramps on both ends.
            Prim(PrimitiveType.Cube, "Catwalk", g, new Vector3(0f, 5f, 35f), new Vector3(50f, 0.4f, 4f), "Metal");
            Ramp(g, new Vector3(-31f, 2.5f, 35f), 90f, 10f, 5f, 4f, "Metal");
            Ramp(g, new Vector3(31f, 2.5f, 35f), -90f, 10f, 5f, 4f, "Metal");
            for (int p = -2; p <= 2; p++)
                Prim(PrimitiveType.Cube, "Pier", g, new Vector3(p * 16f, -0.5f, -70f), new Vector3(5f, 1f, 30f), "Wood");
        }

        /// <summary>Eclipse Valley: open hills (from the terrain) with rocks and a few ruins for long-range fights.</summary>
        private static void BuildValley(Transform g, Transform world)
        {
            g.SetParent(world, false);
            for (int i = 0; i < 45; i++)
            {
                float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float r = 150f * Mathf.Sqrt((float)_random.NextDouble());
                float x = ValleyCentre.x + Mathf.Cos(a) * r, z = ValleyCentre.y + Mathf.Sin(a) * r;
                float s = 2f + (float)_random.NextDouble() * 3f;
                Prim(PrimitiveType.Sphere, "Rock", g, new Vector3(x, Height(x, z) + s * 0.25f, z), new Vector3(s * 1.5f, s, s * 1.3f), "Rock", default, true);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = ValleyCentre.x - 90f + i * 36f, z = ValleyCentre.y + (i % 2 == 0 ? 40f : -50f);
                Prim(PrimitiveType.Cube, "Ruin", g, new Vector3(x, Height(x, z) + 2f, z), new Vector3(8f, 4f, 1.2f), "TempleStone", new Vector3(0f, i * 25f, 0f), true);
            }
        }

        /// <summary>Several routes between the locations with cover along them (spec 16: no endless open ground).</summary>
        private static void BuildRoutes(Transform g, Transform world)
        {
            g.SetParent(world, false);
            for (int i = 0; i < Pois.Length; i++)
            for (int j = i + 1; j < Pois.Length; j++)
            {
                var a = Pois[i].Centre;
                var b = Pois[j].Centre;
                if (Vector2.Distance(a, b) > 520f)
                    continue;
                for (float t = 0.2f; t < 0.85f; t += 0.13f)
                {
                    var p = Vector2.Lerp(a, b, t);
                    var side = new Vector2(-(b - a).y, (b - a).x).normalized * (t % 0.26f < 0.13f ? 6f : -6f);
                    p += side;
                    float yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
                    Prim(PrimitiveType.Cube, "RouteCover", g, new Vector3(p.x, Height(p.x, p.y) + 0.7f, p.y), new Vector3(4f, 1.4f, 0.8f), "Concrete",
                        new Vector3(0f, yaw + 90f, 0f), true);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void CreateMaterials()
        {
            _materials = new Dictionary<string, Material>();
            var toon = Shader.Find("WarOfNyx/NyxToon") ?? Shader.Find("Universal Render Pipeline/Lit");
            void M(string key, Color color, Color emission)
            {
                string path = $"{MaterialFolder}/Island_{key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(toon);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = toon;
                m.SetColor("_BaseColor", color);
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(m);
                _materials[key] = m;
            }
            M("Concrete", new Color(0.34f, 0.34f, 0.42f), Color.black);
            M("Metal", new Color(0.24f, 0.25f, 0.32f), Color.black);
            M("ConcreteTeal", new Color(0.26f, 0.42f, 0.48f), Color.black);
            M("ConcreteRose", new Color(0.52f, 0.36f, 0.48f), Color.black);
            M("TempleStone", new Color(0.55f, 0.48f, 0.46f), Color.black);
            M("TempleRed", new Color(0.78f, 0.12f, 0.2f), Color.black);
            M("Wood", new Color(0.36f, 0.24f, 0.2f), Color.black);
            M("Foliage", new Color(0.18f, 0.32f, 0.36f), Color.black);
            M("Rock", new Color(0.42f, 0.39f, 0.48f), Color.black);
            M("ContainerA", new Color(0.8f, 0.25f, 0.3f), Color.black);
            M("ContainerB", new Color(0.2f, 0.45f, 0.75f), Color.black);
            M("ContainerC", new Color(0.9f, 0.65f, 0.2f), Color.black);
            M("NeonPink", new Color(1f, 0.3f, 0.7f), new Color(1.6f, 0.4f, 1.1f));
            M("NeonCyan", new Color(0.2f, 0.9f, 1f), new Color(0.2f, 1.4f, 1.6f));
            M("NeonViolet", new Color(0.6f, 0.3f, 1f), new Color(0.9f, 0.4f, 1.6f));
            M("CoreGlow", new Color(0.55f, 0.35f, 1f), new Color(1.2f, 0.6f, 2.2f));
            M("Lantern", new Color(1f, 0.6f, 0.3f), new Color(1.8f, 0.9f, 0.4f));
            M("Water", new Color(0.12f, 0.22f, 0.45f), new Color(0.02f, 0.06f, 0.14f));
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, string material,
            Vector3 euler = default, bool worldPosition = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (worldPosition)
                go.transform.position = localPosition;
            else
                go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = _materials[material];
            if (type == PrimitiveType.Cylinder)
            {
                // Unity's cylinder primitive has a CAPSULE collider (rounded ends); use the real shape
                // so the NavMesh and shots match what players see.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }

        /// <summary>Decoration: no collider (signs, lights, canopies above head height).</summary>
        private static GameObject Deco(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, string material,
            Vector3 euler = default)
        {
            var go = Prim(type, name, parent, localPosition, scale, material, euler);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A walkable ramp: <paramref name="centre"/> is its middle, rising by <paramref name="rise"/> over <paramref name="run"/> metres along yaw.</summary>
        private static void Ramp(Transform parent, Vector3 centre, float yaw, float run, float rise, float width, string material)
        {
            float length = Mathf.Sqrt(run * run + rise * rise);
            float pitch = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Prim(PrimitiveType.Cube, "Ramp", parent, centre, new Vector3(width, 0.3f, length), material, new Vector3(pitch, yaw, 0f));
        }

        private static void Stairs(Transform parent, Vector3 bottomFront, float yaw, float rise, float width)
        {
            const int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float h = rise * (i + 1) / steps;
                Prim(PrimitiveType.Cube, "Step", parent, bottomFront + new Vector3(0f, h * 0.5f, i * 0.45f), new Vector3(width, h, 0.45f), "TempleStone",
                    new Vector3(0f, yaw, 0f));
            }
        }

        private static void BakeNavMesh()
        {
            var surface = new GameObject("NavMesh").AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            // A 1.3 km island: coarser voxels keep the bake to a reasonable time and memory.
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogWarning("[WarOfNyx] NYX ISLAND NavMesh bake produced no data.");
                return;
            }
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
