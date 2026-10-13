using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Builds a PLACEHOLDER anime-styled character (spec 17) under the visual root: legs, torso, arms,
    /// head with big eyes, a per-character hair style, visor / scarf / armour, and glowing accents,
    /// all cel-shaded with outlines. Replaces the Phase 2 capsule look; hit shapes are unchanged
    /// (they live on the character root, not on these parts). Final rigged models replace this later.
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
            var dark = look.Primary * 0.7f;
            dark.a = 1f;
            var glow = look.Accent * 1.6f;

            // Legs and boots
            for (int side = -1; side <= 1; side += 2)
            {
                StyleKit.Part(PrimitiveType.Capsule, "Leg", root, new Vector3(0.13f * side, 0.42f, 0f), new Vector3(0.22f, 0.4f, 0.22f), dark);
                StyleKit.Part(PrimitiveType.Cube, "Boot", root, new Vector3(0.13f * side, 0.06f, 0.04f), new Vector3(0.2f, 0.12f, 0.3f), look.Secondary);
            }

            // Torso, belt, accent strips
            StyleKit.Part(PrimitiveType.Capsule, "Torso", root, new Vector3(0f, 1.08f, 0f), new Vector3(0.5f, 0.34f, 0.32f), look.Primary);
            StyleKit.Part(PrimitiveType.Cube, "Belt", root, new Vector3(0f, 0.8f, 0f), new Vector3(0.46f, 0.07f, 0.32f), look.Secondary);
            for (int side = -1; side <= 1; side += 2)
                StyleKit.Part(PrimitiveType.Cube, "Glow", root, new Vector3(0.17f * side, 1.1f, 0.15f), new Vector3(0.03f, 0.3f, 0.02f), look.Accent, default, glow);

            // Arms (the weapon hand is built by the weapon rig)
            for (int side = -1; side <= 1; side += 2)
            {
                StyleKit.Part(PrimitiveType.Capsule, "Arm", root, new Vector3(0.33f * side, 1.06f, 0.02f), new Vector3(0.15f, 0.3f, 0.15f),
                    look.Primary, new Vector3(0f, 0f, 8f * side));
                StyleKit.Part(PrimitiveType.Sphere, "Hand", root, new Vector3(0.36f * side, 0.78f, 0.05f), Vector3.one * 0.11f, look.Skin);
            }

            // Armour
            if (look.HeavyArmor)
            {
                StyleKit.Part(PrimitiveType.Cube, "ChestPlate", root, new Vector3(0f, 1.12f, 0.12f), new Vector3(0.46f, 0.36f, 0.14f), look.Secondary);
                for (int side = -1; side <= 1; side += 2)
                    StyleKit.Part(PrimitiveType.Sphere, "Pauldron", root, new Vector3(0.32f * side, 1.32f, 0f), new Vector3(0.3f, 0.2f, 0.3f), look.Secondary);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                    StyleKit.Part(PrimitiveType.Cube, "ShoulderTrim", root, new Vector3(0.28f * side, 1.3f, 0f), new Vector3(0.16f, 0.06f, 0.24f), look.Secondary);
            }

            // Head: slightly large (anime proportions), big eyes, hair
            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            StyleKit.Part(PrimitiveType.Sphere, "Face", head, Vector3.zero, new Vector3(0.4f, 0.44f, 0.4f), look.Skin);
            for (int side = -1; side <= 1; side += 2)
                StyleKit.Part(PrimitiveType.Sphere, "Eye", head, new Vector3(0.075f * side, 0.01f, 0.18f), new Vector3(0.07f, 0.1f, 0.03f),
                    new Color(0.08f, 0.06f, 0.12f), default, look.Accent * 0.5f);
            BuildHair(head, look);

            if (look.Visor)
                StyleKit.Part(PrimitiveType.Cube, "Visor", head, new Vector3(0f, 0.02f, 0.19f), new Vector3(0.38f, 0.08f, 0.04f), look.Accent, default, glow);

            if (look.Scarf)
            {
                StyleKit.Part(PrimitiveType.Cube, "Scarf", root, new Vector3(0f, 1.38f, 0f), new Vector3(0.36f, 0.1f, 0.34f), look.Secondary);
                StyleKit.Part(PrimitiveType.Cube, "ScarfTail", root, new Vector3(0.08f, 1.15f, -0.24f), new Vector3(0.1f, 0.5f, 0.03f),
                    look.Secondary, new Vector3(-20f, 0f, 6f), look.Accent * 0.3f);
            }
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
