using System;
using System.Globalization;

namespace WarOfNyx.Core.Networking
{
    /// <summary>
    /// Command-line options for the headless dedicated server (spec 3 "Hosting Strategy").
    ///
    ///   WarOfNyxServer -batchmode -nographics -server -session WON-DEV -maxPlayers 12 -port 27015 -region eu
    ///
    /// Unknown arguments are ignored (Unity adds its own). Invalid values fall back to defaults
    /// and are reported in <see cref="Warnings"/> so the server log shows what happened.
    /// </summary>
    public sealed class ServerLaunchOptions
    {
        public const int DefaultPort = 27015;
        public const int DefaultMaxPlayers = 12;
        public const int MinPlayers = 2;
        public const int MaxPlayersLimit = 60;
        public const string DefaultSession = "WON-DEV";

        public bool IsServer { get; private set; }
        public string SessionName { get; private set; } = DefaultSession;
        public int MaxPlayers { get; private set; } = DefaultMaxPlayers;
        /// <summary>UDP port to bind. 0 = let the OS choose.</summary>
        public int Port { get; private set; } = DefaultPort;
        /// <summary>Photon region code (e.g. "eu", "us", "asia"). Empty = best region.</summary>
        public string Region { get; private set; } = string.Empty;
        /// <summary>Players needed to start (-minPlayers). -1 = use the configured MatchRules (2 = dev two-player test mode).</summary>
        public int MinPlayersToStart { get; private set; } = -1;
        /// <summary>Seconds to wait for more players once the minimum is reached (-lobbyFill). -1 = configured value.</summary>
        public float LobbyFillSeconds { get; private set; } = -1f;
        public string Warnings { get; private set; } = string.Empty;

        public static ServerLaunchOptions Parse(string[] args)
        {
            var options = new ServerLaunchOptions();
            if (args == null)
                return options;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg) || arg[0] != '-')
                    continue;

                string key = arg.TrimStart('-').ToLowerInvariant();
                string value = i + 1 < args.Length && !IsFlag(args[i + 1]) ? args[i + 1] : null;

                switch (key)
                {
                    case "server":
                        options.IsServer = true;
                        break;
                    case "session":
                        if (value != null && SessionCodes.TrySanitize(value, out string session))
                            options.SessionName = session;
                        else
                            options.Warn($"invalid -session '{value}', using {DefaultSession}");
                        break;
                    case "maxplayers":
                        if (TryInt(value, out int max) && max >= MinPlayers && max <= MaxPlayersLimit)
                            options.MaxPlayers = max;
                        else
                            options.Warn($"invalid -maxPlayers '{value}' (allowed {MinPlayers}-{MaxPlayersLimit}), using {DefaultMaxPlayers}");
                        break;
                    case "minplayers":
                        if (TryInt(value, out int min) && min >= 1 && min <= MaxPlayersLimit)
                            options.MinPlayersToStart = min;
                        else
                            options.Warn($"invalid -minPlayers '{value}', using configured value");
                        break;
                    case "lobbyfill":
                        if (TryInt(value, out int fill) && fill >= 0 && fill <= 600)
                            options.LobbyFillSeconds = fill;
                        else
                            options.Warn($"invalid -lobbyFill '{value}', using configured value");
                        break;
                    case "port":
                        if (TryInt(value, out int port) && port >= 0 && port <= 65535)
                            options.Port = port;
                        else
                            options.Warn($"invalid -port '{value}', using {DefaultPort}");
                        break;
                    case "region":
                        if (value != null && value.Length <= 8)
                            options.Region = value.Trim().ToLowerInvariant();
                        else
                            options.Warn($"invalid -region '{value}', using best region");
                        break;
                }
            }

            return options;
        }

        public override string ToString() =>
            $"session={SessionName} maxPlayers={MaxPlayers} minPlayers={(MinPlayersToStart < 0 ? "config" : MinPlayersToStart.ToString())} " +
            $"lobbyFill={(LobbyFillSeconds < 0f ? "config" : LobbyFillSeconds + "s")} port={Port} region={(Region.Length == 0 ? "best" : Region)}";

        private static bool IsFlag(string s) => !string.IsNullOrEmpty(s) && s[0] == '-' && !(s.Length > 1 && char.IsDigit(s[1]));

        private static bool TryInt(string s, out int value) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private void Warn(string message) => Warnings = Warnings.Length == 0 ? message : Warnings + "; " + message;
    }
}
