using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Core.Zone;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER anime energy barrier for the Nyx Circle (spec 10 "Circle Visual Design"):
    /// a translucent violet/electric-blue wall with a scrolling hexagon pattern, a glowing,
    /// pulsing ground edge, rising energy particles, lightning accents, and a thin white ring
    /// for the next circle. See-through so it never hides players. Client-only; the circle's
    /// position comes from replicated server state. "Reduce flashing" removes the lightning and
    /// softens the pulse.
    /// </summary>
    public sealed class NyxCircleVisual : MonoBehaviour
    {
        private const int Segments = 96;
        private const int EdgePoints = 96;
        private const float WallHeight = 40f;
        private const float WallSink = 3f;
        /// <summary>World size of one hexagon tile on the wall.</summary>
        private const float HexTileMetres = 5f;

        private static readonly Color Violet = new Color(0.54f, 0.17f, 0.89f);
        private static readonly Color Blue = new Color(0.1f, 0.55f, 1f);
        private static Texture2D _hexTexture;

        private Transform _wall;
        private Material _wallMaterial;
        private LineRenderer _edge;
        private LineRenderer _next;
        private LineRenderer _lightning;
        private ParticleSystem _particles;
        private readonly Vector3[] _edgePositions = new Vector3[EdgePoints];
        private readonly Vector3[] _boltPoints = new Vector3[10];

        private ZoneCircle _current;
        private ZoneCircle _shownEdge = new ZoneCircle(float.NaN, 0f, -1f);
        private ZoneCircle _shownNext = new ZoneCircle(float.NaN, 0f, -1f);
        private float _groundY;
        private float _nextBoltAt;
        private float _boltUntil;
        private float _particleBudget;

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public static NyxCircleVisual Create(Transform parent)
        {
            var go = new GameObject("NyxCircle (PLACEHOLDER VFX)");
            go.transform.SetParent(parent, false);
            var visual = go.AddComponent<NyxCircleVisual>();
            visual.Build();
            return visual;
        }

        /// <summary>Called every frame with the interpolated circle.</summary>
        public void Show(in ZoneCircle current, in ZoneCircle next, bool showNext, float groundY)
        {
            _current = current;
            _groundY = groundY;
            bool open = current.Radius > 0.05f;

            if (_wall != null)
            {
                _wall.gameObject.SetActive(open);
                _wall.position = new Vector3(current.X, groundY - WallSink, current.Z);
                _wall.localScale = new Vector3(current.Radius, WallHeight + WallSink, current.Radius);
            }

            if (Moved(_shownEdge, current))
            {
                _shownEdge = current;
                FillRing(_edge, current, 0.25f);
            }
            _edge.enabled = open;

            bool nextVisible = showNext && next.Radius > 0.05f && !next.Equals(current);
            if (nextVisible && Moved(_shownNext, next))
            {
                _shownNext = next;
                FillRing(_next, next, 0.2f);
            }
            _next.enabled = nextVisible;
        }

        private void Update()
        {
            if (_wallMaterial == null)
                return;

            float t = Time.time;
            bool reduce = ReduceFlashing;
            float pulse = reduce ? 0.95f + 0.05f * Mathf.Sin(t * 1.5f) : 0.82f + 0.18f * Mathf.Sin(t * 2.4f);

            // Hexagons keep a constant world size however big the circle is; they drift upward slowly.
            float circumference = 2f * Mathf.PI * Mathf.Max(_current.Radius, 0.1f);
            _wallMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, Mathf.Round(circumference / HexTileMetres)),
                (WallHeight + WallSink) / (HexTileMetres * 1.73f));
            _wallMaterial.mainTextureOffset = new Vector2(0f, -t * 0.04f);
            _wallMaterial.color = new Color(1f, 1f, 1f, pulse);

            var edgeColor = Color.Lerp(Blue, Violet, 0.5f + 0.5f * Mathf.Sin(t * 1.7f));
            _edge.startColor = _edge.endColor = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.9f * pulse);
            _edge.widthMultiplier = 0.6f + (reduce ? 0f : 0.15f * Mathf.Sin(t * 5f));

            EmitParticles(Time.deltaTime);
            UpdateLightning(t, reduce);
        }

        // ------------------------------------------------------------------ effects

        private void EmitParticles(float dt)
        {
            if (_particles == null || _current.Radius <= 0.05f)
                return;
            // Density follows the circumference, capped for mobile.
            float rate = Mathf.Clamp(_current.Radius * 0.6f, 6f, 60f);
            _particleBudget += rate * dt;
            while (_particleBudget >= 1f)
            {
                _particleBudget -= 1f;
                float a = Random.value * Mathf.PI * 2f;
                var p = new Vector3(_current.X + Mathf.Sin(a) * _current.Radius, _groundY + Random.Range(0f, 3f), _current.Z + Mathf.Cos(a) * _current.Radius);
                var emit = new ParticleSystem.EmitParams
                {
                    position = p,
                    velocity = new Vector3(0f, Random.Range(1.5f, 4f), 0f),
                    startLifetime = Random.Range(1.2f, 2.2f),
                    startSize = Random.Range(0.12f, 0.35f),
                    startColor = Random.value < 0.5f ? Violet : Blue,
                    applyShapeToPosition = false,
                };
                _particles.Emit(emit, 1);
            }
        }

        private void UpdateLightning(float t, bool reduce)
        {
            if (reduce || _current.Radius <= 0.5f)
            {
                _lightning.enabled = false;
                return;
            }

            if (t >= _nextBoltAt)
            {
                _nextBoltAt = t + Random.Range(0.5f, 1.6f);
                _boltUntil = t + 0.09f;
                float a = Random.value * Mathf.PI * 2f;
                var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                var basePoint = new Vector3(_current.X + Mathf.Sin(a) * _current.Radius, _groundY, _current.Z + Mathf.Cos(a) * _current.Radius);
                float height = Random.Range(6f, 18f);
                for (int i = 0; i < _boltPoints.Length; i++)
                {
                    float f = i / (float)(_boltPoints.Length - 1);
                    _boltPoints[i] = basePoint + Vector3.up * (height * f) + tangent * (i == 0 ? 0f : Random.Range(-0.9f, 0.9f));
                }
                _lightning.positionCount = _boltPoints.Length;
                _lightning.SetPositions(_boltPoints);
            }
            _lightning.enabled = t < _boltUntil;
        }

        private void FillRing(LineRenderer line, in ZoneCircle circle, float lift)
        {
            for (int i = 0; i < EdgePoints; i++)
            {
                float a = i * Mathf.PI * 2f / EdgePoints;
                float x = circle.X + Mathf.Sin(a) * circle.Radius;
                float z = circle.Z + Mathf.Cos(a) * circle.Radius;
                // Follow the ground so the edge never hides under a hill (players have no colliders on layer 2 hits).
                float y = Physics.Raycast(new Vector3(x, _groundY + 60f, z), Vector3.down, out var hit, 160f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    ? hit.point.y
                    : _groundY;
                _edgePositions[i] = new Vector3(x, y + lift, z);
            }
            line.positionCount = EdgePoints;
            line.SetPositions(_edgePositions);
        }

        private static bool Moved(in ZoneCircle shown, in ZoneCircle now) =>
            float.IsNaN(shown.X) || Mathf.Abs(shown.Radius - now.Radius) > 0.2f
            || Mathf.Abs(shown.X - now.X) > 0.2f || Mathf.Abs(shown.Z - now.Z) > 0.2f;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            var fx = Resources.Load<Material>(SpawnEffect.MaterialResource);
            if (fx == null)
            {
                Debug.LogWarning($"[WarOfNyx][Zone] FX material Resources/{SpawnEffect.MaterialResource} missing - run War of Nyx > Setup. " +
                                 "The circle is shown as plain lines.");
                fx = new Material(Shader.Find("Sprites/Default"));
            }

            _wallMaterial = new Material(fx) { name = "NyxCircleWall (runtime)", mainTexture = HexTexture };
            var wall = new GameObject("Barrier");
            wall.transform.SetParent(transform, false);
            wall.AddComponent<MeshFilter>().sharedMesh = BuildWallMesh();
            var renderer = wall.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _wallMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _wall = wall.transform;

            _edge = CreateLine("Edge", fx, 0.6f);
            _next = CreateLine("NextCircle", fx, 0.25f);
            _next.startColor = _next.endColor = new Color(1f, 1f, 1f, 0.55f);
            _lightning = CreateLine("Lightning", fx, 0.12f);
            _lightning.loop = false;
            _lightning.startColor = new Color(0.75f, 0.85f, 1f, 1f);
            _lightning.endColor = new Color(0.54f, 0.17f, 0.89f, 0.2f);
            _lightning.enabled = false;

            var particlesGo = new GameObject("EnergyParticles");
            particlesGo.transform.SetParent(transform, false);
            _particles = particlesGo.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = false;
            var emission = _particles.emission;
            emission.enabled = false; // particles are emitted manually along the edge
            var shape = _particles.shape;
            shape.enabled = false;
            var colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
            var particleRenderer = particlesGo.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = fx;
            _particles.Play();
        }

        private LineRenderer CreateLine(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>Open cylinder, radius 1, height 1, visible from inside and outside; bright at the base, fading upward.</summary>
        private static Mesh BuildWallMesh()
        {
            int ring = Segments + 1;
            var vertices = new Vector3[ring * 2];
            var uvs = new Vector2[ring * 2];
            var colors = new Color[ring * 2];
            for (int i = 0; i < ring; i++)
            {
                float u = i / (float)Segments;
                float a = u * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                vertices[i] = p;
                vertices[i + ring] = p + Vector3.up;
                uvs[i] = new Vector2(u, 0f);
                uvs[i + ring] = new Vector2(u, 1f);
                colors[i] = new Color(Violet.r, Violet.g, Violet.b, 0.6f);
                colors[i + ring] = new Color(Blue.r, Blue.g, Blue.b, 0f);
            }

            var triangles = new int[Segments * 12];
            int k = 0;
            for (int i = 0; i < Segments; i++)
            {
                int a = i, b = i + 1, c = i + ring, d = i + 1 + ring;
                // Outside face
                triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                // Inside face (reverse winding)
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
            }

            var mesh = new Mesh { name = "NyxCircleWall" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            // The wall is rescaled every frame; generous bounds keep it from being culled.
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1.2f, 2.2f));
            return mesh;
        }

        /// <summary>Tileable pointy-top hexagon grid (white lines, faint fill), generated once.</summary>
        private static Texture2D HexTexture
        {
            get
            {
                if (_hexTexture != null)
                    return _hexTexture;

                const int width = 64;
                const int height = 111; // 64 × 3/√3: one full period of the hex lattice
                float sqrt3 = Mathf.Sqrt(3f);
                float inradius = sqrt3 * 0.5f;
                var pixels = new Color32[width * height];
                for (int py = 0; py < height; py++)
                {
                    for (int px = 0; px < width; px++)
                    {
                        float x = (px + 0.5f) / width * sqrt3;
                        float y = (py + 0.5f) / height * 3f;
                        float best = float.MaxValue;
                        for (int j = -1; j <= 1; j++)
                        {
                            for (int i = -1; i <= 1; i++)
                            {
                                best = Mathf.Min(best, HexMetric(x - sqrt3 * i, y - 3f * j));
                                best = Mathf.Min(best, HexMetric(x - (sqrt3 * i + sqrt3 * 0.5f), y - (3f * j + 1.5f)));
                            }
                        }
                        float line = Mathf.Clamp01(1f - (inradius - best) / 0.09f);
                        byte alpha = (byte)(255f * Mathf.Clamp01(0.14f + line * 0.86f));
                        pixels[py * width + px] = new Color32(255, 255, 255, alpha);
                    }
                }

                _hexTexture = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = "NyxHexGrid (runtime)",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                };
                _hexTexture.SetPixels32(pixels);
                _hexTexture.Apply(true, true);
                return _hexTexture;
            }
        }

        /// <summary>Hexagon "radius" of a point: √3/2 on the edge of a unit pointy-top hexagon.</summary>
        private static float HexMetric(float x, float y)
        {
            x = Mathf.Abs(x);
            y = Mathf.Abs(y);
            return Mathf.Max(x, x * 0.5f + y * 0.8660254f);
        }

        private void OnDestroy()
        {
            if (_wallMaterial != null)
                Destroy(_wallMaterial);
        }
    }
}
