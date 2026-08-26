# UFW WebUI

UFW WebUI separates network-facing management concerns from privileged host firewall execution.

The solution currently contains:

- `Ufw.Web`: ASP.NET Core REST API infrastructure with ASP.NET Core Identity, EF Core/SQLite, JWT access tokens, rotating refresh tokens, API versioning, Swagger in development, CORS for a future Blazor frontend, and the local IPC client.
- `Ufw.Systemd`: privileged host daemon responsible for the authoritative UFW-facing state and command execution.
- `Ufw.Ipc.Client` / `Ufw.Ipc.Shared`: local IPC client, protocol models, serialization, and transport abstractions shared with the daemon.
- `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen`: source-generated routing support used by the daemon-side IPC API.

The browser UI is intentionally absent from this stage. Rule-management REST controllers are also out of scope until the signed mutation protocol is implemented end to end.

## Architecture

See [docs/architecture.md](docs/architecture.md) for component responsibilities and data flow, and [security/architecture-baseline.md](security/architecture-baseline.md) for the security boundaries and mutation invariants.

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
