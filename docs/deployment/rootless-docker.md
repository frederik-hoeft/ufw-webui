# Rootless Docker Deployment

This runbook is for Docker Engine running in rootless mode under a dedicated or normal unprivileged host user. Application containers remain non-root; host bind-mount access is granted through the rootless user namespace rather than by exposing arbitrary host GIDs.

The key mapping rule is simple: supplemental container GID `0` maps to the rootless Docker user's primary host group. In this runbook, `UFW_IPC_CONTAINER_GID`, `UFW_JWT_CONTAINER_GID`, and `UFW_TLS_CONTAINER_GID` therefore remain `0`.

Do not substitute the explicit host-GID instructions from the rootful runbook.

## 1. Prepare rootless Docker

Run the stack as the intended rootless Docker user and verify that the rootless context/service works:

```bash
docker context ls
systemctl --user status docker
```

If the production stack must start at boot and survive user logout, enable the user service and lingering:

```bash
systemctl --user enable --now docker
sudo loginctl enable-linger "$USER"
```

Lingering is an operational choice; it is not required for an interactive development machine where the rootless Docker service may stop with the user session.

The default deployment publishes nginx on host port `8443`. Binding rootless Docker directly to host port `443` requires separate host preparation according to Docker's rootless privileged-port guidance. Keep `8443` until that is configured deliberately.

## 2. Build and install the host daemon

Build the NativeAOT daemon:

```bash
./deploy/systemd/publish.sh linux-x64
```

Use `linux-arm64` on ARM64. Smoke-test the exported binary on the target host:

```bash
artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd --help
```

Install the daemon with the rootless Docker user's primary host group as the IPC group:

```bash
IPC_GROUP="$(id -gn)"
sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd \
  --ipc-group "$IPC_GROUP"
```

The installer creates `/var/lib/ufw-webui/ipc` as `root:<your-primary-group>` mode `0750`; the daemon socket is created mode `0660` with the same group.

Do not relocate this socket below `/run`. RootlessKit commonly gives rootless `dockerd` a private/copied-up `/run`, so sockets created later in the host mount namespace may not appear inside rootless bind mounts. `/var/lib/ufw-webui/ipc` is the supported host path.

Verify the result:

```bash
systemctl status ufw-systemd.service
sudo stat -c '%A %U:%G %n' \
  /var/lib/ufw-webui/ipc \
  /var/lib/ufw-webui/ipc/ufw-systemd.sock
```

## 3. Provision an administrator mutation key

Generate the browser/admin P-256 keypair:

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

Keep the private key with the administrator/password manager. It is not a container secret and must not be copied into the daemon.

## 4. Provision ASP and nginx private keys

Create user-owned directories for the JWT and browser-facing TLS material:

```bash
install -d -m 0700 "$HOME/ufw-webui/secrets"
install -d -m 0750 "$HOME/ufw-webui/tls"

umask 077
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 \
  -out "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
```

Place the browser certificate and key at:

```text
$HOME/ufw-webui/tls/tls.crt
$HOME/ufw-webui/tls/tls.key
```

Keep both private keys owned by the rootless Docker user and its primary group:

```bash
chgrp "$(id -gn)" \
  "$HOME/ufw-webui/secrets/jwt-signing-key.pem" \
  "$HOME/ufw-webui/tls/tls.crt" \
  "$HOME/ufw-webui/tls/tls.key"
chmod 0640 "$HOME/ufw-webui/secrets/jwt-signing-key.pem"
chmod 0644 "$HOME/ufw-webui/tls/tls.crt"
chmod 0640 "$HOME/ufw-webui/tls/tls.key"
```

Container supplemental GID `0` maps to this host primary group. This does not make ASP or nginx container root and does not grant host-root group membership.

The TLS directory is mounted only into nginx. The JWT key is mounted only into ASP.

## 5. Choose PostgreSQL storage

The default and simplest rootless layout uses the Docker-managed `postgres-data` named volume. Leave:

```text
POSTGRES_DATA_SOURCE=postgres-data
```

If host-level storage placement requires a stable path, create one as the rootless Docker user:

```bash
mkdir -p "$HOME/ufw-webui/postgres-data"
```

Then set an absolute path, for example:

```text
POSTGRES_DATA_SOURCE=/home/<docker-user>/ufw-webui/postgres-data
```

Do not force PostgreSQL to run as container root to make the host files easier to inspect. Under rootless Docker, PostgreSQL's container UID maps to a subordinate host UID. Use logical backups rather than copying a live `PGDATA` directory.

## 6. Configure Compose

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
$EDITOR deploy/docker/.env
```

For rootless Docker, the relevant values are:

```text
UFW_TLS_HOST_DIR=/home/<docker-user>/ufw-webui/tls
UFW_TLS_CONTAINER_GID=0

UFW_IPC_HOST_DIR=/var/lib/ufw-webui/ipc
UFW_IPC_CONTAINER_GID=0

UFW_WEB_JWT_KEY_PATH=/home/<docker-user>/ufw-webui/secrets/jwt-signing-key.pem
UFW_JWT_CONTAINER_GID=0

UFW_BOOTSTRAP_EMAIL=<initial administrator email>
UFW_BOOTSTRAP_USERNAME=<initial administrator username>
UFW_BOOTSTRAP_PASSWORD=<initial administrator password>

POSTGRES_PASSWORD=<strong database password>
POSTGRES_DATA_SOURCE=postgres-data
```

Replace `POSTGRES_DATA_SOURCE` only if you deliberately chose the host bind-mounted variant above.

The bootstrap password is creation-only. After successful first provisioning it may be emptied; bootstrap does not reset an existing password.

See [Deployment configuration](configuration.md) for the complete reference.

## 7. Build and start the application stack

Run Compose as the rootless Docker user:

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

Only nginx should publish a host port. ASP has no TCP listener, and PostgreSQL is reachable only on the internal database network.

## 8. Verify the deployment

Inspect service state and logs:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml ps
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml logs --tail=200 frontend asp postgres
```

Verify the browser endpoint and API proxy:

```bash
curl -kI https://127.0.0.1:8443/
curl -ksS -o /dev/null -w '%{http_code}\n' https://127.0.0.1:8443/api/v1/rules
```

Before login, `/api/v1/rules` should return `401`.

Verify ASP can see the host daemon directory:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml exec asp \
  ls -la /run/ufw-manager
```

If the host socket exists but the container directory is empty, first confirm `UFW_IPC_HOST_DIR=/var/lib/ufw-webui/ipc`. Do not work around the problem by moving the socket into `/run` or making it world-accessible.

Finally sign in through the browser, verify daemon-backed status, reconcile network interfaces, and only then test a harmless signed mutation.
