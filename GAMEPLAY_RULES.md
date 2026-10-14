# Game Design Document & Authoritative Rules

Values marked **[T]** are tunable starting points stored in ScriptableObjects or config. They are not final balance. Anything not yet in code is a **design target**.

## 1. Identity
- **Title:** WAR OF NYX: LAST CIRCLE (short: WAR OF NYX) · taglines "MOVE. HUNT. SURVIVE." / "THE CIRCLE CLOSES. THE WAR BEGINS."
- **Genre:** online & offline third-person anime battle royale for Android (landscape).
- **Pillars:** fast and readable combat · the shrinking Nyx Circle forces fights · camping is punished publicly · every kill feeds you loot.

## 2. Modes (spec 38.1)
| Mode | Opponents | Authority | Network |
|---|---|---|---|
| Online Battle Royale | Real humans only | Dedicated server | Required |
| Offline Battle Royale (Ghost Protocol) | 1 human + N bots | Local simulation | **None** |
| Practice Arena | Optional bots, small map | Local simulation | None |

- Offline is always selectable. Online is disabled with an explanation when there is no internet.
- After **30 s [T]** in the online queue without enough players, offer **PLAY OFFLINE WITH BOTS**. Never silently swap humans for bots, and never label bots as humans.
- Offline stats are kept apart from online/ranked stats and are never uploaded as ranked results.

## 3. Match flow (spec 5)
`MainMenu → Matchmaking → WaitingLobby → MatchPreparation → SynchronizedSpawn → Countdown → ActiveMatch → FinalCircle → MatchEnded → ResultsScreen → ReturnToLobby`
- Offline/practice skip Matchmaking and WaitingLobby. Any state can abort to MainMenu. *(Implemented in `GameStateMachine`.)*
- Online start: server sends one **start timestamp**. Everyone spawns during preparation, sees **3‑2‑1‑FIGHT!**, and gets control at the same server tick. Slow loaders are not waited on forever: there is a load timeout **[T 20 s]**, and players who miss it are removed before spawn.
- Lobby start policy [T]: public servers start loading once `minPlayersToStart` players are in (12 for public play; `-minPlayers 2` for the explicit dev two-player mode), after a `lobbyFillSeconds` window (15 s) to let more join, or immediately when full. Private rooms start only when the host presses START (the host may start alone for testing). If too few players finish loading, the start is abandoned and the lobby reopens. *(Implemented: `MatchLifecycle`.)*
- No fresh joins once a match is active. Reconnects within the **60 s [T]** grace window get their own authoritative state back.
- Lobby sizes: dev 2–4, public MVP 12, scalability target 24 (40–60 later). Higher counts must not be claimed until they are measured.

## 4. Spawning (spec 6)
- The server picks from navmesh-valid candidate points inside the starting circle. It rejects points in walls, water, closed interiors or overlapping capsules.
- Minimum separation **65 m [T]**, spread across regions (randomized farthest-point picking). If placement fails, relax step by step (65 → 50 → 40 → 25 m). As a last resort on very small test maps, use a 3 m anti-overlap floor (logged as relaxed). Never place a player at an invalid spot. If even that fails, the server ends the start attempt with an error instead of spawning anyone. *(Implemented: `SpawnPlanner`, `ArenaSpawnArea`.)*
- Spawn protection lasts **3 s [T] counted from FIGHT!** (players are locked during the countdown). It ends early on attacking, picking up a combat item, or any other aggressive action. It is granted only at spawn and never refreshed. *(Implemented: `SpawnProtectionState`, `NetworkPlayer`; damage blocking arrives in Phase 5.)*
- VFX: blue‑purple portal, energy burst, landing, ground shockwave, spawn sound.

## 5. Starting loadouts (spec 7)
Every player gets 1 primary + 1 melee + 2 magazines of spare ammo (by type) + 1–2 medkits + Level 1 armor, chosen by the server from a **power budget**: each item has a power score (weapons 10–31, Small/Standard medkit 4/7, armor L1 6) and every loadout must land within **58 ±10 % [T]**. The package is random, and the medkit mix (Small/Standard) is picked so the total fits. *(Implemented: `LoadoutGenerator`; verified over 1,000 seeds.)*

