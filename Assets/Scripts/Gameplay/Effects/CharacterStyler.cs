using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Builds a detailed PLACEHOLDER character (spec 17) under the visual root: a jointed body
    /// (<see cref="CharacterBody"/>) whose parts are the procedural meshes of <see cref="CharacterModels"/>
    /// (anatomy, face, hair, outfit and gear per character), cloth chains (<see cref="ClothDynamics"/>), blinking
    /// eyes (<see cref="FaceAnimator"/>), and the procedural animator and movement sounds on the character root.
    /// One renderer per body part and detail level (vertex-coloured, WarOfNyx/NyxToonVC); a LODGroup swaps the close-up
    /// model (LOD 0) for lighter ones with distance, so 24 detailed characters stay affordable. Hit shapes are unchanged (they live on the character root, the same for every character).
    /// </summary>
    public static class CharacterStyler
    {
        public const string StyledRootName = "Styled (PLACEHOLDER)";
        public const string MaterialResource = "WarOfNyx/NyxToonVC";

        private static Material _material;

        /// <summary>The vertex-colour material shared by characters, detailed guns and vehicles.</summary>
        public static Material SharedMaterial => Material;

        private static Material Material
        {
            get
            {
                if (_material != null)
                    return _material;
                _material = Resources.Load<Material>(MaterialResource);
                if (_material == null)
                {
                    var shader = Shader.Find("WarOfNyx/NyxToonVC");
                    _material = shader != null ? new Material(shader) { name = "NyxToonVC (runtime)" } : StyleKit.Toon;
                }
                return _material;
            }
        }

        public static void Apply(Transform visualRoot, CharacterLook look)
        {
            if (visualRoot == null || look == null || StyleKit.Headless)
                return;

            var old = visualRoot.Find(StyledRootName);
            if (old != null)
                StyleKit.Remove(old.gameObject);
            // Hide the Phase 2 capsule parts (kept in the prefab for tests and tools).
            foreach (var name in new[] { "Body", "Visor", "Pack" })
            {
                var part = visualRoot.Find(name);
                if (part != null && part.TryGetComponent<Renderer>(out var r))
                    r.enabled = false;
            }

            // Skinned humanoid characters (section 40) replace the jointed placeholder when their model exists.
            var skinned = Characters.SkinnedCharacterLoader.TryCreate(visualRoot, look, StyledRootName);
            if (skinned != null)
            {
                var owner = visualRoot.parent != null ? visualRoot.parent : visualRoot;
                // A previous jointed look (menu preview switching characters) leaves its procedural animator behind.
                if (owner.TryGetComponent<CharacterAnimator>(out var jointed))
                    StyleKit.Remove(jointed);
                if (owner.TryGetComponent<ClothDynamics>(out var cloth))
                    StyleKit.Remove(cloth);
                if (owner.GetComponent<CharacterAudio>() == null)
                    owner.gameObject.AddComponent<CharacterAudio>();
                return;
            }

            var model = CharacterModels.Get(look);
            var levels = new CharacterModel[CharacterModels.LodCount];
            var lodRenderers = new List<Renderer>[CharacterModels.LodCount];
            for (int lod = 0; lod < levels.Length; lod++)
            {
                levels[lod] = CharacterModels.Get(look, lod);
                lodRenderers[lod] = new List<Renderer>();
            }
            var shape = model.Shape;
            var root = new GameObject(StyledRootName).transform;
            root.SetParent(visualRoot, false);
            var body = root.gameObject.AddComponent<CharacterBody>();
            var joints = new Dictionary<string, Transform>();

            Transform Joint(string name, Transform parent, Vector3 localPosition)
            {
                var joint = new GameObject(name).transform;
                joint.SetParent(parent, false);
                joint.localPosition = localPosition;
                joints[name] = joint;
                for (int lod = 0; lod < levels.Length; lod++)
                    if (levels[lod].Meshes.TryGetValue(name, out var mesh))
                        lodRenderers[lod].Add(Attach(joint, mesh, lod));
                return joint;
            }

            body.Pelvis = Joint("Pelvis", root, new Vector3(0f, body.PelvisRestHeight, 0f));
            body.Spine = Joint("Spine", body.Pelvis, new Vector3(0f, 0.05f, 0f));
            body.Head = Joint(CharacterBodyNames.Head, body.Spine, new Vector3(0f, 0.48f, 0f));
            var eyes = Joint(CharacterBodyNames.Eyes, body.Head, new Vector3(0f, CharacterModels.EyePivotY, 0f));
            float shoulderX = shape.Shoulder + shape.Arm * 0.3f;
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "L" : "R";
                var thigh = Joint("Thigh" + sfx, body.Pelvis, new Vector3(CharacterBody.HipSpacing * side, CharacterBody.HipHeight - body.PelvisRestHeight, 0f));
                var shin = Joint("Shin" + sfx, thigh, new Vector3(0f, -CharacterBody.ThighLength, 0f));
                var foot = Joint("Foot" + sfx, shin, new Vector3(0f, -CharacterBody.ShinLength, 0f));
                var upper = Joint("UpperArm" + sfx, body.Spine, new Vector3(shoulderX * side, CharacterBody.ShoulderHeight - body.PelvisRestHeight - 0.05f, 0f));
                var fore = Joint("Forearm" + sfx, upper, new Vector3(0f, -CharacterBody.UpperArmLength, 0f));
                var hand = Joint("Hand" + sfx, fore, new Vector3(0f, -CharacterBody.ForearmLength, 0f));
                if (side < 0) { body.ThighL = thigh; body.ShinL = shin; body.FootL = foot; body.UpperArmL = upper; body.ForearmL = fore; body.HandL = hand; }
                else { body.ThighR = thigh; body.ShinR = shin; body.FootR = foot; body.UpperArmR = upper; body.ForearmR = fore; body.HandR = hand; }
            }

            // Cloth chains (scarf tails, coat panels): a segment joint per mesh, animated by ClothDynamics.
            if (model.Dangles.Count > 0)
            {
                var cloth = root.gameObject.AddComponent<ClothDynamics>();
                foreach (var spec in model.Dangles)
                {
                    if (!joints.TryGetValue(spec.Parent, out var parent))
                        continue;
                    var chain = new Transform[spec.Segments];
                    var at = parent;
                    var position = spec.LocalPosition;
                    for (int i = 0; i < spec.Segments; i++)
                    {
                        chain[i] = Joint($"{spec.Name}_{i}", at, position);
                        at = chain[i];
                        position = new Vector3(0f, -spec.SegmentLength, 0f);
                    }
                    joints.TryGetValue(spec.Driver ?? string.Empty, out var driver);
                    cloth.AddChain(chain, spec.SegmentLength, spec.Stiffness, driver, spec.DriverWeight);
                }
            }

            root.gameObject.AddComponent<FaceAnimator>().Bind(eyes);

            // Detail levels by screen height (a 1.8 m character, 60° view): LOD 0 within ~10 m, LOD 1 to ~35 m, then LOD 2.
            var group = root.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(LodScreenHeights[0], lodRenderers[0].ToArray()),
                new LOD(LodScreenHeights[1], lodRenderers[1].ToArray()),
                new LOD(LodScreenHeights[2], lodRenderers[2].ToArray()),
            });
            group.RecalculateBounds();

            // The animator and movement sounds live on the character root and find this body, the movement and the weapon rig.
            var character = visualRoot.parent != null ? visualRoot.parent : visualRoot;
            if (character.GetComponent<CharacterAnimator>() == null)
                character.gameObject.AddComponent<CharacterAnimator>();
            if (character.GetComponent<CharacterAudio>() == null)
                character.gameObject.AddComponent<CharacterAudio>();
        }

        /// <summary>Screen-height thresholds of the three detail levels (the last one is the cull height).</summary>
        public static readonly float[] LodScreenHeights = { 0.15f, 0.045f, 0.0015f };

        private static Renderer Attach(Transform joint, Mesh mesh, int lod)
        {
            var go = new GameObject(lod == 0 ? "Mesh" : "Mesh_LOD" + lod);
            go.transform.SetParent(joint, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return renderer;
        }
    }
}
