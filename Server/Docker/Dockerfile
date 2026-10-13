# WAR OF NYX - headless dedicated game server (Linux x86_64).
#
# 1. In Unity (with Photon Fusion installed): War of Nyx > Multiplayer > Build Linux Dedicated Server
#    -> Builds/LinuxServer/WarOfNyxServer.x86_64
# 2. From the repository root:
#      docker build -f Server/Docker/Dockerfile -t warofnyx-server:dev .
#      docker run --rm -p 27015:27015/udp -p 8080:8080 warofnyx-server:dev
#
# Configuration comes from WON_* environment variables (see SERVER_DEPLOYMENT.md). The Photon App ID
# is part of the Unity build's PhotonAppSettings; nothing secret is baked into this file.

FROM ubuntu:24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

# Run as an unprivileged user.
RUN useradd --create-home --shell /usr/sbin/nologin nyx
WORKDIR /opt/warofnyx
COPY --chown=nyx:nyx Builds/LinuxServer/ ./
RUN chmod +x ./WarOfNyxServer.x86_64
USER nyx

# Hosted default: one match per container, then exit (the orchestrator starts a fresh one).
ENV WON_SERVER=1 \
    WON_PORT=27015 \
    WON_HEALTH_PORT=8080 \
    WON_MAX_PLAYERS=12 \
    WON_MAX_MATCHES=1 \
    WON_IDLE_SHUTDOWN=600

EXPOSE 27015/udp
EXPOSE 8080/tcp

# 200 = serving, 503 = starting or shutting down.
HEALTHCHECK --interval=15s --timeout=3s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

# -logFile - : Unity logs to stdout, so `docker logs` shows the [Telemetry] JSON lines.
ENTRYPOINT ["./WarOfNyxServer.x86_64", "-batchmode", "-nographics", "-logFile", "-"]
