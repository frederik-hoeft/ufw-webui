# Deployment Configuration

This document is a reference for runtime configuration and secrets. Use either the [rootful](rootful-docker.md) or [rootless](rootless-docker.md) runbook for installation order and file ownership; the same setting can require different host permissions in the two modes.

## Configuration sources

### Ufw.Web

`Ufw.Web` intentionally has a small configuration source chain:

1. optional `src/Ufw.Web/appsettings.json` relative to the application content root;
2. environment variables;
3. command-line arguments.

`src/Ufw.Web/appsettings.default.json` is a committed template/reference and is not loaded as a second runtime layer. Environment-specific `appsettings.{Environment}.json` files and ASP.NET Core user secrets are not part of the application configuration model.

The local `appsettings.json` file is gitignored and excluded from publish output. Container deployments normally use environment variables instead.

### Ufw.Systemd

The daemon `serve` command loads one explicit JSON settings file. The systemd installer uses `/etc/ufw-manager/settings.json` and seeds it from `deploy/systemd/settings.json.example` on first install.

The installer preserves an existing settings file on update unless `--settings PATH` is supplied explicitly.

### Ufw.Client

Production `Ufw.Client` uses a same-origin API base (`/`). The development settings override this with the standalone local Web API URL. Culture configuration is also shipped with the static client assets.

Do not place secrets in client configuration: browser configuration is public by definition.

## Compose environment

The production Compose stack reads `deploy/docker/.env`. Keep this file out of source control and mode `0600`.

### Images and browser endpoint

| Variable | Purpose | Default |
| --- | --- | --- |
| `UFW_FRONTEND_IMAGE` | nginx/frontend image tag | `ufw-webui-frontend:local` |
| `UFW_ASP_IMAGE` | ASP image tag | `ufw-webui-asp:local` |
| `UFW_HTTPS_BIND_ADDRESS` | host address publishing nginx | `0.0.0.0` |
| `UFW_HTTPS_PORT` | host HTTPS port | `8443` |
| `UFW_TLS_HOST_DIR` | host directory containing `tls.crt` and `tls.key` | required |
| `UFW_TLS_CONTAINER_GID` | supplemental nginx group used to read host TLS key | rootless: `0`; rootful: TLS host-group GID |

nginx listens on unprivileged container port `8443`. A rootless host that should publish port `443` directly requires separate privileged-port preparation.

### Host daemon IPC

| Variable | Purpose | Required value |
| --- | --- | --- |
| `UFW_IPC_HOST_DIR` | host directory containing daemon Unix socket | `/var/lib/ufw-webui/ipc` |
| `UFW_IPC_CONTAINER_GID` | supplemental ASP group used to connect to the host socket | rootless: `0`; rootful: daemon IPC host-group GID |

Inside ASP the directory is mounted at `/run/ufw-manager`, and `IpcOptions__Endpoint` is `/run/ufw-manager/ufw-systemd.sock`.

The host path must remain separate from `/var/lib/ufw-manager`, which contains root-only daemon security state.

### ASP JWT signing

| Variable | Purpose | Notes |
| --- | --- | --- |
| `UFW_WEB_JWT_KEY_PATH` | host P-256 PKCS#8 private key mounted into ASP | required |
| `UFW_JWT_CONTAINER_GID` | supplemental ASP group used to read the key | rootless: `0`; rootful: dedicated JWT host-group GID |
| `UFW_JWT_ISSUER` | JWT issuer | `ufw-webui` |
| `UFW_JWT_AUDIENCE` | JWT audience | `ufw-webui-client` |

The JWT key is an ASP secret and is unrelated to the browser mutation-signing key.

### Bootstrap account and PostgreSQL

| Variable | Purpose |
| --- | --- |
| `UFW_BOOTSTRAP_EMAIL` | email used to identify or create the bootstrap account |
| `UFW_BOOTSTRAP_USERNAME` | username assigned when creating the account |
| `UFW_BOOTSTRAP_PASSWORD` | creation-only password for a missing account |
| `POSTGRES_DB` | database name |
| `POSTGRES_USER` | database user |
| `POSTGRES_PASSWORD` | database password |
| `POSTGRES_DATA_SOURCE` | named volume or absolute host bind path for PostgreSQL data |

Bootstrap is intentionally non-destructive. Existing passwords are never reset from configuration, and removing an account from bootstrap configuration does not delete it. An explicitly configured username must match an existing account; `EmailConfirmed` is reconciled to the configured value. After initial creation, the password may be removed.

