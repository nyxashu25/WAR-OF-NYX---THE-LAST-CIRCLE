using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Core.Events;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Movement;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.Mobile;

namespace WarOfNyx.Gameplay
{
    /// <summary>
    /// Runs the Practice Arena (spec 38.1 mode 3; Phase 2 movement test arena).
    /// Local simulation only: spawns the placeholder player, binds the third-person camera,
    /// shows touch controls on touch devices, and provides pause / return-to-menu.
    ///
    /// Desktop debug keys: F2 toggles touch controls (test them with the mouse), F3 toggles the stats overlay.
    /// </summary>
    public sealed class PracticeArenaController : MonoBehaviour
    {
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float killPlaneY = -30f;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private PlayerController _player;
        private ThirdPersonCamera _camera;
        private MobileControlsUI _mobile;
        private GameObject _pausePanel;
        private Text _stats;
        private float _smoothedFrameTime = 1f / 60f;
        private bool _paused;

        public PlayerController Player => _player;
        public bool IsPaused => _paused;

        private void Start()
        {
            AppServices.EnsureInitialized();
            EnterActiveState();

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            SpawnPlayer();
            if (_player == null)
                return;
            BindCamera();

            bool touch = Application.isMobilePlatform || Touchscreen.current != null;
            _mobile = MobileControlsUI.Create(_player.InputRouter);
            _mobile.SetVisible(touch);

            BuildHud();
            _subscriptions.Add(AppServices.Events.Subscribe<BackRequested>(_ => TogglePause()));
            _subscriptions.Add(AppServices.Events.Subscribe<AppPauseChanged>(e =>
            {
                if (e.IsPaused && !_paused) TogglePause(); // pause when the app goes to background
            }));

            UpdateCursor();
        }

