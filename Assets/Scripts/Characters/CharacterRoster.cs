using UnityEngine;

namespace WarOfNyx.Characters
{
    public enum HairStyle : byte { Spiky = 0, SweptBack = 1, Short = 2, Crest = 3 }

    /// <summary>Body proportions (visual only; every character uses the same hit shapes).</summary>
    public enum BodyBuild : byte { Lean = 0, Athletic = 1, Slender = 2, Heavy = 3 }

    /// <summary>
    /// One original character (spec 17): look, build, role and story. Purely cosmetic: every character has
    /// the same hitboxes, speed and health (no gameplay bonus). The gear each one wears is chosen to fit
    /// their story (for example KAI's scabbard sits on the left hip for a right-handed draw, AKIRA's chest rig
    /// holds rifle magazines, REN's bandolier holds shotgun shells).
    /// </summary>
    public sealed class CharacterLook
    {
        public byte Id;
        public string Name;
        public string Title;
        public string Role;
        public string Story;
        public string[] Gear;
        /// <summary>Weapon shown in the character preview (fits the story; loadouts in matches stay random).</summary>
        public string SignatureWeapon;
        public BodyBuild Build;
        public Color Primary;   // main clothing
        public Color Secondary; // armour / trim
        public Color Accent;    // glowing details
        public Color Hair;
        public Color HairTips;
        public Color Skin;
        public Color Eyes;
        public HairStyle HairStyle;
        public bool HeavyArmor;
        public bool Scarf;
        public bool Visor;
    }

    /// <summary>The roster (original characters, PLACEHOLDER procedural models until final art exists).</summary>
    public static class CharacterRoster
    {
        public static readonly CharacterLook[] All =
        {
            new CharacterLook
            {
                Id = 0, Name = "KAI", Title = "The Shadow Ronin", Role = "Blade Duelist", SignatureWeapon = "katana", Build = BodyBuild.Lean,
                Story = "The last sworn guardian of the fallen Nyx clan. When the reactor tore the sky open, Kai's oath had no one left to " +
                        "protect, so he swore it to the fight itself. He moves in silence and finishes things with the blade.",
                Gear = new[]
                {
                    "Violet haori coat over a fitted black uniform",
                    "Katana scabbard on the left hip, for a right-handed draw",
                    "Bandaged forearms and fingerless gloves",
                    "Clan scarf whose tails trail behind him",
                    "Old scar across the left eye",
                },
                Primary = new Color(0.12f, 0.08f, 0.2f), Secondary = new Color(0.35f, 0.12f, 0.55f), Accent = new Color(0.62f, 0.25f, 1f),
                Hair = new Color(0.06f, 0.05f, 0.1f), HairTips = new Color(0.4f, 0.18f, 0.7f), Skin = new Color(0.98f, 0.84f, 0.74f),
                Eyes = new Color(0.6f, 0.3f, 1f), HairStyle = HairStyle.Spiky, Scarf = true,
            },
            new CharacterLook
            {
                Id = 1, Name = "AKIRA", Title = "The Crimson Hunter", Role = "Assault Tracker", SignatureWeapon = "nx7_ar", Build = BodyBuild.Athletic,
                Story = "A bounty hunter who tracks targets across Nyx Island for a living. Methodical and patient, always counting rounds: " +
                        "the chest rig carries three spare rifle magazines and the thigh holster a backup pistol.",
                Gear = new[]
                {
                    "Cropped black tactical jacket with crimson panels and a high collar",
                    "Chest rig with three rifle magazine pouches",
                    "Pistol holster strapped to the right thigh",
                    "Knee pads, cargo trousers, laced combat boots",
                    "Crimson hunter's armband and a single earring",
                },
                Primary = new Color(0.16f, 0.14f, 0.18f), Secondary = new Color(0.8f, 0.12f, 0.2f), Accent = new Color(1f, 0.3f, 0.35f),
                Hair = new Color(0.55f, 0.08f, 0.12f), HairTips = new Color(0.85f, 0.2f, 0.22f), Skin = new Color(0.96f, 0.8f, 0.68f),
                Eyes = new Color(0.95f, 0.45f, 0.2f), HairStyle = HairStyle.SweptBack,
            },
            new CharacterLook
            {
                Id = 2, Name = "YUNA", Title = "The Neon Phantom", Role = "Recon Hacker", SignatureWeapon = "shadow_smg", Build = BodyBuild.Slender,
                Story = "A runaway from the island's research labs who opens drones and doors faster than most can reload. The visor feeds " +
                        "her live scans, the wrist computer runs her intrusion kit, and the earpiece keeps her on every channel.",
                Gear = new[]
                {
                    "White techwear jacket with cyan light piping and a hood",
                    "Holographic half-visor over the right eye",
                    "Wrist computer on the left forearm",
                    "Earpiece with antenna, glowing hair clip",
                    "Light-up boots with glowing soles",
                },
                Primary = new Color(0.9f, 0.92f, 1f), Secondary = new Color(0.1f, 0.35f, 0.75f), Accent = new Color(0f, 0.9f, 1f),
                Hair = new Color(0.25f, 0.55f, 0.95f), HairTips = new Color(0.55f, 0.85f, 1f), Skin = new Color(1f, 0.87f, 0.8f),
                Eyes = new Color(0.2f, 0.8f, 1f), HairStyle = HairStyle.Short, Visor = true,
            },
            new CharacterLook
            {
                Id = 3, Name = "REN", Title = "The Iron Wolf", Role = "Heavy Vanguard", SignatureWeapon = "crimson_shotgun", Build = BodyBuild.Heavy,
                Story = "A veteran of the reactor wars who lost his left arm to the Nyx Core and rebuilt it himself. He holds the line at close " +
                        "range: shells in the bandolier across his chest, his old unit's dog tags around his neck.",
                Gear = new[]
                {
                    "Ribbed armour plates and an oversized left pauldron",
                    "Self-built mechanical left arm with glowing joints",
                    "Shotgun-shell bandolier across the chest",
                    "Wolf-fur mantle, dog tags",
                    "Silver crest hair, beard and a battle scar",
                },
                Primary = new Color(0.22f, 0.24f, 0.28f), Secondary = new Color(0.42f, 0.45f, 0.5f), Accent = new Color(1f, 0.72f, 0.25f),
                Hair = new Color(0.75f, 0.76f, 0.8f), HairTips = new Color(0.92f, 0.93f, 0.96f), Skin = new Color(0.86f, 0.7f, 0.58f),
                Eyes = new Color(1f, 0.7f, 0.25f), HairStyle = HairStyle.Crest, HeavyArmor = true,
            },
        };

        public static int Count => All.Length;

        public static CharacterLook Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];
    }
}
