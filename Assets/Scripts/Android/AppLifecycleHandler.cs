using UnityEngine;
using UnityEngine.InputSystem;
using WarOfNyx.Core.Events;

namespace WarOfNyx.Platform
{
    /// <summary>
    /// Forwards Android lifecycle signals to the event bus so systems can react without
    /// each implementing OnApplicationPause (spec 25: pause/resume, audio focus, back button).
    /// Purely local; online matches keep running on the server while the app is backgrounded,
    /// and the reconnect logic (Phase 3+) decides what happens on resume.
    /// </summary>
    public sealed class AppLifecycleHandler : MonoBehaviour
    {
        private GameEventBus _bus;

        public bool IsPaused { get; private set; }

        public void Initialize(GameEventBus bus)
        {
            _bus = bus;
            // Menus may let the screen sleep; matches switch to NeverSleep when they start.
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Update()
        {
            // The Input System maps the Android back button to Keyboard.escapeKey.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                _bus?.Publish(new BackRequested());
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            IsPaused = pauseStatus;
            _bus?.Publish(new AppPauseChanged(pauseStatus));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _bus?.Publish(new AppFocusChanged(hasFocus));
        }
    }
}
