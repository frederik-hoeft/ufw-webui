# UFW WebUI coding instructions

## Project model

UFW WebUI is a .NET 10 solution with a Blazor WebAssembly client, a network-facing ASP.NET Core API, and a privileged host daemon.

- `Ufw.Client` is the MudBlazor-based browser frontend. It owns presentation, in-memory HTTP authentication state, and browser-side signed-intent creation.
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
- CORS configuration for the separate `Ufw.Client` frontend

Keep browser UI code in `Ufw.Client`. Do not reintroduce Razor Pages or UI assets into `Ufw.Web`, and do not move privileged host behavior into the browser. Access tokens stay in memory; refresh-token cookies remain `HttpOnly`. Client operations that mutate the rotating refresh-token cookie must use the shared authentication-operation coordinator so concurrent tabs cannot consume the same token family member. The browser client is an HTTPS application and deployments must use one consistent client origin that is same-site with the API; Web Locks cannot serialize refresh-cookie use across different client origins. Mutation private keys must not be persisted by the client unless a later design explicitly introduces a secure key-storage boundary.

Keep expected browser/API failures behind the client error-classification boundary. Do not render arbitrary exception messages in Razor components. Distinguish an absent/expired authentication session from API unavailability or an incompatible response, and preserve the explicit startup failure/retry state instead of converting infrastructure failures into anonymous authentication state.

Use injected `TimeProvider` for client-side token-expiry and signed-intent timestamps rather than reading the wall clock directly. Treat daemon-backed rule state as a freshness-tracked snapshot. Do not infer firewall status or an empty rule set before the first successful read, and do not enable mutations against stale state. If refresh or mutation reconciliation fails, preserve the last confirmed snapshot only as explicitly stale state until a fresh daemon read succeeds.

Keep the browser UI usable with keyboard, assistive technology, and narrow viewports. Routed pages must expose a single semantic `h1`, authenticated layouts must preserve explicit navigation/main landmarks, icon-only actions require accessible labels, and responsive rule-table cells must retain meaningful `DataLabel` text. Use modal dialogs for destructive confirmations so focus stays with the active interaction instead of rendering confirmation UI away from the initiating row.

## Daemon conventions

Daemon IPC controllers use the existing route attributes and source-generated endpoint map. Keep privileged UFW execution and parsing inside the daemon rather than moving host-facing behavior into `Ufw.Web`.

Follow `/code-style.md` for C# style and keep documentation architecture-first and steady-state rather than documenting implementation history.
