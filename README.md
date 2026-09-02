# UFW WebUI

UFW WebUI separates network-facing management concerns from privileged host firewall execution.

The solution currently contains:

- `Ufw.Web`: ASP.NET Core REST API infrastructure with ASP.NET Core Identity, EF Core/SQLite, JWT access tokens, rotating refresh tokens, API versioning, Swagger in development, CORS for a future Blazor frontend, and the local IPC client.
- `Ufw.Systemd`: privileged host daemon responsible for the authoritative UFW-facing state and command execution.
- `Ufw.Ipc.Client` / `Ufw.Ipc.Shared`: local IPC client, protocol models, serialization, and transport abstractions shared with the daemon.
- `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen`: source-generated routing support used by the daemon-side IPC API.

The browser UI is intentionally absent from this stage. Signed `AddRule` / `DeleteRule` flows and unsigned rule listing are implemented through `Ufw.Web` and `Ufw.Systemd`. A future Blazor client is expected to create the in-browser signatures.

## Architecture

See [docs/architecture.md](docs/architecture.md) for component responsibilities and data flow, [docs/protocols/README.md](docs/protocols/README.md) for the IPC transport (ITP) and application protocols, and [security/architecture-baseline.md](security/architecture-baseline.md) for the security boundaries and mutation invariants.

## Development setup

The solution targets .NET 10.

```bash
dotnet restore src/Ufw.sln
dotnet build src/Ufw.sln
dotnet test src/Ufw.sln
```

`Ufw.Web` requires an RSA private key in PEM format for JWT signing. Configure its path through user secrets or another non-repository configuration source:

```bash
dotnet user-secrets --project src/Ufw.Web set "Auth:Jwt:SigningKeyPath" "/path/to/jwt-signing-key.pem"
```

The default web database is SQLite. The web API applies EF Core migrations at startup. The default IPC endpoint is `/run/ufw-systemd.pipe`; development configuration uses `/tmp/ufw-systemd.pipe`.

No public user-registration endpoint is provided. User provisioning and administrative user-management APIs are separate work from the authentication foundation in this stage.

## Firewall mutations

Rule listing is unsigned and JWT-protected. Add and delete require a user-signed intent that the daemon verifies against `/etc/ufw-manager/authorized_keys` (configurable). Generate a P-256 test key with:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out intent-key.pem
openssl ec -in intent-key.pem -pubout -out intent-key.pub.pem
```

Place the public key PEM in the daemon authorized-keys file. Optional mTLS between `Ufw.Web` and `Ufw.Systemd` is configured through `IpcOptions:SslProtocols` plus client certificate paths on the web side, and `pipe.ssl_protocols` plus `remote_certificate_validation` on the daemon.
