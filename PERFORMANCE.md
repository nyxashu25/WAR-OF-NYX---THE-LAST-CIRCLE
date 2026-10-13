# Performance and stability (Phase 13)

All numbers here are **measured**, with date and hardware. Nothing is estimated as if it were measured.

## 1. Offline simulation cost (AI + rules), desktop
`Tests/PlayMode/OfflinePerformanceTests` loads NYX ISLAND, runs NIGHTMARE bots with no human and times every authoritative 60 Hz tick (bots perceive, decide, path-find, aim, shoot, take loot; circle, Nyx Hunt, damage, results). Rendering is excluded (batch mode).

| Date | Hardware | Unity | Bots | Avg tick | p95 | Max | 60 Hz budget |
|---|---|---|---|---|---|---|---|
| 2026-10-13 | AMD Ryzen 7 7445HS, 23.8 GB (laptop) | 6000.6.5f1 | 5 | 0.060 ms | 0.077 ms | 2.46 ms | 16.7 ms |
| 2026-10-13 | same | same | 11 | 0.138 ms | 1.044 ms | 3.19 ms | 16.7 ms |
| 2026-10-13 | same | same | 23 | 0.252 ms | 1.171 ms | 3.50 ms | 16.7 ms |

Reading: the AI and rules use well under 10 % of a frame even with 23 bots on this CPU. Spikes (max) come from bot perception rounds and NavMesh path requests, which are staggered across bots. A mid-range phone CPU is several times slower; the remaining budget there goes to rendering, which this test does not measure.

## 2. Still to measure on Android (needs devices, see ANDROID_BUILD.md)
| Target (spec 38.15 / 30) | How |
|---|---|
| 1 human + 11 bots ≈ 30 FPS on a mid-range phone | Settings → SHOW FPS + RECORD PERFORMANCE (CSV in `persistentDataPath/perf/`), 10-minute offline match on NYX ISLAND, tiers LOW and MEDIUM |
| 15–23 bots | same, record which tier holds 30 FPS |
| Memory | CSV `total_mb` column; must stay well below the device's RAM class |
| Thermal | note device temperature / throttling after 10 minutes |
| GPU | Unity Profiler (USB) with Development Build; or Android GPU Inspector |

## 3. Network (needs Fusion + App ID)
| Test | How |
|---|---|
| Bandwidth per client | Fusion's network statistics (FusionStats / Network Debugger in the SDK) during a 12-player match |
| 2 / 4 / 12 / 24 players | local dev server (`-minPlayers N`) + desktop clients + phones; record server tick time from the `[Telemetry] heartbeat` FPS and client RTT (HUD ping) |
| High latency / loss | Fusion's network condition simulation in NetworkProjectConfig (delay, jitter, loss), or Windows `clumsy`; check movement correction, hit registration and loot claims at 150 ms / 2 % loss and 300 ms / 5 % loss |

## 4. Exploit and abuse test matrix (spec 24)
| Attack | Defence | Automated test |
|---|---|---|
| Speed hack / huge stick input | Server sanitizes input (stick ≤ 1, angles clamped, NaN removed); speed comes from the shared model | `RobustnessTests.SanitizedInputIsAlwaysFiniteAndInRange`, `MovementModelStaysFiniteAndBoundedUnderRandomInput`, `CombatRulesTests` forged speed multiplier |
| Teleport / forged position | Clients send intents only; positions are simulated by the server | by construction (`NetworkInputData` has no position); Nyx Hunt ignores implausible jumps (`HuntTests.TeleportsCountForNothing`) |
| Shooting outside the circle | Server classifies its own simulated position | `ZoneTests` (crossing the edge, late reports, forged input) |
| Removing the outside-circle wobble | Authority-only field, wiped on arrival | `ZoneTests.ClientsCannotForgeTheirZoneState`, `RobustnessTests` |
| Fire-rate / ammo hacks | `WeaponRules` on the server: cooldown, magazine, reload, blocked attempts cost nothing | `CombatRulesTests` |
| Self-reported hits / kills | Lag-compensated hit detection on the server; single damage entry point | by construction; `RobustnessTests.DamageCanNeverHealOrOverflow` |
| Loot duplication / racing claims | Atomic `TryTake`, claims processed one by one, items moved not copied | `ItemsTests` racing claim, `LootClaimTests` |
| Camping-timer tricks (jiggle, circle, wall) | Net displacement AND path inside a window | `HuntTests` |
| Weapon slot forging | Input codec only allows slots 1–3 | `RobustnessTests.ButtonPackingRoundTripsAndIgnoresUnknownBits` |
| Fake results / ranked wins from offline | Offline history tagged "offline", never uploaded; online results are server JSON | `MatchEndTests.HistoryKeepsOnlineAndOfflineSeparateAndBounded` |
| Wall-hack style info for bots | Bot vision requires a physics line of sight | `BotTests.NeverSeesThroughWalls` |

## 5. Android lifecycle checklist (device)
- [ ] Home → back mid-match: offline pauses (time stops); online keeps running and the player continues.
- [ ] Incoming call / notification shade: no input stuck (sprint toggle, fire).
- [ ] Screen lock 30 s, unlock: online session still connected or a clear "connection lost" return to the menu.
- [ ] Low-memory warning: no crash.
- [ ] Rotation between the two landscape orientations: HUD and safe area correct, gyro direction correct.
- [ ] Airplane mode toggled while in the menu: ONLINE disabled with explanation, OFFLINE available.

## 6. Reconnects
Late joins are refused by design (spec 20 MVP), so a dropped player cannot rejoin a running match yet. Their camping timer and placement are kept server-side (the player counts as eliminated if they leave). Adding approved reconnects is a post-MVP task (KNOWN_ISSUES 54).
