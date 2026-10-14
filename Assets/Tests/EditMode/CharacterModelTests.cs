using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Tests
{
    /// <summary>The detailed roster models: complete at every detail level, within budget, well-formed, material-rich,
    /// and every character's story fits together.</summary>
    public class CharacterModelTests
    {
        private static readonly string[] Parts =
        {
            "Pelvis", "Spine", CharacterBodyNames.Head, CharacterBodyNames.Eyes,
            "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
            "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR",
        };

        // Vertex budgets per detail level: close-up, mid range, distance.
        private static readonly int[] MaxVertices = { 90000, 34000, 14000 };

        [Test]
        public void EveryCharacterHasEveryBodyPart_AtEveryLevel_WithinTheVertexBudget()
        {
            foreach (var look in CharacterRoster.All)
            {
                int previous = int.MaxValue;
                for (int lod = 0; lod < CharacterModels.LodCount; lod++)
                {
                    var model = CharacterModels.Get(look, lod);
                    foreach (var part in Parts)
                    {
                        Assert.That(model.Meshes.ContainsKey(part), $"{part} of {look.Name} LOD{lod}");
                        Assert.That(model.Meshes[part].vertexCount, Is.GreaterThan(20), $"{part} of {look.Name} LOD{lod}");
                    }
                    TestContext.WriteLine($"{look.Name} LOD{lod}: {model.Vertices} vertices, {model.Meshes.Count} meshes");
                    Assert.That(model.Vertices, Is.InRange(2000, MaxVertices[lod]), $"{look.Name} LOD{lod} vertices");
                    Assert.That(model.Vertices, Is.LessThan(previous), $"{look.Name}: LOD{lod} is lighter than the level before");
                    Assert.That(model.Meshes.Count, Is.LessThan(32), look.Name + " renderers");
                    previous = model.Vertices;
                }
            }
        }

        [Test]
        public void EveryLevel_HasTheSameParts_SoTheLodGroupSwapsWholeCharacters()
        {
            foreach (var look in CharacterRoster.All)
            {
                var close = CharacterModels.Get(look, 0);
                for (int lod = 1; lod < CharacterModels.LodCount; lod++)
                {
                    var far = CharacterModels.Get(look, lod);
                    Assert.That(far.Meshes.Keys, Is.EquivalentTo(close.Meshes.Keys), $"{look.Name} LOD{lod}");
                    Assert.That(far.Dangles.Count, Is.EqualTo(close.Dangles.Count), $"{look.Name} LOD{lod} cloth chains");
                }
            }
        }

        [Test]
        public void Meshes_AreFinite_FaceOutward_AndCarryMaterials()
        {
            var uv2 = new List<Vector4>();
            foreach (var look in CharacterRoster.All)
                for (int lod = 0; lod < CharacterModels.LodCount; lod++)
                    foreach (var pair in CharacterModels.Get(look, lod).Meshes)
                    {
                        var mesh = pair.Value;
                        string where = $"{look.Name} LOD{lod} {pair.Key}";
                        var v = mesh.vertices;
                        var n = mesh.normals;
                        var c = mesh.colors;
                        Assert.That(c.Length, Is.EqualTo(v.Length), where + " colours");
                        Assert.That(mesh.tangents.Length, Is.EqualTo(v.Length), where + " tangents");
                        mesh.GetUVs(1, uv2);
                        Assert.That(uv2.Count, Is.EqualTo(v.Length), where + " material channel");
                        for (int i = 0; i < v.Length; i++)
                        {
                            Assert.That(float.IsNaN(v[i].x + v[i].y + v[i].z), Is.False, where);
                            Assert.That(c[i].a, Is.InRange(0f, 1f), where);
                            Assert.That(uv2[i].x, Is.InRange(Surf.Cloth, Surf.Lacquer), where + " material id");
                            Assert.That(Mathf.Abs(uv2[i].x - Mathf.Round(uv2[i].x)), Is.LessThan(1e-4f), where + " material id is whole");
                            Assert.That(uv2[i].y, Is.InRange(0f, 1f), where + " occlusion");
                            Assert.That(uv2[i].z, Is.InRange(0f, 1f), where + " outline weight");
                        }
                        var t = mesh.triangles;
                        int good = 0;
                        for (int i = 0; i < t.Length; i += 3)
                        {
                            var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                            if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) >= 0f)
                                good++;
                        }
                        Assert.That(good, Is.EqualTo(t.Length / 3), $"{where}: every triangle's front face matches its normals");
                    }
        }

        [Test]
        public void CloseUpModels_UseManyMaterials_AndFoldsDarkenCreases()
        {
            var uv2 = new List<Vector4>();
            foreach (var look in CharacterRoster.All)
            {
                var materials = new HashSet<int>();
                float darkest = 1f;
                foreach (var mesh in CharacterModels.Get(look, 0).Meshes.Values)
                {
                    mesh.GetUVs(1, uv2);
                    foreach (var s in uv2)
                    {
                        materials.Add(Mathf.RoundToInt(s.x));
                        darkest = Mathf.Min(darkest, s.y);
                    }
                }
                TestContext.WriteLine($"{look.Name}: {materials.Count} materials");
                Assert.That(materials.Count, Is.GreaterThanOrEqualTo(8), look.Name + ": skin, hair, eyes, fabrics, leather, metal, polymer...");
                foreach (var required in new[] { Surf.Skin, Surf.Hair, Surf.Eye })
                    Assert.That(materials.Contains(Mathf.RoundToInt(required)), look.Name + " material " + required);
                Assert.That(darkest, Is.LessThan(0.97f), look.Name + ": cloth folds bake occlusion into their creases");
            }
        }

        [Test]
        public void ClothChains_HaveAMeshPerSegment()
        {
            foreach (var look in CharacterRoster.All)
            {
                var model = CharacterModels.Get(look);
                foreach (var chain in model.Dangles)
                    for (int i = 0; i < chain.Segments; i++)
                        Assert.That(model.Meshes.ContainsKey($"{chain.Name}_{i}"), $"{look.Name} {chain.Name}_{i}");
            }
            Assert.That(CharacterModels.Get(CharacterRoster.Get(0)).Dangles.Count, Is.GreaterThanOrEqualTo(4), "KAI: scarf tails and coat panels");
        }

        [Test]
        public void HeadsAndHair_StayWithinAPlausibleHeight()
        {
            foreach (var look in CharacterRoster.All)
            {
                var head = CharacterModels.Get(look).Meshes[CharacterBodyNames.Head];
                // Head joint is 1.48 m above the ground: hair may rise to ~2 m, no further (the hit shapes end at ~1.82 m).
                Assert.That(head.bounds.max.y, Is.LessThan(0.52f), look.Name);
                Assert.That(head.bounds.min.y, Is.GreaterThan(-0.12f), look.Name);
            }
        }

        [Test]
        public void EveryCharacterHasAStory_GearAndAValidSignatureWeapon()
        {
            foreach (var look in CharacterRoster.All)
            {
                Assert.That(look.Role, Is.Not.Empty, look.Name);
                Assert.That(look.Story.Length, Is.GreaterThan(80), look.Name);
                Assert.That(look.Gear.Length, Is.GreaterThanOrEqualTo(4), look.Name);
                Assert.That(WeaponCatalog.Get(look.SignatureWeapon), Is.Not.Null, look.Name + " signature weapon");
            }
            // The gear fits the story.
            Assert.That(WeaponCatalog.Get(CharacterRoster.Get(0).SignatureWeapon).IsMelee, "KAI the blade duelist carries a blade");
            Assert.That(CharacterRoster.Get(3).SignatureWeapon, Is.EqualTo(WeaponCatalog.CrimsonShotgun), "REN's bandolier holds shotgun shells");
        }

        [Test]
        public void CharactersAreDistinct_AndModelsAreCached()
        {
            var a = CharacterModels.Get(CharacterRoster.Get(0));
            Assert.That(CharacterModels.Get(CharacterRoster.Get(0)), Is.SameAs(a), "built once, shared by every instance");
            for (int i = 1; i < CharacterRoster.Count; i++)
                Assert.That(CharacterModels.Get(CharacterRoster.Get(i)).Meshes[CharacterBodyNames.Head].vertexCount,
                    Is.Not.EqualTo(a.Meshes[CharacterBodyNames.Head].vertexCount), "different hair and faces");
        }

        [Test]
        public void RoundBox_HasBevelledOutwardNormals()
        {
            var kit = new MeshKit();
            kit.RoundBox(new Vector3(0.1f, 0.06f, 0.04f), 0.01f, Color.white, Matrix4x4.identity);
            var mesh = kit.ToMesh("roundbox");
            var v = mesh.vertices;
            var n = mesh.normals;
            int diagonal = 0;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.That(Vector3.Dot(n[i], v[i]), Is.GreaterThan(0f), "normals point away from the centre");
                if (Mathf.Abs(n[i].x) > 0.2f && Mathf.Abs(n[i].y) > 0.2f)
                    diagonal++;
            }
            Assert.That(diagonal, Is.GreaterThan(0), "edges are rounded (normals between faces)");
            Assert.That(mesh.bounds.size.x, Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void Resample_KeepsTheProfileEnds_AndAddsRings()
        {
            var rings = new[] { new Ring(0f, 0.1f, 0.1f), new Ring(0.2f, 0.15f, 0.12f), new Ring(0.3f, 0.05f, 0.05f) };
            var smooth = MeshKit.Resample(rings, 0.01f);
            Assert.That(smooth.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(smooth[0].Y, Is.EqualTo(0f));
            Assert.That(smooth[smooth.Count - 1].Rx, Is.EqualTo(0.05f));
            for (int i = 1; i < smooth.Count; i++)
                Assert.That(smooth[i].Y, Is.GreaterThan(smooth[i - 1].Y));
        }
    }
}
