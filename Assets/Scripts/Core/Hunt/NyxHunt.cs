using System;
using System.Collections.Generic;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Hunt
{
    /// <summary>Tunable Nyx Hunt Protocol rules (spec 13). Values marked [T] are starting points for balancing.</summary>
    [Serializable]
    public sealed class HuntSettings
    {
        /// <summary>Authoritative positions are sampled this often (spec: "such as once per second").</summary>
        public float sampleIntervalSeconds = 1f;

        public float warningSeconds = 20f;       // "WARNING: KEEP MOVING"
        public float finalWarningSeconds = 25f;  // "NYX IS WATCHING YOU"
        public float exposeSeconds = 30f;        // exposure + health penalty

        /// <summary>How long each exposure (beacon, minimap marker) lasts.</summary>
        public float exposureDurationSeconds = 10f;
        /// <summary>While still camping, a new exposure starts this long after the previous one started. [T]</summary>
        public float reexposeIntervalSeconds = 20f;

        /// <summary>Camping health penalty (spec 13: 2 HP/s). Ignores armor.</summary>
        public float damagePerSecond = 2f;
        public float damageIntervalSeconds = 0.5f;

        // Meaningful movement (spec 13): BOTH within the recovery window.
        public float meaningfulNetDisplacement = 8f;
        public float meaningfulPathDistance = 12f;
        public float recoveryWindowSeconds = 10f;
        /// <summary>Sample-to-sample moves shorter than this don't add to the path (microscopic jiggling). [T]</summary>
        public float minimumStepMetres = 0.5f;
        /// <summary>Faster than this between samples is not legitimate travel (teleport / malformed state) and counts for nothing. [T]</summary>
        public float maximumPlausibleSpeed = 14f;

        /// <summary>"HUNT STATUS CLEARED" phase after real movement, before NORMAL.</summary>
        public float recoveringSeconds = 2f;

        public float spawnGraceSeconds = 5f;
        /// <summary>After an approved reconnect the PENALTY pauses briefly; the inactivity timer is never reset.</summary>
        public float reconnectGraceSeconds = 5f;

        public HuntSettings Clone() => (HuntSettings)MemberwiseClone();

        public string Validate()
        {
            if (sampleIntervalSeconds <= 0f) return "Sample interval must be positive.";
            if (!(warningSeconds < finalWarningSeconds && finalWarningSeconds < exposeSeconds)) return "Need warning < final warning < exposure.";
            if (exposureDurationSeconds <= 0f || reexposeIntervalSeconds < exposureDurationSeconds) return "Re-exposure must not start before the previous exposure ends.";
            if (damagePerSecond < 0f || damageIntervalSeconds <= 0f) return "Invalid damage settings.";
            if (recoveryWindowSeconds < sampleIntervalSeconds) return "The recovery window must hold at least one sample step.";
            if (meaningfulNetDisplacement <= 0f || meaningfulPathDistance < meaningfulNetDisplacement) return "Path distance must be at least the net displacement.";
            return null;
        }
    }

    /// <summary>Spec 13 state machine: NORMAL → CAMPING_WARNING → EXPOSED → CAMPING_PENALTY → RECOVERING → NORMAL.</summary>
    public enum HuntState : byte
    {
        Normal = 0,
        CampingWarning = 1,
        Exposed = 2,
        CampingPenalty = 3,
        Recovering = 4,
    }

    [Flags]
    public enum HuntEvents : byte
    {
        None = 0,
        Warning = 1,
        FinalWarning = 2,
        /// <summary>An exposure started (first or repeated): beacon on, minimap marker, feed alert.</summary>
        Exposed = 4,
        /// <summary>The current exposure interval ended (beacon off) while still camping.</summary>
        ExposureEnded = 8,
        /// <summary>Meaningful movement after a warning or penalty: "HUNT STATUS CLEARED".</summary>
        Cleared = 16,
        /// <summary>The tracked player died or was removed: any beacon must disappear.</summary>
        Removed = 32,
    }

    public struct HuntTickResult
    {
        public float Damage;
        public HuntEvents Events;
    }

    /// <summary>What the server publishes about one player (networked for HUD, beacon and minimap).</summary>
    public struct HuntStatus
    {
        public HuntState State;
        /// <summary>0 none, 1 "KEEP MOVING", 2 "NYX IS WATCHING YOU".</summary>
        public byte WarningLevel;
        /// <summary>Server time the inactivity timer started (last meaningful movement or end of spawn grace).</summary>
        public float InactiveSince;
        /// <summary>While exposed: when the beacon goes off.</summary>
        public float ExposedUntil;
        public int ExposureCount;

        public bool BeaconVisible => State == HuntState.Exposed;
        public bool PenaltyActive => State == HuntState.Exposed || State == HuntState.CampingPenalty;
    }

    /// <summary>
    /// One player's camping tracker (server-side, spec 13). Fed the AUTHORITATIVE position every
    /// tick; samples it once per <see cref="HuntSettings.sampleIntervalSeconds"/>. Only movement that
    /// is both far enough (net displacement) and long enough (path) inside the recovery window
    /// resets the inactivity timer, so rotating, crouching, firing, swapping weapons, walking into a
    /// wall, jiggling, circling in a small area or a forged jump never count.
    /// </summary>
    public sealed class HuntTracker
    {
        private readonly HuntSettings _s;
        private readonly Queue<(float t, float dx, float dz, float length)> _steps = new Queue<(float, float, float, float)>();
        private float _lastX, _lastZ, _nextSampleAt;
        private bool _hasSample;
        private float _nextExposureAt;
        private float _recoveringUntil;
        private float _penaltySuspendedUntil;
        private float _pending;

        public HuntTracker(HuntSettings settings, float now, float x, float z, float grace)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Status = new HuntStatus { State = HuntState.Normal, InactiveSince = now + Math.Max(0f, grace) };
            _lastX = x;
            _lastZ = z;
            _hasSample = true;
            _nextSampleAt = now + _s.sampleIntervalSeconds;
        }

        public HuntStatus Status { get; private set; }

        /// <summary>Seconds without meaningful movement (0 during the spawn grace).</summary>
        public float InactiveSeconds(float now) => Math.Max(0f, now - Status.InactiveSince);

        /// <summary>Approved reconnect: keep every timer, only pause the penalty briefly (spec 13).</summary>
        public void OnReconnected(float now, float x, float z)
        {
            _penaltySuspendedUntil = now + _s.reconnectGraceSeconds;
            // The body may have been moved while away; don't count that as travel.
            _lastX = x;
            _lastZ = z;
            _steps.Clear();
            _nextSampleAt = now + _s.sampleIntervalSeconds;
        }

        public HuntTickResult Update(float now, float x, float z, float dt)
        {
            var result = new HuntTickResult();
            var st = Status;

            if (now >= _nextSampleAt)
            {
                _nextSampleAt = now + _s.sampleIntervalSeconds;
                if (Sample(now, x, z))
                {
                    if (st.State != HuntState.Normal && st.State != HuntState.Recovering)
                    {
                        result.Events |= HuntEvents.Cleared;
                        if (st.State == HuntState.Exposed)
                            result.Events |= HuntEvents.ExposureEnded;
                        st.State = HuntState.Recovering;
                        _recoveringUntil = now + _s.recoveringSeconds;
                    }
                    st.InactiveSince = Math.Max(st.InactiveSince, now);
                    st.WarningLevel = 0;
                    _pending = 0f;
                }
            }

            float inactive = now - st.InactiveSince;
            if (inactive >= _s.exposeSeconds)
            {
                switch (st.State)
                {
                    case HuntState.Exposed:
                        if (now >= st.ExposedUntil)
                        {
                            st.State = HuntState.CampingPenalty;
                            result.Events |= HuntEvents.ExposureEnded;
                        }
                        break;
                    case HuntState.CampingPenalty:
                        if (now >= _nextExposureAt)
                            Expose(ref st, now, ref result);
                        break;
                    default:
                        Expose(ref st, now, ref result);
                        break;
                }
            }
            else if (inactive >= _s.finalWarningSeconds)
            {
                if (st.WarningLevel < 2)
                    result.Events |= HuntEvents.FinalWarning;
                st.State = HuntState.CampingWarning;
                st.WarningLevel = 2;
            }
            else if (inactive >= _s.warningSeconds)
            {
                if (st.WarningLevel < 1)
                    result.Events |= HuntEvents.Warning;
                st.State = HuntState.CampingWarning;
                st.WarningLevel = 1;
            }
            else if (st.State == HuntState.Recovering && now >= _recoveringUntil)
            {
                st.State = HuntState.Normal;
            }

            // Health penalty from the 30 s mark until meaningful movement (pulsed, like zone damage).
            if (st.State == HuntState.Exposed || st.State == HuntState.CampingPenalty)
            {
                if (now >= _penaltySuspendedUntil && dt > 0f && _s.damagePerSecond > 0f)
                {
                    _pending += dt;
                    while (_pending >= _s.damageIntervalSeconds - 1e-4f)
                    {
                        _pending = Math.Max(0f, _pending - _s.damageIntervalSeconds);
                        result.Damage += _s.damagePerSecond * _s.damageIntervalSeconds;
                    }
                }
            }

            Status = st;
            return result;
        }

        private void Expose(ref HuntStatus st, float now, ref HuntTickResult result)
        {
            st.State = HuntState.Exposed;
            st.WarningLevel = 2;
            st.ExposedUntil = now + _s.exposureDurationSeconds;
            st.ExposureCount++;
            _nextExposureAt = now + _s.reexposeIntervalSeconds;
            result.Events |= HuntEvents.Exposed;
        }

        /// <returns>True when the recovery window now shows meaningful movement.</returns>
        private bool Sample(float now, float x, float z)
        {
            float dx = x - _lastX;
            float dz = z - _lastZ;
            float length = MathF.Sqrt(dx * dx + dz * dz);
            _lastX = x;
            _lastZ = z;
            if (!_hasSample || float.IsNaN(length) || float.IsInfinity(length))
            {
                _hasSample = true;
                return false;
            }

            // Implausibly fast = not legitimate travel: it counts for nothing (neither path nor displacement).
            if (length > _s.maximumPlausibleSpeed * _s.sampleIntervalSeconds * 1.5f)
            {
                dx = dz = length = 0f;
            }

            _steps.Enqueue((now, dx, dz, length));
            while (_steps.Count > 0 && _steps.Peek().t <= now - _s.recoveryWindowSeconds - 1e-3f)
                _steps.Dequeue();

            float netX = 0f, netZ = 0f, path = 0f;
            foreach (var step in _steps)
            {
                netX += step.dx;
                netZ += step.dz;
                if (step.length >= _s.minimumStepMetres)
                    path += step.length;
            }

            bool meaningful = path >= _s.meaningfulPathDistance
                              && MathF.Sqrt(netX * netX + netZ * netZ) >= _s.meaningfulNetDisplacement;
            if (meaningful)
                _steps.Clear(); // the next reset needs fresh travel
            return meaningful;
        }
    }

    /// <summary>
    /// All players' trackers on the server, keyed by a stable participant key (the network player
    /// id now; an account id once reconnects to running matches exist). A player who disconnects
    /// keeps their tracker, so reconnecting never resets the camping timer.
    /// </summary>
    public sealed class NyxHuntProtocol
    {
        private readonly Dictionary<int, HuntTracker> _trackers = new Dictionary<int, HuntTracker>();
        private readonly HashSet<int> _disconnected = new HashSet<int>();

        public NyxHuntProtocol(HuntSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));
        }

        public HuntSettings Settings { get; }
        public int Count => _trackers.Count;

        /// <summary>Start tracking at spawn / match start (with the spawn grace). A reconnecting key keeps its old tracker.</summary>
        public HuntTracker Track(int key, float now, float x, float z)
        {
            if (_trackers.TryGetValue(key, out var existing))
            {
                if (_disconnected.Remove(key))
                    existing.OnReconnected(now, x, z);
                return existing;
            }
            var tracker = new HuntTracker(Settings, now, x, z, Settings.spawnGraceSeconds);
            _trackers[key] = tracker;
            return tracker;
        }

        public bool TryGet(int key, out HuntTracker tracker) => _trackers.TryGetValue(key, out tracker);

        /// <summary>Server tick for one living player. Returns the camping damage to apply this tick.</summary>
        public HuntTickResult Update(int key, float now, float x, float z, float dt) =>
            _trackers.TryGetValue(key, out var t) && !_disconnected.Contains(key) ? t.Update(now, x, z, dt) : default;

        public void MarkDisconnected(int key)
        {
            if (_trackers.ContainsKey(key))
                _disconnected.Add(key);
        }

        /// <summary>Eliminated: stop tracking; the beacon must go (spec 13 test 11).</summary>
        public HuntEvents Remove(int key)
        {
            _disconnected.Remove(key);
            return _trackers.Remove(key) ? HuntEvents.Removed : HuntEvents.None;
        }

        public void Clear()
        {
            _trackers.Clear();
            _disconnected.Clear();
        }
    }

    /// <summary>
    /// Environmental damage for one tick (spec 13: zone and camping damage stack additively in one
    /// pass, never twice). The cause of a resulting elimination is the larger contributor.
    /// </summary>
    public static class EnvironmentDamage
    {
        public static float Total(float zone, float camping) => Positive(zone) + Positive(camping);

        public static DamageKind Cause(float zone, float camping) =>
            Positive(camping) > Positive(zone) ? DamageKind.Camping : DamageKind.Zone;

        private static float Positive(float v) => float.IsNaN(v) || v < 0f ? 0f : v;
    }

    /// <summary>Text shown by the HUD and the feed (spec 13, 19).</summary>
    public static class HuntText
    {
        public const string Warning = "WARNING: KEEP MOVING";
        public const string FinalWarning = "NYX IS WATCHING YOU";
        public const string YouAreExposed = "YOU ARE BEING HUNTED";
        public const string Cleared = "HUNT STATUS CLEARED";

        public static string Alert(string playerName) => $"HUNT ALERT: {playerName} EXPOSED FOR CAMPING";
    }
}

