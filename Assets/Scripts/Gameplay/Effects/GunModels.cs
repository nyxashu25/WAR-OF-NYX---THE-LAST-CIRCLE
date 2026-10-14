using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Detailed procedural PLACEHOLDER firearm models (one mesh per weapon, built once, WarOfNyx/NyxToonVC materials):
    /// bevelled polymer receivers with ejection ports and glowing rarity strips, machined steel barrels with muzzle
    /// devices, vented handguards with accessory rails, stippled grips with trigger guards, magazines shaped for the
    /// weapon (curved rifle, straight SMG, drum, belt box, shotgun tube, revolver cylinder), stocks, and optics (iron
    /// sights, red dot, holographic, scopes with glossy lenses and turrets). Hand at the origin, +Z forward, +Y up.
    /// </summary>
    public static class GunModels
    {
        private struct Built
        {
            public Mesh Mesh;
            public Vector3 Muzzle;
        }

        private static readonly Dictionary<string, Built> Cache = new Dictionary<string, Built>();

        private static readonly Color Steel = new Color(0.17f, 0.18f, 0.21f);
        private static readonly Color Bright = new Color(0.55f, 0.57f, 0.62f);
        private static readonly Color Body = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Rubber = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color Port = new Color(0.03f, 0.03f, 0.04f);
        private static readonly Color Wood = new Color(0.35f, 0.18f, 0.1f);
        private static readonly Color Brass = new Color(0.85f, 0.65f, 0.3f);

        public static Mesh Get(WeaponStats weapon, out Vector3 muzzle)
        {
            muzzle = new Vector3(0f, 0.02f, 0.55f);
            if (weapon == null || !weapon.IsRanged)
                return null;
            if (Cache.TryGetValue(weapon.id, out var built) && built.Mesh != null)
            {
                muzzle = built.Muzzle;
                return built.Mesh;
            }
            var k = new MeshKit();
            muzzle = Build(k, weapon);
            built = new Built { Mesh = k.ToMesh("Gun_" + weapon.id), Muzzle = muzzle };
            Cache[weapon.id] = built;
            return built.Mesh;
        }

        private static Mesh _axe;

        /// <summary>
        /// The Nyx War Axe, held like the blades (haft along +Y, edge toward +Z): a lacquered haft with leather wraps and
        /// a steel pommel, steel langets, a bearded head flaring to a polished edge with a glowing inlay, a back spike.
        /// </summary>
        public static Mesh Axe(Color accent)
        {
            if (_axe != null)
                return _axe;
            var k = new MeshKit();
            k.Use(Surf.Lacquer).Surface(new[] { new Ring(-0.24f, 0.016f, 0.014f), new Ring(0.2f, 0.017f, 0.015f), new Ring(0.52f, 0.015f, 0.013f) }, 12,
                S(new Color(0.18f, 0.08f, 0.06f)), Matrix4x4.identity, 0f, null, true, true);
            k.Use(Surf.Leather).Surface(new[] { new Ring(-0.16f, 0.0185f, 0.0165f), new Ring(0.08f, 0.0185f, 0.0165f) }, 12, (y, a) => S(new Color(0.12f, 0.08f, 0.06f)),
                Matrix4x4.identity, 0.01f, (y, a) => -0.0018f * Mathf.Repeat(y / 0.02f + a / 360f, 1f), true, true);
            k.Use(Surf.Metal).Ellipsoid(new Vector3(0.022f, 0.016f, 0.02f), 6, 10, S(Steel), MeshKit.At(new Vector3(0f, -0.25f, 0f)));
            for (int s = -1; s <= 1; s += 2)
                k.RoundBox(new Vector3(0.004f, 0.12f, 0.02f), 0.0015f, S(Steel), MeshKit.At(new Vector3(s * 0.018f, 0.36f, 0f)));
            // Head: the eye around the haft, then the blade flaring forward to the edge.
            k.RoundBox(new Vector3(0.032f, 0.08f, 0.05f), 0.006f, S(Steel), MeshKit.At(new Vector3(0f, 0.45f, 0.005f)));
            k.Band(new[] { new Vector3(0f, 0.45f, 0.03f), new Vector3(0f, 0.445f, 0.08f), new Vector3(0f, 0.44f, 0.13f), new Vector3(0f, 0.44f, 0.155f) },
                new[] { 0.06f, 0.085f, 0.13f, 0.17f }, 0.012f, Vector3.up, _ => S(new Color(0.42f, 0.44f, 0.5f)), Matrix4x4.identity);
            var edge = new[] { new Vector3(0f, 0.355f, 0.15f), new Vector3(0f, 0.4f, 0.163f), new Vector3(0f, 0.44f, 0.166f), new Vector3(0f, 0.48f, 0.163f), new Vector3(0f, 0.525f, 0.15f) };
            k.Use(Surf.Gold).Band(edge, new[] { 0.01f, 0.012f, 0.012f, 0.012f, 0.01f }, 0.005f, Vector3.forward, _ => S(new Color(0.85f, 0.88f, 0.95f)), Matrix4x4.identity);
            k.Use(Surf.Circuit, Weave.Plain, 0f);
            for (int s = -1; s <= 1; s += 2)
                k.Band(new[] { new Vector3(s * 0.0065f, 0.39f, 0.13f), new Vector3(s * 0.0065f, 0.44f, 0.14f), new Vector3(s * 0.0065f, 0.49f, 0.13f) },
                    new[] { 0.003f, 0.004f, 0.003f }, 0.001f, Vector3.forward, _ => G(accent, 0.95f), Matrix4x4.identity);
            k.Outline = 1f;
            k.Use(Surf.Metal).Strand(new Vector3(0f, 0.45f, -0.02f), new Vector3(0f, 0.45f, -0.05f), new Vector3(0f, 0.44f, -0.08f), new Vector3(0f, 0.42f, -0.11f),
                0.016f, 0.4f, S(Steel), S(Bright), Matrix4x4.identity);
            _axe = k.ToMesh("WarAxe");
            return _axe;
        }

        private static Color S(Color c) => MeshKit.Solid(c);
        private static Color G(Color c, float glow) => MeshKit.Glow(c, glow);

        private static Color Accent(WeaponStats w)
        {
            switch (w.rarity)
            {
                case Rarity.Legendary: return new Color(1f, 0.72f, 0.28f);
                case Rarity.Epic: return new Color(0.62f, 0.25f, 1f);
                case Rarity.Rare: return new Color(0.2f, 0.55f, 1f);
                case Rarity.Uncommon: return new Color(0.3f, 0.95f, 0.5f);
                default: return new Color(0f, 0.9f, 1f);
            }
        }

        private static Vector3 Build(MeshKit k, WeaponStats w)
        {
            var accent = Accent(w);
            var paint = Color.Lerp(Body, accent, 0.18f);
            switch (w.id)
            {
                case WeaponCatalog.Nx7AssaultRifle:
                    Receiver(k, -0.05f, 0.33f, 0.11f, 0.07f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.33f, 0.52f, 0.035f, Surf.Polymer, Body);
                    Rail(k, -0.02f, 0.5f, 0.062f);
                    MagCurved(k, 0.17f, 0.17f);
                    StockFixed(k, -0.05f, 0.24f, paint);
                    RedDot(k, 0.1f, accent);
                    return Barrel(k, 0.52f, 0.66f, 0.013f, true);
                case WeaponCatalog.ShadowSmg:
                    Receiver(k, -0.04f, 0.24f, 0.1f, 0.065f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.24f, 0.34f, 0.03f, Surf.Polymer, Body);
                    MagStraight(k, 0.11f, 0.22f, 0f);
                    StockSkeleton(k, -0.04f, 0.2f);
                    RedDot(k, 0.07f, accent);
                    return Barrel(k, 0.34f, 0.44f, 0.012f, false);
                case WeaponCatalog.StormLightSmg:
                    Receiver(k, -0.04f, 0.22f, 0.095f, 0.06f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.22f, 0.32f, 0.028f, Surf.Carbon, Body);
                    MagStraight(k, 0.1f, 0.25f, 0f);
                    ForeGrip(k, 0.27f);
                    IronSights(k, -0.02f, 0.3f, 0.06f);
                    return Barrel(k, 0.32f, 0.42f, 0.011f, false);
                case WeaponCatalog.CrimsonShotgun:
                    Receiver(k, -0.05f, 0.3f, 0.12f, 0.075f, paint, accent);
                    Grip(k, 0.0f);
                    k.Use(Surf.Metal).Surface(Tube(0.3f, 0.8f, 0.022f), 14, S(Steel), Along(0f, 0.025f), 0f, null, false, true);
                    k.Use(Surf.Metal).Surface(Tube(0.3f, 0.72f, 0.017f), 12, S(Steel), Along(0f, -0.02f), 0f, null, false, true);
                    Pump(k, 0.42f, 0.6f, -0.02f, accent);
                    StockFixed(k, -0.05f, 0.27f, paint);
                    k.Use(Surf.Gold).Ellipsoid(new Vector3(0.006f, 0.006f, 0.006f), 4, 8, S(Brass), MeshKit.At(new Vector3(0f, 0.05f, 0.78f)));
                    return new Vector3(0f, 0.025f, 0.8f);
                case WeaponCatalog.PhantomMarksman:
                    Receiver(k, -0.05f, 0.36f, 0.1f, 0.07f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.36f, 0.6f, 0.032f, Surf.Carbon, Body);
                    MagStraight(k, 0.13f, 0.1f, 0f);
                    StockSniper(k, -0.05f, 0.3f, paint);
                    Scope(k, 0.02f, 0.3f, 0.022f, accent);
                    return Barrel(k, 0.6f, 0.95f, 0.012f, true);
                case WeaponCatalog.StormBurstRifle:
                    Receiver(k, -0.12f, 0.3f, 0.12f, 0.075f, paint, accent); // bullpup: magazine behind the grip
                    Grip(k, 0.06f);
                    Handguard(k, 0.3f, 0.46f, 0.034f, Surf.Polymer, Body);
                    MagCurved(k, -0.06f, 0.14f);
                    for (int i = 0; i < 3; i++)
                        k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.004f, 0.06f, 0.016f), G(accent, 0.9f), MeshKit.At(new Vector3(0.039f, 0.0f, 0.08f + i * 0.05f)));
                    k.Outline = 1f;
                    k.Use(Surf.Rubber).RoundBox(new Vector3(0.06f, 0.11f, 0.03f), 0.01f, S(Rubber), MeshKit.At(new Vector3(0f, -0.005f, -0.135f)));
                    Holo(k, 0.12f, accent);
                    return Barrel(k, 0.46f, 0.62f, 0.013f, true);
                case WeaponCatalog.NeonPistol:
                    Pistol(k, paint, accent);
                    return new Vector3(0f, 0.04f, 0.2f);
                case WeaponCatalog.VortexLmg:
                    Receiver(k, -0.06f, 0.36f, 0.14f, 0.09f, paint, accent);
                    Grip(k, 0.0f);
                    k.Use(Surf.Metal).Surface(Tube(0.36f, 0.62f, 0.03f), 14, (y, a) => S(Steel), Along(0f, 0.02f), 0f,
                        (y, a) => Mathf.Repeat(a, 45f) < 12f ? -0.004f : 0f, false, true); // finned heat shroud
                    Rail(k, -0.03f, 0.34f, 0.075f);
                    // Belt box on the left, a short belt feeding the receiver.
                    k.Use(Surf.Polymer).RoundBox(new Vector3(0.09f, 0.11f, 0.12f), 0.008f, S(Body), MeshKit.At(new Vector3(-0.07f, -0.07f, 0.14f)));
                    k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.003f, 0.06f, 0.08f), G(accent, 0.9f), MeshKit.At(new Vector3(-0.116f, -0.07f, 0.14f)));
                    k.Outline = 1f;
                    for (int i = 0; i < 5; i++)
                        k.Use(Surf.Gold).Surface(Tube(0f, 0.035f, 0.0045f), 6, S(Brass), MeshKit.TRS(new Vector3(-0.045f + i * 0.012f, 0.0f, 0.14f), new Vector3(0f, 0f, 90f), Vector3.one),
                            0f, null, false, true);
                    // Carry handle and folded bipod.
                    k.Use(Surf.Metal).Tube(new[] { new Vector3(0f, 0.07f, 0.08f), new Vector3(0f, 0.13f, 0.12f), new Vector3(0f, 0.13f, 0.24f), new Vector3(0f, 0.07f, 0.28f) },
                        0.008f, S(Steel), Matrix4x4.identity);
                    for (int s = -1; s <= 1; s += 2)
                        k.Tube(new[] { new Vector3(s * 0.02f, -0.03f, 0.6f), new Vector3(s * 0.025f, -0.035f, 0.42f) }, 0.006f, S(Steel), Matrix4x4.identity);
                    StockFixed(k, -0.06f, 0.26f, paint);
                    IronSights(k, -0.04f, 0.6f, 0.075f);
                    return Barrel(k, 0.62f, 0.86f, 0.015f, true);
                case WeaponCatalog.ArcSniper:
                    Receiver(k, -0.06f, 0.36f, 0.1f, 0.07f, paint, accent);
                    Grip(k, 0.0f);
                    // Bolt handle with a knob.
                    k.Use(Surf.Metal).Tube(new[] { new Vector3(0.035f, 0.03f, 0.05f), new Vector3(0.07f, 0.02f, 0.045f) }, 0.005f, S(Bright), Matrix4x4.identity);
                    k.Ellipsoid(new Vector3(0.011f, 0.011f, 0.011f), 4, 8, S(Bright), MeshKit.At(new Vector3(0.075f, 0.018f, 0.045f)));
                    k.Use(Surf.Carbon).Surface(Tube(0.36f, 1.05f, 0.017f), 14, S(new Color(0.1f, 0.1f, 0.12f)), Along(0f, 0.02f), 0f, null, false, true);
                    // Arc coils down the barrel.
                    k.Use(Surf.Circuit, Weave.Plain, 0f);
                    for (int i = 0; i < 6; i++)
                        k.Torus(0.021f, 0.021f, 0.004f, 14, 4, G(accent, 0.9f), MeshKit.TRS(new Vector3(0f, 0.02f, 0.45f + i * 0.09f), new Vector3(90f, 0f, 0f), Vector3.one));
                    k.Outline = 1f;
                    MagStraight(k, 0.15f, 0.09f, 0f);
                    StockSniper(k, -0.06f, 0.34f, paint);
                    Scope(k, -0.02f, 0.36f, 0.026f, accent);
                    for (int s = -1; s <= 1; s += 2)
                        k.Use(Surf.Metal).Tube(new[] { new Vector3(s * 0.022f, -0.025f, 0.7f), new Vector3(s * 0.026f, -0.03f, 0.48f) }, 0.006f, S(Steel), Matrix4x4.identity);
                    return Barrel(k, 1.05f, 1.15f, 0.02f, true);
                case WeaponCatalog.TalonAutoShotgun:
                    Receiver(k, -0.05f, 0.3f, 0.13f, 0.08f, paint, accent);
                    Grip(k, 0.0f);
                    k.Use(Surf.Polymer).Surface(Tube(0.3f, 0.52f, 0.034f), 10, (y, a) => S(Body), Along(0f, 0.02f), 0f,
                        (y, a) => Mathf.Repeat(y * 40f, 1f) < 0.35f && Mathf.Abs(Mathf.Abs(a) - 90f) < 30f ? -0.006f : 0f, false, true); // vented shroud
                    // Drum magazine: a ribbed cylinder across the gun.
                    k.Use(Surf.Polymer).Surface(Tube(-0.045f, 0.045f, 0.065f), 20, (y, a) => S(Body), MeshKit.TRS(new Vector3(0f, -0.12f, 0.13f), new Vector3(0f, 0f, 90f), Vector3.one), 0f,
                        (y, a) => Mathf.Repeat(a, 30f) < 8f ? -0.003f : 0f, true, true);
                    k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.055f, 0.055f, 0.003f, 20, 4, G(accent, 0.9f), MeshKit.TRS(new Vector3(0.047f, -0.12f, 0.13f), new Vector3(0f, 0f, 90f), Vector3.one));
                    k.Outline = 1f;
                    StockFixed(k, -0.05f, 0.24f, paint);
                    RedDot(k, 0.08f, accent);
                    return Barrel(k, 0.52f, 0.6f, 0.02f, true);
                case WeaponCatalog.RiftCarbine:
                    Receiver(k, -0.05f, 0.28f, 0.105f, 0.068f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.28f, 0.42f, 0.033f, Surf.Carbon, Body);
                    Rail(k, -0.02f, 0.4f, 0.06f);
                    MagCurved(k, 0.14f, 0.16f);
                    StockSkeleton(k, -0.05f, 0.22f);
                    Holo(k, 0.1f, accent);
                    // Integrated suppressor.
                    k.Use(Surf.Metal).Surface(Tube(0.42f, 0.6f, 0.024f), 14, S(new Color(0.1f, 0.1f, 0.11f)), Along(0f, 0.02f), 0f,
                        (y, a) => Mathf.Repeat(y * 25f, 1f) < 0.2f ? -0.002f : 0f, true, true);
                    return new Vector3(0f, 0.02f, 0.6f);
                case WeaponCatalog.ViperRevolver:
                    Revolver(k, paint, accent);
                    return new Vector3(0f, 0.045f, 0.3f);
                case WeaponCatalog.PulseDmr:
                    Receiver(k, -0.05f, 0.34f, 0.11f, 0.07f, paint, accent);
                    Grip(k, 0.0f);
                    Handguard(k, 0.34f, 0.62f, 0.034f, Surf.Polymer, Body);
                    k.Use(Surf.Circuit, Weave.Plain, 0f);
                    for (int s = -1; s <= 1; s += 2)
                        for (int i = 0; i < 4; i++)
                            k.RoundBox(new Vector3(0.004f, 0.016f, 0.045f), 0.0015f, G(accent, 0.9f), MeshKit.At(new Vector3(s * 0.035f, 0.02f, 0.38f + i * 0.06f)));
                    k.Outline = 1f;
                    MagStraight(k, 0.15f, 0.12f, 6f);
                    StockFixed(k, -0.05f, 0.28f, paint);
                    Scope(k, 0.03f, 0.24f, 0.02f, accent);
                    return Barrel(k, 0.62f, 0.8f, 0.013f, true);
                default:
                    Receiver(k, -0.05f, 0.33f, 0.11f, 0.07f, paint, accent);
                    Grip(k, 0.0f);
                    MagStraight(k, 0.15f, 0.14f, 0f);
                    return Barrel(k, 0.33f, 0.55f, 0.013f, false);
            }
        }

        // ------------------------------------------------------------------ parts

        /// <summary>Rings from z0 to z1 (built along +Y, then turned to +Z by <see cref="Along"/>).</summary>
        private static Ring[] Tube(float z0, float z1, float radius) => new[] { new Ring(z0, radius, radius), new Ring(z1, radius, radius) };

        private static Matrix4x4 Along(float x, float y) => Matrix4x4.TRS(new Vector3(x, y, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);

        private static void Receiver(MeshKit k, float z0, float z1, float height, float width, Color paint, Color accent)
        {
            float len = z1 - z0, zc = (z0 + z1) * 0.5f;
            k.Use(Surf.Polymer).RoundBox(new Vector3(width, height, len), 0.012f, S(paint), MeshKit.At(new Vector3(0f, 0.005f, zc)));
            // Upper receiver in steel, ejection port on the right, glowing rarity strips on both sides.
            k.Use(Surf.Metal).RoundBox(new Vector3(width * 0.92f, height * 0.38f, len * 0.92f), 0.006f, S(Steel), MeshKit.At(new Vector3(0f, 0.005f + height * 0.42f, zc)));
            k.Use(Surf.Metal, Weave.Plain, 0f).Box(new Vector3(0.003f, height * 0.25f, len * 0.22f), S(Port), MeshKit.At(new Vector3(width * 0.5f + 0.0005f, height * 0.2f, zc + len * 0.12f)));
            k.Use(Surf.Circuit, Weave.Plain, 0f);
            for (int s = -1; s <= 1; s += 2)
                k.Box(new Vector3(0.003f, height * 0.12f, len * 0.6f), G(accent, 0.9f), MeshKit.At(new Vector3(s * (width * 0.5f + 0.0012f), -height * 0.12f, zc - len * 0.05f)));
            // Trigger guard and trigger.
            k.Use(Surf.Metal).Torus(0.022f, 0.032f, 0.0035f, 14, 4, S(Steel), MeshKit.TRS(new Vector3(0f, -height * 0.5f - 0.012f, 0.045f), new Vector3(0f, 0f, 90f), Vector3.one));
            k.Box(new Vector3(0.006f, 0.022f, 0.006f), S(Bright), MeshKit.TRS(new Vector3(0f, -height * 0.5f - 0.008f, 0.042f), new Vector3(15f, 0f, 0f), Vector3.one));
            k.Outline = 1f;
        }

        private static void Grip(MeshKit k, float z)
        {
            var m = MeshKit.TRS(new Vector3(0f, -0.1f, z + 0.005f), new Vector3(-16f, 0f, 0f), Vector3.one);
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.042f, 0.12f, 0.055f), 0.012f, S(Rubber), m);
            // Stippling panels and finger grooves.
            k.Use(Surf.Rubber, Weave.Plain, 0f);
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(0.044f, 0.004f, 0.012f), S(new Color(0.12f, 0.12f, 0.13f)), m * MeshKit.At(new Vector3(0f, 0.02f - i * 0.026f, 0.026f)));
            k.Outline = 1f;
        }

        private static void ForeGrip(MeshKit k, float z)
        {
            k.Use(Surf.Rubber).Surface(Tube(-0.09f, -0.02f, 0.014f), 10, S(Rubber), MeshKit.At(new Vector3(0f, 0f, z)), 0f,
                (y, a) => Mathf.Repeat(y * 60f, 1f) < 0.3f ? -0.0015f : 0f, true, false);
        }

        private static void Handguard(MeshKit k, float z0, float z1, float radius, float surf, Color color)
        {
            // Octagonal handguard with rows of vents on the sides and underneath.
            k.Use(surf, Weave.Plain).Surface(Tube(z0, z1, radius), 8, S(color), Along(0f, 0.02f), 0f, null, true, true);
            k.Use(Surf.Metal, Weave.Plain, 0f);
            int vents = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / 0.035f));
            for (int i = 0; i < vents; i++)
            {
                float z = Mathf.Lerp(z0 + 0.02f, z1 - 0.02f, (i + 0.5f) / vents);
                for (int s = -1; s <= 1; s += 2)
                    k.RoundBox(new Vector3(0.002f, 0.009f, 0.02f), 0.001f, S(Port), MeshKit.At(new Vector3(s * radius * 0.93f, 0.02f, z)));
                k.RoundBox(new Vector3(0.009f, 0.002f, 0.02f), 0.001f, S(Port), MeshKit.At(new Vector3(0f, 0.02f - radius * 0.93f, z)));
            }
            k.Outline = 1f;
        }

        private static void Rail(MeshKit k, float z0, float z1, float y)
        {
            k.Use(Surf.Metal).Box(new Vector3(0.022f, 0.006f, z1 - z0), S(Steel), MeshKit.At(new Vector3(0f, y, (z0 + z1) * 0.5f)));
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (float z = z0 + 0.006f; z < z1 - 0.004f; z += 0.01f)
                k.Box(new Vector3(0.022f, 0.004f, 0.005f), S(Steel), MeshKit.At(new Vector3(0f, y + 0.005f, z)));
            k.Outline = 1f;
        }

        private static Vector3 Barrel(MeshKit k, float z0, float z1, float radius, bool brake)
        {
            k.Use(Surf.Metal).Surface(Tube(z0, z1, radius), 12, S(Steel), Along(0f, 0.02f), 0f, null, false, true);
            if (!brake)
                return new Vector3(0f, 0.02f, z1);
            // Muzzle brake with ports on both sides.
            float bz = z1;
            k.Surface(Tube(bz - 0.005f, bz + 0.05f, radius * 1.55f), 12, S(new Color(0.12f, 0.12f, 0.14f)), Along(0f, 0.02f), 0f, null, true, true);
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 2; i++)
                    k.Box(new Vector3(0.003f, radius * 1.4f, 0.01f), S(Port), MeshKit.At(new Vector3(s * radius * 1.56f, 0.02f, bz + 0.012f + i * 0.02f)));
            k.Outline = 1f;
            return new Vector3(0f, 0.02f, bz + 0.05f);
        }

        private static void MagStraight(MeshKit k, float z, float height, float tilt)
        {
            var m = MeshKit.TRS(new Vector3(0f, -0.05f - height * 0.5f, z), new Vector3(tilt, 0f, 0f), Vector3.one);
            k.Use(Surf.Polymer).RoundBox(new Vector3(0.036f, height, 0.06f), 0.006f, S(Rubber), m);
            k.Use(Surf.Polymer, Weave.Plain, 0f);
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(0.038f, 0.003f, 0.05f), S(new Color(0.14f, 0.14f, 0.15f)), m * MeshKit.At(new Vector3(0f, height * (0.2f - i * 0.15f), 0f)));
            k.Outline = 1f;
            k.Use(Surf.Metal).RoundBox(new Vector3(0.04f, 0.012f, 0.064f), 0.003f, S(Steel), m * MeshKit.At(new Vector3(0f, -height * 0.5f, 0f)));
        }

        private static void MagCurved(MeshKit k, float z, float height)
        {
            // Four segments, each angled further forward: the classic rifle-magazine curve.
            int parts = 4;
            float seg = height / parts;
            var at = new Vector3(0f, -0.05f, z);
            float angle = 0f;
            k.Use(Surf.Polymer);
            for (int i = 0; i < parts; i++)
            {
                angle += 7f;
                var rot = Quaternion.Euler(angle, 0f, 0f);
                var centre = at + rot * new Vector3(0f, -seg * 0.5f, 0f);
                k.RoundBox(new Vector3(0.034f, seg * 1.08f, 0.062f), 0.005f, S(i == parts - 1 ? Steel : Rubber), Matrix4x4.TRS(centre, rot, Vector3.one));
                at += rot * new Vector3(0f, -seg, 0f);
            }
        }

        private static void StockFixed(MeshKit k, float zFront, float length, Color paint)
        {
            float zc = zFront - length * 0.5f;
            k.Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.085f, length), 0.014f, S(paint), MeshKit.At(new Vector3(0f, -0.015f, zc)));
            k.RoundBox(new Vector3(0.045f, 0.03f, length * 0.6f), 0.01f, S(Body), MeshKit.At(new Vector3(0f, 0.035f, zc - length * 0.1f))); // cheek riser
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.054f, 0.11f, 0.018f), 0.006f, S(Rubber), MeshKit.At(new Vector3(0f, -0.015f, zFront - length - 0.006f)));
        }

        private static void StockSkeleton(MeshKit k, float zFront, float length)
        {
            float back = zFront - length;
            k.Use(Surf.Metal).Tube(new[] { new Vector3(0f, 0.03f, zFront), new Vector3(0f, 0.03f, back), new Vector3(0f, -0.06f, back), new Vector3(0f, -0.02f, zFront) },
                0.007f, S(Steel), Matrix4x4.identity);
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.04f, 0.1f, 0.014f), 0.005f, S(Rubber), MeshKit.At(new Vector3(0f, -0.015f, back - 0.006f)));
        }

        private static void StockSniper(MeshKit k, float zFront, float length, Color paint)
        {
            float back = zFront - length;
            // Thumbhole stock: upper spine, lower spine, a deep butt and an adjustable cheek piece.
            k.Use(Surf.Polymer).RoundBox(new Vector3(0.05f, 0.04f, length), 0.012f, S(paint), MeshKit.At(new Vector3(0f, 0.01f, zFront - length * 0.5f)));
            k.RoundBox(new Vector3(0.05f, 0.035f, length * 0.6f), 0.012f, S(paint), MeshKit.At(new Vector3(0f, -0.08f, back + length * 0.3f), new Vector3(-12f, 0f, 0f)));
            k.RoundBox(new Vector3(0.052f, 0.14f, 0.05f), 0.014f, S(paint), MeshKit.At(new Vector3(0f, -0.03f, back + 0.025f)));
            k.Use(Surf.Metal).RoundBox(new Vector3(0.04f, 0.022f, length * 0.45f), 0.008f, S(Steel), MeshKit.At(new Vector3(0f, 0.045f, back + length * 0.3f)));
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.056f, 0.15f, 0.02f), 0.007f, S(Rubber), MeshKit.At(new Vector3(0f, -0.03f, back - 0.01f)));
        }

        private static void IronSights(MeshKit k, float zRear, float zFront, float y)
        {
            k.Use(Surf.Metal).RoundBox(new Vector3(0.03f, 0.022f, 0.02f), 0.003f, S(Steel), MeshKit.At(new Vector3(0f, y, zRear)));
            k.RoundBox(new Vector3(0.006f, 0.035f, 0.008f), 0.002f, S(Steel), MeshKit.At(new Vector3(0f, y + 0.005f, zFront)));
        }

        private static void RedDot(MeshKit k, float z, Color accent)
        {
            k.Use(Surf.Metal).RoundBox(new Vector3(0.03f, 0.012f, 0.05f), 0.003f, S(Steel), MeshKit.At(new Vector3(0f, 0.075f, z)));
            k.Surface(Tube(-0.03f, 0.03f, 0.017f), 14, S(Body), MeshKit.TRS(new Vector3(0f, 0.1f, z), new Vector3(90f, 0f, 0f), Vector3.one), 0f, null, false, false);
            k.Use(Surf.Eye, Weave.Plain, 0f).Disc(0.015f, 0.015f, 14, S(new Color(0.15f, 0.25f, 0.3f)), MeshKit.At(new Vector3(0f, 0.1f, z + 0.025f)));
            k.Use(Surf.Circuit, Weave.Plain, 0f).Disc(0.0025f, 0.0025f, 8, G(new Color(1f, 0.15f, 0.1f), 1f), MeshKit.TRS(new Vector3(0f, 0.1f, z - 0.02f), new Vector3(0f, 180f, 0f), Vector3.one));
            k.Disc(0.004f, 0.004f, 8, G(accent, 0.9f), MeshKit.TRS(new Vector3(0.0175f, 0.1f, z), new Vector3(0f, 90f, 0f), Vector3.one));
            k.Outline = 1f;
        }

        private static void Holo(MeshKit k, float z, Color accent)
        {
            k.Use(Surf.Metal).RoundBox(new Vector3(0.034f, 0.014f, 0.07f), 0.004f, S(Steel), MeshKit.At(new Vector3(0f, 0.075f, z)));
            // Window frame and the glowing reticle glass.
            k.Use(Surf.Polymer).RoundBox(new Vector3(0.004f, 0.036f, 0.06f), 0.0015f, S(Body), MeshKit.At(new Vector3(0.018f, 0.1f, z)));
            k.RoundBox(new Vector3(0.004f, 0.036f, 0.06f), 0.0015f, S(Body), MeshKit.At(new Vector3(-0.018f, 0.1f, z)));
            k.RoundBox(new Vector3(0.04f, 0.006f, 0.06f), 0.002f, S(Body), MeshKit.At(new Vector3(0f, 0.12f, z)));
            k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.007f, 0.007f, 0.0009f, 14, 3, G(accent, 1f), MeshKit.TRS(new Vector3(0f, 0.1f, z - 0.02f), new Vector3(90f, 0f, 0f), Vector3.one));
            k.Outline = 1f;
        }

        private static void Scope(MeshKit k, float zRear, float zFront, float radius, Color accent)
        {
            float y = 0.1f;
            var tube = new[]
            {
                new Ring(zRear, radius * 1.3f, radius * 1.3f), new Ring(zRear + 0.04f, radius * 1.3f, radius * 1.3f), new Ring(zRear + 0.07f, radius, radius),
                new Ring(zFront - 0.08f, radius, radius), new Ring(zFront - 0.04f, radius * 1.55f, radius * 1.55f), new Ring(zFront, radius * 1.6f, radius * 1.6f),
            };
            k.Use(Surf.Metal).Surface(tube, 18, S(new Color(0.09f, 0.09f, 0.1f)), Along(0f, y), 0f, null, false, false);
            // Glossy lenses: dark glass with a coloured coating sheen.
            k.Use(Surf.Eye, Weave.Plain, 0f).Disc(radius * 1.5f, radius * 1.5f, 18, S(Color.Lerp(new Color(0.05f, 0.1f, 0.15f), accent, 0.25f)), MeshKit.At(new Vector3(0f, y, zFront - 0.002f)));
            k.Disc(radius * 1.2f, radius * 1.2f, 16, S(new Color(0.05f, 0.08f, 0.1f)), MeshKit.TRS(new Vector3(0f, y, zRear + 0.002f), new Vector3(0f, 180f, 0f), Vector3.one));
            k.Outline = 1f;
            // Turrets (elevation and windage) and mount rings.
            float zt = (zRear + zFront) * 0.5f;
            k.Use(Surf.Metal).Surface(Tube(0f, 0.022f, 0.011f), 12, S(Steel), MeshKit.At(new Vector3(0f, y + radius * 0.8f, zt)), 0f,
                (h, a) => Mathf.Repeat(a, 30f) < 10f ? -0.0012f : 0f, false, true);
            k.Surface(Tube(0f, 0.02f, 0.01f), 12, S(Steel), MeshKit.TRS(new Vector3(radius * 0.8f, y, zt), new Vector3(0f, 0f, -90f), Vector3.one), 0f, null, false, true);
            k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(radius * 1.02f, radius * 1.02f, 0.0015f, 18, 3, G(accent, 0.9f),
                MeshKit.TRS(new Vector3(0f, y, zt + 0.03f), new Vector3(90f, 0f, 0f), Vector3.one));
            k.Outline = 1f;
            for (int i = 0; i < 2; i++)
            {
                float z = Mathf.Lerp(zRear + 0.07f, zFront - 0.08f, 0.15f + i * 0.7f);
                k.Use(Surf.Metal).Torus(radius * 1.12f, radius * 1.12f, 0.004f, 16, 4, S(Steel), MeshKit.TRS(new Vector3(0f, y, z), new Vector3(90f, 0f, 0f), Vector3.one));
                k.RoundBox(new Vector3(0.016f, y - 0.065f, 0.012f), 0.002f, S(Steel), MeshKit.At(new Vector3(0f, (y + 0.065f) * 0.5f - radius * 0.4f, z)));
            }
        }

        private static void Pump(MeshKit k, float z0, float z1, float y, Color accent)
        {
            k.Use(Surf.Polymer).Surface(new[] { new Ring(z0, 0.028f, 0.028f), new Ring(z0 + 0.01f, 0.031f, 0.031f), new Ring(z1 - 0.01f, 0.031f, 0.031f), new Ring(z1, 0.028f, 0.028f) },
                14, (h, a) => S(Body), Along(0f, y), 0f, (h, a) => Mathf.Repeat(h * 70f, 1f) < 0.3f && Mathf.Abs(a) > 60f ? -0.002f : 0f, true, true);
            k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(0.0315f, 0.0315f, 0.0015f, 16, 3, G(accent, 0.9f), MeshKit.TRS(new Vector3(0f, y, (z0 + z1) * 0.5f), new Vector3(90f, 0f, 0f), Vector3.one));
            k.Outline = 1f;
        }

        private static void Pistol(MeshKit k, Color paint, Color accent)
        {
            // Slide with rear serrations, frame, grip with stippling, a glowing neon line.
            k.Use(Surf.Metal).RoundBox(new Vector3(0.048f, 0.05f, 0.21f), 0.008f, S(Steel), MeshKit.At(new Vector3(0f, 0.045f, 0.085f)));
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (int i = 0; i < 6; i++)
                for (int s = -1; s <= 1; s += 2)
                    k.Box(new Vector3(0.002f, 0.035f, 0.003f), S(Port), MeshKit.At(new Vector3(s * 0.0242f, 0.045f, 0.0f + i * 0.007f)));
            k.Box(new Vector3(0.002f, 0.016f, 0.03f), S(Port), MeshKit.At(new Vector3(0.0242f, 0.05f, 0.1f))); // ejection port
            k.Outline = 1f;
            k.Use(Surf.Polymer).RoundBox(new Vector3(0.044f, 0.035f, 0.2f), 0.008f, S(paint), MeshKit.At(new Vector3(0f, 0.008f, 0.075f)));
            var grip = MeshKit.TRS(new Vector3(0f, -0.055f, -0.005f), new Vector3(-16f, 0f, 0f), Vector3.one);
            k.Use(Surf.Rubber).RoundBox(new Vector3(0.042f, 0.12f, 0.058f), 0.012f, S(Rubber), grip);
            k.Use(Surf.Metal).Torus(0.018f, 0.026f, 0.0035f, 14, 4, S(paint), MeshKit.TRS(new Vector3(0f, -0.015f, 0.05f), new Vector3(0f, 0f, 90f), Vector3.one));
            k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.003f, 0.008f, 0.17f), G(accent, 0.95f), MeshKit.At(new Vector3(0.0235f, 0.012f, 0.08f)));
            k.Box(new Vector3(0.003f, 0.008f, 0.17f), G(accent, 0.95f), MeshKit.At(new Vector3(-0.0235f, 0.012f, 0.08f)));
            k.Outline = 1f;
            k.Use(Surf.Metal).Surface(Tube(0.19f, 0.2f, 0.007f), 10, S(Port), Along(0f, 0.04f), 0f, null, false, true);
        }

        private static void Revolver(MeshKit k, Color paint, Color accent)
        {
            // Frame, fluted cylinder with chambers, long barrel with a top rib, hammer, lacquered wood grip.
            k.Use(Surf.Metal).RoundBox(new Vector3(0.04f, 0.06f, 0.1f), 0.008f, S(Steel), MeshKit.At(new Vector3(0f, 0.03f, 0.04f)));
            k.Surface(new[] { new Ring(0.0f, 0.03f, 0.03f), new Ring(0.006f, 0.034f, 0.034f), new Ring(0.05f, 0.034f, 0.034f), new Ring(0.056f, 0.03f, 0.03f) }, 24,
                S(Bright), MeshKit.TRS(new Vector3(0f, 0.035f, 0.02f), new Vector3(90f, 0f, 0f), Vector3.one), 0f, (h, a) => Mathf.Repeat(a + 30f, 60f) < 18f ? -0.004f : 0f, true, true);
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                k.Disc(0.0055f, 0.0055f, 8, S(Port), MeshKit.At(new Vector3(Mathf.Sin(a) * 0.02f, 0.035f + Mathf.Cos(a) * 0.02f, 0.0765f)));
            }
            k.Outline = 1f;
            k.Use(Surf.Metal).Surface(Tube(0.09f, 0.3f, 0.011f), 12, S(Steel), Along(0f, 0.045f), 0f, null, false, true);
            k.RoundBox(new Vector3(0.012f, 0.014f, 0.2f), 0.003f, S(Steel), MeshKit.At(new Vector3(0f, 0.058f, 0.2f)));
            k.RoundBox(new Vector3(0.008f, 0.012f, 0.012f), 0.002f, S(Steel), MeshKit.At(new Vector3(0f, 0.07f, 0.295f)));
            k.RoundBox(new Vector3(0.01f, 0.028f, 0.014f), 0.003f, S(Bright), MeshKit.TRS(new Vector3(0f, 0.07f, -0.015f), new Vector3(-30f, 0f, 0f), Vector3.one));
            var grip = MeshKit.TRS(new Vector3(0f, -0.04f, -0.01f), new Vector3(-22f, 0f, 0f), Vector3.one);
            k.Use(Surf.Lacquer).RoundBox(new Vector3(0.04f, 0.11f, 0.05f), 0.016f, S(Wood), grip);
            k.Use(Surf.Gold, Weave.Plain, 0f).Disc(0.007f, 0.007f, 10, S(Brass), grip * MeshKit.TRS(new Vector3(0.0205f, 0.0f, 0f), new Vector3(0f, 90f, 0f), Vector3.one));
            k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.003f, 0.006f, 0.17f), G(accent, 0.95f), MeshKit.At(new Vector3(0.0125f, 0.045f, 0.2f)));
            k.Outline = 1f;
            k.Use(Surf.Metal).Torus(0.018f, 0.024f, 0.0035f, 14, 4, S(Steel), MeshKit.TRS(new Vector3(0f, -0.012f, 0.025f), new Vector3(0f, 0f, 90f), Vector3.one));
        }
    }
}
