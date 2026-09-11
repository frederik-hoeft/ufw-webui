# Local development

UFW WebUI can be developed without granting the development process firewall privileges. The normal local setup runs the browser client, ASP.NET Core application, PostgreSQL, and daemon as separate processes, while the daemon can execute either the real `ufw` binary or the platform-neutral [UFW mock](ufw-mock.md).

The development topology deliberately resembles production: the browser still talks to the web API, the web API still uses the IPC protocol to reach the daemon, and firewall mutations are still signed by a browser-held key and verified by the daemon. Development configuration changes endpoints and credentials; it does not introduce a separate application path.

## Prerequisites

Development requires:

- the .NET 10 SDK;
- Docker for the local PostgreSQL helper;
- Bash and OpenSSL for the development setup script;
- a browser;
- either UFW on the development host or `Ufw.Mock` for firewall-command emulation.

On Windows, run `scripts/setup-dev.sh` from Git Bash. The script handles Windows-native paths and uses a named pipe for the daemon IPC endpoint. Building `Ufw.Mock` before running the setup script lets it discover the mock automatically when no `ufw` executable exists.

## Build and test

The solution lives under `src`:

```bash
cd src
dotnet restore Ufw.slnx
dotnet build Ufw.slnx --no-restore
dotnet test Ufw.slnx --no-restore --no-build
```

`src/restore.sh` is the repository convenience wrapper for a normal online restore. Contributors working from an offline handoff should use the supplied SDK/package-cache workflow instead of changing project dependencies or package sources to accommodate the local environment.

## Prepare the development environment

On Windows, or whenever the daemon should use the mock, build it first:

```bash
dotnet build src/Ufw.Mock/Ufw.Mock.csproj
```

Start the development PostgreSQL instance:

```bash
docker compose up -d postgres
```

Generate the local CA, IPC TLS certificates, JWT signing key, browser mutation-signing key, daemon authorized-key file, and application configuration:

```bash
./scripts/setup-dev.sh --install-ca
```

The script writes generated material below `artifacts/dev` and creates local `appsettings.json` files for `Ufw.Systemd` and `Ufw.Web`. These files are development state, not source-controlled configuration. Re-running the script requires `--force` if generated material already exists.

If installing the development CA automatically is undesirable, omit `--install-ca` and trust the generated CA manually before starting the web application and daemon. IPC development uses normal certificate-chain validation; an untrusted development CA causes the TLS/mTLS handshake to fail.

To select a specific UFW-compatible executable, set `UFW_PATH` before running the setup script. For example:

```bash
UFW_PATH=/path/to/Ufw.Mock ./scripts/setup-dev.sh --force --install-ca
```

See [UFW mock](ufw-mock.md) for its persistence and compatibility behavior.

## Run the stack

Run the three application processes in separate terminals after PostgreSQL is healthy and the development CA is trusted:

```bash
dotnet run --project src/Ufw.Systemd --no-launch-profile -- serve --config src/Ufw.Systemd/appsettings.json
```

```bash
dotnet run --project src/Ufw.Web
```

```bash
dotnet run --project src/Ufw.Client
```

The default launch profiles expose the web API at `https://localhost:7259` and the Blazor client at `https://localhost:7298`. The browser client talks to the API using its development configuration; the web application reaches the daemon through the generated local IPC endpoint.

For a firewall mutation, use the generated intent-key data URI from `artifacts/dev/intent/intent-key.data-uri.txt` in the client's mutation authorization field. The private key is supplied to the browser for the individual signing operation; the corresponding public key is already present in the generated daemon `authorized_keys` file.

## Generated credentials and state

`scripts/setup-dev.sh` creates distinct credentials for distinct trust boundaries:

- a local CA plus daemon server and web-client certificates for IPC TLS/mTLS;
- an EC key used by the web application to sign access JWTs;
- a browser mutation-signing keypair, with only the public key authorized by the daemon;
- daemon replay/deployment state under the generated development state directory.

These keys are intentionally separate. Reusing one key across JWT signing, browser mutation authorization, or TLS would blur independent security boundaries that are separate in production as well.

The development PostgreSQL container stores its data in the `postgres-data` Docker volume. Resetting that volume resets ASP-owned application data such as users, refresh-token state, and interface metadata; it does not affect UFW or `Ufw.Mock` state.

## Where to look next

The [architecture overview](../architecture/architecture-overview.md) explains how the client, web application, daemon, and UFW boundary relate. The [protocol documentation](../protocols/README.md) covers IPC and signed mutation contracts. Production configuration and secret ownership are documented separately in [deployment configuration](../deployment/configuration.md).