`Ufw.Web` applies EF Core migrations at startup before bootstrap runs.

## Web authentication settings

The `Auth` section in `Ufw.Web` controls Identity, JWT, refresh-token, and bootstrap policy. Important production settings include:

- `Auth:Jwt:SigningKeyPath`: P-256 PKCS#8 PEM path visible inside ASP;
- `Auth:Jwt:AccessTokenLifetime`: short-lived access-token duration;
- `Auth:Jwt:ClockSkew`: JWT validation skew;
- `Auth:RefreshToken:CookieName`: must retain the `__Host-` prefix;
- `Auth:RefreshToken:Lifetime`: refresh-token family lifetime;
- `Auth:Identity`: normal ASP.NET Core Identity password, lockout, user, and sign-in policy;
- `Auth:Bootstrap:Users`: optional initial account definitions.

`appsettings.default.json` is a local-development template and intentionally contains permissive bootstrap credentials. Production Compose supplies bootstrap values through its environment instead; do not reuse the template credentials in production.

Refresh cookies are `Secure`, `HttpOnly`, `SameSite=Strict`, and path `/`. Production therefore requires HTTPS and a consistent same-site browser/API deployment.

## Daemon settings

The production template is `deploy/systemd/settings.json.example`.

### Process and diagnostics

| Setting | Purpose | Production example |
| --- | --- | --- |
| `debug_mode` | include daemon diagnostic detail where supported | `false` |
| `ufw_path` | UFW executable | `/usr/sbin/ufw` |
| `write_to_console` | enable console logging for systemd capture | `true` |

### Pipe and stream security

| Setting | Purpose |
| --- | --- |
| `pipe.pipe_name` | local endpoint; production default `/var/lib/ufw-webui/ipc/ufw-systemd.sock` |
| `pipe.tls_enabled` | wrap the local stream in TLS |
| `pipe.ssl_protocols` | protocol selection; `none` delegates selection to .NET/OS |
| `pipe.server_certificate_path` | server certificate path when TLS is enabled |
| `pipe.server_certificate_key_path` | matching server private key |
| `pipe.remote_certificate_validation` | optional client-certificate validation policy; enables mTLS when configured |

Production Compose defaults to TLS disabled because the endpoint is a host Unix socket with group-restricted access. TLS/mTLS is defense in depth and does not replace signed mutation authorization.

When TLS is enabled, `Ufw.Web` must use matching `IpcOptions` values for server name, protocol policy, and optional client certificate.

### Network policy

| Setting | Purpose | Example |
| --- | --- | --- |
| `network.max_connections` | maximum concurrent accepted peer connections | `8` |
| `network.io_timeout` | idle timeout for an individual stream I/O operation | `00:00:30` |
| `network.request_timeout` | overall daemon-side request deadline | `00:30:00` |

These values are connection policy, not fields in the IPC wire protocol.

### Mutation security state

| Setting | Purpose | Production example |
| --- | --- | --- |
| `security.authorized_keys_path` | operator-managed P-256 public keys | `/etc/ufw-manager/authorized_keys` |
| `security.nonce_store_path` | durable replay records | `/var/lib/ufw-manager/intent-nonces` |
| `security.deployment_id_path` | stable deployment identity | `/var/lib/ufw-manager/deployment-id` |
| `security.reorder_recovery_journal_path` | durable recovery record for an interrupted reorder move | `/var/lib/ufw-manager/reorder-recovery.json` |
| `security.max_intent_age` | maximum accepted intent age | `00:05:00` |
| `security.clock_skew` | tolerated clock skew | `00:00:30` |

Private administrator mutation keys never belong in daemon configuration. The reorder recovery journal is daemon-owned safety state rather than a firewall database; an outstanding record must be reconciled before later firewall mutations are allowed to proceed.

## Secret ownership

A production deployment has three independent private-key classes:

| Secret | Used by | Must not be exposed to |
| --- | --- | --- |
| administrator mutation private key | browser/admin signing workflow | nginx, ASP, PostgreSQL, daemon |
| ASP JWT signing private key | `Ufw.Web` | nginx, PostgreSQL, daemon |
| browser-facing TLS private key | nginx | ASP, PostgreSQL, daemon |

The daemon stores only administrator **public** keys. Keep these key roles separate even when all keys use P-256.

Also keep `deploy/docker/.env`, database dumps, and local development credentials out of source control.
