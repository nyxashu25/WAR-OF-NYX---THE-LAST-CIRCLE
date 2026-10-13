# Dedicated server deployment

Status: **Stage 1 code written** (Phase 3). **Stage 2 (Docker) and stage 3 (Edgegap) files written in Phase 12** together with the health endpoint, telemetry and process lifetime (verified in Unity: the health endpoint answers HTTP requests). Not yet deployed: needs the Fusion SDK + App ID for a Linux server build, Docker, and an Edgegap account.

## Stage 1 – Local dedicated server (Phase 3, code written)
- **Unity Hub modules:** *Windows Dedicated Server Build Support* (local tests) and *Linux Dedicated Server Build Support* (hosting).
- **Build:** **War of Nyx → Multiplayer → Build Windows Dedicated Server**, which outputs `Builds/WindowsServer/WarOfNyxServer.exe`. Or **Build Linux Dedicated Server**, which outputs `Builds/LinuxServer/WarOfNyxServer.x86_64`. Both use the Dedicated Server subtarget, so they are headless.
- **Run locally:** **War of Nyx → Multiplayer → Launch Local Windows Server (WON-DEV)**. Or from a terminal:
  `WarOfNyxServer.exe -batchmode -nographics -server -session WON-DEV -maxPlayers 12 -minPlayers 2 -lobbyFill 10 -port 27015 -logFile server.log`
  (`-minPlayers 2` is the explicit two-player dev mode; public servers use the configured 12.)
- **Entry:** `GameBootstrap` parses the arguments with `ServerLaunchOptions`. With `-server` it starts `NetworkRunner` in `GameMode.Server` and loads BattleArena. It never loads the menu. Invalid arguments fall back to defaults and are logged.
- **Arguments:** `-server`, `-session <code>` (4–16 chars, default WON-DEV), `-maxPlayers <2-60>` (default 12), `-minPlayers <1-60>` (players needed to start; `2` = dev two-player test mode; default from `GameConfig.matchRules`), `-lobbyFill <0-600>` (seconds to wait for more players once the minimum is reached), `-zoneSeconds <5-600>` (testing only: every Nyx Circle phase lasts this long instead of 60 s; **Launch Local Windows Server (WON-DEV, fast circle 20 s)** uses 20), `-port <udp>` (default 27015, 0 = any), `-region <code>` (parsed, applied in Phase 12).
- **Exit codes:** 0 normal shutdown, 1 online module missing (Fusion not installed in the build), 2 start failed, 3 abnormal shutdown.
- One process = one session. Players connect through Photon Cloud using the session name, so no port forwarding is needed for basic tests.

## Stage 2 – Docker (Phase 12, files written)
- **Image:** `Server/Docker/Dockerfile` (Ubuntu 24.04, unprivileged user `nyx`, the Linux server build copied in, logs to stdout, Docker `HEALTHCHECK` on `/health`). `.dockerignore` sends only `Builds/LinuxServer/` as build context, so sources and secrets never enter the image.
- **Build & run** (from the repository root, after **War of Nyx → Multiplayer → Build Linux Dedicated Server**):
  ```bash
  docker build -f Server/Docker/Dockerfile -t warofnyx-server:dev .
  ```
  ```bash
  docker run --rm -p 27015:27015/udp -p 8080:8080 warofnyx-server:dev
  ```
  or the local test setup (two-player mode, fast circle, stays up between matches):
  ```bash
  docker compose -f Server/Docker/docker-compose.yml up --build
  ```
- **Environment variables** (arguments override them): `WON_SERVER=1`, `WON_SESSION`, `WON_MAX_PLAYERS`, `WON_MIN_PLAYERS`, `WON_LOBBY_FILL`, `WON_ZONE_SECONDS`, `WON_PORT`, `WON_REGION`, `WON_HEALTH_PORT`, `WON_MAX_MATCHES`, `WON_IDLE_SHUTDOWN`.
- **Health:** `GET /health` returns e.g. `{"status":"ok","session":"WON-DEV","phase":"Active","players":7,"maxPlayers":12,"matchesFinished":0,"uptimeSeconds":61,"protocol":10}` with **200** while serving and **503** while starting or shutting down. No player names, IPs or secrets.
- **Telemetry:** one JSON object per log line, prefixed `[WarOfNyx][Telemetry]`: `server_start`, `players`, `match_start`, `match_end`, `heartbeat` (every 60 s, with FPS), `server_exit`. Ship stdout to any log collector.
- **Lifetime / cleanup:** `-maxMatches 1` (image default) → the process exits with code 0 after its match's results (players are back in the lobby); `-idleShutdown 600` → exits after 10 minutes without players. In-match cleanup (avatars, loot capsules, hunt trackers, circle) is described in MULTIPLAYER.md (Phase 9).

## Stage 3 – Edgegap (Phase 12, template written)
- `Server/Edgegap/app-version.template.json`: an application-version template (UDP game port 27015, HTTP health port 8080, the WON_* variables, one match per deployment). **Check field names against Edgegap's current documentation** before uploading. Tokens go into the Edgegap dashboard as secret variables, never into this repository or the Android app.
- **Allocation design (spec 20, Phase 12 task 6):** players never start servers. A small trusted backend (post-MVP accounts service) keeps a warm pool: when fewer than N servers are in `WaitingForPlayers`, it asks Edgegap to deploy one near the players (region from the clients' Photon ping). Each server announces itself as a Photon session (`pv` / `mode` properties, Phase 4), so Quick Match finds it with no extra client code. Containers exit after their match, so capacity follows demand. The Edgegap API key lives only in that backend.
- **Photon:** with Fusion, clients reach the server through Photon Cloud (relay or punch-through), so a hosted server needs outbound internet; the UDP port mapping enables direct connections when possible.

## Not used
- Unity Multiplay Hosting (discontinued).
- Android phones as hosts for public matches.

## Secrets
Server tokens and backend credentials come from environment variables at deploy time. They are never baked into the Android client or committed to git.
