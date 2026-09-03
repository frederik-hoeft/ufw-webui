# UFW WebUI coding instructions

## Project model

UFW WebUI is a .NET 10 solution with a network-facing ASP.NET Core API and a privileged host daemon.

- `Ufw.Web` is the REST API. It owns ASP.NET Core Identity, EF Core application state, JWT/refresh-token handling, application authorization, browser-facing data models, and the local IPC client.
- `Ufw.Systemd` is the privileged daemon and the authority for actual UFW state.
- `Ufw.Ipc.Client` and `Ufw.Ipc.Shared` implement the typed local IPC protocol.
- `Ufw.Roslyn` and `Ufw.Roslyn.SourceGen` support daemon-side source-generated routing.

Read [docs/architecture.md](../docs/architecture.md) before changing subsystem boundaries and [security/architecture-baseline.md](../security/architecture-baseline.md) before working on authentication, authorization, IPC, or firewall mutations.

## Security constraints

Treat `Ufw.Web` as untrusted relative to the daemon for privileged mutation authority. HTTP authentication, IPC reachability, and optional mTLS peer authentication are not sufficient proof for a firewall change.

Mutating daemon endpoints must use the existing deployment-scoped signed-intent boundary: `Ufw.Systemd` verifies an authorized ECDSA P-256 signature, freshness, durable replay state, and the complete normalized mutation semantics before UFW execution. Reuse the shared intent protocol rather than creating endpoint-specific authorization conventions. See [docs/protocols/signed-intent.md](../docs/protocols/signed-intent.md).

The daemon-facing transport is local named-pipe/Unix-domain IPC. Do not add a network transport for the privileged daemon.

UFW/daemon state is authoritative. The web database may store richer metadata keyed to stable daemon-visible identifiers, but it must not become a second source of truth for firewall state.

## Web API conventions

Use controller-based, versioned APIs under `Ufw.Web/Api/V{N}`. Keep request/response contracts near the versioned API surface and place reusable application logic behind services.

Authentication infrastructure consists of:

- ASP.NET Core Identity with EF Core/SQLite
- RSA-signed JWT bearer access tokens
- opaque rotating refresh tokens stored as hashes in the database and delivered through a secure `HttpOnly` cookie
- CORS configuration intended for a future Blazor frontend

Do not reintroduce Razor Pages or UI assets into `Ufw.Web`; UI implementation is outside this repository.

## Daemon conventions

Daemon IPC controllers use the existing route attributes and source-generated endpoint map. Keep privileged UFW execution and parsing inside the daemon rather than moving host-facing behavior into `Ufw.Web`.

Follow `/code-style.md` for C# style and keep documentation architecture-first and steady-state rather than documenting implementation history.
