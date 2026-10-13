# Multiplayer model

Status: **Phase 3 networking code written** (Photon Fusion 2.1, see the section at the end). Not yet run: it needs the Fusion SDK, an App ID and a Unity install.

## Topology
- **Photon Fusion 2, Server mode.** A headless Linux dedicated server runs `NetworkRunner` with `GameMode.Server`. Android clients run `GameMode.Client`.
- **Private test rooms** may use `GameMode.Host` (a PC or phone hosts) for quick friend testing only. **Public matches always use a dedicated server.**
- Tick rate: **30 Hz [T]** simulation. Snapshot/send rates are tuned after profiling.
- Interest management (Fusion Area of Interest) limits what each client receives. Spectators get a filtered view.
- Reliable RPCs are used only for one-off events (eliminations, feed lines, loot claim results). Everything continuous goes through networked state.

## Authority matrix

S = authoritative server (or the local simulation in offline mode) · C = client · P = client predicts, server corrects · V = client visual only

| Action / state | Client | Server | Notes |
|---|:-:|:-:|---|
| Raw input capture (touch, keys, gyro) | C | | Sent as a compact input struct each tick |
| Camera, HUD, audio, VFX, animation | V | | Never affects outcomes |
| Character movement | P | S | Server simulates from inputs and enforces speed and teleport limits. Client reconciles |
| Instability wobble (outside circle) | P | S | Deterministic from tick and player id, so prediction matches the server |
| Spawn location | | S | Navmesh and separation validation |
| Starting loadout | | S | Power-budgeted roll |
| Fire permission (ammo, fire-rate, **inside circle**) | P (FX only) | S | Server checks body position against the circle at the shot tick |
| Hit detection | V (hit marker after confirm) | S | Lag-compensated hitbox raycast |
| Damage, armor, health, death | | S | Client never reports damage |
| Reload completion, ammo counts | P (display) | S | |
| Melee windup / reach / hit | P (anim) | S | |
| Medkit channel and heal | P (anim) | S | Interrupted on the server by sprint or attack |
| Inventory contents | | S | Replicated to the owner. Others see only what's visible |
| Loot capsule claim | request | S | Atomic: first valid claim wins |
| Circle phase, center, radius | V (interpolated) | S | Clients receive phase start tick and parameters, then compute visuals |
| Zone damage | | S | |
| Camping timer and state | V (warnings UI) | S | Uses server-sampled positions only |
| Camping beacon / minimap exposure | V (render) | S | Server decides who is exposed and for how long |
| Kill feed / announcements | V | S | Event id plus timestamp for de-duplication |
| Match state machine | V (mirror) | S | `GameStateMachine` is advanced only by the authority |
| Winner selection | | S | Deterministic tie policy (GAMEPLAY_RULES §12) |
| Persistent stats upload | | S → backend | Clients never submit results |

## Connection handling *(planned)*
- 60 s reconnect grace. The player's object stays on the server, and camping timers keep running.
- Duplicate sessions: a second connection with the same player id replaces the first.
- Matches can't be joined once active. Late arrivals are sent to another waiting session.

## Anti-cheat principles
The server validates every command. It enforces speed and teleport limits, fire rate, ammo and inventory, and runs damage and timers itself. Clients have no win authority. Suspicious events are logged in a structured format. Requests are rate-limited. Obfuscation is never the main defense.

## Phase 3 implementation (code written, not yet run)

| Piece | File | Runs on |
|---|---|---|
| Session service interface (game ↔ network) | `App/OnlineServices.cs` | all |
| Runner start/stop, roles, spawning, input polling | `Networking/NetworkSessionManager.cs` | all (spawning server-only) |
| Networked player: movement in `FixedUpdateNetwork`, name RPC | `Networking/NetworkPlayer.cs` | server + owner simulate; proxies render |
| Input struct and networked movement state | `Networking/NetworkInputData.cs` | all |
| Input packing and server-side sanitizing | `Core/Networking/InputCodec.cs` | server (decode), client (encode) |
| Dedicated-server launch arguments | `Core/Networking/ServerLaunchOptions.cs` + `App/GameBootstrap.cs` | server |
| Room codes, display-name sanitizing | `Core/Networking/SessionCodes.cs` | server (authoritative), client (pre-check) |
| ~~Temporary ring spawns~~ (replaced in Phase 4 by `SpawnPlanner`) | `Gameplay/ArenaSpawnArea.cs` | server |
| Local presentation (camera, touch UI, HUD, leave) | `Gameplay/OnlineArenaController.cs` | clients |

