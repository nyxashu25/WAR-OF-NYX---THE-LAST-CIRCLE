using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.Gameplay.World;

namespace WarOfNyx.EditorTools
{
    /// <summary>
    /// Builds the environment art library (idempotent, keeps asset GUIDs): procedural tree, bush,
    /// rock, grass and flower meshes with two levels of detail, their materials (WarOfNyx/NyxFoliage),
    /// prefabs with LOD groups and colliders, the sea material, rain / lightning material, painted
    /// terrain textures, and the "Foliage" physics layer. PLACEHOLDER art: original procedural
    /// geometry, meant to be replaced by artist-made models later without changing the map code.
    /// </summary>
    public static class EnvironmentArtBuilder
    {
        public const string Folder = "Assets/Art/Vegetation";
        private const string MeshFolder = Folder + "/Meshes";
        private const string MaterialFolder = Folder + "/Materials";
        private const string PrefabFolder = Folder + "/Prefabs";
        private const string TextureFolder = "Assets/Art/Terrain";
        public const string RainMaterialPath = "Assets/Resources/" + WeatherSystem.RainMaterialResource + ".mat";

        public const int VariantsPerTree = 4;

        /// <summary>Everything the map builder places.</summary>
        public sealed class Library
        {
            public readonly Dictionary<TreeKind, List<GameObject>> Trees = new Dictionary<TreeKind, List<GameObject>>();
            public readonly List<GameObject> Bushes = new List<GameObject>();
            public readonly List<GameObject> Rocks = new List<GameObject>();
            public readonly List<GameObject> Grass = new List<GameObject>();
            public readonly List<GameObject> Flowers = new List<GameObject>();
            public Material ShadowForestLeaves;
            public Material Water;
        }

        private static Shader _foliage;

