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
- No fresh joins once a match is active. Reconnects within the **60 s [T]** grace window get their own authoritative state back.
- Lobby sizes: dev 2–4, public MVP 12, scalability target 24 (40–60 later). Higher counts must not be claimed until they are measured.

## 4. Spawning (spec 6)
- The server picks from navmesh-valid candidate points inside the starting circle. It rejects points in walls, water, closed interiors or overlapping capsules.
- Minimum separation **65 m [T]**, spread across regions. If placement fails, relax the separation in steps (65 → 50 → 40 m), never below a hard floor of **25 m**. Never place a player at an invalid spot.
- Spawn protection lasts **3 s [T]**. It ends early on attacking, picking up a combat item, or any other aggressive action.
- VFX: blue‑purple portal, energy burst, landing, ground shockwave, spawn sound.

## 5. Starting loadouts (spec 7)
Every player gets 1 primary + 1 melee + ammo + 1–2 medkits + Level 1 armor, chosen by the server from a **power budget**: each item has a power score and every loadout must land within ±10 % of the target **[T]**.

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
Walk 4 m/s · Sprint 6.5 m/s · Crouch 2.5 m/s · Jump 1.5 m · Slide 0.7 s · HP 100. State-driven controller with client prediction and server reconciliation.

## 7. Weapons (spec 9) [T]
| Weapon | Dmg | Mag | Role |
|---|---:|---:|---|
| NX‑7 Assault Rifle | 24 | 30 | General |
| Shadow SMG | 16 | 40 | Close |
| Crimson Shotgun | 12 × 8 | 6 | Burst |
| Phantom Marksman | 55 | 10 | Precision |
| Storm Burst Rifle | 21/shot | 24 | Burst fire |
| Neon Pistol | 26 | 12 | Backup |

- Hitscan with server-side lag-compensated validation · damage falloff · headshot ×1.5 **[T]**, body ×1.0 · fire-rate enforced on the server · magazine and reserve ammo tracked on the server.
- **Melee:** Katana, Combat Knife, Energy Blade (+ Dagger). Windup → server checks reach and hitbox → cooldown.

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

## 10. Health, medkits, armor (spec 15) [T]
- HP 100. Small medkit 25 HP / 2 s · Standard 50 / 4 s · Advanced 75 / 6 s. Healing is channeled; sprinting, attacking or firing interrupts it. Health never goes above max.
- Armor L1/L2/L3 reduces weapon damage by 15/25/35 % **[T]**. It does **not** reduce zone or camping damage.

## 11. Elimination & loot (spec 14)
- At 0 HP the server confirms the elimination. It records the killer and source, updates the alive count and kill stats, spawns a **loot capsule** with the victim's whole inventory (keeping ammo counts and weapon state), announces it, and moves the victim to spectate or results.
- Capsule color shows the best item: Common white, Uncommon green, Rare blue, Epic purple, Legendary gold.
- Claims are **atomic on the server**: the first valid claim wins and nothing is ever duplicated. Capsules disappear after **90 s [T]** or when empty.

## 12. Victory (spec 21)
- The last living competitor wins, decided by the server only.
- **Tie policy (deterministic):** if everyone left dies in the same tick, the winner is (1) whoever had the most health at the start of that tick, then (2) most kills, then (3) most damage dealt, then (4) the lowest server-assigned player id.
- Results screen: name, kills, damage, survival time, placement. Victory screen: "WAR OF NYX · LAST SURVIVOR · VICTORY ACHIEVED".

## 13. Ghost Protocol — offline bots (spec 38)
**Project configuration:** default difficulty **NIGHTMARE**, default **23 bots** (24 players). On devices whose capacity check allows fewer bots, the count drops to the largest supported option (5/11/15). The difficulty never changes. *(Implemented: `GameConfig`, `OfflineMatchSettings`, `OfflineCapacityPolicy`, setup screen.)*

- Bots use the **same** rules as humans: real inventories, ammo, reloads, circle lock, zone damage, instability and camping penalties. They have no wallhacks, no exact knowledge of player positions, and no invulnerability.
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
- **Desktop dev:** WASD, mouse, LMB fire, RMB aim, Shift sprint, Space jump, C crouch, Ctrl slide, R reload, E interact, F melee, 1/2/3 slots, H medkit. *(Implemented: `GameInputActions`.)*

## 15. HUD & feed (spec 19)
Palette: background `#0B0B18`, purple `#8A2BE2`, cyan `#00E5FF`, crimson `#FF304F` (danger/exposure), gold `#FFC857`. Feed events carry an **event id and server timestamp** so duplicates are dropped. The minimap never shows enemies except during camping exposure.

## 16. Map — NYX ISLAND: THE FALLEN REALM (spec 16)
Seven POIs: Neon District, Crimson Temple, Shadow Forest, Iron Fortress, Phantom Docks, Eclipse Valley, NYX CORE (final-circle reactor). Built graybox first, then paths/spawns/cover, then art, lighting, VFX, optimization.
