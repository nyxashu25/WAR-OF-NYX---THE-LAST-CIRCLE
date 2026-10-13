# Dedicated server deployment

Status: **not implemented** (Phases 3 and 12). This file records the plan so later phases follow one route.

## Stage 1 – Local dedicated server (Phase 3)
- Build target: **Dedicated Server → Linux** (or Windows for local testing). Requires the *Linux Dedicated Server Build Support* module.
- Entry: `GameBootstrap` detects batch mode or a `-server` argument and starts Fusion `NetworkRunner` with `GameMode.Server` instead of loading the menu.
- Planned launch arguments: `-server -port <udp> -region <code> -maxPlayers 12 -matchId <id> -logFile -`.
- One process = one match. The process exits after results are sent.

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