        public static Library Build()
        {
            foreach (var f in new[] { Folder, MeshFolder, MaterialFolder, PrefabFolder, TextureFolder, "Assets/Resources/WarOfNyx" })
                ProjectSetupWizard.EnsureFolder(f);
            EnsureFoliageLayer();

            _foliage = Shader.Find("WarOfNyx/NyxFoliage");
            if (_foliage == null)
                throw new System.InvalidOperationException("Shader WarOfNyx/NyxFoliage not found (Assets/Art/Shaders/NyxFoliage.shader failed to import?).");

            var bark = Mat("Veg_Bark", m => { Common(m, cull: CullMode.Back, outline: 0.012f, wind: 0.1f, frequency: 1.1f, translucency: 0f); m.SetFloat("_TipLighten", 0f); });
            var leaves = Mat("Veg_Leaves", m => Common(m, CullMode.Back, 0.012f, 0.22f, 1.3f, 0.35f));
            var shadowLeaves = Mat("Veg_Leaves_ShadowForest", m =>
            {
                Common(m, CullMode.Back, 0.012f, 0.22f, 1.3f, 0.3f);
                m.SetColor("_BaseColor", new Color(0.72f, 0.78f, 1f)); // bluish-violet tint for the Shadow Forest
                m.SetColor("_TranslucencyColor", new Color(0.55f, 0.45f, 0.95f));
            });
            var fronds = Mat("Veg_Fronds", m => Common(m, CullMode.Off, 0f, 0.35f, 1.5f, 0.4f));
            var bushMat = Mat("Veg_Bush", m => Common(m, CullMode.Back, 0.01f, 0.1f, 1.6f, 0.3f));
            var grassMat = Mat("Veg_Grass", m =>
            {
                Common(m, CullMode.Off, 0f, 0.14f, 2.1f, 0.25f);
                m.SetFloat("_NearFade", 1f);
                m.SetFloat("_FieldVariation", 1f);
            });
            var rockMat = Mat("Veg_Rock", m =>
            {
                Common(m, CullMode.Back, 0.01f, 0f, 1f, 0f);
                m.SetFloat("_ShadeThreshold", 0.02f);
                m.SetFloat("_ShadeSoftness", 0.06f);
                m.SetFloat("_TipLighten", 0f);
            });

            var library = new Library { ShadowForestLeaves = shadowLeaves, Water = WaterMaterial() };
            RainMaterial();

            foreach (TreeKind kind in System.Enum.GetValues(typeof(TreeKind)))
            {
                var list = new List<GameObject>();
                for (int v = 0; v < VariantsPerTree; v++)
                {
                    int seed = 100 + v * 31;
                    var lod0 = SaveMesh(VegetationMeshes.Tree(kind, seed, lowDetail: false), $"Tree_{kind}_{v}");
                    var lod1 = SaveMesh(VegetationMeshes.Tree(kind, seed, lowDetail: true), $"Tree_{kind}_{v}_LOD1");
                    var materials = new[] { bark, kind == TreeKind.Palm ? fronds : leaves };
                    list.Add(TreePrefab($"Tree_{kind}_{v}", lod0, lod1, materials, kind));
                }
                library.Trees[kind] = list;
            }

            var bushColours = new[]
            {
                new Color(0.3f, 0.55f, 0.4f), new Color(0.36f, 0.6f, 0.36f), new Color(0.26f, 0.48f, 0.46f),
                new Color(0.42f, 0.56f, 0.34f), new Color(0.62f, 0.44f, 0.62f), new Color(0.3f, 0.5f, 0.52f),
            };
            for (int v = 0; v < bushColours.Length; v++)
            {
                var lod0 = SaveMesh(VegetationMeshes.Bush(300 + v * 17, bushColours[v], lowDetail: false), $"Bush_{v}");
                var lod1 = SaveMesh(VegetationMeshes.Bush(300 + v * 17, bushColours[v], lowDetail: true), $"Bush_{v}_LOD1");
                library.Bushes.Add(BushPrefab($"Bush_{v}", lod0, lod1, bushMat));
            }

            for (int v = 0; v < 8; v++)
            {
                var mesh = SaveMesh(VegetationMeshes.Rock(500 + v * 13, moss: v % 2 == 0), $"Rock_{v}");
                library.Rocks.Add(RockPrefab($"Rock_{v}", mesh, rockMat));
            }

            for (int v = 0; v < 3; v++)
                library.Grass.Add(DetailPrefab($"Grass_{v}", SaveMesh(VegetationMeshes.GrassClump(700 + v * 7), $"Grass_{v}"), grassMat));
            var petals = new[] { new Color(1f, 0.55f, 0.78f), new Color(0.7f, 0.55f, 1f), new Color(1f, 0.95f, 0.75f) };
            for (int v = 0; v < petals.Length; v++)
                library.Flowers.Add(DetailPrefab($"Flowers_{v}", SaveMesh(VegetationMeshes.FlowerClump(800 + v * 7, petals[v]), $"Flowers_{v}"), grassMat));

            AssetDatabase.SaveAssets();
            return library;
        }

        // ------------------------------------------------------------------ layer

        public static void EnsureFoliageLayer()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0)
                return;
            var so = new SerializedObject(tagManager[0]);
            var layers = so.FindProperty("layers");
            if (layers == null || !layers.isArray || layers.arraySize <= EnvironmentLayers.Foliage)
                return;
            var slot = layers.GetArrayElementAtIndex(EnvironmentLayers.Foliage);
            if (slot.stringValue == EnvironmentLayers.FoliageName)
                return;
            if (!string.IsNullOrEmpty(slot.stringValue))
                Debug.LogWarning($"[WarOfNyx] Layer {EnvironmentLayers.Foliage} was '{slot.stringValue}'; renaming it to '{EnvironmentLayers.FoliageName}'.");
            slot.stringValue = EnvironmentLayers.FoliageName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ materials

        private static void Common(Material m, CullMode cull, float outline, float wind, float frequency, float translucency)
        {
            m.SetFloat("_Cull", (float)cull);
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_WindAmount", wind);
            m.SetFloat("_WindFrequency", frequency);
            m.SetFloat("_TranslucencyStrength", translucency);
            m.enableInstancing = true; // terrain grass / flowers are drawn instanced
            // No outline: skip the pass entirely instead of drawing it with zero width.
            m.SetShaderPassEnabled("SRPDefaultUnlit", outline > 0f);
        }