| Package | Primary | Melee | Medkits |
|---|---|---|---|
| Shadow Runner | Shadow SMG | Katana | 2 |
| Iron Hunter | NX‑7 AR | Combat Knife | 1 |
| Neon Reaper | Crimson Shotgun | Energy Blade | 2 |
| Phantom Scout | Phantom Marksman | Katana | 1 |
| Crimson Fighter | Storm Burst Rifle | Dagger | 2 |
| Storm Raider | Light SMG | Energy Blade | 1 |

These are random equipment sets, not character classes.

## 6. Movement (spec 8) [T]
Walk ≈ 1.8 m/s (gentle stick or the walk key) · Run 4 m/s (full stick) · Sprint 6.5 m/s · Crouch 2.5 m/s · Jump 1.5 m · Slide 0.7 s · HP 100. State-driven controller with client prediction and server reconciliation.

## 7. Weapons (spec 9) [T]
*(Implemented: `WeaponCatalog` / `WeaponRules` / `NetworkCombat`. Values are tunable in the WeaponDefinition assets.)*

| Weapon | Mode | Dmg | Rate (/s) | Mag / Reserve | Reload | Full dmg → min (m) | Max range | Headshot | Spread hip / aim | Move |
|---|---|---:|---:|---|---:|---|---:|---:|---|---:|
| NX‑7 Assault Rifle | Auto | 24 | 9 | 30 / 90 | 2.1 s | 30 → 80 (×0.6) | 150 m | ×1.5 | 1.5° / 0.3° | ×0.95 |
| Shadow SMG | Auto | 16 | 13 | 40 / 120 | 1.8 s | 15 → 40 (×0.5) | 80 m | ×1.4 | 1.8° / 0.7° | ×1.0 |
| Crimson Shotgun | Semi | 12 × 8 | 1.1 | 6 / 24 | 3.0 s | 6 → 18 (×0.2) | 30 m | ×1.25 | 4.5° / 3.2° | ×0.92 |
| Phantom Marksman | Semi | 55 | 1.4 | 10 / 30 | 2.6 s | 80 → 200 (×0.8) | 300 m | ×2.0 | 2.5° / 0.05° | ×0.88 |
| Storm Burst Rifle | Burst ×3 | 21/shot | 3 bursts | 24 / 72 | 2.2 s | 35 → 90 (×0.6) | 160 m | ×1.5 | 1.3° / 0.25° | ×0.95 |
| Neon Pistol | Semi | 26 | 5 | 12 / 48 | 1.4 s | 20 → 50 (×0.55) | 90 m | ×1.6 | 1.2° / 0.35° | ×1.0 |
| Storm Light SMG (from the loadout table) | Auto | 14 | 15 | 45 / 135 | 1.6 s | 12 → 35 (×0.45) | 70 m | ×1.4 | 2° / 0.8° | ×1.03 |

**Accuracy pass (2026-10-14):** spread cones were 2–3× tighter, bloom grows slower (AR: +0.12° per shot up to +1.4°), and bullets are weighted toward the crosshair (the radius is uniform, not the area: half of all bullets land within half the cone). Tested: an AR hip spray puts over 45 % of rounds on a standing player at 20 m, an aimed spray over 45 % at 50 m; a long-range hip spray still misses most (`CombatRulesTests.Sprays_PutAGoodShareOfBulletsOnTarget`). Existing weapon assets are refreshed by setup (`WeaponCatalog.Version` 2).

| Melee | Dmg | Windup | Cooldown | Reach | Arc |
|---|---:|---:|---:|---:|---:|
| Katana | 45 | 0.22 s | 0.65 s | 2.4 m | 100° |
| Combat Knife | 35 | 0.12 s | 0.45 s | 1.7 m | 80° |
| Energy Blade | 50 | 0.28 s | 0.80 s | 2.6 m | 110° |
| Dagger | 30 | 0.10 s | 0.40 s | 1.5 m | 70° |

**Melee combos (2026-10-14):** pressing again within 0.6 s of a swing's recovery continues the weapon's chain; waiting longer (or finishing it) starts over. Each attack changes arc, reach, damage and timing (multipliers of the table above), decided by the same server rule online and offline (`MeleeRules.Combo`):

| Style | Chain |
|---|---|
| Blade (katana) | horizontal slash 110° → backhand slash 100° → overhead strike 55°, +0.3 m, ×1.4 → thrust 30°, +0.9 m, ×1.25 (finisher) |
| Knife (combat knife, dagger) | slash 90° → backhand 90° → quick stab 35°, +0.3 m, ×1.3 |
| Energy blade | slash 115° → rising slash 90° → spin 360°, −0.2 m, ×0.9 |

