using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Core.Zone;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Networking
{
    /// <summary>Networked form of a <see cref="ZoneCircle"/>.</summary>
    public struct NetworkZoneCircle : INetworkStruct
    {
        public float X;
        public float Z;
        public float Radius;

        public ZoneCircle ToCircle() => new ZoneCircle(X, Z, Radius);

        public static NetworkZoneCircle From(in ZoneCircle c) => new NetworkZoneCircle { X = c.X, Z = c.Z, Radius = c.Radius };
    }

    /// <summary>
    /// The Nyx Circle (Phase 7, spec 10-12). Lives on the MatchController object.
    ///
    ///   SERVER: plans every circle when the countdown starts (centres on fair ground, each inside
    ///     the previous one) and publishes the CURRENT phase: start circle, target circle, start
    ///     and end time, damage per second. Future circles are never sent, so nobody learns the
    ///     endgame early; each new target appears when its phase begins.
    ///   EVERY PEER: derives the shrinking circle from those values and the simulation time, so
    ///     the shrink is smooth and identical everywhere without per-tick radius updates.
    ///
    /// Inside/outside decisions are made by <see cref="NetworkPlayer"/> on the server from the
    /// authoritative body position (the owner predicts the same check).
    /// </summary>
    public sealed class NetworkNyxCircle : NetworkBehaviour
    {
        public static NetworkNyxCircle Instance { get; private set; }

        [Networked] private NetworkBool NetActive { get; set; }
        [Networked] private byte NetStageIndex { get; set; }
        [Networked] private byte NetStageCount { get; set; }
        [Networked] private NetworkZoneCircle NetFrom { get; set; }
        [Networked] private NetworkZoneCircle NetTo { get; set; }
        [Networked] private float NetStartsAt { get; set; }
        [Networked] private float NetEndsAt { get; set; }
        [Networked] private float NetFinalStartsAt { get; set; }
        [Networked] private float NetDamagePerSecond { get; set; }
        /// <summary>Ground height of the map centre, for placing the barrier visual.</summary>
        [Networked] private float NetGroundY { get; set; }

        private ZonePlan _plan;
        private ZoneSettings _serverSettings;
        private float _matchStart;
        private NyxCircleVisual _visual;
        private static ZoneSettings _fallbackSettings;

        public bool IsActive => NetActive;

        public float DamagePerSecond => NetDamagePerSecond;

        /// <summary>
        /// Rules used locally (instability amplitude, aim sway, damage interval). Phase timing always
        /// comes from the replicated stage, never from these settings.
        /// </summary>
        public static ZoneSettings LocalSettings
        {
            get
            {
                if (AppServices.IsInitialized && AppServices.Config.zone != null)
                    return AppServices.Config.zone;
                return _fallbackSettings ??= new ZoneSettings();
            }
        }

        public ZoneStage Stage => new ZoneStage
        {
            Index = NetStageIndex,
            Count = NetStageCount,
            From = NetFrom.ToCircle(),
            To = NetTo.ToCircle(),
            StartsAt = NetStartsAt,
            EndsAt = NetEndsAt,
            FinalStartsAt = NetFinalStartsAt,
            DamagePerSecond = NetDamagePerSecond,
        };

        public ZoneCircle CircleAt(float time) => Stage.CircleAt(time);

        /// <summary>Inside the circle at <paramref name="time"/>? Everyone is inside while the circle is not active.</summary>
        public bool IsInside(Vector3 position, float time) =>
            ZoneRules.IsInside(NetActive, CircleAt(time), position.x, position.z);

        // ------------------------------------------------------------------ server

        /// <summary>
        /// Server: plan the match's circles and start the schedule at <paramref name="matchStart"/> ("FIGHT!").
        /// The first circle is the map's starting safe zone (the spawn area), so every spawn is inside it.
        /// </summary>
        public void ServerBegin(ZoneSettings settings, float matchStart, int seed, Vector3 centre, float startRadius,
            IZoneCentreValidator validator)
        {
            if (!HasStateAuthority)
                return;

            string error = settings?.Validate();
            if (settings == null || error != null)
            {
                Debug.LogError($"[WarOfNyx][Zone] Invalid circle settings ({error ?? "missing"}); using the spec defaults.");
                settings = new ZoneSettings();
            }

            _serverSettings = settings;
            _matchStart = matchStart;
            _plan = ZonePlanner.Plan(settings, centre.x, centre.z, startRadius, seed, validator);
            NetGroundY = centre.y;
            NetActive = true;
            Publish();

            Debug.Log($"[WarOfNyx][Zone] Planned {_plan.StageCount} phases (scale {_plan.Scale:0.###}, {settings.TotalSeconds:0} s): " +
                      string.Join(" -> ", System.Array.ConvertAll(_plan.Circles, c => c.ToString())));
        }

        /// <summary>Server: the match was abandoned (back to the lobby).</summary>
        public void ServerStop()
        {
            if (!HasStateAuthority)
                return;
            _plan = null;
            NetActive = false;
        }

        /// <summary>Server: the match is over. The circle stops where it is (spec 21: stop the safe-zone timer).</summary>
        public void ServerFreeze(float now)
        {
            if (!HasStateAuthority || !NetActive)
                return;
            var here = CircleAt(now);
            _plan = null;
            NetFrom = NetworkZoneCircle.From(here);
            NetTo = NetworkZoneCircle.From(here);
            NetStartsAt = now;
            NetEndsAt = now;
        }

        public override void Spawned() => Instance = this;

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;
            if (_visual != null)
                Destroy(_visual.gameObject);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && _plan != null)
                Publish();
        }

        private void Publish()
        {
            var stage = ZoneTimeline.Evaluate(_plan, _serverSettings, _matchStart, Runner.SimulationTime);
            // Unchanged values are not re-sent; these only change when a phase begins.
            NetStageIndex = (byte)stage.Index;
            NetStageCount = (byte)stage.Count;
            NetFrom = NetworkZoneCircle.From(stage.From);
            NetTo = NetworkZoneCircle.From(stage.To);
            NetStartsAt = stage.StartsAt;
            NetEndsAt = stage.EndsAt;
            NetFinalStartsAt = stage.FinalStartsAt;
            NetDamagePerSecond = stage.DamagePerSecond;
        }

        // ------------------------------------------------------------------ presentation

        public override void Render()
        {
            if (Application.isBatchMode)
                return;

            if (!NetActive)
            {
                if (_visual != null)
                    _visual.gameObject.SetActive(false);
                return;
            }

            if (_visual == null)
                _visual = NyxCircleVisual.Create(transform);
            _visual.gameObject.SetActive(true);

            // Render time sits between ticks, so the barrier moves smoothly every frame.
            var stage = Stage;
            _visual.Show(stage.CircleAt(Runner.LocalRenderTime), stage.To, !stage.Finished, NetGroundY);
        }
    }
}
