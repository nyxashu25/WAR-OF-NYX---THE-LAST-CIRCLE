using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.App
{
    /// <summary>
    /// Operations side of a dedicated server process (Phase 12), independent of the networking SDK:
    ///  * HTTP health endpoint (GET /health → JSON, 200 when serving, 503 while starting or shutting down)
    ///    for Docker / Edgegap health checks. Minimal data: no player names, no secrets.
    ///  * Structured telemetry log lines: <c>[WarOfNyx][Telemetry] {json}</c> (match start/end, players, heartbeat).
    ///  * Process lifetime: exit after -maxMatches finished matches or -idleShutdown seconds without players,
    ///    so hosted containers free their resources.
    /// </summary>
    public sealed class ServerRuntime : MonoBehaviour
    {
        private const float PollSeconds = 0.25f;
        private const float HeartbeatSeconds = 60f;

        private ServerLaunchOptions _options;
        private ServerLifetimePolicy _lifetime;
        private TcpListener _listener;
        private Thread _thread;
        private volatile string _healthJson = "{\"status\":\"starting\"}";
        private volatile bool _healthy;
        private volatile bool _running;
        private float _nextPoll, _nextHeartbeat;
        private MatchPhase _lastPhase = MatchPhase.WaitingForPlayers;
        private int _lastPlayers = -1;
        private bool _exiting;

        public static ServerRuntime Start(ServerLaunchOptions options)
        {
            var go = new GameObject("[WarOfNyx Server Runtime]");
            DontDestroyOnLoad(go);
            var runtime = go.AddComponent<ServerRuntime>();
            runtime._options = options;
            runtime._lifetime = new ServerLifetimePolicy(options.MaxMatches, options.IdleShutdownSeconds);
            if (options.HealthPort > 0)
                runtime.StartHealthEndpoint(options.HealthPort);
            Telemetry("server_start", ("session", options.SessionName), ("maxPlayers", options.MaxPlayers),
                ("maxMatches", options.MaxMatches), ("idleShutdown", options.IdleShutdownSeconds), ("protocol", NetworkProtocol.Version));
            return runtime;
        }

        public static void Telemetry(string name, params (string key, object value)[] fields) =>
            Debug.Log("[WarOfNyx][Telemetry] " + ServerJson.Event(name, Time.realtimeSinceStartup, fields));

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextPoll || _exiting)
                return;
            _nextPoll = now + PollSeconds;

            var session = OnlineServices.Session;
            var match = session?.Match;
            int players = session != null ? session.PlayerCount : 0;
            var phase = match != null ? match.Phase : MatchPhase.WaitingForPlayers;
            bool serving = session != null && session.State == OnlineSessionState.Running;

            if (phase != _lastPhase)
            {
                if (phase == MatchPhase.Active)
                    Telemetry("match_start", ("players", match != null ? match.Participants : players));
                if (phase == MatchPhase.Ended)
                    Telemetry("match_end", ("participants", match != null ? match.Participants : 0));
                if (_lastPhase == MatchPhase.Ended && phase != MatchPhase.Ended)
                    _lifetime.OnMatchFinished(); // results shown, players back in the lobby
                _lastPhase = phase;
            }
            if (players != _lastPlayers)
            {
                Telemetry("players", ("count", players), ("phase", phase.ToString()));
                _lastPlayers = players;
            }
            if (now >= _nextHeartbeat)
            {
                _nextHeartbeat = now + HeartbeatSeconds;
                Telemetry("heartbeat", ("players", players), ("phase", phase.ToString()), ("fps", Mathf.RoundToInt(1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f))));
            }

            _lifetime.Update(players, now);
            _healthy = serving && !_lifetime.ShouldExit;
            _healthJson = ServerJson.Health(new ServerHealth
            {
                Status = _lifetime.ShouldExit ? "shutting-down" : serving ? "ok" : "starting",
                Session = session != null ? session.SessionName : _options.SessionName,
                Phase = phase.ToString(),
                Players = players,
                MaxPlayers = session != null ? session.MaxPlayers : _options.MaxPlayers,
                MatchesFinished = _lifetime.MatchesFinished,
                UptimeSeconds = now,
                Protocol = NetworkProtocol.Version,
            });

            if (_lifetime.ShouldExit)
                Exit(session);
        }

        private async void Exit(IOnlineSessionService session)
        {
            _exiting = true;
            Telemetry("server_exit", ("reason", _lifetime.ExitReason), ("matches", _lifetime.MatchesFinished));
            Debug.Log($"[WarOfNyx][Server] Shutting down: {_lifetime.ExitReason}");
            try
            {
                if (session != null && session.State != OnlineSessionState.Idle)
                    await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WarOfNyx][Server] Clean shutdown failed: {e.Message}");
            }
            Application.Quit(0);
        }

        // ------------------------------------------------------------------ health endpoint

        private void StartHealthEndpoint(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _running = true;
                _thread = new Thread(ServeLoop) { IsBackground = true, Name = "WarOfNyx health" };
                _thread.Start();
                Debug.Log($"[WarOfNyx][Server] Health endpoint on http://0.0.0.0:{port}/health");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WarOfNyx][Server] Health endpoint failed on port {port}: {e.Message}");
            }
        }

        private void ServeLoop()
        {
            while (_running)
            {
                try
                {
                    using (var client = _listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 2000;
                        client.SendTimeout = 2000;
                        var stream = client.GetStream();
                        var buffer = new byte[1024];
                        int read = stream.Read(buffer, 0, buffer.Length);
                        string request = read > 0 ? Encoding.ASCII.GetString(buffer, 0, read) : string.Empty;
                        bool health = request.StartsWith("GET /health") || request.StartsWith("GET / ");
                        string body = health ? _healthJson : "{\"error\":\"not found\"}";
                        string status = !health ? "404 Not Found" : _healthy ? "200 OK" : "503 Service Unavailable";
                        var bytes = Encoding.UTF8.GetBytes(body);
                        string header = $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                        var headerBytes = Encoding.ASCII.GetBytes(header);
                        stream.Write(headerBytes, 0, headerBytes.Length);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception)
                {
                    if (!_running)
                        return; // listener stopped
                }
            }
        }

        private void OnDestroy()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // already closed
            }
        }
    }
}
