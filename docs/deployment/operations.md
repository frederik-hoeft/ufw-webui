# Deployment Operations

These procedures apply after either the [rootful](rootful-docker.md) or [rootless](rootless-docker.md) deployment has been established.

## Routine verification

Check container state and recent logs:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml ps
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml logs --tail=200 frontend asp postgres
```

Only nginx should expose a published host port. `frontend` joins the normal public network; `asp` and `postgres` share the internal database network; nginx reaches ASP through the `api-socket` volume rather than TCP.

Verify the host daemon separately:

```bash
systemctl status ufw-systemd.service
sudo stat -c '%A %U:%G %n' \
  /var/lib/ufw-webui/ipc \
  /var/lib/ufw-webui/ipc/ufw-systemd.sock
```

Do not fix Unix-socket access problems by making the socket world-readable/writable. Correct the deployment-mode group mapping instead.

## TLS certificate renewal

The nginx TLS directory is bind-mounted read-only. When external certificate automation replaces `tls.crt` or `tls.key`, reload nginx:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml exec frontend nginx -s reload
```

Recreating only the frontend container is also safe.

## PostgreSQL backups

Use the repository helper to create a logical custom-format dump:

```bash
./deploy/docker/backup-postgres.sh /path/to/backup-directory
```

The helper writes the dump with mode `0600` and only renames it into place after `pg_dump` succeeds. This logical dump is the supported database backup boundary for both named-volume and host-bind PostgreSQL storage.

Back up these non-database assets separately:

- `/etc/ufw-manager/settings.json`;
- `/etc/ufw-manager/authorized_keys`;
- ASP's JWT signing key;
- nginx TLS certificate/key or the external certificate-automation state needed to recreate them;
- administrator mutation-signing private keys/password-manager entries.

The daemon deployment ID, replay store, and any active reorder recovery journal under `/var/lib/ufw-manager` are host security/safety state. Preserve the deployment ID when restoring the same logical deployment. Restoring the complete recent daemon state is safer than selectively restoring stale replay records or discarding an active recovery record. The socket under `/var/lib/ufw-webui/ipc` is ephemeral and must not be backed up.

## Updating the daemon

Before an update, keep a copy of the current daemon binary and create the normal application/database backups.

Build the replacement artifact and reinstall it with the same IPC group used by the host:

```bash
./deploy/systemd/publish.sh linux-x64
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd \
  --ipc-group <existing-ipc-group>
```

The installer preserves existing daemon settings and `authorized_keys` unless replacement files are supplied explicitly.

Review protocol/security changes before rolling out a daemon that changes IPC or signed-intent versions. The web application and browser must understand the corresponding contracts.

### Interrupted reorder recovery

A reorder writes `/var/lib/ufw-manager/reorder-recovery.json` before a rule may be temporarily removed. The daemon resolves that record during startup and before any later firewall mutation. If recovery cannot establish a safe authoritative state, mutations fail closed while rule listing remains available for diagnostics.

Do not delete or edit an outstanding recovery journal merely to unblock mutations. Inspect daemon logs and authoritative `ufw status numbered` state first. The record exists specifically to retain enough information to confirm or restore a row whose delete/reinsert move may have been interrupted.

## Updating containers

Create a PostgreSQL backup before applying an application release. Retain the currently deployed frontend and ASP image tags/digests until verification succeeds.

Build both images first:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml \
  build --pull frontend asp
```

Then apply the stack:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml up -d
```

`Ufw.Web` applies forward EF Core migrations at startup. Wait for the stack to stabilize and verify browser frontend integrity plus daemon-backed status before discarding rollback artifacts.

Frontend and ASP images can be replaced independently, but a release that does so must preserve API/client compatibility. Replacing ASP never changes the nginx frontend filesystem, which preserves the frontend-delivery security boundary.

The committed PostgreSQL image is pinned to major version 18. Treat a future PostgreSQL major-version change as a database migration project (`pg_upgrade` or logical dump/restore), not as a normal image refresh.

## Rollback

Frontend rollback is independent: restore the previous frontend image and recreate only that service.

ASP rollback is straightforward only while the database schema remains compatible. EF migrations are applied forward automatically and are not rolled back automatically. If an application release introduced incompatible schema changes, restore the pre-update PostgreSQL backup before starting the older ASP image.

Daemon rollback reinstalls the previous binary through the normal systemd installer while keeping the same configuration and daemon security state unless the release explicitly requires otherwise.

## Uninstalling the daemon

Remove the host daemon while preserving its configuration and security state:

```bash
sudo ./deploy/systemd/uninstall.sh
```

This stops/disables the service and removes the installed unit, binary, installed documentation, and ephemeral IPC tree. It does not alter existing UFW rules and does not remove the IPC group.

By default, `/etc/ufw-manager` and `/var/lib/ufw-manager` are retained. To deliberately remove daemon authorization, replay, deployment, and configuration state as well:

```bash
sudo ./deploy/systemd/uninstall.sh --purge
```

`--purge` still does not modify UFW rules. Container teardown and PostgreSQL deletion are separate operations.
