# Deployment runbook

## Deployment model

A production deployment has three trust/runtime domains:

1. `Ufw.Systemd` runs directly on the Linux host as a privileged systemd service. It owns UFW execution, daemon signing trust, replay state, deployment identity, and the Unix-domain IPC socket.
2. `Ufw.Web` runs as an unprivileged container user. The production image also serves the published `Ufw.Client` WebAssembly assets, so the browser and REST API share one origin.
3. PostgreSQL runs in a separate container and is reachable only on the private Compose network.

The web container never receives host network capabilities, the Docker socket, UFW files, or daemon security state. Its only privileged-host reachability is the read-only bind mount containing the daemon's Unix socket. Signed mutation authorization remains mandatory even for peers that can connect to that socket.

The committed Compose stack binds Kestrel only to `127.0.0.1` on the host. Terminate browser HTTPS at a host reverse proxy and forward to that loopback listener. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set by Compose so ASP.NET Core observes the original HTTPS scheme. That framework switch accepts forwarded headers without a fixed proxy-IP allowlist, which is useful because the host-side proxy source address differs between rootful and rootless Docker. The corresponding trust assumption is therefore strict: keep the Kestrel publication loopback-only, do not expose port 8080 to untrusted networks, and do not attach untrusted containers to the application network.

## Supported Docker ownership models

The same Compose file supports the following models. `Ufw.Web` always runs as the image's non-root UID/GID (`1654:1654`). PostgreSQL uses the official image lifecycle: its entrypoint may perform storage initialization as container root, but the database server itself is always exec'd as the image's unprivileged `postgres` account. We do not override that image user. The differences are Docker's privilege model, the IPC supplemental group, and whether PostgreSQL uses a named volume or a host bind mount.

### 1. Rootful Docker, non-root application containers

Use a dedicated host IPC group such as `ufw-webui-ipc` when installing the daemon. The daemon runs as `root:<ipc-group>` and creates:

- `/run/ufw-manager` as `root:<ipc-group>` mode `0750` via systemd;
- `/run/ufw-manager/ufw-systemd.sock` as `root:<ipc-group>` mode `0660` via `Ufw.Systemd`.

Set `UFW_IPC_CONTAINER_GID` to the numeric host GID of that group. Docker adds that group to the non-root ASP process, allowing socket connect without granting container root.

The PostgreSQL server runs as its non-root `postgres` account and normally uses the Docker-managed named volume.

Rootful Docker programs host firewall rules independently from UFW and can undermine assumptions about published-port filtering. The supplied stack therefore publishes only the web HTTP port on host loopback and does not publish PostgreSQL. Rootless Docker is preferable when practical.

### 2. Rootless Docker, non-root ASP and PostgreSQL with a named volume

Install Rootless Docker normally and enable lingering for the Docker user if the stack must survive logout/reboot. In rootless Docker, container UID/GID `0` maps to the host UID/primary GID of the user running the rootless daemon, while nonzero container IDs map into the user's subordinate UID/GID ranges.

Install `Ufw.Systemd` with the **rootless Docker user's existing primary host group** as its IPC group. Keep:

```text
UFW_IPC_CONTAINER_GID=0
POSTGRES_DATA_SOURCE=postgres-data
```

The ASP process is still UID 1654 rather than container root; GID 0 is only an additional group used to reach the host socket. Under rootless Docker that group maps to the Docker user's primary host group, which is the same group assigned to the daemon socket.

The PostgreSQL server also runs non-root. Let the official entrypoint initialize the named volume and establish the ownership expected by the image; the resulting database-file ownership maps into the rootless Docker user's subordinate ID range.

### 3. Rootless Docker, non-root ASP and PostgreSQL with host bind storage

A host bind mount can be used when it is operationally useful to keep PostgreSQL storage under a known host path. Create an empty directory owned by the rootless Docker user and point Compose at it:

```bash
mkdir -p "$HOME/ufw-webui/postgres-data"
```

```text
POSTGRES_DATA_SOURCE=/home/<docker-user>/ufw-webui/postgres-data
```

Do not force the PostgreSQL service to UID 0 or to the image's `postgres` UID through Compose. Leave the official image lifecycle intact: under rootless Docker its container-root entrypoint maps to the Docker host user and can initialize/chown the bind mount, then the actual PostgreSQL server is exec'd as `postgres`.

Because that nonzero container UID maps to a subordinate host UID, the live database files are **not** expected to be owned by or normally readable as the rootless Docker user's host UID after initialization. A bind mount therefore provides a stable host path, not host-user ownership of raw `PGDATA`. Use `deploy/docker/backup-postgres.sh`, `pg_dump`, `pg_basebackup`, or a coordinated filesystem snapshot/clean shutdown for backups rather than copying a live data directory. The included helper streams `pg_dump` through Docker and writes the resulting dump as the invoking host user.

