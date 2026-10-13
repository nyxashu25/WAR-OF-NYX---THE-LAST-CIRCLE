# Store listing draft (Google Play) — Phase 14

_Draft only. Nothing here has been submitted, and store approval is never assumed. Check Google Play's current requirements before submitting._

## App details
| Field | Draft |
|---|---|
| App name (≤ 30) | WAR OF NYX: LAST CIRCLE |
| Short description (≤ 80) | Anime battle royale: outrun the Nyx Circle, hunt campers, be the last one standing |
| Category | Action |
| Tags | Battle royale, Shooter, Anime, Multiplayer, Offline |
| Contact e-mail | `<owner>` |
| Privacy policy URL | `<owner: host PRIVACY.md publicly>` |

## Full description (draft)
MOVE. HUNT. SURVIVE. — THE CIRCLE CLOSES. THE WAR BEGINS.

Drop onto NYX ISLAND, a fallen anime realm of neon streets, crimson temples, shadow forests and a roaring reactor core. Every match is a fight for one title: LAST SURVIVOR.

• THE NYX CIRCLE — a violet energy barrier shrinks every minute. Step outside and your guns go silent: only blades still work, the ground trembles under you, and the circle drains your health.
• NO CAMPING — stand still for 30 seconds and the Nyx Hunt Protocol exposes you to everyone with a giant beacon. Keep moving or be hunted.
• FAIR START — everyone spawns apart at the same moment with a balanced random loadout: guns, a melee weapon, medkits and armor.
• LOOT THE FALLEN — every elimination drops a glowing capsule with everything that player carried.
• GHOST PROTOCOL OFFLINE — no internet? Fight up to 23 AI opponents that see, hear, hide, loot, heal and fight each other, from ROOKIE to NIGHTMARE.
• MADE FOR PHONES — adjustable touch controls, drag-to-aim on the fire button, optional gyro aim, and LOW / MEDIUM / HIGH graphics.

Online matches are real players only — never secretly filled with bots.

## Graphics assets
| Asset | Status |
|---|---|
| App icon 512×512 | Generated from `Assets/Art/Branding/AppIcon.png` (1024², placeholder emblem). Export at 512 for the store |
| Feature graphic 1024×500 | `Assets/Art/Branding/FeatureGraphic.png` (placeholder, no title text yet) |
| Phone screenshots (16:9, min 2) | Draft renders in `Builds/Screenshots` (War of Nyx → Build → Capture Showcase Screenshots); **replace with real in-game captures from a phone** before submission |
| Promo video | Not made |

## Content rating (IARC questionnaire — owner answers it in Play Console)
Expected answers for the current game: stylized, non-realistic weapon violence against human-like characters; no blood or gore (characters fall and dissolve into energy); no sexual content, gambling, drugs or user-generated content beyond display names; players can interact online (display names visible). Typical outcomes for such games are around PEGI 12 / Teen, but the questionnaire decides.

## Data safety form (draft answers, verify against the current form)
| Question | Draft answer |
|---|---|
| Does the app collect or share user data? | Online play transmits the display name, IP address and gameplay input to the game network to provide the multiplayer service. Review whether this counts as "collected" under the current definitions; the developer does not use it for ads or analytics. |
| Data types | Possibly: "Other user-generated content / in-game names" (display name); device or other IDs: none; location: none |
| Purpose | App functionality (multiplayer) |
| Encrypted in transit | Confirm with the Photon configuration used in the release build |
| Users can request deletion | Yes, by contacting the developer (server match records) |
| Ads | No |

## Release track
1. Internal testing track with the signed AAB (`BatchTools.BuildAndroidAab`) → testers install from Play.
2. Fix device issues (ANDROID_BUILD.md test plan, PERFORMANCE.md).
3. Closed testing → production only after the RELEASE_CHECKLIST.md items pass.
