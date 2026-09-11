# Production Docker stack

This directory contains the containerized portion of the supported production deployment. The privileged firewall daemon is **not** a container; `Ufw.Systemd` runs on the host and is reached through its restricted Unix-domain socket.

The Compose stack contains three services:

- `frontend`: non-root nginx, the only public service. It terminates browser TLS, serves the immutable Blazor WebAssembly build, and proxies `/api/*` to ASP over a private Unix socket.
- `asp`: non-root `Ufw.Web`, with no published TCP port. It owns web authentication/application state and reaches the host daemon through a read-only bind mount of the daemon socket directory.
- `postgres`: PostgreSQL on an internal network reachable only by ASP.

Frontend and ASP are built as independent images. This is a security boundary: compromising the server-side application must not make ASP's writable filesystem the source of browser-delivered mutation-signing code.

## Before using Compose

Choose the deployment guide that matches the host Docker mode:

- [Rootful Docker](../../docs/deployment/rootful-docker.md)
- [Rootless Docker](../../docs/deployment/rootless-docker.md)

The two modes use the same Compose file but **different host ownership/group mappings**. Do not mix their permission instructions.

After the host daemon, TLS material, JWT key, and mode-specific permissions are prepared:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
$EDITOR deploy/docker/.env

docker compose \
  --env-file deploy/docker/.env \
  -f deploy/docker/compose.yml \
  up -d --build
```

See [Deployment](../../docs/deployment/deployment.md) for the mode selector, [Configuration](../../docs/deployment/configuration.md) for environment/settings reference, and [Operations](../../docs/deployment/operations.md) for backup and lifecycle procedures.

The repository-root `docker-compose.yml` is a development-only PostgreSQL helper and is not part of this production stack.
