# Architecture — WAR OF NYX: LAST CIRCLE

Status: **design + Phase 1 foundation**. Sections marked *(planned)* do not exist in code yet.

## 1. Guiding principles

1. **One rule set, two authorities.** The game rules (health, damage, weapons, inventory, loot, circle, camping, elimination, victory) are written once and executed by:
   - the **dedicated Fusion server** in online matches, and
   - a **local authoritative simulation** in offline / practice matches (spec 38.14).
2. **Controllers issue commands, never results.** A human (touch / keyboard / gamepad) and an AI bot both produce the same *intent* — move, look, fire, melee, reload, use item, interact. Only the authority decides what happens.
3. **Clients are presentation.** Camera, UI, audio, VFX, animation, prediction and interpolation. A client never declares a hit, a kill, an item gain or a win.
4. **Playable multiplayer before polish.** Visual work starts only after the networked match loop works end to end.

## 2. Layers

```
┌───────────────────────────────────────────────────────────────────────────┐
│ PRESENTATION (client only)                                                │
│  UI/HUD · camera · animation · VFX · audio · minimap · kill feed render   │
├───────────────────────────────────────────────────────────────────────────┤
│ CONTROLLERS                                                               │
│  HumanInputController (Input System actions)  │  BotController (AI)       │
│            └──────── both emit PlayerCommand ─────────┘                   │
├───────────────────────────────────────────────────────────────────────────┤
│ SIMULATION AUTHORITY                                                      │
│  Online: Fusion NetworkRunner (GameMode.Server, dedicated Linux build)    │
│  Offline: LocalMatchSimulation (fixed tick on device, no network)         │
├───────────────────────────────────────────────────────────────────────────┤
│ GAME RULES  (WarOfNyx.Core — pure C#, no UnityEngine, unit-testable)      │
│  flow state machine · damage · weapons · inventory · loot transactions ·  │
│  circle phases · inside/outside · instability · camping · victory         │
└───────────────────────────────────────────────────────────────────────────┘
```

The rules layer holds no physics. Line of sight, hit scans, navmesh and collision are given to it as **query interfaces** (for example `IHitQuery`, `ISpawnValidator`) that each authority implements: Fusion lag-compensated raycasts online, plain `Physics` raycasts offline. *(Those interfaces arrive in Phases 4–5.)*

## 3. Assemblies

| Assembly | Folder | Depends on | Purpose |
|---|---|---|---|
| `WarOfNyx.Core` | `Assets/Scripts/Core` | — (`noEngineReferences: true`) | Pure rules and data: `GameStateMachine`, `GameEventBus`, offline settings, bot difficulty profiles. Can also be compiled by a plain .NET test project. |
| `WarOfNyx.Game` | `Assets/Scripts` (all folders without their own asmdef) | Core, Unity.InputSystem, UnityEngine.UI | Unity runtime: bootstrap, services, settings, input, UI, platform. |
| `WarOfNyx.Editor` | `Assets/Scripts/Editor` | Core, Game, URP | Setup and validation tools (`War of Nyx > Setup`). |
| `WarOfNyx.Tests.EditMode` | `Assets/Tests/EditMode` | Core, Game, Input System, NUnit | Fast logic tests. |
| `WarOfNyx.Tests.PlayMode` | `Assets/Tests/PlayMode` | Core, Game, NUnit | Scene-level smoke tests. |
| `WarOfNyx.Networking` (Phase 3) | `Assets/Scripts/Networking` | Core, Game, Fusion.Unity + Fusion DLLs | Fusion session manager, networked player, input/state structs. Compiles only with `WON_FUSION` (set automatically when the SDK is present). Always woven (`FUSION_FORCE_WEAVING`). |
| `WarOfNyx.Networking.Editor` | `Assets/Scripts/Networking/Editor` | Networking, Editor, Fusion | Network player prefab builder (`WON_FUSION` only). |
| `WarOfNyx.Tests.Multiplayer` | `Assets/Tests/Multiplayer` | Networking, Fusion, NUnit | Network struct tests (`WON_FUSION` only). |
| `WarOfNyx.AI` *(planned, Ghost Protocol)* | `Assets/Scripts/AI` | Core, Game | Bot brain, perception, navigation adapters. |

Namespace note: the `Android/` folder uses the namespace `WarOfNyx.Platform`, and input lives in `Controls/` (`WarOfNyx.Controls`). Namespaces called `Android`, `Input` or `Editor` would hide Unity types (`UnityEngine.Input`, `UnityEditor.Editor`) inside the `WarOfNyx.*` namespaces.

## 4. Runtime composition (Phase 1)

