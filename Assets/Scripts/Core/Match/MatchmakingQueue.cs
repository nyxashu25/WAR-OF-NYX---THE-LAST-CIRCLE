using System;

namespace WarOfNyx.Core.Match
{
    public enum MatchmakingStatus
    {
        Idle = 0,
        Searching = 1,
        /// <summary>A join attempt is in flight.</summary>
        Attempting = 2,
        Joined = 3,
        Cancelled = 4,
    }

    /// <summary>
    /// Client-side Quick Match queue (spec 20 + 38.2). Repeatedly tries to join an open public
    /// session with a short back-off. After <see cref="OfflineOfferSeconds"/> it OFFERS offline
    /// play with bots, but keeps searching until the player chooses — it never silently swaps
    /// real opponents for bots.
    /// </summary>
    public sealed class MatchmakingQueue
    {
        public static readonly float[] RetryDelays = { 2f, 3f, 5f };

        private float _startedAt;
        private float _nextAttemptAt;
        private int _failures;

        public MatchmakingQueue(float offlineOfferSeconds)
        {
            OfflineOfferSeconds = Math.Max(0f, offlineOfferSeconds);
        }

        public float OfflineOfferSeconds { get; }
        public MatchmakingStatus Status { get; private set; } = MatchmakingStatus.Idle;
        public int Attempts { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;

        public bool IsActive => Status == MatchmakingStatus.Searching || Status == MatchmakingStatus.Attempting;

        public float Elapsed(float now) => IsActive ? Math.Max(0f, now - _startedAt) : 0f;

        public bool ShouldOfferOffline(float now) => IsActive && Elapsed(now) >= OfflineOfferSeconds;

        public void Start(float now)
        {
            Status = MatchmakingStatus.Searching;
            _startedAt = now;
            _nextAttemptAt = now; // first attempt immediately
            _failures = 0;
            Attempts = 0;
            LastFailure = string.Empty;
        }

        /// <summary>True when the caller should start a join attempt now (then call <see cref="ReportAttemptStarted"/>).</summary>
        public bool ShouldAttempt(float now) => Status == MatchmakingStatus.Searching && now >= _nextAttemptAt;

        public void ReportAttemptStarted()
        {
            if (Status != MatchmakingStatus.Searching)
                return;
            Status = MatchmakingStatus.Attempting;
            Attempts++;
        }

        public void ReportAttemptFailed(float now, string reason)
        {
            if (Status != MatchmakingStatus.Attempting)
                return;
            LastFailure = reason ?? string.Empty;
            float delay = RetryDelays[Math.Min(_failures, RetryDelays.Length - 1)];
            _failures++;
            _nextAttemptAt = now + delay;
            Status = MatchmakingStatus.Searching;
        }

        public void ReportJoined()
        {
            if (IsActive)
                Status = MatchmakingStatus.Joined;
        }

        public void Cancel()
        {
            if (IsActive)
                Status = MatchmakingStatus.Cancelled;
        }
    }
}
