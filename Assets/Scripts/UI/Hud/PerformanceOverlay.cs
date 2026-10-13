using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.UI;
using WarOfNyx.App;
using WarOfNyx.UI.Common;

namespace WarOfNyx.UI.Hud
{
    /// <summary>
    /// Phase 13 profiling aid on any device: FPS, average and worst frame time, managed and total memory,
    /// GC count and the graphics tier, in a corner overlay (F4 or Settings → SHOW FPS). Optionally records
    /// one CSV line per second to persistentDataPath/perf/ for device test sessions (Settings → RECORD
    /// PERFORMANCE). Lives for the whole app; costs nothing visible when hidden.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        private Text _text;
        private GameObject _panel;
        private float _windowStart;
        private int _frames;
        private float _worst;
        private float _sum;
        private int _lastGcCount;
        private StreamWriter _csv;

        public static PerformanceOverlay Instance { get; private set; }

        public static void EnsureCreated()
        {
            if (Instance != null || Application.isBatchMode)
                return;
            var go = new GameObject("[WarOfNyx Performance]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PerformanceOverlay>();
            Instance.Build();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("PerformanceCanvas", sortingOrder: 100);
            canvas.transform.SetParent(transform, false);
            var panel = UiFactory.CreatePanel("Stats", canvas.transform, NyxPalette.WithAlpha(NyxPalette.DeepBlack, 0.6f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(430f, 120f);
            rect.anchoredPosition = new Vector2(10f, 10f);
            _text = UiFactory.CreateText(rect, string.Empty, 18, NyxPalette.ElectricCyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UiFactory.Stretch(_text.rectTransform, 8f, 6f, 8f, 6f);
            _panel = panel.gameObject;
            _windowStart = Time.unscaledTime;
            _lastGcCount = GC.CollectionCount(0);
        }

        private static bool ShowSetting => AppServices.IsInitialized && AppServices.UserSettings.Data.showPerformance;
        private static bool RecordSetting => AppServices.IsInitialized && AppServices.UserSettings.Data.recordPerformance;
        private bool _forced;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
                _forced = !_forced;
            bool visible = _forced || ShowSetting;
            if (_panel.activeSelf != visible)
                _panel.SetActive(visible);

            float dt = Time.unscaledDeltaTime;
            _frames++;
            _sum += dt;
            _worst = Mathf.Max(_worst, dt);
            if (Time.unscaledTime - _windowStart < 1f)
                return;

            float fps = _frames / Mathf.Max(0.001f, _sum);
            float avgMs = _sum / Mathf.Max(1, _frames) * 1000f;
            float worstMs = _worst * 1000f;
            long managed = GC.GetTotalMemory(false) / (1024 * 1024);
            long total = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            int gc = GC.CollectionCount(0);
            int gcDelta = gc - _lastGcCount;
            string tier = AppServices.IsInitialized ? AppServices.Graphics.CurrentTier.ToString() : "-";

            if (visible)
                _text.text = $"{fps:0} FPS   avg {avgMs:0.0} ms   worst {worstMs:0.0} ms\nmemory {total} MB (managed {managed} MB)   GC/s {gcDelta}\n" +
                             $"tier {tier}   {Screen.width}x{Screen.height}   target {Application.targetFrameRate}";
            Record(fps, avgMs, worstMs, total, managed, gcDelta, tier);

            _frames = 0;
            _sum = 0f;
            _worst = 0f;
            _windowStart = Time.unscaledTime;
            _lastGcCount = gc;
        }

        private void Record(float fps, float avgMs, float worstMs, long total, long managed, int gc, string tier)
        {
            if (!RecordSetting)
            {
                CloseCsv();
                return;
            }
            try
            {
                if (_csv == null)
                {
                    string dir = Path.Combine(Application.persistentDataPath, "perf");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, $"perf-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                    _csv = new StreamWriter(path, false, Encoding.UTF8);
                    _csv.WriteLine($"# {SystemInfo.deviceModel}; {SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}; {SystemInfo.systemMemorySize} MB; {Application.version}");
                    _csv.WriteLine("time_s,scene,fps,avg_ms,worst_ms,total_mb,managed_mb,gc_per_s,tier");
                    Debug.Log("[WarOfNyx] Recording performance to " + path);
                }
                _csv.WriteLine(FormattableString.Invariant(
                    $"{Time.realtimeSinceStartup:0.0},{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name},{fps:0.0},{avgMs:0.00},{worstMs:0.00},{total},{managed},{gc},{tier}"));
                _csv.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WarOfNyx] Performance recording stopped: " + e.Message);
                CloseCsv();
            }
        }

        private void CloseCsv()
        {
            if (_csv == null)
                return;
            _csv.Dispose();
            _csv = null;
        }

        private void OnDestroy() => CloseCsv();
    }
}
