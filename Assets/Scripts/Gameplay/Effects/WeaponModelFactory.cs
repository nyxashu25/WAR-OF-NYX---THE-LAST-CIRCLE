using UnityEngine;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER weapon models (Phase 10 "improve weapon models"): a distinct silhouette per catalog
    /// weapon built from cel-shaded parts, so players can tell an SMG from a marksman rifle at a glance.
    /// Returns the muzzle point for tracers and flashes. Visual only.
    /// </summary>
    public static class WeaponModelFactory
    {
        private static readonly Color Metal = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color Grip = new Color(0.1f, 0.09f, 0.12f);

        public static Transform Build(WeaponStats weapon, Transform hand, out Vector3 muzzleLocal)
        {
            var root = new GameObject("Model (PLACEHOLDER)").transform;
            root.SetParent(hand, false);
            muzzleLocal = new Vector3(0f, 0.02f, 0.55f);
            if (weapon == null)
                return root;

            var accent = AccentFor(weapon);
            var glow = accent * 1.5f;
            switch (weapon.id)
            {
                case WeaponCatalog.Nx7AssaultRifle:
                    Receiver(root, 0.42f, 0.12f, accent, glow);
                    Barrel(root, 0.2f, 0.025f, 0.41f);
                    Mag(root, 0.15f, 0.16f, 15f);
                    Stock(root, 0.2f);
                    StyleKit.Part(PrimitiveType.Cube, "Sight", root, new Vector3(0f, 0.09f, 0.16f), new Vector3(0.04f, 0.04f, 0.1f), Metal);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.62f);
                    break;
                case WeaponCatalog.ShadowSmg:
                case WeaponCatalog.StormLightSmg:
                    Receiver(root, 0.3f, 0.11f, accent, glow);
                    Barrel(root, 0.12f, 0.022f, 0.3f);
                    Mag(root, 0.1f, 0.22f, 0f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.43f);
                    break;
                case WeaponCatalog.CrimsonShotgun:
                    Receiver(root, 0.45f, 0.13f, accent, glow);
                    Barrel(root, 0.3f, 0.04f, 0.47f);
                    StyleKit.Part(PrimitiveType.Cube, "Pump", root, new Vector3(0f, -0.06f, 0.42f), new Vector3(0.07f, 0.06f, 0.18f), accent * 0.8f);
                    Stock(root, 0.22f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.78f);
                    break;
                case WeaponCatalog.PhantomMarksman:
                    Receiver(root, 0.5f, 0.11f, accent, glow);
                    Barrel(root, 0.45f, 0.022f, 0.5f);
                    StyleKit.Part(PrimitiveType.Cylinder, "Scope", root, new Vector3(0f, 0.1f, 0.2f), new Vector3(0.05f, 0.12f, 0.05f), Metal,
                        new Vector3(90f, 0f, 0f));
                    StyleKit.Part(PrimitiveType.Sphere, "Lens", root, new Vector3(0f, 0.1f, 0.33f), Vector3.one * 0.045f, accent, default, glow);
                    Mag(root, 0.12f, 0.1f, 0f);
                    Stock(root, 0.26f);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.95f);
                    break;
                case WeaponCatalog.StormBurstRifle:
                    Receiver(root, 0.4f, 0.13f, accent, glow);
                    Barrel(root, 0.22f, 0.026f, 0.4f);
                    Mag(root, 0.14f, 0.14f, 10f);
                    for (int i = 0; i < 3; i++)
                        StyleKit.Part(PrimitiveType.Cube, "Stripe", root, new Vector3(0.045f, 0.02f, 0.08f + i * 0.08f), new Vector3(0.01f, 0.08f, 0.03f),
                            accent, default, glow);
                    muzzleLocal = new Vector3(0f, 0.02f, 0.63f);
                    break;
                case WeaponCatalog.NeonPistol:
                    StyleKit.Part(PrimitiveType.Cube, "Slide", root, new Vector3(0f, 0.03f, 0.08f), new Vector3(0.05f, 0.07f, 0.22f), Metal);
                    StyleKit.Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.07f, 0f), new Vector3(0.045f, 0.14f, 0.06f), Grip,
                        new Vector3(-15f, 0f, 0f));
                    StyleKit.Part(PrimitiveType.Cube, "Neon", root, new Vector3(0.027f, 0.03f, 0.08f), new Vector3(0.005f, 0.02f, 0.18f), accent, default, glow);
                    muzzleLocal = new Vector3(0f, 0.04f, 0.2f);
                    break;
                default:
                    if (weapon.IsMelee)
                        BuildBlade(root, weapon, accent, glow);
                    else
                    {
                        Receiver(root, 0.4f, 0.12f, accent, glow);
                        Barrel(root, 0.2f, 0.025f, 0.4f);
                    }
                    break;
            }
            return root;
        }

        private static void BuildBlade(Transform root, WeaponStats weapon, Color accent, Color glow)
        {
            float length = weapon.id == WeaponCatalog.Katana ? 0.95f
                : weapon.id == WeaponCatalog.EnergyBlade ? 0.85f
                : weapon.id == WeaponCatalog.CombatKnife ? 0.32f
                : 0.26f;
            bool energy = weapon.id == WeaponCatalog.EnergyBlade;
            var bladeColor = energy ? accent : new Color(0.82f, 0.86f, 0.95f);
            StyleKit.Part(PrimitiveType.Cylinder, "Handle", root, new Vector3(0f, 0f, 0f), new Vector3(0.04f, 0.09f, 0.04f), Grip);
            StyleKit.Part(PrimitiveType.Cube, "Guard", root, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.02f, 0.05f), accent * 0.8f);
            StyleKit.Part(PrimitiveType.Cube, "Blade", root, new Vector3(0f, 0.11f + length * 0.5f, 0f), new Vector3(0.025f, length, 0.06f),
                bladeColor, default, energy ? glow * 1.4f : default);
            StyleKit.Cone("Tip", root, new Vector3(0f, 0.11f + length, 0f), new Vector3(0.06f, 0.08f, 0.025f), bladeColor, Vector3.zero,
                energy ? glow : default);
        }

        private static void Receiver(Transform root, float length, float height, Color accent, Color glow)
        {
            StyleKit.Part(PrimitiveType.Cube, "Receiver", root, new Vector3(0f, 0f, length * 0.4f), new Vector3(0.08f, height, length), Metal);
            StyleKit.Part(PrimitiveType.Cube, "Accent", root, new Vector3(0.042f, 0.01f, length * 0.4f), new Vector3(0.006f, height * 0.25f, length * 0.8f),
                accent, default, glow);
            StyleKit.Part(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.09f, 0.02f), new Vector3(0.05f, 0.13f, 0.06f), Grip,
                new Vector3(-15f, 0f, 0f));
        }

        private static void Barrel(Transform root, float length, float radius, float start)
        {
            StyleKit.Part(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.02f, start + length * 0.5f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), Metal, new Vector3(90f, 0f, 0f));
        }

        private static void Mag(Transform root, float z, float height, float tilt)
        {
            StyleKit.Part(PrimitiveType.Cube, "Magazine", root, new Vector3(0f, -0.06f - height * 0.4f, z), new Vector3(0.05f, height, 0.07f), Grip,
                new Vector3(tilt, 0f, 0f));
        }

        private static void Stock(Transform root, float length)
        {
            StyleKit.Part(PrimitiveType.Cube, "Stock", root, new Vector3(0f, -0.02f, -length * 0.5f), new Vector3(0.06f, 0.1f, length), Metal);
        }

        private static Color AccentFor(WeaponStats weapon)
        {
            switch (weapon.rarity)
            {
                case Core.Items.Rarity.Legendary: return new Color(1f, 0.78f, 0.34f);
                case Core.Items.Rarity.Epic: return new Color(0.62f, 0.25f, 1f);
                case Core.Items.Rarity.Rare: return new Color(0.2f, 0.55f, 1f);
                case Core.Items.Rarity.Uncommon: return new Color(0.3f, 0.95f, 0.5f);
                default: return new Color(0f, 0.9f, 1f);
            }
        }
    }
}
