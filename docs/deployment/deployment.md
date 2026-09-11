# Production Deployment

UFW WebUI supports one production application topology with two Docker ownership models. Choose the runbook that matches how Docker runs on the firewall host; do not combine the group/ownership instructions from both modes.

| Docker mode | Use this guide | Host-file group mapping |
| --- | --- | --- |
| Rootful Docker daemon | [Rootful Docker](rootful-docker.md) | explicit host group GIDs are added to the non-root containers |
| Rootless Docker daemon | [Rootless Docker](rootless-docker.md) | supplemental container GID `0` maps to the rootless Docker user's primary host group |

Both modes keep the same runtime separation:

- `Ufw.Systemd` runs directly on the Linux host as a privileged systemd service;
- nginx is the only public container and serves the immutable browser application over HTTPS;
- `Ufw.Web` is a private non-root container with no TCP listener;
- PostgreSQL is reachable only by `Ufw.Web` on an internal Compose network;
- ASP reaches the host daemon through `/var/lib/ufw-webui/ipc/ufw-systemd.sock`, bind-mounted read-only at `/run/ufw-manager` inside the container.

The host daemon directory is intentionally outside `/run`. This works for both Docker modes and avoids RootlessKit's private/copied-up `/run` behavior.

## Before deploying

A production host needs Linux with systemd and UFW, Docker Engine with Compose and Buildx, `openssl`, and browser-facing TLS material. Root access is required for daemon installation and host-owned security state; the application containers themselves remain unprivileged.

The host does not need a .NET SDK or NativeAOT toolchain. `deploy/systemd/publish.sh` performs the daemon NativeAOT build in Docker and exports a native Linux executable.

Before choosing a runbook, read the [security architecture](../architecture/security.md). In particular, do not collapse nginx and ASP into one writable runtime image and do not mount daemon security state, UFW state, or the Docker socket into application containers.

## Reference material

The mode-specific runbooks intentionally contain the steps that differ by ownership model. Shared reference material is kept separately:

- [Deployment configuration](configuration.md) documents daemon settings, ASP configuration, Compose environment values, credentials, and optional IPC TLS/mTLS.
- [Operations](operations.md) covers backup, update, rollback, daemon uninstall, and routine verification.
- [Architecture overview](../architecture/architecture-overview.md) explains why the deployment is split this way.

The committed production stack is `deploy/docker/compose.yml`. The top-level `docker-compose.yml` is development-only and starts PostgreSQL for local development; it is not a production deployment file.
