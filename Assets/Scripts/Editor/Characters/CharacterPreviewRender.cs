using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters
{
    /// <summary>
    /// Visual QA for the skinned characters (section 40.19): renders a character in a neutral studio (key, rim, fill
    /// light) from the front, three-quarter, side and back, a face close-up, and sampled frames of its clips (idle,
    /// walk, run, sprint, crouch, melee, hit, death, victory) to PNG files. Batch: -executeMethod
    /// WarOfNyx.EditorTools.Characters.CharacterPreviewRender.RenderKai -previewOut &lt;folder&gt; (needs graphics).
    /// </summary>
    public static class CharacterPreviewRender
    {
        [MenuItem("War of Nyx/Characters/Render KAI previews", priority = 31)]
        public static void RenderKai() => Render("KAI", OutputFolder());

        /// <summary>Batch: rebuild KAI's assets, then render the previews.</summary>
        public static void BuildAndRenderKai()
        {
            try
            {
                CharacterAssetBuilder.BuildKai();
                Render("KAI", OutputFolder());
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static string OutputFolder()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-previewOut")
                    return args[i + 1];
            return Path.GetFullPath("Temp/CharacterPreviews");
        }

        public static void Render(string name, string folder)
        {
            Directory.CreateDirectory(folder);
            var profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>($"{CharacterAssetBuilder.ResourcesRoot}/{name}.asset");
            if (profile == null)
            {
                CharacterAssetBuilder.BuildKai();
                profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>($"{CharacterAssetBuilder.ResourcesRoot}/{name}.asset");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.36f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.22f);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.7f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.8f;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.8f, 0.85f, 1f);
            fill.transform.rotation = Quaternion.Euler(10f, 35f, 0f);
            rim.color = new Color(0.6f, 0.45f, 1f);
            rim.transform.rotation = Quaternion.Euler(18f, 160f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.42f, 0.42f, 0.48f) };
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(profile.prefab);
            var animator = model.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // offscreen renders: always write the pose
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                clips[clip.name.Substring(name.Length + 1)] = clip;

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.34f, 0.42f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(900, 1200, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            // A katana in the animated right hand (same grip as the game), for the melee frames.
            var grip = new GameObject("Grip").transform;
            grip.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
            grip.localPosition = CharacterAnimationController.MeleeGripPosition;
            grip.localRotation = CharacterAnimationController.MeleeGripRotation;
            var blade = Gameplay.Effects.WeaponModelFactory.Build(Core.Combat.WeaponCatalog.Get(Core.Combat.WeaponCatalog.Katana), grip, out _);
            blade.gameObject.SetActive(false);
            var lodGroup = model.GetComponent<LODGroup>();
            lodGroup.ForceLOD(0);
            // Many poses are rendered inside one editor frame: re-skin on every render, not once per frame.
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.forceMatrixRecalculationPerRender = true;

            void Shot(string label, Vector3 from, Vector3 at)
            {
                cam.transform.position = from;
                cam.transform.LookAt(at);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, $"{name}_{label}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            // Humanoid clips are evaluated through a playable graph (reliable outside play mode).
            var graph = UnityEngine.Playables.PlayableGraph.Create("Preview");
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Preview", animator);
            void Pose(string clip, float t)
            {
                if (!clips.TryGetValue(clip, out var c))
                    return;
                blade.gameObject.SetActive(clip.StartsWith("Melee"));
                var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, c);
                playable.SetApplyFootIK(false); // generated clips carry no IK-goal curves
                output.SetSourcePlayable(playable);
                playable.SetTime(t);
                graph.Evaluate();
                playable.Destroy();
            }

            // Curve ranges (QA log): proves the clips carry limb motion.
            foreach (var clipName in new[] { "WalkF", "RunF", "Melee_HorizontalSlash", "Death" })
            {
                if (!clips.TryGetValue(clipName, out var c))
                    continue;
                var line = new System.Text.StringBuilder(clipName + ":");
                foreach (var muscle in new[] { "Left Upper Leg Front-Back", "Left Lower Leg Stretch", "Right Arm Down-Up", "Right Forearm Stretch", "Spine Front-Back" })
                {
                    var curve = AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle));
                    if (curve == null)
                    {
                        line.Append($" [{muscle}: missing]");
                        continue;
                    }
                    float min = float.MaxValue, max = float.MinValue;
                    foreach (var k in curve.keys)
                    {
                        min = Mathf.Min(min, k.value);
                        max = Mathf.Max(max, k.value);
                    }
                    line.Append($" [{muscle}: {min:0.00}..{max:0.00}]");
                }
                Debug.Log("[WarOfNyx][Characters] " + line);
            }

            // Retarget check (QA log): where the humanoid clip puts the hips and feet.
            foreach (var probe in new[] { ("Idle", 1f), ("WalkF", 0f), ("RunF", 0.2f), ("Melee_HorizontalSlash", 0.36f) })
            {
                Pose(probe.Item1, probe.Item2);
                Vector3 P(HumanBodyBones b) => model.transform.InverseTransformPoint(animator.GetBoneTransform(b).position);
                Debug.Log($"[WarOfNyx][Characters] {probe.Item1}@{probe.Item2}: hips {P(HumanBodyBones.Hips)} ankle {P(HumanBodyBones.LeftFoot)} " +
                          $"lHand {P(HumanBodyBones.LeftHand)} rElbow {P(HumanBodyBones.RightLowerArm)} rHand {P(HumanBodyBones.RightHand)}");
            }
            // Ground contact (QA log): lowest ankle per clip against the bind-pose ankle height (negative = sinks).
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            int footIndex = System.Array.IndexOf(skin.bones, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            float ankleRest = skin.sharedMesh.bindposes[footIndex].inverse.GetColumn(3).y;
            foreach (var clipName in new[] { "Idle", "WalkF", "RunF", "Sprint", "CrouchIdle", "CrouchWalk", "Slide" })
            {
                if (!clips.TryGetValue(clipName, out var c))
                    continue;
                float lowest = float.MaxValue;
                for (int k = 0; k < 16; k++)
                {
                    Pose(clipName, c.length * k / 16f);
                    foreach (var foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                        lowest = Mathf.Min(lowest, model.transform.InverseTransformPoint(animator.GetBoneTransform(foot).position).y);
                }
                Debug.Log($"[WarOfNyx][Characters] {name} ground contact {clipName}: lowest ankle {lowest:0.000} m vs rest {ankleRest:0.000} m ({(lowest - ankleRest) * 100f:+0.0;-0.0} cm)");
            }
            Pose("Idle", 1f);
            var body = new Vector3(0f, 1f, 0f);
            Shot("01_front", new Vector3(0f, 1.05f, 5.4f), body);
            Shot("02_three_quarter", new Vector3(3.6f, 1.1f, 4.1f), body);
            Shot("03_side", new Vector3(5.4f, 1.05f, 0f), body);
            Shot("04_back", new Vector3(-2.6f, 1.15f, -4.8f), body);
            cam.fieldOfView = 18f;
            var head = new Vector3(0f, profile.headHeight + 0.01f, 0.02f);
            Shot("05_face", head + new Vector3(0.12f, 0.03f, 1.25f), head);
            Shot("06_face_side", head + new Vector3(1.15f, 0.05f, 0.55f), head);
            cam.fieldOfView = 30f;
            int n = 7;
            foreach (var (clip, t) in new[]
                     {
                         ("IdleVariant", 1.2f), ("WalkF", 0.25f), ("WalkF", 0.8f), ("RunF", 0.2f), ("Sprint", 0.15f), ("WalkL", 0.3f), ("CrouchWalk", 0.3f),
                         ("Air", 0.2f), ("Slide", 0.2f), ("Melee_HorizontalSlash", 0.3f), ("Melee_HorizontalSlash", 0.4f), ("Melee_OverheadStrike", 0.42f),
                         ("Melee_Thrust", 0.4f), ("Hit", 0.1f), ("Death", 0.6f), ("Death", 1.6f), ("Victory", 2.5f),
                     })
            {
                Pose(clip, t);
                Shot($"{n++:00}_{clip}_{t:0.00}", new Vector3(3.9f, 1.15f, 3.9f), body);
            }
            graph.Destroy();
            Object.DestroyImmediate(rt);
            Debug.Log($"[WarOfNyx][Characters] Preview renders written to {folder}");
        }
    }
}
