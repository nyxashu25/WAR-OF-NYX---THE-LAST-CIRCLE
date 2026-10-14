using UnityEngine;
using UnityEngine.InputSystem;

namespace WarOfNyx.Controls
{
    /// <summary>
    /// The single gameplay action abstraction shared by every input scheme (spec 18).
    ///
    /// Keyboard/mouse bindings follow the spec's desktop list. Touch controls (Phase 2)
    /// use the Input System's On-Screen Stick / On-Screen Button components, which emit
    /// virtual Gamepad controls — so the gamepad bindings below double as the mobile bindings.
    /// Gameplay code only ever reads actions by name; it never checks which device produced them.
    ///
    /// Actions are built in code (not a .inputactions asset) so they are reviewable in diffs
    /// and covered by unit tests. Rebinding can later be persisted with SaveBindingOverridesAsJson.
    /// </summary>
    public static class GameInputActions
    {
        public const string PlayerMap = "Player";

        public const string Move = "Move";
        public const string Look = "Look";
        public const string Fire = "Fire";
        public const string Aim = "Aim";
        public const string Sprint = "Sprint";
        /// <summary>
        /// Hold to walk (keyboard only, so not in <see cref="AllPlayerActions"/>, which need a touch-compatible
        /// gamepad binding): sticks and the touch joystick walk by pushing gently.
        /// </summary>
        public const string Walk = "Walk";
        /// <summary>Hold to look around without turning the character (PUBG-style "eye"). Touch: the EYE button.</summary>
        public const string FreeLook = "FreeLook";
        public const string Jump = "Jump";
        public const string Crouch = "Crouch";
        public const string Slide = "Slide";
        public const string Reload = "Reload";
        public const string Interact = "Interact";
        public const string Melee = "Melee";
        public const string WeaponSlot1 = "WeaponSlot1";
        public const string WeaponSlot2 = "WeaponSlot2";
        public const string WeaponSlot3 = "WeaponSlot3";
        public const string NextWeapon = "NextWeapon";
        public const string Medkit = "Medkit";
        public const string Pause = "Pause";

        public static readonly string[] AllPlayerActions =
        {
            Move, Look, Fire, Aim, Sprint, FreeLook, Jump, Crouch, Slide, Reload, Interact, Melee,
            WeaponSlot1, WeaponSlot2, WeaponSlot3, NextWeapon, Medkit, Pause,
        };

        /// <summary>Creates a fresh, disabled action asset. Caller owns it (Destroy when done).</summary>
        public static InputActionAsset CreateDefaultAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "WarOfNyxControls";

            var player = asset.AddActionMap(PlayerMap);

            var move = player.AddAction(Move, InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            var look = player.AddAction(Look, InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            AddButton(player, Fire, "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            AddButton(player, Aim, "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            AddButton(player, Sprint, "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            player.AddAction(Walk, InputActionType.Button).AddBinding("<Keyboard>/x");
            AddButton(player, FreeLook, "<Keyboard>/leftAlt", "<Gamepad>/select");
            AddButton(player, Jump, "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(player, Crouch, "<Keyboard>/c", "<Gamepad>/buttonEast");
            AddButton(player, Slide, "<Keyboard>/leftCtrl", "<Gamepad>/leftShoulder");
            AddButton(player, Reload, "<Keyboard>/r", "<Gamepad>/buttonWest");
            AddButton(player, Interact, "<Keyboard>/e", "<Gamepad>/buttonNorth");
            AddButton(player, Melee, "<Keyboard>/f", "<Gamepad>/rightShoulder");
            AddButton(player, WeaponSlot1, "<Keyboard>/1", "<Gamepad>/dpad/left");
            AddButton(player, WeaponSlot2, "<Keyboard>/2", "<Gamepad>/dpad/up");
            AddButton(player, WeaponSlot3, "<Keyboard>/3", "<Gamepad>/dpad/right");
            AddButton(player, NextWeapon, "<Mouse>/middleButton", "<Gamepad>/rightStickPress");
            AddButton(player, Medkit, "<Keyboard>/h", "<Gamepad>/dpad/down");
            AddButton(player, Pause, "<Keyboard>/escape", "<Gamepad>/start");

            return asset;
        }

        private static void AddButton(InputActionMap map, string name, string keyboardOrMouse, string gamepad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardOrMouse);
            action.AddBinding(gamepad);
        }
    }
}
