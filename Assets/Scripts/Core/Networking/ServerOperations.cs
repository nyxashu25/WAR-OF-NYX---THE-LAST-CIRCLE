using System;
using System.Globalization;
using System.Text;

namespace WarOfNyx.Core.Networking
{
    /// <summary>
    /// When a dedicated server process should exit (spec Phase 12 "match shutdown and resource cleanup").
    /// Hosted containers run one match and exit (the orchestrator starts a fresh one); a long-running
    /// dev server can still idle out when nobody is connected.
    /// </summary>
    public sealed class ServerLifetimePolicy
    {
        private readonly int _maxMatches;
        private readonly float _idleShutdownSeconds;
        private float _emptySince = -1f;

        public ServerLifetimePolicy(int maxMatches, float idleShutdownSeconds)
        {
            _maxMatches = Math.Max(0, maxMatches);
            _idleShutdownSeconds = Math.Max(0f, idleShutdownSeconds);
        }

        public int MatchesFinished { get; private set; }
        public string ExitReason { get; private set; }
        public bool ShouldExit => ExitReason != null;

        /// <summary>A match finished and players were returned to the lobby.</summary>
        public void OnMatchFinished()
        {
            MatchesFinished++;
            if (_maxMatches > 0 && MatchesFinished >= _maxMatches)
                ExitReason ??= $"played {MatchesFinished} match(es) (-maxMatches {_maxMatches})";
        }

        /// <summary>Call regularly with the connected player count (server time in seconds).</summary>
        public void Update(int connectedPlayers, float now)
        {
            if (connectedPlayers > 0)
            {
                _emptySince = -1f;
                return;
            }
            if (_emptySince < 0f)
                _emptySince = now;
            if (_idleShutdownSeconds > 0f && now - _emptySince >= _idleShutdownSeconds)
                ExitReason ??= $"no players for {_idleShutdownSeconds:0} s (-idleShutdown)";
        }
    }

    /// <summary>What the health endpoint reports (no secrets, no player identities).</summary>
    public struct ServerHealth
    {
        public string Status;     // "starting", "ok", "shutting-down"
        public string Session;
        public string Phase;
        public int Players;
        public int MaxPlayers;
        public int MatchesFinished;
        public float UptimeSeconds;
        public int Protocol;
    }

    /// <summary>Small, dependency-free JSON writers for health checks and structured telemetry logs.</summary>
    public static class ServerJson
    {
        public static string Health(in ServerHealth h)
        {
            var sb = new StringBuilder(160);
            sb.Append('{');
            Str(sb, "status", h.Status ?? "unknown").Append(',');
            Str(sb, "session", h.Session ?? string.Empty).Append(',');
            Str(sb, "phase", h.Phase ?? string.Empty).Append(',');
            Num(sb, "players", h.Players).Append(',');
            Num(sb, "maxPlayers", h.MaxPlayers).Append(',');
            Num(sb, "matchesFinished", h.MatchesFinished).Append(',');
            Num(sb, "uptimeSeconds", (int)Math.Max(0f, h.UptimeSeconds)).Append(',');
            Num(sb, "protocol", h.Protocol);
            return sb.Append('}').ToString();
        }

        /// <summary>One telemetry line: {"event":"match_end","t":12.5,"k":"v",...}. Values are strings or numbers.</summary>
        public static string Event(string name, float serverTime, params (string key, object value)[] fields)
        {
            var sb = new StringBuilder(128);
            sb.Append('{');
            Str(sb, "event", name ?? "event").Append(',');
            sb.Append("\"t\":").Append(serverTime.ToString("0.###", CultureInfo.InvariantCulture));
            if (fields != null)
            {
                foreach (var (key, value) in fields)
                {
                    sb.Append(',');
                    if (value is int || value is long || value is float || value is double)
                        sb.Append('"').Append(Escape(key)).Append("\":").Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    else if (value is bool b)
                        sb.Append('"').Append(Escape(key)).Append("\":").Append(b ? "true" : "false");
                    else
                        Str(sb, key, value?.ToString() ?? string.Empty);
                }
            }
            return sb.Append('}').ToString();
        }

        private static StringBuilder Str(StringBuilder sb, string key, string value) =>
            sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');

        private static StringBuilder Num(StringBuilder sb, string key, int value) =>
            sb.Append('"').Append(Escape(key)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
