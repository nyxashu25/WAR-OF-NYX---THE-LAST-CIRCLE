using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Tests
{
    /// <summary>The detailed roster models: complete, within budget, well-formed, and every character's story fits together.</summary>
    public class CharacterModelTests
    {
        private static readonly string[] Parts =
        {
            "Pelvis", "Spine", CharacterBodyNames.Head, CharacterBodyNames.Eyes,
            "UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR",
            "ThighL", "ThighR", "ShinL", "ShinR", "FootL", "FootR",
        };

        [Test]
        public void EveryCharacterHasEveryBodyPart_WithinTheVertexBudget()
        {
            foreach (var look in CharacterRoster.All)
            {
                var model = CharacterModels.Get(look);
                foreach (var part in Parts)
                {
                    Assert.That(model.Meshes.ContainsKey(part), part + " of " + look.Name);
                    Assert.That(model.Meshes[part].vertexCount, Is.GreaterThan(20), part + " of " + look.Name);
                }
                // A whole detailed character: ~16 draw calls and a phone-friendly vertex count.
                Assert.That(model.Vertices, Is.InRange(4000, 24000), look.Name + " vertices");
                Assert.That(model.Meshes.Count, Is.LessThan(32), look.Name + " renderers");
            }
        }

        [Test]
        public void Meshes_AreFinite_AndFaceOutward()
        {
            foreach (var look in CharacterRoster.All)
            foreach (var pair in CharacterModels.Get(look).Meshes)
            {
                var mesh = pair.Value;
                var v = mesh.vertices;
                var n = mesh.normals;
                var c = mesh.colors;
                Assert.That(c.Length, Is.EqualTo(v.Length), pair.Key + " colours");
                for (int i = 0; i < v.Length; i++)
                {
                    Assert.That(float.IsNaN(v[i].x + v[i].y + v[i].z), Is.False, pair.Key);
                    Assert.That(c[i].a, Is.InRange(0f, 1f), pair.Key);
                }
                var t = mesh.triangles;
                int good = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) >= 0f)
                        good++;
                }
                Assert.That(good, Is.EqualTo(t.Length / 3), $"{look.Name} {pair.Key}: every triangle's front face matches its normals");
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
    }
}
