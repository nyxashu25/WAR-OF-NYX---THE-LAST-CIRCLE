# Roadmap

Complexity: S (days) · M (1–2 weeks) · L (2–4 weeks) · XL (1–2+ months), for one experienced indie developer. These are rough estimates.

| # | Phase | Depends on | Size | Status |
|---|---|---|---|---|
| 1 | Architecture & project initialization | — | S | Code written; core tests pass under .NET; **not yet verified in Unity** |
| 2 | Character, camera, PC + touch controls, Practice Arena graybox | 1 | M | **Code written; movement model 30/30 tests pass under .NET; Unity side not yet verified** |
| 3 | Real multiplayer connectivity (Fusion 2, dedicated server route, 2 clients) | 2, Fusion App ID | L | **Code written; core networking 35/35 tests pass under .NET; needs Fusion SDK + App ID + Unity to run.** Gate: nothing advanced until the two-client test passes |
| 4 | Matchmaking, waiting lobby, synchronized start, random spawning | 3 | M | **Code written; match lifecycle, spawn planner, protection and queue: 40 tests pass under .NET; Unity/Fusion side not yet run** |
| 5 | Shooting & melee (server-validated, lag-compensated) | 4 | L | **Code written; combat rules 47 tests pass under .NET; Unity/Fusion side not yet run** |
| 6 | Loadouts, inventory, medkits, loot capsules | 5 | M | **Code written; item rules 29 tests pass under .NET; Unity/Fusion side not yet run** |
| 7 | Shrinking Nyx Circle, firearm lock, instability, zone damage | 5 | M | **Code written; circle rules 29 tests pass under .NET; Unity/Fusion side not yet run** |
| 8 | Anti-camping (Nyx Hunt Protocol) | 7 | M | **Code written; hunt rules 19 tests pass under .NET; Unity/Fusion side not yet run** |
| 9 | Complete match lifecycle: feed, spectate, victory, cleanup | 6, 7, 8 | M | **Code written; match-end rules 11 tests pass; non-Fusion code compiles in Unity 6.6, 299/299 EditMode tests pass in Unity; Fusion side not yet run** |
| **9G** | **Offline Ghost Protocol: local simulation + AI bots** (spec 38.18) | 5–9 | XL | **Code written and RUN in Unity: 2 full automated offline matches finish with one legitimate winner; bot rules 26 tests pass. Physical Android tests pending** |
| 10 | Anime visual upgrade (cel shading, characters, VFX, audio) | 9, 9G | XL | **Code-built visual identity done and checked in Unity (rendered screenshots, island match). Final art assets pending** |
| 11 | Android controls finalization, quality presets, physical devices | 9 | M | **Code done (controls, layout editor, gyro, build methods); development APK built with Unity 6000.3.26f1 (offline-only); device tests pending** |
| 12 | Public infrastructure: Linux server, Docker, Edgegap, health checks | 9 | L | **Files and server code done (health endpoint verified in Unity); deployment pending Fusion + Docker + Edgegap account** |
| 13 | Optimization & testing (2/4/12/24 players, latency/loss, bots on devices) | 11, 12, 9G | L | **Tools, benchmarks and fuzz tests done (PERFORMANCE.md); device + network measurements pending** |
| 10E | Environment pass: weather, time of day, vegetation, shadows, ambient life | 10 | L | **Done and checked in the built Windows game (screenshots of all six atmospheres); phone frame rate not measured** |
| 10F | Combat feel, character animation, map detail (accuracy, walk/run/sprint, jointed procedural animation, melee combos, katana, facades and props) | 10E | L | **Done and checked in the built Windows game (character and location screenshots); phone frame rate not measured** |
| 14 | Release preparation (AAB, target API 36, store listing, data safety) | 13 | M | **Prepared: validator, icon/splash, privacy + store drafts, release checklist. APK builds with target API 36. Blocked for submission: Fusion, device tests, signed AAB (owner's upload key), final art** |
| post | Accounts and stats backend, leaderboards, extra modes, cosmetics | 14 | — | Not started |

## Why 9G sits there
Spec 38.18 says to implement the offline mode after shared movement, combat, inventory and match rules work. Bots must use the exact same rules, so building them earlier would mean writing the rules twice.

## Milestones
- **M1 – Foundation (Phase 1):** project opens with no compile errors, menu runs, tests pass.
- **M2 – Two clients see each other (Phase 3).**
- **M3 – Networked kill (Phase 5).**
- **M4 – Full online match loop on PC (Phase 9):** the 32-step two-player scenario from spec §36, run with PC clients.
- **M5 – Offline match without internet (9G):** complete match against 11 bots on a phone in airplane mode. A bot can win, and the human can win.
- **M6 – Two Android phones, internet server (Phases 11–12).**
- **M7 – Internal testing track (Phase 14).**

## Phase 1 checklist
- [x] Version compatibility report ([DEPENDENCIES.md](DEPENDENCIES.md))
- [x] Folder structure and assembly definitions
- [x] Package manifest (URP, Input System, AI Navigation, Test Framework, uGUI)
- [x] Foundational code: bootstrap, services, config, flow state machine, event bus, settings, graphics presets, connectivity, lifecycle, input actions
- [x] Main menu with Online / Offline (Ghost Protocol setup) / Practice / Settings
- [x] Editor setup tool (player settings, URP presets, scenes, input backend)
- [x] EditMode and PlayMode tests written
- [x] **Open in Unity 6.3, fix any compile errors, run setup, run tests** (6000.3.26f1, 2026-10-13: no errors, 339 / 16 tests pass)
- [ ] Commit the generated `.meta` files, ProjectSettings and scenes

## Phase 2 checklist
- [x] Shared movement state machine in Core (`MovementModel`: Standing / Crouching / Sliding / Airborne), deterministic, server-ready
- [x] Movement values from spec §8 in `MovementSettings` / `MovementConfig` asset (walk 4, sprint 6.5, crouch 2.5, jump 1.5 m, slide 0.7 s)
- [x] Unity physics adapter `PlayerMovement` (CharacterController, slopes up to 45°, 0.35 m steps, headroom check)
- [x] `PlayerInputRouter`: keyboard/mouse, gamepad and touch → one `MovementCommand`
- [x] Third-person over-the-shoulder camera with aim zoom and wall collision
- [x] Placeholder character with procedural squash/lean/bob (`PlayerAnimationController`; Animator parameters ready for real rigs)
- [x] Touch controls: floating joystick, swipe look, sprint toggle, crouch, slide, jump, aim (combat buttons placed, dimmed)
- [x] Practice Arena graybox generated by `War of Nyx > Setup` (ramps, stairs, jump boxes, crouch tunnel, slide bar, cover, sprint lane)
- [x] Main menu → Practice Arena → pause → back to menu
- [x] Tests: 30 movement model tests (passing under .NET) + 6 PlayMode physics/arena tests (not yet run)
- [ ] **Run in Unity, fix compile errors, play-test feel on PC and a phone** ← next action
- [ ] Deferred to Phase 11: drag-to-customize touch layout, button scaling, gyro aim

## Phase 3 checklist
- [x] Fusion version check: **2.1.3 Stable** supports Unity 6.3 (confirmed on Photon's SDK page)
- [x] `WarOfNyx.Networking` module, compiled only with Fusion (`WON_FUSION` auto-detected), forced weaving + weaver registration
- [x] Session service: dedicated server / private host room / client join, with errors surfaced to the menu
- [x] Networked player: server-authoritative movement, owner prediction and re-simulation, proxy interpolation, sanitized names
- [x] Input over the network: intent only, sanitized on the server (tested)
- [x] Dedicated-server entry with launch arguments (tested parser), exit codes, Windows/Linux server build menus, local launch
- [x] BattleArena test scene, ring spawns, online HUD (room, players, ping), leave-match flow
- [x] Main menu ONLINE screen: name, room code, HOST PRIVATE ROOM, JOIN ROOM, JOIN DEV SERVER
- [ ] **Import Fusion 2.1.3 + App ID, fix compile issues, run the two-client manual test (TESTING.md)** ← next action, needs the project owner
- [ ] Automated multi-peer test (moved to Phase 4–5)

## Phase 4 checklist
- [x] Quick Match: random join of open, same-version public sessions; retry back-off; offers PLAY OFFLINE WITH BOTS after 30 s but never switches on its own
- [x] Waiting lobby scene: room, players n/max, players needed, lobby countdown, loading progress, connection and ping, host START, LEAVE
- [x] Server match lifecycle (`MatchLifecycle`): min-player threshold (dev two-player mode via `-minPlayers 2`), lobby fill timer, full-lobby instant start, load timeout with straggler removal, reset to the lobby when empty
- [x] Late joins refused (session closed + connection requests refused once loading starts)
- [x] Synchronized start: server start time; input unlocks on the same tick for server and every predicting client; "3‑2‑1‑FIGHT!" from the same clock
- [x] Server-side random spawns (`SpawnPlanner` + `ArenaSpawnArea`): ground, slope, water, overlap, roof, navmesh and reachability checks; 65 m separation relaxed safely (50 → 40 → 25 → 3 m); never invalid
- [x] Spawn effects (portal, beam, sparks, shockwave, sound) and drop-in landing
- [x] Spawn protection (3 s after FIGHT, ended by any aggressive action), shield bubble, HUD timer
- [x] Health initialized to 100, armor 0 (damage in Phase 5)
- [ ] Starting inventory/loadout: moved to Phase 6, where the inventory system is built (spec Phase 6 task 1)
- [ ] **Run the multi-client test in TESTING.md** ← needs the project owner (Unity + Fusion + App ID)
- [ ] Automated multi-peer PlayMode test (Fusion multi-peer mode): moved to Phase 5

## Phase 5 checklist
- [x] Weapon definitions: 7 ranged (spec table + Storm Light SMG from the loadout table), 4 melee (Katana, Combat Knife, Energy Blade, Dagger); engine-independent stats + editable `WeaponDefinition` assets + `WeaponDatabase`
- [x] Firing: semi / auto / burst, server fire-rate enforcement, magazine + reserve, reload (manual and auto), weapon switching (1/2/3, next) with a 0.3 s swap
- [x] Hit detection: lag-compensated hitscan (Fusion hitboxes rewound to the shooter's view), from the camera shoulder point (anti-peek fallback to the eye), deterministic per-pellet spread, bloom recoil, damage falloff, headshot multipliers, max range
- [x] Melee: windup → lag-compensated reach + arc + line-of-sight check → cooldown; works with any active weapon via the MELEE button
- [x] Damage: single server entry point (`ReceiveDamage`), spawn protection blocks it, armor rules ready for Phase 6; health replicated
- [x] Elimination: attribution (killer, weapon, headshot), kills and damage stats, hitboxes and collision off, defeat fall, ELIMINATED screen, kill feed with event-id de-duplication
- [x] Feedback: muzzle flash, tracers, impacts, slash arc, recoil kick, reload motion, synthesized sounds, hit markers (server-confirmed), damage flash and direction, dynamic crosshair
- [x] Touch: FIRE, RELOAD, MELEE, WEAPON buttons live
- [ ] **Run the two-player elimination test (TESTING.md)** ← needs the project owner (Unity + Fusion + App ID)
- [ ] Combat in the offline Practice Arena: arrives with the local authoritative simulation (9G)
- [ ] Automated multi-peer test: deferred to Phase 13 (needs Fusion multi-peer configuration; see KNOWN_ISSUES)

## Phase 6 checklist
- [x] Item model: rarity (Common–Legendary), ammo by type with caps, medkits S/M/L (25/50/75 HP, 2/4/6 s), armor L1–3; player inventory as a networked value type
- [x] Randomized starting loadouts: the six spec packages, medkit mix chosen so every loadout lands inside a ±10 % power band (verified over 1,000 seeds); primary + melee + spare ammo + 1–2 medkits + Level 1 armor
- [x] Spare ammo moved from weapons into the inventory by ammo type (reload draws from it)
- [x] Medkits: channelled, interrupted by sprinting or attacking, consumed only on completion, never above max health; MEDKIT picks the best-fitting kit
- [x] Elimination capsules: everything the player carried (weapons keep magazines), rarity colour, 90 s lifetime, despawn when empty
- [x] Looting: TAKE / SWAP per item (with weapon comparison), TAKE ALL (never swaps weapons away), armor only if better, partial stacks; server re-checks distance and availability; atomic one-at-a-time claims (no duplication, verified)
- [x] Weapon / ammo pickups end spawn protection
- [x] UI: loot panel, inventory line, healing progress, prompts and result messages; USE / MEDKIT touch buttons live
- [ ] **Run the loot test in TESTING.md** ← needs the project owner
- [ ] World loot (crates around the map) is not in the MVP list. Only elimination capsules exist.

## Phase 7 checklist
- [x] Circle geometry (contains / distance to edge / circle-in-circle) and the configurable phase table (spec 10: 500→…→0 m, 60 s each, 1/2/4/6 HP/s), scaled to each map's starting safe zone (the 120 m test arena uses ×0.11)
- [x] Server plans every centre at the countdown: each next circle lies entirely inside the previous one (verified over 500 seeds), candidates scored for playable ground (no water, cliffs or unreachable areas)
- [x] Synchronization: the server replicates only the current phase (from, to, start, end, damage); every peer derives the same smooth shrink. Future circles are never sent
- [x] Inside/outside decided by the server from the authoritative body position each tick (the owner predicts the same check); the input stream has no inside flag
- [x] Firearms can't fire outside: no shot, no ammo, no damage, no auto-reload; crossing the edge while holding the trigger stops at the edge (tested tick by tick)
- [x] Melee, medkits, reloading, switching, looting and movement keep working outside
- [x] Instability: deterministic 5–8 % sideways wobble (tick + player id, authority-applied, client can't remove it), small aim sway; no forced movement, teleports or camera turns
- [x] Zone damage in 0.5 s pulses, ignores armor and spawn protection, stops on re-entry, can't be dodged by stepping in and out; elimination by the circle has its own feed line
- [x] Re-entry restores guns at once and shows "SAFE ZONE ENTERED"
- [x] Visuals: energy barrier (scrolling hexagons, pulsing glowing edge, particles, lightning), next-circle ring, trembling characters with violet arcs, screen-edge distortion, mild camera vibration; "Reduce flashing / shake" soften visuals only
- [x] HUD: minimap (map, player, current + next circle, arrow to safety), phase status and timer, RETURN TO SAFE ZONE warning with distance, announcements (shrinking, final in 10 s, final circle), disabled FIRE button
- [x] Test helper: `-zoneSeconds 20` and a "fast circle" local server menu item
- [ ] **Run the circle test in TESTING.md** ← needs the project owner

## Phase 8 checklist
- [x] Authoritative position sampling once per second (server), timestamped steps in a 10 s recovery window
- [x] Meaningful movement = ≥ 8 m net displacement AND ≥ 12 m path in the window; micro-steps (< 0.5 m) don't add path; implausible jumps (> 14 m/s) count for nothing
- [x] Inactivity timer only resets on meaningful movement; rotating, crouching, firing, swapping, walking into walls, jiggling and circling in a small area never reset it (tested)
- [x] State machine NORMAL → CAMPING_WARNING (20 s "WARNING: KEEP MOVING", 25 s "NYX IS WATCHING YOU") → EXPOSED (30 s) → CAMPING_PENALTY → RECOVERING ("HUNT STATUS CLEARED") → NORMAL
- [x] Exposure: 10 s giant crimson/purple beacon (all clients), minimap marker for everyone, direction indicators with distance, feed line "HUNT ALERT: <name> EXPOSED FOR CAMPING" (one per exposure, event-id deduplicated)
- [x] Penalty 2 HP/s from 30 s until meaningful movement, ignores armor; repeated exposure every 20 s while still camping
- [x] Zone + camping damage in ONE pass per server tick (`ApplyEnvironmentDamage`), elimination cause = larger contributor; "taken by the Nyx Hunt" feed line
- [x] Spawn grace (5 s); trackers survive a disconnect so a reconnect never resets the timer (only a 5 s penalty pause); death removes the beacon; multiple campers tracked independently
- [ ] **Run the camping test in TESTING.md** ← needs the project owner (Fusion + 2 clients)
- [ ] Reconnecting into a running match is not possible yet (late joins are refused); the tracker side is ready (KNOWN_ISSUES 54)

## Phase 9 checklist
- [x] Alive tracking on the server (`MatchResolver`), ALIVE x / y counter on every client
- [x] Kill feed with causes (weapon, melee, circle, camping) plus FIRST BLOOD / DOUBLE / TRIPLE ELIMINATION highlights
- [x] Match statistics: kills, damage, survival time, placement (server-trusted)
- [x] Spectating living players after elimination (NEXT / STOP, auto-switch when the watched player falls); spectators see only what that player's camera shows
- [x] Final survivor detection; same-tick eliminations ranked by kills → damage → player id; if everyone falls in one tick the best-ranked is the single winner (tie-break)
- [x] Leavers count as eliminated; the circle freezes and combat stops when the match ends
- [x] Victory screen ("WAR OF NYX / LAST SURVIVOR / VICTORY ACHIEVED" + name, kills, damage, survival, placement), results table for everyone, PLACEHOLDER victory effect
- [x] Results saved where supported: trusted JSON on the server (`persistentDataPath/match-results`), local history per device (online/offline kept separate), record shown on the Online screen
- [x] Automatic return to the lobby after 15 s; connected players stay for the next match; flow state machine supports new rounds
- [x] Server cleanup on reset: avatars, loot capsules, hunt trackers, results, circle
- [ ] **Run the full-match test in TESTING.md** ← needs Fusion + App ID + 2 clients

## Phase 9G checklist (spec 38.18)
- [x] 1 Local authoritative match simulation (`OfflineMatch`): fixed 60 Hz ticks, no internet, server or Photon; same Core rules as online
- [x] 2 Offline match selection: START MATCH on the Offline screen (5/11/15/23 bots, ROOKIE–NIGHTMARE; default NIGHTMARE / 23, lowered on weaker devices)
- [x] 3 Bot character controllers: bots and the human are the same `OfflineCombatant` fed by the same commands
- [x] 4 Navigation: shared baked NavMesh, cached paths, stuck detection (hop + new goal)
- [x] 5 Perception: vision (range, FOV, crouch/still/firing, physics line of sight), hearing with approximate sources, decaying memory
- [x] 6 Utility decisions: 19-state brain with hysteresis and difficulty-based mistakes (every state reachable, tested)
- [x] 7 Personalities: Hunter, Phantom, Strategist, Berserker, Survivor, Scavenger (random per bot per match)
- [x] 8 Combat intelligence: reaction delay, distance-based aim error, limited turn speed, burst discipline, strafing, flanking, preferred ranges, weapon choice, melee, tactical reloads
- [x] 9 Looting: context-aware item choice through the same claim rules as humans
- [x] 10 Healing decisions (threshold × personality caution)
- [x] 11 Safe-zone awareness: proactive moves before the shrink (lead time by difficulty), escape when outside, firearm lock and instability apply to bots
- [x] 12 Anti-camping: same Nyx Hunt Protocol; bots react to warnings
- [x] 13 Bot-vs-bot combat (verified: a bots-only match ends with 10 combat eliminations)
- [x] 14 Offline victory conditions (same `MatchResolver`), results with bots eliminated / accuracy / difficulty, local offline history kept separate
- [x] 15 Difficulty settings (catalog profiles)
- [~] 16 Optimize for Android: staggered perception/decisions, distance-based decision rate, no per-frame allocations in hot paths; **not yet measured on a device**
- [x] 17 Test without internet: the offline path never touches the network (the automated matches run in batch mode with no connection)
- [ ] 18 Test on physical devices ← needs an Android phone (Phase 12/13)

## Phase 10 checklist
- [x] 1 Environment: NYX ISLAND (1.2 km, coast, hills, plateaus) with the seven locations (Neon District, Crimson Temple, Shadow Forest, Iron Fortress, Phantom Docks, Eclipse Valley, NYX CORE), routes and cover, NavMesh. Environment stages 1–2 (graybox); stage 3+ art pending
- [x] 2 Character presentation: KAI, AKIRA, YUNA, REN (original, cosmetic only, same hitboxes); choose in Settings; replicated online, random for bots
- [x] 3 Cel-shaded materials: `WarOfNyx/NyxToon` (two-band light, shade colour, rim, emission, inverted-hull outline; outline off on Low)
- [~] 4 Animations: procedural (idle bob, lean, squash, aim raise, reload dip, hurt flinch, healing pose + ring, defeat fall, victory spin/hop); rigged Animator clips still need models (the controller already drives Animator parameters when one exists)
- [x] 5 Weapon models: a distinct cel-shaded silhouette per catalog weapon, rarity-coloured accents
- [x] 6 Effects: elimination burst, healing ring, existing muzzle/tracers/impacts/slash, circle and hunt effects
- [x] 7 Sounds: added hurt, heal, victory sting, elimination (procedural placeholders)
- [x] 8 Lighting: dusk gradient sky, trilight ambient, warm key light, distance fog, bloom + grading + vignette (Medium/High)
- [x] 9 Victory presentation: camera orbit around the winner + celebration, then results (online and offline)
- [x] 10 Mobile: post-processing and outlines off on Low; static batching on the island; shared toon material with property blocks
- [x] Showcase screenshots rendered headless (`ShowcaseCapture`) and reviewed: fixed a yellow sky band and mirror-like terrain found in them

## Phase 11 checklist
- [x] 1 Mobile HUD: all actions on touch; FIRE shows disabled outside the circle; drag-to-aim on FIRE (one finger fires and steers)
- [x] 2 Landscape orientation (left + right only)
- [x] 3 Adjustable touch controls: in-match editor (drag buttons, SIZE, OPACITY, RESET, DONE), saved per device, clamped on screen (`TouchLayout`, tested)
- [x] 4 Resolution handling: canvases scale by height (any width), safe area on every HUD canvas
- [x] 5 Quality settings: LOW / MEDIUM / HIGH (frame-rate cap, render scale, shadows, outlines, post-processing)
- [x] 6 Android build targets: IL2CPP, ARM64, min API 26, target API auto (≥ 36 required for release; checked in Phase 14)
- [x] 7 APK build path: `BatchTools.BuildAndroidApk` / signed `BuildAndroidAab` (key from environment only)
- [ ] 8 Test on physical Android devices ← needs the owner's phones + Unity Android Build Support
- [ ] 9 Test the mobile-to-server connection ← needs Fusion App ID (+ Phase 12 server)
- [ ] 10 Record and fix device-specific problems ← after 8/9
- [x] Gyro aim (optional setting): mapping unit-tested; real axes need a device check

## Phase 12 checklist
- [~] 1 Linux dedicated-server build: menu + build method exist (Phase 3); needs Fusion SDK and the Linux Dedicated Server module
- [x] 2 Launch configuration: arguments + WON_* environment variables (tested)
- [x] 3 Containerize: `Server/Docker/Dockerfile`, `.dockerignore`
- [ ] 4 Test local Docker deployment ← Docker is not installed on this PC; needs the Linux server build
- [x] 5 Hosting provider: Edgegap app-version template + instructions (account needed)
- [x] 6 Matchmaking with allocation: design documented (warm pool by a trusted backend; servers self-announce as Photon sessions)
- [x] 7 Health checks: `GET /health` (200 / 503 / 404), Docker HEALTHCHECK (endpoint verified in Unity PlayMode)
- [x] 8 Logs and telemetry: structured JSON lines (`[WarOfNyx][Telemetry]`)
- [x] 9 Match shutdown and resource cleanup: `-maxMatches`, `-idleShutdown`, in-match cleanup (Phase 9)
- [x] 10 Deployment documentation: SERVER_DEPLOYMENT.md stages 2–3

## Phase 13 checklist
- [x] 1 Profile CPU: offline AI + rules benchmark (5/11/23 bots) in PERFORMANCE.md; FPS / frame-time overlay + CSV recorder for devices
- [ ] 2 Profile GPU ← device (Profiler / GPU inspector)
- [~] 3 Profile memory: overlay + CSV columns; device numbers pending
- [ ] 4 Network bandwidth ← Fusion SDK statistics
- [~] 5 2/4/12/24 players: offline 5/11/23 bots measured; online counts need Fusion + clients
- [ ] 6–7 Latency / packet loss ← Fusion network simulation (procedure in PERFORMANCE.md)
- [x] 8 Reconnects: behaviour documented (refused by design, state kept server-side)
- [x] 9 Collision and combat exploits: abuse matrix + fuzz tests (50 000 hostile inputs, no failure)
- [ ] 10 Android lifecycle ← device checklist in PERFORMANCE.md
- [ ] 11 Crashes and desync ← found none in the automated runs; real sessions pending
- [x] 12 Document performance measurements: PERFORMANCE.md (only measured values)

## Phase 14 checklist
- [x] 1 Release configuration: version 0.1.0 / code 1, release validator (`ReleaseTools.ValidateRelease` → `Builds/release-check.txt`)
- [x] 2 App icon and splash (generated placeholder emblem; feature graphic 1024×500)
- [x] 3 Privacy documentation: PRIVACY.md draft (owner completes contact / retention / engine settings and hosts it)
- [x] 4 Permissions: INTERNET (+ network state) only; validator checks for sensitive permissions
- [x] 5 Signing: environment-only upload key, passwords cleared after the build, validator checks
- [~] 6 Build an Android App Bundle ← APK builds; the signed AAB needs the owner's upload key (environment variables)
- [x] 7 Validate target SDK 36 (pinned; confirmed in the APK with `aapt`)
- [x] 8 Data-safety disclosures: draft in STORE_LISTING.md
- [~] 9 Store screenshots and descriptions: description drafted; rendered screenshots are drafts, real phone captures needed
- [~] 10 Final test checklist: RELEASE_CHECKLIST.md maps spec 36 / 38 / 25; the networked two-phone run is still open
