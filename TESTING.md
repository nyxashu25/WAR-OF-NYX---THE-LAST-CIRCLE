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
| — | Unity 6.3 | EditMode (incl. `ProjectFoundationTests`) / PlayMode | **Not run** | Needs the Unity Editor |

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
"C:/Program Files/Unity/Hub/Editor/<6000.3.x>/Editor/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile -
```
```bash
"C:/Program Files/Unity/Hub/Editor/<6000.3.x>/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile -
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
