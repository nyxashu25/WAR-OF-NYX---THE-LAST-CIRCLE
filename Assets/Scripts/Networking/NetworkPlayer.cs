using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;
using WarOfNyx.Gameplay;
using WarOfNyx.Gameplay.Effects;

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

        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private NetworkCombat _combat;
        private TextMesh _nameTag;
        private SpawnShieldVisual _shield;

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
            var state = Movement;
            _movement.RestoreState(state.ToState(), state.Grounded, state.Position);
            gameObject.name = $"Player {Object.InputAuthority.PlayerId}{(HasInputAuthority ? " (local)" : string.Empty)}";

            if (!Application.isBatchMode)
            {
                float drop = AppServices.IsInitialized ? AppServices.Config.matchRules.spawnDropHeight : 0f;
                SpawnEffect.Play(state.Position - Vector3.up * drop);
                _shield = SpawnShieldVisual.Attach(transform);
            }

            if (HasInputAuthority)
            {
                LocalRouter = _router;
                _router.enabled = true;
                _router.SetAim(state.Yaw, 10f);
                if (OnlineArenaController.Instance != null)
                    OnlineArenaController.Instance.BindLocalPlayer(_movement, _router);

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
            if (alive)
            {
                // Authority-applied modifiers (never from the client): active weapon speed.
                command.SpeedMultiplier = _combat != null ? _combat.ActiveSpeedMultiplier : 1f;
                _movement.Simulate(command, Runner.DeltaTime);
                Movement = NetworkMovementState.From(_movement.State, _movement.IsGrounded, transform.position);
            }

            // Combat after movement: shots start from this tick's position (same order on server and client).
            if (_combat != null)
                _combat.Simulate(combat, command.Yaw, command.Pitch, command.AimHeld, _movement.State.IsLowStance, _movement.State.IsSprinting, unlocked);
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
