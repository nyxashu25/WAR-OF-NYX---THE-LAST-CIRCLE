# Development state (handoff record)

_Last updated: 2026-10-13 — end of Phase 11_

## Phase map
15 phases in total: 1–14 from the master prompt, plus **9G Offline Ghost Protocol** (Section 38).
Code written: **1–11 and 9G**. Remaining: 12, 13, 14.

## Current phase
**Phase 11 – Android controls and builds.** Adjustable touch layout + editor, drag-to-aim on FIRE, gyro aim, APK / signed AAB build methods (key from environment variables only), device test plan in ANDROID_BUILD.md. Unity: EditMode 329/329. Blocked by the owner's setup: Android Build Support module (none installed) and physical phones; Fusion App ID for the networked part.

## Configuration requested by the project owner
- Offline Ghost Protocol defaults: **Difficulty = NIGHTMARE, Bots = 23** (in `GameConfig`, clamped by `OfflineCapacityPolicy`; difficulty never lowered).

## Implemented
| Area | Files | Verified? |
|---|---|---|
| Core rules | `Core/Flow`, `Core/Events`, `Core/Offline`, `Core/Movement` (+ authority `SpeedMultiplier`), `Core/Networking` (input codec now carries combat bits), `Core/Match` | Yes (.NET) |
| **Combat rules (Phase 5)** | `Core/Combat/WeaponStats`, `WeaponCatalog` (7 ranged + 4 melee), `WeaponRules`, `HealthRules`, `MeleeRules`, `CombatLoadout` (+`CombatCommand`, `CombatText`, `EventDeduplicator`), `AimGeometry` | Yes (.NET, 47 tests) |
| **Item rules (Phase 6)** | `Core/Items/ItemTypes` (rarity, medkits, armor, ammo caps, `PlayerInventory`), `LootContainer` (+`LootItem`), `InventoryRules` (+`HealingRules`), `LoadoutGenerator`; weapon reserve moved into the inventory pool | Yes (.NET, 29 tests) |
| **Circle rules (Phase 7)** | `Core/Zone/ZoneSettings` (spec table, wobble, sway, damage interval), `ZonePlanner` (`ZoneCircle`, `ZonePlan`, `IZoneCentreValidator`), `ZoneTimeline` (`ZoneStage`), `ZoneRules` (+`ZoneDamage`, `ZoneInstability`), `ZoneAnnouncer` (+`ZoneView`); `MovementCommand.LateralWobble` (authority-only, stripped by `InputCodec.Sanitize`); `CombatText.Environmental`; `-zoneSeconds` | Yes (.NET, 29 tests) |
| **Hunt rules (Phase 8)** | `Core/Hunt/NyxHunt.cs`: `HuntSettings`, `HuntTracker` (sampling, meaningful movement, state machine, penalty), `NyxHuntProtocol` (per-player, disconnect-safe), `EnvironmentDamage`, `HuntText`, `HuntStatusAnnouncer` | Yes (.NET, 19 tests) |
| **Match-end rules (Phase 9)** | `Core/Match/MatchResults.cs`: `MatchResolver` (placements, last survivor, tie-break), `KillNotices`, `MatchResultText`, `MatchHistory` (+entries, totals); `MatchLifecycle.EndMatch` + results timer; `GameStateMachine` new-round wrap | Yes (.NET + Unity EditMode) |
| **Bot rules (Phase 9G)** | `Core/Bots`: `BotPersonality` (+`BotNames`), `BotPerception` (+`BotMemory`, `SoundEvent`), `BotBrain` (19 states, utility, hysteresis) + `BotAim`, `BotLootPlanner`; `Core/Items/LootClaims` (shared by online + offline) | Yes (.NET + Unity) |
| Offline Ghost Protocol (9G) | `Gameplay/Offline`: `OfflineMatch` (local authority, 60 Hz ticks, circle, hunt, resolver, damage, loot, history) + `OfflineMatchRequest`, `OfflineCombatant`, `BotController` (perception, decisions, NavMesh, combat), `OfflineLootCrate`, `OfflineMatchHud`; `Gameplay/Effects/LootCapsuleVisual` (shared); `UI/Hud/LootViews` (shared); GhostProtocol scene builder; menu START | **Yes: compiled, 2 full matches run in Unity PlayMode** |
| Phase 11 controls/builds | `Core/Controls/TouchLayout` (+`GyroAim`), `UI/Mobile`: `MobileControlsUI` (layout, editor), `DraggableControl`, `TouchDragLook`; `PlayerInputRouter` gyro; `UserSettings.touchLayout`; EDIT TOUCH CONTROLS in the online menu and offline pause; `BatchTools.BuildAndroidApk/BuildAndroidAab` | Unity compile + EditMode; device tests pending |
| Phase 10 visuals | `Assets/Art/Shaders/NyxToon.shader`, `NyxSky.shader`; `Gameplay/Effects`: `StyleKit`, `CharacterStyler`, `WeaponModelFactory`, `EliminationEffect`, `QualityAwareVolume`, `WeaponVisualRig` (+models, hurt, heal, aim, victory), `ProceduralAudio` (+hurt, heal, victory, elimination); `Characters/CharacterRoster`; `Editor`: `VisualStyleBuilder`, `NyxIslandBuilder`, `ShowcaseCapture`, `BatchTools.RebuildIsland/DiagnoseIslandSpawns`; settings CHARACTER + offline MAP; victory camera online/offline; `NetworkPlayer.Look` (protocol 10) | Yes in Unity (compile, render, tests) |
| Phase 9 Unity/Fusion | `NetworkMatchController` (resolver, notices, end, freeze, results JSON, results view), `NetworkPlayer` (placement/survival/winner, spectate targets), `NetworkCombat` (elimination details), `NetworkNyxCircle.ServerFreeze`, session cleanup (capsules), `UI/Hud/MatchResultsPanel`, `CombatHud` (SPECTATE / RETURN TO LOBBY), `OnlineArenaController` (alive counter, spectating, results, history), `Settings/MatchHistoryStore`, main menu record line | Game/UI side compiles in Unity; Fusion side not compiled |
| Phase 8 Unity/Fusion | `NetworkPlayer` (+hunt fields, `ServerEnvironmentPass`, `All` registry, local hunt view), `NetworkCombat` (`StepZoneExposure`, `ApplyEnvironmentDamage`), `NetworkMatchController` (`Hunt`, `RPC_HuntAlert`, `OnPlayerLeftMatch`), `NetworkSessionManager` (leave hook), `Gameplay/Effects/HuntBeaconVisual`, `UI/Hud/HuntHud` (+`HuntTarget`), `Minimap.SetHuntMarkers`, `OnlineArenaController.ShowHunt` | No |
| Phase 7 Unity/Fusion | `Networking/NetworkNyxCircle` (+`NetworkZoneCircle`), `NetworkPlayer` (+`OutsideZone`, wobble, sway, zone pass), `NetworkCombat` (+`firearmsAllowed`, `ApplyZoneExposure`, `EliminationCause`), `NetworkMatchController` (feed cause), `NetworkSessionManager` (plans the circle at the countdown, stops it on reset), `ArenaSpawnArea` (+`PlayableFraction`), `Gameplay/Effects/NyxCircleVisual`, `OutsideZoneVisual`, `UI/Hud/ZoneHud`, `Minimap`, `UI/Common/UiShapes`, `OnlineArenaController.ShowZone`, camera shake, FIRE button disabled state, crosshair tint, prefab builder (+`NetworkNyxCircle`, auto-rebuild), fast-circle server menu item | No |
| Phase 6 Unity/Fusion | `Networking/NetworkLootCrate` (+12-slot struct), `NetworkCombat` (loadout, ammo pool, medkits, drop on death, `RPC_TakeLoot`, loot UI driver), `NetworkWeaponSlot.cs` (+`NetworkInventory`), `UI/Hud/LootPanel`, `UI/Common/RarityColors`, `CombatHud` (+inventory/heal/prompt/message), `OnlineArenaController` (+loot modal), router (+medkit, interact) | No |
| Unity combat data | `Combat/WeaponDefinition` (+`WeaponRegistry`), `Combat/WeaponDatabase` | No |
| Input | `Characters/PlayerInputRouter.ConsumeInput` (movement + combat, mouse fire only when captured) | No |
| HUD / FX | `UI/Hud/CombatHud`, `Gameplay/Effects/WeaponVisualRig`, `ProceduralAudio` (+gunshot, heavy shot, swing, reload, hit tick) | No |
| Fusion | `Networking/NetworkCombat` (new), `NetworkWeaponSlot` (new), `NetworkPlayer` (vitals moved to combat; combat after movement; weapon speed), `NetworkMatchController` (+eliminations, kill feed), `NetworkInputData` (+combat), `NetworkSessionManager` (sends combat input) | No (lag comp / hitbox APIs checked against Fusion 2.1.3 docs) |
| Editor | `Editor/WeaponAssetBuilder` (new), `Networking/Editor/NetworkPlayerPrefabBuilder` (+hitboxes, NetworkCombat, layer 2, auto-rebuild of old prefabs) | No |
| Earlier phases | see previous handoffs in git history | Core only |

