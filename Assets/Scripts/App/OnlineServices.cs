using System;
using System.Threading.Tasks;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.App
{
    public enum OnlineSessionState
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        ShuttingDown = 3,
        Failed = 4,
    }

    public enum OnlineRole
    {
        None = 0,
        /// <summary>Headless dedicated server (authoritative).</summary>
        DedicatedServer = 1,
        /// <summary>A player's device that is also the authority. Private test rooms only.</summary>
        Host = 2,
        /// <summary>A player connected to a server or host.</summary>
        Client = 3,
    }

    /// <summary>
    /// What the game (menu, HUD, bootstrap) needs from online multiplayer. Implemented by the
    /// Photon Fusion module (assembly WarOfNyx.Networking), which only compiles once the Fusion
    /// SDK is imported. Without it <see cref="OnlineServices.Session"/> stays null and the
    /// game reports that online play is unavailable instead of failing to compile.
    /// </summary>
    public interface IOnlineSessionService
    {
        OnlineSessionState State { get; }
        OnlineRole Role { get; }
        string SessionName { get; }
        string LastError { get; }
        int PlayerCount { get; }
        int MaxPlayers { get; }

        /// <summary>Round-trip time to the server in milliseconds (clients only; 0 on the authority).</summary>
        int RoundTripMs { get; }

        event Action<OnlineSessionState> StateChanged;

        /// <summary>Private test room hosted on this device (spec 20: allowed for testing only).</summary>
        Task<bool> HostPrivateRoomAsync(string roomCode, int maxPlayers);

        /// <summary>Joins an existing session (dedicated server or private room) by name/code.</summary>
        Task<bool> JoinAsync(string sessionName);

        /// <summary>Starts this process as the headless authoritative server.</summary>
        Task<bool> StartDedicatedServerAsync(ServerLaunchOptions options);

        Task LeaveAsync();
    }

    public static class OnlineServices
    {
        /// <summary>Registered by the networking module at startup; null when Fusion is not installed.</summary>
        public static IOnlineSessionService Session { get; set; }

        public static bool IsAvailable => Session != null;

        public const string UnavailableMessage =
            "Online play needs the Photon Fusion 2 SDK and an App ID. See DEPENDENCIES.md (Phase 3 setup).";
    }
}