        private static Material Mat(string name, System.Action<Material> configure)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(_foliage) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = _foliage;
            configure(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material WaterMaterial()
        {
            const string path = MaterialFolder + "/Water_NyxSea.mat";
            var shader = Shader.Find("WarOfNyx/NyxWater");
            if (shader == null)
                return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "Water_NyxSea" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void RainMaterial()
        {
            var shader = Shader.Find("WarOfNyx/NyxAdditive");
            if (shader == null)
                return;
            var m = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            if (m == null)
            {
                m = new Material(shader) { name = "NyxRain" };
                AssetDatabase.CreateAsset(m, RainMaterialPath);
            }
            m.shader = shader;
            m.SetColor("_Color", new Color(0.8f, 0.85f, 1f, 1f));
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------ meshes and prefabs

        /// <summary>Saves a mesh, reusing the existing asset (and its GUID) when there is one.</summary>
        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject Renderer(Transform parent, string name, Mesh mesh, Material[] materials, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private static GameObject TreePrefab(string name, Mesh lod0, Mesh lod1, Material[] materials, TreeKind kind)
        {
            var root = new GameObject(name);
            var r0 = Renderer(root.transform, "LOD0", lod0, materials, ShadowCastingMode.On);
            var r1 = Renderer(root.transform, "LOD1", lod1, materials, ShadowCastingMode.On);
            var group = root.AddComponent<LODGroup>();
            // Trees are cover: culled only when tiny on screen (same on every quality tier, see ProjectSetupWizard).
            group.SetLODs(new[]
            {
                new LOD(0.22f, new UnityEngine.Renderer[] { r0.GetComponent<MeshRenderer>() }),
                new LOD(0.008f, new UnityEngine.Renderer[] { r1.GetComponent<MeshRenderer>() }),
            });
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();

            // Trunk collider only: canopies don't stop bullets or players; the trunk does.
            var trunk = root.AddComponent<CapsuleCollider>();
            float height = kind == TreeKind.Pine ? 8f : kind == TreeKind.Palm ? 6.5f : 5f;
            trunk.radius = kind == TreeKind.Palm ? 0.3f : 0.38f;
            trunk.height = height;
            trunk.center = new Vector3(0f, height * 0.5f, 0f);
            return SavePrefab(root, name);
        }

        private static GameObject BushPrefab(string name, Mesh lod0, Mesh lod1, Material material)
        {
            var root = new GameObject(name) { layer = EnvironmentLayers.Foliage };
            var r0 = Renderer(root.transform, "LOD0", lod0, new[] { material }, ShadowCastingMode.On);
            var r1 = Renderer(root.transform, "LOD1", lod1, new[] { material }, ShadowCastingMode.Off);
            r0.layer = r1.layer = EnvironmentLayers.Foliage;
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.06f, new UnityEngine.Renderer[] { r0.GetComponent<MeshRenderer>() }),
                new LOD(0.004f, new UnityEngine.Renderer[] { r1.GetComponent<MeshRenderer>() }),
            });
            group.RecalculateBounds();
            // Sight blocker for bots (trigger: bullets and players pass through, like a real bush).
            var sight = root.AddComponent<SphereCollider>();
            sight.isTrigger = true;
            sight.radius = 0.95f;
            sight.center = new Vector3(0f, 0.75f, 0f);
            return SavePrefab(root, name);
        }

        private static GameObject RockPrefab(string name, Mesh mesh, Material material)
        {
            var root = new GameObject(name);
            var r = Renderer(root.transform, "Mesh", mesh, new[] { material }, ShadowCastingMode.On);
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.004f, new UnityEngine.Renderer[] { r.GetComponent<MeshRenderer>() }) });
            group.RecalculateBounds();
            root.AddComponent<MeshCollider>().sharedMesh = mesh;
            return SavePrefab(root, name);
        }

        private static GameObject DetailPrefab(string name, Mesh mesh, Material material)
        {
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // grass receives shadows, casting them costs too much on phones
            return SavePrefab(root, name);
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ terrain textures

        /// <summary>Painted, tileable terrain layers: 0 grass, 1 dirt / sand, 2 rock, 3 plaza stone.</summary>
        public static TerrainLayer[] TerrainLayers()
        {
            ProjectSetupWizard.EnsureFolder(TextureFolder);
            return new[]
            {
                Layer("Island_Grass", 8f, GrassTexture),
                Layer("Island_Dirt", 6f, DirtTexture),
                Layer("Island_Rock", 10f, RockTexture),
                Layer("Island_Plaza", 12f, PlazaTexture),
            };
        }

        private const int TextureSize = 256;

        private static TerrainLayer Layer(string name, float tile, System.Func<int, int, Color> paint)
        {
            string texPath = $"{TextureFolder}/{name}_Tex.asset";
            string layerPath = $"{TextureFolder}/{name}.terrainlayer";

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                var c = paint(x, y);
                c.a = 0.04f; // alpha = smoothness on URP terrain: matte
                pixels[y * TextureSize + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (existingTex == null)
            {
                AssetDatabase.CreateAsset(texture, texPath);
                existingTex = texture;
            }
            else
            {
                EditorUtility.CopySerialized(texture, existingTex);
                Object.DestroyImmediate(texture);
                EditorUtility.SetDirty(existingTex);
            }

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = existingTex;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>Tileable value noise: lattice of <paramref name="period"/> cells across the texture.</summary>
        private static float Tile(int x, int y, int period, int seed)
        {
            float fx = x / (float)TextureSize * period, fy = y / (float)TextureSize * period;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float H(int a, int b)
            {
                a = ((a % period) + period) % period;
                b = ((b % period) + period) % period;
                uint h = (uint)(a * 73856093 ^ b * 19349663 ^ seed * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
            return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx), ty);
        }

        private static float Fbm(int x, int y, int seed) =>
            Tile(x, y, 4, seed) * 0.5f + Tile(x, y, 8, seed + 1) * 0.25f + Tile(x, y, 16, seed + 2) * 0.15f + Tile(x, y, 32, seed + 3) * 0.1f;

        private static Color GrassTexture(int x, int y)
        {
            float n = Fbm(x, y, 11);
            float strokes = Tile(x, y * 3, 64, 12); // short vertical "blade" streaks
            // Deep, slightly warm greens: the ground sits under bright anime light and blue ambient,
            // so its albedo is kept well below the grass blade tips.
            var dark = new Color(0.15f, 0.28f, 0.15f);
            var light = new Color(0.27f, 0.42f, 0.2f);
            var c = Color.Lerp(dark, light, n);
            c = Color.Lerp(c, c * 1.18f, Mathf.Clamp01((strokes - 0.6f) * 3f));
            float dry = Mathf.Clamp01((Tile(x, y, 4, 13) - 0.68f) * 4f);
            return Color.Lerp(c, new Color(0.42f, 0.42f, 0.24f), dry * 0.5f);
        }

        private static Color DirtTexture(int x, int y)
        {
            float n = Fbm(x, y, 21);
            var c = Color.Lerp(new Color(0.4f, 0.31f, 0.23f), new Color(0.56f, 0.45f, 0.33f), n);
            float pebble = Tile(x, y, 48, 22);
            if (pebble > 0.82f)
                c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.62f), (pebble - 0.82f) * 5f);
            return c;
        }

        private static Color RockTexture(int x, int y)
        {
            float n = Fbm(x, y, 31);
            float strata = Mathf.Abs(Mathf.Sin((y / (float)TextureSize * 6f + n * 1.5f) * Mathf.PI * 2f)); // 6 whole cycles: tileable
            var c = Color.Lerp(new Color(0.3f, 0.28f, 0.36f), new Color(0.5f, 0.47f, 0.55f), n);
            return Color.Lerp(c * 0.8f, c, Mathf.SmoothStep(0f, 1f, strata));
        }

        private static Color PlazaTexture(int x, int y)
        {
            const int tiles = 4; // 4 x 4 slabs per texture tile
            int cell = TextureSize / tiles;
            int tx = x / cell, ty = y / cell;
            int lx = x % cell, ly = y % cell;
            bool grout = lx < 3 || ly < 3;
            float tint = Tile(tx * cell, ty * cell, tiles, 41) * 0.15f;
            float n = Fbm(x, y, 42) * 0.12f;
            var stone = new Color(0.33f + tint + n, 0.31f + tint + n, 0.4f + tint + n);
            return grout ? new Color(0.18f, 0.16f, 0.22f) : stone;
        }
    }
}
