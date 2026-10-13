# Dependencies & version compatibility report

Checked on **2026-10-13** from public sources. Re-verify exact patch numbers in Unity Hub and the Photon dashboard before installing.

## Engine

| Item | Choice | Why / notes |
|---|---|---|
| Unity | **Unity 6.3 LTS (`6000.3.x`, latest patch)** | Current LTS, supported through Dec 2027. Unity 6.0 LTS standard support ends **Oct 2026**, so a new project should not start on it. `ProjectSettings/ProjectVersion.txt` says `6000.3.0f1` as a hint. Opening with any newer 6000.3 patch is fine. |
| C# | C# 9 (Unity 6 default), .NET Standard 2.1 API profile | No language features beyond what Unity 6 supports (no `init`/records, which need extra shims). |
| Render pipeline | **URP** (`com.unity.render-pipelines.universal`) | Mobile-first. Low/Medium/High pipeline assets are created by the setup tool. |
| Scripting backend (Android) | **IL2CPP, ARM64** | Required for 64-bit Google Play builds. |

### Unity Hub modules to install
- Android Build Support → OpenJDK, Android SDK & NDK Tools
- Linux Dedicated Server Build Support (Phase 3+)
- Windows Build Support (IL2CPP) – optional PC test client
- Visual Studio / Rider – any C# IDE

## Unity packages (`Packages/manifest.json`)

| Package | Pinned | Purpose | Note |
|---|---|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 | Rendering | URP's version is tied to the editor (17.3.x ↔ 6000.3). If your editor ships a different 17.3 patch, Unity bumps it automatically. |
| `com.unity.inputsystem` | 1.14.2 | Unified input actions, on-screen controls | 1.15.x is also published. Upgrade in Package Manager if 6.3 recommends it. |
| `com.unity.ai.navigation` | 2.0.9 | NavMesh baking and agents for bots and spawn validation | 2.0.12 also published. Either works with Unity 6. |
| `com.unity.test-framework` | 1.5.1 | EditMode / PlayMode tests | |
| `com.unity.ugui` | 2.0.0 | Runtime UI (includes TextMesh Pro in Unity 6) | Phase 1 uses legacy `Text`, so TMP resources don't need importing yet. |
| `com.unity.ide.visualstudio` | 2.0.22 | IDE integration | Optional. |
| `com.unity.modules.*` | 1.0.0 | Built-in modules (physics, AI, animation, audio, UI, web requests, Android JNI…) | |

> These package versions were chosen from published registry data. They have **not** been resolved by an actual Unity 6.3 install yet. If the Package Manager reports a missing version, select the version it recommends for 6000.3 and update this table.

## Networking — Photon Fusion 2 (required for online play, **not installed yet**)

| Item | Status |
|---|---|
| SDK | **Fusion 2.1.3 Stable** (Sep 17, 2026), the latest stable line. A new project starts directly on 2.1 (the "upgrade to latest 2.0.x first" rule only applies to existing 2.0 projects). Fusion 2.1 uses Photon Realtime 5. |
| Distribution | `.unitypackage` from Photon's "SDK & Download" page (Fusion 2 docs). It is **not** on the Unity Package Manager. Import it into `Assets/Photon/`. |
| Account | **Required:** a free Photon account and a **Fusion 2 App ID** from the Photon Dashboard. Enter it in `PhotonAppSettings` after import. |
| Unity compatibility | **Confirmed:** Photon lists Unity 2021.3.45, 2022.3.45, **6.0.x and 6.3.x** as supported (SDK & Download page, updated 22/09/2026). |
| Project setting | Asset Serialization = **Force Text** (the setup tool sets it). |
| Reference sample | **BR200** (Fusion 2 battle royale sample). We study its patterns only. Its art and code are **not** copied or rebranded. |

### Manual install steps (Phase 3)
1. Create a Photon account → Dashboard → **Create a new app** → type **Fusion**, SDK **Fusion 2** → copy the App ID.
2. Download **Fusion SDK 2.1.3 Stable** (`.unitypackage`) from the Photon docs "SDK & Download" page.
3. Unity: **Assets → Import Package → Custom Package…** → import everything.
4. Paste the App ID into the Fusion Hub window (opens after import) or into `PhotonAppSettings`.
5. Unity recompiles. The editor detects Fusion and adds the `WON_FUSION` define automatically (Console: "Fusion detected..."), which switches on the `WarOfNyx.Networking` module.
6. Run **War of Nyx → Setup → Run Project Setup** again. It builds `Resources/WarOfNyx/NetworkPlayer.prefab` and registers the module with the Fusion weaver.
7. Optional, to match the spec (~30 Hz): open the `NetworkProjectConfig` asset (Fusion Hub → Project Config) and set the simulation tick rate to 30. The movement code uses `Runner.DeltaTime`, so it works at any tick rate.
8. Run **War of Nyx → Setup → Validate Project**. Every Fusion line should say OK.

### How the project behaves without Fusion
`WarOfNyx.Networking` compiles only when `WON_FUSION` is defined, and that define is only set when the editor finds `Fusion.Runtime`. Without the SDK the game still compiles and runs. The ONLINE button explains that the online module is missing.

## Later / optional

| Item | Phase | Notes |
|---|---|---|
| Docker | 12 | Containerizes the Linux dedicated server. |
| Edgegap account | 12 | Optional scalable hosting; has a Fusion integration. Account and billing are needed. |
| Node.js 20+ / TypeScript, PostgreSQL, Redis | 23 (post-core) | Optional backend for accounts and stats. Not needed for the MVP match loop. |
| Google Play Console account | 14 | One-time registration fee. Needed for internal testing tracks. |
| Android physical devices (≥2) | 11 | Required to meet the MVP definition of done. |