## Host prerequisites

The host requires:

- Linux with UFW installed and functional;
- systemd for the privileged daemon service;
- Docker Engine plus the Compose and Buildx plugins, rootful or rootless;
- OpenSSL for initial key generation;
- a host HTTPS reverse proxy such as Caddy, nginx, Apache, or HAProxy.

For rootless Docker, follow Docker's rootless setup requirements (`newuidmap`, `newgidmap`, subordinate ID ranges, and user-systemd lingering when required).

## 1. Build and install the privileged daemon

The firewall host does not need a .NET SDK, compiler, or NativeAOT toolchain. `deploy/systemd/publish.sh` builds the daemon inside a Docker BuildKit builder and exports the NativeAOT executable from a `scratch` artifact stage back to the host:

```bash
./deploy/systemd/publish.sh linux-x64
```

For ARM64 use `linux-arm64` instead. The script requires Docker Buildx, builds for the matching Linux target platform, and prints the resulting host path. Cross-architecture builds require a Buildx builder capable of executing that target platform (for example through configured emulation or a native builder node).

The exported artifact contains the native daemon executable only; it does not require the .NET runtime on the firewall host. The committed builder is Debian Bookworm/glibc based, so deploy it to a compatible Linux userspace. If the host is older, build against an appropriately old compatible Linux base rather than assuming NativeAOT removes libc compatibility requirements.

Before installation, a simple host compatibility check is:

```bash
artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd --help
```

### Rootful Docker

Install with the default dedicated IPC group:

```bash
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd
```

Resolve the group GID for Compose:

```bash
getent group ufw-webui-ipc
```

Set that numeric GID as `UFW_IPC_CONTAINER_GID`.

### Rootless Docker

Assume the rootless Docker daemon runs as user `ufw-web`. Resolve that user's primary group and install the daemon using it:

```bash
IPC_GROUP="$(id -gn ufw-web)"
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd \
  --ipc-group "$IPC_GROUP"
```

Keep `UFW_IPC_CONTAINER_GID=0` in Compose.

The installer preserves existing daemon configuration and `authorized_keys` on updates unless replacement files are supplied explicitly. It enables/restarts the systemd service by default.

Inspect the result:

```bash
systemctl status ufw-systemd.service
sudo stat -c '%A %U:%G %n' /run/ufw-manager /run/ufw-manager/ufw-systemd.sock
```

Expected socket mode is `srw-rw----`; the directory is group-traversable but not writable by the web peer.

## 2. Provision daemon mutation authority

Generate the browser/admin P-256 signing key outside the container stack:

```bash
umask 077
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out intent-key.pem
openssl pkey -in intent-key.pem -pubout -out intent-key.pub.pem
```

Install only the public key on the daemon:

```bash
sudo install -o root -g root -m 0640 intent-key.pub.pem /etc/ufw-manager/authorized_keys
sudo systemctl restart ufw-systemd.service
```

Keep the private key with the administrator/password manager. Never mount it into `Ufw.Web`.

Multiple authorized public keys and comments can be placed in the daemon file as supported by the signed-intent protocol.

## 3. Provision the web JWT key

Create a separate P-256 PKCS#8 key for ASP JWT signing:

```bash
install -d -m 0700 "$HOME/ufw-webui/secrets"
umask 077
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 \
  -out "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
```

The ASP process is non-root. The bind-mounted key therefore needs group-read access through the same mapped supplemental group used for IPC.

For rootless Docker:

```bash
chmod 0640 "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
# The file should remain owned by the rootless Docker user and its primary group.
```

For rootful Docker, set the key's group to the daemon IPC group and keep mode `0640`:

```bash
sudo chown root:ufw-webui-ipc /path/to/jwt-signing-key.pem
sudo chmod 0640 /path/to/jwt-signing-key.pem
```

## 4. Configure Compose

Create the private Compose environment file:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
$EDITOR deploy/docker/.env
```

At minimum change:

- `UFW_WEB_JWT_KEY_PATH`;
- bootstrap email/username/password;
- PostgreSQL password;
- `UFW_IPC_CONTAINER_GID` for rootful Docker;
- `POSTGRES_DATA_SOURCE` if using the rootless bind-storage model.

The initial bootstrap password must satisfy the configured ASP.NET Core Identity password policy. After the account has been created successfully, set `UFW_BOOTSTRAP_PASSWORD=` to an empty value; bootstrap never resets the password of an existing account. Use a strong generated PostgreSQL password that does not contain connection-string delimiters; for example, base64 output from `openssl rand -base64 32` is convenient for this Compose configuration.

## 5. Build and start the container stack

From the repository root:

```bash
docker compose \
  --env-file deploy/docker/.env \
  -f deploy/docker/compose.yml \
  up -d --build
