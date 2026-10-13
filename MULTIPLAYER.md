# Multiplayer model

Status: **design only**. No networking code exists yet. Phase 3 starts by installing Fusion 2 (see [DEPENDENCIES.md](DEPENDENCIES.md)).

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
