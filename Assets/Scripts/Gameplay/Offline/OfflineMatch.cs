using System;
using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Characters;
using WarOfNyx.Combat;
using WarOfNyx.Core.Bots;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Flow;
using WarOfNyx.Core.Hunt;
using WarOfNyx.Core.Items;
using WarOfNyx.Core.Match;
using WarOfNyx.Core.Movement;
using WarOfNyx.Core.Offline;
using WarOfNyx.Core.Zone;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>What the menu asks for (spec 38.3). Read once when the Ghost Protocol scene starts.</summary>
    public static class OfflineMatchRequest
    {
        public static int Bots = -1;
        public static BotDifficulty? Difficulty;
        /// <summary>False = bots only (the spec's "no human input" acceptance test, or spectator mode).</summary>
        public static bool IncludeHuman = true;
        /// <summary>Length of every circle phase; 0 or less = configured (60 s).</summary>
        public static float ZoneStageSeconds = -1f;
        public static int Seed;

        public static void Reset()
        {
            Bots = -1;
            Difficulty = null;
            IncludeHuman = true;
            ZoneStageSeconds = -1f;
            Seed = 0;
        }
    }

    public enum OfflinePhase : byte { Starting = 0, Countdown = 1, Active = 2, Ended = 3 }

    /// <summary>
    /// The LOCAL AUTHORITY of an offline Ghost Protocol match (spec 38.14): it runs entirely on the
    /// device, with no Photon session or server. It owns the match clock, spawns, the circle, the Nyx
    /// Hunt Protocol, damage, eliminations, loot capsules and the result, using the same shared Core
    /// rules as the dedicated server. Controllers (the human's input router and the bots) only issue
    /// commands; every competitor is simulated by the same <see cref="OfflineCombatant"/> code in a
    /// fixed order at a fixed rate.
    /// </summary>
    public sealed class OfflineMatch : MonoBehaviour
    {
        public const float TickSeconds = 1f / 60f;
        public const float CountdownSeconds = 3f;
        public const float SpawnProtectionSeconds = 3f;
        public const int SoundBufferSize = 256;
        private static readonly int EnvironmentMask = ~(1 << 2);

        [SerializeField] private PlayerController characterPrefab;

        public static OfflineMatch Instance { get; private set; }

        private readonly List<OfflineCombatant> _combatants = new List<OfflineCombatant>();
        private readonly Dictionary<int, OfflineCombatant> _byId = new Dictionary<int, OfflineCombatant>();
        private readonly List<OfflineLootCrate> _crates = new List<OfflineLootCrate>();
        private readonly SoundEvent[] _sounds = new SoundEvent[SoundBufferSize];
        private readonly List<string> _pendingFeed = new List<string>();

        private System.Random _random;
        private ArenaSpawnArea _area;
        private ZoneSettings _zoneSettings;
        private ZonePlan _zonePlan;
        private NyxHuntProtocol _hunt;
        private MatchResolver _resolver;
        private KillNotices _notices;
        private NyxCircleVisual _circleVisual;
        private float _accumulator;
        private int _tick;

        public OfflinePhase Phase { get; private set; } = OfflinePhase.Starting;
        public float Now { get; private set; }
        public float FightStartedAt { get; private set; } = float.PositiveInfinity;
        public int BotCount { get; private set; }
        public BotDifficulty Difficulty { get; private set; } = BotDifficulty.Nightmare;
        public OfflineCombatant Human { get; private set; }
        public IReadOnlyList<OfflineCombatant> Combatants => _combatants;
        public IReadOnlyList<OfflineLootCrate> Crates => _crates;
        public MatchResolver Resolver => _resolver;
        public ZoneStage Stage { get; private set; }
        public bool ZoneActive => _zonePlan != null;
        public ZoneCircle CurrentCircle => ZoneActive ? Stage.CircleAt(Now) : new ZoneCircle(0f, 0f, 1e6f);
        public SoundEvent[] Sounds => _sounds;
        public int SoundCount { get; private set; }
        public bool Unlocked => Phase == OfflinePhase.Active;
        public float SimulationSpeed { get; set; } = 1f;

        /// <summary>Feed lines and headlines produced by the simulation, consumed by the HUD.</summary>
        public event Action<string, bool> Feed;
        public event Action<string, Color> Headline;
        public event Action<string> Message;
        public event Action<OfflineCombatant, bool, bool> HumanHit;
        /// <summary>The human was hit: damage applied, where it came from, and whether it was a headshot.</summary>
        public event Action<float, Vector3, bool> HumanDamaged;
        public event Action Ended;

        // ------------------------------------------------------------------ setup

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            AppServices.EnsureInitialized();
            var config = AppServices.Config;
            var fallback = config.DefaultOfflineSettings;
            int requestedBots = OfflineMatchRequest.Bots > 0 ? OfflineMatchRequest.Bots : fallback.BotCount;
            Difficulty = OfflineMatchRequest.Difficulty ?? fallback.Difficulty;
            // Spec 38.3: the bot count is lowered on devices that can't run it; difficulty is never changed.
            BotCount = new OfflineMatchSettings(requestedBots, Difficulty).ClampToCapacity(Platform.DeviceCapabilityService.MaxOfflineBots).BotCount;
            int seed = OfflineMatchRequest.Seed != 0 ? OfflineMatchRequest.Seed : Environment.TickCount;
            _random = new System.Random(seed);

            _zoneSettings = (config.zone ?? new ZoneSettings()).WithStageSeconds(OfflineMatchRequest.ZoneStageSeconds);
            _area = ArenaSpawnArea.Current;
            if (_area == null || characterPrefab == null)
            {
                Debug.LogError("[WarOfNyx][Offline] The Ghost Protocol scene is missing its spawn area or character prefab. Run War of Nyx > Setup.");
                return;
            }

            if (OfflineMatchRequest.IncludeHuman)
                AppServices.Flow.TryStartSession(GameMode.OfflineBattleRoyale);
            SpawnEveryone(seed, OfflineMatchRequest.IncludeHuman);
            // Same seed, same sky: time of day and weather are part of the match (visual only).
            World.WeatherSystem.Ensure().Begin(Core.Weather.Atmosphere.Plan(seed), 0f, seed);
            Debug.Log($"[WarOfNyx][Offline] Ghost Protocol: {(Human != null ? "1 human + " : string.Empty)}{BotCount} bots, {Difficulty}, seed {seed}.");
        }

        private void SpawnEveryone(int seed, bool includeHuman)
        {
            int total = BotCount + (includeHuman ? 1 : 0);
            var rules = AppServices.Config.matchRules ?? new MatchRules();
            var plan = _area.PlanSpawns(total, rules, seed);
            if (!plan.Success)
            {
                Debug.LogError("[WarOfNyx][Offline] No valid spawn layout: " + plan.Failure);
                return;
            }

            var profile = BotDifficultyCatalog.Get(Difficulty);
            var names = BotNames.Generate(BotCount, _random);
            var budget = AppServices.Config.loadoutBudget ?? new LoadoutBudget();
            float protectionEnds = CountdownSeconds + SpawnProtectionSeconds;

            for (int i = 0; i < total; i++)
            {
                bool human = includeHuman && i == 0;
                var p = plan.Points[i];
                var instance = Instantiate(characterPrefab);
                var combatant = instance.gameObject.AddComponent<OfflineCombatant>();
                BotController bot = null;
                if (!human)
                    bot = new BotController(this, BotPersonality.Random(_random), profile, unchecked(seed * 31 + i * 7919));
                var loadout = LoadoutGenerator.Generate(unchecked(seed * 17 + i * 104729), budget, WeaponRegistry.Get);
                string name = human ? PlayerName() : names[includeHuman ? i - 1 : i];
                var look = human
                    ? CharacterRoster.Get(AppServices.UserSettings.Data.characterId)
                    : CharacterRoster.Get(_random.Next(CharacterRoster.Count)); // cosmetic only
                combatant.Initialize(this, i + 1, name, loadout, new Vector3(p.X, p.Y + rules.spawnDropHeight, p.Z), p.Yaw, protectionEnds, bot, look);
                bot?.Attach(combatant);
                _combatants.Add(combatant);
                _byId[combatant.Id] = combatant;
                if (human)
                    Human = combatant;
            }

            // The circle exists from the countdown on; phase 1 starts shrinking at FIGHT.
            _zonePlan = ZonePlanner.Plan(_zoneSettings, _area.transform.position.x, _area.transform.position.z, _area.ZoneRadius,
                unchecked(seed * 16777619 + 0x5A17), _area);
            if (!Application.isBatchMode)
                _circleVisual = NyxCircleVisual.Create(transform);

            Phase = OfflinePhase.Countdown;
            FightStartedAt = Now + CountdownSeconds;
            Stage = ZoneTimeline.Evaluate(_zonePlan, _zoneSettings, FightStartedAt, Now);
            AdvanceFlow(GameFlowState.Countdown);
        }

        private static string PlayerName()
        {
            string saved = AppServices.IsInitialized ? AppServices.UserSettings.Data.playerName : null;
            return Core.Networking.PlayerNames.Sanitize(saved ?? string.Empty, 1);
        }

        // ------------------------------------------------------------------ the fixed-rate simulation

        private void Update()
        {
            if (Phase == OfflinePhase.Starting || Time.timeScale <= 0f)
                return;
            _accumulator += Mathf.Min(Time.deltaTime, 0.25f) * SimulationSpeed;
            int steps = 0;
            while (_accumulator >= TickSeconds && steps < 240)
            {
                _accumulator -= TickSeconds;
                Step();
                steps++;
            }

            if (_circleVisual != null && ZoneActive)
                _circleVisual.Show(CurrentCircle, Stage.To, !Stage.Finished, _area.transform.position.y);
            foreach (var crate in _crates)
                if (crate != null)
                    crate.Render(Now);
        }

        /// <summary>One authoritative tick: commands → combatants (fixed id order) → environment → results.</summary>
        public void Step()
        {
            _tick++;
            Now += TickSeconds;

            if (Phase == OfflinePhase.Countdown && Now >= FightStartedAt)
                BeginFight();

            if (ZoneActive)
                Stage = ZoneTimeline.Evaluate(_zonePlan, _zoneSettings, FightStartedAt, Now);
            var circle = CurrentCircle;
            bool zoneLive = Unlocked && ZoneActive;

            foreach (var c in _combatants)
            {
                if (!c.Alive)
                    continue;
                MovementCommand move;
                CombatCommand combat;
                if (c.IsBot)
                    c.Bot.Think(Now, TickSeconds, out move, out combat);
                else if (c.Router != null && c.Router.enabled)
                    c.Router.ConsumeInput(out move, out combat);
                else
                {
                    move = Core.Networking.InputCodec.Neutral(c.Yaw, 0f);
                    combat = default;
                }

                if (Phase == OfflinePhase.Ended)
                {
                    move = Core.Networking.InputCodec.Neutral(move.Yaw, move.Pitch);
                    combat = default;
                }
                c.Simulate(move, combat, Unlocked, Now, TickSeconds, _tick, circle, zoneLive, _zoneSettings);
            }

            if (Unlocked)
            {
                EnvironmentPass(circle, zoneLive);
                ResolveResults();
                ExpireCrates();
            }
        }

        private void BeginFight()
        {
            Phase = OfflinePhase.Active;
            var huntSettings = AppServices.Config.hunt ?? new HuntSettings();
            _hunt = new NyxHuntProtocol(huntSettings.Validate() == null ? huntSettings : new HuntSettings());
            _resolver = new MatchResolver(FightStartedAt);
            _notices = new KillNotices();
            foreach (var c in _combatants)
            {
                _resolver.AddParticipant(c.Id, c.DisplayName);
                _hunt.Track(c.Id, Now, c.Feet.x, c.Feet.z);
            }
            AdvanceFlow(GameFlowState.ActiveMatch);
        }

        /// <summary>Zone + camping damage in ONE application per combatant per tick (spec 13), same rules as online.</summary>
        private void EnvironmentPass(in ZoneCircle circle, bool zoneLive)
        {
            foreach (var c in _combatants)
            {
                if (!c.Alive)
                    continue;
                float zone = 0f;
                if (zoneLive)
                    zone = ZoneDamage.Step(ref c.Zone, !c.OutsideZone, Stage.DamagePerSecond, TickSeconds, _zoneSettings.damageIntervalSeconds).Damage;

                var hunt = _hunt.Update(c.Id, Now, c.Feet.x, c.Feet.z, TickSeconds);
                if ((hunt.Events & HuntEvents.Exposed) != 0)
                    Feed?.Invoke(HuntText.Alert(c.DisplayName), true);

                float total = EnvironmentDamage.Total(zone, hunt.Damage);
                if (total <= 0f)
                    continue;
                var cause = EnvironmentDamage.Cause(zone, hunt.Damage);
                var result = c.ReceiveDamage(total, cause, null, WeaponCatalog.NoWeapon, c.Feet, Now);
                if (result.Killed)
                    HandleElimination(c, null, cause, WeaponCatalog.NoWeapon, false);
            }
        }

        private void ResolveResults()
        {
            foreach (var c in _combatants)
                _resolver.UpdateStats(c.Id, c.Kills, c.DamageDealt);
            _resolver.ResolveUpTo(_tick - 1, Now);
            if (_resolver.Finished && Phase != OfflinePhase.Ended)
                EndMatch();
        }

        private void EndMatch()
        {
            Phase = OfflinePhase.Ended;
            var winner = _resolver.Winner;
            Debug.Log(winner != null
                ? $"[WarOfNyx][Offline] VICTORY: {winner.Name}{(winner.WonByTieBreak ? " (tie-break)" : string.Empty)} - {winner.Kills} kills. Match time {MatchResultText.Clock(Now - FightStartedAt)}."
                : "[WarOfNyx][Offline] The match ended with no survivor.");
            AdvanceFlow(GameFlowState.MatchEnded);
            if (winner != null)
            {
                var winnerCombatant = Find(winner.PlayerId);
                winnerCombatant?.Celebrate();
                if (!Application.isBatchMode && winnerCombatant != null)
                    ProceduralAudio.PlayAt(ProceduralAudio.Victory, winnerCombatant.Feet, winnerCombatant == Human ? 1f : 0.6f);
            }
            SaveHistory();
            Ended?.Invoke();
        }

        private void ExpireCrates()
        {
            for (int i = _crates.Count - 1; i >= 0; i--)
            {
                var crate = _crates[i];
                if (crate == null || crate.Contents.IsEmpty || Now >= crate.ExpiresAt)
                {
                    if (crate != null)
                        Destroy(crate.gameObject);
                    _crates.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ damage and eliminations (single entry point)

        public void ApplyWeaponDamage(OfflineCombatant attacker, OfflineCombatant victim, float amount, bool headshot, short weaponId)
        {
            var result = victim.ReceiveDamage(amount, DamageKind.Weapon, attacker, weaponId, attacker.Feet, Now);
            AfterHit(attacker, victim, result, headshot, weaponId, DamageKind.Weapon);
        }

        public void ApplyMeleeDamage(OfflineCombatant attacker, OfflineCombatant victim, float amount, short weaponId)
        {
            var result = victim.ReceiveDamage(amount, DamageKind.Melee, attacker, weaponId, attacker.Feet, Now);
            AfterHit(attacker, victim, result, false, weaponId, DamageKind.Melee);
        }

        private void AfterHit(OfflineCombatant attacker, OfflineCombatant victim, in DamageResult result, bool headshot, short weaponId, DamageKind kind)
        {
            if (result.Applied <= 0f)
                return;
            attacker.DamageDealt += result.Applied;
            if (attacker == Human)
                HumanHit?.Invoke(victim, headshot, result.Killed);
            if (victim == Human)
                HumanDamaged?.Invoke(result.Applied, attacker.Feet, headshot);
            if (result.Killed)
            {
                attacker.Kills++;
                HandleElimination(victim, attacker, kind, weaponId, headshot);
            }
        }

        private void HandleElimination(OfflineCombatant victim, OfflineCombatant killer, DamageKind cause, short weaponId, bool headshot)
        {
            _resolver?.RecordElimination(victim.Id, _tick, Now, killer != null ? killer.Id : -1, cause);
            _hunt?.Remove(victim.Id); // any beacon disappears

            string text = killer != null
                ? CombatText.Elimination(killer.DisplayName, victim.DisplayName, WeaponRegistry.DisplayName(weaponId), headshot)
                : CombatText.Environmental(victim.DisplayName, cause);
            Feed?.Invoke(text, victim == Human || killer == Human);
            string notice = killer != null && _notices != null ? _notices.OnElimination(killer.Id, victim.Id, Now) : null;
            if (notice != null)
            {
                Feed?.Invoke($"{notice}: {killer.DisplayName}", true);
                Headline?.Invoke($"{notice}  -  {killer.DisplayName}", new Color(1f, 0.78f, 0.34f));
            }

            // Everything the player carried moves into a capsule (spec 14 / 38.13).
            var contents = LootContainer.FromEliminated(victim.Slots, victim.Inventory);
            for (int i = 0; i < CombatLoadout.SlotCount; i++)
                victim.Slots[i] = WeaponState.Empty;
            victim.Inventory = default;
            var crate = OfflineLootCrate.Spawn(victim.Feet, contents, victim.DisplayName, Now);
            if (crate != null)
            {
                crate.transform.SetParent(transform, true);
                _crates.Add(crate);
            }
        }

        // ------------------------------------------------------------------ shared queries for controllers and combatants

        /// <summary>Closest character the ray hits before <paramref name="maxDistance"/> (walls already applied).</summary>
        public OfflineCombatant RaycastCharacters(Vector3 origin, Vector3 direction, float maxDistance, OfflineCombatant shooter,
            out float distance, out bool headshot)
        {
            OfflineCombatant best = null;
            distance = maxDistance;
            headshot = false;
            foreach (var c in _combatants)
            {
                if (c == shooter || !c.Alive)
                    continue;
                if (c.Intersect(origin, direction, distance, out float d, out bool head) && d < distance)
                {
                    best = c;
                    distance = d;
                    headshot = head;
                }
            }
            return best;
        }

        /// <summary>Nearest living target in reach and arc with no wall in between (same rule as online melee).</summary>
        public OfflineCombatant FindMeleeTarget(OfflineCombatant attacker, float reach, float arcDegrees, float yaw)
        {
            OfflineCombatant best = null;
            float bestDistance = float.MaxValue;
            var feet = attacker.Feet;
            foreach (var c in _combatants)
            {
                if (c == attacker || !c.Alive)
                    continue;
                var p = c.Feet;
                if (!MeleeRules.InArc(feet.x, feet.z, yaw, p.x, p.z, reach + OfflineCombatant.BodyRadius, arcDegrees))
                    continue;
                if (Physics.Linecast(attacker.Chest, c.Chest, EnvironmentMask, QueryTriggerInteraction.Ignore))
                    continue;
                float d = (p - feet).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
            return best;
        }

        public OfflineCombatant Find(int id) => _byId.TryGetValue(id, out var c) ? c : null;

        public bool TryGetHunt(int id, out HuntStatus status)
        {
            status = default;
            if (_hunt == null || !_hunt.TryGet(id, out var tracker))
                return false;
            status = tracker.Status;
            return true;
        }

        public bool IsExposed(int id) => TryGetHunt(id, out var s) && s.BeaconVisible;

        public void EmitSound(SoundKind kind, Vector3 position, int sourceId)
        {
            _sounds[SoundCount % SoundBufferSize] = new SoundEvent { Kind = kind, X = position.x, Z = position.z, SourceId = sourceId, Time = Now };
            SoundCount++;
        }

        public void LocalMessage(string text) => Message?.Invoke(text);

        /// <summary>Distance to what the player is looking at (bot decision LOD; never affects rules).</summary>
        public float DistanceToViewer(Vector3 position)
        {
            var cam = Camera.main;
            return cam != null ? Vector3.Distance(cam.transform.position, position) : 0f;
        }

        public Vector3 RandomArenaPoint(System.Random random, float y)
        {
            var c = _area != null ? _area.transform.position : Vector3.zero;
            float r = _area != null ? _area.ZoneRadius : 50f;
            float a = (float)(random.NextDouble() * Math.PI * 2.0);
            float d = r * Mathf.Sqrt((float)random.NextDouble());
            return new Vector3(c.x + Mathf.Sin(a) * d, y, c.z + Mathf.Cos(a) * d);
        }

        // ------------------------------------------------------------------ loot (humans and bots, same rules)

        public OfflineLootCrate NearestCrate(Vector3 position, float range)
        {
            OfflineLootCrate best = null;
            float bestSqr = range * range;
            foreach (var crate in _crates)
            {
                if (crate == null)
                    continue;
                float d = (crate.transform.position - position).sqrMagnitude;
                if (d <= bestSqr)
                {
                    bestSqr = d;
                    best = crate;
                }
            }
            return best;
        }

        /// <summary>The most valuable capsule within <paramref name="range"/> for this bot (only what it could see on the map).</summary>
        public OfflineLootCrate BestLootFor(OfflineCombatant c, float range, out float value)
        {
            value = 0f;
            OfflineLootCrate best = null;
            float hp = c.Health.MaxHealth > 0f ? c.Health.Health / c.Health.MaxHealth : 1f;
            foreach (var crate in _crates)
            {
                if (crate == null || (crate.transform.position - c.Feet).sqrMagnitude > range * range)
                    continue;
                float crateValue = 0f;
                for (int i = 0; i < LootContainer.Capacity; i++)
                {
                    var item = crate.Contents[i];
                    if (!item.IsEmpty)
                        crateValue = Mathf.Max(crateValue, BotLootPlanner.Value(item, c.Slots, c.Inventory, hp, WeaponRegistry.Get));
                }
                float score = crateValue - Vector3.Distance(crate.transform.position, c.Feet) * 0.2f;
                if (crateValue > 0f && score > value)
                {
                    value = crateValue;
                    best = crate;
                }
            }
            return best;
        }

        /// <summary>Take one item (index) or all (-1). The authority re-checks life and distance.</summary>
        public string TryTakeLoot(OfflineCombatant c, OfflineLootCrate crate, int index)
        {
            if (c == null || !c.Alive || crate == null || !_crates.Contains(crate))
                return "That capsule is gone.";
            if ((crate.transform.position - c.Feet).sqrMagnitude > (OfflineLootCrate.InteractRange + 0.75f) * (OfflineLootCrate.InteractRange + 0.75f))
                return "Too far away.";
            int active = c.ActiveSlot;
            var result = LootClaims.Take(crate.Contents, c.Slots, ref active, ref c.Inventory, index, WeaponRegistry.Get);
            c.ActiveSlot = active;
            if (result.TookCombatItem)
                c.Protection.OnAggressiveAction(AggressiveAction.PickedUpCombatItem);
            return result.Message;
        }

        public void BotTakeLoot(OfflineCombatant bot, OfflineLootCrate crate, LootSelectionStrategy strategy, System.Random random)
        {
            float hp = bot.Health.MaxHealth > 0f ? bot.Health.Health / bot.Health.MaxHealth : 1f;
            int index = BotLootPlanner.Choose(crate.Contents, bot.Slots, bot.Inventory, hp, strategy, random, WeaponRegistry.Get);
            if (index >= 0)
                TryTakeLoot(bot, crate, index);
        }

        // ------------------------------------------------------------------ results (spec 38.16)

        private void SaveHistory()
        {
            if (Human == null || !_resolver.TryGet(Human.Id, out var me))
                return;
            Settings.MatchHistoryStore.Add(new MatchHistoryEntry
            {
                mode = "offline", // never mixed with online results (spec 38.16)
                unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                placement = me.Placement,
                participants = _resolver.ParticipantCount,
                kills = Human.Kills,
                damage = Human.DamageDealt,
                survivalSeconds = me.SurvivalSeconds,
                won = me.Winner,
                eliminatedBy = Human.EliminatedBy >= 0 ? Find(Human.EliminatedBy)?.DisplayName ?? string.Empty : string.Empty,
            });
        }

        /// <summary>Spec 38.16 extra details: bots eliminated, accuracy, difficulty.</summary>
        public string HumanResultDetails()
        {
            if (Human == null)
                return $"BOTS ONLY  ·  {Difficulty.ToString().ToUpperInvariant()}";
            float accuracy = Human.ShotsFired > 0 ? Human.ShotsHit * 100f / Human.ShotsFired : 0f;
            return $"BOTS ELIMINATED {Human.Kills}  ·  ACCURACY {accuracy:0}%  ·  DIFFICULTY {Difficulty.ToString().ToUpperInvariant()}  ·  OFFLINE (not ranked)";
        }

        private static void AdvanceFlow(GameFlowState state)
        {
            if (AppServices.IsInitialized && AppServices.Flow.Mode == GameMode.OfflineBattleRoyale)
                AppServices.Flow.TryAdvanceTo(state);
        }
    }
}
