# Release checklist (Phase 14)

Status legend: **✔ verified** = run and checked (with where) · **◐ implemented** = code exists and is covered by unit tests, not yet run as described · **✖ blocked** = needs something the owner must provide.

_Last updated 2026-10-13. Nothing here claims store approval._

## Spec 36 — Minimum Definition of Done (two real players on Android, networked)
The spec requires these 32 steps on the **actual networked project with two Android phones**. That run has not happened yet: it needs Photon Fusion + App ID, the Android module and two phones. Until then the online steps are "implemented", not "verified".

| # | Step | Status | Evidence so far |
|---|---|---|---|
| 1–2 | Both players open the game on Android | ◐ | Offline-only development APK built (no Fusion); not yet run on a phone |
| 3–4 | Both press PLAY and are allocated to the same match | ◐ | Quick Match + session properties (Phase 4); needs Fusion |
| 5 | Waiting lobby shows both | ◐ | Lobby UI (Phase 4) |
| 6 | Synchronized countdown | ◐ | `MatchLifecycle` tests; shared start tick (Phase 4) |
| 7 | Different valid spawn locations | ◐ / ✔ offline | Spawn planner tests; PlayMode spawn validation; 389/400 valid points on NYX ISLAND |
| 8 | Randomized starting equipment | ◐ / ✔ offline | 1 000-seed loadout test; used by bots in full offline matches |
| 9 | Both move and see each other | ◐ | Prediction/reconciliation code (Phase 3) |
| 10–11 | A shoots B inside the circle; damage and health sync | ◐ / ✔ offline | Combat tests; offline matches end with combat eliminations |
| 12 | Circle shrinks every minute | ◐ / ✔ offline | Zone tests; circle drives offline matches |
| 13–17 | B outside: no guns, melee works, instability, guns back inside | ◐ / ✔ offline | Zone tests (crossing, forging, melee); bots obey the lock offline |
| 18–24 | A camps 30 s → exposed, minimap marker, beacon, feed alert, damage, recovery | ◐ / ✔ offline | Hunt tests; idle human eliminated by the Nyx Hunt in an automated offline match |
| 25–27 | B eliminates A; capsule; B loots | ◐ / ✔ offline | Loot claim tests; bots loot offline |
| 28–30 | Server registers elimination, decides last survivor, victory screen | ◐ / ✔ offline | `MatchResolver` tests; offline matches end with one winner and results |
| 31 | Both return to the lobby | ◐ | Lifecycle test (results → lobby) |
| 32 | Server cleans up the finished match | ◐ | Reset cleanup + `-maxMatches` exit; health endpoint verified |

**To verify:** install Fusion 2.1.3 + App ID → Run Project Setup → build the Windows/Linux server and two Android APKs → follow TESTING.md Phases 3–9 manual tests with two phones → mark each row ✔.

## Spec 38 — Offline acceptance
| Requirement | Status |
|---|---|
| Phone offline, select Offline Battle Royale, 11 bots, complete match | ✔ in Unity (automated, desktop, 6.3 + 6.6) · ◐ APK built, not yet played on a phone |
| Bots move, hide, search, fight each other, loot, heal, escape the circle, produce a legitimate winner | ✔ automated full matches on the training arena and NYX ISLAND |
| Bots obey circle, firearm lock, camping rules | ✔ tests + offline matches |
| Offline results separate from online | ✔ test |

## Spec 25 — Android release requirements
| Item | Status |
|---|---|
| Native Android output, landscape, ARM64, IL2CPP | ✔ settings + development APK built (checked with `aapt`) |
| Target API 36 for 2026 submissions | ✔ pinned to 36; confirmed in the built APK (`aapt`). Confirm again in the release AAB |
| Signed AAB procedure, keystore guidance, never commit keys | ✔ `BuildAndroidAab` (environment-only key), ANDROID_BUILD.md, validator checks |
| APK for local testing | ✔ built (46 MB, debug-signed) · not yet installed on a phone |
| Linux dedicated-server build | ◐ menu + Dockerfile; needs Fusion + Linux module |
| Permissions explained | ✔ ANDROID_BUILD.md, PRIVACY.md; validator: no sensitive permissions |
| Quality presets LOW / MEDIUM / HIGH | ✔ |
| 30 FPS on mid-range devices | ✖ not measured on a device (PERFORMANCE.md §2) |

## Store preparation
| Item | Status |
|---|---|
| App icon, splash | ✔ generated (placeholder emblem) |
| Privacy policy | ◐ PRIVACY.md draft: owner fills contact, retention, engine settings, hosts it |
| Data safety, content rating | ◐ drafts in STORE_LISTING.md |
| Screenshots | ◐ rendered drafts; real phone captures needed |
| Store description | ◐ STORE_LISTING.md |
| Final art (characters, buildings, audio) | ✖ placeholders (KNOWN_ISSUES 66) |

## Automated checks to run before every release build
- `dotnet test Tools/CoreTests`
- Unity: `BatchTools.Setup`, EditMode + PlayMode tests (TESTING.md), `ReleaseTools.ValidateRelease` (writes `Builds/release-check.txt`)
