using Fusion;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Networking;
using WarOfNyx.Gameplay;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// Networked player (Phase 3). Runs the shared movement model inside Fusion's tick loop.
    ///
    /// Authority per action:
    ///   SERVER (state authority): simulates movement from received inputs, sanitizes inputs,
    ///     writes <see cref="Movement"/> (authoritative), sanitizes the display name.
    ///   OWNING CLIENT (input authority): sends inputs; PREDICTS the same simulation locally;
    ///     Fusion rolls back to server state and re-simulates when corrections arrive.
    ///   OTHER CLIENTS (proxies): never simulate; show the interpolated transform
    ///     (NetworkTransform) and the replicated stance for animation. Purely visual.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerInputRouter))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>Input source polled by the session manager's OnInput. Only set for this device's player.</summary>
        public static PlayerInputRouter LocalRouter { get; private set; }

        [Networked] public NetworkMovementState Movement { get; set; }
        [Networked] public NetworkString<_16> DisplayName { get; set; }

        private PlayerMovement _movement;
        private PlayerInputRouter _router;
        private TextMesh _nameTag;

        public string PlayerName => DisplayName.Value;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _router = GetComponent<PlayerInputRouter>();
        }

        /// <summary>Server only, before the object exists on the network: initial authoritative state.</summary>
        public void InitializeSpawn(Vector3 position, float yaw, int playerId)
        {
            Movement = NetworkMovementState.From(MovementState.Initial(yaw), false, position);
            DisplayName = PlayerNames.Fallback(playerId);
        }

        public override void Spawned()
        {
            var state = Movement;
            _movement.RestoreState(state.ToState(), state.Grounded, state.Position);
            gameObject.name = $"Player {Object.InputAuthority.PlayerId}{(HasInputAuthority ? " (local)" : string.Empty)}";

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
            if (LocalRouter == _router)
                LocalRouter = null;
            if (OnlineArenaController.Instance != null)
                OnlineArenaController.Instance.UnbindLocalPlayer(_movement);
        }

        public override void FixedUpdateNetwork()
        {
            // Only the authority and the predicting owner simulate; proxies just render.
            if (!HasStateAuthority && !HasInputAuthority)
                return;

            var state = Movement;
            _movement.RestoreState(state.ToState(), state.Grounded, state.Position);

            var command = GetInput(out NetworkInputData input)
                ? input.ToCommand()
                : InputCodec.Neutral(state.Yaw, 0f); // input lost this tick: stand still, gravity still applies

            _movement.Simulate(command, Runner.DeltaTime);
            Movement = NetworkMovementState.From(_movement.State, _movement.IsGrounded, transform.position);
        }

        public override void Render()
        {
            if (!HasStateAuthority && !HasInputAuthority)
                _movement.ApplyVisualState(Movement.ToState());

            if (_nameTag != null)
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