namespace WarOfNyx.Core.Hunt
{
    /// <summary>
    /// The local player's hunt notices from successive replicated statuses (client-only, cosmetic):
    /// each fires once per change, so repeated replication or rollbacks never repeat it.
    /// </summary>
    public sealed class HuntStatusAnnouncer
    {
        private HuntState _state = HuntState.Normal;
        private byte _level;
        private int _exposures;

        /// <returns>A headline to show, or null.</returns>
        public string Update(in HuntStatus status, bool alive)
        {
            string headline = null;
            if (!alive)
            {
                _state = HuntState.Normal;
                _level = 0;
                return null;
            }

            if (status.State == HuntState.Exposed && (_state != HuntState.Exposed || status.ExposureCount != _exposures))
                headline = HuntText.YouAreExposed;
            else if (status.State == HuntState.Recovering && _state != HuntState.Recovering && _state != HuntState.Normal)
                headline = HuntText.Cleared;
            else if (status.State == HuntState.CampingWarning && status.WarningLevel > _level)
                headline = status.WarningLevel >= 2 ? HuntText.FinalWarning : HuntText.Warning;

            _state = status.State;
            _level = status.State == HuntState.CampingWarning || status.State == HuntState.Exposed || status.State == HuntState.CampingPenalty
                ? status.WarningLevel
                : (byte)0;
            _exposures = status.ExposureCount;
            return headline;
        }
    }
}