## Generated by the setup tool (not in git until the first Unity run)
Scenes `Bootstrap`, `MainMenu`, `PracticeArena`, `MatchLobby`, `BattleArena` (+NavMesh). Prefabs `PlayerPlaceholder`; Fusion-only `Resources/WarOfNyx/NetworkPlayer` (with hitboxes), `MatchController`, `LootCrate`. Assets `MovementConfig`, `GameConfig`, `FX_Additive.mat`, **`ScriptableObjects/Weapons/*.asset` + `Resources/WarOfNyx/WeaponDatabase.asset`**, URP assets, graybox materials.

## Versions
Unity 6000.3.x · URP 17.3.0 · Input System 1.14.2 · AI Navigation 2.0.9 · Test Framework 1.5.1 · uGUI 2.0.0 · Photon Fusion 2.1.3 Stable (to install) · `NetworkProtocol.Version` = **9**.

## Key decisions (cumulative, newest last)
1. Unity 6.3 LTS + Fusion 2.1.3. Rules in pure C# shared by server, prediction, offline simulation and bots. Online isolated behind `WON_FUSION`.
2. Dedicated servers for public matches; host mode only for private test rooms.
3. Intent-only, server-sanitized input; full movement state networked and restored each tick.
4. Lobby-first sessions; server `MatchLifecycle`; synchronized start by shared simulation time; validated, spread-out spawns; spawn protection from FIGHT.
5. Phase 5: all guns hitscan from the camera shoulder point (exact crosshair) with an eye fallback against wall-peeking; lag-compensated hitboxes (body capsule + head sphere, stance-adjusted); player CharacterControllers on *Ignore Raycast* so only rewound hitboxes are hit; deterministic spread seeded by player, shot and pellet; recoil = bloom; weapon network id = catalog index (append-only); one damage entry point on the server; FX from replicated counters (owner plays predicted FX on forward ticks only); fixed dev loadout until Phase 6.
6. **Phase 6:** spare ammo lives in the inventory by type; loadouts are random packages balanced by power band; medkits consumed on completion only; loot capsule = 12 fixed networked slots; claims via validated owner→server RPC, processed one after another (atomic); items are moved, never copied; the loot panel is a client view with server-checked buttons.

