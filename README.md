# WAR OF NYX: LAST CIRCLE

**MOVE. HUNT. SURVIVE.** — *The circle closes. The war begins.*

An original anime-style third-person battle royale for **Android**, built with **Unity 6.3 LTS (C#, URP)** and **Photon Fusion 2**, with server-authoritative online multiplayer and an offline mode against AI bots (Ghost Protocol).

| | |
|---|---|
| Internal id | `WarOfNyx` |
| Android package | `com.nyxgames.warofnyx` |
| Current phase | **Phase 1 – Architecture & project initialization** (code written, not yet opened in Unity) |
| Offline default | Ghost Protocol, **NIGHTMARE**, **23 bots** (automatically lowered on devices that can't run 23) |

> **Honest status:** the pure rules core (`WarOfNyx.Core`) compiles and passes 39/39 tests under .NET (`dotnet test Tools/CoreTests`). The Unity-side code has **not** been compiled in Unity yet. Gameplay (movement, combat, circle, bots, multiplayer) is **not implemented**. See [DEVELOPMENT_STATE.md](DEVELOPMENT_STATE.md) and [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

---

## Quick start (Phase 1)

1. **Install Unity Hub**, then **Unity 6.3 LTS** (latest `6000.3.x` patch) with these modules:
   - Android Build Support (+ OpenJDK + Android SDK & NDK Tools)
   - Linux Dedicated Server Build Support (needed from Phase 3)
   - Windows Build Support (IL2CPP) – optional, for PC test clients
2. In Unity Hub choose **Add → Add project from disk** and select this folder. If Hub asks which editor version to use, pick your installed 6000.3.x.
3. On first open, Unity imports the packages in `Packages/manifest.json`. If it says it **updated package versions** to match your editor, accept that.
   - If Unity asks *"enable the new Input System backends?"*, answer **Yes**. Unity will restart.
4. Menu **War of Nyx → Setup → Run Phase 1 Project Setup**. This:
   - applies the Android player settings (package id, landscape, IL2CPP, ARM64, min API 26),
   - creates URP **Low / Medium / High** quality presets,
   - creates `Assets/Resources/WarOfNyx/GameConfig.asset`,
   - generates the `Bootstrap` and `MainMenu` scenes and adds them to Build Settings,
   - switches Active Input Handling to the Input System. If Unity asks to restart, click **Restart now**.
5. Menu **War of Nyx → Setup → Validate Project**. Every line should say OK. The Fusion line will say "not installed", which is expected until Phase 3.
6. Open `Assets/Scenes/Bootstrap.unity` and press **Play**. You should see the main menu:
   - **ONLINE BATTLE ROYALE** – enabled only when internet is detected. Pressing it says online play arrives in Phases 3–4.
   - **OFFLINE BATTLE ROYALE** – Ghost Protocol setup: bot count 5/11/15/23 and difficulty Rookie/Warrior/Elite/NIGHTMARE. Your choice is saved. Starting a match is **not available yet**.
   - **PRACTICE ARENA** – arrives in Phase 2.
   - **SETTINGS** – graphics preset, volumes, sensitivity and comfort options (saved on the device).
7. Run the tests: **Window → General → Test Runner**, then run **EditMode** and **PlayMode**.
8. **Commit the generated `.meta` files and settings.** Unity creates `.meta` files, `ProjectSettings/*` and the scenes on first open. Commit them so every machine shares the same asset GUIDs:
   ```bash
   git add -A && git commit -m "Add Unity-generated meta files, settings and Phase 1 scenes"
   ```

## Documentation

| Document | Contents |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layers, assemblies, project structure, offline/online authority design |
| [DEPENDENCIES.md](DEPENDENCIES.md) | Exact engine/package versions, install steps, compatibility notes |
| [GAMEPLAY_RULES.md](GAMEPLAY_RULES.md) | Game design document and authoritative gameplay values |
| [MULTIPLAYER.md](MULTIPLAYER.md) | Networking model, authority matrix, anti-cheat principles |
| [ROADMAP.md](ROADMAP.md) | Phases, milestones, dependencies, complexity |
| [RISKS.md](RISKS.md) | Hardest engineering problems and mitigations |
| [ANDROID_BUILD.md](ANDROID_BUILD.md) | Toolchain, APK/AAB builds, signing, device testing |
| [SERVER_DEPLOYMENT.md](SERVER_DEPLOYMENT.md) | Dedicated server, Docker, Edgegap plan |
| [TESTING.md](TESTING.md) | How to run tests; verified vs unverified results |
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Real limitations and unverified assumptions |
| [DEVELOPMENT_STATE.md](DEVELOPMENT_STATE.md) | Handoff record: current phase, files, decisions, next tasks |

## Repository layout

```
Assets/
  Scripts/
    Core/         WarOfNyx.Core   – pure C# rules (no UnityEngine): flow state machine, event bus, offline settings, bot difficulty
    App/          bootstrap, services, GameConfig, connectivity
    Settings/     user settings, graphics quality presets
    Controls/     input action abstraction (keyboard/mouse + gamepad/on-screen)
    Android/      device capability, lifecycle (namespace WarOfNyx.Platform)
    UI/           code-built Phase 1 menu, safe area, palette
    Editor/       War of Nyx > Setup tools
    Gameplay/ Characters/ Combat/ Inventory/ Loot/ SafeZone/ AntiCamping/ AI/
    Networking/ Matchmaking/ Audio/ Backend/          (empty – later phases)
  Tests/EditMode, Tests/PlayMode, Tests/Multiplayer
  Art/ Audio/ Prefabs/ Scenes/ ScriptableObjects/ Settings/
Tools/CoreTests/          .NET test runner for WarOfNyx.Core (no Unity needed)
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```
