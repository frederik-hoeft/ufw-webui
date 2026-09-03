# UFW WebUI

UFW WebUI separates network-facing firewall-management concerns from privileged host firewall execution.

The solution contains:

- `Ufw.Web`: ASP.NET Core REST API with Identity, EF Core/SQLite, JWT access tokens, rotating refresh tokens, API versioning, CORS/Swagger infrastructure, and the local IPC client;
- `Ufw.Systemd`: privileged host daemon responsible for authoritative UFW state, signed mutation authorization, semantic rule handling, and UFW subprocess execution;
- `Ufw.Ipc.Client` / `Ufw.Ipc.Shared`: local IPC client, protocol models, serialization, transport security, and shared signed-intent/rule semantics;
- `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen`: source-generated routing support used by the daemon-side IPC API.

The browser UI is not part of this repository yet. The backend implements unsigned rule/context reads plus signed AddRule and DeleteRule flows. A future Blazor WebAssembly client can use the shared intent contract to create signatures in-browser.

## Architecture

See [docs/architecture.md](docs/architecture.md) for component responsibilities and request/data flow, [docs/protocols/README.md](docs/protocols/README.md) for IPC protocol boundaries, [docs/protocols/signed-intent.md](docs/protocols/signed-intent.md) for the mutation wire/signing contract, and [security/architecture-baseline.md](security/architecture-baseline.md) for trust boundaries and security invariants.

## Development setup

The solution targets .NET 10.

```bash
dotnet restore src/Ufw.slnx
dotnet build src/Ufw.slnx
dotnet test src/Ufw.slnx
```

`Ufw.Web` requires an RSA private key in PEM format for JWT signing. Configure its path through user secrets or another non-repository configuration source:

```bash
dotnet user-secrets --project src/Ufw.Web set "Auth:Jwt:SigningKeyPath" "/path/to/jwt-signing-key.pem"
```

The default web database is SQLite and EF Core migrations are applied at startup. `Ufw.Web` and `Ufw.Systemd` must be configured for the same local IPC endpoint; their default Linux paths use the conventional `/run`/`/var/run` runtime directory.

No public user-registration endpoint is provided. User provisioning and administrative user-management APIs are separate from the authentication foundation.

## Firewall mutations

`GET /api/v1/rules` and `GET /api/v1/intent/context` are unsigned at the mutation-protocol layer. Add and Delete require a signed intent that `Ufw.Systemd` verifies against its daemon-local authorized-key set.

Generate a P-256 signing keypair for development with:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out intent-key.pem
openssl ec -in intent-key.pem -pubout -out intent-key.pub.pem
```

Keep the private key with the signing client and place only the public-key PEM in the daemon's `security.authorized_keys_path` file. The daemon also persists replay state and a stable deployment identifier under its configured `security` paths. See [the signed-intent protocol](docs/protocols/signed-intent.md) for the exact v2 contract.

## IPC TLS and mTLS

IPC uses a local named pipe/Unix-domain socket. TLS is optional defense in depth and is configured independently from mutation authorization.

On `Ufw.Web`, `IpcOptions:TlsEnabled=true` enables TLS and requires `IpcOptions:TlsServerName` for server-certificate identity validation. `IpcOptions:SslProtocols=None` keeps .NET's automatic protocol-selection behavior; an explicit protocol set may be configured when required. `ClientCertificatePath` and `ClientCertificateKeyPath` optionally configure the client certificate used for mTLS.

On `Ufw.Systemd`, `pipe.tls_enabled=true` requires `server_certificate_path` and `server_certificate_key_path`. `pipe.ssl_protocols=none` likewise delegates protocol selection to .NET/the OS. Configuring `pipe.remote_certificate_validation` enables client-certificate validation and therefore requires mTLS; omitting it keeps client certificates optional/not required.

mTLS authenticates the IPC peer, not the end user's firewall intent. Signed-intent verification remains mandatory for every mutation regardless of transport mode.
