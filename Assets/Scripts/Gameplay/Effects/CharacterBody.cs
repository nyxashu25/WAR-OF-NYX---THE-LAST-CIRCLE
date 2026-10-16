using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// What gameplay code tells a character's animation layer, whichever kind it is: the jointed placeholder body
    /// (<see cref="CharacterAnimator"/>) or a skinned humanoid (<see cref="Characters.CharacterAnimationController"/>).
    /// </summary>
    public interface ICharacterPose
    {
        /// <summary>Aim pitch in degrees (+ = looking down).</summary>
        float AimPitch { get; set; }
        bool Seated { get; set; }
        /// <summary>Seated: feet rest (height, forward) relative to the character root.</summary>
        Vector2 SeatFoot { get; set; }
        Vector3? HandTargetL { get; set; }
        Vector3? HandTargetR { get; set; }
        /// <summary>Health share (0..1) for the facial expression.</summary>
        float Health01 { get; set; }
        /// <summary>A foot touched the ground: side (-1 left, +1 right) and intensity.</summary>
        event System.Action<int, float> FootContact;
    }

    /// <summary>
    /// Joints of a styled PLACEHOLDER character (built by <see cref="CharacterStyler"/>), posed every frame by
    /// <see cref="CharacterAnimator"/>. Every limb segment hangs along its joint's local -Y, so a joint's
    /// rotation alone decides where the segment points. Joints are plain transforms (scale 1); the visible
    /// parts are their children. Visual only: hit shapes live on the character root.
    /// </summary>
    public sealed class CharacterBody : MonoBehaviour
    {
        public const float HipHeight = 0.92f;
        public const float HipSpacing = 0.12f;
        public const float ThighLength = 0.44f;
        public const float ShinLength = 0.42f;
        public const float AnkleHeight = 0.06f;
        public const float ShoulderHeight = 1.4f;
        public const float ShoulderSpacing = 0.26f;
        public const float UpperArmLength = 0.28f;
        public const float ForearmLength = 0.27f;

        public Transform Pelvis;
        public Transform Spine;
        public Transform Head;
        public Transform UpperArmL, UpperArmR;
        public Transform ForearmL, ForearmR;
        public Transform HandL, HandR;
        public Transform ThighL, ThighR;
        public Transform ShinL, ShinR;
        public Transform FootL, FootR;

        /// <summary>Pelvis height when standing still (local to the styled root).</summary>
        public float PelvisRestHeight = 0.95f;

        public Transform UpperArm(int side) => side < 0 ? UpperArmL : UpperArmR;
        public Transform Forearm(int side) => side < 0 ? ForearmL : ForearmR;
        public Transform Hand(int side) => side < 0 ? HandL : HandR;
        public Transform Thigh(int side) => side < 0 ? ThighL : ThighR;
        public Transform Shin(int side) => side < 0 ? ShinL : ShinR;
        public Transform Foot(int side) => side < 0 ? FootL : FootR;
    }
}
