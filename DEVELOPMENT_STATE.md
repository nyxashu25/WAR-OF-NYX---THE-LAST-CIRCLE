# Development state (handoff record)

_Last updated: 2026-10-13 — end of Phase 1 authoring_

## Current phase
**Phase 1 – Architecture & project initialization.** Source and docs are written. **Not yet opened in Unity, compiled, or tested.**

## Configuration requested by the project owner
- Offline Ghost Protocol defaults: **Difficulty = NIGHTMARE, Bots = 23** (stored in `GameConfig`, clamped at runtime by `OfflineCapacityPolicy`; difficulty is never lowered).

## Implemented (code exists, unverified)
| Area | Files |
|---|---|
| Pure rules core | `Assets/Scripts/Core/**` – `GameStateMachine`, `GameFlowState`, `GameMode`, `GameEventBus`, `AppEvents`, `BotDifficulty`, `BotDifficultyProfile`/`Catalog`, `OfflineMatchSettings`, `OfflineCapacityPolicy` |
| App foundation | `App/GameBootstrap`, `App/AppServices`, `App/GameConfig`, `App/ConnectivityMonitor` |
| Settings | `Settings/UserSettings` (data + PlayerPrefs service), `Settings/GraphicsQualityService` |
| Platform | `Android/DeviceCapabilityService`, `Android/AppLifecycleHandler` (namespace `WarOfNyx.Platform`) |
| Input | `Controls/GameInputActions` (code-defined actions, desktop + gamepad/on-screen bindings) |
| UI | `UI/Common/UiFactory`, `NyxPalette`, `SafeAreaFitter`; `UI/MainMenu/MainMenuController`, `MenuBackgroundAnimator` |
| Editor | `Editor/ProjectSetupWizard` (War of Nyx > Setup: run setup / rebuild scenes / validate) |
| Tests | 4 EditMode files, 1 PlayMode smoke test |
| Project | `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `.gitignore`, `.gitattributes`, folder tree |

## Package versions (pinned, unresolved)
URP 17.3.0 · Input System 1.14.2 · AI Navigation 2.0.9 · Test Framework 1.5.1 · uGUI 2.0.0 · Unity 6000.3.x. Fusion 2.0.x **not installed**.

## Key decisions
1. Unity **6.3 LTS** instead of 6.0 (6.0 LTS standard support ends Oct 2026).
2. Rules in a **pure C# assembly** (`noEngineReferences`), shared by the online server and the offline local simulation (spec 38.14).
3. **Fusion Server mode** for public matches. Host mode only for private test rooms.
4. Input actions **built in code**. Touch controls emit gamepad controls via On-Screen components.
5. Phase 1 UI is **code-built** (no prefabs needed). Replaced in Phase 10.
6. Offline phase scheduled as **9G** (after the shared rules exist).
7. Namespaces avoid `Android` / `Input` / `Editor` to prevent hiding Unity types.

## Verified tests
None.

## Unverified
Everything in "Implemented".

## External dependencies still required
- Unity 6.3 LTS install (with Android + Linux Dedicated Server modules)
- Photon account + Fusion 2 App ID + SDK download (Phase 3)
- Two physical Android devices (Phase 11)
- Later: Edgegap account, Google Play Console account

## Next tasks
1. Open the project in Unity 6.3. Resolve packages, fix compile errors.
2. Run **War of Nyx → Setup → Run Phase 1 Project Setup**, then **Validate Project**.
3. Run EditMode + PlayMode tests. Record results in TESTING.md.
4. Commit the `.meta` files, ProjectSettings and scenes.
5. Start **Phase 2**: placeholder character (capsule rig), third-person camera, state-driven movement (walk/sprint/crouch/slide/jump), PC + on-screen touch controls, `PracticeArena` graybox scene. Movement logic is written so Phase 3 can run it inside Fusion's prediction loop.
