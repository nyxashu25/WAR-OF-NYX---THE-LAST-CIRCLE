using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.World;

namespace WarOfNyx.Tests
{
    /// <summary>Procedural vegetation meshes and the runtime weather system.</summary>
    public class EnvironmentTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null)
                    Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Keep<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        // ------------------------------------------------------------------ meshes

        private static void AssertFinite(Mesh mesh)
        {
            foreach (var v in mesh.vertices)
                Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x + v.y + v.z), Is.False, mesh.name);
            foreach (var c in mesh.colors)
                Assert.That(c.a, Is.InRange(0f, 1f), mesh.name + " wind weight");
        }

        /// <summary>Share of triangles whose winding (front face) agrees with their vertex normals.</summary>
        private static float FrontFacingShare(Mesh mesh, int submesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.GetTriangles(submesh);
            int good = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) > 0f)
                    good++;
            }
            return t.Length == 0 ? 1f : good / (t.Length / 3f);
        }

        [Test]
        public void Trees_AreValid_WithinBudget_AndFaceOutward()
        {
            foreach (TreeKind kind in System.Enum.GetValues(typeof(TreeKind)))
            for (int seed = 1; seed <= 3; seed++)
            {
                var lod0 = Keep(VegetationMeshes.Tree(kind, seed, lowDetail: false));
                var lod1 = Keep(VegetationMeshes.Tree(kind, seed, lowDetail: true));
                foreach (var mesh in new[] { lod0, lod1 })
                {
                    AssertFinite(mesh);
                    Assert.That(mesh.subMeshCount, Is.EqualTo(2), mesh.name);
                    Assert.That(mesh.GetTriangles(0).Length, Is.GreaterThan(0), mesh.name + " bark");
                    Assert.That(mesh.GetTriangles(1).Length, Is.GreaterThan(0), mesh.name + " leaves");
                    Assert.That(mesh.bounds.size.y, Is.InRange(4f, 16f), mesh.name + " height");
                    Assert.That(mesh.bounds.min.y, Is.GreaterThan(-0.5f), mesh.name + " sits on the ground");
                    Assert.That(FrontFacingShare(mesh, 0), Is.GreaterThan(0.95f), mesh.name + " bark winding");
                    if (kind != TreeKind.Palm) // palm fronds are two-sided
                        Assert.That(FrontFacingShare(mesh, 1), Is.GreaterThan(0.9f), mesh.name + " canopy winding");
                }
                Assert.That(lod0.vertexCount, Is.LessThan(3500), lod0.name + " LOD0 budget");
                Assert.That(lod1.vertexCount, Is.LessThan(lod0.vertexCount * 0.6f), lod1.name + " LOD1 must be cheaper");
                // Same silhouette at both levels (no visible pop): heights within 10 %.
                Assert.That(lod1.bounds.size.y, Is.EqualTo(lod0.bounds.size.y).Within(lod0.bounds.size.y * 0.1f), kind + " LOD heights");
            }
        }

        [Test]
        public void Trees_DontSwayAtTheirRoots()
        {
            var mesh = Keep(VegetationMeshes.Tree(TreeKind.Broadleaf, 5, lowDetail: false));
            var v = mesh.vertices;
            var c = mesh.colors;
            for (int i = 0; i < v.Length; i++)
                if (v[i].y < 0.5f)
                    Assert.That(c[i].a, Is.LessThan(0.02f), "trunk base must stay planted");
        }

        [Test]
        public void Grass_IsShorterThanAnyCharacterCanHideBehind()
        {
            Assert.That(VegetationMeshes.MaxGrassHeight * 1.1f, Is.LessThan(0.65f), "detail scale included");
            for (int seed = 0; seed < 20; seed++)
            {
                foreach (GrassStyle style in System.Enum.GetValues(typeof(GrassStyle)))
                {
                    var clump = Keep(VegetationMeshes.GrassClump(seed, style));
                    AssertFinite(clump);
                    Assert.That(clump.bounds.max.y, Is.LessThanOrEqualTo(VegetationMeshes.MaxGrassHeight + 0.01f), style.ToString());
                    Assert.That(clump.vertexCount, Is.LessThan(260), style + " vertex budget (instanced by the thousand)");
                }
                var grass = Keep(VegetationMeshes.GrassClump(seed));
                AssertFinite(grass);
                Assert.That(grass.bounds.max.y, Is.LessThanOrEqualTo(VegetationMeshes.MaxGrassHeight + 0.01f));
                var flowers = Keep(VegetationMeshes.FlowerClump(seed, Color.magenta));
                Assert.That(flowers.bounds.max.y, Is.LessThanOrEqualTo(VegetationMeshes.MaxGrassHeight + 0.01f));
            }
        }

        [Test]
        public void Bushes_GiveCover_AndLod1IsCheaper()
        {
            for (int seed = 0; seed < 6; seed++)
            {
                var lod0 = Keep(VegetationMeshes.Bush(seed, Color.green, false));
                var lod1 = Keep(VegetationMeshes.Bush(seed, Color.green, true));
                AssertFinite(lod0);
                Assert.That(lod0.bounds.size.y, Is.InRange(0.9f, 2.4f), "tall enough to hide a crouched player");
                Assert.That(lod1.vertexCount, Is.LessThan(lod0.vertexCount));
                Assert.That(FrontFacingShare(lod0, 0), Is.GreaterThan(0.9f));
            }
        }

        [Test]
        public void Rocks_FaceOutward_AndAreDeterministic()
        {
            var a = Keep(VegetationMeshes.Rock(9, moss: true));
            var b = Keep(VegetationMeshes.Rock(9, moss: true));
            Assert.That(a.vertices, Is.EqualTo(b.vertices));
            AssertFinite(a);
            var v = a.vertices;
            var t = a.triangles;
            var centre = a.bounds.center;
            int outward = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                var mid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                if (Vector3.Dot(face, mid - centre) > 0f)
                    outward++;
            }
            Assert.That(outward / (t.Length / 3f), Is.GreaterThan(0.95f), "visible from outside (back faces are culled)");
        }

        // ------------------------------------------------------------------ character IK

        [Test]
        public void TwoBoneIk_ReachesTargets_AndBendsTowardThePole()
        {
            var shoulder = Keep(new GameObject("Upper")).transform;
            var elbow = new GameObject("Lower").transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(0f, -0.3f, 0f);
            var hand = new GameObject("End").transform;
            hand.SetParent(elbow, false);
            hand.localPosition = new Vector3(0f, -0.3f, 0f);
            shoulder.position = new Vector3(0f, 1.4f, 0f);

            var random = new System.Random(5);
            for (int i = 0; i < 200; i++)
            {
                var target = shoulder.position + RandomDirection(random) * (float)(0.1 + random.NextDouble() * 0.48);
                var pole = new Vector3(0f, 0f, 1f);
                WarOfNyx.Gameplay.Effects.Ik.SolveTwoBone(shoulder, elbow, 0.3f, 0.3f, target, pole);
                Assert.That(Vector3.Distance(hand.position, target), Is.LessThan(0.01f), "reachable target");
                var mid = (shoulder.position + target) * 0.5f;
                Assert.That(Vector3.Dot(elbow.position - mid, Vector3.ProjectOnPlane(pole, (target - shoulder.position).normalized)), Is.GreaterThanOrEqualTo(-1e-3f),
                    "the joint bends toward the pole");
            }
            // Too far: the limb straightens and points at the target.
            var far = shoulder.position + new Vector3(0f, 0f, 2f);
            WarOfNyx.Gameplay.Effects.Ik.SolveTwoBone(shoulder, elbow, 0.3f, 0.3f, far, Vector3.down);
            Assert.That(Vector3.Distance(hand.position, shoulder.position + new Vector3(0f, 0f, 0.6f)), Is.LessThan(0.01f));
        }

        private static Vector3 RandomDirection(System.Random r)
        {
            var v = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 2f - 1f);
            return v.sqrMagnitude < 1e-4f ? Vector3.down : v.normalized;
        }

        // ------------------------------------------------------------------ weather

        [Test]
        public void Weather_AppliesLighting_AndNeverFogsBelowTheMinimum()
        {
            bool fog = RenderSettings.fog;
            float fogEnd = RenderSettings.fogEndDistance, fogStart = RenderSettings.fogStartDistance;
            var fogMode = RenderSettings.fogMode;
            var sunBefore = RenderSettings.sun;
            try
            {
                var light = Keep(new GameObject("Sun")).AddComponent<Light>();
                light.type = LightType.Directional;
                RenderSettings.sun = light;
                var weather = Keep(new GameObject("Weather")).AddComponent<WeatherSystem>();

                weather.Begin(new AtmospherePlan { Time = TimeOfDay.Night, Start = WeatherKind.Fog, Later = WeatherKind.Fog, ChangeAt = float.PositiveInfinity });
                Assert.That(weather.Running, Is.True);
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(RenderSettings.fogEndDistance, Is.GreaterThanOrEqualTo(Atmosphere.MinimumVisibilityMetres));
                Assert.That(light.intensity, Is.LessThan(1f), "moonlight");
                Assert.That(light.intensity, Is.GreaterThan(0.2f), "night must stay playable");
                Assert.That(Shader.GetGlobalFloat("_NyxNight"), Is.EqualTo(1f));

                weather.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                Assert.That(light.intensity, Is.GreaterThan(1f), "daylight");
                Assert.That(Shader.GetGlobalFloat("_NyxNight"), Is.EqualTo(0f));
                Assert.That(Shader.GetGlobalFloat("_NyxRain"), Is.EqualTo(0f));
                Assert.That(light.transform.forward.y, Is.LessThan(-0.5f), "midday sun is high");
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogMode = fogMode;
                RenderSettings.sun = sunBefore;
            }
        }

        [Test]
        public void TerrainDetail_IsSparserOnLowerTiers()
        {
            var low = TerrainDetailQuality.For(Settings.GraphicsQualityTier.Low);
            var medium = TerrainDetailQuality.For(Settings.GraphicsQualityTier.Medium);
            var high = TerrainDetailQuality.For(Settings.GraphicsQualityTier.High);
            Assert.That(low.density, Is.LessThan(medium.density));
            Assert.That(medium.density, Is.LessThan(high.density));
            Assert.That(low.distance, Is.LessThan(high.distance));
            Assert.That(low.density, Is.GreaterThan(0f), "Low still shows some grass");
        }
    }
}
