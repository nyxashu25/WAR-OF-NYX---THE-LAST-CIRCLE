# Testing

## Verified results
Record every real run here, with the date and toolchain.

| Date | Toolchain | Suite | Result | Notes |
|---|---|---|---|---|
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` (WarOfNyx.Core + GameStateMachine, GameEventBus, OfflineSettings/BotDifficulty tests) | **39 passed, 0 failed** | Run outside Unity; warnings treated as errors |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 29 `.cs` files under `Assets/` | **0 syntax errors** | Parse only. Unity API usage (types, members) is **not** checked |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 2 (adds `MovementModelTests`) | **69 passed, 0 failed** | 30 new movement tests |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 46 `.cs` files under `Assets/` (Phase 2) | **0 syntax errors** | Parse only |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 3 (adds `NetworkingCoreTests`) | **104 passed, 0 failed** | 35 new: launch args, room codes, names, input codec, spawn ring |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 60 `.cs` files under `Assets/` (Phase 3, incl. Fusion module) | **0 syntax errors** | Parse only. Fusion API names were checked against the official Fusion 2.1.3 API reference, but never compiled against the SDK |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 4 (adds `MatchStartTests`; removes the retired spawn-ring tests) | **144 passed, 0 failed** | 43 new: lifecycle, spawn planner, protection, matchmaking queue, countdown, flow catch-up, server args |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 72 `.cs` files under `Assets/` (Phase 4) | **0 syntax errors** | Parse only. New Fusion members checked against the 2.1.3 API reference |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 5 (adds `CombatRulesTests`) | **191 passed, 0 failed** | 47 new: catalog, fire rate, semi/auto/burst, pellets, ammo never spent on blocked shots, reload, spread/bloom, falloff, headshots, health/armor/protection, melee timing/arc, combat input, loadout, kill feed, dedup, aim geometry, weapon speed. Caught and fixed a real bloom-tuning bug |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 87 `.cs` files under `Assets/` (Phase 5) | **0 syntax errors** | Parse only. Lag-compensation / hitbox members checked against the Fusion 2.1.3 API reference |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 6 (adds `ItemsTests`; reload tests rewritten for the ammo pool) | **220 passed, 0 failed** | 29 new: loadout band over 1,000 seeds, packages, caps, capsule contents, single claim, rarity, pickups (fill, swap, melee, partial, refuse, armor), racing claim, healing (choice, channel, interrupt, cap, missing medkit) |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 95 `.cs` files under `Assets/` (Phase 6) | **0 syntax errors** | Parse only |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 7 (adds `ZoneTests`) | **249 passed, 0 failed** | 29 new: spec table, validation, containment over 500 seeds, map scaling, determinism, fair centres, 60 s linear shrink, final/closed circle, monotonic radius, replicated stage = server circle, test override, boundary, gun inside/outside without ammo loss, trigger held across the edge, no grace window, forged input, melee outside, zone damage per phase, re-entry, in-and-out dodging, armor ignored, wobble bounds/determinism/speed/no drift, aim sway, announcements |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 107 `.cs` files under `Assets/` (Phase 7) | **0 syntax errors** | Parse only. `Runner.Tick`, `Tick.Raw` and `Runner.LocalRenderTime` checked against the Fusion 2.1.3 API reference |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 8 (adds `HuntTests`) | **268 passed, 0 failed** | 19 new: spec settings, 10/20/25/30 s, 10 s exposure + penalty, repeated exposure without spam, clearing, rotating, small move, circling, jiggling, teleport, wall, spawn grace, reconnect, death, three campers, combined zone+camping pass, texts, local notices |
| 2026-10-13 | Roslyn 4.12 syntax parse, C# 9 | All 111 `.cs` files under `Assets/` (Phase 8) | **0 syntax errors** | Parse only |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 9 (adds `MatchEndTests`; flow test extended for new rounds) | **279 passed, 0 failed** | 11 new: placements and last survivor, per-tick resolution, same-tick tie-break, everyone-falls winner, leavers, frozen result, solo test room, kill notices, texts, history totals, lifecycle end → results → lobby |
| 2026-10-13 | **Unity 6000.6.5f1 (6.6)**, batch mode, on a scratch copy of the project (Fusion not installed, so `WarOfNyx.Networking` and the Multiplayer tests are excluded by their define constraint) | Import + compile | **0 errors, 0 warnings** after one fix | First real compile. Found and fixed 1 error (CS0165 in `MainMenuController.AddStepper`). Assemblies built: Core, Game, Editor, Tests.EditMode, Tests.PlayMode |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner, batch mode | EditMode (all suites incl. `ProjectFoundationTests`) | **299 passed, 0 failed** | First run inside Unity |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner, batch mode | PlayMode (`BootstrapSmoke`, `PlayerMovement`, `SpawnValidation`) | **7 passed, 0 failed, 2 skipped (they need the scenes generated by Run Project Setup)** | |
| 2026-10-13 | **Unity 6000.6.5f1**, batch mode | `BatchTools.Setup` (= Run Project Setup without dialogs) | **all 9 steps OK** | First real setup run: Android settings, URP presets, GameConfig, all 5 scenes + NavMesh, prefabs, weapons, Input System |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner | PlayMode after setup | **9 passed, 0 failed** | Found and fixed 2 real bugs: the Practice Arena scene lost its player-prefab reference when generated (asset unloaded by the new scene), and PlayMode tests leaked a loaded arena into later tests (now each test starts in an empty world) |
| 2026-10-13 | **Unity 6000.6.5f1** | `BatchTools.BuildWindowsPlayer` (Windows x64, without Fusion) | **Build succeeded** (101 MB, 101 s) | Started headless for 15 s: no crash, services start (`Max offline bots: 23` on this PC) |
| 2026-10-13 | .NET SDK 10.0.401, C# 9, NUnit 3.14 | `Tools/CoreTests` after Phase 9G (adds `BotTests`) | **305 passed, 0 failed** | 26 new: vision/LOS/FOV/hearing/memory, 19-state reachability (caught 2 unreachable states, fixed), decisions, aim, loot choice, names, shared loot claims |
| 2026-10-13 | **Unity 6000.6.5f1** | Compile + setup (adds the GhostProtocol scene + NavMesh) | **0 errors, 0 warnings** | |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner | EditMode | **323 passed, 0 failed** | |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner | PlayMode incl. `GhostProtocolPlayModeTests` | **11 passed, 0 failed** | Spec 38.17 acceptance test: 11 bots with no human finished in 93 simulated s, winner KAGE_46, 10 combat eliminations; 1 idle human + 11 bots: the idle human was exposed and eliminated by the Nyx Hunt (#4), a bot won with 5 kills. Simulated at 12× speed, about 8 real s each |
| 2026-10-13 | **Unity 6000.6.5f1** | Setup incl. NYX ISLAND + visual style; shaders `NyxToon`, `NyxSky` | **0 errors, 0 warnings** | Fixed a Unity 6.6 deprecation warning (`FindObjectsByType` sort mode) with a version-neutral scene walk |
| 2026-10-13 | **Unity 6000.6.5f1** with GPU (batch, no -nographics) | `ShowcaseCapture` (8 screenshots) | **Rendered and reviewed** | Found and fixed: procedural sky gave a yellow band (now a custom gradient sky); terrain looked like water because URP terrain reads diffuse alpha as smoothness (now matte) |
| 2026-10-13 | **Unity 6000.6.5f1** | NYX ISLAND spawn diagnostic (400 samples) | **389 valid** after a fix | First run: 0 valid; the cylinder primitive's capsule collider made the island centre unreachable. Cylinders now use mesh colliders and the spawn area has a plaza anchor |
| 2026-10-13 | **Unity 6000.6.5f1**, Test Runner | EditMode / PlayMode | **325 / 12 passed, 0 failed** | New: bots-only match on NYX ISLAND (11 bots, winner after 2:16, 10 eliminations) |
| 2026-10-13 | .NET + **Unity 6000.6.5f1** | Core / EditMode after Phase 11 (adds `ControlsTests`) | **309 / 329 passed** | 4 new: layout offsets clamped per button, corrupted layouts repaired, reset, gyro mapping both landscapes |
| 2026-10-13 | **Unity 6000.6.5f1** | `BatchTools.BuildAndroidApk` | **Stopped as designed** | Android Build Support not installed on this PC: exit code 1 with the install instructions |
| 2026-10-13 | .NET + **Unity 6000.6.5f1** | Core / EditMode / PlayMode after Phase 12 (adds `ServerOperationsTests`, `ServerRuntimePlayModeTests`) | **314 / 334 / 13 passed** | 5 new core tests (environment config, invalid values, one-match exit, idle shutdown, JSON escaping); PlayMode: real HTTP requests to the health endpoint (503 while starting, 404 elsewhere) |
| — | Docker | image build / run | **Not run** | Docker isn't installed here, and the Linux server build needs Fusion |
| 2026-10-13 | .NET + **Unity 6000.6.5f1** | Core / EditMode / PlayMode after Phase 13 (adds `RobustnessTests`, `OfflinePerformanceTests`) | **319 / 339 / 16 passed** | Fuzz: 20 000 hostile commands through the sanitizer and movement model, 5 000 button masks, 5 000 damage values. Benchmark: 23 NIGHTMARE bots on NYX ISLAND = 0.252 ms average tick (PERFORMANCE.md) |
| 2026-10-13 | **Unity 6000.6.5f1** | `ReleaseTools.GenerateBranding` + `ValidateRelease` | **2 blocked, 1 warning** | Blocked: Android Build Support, Fusion SDK. Warning: target API 'Auto'. Everything else passes (package, version, IL2CPP, ARM64, min API, landscape, permissions, no keys, icon, scenes, privacy text) |
| 2026-10-13 | **Unity 6000.6.5f1** | Windows player (final) | **Built (110 MB) and started** | Copied to `Builds/WindowsPlayer`, desktop shortcut created |
| 2026-10-13 | **Unity 6000.3.26f1** (6.3 LTS, the pinned version) | `BatchTools.Setup` + compile | **No errors, no warnings** | Fresh copy of the repo; all setup steps OK (scenes, NYX ISLAND, visual style, branding) |
| 2026-10-13 | **Unity 6000.3.26f1** | EditMode / PlayMode | **339 / 16 passed, 0 failed** | Same results as on 6.6, including the three complete offline matches |
| 2026-10-13 | **Unity 6000.3.26f1** + Android Build Support | `BatchTools.BuildAndroidApk` | **Built: 46 MB APK** | `aapt`: package com.nyxgames.warofnyx 0.1.0 (1), min API 26, target API 36, arm64-v8a only, permissions INTERNET + ACCESS_NETWORK_STATE; `apksigner`: debug certificate. Offline-only (no Fusion). **Not yet installed on a phone** |
| 2026-10-13 | **Unity 6000.3.26f1** | `ReleaseTools.ValidateRelease` | **1 blocked, 0 warnings** | Blocked: Fusion SDK. Android module and target API 36 now pass |
| 2026-10-14 | .NET + **Unity 6000.3.26f1** | Core / EditMode / PlayMode after the environment pass (adds `AtmosphereTests`, `EnvironmentTests`, `EffectVisualTests`) | **326 / 355 / 16 passed** | New: deterministic weather plans, fog never below 250 m, mesh budgets / face directions / wind weights, grass shorter than a crouched player, weather lighting, hunt beacon hidden until exposed. One real bug found by the tests: pine canopy undersides lit like tops (fixed) |
| 2026-10-14 | **Unity 6000.3.26f1** Windows player | `WarOfNyx.exe -devCapture` (offline NYX ISLAND match, 10 screenshots) | **Reviewed** | Found and fixed: terrain grass not drawn (coverage scatter mode), palms inland (coast by height), over-bright ground, grass blades blocking the camera |
| 2026-10-14 | **Unity 6000.3.26f1** + Android | APK + `ValidateRelease` | **Built (63 MB); 1 blocked (Fusion), 0 warnings** | Not yet run on a phone |
| 2026-10-14 | .NET + **Unity 6000.3.26f1** | Core / EditMode / PlayMode after the combat, animation and detail pass | **331 / 362 / 16 passed** | New: spray hit shares (AR hip > 45 % at 20 m, aimed > 45 % at 50 m), melee combo chains / window / reach, walk binding, two-bone IK, grass styles and vertex budgets |
| 2026-10-14 | **Unity 6000.3.26f1** | NYX ISLAND v3 spawn diagnostic (400 samples) | **388 valid** | With ~3 000 new prop parts (389 before) |
| 2026-10-14 | **Unity 6000.3.26f1** Windows player | `-devCapture -devCaptureOnly characters` / `places` (55 screenshots) | **Reviewed** | Walk / run / sprint / aim / reload poses, every katana and energy blade combo attack with trails, windowed streets day and night, temple, fortress, docks, core. Fixed during review: unlit window glass too bright at night |
| 2026-10-14 | **Unity 6000.3.26f1** + Android | APK | **Built (63.5 MB)** | Not yet run on a phone |
| 2026-10-14 | .NET + **Unity 6000.3.26f1** | Core / EditMode / PlayMode after the sound and free-look pass | **331 / 366 / 16 passed** | New: every gun has 3 distinct, non-clipping shot variants and a duller distant version; reload sounds last the reload time; footsteps for every surface; free-look binding |
| 2026-10-14 | **Unity 6000.3.26f1** Windows player | `-devCapture -devCaptureOnly freelook` | **Works** | Holding the eye turned the camera 150° (aim unchanged at 273°, character seen from the front, crosshair hidden); after release the offset returned to 0° |
| 2026-10-14 | **Unity 6000.3.26f1** + Android | APK | **Built (63.5 MB)** | Not yet run on a phone; sounds not yet heard on a device |
| — | Android phone | install + play the APK | **Not run** | Needs the owner's phone (ANDROID_BUILD.md) |

## Running the Unity checks from the command line
- Setup without dialogs: `Unity.exe -batchmode -nographics -projectPath <project> -executeMethod WarOfNyx.EditorTools.BatchTools.Setup -quit`
- Tests: `Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testResults editmode.xml` (same with `PlayMode`)
- Windows player: `... -executeMethod WarOfNyx.EditorTools.BatchTools.BuildWindowsPlayer -quit` → `Builds/WindowsPlayer/WarOfNyx.exe` (also in the menu: **War of Nyx → Build → Windows Player (offline capable)**)
- Use a short project path on Windows (e.g. `C:\Temp\...`): very long paths break Unity's package cache (260-character limit).

## Running the core rules tests without Unity
Requires the .NET SDK (10.x):
```bash
dotnet test Tools/CoreTests
```
`Tools/CoreTests/CoreTests.csproj` links the files in `Assets/Scripts/Core` and the engine-independent test files. It compiles them with the same C# version as Unity (C# 9), so the code under test is the same code Unity builds.

## How to run
**In the editor:** Window → General → **Test Runner** → *EditMode* → Run All, then *PlayMode* → Run All.

**From the command line (CI):**
```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile -
```
```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile -
```
The PlayMode smoke test is skipped (Ignored) until **War of Nyx → Setup** has created the scenes.

## Phase 1 test inventory (written, unverified)
| File | Covers |
|---|---|
| `Tests/EditMode/GameStateMachineTests.cs` | Online lifecycle order, offline skipping matchmaking/lobby, no state skipping, online re-queue vs offline play-again, abort to menu from any state, mode clearing |
| `Tests/EditMode/GameEventBusTests.cs` | Typed delivery, unsubscribe, throwing handlers isolated, unsubscribe during publish |
| `Tests/EditMode/OfflineSettingsTests.cs` | Bot counts 5/11/15/23, 24/24 competitor count, capacity clamping (never raises, never changes difficulty), device tiers, difficulty monotonicity, Nightmare human-plausibility limits, invalid profiles rejected |
| `Tests/EditMode/ProjectFoundationTests.cs` | GameConfig defaults (NIGHTMARE / 23), quality level lookup, FPS targets, every input action present, spec desktop bindings, every action has a touch-compatible gamepad binding |
| `Tests/PlayMode/BootstrapSmokeTests.cs` | Bootstrap → MainMenu loads, services initialized, flow in MainMenu, menu UI built |

## Phase 2 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/MovementModelTests.cs` | Walk/sprint/crouch/aim speeds, analog scaling, diagonal normalization, deadzone, stopping, yaw→direction, NaN input; crouch toggle, headroom block, auto-stand; jump apex 1.5 m, no double jump, coyote time, jump buffer, air momentum; slide from sprint, crouch-while-sprinting slide, no slide from walk, cooldown, tunnel end, wall end, jump cancel; collision feedback; determinism; dt = 0 | **30/30 pass** (.NET) |
| `Tests/PlayMode/PlayerMovementPlayModeTests.cs` | Real CharacterController: walk distance, wall blocking, jump height, crouch tunnel (standing blocked, crouched passes, can't stand inside), slide distance and low capsule, Practice Arena spawns a grounded player with a camera | Not run (needs Unity) |

## Manual test steps (Phase 2, Practice Arena)
1. Main menu → **PRACTICE ARENA**. The player spawns on the gold pad and the mouse is captured.
2. **Sprint lane** (left, white markers every 10 m): W for 5 s ≈ 20 m. Shift+W for 5 s ≈ 32 m. F3 shows the live speed.
3. **Ramps:** 20° and 35° can be walked up. 50° (red) cannot.
4. **Stairs:** walk up smoothly with no hitching.
5. **Jump boxes:** 0.5 / 1.0 / 1.4 m can be jumped onto. 2.0 m (red) cannot.
6. **Tunnel** (red roof): blocked when standing. C to crouch → walk through. C inside → you stay crouched. Exit → C stands you up.
7. **Slide bar:** sprint, then Ctrl (or C) → you slide under the bar.
8. **Camera:** back into a wall → the camera pulls in and never shows the inside of geometry. RMB → zoom.
9. **Touch:** F2 on PC (or run on a phone). Joystick anywhere bottom-left, swipe right side to look, JUMP / CROUCH / SLIDE / SPRINT / AIM work. Fire etc. are dimmed. Two fingers (move + look) at once don't interfere.
10. **Esc / II** pauses (cursor freed). RESUME continues, MAIN MENU returns.

## Manual test steps (Phase 1)
1. Open `Bootstrap.unity`, press Play → the menu appears with no Console errors.
2. Disable your network adapter → within ~5 s the status says **NOT AVAILABLE** and ONLINE greys out. Re-enable → **CONNECTED**.
3. OFFLINE BATTLE ROYALE → NIGHTMARE and 23 BOTS are preselected, and the summary reads `PLAYERS ALIVE AT START: 24/24`.
4. Change to 11 bots / Elite → stop Play, press Play again → the choice is remembered.
5. Settings → switch LOW/MEDIUM/HIGH → no errors. The Game view stats show the quality level change.
6. Press Esc on a sub-screen → returns to Home.
7. Game view → set a phone resolution (e.g. 2400×1080) → the layout stays inside the safe area.

## Planned suites (later phases)
Gameplay: spawn validity and separation, loadout power budget, damage/ammo/reload, healing caps, elimination attribution, loot atomicity, circle interpolation and containment, boundary classification, outside-circle firearm lock (with latency), melee outside, camping timer, recovery, beacon lifetime, penalty, winner and tie policy.
Network: multi-client connect, late-join rejection, reconnect, simultaneous pickups, duplicate event IDs, forged commands, movement validation, cleanup.
Mobile: aspect ratios, multitouch, background/foreground, offline interruption, presets, long sessions.
Conditions: 30/100/200 ms latency, 2 % loss, short drops, client kill.
Ghost Protocol: the 25 cases from spec 38.17, plus the hands-off "bots finish the match alone" acceptance run.

## Phase 3 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/NetworkingCoreTests.cs` | Server launch arguments (valid, invalid, missing values), room code sanitizing and generation (no look-alike characters), display-name sanitizing and fallback, input packing round trip, forged input (oversized stick, NaN, wild angles, unknown bits), neutral input, spawn ring spacing and facing | **35/35 pass** (.NET) |
| `Tests/Multiplayer/NetworkStateTests.cs` | Networked movement state and input survive the round trip. Forged network input is sanitized. | Not run (needs Unity + Fusion) |

## Manual test: two real clients (Phase 3 acceptance)
Prerequisites: Fusion 2.1.3 imported, App ID set, **Run Project Setup** done, both machines online.

**A. Private room (quickest)**
1. Build a Windows client (**War of Nyx → Multiplayer → Build Windows Test Client**), or use a second Unity editor or a phone build.
2. Instance 1: ONLINE → name "KAI" → **HOST PRIVATE ROOM**. Note the room code shown in the HUD.
3. Instance 2: ONLINE → name "AKIRA" → type the code → **JOIN ROOM**.
4. Expected: both are in BattleArena, the HUD shows `PLAYERS 2/12`, each sees the other capsule with a name tag. Movement (walk, sprint, crouch, slide, jump) appears smoothly on the other screen. Players can't walk through each other.
5. Instance 2 → II → **LEAVE MATCH** → back at the menu. Instance 1's HUD shows `PLAYERS 1/12`, and the other capsule disappears.

**B. Dedicated server (spec target)**
1. **Build Windows Dedicated Server**, then **Launch Local Windows Server (WON-DEV)**. Check `Builds/WindowsServer/server.log` for "running in session 'WON-DEV'".
2. Two clients → ONLINE → **JOIN DEV SERVER (WON-DEV)**.
3. Same expectations as A.4. Neither client is the authority (role CLIENT in the HUD). Closing one client doesn't affect the other.

**C. Robustness**
- Enter a non-existent room code → a clear error appears on the ONLINE screen and you stay in the menu.
- Stop the server while clients are connected → clients return to the menu with "Session ended / Disconnected".
- Unity's network simulation (NetworkProjectConfig → Network Conditions) at 100 ms / 2 % loss → the local player still moves smoothly (prediction), and remote players stay smooth (interpolation).

## Phase 4 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/MatchStartTests.cs` | Lifecycle: waiting, minimum, fill timer, full lobby, cancel on leave, load timeout kick and continue, too few loaded → back to lobby, leaver during loading, reset when empty, duplicate join, private-room host start, solo test start. Spawn planner: 65 m for 12 players on 500 m, inside zone and margin, relaxing on small maps, never invalid ground, clean failure, no overlap, deterministic seed, facing the centre, spread across regions. Protection, matchmaking queue, countdown text, phase → flow, flow catch-up, server args | **43/43 pass** (.NET) |
| `Tests/PlayMode/SpawnValidationPlayModeTests.cs` | Validator with real colliders: never inside a wall or under a roof, never off the map | Not run (needs Unity) |

## Manual test: matchmaking and synchronized start (Phase 4 acceptance)
Prerequisites: as for Phase 3. Re-run **Run Project Setup** (it adds the MatchLobby scene, the FX material, the NavMesh and the MatchController prefab). If BattleArena was generated in Phase 3, use **Rebuild Practice Arena (overwrite)** so it gets the spawn zone and NavMesh.

**A. Dedicated server, Quick Match, two players**
1. **Build Windows Dedicated Server**, then **Launch Local Windows Server (WON-DEV)**. The launch arguments include `-minPlayers 2 -lobbyFill 10`. The log shows the lobby.
2. Client 1: ONLINE → **QUICK MATCH**. It lands in the WAITING LOBBY with `PLAYERS 1 / 12  NEEDED TO START 2`.
3. Client 2: **QUICK MATCH**. Both show `MATCH STARTS IN 10s`.
4. After 10 s both load BattleArena. Each client sees `WAITING FOR ALL PLAYERS TO LOAD`, then both spawn with the portal effect, dropping in at **different places** (server log: `min separation … m`).
5. Both see **3 → 2 → 1 → FIGHT!** at the same moment. Nobody can move before FIGHT; looking around works.
6. After FIGHT, each has a cyan shield and `SPAWN PROTECTION 3.0s` counting down.
7. A third client pressing QUICK MATCH during or after loading does **not** join this match. It keeps searching, and after 30 s **PLAY OFFLINE WITH BOTS** appears.
8. Both leave → the server log shows "Returning to the lobby and reopening the session". A new Quick Match can join again.

**B. Load timeout**: start a match, then kill one client during loading (Task Manager). After 20 s the server removes it and starts with the others (if at least 2 remain). Otherwise it returns to the lobby.

**C. Private room**: HOST PRIVATE ROOM → lobby with START (the host can start alone for testing) → friend joins by code → START → same countdown and spawn behaviour.

## Phase 5 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/CombatRulesTests.cs` | Spec weapon values, ≥6 ranged and ≥2 melee, stable network ids, sustained fire always blooms; fire rate at 60/120 Hz, forged rapid fire rejected, semi needs a press, 3-round burst, 8 pellets per shell, blocked shots never cost ammo (incl. outside-circle flag), empty → auto reload, reload math, cancel gives nothing, spread/bloom/aim, falloff and headshots, deterministic bounded spread; health, overkill, no double kill, protection blocks player damage only, armor levels, zone/camping ignore armor, invalid damage, heal cap; melee windup/once/cooldown, arc and reach; combat input round trip, forged multi-slot, dev loadout, switching, kill feed text, dedup, aim geometry; weapon speed modifier, forged speed multiplier ignored | **47/47 pass** (.NET) |
| `Tests/Multiplayer/NetworkStateTests.cs` (+2) | Networked weapon slot and combat buttons round trip | Not run (needs Unity + Fusion) |

## Manual test: two players eliminate each other (Phase 5 acceptance)
Prerequisites: re-run **Run Project Setup**. It creates the weapon assets and **rebuilds the network player prefab with hitboxes** automatically.
1. Two clients join the same match (Quick Match on the local dev server, or a private room) and wait for **FIGHT!**.
2. Each has `[1] NX-7 Assault Rifle  [2] Neon Pistol  [3] Katana`, `30 / 90`, health 100.
3. Player A shoots player B (LMB or FIRE). A sees tracers, muzzle flash and a **hit marker** with a tick sound. B's health bar drops by 24 per body hit (36 for a headshot at close range), and B sees a red flash plus a direction arc pointing at A.
4. During the 3 s spawn protection after FIGHT, hits do no damage. Once A fires, A's own protection ends immediately.
5. R reloads (`RELOADING …%`). Empty magazine → reloads automatically. Keys 1/2/3 switch weapons, and the move speed changes slightly.
6. F (or MELEE) swings the katana: arc FX, and 45 damage when B is within reach, in front and not behind a wall.
7. B reaches 0 → B's capsule topples, B sees **ELIMINATED – Eliminated by A with …**, both kill feeds show `A eliminated B with …` exactly once, and A can walk through B's body.
8. Both clients agree on the result. The server log shows `Elimination #1: player X -> player Y`.
9. Fairness: a client can't fire faster than the weapon allows, and spamming fire while reloading or switching does nothing.

## Phase 6 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/ItemsTests.cs` | Every package can land in the power band; 1,000 generated loadouts all valid and inside the band, all six packages appear; deterministic seeds; spec package table; ammo and medkit caps; eliminated player drops everything (magazines kept); single claim; invalid indices; capsule rarity; full container; pickups: fill empty slot, swap active, melee to melee slot, partial ammo, full refused with nothing lost, armor better/worse; two players racing for one item → exactly one copy; medkit choice; channel/heal/consume; interrupted heal costs nothing; heal cap; can't use missing/unneeded medkits; spec medkit table | **29/29 pass** (.NET) |
| `Tests/Multiplayer/NetworkStateTests.cs` (+2) | Inventory and loot slots survive the network round trip | Not run (needs Unity + Fusion) |

## Manual test: loadouts and looting (Phase 6 acceptance)
Prerequisites: re-run **Run Project Setup** (builds the LootCrate prefab).
1. Two players start a match. Each spawns with a **different random package**. The server log shows `[Loadout] Shadow Runner (power 58): …`. The HUD shows e.g. `ARMOR L1  MEDKITS S1 M1 L0  LIGHT AMMO 80`, and slot 2 is empty.
2. Take some damage, then press **H / MEDKIT**: `HEALING …%`. Sprinting or firing cancels it and keeps the medkit. Completing it heals (never above 100) and uses one medkit. At full health: "Health is already full."
3. A eliminates B. A glowing capsule (colour = best item's rarity) appears where B fell, with a light beam.
4. A walks up: `[E] OPEN LOOT – B (90s)`. E opens the panel with B's weapon (with its remaining magazine), ammo, medkits and armor.
5. TAKE B's primary → it goes into A's empty slot 2 (or SWAP replaces the active weapon, and A's old weapon appears in the capsule). TAKE the ammo → the reserve increases. TAKE ALL → takes what fits.
6. Concurrency: with a third player, both open the same capsule and click the same item at the same time → exactly one gets it, the other sees "Already taken."
7. Walking away, Esc or CLOSE closes the panel. An empty capsule disappears. An untouched one disappears after 90 s (it blinks in the last 10 s).

## Phase 7 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/ZoneTests.cs` | Plan: spec table, invalid tables, every circle inside the previous (500 seeds), scaling, determinism, fair centres. Timeline: 60 s linear phases, final + closed circle, radius never grows, replicated stage gives the server's circle, delay, `-zoneSeconds`. Rules: boundary, gun inside/outside (no ammo spent), trigger held across the edge, no grace window, forged input stripped, melee outside. Damage: per phase, re-entry, in-and-out, armor ignored. Instability: 5–8 % bounds, deterministic per tick/player, keeps speed, no drift, aim sway. Announcer: each notice once, leave/enter | **29/29 pass** (.NET) |
| `Tests/Multiplayer/NetworkStateTests.cs` (+1) | Circle survives the network round trip | Not run (needs Unity + Fusion) |

## Manual test: the Nyx Circle (Phase 7 acceptance)
Prerequisites: re-run **Run Project Setup** (rebuilds the MatchController prefab with `NetworkNyxCircle`). Build the server, then use **War of Nyx → Multiplayer → Launch Local Windows Server (WON-DEV, fast circle 20 s)** so the whole circle runs in 3 minutes.
1. Start a two-player match. During "3, 2, 1" the violet barrier is already visible around the whole arena. The minimap (top right) shows the arena, you (gold arrow), the circle (cyan) and the next circle (white). The status under it reads `NYX CIRCLE 1/9 · SHRINKS IN …`, then at FIGHT `SHRINKING 0:20`.
2. "NYX CIRCLE SHRINKING" appears and the feed says `Nyx Circle phase 1/9: shrinking for 20 s`. The barrier closes smoothly on both clients and matches the minimap. The server log lists all planned circles.
3. Walk outside. Immediately: crimson `RETURN TO SAFE ZONE · N m / FIREARMS DISABLED OUTSIDE THE CIRCLE`, crimson crosshair, FIRE button greyed out ("NO FIRE / MELEE ONLY" on touch), violet screen edges, mild camera tremor, and your character trembles with violet arcs (the other player sees this too). Health drops 1 HP per second (two pulses of 0.5).
4. Hold fire outside → no shots, magazine unchanged, "WEAPONS DISABLED" message. Walk out across the edge while holding fire → shooting stops exactly when you cross.
5. Melee the other player outside → damage applies. Use a medkit and reload outside → both work.
6. Walk forward outside → you drift gently left and right (a few degrees), never stopped or pushed. Standing still: no drift.
7. Walk back in → "SAFE ZONE ENTERED", guns work at once, all effects stop, no more damage.
8. Settings → REDUCE SHAKE / REDUCE FLASHING: the tremor, edge pulse and lightning soften. Damage, wobble and the gun lock stay exactly the same.
9. Let the circle reach the end: the feed shows "FINAL CIRCLE BEGINS IN 10 SECONDS", then "FINAL CIRCLE" (6 HP/s outside), then "THE NYX CIRCLE HAS CLOSED". A player left in it dies: the feed reads `<name> was consumed by the Nyx Circle` and the elimination screen says "Consumed by the Nyx Circle." (Victory and results arrive in Phase 9.)

## Phase 8 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/HuntTests.cs` | All 13 spec 13 test cases that can run without the network (stationary 10/20/25/30 s, 10 s exposure, penalty start, meaningful movement clears, rotation, small move, reconnect, death removes the beacon, several campers, zone + camping in one pass) plus the anti-exploit list (circling, jiggling, teleports, walls), spawn grace, repeated exposure policy, texts, local notices | **19/19 pass** (.NET) |

## Manual test: camping (Phase 8 acceptance)
Two clients (A and B) in one match on the local dev server.
1. A stands still. At 20 s: crimson `WARNING: KEEP MOVING` headline and `WARNING: KEEP MOVING · EXPOSED IN 10 s` status. At 25 s: `NYX IS WATCHING YOU`. Rotating, crouching, firing and swapping weapons don't stop the countdown.
2. At 30 s: A sees `YOU ARE BEING HUNTED` and loses 2 HP/s (armor makes no difference). B sees a giant crimson pillar over A from anywhere on the map, a pulsing crimson dot on the minimap (clamped to the edge if far away), an arrow toward A with A's name and distance, and the feed line `HUNT ALERT: A EXPOSED FOR CAMPING`.
3. After 10 s the beacon, marker and arrow disappear for B, but A keeps losing health. 20 s after the first exposure A is exposed again (one new feed line).
4. A walks about 10 m in one direction: `HUNT STATUS CLEARED`, the damage stops, and the timer starts again from 0. Walking 5 m and stopping, or running in tight circles, does **not** clear it.
5. A camps outside the circle: zone and camping damage add up in the same ticks (e.g. 1 + 2 HP/s in phase 1), never doubled. A dies: `A was taken by the Nyx Hunt` (or `consumed by the Nyx Circle` if the zone dealt more), and the beacon disappears at once.
6. B camps at the same time as A: two independent beacons and markers.

## Phase 9 test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/MatchEndTests.cs` | Placements count down and the last survivor wins; eliminations placed only once their tick is complete; same-tick tie-break (kills, damage, id); everyone falls together → exactly one winner; leavers; nothing changes after the end; solo test room; FIRST BLOOD / DOUBLE / TRIPLE; result texts; history totals with online/offline separation; lifecycle end → results → lobby with players kept | **11/11 pass** (.NET and Unity) |
| `Tests/EditMode/MatchStartTests.cs` (changed) | Flow: never backwards during a match; a finished match starts a new round through Results → ReturnToLobby → Matchmaking → WaitingLobby | pass |

## Manual test: a full match (Phase 9 acceptance)
Local dev server with the fast circle, 2–3 clients.
1. Start the match. Top centre shows `ALIVE 3 / 3`.
2. A eliminates B: feed `A eliminated B with …`, then `FIRST BLOOD: A` and a gold headline. B sees the elimination panel: who, `1 ELIMINATIONS · SURVIVED 0:42 · #3 of 3`, and the buttons **SPECTATE** and **RETURN TO LOBBY**. `ALIVE 2 / 3`.
3. B presses SPECTATE: the camera follows A (`SPECTATING A`, NEXT [N] / STOP). B sees no extra information. STOP returns to B's own view.
4. A eliminates C within 10 s of B: `DOUBLE ELIMINATION: A`. Now one player is alive: the match ends at once, movement and shooting stop, and the circle freezes.
5. A sees `WAR OF NYX / LAST SURVIVOR / VICTORY ACHIEVED` with name, kills, damage, survival time and `#1 of 3`. Everyone else sees `MATCH OVER / WINNER: A / YOU PLACED #k` and the same standings table. A celebration effect plays at A.
6. The server log prints `VICTORY: A …` and `Results saved: …/match-results/<time>-WON-DEV.json`. Open it: the standings match the screen.
7. After 15 s everyone is back in the lobby, still connected. The next match starts automatically once enough players are there. No capsules, beacons or circle remain.
8. The Online screen in the main menu shows `YOUR ONLINE RECORD: 1 MATCHES · …`.
9. Tie-break: let the last two players sit outside the closed circle with equal health. They die in the same tick, and the player with more kills (then damage, then lower id) wins with `(TIE-BREAK)`.

## Phase 9G test inventory
| File | Covers | Status |
|---|---|---|
| `Tests/EditMode/BotTests.cs` | No vision through walls; FOV, range, crouch/still; proximity sense; hearing radius and approximate source; memory decay and last known position; reaction clock restarts; calm bots explore; top layer (eliminated, spawning, escape circle); engage; heal/retreat; investigate gunfire; proactive zone moves; camping avoidance; looting only when safe; final circle; **all 19 states reachable**; hysteresis; personalities; human reaction floor and imperfect aim; limited turning; loot choice (medkits when hurt, rifle over a starved shotgun, useless items ignored, strategies); bot names; shared loot claims (take-all never swaps, single take moves once) | **26/26 pass** (.NET and Unity) |
| `Tests/PlayMode/GhostProtocolPlayModeTests.cs` | Full offline matches in the real scene: bots only → one winner, distinct placements, bot-vs-bot eliminations, weapons used; idle human + 11 bots → human placed, match ends | **2/2 pass** (Unity) |

## Manual test: offline Ghost Protocol (Phase 9G, no internet)
1. Turn off Wi-Fi / unplug the network. Start the game: the menu says INTERNET: NOT AVAILABLE, and OFFLINE BATTLE ROYALE is available.
2. OFFLINE BATTLE ROYALE → 11 bots, ELITE → START MATCH. Countdown, then `PLAYERS ALIVE: 12/12`.
3. Bots are not visible through walls and have no name tags; you find them by exploring, listening for gunfire, and watching the feed (`GHOST_07 eliminated SHADOW_12 with Shadow SMG`).
4. Fire a shot and hide: a nearby bot comes to investigate the approximate spot, not your exact position.
5. Stand still: after 30 s you are exposed (beacon), and bots camping get beacons on your minimap with a feed alert.
6. Leave the circle: guns are disabled for you and for bots (they switch to melee or run back in).
7. Eliminate a bot and open its capsule (E / USE). Bots loot capsules too.
8. Die: SPECTATE follows bots (NEXT [N]) until one wins. Results: placement, eliminations, damage, survival, bots eliminated, accuracy, difficulty; "OFFLINE (not ranked)". The Online record on the menu is unchanged.
9. II / Esc pauses the offline match (time stops). LEAVE MATCH returns to the menu.

## Phase 10 checks
- `ShowcaseCapture` (menu **War of Nyx → Build → Capture Showcase Screenshots**, or batch with GPU): renders 8 views to `Builds/Screenshots` (island overview, the seven locations, character roster). Review them after visual changes.
- `BatchTools.DiagnoseIslandSpawns`: logs why spawn candidates fail on NYX ISLAND.
- `GhostProtocolPlayModeTests.BotsFinishAMatchOnNyxIsland`: full bots-only match on the island.
- Manual: Settings → CHARACTER cycles KAI / AKIRA / YUNA / REN; Offline → MAP: NYX ISLAND / TRAINING ARENA; Graphics LOW turns off outlines and bloom; a win plays the orbit camera, spin and victory sting before the results.
