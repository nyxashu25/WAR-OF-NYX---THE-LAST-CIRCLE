using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using WarOfNyx.Core.Events;

namespace WarOfNyx.App
{
    /// <summary>
    /// Detects whether online play is possible (spec 38.2). Client-side and advisory only:
    /// it decides whether the ONLINE button is enabled; it never gates offline play.
    ///
    /// OS reachability alone can report "connected" on captive Wi-Fi, so when a probe URL is
    /// configured a lightweight HEAD request confirms real internet access.
    /// </summary>
    public sealed class ConnectivityMonitor : MonoBehaviour
    {
        private GameEventBus _bus;
        private float _interval = 5f;
        private string _probeUrl;
        private int _probeTimeout = 4;
        private bool _paused;
        private Coroutine _loop;

        public ConnectivityStatus Status { get; private set; } = ConnectivityStatus.Unknown;

        public void Initialize(GameEventBus bus, GameConfig config)
        {
            _bus = bus;
            _interval = Mathf.Max(1f, config.connectivityCheckIntervalSeconds);
            _probeUrl = config.connectivityProbeUrl;
            _probeTimeout = Mathf.Max(1, config.connectivityProbeTimeoutSeconds);
            _bus.Subscribe<AppPauseChanged>(e => _paused = e.IsPaused);
            RestartLoop();
        }

        /// <summary>Forces an immediate re-check (e.g. after returning from background).</summary>
        public void CheckNow() => RestartLoop();

        private void RestartLoop()
        {
            if (_loop != null)
                StopCoroutine(_loop);
            _loop = StartCoroutine(CheckLoop());
        }

        private IEnumerator CheckLoop()
        {
            var wait = new WaitForSecondsRealtime(_interval);
            while (true)
            {
                if (!_paused)
                    yield return CheckOnce();
                yield return wait;
            }
        }

        private IEnumerator CheckOnce()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                SetStatus(ConnectivityStatus.Offline);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(_probeUrl))
            {
                SetStatus(ConnectivityStatus.Online);
                yield break;
            }

            using (var request = UnityWebRequest.Head(_probeUrl))
            {
                request.timeout = _probeTimeout;
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success &&
                          request.responseCode >= 200 && request.responseCode < 300;
                SetStatus(ok ? ConnectivityStatus.Online : ConnectivityStatus.Offline);
            }
        }

        private void SetStatus(ConnectivityStatus next)
        {
            if (next == Status)
                return;

            var previous = Status;
            Status = next;
            _bus?.Publish(new ConnectivityChanged(previous, next));
        }
    }
}
