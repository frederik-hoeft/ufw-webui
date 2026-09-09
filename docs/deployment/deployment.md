# Deployment runbook

## Deployment model

A production deployment has four deliberately separate runtime/trust domains:

1. `Ufw.Client` is compiled to static WebAssembly/browser assets and copied into a dedicated non-root nginx image. nginx is the only public container, serves those immutable frontend assets, terminates browser TLS, and proxies only `/api/*` to ASP.
2. `Ufw.Web` runs as a separate unprivileged container. Kestrel listens only on a Unix-domain socket in a Docker volume shared read-only with nginx; ASP exposes no TCP listener and has no copy of the frontend assets. It owns HTTP authentication/session handling, application metadata, database access, and the local IPC client.
3. PostgreSQL runs in a separate container on a second private Compose network that nginx cannot join.
4. `Ufw.Systemd` runs directly on the Linux host as a privileged systemd service. It owns authoritative UFW execution, daemon signing trust, replay state, deployment identity, and the Unix-domain IPC socket.

```text
Browser
   | HTTPS
   v
nginx (public, static Ufw.Client + TLS)
   | /api/* only over Docker-volume UDS
   v
Ufw.Web / ASP (no TCP listener) -> PostgreSQL (private DB network)
   |
   | Unix-domain socket bind mount
   v
Ufw.Systemd (host systemd service, privileged)
   |
   v
UFW / iptables-nftables host state
```

The split between nginx and ASP is a security boundary, not only a packaging choice. Browser-side mutation signing means the code delivered to the browser participates in the mutation-authorization trusted computing base. A compromised ASP process must therefore not be able to replace `index.html`, the Blazor runtime, application assemblies, or JavaScript with a modified frontend that captures administrator signing keys. The production ASP image contains no frontend publish output, has no Docker socket, and cannot write the read-only nginx filesystem. Updating the frontend requires replacing/restarting the independently built nginx image through the deployment operator.

nginx is the sole public endpoint. It accepts TLS on container port `8443`, serves static files itself, and proxies only `/api/*` to `Ufw.Web` over `/run/ufw-web-api/socket/asp.sock`. The socket lives under a pre-seeded `socket/` directory in a Docker-managed `api-socket` volume mounted read-write by ASP and read-only by nginx. Both images seed that child directory with UID/GID `1654:1654` and mode `0770`, so Docker volume initialization does not depend on the ownership of the volume root or on which container is created first. ASP exposes no TCP listener at all. ASP and PostgreSQL share a separate internal database network that nginx does not join, while nginx alone joins the normal externally connected `public` network. The daemon remains outside Docker and is reachable by ASP only through the independent group-restricted host Unix socket bind mount.

The nginx image runs as the official image's unprivileged `nginx` user, with all Linux capabilities dropped, a read-only root filesystem, and writable nginx runtime state limited to a small `/tmp` tmpfs. The ASP image runs as UID/GID `1654:1654`, also with all capabilities dropped and a read-only root filesystem. PostgreSQL follows the official image lifecycle and runs the database server as its unprivileged `postgres` user.

nginx sets the original scheme/host forwarding headers when proxying `/api/*`. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set only on ASP. Because Kestrel listens only on the shared Unix socket and only nginx receives that volume as a peer mount, forwarded-header trust is constrained by the socket boundary rather than a brittle container IP allowlist.

## Supported Docker ownership models

The same Compose file supports three host/container ownership layouts. The application containers keep the same internal users in every model; the differences are whether the Docker daemon itself is rootful/rootless, how supplemental groups map to host bind mounts, and whether PostgreSQL uses a managed volume or a host bind path.

### 1. Rootful Docker, non-root application containers

Use a dedicated host IPC group such as `ufw-webui-ipc` when installing the daemon. The daemon runs as `root:<ipc-group>` and creates:

- `/run/ufw-manager` as `root:<ipc-group>` mode `0750` via systemd;
- `/run/ufw-manager/ufw-systemd.sock` as `root:<ipc-group>` mode `0660` via `Ufw.Systemd`.

Set `UFW_IPC_CONTAINER_GID` to the numeric host GID of that group. Docker adds the group to the non-root ASP process, allowing socket connection without running ASP as root.

Use separate host groups for ASP's JWT key and the nginx TLS key, for example `ufw-webui-jwt` and `ufw-webui-tls`. Set `UFW_JWT_CONTAINER_GID` and `UFW_TLS_CONTAINER_GID` to their numeric host GIDs. Keeping those groups distinct from `ufw-webui-ipc` prevents ordinary IPC-group membership from also granting read access to application private keys.

PostgreSQL normally uses the Docker-managed named volume. The PostgreSQL server runs as the image's unprivileged `postgres` user.

