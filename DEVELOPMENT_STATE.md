# Development state (handoff record)

_Last updated: 2026-10-13 â€” end of Phase 9 authoring_

## Phase map
15 phases in total: 1â€“14 from the master prompt, plus **9G Offline Ghost Protocol** (Section 38).
Code written: **1â€“9**. Remaining: 9G, 10, 11, 12, 13, 14.

## Current phase
**Phase 9 â€“ Complete match lifecycle.** Code and tests written. **Unity is now installed (6000.6.5f1 = 6.6):** on a scratch copy, everything except the Fusion module compiles with 0 errors/warnings and 299/299 EditMode tests pass in Unity. **Not yet run:** everything Unity/Fusion. The manual tests for Phases 2â€“8 (TESTING.md) are pending the project owner's Unity 6.3 + Fusion 2.1.3 + App ID setup.

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
Unity 6000.3.x Â· URP 17.3.0 Â· Input System 1.14.2 Â· AI Navigation 2.0.9 Â· Test Framework 1.5.1 Â· uGUI 2.0.0 Â· Photon Fusion 2.1.3 Stable (to install) Â· `NetworkProtocol.Version` = **9**.

## Key decisions (cumulative, newest last)
1. Unity 6.3 LTS + Fusion 2.1.3. Rules in pure C# shared by server, prediction, offline simulation and bots. Online isolated behind `WON_FUSION`.
2. Dedicated servers for public matches; host mode only for private test rooms.
3. Intent-only, server-sanitized input; full movement state networked and restored each tick.
4. Lobby-first sessions; server `MatchLifecycle`; synchronized start by shared simulation time; validated, spread-out spawns; spawn protection from FIGHT.
5. Phase 5: all guns hitscan from the camera shoulder point (exact crosshair) with an eye fallback against wall-peeking; lag-compensated hitboxes (body capsule + head sphere, stance-adjusted); player CharacterControllers on *Ignore Raycast* so only rewound hitboxes are hit; deterministic spread seeded by player, shot and pellet; recoil = bloom; weapon network id = catalog index (append-only); one damage entry point on the server; FX from replicated counters (owner plays predicted FX on forward ticks only); fixed dev loadout until Phase 6.
6. **Phase 6:** spare ammo lives in the inventory by type; loadouts are random packages balanced by power band; medkits consumed on completion only; loot capsule = 12 fixed networked slots; claims via validated ownerâ†’server RPC, processed one after another (atomic); items are moved, never copied; the loot panel is a client view with server-checked buttons.

7. **Phase 7:** the server plans all circles at the countdown but replicates only the current phase (no endgame leak); every peer interpolates the same circle from it. Inside/outside is classified per tick on the simulated body position (server authoritative, owner predicted); clients never send zone state. Instability is a deterministic, authority-applied direction bend (tick + player id) so prediction agrees and clients can't remove it. Zone damage runs once per server tick in exposure pulses through the single damage entry point. The phase table is scaled to the map's starting safe zone (the spawn area).
8. **Phase 8:** camping is judged only from server-simulated positions sampled at 1 Hz; a reset needs both net displacement and path inside a sliding window; trackers live in a server registry keyed by player (not on the avatar) so disconnects can't reset them; zone + camping are applied in one environmental pass per tick through the single damage entry point.
9. **Phase 9:** the server decides placements per completed tick (same-tick deaths ranked kills â†’ damage â†’ id; a match always has one winner when it started with 2+ players); results are server records (JSON) plus replicated per-player fields; clients only display them. Spectating is local camera work over already-replicated data. Unity checks run on a scratch copy at C:\Temp\wonchk because the repo is pinned to 6.3 and the machine has 6.6.

## Verified tests
- `dotnet test Tools/CoreTests` â†’ **279/279 passed** (2026-10-13).
- **Unity 6000.6.5f1:** non-Fusion project compiles (0 errors, 0 warnings); EditMode **299/299 passed**; PlayMode: 7 passed, 0 failed, 2 skipped (they need the scenes generated by Run Project Setup).
- 115/115 scripts parse as C# 9 (syntax only).

## External dependencies still required (project owner)
- Unity 6.3 LTS (Android, Windows/Linux Dedicated Server modules); Photon account + Fusion 2 App ID + Fusion SDK 2.1.3; 2+ clients for multiplayer tests.

## Next tasks
1. Decide the editor: install Unity 6.3 LTS (recommended, matches Fusion 2.1.3 support) or move to 6.6. Then install Fusion 2.1.3 + App ID, run setup, and run the manual tests for Phases 2â€“9 (TESTING.md).
2. **Phase 9G â€“ Offline Ghost Protocol:** a local authoritative simulation using the same Core rules (movement, combat, items, circle, hunt, match end) and AI bots (perception, personalities, difficulty profiles; defaults NIGHTMARE / 23), separate offline stats.