7. **Phase 7:** the server plans all circles at the countdown but replicates only the current phase (no endgame leak); every peer interpolates the same circle from it. Inside/outside is classified per tick on the simulated body position (server authoritative, owner predicted); clients never send zone state. Instability is a deterministic, authority-applied direction bend (tick + player id) so prediction agrees and clients can't remove it. Zone damage runs once per server tick in exposure pulses through the single damage entry point. The phase table is scaled to the map's starting safe zone (the spawn area).
8. **Phase 8:** camping is judged only from server-simulated positions sampled at 1 Hz; a reset needs both net displacement and path inside a sliding window; trackers live in a server registry keyed by player (not on the avatar) so disconnects can't reset them; zone + camping are applied in one environmental pass per tick through the single damage entry point.
9. **Phase 9:** the server decides placements per completed tick (same-tick deaths ranked kills → damage → id; a match always has one winner when it started with 2+ players); results are server records (JSON) plus replicated per-player fields; clients only display them. Spectating is local camera work over already-replicated data. Unity checks run on a scratch copy at C:\Temp\wonchk because the repo is pinned to 6.3 and the machine has 6.6.
10. **Phase 9G:** one simulation path for human and bots (`OfflineCombatant`), commands only from controllers, the local match as authority with the same Core rules as the server; characters on Ignore Raycast with explicit hit shapes equal to the online hitboxes; bots get information only from their senses (LOS, hearing with error, memory) and the HUD-visible circle; no bot name tags (they render through walls).
11. **Phase 10:** one shared toon material coloured per renderer (property blocks) keeps batching cheap; visuals never touch hit shapes; the outline and post-processing follow the quality tier; NYX ISLAND is generated by an editor builder (reproducible, reviewable in screenshots) rather than hand-placed, until real art replaces it. Cylinders use mesh colliders (Unity's capsule collider broke NavMesh reachability).

## Verified tests
- `dotnet test Tools/CoreTests` → **309/309 passed** (2026-10-13).
- **Unity 6000.6.5f1:** non-Fusion project compiles (0 errors, 0 warnings); setup OK incl. NYX ISLAND; EditMode **329/329**; PlayMode **12/12** incl. three complete offline matches; showcase screenshots rendered; Windows player builds and starts.
- 115/115 scripts parse as C# 9 (syntax only).

## External dependencies still required (project owner)
- Unity 6.3 LTS (Android, Windows/Linux Dedicated Server modules); Photon account + Fusion 2 App ID + Fusion SDK 2.1.3; 2+ clients for multiplayer tests.

## Next tasks
1. Owner: Unity 6.3 LTS + Android Build Support (recommended), Fusion 2.1.3 + App ID, two Android phones → ANDROID_BUILD.md device test plan.
2. **Phase 12 – Public infrastructure:** Linux dedicated server container (Dockerfile, compose), health endpoint, structured logs, match/idle shutdown, Edgegap-ready config, deployment docs.
