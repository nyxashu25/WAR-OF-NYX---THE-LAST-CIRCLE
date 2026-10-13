using UnityEngine;

namespace WarOfNyx.Characters
{
    public enum HairStyle : byte { Spiky = 0, SweptBack = 1, Short = 2, Crest = 3 }

    /// <summary>
    /// One original character look (spec 17). Purely cosmetic: every character has the same hitboxes,
    /// speed and health (no gameplay bonus in the MVP).
    /// </summary>
    public sealed class CharacterLook
    {
        public byte Id;
        public string Name;
        public string Title;
        public Color Primary;   // clothing
        public Color Secondary; // armour / trim
        public Color Accent;    // glowing details
        public Color Hair;
        public Color Skin;
        public HairStyle HairStyle;
        public bool HeavyArmor;
        public bool Scarf;
        public bool Visor;
    }

    /// <summary>The initial roster (spec 17 examples). PLACEHOLDER builds from primitives until final art exists.</summary>
    public static class CharacterRoster
    {
        public static readonly CharacterLook[] All =
        {
            new CharacterLook
            {
                Id = 0, Name = "KAI", Title = "The Shadow Ronin",
                Primary = new Color(0.12f, 0.08f, 0.2f), Secondary = new Color(0.35f, 0.12f, 0.55f), Accent = new Color(0.62f, 0.25f, 1f),
                Hair = new Color(0.06f, 0.05f, 0.1f), Skin = new Color(0.98f, 0.84f, 0.74f), HairStyle = HairStyle.Spiky, Scarf = true,
            },
            new CharacterLook
            {
                Id = 1, Name = "AKIRA", Title = "The Crimson Hunter",
                Primary = new Color(0.16f, 0.14f, 0.18f), Secondary = new Color(0.8f, 0.12f, 0.2f), Accent = new Color(1f, 0.3f, 0.35f),
                Hair = new Color(0.55f, 0.08f, 0.12f), Skin = new Color(0.96f, 0.8f, 0.68f), HairStyle = HairStyle.SweptBack, HeavyArmor = false,
            },
            new CharacterLook
            {
                Id = 2, Name = "YUNA", Title = "The Neon Phantom",
                Primary = new Color(0.9f, 0.92f, 1f), Secondary = new Color(0.1f, 0.35f, 0.75f), Accent = new Color(0f, 0.9f, 1f),
                Hair = new Color(0.25f, 0.55f, 0.95f), Skin = new Color(1f, 0.87f, 0.8f), HairStyle = HairStyle.Short, Visor = true,
            },
            new CharacterLook
            {
                Id = 3, Name = "REN", Title = "The Iron Wolf",
                Primary = new Color(0.22f, 0.24f, 0.28f), Secondary = new Color(0.42f, 0.45f, 0.5f), Accent = new Color(1f, 0.72f, 0.25f),
                Hair = new Color(0.75f, 0.76f, 0.8f), Skin = new Color(0.86f, 0.7f, 0.58f), HairStyle = HairStyle.Crest, HeavyArmor = true,
            },
        };

        public static int Count => All.Length;

        public static CharacterLook Get(int id) => All[Mathf.Clamp(id, 0, All.Length - 1)];
    }
}
