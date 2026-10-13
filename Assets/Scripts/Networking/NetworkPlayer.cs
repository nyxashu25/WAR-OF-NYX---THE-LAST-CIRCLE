using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;
using WarOfNyx.Core.Zone;
using WarOfNyx.Gameplay;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.UI.Hud;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// Networked player avatar. Runs the shared movement model inside Fusion's tick loop.
    ///
    /// Authority per action:
    ///   SERVER (state authority): simulates movement from received inputs (sanitized), owns
    ///     health, armor and spawn protection, sanitizes the display name.
    ///   OWNING CLIENT (input authority): sends inputs and PREDICTS the same simulation; Fusion
    ///     rolls back to server state and re-simulates on corrections.
    ///   OTHER CLIENTS (proxies): never simulate; show the interpolated transform and the
    ///     replicated stance / shield. Purely visual.
    ///
    /// Synchronized start: until the match's shared start tick, movement input is ignored on
    /// the server AND in prediction (players can only look around), so nobody gets a head start.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInputRouter))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public const float DefaultMaxHealth = 100f;

        /// <summary>Input source polled by the session manager's OnInput. Only set for this device's player.</summary>
        public static PlayerInputRouter LocalRouter { get; private set; }

        [Networked] public NetworkMovementState Movement { get; set; }
        [Networked] public NetworkString<_16> DisplayName { get; set; }

        // Spawn protection (spec 6), server-authoritative.
        [Networked] private float ProtectionEndsAt { get; set; }
        [Networked] private NetworkBool ProtectionBroken { get; set; }

        /// <summary>
        /// Outside the Nyx Circle this tick (spec 11-12). Decided by the SERVER from its own simulated
        /// body position; the owner predicts it the same way, proxies only display it.
        /// </summary>
        [Networked] public NetworkBool OutsideZone { get; set; }

        // Nyx Hunt Protocol (spec 13), written by the server only, shown by every client.
        [Networked] private byte HuntStateNet { get; set; }
        [Networked] private byte HuntWarningNet { get; set; }
        [Networked] private float HuntInactiveSinceNet { get; set; }
        [Networked] private float HuntExposedUntilNet { get; set; }
        [Networked] private int HuntExposureCountNet { get; set; }

        // Match result (Phase 9), set by the server when the player is placed.
        [Networked] public byte Placement { get; set; }
        [Networked] public float SurvivalSeconds { get; set; }
        [Networked] public NetworkBool IsWinner { get; set; }

        /// <summary>Server only: final placement for this player (1 = winner).</summary>
        public void ServerSetResult(int placement, float survivalSeconds, bool winner)
        {
            if (!HasStateAuthority)
                return;
            Placement = (byte)Mathf.Clamp(placement, 0, 255);
            SurvivalSeconds = survivalSeconds;
            IsWinner = winner;
        }

        /// <summary>Every spawned player on this peer (for beacons, minimap markers and direction indicators).</summary>
        public static readonly System.Collections.Generic.List<NetworkPlayer> All = new System.Collections.Generic.List<NetworkPlayer>();

        public HuntStatus Hunt => new HuntStatus
        {
            State = (HuntState)HuntStateNet,
            WarningLevel = HuntWarningNet,
            InactiveSince = HuntInactiveSinceNet,
            ExposedUntil = HuntExposedUntilNet,
            ExposureCount = HuntExposureCountNet,
        };

        public bool IsAlive => _combat == null || _combat.IsAlive;

        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private NetworkCombat _combat;
        private TextMesh _nameTag;
        private SpawnShieldVisual _shield;
        private OutsideZoneVisual _outsideVisual;
        private HuntBeaconVisual _beacon;

        public string PlayerName => DisplayName.Value;

        /// <summary>Local aim button state (crosshair size on the owner). Gameplay uses the networked input instead.</summary>
        public bool LocalAimHeld => _router != null && _router.enabled && _router.AimHeld;

        private SpawnProtectionState Protection =>
            new SpawnProtectionState { EndsAt = ProtectionEndsAt, Broken = ProtectionBroken };

        public bool IsSpawnProtected => Runner != null && Protection.IsActive(Runner.SimulationTime);

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _router = GetComponent<PlayerInputRouter>();
            _combat = GetComponent<NetworkCombat>();
        }

        /// <summary>Server only, before the object exists on the network: initial authoritative state.</summary>
        public void InitializeSpawn(Vector3 position, float yaw, int playerId, float protectionEndsAt, int loadoutSeed)
        {
            Movement = NetworkMovementState.From(MovementState.Initial(yaw), false, position);
            DisplayName = PlayerNames.Fallback(playerId);
            GetComponent<NetworkCombat>().InitializeSpawn(loadoutSeed); // health 100 + randomized starting loadout
            ProtectionEndsAt = protectionEndsAt;
            ProtectionBroken = false;
        }

        /// <summary>
        /// Server only. Call BEFORE resolving any aggressive action (firing, melee, picking up a combat
        /// item), so a protected player can never hurt anyone while still protected.
        /// </summary>
        public void OnAggressiveAction(AggressiveAction action)
        {
            if (!HasStateAuthority)
                return;
            var p = Protection;
            p.OnAggressiveAction(action);
            ProtectionBroken = p.Broken;
        }

        public override void Spawned()
        {
            All.Add(this);
            var state = Movement;
            _movement.RestoreState(state.ToState(), state.Grounded, state.Position);
            gameObject.name = $"Player {Object.InputAuthority.PlayerId}{(HasInputAuthority ? " (local)" : string.Empty)}";

            if (!Application.isBatchMode)
            {
                float drop = AppServices.IsInitialized ? AppServices.Config.matchRules.spawnDropHeight : 0f;
                SpawnEffect.Play(state.Position - Vector3.up * drop);
                _shield = SpawnShieldVisual.Attach(transform);
                _outsideVisual = OutsideZoneVisual.Attach(transform);
                _beacon = HuntBeaconVisual.Attach(transform);
            }

            if (HasInputAuthority)
            {
                LocalRouter = _router;
                _router.enabled = true;
                _router.SetAim(state.Yaw, 10f);
                if (OnlineArenaController.Instance != null)
                {
                    OnlineArenaController.Instance.BindLocalPlayer(_movement, _router);
                    OnlineArenaController.Instance.SpectateTargets = GatherSpectateTargets;
                }

                string requested = AppServices.IsInitialized ? AppServices.UserSettings.Data.playerName : string.Empty;
                RPC_RequestName(requested ?? string.Empty);
            }
            else
            {
                // Proxies and the server never read local devices.
                _router.enabled = false;
                if (!Application.isBatchMode)
                    CreateNameTag();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            bool wasLocal = LocalRouter == _router;
            if (wasLocal)
                LocalRouter = null;
            if (OnlineArenaController.Instance != null)
            {
                OnlineArenaController.Instance.UnbindLocalPlayer(_movement);
                if (wasLocal)
                    OnlineArenaController.Instance.ShowSpawnProtection(0f);
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Only the authority and the predicting owner simulate; proxies just render.
            if (!HasStateAuthority && !HasInputAuthority)
                return;

            var state = Movement;
            _movement.RestoreState(state.ToState(), state.Grounded, state.Position);

            bool hasInput = GetInput(out NetworkInputData input);
            var command = hasInput ? input.ToCommand() : InputCodec.Neutral(state.Yaw, 0f); // lost input: stand still
            var combat = hasInput ? input.ToCombat() : default;

            // Before the shared start tick: look around only (same rule on server and in prediction).
            var match = NetworkMatchController.Instance;
            bool unlocked = match == null || match.MovementAllowedAt(Runner.SimulationTime);
            if (!unlocked)
                command = InputCodec.Neutral(command.Yaw, command.Pitch);

            bool alive = _combat == null || _combat.IsAlive;

            // Nyx Circle (spec 11-12): classified from this peer's simulated position, never from client data.
            var zone = NetworkNyxCircle.Instance;
            bool zoneLive = unlocked && zone != null && zone.IsActive;
            float now = Runner.SimulationTime;
            var settings = NetworkNyxCircle.LocalSettings;
            int tick = Runner.Tick.Raw;
            int playerId = Object.InputAuthority.PlayerId;

            if (alive)
            {
                // Authority-applied modifiers (never from the client): active weapon speed and,
                // outside the circle, the deterministic sideways wobble.
                command.SpeedMultiplier = _combat != null ? _combat.ActiveSpeedMultiplier : 1f;
                bool outsideBeforeMove = zoneLive && !zone.IsInside(transform.position, now);
                command.LateralWobble = outsideBeforeMove ? ZoneInstability.Lateral(tick, Runner.DeltaTime, playerId, settings) : 0f;
                _movement.Simulate(command, Runner.DeltaTime);
                Movement = NetworkMovementState.From(_movement.State, _movement.IsGrounded, transform.position);
            }

            // After moving: where the body is now decides firearms and zone damage for this tick.
            bool outside = alive && zoneLive && !zone.IsInside(transform.position, now);
            OutsideZone = outside;

            float yaw = command.Yaw, pitch = command.Pitch;
            if (outside)
            {
                ZoneInstability.AimSway(tick, Runner.DeltaTime, playerId, settings, out float swayYaw, out float swayPitch);
                yaw += swayYaw;
                pitch += swayPitch;
            }

            // Combat after movement: shots start from this tick's position (same order on server and client).
            if (_combat != null)
                _combat.Simulate(combat, yaw, pitch, command.AimHeld, _movement.State.IsLowStance, _movement.State.IsSprinting, unlocked,
                    firearmsAllowed: !outside);

            // Server: one environmental damage pass per tick (zone + camping, spec 12-13).
            if (HasStateAuthority && unlocked && _combat != null)
                ServerEnvironmentPass(zoneLive ? zone : null, outside, settings);
        }

        /// <summary>Server only. Zone exposure and the Nyx Hunt Protocol for this tick, applied together.</summary>
        private void ServerEnvironmentPass(NetworkNyxCircle zone, bool outside, ZoneSettings zoneSettings)
        {
            var match = NetworkMatchController.Instance;
            var hunt = match != null ? match.Hunt : null;
            int key = Object.InputAuthority.PlayerId;

            if (!_combat.IsAlive)
            {
                if (hunt != null)
                    hunt.Remove(key); // eliminated: beacon removed, tracking stops
                PublishHunt(default);
                return;
            }

            float dt = Runner.DeltaTime;
            float now = Runner.SimulationTime;
            float zoneDamage = zone != null ? _combat.StepZoneExposure(!outside, zone.DamagePerSecond, dt, zoneSettings.damageIntervalSeconds) : 0f;

            float campingDamage = 0f;
            if (hunt != null)
            {
                var p = transform.position;
                var tracker = hunt.TryGet(key, out var existing) ? existing : hunt.Track(key, now, p.x, p.z);
                var tick = hunt.Update(key, now, p.x, p.z, dt);
                campingDamage = tick.Damage;
                if ((tick.Events & HuntEvents.Exposed) != 0)
                    match.AnnounceHuntAlert(Object.InputAuthority, tracker.Status.ExposureCount);
                PublishHunt(tracker.Status);
            }

            if (_combat.ApplyEnvironmentDamage(zoneDamage, campingDamage) && hunt != null)
            {
                hunt.Remove(key);
                PublishHunt(default);
            }
        }

        private void PublishHunt(in HuntStatus status)
        {
            HuntStateNet = (byte)status.State;
            HuntWarningNet = status.WarningLevel;
            HuntInactiveSinceNet = status.InactiveSince;
            HuntExposedUntilNet = status.ExposedUntil;
            HuntExposureCountNet = status.ExposureCount;
        }

        public override void Render()
        {
            if (!HasStateAuthority && !HasInputAuthority)
                _movement.ApplyVisualState(Movement.ToState());

            float remaining = Runner != null ? Protection.Remaining(Runner.SimulationTime) : 0f;
            if (_shield != null)
                _shield.SetVisible(remaining > 0f);
            if (HasInputAuthority && OnlineArenaController.Instance != null)
                OnlineArenaController.Instance.ShowSpawnProtection(remaining);

            bool outsideVisible = OutsideZone && (_combat == null || _combat.IsAlive);
            if (_outsideVisual != null)
                _outsideVisual.SetActive(outsideVisible);
            if (_beacon != null)
                _beacon.SetActive(IsAlive && Hunt.BeaconVisible);
            if (HasInputAuthority && OnlineArenaController.Instance != null)
            {
                OnlineArenaController.Instance.ShowZone(BuildZoneView(), transform.position, Movement.Yaw);
                ShowHuntToLocalPlayer(OnlineArenaController.Instance);
            }

            if (_nameTag != null)
                _nameTag.gameObject.SetActive(_combat == null || _combat.IsAlive);
            if (_nameTag != null && _nameTag.gameObject.activeSelf)
            {
                _nameTag.text = DisplayName.Value;
                var cam = Camera.main;
                if (cam != null)
                    _nameTag.transform.rotation = Quaternion.LookRotation(_nameTag.transform.position - cam.transform.position);
            }
        }

        private readonly System.Collections.Generic.List<SpectateTarget> _spectateTargets = new System.Collections.Generic.List<SpectateTarget>();

        /// <summary>Living players this (eliminated) player may watch (spec 21). Only what any player can already see.</summary>
        private System.Collections.Generic.IReadOnlyList<SpectateTarget> GatherSpectateTargets()
        {
            _spectateTargets.Clear();
            foreach (var other in All)
            {
                if (other == null || other == this || !other.IsAlive)
                    continue;
                _spectateTargets.Add(new SpectateTarget { Transform = other.transform, Movement = other._movement, Name = other.PlayerName });
            }
            return _spectateTargets;
        }

        private readonly System.Collections.Generic.List<HuntTarget> _exposed = new System.Collections.Generic.List<HuntTarget>();

        /// <summary>Local player: own hunt status plus every OTHER exposed player (beacon targets for minimap and arrows).</summary>
        private void ShowHuntToLocalPlayer(OnlineArenaController arena)
        {
            _exposed.Clear();
            foreach (var other in All)
            {
                if (other == null || other == this || !other.IsAlive || !other.Hunt.BeaconVisible)
                    continue;
                _exposed.Add(new HuntTarget { Position = other.transform.position, Name = other.PlayerName });
            }
            float now = Runner != null ? Runner.SimulationTime : 0f;
            arena.ShowHunt(Hunt, IsAlive, now, transform.position, _exposed);
        }

        private ZoneView BuildZoneView()
        {
            var zone = NetworkNyxCircle.Instance;
            if (zone == null || !zone.IsActive || Runner == null)
                return default;
            float now = Runner.SimulationTime;
            var current = zone.CircleAt(now);
            var p = transform.position;
            return new ZoneView
            {
                Active = true,
                Stage = zone.Stage,
                Current = current,
                Time = now,
                PlayerOutside = OutsideZone,
                PlayerAlive = _combat == null || _combat.IsAlive,
                PlayerDistanceToEdge = current.DistanceToEdge(p.x, p.z),
            };
        }

        /// <summary>Owner → server. The server sanitizes; the client never sets its own networked name.</summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestName(string requested)
        {
            DisplayName = PlayerNames.Sanitize(requested, Object.InputAuthority.PlayerId);
        }

        private void CreateNameTag()
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.25f, 0f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _nameTag = go.AddComponent<TextMesh>();
            _nameTag.font = font;
            _nameTag.fontSize = 48;
            _nameTag.characterSize = 0.05f;
            _nameTag.anchor = TextAnchor.MiddleCenter;
            _nameTag.alignment = TextAlignment.Center;
            _nameTag.color = new Color(0.94f, 0.94f, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