- **Firing:** hitscan from the camera shoulder point along the aim, server-side and lag-compensated. Deterministic per-pellet spread. Recoil = bloom (spread grows per shot, recovers over time; recovery is always slower than sustained fire). Blocked attempts (cooldown, reload, empty, outside the circle from Phase 7) never cost ammo. Empty magazine → automatic reload.
- **Slots:** [1] primary, [2] secondary, [3] melee. Switching takes 0.3 s, cancels a reload, and cancels a melee windup. The MELEE button always swings the melee slot.
- **Melee:** windup → lag-compensated overlap → nearest target within reach (+0.35 m body radius) and inside the arc, with no wall in between → cooldown. Melee is never restricted by the circle.
- **Damage:** one server entry point. Spawn protection blocks weapon and melee damage. Armor (Phase 6) reduces weapon and melee damage by 15/25/35 %. At 0 HP: eliminated (attribution, kill credit, feed line, body stops colliding).

## 8. The Nyx Circle (spec 10–12)
- Map 1.2 × 1.2 km. Start radius 500 m. Each phase lasts **60 s**, and the circle shrinks continuously toward the next target during it.

| Phase | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | Final |
|---|---|---|---|---|---|---|---|---|---|
| Radius | 500→400 | 400→310 | 310→230 | 230→160 | 160→105 | 105→65 | 65→35 | 35→12 | 12→0 |

- Each next circle lies entirely inside the current one and avoids unfair terrain (water, cliffs).
- **Outside the circle, judged on the server by body position:**
  - Firearms **cannot fire**: no shot, no ammo spent, no damage. The fire button shows as disabled, and "FIREARMS DISABLED OUTSIDE THE CIRCLE" appears.
  - Melee, medkits, inventory and movement still work.
  - Shots fired legally from inside may finish their flight. Nothing new can be fired once outside.
  - **Instability:** a deterministic 5–8 % sideways movement wobble plus aim sway, seeded from server tick and player id so the client can't remove it. No teleports and no forced camera turns. Visual shake and distortion can be reduced in settings; the penalty itself can't.
  - **Zone damage:** 1 HP/s (phases 1–3), 2 HP/s (4–6), 4 HP/s (7–8), 6 HP/s (final) **[T]**. It ignores armor.
