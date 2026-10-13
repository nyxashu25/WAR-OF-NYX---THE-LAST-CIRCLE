# WAR OF NYX: LAST CIRCLE — Privacy Policy (DRAFT)

_Draft prepared during development (2026-10-13). It describes what the current build does. The owner must review it, fill in the contact details, host it at a public URL, and update it whenever the app changes (for example when accounts or analytics are added)._

**Developer:** Nyx Games · **Contact:** `<owner: add a support e-mail>`

## Summary
- No account is needed. We do not ask for your real name, e-mail, phone number, contacts, photos, microphone, camera or location.
- There are **no ads** and **no analytics or tracking SDKs** in the game.
- Offline play (Ghost Protocol) works with no network connection and sends nothing.

## Data stored on your device
| Data | Why | Where |
|---|---|---|
| Settings (graphics, audio, sensitivity, touch layout, chosen character, the display name you type) | Remember your preferences | On the device (app preferences) |
| Match history (placement, kills, damage, survival time; online and offline kept separate) | Show your record in the menu | On the device |
| Performance log (only if you turn on Settings → RECORD PERFORMANCE) | Help diagnose performance | On the device (app folder) |

Uninstalling the app deletes this data.

## Data sent over the network (online play only)
| Recipient | Data | Why |
|---|---|---|
| Photon (Exit Games GmbH) game network | Your IP address and connection data, your display name, your gameplay input (movement, aiming, actions) | Connect you to matches and run the multiplayer game |
| The game server operated for WAR OF NYX | Display name and match events (eliminations, placement, damage) | Run the match and keep a match result record |
| `connectivitycheck` endpoint (`www.gstatic.com/generate_204`, Google) | A simple request revealing your IP address | Check whether the internet is reachable so the menu can enable or disable online play |

Display names are visible to the other players in your match. Choose a name that does not identify you.

Server match records contain display names and match statistics only; they are kept to run and balance the game and are deleted after `<owner: retention period, e.g. 90 days>`.

## Engine
The game is built with Unity. Depending on engine settings and license, the Unity engine may send anonymous technical information (for example crash or hardware statistics). `<owner: confirm the engine settings used for the release build and adjust this section.>`

## Children
The game is not directed at children under 13 `<owner: confirm the target age group and the store rating>`.

## Your choices
- Play offline to send no data at all.
- Change or remove your display name in the menu at any time.
- Contact us to ask about server match records linked to a display name.

## Changes
We will update this policy and the "last updated" date when the app's data use changes.
