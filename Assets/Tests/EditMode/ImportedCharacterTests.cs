using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using WarOfNyx.EditorTools.Characters;
using WarOfNyx.EditorTools.Characters.Import;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.Tests
{
    /// <summary>
    /// The supplied-model import pipeline: glTF reading and space conversion, joint-name mapping, simplification,
    /// T-pose measurement and automatic weights (checked against KAI, whose true skeleton is known), and, after
    /// project setup with the files present, the generated imported characters.
    /// </summary>
    public class ImportedCharacterTests
    {
        [Test]
        public void MiniJson_ParsesObjectsArraysNumbersAndEscapes()
        {
            var o = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [1, 2.5, -3e2], \"b\": {\"c\": \"x\\\"y\\u0041\"}, \"t\": true, \"n\": null}");
            var a = (List<object>)o["a"];
            Assert.That(a.Select(v => (double)v), Is.EqualTo(new[] { 1.0, 2.5, -300.0 }));
            Assert.That(((Dictionary<string, object>)o["b"])["c"], Is.EqualTo("x\"yA"));
            Assert.That(o["t"], Is.EqualTo(true));
            Assert.That(o["n"], Is.Null);
        }

        /// <summary>A one-triangle .glb with a translated node and Sketchfab-style credit.</summary>
        private static string WriteTinyGlb()
        {
            var bin = new List<byte>();
            foreach (var f in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f })
                bin.AddRange(BitConverter.GetBytes(f));
            foreach (var i in new ushort[] { 0, 1, 2 })
                bin.AddRange(BitConverter.GetBytes(i));
            while (bin.Count % 4 != 0)
                bin.Add(0);
            string json = "{\"asset\":{\"version\":\"2.0\",\"extras\":{\"title\":\"Tiny\",\"author\":\"Someone (https://example.com)\",\"license\":\"CC-BY-4.0 (x)\"}}," +
                          "\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0,\"translation\":[0,2,0]}]," +
                          "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
                          "\"buffers\":[{\"byteLength\":" + bin.Count + "}]," +
                          "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6}]," +
                          "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
            var jsonBytes = new List<byte>(Encoding.UTF8.GetBytes(json));
            while (jsonBytes.Count % 4 != 0)
                jsonBytes.Add((byte)' ');
            var file = new List<byte>();
            file.AddRange(BitConverter.GetBytes(0x46546C67u));
            file.AddRange(BitConverter.GetBytes(2u));
            file.AddRange(BitConverter.GetBytes((uint)(12 + 8 + jsonBytes.Count + 8 + bin.Count)));
            file.AddRange(BitConverter.GetBytes((uint)jsonBytes.Count));
            file.AddRange(BitConverter.GetBytes(0x4E4F534Au));
            file.AddRange(jsonBytes);
            file.AddRange(BitConverter.GetBytes((uint)bin.Count));
            file.AddRange(BitConverter.GetBytes(0x004E4942u));
            file.AddRange(bin);
            string path = Path.Combine(Path.GetTempPath(), "won_tiny_test.glb");
            File.WriteAllBytes(path, file.ToArray());
            return path;
        }

        [Test]
        public void GlbReader_ConvertsToUnitySpace_AndReadsTheCredit()
        {
            var model = SourceModel.Load(WriteTinyGlb());
            Assert.That(model.VertexCount, Is.EqualTo(3));
            // Node translation applied, X mirrored (glTF is right-handed).
            Assert.That(model.Positions[0], Is.EqualTo(new Vector3(0f, 2f, 0f)));
            Assert.That(model.Positions[1], Is.EqualTo(new Vector3(-1f, 2f, 0f)));
            // Winding reversed so the triangle still faces the same way after mirroring.
            Assert.That(model.Triangles[0], Is.EqualTo(new List<int> { 0, 2, 1 }));
            Assert.That(model.Credit.Title, Is.EqualTo("Tiny"));
            Assert.That(model.Credit.License, Does.StartWith("CC-BY-4.0"));
        }

        [Test]
        public void JointNames_MapToTheSharedSkeleton()
        {
            Assert.That(SkinConverter.Map("CC_Base_L_Upperarm_050", out bool primary), Is.EqualTo("LeftUpperArm"));
            Assert.That(primary);
            Assert.That(SkinConverter.Map("CC_Base_R_ForearmTwist01_063", out primary), Is.EqualTo("RightLowerArm"));
            Assert.That(primary, Is.False, "twist helpers merge into the main bone");
            Assert.That(SkinConverter.Map("CC_Base_Spine02_036", out _), Is.EqualTo("UpperChest"));
            Assert.That(SkinConverter.Map("CC_Base_JawRoot_041", out _), Is.EqualTo("Head"));
            Assert.That(SkinConverter.Map("CC_Base_L_BigToe1_013", out _), Is.EqualTo("LeftToes"));
            Assert.That(SkinConverter.Map("mixamorig:LeftUpLeg", out _), Is.EqualTo("LeftUpperLeg"));
            Assert.That(SkinConverter.Map("mixamorig:RightHandPinky2", out _), Is.EqualTo("RightLittle2"));
            Assert.That(SkinConverter.Map("mixamorig:Spine1", out _), Is.EqualTo("Chest"));
        }

        [Test]
        public void LodBuilder_ProducesSmallerNestedLevels()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                var model = new SourceModel
                {
                    Positions = mesh.vertices, Normals = mesh.normals, Uv = mesh.uv,
                    Triangles = new[] { new List<int>(mesh.triangles) }, Materials = new[] { new SourceMaterial() },
                };
                var levels = LodBuilder.Generate(model);
                Assert.That(levels.Count, Is.GreaterThan(2));
                for (int i = 1; i < levels.Count; i++)
                {
                    Assert.That(levels[i].TriangleCount, Is.LessThan(levels[i - 1].TriangleCount));
                    var used = new HashSet<int>(levels[i].Triangles[0]);
                    Assert.That(new HashSet<int>(levels[i - 1].Triangles[0]).IsSupersetOf(used), "each level reuses the previous level's vertices");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sphere);
            }
        }

        private static SkinnedCharacterMeshes _kai;
        private static SkinnedCharacterMeshes Kai => _kai ??= KaiModel.Build();

        [Test]
        public void HumanoidFitter_FindsKaisJoints_FromHisMeshAlone()
        {
            var fit = HumanoidFitter.FitTPose(Kai.Lods[0].vertices, Kai.Lods[0].triangles);
            foreach (var line in fit.Notes)
                TestContext.WriteLine(line);
            var truth = Kai.Skeleton;
            void Near(string bone, float tolerance)
            {
                float error = Vector3.Distance(fit.Heads[bone], truth[bone].Head);
                TestContext.WriteLine($"{bone}: measured {fit.Heads[bone]} true {truth[bone].Head} error {error * 100f:0.0} cm");
                Assert.That(error, Is.LessThan(tolerance), bone);
            }
            foreach (var side in new[] { "Left", "Right" })
            {
                Near(side + "Hand", 0.05f);
                Near(side + "LowerArm", 0.06f);
                Near(side + "UpperArm", 0.07f);
                Near(side + "Foot", 0.07f);
                Near(side + "LowerLeg", 0.08f);
            }
            Near("Head", 0.1f);
            Assert.That(fit.Heads["LeftHand"].x, Is.LessThan(0f), "the character's left is -X");
        }

        [Test]
        public void AutoSkinner_WeightsKaisMesh_Plausibly()
        {
            var mesh = Kai.Lods[1];
            var positions = mesh.vertices;
            var fit = HumanoidFitter.FitTPose(Kai.Lods[0].vertices, Kai.Lods[0].triangles);
            var skeleton = NyxSkeleton.Fitted(fit.Heads, fit.Tails);
            var vertices = Enumerable.Range(0, positions.Length).ToArray();
            var weights = AutoSkinner.Skin(skeleton, fit, positions, vertices, new[] { new List<int>(mesh.triangles) });
            int leftLeg = 0, wrongSide = 0;
            var byPosition = new Dictionary<Vector3, BoneWeight>();
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1f).Within(1e-3f));
                var p = positions[i];
                string main = skeleton.Bones[w.boneIndex0].Name;
                if (p.y < 0.3f && p.x < -0.05f && main.StartsWith("Left") && (main.Contains("Leg") || main.Contains("Foot") || main.Contains("Toes")))
                    leftLeg++;
                if (p.x > 0.05f && main.StartsWith("Left"))
                    wrongSide++;
                if (byPosition.TryGetValue(p, out var other))
                    Assert.That(other, Is.EqualTo(w), "copies of a seam vertex share their weights");
                else
                    byPosition[p] = w;
            }
            Assert.That(leftLeg, Is.GreaterThan(100), "the left shin and foot follow the left leg");
            Assert.That(wrongSide, Is.EqualTo(0), "no left bone owns the right side");
        }

        [Test]
        public void ImportedCharacters_AreRiggedValidAndCredited()
        {
            int checkedCount = 0;
            foreach (var spec in ImportedCharacterCatalog.All)
            {
                var profile = Resources.Load<CharacterVisualProfile>(SkinnedCharacterLoader.ResourceFolder + spec.Slot);
                if (profile == null || profile.artSource == null || !profile.artSource.StartsWith("Imported"))
                    continue;
                checkedCount++;
                var animator = profile.prefab.GetComponent<Animator>();
                Assert.That(animator.avatar.isHuman && animator.avatar.isValid, spec.Slot + ": valid humanoid avatar");
                Assert.That(profile.prefab.GetComponent<LODGroup>().lodCount, Is.EqualTo(3));
                Assert.That(profile.credit, Does.Contain(" by "), spec.Slot + ": attribution present");
                Assert.That(profile.faceSubmesh, Is.LessThan(0), "imported faces are part of the model");
                int previous = int.MaxValue;
                foreach (var r in profile.prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = r.sharedMesh;
                    int triangles = mesh.triangles.Length / 3;
                    TestContext.WriteLine($"{spec.Slot} {mesh.name}: {triangles} triangles, {mesh.vertexCount} vertices");
                    Assert.That(triangles, Is.LessThan(previous));
                    Assert.That(triangles, Is.LessThanOrEqualTo(spec.Lod0 * 1.08f));
                    previous = triangles;
                    Assert.That(ImportDiagnostics.OpenEdges(mesh, out _), Is.EqualTo(0), mesh.name + ": no cracks");
                    foreach (var w in mesh.boneWeights)
                        Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1f).Within(1e-3f));
                }
                var clips = profile.animation.controller.animationClips.Select(c => c.name.Substring(spec.Slot.Length + 1)).ToList();
                foreach (var slot in CharacterAnimatorParams.Clips)
                    Assert.That(clips, Does.Contain(slot), spec.Slot + " clip " + slot);
            }
            if (checkedCount == 0)
                Assert.Ignore("No supplied models imported (ArtSource/Characters is empty on this machine).");
        }
    }
}
