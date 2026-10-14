using System.Collections.Generic;
using Fusion;
using UnityEngine;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Zone;
using WarOfNyx.Gameplay;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.UI.Common;
using WarOfNyx.UI.Hud;

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
        /// <summary>Combo step of the current / last swing (MeleeRules.Combo): every peer animates the same attack.</summary>
        [Networked] private byte MeleeComboStep { get; set; }

        [Networked] public float Health { get; set; }
        [Networked] public float MaxHealth { get; set; }
        [Networked] public NetworkInventory Inv { get; set; }
        [Networked] private byte HealType { get; set; }
        [Networked] private float HealStartedAt { get; set; }
        [Networked] private float HealEndsAt { get; set; }
        [Networked] public NetworkBool Alive { get; set; }
        [Networked] public int Kills { get; set; }
        [Networked] public float DamageDealt { get; set; }
        [Networked] public PlayerRef EliminatedBy { get; set; }
        [Networked] public short EliminatedByWeapon { get; set; }
        /// <summary>What eliminated this player (<see cref="DamageKind"/>): a weapon, melee, or the Nyx Circle.</summary>
        [Networked] public byte EliminationCause { get; set; }
        /// <summary>Zone exposure not yet charged as damage (server, spec 12).</summary>
        [Networked] private float ZonePending { get; set; }
        [Networked] private NetworkBool ZoneOutside { get; set; }

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
        private float _renderedHealth = -1f;
        private bool _celebrated;

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

        /// <summary>
        /// Server only, before spawn: full health and a randomized, power-budgeted starting loadout
        /// (spec 7): primary + melee + spare ammo + 1-2 medkits + Level 1 armor.
        /// </summary>
        public void InitializeSpawn(int loadoutSeed)
        {
            MaxHealth = NetworkPlayer.DefaultMaxHealth;
            Health = MaxHealth;
            Alive = true;
            Kills = 0;
            DamageDealt = 0f;
            EliminatedBy = PlayerRef.None;
            EliminatedByWeapon = WeaponCatalog.NoWeapon;
            HealEndsAt = 0f;
            EliminationCause = (byte)DamageKind.Weapon;
            ZonePending = 0f;
            ZoneOutside = false;

            var budget = App.AppServices.IsInitialized ? App.AppServices.Config.loadoutBudget : new LoadoutBudget();
            var loadout = LoadoutGenerator.Generate(loadoutSeed, budget, WeaponRegistry.Get);
            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                SetSlot(i, loadout.Slots[i]);
            Inv = NetworkInventory.From(loadout.Inventory);
            ActiveSlot = 0;
            Debug.Log($"[WarOfNyx][Loadout] {loadout.Package.Name} (power {loadout.Power:0}): " +
                      $"{WeaponRegistry.DisplayName(loadout.Slots[0].WeaponId)} + {WeaponRegistry.DisplayName(loadout.Slots[2].WeaponId)}, " +
                      $"medkits S{loadout.Inventory.SmallMedkits}/M{loadout.Inventory.StandardMedkits}, armor L{loadout.Inventory.ArmorLevel}");
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

        /// <param name="firearmsAllowed">False while the body is outside the Nyx Circle (spec 11): guns can't fire,
        /// nothing is spent; melee, medkits, reloading and switching still work.</param>
        public void Simulate(in CombatCommand cmd, float yaw, float pitch, bool aiming, bool lowStance, bool sprinting, bool unlocked,
            bool firearmsAllowed)
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
            var inventory = Inv.ToInventory();
            var heal = new HealState { Type = (MedkitType)HealType, StartedAt = HealStartedAt, EndsAt = HealEndsAt };
            bool attacked = false;

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
            if (weapon != null)
            {
                // Reload draws from the inventory's ammo of this weapon's type.
                if (weapon.IsRanged)
                {
                    int pool = inventory.GetAmmo(weapon.ammoType);
                    if (WeaponRules.UpdateReload(ref state, weapon, now, ref pool))
                        inventory.SetAmmo(weapon.ammoType, pool);
                    if (cmd.ReloadPressed && WeaponRules.TryStartReload(ref state, weapon, now, pool))
                        ReloadCount++;
                }

                // Melee: the melee button always swings the melee slot; with the melee weapon out, fire swings too.
                var melee = new MeleeState
                {
                    SwingStartedAt = MeleeSwingStartedAt, NextSwingAt = MeleeNextSwingAt, SwingCount = MeleeSwingCount, ComboStep = MeleeComboStep,
                };
                var meleeWeapon = WeaponRegistry.Get(GetSlot(CombatLoadout.MeleeSlot).WeaponId);
                bool wantsMelee = cmd.MeleePressed || (weapon.IsMelee && cmd.FirePressed);
                if (wantsMelee && now >= SwitchReadyAt && meleeWeapon != null && MeleeRules.TryStartSwing(ref melee, meleeWeapon, now))
                {
                    _player.OnAggressiveAction(AggressiveAction.MeleeAttack); // before resolving: protection never covers an attack
                    WeaponRules.CancelReload(ref state);
                    attacked = true;
                    if (IsPredictedForward)
                        _rig?.PlaySwing(MeleeRules.Current(melee, meleeWeapon), meleeWeapon);
                }
                if (meleeWeapon != null && MeleeRules.TryResolve(ref melee, meleeWeapon, now) && HasStateAuthority)
                    ResolveMelee(meleeWeapon, MeleeRules.Current(melee, meleeWeapon), yaw);

                // Firearms: only with the body inside the Nyx Circle (spec 11). A blocked attempt costs nothing.
                if (weapon.IsRanged && now >= SwitchReadyAt && !melee.InProgress)
                {
                    int pool = inventory.GetAmmo(weapon.ammoType);
                    var decision = WeaponRules.TryFire(ref state, weapon, now, cmd.FireHeld, cmd.FirePressed, aiming,
                        firearmsAllowed, reserveAvailable: pool);
                    if (decision.Reason == FireBlockReason.FirearmsDisabled && cmd.FirePressed && IsPredictedForward)
                    {
                        LocalMessage("WEAPONS DISABLED - MELEE ONLY OUTSIDE THE CIRCLE", 1.2f);
                        ProceduralAudio.PlayAt(ProceduralAudio.HitTick, transform.position, 0.25f); // dry "denied" click
                    }
                    if (decision.Fired)
                    {
                        _player.OnAggressiveAction(AggressiveAction.FiredWeapon);
                        ShotCount++;
                        attacked = true;
                        FireShot(weapon, decision, yaw, pitch, lowStance);
                    }
                    if (decision.ShouldAutoReload && WeaponRules.TryStartReload(ref state, weapon, now, pool))
                        ReloadCount++;
                }

                MeleeSwingStartedAt = melee.SwingStartedAt;
                MeleeNextSwingAt = melee.NextSwingAt;
                MeleeSwingCount = melee.SwingCount;
                MeleeComboStep = melee.ComboStep;
            }

            // Medkits (spec 15): channelled; sprinting or attacking interrupts; consumed only on completion.
            if (heal.Active && (attacked || sprinting))
                HealingRules.Interrupt(ref heal);
            var health = new HealthState { Health = Health, MaxHealth = MaxHealth, ArmorLevel = inventory.ArmorLevel, Alive = Alive };
            if (cmd.MedkitPressed && !heal.Active)
            {
                var choice = HealingRules.ChooseMedkit(inventory, MaxHealth - Health);
                var reason = choice.HasValue
                    ? HealingRules.TryStart(ref heal, inventory, choice.Value, health, now)
                    : HealBlockReason.NoMedkit;
                if (reason != HealBlockReason.None && IsPredictedForward)
                    LocalMessage(reason == HealBlockReason.FullHealth ? "Health is already full." : "No medkits.");
            }
            if (HealingRules.TryComplete(ref heal, ref inventory, ref health, now) > 0f)
                Health = health.Health;

            SetSlot(active, state);
            Inv = NetworkInventory.From(inventory);
            HealType = (byte)heal.Type;
            HealStartedAt = heal.StartedAt;
            HealEndsAt = heal.EndsAt;
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

        private void ResolveMelee(WeaponStats meleeWeapon, in MeleeAttack attack, float yaw)
        {
            var feet = transform.position;
            float reach = MeleeRules.Reach(meleeWeapon, attack);
            // Gather candidates around the attacker (spins hit all around), then apply the attack's arc.
            var centre = feet + Vector3.up * 1.1f;
            Runner.LagCompensation.OverlapSphere(centre, reach + 0.6f, Object.InputAuthority, _hits,
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
                if (!MeleeRules.InArc(feet.x, feet.z, yaw, targetPos.x, targetPos.z, reach + 0.35f, attack.ArcDegrees))
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
                DealDamage(best, MeleeRules.Damage(meleeWeapon, attack), DamageKind.Melee, false, GetSlot(CombatLoadout.MeleeSlot).WeaponId);
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
                NetworkMatchController.Instance.RegisterElimination(target.Object.InputAuthority, Object.InputAuthority, weaponId, headshot, kind);
        }

        /// <summary>
        /// Server only, once per tick while the circle is active: zone damage earned this tick for time
        /// spent outside (spec 12). Pulses of damage-per-second × interval; stops on re-entry.
        /// Apply it through <see cref="ApplyEnvironmentDamage"/> together with camping damage.
        /// </summary>
        public float StepZoneExposure(bool inside, float damagePerSecond, float dt, float interval)
        {
            if (!HasStateAuthority || !Alive)
                return 0f;
            var exposure = new ZoneExposure { Pending = ZonePending, Outside = ZoneOutside };
            var step = ZoneDamage.Step(ref exposure, inside, damagePerSecond, dt, interval);
            ZonePending = exposure.Pending;
            ZoneOutside = exposure.Outside;
            return step.Damage;
        }

        /// <summary>
        /// Server only: the single environmental damage pass of a tick (spec 13). Zone and camping
        /// damage stack additively in ONE application, so neither can hit twice in the same tick.
        /// Both ignore armor. An elimination is attributed to the larger contributor.
        /// </summary>
        /// <returns>True when this eliminated the player.</returns>
        public bool ApplyEnvironmentDamage(float zoneDamage, float campingDamage)
        {
            float total = EnvironmentDamage.Total(zoneDamage, campingDamage);
            if (!HasStateAuthority || !Alive || total <= 0f)
                return false;

            var cause = EnvironmentDamage.Cause(zoneDamage, campingDamage);
            var result = ReceiveDamage(total, cause, PlayerRef.None, WeaponCatalog.NoWeapon, transform.position);
            if (result.Killed && NetworkMatchController.Instance != null)
                NetworkMatchController.Instance.RegisterElimination(Object.InputAuthority, PlayerRef.None, WeaponCatalog.NoWeapon, false, cause);
            return result.Killed;
        }

        /// <summary>Server only. The single entry point for all damage (weapons, melee, the Nyx Circle, camping).</summary>
        public DamageResult ReceiveDamage(float amount, DamageKind kind, PlayerRef attacker, short weaponId, Vector3 sourcePosition)
        {
            if (!HasStateAuthority)
                return default;

            var health = new HealthState { Health = Health, MaxHealth = MaxHealth, ArmorLevel = Inv.ArmorLevel, Alive = Alive };
            var result = HealthRules.ApplyDamage(ref health, amount, kind, _player.IsSpawnProtected);
            Health = health.Health;

            // Direction indicator for player damage only; zone damage is shown by the outside-circle effects.
            if (result.Applied > 0f && (kind == DamageKind.Weapon || kind == DamageKind.Melee))
                RPC_DamageTaken(sourcePosition, result.Applied);

            if (result.Killed)
            {
                Alive = false;
                EliminatedBy = attacker;
                EliminatedByWeapon = weaponId;
                EliminationCause = (byte)kind;
                HealEndsAt = 0f;
                ApplyLifeState();
                DropLoot();
            }
            return result;
        }

        /// <summary>
        /// Server: move everything this player carried into a loot capsule at the body (spec 14).
        /// Items are MOVED: the player's slots and inventory are cleared in the same step, so
        /// nothing can be duplicated.
        /// </summary>
        private void DropLoot()
        {
            var slots = new[] { Slot0.ToState(), Slot1.ToState(), Slot2.ToState() };
            var contents = LootContainer.FromEliminated(slots, Inv.ToInventory());
            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                SetSlot(i, WeaponState.Empty);
            Inv = default;

            var crate = NetworkLootCrate.SpawnFor(Runner, transform.position, contents, _player.PlayerName);
            if (crate != null)
                Debug.Log($"[WarOfNyx][Loot] {_player.PlayerName} dropped a capsule with {contents.Count} items.");
        }

        // ------------------------------------------------------------------ looting (server-validated)

        /// <summary>
        /// Owner → server: take one item (index 0-11) or everything that fits (index -1) from a capsule.
        /// The server re-checks: alive, capsule exists, within reach, item still there. Claims run one
        /// at a time on the server, so two players can never both get the same item.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_TakeLoot(NetworkId crateId, int index)
        {
            if (!Alive)
                return;
            if (!Runner.TryFindObject(crateId, out var obj) || !obj.TryGetComponent<NetworkLootCrate>(out var crate))
            {
                RPC_LootResult("That capsule is gone.");
                return;
            }
            if ((crate.transform.position - transform.position).sqrMagnitude > NetworkLootCrate.ServerRange * NetworkLootCrate.ServerRange)
            {
                RPC_LootResult("Too far away.");
                return;
            }

            var container = crate.ToContainer();
            var slots = new[] { Slot0.ToState(), Slot1.ToState(), Slot2.ToState() };
            var inventory = Inv.ToInventory();
            int active = ActiveSlot;
            var result = LootClaims.Take(container, slots, ref active, ref inventory, index, WeaponRegistry.Get);
            if (result.TookCombatItem)
                _player.OnAggressiveAction(AggressiveAction.PickedUpCombatItem); // spec 6: ends spawn protection

            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                SetSlot(i, slots[i]);
            ActiveSlot = (byte)active;
            Inv = NetworkInventory.From(inventory);
            crate.Store(container);
            RPC_LootResult(result.Message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RPC_LootResult(string message)
        {
            if (!string.IsNullOrEmpty(message))
                LocalMessage(message);
        }

        private static void LocalMessage(string message, float seconds = 2.5f)
        {
            var arena = OnlineArenaController.Instance;
            if (arena != null && arena.Hud != null)
                arena.Hud.ShowMessage(message, seconds);
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
            // Screen blood (critical hits); the server doesn't send headshot info for received hits, so big hits / low health count.
            if (_blood == null && OnlineArenaController.Instance != null)
                _blood = UI.Hud.BloodOverlay.Create(OnlineArenaController.Instance.transform);
            _blood?.OnDamaged(amount, false, Health, MaxHealth);
        }

        private static UI.Hud.BloodOverlay _blood;

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
                    {
                        var remoteMelee = WeaponRegistry.Get(GetSlot(CombatLoadout.MeleeSlot).WeaponId);
                        if (remoteMelee != null)
                            _rig.PlaySwing(MeleeRules.Current(new MeleeState { ComboStep = MeleeComboStep }, remoteMelee), remoteMelee);
                    }
                    if (ReloadCount != _renderedReloads)
                        _rig.PlayReload();
                }
                else if (ReloadCount != _renderedReloads)
                {
                    _rig.PlayReload();
                }

                if (!Alive && !_renderedDead)
                {
                    _rig.PlayDefeat();
                    EliminationEffect.Play(transform.position);
                }
                if (_renderedHealth >= 0f && Health < _renderedHealth - 0.01f && Alive)
                    _rig.PlayHurt();
                _rig.SetHealing(Alive && HealEndsAt > 0f);
                _rig.SetAiming(Alive && HasInputAuthority && _player.LocalAimHeld);
                var match = NetworkMatchController.Instance;
                if (!_celebrated && match != null && match.Phase == MatchPhase.Ended && _player.IsWinner)
                {
                    _celebrated = true;
                    _rig.PlayVictory();
                    ProceduralAudio.PlayAt(ProceduralAudio.Victory, transform.position, HasInputAuthority ? 1f : 0.6f);
                }
            }

            _renderedShots = ShotCount;
            _renderedSwings = MeleeSwingCount;
            _renderedReloads = ReloadCount;
            _renderedDead = !Alive;
            _renderedHealth = Health;

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
            if (hud == null)
                return;

            float now = Runner.SimulationTime;
            var inventory = Inv.ToInventory();
            bool ranged = weapon != null && weapon.IsRanged;
            int spare = ranged ? inventory.GetAmmo(weapon.ammoType) : 0;

            hud.SetVitals(Health, MaxHealth);
            _blood?.SetHealth(Health, MaxHealth, Alive);
            hud.SetWeapon(weapon != null ? weapon.displayName : "Unarmed", ranged, state.Magazine, spare,
                weapon != null ? WeaponRules.ReloadProgress(state, weapon, now) : 0f);
            hud.SetInventory($"ARMOR L{inventory.ArmorLevel}     MEDKITS  S{inventory.SmallMedkits}  M{inventory.StandardMedkits}  L{inventory.AdvancedMedkits}     " +
                             (ranged ? AmmoInfo.DisplayName(weapon.ammoType).ToUpperInvariant() + " " + spare : string.Empty));
            var heal = new HealState { Type = (MedkitType)HealType, StartedAt = HealStartedAt, EndsAt = HealEndsAt };
            hud.SetHealing(HealingRules.Progress(heal, now), Medkits.DisplayName(heal.Type));
            hud.SetSlots(WeaponRegistry.DisplayName(Slot0.WeaponId), WeaponRegistry.DisplayName(Slot1.WeaponId),
                WeaponRegistry.DisplayName(Slot2.WeaponId), ActiveSlot);

            var cam = Camera.main;
            float spread = ranged ? WeaponRules.CurrentSpread(state, weapon, now, _player.LocalAimHeld) : 0.5f;
            hud.SetCrosshair(spread, cam != null ? cam.fieldOfView : 60f, Alive);

            if (!Alive)
            {
                // Spec 21: who, final kill count, time survived (and placement once the server gave it).
                var match = NetworkMatchController.Instance;
                int participants = match != null ? match.Participants : 0;
                string details = $"{Kills} ELIMINATIONS  ·  SURVIVED {MatchResultText.Clock(_player.SurvivalSeconds)}" +
                                 (_player.Placement > 0 ? $"  ·  {MatchResultText.Placement(_player.Placement, participants)}" : string.Empty);
                var cause = (DamageKind)EliminationCause;
                if (cause == DamageKind.Zone)
                    hud.ShowEliminated(null, null, "Consumed by the Nyx Circle.", details);
                else if (cause == DamageKind.Camping)
                    hud.ShowEliminated(null, null, "Taken by the Nyx Hunt (camping).", details);
                else
                    hud.ShowEliminated(NameOf(EliminatedBy), WeaponRegistry.DisplayName(EliminatedByWeapon), null, details);
            }
            else
                hud.HideEliminated();

            UpdateLocalLoot(arena, hud);
        }

        // ------------------------------------------------------------------ local loot interaction (client UI only)

        private NetworkLootCrate _nearbyCrate;
        private NetworkLootCrate _openCrate;
        private bool _lootHooked;
        private readonly List<LootEntryView> _lootEntries = new List<LootEntryView>();

        private void HookLootUi(OnlineArenaController arena)
        {
            if (_lootHooked || arena.Loot == null)
                return;
            _lootHooked = true;
            var router = GetComponent<Characters.PlayerInputRouter>();
            router.InteractPressed += OnInteractPressed;
            arena.Loot.TakeRequested += i => { if (_openCrate != null) RPC_TakeLoot(_openCrate.Object.Id, i); };
            arena.Loot.TakeAllRequested += () => { if (_openCrate != null) RPC_TakeLoot(_openCrate.Object.Id, -1); };
            arena.Loot.CloseRequested += CloseLoot;
            arena.ModalCloseRequested += CloseLoot;
        }

        private void OnInteractPressed()
        {
            var arena = OnlineArenaController.Instance;
            if (arena == null || arena.MenuOpen || !HasInputAuthority)
                return;
            if (_openCrate != null)
                CloseLoot();
            else if (_nearbyCrate != null && Alive)
                OpenLoot(_nearbyCrate);
        }

        private void OpenLoot(NetworkLootCrate crate)
        {
            var arena = OnlineArenaController.Instance;
            _openCrate = crate;
            arena.Loot.Open(crate.OwnerName.Value.ToUpperInvariant() + " - LOOT CAPSULE");
            arena.SetModalOpen(true);
        }

        private void CloseLoot()
        {
            var arena = OnlineArenaController.Instance;
            _openCrate = null;
            if (arena == null)
                return;
            if (arena.Loot != null)
                arena.Loot.Close();
            arena.SetModalOpen(false);
        }

        private void UpdateLocalLoot(OnlineArenaController arena, CombatHud hud)
        {
            HookLootUi(arena);

            // Nearest capsule within reach (client-side, for the prompt only; the server re-checks).
            _nearbyCrate = null;
            float best = NetworkLootCrate.InteractRange * NetworkLootCrate.InteractRange;
            if (Alive)
            {
                foreach (var crate in NetworkLootCrate.Active)
                {
                    if (crate == null)
                        continue;
                    float d = (crate.transform.position - transform.position).sqrMagnitude;
                    if (d <= best)
                    {
                        best = d;
                        _nearbyCrate = crate;
                    }
                }
            }

            if (_openCrate != null && (_openCrate != _nearbyCrate || !Alive))
                CloseLoot(); // walked away, capsule emptied/expired, or eliminated

            string key = Application.isMobilePlatform ? "USE" : "E";
            hud.SetPrompt(_nearbyCrate != null && _openCrate == null
                ? $"[{key}] OPEN LOOT - {_nearbyCrate.OwnerName.Value}  ({Mathf.CeilToInt(_nearbyCrate.SecondsLeft)}s)"
                : string.Empty);

            if (_openCrate != null && arena.Loot != null)
                RefreshLootPanel(arena.Loot);
        }

        private void RefreshLootPanel(LootPanel panel)
        {
            var slots = new[] { Slot0.ToState(), Slot1.ToState(), Slot2.ToState() };
            var inventory = Inv.ToInventory();
            var crate = _openCrate;
            LootViews.Build(crate.Get, slots, ActiveSlot, inventory, _lootEntries);
            panel.SetEntries(_lootEntries);
            panel.SetInventorySummary(LootViews.Summary(slots, inventory, crate.SecondsLeft));
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
