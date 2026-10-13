using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Building blocks for the PLACEHOLDER anime presentation (Phase 10): primitives with the
    /// cel-shaded <c>WarOfNyx/NyxToon</c> material (colour per renderer through property blocks,
    /// so one material is shared), plus procedural cones for hair spikes and blade tips.
    /// All parts are visual only: colliders are removed, so they never affect hits or movement.
    /// </summary>
    public static class StyleKit
    {
        public const string ToonMaterialResource = "WarOfNyx/NyxToon";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private static Material _toon;
        private static Mesh _cone;
        private static MaterialPropertyBlock _block;

        public static Material Toon
        {
            get
            {
                if (_toon != null)
                    return _toon;
                _toon = Resources.Load<Material>(ToonMaterialResource);
                if (_toon == null)
                {
                    var shader = Shader.Find("WarOfNyx/NyxToon") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _toon = new Material(shader) { name = "NyxToon (runtime fallback)" };
                }
                return _toon;
            }
        }

        public static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color color,
            Vector3 euler = default, Color emission = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Remove(go.GetComponent<Collider>());
            Setup(go, parent, position, scale, euler, color, emission);
            return go.transform;
        }

        /// <summary>A cone pointing along local +Y (base radius 0.5, height 1 before scaling).</summary>
        public static Transform Cone(string name, Transform parent, Vector3 position, Vector3 scale, Color color, Vector3 euler,
            Color emission = default)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh;
            go.AddComponent<MeshRenderer>();
            Setup(go, parent, position, scale, euler, color, emission);
            return go.transform;
        }

        /// <summary>Destroy that also works in edit mode (screenshot / preview tools).</summary>
        public static void Remove(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        /// <summary>No GPU (dedicated server, -nographics tests): skip building visuals.</summary>
        public static bool Headless => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

        public static void Paint(Renderer renderer, Color color, Color emission = default)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _block.SetColor(EmissionId, emission);
            renderer.SetPropertyBlock(_block);
        }

        private static void Setup(GameObject go, Transform parent, Vector3 position, Vector3 scale, Vector3 euler, Color color, Color emission)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Toon;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Paint(renderer, color, emission);
        }

        private static Mesh ConeMesh
        {
            get
            {
                if (_cone != null)
                    return _cone;
                const int segments = 10;
                var vertices = new Vector3[segments * 2 + 2];
                var triangles = new int[segments * 6];
                vertices[0] = Vector3.up;       // tip
                vertices[1] = Vector3.zero;     // base centre
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    var p = new Vector3(Mathf.Sin(a) * 0.5f, 0f, Mathf.Cos(a) * 0.5f);
                    vertices[2 + i] = p;                 // side ring
                    vertices[2 + segments + i] = p;      // base ring (separate normals)
                }
                int t = 0;
                for (int i = 0; i < segments; i++)
                {
                    int a = 2 + i, b = 2 + (i + 1) % segments;
                    triangles[t++] = 0; triangles[t++] = a; triangles[t++] = b;
                    int c = 2 + segments + i, d = 2 + segments + (i + 1) % segments;
                    triangles[t++] = 1; triangles[t++] = d; triangles[t++] = c;
                }
                _cone = new Mesh { name = "NyxCone", vertices = vertices, triangles = triangles };
                _cone.RecalculateNormals();
                _cone.RecalculateBounds();
                return _cone;
            }
        }
    }
}
