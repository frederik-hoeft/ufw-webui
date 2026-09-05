# UFW WebUI

UFW WebUI separates network-facing firewall-management concerns from privileged host firewall execution.

The solution contains:

- `Ufw.Client`: Blazor WebAssembly frontend using MudBlazor, with in-memory access-token state and browser-side signed-intent creation;
- `Ufw.Web`: ASP.NET Core REST API with Identity, EF Core/SQLite, JWT access tokens, rotating refresh tokens, API versioning, CORS/Swagger infrastructure, and the local IPC client;
- `Ufw.Systemd`: privileged host daemon responsible for authoritative UFW state, signed mutation authorization, semantic rule handling, and UFW subprocess execution;
- `Ufw.Ipc.Client` / `Ufw.Ipc.Shared`: local IPC client, protocol models, serialization, transport security, and shared signed-intent/rule semantics;
- `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen`: source-generated routing support used by the daemon-side IPC API.

`Ufw.Client` consumes the versioned REST API. Its global stylesheet is maintained as `src/Ufw.Client/Styles/app.scss` and compiled to `src/Ufw.Client/wwwroot/css/app.css` during build/publish by `AspNetCore.SassCompiler`; the generated CSS is not committed. Access JWTs remain in memory, refresh-token cookies remain inaccessible to application JavaScript, and firewall mutations are signed in the browser with the shared v2 intent contract. Refresh-cookie mutations are serialized across same-origin tabs with the browser Web Locks API, so the client must run in a secure browser context. Because the refresh cookie is `Secure` and `SameSite=Strict` while Web Locks are origin-scoped, production deployments must serve the client over HTTPS, keep it same-site with the API, and use one consistent client origin for tabs that share the API refresh cookie. The initial signing UX asks for an unencrypted PKCS#8 P-256 private key for each mutation and does not persist it.

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

The default web database is SQLite and EF Core migrations are applied at startup. `Ufw.Web` and `Ufw.Systemd` must be configured for the same local IPC endpoint; their default Linux paths use the conventional `/run`/`/var/run` runtime directory. `Ufw.Client/wwwroot/appsettings.json` configures the REST API base URL. The client requires an absolute HTTPS base URL and normalizes a missing trailing slash before constructing versioned API paths. Development CORS is configured for the HTTPS client profile at `https://localhost:7298`.

No public user-registration endpoint is provided. User provisioning and administrative user-management APIs are separate from the authentication foundation.

## Firewall mutations

`GET /api/v1/rules` and `GET /api/v1/intent/context` are unsigned at the mutation-protocol layer. Add and Delete require a signed intent that `Ufw.Systemd` verifies against its daemon-local authorized-key set.

Generate a P-256 signing keypair for development with:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out intent-key.pem
openssl pkey -in intent-key.pem -pubout -out intent-key.pub.pem
```

Keep the private key with the signing client and place only the public-key PEM in the daemon's `security.authorized_keys_path` file. The daemon also persists replay state and a stable deployment identifier under its configured `security` paths. See [the signed-intent protocol](docs/protocols/signed-intent.md) for the exact v2 contract.

## IPC TLS and mTLS

IPC uses a local named pipe/Unix-domain socket. TLS is optional defense in depth and is configured independently from mutation authorization.

On `Ufw.Web`, `IpcOptions:TlsEnabled=true` enables TLS and requires `IpcOptions:TlsServerName` for server-certificate identity validation. `IpcOptions:SslProtocols=None` keeps .NET's automatic protocol-selection behavior; an explicit protocol set may be configured when required. `ClientCertificatePath` and `ClientCertificateKeyPath` optionally configure the client certificate used for mTLS.

On `Ufw.Systemd`, `pipe.tls_enabled=true` requires `server_certificate_path` and `server_certificate_key_path`. `pipe.ssl_protocols=none` likewise delegates protocol selection to .NET/the OS. Configuring `pipe.remote_certificate_validation` enables client-certificate validation and therefore requires mTLS; omitting it keeps client certificates optional/not required.

mTLS authenticates the IPC peer, not the end user's firewall intent. Signed-intent verification remains mandatory for every mutation regardless of transport mode.
