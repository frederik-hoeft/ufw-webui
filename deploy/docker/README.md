# Docker deployment

The production Compose stack and ASP/frontend image live in this directory. See
[`../../docs/deployment/deployment.md`](../../docs/deployment/deployment.md) for
the complete deployment, permission, backup, update, and rollback runbook.

Quick start after completing the host/daemon preparation from the runbook:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
# edit .env
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml up -d --build
```

The web container is intentionally published only on host loopback. A host
reverse proxy terminates browser HTTPS and forwards to that listener.