**Tick flow (owner client and server):** at the start of each tick, restore the networked `MovementState`, grounded flag and position. Then read this tick's input. If the input is missing, use a neutral command (stand still, gravity still applies). Run `PlayerMovement.Simulate(cmd, Runner.DeltaTime)` and write the networked state back. Fusion re-runs this for every corrected tick on the owner, so prediction and reconciliation fall out of the same code.

**What the client sends:** stick (Vector2), yaw, pitch, 5 button bits. Never position, velocity or state. The server clamps the stick to length 1, zeroes NaN/∞, wraps yaw, clamps pitch, and ignores unknown bits. So speed hacks through input are impossible, and a client cannot teleport. The name is requested by RPC and sanitized on the server.

**Roles in Phase 3:**
- *Dedicated server:* `WarOfNyxServer -batchmode -nographics -server -session WON-DEV -maxPlayers 12`. No local player.
- *Private test room:* a device hosts (`GameMode.Host`). Testing only, never used for public matches.
- *Client:* joins by room code or the dev server session.

**Not yet (Phase 4+):** matchmaking queue, waiting lobby and synchronized countdown, real spawn selection, late-join rejection, reconnect grace, lag compensation (Phase 5), interest management tuning (Phase 13). Region selection (`-region`) is parsed but not applied.

## Phase 4: matchmaking and synchronized start (code written, not yet run)

| Piece | File | Runs on |
|---|---|---|
| Start-up rules (min/max players, fill timer, load timeout, countdown, protection, spawn separation) | `Core/Match/MatchRules.cs`, `GameConfig.matchRules` | server |
| Match lifecycle state machine | `Core/Match/MatchLifecycle.cs` | server |
| Networked lifecycle view, load reports, shared start time | `Networking/NetworkMatchController.cs` | server writes, clients read |
| Spawn selection | `Core/Match/SpawnPlanner.cs` + `Gameplay/ArenaSpawnArea.cs` (validator) | server |
| Spawn protection | `Core/Match/SpawnProtection.cs`, `NetworkPlayer` | server; shield visual on clients |
| Quick Match queue | `Core/Match/MatchmakingQueue.cs`, main menu | client |
| Lobby UI | `UI/Lobby/LobbyUIController.cs` (MatchLobby scene) | client |

- **Matchmaking:** dedicated servers publish `pv` (protocol version) and `mode` session properties and are visible. Quick Match is a Fusion random join filtered on those properties. Private rooms are invisible and joined only by code.
- **No late joins:** when loading starts, the server closes the session (`SessionInfo.IsOpen = false`) and refuses connection requests. Joins that still slip through are disconnected.
- **Synchronized start:** the server sets the start time (simulation seconds) when the countdown begins. `NetworkPlayer` ignores movement input until `MovementAllowedAt(Runner.SimulationTime)`. The server and the owner's prediction evaluate this with the same tick time, so everyone gains control on the same tick, however long their device took to load.
- **Spawning:** after all players report loaded (or the 20 s timeout removes stragglers), the server plans spawns once and spawns everyone 1.2 m above validated ground, facing the zone centre, locked until FIGHT.
- **Bump `NetworkProtocol.Version`** whenever inputs or networked state change, so old clients can't match with new servers.

## Phase 5: combat (code written, not yet run)
| Piece | File | Runs on |
|---|---|---|
| Weapon stats and catalog (network ids = catalog index, append-only) | `Core/Combat/WeaponStats.cs`, `WeaponCatalog.cs`; tunable via `Combat/WeaponDefinition` assets | all (same numbers) |
| Fire rate, ammo, reload, spread, falloff, headshots | `Core/Combat/WeaponRules.cs` | server authoritative, owner predicts |
| Health, armor, spawn-protection gate | `Core/Combat/HealthRules.cs` | server |
| Melee timing and arc | `Core/Combat/MeleeRules.cs` | server authoritative, owner predicts timing |
| Networked weapons, hits, damage, eliminations | `Networking/NetworkCombat.cs` | server decides; owner FX; proxies FX from counters |
| Kill feed (event ids) | `NetworkMatchController.RegisterElimination` / `RPC_EliminationFeed` | server → all |