Rootful Docker programs host firewall rules independently from UFW and can undermine assumptions about packet filtering around published ports. For that reason only nginx is published; ASP and PostgreSQL have no host port. Rootless Docker is preferable when practical on a UFW-managed firewall host.

### 2. Rootless Docker, non-root ASP/nginx/PostgreSQL with a named volume

Install Rootless Docker normally and enable lingering for the Docker user if the stack must survive logout/reboot. Container UID/GID `0` maps to the rootless Docker user's host identity, while nonzero container IDs map through subordinate UID/GID ranges.

Install `Ufw.Systemd` with the rootless Docker user's existing primary host group as its IPC group. Keep:

```text
UFW_IPC_CONTAINER_GID=0
UFW_JWT_CONTAINER_GID=0
UFW_TLS_CONTAINER_GID=0
POSTGRES_DATA_SOURCE=postgres-data
```

ASP remains UID 1654 and nginx remains the image's unprivileged `nginx` user. GID 0 is only an additional container group used to access host files owned by the rootless Docker user's primary group. It does not make either process container root and does not grant host-root group membership.

The PostgreSQL server runs non-root. Let the official image initialize its named volume and establish the internal ownership expected by PostgreSQL.

### 3. Rootless Docker with host bind-mounted PostgreSQL storage

This is identical to model 2 except PostgreSQL storage uses a stable host path instead of the Docker-managed named volume. This can be convenient when filesystem-level storage placement or backup tooling needs a predictable directory.

Create the directory as the rootless Docker user:

```bash
mkdir -p "$HOME/ufw-webui/postgres-data"
```

Then set:

```text
POSTGRES_DATA_SOURCE=/home/<docker-user>/ufw-webui/postgres-data
```

Do not override PostgreSQL to container root or invent a different runtime UID to make host permissions appear simpler. Leave the official image's expected ownership model intact. Under rootless Docker, the PostgreSQL user's container UID maps to a subordinate host UID, so the live database files are not expected to be directly readable as the rootless Docker user's ordinary host UID.

A bind mount provides a stable host path, but **logical backups remain the supported backup boundary**. Use the supplied `pg_dump` helper instead of copying a live `PGDATA` directory.

## Host prerequisites

The firewall host needs:

- a supported Linux distribution with systemd and UFW;
- Docker Engine with Compose/Buildx, either rootful or rootless;
- a TLS certificate/key pair for the browser-facing nginx endpoint;
- `openssl` for initial application/signing key provisioning;
- root access only for installing/updating the privileged daemon and its host state.

The host does **not** need a .NET SDK, .NET runtime, Clang, or NativeAOT toolchain. The daemon publish flow performs NativeAOT compilation inside Docker and exports the resulting native executable.

For rootless Docker, enable the user's Docker service and lingering as appropriate:

```bash
systemctl --user enable --now docker
sudo loginctl enable-linger "$USER"
```

The default deployment exposes nginx on host port `8443`, avoiding privileged-port setup. If the final service should bind host port `443` directly under rootless Docker, follow Docker's rootless privileged-port procedure before changing `UFW_HTTPS_PORT=443` (for example granting `CAP_NET_BIND_SERVICE` to `rootlesskit` or lowering `net.ipv4.ip_unprivileged_port_start`).

## 1. Build and install the privileged daemon

Build a NativeAOT daemon artifact using Docker Buildx:

```bash
./deploy/systemd/publish.sh linux-x64
```

For ARM64 use `linux-arm64` instead. The Buildx builder contains the .NET SDK and native compiler toolchain. Its final `scratch` export stage contains only the native `Ufw.Systemd` executable, which is copied to the host under:

```text
artifacts/publish/ufw-systemd/<rid>/Ufw.Systemd
```

Cross-architecture builds require a Buildx builder capable of executing the target platform through a native node or configured emulation.

The exported daemon is self-contained with respect to .NET but still targets the normal Linux libc ABI used by the committed build image. Before installation, smoke-test compatibility on the target host:

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

Expected socket mode is `srw-rw----`; the directory is group-traversable but not writable by ASP.

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

Keep the private key with the administrator/password manager. Never mount it into nginx, ASP, PostgreSQL, or the daemon.

Multiple authorized public keys and comments can be placed in the daemon file as supported by the signed-intent protocol.

## 3. Provision the ASP JWT key

Create a separate P-256 PKCS#8 key for ASP JWT signing:

```bash
install -d -m 0700 "$HOME/ufw-webui/secrets"
umask 077
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 \
  -out "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
```

ASP is non-root. The bind-mounted key therefore needs group-read access through an ASP supplemental group. Keep this separate from daemon IPC membership in rootful deployments.

For rootless Docker:

```bash
chmod 0640 "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
# Keep the file owned by the rootless Docker user and its primary group.
```

For rootful Docker, create a dedicated JWT-key group, set the key's group to it, and expose only that numeric GID to ASP:

