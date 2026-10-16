using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>Animator parameter and clip names shared by the controller builder (editor) and the runtime driver.</summary>
    public static class CharacterAnimatorParams
    {
        public const string MoveX = "MoveX";             // local sideways velocity / run speed (-1.6..1.6)
        public const string MoveY = "MoveY";             // local forward velocity / run speed
        public const string Speed = "Speed";             // m/s
        public const string LocomotionRate = "LocoRate"; // playback multiplier so feet match the ground speed
        public const string Sprint = "Sprint";
        public const string Crouch = "Crouch";
        public const string Grounded = "Grounded";
        public const string Slide = "Slide";
        public const string Seated = "Seated";
        public const string Dead = "Dead";
        public const string Victory = "Victory";
        public const string Hit = "Hit";
        public const string Melee = "Melee";
        public const string MeleePattern = "MeleePattern";
        public const string MeleeRate = "MeleeRate";     // playback multiplier so the strike lands on the server's windup
        public const string IdleVariant = "IdleVariant";

        /// <summary>Clip names every character provides (the base controller's slots; per-character overrides).</summary>
        public static readonly string[] Clips =
        {
            "Idle", "IdleVariant", "WalkF", "WalkB", "WalkL", "WalkR", "RunF", "RunB", "RunL", "RunR", "Sprint",
            "CrouchIdle", "CrouchWalk", "Air", "Slide", "Seated", "Hit", "Death", "Victory",
            "Melee_HorizontalSlash", "Melee_BackhandSlash", "Melee_OverheadStrike", "Melee_Thrust", "Melee_RisingSlash", "Melee_SpinSlash", "Melee_QuickStab",
        };

        /// <summary>Where the blade connects within each melee clip (fraction of the clip), for timing.</summary>
        public const float MeleeStrikeFraction = 0.4f;
        public const float MeleeClipSeconds = 0.9f;

        /// <summary>Nominal ground speeds of the authored locomotion clips (m/s).</summary>
        public const float WalkSpeed = 1.8f, RunSpeed = 4f, SprintSpeed = 6.5f, CrouchSpeed = 2.5f;
    }
}
