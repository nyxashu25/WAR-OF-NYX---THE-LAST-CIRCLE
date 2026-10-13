using UnityEngine;
using UnityEngine.AI;
using WarOfNyx.Combat;
using WarOfNyx.Core.Bots;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Offline;
using WarOfNyx.Core.Zone;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>
    /// The AI "controller layer" for one bot (spec 38.4, 38.14). Each tick it turns legitimate
    /// information (its own senses → <see cref="BotMemory"/>, the circle as shown on every HUD, its
    /// own camping status) into the SAME <see cref="MovementCommand"/> + <see cref="CombatCommand"/> a
    /// human produces. It never moves the character, deals damage or reads another player's true
    /// position directly: the match's shared rules do all of that.
    ///
    /// Scheduling (spec 38.15): perception and decisions run on staggered timers; bots far from the
    /// camera think less often but are never frozen (they still move, fight, loot and take damage).
    /// </summary>
    public sealed class BotController
    {
        private static readonly int EnvironmentMask = ~(1 << 2);
        private const float ArriveDistance = 2f;

        private readonly OfflineMatch _match;
        private readonly System.Random _random;
        private readonly NavMeshPath _path = new NavMeshPath();
        private readonly Vector3[] _corners = new Vector3[64];

        private float _nextPerceptionAt, _lastPerceptionAt, _nextDecisionAt, _nextPathAt;
        private int _soundCursor;
        private int _cornerCount, _cornerIndex;
        private Vector3 _goal;
        private bool _hasGoal;
        private Vector3 _home;
        private Vector3 _progressAnchor;
        private float _progressCheckAt;
        private bool _jumpNext;
        private float _strafeSign = 1f, _nextStrafeFlipAt;
        private int _burstTarget, _burstStartShots;
        private float _burstPauseUntil, _nextTriggerAt;
        private OfflineLootCrate _lootCrate;
        private float _nextLootAt;
        private bool _crouchWanted;
        private bool _coverNearby;
        private float _lootValue;

        public BotController(OfflineMatch match, BotPersonality personality, BotDifficultyProfile difficulty, int seed)
        {
            _match = match;
            _random = new System.Random(seed);
            Personality = personality;
            Difficulty = difficulty;
            Brain = new BotBrain(personality, difficulty, seed * 7 + 1);
            Memory = new BotMemory();
            Aim = new BotAim(difficulty, seed * 13 + 5);
            // Stagger: bots don't all think on the same frame.
            _nextPerceptionAt = (float)_random.NextDouble() * 0.3f;
            _nextDecisionAt = (float)_random.NextDouble() * difficulty.DecisionIntervalSeconds;
        }

        public OfflineCombatant Self { get; private set; }
        public BotPersonality Personality { get; }
        public BotDifficultyProfile Difficulty { get; }
        public BotBrain Brain { get; }
        public BotMemory Memory { get; }
        public BotAim Aim { get; }
        public BotState State => Brain.State;

        public void Attach(OfflineCombatant self)
        {
            Self = self;
            Aim.Reset(self.Yaw, 0f);
            _home = self.Feet;
            _progressAnchor = self.Feet;
        }

        // ------------------------------------------------------------------ main loop

        public void Think(float now, float dt, out MovementCommand move, out CombatCommand combat)
        {
            move = default;
            combat = default;
            if (Self == null || !Self.Alive)
            {
                move.Yaw = Aim.Yaw;
                return;
            }

            float lod = _match.DistanceToViewer(Self.Feet) > 60f ? 2f : 1f;
            if (now >= _nextPerceptionAt)
            {
                Perceive(now);
                _nextPerceptionAt = now + (0.15f + Difficulty.DecisionIntervalSeconds * 0.25f) * lod;
            }
            if (now >= _nextDecisionAt)
            {
                Decide(now);
                _nextDecisionAt = now + Difficulty.DecisionIntervalSeconds * lod * (0.85f + 0.3f * (float)_random.NextDouble());
            }

            Behave(now, dt, ref move, ref combat);
        }

        // ------------------------------------------------------------------ perception (spec 38.6)

        private void Perceive(float now)
        {
            float elapsed = _lastPerceptionAt > 0f ? now - _lastPerceptionAt : 0f;
            _lastPerceptionAt = now;
            var eye = Self.Eye;

            foreach (var other in _match.Combatants)
            {
                if (other == Self || other == null)
                    continue;
                if (!other.Alive)
                {
                    Memory.Forget(other.Id);
                    continue;
                }
                var chest = other.Chest;
                float sqr = (chest - eye).sqrMagnitude;
                float maxRange = BotDifficultyProfile.MaxVisionRangeMeters;
                if (sqr > maxRange * maxRange)
                {
                    Memory.LostSight(other.Id);
                    continue;
                }

                var q = new VisibilityQuery
                {
                    ObserverX = Self.Feet.x, ObserverZ = Self.Feet.z, ObserverYaw = Self.Yaw,
                    TargetX = other.Feet.x, TargetZ = other.Feet.z,
                    TargetCrouched = other.LowStance, TargetMoving = other.Moving, TargetFiring = other.FiredRecently(now),
                    // Physics line of sight from eye to chest: walls and terrain always block (spec 38.6 / 38.8).
                    LineOfSightClear = !Physics.Linecast(eye, chest, EnvironmentMask, QueryTriggerInteraction.Ignore),
                };
                if (BotPerception.CanSee(Difficulty, q))
                    Memory.See(other.Id, other.Feet.x, other.Feet.z, now);
                else
                    Memory.LostSight(other.Id);
            }

            // Sounds since the last check: an APPROXIMATE source, to be investigated.
            var sounds = _match.Sounds;
            for (; _soundCursor < _match.SoundCount; _soundCursor++)
            {
                var sound = sounds[_soundCursor % sounds.Length];
                if (sound.SourceId == Self.Id || now - sound.Time > 2f)
                    continue;
                if (!BotPerception.CanHear(Difficulty, sound, Self.Feet.x, Self.Feet.z))
                    continue;
                BotPerception.EstimateSource(sound, Self.Feet.x, Self.Feet.z, _random, out float x, out float z);
                Memory.Hear(sound.SourceId, x, z, now);
            }

            // Being shot: the damage direction is known (humans see the same indicator), the shooter is not.
            if (Self.HurtRecently(now) && Memory.BestVisible(Self.Feet.x, Self.Feet.z) == null)
            {
                var from = Self.LastHurtFrom;
                Memory.Hear(-1000 - Self.Id, from.x + (float)(_random.NextDouble() * 8 - 4), from.z + (float)(_random.NextDouble() * 8 - 4), now);
            }

            Memory.Decay(elapsed);
        }

        // ------------------------------------------------------------------ decisions (spec 38.4)

        private void Decide(float now)
        {
            var s = BuildSituation(now);
            var previous = Brain.State;
            var state = Brain.Decide(s);
            if (state != previous)
                OnEnter(state, now);
        }

        private BotSituation BuildSituation(float now)
        {
            var me = Self;
            var pos = me.Feet;
            var s = new BotSituation
            {
                Alive = me.Alive,
                SecondsSinceSpawn = now - _match.FightStartedAt,
                Health = me.Health.Health,
                MaxHealth = me.Health.MaxHealth,
                Medkits = me.Inventory.TotalMedkits,
                Healing = me.Heal.Active,
                UnderFire = me.HurtRecently(now),
                HasMelee = !me.Slots[CombatLoadout.MeleeSlot].IsEmptySlot,
            };

            var melee = WeaponRegistry.Get(me.Slots[CombatLoadout.MeleeSlot].WeaponId);
            s.MeleeReach = melee != null ? melee.meleeReach : 1.5f;
            int ranged = BestRangedSlot();
            if (ranged >= 0)
            {
                var w = WeaponRegistry.Get(me.Slots[ranged].WeaponId);
                s.HasRangedWithAmmo = true;
                s.RangedEngagementRange = Mathf.Max(8f, Mathf.Min(w.falloffEnd, w.maxRange * 0.6f));
            }

            if (_match.ZoneActive)
            {
                var stage = _match.Stage;
                var circle = _match.CurrentCircle;
                s.ZoneActive = true;
                s.Outside = !circle.Contains(pos.x, pos.z);
                s.DistanceToEdge = circle.DistanceToEdge(pos.x, pos.z);
                s.SecondsToShrinkEnd = stage.SecondsRemaining(now);
                s.InsideNextCircle = stage.To.Radius <= 0.01f || stage.To.DistanceToEdge(pos.x, pos.z) < -2f;
                s.FinalStage = stage.IsFinal || stage.Finished;
            }

            if (_match.TryGetHunt(me.Id, out var hunt))
            {
                s.InactiveSeconds = Mathf.Max(0f, now - hunt.InactiveSince);
                s.Exposed = hunt.State == HuntState.Exposed || hunt.State == HuntState.CampingPenalty;
            }

            var best = Memory.Best(pos.x, pos.z);
            if (best != null)
            {
                float d = Vector2.Distance(new Vector2(best.X, best.Z), new Vector2(pos.x, pos.z));
                if (best.Visible)
                {
                    s.EnemyVisible = true;
                    s.EnemyDistance = d;
                }
                else
                {
                    s.EnemyRemembered = true;
                    s.RememberedConfidence = best.Confidence;
                    s.RememberedFromSound = best.FromSound;
                }
            }

            _lootCrate = _match.BestLootFor(me, 45f, out _lootValue);
            s.LootNearby = _lootCrate != null;
            s.LootValue = _lootValue;

            _coverNearby = ProbeCover(pos);
            s.CoverNearby = _coverNearby;
            return s;
        }

        /// <summary>Cheap cover probe: something solid within 6 m at chest height in one of 8 directions.</summary>
        private static bool ProbeCover(Vector3 feet)
        {
            var chest = feet + Vector3.up * 1.1f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                if (Physics.Raycast(chest, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), 6f, EnvironmentMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        private void OnEnter(BotState state, float now)
        {
            _hasGoal = false;
            _crouchWanted = false;
            switch (state)
            {
                case BotState.Patrolling:
                    _home = RandomPointInCircle(_match.CurrentCircle, 0.7f, Self.Feet, 0f, 25f);
                    break;
                case BotState.SeekingCover:
                case BotState.Hiding:
                case BotState.Ambushing:
                    SetGoal(FindCover(ThreatPosition()));
                    break;
            }
        }

        // ------------------------------------------------------------------ behaviour

        private void Behave(float now, float dt, ref MovementCommand move, ref CombatCommand combat)
        {
            var me = Self;
            var pos = me.Feet;
            var target = Memory.BestVisible(pos.x, pos.z);
            var targetCombatant = target != null ? _match.Find(target.Id) : null;
            if (targetCombatant != null && !targetCombatant.Alive)
                targetCombatant = null;

            bool sprint = false;
            bool fight = false;
            Vector3 strafe = Vector3.zero;
            var state = Brain.State;
            var circle = _match.CurrentCircle;

            switch (state)
            {
                case BotState.Spawning:
                case BotState.Eliminated:
                    break;

                case BotState.Exploring:
                    if (!_hasGoal || Arrived())
                        SetGoal(RandomPointInCircle(circle, 0.85f, pos, 15f, 60f));
                    break;

                case BotState.Patrolling:
                    if (!_hasGoal || Arrived())
                        SetGoal(RandomAround(_home, 15f, circle));
                    break;

                case BotState.SearchingForEnemies:
                case BotState.InvestigatingSound:
                {
                    var c = Memory.Best(pos.x, pos.z);
                    if (c != null && (!_hasGoal || Arrived()))
                        SetGoal(RandomAround(new Vector3(c.X, pos.y, c.Z), state == BotState.SearchingForEnemies ? 6f : 3f, circle));
                    _crouchWanted = Personality.Stealth > 1.2f && c != null && Vector3.Distance(pos, new Vector3(c.X, pos.y, c.Z)) < 15f;
                    fight = targetCombatant != null;
                    break;
                }

                case BotState.SeekingCover:
                case BotState.Hiding:
                case BotState.Ambushing:
                    if (!_hasGoal)
                        SetGoal(FindCover(ThreatPosition()));
                    _crouchWanted = Arrived();
                    fight = targetCombatant != null;
                    break;

                case BotState.StalkingTarget:
                {
                    var c = Memory.Best(pos.x, pos.z);
                    if (c != null && (!_hasGoal || now >= _nextPathAt))
                        SetGoal(new Vector3(c.X, pos.y, c.Z));
                    _crouchWanted = Personality.Stealth > 1.0f && c != null && Vector3.Distance(pos, new Vector3(c.X, pos.y, c.Z)) < 25f;
                    fight = targetCombatant != null;
                    break;
                }

                case BotState.EngagingEnemy:
                case BotState.FinalCircleCombat:
                case BotState.Repositioning:
                {
                    fight = targetCombatant != null;
                    if (targetCombatant != null)
                    {
                        var toTarget = targetCombatant.Feet - pos;
                        toTarget.y = 0f;
                        float distance = toTarget.magnitude;
                        float preferred = PreferredDistance();
                        var forward = distance > 0.01f ? toTarget / distance : Vector3.forward;
                        if (state == BotState.Repositioning)
                        {
                            // Flank: move around the target at the current distance.
                            if (!_hasGoal || Arrived())
                            {
                                float angle = (_random.NextDouble() < 0.5 ? 1f : -1f) * 70f;
                                var offset = Quaternion.Euler(0f, angle, 0f) * (-forward) * Mathf.Max(8f, distance);
                                SetGoal(Clamp(targetCombatant.Feet + offset, circle));
                            }
                        }
                        else
                        {
                            _hasGoal = false;
                            float approach = distance > preferred * 1.2f ? 1f : distance < preferred * 0.6f ? -1f : 0f;
                            if (now >= _nextStrafeFlipAt)
                            {
                                _strafeSign = _random.NextDouble() < 0.5 ? -1f : 1f;
                                _nextStrafeFlipAt = now + Mathf.Lerp(1.6f, 0.7f, Difficulty.FlankingTendency);
                            }
                            var right = new Vector3(forward.z, 0f, -forward.x);
                            float strafeAmount = 0.4f + Difficulty.CoverUsage * 0.6f;
                            strafe = (forward * approach + right * _strafeSign * strafeAmount).normalized;
                        }
                    }
                    else if (state == BotState.FinalCircleCombat && (!_hasGoal || Arrived()))
                    {
                        SetGoal(RandomAround(new Vector3(circle.X, pos.y, circle.Z), Mathf.Max(1f, circle.Radius * 0.4f), circle));
                        _crouchWanted = true;
                    }
                    break;
                }

                case BotState.Retreating:
                {
                    var threat = ThreatPosition();
                    if (!_hasGoal || Arrived())
                    {
                        var away = pos - threat;
                        away.y = 0f;
                        SetGoal(Clamp(pos + away.normalized * 25f, circle));
                    }
                    sprint = true;
                    fight = targetCombatant != null && me.HurtRecently(now) && Personality.TriggerHappiness >= 1f;
                    break;
                }

                case BotState.Healing:
                    _hasGoal = false;
                    _crouchWanted = _coverNearby;
                    if (!me.Heal.Active)
                        combat.MedkitPressed = true;
                    break;

                case BotState.Looting:
                    if (_lootCrate != null)
                    {
                        SetGoal(_lootCrate.transform.position);
                        if (Vector3.Distance(pos, _lootCrate.transform.position) <= OfflineLootCrate.InteractRange - 0.3f && now >= _nextLootAt)
                        {
                            _nextLootAt = now + 0.45f;
                            _match.BotTakeLoot(me, _lootCrate, Difficulty.LootSelection, _random);
                        }
                    }
                    break;

                case BotState.SeekingSafeZone:
                case BotState.EscapingCircle:
                {
                    var stage = _match.Stage;
                    var safe = state == BotState.SeekingSafeZone && stage.To.Radius > 0.01f ? stage.To : circle;
                    if (!_hasGoal || Arrived() || now >= _nextPathAt)
                    {
                        var c = new Vector3(safe.X, pos.y, safe.Z);
                        var dir = pos - c;
                        dir.y = 0f;
                        float depth = Mathf.Max(0f, safe.Radius * 0.55f);
                        SetGoal(c + (dir.sqrMagnitude > 0.01f ? dir.normalized * depth : Vector3.zero));
                    }
                    sprint = state == BotState.EscapingCircle || _match.Stage.SecondsRemaining(now) < 20f;
                    // Outside, guns are locked by the rules: melee anyone who blocks the way.
                    fight = targetCombatant != null && Vector3.Distance(pos, targetCombatant.Feet) < 3f;
                    break;
                }

                case BotState.AvoidingCampingPenalty:
                    if (!_hasGoal || Arrived())
                        SetGoal(RandomPointInCircle(circle, 0.8f, pos, 18f, 35f));
                    sprint = _match.TryGetHunt(me.Id, out var hunt) && hunt.State != HuntState.Normal;
                    break;
            }

            // ----- combat
            bool aiming = false;
            if (fight && targetCombatant != null)
                aiming = Fight(targetCombatant, now, dt, ref combat);
            else
            {
                _burstTarget = 0;
                // Tactical reload while nothing is in sight.
                var w = me.ActiveWeapon;
                if (w != null && w.IsRanged && me.Slots[me.ActiveSlot].Magazine < w.magazineSize * 0.4f && me.Inventory.GetAmmo(w.ammoType) > 0
                    && !me.Slots[me.ActiveSlot].IsReloading)
                    combat.ReloadPressed = true;
                SelectBestRanged(ref combat);
            }

            // ----- movement
            Vector3 desired = strafe;
            if (desired == Vector3.zero && _hasGoal && !Arrived())
                desired = FollowPath(pos, now);

            if (!aiming)
            {
                if (desired.sqrMagnitude > 0.01f)
                    Aim.LookTowards(Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg, 0f, dt);
                else if (target != null)
                    Aim.LookTowards(Mathf.Atan2(target.X - pos.x, target.Z - pos.z) * Mathf.Rad2Deg, 0f, dt);
            }

            move.Yaw = Aim.Yaw;
            move.Pitch = Aim.Pitch;
            if (desired.sqrMagnitude > 0.01f)
            {
                desired.Normalize();
                MovementModel.YawToBasis(move.Yaw, out float fx, out float fz, out float rx, out float rz);
                move.MoveY = desired.x * fx + desired.z * fz;
                move.MoveX = desired.x * rx + desired.z * rz;
            }
            move.SprintHeld = sprint && !aiming && move.MoveY > 0.7f;
            move.AimHeld = aiming && Vector3.Distance(pos, targetCombatant != null ? targetCombatant.Feet : pos) > 20f;

            bool crouched = me.Movement.State.CrouchToggled;
            if (crouched != (_crouchWanted && !sprint))
                move.CrouchPressed = true;

            CheckProgress(pos, now, desired, ref move);
        }

        // ------------------------------------------------------------------ fighting (spec 38.8)

        /// <returns>True while the bot is aiming at the target (movement must not turn the view).</returns>
        private bool Fight(OfflineCombatant target, float now, float dt, ref CombatCommand combat)
        {
            var me = Self;
            var eye = me.Eye;
            var aimPoint = target.Chest;
            var to = aimPoint - eye;
            float distance = to.magnitude;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(to.y / Mathf.Max(distance, 0.01f), -1f, 1f)) * Mathf.Rad2Deg;
            Aim.Track(target.Id, yaw, pitch, distance, now, dt);

            var melee = WeaponRegistry.Get(me.Slots[CombatLoadout.MeleeSlot].WeaponId);
            int ranged = BestRangedSlot();
            bool meleeRange = melee != null && distance <= melee.meleeReach + 0.6f;
            bool preferMelee = meleeRange && (ranged < 0 || me.OutsideZone || Personality.MeleePreference >= 1.5f || distance < 1.6f);

            if (preferMelee)
            {
                if (now >= Aim.ReadyAt && Mathf.Abs(BotPerception.DeltaAngle(me.Yaw, yaw)) < melee.meleeArcDegrees * 0.4f)
                    combat.MeleePressed = true;
                return true;
            }

            if (ranged < 0 || me.OutsideZone)
                return true; // keep facing the threat; the brain decides to flee or close in

            if (me.ActiveSlot != ranged)
            {
                combat.SelectSlot = ranged + 1;
                return true;
            }

            var w = me.ActiveWeapon;
            if (w == null || distance > w.maxRange * 0.9f || now < _burstPauseUntil || !Aim.CanShoot(yaw, pitch, distance, now))
                return true;

            if (w.fireMode == FireMode.Auto)
            {
                if (_burstTarget == 0)
                {
                    // Burst discipline: short bursts, longer for trigger-happy bots, shorter at range.
                    _burstStartShots = me.ShotsFired;
                    _burstTarget = Mathf.Clamp(Mathf.RoundToInt((3f + 4f * Personality.TriggerHappiness) * (distance < 15f ? 1.4f : 0.8f)), 2, 12);
                }
                if (me.ShotsFired - _burstStartShots >= _burstTarget)
                {
                    _burstTarget = 0;
                    // Recoil recovery: better recoil control = shorter pause.
                    _burstPauseUntil = now + Mathf.Lerp(0.55f, 0.18f, Difficulty.RecoilControl);
                }
                else
                {
                    combat.FireHeld = true;
                    combat.FirePressed = me.ShotsFired == _burstStartShots;
                }
            }
            else if (now >= _nextTriggerAt)
            {
                combat.FirePressed = true;
                combat.FireHeld = true;
                _nextTriggerAt = now + Mathf.Max(w.FireInterval, 0.12f) + (float)_random.NextDouble() * Mathf.Lerp(0.35f, 0.08f, Difficulty.RecoilControl);
            }
            return true;
        }

        private float PreferredDistance()
        {
            var me = Self;
            int ranged = BestRangedSlot();
            if (ranged < 0 || Personality.MeleePreference >= 1.5f)
                return 1.5f;
            var w = WeaponRegistry.Get(me.Slots[ranged].WeaponId);
            return Mathf.Clamp(w.falloffStart * 0.8f, 5f, 35f);
        }

        /// <summary>The ranged slot with ammunition and the best power, or -1.</summary>
        private int BestRangedSlot()
        {
            var me = Self;
            int best = -1;
            float bestPower = float.MinValue;
            for (int i = 0; i < CombatLoadout.MeleeSlot; i++)
            {
                var s = me.Slots[i];
                if (s.IsEmptySlot)
                    continue;
                var w = WeaponRegistry.Get(s.WeaponId);
                if (w == null || !w.IsRanged || s.Magazine + me.Inventory.GetAmmo(w.ammoType) <= 0)
                    continue;
                if (w.powerScore > bestPower)
                {
                    bestPower = w.powerScore;
                    best = i;
                }
            }
            return best;
        }

        private void SelectBestRanged(ref CombatCommand combat)
        {
            int best = BestRangedSlot();
            if (best >= 0 && best != Self.ActiveSlot && !Self.Slots[Self.ActiveSlot].IsReloading)
                combat.SelectSlot = best + 1;
        }

        // ------------------------------------------------------------------ navigation (spec 38.15: shared NavMesh, cached paths)

        private void SetGoal(Vector3 goal)
        {
            if (NavMesh.SamplePosition(goal, out var hit, 6f, NavMesh.AllAreas))
                goal = hit.position;
            if (_hasGoal && (goal - _goal).sqrMagnitude < 1f)
                return;
            _goal = goal;
            _hasGoal = true;
            _nextPathAt = 0f;
        }

        private bool Arrived() => !_hasGoal || HorizontalDistance(Self.Feet, _goal) <= ArriveDistance;

        private Vector3 FollowPath(Vector3 pos, float now)
        {
            if (now >= _nextPathAt)
            {
                _nextPathAt = now + 1.2f + (float)_random.NextDouble() * 0.4f;
                _cornerCount = 0;
                _cornerIndex = 0;
                if (NavMesh.SamplePosition(pos, out var from, 3f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position, _goal, NavMesh.AllAreas, _path)
                    && _path.status != NavMeshPathStatus.PathInvalid)
                {
                    _cornerCount = _path.GetCornersNonAlloc(_corners);
                    _cornerIndex = _cornerCount > 1 ? 1 : 0;
                }
            }

            var next = _goal;
            if (_cornerCount > 0)
            {
                while (_cornerIndex < _cornerCount - 1 && HorizontalDistance(pos, _corners[_cornerIndex]) < 1.0f)
                    _cornerIndex++;
                next = _corners[Mathf.Min(_cornerIndex, _cornerCount - 1)];
            }
            var dir = next - pos;
            dir.y = 0f;
            return dir;
        }

        /// <summary>No progress for a while although the bot wants to move: hop and pick a new goal.</summary>
        private void CheckProgress(Vector3 pos, float now, Vector3 desired, ref MovementCommand move)
        {
            if (_jumpNext)
            {
                move.JumpPressed = true;
                _jumpNext = false;
            }
            if (now < _progressCheckAt)
                return;
            bool wantsToMove = desired.sqrMagnitude > 0.01f;
            if (wantsToMove && HorizontalDistance(pos, _progressAnchor) < 0.6f)
            {
                _jumpNext = true;
                _hasGoal = false;
                _nextPathAt = 0f;
            }
            _progressAnchor = pos;
            _progressCheckAt = now + 1.5f;
        }

        private Vector3 ThreatPosition()
        {
            var c = Memory.Best(Self.Feet.x, Self.Feet.z);
            if (c != null)
                return new Vector3(c.X, Self.Feet.y, c.Z);
            return Self.HurtRecently(_match.Now) ? Self.LastHurtFrom : Self.Feet + Self.transform.forward * 10f;
        }

        /// <summary>A nearby spot hidden from the threat (a wall between them at chest height).</summary>
        private Vector3 FindCover(Vector3 threat)
        {
            var pos = Self.Feet;
            Vector3 best = pos;
            float bestScore = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float r = 3f + (float)_random.NextDouble() * 9f;
                var candidate = pos + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                    continue;
                candidate = hit.position;
                bool hidden = Physics.Linecast(threat + Vector3.up * 1.4f, candidate + Vector3.up * 1.1f, EnvironmentMask, QueryTriggerInteraction.Ignore);
                bool safe = _match.CurrentCircle.Contains(candidate.x, candidate.z) || !_match.ZoneActive;
                float score = (hidden ? 10f : 0f) + (safe ? 5f : -20f) - r * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private Vector3 RandomPointInCircle(in ZoneCircle circle, float fraction, Vector3 from, float minDistance, float maxDistance)
        {
            bool zone = _match.ZoneActive && circle.Radius > 1f;
            for (int i = 0; i < 10; i++)
            {
                Vector3 p;
                if (zone)
                {
                    float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
                    float r = circle.Radius * fraction * Mathf.Sqrt((float)_random.NextDouble());
                    p = new Vector3(circle.X + Mathf.Sin(a) * r, from.y, circle.Z + Mathf.Cos(a) * r);
                }
                else
                {
                    p = _match.RandomArenaPoint(_random, from.y);
                }
                float d = HorizontalDistance(p, from);
                if (d >= minDistance && d <= maxDistance)
                    return p;
            }
            return zone ? new Vector3(circle.X, from.y, circle.Z) : from;
        }

        private Vector3 RandomAround(Vector3 centre, float radius, in ZoneCircle circle)
        {
            float a = (float)(_random.NextDouble() * Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt((float)_random.NextDouble());
            return Clamp(centre + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r), circle);
        }

        /// <summary>Keeps a goal inside the current circle (bots never plan to walk out of it).</summary>
        private Vector3 Clamp(Vector3 p, in ZoneCircle circle)
        {
            if (!_match.ZoneActive || circle.Radius <= 0.5f || circle.Contains(p.x, p.z))
                return p;
            var c = new Vector3(circle.X, p.y, circle.Z);
            var dir = p - c;
            dir.y = 0f;
            return c + dir.normalized * circle.Radius * 0.85f;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
