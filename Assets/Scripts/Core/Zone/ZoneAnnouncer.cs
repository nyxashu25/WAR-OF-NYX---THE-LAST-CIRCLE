using System.Collections.Generic;

namespace WarOfNyx.Core.Zone
{
    /// <summary>What the local HUD knows about the circle in one frame (all replicated or predicted values).</summary>
    public struct ZoneView
    {
        public bool Active;
        public ZoneStage Stage;
        public ZoneCircle Current;
        /// <summary>Server simulation time of this view.</summary>
        public float Time;
        /// <summary>The local player's server-classified state (predicted on the owner).</summary>
        public bool PlayerOutside;
        public bool PlayerAlive;
        public float PlayerDistanceToEdge;
    }

    public enum ZoneAnnouncementKind : byte
    {
        PhaseStarted = 0,
        FinalWarning = 1,
        FinalStarted = 2,
        CircleClosed = 3,
        LeftSafeZone = 4,
        EnteredSafeZone = 5,
    }

    public readonly struct ZoneAnnouncement
    {
        public readonly ZoneAnnouncementKind Kind;
        public readonly string Text;
        /// <summary>True for big centre-screen notices, false for feed lines.</summary>
        public readonly bool Headline;

        public ZoneAnnouncement(ZoneAnnouncementKind kind, string text, bool headline)
        {
            Kind = kind;
            Text = text;
            Headline = headline;
        }
    }

    /// <summary>
    /// Turns successive <see cref="ZoneView"/>s into one-time notifications (spec 10, 12, 19):
    /// "NYX CIRCLE SHRINKING", "FINAL CIRCLE BEGINS IN 10 SECONDS", "FINAL CIRCLE",
    /// "FIREARMS DISABLED OUTSIDE THE CIRCLE", "SAFE ZONE ENTERED". Each fires once per change, so
    /// replicated state arriving again (or rollbacks) never repeats a notice. Client-only, cosmetic.
    /// </summary>
    public sealed class ZoneAnnouncer
    {
        public const string FirearmsDisabledText = "FIREARMS DISABLED OUTSIDE THE CIRCLE";
        public const string SafeZoneEnteredText = "SAFE ZONE ENTERED";
        public const string ShrinkingText = "NYX CIRCLE SHRINKING";
        public const string FinalCircleText = "FINAL CIRCLE";

        private readonly float _finalWarningSeconds;
        private int _announcedStage = -1;
        private bool _finalWarned;
        private bool _wasOutside;
        private bool _wasActive;
        private readonly List<ZoneAnnouncement> _out = new List<ZoneAnnouncement>();

        public ZoneAnnouncer(float finalWarningSeconds = 10f)
        {
            _finalWarningSeconds = finalWarningSeconds > 0f ? finalWarningSeconds : 10f;
        }

        public void Reset()
        {
            _announcedStage = -1;
            _finalWarned = false;
            _wasOutside = false;
            _wasActive = false;
        }

        /// <summary>Notifications triggered by this view (the list is reused between calls).</summary>
        public IReadOnlyList<ZoneAnnouncement> Update(in ZoneView view)
        {
            _out.Clear();
            if (!view.Active)
            {
                if (_wasActive)
                    Reset();
                return _out;
            }
            _wasActive = true;

            var stage = view.Stage;
            if (stage.Index != _announcedStage && view.Time >= stage.StartsAt)
            {
                _announcedStage = stage.Index;
                if (stage.Finished)
                    _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.CircleClosed, "THE NYX CIRCLE HAS CLOSED", true));
                else if (stage.IsFinal)
                    _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.FinalStarted, FinalCircleText, true));
                else
                {
                    _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.PhaseStarted, ShrinkingText, true));
                    _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.PhaseStarted,
                        $"Nyx Circle phase {stage.Index + 1}/{stage.Count}: shrinking for {stage.EndsAt - stage.StartsAt:0} s", false));
                }
            }

            float toFinal = stage.FinalStartsAt - view.Time;
            if (!_finalWarned && stage.Count > 1 && stage.Index < stage.Count - 1 && toFinal > 0f && toFinal <= _finalWarningSeconds)
            {
                _finalWarned = true;
                _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.FinalWarning, $"FINAL CIRCLE BEGINS IN {_finalWarningSeconds:0} SECONDS", false));
            }

            bool outside = view.PlayerAlive && view.PlayerOutside;
            if (outside && !_wasOutside)
                _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.LeftSafeZone, FirearmsDisabledText, true));
            else if (!outside && _wasOutside && view.PlayerAlive)
                _out.Add(new ZoneAnnouncement(ZoneAnnouncementKind.EnteredSafeZone, SafeZoneEnteredText, true));
            _wasOutside = outside;

            return _out;
        }
    }
}
