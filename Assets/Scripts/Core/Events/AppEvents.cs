namespace WarOfNyx.Core.Events
{
    /// <summary>Internet reachability as seen by the client (master spec 38.2).</summary>
    public enum ConnectivityStatus
    {
        Unknown = 0,
        Offline = 1,
        Online = 2,
    }

    public readonly struct ConnectivityChanged
    {
        public readonly ConnectivityStatus Previous;
        public readonly ConnectivityStatus Current;

        public ConnectivityChanged(ConnectivityStatus previous, ConnectivityStatus current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>App moved to background (true) or returned to foreground (false). Android lifecycle.</summary>
    public readonly struct AppPauseChanged
    {
        public readonly bool IsPaused;

        public AppPauseChanged(bool isPaused) => IsPaused = isPaused;
    }

    public readonly struct AppFocusChanged
    {
        public readonly bool HasFocus;

        public AppFocusChanged(bool hasFocus) => HasFocus = hasFocus;
    }

    /// <summary>The Android back button / Escape key was pressed.</summary>
    public readonly struct BackRequested
    {
    }
}
