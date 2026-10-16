using UnityEngine;
using WarOfNyx.Characters;
using WarOfNyx.Combat;
using WarOfNyx.Core.Bots;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Zone;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>
    /// One competitor in an offline Ghost Protocol match: the human or a bot. Spec 38.14: both are
    /// driven by the SAME commands (<see cref="MovementCommand"/> + <see cref="CombatCommand"/>) and
    /// the SAME shared rules as online play (movement model, weapon/melee/health/healing/inventory
    /// rules, circle, Nyx Hunt). The local <see cref="OfflineMatch"/> is the authority; nothing here
    /// knows whether a human or an AI produced the commands.
    ///
    /// Hits are resolved against the same hitbox shapes as online (body capsule + head sphere,
    /// stance-adjusted). Character capsules live on "Ignore Raycast" so walls are tested with physics
    /// and characters with these shapes.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class OfflineCombatant : MonoBehaviour
    {
        public const float BodyRadius = 0.35f;
        public const float BodyHalfSegment = 0.35f;
        public const float HeadRadius = 0.2f;
        private static readonly int EnvironmentMask = ~(1 << 2);
        private static readonly Vector3[] TracerEnds = new Vector3[16];

        public int Id { get; private set; }
        public string DisplayName { get; private set; } = string.Empty;
        public bool IsBot => Bot != null;
        public BotController Bot { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerInputRouter Router { get; private set; }
        public string LoadoutName { get; private set; } = string.Empty;

        public readonly WeaponState[] Slots = new WeaponState[CombatLoadout.SlotCount];
        public int ActiveSlot;
        public float SwitchReadyAt;
        [System.NonSerialized] public PlayerInventory Inventory;
        [System.NonSerialized] public HealthState Health;
        [System.NonSerialized] public HealState Heal;
        [System.NonSerialized] public MeleeState Melee;
        [System.NonSerialized] public ZoneExposure Zone;
        [System.NonSerialized] public SpawnProtectionState Protection;
        public bool OutsideZone;

        public int Kills;
        public float DamageDealt;
        public int ShotsFired;
        public int ShotsHit;
        public float LastHurtAt = -999f;
        public Vector3 LastHurtFrom;
        public float LastShotAt = -999f;
        public float Pitch;

        public DamageKind EliminationCause;
        public int EliminatedBy = -1;
        public short EliminatedByWeapon = WeaponCatalog.NoWeapon;
        public float EliminatedAt;

        private OfflineMatch _match;
        private WeaponVisualRig _rig;
        private OutsideZoneVisual _outsideVisual;
        private HuntBeaconVisual _beacon;
        private ICharacterPose _animator;
        private SpawnShieldVisual _shield;
        private short _shownWeapon = -2;
        private float _nextFootstepAt;

        public bool Alive => Health.Alive;
        public bool LowStance => Movement.State.IsLowStance;
        public bool Moving => Movement.State.HorizontalSpeed > 0.6f;
        public Vector3 Feet => transform.position;
        public Vector3 Eye => transform.position + Vector3.up * (LowStance ? 1.0f : 1.6f);
        public Vector3 Chest => transform.position + Vector3.up * (LowStance ? 0.6f : 1.1f);
        public float Yaw => Movement.State.Yaw;
        public WeaponStats ActiveWeapon => WeaponRegistry.Get(Slots[ActiveSlot].WeaponId);
        public bool FiredRecently(float now) => now - LastShotAt < 1f;
        public bool HurtRecently(float now) => now - LastHurtAt < 2f;

        /// <summary>Movement speed multiplier of the active weapon (authority-applied, like online).</summary>
        public float ActiveSpeedMultiplier => ActiveWeapon != null ? ActiveWeapon.moveSpeedMultiplier : 1f;

        public CharacterLook Look { get; private set; }

        /// <summary>The vehicle this character rides (set by the match), and the seat (0 = driver).</summary>
        public OfflineVehicle Vehicle { get; internal set; }
        public int Seat { get; internal set; } = -1;
        public bool Driving => Vehicle != null && Seat == 0;

        /// <summary>Carried by a vehicle seat (the vehicle calls this every frame).</summary>
        internal void PlaceInSeat(Vector3 feet, Quaternion rotation) => Movement.Seat(feet, rotation);

        public void Initialize(OfflineMatch match, int id, string displayName, StartingLoadout loadout, Vector3 position, float yaw,
            float protectionEndsAt, BotController bot, CharacterLook look)
        {
            _match = match;
            Id = id;
            DisplayName = displayName;
            Bot = bot;
            Movement = GetComponent<PlayerMovement>();
            Router = GetComponent<PlayerInputRouter>();

            // The local match drives the simulation at a fixed rate; the practice loop must not run too.
            var practiceLoop = GetComponent<PlayerController>();
            if (practiceLoop != null)
                practiceLoop.enabled = false;
            if (Router != null)
            {
                Router.enabled = bot == null;
                if (bot == null)
                    Router.SetAim(yaw, 10f);
            }

            gameObject.layer = 2; // capsule ignored by shots; hit shapes are tested explicitly
            gameObject.name = (bot != null ? "Bot " : "Human ") + displayName;
            Movement.Teleport(position, yaw);

            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                Slots[i] = loadout.Slots[i];
            Inventory = loadout.Inventory;
            LoadoutName = loadout.Package != null ? loadout.Package.Name : string.Empty;
            ActiveSlot = 0;
            Health = HealthState.Full(100f, Inventory.ArmorLevel);
            Protection = new SpawnProtectionState { EndsAt = protectionEndsAt, Broken = false };

            if (!Application.isBatchMode)
            {
                Look = look;
                CharacterStyler.Apply(transform.Find("Visual (PLACEHOLDER)"), look);
                _rig = WeaponVisualRig.Attach(transform, transform.Find("Visual (PLACEHOLDER)"));
                _outsideVisual = OutsideZoneVisual.Attach(transform);
                _beacon = HuntBeaconVisual.Attach(transform);
                _shield = SpawnShieldVisual.Attach(transform);
                SpawnEffect.Play(position - Vector3.up * 1.2f);
                // No floating name tags: they draw through walls and would give hidden bots away (spec 38.5).
            }
        }

        // ------------------------------------------------------------------ simulation (called by OfflineMatch, fixed rate)

        /// <summary>
        /// One authoritative tick, in the same order as the online player: classify against the circle,
        /// apply the outside wobble, move, re-classify, then combat with the firearm lock.
        /// </summary>
        public void Simulate(MovementCommand move, in CombatCommand combat, bool unlocked, float now, float dt, int tick,
            in ZoneCircle circle, bool zoneLive, Core.Zone.ZoneSettings zoneSettings)
        {
            if (!Alive)
                return;

            move = Core.Networking.InputCodec.Sanitize(move); // authority-only fields are never taken from a controller
            if (!unlocked)
                move = Core.Networking.InputCodec.Neutral(move.Yaw, move.Pitch);

            // Riding: the vehicle carries the character; no walking, weapons or medkits until they step out.
            if (Vehicle != null)
            {
                Pitch = move.Pitch;
                OutsideZone = zoneLive && !circle.Contains(transform.position.x, transform.position.z);
                if (Heal.Active)
                    HealingRules.Interrupt(ref Heal);
                return;
            }

            bool outsideBefore = zoneLive && !circle.Contains(transform.position.x, transform.position.z);
            move.SpeedMultiplier = ActiveSpeedMultiplier;
            move.LateralWobble = outsideBefore ? ZoneInstability.Lateral(tick, dt, Id, zoneSettings) : 0f;
            Movement.Simulate(move, dt);
            Pitch = move.Pitch;

            OutsideZone = zoneLive && !circle.Contains(transform.position.x, transform.position.z);
            float yaw = move.Yaw, pitch = move.Pitch;
            if (OutsideZone)
            {
                ZoneInstability.AimSway(tick, dt, Id, zoneSettings, out float sy, out float sp);
                yaw += sy;
                pitch += sp;
            }

            if (Movement.State.IsSprinting && now >= _nextFootstepAt)
            {
                _nextFootstepAt = now + 0.5f;
                _match.EmitSound(SoundKind.Footsteps, transform.position, Id);
            }

            if (unlocked)
                SimulateCombat(combat, yaw, pitch, move.AimHeld, now, firearmsAllowed: !OutsideZone);
        }

        private void SimulateCombat(in CombatCommand cmd, float yaw, float pitch, bool aiming, float now, bool firearmsAllowed)
        {
            bool attacked = false;
            int target = CombatLoadout.ResolveSwitch(ActiveSlot, cmd, i => !Slots[i].IsEmptySlot);
            if (target >= 0)
            {
                WeaponRules.CancelReload(ref Slots[ActiveSlot]);
                Melee.SwingStartedAt = 0f;
                ActiveSlot = target;
                SwitchReadyAt = now + CombatLoadout.SwitchSeconds;
            }

            var weapon = ActiveWeapon;
            if (weapon != null)
            {
                if (weapon.IsRanged)
                {
                    int pool = Inventory.GetAmmo(weapon.ammoType);
                    if (WeaponRules.UpdateReload(ref Slots[ActiveSlot], weapon, now, ref pool))
                        Inventory.SetAmmo(weapon.ammoType, pool);
                    if (cmd.ReloadPressed && WeaponRules.TryStartReload(ref Slots[ActiveSlot], weapon, now, pool))
                        OnReloadStarted();
                }

                var meleeWeapon = WeaponRegistry.Get(Slots[CombatLoadout.MeleeSlot].WeaponId);
                bool wantsMelee = cmd.MeleePressed || (weapon.IsMelee && cmd.FirePressed);
                if (wantsMelee && now >= SwitchReadyAt && meleeWeapon != null && MeleeRules.TryStartSwing(ref Melee, meleeWeapon, now))
                {
                    Protection.OnAggressiveAction(AggressiveAction.MeleeAttack);
                    WeaponRules.CancelReload(ref Slots[ActiveSlot]);
                    attacked = true;
                    _rig?.PlaySwing(MeleeRules.Current(Melee, meleeWeapon), meleeWeapon);
                    _match.EmitSound(SoundKind.Melee, transform.position, Id);
                }
                if (meleeWeapon != null && MeleeRules.TryResolve(ref Melee, meleeWeapon, now))
                    ResolveMelee(meleeWeapon, yaw, now);

                if (weapon.IsRanged && now >= SwitchReadyAt && !Melee.InProgress)
                {
                    int pool = Inventory.GetAmmo(weapon.ammoType);
                    var decision = WeaponRules.TryFire(ref Slots[ActiveSlot], weapon, now, cmd.FireHeld, cmd.FirePressed, aiming,
                        firearmsAllowed, pool);
                    if (decision.Fired)
                    {
                        Protection.OnAggressiveAction(AggressiveAction.FiredWeapon);
                        attacked = true;
                        FireShot(weapon, decision, yaw, pitch, now);
                    }
                    else if (decision.Reason == FireBlockReason.FirearmsDisabled && cmd.FirePressed && !IsBot)
                    {
                        _match.LocalMessage("WEAPONS DISABLED - MELEE ONLY OUTSIDE THE CIRCLE");
                    }
                    else if (decision.Reason == FireBlockReason.EmptyMagazine && cmd.FirePressed && !decision.ShouldAutoReload)
                    {
                        _rig?.PlayDryFire();
                    }
                    if (decision.ShouldAutoReload && WeaponRules.TryStartReload(ref Slots[ActiveSlot], weapon, now, pool))
                        OnReloadStarted();
                }
            }

            // Medkits: channelled; sprinting or attacking interrupts; consumed only on completion (same rules as online).
            if (Heal.Active && (attacked || Movement.State.IsSprinting))
                HealingRules.Interrupt(ref Heal);
            if (cmd.MedkitPressed && !Heal.Active)
            {
                var choice = HealingRules.ChooseMedkit(Inventory, Health.MaxHealth - Health.Health);
                var reason = choice.HasValue ? HealingRules.TryStart(ref Heal, Inventory, choice.Value, Health, now) : HealBlockReason.NoMedkit;
                if (reason != HealBlockReason.None && !IsBot)
                    _match.LocalMessage(reason == HealBlockReason.FullHealth ? "Health is already full." : "No medkits.");
            }
            if (HealingRules.TryComplete(ref Heal, ref Inventory, ref Health, now) > 0f && !IsBot)
                ProceduralAudio.PlayAt(ProceduralAudio.Heal, transform.position, 0.7f);
        }

        /// <summary>The winner celebrates (victory presentation, visual only).</summary>
        public void Celebrate() => _rig?.PlayVictory();

        private void OnReloadStarted()
        {
            _rig?.PlayReload();
            _match.EmitSound(SoundKind.Reload, transform.position, Id);
        }

        private void FireShot(WeaponStats weapon, in FireDecision decision, float yaw, float pitch, float now)
        {
            ShotsFired++;
            LastShotAt = now;
            _match.EmitSound(weapon.ammoType == AmmoType.Marksman || weapon.ammoType == AmmoType.Shell ? SoundKind.HeavyGunshot : SoundKind.Gunshot,
                transform.position, Id);

            AimGeometry.ShoulderOffset(yaw, LowStance, out float sx, out float sy, out float sz);
            var feet = transform.position;
            var shoulder = feet + new Vector3(sx, sy, sz);
            var eye = feet + Vector3.up * sy;
            var origin = Physics.Linecast(eye, shoulder, EnvironmentMask, QueryTriggerInteraction.Ignore) ? eye : shoulder;

            int pellets = Mathf.Min(decision.Pellets, TracerEnds.Length);
            bool anyHit = false, anyCharacter = false;
            OfflineCombatant lastTarget = null;
            float damageSum = 0f;
            bool headshotAny = false;

            for (int p = 0; p < pellets; p++)
            {
                WeaponRules.SpreadOffset(Id, decision.ShotIndex, p, decision.SpreadDegrees, out float yo, out float po);
                AimGeometry.Forward(yaw + yo, pitch + po, out float dx, out float dy, out float dz);
                var direction = new Vector3(dx, dy, dz);
                float wall = weapon.maxRange;
                if (Physics.Raycast(origin, direction, out var hit, weapon.maxRange, EnvironmentMask, QueryTriggerInteraction.Ignore))
                {
                    wall = hit.distance;
                    anyHit = true;
                }
                TracerEnds[p] = origin + direction * wall;

                var victim = _match.RaycastCharacters(origin, direction, wall, this, out float distance, out bool headshot);
                if (victim == null)
                {
                    // The pellet stopped on a vehicle: the vehicle takes the damage.
                    if (wall < weapon.maxRange && hit.collider != null && hit.collider.attachedRigidbody != null &&
                        hit.collider.attachedRigidbody.TryGetComponent<OfflineVehicle>(out var vehicle))
                        _match.ApplyVehicleDamage(this, vehicle, WeaponRules.DamageAt(weapon, wall, false));
                    continue;
                }
                anyHit = anyCharacter = true;
                TracerEnds[p] = origin + direction * distance;
                // Pellets on several targets are applied per target; most shots hit one.
                if (lastTarget != null && lastTarget != victim)
                {
                    _match.ApplyWeaponDamage(this, lastTarget, damageSum, headshotAny, Slots[ActiveSlot].WeaponId);
                    damageSum = 0f;
                    headshotAny = false;
                }
                lastTarget = victim;
                damageSum += WeaponRules.DamageAt(weapon, distance, headshot);
                headshotAny |= headshot;
            }

            if (lastTarget != null)
                _match.ApplyWeaponDamage(this, lastTarget, damageSum, headshotAny, Slots[ActiveSlot].WeaponId);
            if (anyCharacter)
                ShotsHit++;

            _rig?.PlayShot(weapon, origin, TracerEnds, pellets, anyHit);
        }

        private void ResolveMelee(WeaponStats meleeWeapon, float yaw, float now)
        {
            // The combo step decides the arc, reach and damage (same rule as online).
            var attack = MeleeRules.Current(Melee, meleeWeapon);
            var target = _match.FindMeleeTarget(this, MeleeRules.Reach(meleeWeapon, attack), attack.ArcDegrees, yaw);
            if (target != null)
                _match.ApplyMeleeDamage(this, target, MeleeRules.Damage(meleeWeapon, attack), Slots[CombatLoadout.MeleeSlot].WeaponId);
        }

        // ------------------------------------------------------------------ hit shapes (same as the online hitboxes)

        /// <summary>Distance along the ray to this character's head or body, or false.</summary>
        public bool Intersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance, out bool headshot)
        {
            distance = 0f;
            headshot = false;
            if (!Alive)
                return false;
            var feet = transform.position;
            var head = feet + Vector3.up * (LowStance ? 1.0f : 1.62f);
            if (RaySphere(origin, direction, head, HeadRadius, out float th) && th <= maxDistance)
            {
                distance = th;
                headshot = true;
            }
            var bodyCenter = feet + Vector3.up * (LowStance ? 0.45f : 0.75f);
            if (RayCapsule(origin, direction, bodyCenter - Vector3.up * BodyHalfSegment, bodyCenter + Vector3.up * BodyHalfSegment, BodyRadius,
                    out float tb) && tb <= maxDistance && (!headshot || tb < distance))
            {
                distance = tb;
                headshot = false;
            }
            return distance > 0f;
        }

        private static bool RaySphere(Vector3 o, Vector3 d, Vector3 c, float r, out float t)
        {
            t = 0f;
            var m = o - c;
            float b = Vector3.Dot(m, d);
            float cc = Vector3.Dot(m, m) - r * r;
            if (cc > 0f && b > 0f)
                return false;
            float disc = b * b - cc;
            if (disc < 0f)
                return false;
            t = Mathf.Max(0.001f, -b - Mathf.Sqrt(disc));
            return true;
        }

        private static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
        {
            // Closest approach between the ray and the capsule's segment.
            t = 0f;
            var u = d;
            var v = b - a;
            var w = o - a;
            float uu = 1f, uv = Vector3.Dot(u, v), vv = Vector3.Dot(v, v), uw = Vector3.Dot(u, w), vw = Vector3.Dot(v, w);
            float denom = uu * vv - uv * uv;
            float s = denom > 1e-6f ? (uv * vw - vv * uw) / denom : 0f;
            s = Mathf.Max(0f, s);
            float q = Mathf.Clamp01((uv * s + vw) / Mathf.Max(vv, 1e-6f));
            var onRay = o + u * s;
            var onSeg = a + v * q;
            float dist = Vector3.Distance(onRay, onSeg);
            if (dist > r)
                return false;
            t = Mathf.Max(0.001f, s - Mathf.Sqrt(Mathf.Max(0f, r * r - dist * dist)));
            return true;
        }

        // ------------------------------------------------------------------ damage (called by the match, the single entry point)

        public DamageResult ReceiveDamage(float amount, DamageKind kind, OfflineCombatant attacker, short weaponId, Vector3 source, float now)
        {
            Health.ArmorLevel = Inventory.ArmorLevel;
            var result = HealthRules.ApplyDamage(ref Health, amount, kind, Protection.IsActive(now));
            if (result.Applied > 0f && (kind == DamageKind.Weapon || kind == DamageKind.Melee))
            {
                LastHurtAt = now;
                LastHurtFrom = source;
            }
            if (result.Applied > 0f)
            {
                _rig?.PlayHurt();
                if (!IsBot)
                    ProceduralAudio.PlayAt(ProceduralAudio.Hurt, transform.position, 0.6f);
            }
            if (result.Killed)
            {
                EliminationCause = kind;
                EliminatedBy = attacker != null ? attacker.Id : -1;
                EliminatedByWeapon = weaponId;
                EliminatedAt = now;
                Heal = default;
                Movement.Controller.enabled = false; // body stops colliding
                _rig?.PlayDefeat();
                if (!Application.isBatchMode)
                    EliminationEffect.Play(transform.position);
            }
            return result;
        }

        // ------------------------------------------------------------------ presentation

        private void LateUpdate()
        {
            float now = _match != null ? _match.Now : 0f;
            if (_rig != null && _shownWeapon != Slots[ActiveSlot].WeaponId)
            {
                _shownWeapon = Slots[ActiveSlot].WeaponId;
                _rig.ShowWeapon(ActiveWeapon);
            }
            if (_rig != null)
            {
                _rig.SetHealing(Alive && Heal.Active);
                _rig.SetAiming(Alive && Router != null && Router.enabled && Router.AimHeld);
            }
            // Bots' heads and weapons follow their aim (the human's animator reads the input router itself).
            if (_animator == null)
                _animator = GetComponentInChildren<ICharacterPose>();
            if (_animator != null)
                _animator.Health01 = Health.MaxHealth > 0f ? Health.Health / Health.MaxHealth : 0f;
            if (_animator != null && (Router == null || !Router.enabled))
                _animator.AimPitch = Pitch;
            if (_animator != null)
            {
                bool seated = Alive && Vehicle != null;
                _animator.Seated = seated;
                if (seated)
                {
                    Vehicle.PoseRider(this, Seat, out var foot, out var left, out var right);
                    _animator.SeatFoot = foot;
                    _animator.HandTargetL = left;
                    _animator.HandTargetR = right;
                }
            }
            _rig?.SetHidden(Alive && Vehicle != null);
            if (_outsideVisual != null)
                _outsideVisual.SetActive(Alive && OutsideZone);
            if (_shield != null)
                _shield.SetVisible(Alive && Protection.IsActive(now));
            if (_beacon != null && _match != null)
                _beacon.SetActive(Alive && _match.IsExposed(Id));
        }
    }
}