```
Bootstrap scene (build index 0)
  GameBootstrap ──► AppServices.Initialize(GameConfig)
                      ├─ GameEventBus            (local pub/sub)
                      ├─ GameStateMachine        (spec §5 flow)
                      ├─ UserSettingsService     (PlayerPrefs JSON)
                      ├─ GraphicsQualityService  (Low/Medium/High → Quality level + FPS target)
                      ├─ [WarOfNyx Services] GameObject (DontDestroyOnLoad)
                      │    ├─ AppLifecycleHandler (pause/focus/back → events)
                      │    └─ ConnectivityMonitor (reachability + HTTP probe → events)
                  ──► loads MainMenu scene
MainMenu scene
  MainMenuController ── builds the uGUI menu in code; reads AppServices; publishes nothing authoritative
```

`AppServices` is a deliberately small service locator for **app-lifetime** services only. Match systems (Phase 3+) receive their dependencies from a per-match context, so tests can build a match without global state.

## 4b. Character & movement (Phase 2)

```
PlayerInputRouter ──(MovementCommand: move, yaw/pitch, sprint, aim, jump/crouch/slide edges)──► PlayerController
   ▲  keyboard/mouse, gamepad, On-Screen joystick/buttons (virtual gamepad), TouchLookArea, sprint toggle
PlayerController ──► PlayerMovement.Simulate(cmd, dt)
                        ├─ GroundInfo (CharacterController.isGrounded, headroom OverlapCapsule)
                        ├─ MovementModel.Step  (Core, pure: Standing/Crouching/Sliding/Airborne)
                        ├─ CharacterController.Move  (collision, slopes ≤45°, steps ≤0.35 m)
                        └─ MovementModel.ApplyCollisionResult  (walls/ceilings feed back)
                     └─► Stepped event ──► PlayerAnimationController (Animator or placeholder procedural)
ThirdPersonCamera ◄── reads router yaw/pitch + movement stance (visual only)
```

- **Authority mapping for Phase 3:** `MovementCommand` becomes the Fusion input struct. `Simulate` runs in `FixedUpdateNetwork` on the server (authoritative) and on the owning client (prediction plus re-simulation). `MovementState` is a plain struct, so it can be stored in networked state and rolled back.
- **Bots** (Ghost Protocol) produce the same `MovementCommand`, so they move under identical rules.
- The capsule's feet sit at the transform origin. Height switches between 1.8 m (standing) and 1.15 m (crouch/slide). Standing up requires the headroom check to pass.

## 5. Match architecture *(planned)*

| Concern | Online (Phase 3–9) | Offline (Ghost Protocol) |
|---|---|---|
| Authority | Fusion `NetworkRunner` in `GameMode.Server` on a dedicated Linux build | `LocalMatchSimulation` MonoBehaviour driving a fixed 30 Hz tick |
| Player representation | `NetworkObject` + `NetworkBehaviour` components that call into Core rules | Plain components that call the **same** Core rules |
| Movement | Fusion predicted KCC (Fusion's Simple KCC addon or equivalent), server-validated | Same KCC logic, run locally |
| Hit detection | Fusion lag-compensated hitbox raycasts | `Physics.Raycast` against the same hitboxes |
| Bots | Not used. Online matches are never secretly filled with bots | `BotController` → `PlayerCommand` |
| Time source | `runner.Tick` / server timestamps | Local tick counter |

**Decision pending (Phase 3):** Fusion 2's `GameMode.Single` runs a runner locally. If testing confirms it works with **no internet and no Photon connection**, offline mode can reuse the network components directly. Otherwise the `LocalMatchSimulation` adapter is used. In both cases the rules stay in Core, so this choice does not change gameplay code.

## 6. Dedicated server approach *(planned)*

- Same Unity project, built with the **Dedicated Server** build target (Linux x86_64, headless, no rendering or audio).
- `GameBootstrap` detects server mode (`Application.isBatchMode` / `-server` argument) and starts the Fusion runner in `GameMode.Server` instead of loading the menu.
- One process per match. It exits after match cleanup so orchestration (Docker / Edgegap) can recycle it.
- Phase 12 containerizes it and adds Edgegap allocation. It is not built on the discontinued Unity Multiplay Hosting.

## 7. Android pipeline

Unity Android (IL2CPP, ARM64, min API 26, target API 36 for Play, landscape) → APK for local testing, signed AAB for Google Play. See [ANDROID_BUILD.md](ANDROID_BUILD.md).

## 8. Folder structure

The folders follow master spec §26 (`Art/`, `Audio/`, `Prefabs/`, `Scenes/`, `Scripts/<system>/`, `ScriptableObjects/`, `Settings/`, `Tests/`). Empty folders are kept with `.gitkeep`. Planned scenes: `Bootstrap`, `MainMenu` (Phase 1), `PracticeArena` (Phase 2), `MatchLobby`, `BattleArena`, `MatchResults` (Phases 3–9).