```

`Ufw.Web` waits for the PostgreSQL health check, applies EF Core migrations at startup, and serves both the REST API and the published Blazor client. Its persistent `web-state` volume holds framework per-instance state such as ASP.NET Core Data Protection keys; application/domain data remains in PostgreSQL.

Inspect logs:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml ps
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml logs --tail=200 web postgres
```

The direct HTTP listener is intentionally loopback-only. A local probe should include the original scheme header if HTTPS redirection is enabled:

```bash
curl -H 'X-Forwarded-Proto: https' http://127.0.0.1:8080/health
```

## 6. Terminate HTTPS at the host reverse proxy

Production browser access must be HTTPS because the refresh cookie is `Secure`, and the client/API should use one consistent origin. The container image serves both, so no production CORS exception is required.

A minimal Caddy example is:

```caddyfile
ufw.example.net {
    reverse_proxy 127.0.0.1:8080
}
```

Equivalent nginx/Apache/HAProxy configurations must preserve the original host and forward the HTTPS scheme. Keep the Compose-published port on loopback rather than exposing Kestrel directly.

## 7. Verify application/daemon connectivity

After signing in:

1. Open **Status** and verify daemon-backed availability.
2. Reconcile **Network interfaces** and confirm the host inventory appears.
3. Add a harmless test rule only after verifying the canonical command preview and signing key setup.

For host-side permission troubleshooting:

```bash
namei -l /run/ufw-manager/ufw-systemd.sock
sudo stat -c '%a %U:%G %n' /run/ufw-manager /run/ufw-manager/ufw-systemd.sock
```

For rootless Docker, remember that container GID 0 maps to the rootless Docker user's primary host GID; it does not grant host-root group membership.

## Backups

Create a logical PostgreSQL backup as the invoking host user:

```bash
./deploy/docker/backup-postgres.sh /path/to/backup-directory
```

The script writes a custom-format `pg_dump` with mode `0600` and only atomically renames it into place after `pg_dump` succeeds. Automate this host-side with a timer/backup framework if desired.

Also back up separately:

- `/etc/ufw-manager/authorized_keys`;
- `/etc/ufw-manager/settings.json`;
- the ASP JWT signing key;
- administrator browser signing private keys/password-manager entries.

The daemon deployment ID and replay store under `/var/lib/ufw-manager` are host state. Preserve the deployment ID when restoring the same logical deployment. Do not restore stale nonce data in a way that weakens replay protection; restoring the complete recent daemon state is safest.

## Updates

Before an update:

1. create a PostgreSQL backup;
2. retain the currently deployed web image/tag and daemon binary for rollback;
3. review database migrations and protocol/security changes in the target release.

Update the daemon by rebuilding the NativeAOT artifact in Docker and reinstalling it:

```bash
./deploy/systemd/publish.sh linux-x64
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd \
  --ipc-group <same-group-used-for-this-host>
```

No .NET/native build dependencies are installed on the host; updates use the same containerized compiler path as first installation.

The installer replaces the executable/unit but preserves settings and authorized keys unless explicit replacement files are supplied.

The committed PostgreSQL image is pinned to major version 18. Treat a future PostgreSQL **major** version change as a database migration project (`pg_upgrade` or logical dump/restore), not as an ordinary Compose image update.

Update the container stack:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml build --pull web
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml up -d
```

`Ufw.Web` applies forward EF Core migrations during startup. Wait for a healthy running stack and verify daemon-backed status before discarding rollback artifacts.

## Rollback

Application image rollback is straightforward only while the database schema remains compatible with the previous application. EF migrations are applied forward automatically but are **not** automatically rolled back. For a release containing incompatible schema changes, restore the pre-update PostgreSQL backup before starting the previous web image.

Restore the previous daemon binary using the same installer, keeping the same settings/security state unless the release notes explicitly require otherwise.

## Security and permission notes

- The web container's socket bind mount is read-only; connecting to the Unix-domain socket does not require filesystem write access to its parent directory.
- The socket is `0660`, and `/run/ufw-manager` is `0750`. Do not solve IPC failures by making the socket world-accessible.
- Unix socket permissions and optional IPC TLS/mTLS are defense in depth. A mutating daemon request still requires a valid browser-created signed intent.
- Do not mount `/etc/ufw`, `/var/lib/ufw-manager`, the host Docker socket, or the browser signing private key into the ASP container.
- PostgreSQL is not published to the host by the production Compose stack.
- Keep `deploy/docker/.env`, JWT private keys, database dumps, and browser private keys out of source control.
