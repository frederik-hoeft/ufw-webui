# Docker deployment

The production Compose stack lives in this directory. It deliberately builds two
independent application images:

- `Dockerfile.frontend` publishes `Ufw.Client` and copies only its static output
  into a hardened, non-root nginx runtime. nginx is the only public service,
  terminates TLS, serves the WASM application, and proxies `/api/*` to ASP over
  a shared Unix-domain socket volume.
- `Dockerfile.asp` publishes only `Ufw.Web`. Kestrel listens only on that Unix
  socket; ASP has no frontend files and no TCP listener.

PostgreSQL is reachable only from ASP on a separate internal network. The
privileged `Ufw.Systemd` daemon remains a host systemd service reached through
its group-restricted Unix socket.

See [`../../docs/deployment/deployment.md`](../../docs/deployment/deployment.md)
for the complete deployment, TLS, permission, backup, update, and rollback
runbook.

Quick start after completing the host/daemon/TLS preparation from the runbook:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
# edit .env
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml up -d --build
```
