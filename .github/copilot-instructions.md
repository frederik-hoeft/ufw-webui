# UFW WebUI coding instructions

## Project model

UFW WebUI is a .NET 10 solution with a Blazor WebAssembly client, a network-facing ASP.NET Core API, and a privileged host daemon.

- `Ufw.Client` is the MudBlazor-based browser frontend. It owns presentation, in-memory HTTP authentication state, rule-authoring interaction, and browser-side signed-intent creation.
- `Ufw.Web` is the REST API. It owns ASP.NET Core Identity, PostgreSQL-backed application state, JWT/refresh-token handling, application authorization, browser-facing metadata, and the local IPC client.
- `Ufw.Systemd` is the privileged daemon and the authority for actual UFW observation and execution.
- `Ufw.Shared` owns cross-process firewall semantics, security primitives, and the `Ufw.Shared.Ipc` protocol/serialization contract.
- `Ufw.Ipc.Client` implements the typed local IPC client on top of `Ufw.Shared.Ipc`.
- `Ufw.Roslyn` and `Ufw.Roslyn.SourceGen` provide the runtime/source-generator boundary for daemon routing and serialization.

Read [the architecture overview](../docs/architecture/architecture-overview.md) before changing subsystem boundaries and [the security architecture](../docs/architecture/security.md) before working on authentication, authorization, IPC security, or firewall mutations.

## Security constraints

Treat `Ufw.Web` as untrusted relative to the daemon for privileged mutation authority. HTTP authentication, IPC reachability, and optional mTLS peer authentication are not sufficient proof for a firewall change.

Mutating daemon endpoints must use the deployment-scoped signed-intent boundary. `Ufw.Systemd` verifies an authorized ECDSA P-256 signature, deployment identity, freshness, durable replay state, and the complete operation-specific mutation semantics before UFW execution. Append, ordered insertion, delete, and reorder all use this mechanism; reuse the shared intent protocol rather than creating endpoint-specific authorization conventions. See [signed mutation intent v2](../docs/protocols/signed-intent.md).

The daemon-facing transport is local named-pipe/Unix-domain IPC. Do not add a network transport for the privileged daemon.

UFW/daemon state is authoritative. PostgreSQL may store users, refresh-token state, and application-owned authoring metadata, but it must not become a second source of truth for firewall rules or host state.

## Web API and client conventions

Use controller-based, versioned browser APIs under `Ufw.Web/Api/V{N}`. Keep request/response contracts near the versioned API surface and place reusable application logic behind focused services.

Authentication infrastructure consists of:

- ASP.NET Core Identity with EF Core/PostgreSQL;
- ES256 access tokens signed with a P-256 application key;
- opaque rotating refresh tokens stored as hashes in PostgreSQL and delivered through a `Secure`, `HttpOnly`, `SameSite=Strict` cookie;
- CORS only where the development client and API run on separate origins. Production is same-origin behind nginx.

`Ufw.Web` configuration has one optional local JSON source: the gitignored `src/Ufw.Web/appsettings.json`. Keep `src/Ufw.Web/appsettings.default.json` as the committed template/reference, and use environment variables or command-line overrides for deployment. Do not reintroduce environment-specific appsettings files or user-secrets configuration.

Bootstrap users are initial provisioning only. Keep `Auth:Bootstrap:Users` idempotent and non-destructive: create missing accounts through ASP.NET Core Identity, never reset an existing password from configuration, and never delete users merely because they disappear from bootstrap configuration. Standard ASP.NET Core configuration providers, including Docker environment variables, must remain sufficient to drive bootstrap.

Keep browser UI code in `Ufw.Client`. Maintain global client styles in `src/Ufw.Client/Styles/app.scss`; `src/Ufw.Client/wwwroot/css/app.css` is generated during build/publish and must not be edited or committed. Do not reintroduce Razor Pages or UI assets into `Ufw.Web`, and do not move privileged host behavior into the browser.

Access tokens stay in memory; refresh-token cookies remain `HttpOnly`. Client operations that mutate the rotating refresh-token cookie must use the shared authentication-operation coordinator so concurrent tabs cannot consume the same token family member. Production uses one HTTPS origin for browser and API traffic; development may use the configured CORS origin because the processes run separately.

Mutation private keys must not be persisted by the client unless a later design explicitly introduces a secure key-storage boundary. Persisting non-sensitive UI preferences such as appearance or culture is allowed, but browser storage must not become a general-purpose authentication or secret store.

Keep expected browser/API failures behind the client error-classification boundary. Do not render arbitrary exception messages in Razor components. Distinguish an absent/expired authentication session from API unavailability or an incompatible response, and preserve explicit startup/freshness failure states instead of converting infrastructure failures into anonymous authentication state.

Use injected `TimeProvider` for client-side token-expiry and signed-intent timestamps rather than reading the wall clock directly. Treat daemon-backed rule state as a freshness-tracked authoritative snapshot. Do not infer firewall status or an empty rule set before the first successful read, and do not enable mutations against stale state. If refresh or mutation reconciliation fails, preserve the last confirmed snapshot only as explicitly stale state until a fresh daemon read succeeds.

Keep the browser UI usable with keyboard, assistive technology, and narrow viewports. Routed pages must expose a single semantic `h1`, authenticated layouts must preserve explicit navigation/main landmarks, icon-only actions require accessible labels, and responsive rule-table cells must retain meaningful `DataLabel` text. Use modal dialogs for destructive confirmations so focus stays with the active interaction instead of rendering confirmation UI away from the initiating row.

## Daemon conventions

Daemon IPC controllers use the existing route attributes and source-generated endpoint map. Keep privileged UFW execution, host inventory, authoritative rule parsing, signed-intent verification, and mutation reconciliation inside the daemon rather than moving host-facing behavior into `Ufw.Web`.

All daemon-managed UFW reads and mutations share the execution gate. Keep state-conditioned operations conservative: exact-snapshot insertion/reorder must fail rather than reinterpret stale occurrence coordinates, and any reorder delete/reinsert obligation must be recovered or left as a durable fail-closed condition before later mutations proceed.

Follow [`code-style.md`](../code-style.md) for C# style. Permanent documentation should describe steady-state architecture and behavior; temporary implementation sequencing belongs only under `docs/internal` while it remains active.
