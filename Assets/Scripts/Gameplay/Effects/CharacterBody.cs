using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
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
