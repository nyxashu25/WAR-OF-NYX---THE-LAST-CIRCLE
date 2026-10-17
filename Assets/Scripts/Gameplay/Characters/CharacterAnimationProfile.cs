using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// How a character moves (section 40.6): its animator controller (an override of the shared controller with the
    /// character's own clips), plus presentation numbers the runtime layers use: weapon carry and recoil body language,
    /// how much the upper body follows the aim, idle-variation frequency. Cosmetic only: gameplay speed, timing and hit
    /// shapes are the same for every character.
    /// </summary>
    [CreateAssetMenu(menuName = "War of Nyx/Character Animation Profile")]
    public sealed class CharacterAnimationProfile : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        [Tooltip("Rifle carried at the hip: offset in the body frame (metres).")]
        public Vector3 rifleHip = new Vector3(0.17f, 1.29f, 0.3f);
        public Vector3 rifleAim = new Vector3(0.12f, 1.43f, 0.34f);
        [Tooltip("Visual recoil multiplier (body language only).")]
        public float recoilBodyLanguage = 0.8f;
        [Tooltip("Share of the aim pitch taken by the spine (the rest by the head).")]
        public float spineAimShare = 0.35f;
        [Tooltip("Seconds between idle variations (look around, adjust gloves).")]
        public float idleVariationSeconds = 9f;
        [Tooltip("Foot IK: how far a foot may be raised or lowered to meet the ground (m).")]
        public float footIkRange = 0.35f;
        [Tooltip("Body size relative to KAI (shoulder height ratio): scales where weapons are carried so the hands reach them.")]
        public float bodyScale = 1f;
    }
}
