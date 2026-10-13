# Testing

## Verified results
**None yet.** No test has been run, because no Unity Editor was available when Phase 1 was written. Record real results here, with date and Unity version, after the first run.

| Date | Unity | Suite | Result | Notes |
|---|---|---|---|---|
| — | — | — | — | Not run |

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