        private void OnDestroy()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            _subscriptions.Clear();

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f2Key.wasPressedThisFrame)
                {
                    _mobile.SetVisible(!_mobile.Visible);
                    UpdateCursor();
                }
                if (keyboard.f3Key.wasPressedThisFrame)
                    _stats.gameObject.SetActive(!_stats.gameObject.activeSelf);
            }

            // Desktop: clicking the game view re-captures the mouse.
            var mouse = Mouse.current;
            if (!_paused && !_mobile.Visible && mouse != null && mouse.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
                UpdateCursor();

            if (_player != null && _player.transform.position.y < killPlaneY)
                RespawnPlayer();

            _smoothedFrameTime = Mathf.Lerp(_smoothedFrameTime, Time.unscaledDeltaTime, 0.05f);
            if (_stats.gameObject.activeSelf && _player != null)
                _stats.text = DescribeState(_player.Movement.State);
        }

        // ------------------------------------------------------------------ Flow

        private static void EnterActiveState()
        {
            var flow = AppServices.Flow;
            if (flow.Current == GameFlowState.MainMenu)
                flow.TryStartSession(GameMode.PracticeArena); // scene opened directly in the editor

            // Practice has no lobby or countdown; walk straight to the active state.
            flow.TryTransition(GameFlowState.SynchronizedSpawn);
            flow.TryTransition(GameFlowState.Countdown);
            flow.TryTransition(GameFlowState.ActiveMatch);
        }

        private void SpawnPlayer()
        {
            var position = spawnPoint != null ? spawnPoint.position : Vector3.up;
            float yaw = spawnPoint != null ? spawnPoint.eulerAngles.y : 0f;

            if (playerPrefab == null)
            {
                Debug.LogError("[WarOfNyx] PracticeArenaController has no player prefab. Rebuild the arena via War of Nyx > Setup.");
                enabled = false;
                return;
            }

            _player = Instantiate(playerPrefab);
            _player.name = "Player (local)";
            _player.Spawn(position, yaw);
        }

        private void RespawnPlayer()
        {
            var position = spawnPoint != null ? spawnPoint.position : Vector3.up;
            _player.Spawn(position, spawnPoint != null ? spawnPoint.eulerAngles.y : 0f);
        }

        private void BindCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            _camera = cam.GetComponent<ThirdPersonCamera>();
            if (_camera == null)
                _camera = cam.gameObject.AddComponent<ThirdPersonCamera>();
            _camera.Bind(_player.transform, _player.Movement, _player.InputRouter);
        }

        public void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            if (_player != null)
                _player.InputRouter.InputEnabled = !_paused;
            _pausePanel.SetActive(_paused);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            UpdateCursor();
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            AppServices.Flow.AbortToMainMenu();
            SceneManager.LoadScene(AppServices.Config.mainMenuScene);
        }

        private void UpdateCursor()
        {
            bool capture = !_paused && !(_mobile != null && _mobile.Visible) && !Application.isMobilePlatform;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        // ------------------------------------------------------------------ HUD

        private void BuildHud()
        {
            var canvas = UiFactory.CreateCanvas("PracticeHud", sortingOrder: 20);
            canvas.transform.SetParent(transform, false);
            var safe = UiFactory.CreateRect("SafeArea", canvas.transform);
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var title = UiFactory.CreateText(safe, "PRACTICE ARENA", 30, NyxPalette.ElectricCyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.02f, 0.90f), new Vector2(0.5f, 0.98f));

            _stats = UiFactory.CreateText(safe, string.Empty, 22, NyxPalette.TextPrimary, TextAnchor.UpperLeft);
            UiFactory.Anchor(_stats.rectTransform, new Vector2(0.02f, 0.68f), new Vector2(0.4f, 0.90f));

            var help = UiFactory.CreateText(safe,
                Application.isMobilePlatform
                    ? string.Empty
                    : "WASD move | Mouse look | Shift sprint | Space jump | C crouch | Ctrl slide (or C while sprinting) | RMB aim | Esc pause | F2 touch UI | F3 stats",
                20, NyxPalette.TextMuted, TextAnchor.UpperCenter);
            UiFactory.Anchor(help.rectTransform, new Vector2(0.2f, 0.94f), new Vector2(0.8f, 0.995f));

            // Pause button (needed on touch devices without a back key in view).
            var pauseButton = UiFactory.CreateButton(safe, "II", TogglePause, 90f, 34);
            var pauseRect = (RectTransform)pauseButton.Button.transform;
            pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(1f, 1f);
            pauseRect.pivot = new Vector2(1f, 1f);
            pauseRect.sizeDelta = new Vector2(100f, 90f);
            pauseRect.anchoredPosition = new Vector2(-20f, -20f);

            var panel = UiFactory.CreatePanel("PausePanel", safe, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.85f), raycastTarget: true);
            UiFactory.Stretch(panel.rectTransform);
            _pausePanel = panel.gameObject;

            var column = UiFactory.CreateRect("Column", panel.rectTransform);
            UiFactory.Anchor(column, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.75f));
            UiFactory.AddVerticalLayout(column.gameObject, 18f, null, TextAnchor.MiddleCenter);
            UiFactory.CreateText(column, "PAUSED", 64, NyxPalette.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold, 90f);
            UiFactory.CreateButton(column, "RESUME", TogglePause, 100f, 36);
            UiFactory.CreateButton(column, "MAIN MENU", ReturnToMenu, 100f, 36);
            _pausePanel.SetActive(false);
        }

        private string DescribeState(in MovementState s)
        {
            float fps = 1f / Mathf.Max(0.0001f, _smoothedFrameTime);
            return $"STATE  {s.Mode}{(s.IsSprinting ? " (SPRINT)" : string.Empty)}\n" +
                   $"SPEED  {s.HorizontalSpeed:0.00} m/s   VERT {s.VelocityY:0.00}\n" +
                   $"POS    {_player.transform.position.x:0.0}, {_player.transform.position.y:0.0}, {_player.transform.position.z:0.0}\n" +
                   $"AIM    yaw {_player.InputRouter.Yaw:0}  pitch {_player.InputRouter.Pitch:0}\n" +
                   $"FPS    {fps:0}   GFX {AppServices.Graphics.CurrentTier}";
        }
    }
}