```bash
sudo groupadd --system ufw-webui-jwt  # only if it does not already exist
sudo chown root:ufw-webui-jwt /path/to/jwt-signing-key.pem
sudo chmod 0640 /path/to/jwt-signing-key.pem
getent group ufw-webui-jwt
```

Set the reported numeric GID as `UFW_JWT_CONTAINER_GID`. The JWT key is mounted only into ASP. It is not available to nginx, and members of the daemon IPC group do not gain JWT-key read access merely by being able to connect to the daemon socket.

## 4. Provision nginx TLS material

Create a directory containing exactly the certificate/key names expected by the hardened nginx configuration:

```text
/path/to/ufw-webui/tls/
├── tls.crt
└── tls.key
```

`tls.crt` may contain the appropriate certificate chain for the clients that will connect. `tls.key` is the matching private key.

For a rootless deployment owned by the Docker user:

```bash
mkdir -p "$HOME/ufw-webui/tls"
chmod 0750 "$HOME/ufw-webui/tls"
chmod 0644 "$HOME/ufw-webui/tls/tls.crt"
chmod 0640 "$HOME/ufw-webui/tls/tls.key"
```

Keep the directory/key group equal to the rootless Docker user's primary group and leave `UFW_TLS_CONTAINER_GID=0`.

For rootful Docker, use a dedicated group and map its numeric GID through `UFW_TLS_CONTAINER_GID`:

```bash
sudo groupadd --system ufw-webui-tls  # only if it does not already exist
sudo chown -R root:ufw-webui-tls /path/to/ufw-webui/tls
sudo chmod 0750 /path/to/ufw-webui/tls
sudo chmod 0644 /path/to/ufw-webui/tls/tls.crt
sudo chmod 0640 /path/to/ufw-webui/tls/tls.key
getent group ufw-webui-tls
```

The TLS directory is mounted read-only **only into nginx**. ASP has no route to the TLS private key. If external certificate automation replaces `tls.crt`/`tls.key` in that directory, reload nginx after renewal:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml exec frontend nginx -s reload
```

Recreating only the frontend container is also safe.

## 5. Configure Compose

Create the private Compose environment file:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
$EDITOR deploy/docker/.env
```

At minimum change:

- `UFW_TLS_HOST_DIR`;
- `UFW_WEB_JWT_KEY_PATH`;
- bootstrap email/username/password;
- PostgreSQL password;
- `UFW_IPC_CONTAINER_GID`, `UFW_JWT_CONTAINER_GID`, and `UFW_TLS_CONTAINER_GID` for rootful Docker;
- `POSTGRES_DATA_SOURCE` if using the rootless bind-storage model.

The initial bootstrap password must satisfy the configured ASP.NET Core Identity password policy. After the account has been created successfully, set `UFW_BOOTSTRAP_PASSWORD=` to an empty value; bootstrap never resets the password of an existing account.

Use a strong generated PostgreSQL password that does not contain connection-string delimiters. Base64 output from `openssl rand -base64 32` is convenient for this Compose configuration.

## 6. Build and start the container stack

From the repository root:

```bash
docker compose \
  --env-file deploy/docker/.env \
  -f deploy/docker/compose.yml \
  build --pull frontend asp

docker compose \
  --env-file deploy/docker/.env \
  -f deploy/docker/compose.yml \
  up -d
```

The two build products are intentionally independent:

- `frontend` compiles only `Ufw.Client` and copies its publish output into nginx; its build also verifies that the .NET 10 Blazor boot-script fingerprint placeholder was resolved into an actual published asset;
- `asp` compiles only `Ufw.Web` and fails the image build if a frontend `wwwroot/index.html` appears in the ASP publish output.

Do not collapse them into one runtime image. The inability of a compromised ASP process to replace browser signing code is part of the production security model.

ASP waits for PostgreSQL health, applies EF Core migrations at startup, and performs bootstrap reconciliation. Its persistent `asp-state` volume contains framework per-instance state such as ASP.NET Core Data Protection keys; application/domain data remains in PostgreSQL.

Inspect the topology and logs:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml ps
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml logs --tail=200 frontend asp postgres
```

Only nginx should have a published host port. `docker compose ps` must not show host-published ports for ASP or PostgreSQL.

## 7. Verify TLS/frontend/API isolation

Probe nginx over HTTPS:

```bash
curl -kI https://127.0.0.1:8443/
curl -ksS -o /dev/null -w '%{http_code}\n' https://127.0.0.1:8443/api/v1/rules
# expected before login: 401 from ASP, proving /api/* is proxied
```

Use the configured hostname/CA rather than `-k` for the real browser verification.

Verify the private ASP socket exists and that ASP has no published TCP port:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml exec asp \
  sh -c 'test -S /run/ufw-web-api/socket/asp.sock && echo "ASP Unix socket present"'
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml port asp 8080
# expected: no published mapping / an error because ASP exposes no TCP port
```

