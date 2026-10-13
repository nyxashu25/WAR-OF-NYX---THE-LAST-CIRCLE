# Dedicated server deployment

Status: **Stage 1 code written** (Phase 3: server entry, launch arguments, build and launch menus). Not yet built or run. Stages 2–3 are planned for Phase 12.

## Stage 1 – Local dedicated server (Phase 3, code written)
- **Unity Hub modules:** *Windows Dedicated Server Build Support* (local tests) and *Linux Dedicated Server Build Support* (hosting).
- **Build:** **War of Nyx → Multiplayer → Build Windows Dedicated Server**, which outputs `Builds/WindowsServer/WarOfNyxServer.exe`. Or **Build Linux Dedicated Server**, which outputs `Builds/LinuxServer/WarOfNyxServer.x86_64`. Both use the Dedicated Server subtarget, so they are headless.
- **Run locally:** **War of Nyx → Multiplayer → Launch Local Windows Server (WON-DEV)**. Or from a terminal:
  `WarOfNyxServer.exe -batchmode -nographics -server -session WON-DEV -maxPlayers 12 -minPlayers 2 -lobbyFill 10 -port 27015 -logFile server.log`
  (`-minPlayers 2` is the explicit two-player dev mode; public servers use the configured 12.)
- **Entry:** `GameBootstrap` parses the arguments with `ServerLaunchOptions`. With `-server` it starts `NetworkRunner` in `GameMode.Server` and loads BattleArena. It never loads the menu. Invalid arguments fall back to defaults and are logged.
- **Arguments:** `-server`, `-session <code>` (4–16 chars, default WON-DEV), `-maxPlayers <2-60>` (default 12), `-minPlayers <1-60>` (players needed to start; `2` = dev two-player test mode; default from `GameConfig.matchRules`), `-lobbyFill <0-600>` (seconds to wait for more players once the minimum is reached), `-port <udp>` (default 27015, 0 = any), `-region <code>` (parsed, applied in Phase 12).
- **Exit codes:** 0 normal shutdown, 1 online module missing (Fusion not installed in the build), 2 start failed, 3 abnormal shutdown.
- One process = one session. Players connect through Photon Cloud using the session name, so no port forwarding is needed for basic tests.

## Stage 2 – Docker (Phase 12)
- Base image `ubuntu:24.04` (glibc compatible with Unity Linux players). Copy the server build in and run as a non-root user.
- Expose the game UDP port. Health check: process alive plus a small HTTP status endpoint *(to be designed)*.
- Logs go to stdout as structured JSON lines.

## Stage 3 – Edgegap (Phase 12, optional)
- Push the image to Edgegap's registry and create an application version with port mapping.
- Matchmaking requests a deployment near the players and passes the IP/port (or Photon session) to clients.
- Requires an Edgegap account and billing, which the project owner must provide.

## Not used
- Unity Multiplay Hosting (discontinued).
- Android phones as hosts for public matches.

## Secrets
Server tokens and backend credentials come from environment variables at deploy time. They are never baked into the Android client or committed to git.
