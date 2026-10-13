using UnityEngine;
using UnityEngine.InputSystem;
using WarOfNyx.App;
using WarOfNyx.Controls;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Characters
{
    /// <summary>
    /// Turns every human input source into one <see cref="MovementCommand"/> per simulation step
    /// (spec 18: "Both input schemes should use the same gameplay action abstraction").
    ///
    /// Sources:
    ///  * Input System actions (keyboard/mouse, gamepad, and the on-screen touch controls, which
    ///    emit virtual gamepad controls),
    ///  * swipe-to-look deltas from <c>TouchLookArea</c>,
    ///  * the mobile sprint toggle.
    ///
    /// Client-only. It owns the aim angles (yaw/pitch) because aiming is the player's intent;
    /// the authority receives them inside the command.
    /// </summary>
    [DefaultExecutionOrder(-50)] // update aim before PlayerController consumes the command
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float mouseDegreesPerPixel = 0.12f;
        [SerializeField] private float gamepadDegreesPerSecond = 160f;
        [Tooltip("Degrees turned when a finger swipes the full screen height.")]
        [SerializeField] private float touchDegreesPerScreenHeight = 180f;
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 75f;

        private InputActionAsset _asset;
        private InputAction _move, _look, _sprint, _aim, _jump, _crouch, _slide;

        private bool _jumpLatched, _crouchLatched, _slideLatched;
        private bool _sprintToggled;
        private Vector2 _pendingTouchLook;

        /// <summary>Aim yaw in degrees (0 = +Z). Drives the camera and the character facing.</summary>
        public float Yaw { get; private set; }

        /// <summary>Aim pitch in degrees; positive looks down (Unity Euler X).</summary>
        public float Pitch { get; private set; }

        /// <summary>Disable while paused or in menus. Look and edges are ignored, commands become neutral.</summary>
        public bool InputEnabled { get; set; } = true;

        public bool AimHeld => InputEnabled && _aim != null && _aim.IsPressed();
        public bool SprintToggled => _sprintToggled;

        private void Awake()
        {
            _asset = GameInputActions.CreateDefaultAsset();
            var map = _asset.FindActionMap(GameInputActions.PlayerMap, throwIfNotFound: true);
            _move = map.FindAction(GameInputActions.Move, true);
            _look = map.FindAction(GameInputActions.Look, true);
            _sprint = map.FindAction(GameInputActions.Sprint, true);
            _aim = map.FindAction(GameInputActions.Aim, true);
            _jump = map.FindAction(GameInputActions.Jump, true);
            _crouch = map.FindAction(GameInputActions.Crouch, true);
            _slide = map.FindAction(GameInputActions.Slide, true);

            // Latch button edges so a press between two simulation steps is never lost.
            _jump.performed += _ => _jumpLatched = InputEnabled;
            _crouch.performed += _ => _crouchLatched = InputEnabled;
            _slide.performed += _ => _slideLatched = InputEnabled;

            Yaw = transform.eulerAngles.y;
        }

        private void OnEnable() => _asset?.Enable();

        private void OnDisable() => _asset?.Disable();

        private void OnDestroy()
        {
            if (_asset != null)
                Destroy(_asset);
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                _pendingTouchLook = Vector2.zero;
                return;
            }

            float sensitivity = AppServices.IsInitialized ? AppServices.UserSettings.Data.aimSensitivity : 1f;
            Vector2 degrees = Vector2.zero;

            // Mouse delta is pixels this frame; stick is a rate. Tell them apart by the active device.
            var lookValue = _look.ReadValue<Vector2>();
            var device = _look.activeControl?.device;
            if (device is Pointer)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    degrees += lookValue * mouseDegreesPerPixel;
            }
            else if (lookValue.sqrMagnitude > 0f)
            {
                degrees += lookValue * (gamepadDegreesPerSecond * Time.unscaledDeltaTime);
            }

            if (_pendingTouchLook != Vector2.zero)
            {
                float screenHeight = Mathf.Max(1f, Screen.height);
                degrees += _pendingTouchLook * (touchDegreesPerScreenHeight / screenHeight);
                _pendingTouchLook = Vector2.zero;
            }

            degrees *= sensitivity;
            Yaw = Mathf.Repeat(Yaw + degrees.x, 360f);
            Pitch = Mathf.Clamp(Pitch - degrees.y, minPitch, maxPitch);
        }

        /// <summary>Swipe-to-look input from the touch look area, in screen pixels.</summary>
        public void AddTouchLook(Vector2 screenDelta)
        {
            if (InputEnabled)
                _pendingTouchLook += screenDelta;
        }

        /// <summary>Mobile sprint button: toggles auto-sprint. Cleared when the player stops, aims or crouches.</summary>
        public void ToggleSprint()
        {
            if (InputEnabled)
                _sprintToggled = !_sprintToggled;
        }

        /// <summary>Sets the aim angles directly (spawn facing, respawn).</summary>
        public void SetAim(float yaw, float pitch)
        {
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        /// <summary>Builds the command for one simulation step and clears the button edges.</summary>
        public MovementCommand ConsumeCommand()
        {
            if (!InputEnabled)
            {
                _jumpLatched = _crouchLatched = _slideLatched = false;
                _sprintToggled = false;
                return new MovementCommand { Yaw = Yaw, Pitch = Pitch };
            }

            var move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            bool aim = _aim.IsPressed();

            if (_sprintToggled && (move.sqrMagnitude < 0.01f || aim || _crouchLatched || _slideLatched))
                _sprintToggled = false;

            var command = new MovementCommand
            {
                MoveX = move.x,
                MoveY = move.y,
                Yaw = Yaw,
                Pitch = Pitch,
                SprintHeld = _sprint.IsPressed() || _sprintToggled,
                AimHeld = aim,
                JumpPressed = _jumpLatched,
                CrouchPressed = _crouchLatched,
                SlidePressed = _slideLatched,
            };

            _jumpLatched = _crouchLatched = _slideLatched = false;
            return command;
        }
    }
}
