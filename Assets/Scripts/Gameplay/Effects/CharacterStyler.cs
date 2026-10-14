using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Builds a PLACEHOLDER anime-styled character (spec 17) under the visual root as a jointed body
    /// (<see cref="CharacterBody"/>): pelvis, spine, head, two-segment arms and legs with hands and boots,
    /// plus per-character hair, visor / scarf / armour, collar, gloves, knee pads, a back holster and glowing
    /// accents, all cel-shaded with outlines. <see cref="CharacterAnimator"/> poses it (walk / run / sprint,
    /// crouch, jump, slide, weapon holding, melee combos). Hit shapes are unchanged (they live on the
    /// character root, not on these parts). Final rigged models replace this later.
    /// </summary>
    public static class CharacterStyler
    {
        public const string StyledRootName = "Styled (PLACEHOLDER)";

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

            var root = new GameObject(StyledRootName).transform;
            root.SetParent(visualRoot, false);
            var body = root.gameObject.AddComponent<CharacterBody>();
            var dark = look.Primary * 0.7f;
            dark.a = 1f;
            var glove = look.Secondary * 0.75f;
            glove.a = 1f;
            var glow = look.Accent * 1.6f;

            // ---- Pelvis and legs
            body.Pelvis = Joint("Pelvis", root, new Vector3(0f, body.PelvisRestHeight, 0f));
            StyleKit.Part(PrimitiveType.Cube, "Hips", body.Pelvis, new Vector3(0f, -0.05f, 0f), new Vector3(0.4f, 0.17f, 0.26f), dark);
            StyleKit.Part(PrimitiveType.Cube, "Belt", body.Pelvis, new Vector3(0f, -0.11f, 0f), new Vector3(0.44f, 0.07f, 0.3f), look.Secondary);
            StyleKit.Part(PrimitiveType.Cube, "Buckle", body.Pelvis, new Vector3(0f, -0.11f, 0.155f), new Vector3(0.08f, 0.06f, 0.02f), look.Accent, default, glow * 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                var thigh = Joint(side < 0 ? "ThighL" : "ThighR", body.Pelvis,
                    new Vector3(CharacterBody.HipSpacing * side, CharacterBody.HipHeight - body.PelvisRestHeight, 0f));
                StyleKit.Part(PrimitiveType.Capsule, "Thigh", thigh, new Vector3(0f, -0.21f, 0f), new Vector3(0.19f, 0.23f, 0.19f), dark);
                var shin = Joint(side < 0 ? "ShinL" : "ShinR", thigh, new Vector3(0f, -CharacterBody.ThighLength, 0f));
                StyleKit.Part(PrimitiveType.Capsule, "Shin", shin, new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.22f, 0.16f), dark);
                StyleKit.Part(PrimitiveType.Cube, "KneePad", shin, new Vector3(0f, -0.02f, 0.08f), new Vector3(0.13f, 0.12f, 0.06f), look.Secondary);
                var foot = Joint(side < 0 ? "FootL" : "FootR", shin, new Vector3(0f, -CharacterBody.ShinLength, 0f));
                StyleKit.Part(PrimitiveType.Cube, "Boot", foot, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.17f, 0.1f, 0.28f), look.Secondary);
                StyleKit.Part(PrimitiveType.Cube, "Sole", foot, new Vector3(0f, -0.065f, 0.05f), new Vector3(0.18f, 0.03f, 0.3f), new Color(0.08f, 0.07f, 0.1f));
                if (side < 0) { body.ThighL = thigh; body.ShinL = shin; body.FootL = foot; }
                else { body.ThighR = thigh; body.ShinR = shin; body.FootR = foot; }
            }

            // ---- Spine: torso, armour, accents, collar, back holster
            body.Spine = Joint("Spine", body.Pelvis, new Vector3(0f, 0.05f, 0f));
            StyleKit.Part(PrimitiveType.Capsule, "Torso", body.Spine, new Vector3(0f, 0.18f, 0f), new Vector3(0.46f, 0.3f, 0.3f), look.Primary);
            for (int side = -1; side <= 1; side += 2)
                StyleKit.Part(PrimitiveType.Cube, "Glow", body.Spine, new Vector3(0.17f * side, 0.12f, 0.15f), new Vector3(0.03f, 0.28f, 0.02f), look.Accent, default, glow);
            StyleKit.Part(PrimitiveType.Cylinder, "Collar", body.Spine, new Vector3(0f, 0.42f, 0f), new Vector3(0.24f, 0.04f, 0.22f), look.Secondary);
            StyleKit.Part(PrimitiveType.Cylinder, "Neck", body.Spine, new Vector3(0f, 0.46f, 0f), new Vector3(0.1f, 0.05f, 0.1f), look.Skin);
            StyleKit.Part(PrimitiveType.Cube, "Holster", body.Spine, new Vector3(0f, 0.2f, -0.17f), new Vector3(0.3f, 0.32f, 0.1f), dark);
            StyleKit.Part(PrimitiveType.Cube, "HolsterStrap", body.Spine, new Vector3(0f, 0.2f, 0f), new Vector3(0.48f, 0.04f, 0.32f), look.Secondary,
                new Vector3(0f, 0f, 35f));
            if (look.HeavyArmor)
            {
                StyleKit.Part(PrimitiveType.Cube, "ChestPlate", body.Spine, new Vector3(0f, 0.2f, 0.12f), new Vector3(0.44f, 0.34f, 0.12f), look.Secondary);
                StyleKit.Part(PrimitiveType.Cube, "PlateTrim", body.Spine, new Vector3(0f, 0.2f, 0.185f), new Vector3(0.04f, 0.3f, 0.01f), look.Accent, default, glow * 0.6f);
                for (int side = -1; side <= 1; side += 2)
                    StyleKit.Part(PrimitiveType.Sphere, "Pauldron", body.Spine, new Vector3(0.3f * side, 0.36f, 0f), new Vector3(0.28f, 0.18f, 0.28f), look.Secondary);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                    StyleKit.Part(PrimitiveType.Cube, "ShoulderTrim", body.Spine, new Vector3(0.26f * side, 0.36f, 0f), new Vector3(0.16f, 0.06f, 0.24f), look.Secondary);
            }
            if (look.Scarf)
            {
                StyleKit.Part(PrimitiveType.Cube, "Scarf", body.Spine, new Vector3(0f, 0.4f, 0f), new Vector3(0.34f, 0.1f, 0.32f), look.Secondary);
                StyleKit.Part(PrimitiveType.Cube, "ScarfTail", body.Spine, new Vector3(0.08f, 0.18f, -0.24f), new Vector3(0.1f, 0.5f, 0.03f),
                    look.Secondary, new Vector3(-20f, 0f, 6f), look.Accent * 0.3f);
            }

            // ---- Head: slightly large (anime proportions), big eyes, hair
            body.Head = Joint("HeadJoint", body.Spine, new Vector3(0f, 0.48f, 0f));
            var head = new GameObject("Head").transform;
            head.SetParent(body.Head, false);
            head.localPosition = new Vector3(0f, 0.14f, 0f);
            StyleKit.Part(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.4f, 0.44f, 0.4f), look.Skin);
            for (int side = -1; side <= 1; side += 2)
            {
                StyleKit.Part(PrimitiveType.Sphere, "Eye", head, new Vector3(0.075f * side, 0.01f, 0.18f), new Vector3(0.07f, 0.1f, 0.03f),
                    new Color(0.08f, 0.06f, 0.12f), default, look.Accent * 0.5f);
                StyleKit.Part(PrimitiveType.Sphere, "Ear", head, new Vector3(0.2f * side, 0f, 0f), new Vector3(0.05f, 0.09f, 0.06f), look.Skin);
            }
            BuildHair(head, look);
            if (look.Visor)
                StyleKit.Part(PrimitiveType.Cube, "Visor", head, new Vector3(0f, 0.02f, 0.19f), new Vector3(0.38f, 0.08f, 0.04f), look.Accent, default, glow);

            // ---- Arms: shoulder → elbow → hand (gloved)
            for (int side = -1; side <= 1; side += 2)
            {
                var upper = Joint(side < 0 ? "UpperArmL" : "UpperArmR", body.Spine,
                    new Vector3(CharacterBody.ShoulderSpacing * side, CharacterBody.ShoulderHeight - body.PelvisRestHeight - 0.05f, 0f));
                StyleKit.Part(PrimitiveType.Capsule, "UpperArm", upper, new Vector3(0f, -0.13f, 0f), new Vector3(0.13f, 0.15f, 0.13f), look.Primary);
                var fore = Joint(side < 0 ? "ForearmL" : "ForearmR", upper, new Vector3(0f, -CharacterBody.UpperArmLength, 0f));
                StyleKit.Part(PrimitiveType.Capsule, "Forearm", fore, new Vector3(0f, -0.12f, 0f), new Vector3(0.11f, 0.14f, 0.11f), look.Primary);
                StyleKit.Part(PrimitiveType.Cylinder, "Cuff", fore, new Vector3(0f, -0.22f, 0f), new Vector3(0.12f, 0.03f, 0.12f), look.Secondary);
                var hand = Joint(side < 0 ? "HandL" : "HandR", fore, new Vector3(0f, -CharacterBody.ForearmLength, 0f));
                StyleKit.Part(PrimitiveType.Sphere, "Glove", hand, new Vector3(0f, -0.03f, 0f), new Vector3(0.1f, 0.11f, 0.09f), glove);
                if (side < 0) { body.UpperArmL = upper; body.ForearmL = fore; body.HandL = hand; }
                else { body.UpperArmR = upper; body.ForearmR = fore; body.HandR = hand; }
            }

            // The animator lives on the character root and finds this body, the movement and the weapon rig.
            var character = visualRoot.parent != null ? visualRoot.parent : visualRoot;
            if (character.GetComponent<CharacterAnimator>() == null)
                character.gameObject.AddComponent<CharacterAnimator>();
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }

        private static void BuildHair(Transform head, CharacterLook look)
        {
            var hair = look.Hair;
            StyleKit.Part(PrimitiveType.Sphere, "HairCap", head, new Vector3(0f, 0.07f, -0.02f), new Vector3(0.44f, 0.36f, 0.44f), hair);
            switch (look.HairStyle)
            {
                case HairStyle.Spiky:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 360f / 7f;
                        var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        StyleKit.Cone("Spike", head, new Vector3(dir.x * 0.12f, 0.18f, dir.z * 0.12f - 0.02f), new Vector3(0.12f, 0.22f, 0.12f),
                            hair, new Vector3(-dir.z * -40f + 0f, 0f, -dir.x * 40f));
                    }
                    break;
                case HairStyle.SweptBack:
                    for (int i = 0; i < 4; i++)
                        StyleKit.Cone("Swept", head, new Vector3(-0.12f + i * 0.08f, 0.12f, -0.1f), new Vector3(0.12f, 0.32f, 0.1f), hair,
                            new Vector3(-115f, 0f, 0f));
                    break;
                case HairStyle.Short:
                    StyleKit.Cone("Fringe", head, new Vector3(0.05f, 0.14f, 0.15f), new Vector3(0.18f, 0.16f, 0.08f), hair, new Vector3(150f, 0f, 20f));
                    break;
                case HairStyle.Crest:
                    for (int i = 0; i < 4; i++)
                        StyleKit.Cone("Crest", head, new Vector3(0f, 0.2f, 0.1f - i * 0.08f), new Vector3(0.07f, 0.22f - i * 0.03f, 0.12f), hair,
                            new Vector3(-20f - i * 12f, 0f, 0f));
                    break;
            }
        }
    }
}
