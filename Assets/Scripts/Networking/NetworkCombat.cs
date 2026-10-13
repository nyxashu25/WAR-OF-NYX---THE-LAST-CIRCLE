using System.Collections.Generic;
using Fusion;
using UnityEngine;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Match;
using WarOfNyx.Gameplay;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Networking
{
    /// <summary>
    /// Weapons, health and elimination for one player (Phase 5, spec 9/14/15).
    ///
    /// Authority:
    ///   SERVER: owns everything here. Validates fire rate, ammo, reload, switching and melee
    ///     timing; performs LAG-COMPENSATED hit detection (shots are checked against hitboxes
    ///     where the shooter saw them); applies damage (spawn protection, armor); decides
    ///     eliminations and attribution. Clients never report hits or damage.
    ///   OWNING CLIENT: predicts ammo/reload/switch/fire timing and plays its own muzzle flash,
    ///     tracers and sounds immediately (forward ticks only); health and kills come from the server.
    ///   PROXIES: play FX when replicated shot/swing/reload counters change. Purely visual.
    ///
    /// Driven by <see cref="NetworkPlayer.FixedUpdateNetwork"/> after movement, so the order is fixed
    /// and identical on server and client.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayer))]
    public sealed class NetworkCombat : NetworkBehaviour
    {
        public const string BodyHitboxName = "Hitbox_Body";
        public const string HeadHitboxName = "Hitbox_Head";

        /// <summary>Everything except "Ignore Raycast" (layer 2), where player CharacterControllers live,
        /// so shots hit lag-compensated hitboxes instead of current-time capsules.</summary>
        private static readonly int HitMask = ~(1 << 2);

        [Networked] private NetworkWeaponSlot Slot0 { get; set; }
        [Networked] private NetworkWeaponSlot Slot1 { get; set; }
        [Networked] private NetworkWeaponSlot Slot2 { get; set; }
        [Networked] public byte ActiveSlot { get; set; }
        [Networked] private float SwitchReadyAt { get; set; }

        [Networked] private float MeleeSwingStartedAt { get; set; }
        [Networked] private float MeleeNextSwingAt { get; set; }
        [Networked] private int MeleeSwingCount { get; set; }

        [Networked] public float Health { get; set; }
        [Networked] public float MaxHealth { get; set; }
        [Networked] public byte ArmorLevel { get; set; }
        [Networked] public NetworkBool Alive { get; set; }
        [Networked] public int Kills { get; set; }
        [Networked] public float DamageDealt { get; set; }
        [Networked] public PlayerRef EliminatedBy { get; set; }
        [Networked] public short EliminatedByWeapon { get; set; }

        // Presentation counters (replicated so every client plays the same FX once).
        [Networked] private int ShotCount { get; set; }
        [Networked] private int ReloadCount { get; set; }
        [Networked] private float AimPitch { get; set; }

        private readonly List<LagCompensatedHit> _hits = new List<LagCompensatedHit>();
        private readonly Dictionary<NetworkCombat, (float damage, bool headshot)> _pendingDamage = new Dictionary<NetworkCombat, (float, bool)>();
        private readonly Vector3[] _tracerEnds = new Vector3[16];

        private NetworkPlayer _player;
        private HitboxRoot _hitboxRoot;
        private Transform _bodyHitbox;
        private Transform _headHitbox;
        private CharacterController _controller;
        private WeaponVisualRig _rig;
        private int _renderedShots, _renderedSwings, _renderedReloads;
        private short _renderedWeapon = -2;
        private bool _renderedDead;

        public bool IsAlive => Alive;

        /// <summary>Movement speed multiplier of the active weapon (applied by the authority).</summary>
        public float ActiveSpeedMultiplier
        {
            get
            {
                var stats = WeaponRegistry.Get(GetSlot(ActiveSlot).WeaponId);
                return stats != null ? stats.moveSpeedMultiplier : 1f;
            }
        }

        private void Awake()
        {
            _player = GetComponent<NetworkPlayer>();
            _hitboxRoot = GetComponent<HitboxRoot>();
            _controller = GetComponent<CharacterController>();
            var body = transform.Find(BodyHitboxName);
            var head = transform.Find(HeadHitboxName);
            _bodyHitbox = body;
            _headHitbox = head;
        }

        // ------------------------------------------------------------------ spawn

        /// <summary>Server only, before spawn: full health and the Phase 5 development loadout.</summary>
        public void InitializeSpawn()
        {
            MaxHealth = NetworkPlayer.DefaultMaxHealth;
            Health = MaxHealth;
            ArmorLevel = 0; // Phase 6 loadouts grant Level 1 armor
            Alive = true;
            Kills = 0;
            DamageDealt = 0f;
            EliminatedBy = PlayerRef.None;
            EliminatedByWeapon = WeaponCatalog.NoWeapon;

            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                SetSlot(i, WeaponRegistry.CreateState(WeaponCatalog.NetworkIdOf(CombatLoadout.DevelopmentLoadout[i])));
            ActiveSlot = 0;
        }

        public override void Spawned()
        {
            if (!Application.isBatchMode)
                _rig = WeaponVisualRig.Attach(transform, transform.Find("Visual (PLACEHOLDER)"));
            _renderedShots = ShotCount;
            _renderedSwings = MeleeSwingCount;
            _renderedReloads = ReloadCount;
        }

        // ------------------------------------------------------------------ simulation (server + predicting owner)

        public void Simulate(in CombatCommand cmd, float yaw, float pitch, bool aiming, bool lowStance, bool unlocked)
        {
            ApplyLifeState();
            if (!Alive)
                return;

            UpdateHitboxStance(lowStance);
            AimPitch = pitch;
            if (!unlocked)
                return; // nobody fights before "FIGHT!"

            float now = Runner.SimulationTime;
            int active = ActiveSlot;

            // Weapon switching (1/2/3 or next).
            int target = CombatLoadout.ResolveSwitch(active, cmd, i => !GetSlot(i).ToState().IsEmptySlot);
            if (target >= 0)
            {
                var leaving = GetSlot(active).ToState();
                WeaponRules.CancelReload(ref leaving);
                SetSlot(active, leaving);
                MeleeSwingStartedAt = 0f;
                ActiveSlot = (byte)target;
                active = target;
                SwitchReadyAt = now + CombatLoadout.SwitchSeconds;
            }

            var state = GetSlot(active).ToState();
            var weapon = WeaponRegistry.Get(state.WeaponId);
            if (weapon == null)
                return;

            WeaponRules.UpdateReload(ref state, weapon, now);
            if (cmd.ReloadPressed && WeaponRules.TryStartReload(ref state, weapon, now))
                ReloadCount++;

            // Melee: the melee button always swings the melee slot; with the melee weapon out, fire swings too.
            var melee = new MeleeState { SwingStartedAt = MeleeSwingStartedAt, NextSwingAt = MeleeNextSwingAt, SwingCount = MeleeSwingCount };
            var meleeWeapon = WeaponRegistry.Get(GetSlot(CombatLoadout.MeleeSlot).WeaponId);
            bool wantsMelee = cmd.MeleePressed || (weapon.IsMelee && cmd.FirePressed);
            if (wantsMelee && now >= SwitchReadyAt && meleeWeapon != null && MeleeRules.TryStartSwing(ref melee, meleeWeapon, now))
            {
                _player.OnAggressiveAction(AggressiveAction.MeleeAttack); // before resolving: protection never covers an attack
                WeaponRules.CancelReload(ref state);
                if (IsPredictedForward)
                    _rig?.PlaySwing();
            }
            if (meleeWeapon != null && MeleeRules.TryResolve(ref melee, meleeWeapon, now) && HasStateAuthority)
                ResolveMelee(meleeWeapon, yaw);

            // Firearms. Phase 7 supplies the inside-the-circle flag (spec 11); until then always allowed.
            if (weapon.IsRanged && now >= SwitchReadyAt && !melee.InProgress)
            {
                var decision = WeaponRules.TryFire(ref state, weapon, now, cmd.FireHeld, cmd.FirePressed, aiming, firearmsAllowed: true);
                if (decision.Fired)
                {
                    _player.OnAggressiveAction(AggressiveAction.FiredWeapon);
                    ShotCount++;
                    FireShot(weapon, decision, yaw, pitch, lowStance);
                }
                if (decision.ShouldAutoReload && WeaponRules.TryStartReload(ref state, weapon, now))
                    ReloadCount++;
            }

            SetSlot(active, state);
            MeleeSwingStartedAt = melee.SwingStartedAt;
            MeleeNextSwingAt = melee.NextSwingAt;
            MeleeSwingCount = melee.SwingCount;
        }

        private bool IsPredictedForward => HasInputAuthority && Runner.IsForward;

        private void FireShot(WeaponStats weapon, in FireDecision decision, float yaw, float pitch, bool lowStance)
        {
            // Shots start at the camera's shoulder point so the crosshair is exact; if that point is
            // through a wall (peeking exploit), fall back to the character's eye.
            var feet = transform.position;
            AimGeometry.ShoulderOffset(yaw, lowStance, out float sx, out float sy, out float sz);
            var shoulder = feet + new Vector3(sx, sy, sz);
            var eye = feet + Vector3.up * sy;
            var origin = Physics.Linecast(eye, shoulder, HitMask, QueryTriggerInteraction.Ignore) ? eye : shoulder;

            _pendingDamage.Clear();
            int pellets = Mathf.Min(decision.Pellets, _tracerEnds.Length);
            bool anyHit = false;

            for (int p = 0; p < pellets; p++)
            {
                WeaponRules.SpreadOffset(Object.InputAuthority.PlayerId, decision.ShotIndex, p, decision.SpreadDegrees,
                    out float yawOffset, out float pitchOffset);
                AimGeometry.Forward(yaw + yawOffset, pitch + pitchOffset, out float dx, out float dy, out float dz);
                var direction = new Vector3(dx, dy, dz);
                _tracerEnds[p] = origin + direction * weapon.maxRange;

                if (HasStateAuthority)
                {
                    // Lag-compensated: hitboxes are rewound to where this player saw them.
                    if (Runner.LagCompensation.Raycast(origin, direction, weapon.maxRange, Object.InputAuthority, out var hit,
                            HitMask, HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority))
                    {
                        _tracerEnds[p] = hit.Point;
                        anyHit = true;
                        if (hit.Hitbox != null && hit.Hitbox.Root != null)
                        {
                            var target = hit.Hitbox.Root.GetComponent<NetworkCombat>();
                            if (target != null && target != this && target.Alive)
                            {
                                bool headshot = hit.Hitbox.name == HeadHitboxName;
                                float damage = WeaponRules.DamageAt(weapon, hit.Distance, headshot);
                                _pendingDamage.TryGetValue(target, out var sum);
                                _pendingDamage[target] = (sum.damage + damage, sum.headshot || headshot);
                            }
                        }
                    }
                }
                else if (Physics.Raycast(origin, direction, out var local, weapon.maxRange, HitMask, QueryTriggerInteraction.Ignore))
                {
                    _tracerEnds[p] = local.point; // visual estimate only
                    anyHit = true;
                }
            }

            if (HasStateAuthority)
            {
                foreach (var pair in _pendingDamage)
                    DealDamage(pair.Key, pair.Value.damage, DamageKind.Weapon, pair.Value.headshot, GetSlot(ActiveSlot).WeaponId);
            }

            if (IsPredictedForward)
                _rig?.PlayShot(weapon, origin, _tracerEnds, pellets, anyHit);
        }

        private void ResolveMelee(WeaponStats meleeWeapon, float yaw)
        {
            var feet = transform.position;
            AimGeometry.Forward(yaw, 0f, out float fx, out _, out float fz);
            var forward = new Vector3(fx, 0f, fz);
            var centre = feet + Vector3.up * 1.1f + forward * (meleeWeapon.meleeReach * 0.5f);

            Runner.LagCompensation.OverlapSphere(centre, meleeWeapon.meleeReach * 0.6f, Object.InputAuthority, _hits,
                HitMask, HitOptions.IgnoreInputAuthority);

            NetworkCombat best = null;
            float bestDistance = float.MaxValue;
            var chest = feet + Vector3.up * 1.1f;
            foreach (var hit in _hits)
            {
                if (hit.Hitbox == null || hit.Hitbox.Root == null)
                    continue;
                var target = hit.Hitbox.Root.GetComponent<NetworkCombat>();
                if (target == null || target == this || !target.Alive)
                    continue;

                var targetPos = hit.Hitbox.Root.transform.position;
                // Reach is measured to the target's capsule surface, not its centre.
                if (!MeleeRules.InArc(feet.x, feet.z, yaw, targetPos.x, targetPos.z, meleeWeapon.meleeReach + 0.35f, meleeWeapon.meleeArcDegrees))
                    continue;
                // No hits through walls.
                if (Physics.Linecast(chest, targetPos + Vector3.up * 1.1f, HitMask, QueryTriggerInteraction.Ignore))
                    continue;

                float distance = (targetPos - feet).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }

            if (best != null)
                DealDamage(best, meleeWeapon.damage, DamageKind.Melee, false, GetSlot(CombatLoadout.MeleeSlot).WeaponId);
        }

        // ------------------------------------------------------------------ damage (server)

        private void DealDamage(NetworkCombat target, float amount, DamageKind kind, bool headshot, short weaponId)
        {
            var result = target.ReceiveDamage(amount, kind, Object.InputAuthority, weaponId, transform.position);
            if (result.Applied <= 0f)
                return;

            DamageDealt += result.Applied;
            if (result.Killed)
                Kills++;
            RPC_HitConfirmed(headshot, result.Killed);

            if (result.Killed && NetworkMatchController.Instance != null)
                NetworkMatchController.Instance.RegisterElimination(target.Object.InputAuthority, Object.InputAuthority, weaponId, headshot);
        }

        /// <summary>Server only. The single entry point for all damage (weapons now; zone and camping in Phases 7-8).</summary>
        public DamageResult ReceiveDamage(float amount, DamageKind kind, PlayerRef attacker, short weaponId, Vector3 sourcePosition)
        {
            if (!HasStateAuthority)
                return default;

            var health = new HealthState { Health = Health, MaxHealth = MaxHealth, ArmorLevel = ArmorLevel, Alive = Alive };
            var result = HealthRules.ApplyDamage(ref health, amount, kind, _player.IsSpawnProtected);
            Health = health.Health;

            if (result.Applied > 0f)
                RPC_DamageTaken(sourcePosition, result.Applied);

            if (result.Killed)
            {
                Alive = false;
                EliminatedBy = attacker;
                EliminatedByWeapon = weaponId;
                ApplyLifeState();
            }
            return result;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_HitConfirmed(NetworkBool headshot, NetworkBool kill)
        {
            var hud = OnlineArenaController.Instance != null ? OnlineArenaController.Instance.Hud : null;
            hud?.HitMarker(headshot, kill);
            ProceduralAudio.PlayAt(ProceduralAudio.HitTick, transform.position, 0.7f);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_DamageTaken(Vector3 sourcePosition, float amount)
        {
            var hud = OnlineArenaController.Instance != null ? OnlineArenaController.Instance.Hud : null;
            var cam = Camera.main;
            if (hud == null || cam == null)
                return;
            var toSource = sourcePosition - cam.transform.position;
            float relative = Vector3.SignedAngle(Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up), Vector3.ProjectOnPlane(toSource, Vector3.up), Vector3.up);
            hud.DamageTaken(amount, relative);
        }

        // ------------------------------------------------------------------ helpers

        public NetworkWeaponSlot GetSlot(int index)
        {
            switch (index)
            {
                case 0: return Slot0;
                case 1: return Slot1;
                default: return Slot2;
            }
        }

        private void SetSlot(int index, in WeaponState state)
        {
            var slot = NetworkWeaponSlot.From(state);
            switch (index)
            {
                case 0: Slot0 = slot; break;
                case 1: Slot1 = slot; break;
                default: Slot2 = slot; break;
            }
        }

        /// <summary>Hitboxes follow the stance so crouching really makes you smaller (and the head lower).</summary>
        private void UpdateHitboxStance(bool lowStance)
        {
            if (_bodyHitbox != null)
                _bodyHitbox.localPosition = new Vector3(0f, lowStance ? 0.45f : 0.75f, 0f);
            if (_headHitbox != null)
                _headHitbox.localPosition = new Vector3(0f, lowStance ? 1.0f : 1.62f, 0f);
        }

        /// <summary>Dead players can't be hit, can't block movement and stop colliding.</summary>
        private void ApplyLifeState()
        {
            bool alive = Alive;
            if (_hitboxRoot != null && _hitboxRoot.HitboxRootActive != alive)
                _hitboxRoot.HitboxRootActive = alive;
            if (_controller != null && _controller.enabled != alive)
                _controller.enabled = alive;
        }

        // ------------------------------------------------------------------ presentation

        public override void Render()
        {
            ApplyLifeState();
            var activeState = GetSlot(ActiveSlot).ToState();
            var weapon = WeaponRegistry.Get(activeState.WeaponId);

            if (_rig != null)
            {
                if (_renderedWeapon != activeState.WeaponId)
                {
                    _rig.ShowWeapon(weapon);
                    _renderedWeapon = activeState.WeaponId;
                }

                // The owner already played these in prediction; everyone else follows the counters.
                if (!HasInputAuthority)
                {
                    if (ShotCount != _renderedShots && weapon != null && weapon.IsRanged)
                        PlayRemoteShot(weapon);
                    if (MeleeSwingCount != _renderedSwings)
                        _rig.PlaySwing();
                    if (ReloadCount != _renderedReloads)
                        _rig.PlayReload();
                }
                else if (ReloadCount != _renderedReloads)
                {
                    _rig.PlayReload();
                }

                if (!Alive && !_renderedDead)
                    _rig.PlayDefeat();
            }

            _renderedShots = ShotCount;
            _renderedSwings = MeleeSwingCount;
            _renderedReloads = ReloadCount;
            _renderedDead = !Alive;

            if (HasInputAuthority)
                UpdateLocalHud(activeState, weapon);
        }

        private void PlayRemoteShot(WeaponStats weapon)
        {
            float yaw = _player.Movement.Yaw;
            AimGeometry.Forward(yaw, AimPitch, out float dx, out float dy, out float dz);
            var direction = new Vector3(dx, dy, dz);
            var origin = _rig.MuzzlePosition;
            bool hit = Physics.Raycast(origin, direction, out var info, weapon.maxRange, HitMask, QueryTriggerInteraction.Ignore);
            _tracerEnds[0] = hit ? info.point : origin + direction * weapon.maxRange;
            _rig.PlayShot(weapon, origin, _tracerEnds, 1, hit);
        }

        private void UpdateLocalHud(in WeaponState state, WeaponStats weapon)
        {
            var arena = OnlineArenaController.Instance;
            var hud = arena != null ? arena.Hud : null;
            if (hud == null || weapon == null)
                return;

            float now = Runner.SimulationTime;
            hud.SetVitals(Health, MaxHealth);
            hud.SetWeapon(weapon.displayName, weapon.IsRanged, state.Magazine, state.Reserve, WeaponRules.ReloadProgress(state, weapon, now));
            hud.SetSlots(WeaponRegistry.DisplayName(Slot0.WeaponId), WeaponRegistry.DisplayName(Slot1.WeaponId),
                WeaponRegistry.DisplayName(Slot2.WeaponId), ActiveSlot);

            var cam = Camera.main;
            bool aiming = _player.LocalAimHeld;
            float spread = weapon.IsRanged ? WeaponRules.CurrentSpread(state, weapon, now, aiming) : 0.5f;
            hud.SetCrosshair(spread, cam != null ? cam.fieldOfView : 60f, Alive);

            if (!Alive)
            {
                string killer = NameOf(EliminatedBy);
                hud.ShowEliminated(killer, WeaponRegistry.DisplayName(EliminatedByWeapon));
            }
            else
            {
                hud.HideEliminated();
            }
        }

        private string NameOf(PlayerRef player)
        {
            if (player == PlayerRef.None || Runner == null)
                return string.Empty;
            return Runner.TryGetPlayerObject(player, out var obj) && obj.TryGetComponent<NetworkPlayer>(out var p)
                ? p.PlayerName
                : $"Player {player.PlayerId}";
        }
    }
}
