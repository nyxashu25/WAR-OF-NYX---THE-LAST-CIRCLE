using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.Tests
{
    /// <summary>
    /// The skinned character pipeline (section 40): a Unity-humanoid skeleton, a continuous skinned mesh with clean
    /// weights at every detail level within the triangle budgets, a painted face with expressions, and (after project
    /// setup) the generated prefab, avatar, clips and controller.
    /// </summary>
    public class SkinnedCharacterTests
    {
        private static SkinnedCharacterMeshes _kai;
        private static SkinnedCharacterMeshes Kai => _kai ??= KaiModel.Build();

        [Test]
        public void Skeleton_MapsEveryRequiredHumanoidBone_InATPose()
        {
            var s = NyxSkeleton.Build(RigProportions.Lean);
            var human = new HashSet<string>(HumanTrait.BoneName);
            var mapped = s.Bones.Where(b => b.Human != null).Select(b => b.Human).ToList();
            Assert.That(mapped, Is.Unique);
            foreach (var name in mapped)
                Assert.That(human.Contains(name), name + " is a Unity humanoid bone");
            for (int i = 0; i < HumanTrait.BoneCount; i++)
                if (HumanTrait.RequiredBone(i))
                    Assert.That(mapped, Does.Contain(HumanTrait.BoneName[i]), "required: " + HumanTrait.BoneName[i]);
            // Five three-joint fingers per hand, toes, helper bones for hair and the coat.
            Assert.That(mapped.Count(n => n.StartsWith("Left ") || n.StartsWith("Right ")), Is.EqualTo(30));
            Assert.That(s.Has("LeftToes") && s.Has("HairBack2") && s.Has("CoatBackR2"));
            // T-pose: arms horizontal, legs vertical.
            Assert.That(Mathf.Abs(s["LeftHand"].Head.y - s["LeftUpperArm"].Head.y), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(s["LeftFoot"].Head.x - s["LeftUpperLeg"].Head.x), Is.LessThan(0.03f));
            Assert.That(s["Head"].Tail.y, Is.EqualTo(1.8f).Within(0.01f), "about 7.5 heads tall");
        }

        [Test]
        public void KaiMesh_IsSkinned_WithNormalisedWeights_AtEveryLevel_WithinBudget()
        {
            int[] maxTriangles = { 28000, 15000, 6000 };
            int previous = int.MaxValue;
            for (int lod = 0; lod < Kai.Lods.Length; lod++)
            {
                var mesh = Kai.Lods[lod];
                var weights = mesh.boneWeights;
                Assert.That(weights.Length, Is.EqualTo(mesh.vertexCount), $"LOD{lod}: every vertex is skinned");
                Assert.That(mesh.bindposes.Length, Is.EqualTo(Kai.Skeleton.Bones.Count));
                foreach (var w in weights)
                {
                    Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1f).Within(1e-3f), $"LOD{lod}: weights sum to 1");
                    Assert.That(w.boneIndex0, Is.InRange(0, Kai.Skeleton.Bones.Count - 1));
                }
                int triangles = mesh.triangles.Length / 3;
                TestContext.WriteLine($"KAI LOD{lod}: {triangles} triangles, {mesh.vertexCount} vertices");
                Assert.That(triangles, Is.LessThanOrEqualTo(maxTriangles[lod]), $"LOD{lod} triangle budget");
                Assert.That(triangles, Is.LessThan(previous), "each level is lighter");
                previous = triangles;
                Assert.That(mesh.subMeshCount, Is.EqualTo(2), "body + painted face");
            }
        }

        [Test]
        public void KaiMesh_FacesOutward_AndJointsBlendSmoothly()
        {
            var mesh = Kai.Lods[1];
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.GetTriangles(0);
            int bad = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f)
                    bad++;
            }
            Assert.That(bad, Is.EqualTo(0), "every triangle's front face matches its normals");
            // Elbows and knees: vertices near the joint share weight between both bones (no rigid crease).
            var s = Kai.Skeleton;
            int elbow = s.IndexOf("LeftLowerArm"), upper = s.IndexOf("LeftUpperArm");
            var joint = s["LeftLowerArm"].Head;
            bool blended = false;
            var w = mesh.boneWeights;
            for (int i = 0; i < v.Length && !blended; i++)
                if ((v[i] - joint).sqrMagnitude < 0.07f * 0.07f)
                {
                    var bw = w[i];
                    bool hasUpper = bw.boneIndex0 == upper || bw.boneIndex1 == upper;
                    bool hasLower = bw.boneIndex0 == elbow || bw.boneIndex1 == elbow;
                    blended = hasUpper && hasLower && bw.weight1 > 0.1f;
                }
            Assert.That(blended, "the elbow blends upper and lower arm");
        }

        [Test]
        public void Face_HasFiveDecalParts_AndAPaintedAtlasWithExpressions()
        {
            var mesh = Kai.Lods[0];
            var parts = new List<Vector4>();
            mesh.GetUVs(1, parts);
            var faceTriangles = mesh.GetTriangles(1);
            var ids = new HashSet<int>(faceTriangles.Select(i => Mathf.RoundToInt(parts[i].x)));
            Assert.That(ids, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }), "eyes, brows, nose and mouth");

            var pixels = FacePainter.PaintPixels(KaiModel.Face);
            int Opaque(int tile)
            {
                int col = tile % 4, row = tile / 4, count = 0;
                for (int y = (3 - row) * FacePainter.Tile; y < (4 - row) * FacePainter.Tile; y++)
                for (int x = col * FacePainter.Tile; x < (col + 1) * FacePainter.Tile; x++)
                    if (pixels[y * FacePainter.Size + x].a > 128)
                        count++;
                return count;
            }
            for (int tile = 0; tile < 16; tile++)
                Assert.That(Opaque(tile), Is.GreaterThan(200), "tile " + tile + " is painted");
            Assert.That(Opaque(FaceTiles.EyeClosed), Is.LessThan(Opaque(FaceTiles.EyeNeutral) / 3), "a blink closes the eye");
            Assert.That(Opaque(FaceTiles.EyeWide), Is.GreaterThan(Opaque(FaceTiles.EyeFocused)), "wide eyes show more than narrowed ones");
        }

        [Test]
        public void Expressions_UseTheRightTiles()
        {
            CharacterFacialAnimator.Tiles(FaceExpression.Defeat, out int eye, out _, out _);
            Assert.That(eye, Is.EqualTo(FaceTiles.EyeClosed));
            CharacterFacialAnimator.Tiles(FaceExpression.Angry, out eye, out int brow, out int mouth);
            Assert.That(eye, Is.EqualTo(FaceTiles.EyeAngry));
            Assert.That(brow, Is.EqualTo(FaceTiles.BrowAngry));
            Assert.That(mouth, Is.EqualTo(FaceTiles.MouthGrit));
            foreach (FaceExpression e in System.Enum.GetValues(typeof(FaceExpression)))
            {
                CharacterFacialAnimator.Tiles(e, out eye, out brow, out mouth);
                Assert.That(eye, Is.InRange(0, 7));
                Assert.That(brow, Is.InRange(8, 11));
                Assert.That(mouth, Is.InRange(12, 15));
            }
        }

        [Test]
        public void GeneratedKai_HasAHumanoidAvatar_ThreeLods_AndEveryClipSlot()
        {
            var profile = Resources.Load<CharacterVisualProfile>(SkinnedCharacterLoader.ResourceFolder + "KAI");
            if (profile == null)
                Assert.Ignore("Run project setup first (War of Nyx > Setup): it generates the skinned characters.");
            Assert.That(profile.prefab, Is.Not.Null);
            var animator = profile.prefab.GetComponent<Animator>();
            Assert.That(animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid, "valid humanoid avatar");
            Assert.That(profile.prefab.GetComponent<LODGroup>().lodCount, Is.EqualTo(3));
            var renderers = profile.prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(3));
            foreach (var r in renderers)
                Assert.That(r.sharedMaterials.Length, Is.EqualTo(2));
            var controller = profile.animation.controller;
            var clips = controller.animationClips.Select(c => c.name.Substring("KAI_".Length)).ToList();
            foreach (var slot in CharacterAnimatorParams.Clips)
                Assert.That(clips, Does.Contain(slot), "clip slot " + slot);
            foreach (var clip in controller.animationClips)
                Assert.That(clip.humanMotion, clip.name + " is a humanoid (retargetable) clip");
        }
    }
}