- **Input:** 8 more button bits (fire held/pressed, reload, melee, slot 1–3, next weapon). Still intent only.
- **Hits:** the server runs `Runner.LagCompensation.Raycast` from the shooter's shoulder point along the received aim, with deterministic spread. Hitboxes are rewound to what the shooter saw. Player capsules sit on *Ignore Raycast*, so only rewound hitboxes count. Melee uses a lag-compensated overlap, then reach, arc and a wall check.
- **Damage:** only `NetworkCombat.ReceiveDamage` (server) changes health. It checks spawn protection and armor. Eliminations record killer, weapon and headshot, increment kills, and broadcast one feed event.
- **Never trusted from clients:** hits, damage, ammo counts, reload completion, fire timing, speed multipliers, kills.
- `NetworkProtocol.Version` = **5**.

## Phase 6: inventory and loot (code written, not yet run)
- **Networked state:** `NetworkCombat.Inv` (ammo ×4, medkits ×3, armor) and heal channel (type, start, end). Capsules: `NetworkLootCrate` holds 12 fixed item slots in one struct, plus expiry, highest rarity and owner name.
- **Server-only:** loadout rolls (`LoadoutGenerator`, per-player seed), dropping loot on elimination (items are *moved*: slots and inventory are cleared in the same step), every claim (`RPC_TakeLoot` → re-check alive, capsule exists, ≤ 3.75 m, item still there → `LootContainer.TryTake` → `InventoryRules.ApplyPickup` → swapped/remaining items back into the same slot). Claims are processed one after another on the server, so the same item can never be granted twice.
- **Client-only:** proximity prompt, loot panel, comparisons. These are views of replicated state, and buttons only send requests.
- **Prediction:** reload/medkit timing is predicted by the owner like the rest of combat. Loot results arrive from the server (`RPC_LootResult`).
- `NetworkProtocol.Version` = **6** at the end of Phase 6.

## Phase 7: the Nyx Circle (code written, not yet run)
- **Server-only:** circle planning (`ZonePlanner`, seeded per match, fair-ground scoring through `ArenaSpawnArea`), the phase schedule (`ZoneTimeline`), zone damage (`ZoneDamage`, once per server tick through `NetworkCombat.ReceiveDamage`, kind `Zone`, no attacker).
- **Replicated (`NetworkNyxCircle`, on the MatchController):** active flag, phase index/count, the current phase's start and target circle, start/end time, damage per second, final-phase start. Only the current target is ever sent, so clients can't see the endgame circles early. Values change once per phase, so the bandwidth cost is negligible.
- **Derived on every peer:** the shrinking circle = interpolation of the replicated phase at the simulation time (the barrier uses `LocalRenderTime`, so it moves smoothly between ticks).
- **Inside/outside:** `NetworkPlayer` classifies its own simulated position each tick, before moving (wobble) and after moving (firearms, damage). The server's result is authoritative and replicated as `OutsideZone`; the owner predicts the same check, and Fusion corrects any mismatch. Clients never send zone state, and `InputCodec.Sanitize` wipes the authority-only `LateralWobble`.
- **Instability:** `ZoneInstability.Lateral(tick, dt, playerId)` is applied by the authority into `MovementCommand.LateralWobble`. Prediction computes the same value from the same tick, so it doesn't cause corrections.
- **Firearm lock:** `NetworkCombat.Simulate(..., firearmsAllowed)` passes it to `WeaponRules.TryFire`. Blocked attempts cost nothing. Shots fired from inside resolve in the tick they were fired (all guns are hitscan; nothing is in flight to cancel).
- **Feed:** `RegisterElimination` / `RPC_EliminationFeed` carry the cause (weapon, melee, zone). Circle announcements are derived locally from the replicated stage (`ZoneAnnouncer`), so they need no RPC and can't repeat.
- `NetworkProtocol.Version` = **7**.
