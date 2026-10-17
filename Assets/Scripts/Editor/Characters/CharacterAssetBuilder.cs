using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters
{
    /// <summary>
    /// Generates the skinned character assets (section 40.14, steps 3-19 for the procedural pipeline) into
    /// Assets/Generated: per detail level a skinned mesh (bone weights, bind poses), the painted face atlas (PNG,
    /// mobile-compressed), the face material, a validated Unity Humanoid Avatar, the authored humanoid clips, the
    /// Animator Controller and upper-body mask, the runtime prefab (Animator + LODGroup + three SkinnedMeshRenderers on
    /// one skeleton) and the visual / animation profiles the game loads from Resources. Run by project setup; rerun
    /// any time from War of Nyx > Characters. An artist-made FBX / VRM can replace the prefab (see CHARACTER_PIPELINE.md).
    /// </summary>
    public static class CharacterAssetBuilder
    {
        public const string GeneratedRoot = "Assets/Generated/Characters";
        public const string ResourcesRoot = "Assets/Generated/Resources/WarOfNyx/Characters";
        public const string ToonVcMaterialPath = "Assets/Resources/WarOfNyx/NyxToonVC.mat";

        /// <summary>Raise when the generated characters change (setup rebuilds them).</summary>
        public const int Version = 1;

        [MenuItem("War of Nyx/Characters/Build skinned characters (KAI)", priority = 30)]
        public static void BuildAll() => BuildKai();

        public static string BuildKai()
        {
            const string name = "KAI";
            string dir = $"{GeneratedRoot}/{name}";
            EnsureFolder(dir);
            EnsureFolder(ResourcesRoot);
            var built = KaiModel.Build();

            // Meshes.
            for (int lod = 0; lod < built.Lods.Length; lod++)
                Save(built.Lods[lod], $"{dir}/{name}_LOD{lod}.asset");

            // Face atlas: painted, written as PNG, imported with mobile compression.
            string facePath = $"{dir}/{name}_FaceAtlas.png";
            var face = FacePainter.Paint(built.Face);
            File.WriteAllBytes(facePath, face.EncodeToPNG());
            Object.DestroyImmediate(face);
            AssetDatabase.ImportAsset(facePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(facePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_4x4,
            });
            importer.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(facePath);

            // Materials: the shared vertex-colour toon material for the body, a face material per character.
            var body = AssetDatabase.LoadAssetAtPath<Material>(ToonVcMaterialPath);
            if (body == null)
                throw new System.InvalidOperationException("NyxToonVC material missing: run the visual style setup step first.");
            var faceShader = Shader.Find("WarOfNyx/NyxFace");
            if (faceShader == null)
                throw new System.InvalidOperationException("WarOfNyx/NyxFace shader missing.");
            var faceMaterial = new Material(faceShader) { name = name + "_Face" };
            faceMaterial.SetTexture("_FaceAtlas", atlas);
            faceMaterial.SetColor("_GlowColor", new Color(0.62f, 0.25f, 1f));
            Save(faceMaterial, $"{dir}/{name}_Face.mat");

            // The rig in the bind T-pose.
            var go = new GameObject(name);
            try
            {
                var armature = new GameObject("Armature").transform;
                armature.SetParent(go.transform, false);
                var bones = built.Skeleton.Instantiate(armature);
                var hips = bones[built.Skeleton.IndexOf("Hips")];
                var renderers = new Renderer[built.Lods.Length];
                for (int lod = 0; lod < built.Lods.Length; lod++)
                {
                    var r = new GameObject($"{name}_LOD{lod}").AddComponent<SkinnedMeshRenderer>();
                    r.transform.SetParent(go.transform, false);
                    r.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{dir}/{name}_LOD{lod}.asset");
                    r.bones = bones;
                    r.rootBone = hips;
                    r.sharedMaterials = new[] { body, faceMaterial };
                    r.localBounds = new Bounds(new Vector3(0f, -0.05f, 0f), new Vector3(2f, 2.3f, 1.6f));
                    r.quality = lod == 0 ? SkinQuality.Bone4 : SkinQuality.Bone2;
                    r.updateWhenOffscreen = false;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    renderers[lod] = r;
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.16f, new[] { renderers[0] }), new LOD(0.05f, new[] { renderers[1] }), new LOD(0.0015f, new[] { renderers[2] }),
                });
                group.RecalculateBounds();

                var avatar = BuildAvatar(go, built.Skeleton, name);
                Save(avatar, $"{dir}/{name}_Avatar.asset");
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{dir}/{name}_Avatar.asset");
                var animator = go.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // Authored clips (humanoid), then the controller.
                var clips = CharacterMotionAuthoring.BakeAll(go, avatar, MotionStyle.Kai);
                foreach (var pair in clips)
                {
                    Save(pair.Value, $"{dir}/Anim/{name}_{pair.Key}.anim");
                }
                var saved = new System.Collections.Generic.Dictionary<string, AnimationClip>();
                foreach (var key in clips.Keys)
                    saved[key] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/Anim/{name}_{key}.anim");
                var controller = CharacterControllerBuilder.Build(saved, $"{dir}/{name}.controller", $"{dir}/{name}_UpperBody.mask");
                animator.runtimeAnimatorController = controller;

                string prefabPath = $"{ResourcesRoot}/{name}_Model.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

                var animationProfile = ScriptableObject.CreateInstance<CharacterAnimationProfile>();
                animationProfile.controller = controller;
                Save(animationProfile, $"{dir}/{name}_Animation.asset");
                animationProfile = AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>($"{dir}/{name}_Animation.asset");
                var profile = ScriptableObject.CreateInstance<CharacterVisualProfile>();
                profile.characterName = name;
                profile.prefab = prefab;
                profile.animation = animationProfile;
                profile.energyColor = new Color(0.62f, 0.25f, 1f);
                profile.headHeight = built.HeadCentre.y;
                profile.faceSubmesh = 1;
                profile.artSource = "Procedural (CharacterAssetBuilder v" + Version + ")";
                Save(profile, $"{ResourcesRoot}/{name}.asset");
                AssetDatabase.SaveAssets();

                string report = $"{name}: LOD triangles {built.Lods[0].triangles.Length / 3} / {built.Lods[1].triangles.Length / 3} / {built.Lods[2].triangles.Length / 3}, " +
                                $"{built.Skeleton.Bones.Count} bones, {clips.Count} clips, avatar human={avatar.isHuman} valid={avatar.isValid}";
                Debug.Log("[WarOfNyx][Characters] " + report);
                return report;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Authors a motion style on KAI's rig (the authoring skeleton every clip was designed on) and saves the humanoid
        /// clips plus their controller under <paramref name="dir"/>, named {name}_{clip}. Humanoid clips are stored in
        /// muscle space, so they retarget to any character with a humanoid Avatar (imported models included).
        /// </summary>
        public static AnimatorController BakeStyle(MotionStyle style, string dir, string name, out int clipCount)
        {
            var skeleton = NyxSkeleton.Build(RigProportions.Lean);
            var go = new GameObject("AuthoringRig");
            try
            {
                var armature = new GameObject("Armature").transform;
                armature.SetParent(go.transform, false);
                skeleton.Instantiate(armature);
                var avatar = BuildAvatar(go, skeleton, "AuthoringRig");
                var clips = CharacterMotionAuthoring.BakeAll(go, avatar, style);
                var saved = new System.Collections.Generic.Dictionary<string, AnimationClip>();
                foreach (var pair in clips)
                {
                    string path = $"{dir}/Anim/{name}_{pair.Key}.anim";
                    Save(pair.Value, path);
                    saved[pair.Key] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                }
                Object.DestroyImmediate(avatar);
                clipCount = saved.Count;
                return CharacterControllerBuilder.Build(saved, $"{dir}/{name}.controller", $"{dir}/{name}_UpperBody.mask");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>A Unity Humanoid Avatar for the shared skeleton (bind T-pose): every humanoid bone mapped, default limits.</summary>
        public static Avatar BuildAvatar(GameObject root, NyxSkeleton skeleton, string name)
        {
            var human = new System.Collections.Generic.List<HumanBone>();
            var known = new System.Collections.Generic.HashSet<string>(HumanTrait.BoneName);
            foreach (var bone in skeleton.Bones)
            {
                if (bone.Human == null)
                    continue;
                if (!known.Contains(bone.Human))
                    throw new System.InvalidOperationException($"'{bone.Human}' is not a Unity humanoid bone name.");
                var hb = new HumanBone { boneName = bone.Name, humanName = bone.Human };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }
            var skeletonBones = new System.Collections.Generic.List<SkeletonBone>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                skeletonBones.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            var description = new HumanDescription
            {
                human = human.ToArray(), skeleton = skeletonBones.ToArray(), upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f, armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = name + "_Avatar";
            if (!avatar.isValid || !avatar.isHuman)
                throw new System.InvalidOperationException($"{name}: the humanoid avatar is not valid.");
            return avatar;
        }

        internal static void Save(Object asset, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