- **Re-entry:** guns work again immediately, damage and instability stop, and "SAFE ZONE ENTERED" appears.
- *Implemented (Phase 7):* `Core/Zone` (`ZoneSettings` table in GameConfig, `ZonePlanner`, `ZoneTimeline`, `ZoneRules`, `ZoneDamage`, `ZoneInstability`, `ZoneAnnouncer`). Details:
  - The phase table is scaled to each map's starting safe zone (×0.11 on the 120 m test arena).
  - Phase 1 starts shrinking at FIGHT; the circle is shown from the countdown.
  - On the edge counts as inside. A closed circle (radius 0) contains nobody.
  - Zone damage is charged in 0.5 s pulses of exposure **[T]**. Partial exposure is kept, so stepping in and out doesn't dodge it.
  - Aim sway: ±1.5° yaw, ±1° pitch **[T]** (affects melee aim; guns can't fire outside anyway).
  - After the final phase the circle stays closed (6 HP/s) until the match ends.

## 9. Nyx Hunt Protocol — anti-camping (spec 13)
- The server samples each player's position once per second. Rotating, aiming, crouching, firing, swapping weapons and using inventory don't count as movement.
- **Meaningful movement [T]:** ≥ 8 m net displacement **and** ≥ 12 m of path within the recovery window. Circling inside a tiny area never resets the timer.
- Timeline: 20 s "WARNING: KEEP MOVING" → 25 s "NYX IS WATCHING YOU" → **30 s EXPOSED**.
- **Exposed:** for 10 s the player gets a crimson/purple world beacon, a minimap marker for everyone, a direction indicator and the feed line "HUNT ALERT: <name> EXPOSED FOR CAMPING".
- **Penalty:** 2 HP/s **[T]** from the 30 s mark until meaningful movement resumes. It ignores armor. If the player stays still, exposure repeats according to a configurable re-alert policy (default: a new 10 s exposure every 20 s), without spamming the feed.
- States: NORMAL → CAMPING_WARNING → EXPOSED → CAMPING_PENALTY → RECOVERING → NORMAL. Recovery shows "HUNT STATUS CLEARED".
- Grace period after spawn and after an approved reconnect. **Reconnecting never resets the timer.**
- Zone and camping damage stack additively in one damage pass per tick. Neither can be applied twice in the same tick.
- Death while camping counts as an environmental elimination with cause "Camping".
- *Implemented (Phase 8):* `Core/Hunt/NyxHunt.cs` (`HuntSettings` in GameConfig, `HuntTracker`, `NyxHuntProtocol`, `EnvironmentDamage`, `HuntStatusAnnouncer`). Details:
  - Sample steps shorter than 0.5 m add no path; steps faster than 14 m/s count for nothing **[T]**.
  - Spawn grace: 5 s **[T]**. Reconnect grace: the penalty pauses for 5 s **[T]**, but the timer is never reset.
  - RECOVERING lasts 2 s.
  - When zone and camping damage both apply, the elimination cause is the larger contributor.

## 10. Health, medkits, armor (spec 15) [T]
- HP 100. Small medkit 25 HP / 2 s · Standard 50 / 4 s · Advanced 75 / 6 s. Healing is channeled; sprinting, attacking or firing interrupts it. Health never goes above max.
- Armor L1/L2/L3 reduces weapon damage by 15/25/35 % **[T]**. It does **not** reduce zone or camping damage.
- Carry limits [T]: Light/Rifle ammo 240, Shells 48, Marksman 60; up to 6 medkits of each size. The MEDKIT button uses the smallest kit that fully heals, otherwise the largest available. *(Implemented: `HealingRules`, `PlayerInventory`.)*

## 11. Elimination & loot (spec 14)
- At 0 HP the server confirms the elimination. It records the killer and source, updates the alive count and kill stats, spawns a **loot capsule** with the victim's whole inventory (keeping ammo counts and weapon state), announces it, and moves the victim to spectate or results.
- Capsule color shows the best item: Common white, Uncommon green, Rare blue, Epic purple, Legendary gold.
- Claims are **atomic on the server**: the first valid claim wins and nothing is ever duplicated. Capsules disappear after **90 s [T]** or when empty.
- Taking: weapons fill an empty matching slot, otherwise SWAP replaces the active ranged weapon (or the melee weapon), and the old one goes back into the capsule. TAKE ALL only fills empty weapon slots. Ammo and medkits stack up to the carry limits, and the rest stays in the capsule. Armor is taken only if better (the old armor goes into the capsule). Reach 3 m (the server allows 3.75 m). Weapon and ammo pickups end spawn protection. *(Implemented: `LootContainer`, `InventoryRules`, `NetworkLootCrate`, `NetworkCombat.RPC_TakeLoot`.)*

## 12. Victory (spec 21)
- The last living competitor wins, decided by the server only.
- **Tie policy (deterministic):** if everyone left dies in the same tick, the winner is (1) whoever had the most health at the start of that tick, then (2) most kills, then (3) most damage dealt, then (4) the lowest server-assigned player id.
- Results screen: name, kills, damage, survival time, placement. Victory screen: "WAR OF NYX · LAST SURVIVOR · VICTORY ACHIEVED".

## 13. Ghost Protocol — offline bots (spec 38)
**Project configuration:** default difficulty **NIGHTMARE**, default **23 bots** (24 players). On devices whose capacity check allows fewer bots, the count drops to the largest supported option (5/11/15). The difficulty never changes. *(Implemented: `GameConfig`, `OfflineMatchSettings`, `OfflineCapacityPolicy`, setup screen.)*

- Bots use the **same** rules as humans: real inventories, ammo, reloads, circle lock, zone damage, instability and camping penalties. They have no wallhacks, no exact knowledge of player positions, and no invulnerability.
- *Implemented (Phase 9G):* `Core/Bots` (perception, memory, brain, aim, loot planner, personalities, names) and `Gameplay/Offline` (`OfflineMatch` authority, `OfflineCombatant`, `BotController`, `OfflineLootCrate`, `OfflineMatchHud`). Bot name tags are not shown, because they would draw through walls.
- **Brain:** a hierarchical FSM plus utility scoring over these states: Spawning, Exploring, Patrolling, SearchingForEnemies, InvestigatingSound, SeekingCover, Hiding, StalkingTarget, Ambushing, EngagingEnemy, Repositioning, Retreating, Healing, Looting, SeekingSafeZone, EscapingCircle, AvoidingCampingPenalty, FinalCircleCombat, Eliminated.
- **Perception:** vision limited by FOV, distance and line-of-sight raycasts. Hearing uses per-sound radii and gives an approximate source position only. Memory stores last known position, time, heading and a confidence value that decays.
- **Personalities**, assigned at random and fixed for the match: Hunter, Phantom, Strategist, Berserker, Survivor, Scavenger.
- **Bots fight bots** with no bias toward the human, so matches keep moving even when the human is far away.
- **Difficulty profiles** (`BotDifficultyCatalog`, provisional):

| | Rookie | Warrior | Elite | NIGHTMARE |
|---|---|---|---|---|
| Reaction (s) | 0.65–1.00 | 0.45–0.70 | 0.30–0.48 | 0.22–0.35 (floor 0.20) |
| Base aim error (°) | 6.0 | 4.0 | 2.6 | 1.8 (never < 1.0) |
| Cover / flank | 0.15 / 0.05 | 0.40 / 0.20 | 0.70 / 0.45 | 0.90 / 0.65 |
| Decision interval (s) | 1.00 | 0.75 | 0.50 | 0.35 |
| Vision (m, cap 150) | 70 | 90 | 110 | 120 |
| Heal below HP | 30 | 45 | 55 | 60 |
| Zone lead time (s) | 5 | 15 | 30 | 45 |
| Mistake chance | 0.25 | 0.15 | 0.08 | 0.04 |
| Loot choice | Random | Basic | Tactical | Optimized |

- **Performance:** decision ticks staggered across frames, distant bots run cheaper logic but still fully simulate (they keep moving, fighting, looting and taking damage), shared navmesh and path caching, pooled objects, no per-frame allocations. Target: 1 human + 11 bots at ~30 FPS on a mid-range phone. 15–23 bots must be validated on real devices.

## 14. Controls (spec 18)
- **Android:** left side joystick, sprint, crouch, slide. Right side swipe-look, fire, aim, jump, reload, melee, weapon switch, medkit, interact. Safe areas respected, layout and sizes customizable, sensitivity sliders, optional gyro. Aim assist, if added, stays modest and validated by the same server rules.
- **Desktop dev:** WASD, mouse, LMB fire, RMB aim, Shift sprint, X walk (hold), Left Alt free look (hold), Space jump, C crouch, Ctrl slide, R reload, E interact, F melee (press repeatedly for combos), 1/2/3 slots, H medkit. *(Implemented: `GameInputActions`.)*
- **Screen blood (presentation only):**
  - A critical hit on you splatters 1–3 blood marks near the screen edges, never over the crosshair, and they slowly run down. A hit is critical if it is a headshot, does 30+ damage, or leaves you at 35 % health or less.
  - Healing clears them: each mark fades once you are 15 % above the health you had when it landed, or back to 90 %.
  - A red vignette grows as health drops and pulses like a heartbeat under 25 %; Reduce flashing turns the pulse off.
  - Settings → BLOOD EFFECTS turns it all off. *(Implemented: `CriticalHit`, `BloodOverlay`.)*
- **Free look ("eye", PUBG-style):** hold Left Alt / the EYE touch button / gamepad Select and look around (up to 170° each side, so you can see behind you) while the character keeps aiming and moving the same way; shots still go where the character aims, so the crosshair hides while you look away. Release and the view swings back in about 0.2 s. The character's head turns toward where you look. Client-side camera only: it reveals nothing the player couldn't see by turning. *(Implemented: `PlayerInputRouter.FreeLook*`, `ThirdPersonCamera`, `TouchHoldButton`.)*

## 15. HUD & feed (spec 19)
Palette: background `#0B0B18`, purple `#8A2BE2`, cyan `#00E5FF`, crimson `#FF304F` (danger/exposure), gold `#FFC857`. Feed events carry an **event id and server timestamp** so duplicates are dropped. The minimap never shows enemies except during camping exposure.

## 16. Map — NYX ISLAND: THE FALLEN REALM (spec 16)
Seven POIs: Neon District, Crimson Temple, Shadow Forest, Iron Fortress, Phantom Docks, Eclipse Valley, NYX CORE (final-circle reactor). Built graybox first, then paths/spawns/cover, then art, lighting, VFX, optimization.
