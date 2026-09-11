# Rootful Docker Deployment

This runbook is for a normal system-wide Docker daemon running as root. Application containers still run as non-root users. Access to host-mounted secrets and the daemon socket is granted with dedicated host groups whose numeric GIDs are passed into Compose.

Do not use the rootless GID-0 mapping rules in this deployment mode.

## 1. Build and install the host daemon

Build the NativeAOT daemon from the repository root:

```bash
./deploy/systemd/publish.sh linux-x64
```

Use `linux-arm64` on ARM64. Cross-architecture builds require a Buildx builder capable of executing the target platform.

Smoke-test the exported binary on the target host, then install it with the default dedicated IPC group:

```bash
artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd --help

sudo ./deploy/systemd/install.sh \
  --binary artifacts/publish/ufw-systemd/linux-x64/Ufw.Systemd
```

The installer creates the `ufw-webui-ipc` system group when necessary, stores daemon configuration under `/etc/ufw-manager`, keeps private daemon state under `/var/lib/ufw-manager`, and creates `/var/lib/ufw-webui/ipc` as the group-restricted ASP-facing socket directory.

Resolve the IPC group GID for Compose:

```bash
getent group ufw-webui-ipc
```

The socket directory should be `root:ufw-webui-ipc` mode `0750`; the daemon-created socket should be mode `0660`.

```bash
systemctl status ufw-systemd.service
sudo stat -c '%A %U:%G %n' \
  /var/lib/ufw-webui/ipc \
  /var/lib/ufw-webui/ipc/ufw-systemd.sock
```

## 2. Provision an administrator mutation key

Generate a P-256 signing key outside the container stack:

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

Keep the private key with the administrator. It must not be mounted into nginx, ASP, PostgreSQL, or the daemon.

The authorized-key file may contain multiple P-256 public-key PEM blocks and comments. See [Signed mutation intent v2](../protocols/signed-intent.md) for the authorization contract.

## 3. Provision the ASP JWT signing key

Create a separate P-256 PKCS#8 key for JWT signing:

```bash
sudo install -d -o root -g root -m 0750 /etc/ufw-webui
sudo install -d -o root -g root -m 0750 /etc/ufw-webui/secrets
sudo openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 \
  -out /etc/ufw-webui/secrets/jwt-signing-key.pem
```

Create a dedicated group for this key and grant group-read access:

```bash
sudo groupadd --system ufw-webui-jwt 2>/dev/null || true
sudo chown root:ufw-webui-jwt /etc/ufw-webui/secrets/jwt-signing-key.pem
sudo chmod 0640 /etc/ufw-webui/secrets/jwt-signing-key.pem
getent group ufw-webui-jwt
```

Record the numeric GID as `UFW_JWT_CONTAINER_GID`. Do not reuse the daemon IPC group: permission to connect to the daemon should not imply permission to read ASP's JWT signing key.

## 4. Provision browser-facing TLS

Create a directory containing the exact filenames expected by nginx:

```text
/etc/ufw-webui/tls/tls.crt
/etc/ufw-webui/tls/tls.key
```

Use a dedicated host group for the private key:

```bash
sudo groupadd --system ufw-webui-tls 2>/dev/null || true
sudo chown -R root:ufw-webui-tls /etc/ufw-webui/tls
sudo chmod 0750 /etc/ufw-webui/tls
sudo chmod 0644 /etc/ufw-webui/tls/tls.crt
sudo chmod 0640 /etc/ufw-webui/tls/tls.key
getent group ufw-webui-tls
```

Record that numeric GID as `UFW_TLS_CONTAINER_GID`. The TLS directory is mounted only into nginx.

## 5. Configure Compose

Create the private environment file:

```bash
cp deploy/docker/.env.example deploy/docker/.env
chmod 0600 deploy/docker/.env
$EDITOR deploy/docker/.env
```

For rootful Docker, set at least:

```text
UFW_TLS_HOST_DIR=/etc/ufw-webui/tls
UFW_TLS_CONTAINER_GID=<gid of ufw-webui-tls>

UFW_IPC_HOST_DIR=/var/lib/ufw-webui/ipc
UFW_IPC_CONTAINER_GID=<gid of ufw-webui-ipc>

UFW_WEB_JWT_KEY_PATH=/etc/ufw-webui/secrets/jwt-signing-key.pem
UFW_JWT_CONTAINER_GID=<gid of ufw-webui-jwt>

UFW_BOOTSTRAP_EMAIL=<initial administrator email>
UFW_BOOTSTRAP_USERNAME=<initial administrator username>
UFW_BOOTSTRAP_PASSWORD=<initial administrator password>

POSTGRES_PASSWORD=<strong database password>
POSTGRES_DATA_SOURCE=postgres-data
```

Leave `POSTGRES_DATA_SOURCE=postgres-data` unless you have a deliberate storage design. The supported default is a Docker-managed named volume.

The bootstrap password is creation-only. After the account has been created successfully, it may be removed/emptied from the environment file; bootstrap does not reset an existing user's password.

See [Deployment configuration](configuration.md) for all available values.

## 6. Build and start the application stack

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

Only nginx should publish a host port. ASP communicates with nginx through the shared `api-socket` volume and with PostgreSQL through the internal `database` network.

Rootful Docker can create host firewall rules outside UFW when publishing container ports. Keep ASP and PostgreSQL unpublished; review Docker/UFW interaction on the host before exposing additional container ports.

## 7. Verify the deployment

Inspect service state and logs:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml ps
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml logs --tail=200 frontend asp postgres
```

Verify nginx and the API proxy:

```bash
curl -kI https://127.0.0.1:8443/
curl -ksS -o /dev/null -w '%{http_code}\n' https://127.0.0.1:8443/api/v1/rules
```

Before login, the API request should return `401`, proving `/api/*` reached ASP. Use the real hostname and trusted CA rather than `-k` for browser verification.

Confirm ASP has its private Unix socket and no published TCP port:

```bash
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml exec asp \
  sh -c 'test -S /run/ufw-web-api/socket/asp.sock && echo "ASP Unix socket present"'
docker compose --env-file deploy/docker/.env -f deploy/docker/compose.yml port asp 8080
```

The second command should report no published mapping.

Finally sign in through the browser, verify daemon-backed status, reconcile the network-interface inventory, and only then test a harmless signed firewall mutation.