Verify network/volume separation with Compose inspection if needed:

- `frontend`: `public` network + read-only `api-socket` volume;
- `asp`: `database` network + read-write `api-socket` volume;
- `postgres`: `database` network only.

nginx is deliberately **not** a generic proxy. Only `/api/*` is forwarded. All other paths are satisfied from the static frontend image (or return an nginx response). Adding a future ASP route does not expose it until the nginx proxy policy is intentionally changed.

After signing in through the browser:

1. open **Status** and verify daemon-backed availability;
2. reconcile **Network interfaces** and confirm the host inventory appears;
3. add a harmless test rule only after verifying the canonical command preview and signing key setup.

For host-side IPC permission troubleshooting:

```bash
namei -l /run/ufw-manager/ufw-systemd.sock
sudo stat -c '%a %U:%G %n' /run/ufw-manager /run/ufw-manager/ufw-systemd.sock
```

For rootless Docker, remember that supplemental container GID 0 maps to the rootless Docker user's primary host GID; it does not grant host-root group membership.

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
- the nginx TLS certificate/key or the certificate automation state needed to reproduce them;
- administrator browser signing private keys/password-manager entries.

The daemon deployment ID and replay store under `/var/lib/ufw-manager` are host state. Preserve the deployment ID when restoring the same logical deployment. Do not restore stale nonce data in a way that weakens replay protection; restoring the complete recent daemon state is safest.

## Updates

Before an update:

1. create a PostgreSQL backup;
2. retain the currently deployed `frontend` and `asp` image tags/digests and daemon binary;
3. review database migrations, frontend signing changes, and daemon protocol/security changes in the target release.

### Daemon update

Rebuild the NativeAOT artifact in Docker and reinstall it:

```bash
./deploy/systemd/publish.sh linux-x64
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd \
  --ipc-group <same-group-used-for-this-host>
```

No .NET/native build dependencies are installed on the firewall host. The installer replaces the executable/unit but preserves settings and authorized keys unless explicit replacement files are supplied.

### Container update

Build both application images before replacing either runtime container:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml build --pull frontend asp
```

Then apply the stack:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml up -d
```

`Ufw.Web` applies forward EF Core migrations during ASP startup. Wait for the stack to stabilize, verify browser frontend integrity and daemon-backed status, and only then discard rollback artifacts.

The frontend and ASP can technically be rebuilt/deployed independently, but releases should document compatibility when doing so. The security boundary is preserved either way because replacing ASP never modifies the nginx image/filesystem.

The committed PostgreSQL image is pinned to major version 18. Treat a future PostgreSQL **major** version change as a database migration project (`pg_upgrade` or logical dump/restore), not an ordinary Compose image update.

## Rollback

Frontend rollback is independent: restore the previous `frontend` image and recreate only that service. No ASP/database state is modified by that operation.

ASP image rollback is straightforward only while the database schema remains compatible with the previous application. EF migrations are applied forward automatically but are **not** automatically rolled back. For a release containing incompatible schema changes, restore the pre-update PostgreSQL backup before starting the previous ASP image.

Restore the previous daemon binary using the same installer, keeping the same settings/security state unless release notes explicitly require otherwise.

## Security and permission notes

- The browser frontend is part of the mutation-signing TCB. It is served only by the dedicated nginx image, never by ASP.
- nginx receives the TLS private key and read-only access to the ASP API socket volume, but does **not** receive the ASP JWT key, daemon socket, PostgreSQL network, Docker socket, UFW state, or browser signing private key.
- ASP receives the JWT key, daemon socket, and read-write side of its nginx-facing API socket volume, but does **not** receive nginx static assets, nginx TLS key, Docker socket, UFW files, daemon security state, or any TCP listener.
- PostgreSQL shares a network only with ASP and has no published host port.
- nginx mounts the ASP API-socket volume read-only; connecting to the Unix-domain socket itself does not require filesystem write access to the mounted volume.
- The daemon socket is `0660`, and `/run/ufw-manager` is `0750`. Do not solve IPC failures by making the socket world-accessible.
- Unix socket permissions and optional IPC TLS/mTLS are defense in depth. A mutating daemon request still requires a valid browser-created signed intent.
- nginx sets HSTS and restrictive browser security headers, including a CSP intended for the committed Blazor WebAssembly application. If future frontend dependencies require additional origins/capabilities, review the CSP rather than broadly disabling it.
- Do not mount `/etc/ufw`, `/var/lib/ufw-manager`, the host Docker socket, or the browser signing private key into any application container.
- Keep `deploy/docker/.env`, TLS/JWT private keys, database dumps, and browser private keys out of source control.
